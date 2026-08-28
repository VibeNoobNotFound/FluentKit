using Microsoft.JSInterop;

namespace FluentKit.Interop;

/// <summary>
/// Owns one component/service JS module and serializes its calls with disposal. A server circuit can
/// disappear while an invocation is in flight, so all expected disconnect handling lives here rather
/// than being duplicated (and accidentally omitted) by individual controls.
/// </summary>
internal sealed class JsModuleLifetime : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly string _modulePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();
    private IJSObjectReference? _module;
    private int _disposed;

    public JsModuleLifetime(IJSRuntime js, string modulePath)
    {
        _js = js;
        _modulePath = modulePath;
    }

    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    public CancellationToken DisposalToken => _disposeCts.Token;

    // Internal test hook. Production code only obtains the module through EnsureModuleAsync.
    internal IJSObjectReference? Module
    {
        get => _module;
        set => _module = value;
    }

    public async ValueTask<bool> EnsureModuleAsync()
    {
        if (IsDisposed)
        {
            return false;
        }

        var entered = false;
        try
        {
            await _gate.WaitAsync(_disposeCts.Token).ConfigureAwait(false);
            entered = true;
            if (IsDisposed)
            {
                return false;
            }

            if (_module is not null)
            {
                return true;
            }

            var module = await _js.InvokeAsync<IJSObjectReference>(
                "import", _disposeCts.Token, _modulePath).ConfigureAwait(false);

            if (IsDisposed)
            {
                await JsModuleDisposal.DisposeAsync(module, duringDisposal: true).ConfigureAwait(false);
                return false;
            }

            _module = module;
            return true;
        }
        catch (JSDisconnectedException)
        {
            return false;
        }
        catch (OperationCanceledException) when (IsDisposed || _disposeCts.IsCancellationRequested)
        {
            return false;
        }
        finally
        {
            if (entered)
            {
                _gate.Release();
            }
        }
    }

    public async ValueTask<JsInvocationResult<T>> InvokeAsync<T>(string identifier, params object?[]? args)
    {
        if (IsDisposed)
        {
            return default;
        }

        var entered = false;
        try
        {
            await _gate.WaitAsync(_disposeCts.Token).ConfigureAwait(false);
            entered = true;
            if (IsDisposed)
            {
                return default;
            }

            if (_module is null)
            {
                var module = await _js.InvokeAsync<IJSObjectReference>(
                    "import", _disposeCts.Token, _modulePath).ConfigureAwait(false);
                if (IsDisposed)
                {
                    await JsModuleDisposal.DisposeAsync(module, duringDisposal: true).ConfigureAwait(false);
                    return default;
                }

                _module = module;
            }

            var value = await _module.InvokeAsync<T>(identifier, _disposeCts.Token, args).ConfigureAwait(false);
            return new JsInvocationResult<T>(true, value);
        }
        catch (JSDisconnectedException)
        {
            return default;
        }
        catch (OperationCanceledException) when (IsDisposed || _disposeCts.IsCancellationRequested)
        {
            return default;
        }
        finally
        {
            if (entered)
            {
                _gate.Release();
            }
        }
    }

    public async ValueTask<bool> InvokeVoidAsync(string identifier, params object?[]? args)
    {
        var result = await InvokeAsync<object?>(identifier, args).ConfigureAwait(false);
        return result.Succeeded;
    }

    public async ValueTask DisposeAsync(params (string Identifier, object?[]? Arguments)[] cleanupCalls)
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _disposeCts.Cancel();
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var module = _module;
            _module = null;
            if (module is null)
            {
                return;
            }

            try
            {
                foreach (var cleanup in cleanupCalls)
                {
                    try
                    {
                        await module.InvokeVoidAsync(cleanup.Identifier, cleanup.Arguments).ConfigureAwait(false);
                    }
                    catch (JSDisconnectedException)
                    {
                        // The browser-side resource is already unreachable.
                    }
                    catch (OperationCanceledException)
                    {
                        // Disposal itself is the expected cancellation boundary.
                    }
                }
            }
            finally
            {
                await JsModuleDisposal.DisposeAsync(module, duringDisposal: true).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
            _disposeCts.Dispose();
            _gate.Dispose();
        }
    }

    ValueTask IAsyncDisposable.DisposeAsync() => DisposeAsync([]);
}

internal readonly record struct JsInvocationResult<T>(bool Succeeded, T? Value);
