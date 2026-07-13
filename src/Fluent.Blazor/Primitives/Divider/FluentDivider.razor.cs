using Microsoft.AspNetCore.Components;

namespace Fluent.Blazor.Primitives;

/// <summary>
/// Mirrors WinUI's plain content-area Divider (a hairline rule), not MenuFlyoutDivider — that one
/// lives in the Composite/Overlay layer since it only makes sense inside a MenuFlyout's own padding
/// rules. This is the general-purpose one for separating sections of ordinary page content.
/// </summary>
public partial class FluentDivider : ComponentBase
{
    [Parameter]
    public bool Vertical { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }
}
