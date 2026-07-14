using Microsoft.AspNetCore.Components;

namespace FluentKit.Primitives;

/// <summary>
/// Linear ProgressBar (ProgressBar_themeresources.xaml). Determinate mode fills to <see cref="Value"/>
/// (0-100); with <see cref="IsIndeterminate"/> it plays WinUI's two-segment sliding-bar loop instead
/// (pure CSS keyframe animation — no JS needed, unlike ProgressRing's arc math).
/// </summary>
public partial class FluentProgressBar : ComponentBase
{
    /// <summary>Progress value 0-100. Ignored when <see cref="IsIndeterminate"/> is true.</summary>
    [Parameter] public double Value { get; set; }

    [Parameter] public bool IsIndeterminate { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private double ClampedValue => Math.Clamp(Value, 0, 100);
}
