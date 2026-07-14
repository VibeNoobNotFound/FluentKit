using Microsoft.AspNetCore.Components;
using System.Globalization;

namespace FluentKit.Primitives;

/// <summary>
/// Circular ProgressRing. Rewritten to match fluent-svelte's precise SVG circle-stroke-dashoffset math 
/// and CSS keyframe animations. Uses vector strokes rather than conic-gradient masks for maximum 
/// rendering sharpness and accurate animation easing curves.
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

    private string? DashOffset
    {
        get
        {
            var circumference = Math.PI * 14.0; // r = 7 -> diameter = 14
            var offset = ((100.0 - ClampedValue) / 100.0) * circumference;
            return offset.ToString(CultureInfo.InvariantCulture);
        }
    }
}
