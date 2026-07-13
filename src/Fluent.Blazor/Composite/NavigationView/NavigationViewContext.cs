namespace Fluent.Blazor.Composite;

/// <summary>
/// Cascaded context a FluentNavigationView exposes to its child FluentNavigationViewItems —
/// mirrors the RadioGroupContext pattern (FluentRadioGroup/FluentRadioButton): the parent owns
/// selection state, children read it and call back into it, no direct parent/child references.
///
/// Kept as a single long-lived instance (created once in FluentNavigationView.OnInitialized, not
/// recreated in OnParametersSet) with mutable SelectedValue/IsLabelVisible instead of being
/// replaced wholesale on every parameter change — replacing it would also wipe the item-order
/// registry below on every re-render (e.g. every selection change), which is exactly when the
/// sliding selection indicator most needs that registry to still be intact.
/// </summary>
public sealed class NavigationViewContext
{
    private readonly Func<object?, Task> _selectItem;
    private readonly List<object?> _itemOrder = [];

    internal NavigationViewContext(Func<object?, Task> selectItem)
    {
        _selectItem = selectItem;
    }

    public object? SelectedValue { get; internal set; }

    /// <summary>
    /// False while the pane is showing as an icon-only rail (Compact/Minimal, collapsed, or
    /// Expanded collapsed to rail width) — items hide their text label in that state.
    /// </summary>
    public bool IsLabelVisible { get; internal set; }

    public Task SelectItemAsync(object? value) => _selectItem(value);

    /// <summary>Fired whenever an item registers/unregisters, so FluentNavigationView can
    /// recompute the sliding indicator's position once every item has had a chance to register
    /// (registration happens during each item's own OnInitialized, which runs after this context
    /// is handed down but is not guaranteed complete by the parent's first render pass).</summary>
    internal event Action? ItemsChanged;

    /// <summary>
    /// Registers an item's Value in first-seen order and returns its index — the sliding indicator
    /// animates to <c>index * rowHeight</c> rather than each item owning/animating its own
    /// indicator. Note: if the same MenuItems RenderFragment is rendered twice concurrently (e.g.
    /// NavigationView's Compact rail plus its floating overlay pane, both showing the same items),
    /// each value registers twice; IndexOf below returns the first-registered occurrence, which is
    /// stable and sufficient for positioning the indicator in whichever list is currently visible.
    /// </summary>
    internal int RegisterItem(object? value)
    {
        _itemOrder.Add(value);
        ItemsChanged?.Invoke();
        return _itemOrder.Count - 1;
    }

    internal void UnregisterItem(object? value)
    {
        _itemOrder.Remove(value);
        ItemsChanged?.Invoke();
    }

    internal int IndexOf(object? value) => _itemOrder.IndexOf(value);
}
