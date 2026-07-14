using Microsoft.AspNetCore.Components;

namespace FluentKit.Primitives;

/// <summary>
/// Mirrors WinUI's TextBlock / fluent-svelte's TextBlock: a single component for every text style
/// in the type ramp, each with a sensible default semantic tag (Subtitle -> h4, Body -> span, etc.)
/// that <see cref="Tag"/> can override — e.g. force a Title-styled span instead of an h3.
/// </summary>
public enum TextBlockVariant
{
    Caption,
    Body,
    BodyStrong,
    BodyLarge,
    Subtitle,
    Title,
    TitleLarge,
    Display
}

public partial class FluentTextBlock : ComponentBase
{
    [Parameter]
    public TextBlockVariant Variant { get; set; } = TextBlockVariant.Body;

    /// <summary>
    /// Overrides the default HTML tag for this variant. Supported: span, div, p, h1-h6.
    /// </summary>
    [Parameter]
    public string? Tag { get; set; }

    [Parameter]
    public string? Class { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private string ResolvedTag => Tag ?? Variant switch
    {
        TextBlockVariant.BodyStrong => "h5",
        TextBlockVariant.BodyLarge => "h5",
        TextBlockVariant.Subtitle => "h4",
        TextBlockVariant.Title => "h3",
        TextBlockVariant.TitleLarge => "h2",
        TextBlockVariant.Display => "h1",
        _ => "span"
    };

    private string VariantClass => Variant switch
    {
        TextBlockVariant.Caption => "fluent-textblock--caption",
        TextBlockVariant.BodyStrong => "fluent-textblock--body-strong",
        TextBlockVariant.BodyLarge => "fluent-textblock--body-large",
        TextBlockVariant.Subtitle => "fluent-textblock--subtitle",
        TextBlockVariant.Title => "fluent-textblock--title",
        TextBlockVariant.TitleLarge => "fluent-textblock--title-large",
        TextBlockVariant.Display => "fluent-textblock--display",
        _ => "fluent-textblock--body"
    };
}
