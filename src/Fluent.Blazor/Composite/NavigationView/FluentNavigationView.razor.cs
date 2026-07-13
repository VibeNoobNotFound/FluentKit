using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Fluent.Blazor.Composite;

public enum NavigationViewPaneDisplayMode
{
    Auto,
    Left,
    Top,
    LeftCompact,
    LeftMinimal
}

public enum NavigationViewDisplayMode
{
    Minimal,
    Compact,
    Expanded
}

public partial class FluentNavigationView : ComponentBase, IDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter]
    public NavigationViewPaneDisplayMode PaneDisplayMode { get; set; } = NavigationViewPaneDisplayMode.Auto;

    [Parameter]
    public bool IsPaneOpen { get; set; } = true;

    [Parameter]
    public EventCallback<bool> IsPaneOpenChanged { get; set; }

    [Parameter]
    public object? SelectedValue { get; set; }

    [Parameter]
    public EventCallback<object?> SelectedValueChanged { get; set; }

    [Parameter]
    public EventCallback<object?> ItemInvoked { get; set; }

    [Parameter]
    public bool ShowBackButton { get; set; }

    [Parameter]
    public EventCallback BackRequested { get; set; }

    [Parameter]
    public RenderFragment? PaneHeader { get; set; }

    [Parameter]
    public RenderFragment? MenuItems { get; set; }

    [Parameter]
    public RenderFragment? PaneFooter { get; set; }

    [Parameter]
    public bool ShowSettingsButton { get; set; }

    [Parameter]
    public EventCallback SettingsRequested { get; set; }

    [Parameter]
    public RenderFragment? HeaderContent { get; set; }

    [Parameter]
    public bool AlwaysShowHeader { get; set; } = true;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private ElementReference _rootElement;
    private NavigationViewContext? _context;
    private IJSObjectReference? _module;

    public NavigationViewDisplayMode DisplayMode { get; private set; } = NavigationViewDisplayMode.Expanded;

    private bool IsCompactOrMinimal =>
        PaneDisplayMode is NavigationViewPaneDisplayMode.LeftCompact or NavigationViewPaneDisplayMode.LeftMinimal;

    private string PaneDisplayModeClass => PaneDisplayMode switch
    {
        NavigationViewPaneDisplayMode.LeftCompact => "compact",
        NavigationViewPaneDisplayMode.LeftMinimal => "minimal",
        NavigationViewPaneDisplayMode.Top => "top",
        NavigationViewPaneDisplayMode.Auto => "auto",
        _ => "expanded"
    };

    protected override void OnInitialized()
    {
        _context = new NavigationViewContext(this);
        _context.SelectionChanged += OnContextSelectionChanged;
        _context.ItemClicked += (object? sender1) => OnItemClicked(sender1);
        base.OnInitialized();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _module = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/Fluent.Blazor/js/navview-interop.js");
            await UpdateDisplayModeAsync();
        }
        await base.OnAfterRenderAsync(firstRender);
    }

    protected override void OnParametersSet()
    {
        if (_context != null && _context.SelectedValue != SelectedValue)
        {
            _context.SelectedValue = SelectedValue;
        }
        base.OnParametersSet();
    }

    private void OnContextSelectionChanged()
    {
        var newValue = _context?.SelectedValue;
        if (!Equals(SelectedValue, newValue))
        {
            SelectedValue = newValue;
            _ = SelectedValueChanged.InvokeAsync(SelectedValue);
            StateHasChanged();
        }
    }

    internal void NotifyContextSelectionChanged(object? newValue)
    {
        OnContextSelectionChanged();
    }

    private async Task OnItemClicked(object? value)
    {
        await ItemInvoked.InvokeAsync(value);
        // Close overlay if in compact/minimal and pane is open
        if (IsCompactOrMinimal && IsPaneOpen)
        {
            await TogglePaneAsync();
        }
    }

    private async Task TogglePaneAsync()
    {
        IsPaneOpen = !IsPaneOpen;
        await IsPaneOpenChanged.InvokeAsync(IsPaneOpen);
        StateHasChanged();
    }

    private async Task UpdateDisplayModeAsync()
    {
        var width = await GetWidthAsync();
        var paneDisplayMode = PaneDisplayMode;

        NavigationViewDisplayMode displayMode;
        if (paneDisplayMode == NavigationViewPaneDisplayMode.Top)
        {
            displayMode = NavigationViewDisplayMode.Minimal;
        }
        else if (paneDisplayMode == NavigationViewPaneDisplayMode.LeftCompact)
        {
            displayMode = NavigationViewDisplayMode.Compact;
        }
        else if (paneDisplayMode == NavigationViewPaneDisplayMode.LeftMinimal)
        {
            displayMode = NavigationViewDisplayMode.Minimal;
        }
        else if (paneDisplayMode == NavigationViewPaneDisplayMode.Left)
        {
            displayMode = NavigationViewDisplayMode.Expanded;
        }
        else // Auto
        {
            var expandedThreshold = 1008.0;
            var compactThreshold = 641.0;

            if (width >= expandedThreshold)
                displayMode = NavigationViewDisplayMode.Expanded;
            else if (width >= compactThreshold)
                displayMode = NavigationViewDisplayMode.Compact;
            else
                displayMode = NavigationViewDisplayMode.Minimal;
        }

        if (DisplayMode != displayMode)
        {
            DisplayMode = displayMode;
            StateHasChanged();
        }

        // Auto close pane when switching to compact/minimal
        if (IsCompactOrMinimal && IsPaneOpen)
        {
            await TogglePaneAsync();
        }
    }

    private async Task<double> GetWidthAsync()
    {
        if (!string.IsNullOrEmpty(_rootElement.Id) && _module != null)
        {
            try
            {
                return await _module.InvokeAsync<double>("getElementWidth", _rootElement);
            }
            catch
            {
                return 800;
            }
        }
        return 800;
    }

    private async Task OnBackClick() => await BackRequested.InvokeAsync();
    private async Task OnSettingsClick() => await SettingsRequested.InvokeAsync();

    public void Dispose()
    {
        _context?.Dispose();
        if (_module is not null)
        {
            try { _module.DisposeAsync().AsTask().Wait(); } catch { }
        }
    }
}