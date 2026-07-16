namespace FluentKit.Theming;

/// <summary>
/// DI-registered (same lifetime rules as <see cref="IThemeService"/> — Scoped everywhere) service
/// that owns the app's accent color: the single base color WinUI derives its whole 7-step accent
/// ramp from (see <see cref="AccentPalette"/>), and pushes that ramp into CSS custom properties on
/// <c>:root</c> so every FluentKit component that reads <c>--accent-*</c> updates live.
///
/// Three ways to set it, matching how Windows itself offers accent color:
///  - <see cref="SetAccentAsync"/> — explicit color, e.g. from a color picker.
///  - <see cref="SetAccentFromImageAsync"/> — derive it from a wallpaper/photo, like Windows'
///    "Automatically pick an accent color from my background".
///  - Do nothing — <see cref="InitializeAsync"/> applies <see cref="AccentPalette.Fallback"/>
///    (Windows' own default blue) so the app never renders with an unset accent.
/// </summary>
public interface IAccentColorService
{
    /// <summary>The current base accent color (before ramp expansion).</summary>
    RgbColor CurrentAccent { get; }

    /// <summary>The full derived 7-step ramp for <see cref="CurrentAccent"/>.</summary>
    AccentPalette CurrentPalette { get; }

    /// <summary>Fires whenever the accent changes, after the new ramp has been pushed to the DOM.</summary>
    event Action? AccentChanged;

    /// <summary>Applies the fallback blue so accent CSS variables exist before any explicit choice is made. Safe to call multiple times.</summary>
    Task InitializeAsync();

    /// <summary>Sets the accent to an explicit color (e.g. "#0078D4" or "#07D").</summary>
    /// <exception cref="FormatException">The string isn't a valid #rgb/#rrggbb color.</exception>
    Task SetAccentAsync(string hexColor);

    /// <summary>
    /// Derives an accent color from an image (any URL a same-origin &lt;img&gt; can load: relative
    /// path, absolute URL, or data: URL) by sampling its pixels and picking a representative,
    /// saturation-weighted color — then applies the ramp derived from that color.
    /// Falls back to <see cref="AccentPalette.Fallback"/> (Windows default blue) if the image can't
    /// be read (e.g. blocked by CORS, failed to load) rather than throwing, since this is meant to
    /// be called opportunistically whenever a wallpaper changes.
    /// </summary>
    Task SetAccentFromImageAsync(string imageUrl);
}
