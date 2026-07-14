using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;

namespace FluentKit.Effects;

/// <summary>
/// Approximates WinUI's in-app Acrylic (AcrylicBrush / DesktopAcrylicBackdrop).
///
/// This is a genuinely different material from Mica (see FluentMicaPanel) — Acrylic is
/// TRANSLUCENT and reacts live to whatever's actually rendered behind it (in-app content
/// scrolling underneath, other panels, etc.), not a static sample of the desktop wallpaper.
/// On the web the correct primitive for that is CSS `backdrop-filter`, which live-samples
/// the page content behind the element and blurs it in real time. This component used to be
/// (incorrectly) named FluentMicaPanel — that name now belongs to the actual wallpaper-based
/// static-blur material, which needed a from-scratch implementation instead.
/// </summary>
public enum AcrylicKind
{
    /// <summary>More opaque of the two in-app Acrylic kinds — closest to AcrylicInAppFillColorDefaultBrush.</summary>
    Base,

    /// <summary>More transparent — closest to AcrylicInAppFillColorSecondaryBrush ("Thin" acrylic),
    /// typically used for smaller transient surfaces like flyouts and context menus.</summary>
    Thin
}

public partial class FluentAcrylicBrush : ComponentBase
{
    [Parameter]
    public AcrylicKind Kind { get; set; } = AcrylicKind.Base;

    /// <summary>
    /// Overrides the theme-default tint color (WinUI's AcrylicBrush.TintColor). Accepts any valid
    /// CSS color — hex, rgb()/rgba(), a named color, whatever the consumer already has on hand —
    /// since the actual blending is done with <c>color-mix()</c> rather than requiring an r,g,b triplet.
    /// </summary>
    [Parameter]
    public string? TintColor { get; set; }

    /// <summary>
    /// Overrides the theme-default flat tint wash opacity (WinUI's AcrylicBrush.TintOpacity), 0.0-1.0.
    /// This is the single color layer sitting on top of the live blur.
    /// </summary>
    [Parameter]
    public double? TintOpacity { get; set; }

    /// <summary>
    /// Mirrors WinUI's AcrylicBrush.TintLuminosityOpacity, 0.0-1.0. Real Acrylic composites TWO tint
    /// passes: a luminosity-blended wash against the blurred backdrop (keeps the tint reading
    /// consistently regardless of how light/dark whatever's actually behind the panel is) UNDER the
    /// flat TintOpacity wash above. Left unset, the brush keeps its simpler single-layer tint (the
    /// original behavior) — set this to opt into the closer two-layer approximation.
    /// </summary>
    [Parameter]
    public double? TintLuminosityOpacity { get; set; }

    /// <summary>
    /// Overrides the theme-default fallback color shown when the browser has no backdrop-filter
    /// support, or under prefers-reduced-transparency. Accepts any valid CSS color.
    /// </summary>
    [Parameter]
    public string? FallbackColor { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private string VariantClass => Kind == AcrylicKind.Thin ? "fluent-acrylic--thin" : "fluent-acrylic--base";

    /// <summary>True once a caller opts into the two-layer luminosity approximation described above.</summary>
    private bool HasLuminosityLayer => TintLuminosityOpacity is not null;

    // AdditionalAttributes may itself carry a `style` (every sample/consumer of this component sets
    // one, e.g. position/size on the outer box) — @attributes splatting that dictionary AND writing
    // our own literal `style=` attribute would fight over which one wins, so instead we merge the
    // caller's own style string with our CSS-variable overrides into one computed value, and splat
    // everything else from AdditionalAttributes as normal.
    private string? CombinedStyle
    {
        get
        {
            var callerStyle = AdditionalAttributes is not null
                && AdditionalAttributes.TryGetValue("style", out var styleValue)
                ? styleValue?.ToString()
                : null;

            var overrides = BuildCssVariableOverrides();

            if (string.IsNullOrEmpty(callerStyle))
            {
                return overrides;
            }

            var trimmed = callerStyle.TrimEnd().TrimEnd(';');
            return overrides is null ? trimmed : $"{trimmed};{overrides}";
        }
    }

    private IReadOnlyDictionary<string, object>? SplattedAttributes =>
        AdditionalAttributes is null
            ? null
            : AdditionalAttributes.Count == 0 || !AdditionalAttributes.ContainsKey("style")
                ? AdditionalAttributes
                : AdditionalAttributes.Where(kv => kv.Key != "style").ToDictionary(kv => kv.Key, kv => kv.Value);

    private string? BuildCssVariableOverrides()
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(TintColor))
        {
            parts.Add($"--acrylic-tint-color-override:{TintColor}");
        }

        var tintOpacityPct = ToPercent(TintOpacity);
        if (tintOpacityPct is not null)
        {
            parts.Add($"--acrylic-tint-opacity-pct-override:{tintOpacityPct}");
        }

        var luminosityPct = ToPercent(TintLuminosityOpacity);
        if (luminosityPct is not null)
        {
            parts.Add($"--acrylic-luminosity-opacity-pct-override:{luminosityPct}");
        }

        if (!string.IsNullOrWhiteSpace(FallbackColor))
        {
            parts.Add($"--acrylic-fallback-color-override:{FallbackColor}");
        }

        return parts.Count == 0 ? null : string.Join(';', parts);
    }

    private static string? ToPercent(double? opacity) =>
        opacity is { } value ? $"{(value * 100).ToString(CultureInfo.InvariantCulture)}%" : null;
}
