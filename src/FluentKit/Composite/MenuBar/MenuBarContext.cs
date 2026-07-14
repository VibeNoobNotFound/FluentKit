namespace FluentKit.Composite;

/// <summary>
/// Shared cascading state for a FluentMenuBar tree — ported from fluent-svelte's <c>currentMenu</c>
/// writable store (MenuBar/flyoutState.ts) plus MenuBar.svelte's <c>sideNavigation</c> context.
/// Tracks which top-level item currently has its dropdown open (so hovering a sibling switches the
/// open menu instead of requiring another click, matching svelte's <c>$currentMenu</c> check) and
/// the left-to-right tab order of registered items (for ArrowLeft/ArrowRight wraparound focus,
/// matching svelte's tabbable-based sideNavigation handler — done here via item self-registration
/// instead of a live DOM query, since that's how this project's other roving-tabindex controls
/// (CalendarView, NavigationView) already do it).
/// </summary>
public sealed class MenuBarContext
{
    private readonly List<FluentMenuBarItem> _items = new();

    public FluentMenuBarItem? CurrentOpenItem { get; private set; }

    public void Register(FluentMenuBarItem item) => _items.Add(item);

    public void Unregister(FluentMenuBarItem item) => _items.Remove(item);

    /// <summary>Marks <paramref name="item"/> as the open one, closing whichever sibling was
    /// previously open (mirrors svelte's <c>$currentMenu !== menu</c> reactive close).</summary>
    public void SetOpen(FluentMenuBarItem item)
    {
        if (CurrentOpenItem is { } previous && !ReferenceEquals(previous, item))
        {
            previous.ForceClose();
        }

        CurrentOpenItem = item;
    }

    public void ClearOpen(FluentMenuBarItem item)
    {
        if (ReferenceEquals(CurrentOpenItem, item))
        {
            CurrentOpenItem = null;
        }
    }

    /// <summary>Moves focus to the previous/next registered item, wrapping around at either end —
    /// matches svelte's sideNavigation ArrowLeft/ArrowRight handler exactly (including the wrap).</summary>
    public async Task FocusAdjacentAsync(FluentMenuBarItem current, int direction)
    {
        var index = _items.IndexOf(current);
        if (index < 0 || _items.Count == 0)
        {
            return;
        }

        var next = (index + direction + _items.Count) % _items.Count;
        await _items[next].FocusAsync();
    }
}
