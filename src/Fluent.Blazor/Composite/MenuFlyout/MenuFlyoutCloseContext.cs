using Microsoft.AspNetCore.Components;

namespace Fluent.Blazor.Composite;

/// <summary>
/// Shared close-coordination for a MenuFlyout/ContextMenu tree. A single instance is created by the
/// root (FluentMenuFlyout or FluentContextMenu) and cascaded down through every nested submenu level.
/// Each open submenu subscribes its own close action to <see cref="RequestCloseAll"/> so that
/// selecting a leaf item anywhere in the tree collapses the whole chain at once — mirroring
/// fluent-svelte's single `closeFlyout` context, just fanned out to however many submenu levels are
/// currently open (each one is a *separate* IOverlayService entry, so there's no automatic parent/
/// child unmounting to piggyback on the way there would be with plain nested DOM).
/// </summary>
public sealed class MenuFlyoutCloseContext
{
    /// <summary>Mirrors fluent-svelte's `closable` — whether the tree can be dismissed by
    /// conventional interaction (selecting an item, clicking outside, Escape) at all.</summary>
    public bool Closable { get; init; } = true;

    /// <summary>Mirrors fluent-svelte's `closeOnSelect` — whether selecting a standard/radio/toggle
    /// item closes the tree. When false, items still raise their click/select callbacks but the
    /// menu stays open (e.g. a multi-toggle menu the user checks several boxes in before dismissing).</summary>
    public bool CloseOnSelect { get; init; } = true;

    public event Action? RequestCloseAll;

    /// <summary>Called by any item's click handler once it decides selection should close the tree.</summary>
    public void CloseAll() => RequestCloseAll?.Invoke();

    /// <summary>Called by a cascading item's own submenu when it opens, so it participates in the
    /// next CloseAll(). Returns an IDisposable — dispose it when the submenu closes on its own
    /// (Escape while focused inside it, external click) so a stale delegate isn't invoked later.</summary>
    public IDisposable Subscribe(Action close)
    {
        RequestCloseAll += close;
        return new Unsubscriber(() => RequestCloseAll -= close);
    }

    private sealed class Unsubscriber(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
