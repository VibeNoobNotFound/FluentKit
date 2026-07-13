using Microsoft.AspNetCore.Components;

namespace Fluent.Blazor.Primitives;

public enum InfoBadgeSeverity
{
    Attention,
    Success,
    Caution,
    Critical,
    Informational
}

/// <summary>
/// Small non-intrusive notification/status dot or count pill. Empty (no ChildContent) renders as a
/// plain 8px dot — used to flag "something changed" without a number; with ChildContent (typically a
/// short number/text) it grows into a pill, same behavior as WinUI's InfoBadge Value/IconSource split.
/// </summary>
public partial class FluentInfoBadge : ComponentBase
{
    [Parameter] public InfoBadgeSeverity Severity { get; set; } = InfoBadgeSeverity.Attention;

    [Parameter] public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private string SeverityClass => Severity switch
    {
        InfoBadgeSeverity.Success => "success",
        InfoBadgeSeverity.Caution => "caution",
        InfoBadgeSeverity.Critical => "critical",
        InfoBadgeSeverity.Informational => "informational",
        _ => "attention"
    };
}
