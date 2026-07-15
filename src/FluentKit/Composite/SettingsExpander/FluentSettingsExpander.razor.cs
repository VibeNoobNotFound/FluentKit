using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FluentKit.Composite;

/// <summary>
/// 1:1 port of the Windows Community Toolkit's SettingsExpander
/// (CommunityToolkit.WinUI.Controls.SettingsExpander) — a collapsible header (itself a
/// <see cref="FluentSettingsCard"/>-shaped surface) that reveals a list of nested
/// <see cref="FluentSettingsCard"/> items underneath when expanded.
///
/// WCT's version hosts its items via an ItemsRepeater bound to an Items/ItemsSource pair. Blazor
/// has no ItemsRepeater equivalent worth reproducing here — the idiomatic approach is simply
/// letting the caller pass the items as ordinary child markup (<see cref="ItemsContent"/>,
/// typically a handful of &lt;FluentSettingsCard&gt; elements), the same way every other
/// FluentKit composite accepts its children. <see cref="ItemsHeader"/> and <see cref="ItemsFooter"/>
/// are preserved as-is since they're just extra render slots above/below the item list.
///
/// The expand/collapse animation reuses FluentExpander's clip+slide technique (see that
/// component's remarks) rather than the CSS-grid 0fr/1fr trick.
/// </summary>
public partial class FluentSettingsExpander : ComponentBase
{
    /// <summary>Header text. Ignored when <see cref="HeaderContent"/> is supplied.</summary>
    [Parameter] public string? Header { get; set; }

    /// <summary>Header content, for anything beyond plain text.</summary>
    [Parameter] public RenderFragment? HeaderContent { get; set; }

    /// <summary>Description text shown under the header. Ignored when <see cref="DescriptionContent"/> is supplied.</summary>
    [Parameter] public string? Description { get; set; }

    /// <summary>Description content, for anything beyond plain text.</summary>
    [Parameter] public RenderFragment? DescriptionContent { get; set; }

    /// <summary>The icon shown at the leading edge of the header.</summary>
    [Parameter] public RenderFragment? HeaderIcon { get; set; }

    /// <summary>
    /// The end-aligned content of the header row (maps to WCT's <c>Content</c> — commonly a
    /// ToggleSwitch that controls the whole group). Named ChildContent so it can be passed as
    /// the component's ordinary child markup.
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// The nested settings cards shown once expanded (maps to WCT's Items/ItemsSource —
    /// typically a handful of &lt;FluentSettingsCard&gt; elements).
    /// </summary>
    [Parameter] public RenderFragment? ItemsContent { get; set; }

    /// <summary>Optional content shown above <see cref="ItemsContent"/>, inside the expanded area.</summary>
    [Parameter] public RenderFragment? ItemsHeader { get; set; }

    /// <summary>Optional content shown below <see cref="ItemsContent"/>, inside the expanded area.</summary>
    [Parameter] public RenderFragment? ItemsFooter { get; set; }

    /// <summary>Whether the items area is expanded.</summary>
    [Parameter] public bool IsExpanded { get; set; }

    [Parameter] public EventCallback<bool> IsExpandedChanged { get; set; }

    /// <summary>Fires when the SettingsExpander is opened.</summary>
    [Parameter] public EventCallback Expanded { get; set; }

    /// <summary>Fires when the SettingsExpander is closed.</summary>
    [Parameter] public EventCallback Collapsed { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private bool HasItems => ItemsContent is not null || ItemsHeader is not null || ItemsFooter is not null;

    private Dictionary<string, object> HeaderAttributes => new()
    {
        ["aria-expanded"] = IsExpanded ? "true" : "false",
    };

    private string ChevronClass =>
        "fluent-settings-expander-chevron" + (IsExpanded ? " fluent-settings-expander-chevron--expanded" : "");

    private Task ToggleAsync(MouseEventArgs args) => SetExpandedAsync(!IsExpanded);

    private async Task SetExpandedAsync(bool value)
    {
        if (IsExpanded == value)
        {
            return;
        }

        IsExpanded = value;
        await IsExpandedChanged.InvokeAsync(value);

        if (value)
        {
            await Expanded.InvokeAsync();
        }
        else
        {
            await Collapsed.InvokeAsync();
        }
    }
}
