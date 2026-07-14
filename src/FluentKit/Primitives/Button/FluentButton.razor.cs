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

    /// <summary>
    /// The URL to navigate to. Setting this renders the button as an &lt;a&gt; element instead of
    /// a &lt;button&gt; — required for links to actually work (a &lt;button&gt; ignores href entirely).
    /// Typically paired with <see cref="ButtonVariant.Hyperlink"/> for WinUI HyperlinkButton parity,
    /// but Href works with any variant if you want an accent/standard-styled button that's really a link.
    /// </summary>
    [Parameter]
    public string? Href { get; set; }

    /// <summary>
    /// Anchor "target" attribute. Defaults to "_blank" (opens in a new tab) whenever <see cref="Href"/>
    /// is set, matching typical hyperlink-button expectations. Set to "_self" to navigate in-place.
    /// </summary>
    [Parameter]
    public string Target { get; set; } = "_blank";

    [Parameter]
    public EventCallback<MouseEventArgs> OnClick { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private bool IsLink => !string.IsNullOrEmpty(Href);

    private bool IsExternalTarget => string.Equals(Target, "_blank", StringComparison.OrdinalIgnoreCase);

    private string VariantClass => Variant switch
    {
        ButtonVariant.Accent => "fluent-button--accent",
        ButtonVariant.Subtle => "fluent-button--subtle",
        ButtonVariant.Hyperlink => "fluent-button--hyperlink",
        _ => "fluent-button--standard"
    };
}
