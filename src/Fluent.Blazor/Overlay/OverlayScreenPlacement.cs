namespace Fluent.Blazor.Overlay;

/// <summary>
/// Placement for a "detached" overlay that has no anchor element to position relative to (see
/// <see cref="IOverlayService.ShowDetached"/>) — positioned relative to the viewport instead, via a
/// plain CSS `position: fixed` computed synchronously in <see cref="OverlayService"/> (unlike the
/// anchor-based <see cref="OverlayPlacement"/> path, this needs no JS measurement round-trip, since
/// there's no anchor rect to measure — the overlay can be positioned correctly on its very first
/// render instead of one frame later).
/// </summary>
public enum OverlayScreenPlacement
{
    BottomCenter,
    TopCenter,
    Center,
    BottomLeft,
    BottomRight,
    TopLeft,
    TopRight
}
