using Microsoft.AspNetCore.Components;
using Fluent.Blazor.Overlay;

namespace Fluent.Blazor.Composite;

/// <summary>
/// Mirrors WinUI's PaneDisplayMode. Only the three modes that matter for a web-first port —
/// WinUI's fourth mode, Top, is a different layout entirely (horizontal tab strip, not a pane)
/// and is out of scope here; add it as its own variant later if needed, don't bolt it onto this enum.
/// </summary>
public enum NavigationViewPaneDisplayMode
{
    /// <summary>Pane is always docked in the layout; toggling slides it between rail and full width.</summary>
    Expanded,

    /// <summary>Icon-only rail always docked; opening it shows the full pane as a floating overlay
    /// on top of content (doesn't reflow it) — WinUI's LeftCompact.</summary>
    Compact,

    /// <summary>Pane fully hidden; opening it shows the full pane as a floating overlay — WinUI's
    /// LeftMinimal, the mobile/narrow-window pattern.</summary>
    Minimal
}

/// <summary>
/// Mirrors WinUI's NavigationView. Built on top of FluentOverlayHost/IOverlayService (Compact and
/// Minimal float their pane rather than pushing content) — the second real consumer of the overlay
/// infra after FluentTooltip, this time exercising light-dismiss for real.
/// No CSS reference to port from: fluent-svelte's own NavigationView.scss is an empty stub upstream,
/// so the pane/header/footer slot shape below is original, informed by its NavigationView.svelte
/// markup skeleton and by real WinUI 3 structure/behavior.
/// TODO (not v1): nested/expandable items, WinUI's Top pane mode, keyboard pane-width resize.
/// </summary>
public partial class FluentNavigationView : ComponentBase, IDisposable
{
    [Inject] private IOverlayService OverlayService { get; set; } = default!;

    [Parameter]
    public NavigationViewPaneDisplayMode PaneDisplayMode { get; set; } = NavigationViewPaneDisplayMode.Expanded;

    [Parameter]
    public bool IsPaneOpen { get; set; } = true;

    [Parameter]
    public EventCallback<bool> IsPaneOpenChanged { get; set; }

    [Parameter]
    public object? SelectedValue { get; set; }

    [Parameter]
    public EventCallback<object?> SelectedValueChanged { get; set; }

    /// <summary>Fires on every item click, selected or not — matches WinUI's ItemInvoked, which
    /// (unlike SelectionChanged) also fires for e.g. a "Settings" item you don't want to select.</summary>
    [Parameter]
    public EventCallback<object?> ItemInvoked { get; set; }

    [Parameter]
    public bool ShowBackButton { get; set; }

    [Parameter]
    public EventCallback BackRequested { get; set; }

    [Parameter]
    public RenderFragment? PaneHeader { get; set; }

    /// <summary>The nav items themselves — put FluentNavigationViewItem children here.</summary>
    [Parameter]
    public RenderFragment? MenuItems { get; set; }

    [Parameter]
    public RenderFragment? PaneFooter { get; set; }

    /// <summary>The page content shown next to (or under) the pane.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private ElementReference _railAnchor;
    private Guid? _overlayId;
    private NavigationViewContext? _context;

    private bool UsesOverlayPane =>
        PaneDisplayMode is NavigationViewPaneDisplayMode.Compact or NavigationViewPaneDisplayMode.Minimal;

    private bool IsLabelVisible => IsPaneOpen;

    private string PaneDisplayModeClass => PaneDisplayMode switch
    {
        NavigationViewPaneDisplayMode.Compact => "compact",
        NavigationViewPaneDisplayMode.Minimal => "minimal",
        _ => "expanded"
    };

    protected override void OnInitialized()
    {
        OverlayService.Changed += HandleOverlayChanged;
    }

    protected override void OnParametersSet()
    {
        _context = new NavigationViewContext(SelectedValue, IsLabelVisible, SelectItemAsync);
    }

    private async Task TogglePaneAsync()
    {
        IsPaneOpen = !IsPaneOpen;
        await IsPaneOpenChanged.InvokeAsync(IsPaneOpen);

        if (!UsesOverlayPane)
        {
            return;
        }

        if (IsPaneOpen)
        {
            // Anchor is only guaranteed rendered post-first-render, which is fine here — toggling
            // is always a response to a user click, never something that can happen before that.
            _overlayId ??= OverlayService.Show(PaneOverlayContent, _railAnchor, OverlayPlacement.Right);
        }
        else if (_overlayId is { } id)
        {
            OverlayService.Close(id);
            _overlayId = null;
        }
    }

    private async Task SelectItemAsync(object? value)
    {
        SelectedValue = value;
        await SelectedValueChanged.InvokeAsync(value);
        await ItemInvoked.InvokeAsync(value);

        if (UsesOverlayPane && IsPaneOpen)
        {
            // Picking an item closes the floating pane — matches WinUI's Compact/Minimal overlay UX.
            await TogglePaneAsync();
        }

        StateHasChanged();
    }

    private async Task BackAsync() => await BackRequested.InvokeAsync();

    private void HandleOverlayChanged()
    {
        if (_overlayId is not { } id || OverlayService.Active.Any(e => e.Id == id))
        {
            return;
        }

        // Our overlay closed without going through TogglePaneAsync — a light-dismiss click outside
        // the pane. Resync local state so the rail's toggle button reflects reality.
        _overlayId = null;
        IsPaneOpen = false;
        InvokeAsync(async () =>
        {
            await IsPaneOpenChanged.InvokeAsync(false);
            StateHasChanged();
        });
    }

    public void Dispose()
    {
        OverlayService.Changed -= HandleOverlayChanged;
        if (_overlayId is { } id)
        {
            OverlayService.Close(id);
        }
    }
}
