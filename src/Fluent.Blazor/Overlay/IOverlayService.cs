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
        bool matchAnchorWidth = false);

    void Close(Guid id);

    void CloseAll();
}
