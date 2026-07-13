using Microsoft.AspNetCore.Components;

namespace Fluent.Blazor.Effects;

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

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private string VariantClass => Kind == AcrylicKind.Thin ? "fluent-acrylic--thin" : "fluent-acrylic--base";
}
