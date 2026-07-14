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

    protected override async Task OnInitializedAsync()
    {
        ThemeService.ThemeChanged += OnThemeChanged;

        _module = await JS.InvokeAsync<IJSObjectReference>(
            "import", "./_content/FluentKit/Theming/theme-interop.js");

        // Reads prefers-color-scheme and sets up a live listener for OS-level changes.
        await ThemeService.InitializeAsync();

        await ApplyThemeAsync();
    }

    private async void OnThemeChanged()
    {
        await ApplyThemeAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task ApplyThemeAsync()
    {
        var resolved = ThemeService.ResolvedTheme;
        if (resolved == _lastAppliedTheme || _module is null)
        {
            return;
        }

        _lastAppliedTheme = resolved;
        await _module.InvokeVoidAsync("applyResolvedTheme", resolved);
    }

    public async ValueTask DisposeAsync()
    {
        ThemeService.ThemeChanged -= OnThemeChanged;
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }
}
