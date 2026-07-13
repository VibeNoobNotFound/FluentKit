using Microsoft.AspNetCore.Components;

namespace Fluent.Blazor.Primitives;

/// <summary>
/// Renders a glyph from the MIT-licensed Fluent System Icons webfont
/// (https://github.com/microsoft/fluentui-system-icons, © Microsoft Corporation) — see
/// wwwroot/Icons/FluentSystemIcons-Regular.css. Replaces the earlier approach of wrapping SVG
/// <c>Icon</c> instances from Microsoft.FluentUI.AspNetCore.Components.Icons: that package pulls
/// in the full Microsoft.FluentUI.AspNetCore.Components core RCL as a transitive dependency,
/// which auto-bundles its own scoped CSS into the host app's .styles.css and visually clashes with
/// our tokens (see git history for the multi-hour static-web-assets fight that caused). The font
/// approach has zero managed dependency and can't leak component CSS since it's just a CSS class.
///
/// <c>Name</c> is the glyph identity as published in the font's own CSS, minus the "icon-" prefix
/// — e.g. "ic_fluent_home_24_regular", "ic_fluent_settings_20_regular". Use
/// <see cref="FluentIconNames"/> for compile-time-checked constants covering common glyphs, or
/// copy any class name straight out of wwwroot/Icons/FluentSystemIcons-Regular.css / the font's
/// own icon gallery (fluentui-system-icons repo) for anything not listed there. The glyph's own
/// baked-in size suffix (_16/_20/_24/...) only affects which pixel grid it was drawn on — <see
/// cref="Size"/> below independently controls the actual rendered box, any combination works, but
/// picking the closest baked-in size gives the crispest result.
///
/// Decorative by default (aria-hidden) since icons are almost always paired with visible text (see
/// NavigationViewItem); pass aria-hidden="false" plus your own aria-label via
/// AdditionalAttributes for icon-only usage.
///
/// Consumers must link wwwroot/Icons/FluentSystemIcons-Regular.css once in their host page
/// (e.g. index.html / App.razor), same as tokens.css:
/// &lt;link rel="stylesheet" href="_content/Fluent.Blazor/Icons/FluentSystemIcons-Regular.css" /&gt;
/// </summary>
public partial class FluentIcon : ComponentBase
{
    [Parameter, EditorRequired]
    public string Name { get; set; } = default!;

    /// <summary>Pixel size for both width and height of the icon box, and its font-size (the glyph
    /// scales with the font box). Independent of whatever size is baked into <see cref="Name"/>.</summary>
    [Parameter]
    public int Size { get; set; } = 20;

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private string IconClassSuffix => Name;

    private string SizeStyle => $"width:{Size}px;height:{Size}px;font-size:{Size}px;";
}
