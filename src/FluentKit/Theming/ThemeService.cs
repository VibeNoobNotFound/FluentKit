using FluentKit.Interop;
using Microsoft.JSInterop;

namespace FluentKit.Theming;

public sealed class ThemeService : IThemeService, IAsyncDisposable
{
    private readonly JsModuleLifetime _interop;
    private DotNetObjectReference<ThemeService>? _selfReference;
    private string _systemPreference = "dark"; // safe default before JS has responded
    private int _disposed;

    public ThemeService(IJSRuntime js)
    {
        _interop = new(js, "./_content/FluentKit/Theming/theme-interop.js");
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

        if (!await _interop.EnsureModuleAsync())
        {
            return;
        }

        _selfReference = DotNetObjectReference.Create(this);

        // theme-interop.js reads matchMedia('(prefers-color-scheme: dark)') once here,
        // then calls back into OnSystemPreferenceChanged whenever the OS-level setting changes live.
        var result = await _interop.InvokeAsync<string>(
            "watchSystemPreference", _selfReference);
        if (!result.Succeeded)
        {
            return;
        }

        _systemPreference = result.Value ?? _systemPreference;

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

        var selfReference = Interlocked.Exchange(ref _selfReference, null);

        try
        {
            await _interop.DisposeAsync(("unwatchSystemPreference", null));
        }
        finally
        {
            selfReference?.Dispose();
        }
    }
}
