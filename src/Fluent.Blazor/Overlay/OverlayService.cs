using Microsoft.AspNetCore.Components;

namespace Fluent.Blazor.Overlay;

public sealed class OverlayService : IOverlayService
{
    private readonly List<OverlayEntry> _active = [];

    public IReadOnlyList<OverlayEntry> Active => _active;

    public event Action? Changed;

    public Guid Show(RenderFragment content, ElementReference anchor,
        OverlayPlacement placement = OverlayPlacement.Bottom, bool lightDismiss = true)
    {
        var entry = new OverlayEntry
        {
            Content = content,
            Anchor = anchor,
            PreferredPlacement = placement,
            LightDismiss = lightDismiss
        };

        _active.Add(entry);
        Changed?.Invoke();
        return entry.Id;
    }

    public void Close(Guid id)
    {
        var removed = _active.RemoveAll(e => e.Id == id);
        if (removed > 0)
        {
            Changed?.Invoke();
        }
    }

    public void CloseAll()
    {
        if (_active.Count == 0)
        {
            return;
        }

        _active.Clear();
        Changed?.Invoke();
    }
}
