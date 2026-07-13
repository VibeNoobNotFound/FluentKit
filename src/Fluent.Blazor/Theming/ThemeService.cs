using Microsoft.JSInterop;

namespace Fluent.Blazor.Theming;

public sealed class ThemeService : IThemeService, IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;
    private DotNetObjectReference<ThemeService>? _selfReference;
    private string _systemPreference = "dark"; // safe default before JS has responded

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
        _module = await _js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Fluent.Blazor/Theming/theme-interop.js");

        _selfReference = DotNetObjectReference.Create(this);

        // theme-interop.js reads matchMedia('(prefers-color-scheme: dark)') once here,
        // then calls back into OnSystemPreferenceChanged whenever the OS-level setting changes live.
        _systemPreference = await _module.InvokeAsync<string>(
            "watchSystemPreference", _selfReference);

        ThemeChanged?.Invoke();
    }

    [JSInvokable]
    public void OnSystemPreferenceChanged(string preference)
    {
        _systemPreference = preference;
        if (Mode == ThemeMode.System)
        {
            ThemeChanged?.Invoke();
        }
    }

    public Task SetModeAsync(ThemeMode mode)
    {
        Mode = mode;
        ThemeChanged?.Invoke();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _selfReference?.Dispose();
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }
}
