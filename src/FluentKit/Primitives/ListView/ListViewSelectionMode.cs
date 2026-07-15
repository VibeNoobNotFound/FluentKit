namespace FluentKit.Primitives;

/// <summary>
/// Mirrors WinUI ListView's SelectionMode — how many rows can be selected at once, and what
/// visual affordance each row gets. WinUI also has "Extended" (click = single, Ctrl/Shift-click =
/// range/multi), which is a desktop mouse-modifier interaction model that doesn't map cleanly onto
/// a plain click/tap row — left out here in favor of the two modes that do (Single/Multiple),
/// same reasoning ComboBox uses for not exposing multi-select.
/// </summary>
public enum ListViewSelectionMode
{
    /// <summary>Rows are not selectable — ListView behaves as a plain read-only list.</summary>
    None,

    /// <summary>Clicking a row selects it and deselects any previously selected row.</summary>
    Single,

    /// <summary>Clicking a row toggles its own selection independently of the others; each row
    /// shows a checkbox affordance, same as WinUI's Multiple mode.</summary>
    Multiple
}
