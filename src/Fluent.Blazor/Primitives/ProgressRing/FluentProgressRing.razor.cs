using Microsoft.AspNetCore.Components;

namespace Fluent.Blazor.Primitives;

/// <summary>
/// Circular ProgressRing. Built with a conic-gradient + radial-gradient mask instead of SVG
/// stroke-dashoffset — no JS, no per-frame arc-length math, just a CSS custom property
/// (--fluent-progress-ring-angle) driving how much of the conic-gradient reads as "filled". The
/// indeterminate mode is a single continuous CSS rotation instead (real WinUI ProgressRing's
/// indeterminate animation is a more elaborate multi-arc easing curve; this is a deliberate v1
/// simplification, flagged for anyone doing a pixel pass later).
/// </summary>
public partial class FluentProgressRing : ComponentBase
{
    /// <summary>Progress value 0-100. Ignored when <see cref="IsIndeterminate"/> is true.</summary>
    [Parameter] public double Value { get; set; }

    [Parameter] public bool IsIndeterminate { get; set; } = true;

    /// <summary>Diameter in pixels.</summary>
    [Parameter] public int Size { get; set; } = 32;

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private double ClampedValue => Math.Clamp(Value, 0, 100);
}
