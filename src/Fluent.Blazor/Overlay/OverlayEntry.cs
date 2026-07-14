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

    /// <summary>Inline "position: fixed; top: …px; left: …px" string, filled in by FluentOverlayHost
    /// after JS interop reports the anchor's measured position. Empty until first measured, so the
    /// overlay renders off-screen-but-in-the-DOM for one frame rather than flashing at (0,0).</summary>
    public string ComputedStyle { get; set; } = "position: fixed; visibility: hidden;";
}
