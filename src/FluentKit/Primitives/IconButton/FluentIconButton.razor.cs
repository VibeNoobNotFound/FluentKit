using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FluentKit.Primitives;

/// <summary>
/// Ported from fluent-svelte's IconButton.svelte — bare icon-only button, fixed square box
/// (icon centered, no label padding). Renders as an anchor when <see cref="Href"/> is set
/// (matching svelte's <c>svelte:element this={href ? "a" : "button"}</c> element-swap),
/// otherwise a button. Supports the same <see cref="ButtonVariant"/> set as <see cref="FluentButton"/>
/// (Standard/Accent/Subtle/Hyperlink) so icon-only and labeled buttons stay visually consistent —
/// defaults to Subtle to preserve the original transparent-by-default look.
/// </summary>
public partial class FluentIconButton : ComponentBase
{
    [Parameter]
    public ButtonVariant Variant { get; set; } = ButtonVariant.Subtle;

    /// <summary>When set (and not Disabled), renders an &lt;a&gt; instead of a &lt;button&gt;.</summary>
    [Parameter] public string? Href { get; set; }

    [Parameter] public bool Disabled { get; set; }

    [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private bool IsAnchor => !string.IsNullOrEmpty(Href) && !Disabled;

    private string VariantClass => Variant switch
    {
        ButtonVariant.Accent => "fluent-icon-button--accent",
        ButtonVariant.Standard => "fluent-icon-button--standard",
        ButtonVariant.Hyperlink => "fluent-icon-button--hyperlink",
        _ => "fluent-icon-button--subtle"
    };
}
