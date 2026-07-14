namespace FluentKit.Primitives;

/// <summary>
/// Minimal contract FluentListView needs to drive roving arrow-key focus across its children —
/// deliberately NOT tied to FluentListViewItem's concrete type, so other composites (e.g.
/// FluentNavigationViewItem, which owns its own selection via NavigationViewContext instead of
/// FluentListViewItem's plain Selected/OnSelect) can register into the same list for keyboard nav
/// without adopting FluentListViewItem's markup or selection model.
/// </summary>
public interface IFocusableListItem
{
    bool Disabled { get; }

    ValueTask FocusAsync();
}
