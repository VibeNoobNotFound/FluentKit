namespace FluentKit.Overlay;

/// <summary>Chooses the reveal origin for an overlay entrance animation.</summary>
public enum OverlayEntranceOrigin
{
    /// <summary>Use the standard overlay fade/scale entrance.</summary>
    Default,

    /// <summary>Reveal from the top portion of the surface.</summary>
    Top,

    /// <summary>Reveal from the center portion of the surface.</summary>
    Center,

    /// <summary>Reveal from the bottom portion of the surface.</summary>
    Bottom
}
