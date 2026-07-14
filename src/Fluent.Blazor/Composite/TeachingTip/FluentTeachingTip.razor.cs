using Microsoft.AspNetCore.Components;
using Fluent.Blazor.Overlay;

namespace Fluent.Blazor.Composite;

/// <summary>
/// Mirrors WinUI's TeachingTip — a persistent, anchor-relative callout (title/subtitle, an optional
/// row of action buttons, and an explicit close button) used for contextual "here's what this does"
/// UI, as opposed to Flyout's generic content or ToolTip's hover-only hint. Structurally close to
/// Flyout: built directly on IOverlayService/FluentOverlayHost with the same
/// programmatic-<see cref="IsOpen"/>-binding shape as <see cref="FluentFlyout"/>, and — like every
/// other flyout/menu/tooltip — relies entirely on OverlaySurface's own default chrome (background,
/// <c>backdrop-filter: blur(60px)</c>, border, shadow) for its blur; it does NOT pass <c>bare</c>.
/// One real difference from Flyout: it adds its own pointer/beak toward the anchor in targeted
/// mode (WinUI teaching tips always draw one there — see the beak's own matching background/border
/// in FluentTeachingTip.razor.css), and it defaults <see cref="LightDismiss"/> to false — a teaching
/// tip is meant to stay up until the person reads it and dismisses it via the explicit close button,
/// not disappear the moment they click elsewhere.
///
/// Two placement modes, chosen automatically by whether <see cref="ChildContent"/> is supplied:
/// <list type="bullet">
/// <item><b>Targeted</b> (<see cref="ChildContent"/> set) — <see cref="ChildContent"/> is the
/// anchor/target element the tip points its beak at, positioned via <see cref="Placement"/>
/// (top/bottom/left/right of the anchor), same anchor-measurement path as Flyout/MenuFlyout. Unlike
/// Flyout/MenuFlyout it is NOT wrapped in a click handler — teaching tips are shown/hidden
/// programmatically (e.g. after a first-run check), never by clicking their own target.</item>
/// <item><b>Detached</b> (<see cref="ChildContent"/> omitted) — for a tip that is not pointing at any
/// particular control (e.g. a general new-features announcement). No beak is drawn, and
/// position is set by <see cref="ScreenPlacement"/> relative to the viewport (defaults to
/// bottom-center) via <see cref="IOverlayService.ShowDetached"/> instead of the anchor-based
/// <see cref="IOverlayService.Show"/> overload.</item>
/// </list>
/// </summary>
public partial class FluentTeachingTip : ComponentBase, IDisposable
{
    [Inject] private IOverlayService OverlayService { get; set; } = default!;

    /// <summary>The target element the tip's beak points at. Omit this to get the detached variant
    /// instead — a tip positioned by <see cref="ScreenPlacement"/> with no beak, not tied to any
    /// particular control.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    [Parameter] public string? Title { get; set; }

    [Parameter] public string? Subtitle { get; set; }

    /// <summary>Optional row of action buttons (e.g. "Got it" / "Learn more") below the text.</summary>
    [Parameter] public RenderFragment? ActionContent { get; set; }

    /// <summary>Placement relative to the anchor. Only meaningful in targeted mode (i.e. when
    /// <see cref="ChildContent"/> is set) — ignored in detached mode, where
    /// <see cref="ScreenPlacement"/> is used instead.</summary>
    [Parameter] public OverlayPlacement Placement { get; set; } = OverlayPlacement.Bottom;

    /// <summary>Placement relative to the viewport for the detached variant (i.e. when
    /// <see cref="ChildContent"/> is NOT set). Ignored in targeted mode. Defaults to
    /// <see cref="OverlayScreenPlacement.BottomCenter"/>, matching WinUI's own default spot for an
    /// untargeted teaching tip.</summary>
    [Parameter] public OverlayScreenPlacement ScreenPlacement { get; set; } = OverlayScreenPlacement.BottomCenter;

    /// <summary>Closes on outside click. Defaults false — teaching tips are dismissed explicitly.</summary>
    [Parameter] public bool LightDismiss { get; set; }

    [Parameter] public bool IsOpen { get; set; }

    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }

    private ElementReference _anchor;
    private Guid? _overlayId;
    private bool _lastRenderedIsOpen;

    private bool IsTargeted => ChildContent is not null;

    private string PlacementClass => Placement.ToString().ToLowerInvariant();

    protected override void OnParametersSet()
    {
        if (IsOpen != _lastRenderedIsOpen)
        {
            _lastRenderedIsOpen = IsOpen;
            if (IsOpen)
            {
                ShowInternal();
            }
            else
            {
                HideInternal();
            }
        }
    }

    private void ShowInternal()
    {
        if (_overlayId is not null)
        {
            return;
        }

        _overlayId = IsTargeted
            ? OverlayService.Show(RenderTipContent, _anchor, Placement, LightDismiss)
            : OverlayService.ShowDetached(RenderTipContent, ScreenPlacement, LightDismiss);
    }

    private void HideInternal()
    {
        if (_overlayId is { } id)
        {
            OverlayService.Close(id);
            _overlayId = null;
        }
    }

    private async Task CloseAsync()
    {
        HideInternal();
        _lastRenderedIsOpen = false;
        if (IsOpen)
        {
            IsOpen = false;
            await IsOpenChanged.InvokeAsync(false);
        }
    }

    public void Dispose() => HideInternal();
}
