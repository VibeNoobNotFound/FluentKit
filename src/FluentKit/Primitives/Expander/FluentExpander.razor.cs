using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FluentKit.Primitives;

/// <summary>
/// Expander — header + collapsible content area, header click/Enter/Space toggles. Animation uses
/// fluent-svelte's clip+slide technique (max-block-size snap + translateY on the content) rather
/// than the CSS-grid 0fr/1fr trick, which has inconsistent auto-minimum-size behavior across
/// browsers (notably Safari) and can leave the row stuck above 0 with content still visible.
/// </summary>
public partial class FluentExpander : ComponentBase
{
    /// <summary>Header title content.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Collapsible body content.</summary>
    [Parameter] public RenderFragment? Content { get; set; }

    /// <summary>Optional leading icon in the header.</summary>
    [Parameter] public RenderFragment? Icon { get; set; }

    [Parameter] public bool IsExpanded { get; set; }

    [Parameter] public EventCallback<bool> IsExpandedChanged { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private Task ToggleAsync() => SetExpandedAsync(!IsExpanded);

    private Task SetExpandedAsync(bool value)
    {
        IsExpanded = value;
        return IsExpandedChanged.InvokeAsync(value);
    }

    private Task HandleHeaderKeyDown(KeyboardEventArgs args)
        => args.Key is "Enter" or " " ? ToggleAsync() : Task.CompletedTask;
}
