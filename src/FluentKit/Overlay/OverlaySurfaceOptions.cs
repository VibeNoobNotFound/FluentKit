namespace FluentKit.Overlay;

/// <summary>Optional surface presentation refinements for an anchored overlay.</summary>
public sealed class OverlaySurfaceOptions
{
    /// <summary>Creates the default padded surface with standard entrance motion.</summary>
    public OverlaySurfaceOptions()
    {
    }

    /// <summary>How the overlay content relates to the surface's standard padding.</summary>
    public OverlayContentLayout ContentLayout { get; init; } = OverlayContentLayout.Padded;

    /// <summary>Entrance reveal origin. The default uses the standard fade/scale motion.</summary>
    public OverlayEntranceOrigin EntranceOrigin { get; init; } = OverlayEntranceOrigin.Default;

    /// <summary>Optional timing and velocity controls for the surface motion.</summary>
    public OverlayAnimationOptions Animation { get; init; } = new();

    internal bool IsEquivalentTo(OverlaySurfaceOptions other)
        => ContentLayout == other.ContentLayout
            && EntranceOrigin == other.EntranceOrigin
            && Animation.EntranceDuration == other.Animation.EntranceDuration
            && Animation.EntranceEasing == other.Animation.EntranceEasing
            && Animation.ExitDuration == other.Animation.ExitDuration
            && Animation.ExitEasing == other.Animation.ExitEasing;
}
