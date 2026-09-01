namespace FluentKit.Overlay;

/// <summary>
/// Optional positioning refinements for an anchored overlay. Existing overlay calls use
/// <see cref="OverlayAnchorAlignment.Adjacent"/> and a zero offset.
/// </summary>
public sealed class OverlayPositioningOptions
{
    /// <summary>How the overlay aligns to its anchor before <see cref="MainAxisOffset"/> is applied.</summary>
    public OverlayAnchorAlignment Alignment { get; init; } = OverlayAnchorAlignment.Adjacent;

    /// <summary>
    /// Additional pixel offset on the placement axis. For an <see cref="OverlayAnchorAlignment.AnchorStart"/>
    /// overlay this is measured from the anchor's leading edge.
    /// </summary>
    public double MainAxisOffset { get; init; }

    internal bool IsEquivalentTo(OverlayPositioningOptions other)
        => Alignment == other.Alignment && MainAxisOffset.Equals(other.MainAxisOffset);
}
