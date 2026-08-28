using FluentKit.Interop;
using Microsoft.JSInterop;

namespace FluentKit.Theming;

public sealed class ThemeService : IThemeService, IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;
    private DotNetObjectReference<ThemeService>? _selfReference;
    private string _systemPreference = "dark"; // safe default before JS has responded
    private int _disposed;

    public ThemeService(IJSRuntime js)
    {
        _js = js;
    }

    public ThemeMode Mode { get; private set; } = ThemeMode.System;

    public string ResolvedTheme => Mode switch
    {
        ThemeMode.Light => "light",
        ThemeMode.Dark => "dark",
        _ => _systemPreference
    };

    public event Action? ThemeChanged;

    public async Task InitializeAsync()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        var module = await _js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/FluentKit/Theming/theme-interop.js");

        if (Volatile.Read(ref _disposed) != 0)
        {
            await JsModuleDisposal.DisposeAsync(module);
            return;
        }

        _module = module;

        _selfReference = DotNetObjectReference.Create(this);

        // theme-interop.js reads matchMedia('(prefers-color-scheme: dark)') once here,
        // then calls back into OnSystemPreferenceChanged whenever the OS-level setting changes live.
        try
        {
            _systemPreference = await module.InvokeAsync<string>(
                "watchSystemPreference", _selfReference);
        }
        catch (JSDisconnectedException)
        {
            // Initialization can overlap circuit teardown just like disposal can.
            return;
        }

        if (Volatile.Read(ref _disposed) == 0)
        {
            ThemeChanged?.Invoke();
        }
    }

    [JSInvokable]
    public void OnSystemPreferenceChanged(string preference)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        _systemPreference = preference;
        if (Mode == ThemeMode.System)
        {
            ThemeChanged?.Invoke();
        }
    }

    public Task SetModeAsync(ThemeMode mode)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return Task.CompletedTask;
        }

        Mode = mode;
        ThemeChanged?.Invoke();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        var module = Interlocked.Exchange(ref _module, null);
        var selfReference = Interlocked.Exchange(ref _selfReference, null);

        try
        {
            if (module is not null)
            {
                try
                {
                    await module.InvokeVoidAsync("unwatchSystemPreference").ConfigureAwait(false);
                }
                catch (JSDisconnectedException)
                {
                    // The browser-side listener is unreachable after circuit teardown.
                }
                finally
                {
                    await JsModuleDisposal.DisposeAsync(module);
                }
            }
        }
        finally
        {
            selfReference?.Dispose();
        }
    }
}
