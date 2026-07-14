namespace Fluent.Blazor.Composite;

/// <summary>
/// Shared registration list for a FluentPivot tree. Pivot items are data-only — they render nothing
/// themselves (see FluentPivotItem) and just register their Title/TabTemplate/ChildContent here;
/// FluentPivot owns all actual DOM output (the tab strip buttons + the single active content pane),
/// reading straight out of this list by index rather than each item rendering itself hidden/shown.
/// Registration happens during the items' own OnInitialized, which runs one render pass after
/// FluentPivot's own — <see cref="NotifyChanged"/> triggers that follow-up render so the tab strip
/// picks the items up as soon as they've registered, same pattern MenuBarContext uses for its list.
/// </summary>
public sealed class PivotContext
{
    private readonly Action _notifyChanged;

    public PivotContext(Action notifyChanged) => _notifyChanged = notifyChanged;

    public List<FluentPivotItem> Items { get; } = new();

    public void Register(FluentPivotItem item)
    {
        if (!Items.Contains(item))
        {
            Items.Add(item);
            _notifyChanged();
        }
    }

    public void Unregister(FluentPivotItem item)
    {
        if (Items.Remove(item))
        {
            _notifyChanged();
        }
    }

    public void NotifyChanged() => _notifyChanged();
}
