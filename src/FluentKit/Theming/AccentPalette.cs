using System.Globalization;

namespace FluentKit.Theming;

/// <summary>
/// A plain sRGB color. Kept deliberately dumb (no HSL fields, no WinUI Windows.UI.Color
/// dependency) so this project has zero platform coupling — works identically in WASM,
/// Server, and MAUI Hybrid.
/// </summary>
public readonly record struct RgbColor(byte R, byte G, byte B)
{
    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    /// <summary>"r, g, b" — the comma-separated form CSS needs for <c>rgba(var(--x-rgb), alpha)</c>.</summary>
    public string ToRgbTriplet() => $"{R}, {G}, {B}";

    /// <summary>WCAG relative luminance (0=black, 1=white). Used to decide black-vs-white text on top of this color.</summary>
    public double RelativeLuminance()
    {
        double Linearize(byte c)
        {
            var s = c / 255.0;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Linearize(R) + 0.7152 * Linearize(G) + 0.0722 * Linearize(B);
    }

    /// <summary>True when black text reads better on this color than white text (i.e. the color is "light").</summary>
    public bool PrefersDarkForeground() => RelativeLuminance() > 0.4;

    public static RgbColor Parse(string hex)
    {
        var s = hex.TrimStart('#');
        if (s.Length == 3)
        {
            // shorthand #rgb -> #rrggbb
            s = string.Concat(s.Select(c => new string(c, 2)));
        }

        if (s.Length != 6 || !byte.TryParse(s[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
                           || !byte.TryParse(s[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
                           || !byte.TryParse(s[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            throw new FormatException($"'{hex}' is not a valid #rrggbb color.");
        }

        return new RgbColor(r, g, b);
    }

    public static bool TryParse(string? hex, out RgbColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        try
        {
            color = Parse(hex);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

/// <summary>
/// The 7-step ramp WinUI derives from a single "system accent color": the base color plus three
/// lightened steps (used as the accent ramp in dark theme, where fills/text need to get *brighter*
/// than the base to read well on dark backgrounds) and three darkened steps (used in light theme,
/// where fills/text need to get *darker* than the base to read on light backgrounds).
///
/// NOTE ON FIDELITY: real WinUI (Microsoft.UI.Xaml.Media.AccentColors / the "Fluent XAML Theme
/// Editor") computes this ramp with an internal, undocumented algorithm that isn't public source.
/// This is a from-scratch approximation — simple linear tint/shade mixing in sRGB space — tuned so
/// the default Windows blue (#0078D4) produces a ramp that's visually in the right neighborhood of
/// the real one. It will not byte-for-byte match Windows' own palette for arbitrary colors, but it's
/// perceptually reasonable, monotonic, and (crucially) works for *any* input color, which a lookup
/// table copied from the default blue would not.
/// </summary>
public sealed class AccentPalette
{
    // How far (0..1) each step mixes toward white (light ramp) or black (dark ramp).
    // Chosen empirically against the default Windows accent (#0078D4) — see class remarks.
    private static readonly double[] LightMixRatios = [0.16, 0.32, 0.48];
    private static readonly double[] DarkMixRatios = [0.16, 0.32, 0.48];

    public RgbColor Base { get; }
    public RgbColor Light1 { get; }
    public RgbColor Light2 { get; }
    public RgbColor Light3 { get; }
    public RgbColor Dark1 { get; }
    public RgbColor Dark2 { get; }
    public RgbColor Dark3 { get; }

    /// <summary>The color WinUI falls back to when there's no OS/user accent to read — Windows default blue.</summary>
    public static RgbColor FallbackBase { get; } = RgbColor.Parse("#0078D4");

    public static AccentPalette Fallback { get; } = FromColor(FallbackBase);

    private AccentPalette(RgbColor @base, RgbColor l1, RgbColor l2, RgbColor l3, RgbColor d1, RgbColor d2, RgbColor d3)
    {
        Base = @base;
        Light1 = l1; Light2 = l2; Light3 = l3;
        Dark1 = d1; Dark2 = d2; Dark3 = d3;
    }

    public static AccentPalette FromColor(RgbColor baseColor)
    {
        RgbColor MixToward(RgbColor from, RgbColor to, double t) => new(
            (byte)Math.Round(from.R + (to.R - from.R) * t),
            (byte)Math.Round(from.G + (to.G - from.G) * t),
            (byte)Math.Round(from.B + (to.B - from.B) * t));

        var white = new RgbColor(255, 255, 255);
        var black = new RgbColor(0, 0, 0);

        return new AccentPalette(
            baseColor,
            MixToward(baseColor, white, LightMixRatios[0]),
            MixToward(baseColor, white, LightMixRatios[1]),
            MixToward(baseColor, white, LightMixRatios[2]),
            MixToward(baseColor, black, DarkMixRatios[0]),
            MixToward(baseColor, black, DarkMixRatios[1]),
            MixToward(baseColor, black, DarkMixRatios[2]));
    }

    public static AccentPalette FromHex(string hex) => FromColor(RgbColor.Parse(hex));

    /// <summary>
    /// Every ramp entry as a flat "cssVarName -&gt; value" map: hex for direct use, plus a "-rgb"
    /// sibling per entry holding the comma-separated triplet so callers can build
    /// <c>rgba(var(--accent-base-rgb), 0.5)</c> for translucent fills. Applied to the DOM by
    /// theme-interop.js's <c>applyAccentPalette</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string> ToCssVariables()
    {
        var entries = new (string Name, RgbColor Color)[]
        {
            ("--accent-base", Base),
            ("--accent-light-1", Light1),
            ("--accent-light-2", Light2),
            ("--accent-light-3", Light3),
            ("--accent-dark-1", Dark1),
            ("--accent-dark-2", Dark2),
            ("--accent-dark-3", Dark3),
        };

        var map = new Dictionary<string, string>(entries.Length * 2);
        foreach (var (name, color) in entries)
        {
            map[name] = color.ToHex();
            map[$"{name}-rgb"] = color.ToRgbTriplet();
        }

        return map;
    }
}
