using Microsoft.AspNetCore.Components;
using Fluent.Blazor.Overlay;
using Fluent.Blazor.Primitives;

namespace Fluent.Blazor.Composite;

/// <summary>
/// Mirrors WinUI's DropDownButton — a button that always opens a MenuFlyout rather than firing a
/// click action itself (unlike SplitButton, which splits those two concerns into separate regions).
/// Thin composition over <see cref="FluentMenuFlyout"/> with a button-styled, chevron-suffixed
/// trigger in place of MenuFlyout's plain <c>ChildContent</c> span.
/// </summary>
public partial class FluentDropDownButton : ComponentBase
{
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>The menu's items — FluentMenuFlyoutItem/FluentMenuFlyoutDivider elements.</summary>
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
        ButtonVariant.Accent => "fluent-dropdownbutton--accent",
        ButtonVariant.Subtle => "fluent-dropdownbutton--subtle",
        _ => "fluent-dropdownbutton--standard"
    };
}
