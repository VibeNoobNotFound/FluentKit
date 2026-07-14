using Microsoft.AspNetCore.Components;

namespace FluentKit.Primitives;

/// <summary>
/// Generic surface primitive — not a literal WinUI control (WinUI doesn't ship a "Card"), but the
/// CardBackgroundFillColorDefault / CardStrokeColorDefault tokens exist precisely because so many
/// real controls (Settings cards, NavigationView content, Expander header/content) are built on
/// this exact surface. Everything else that needs a bordered/tinted panel should compose this
/// rather than repeat the background/border/radius trio inline.
/// </summary>
public partial class FluentCard : ComponentBase
{
    [Parameter] public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }
}
