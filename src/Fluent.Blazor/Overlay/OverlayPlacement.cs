namespace Fluent.Blazor.Overlay;

/// <summary>
/// Preferred placement relative to the anchor element. The JS interop layer (overlay-interop.js)
/// flips this to the opposite side when there isn't room in the viewport — callers get the flipped
/// result back, they don't need to reason about collision themselves.
/// </summary>
public enum OverlayPlacement
{
    Bottom,
    Top,
    Left,
    Right
}
