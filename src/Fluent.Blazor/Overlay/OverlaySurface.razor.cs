using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Fluent.Blazor.Overlay;

public partial class OverlaySurface : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private IOverlayService OverlayService { get; set; } = default!;

    [Parameter, EditorRequired] public OverlayEntry Entry { get; set; } = default!;

    private ElementReference _surfaceElement;
    private IJSObjectReference? _module;
    private DotNetObjectReference<OverlaySurface>? _selfReference;
    private bool _positioned;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _positioned)
        {
            return;
        }

        _module = await JS.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Fluent.Blazor/Overlay/overlay-interop.js");

        if (Entry.IsDetached)
        {
            // No anchor to measure — OverlayService already computed Entry.ComputedStyle
            // synchronously (plain viewport-relative `position: fixed`), so there's nothing to do
            // here except mark positioning done and, if requested, wire up light-dismiss without an
            // anchor exclusion zone.
            _positioned = true;

            if (Entry.LightDismiss)
            {
                _selfReference = DotNetObjectReference.Create(this);
                await _module.InvokeVoidAsync(
                    "registerLightDismissDetached", Entry.Id.ToString(), _surfaceElement, _selfReference);
            }

            return;
        }

        if (Entry.ScrollAnchorIntoView)
        {
            // Must finish (and its two-rAF settle) before computePosition measures the anchor below,
            // or the position math would read a mid-scroll rect and place the popup somewhere that's
            // about to scroll away from it.
            await _module.InvokeVoidAsync("scrollIntoViewIfNeeded", Entry.Anchor);
        }

        var placementArg = Entry.PreferredPlacement.ToString().ToLowerInvariant();
        var position = await _module.InvokeAsync<OverlayPosition>(
            "computePosition", Entry.Anchor, _surfaceElement, placementArg, Entry.MatchAnchorWidth);

        var widthStyle = position.Width is { } width ? $"width: {width}px; " : "";
        Entry.ComputedStyle =
            $"position: fixed; top: {position.Top}px; left: {position.Left}px; {widthStyle}z-index: 1000;";
        _positioned = true;

        if (Entry.LightDismiss || Entry.WatchAnchorRemoved)
        {
            _selfReference = DotNetObjectReference.Create(this);
        }

        if (Entry.LightDismiss)
        {
            await _module.InvokeVoidAsync(
                "registerLightDismiss", Entry.Id.ToString(), _surfaceElement, Entry.Anchor, _selfReference);
        }

        if (Entry.WatchAnchorRemoved)
        {
            await _module.InvokeVoidAsync(
                "watchAnchorRemoved", Entry.Id.ToString(), Entry.Anchor, _selfReference);
        }

        StateHasChanged();
    }

    [JSInvokable]
    public void OnLightDismiss(string overlayIdText)
    {
        if (Guid.TryParse(overlayIdText, out var overlayId) && overlayId == Entry.Id)
        {
            OverlayService.Close(overlayId);
        }
    }

    /// <summary>Called from overlay-interop.js's watchAnchorRemoved when Entry.Anchor is detected to
    /// have left the DOM while this overlay was still open (see WatchAnchorRemoved's own doc comment
    /// for why this only applies to some overlays, not every Flyout/MenuFlyout).</summary>
    [JSInvokable]
    public void OnAnchorRemoved(string overlayIdText)
    {
        if (Guid.TryParse(overlayIdText, out var overlayId) && overlayId == Entry.Id)
        {
            OverlayService.Close(overlayId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("unregisterLightDismiss", Entry.Id.ToString());
            await _module.DisposeAsync();
        }

        _selfReference?.Dispose();
    }

    private sealed class OverlayPosition
    {
        public double Top { get; set; }
        public double Left { get; set; }
        public string Placement { get; set; } = "bottom";
        public double? Width { get; set; }
    }
}
