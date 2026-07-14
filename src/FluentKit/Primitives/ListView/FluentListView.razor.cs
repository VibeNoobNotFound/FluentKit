using Microsoft.AspNetCore.Components;

namespace FluentKit.Primitives;

/// <summary>
/// Mirrors WinUI's ListView as a plain vertical item container: it owns roving-focus arrow-key
/// navigation (Up/Down move focus between items, Home/End jump to the ends) but does NOT own
/// selection state itself — that's deliberate. WinUI's ListView has SelectedItem built in, but
/// composite consumers (FluentNavigationView's pane) need selection driven by their own context
/// (NavigationViewContext) instead, same reasoning as RadioGroup owning RadioButton's selection.
/// Items register via IFocusableListItem, not a concrete type — see that interface's doc comment.
/// </summary>
public partial class FluentListView : ComponentBase
{
    /// <summary>ARIA role for the container. "list" for a plain list, "listbox" if items are meant to read as a single-select widget to AT.</summary>
    [Parameter]
    public string Role { get; set; } = "list";

    /// <summary>Extra class(es) appended to the root element. A plain parameter (not attribute splatting) so it composes instead of clobbering the base "fluent-list-view" class.</summary>
    [Parameter]
    public string? Class { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private readonly List<IFocusableListItem> _items = new();

    internal void Register(IFocusableListItem item) => _items.Add(item);

    internal void Unregister(IFocusableListItem item) => _items.Remove(item);

    internal async Task FocusAdjacentAsync(IFocusableListItem current, int direction)
    {
        var index = _items.IndexOf(current);
        if (index < 0)
        {
            return;
        }

        for (var i = index + direction; i >= 0 && i < _items.Count; i += direction)
        {
            if (!_items[i].Disabled)
            {
                await _items[i].FocusAsync();
                return;
            }
        }
    }

    internal async Task FocusEndAsync(bool start)
    {
        var range = start ? _items : Enumerable.Reverse(_items);
        foreach (var item in range)
        {
            if (!item.Disabled)
            {
                await item.FocusAsync();
                return;
            }
        }
    }
}
