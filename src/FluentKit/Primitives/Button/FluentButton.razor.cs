using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FluentKit.Primitives;

/// <summary>
/// Mirrors WinUI's Button / AccentButton (via the IsAccent-equivalent style) / HyperlinkButton split.
/// "Subtle" matches what WinUI calls a SubtleButton-styled Button (no border, transparent by default).
/// </summary>
public enum ButtonVariant
{
    Standard,
    Accent,
    Subtle,
    Hyperlink
}

public partial class FluentButton : ComponentBase
{
    [Parameter]
    public ButtonVariant Variant { get; set; } = ButtonVariant.Standard;

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public EventCallback<MouseEventArgs> OnClick { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private string VariantClass => Variant switch
    {
        ButtonVariant.Accent => "fluent-button--accent",
        ButtonVariant.Subtle => "fluent-button--subtle",
        ButtonVariant.Hyperlink => "fluent-button--hyperlink",
        _ => "fluent-button--standard"
    };
}
