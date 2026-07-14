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

    public void SelectValue(object? value)
    {
        SelectedValue = value;
        ItemClicked?.Invoke(value);
    }

    public void Dispose() { }
}