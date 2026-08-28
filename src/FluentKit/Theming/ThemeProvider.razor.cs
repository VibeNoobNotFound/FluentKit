using FluentKit.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FluentKit.Theming;

public partial class ThemeProvider : ComponentBase, IAsyncDisposable
{
    [Inject] private IThemeService ThemeService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public RenderFragment? ChildContent { get; set; }

    private IJSObjectReference? _module;
    private string? _lastAppliedTheme;
    private int _disposed;
    private bool _themeHandlerSubscribed;

    protected override async Task OnInitializedAsync()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        ThemeService.ThemeChanged += OnThemeChanged;
        _themeHandlerSubscribed = true;

        var module = await JS.InvokeAsync<IJSObjectReference>(
            "import", "./_content/FluentKit/Theming/theme-interop.js");

        if (Volatile.Read(ref _disposed) != 0)
        {
            await JsModuleDisposal.DisposeAsync(module);
            return;
        }

        _module = module;

        // Reads prefers-color-scheme and sets up a live listener for OS-level changes.
        await ThemeService.InitializeAsync();

        if (Volatile.Read(ref _disposed) == 0)
        {
            await ApplyThemeAsync();
        }
    }

    private async void OnThemeChanged()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        try
        {
            await ApplyThemeAsync();
            if (Volatile.Read(ref _disposed) == 0)
            {
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (JSDisconnectedException)
        {
            // The circuit may disappear while the asynchronous theme update is in flight.
        }
    }

    private async Task ApplyThemeAsync()
    {
        var resolved = ThemeService.ResolvedTheme;
        if (Volatile.Read(ref _disposed) != 0 || resolved == _lastAppliedTheme || _module is null)
        {
            return;
        }

        _lastAppliedTheme = resolved;
        try
        {
            await _module.InvokeVoidAsync("applyResolvedTheme", resolved);
        }
        catch (JSDisconnectedException)
        {
            // The circuit may disappear while applying the theme.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_themeHandlerSubscribed)
        {
            ThemeService.ThemeChanged -= OnThemeChanged;
            _themeHandlerSubscribed = false;
        }

        var module = Interlocked.Exchange(ref _module, null);
        await JsModuleDisposal.DisposeAsync(module);
    }
}
