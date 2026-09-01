namespace FluentKit.Overlay;

/// <summary>Controls how an overlay's velocity changes during its entrance or exit.</summary>
public enum OverlayAnimationEasing
{
    /// <summary>Starts at maximum velocity and gradually slows to rest. Use for entrances.</summary>
    Decelerate,

    /// <summary>Uses the balanced Fluent standard curve.</summary>
    Standard,

    /// <summary>Starts slowly and gains velocity. Use for exits.</summary>
    Accelerate,

    /// <summary>Maintains a constant velocity.</summary>
    Linear
}
