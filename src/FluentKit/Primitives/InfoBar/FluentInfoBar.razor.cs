using Microsoft.AspNetCore.Components;

namespace FluentKit.Primitives;

/// <summary>
/// App-wide status message banner — Title/Message text, an optional action, and (by default) a
/// close button. Ported from fluent-svelte's InfoBar: reuses <see cref="InfoBadgeSeverity"/> (same
/// five-value severity ramp as FluentInfoBadge, which is also this component's default icon slot —
/// same simplification fluent-svelte makes rather than pulling in WinUI's actual per-severity
/// symbol glyphs) and the same <c>--system-fill-color-*-background</c> tokens the InfoBadge tokens
/// comment already calls out as shared between the two components.
///
/// Skips fluent-svelte's JS-measured wrap detection (offsetTop comparisons to add
/// message-wrapped/action-wrapped classes when the title/message/action overflow onto their own
/// line) — plain flexbox wrap gives acceptably close layout without a measurement pass, and this
/// is a static banner, not something worth a resize-observer for.
/// </summary>
public partial class FluentInfoBar : ComponentBase
{
    [Parameter] public InfoBadgeSeverity Severity { get; set; } = InfoBadgeSeverity.Informational;

    [Parameter] public string? Title { get; set; }

    [Parameter] public string? Message { get; set; }

    [Parameter] public bool IsOpen { get; set; } = true;

    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }

    /// <summary>Whether the built-in close button is rendered.</summary>
    [Parameter] public bool Closable { get; set; } = true;

    /// <summary>Overrides the default severity-colored <see cref="FluentInfoBadge"/> icon.</summary>
    [Parameter] public RenderFragment? Icon { get; set; }

    /// <summary>Optional call-to-action content (typically a FluentButton), right-aligned in the content row.</summary>
    [Parameter] public RenderFragment? Action { get; set; }

    /// <summary>Additional message content appended after <see cref="Message"/> — for rich content beyond plain text.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    // Same mapping as FluentInfoBadge.SeverityClass — kept in sync by hand since InfoBadgeSeverity
    // is the shared type; if that enum grows a new value both switch expressions need it.
    private string SeverityClass => Severity switch
    {
        InfoBadgeSeverity.Success => "success",
        InfoBadgeSeverity.Caution => "caution",
        InfoBadgeSeverity.Critical => "critical",
        InfoBadgeSeverity.Attention => "attention",
        _ => "informational"
    };

    private Task CloseAsync()
    {
        IsOpen = false;
        return IsOpenChanged.InvokeAsync(false);
    }
}
