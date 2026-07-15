using FluentKit.Composite;

namespace FluentKit.Sample.Shared;

/// <summary>
/// One searchable entry — mirrors what WinUI Gallery's search index shows: a control/page name,
/// the section it lives under (for a bit of context in results), and the route to navigate to.
/// </summary>
public sealed record GallerySearchEntry(string Name, string Section, string Route);

/// <summary>
/// Static list of every page in the gallery, used both by the AutoSuggestBox in the nav pane
/// header (type-ahead suggestions) and by <see cref="Pages.SearchResultsPage"/> (Enter-to-search).
/// Kept as one flat list rather than re-walking the NavigationView tree at runtime — it's small,
/// hand-maintained, and avoids coupling the search index to MainLayout's markup structure.
/// </summary>
public static class GallerySearchIndex
{
    public static readonly IReadOnlyList<GallerySearchEntry> Entries =
    [
        new("Home", "Get Started", ""),

        new("Button", "Basic Input", "controls/button"),
        new("ToggleButton", "Basic Input", "controls/togglebutton"),
        new("IconButton", "Basic Input", "controls/iconbutton"),
        new("DropDownButton", "Basic Input", "controls/dropdownbutton"),
        new("SplitButton", "Basic Input", "controls/splitbutton"),

        new("CheckBox", "Basic Input", "controls/checkbox"),
        new("RadioButton", "Basic Input", "controls/radiobutton"),
        new("ToggleSwitch", "Basic Input", "controls/toggleswitch"),
        new("Slider", "Basic Input", "controls/slider"),

        new("TextBlock", "Basic Input", "controls/textblock"),
        new("TextBox", "Basic Input", "controls/textbox"),
        new("PasswordBox", "Basic Input", "controls/passwordbox"),
        new("NumberBox", "Basic Input", "controls/numberbox"),
        new("AutoSuggestBox", "Basic Input", "controls/autosuggestbox"),

        new("ProgressBar", "Status & Feedback", "controls/progressbar"),
        new("ProgressRing", "Status & Feedback", "controls/progressring"),
        new("InfoBar", "Status & Feedback", "controls/infobar"),
        new("InfoBadge", "Status & Feedback", "controls/infobadge"),
        new("Tooltip", "Status & Feedback", "controls/tooltip"),
        new("TeachingTip", "Status & Feedback", "controls/teachingtip"),

        new("ListView", "Collections", "controls/listview"),
        new("ComboBox", "Collections", "controls/combobox"),
        new("Calendar & DatePicker", "Collections", "controls/calendar"),
        new("TimePicker", "Collections", "controls/timepicker"),

        new("NavigationView", "Navigation", "controls/navigationview"),
        new("Pivot", "Navigation", "controls/pivot"),
        new("Expander", "Navigation", "controls/expander"),

        new("Flyout", "Flyouts & Menus", "controls/flyout"),
        new("MenuFlyout", "Flyouts & Menus", "controls/menuflyout"),
        new("ContextMenu", "Flyouts & Menus", "controls/contextmenu"),
        new("MenuBar", "Flyouts & Menus", "controls/menubar"),
        new("ContentDialog", "Flyouts & Menus", "controls/contentdialog"),

        new("Card", "Layout", "controls/card"),
        new("SettingsCard", "Settings", "controls/settingscard"),
        new("SettingsExpander", "Settings", "controls/settingsexpander"),
        new("PersonPicture", "Layout", "controls/personpicture"),

        new("Acrylic", "Effects & Materials", "controls/acrylic"),
        new("Mica", "Effects & Materials", "controls/mica"),
        new("Reveal", "Effects & Materials", "controls/reveal"),

        new("Icons", "Design", "design/icons"),

        new("Settings", "Get Started", "settings"),
    ];

    /// <summary>AutoSuggestBox items built from <see cref="Entries"/>, Value = route.</summary>
    public static readonly IReadOnlyList<AutoSuggestBoxItem<string>> SuggestionItems =
        Entries.Select(e => new AutoSuggestBoxItem<string>(e.Name, e.Route)).ToList();

    /// <summary>Same "contains, case-insensitive" matching AutoSuggestBox uses by default,
    /// reused here so the results page and the dropdown agree on what counts as a match.</summary>
    public static IReadOnlyList<GallerySearchEntry> Search(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        return Entries
            .Where(e => e.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || e.Section.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
