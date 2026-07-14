using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Fluent.Blazor.Overlay;
using Fluent.Blazor.Primitives;

namespace Fluent.Blazor.Composite;

/// <summary>
/// Mirrors WinUI's SplitButton — a primary action region (fires <see cref="OnClick"/> immediately,
/// like a plain button) joined to a separate narrow chevron region that opens a MenuFlyout of
/// related actions, rendered as one visually-continuous pill (shared border, no gap, only the
/// chevron side has rounded outer corners on its own edge — see .razor.css). Unlike
/// <see cref="FluentDropDownButton"/>, which is nothing but a menu trigger, SplitButton's main
/// region is a real independent action.
///
/// FluentMenuFlyout's trigger wraps the WHOLE two-button pill (not just the chevron) so its anchor
/// width is the full control's width — needed for <c>MatchAnchorWidth</c> to stretch the menu to
/// the button's own width rather than the chevron's ~32px. The action button stops its click from
/// bubbling to that wrapping trigger (<c>@@onclick:stopPropagation</c>) so clicking it fires only
/// <see cref="OnClick"/>; the chevron has no handler of its own and relies on the bubble to open
/// the menu.
/// </summary>
public partial class FluentSplitButton : ComponentBase
{
    [Parameter] public RenderFragment? ChildContent { get; set; }

    [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }

    /// <summary>The chevron's menu items — FluentMenuFlyoutItem/FluentMenuFlyoutDivider elements.</summary>
    [Parameter, EditorRequired] public RenderFragment MenuItems { get; set; } = default!;

    [Parameter] public ButtonVariant Variant { get; set; } = ButtonVariant.Standard;

    [Parameter] public bool Disabled { get; set; }

    [Parameter] public OverlayPlacement Placement { get; set; } = OverlayPlacement.Bottom;

    [Parameter] public bool IsOpen { get; set; }

    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private string VariantClass => Variant switch
    {
        ButtonVariant.Accent => "fluent-splitbutton--accent",
        ButtonVariant.Subtle => "fluent-splitbutton--subtle",
        _ => "fluent-splitbutton--standard"
    };
}
