using Microsoft.AspNetCore.Components;

namespace Fluent.Blazor.Primitives;

/// <summary>
/// Ported from fluent-svelte's PersonPicture.svelte — round avatar. Shows &lt;img Src&gt; when set
/// and loads successfully; falls back to initials derived from <see cref="Alt"/> (or ChildContent,
/// or a Badge slot) otherwise. Mirrors svelte's own <c>error</c> local-state-on-img-error fallback:
/// if the image 404s/fails to decode, this flips to the initials div instead of showing a broken
/// image icon, same as the source's <c>on:error={() => (error = true)}</c>.
/// </summary>
public partial class FluentPersonPicture : ComponentBase
{
    /// <summary>Diameter in pixels of the round picture.</summary>
    [Parameter] public int Size { get; set; } = 72;

    [Parameter] public string? Src { get; set; }

    /// <summary>Alt text for the image, and the source for the initials fallback (first letter of
    /// each space-separated word, uppercased) when there's no image or no <see cref="ChildContent"/>.</summary>
    [Parameter] public string? Alt { get; set; }

    /// <summary>Overrides the initials fallback with custom content (e.g. a glyph).</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Optional badge slot rendered in the top-right corner (e.g. a presence dot or
    /// notification count), matching fluent-svelte's <c>slot="badge"</c>.</summary>
    [Parameter] public RenderFragment? Badge { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private bool _imageErrored;

    private bool ShowImage => !string.IsNullOrEmpty(Src) && !_imageErrored;

    private string Initials => string.IsNullOrWhiteSpace(Alt)
        ? string.Empty
        : string.Concat(Alt
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0])));

    protected override void OnParametersSet()
    {
        if (!string.IsNullOrEmpty(Src))
        {
            _imageErrored = false;
        }
    }

    private void OnImageError() => _imageErrored = true;
}
