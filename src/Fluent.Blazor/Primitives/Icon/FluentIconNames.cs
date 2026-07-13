namespace Fluent.Blazor.Primitives;

/// <summary>
/// Compile-time-checked constants for commonly used Fluent System Icons glyph names, for use with
/// <see cref="FluentIcon.Name"/> (e.g. <c>Name="@FluentIconNames.Home"</c>). Not exhaustive — the
/// font (wwwroot/Icons/FluentSystemIcons-Regular.css) has thousands of glyphs; for anything not
/// listed here, pass the "icon-" class suffix straight from that file, e.g.
/// <c>Name="ic_fluent_beaker_20_regular"</c>.
/// </summary>
public static class FluentIconNames
{
    public const string Home = "ic_fluent_home_20_regular";
    public const string Document = "ic_fluent_document_20_regular";
    public const string Apps = "ic_fluent_apps_20_regular";
    public const string Settings = "ic_fluent_settings_20_regular";
    public const string Dismiss = "ic_fluent_dismiss_20_regular";
    public const string Add = "ic_fluent_add_20_regular";
    public const string Search = "ic_fluent_search_20_regular";
    public const string ChevronDown = "ic_fluent_chevron_down_20_regular";
    public const string ChevronUp = "ic_fluent_chevron_up_20_regular";
    public const string Subtract = "ic_fluent_subtract_20_regular";
    public const string ChevronRight = "ic_fluent_chevron_right_20_regular";
    public const string Checkmark = "ic_fluent_checkmark_20_regular";
    public const string ArrowLeft = "ic_fluent_arrow_left_20_regular";
    public const string ArrowRight = "ic_fluent_arrow_right_20_regular";
    public const string Calendar = "ic_fluent_calendar_20_regular";
    public const string Delete = "ic_fluent_delete_20_regular";

    /// <summary>The "hamburger"/GlobalNavButton glyph — used by NavigationView's pane-toggle button.</summary>
    public const string Navigation = "ic_fluent_navigation_20_regular";
}
