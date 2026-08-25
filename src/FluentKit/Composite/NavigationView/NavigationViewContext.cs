using Microsoft.AspNetCore.Components;

namespace FluentKit.Composite;

public class NavigationViewContext : IDisposable
{
    private readonly FluentNavigationView _owner;
    private object? _selectedValue;

    public NavigationViewContext(FluentNavigationView owner)
    {
        _owner = owner;
    }

    public object? SelectedValue
    {
        get => _selectedValue;
        internal set
        {
            if (Equals(_selectedValue, value)) return;
            _selectedValue = value;
            _owner?.NotifyContextSelectionChanged(value);
            SelectionChanged?.Invoke();
        }
    }

    public event Action? SelectionChanged;
    public event Action<object?>? ItemClicked;

    /// <summary>Raised whenever any FluentNavigationViewItem expands or collapses its children,
    /// independent of SelectionChanged - a parent with children defaults to non-selectable (see
    /// FluentNavigationViewItem.RealIsSelectable), so toggling it alone never fires
    /// SelectionChanged even though it can reveal or hide the item that should carry the
    /// navigation view's selection anchor (see FluentNavigationView.OnContextExpansionChanged).</summary>
    public event Action? ExpansionChanged;

    internal void NotifyExpansionChanged() => ExpansionChanged?.Invoke();

    public void SelectValue(object? value)
    {
        SelectedValue = value;
        ItemClicked?.Invoke(value);
    }

    public void Dispose() { }
}
