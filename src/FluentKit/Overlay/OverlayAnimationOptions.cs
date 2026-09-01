namespace FluentKit.Overlay;

/// <summary>Optional timing controls for an overlay surface.</summary>
public sealed class OverlayAnimationOptions
{
    /// <summary>
    /// Overrides the entrance duration. When omitted, standard surfaces use the fast duration and
    /// selected-row reveals use the normal duration.
    /// </summary>
    public TimeSpan? EntranceDuration { get; init; }

    /// <summary>
    /// Controls the entrance velocity. The default is WinUI's fast-out, slow-in curve: maximum
    /// speed at the start that progressively decreases as the surface reaches rest.
    /// </summary>
    public OverlayAnimationEasing EntranceEasing { get; init; } = OverlayAnimationEasing.Decelerate;

    /// <summary>Overrides the exit duration. When omitted, the standard fast duration is used.</summary>
    public TimeSpan? ExitDuration { get; init; }

    /// <summary>Controls the exit velocity. The default accelerates out of the scene.</summary>
    public OverlayAnimationEasing ExitEasing { get; init; } = OverlayAnimationEasing.Accelerate;
}
