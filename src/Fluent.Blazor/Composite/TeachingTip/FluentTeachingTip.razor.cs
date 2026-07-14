using Microsoft.AspNetCore.Components;
using Fluent.Blazor.Overlay;

namespace Fluent.Blazor.Composite;

/// <summary>
/// Mirrors WinUI's TeachingTip — a persistent, anchor-relative callout (title/subtitle, an optional
/// row of action buttons, and an explicit close button) used for contextual "here's what this does"
/// UI, as opposed to Flyout's generic content or ToolTip's hover-only hint. Structurally close to
/// Flyout: built directly on IOverlayService/FluentOverlayHost with the same
/// programmatic-<see cref="IsOpen"/>-binding shape as <see cref="FluentFlyout"/>. Two real
/// differences: it renders with <c>bare: true</c> since it supplies its own card chrome plus a
/// pointer/"beak" toward the anchor (WinUI teaching tips always draw one), and it defaults
/// <see cref="LightDismiss"/> to false — a teaching tip is meant to stay up until the person reads it
/// and dismisses it via the explicit close button, not disappear the moment they click elsewhere.
/// <see cref="ChildContent"/> is the anchor/target element the tip points at; unlike Flyout/MenuFlyout
/// it is NOT wrapped in a click handler — teaching tips are shown/hidden programmatically (e.g. after
/// a first-run check), never by clicking their own target.
/// </summary>
public partial class FluentTeachingTip : ComponentBase, IDisposable
{
    [Inject] private IOverlayService OverlayService { get; set; } = default!;

    /// <summary>The target element the tip's beak points at.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    [Parameter] public string? Title { get; set; }

    [Parameter] public string? Subtitle { get; set; }

    /// <summary>Optional row of action buttons (e.g. "Got it" / "Learn more") below the text.</summary>
    [Parameter] public RenderFragment? ActionContent { get; set; }

    [Parameter] public OverlayPlacement Placement { get; set; } = OverlayPlacement.Bottom;

    /// <summary>Closes on outside click. Defaults false — teaching tips are dismissed explicitly.</summary>
    [Parameter] public bool LightDismiss { get; set; }

    [Parameter] public bool IsOpen { get; set; }

    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }

    private ElementReference _anchor;
    private Guid? _overlayId;
    private bool _lastRenderedIsOpen;

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

        _overlayId = OverlayService.Show(RenderTipContent, _anchor, Placement, LightDismiss, bare: true);
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
