using Microsoft.AspNetCore.Components;

namespace Fluent.Blazor.Overlay;

public sealed class OverlayEntry
{
    public Guid Id { get; } = Guid.NewGuid();
    public required RenderFragment Content { get; init; }
    public required ElementReference Anchor { get; init; }
    public OverlayPlacement PreferredPlacement { get; init; } = OverlayPlacement.Bottom;
    public bool LightDismiss { get; init; } = true;

    /// <summary>When true, OverlaySurface renders with no chrome of its own (no background, blur,
    /// border, shadow, or padding) — for consumers like NavigationView's Compact/Minimal overlay
    /// pane that supply their own surface material (e.g. FluentAcrylicBrush) and would otherwise
    /// get a second, redundant blur/background stacked underneath theirs.</summary>
    public bool Bare { get; init; }

    /// <summary>When true, the popup is resized to exactly match the anchor's width before being
    /// positioned (e.g. DropDownButton/SplitButton's menu, which should read as "attached to" the
    /// trigger rather than an independently-sized flyout) — set on overlay-interop.js's
    /// <c>computePosition</c> call, since the anchor's width isn't known until the JS side measures
    /// it via <c>getBoundingClientRect</c>.</summary>
    public bool MatchAnchorWidth { get; init; }

    /// <summary>True for overlays created via <see cref="IOverlayService.ShowDetached"/> — no anchor
    /// element, positioned relative to the viewport instead. OverlaySurface uses this to skip the
    /// anchor-measurement JS round-trip entirely (ComputedStyle is already filled in by
    /// OverlayService by the time this is true) and to register light-dismiss without an anchor
    /// exclusion zone.</summary>
    public bool IsDetached { get; init; }

    /// <summary>When true, the anchor is scrolled into view (centered, instant) before its position
    /// is measured — for overlays like TeachingTip where the anchor may not be the same element the
    /// person just clicked (see FluentTeachingTip's separate <c>Target</c> parameter), so there's no
    /// guarantee it's already on screen the way a Flyout/MenuFlyout's own trigger always is.</summary>
    public bool ScrollAnchorIntoView { get; init; }

    /// <summary>When true, the overlay auto-closes if its anchor element is later removed from the
    /// DOM while still open — for overlays like TeachingTip's <c>Target</c> mode, where the anchor
    /// isn't the trigger the person clicked and so isn't guaranteed to stay mounted for the tip's
    /// lifetime (e.g. it lived inside a list item that got filtered out). Flyout/MenuFlyout don't
    /// need this: their anchor is always their own trigger, which can't disappear out from under an
    /// open overlay it's the parent of.</summary>
    public bool WatchAnchorRemoved { get; init; }

    /// <summary>Inline "position: fixed; top: …px; left: …px" string, filled in by FluentOverlayHost
    /// after JS interop reports the anchor's measured position. Empty until first measured, so the
    /// overlay renders off-screen-but-in-the-DOM for one frame rather than flashing at (0,0).</summary>
    public string ComputedStyle { get; set; } = "position: fixed; visibility: hidden;";
}
