namespace FluentKit.Composite;

/// <summary>
/// Per-menu-level (one instance per FluentMenuFlyoutSurface) tally of how many of its
/// FluentMenuFlyoutItem children currently render an <see cref="FluentMenuFlyoutItem.Icon"/>.
/// Exists so a menu with NO icons anywhere doesn't reserve the leading icon gutter on every row —
/// WinUI/fluent-svelte only indent labels to line up with icons when at least one sibling actually
/// has one; a menu that's icon-free throughout should have its labels start flush left.
///
/// This is inherently a two-pass problem: an item can't know at its own render time whether some
/// *later* sibling has an icon, since siblings render in document order within the same pass. Items
/// register/unregister as their own Icon presence changes; the count transitioning 0->1 (or back)
/// raises <see cref="Changed"/>, which FluentMenuFlyoutSurface subscribes to and reacts to with its
/// own StateHasChanged() — forcing a second render pass in which every item now sees the final,
/// correct <see cref="HasAnyIcon"/> value. A menu that never gets any icons only ever needs the one
/// pass; a menu whose first-rendered item already has an icon effectively self-corrects on the
/// second pass too, harmlessly.
/// </summary>
public sealed class MenuFlyoutIconGutterContext
{
    private int _iconCount;

    public bool HasAnyIcon => _iconCount > 0;

    public event Action? Changed;

    public void RegisterIcon()
    {
        _iconCount++;
        if (_iconCount == 1)
        {
            Changed?.Invoke();
        }
    }

    public void UnregisterIcon()
    {
        if (_iconCount <= 0)
        {
            return;
        }

        _iconCount--;
        if (_iconCount == 0)
        {
            Changed?.Invoke();
        }
    }
}
