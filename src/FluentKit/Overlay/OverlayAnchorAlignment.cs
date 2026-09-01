namespace FluentKit.Overlay;

/// <summary>
/// Determines where an anchored overlay starts on its placement axis.
/// </summary>
public enum OverlayAnchorAlignment
{
    /// <summary>Place the overlay outside the anchor, using the normal placement gap and flip behavior.</summary>
    Adjacent,

    /// <summary>Align the overlay's leading edge with the anchor's leading edge before applying an offset.</summary>
    AnchorStart
}
