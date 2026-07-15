using Microsoft.AspNetCore.Components;

namespace FluentKit.Primitives;

/// <summary>
/// Mirrors WinUI's ListView as a vertical item container. Owns roving-focus arrow-key navigation
/// (Up/Down move focus between items, Home/End jump to the ends) via IFocusableListItem, same as
/// before. Two composition modes, chosen by whether <see cref="Items"/> is set:
///   - Data-bound (Items set): rows are generated from <see cref="Items"/>, with selection driven
///     by <see cref="SelectionMode"/> and the SelectedValue/SelectedValues parameters below — same
///     "Items + optional ItemTemplate" shape as FluentComboBox/FluentAutoSuggestBox.
///   - Freeform (Items left null): the original ChildContent-only mode, where the caller places
///     FluentListViewItem children manually and owns Selected/OnSelect itself (e.g. when selection
///     needs to be driven by external context, the way FluentNavigationView's pane does it).
/// TValue only matters for the data-bound mode — freeform callers can use any TValue (e.g.
/// "object") since it's never referenced.
/// </summary>
public partial class FluentListView<TValue> : ComponentBase, IListViewHost
{
    /// <summary>ARIA role for the container. "list" for a plain list, "listbox" if items are meant to read as a single-select widget to AT.</summary>
    [Parameter]
    public string Role { get; set; } = "list";

    /// <summary>Extra class(es) appended to the root element. A plain parameter (not attribute splatting) so it composes instead of clobbering the base "fluent-list-view" class.</summary>
    [Parameter]
    public string? Class { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Data source for the data-bound mode. When set, rows are generated from this list
    /// instead of <see cref="ChildContent"/> — see the type's own doc comment for the two modes.</summary>
    [Parameter] public IReadOnlyList<ListViewItem<TValue>>? Items { get; set; }

    /// <summary>Optional custom row content for each item. Falls back to a plain
    /// <c>@item.Name</c> when not supplied, same default ComboBox/AutoSuggestBox use. Only
    /// consulted when <see cref="Items"/> is set.</summary>
    [Parameter] public RenderFragment<ListViewItem<TValue>>? ItemTemplate { get; set; }

    /// <summary>How many rows can be selected at once. Only meaningful in data-bound mode —
    /// freeform mode's selection is entirely caller-driven via each FluentListViewItem's own
    /// Selected/OnSelect.</summary>
    [Parameter] public ListViewSelectionMode SelectionMode { get; set; } = ListViewSelectionMode.None;

    /// <summary>Currently selected value in <see cref="ListViewSelectionMode.Single"/> mode. Two-way bindable.</summary>
    [Parameter] public TValue? SelectedValue { get; set; }

    [Parameter] public EventCallback<TValue?> SelectedValueChanged { get; set; }

    /// <summary>Currently selected values in <see cref="ListViewSelectionMode.Multiple"/> mode. Two-way bindable.</summary>
    [Parameter] public IReadOnlyList<TValue>? SelectedValues { get; set; }

    [Parameter] public EventCallback<IReadOnlyList<TValue>> SelectedValuesChanged { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private readonly List<IFocusableListItem> _items = new();

    void IListViewHost.Register(IFocusableListItem item) => _items.Add(item);

    void IListViewHost.Unregister(IFocusableListItem item) => _items.Remove(item);

    async Task IListViewHost.FocusAdjacentAsync(IFocusableListItem current, int direction)
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

    async Task IListViewHost.FocusEndAsync(bool start)
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

    private bool IsSelected(ListViewItem<TValue> item) => SelectionMode switch
    {
        ListViewSelectionMode.Single => EqualityComparer<TValue>.Default.Equals(SelectedValue, item.Value),
        ListViewSelectionMode.Multiple => SelectedValues is not null &&
            SelectedValues.Any(v => EqualityComparer<TValue>.Default.Equals(v, item.Value)),
        _ => false
    };

    private async Task OnItemSelectAsync(ListViewItem<TValue> item)
    {
        switch (SelectionMode)
        {
            case ListViewSelectionMode.Single:
                SelectedValue = item.Value;
                await SelectedValueChanged.InvokeAsync(SelectedValue);
                break;

            case ListViewSelectionMode.Multiple:
                var current = SelectedValues?.ToList() ?? [];
                var index = current.FindIndex(v => EqualityComparer<TValue>.Default.Equals(v, item.Value));
                if (index >= 0)
                {
                    current.RemoveAt(index);
                }
                else
                {
                    current.Add(item.Value);
                }

                SelectedValues = current;
                await SelectedValuesChanged.InvokeAsync(SelectedValues);
                break;

            case ListViewSelectionMode.None:
            default:
                break;
        }
    }
}
