namespace FluentKit.Overlay;

/// <summary>Controls whether an overlay's content participates in the surface's standard inset.</summary>
public enum OverlayContentLayout
{
    /// <summary>Keep the surface's standard content inset.</summary>
    Padded,

    /// <summary>Extend the content to the surface border while retaining the surface chrome.</summary>
    EdgeToEdge
}
