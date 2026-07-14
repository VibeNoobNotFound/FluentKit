using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FluentKit.Primitives;

/// <summary>
/// Ported from fluent-svelte's IconButton.svelte — bare icon-only button, transparent by default,
/// same subtle hover/press ramp as FluentButton's Subtle variant but no min-width/label padding
/// (min 30x30 box, icon centered). Renders as an anchor when <see cref="Href"/> is set (matching
/// svelte's <c>svelte:element this={href ? "a" : "button"}</c> element-swap), otherwise a button.
/// </summary>
public partial class FluentIconButton : ComponentBase
{
    /// <summary>When set (and not Disabled), renders an &lt;a&gt; instead of a &lt;button&gt;.</summary>
    [Parameter] public string? Href { get; set; }

    [Parameter] public bool Disabled { get; set; }

    [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private bool IsAnchor => !string.IsNullOrEmpty(Href) && !Disabled;
}
