using Microsoft.AspNetCore.Components;

namespace Fluent.Blazor.Overlay;

public sealed class OverlayService : IOverlayService
{
    private readonly List<OverlayEntry> _active = [];

    public IReadOnlyList<OverlayEntry> Active => _active;

    public event Action? Changed;

    public Guid Show(RenderFragment content, ElementReference anchor,
        OverlayPlacement placement = OverlayPlacement.Bottom, bool lightDismiss = true, bool bare = false,
        bool matchAnchorWidth = false)
    {
        var entry = new OverlayEntry
        {
            Content = content,
            Anchor = anchor,
            PreferredPlacement = placement,
            LightDismiss = lightDismiss,
            Bare = bare,
            MatchAnchorWidth = matchAnchorWidth
        };

        _active.Add(entry);
        Changed?.Invoke();
        return entry.Id;
    }

    public Guid ShowDetached(RenderFragment content,
        OverlayScreenPlacement screenPlacement = OverlayScreenPlacement.BottomCenter, bool lightDismiss = true)
    {
        var entry = new OverlayEntry
        {
            Content = content,
            Anchor = default,
            IsDetached = true,
            LightDismiss = lightDismiss,
            // Computed synchronously here, not by overlay-interop.js's computePosition — there's no
            // anchor rect to measure against, just a fixed spot relative to the viewport, so
            // OverlaySurface can skip the JS round-trip entirely and render already-positioned on
            // its very first frame instead of the usual "hidden until measured" one-frame delay.
            ComputedStyle = ComputeScreenPosition(screenPlacement)
        };

        _active.Add(entry);
        Changed?.Invoke();
        return entry.Id;
    }

    private static string ComputeScreenPosition(OverlayScreenPlacement placement)
    {
        const int margin = 24;
        const string basePosition = "position: fixed; z-index: 1000; ";

        return placement switch
        {
            OverlayScreenPlacement.TopCenter =>
                $"{basePosition}top: {margin}px; left: 50%; transform: translateX(-50%);",
            OverlayScreenPlacement.Center =>
                $"{basePosition}top: 50%; left: 50%; transform: translate(-50%, -50%);",
            OverlayScreenPlacement.BottomLeft =>
                $"{basePosition}bottom: {margin}px; left: {margin}px;",
            OverlayScreenPlacement.BottomRight =>
                $"{basePosition}bottom: {margin}px; right: {margin}px;",
            OverlayScreenPlacement.TopLeft =>
                $"{basePosition}top: {margin}px; left: {margin}px;",
            OverlayScreenPlacement.TopRight =>
                $"{basePosition}top: {margin}px; right: {margin}px;",
            // BottomCenter is the default.
            _ => $"{basePosition}bottom: {margin}px; left: 50%; transform: translateX(-50%);"
        };
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
