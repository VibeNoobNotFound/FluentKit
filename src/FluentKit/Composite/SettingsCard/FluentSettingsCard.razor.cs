using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FluentKit.Composite;

/// <summary>
/// 1:1 port of the Windows Community Toolkit's SettingsCard
/// (CommunityToolkit.WinUI.Controls.SettingsCard) — the base building block for consistent
/// settings UI: an optional leading icon, a header/description stack, an end-aligned content
/// slot (a ToggleSwitch, ComboBox, Button, etc.), and an optional trailing action icon (a chevron
/// by default) that appears once the whole card is made clickable via <see cref="IsClickEnabled"/>.
/// A FluentSettingsCard can also be hosted as an item inside a <c>FluentSettingsExpander</c>.
///
/// WinUI's version reacts to available width with three extra VisualStates (RightWrapped,
/// RightWrappedNoIcon, Vertical) driven by a ControlSizeTrigger. Blazor has no direct equivalent
/// of that trigger, so the same three breakpoints (476px / 286px) are reproduced with CSS
/// container queries on the card's own inline size in FluentSettingsCard.razor.css instead of
/// C#-side layout code.
/// </summary>
public partial class FluentSettingsCard : ComponentBase
{
    /// <summary>Header text. Ignored when <see cref="HeaderContent"/> is supplied.</summary>
    [Parameter] public string? Header { get; set; }

    /// <summary>Header content, for anything beyond plain text.</summary>
    [Parameter] public RenderFragment? HeaderContent { get; set; }

    /// <summary>Description text (shown under the header, secondary/caption styling). Ignored when <see cref="DescriptionContent"/> is supplied.</summary>
    [Parameter] public string? Description { get; set; }

    /// <summary>Description content, for anything beyond plain text (e.g. a HyperlinkButton).</summary>
    [Parameter] public RenderFragment? DescriptionContent { get; set; }

    /// <summary>The icon shown at the leading edge of the card.</summary>
    [Parameter] public RenderFragment? HeaderIcon { get; set; }

    /// <summary>
    /// The end-aligned content of the card (maps to WCT's <c>Content</c> property — a
    /// ToggleSwitch, Button, ComboBox, etc.). Named ChildContent so it can be passed as the
    /// component's ordinary child markup.
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// The icon shown at the trailing edge when <see cref="IsClickEnabled"/> and
    /// <see cref="IsActionIconVisible"/> are both true. Defaults to a chevron, same as WCT.
    /// </summary>
    [Parameter] public RenderFragment? ActionIcon { get; set; }

    /// <summary>Tooltip for <see cref="ActionIcon"/>.</summary>
    [Parameter] public string? ActionIconToolTip { get; set; }

    /// <summary>Whether the ActionIcon is shown (only relevant while <see cref="IsClickEnabled"/> is true).</summary>
    [Parameter] public bool IsActionIconVisible { get; set; } = true;

    /// <summary>
    /// Makes the whole card behave like a button — pointer/press visual states, keyboard
    /// activation (Enter/Space), and the trailing chevron/action icon become visible.
    /// </summary>
    [Parameter] public bool IsClickEnabled { get; set; }

    /// <summary>Fires when the card is activated (click, or Enter/Space while focused) and <see cref="IsClickEnabled"/> is true.</summary>
    [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }

    /// <summary>The alignment of ChildContent. Mirrors WCT's ContentAlignment.</summary>
    [Parameter] public SettingsCardContentAlignment ContentAlignment { get; set; } = SettingsCardContentAlignment.Right;

    /// <summary>Disables the card — dims foreground/icons, same as WinUI's Disabled visual state.</summary>
    [Parameter] public bool Disabled { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private bool HasHeader => HeaderContent is not null || !string.IsNullOrEmpty(Header);

    private bool HasDescription => DescriptionContent is not null || !string.IsNullOrEmpty(Description);

    private RenderFragment RenderedHeader => HeaderContent ?? (builder => builder.AddContent(0, Header));

    private RenderFragment RenderedDescription => DescriptionContent ?? (builder => builder.AddContent(0, Description));

    private bool ShowActionIcon => IsClickEnabled && IsActionIconVisible;

    private string RootClass =>
        "fluent-settings-card"
        + (IsClickEnabled ? " fluent-settings-card--clickable" : "")
        + (Disabled ? " fluent-settings-card--disabled" : "")
        + (ContentAlignment switch
        {
            SettingsCardContentAlignment.Left => " fluent-settings-card--left",
            SettingsCardContentAlignment.Vertical => " fluent-settings-card--vertical",
            _ => "",
        });

    private async Task HandleClickAsync(MouseEventArgs args)
    {
        if (!IsClickEnabled || Disabled)
        {
            return;
        }

        await OnClick.InvokeAsync(args);
    }

    private async Task HandleKeyDownAsync(KeyboardEventArgs args)
    {
        if (!IsClickEnabled || Disabled)
        {
            return;
        }

        if (args.Key is "Enter" or " ")
        {
            await OnClick.InvokeAsync(new MouseEventArgs());
        }
    }
}

/// <summary>The alignment of a FluentSettingsCard's ChildContent. Mirrors WCT's ContentAlignment enum.</summary>
public enum SettingsCardContentAlignment
{
    /// <summary>ChildContent is end-aligned. Default state.</summary>
    Right,

    /// <summary>
    /// ChildContent is start-aligned while the header icon, header and description are hidden.
    /// Commonly used for content such as CheckBoxes, RadioButtons and custom layouts.
    /// </summary>
    Left,

    /// <summary>ChildContent is stacked below the header/description.</summary>
    Vertical,
}
