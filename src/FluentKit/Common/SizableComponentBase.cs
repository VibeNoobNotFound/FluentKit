using Microsoft.AspNetCore.Components;

namespace FluentKit.Common;

/// <summary>
/// Base class for controls that expose XAML-style <c>Padding</c>/<c>Width</c>/<c>Height</c>/
/// <c>MinWidth</c>/<c>MinHeight</c>/<c>MaxWidth</c>/<c>MaxHeight</c> overrides (mirrors
/// FrameworkElement's sizing properties in WPF/WinUI). Every property here is nullable and
/// <see langword="null"/> by default, meaning "unset — fall back to the control's own stylesheet
/// default"; only the ones a consumer actually sets get written into <see cref="SizingStyle"/>.
/// Inheritors render <c>style="@SizingStyle"</c> on their root element (in addition to, not instead
/// of, their existing CSS classes) so overrides win via normal inline-style specificity without
/// needing !important or duplicated CSS.
/// </summary>
public abstract class SizableComponentBase : ComponentBase
{
    /// <summary>Inner spacing between the border and content, all sides. Unset = the control's CSS default.</summary>
    [Parameter] public Thickness? Padding { get; set; }

    [Parameter] public double? Width { get; set; }
    [Parameter] public double? Height { get; set; }
    [Parameter] public double? MinWidth { get; set; }
    [Parameter] public double? MinHeight { get; set; }
    [Parameter] public double? MaxWidth { get; set; }
    [Parameter] public double? MaxHeight { get; set; }

    /// <summary>
    /// Inline CSS built only from the properties that are actually set — e.g. setting only
    /// <see cref="Width"/> emits just <c>"width:120px;"</c>, leaving height/padding/min/max on
    /// their stylesheet defaults. Concatenate this with any control-specific inline style
    /// (put control-specific styles first so overrides here take effect last / win ties).
    /// </summary>
    protected string SizingStyle
    {
        get
        {
            var css = "";
            if (Padding is { } padding) css += $"padding:{padding.ToCss()};";
            if (Width is { } w) css += $"width:{Fmt(w)}px;";
            if (Height is { } h) css += $"height:{Fmt(h)}px;";
            if (MinWidth is { } minW) css += $"min-width:{Fmt(minW)}px;";
            if (MinHeight is { } minH) css += $"min-height:{Fmt(minH)}px;";
            if (MaxWidth is { } maxW) css += $"max-width:{Fmt(maxW)}px;";
            if (MaxHeight is { } maxH) css += $"max-height:{Fmt(maxH)}px;";
            return css;
        }
    }

    private static string Fmt(double value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
