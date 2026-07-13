using Microsoft.AspNetCore.Components;

namespace Fluent.Blazor.Effects;

/// <summary>
/// Approximates WinUI's Mica material — rebuilt from scratch against the actual recipe in
/// microsoft-ui-xaml's SystemBackdropComponentInternal::BuildMicaEffectBrush
/// (dev/dll/SystemBackdrop/SystemBackdropBrushFactory.cpp). This is NOT the same thing as Acrylic
/// (see FluentAcrylicBrush, which is what this component used to be before this rewrite — Acrylic
/// live-blurs whatever's actually behind it via backdrop-filter; Mica does not).
///
/// The real WinUI effect graph, translated to what it actually does (its own source has a code
/// comment noting BlendEffectMode::Luminosity and BlendEffectMode::Color are swapped due to a bug,
/// so this description uses the TRUE blend semantics, not their mislabeled enum values):
///
///   1. Start from a heavily blurred copy of the desktop wallpaper (ICompositorWithBlurredWallpaper-
///      BackdropBrush — an OS compositor API that samples the wallpaper only, not other windows).
///      Browsers can't reach the real wallpaper, so <see cref="BackgroundImageUrl"/> stands in for
///      it — supply any image (ideally the user's own wallpaper file) and it's blurred the same way.
///   2. LUMINOSITY blend pass: blend TintColor (alpha = LuminosityOpacity) over the blurred
///      wallpaper using true "luminosity" blend mode — this keeps the wallpaper's hue/saturation but
///      replaces its per-pixel luminosity with the tint's. Mica's defaults set LuminosityOpacity to
///      1.0, so this pass ends up governing almost the entire final look.
///   3. COLOR blend pass: blend TintColor (alpha = TintOpacity) over the result of step 2 using true
///      "color" blend mode — this keeps the TINT's hue/saturation but takes luminosity from step 2's
///      result, then composites at TintOpacity. This is what actually gives Mica its "muted,
///      personalized" gradient-ish look Microsoft's docs describe: the wallpaper's light/dark shape
///      survives (that's the "gradient blur from the image" feel), but its color is replaced by the
///      theme tint, at a strength controlled by TintOpacity.
///   4. A fine noise texture on top, same as Acrylic.
///
/// With no BackgroundImageUrl supplied (or a format the browser can't decode, e.g. HEIC outside
/// Safari), this degrades to WinUI's own documented fallback: a flat SolidBackgroundFillColorBase
/// (Alt) fill — the same state real Mica falls back to under RDP, low-power GPUs, or Battery Saver.
/// </summary>
public enum MicaVariant
{
    /// <summary>SolidBackgroundFillColorBase — the default Mica used for a window's main surface.</summary>
    Base,

    /// <summary>Mica Alt — stronger tinting of the wallpaper, used for layered/commanding surfaces
    /// (tabbed title bars, nav panes) that need to read as visually deeper than a Base-Mica window.</summary>
    BaseAlt
}

public partial class FluentMicaPanel : ComponentBase
{
    [Parameter]
    public MicaVariant Variant { get; set; } = MicaVariant.Base;

    /// <summary>
    /// Stand-in for the desktop wallpaper WinUI would otherwise sample directly via the OS
    /// compositor. Any image URL works (relative to wwwroot, or absolute). When omitted, the panel
    /// falls back to WinUI's own documented solid-color fallback.
    /// </summary>
    [Parameter]
    public string? BackgroundImageUrl { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private string VariantClass => Variant == MicaVariant.Base ? "fluent-mica--alt" : "fluent-mica--base";
}
