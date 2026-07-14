using Microsoft.AspNetCore.Components;

namespace FluentKit.Overlay;

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

    /// <summary>Requests that an overlay close. Idempotent — calling this on an already-closing or
    /// already-gone entry is a no-op. Does NOT remove the entry from <see cref="Active"/> immediately;
    /// it flips <see cref="OverlayEntry.IsClosing"/> and lets OverlaySurface play the exit animation
    /// before it calls <see cref="CompleteClose"/> to actually remove it. Consumers (Flyout,
    /// TeachingTip, etc.) don't need to know or care about this two-step handoff — from their side,
    /// calling Close() is still fire-and-forget.</summary>
    void Close(Guid id);

    /// <summary>Actually removes an entry from <see cref="Active"/>. Called by OverlaySurface once an
    /// entry's exit animation has finished (or immediately, for an entry that was never shown/never
    /// got as far as playing one). Not meant to be called by application code — call
    /// <see cref="Close"/> instead, which arranges for this to happen at the right time.</summary>
    void CompleteClose(Guid id);

    void CloseAll();
}
