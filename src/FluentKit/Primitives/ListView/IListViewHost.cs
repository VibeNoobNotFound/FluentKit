namespace FluentKit.Primitives;

/// <summary>
/// Non-generic contract FluentListViewItem cascades against. FluentListView&lt;TValue&gt; is a
/// different closed generic type per TValue (FluentListView&lt;string&gt; and
/// FluentListView&lt;int&gt; don't share a type), so a plain <c>[CascadingParameter]
/// FluentListView&lt;TValue&gt;?</c> on FluentListViewItem could never bind to it — FluentListViewItem
/// itself has no TValue to close over. Cascading this interface instead sidesteps that: any
/// FluentListView&lt;TValue&gt; implements it the same way regardless of TValue.
/// </summary>
public interface IListViewHost
{
    void Register(IFocusableListItem item);

    void Unregister(IFocusableListItem item);

    Task FocusAdjacentAsync(IFocusableListItem current, int direction);

    Task FocusEndAsync(bool start);
}
