using Microsoft.JSInterop;

namespace FluentKit.Tests;

internal sealed class TestJsObjectReference : IJSObjectReference
{
    private readonly Dictionary<string, Exception> _invokeExceptions = new();

    public List<string> Calls { get; } = [];

    public List<(string Identifier, object?[]? Arguments)> Invocations { get; } = [];

    public Dictionary<string, object?> Results { get; } = [];

    public Exception? DisposeException { get; set; }

    public bool BlockInvocationsUntilCanceled { get; set; }

    public bool ThrowTaskCanceledExceptionWhenCanceled { get; set; }

    public int DisposeCount { get; private set; }

    public void ThrowOnInvoke(string identifier, Exception exception) => _invokeExceptions[identifier] = exception;

    public void ClearInvokeException(string identifier) => _invokeExceptions.Remove(identifier);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        Calls.Add(identifier);
        Invocations.Add((identifier, args));

        if (BlockInvocationsUntilCanceled && cancellationToken.CanBeCanceled)
        {
            var completion = new TaskCompletionSource<TValue>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (ThrowTaskCanceledExceptionWhenCanceled)
            {
                cancellationToken.Register(() => completion.TrySetException(new TaskCanceledException("JS call canceled")));
            }
            else
            {
                cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
            }
            return new ValueTask<TValue>(completion.Task);
        }

        if (_invokeExceptions.TryGetValue(identifier, out var exception))
        {
            return ValueTask.FromException<TValue>(exception);
        }

        if (Results.TryGetValue(identifier, out var result))
        {
            return ValueTask.FromResult((TValue)result!);
        }

        return ValueTask.FromResult(default(TValue)!);
    }

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        Calls.Add("dispose");
        return DisposeException is null
            ? ValueTask.CompletedTask
            : ValueTask.FromException(DisposeException);
    }
}

internal sealed class TestJsRuntime : IJSRuntime
{
    private readonly TestJsObjectReference _module;

    public TestJsRuntime(TestJsObjectReference module)
    {
        _module = module;
    }

    public Exception? ImportException { get; set; }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        if (identifier == "import")
        {
            if (ImportException is not null)
            {
                return ValueTask.FromException<TValue>(ImportException);
            }

            return ValueTask.FromResult((TValue)(object)_module);
        }

        return ValueTask.FromResult(default(TValue)!);
    }

}
