using FluentKit.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FluentKit.Overlay;

public partial class OverlaySurface : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private IOverlayService OverlayService { get; set; } = default!;

    [Parameter, EditorRequired] public OverlayEntry Entry { get; set; } = default!;

    private ElementReference _surfaceElement;
    private IJSObjectReference? _module;
    private DotNetObjectReference<OverlaySurface>? _selfReference;
    private bool _needsPositioning = true;
    private bool _needsDismissRegistrationUpdate;
    private int _disposed;
    // Guards HandleClosingAsync against running twice — Entry.IsClosing stays true across every
    // subsequent render once OverlayService.Close() flips it (the same OverlayEntry instance is kept
    // mounted via FluentOverlayHost's @key="entry.Id" specifically so the exit animation can play),
    // so without this OnParametersSet would kick off a redundant wait-and-CompleteClose on every
    // one of those re-renders instead of just the first.
    private bool _closingHandled;

    protected override void OnParametersSet()
    {
        if (Entry.NeedsReposition)
        {
            Entry.NeedsReposition = false;
            _needsPositioning = true;
            Entry.ComputedStyle = "position: fixed; visibility: hidden;";
        }

        if (Entry.NeedsDismissRegistrationUpdate)
        {
            Entry.NeedsDismissRegistrationUpdate = false;
            _needsDismissRegistrationUpdate = true;
        }

        if (Entry.IsClosing && !_closingHandled)
        {
            _closingHandled = true;
            // Fire-and-forget: this render still needs to complete synchronously so the
            // "fluent-overlay-surface--closing" class actually lands in the DOM (that's what starts
            // the CSS exit animation in the first place) before HandleClosingAsync goes looking for
            // its animationend event.
            _ = HandleClosingAsync();
        }
    }

    private async Task HandleClosingAsync()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        try
        {
            _module ??= await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/FluentKit/Overlay/overlay-interop.js");
            await _module.InvokeVoidAsync("waitForExitAnimation", _surfaceElement);
        }
        catch (JSDisconnectedException)
        {
            // Circuit already gone — nothing left to animate or clean up on the client, and
            // OverlayService's own state doesn't need CompleteClose to keep it consistent once the
            // whole circuit is torn down.
            return;
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        OverlayService.CompleteClose(Entry.Id);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        if (!firstRender && !_needsPositioning && !_needsDismissRegistrationUpdate)
        {
            return;
        }

        _module ??= await JS.InvokeAsync<IJSObjectReference>(
            "import", "./_content/FluentKit/Overlay/overlay-interop.js");

        if (_needsDismissRegistrationUpdate)
        {
            await SyncDismissRegistrationAsync();
            _needsDismissRegistrationUpdate = false;
        }

        if (Entry.IsDetached)
        {
            // No anchor to measure — OverlayService already computed Entry.ComputedStyle
            // synchronously (plain viewport-relative `position: fixed`), so there's nothing to do
            // here except mark positioning done and, if requested, wire up light-dismiss without an
            // anchor exclusion zone.
            _needsPositioning = false;
            if (firstRender)
            {
                await RegisterDismissHandlersAsync();
            }

            return;
        }

        if (!_needsPositioning)
        {
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
        _needsPositioning = false;
        if (firstRender)
        {
            await RegisterDismissHandlersAsync();
        }

        StateHasChanged();
    }

    private async Task SyncDismissRegistrationAsync()
    {
        await _module!.InvokeVoidAsync("unregisterLightDismiss", Entry.Id.ToString());
        _selfReference?.Dispose();
        _selfReference = null;
        await RegisterDismissHandlersAsync();
    }

    private async Task RegisterDismissHandlersAsync()
    {
        if (Entry.LightDismiss || Entry.WatchAnchorRemoved)
        {
            _selfReference = DotNetObjectReference.Create(this);
        }

        if (Entry.LightDismiss)
        {
            if (Entry.IsDetached)
            {
                await _module!.InvokeVoidAsync(
                    "registerLightDismissDetached", Entry.Id.ToString(), _surfaceElement, _selfReference);
            }
            else
            {
                await _module!.InvokeVoidAsync(
                    "registerLightDismiss", Entry.Id.ToString(), _surfaceElement, Entry.Anchor, _selfReference);
            }
        }

        if (Entry.WatchAnchorRemoved)
        {
            await _module!.InvokeVoidAsync(
                "watchAnchorRemoved", Entry.Id.ToString(), Entry.Anchor, _selfReference);
        }
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
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        var module = Interlocked.Exchange(ref _module, null);
        var selfReference = Interlocked.Exchange(ref _selfReference, null);

        try
        {
            if (module is not null)
            {
                try
                {
                    await module.InvokeVoidAsync("unregisterLightDismiss", Entry.Id.ToString()).ConfigureAwait(false);
                }
                catch (JSDisconnectedException)
                {
                    // The browser-side registration is unreachable after circuit teardown.
                }
                finally
                {
                    await JsModuleDisposal.DisposeAsync(module);
                }
            }
        }
        finally
        {
            selfReference?.Dispose();
        }
    }

    private sealed class OverlayPosition
    {
        public double Top { get; set; }
        public double Left { get; set; }
        public string Placement { get; set; } = "bottom";
        public double? Width { get; set; }
    }
}
