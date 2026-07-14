using Microsoft.AspNetCore.Components;

namespace Fluent.Blazor.Overlay;

/// <summary>
/// Register as Scoped. Components never position themselves — they call Show() with their own
/// ElementReference as the anchor and let FluentOverlayHost (mounted once, near the app root)
/// render the content at a fixed top-level position. This is the Blazor answer to
/// React's createPortal: render-tree teleportation via a cascading service, not DOM manipulation.
/// </summary>
public interface IOverlayService
{
    IReadOnlyList<OverlayEntry> Active { get; }

    event Action? Changed;

    Guid Show(RenderFragment content, ElementReference anchor,
        OverlayPlacement placement = OverlayPlacement.Bottom, bool lightDismiss = true, bool bare = false,
        bool matchAnchorWidth = false, bool scrollAnchorIntoView = false, bool watchAnchorRemoved = false);

    /// <summary>Shows an overlay with no anchor element — positioned relative to the viewport (see
    /// <see cref="OverlayScreenPlacement"/>) instead of relative to a trigger control. For content
    /// that isn't "pointing at" anything on screen, e.g. a general-purpose announcement/teaching tip
    /// that isn't tied to a specific control.</summary>
    Guid ShowDetached(RenderFragment content,
        OverlayScreenPlacement screenPlacement = OverlayScreenPlacement.BottomCenter, bool lightDismiss = true);

    void Close(Guid id);

    void CloseAll();
}
