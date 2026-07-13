namespace Fluent.Blazor.Composite;

/// <summary>
/// Cascaded context a FluentNavigationView exposes to its child FluentNavigationViewItems —
/// mirrors the RadioGroupContext pattern (FluentRadioGroup/FluentRadioButton): the parent owns
/// selection state, children read it and call back into it, no direct parent/child references.
/// </summary>
public sealed class NavigationViewContext
{
    private readonly Func<object?, Task> _selectItem;

    internal NavigationViewContext(object? selectedValue, bool isLabelVisible, Func<object?, Task> selectItem)
    {
        SelectedValue = selectedValue;
        IsLabelVisible = isLabelVisible;
        _selectItem = selectItem;
    }

    public object? SelectedValue { get; }

    /// <summary>
    /// False while the pane is showing as an icon-only rail (Compact/Minimal, collapsed, or
    /// Expanded collapsed to rail width) — items hide their text label in that state.
    /// </summary>
    public bool IsLabelVisible { get; }

    public Task SelectItemAsync(object? value) => _selectItem(value);
}
