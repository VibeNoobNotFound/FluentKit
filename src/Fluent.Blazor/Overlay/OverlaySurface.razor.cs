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

        var placementArg = Entry.PreferredPlacement.ToString().ToLowerInvariant();
        var position = await _module.InvokeAsync<OverlayPosition>(
            "computePosition", Entry.Anchor, _surfaceElement, placementArg, Entry.MatchAnchorWidth);

        var widthStyle = position.Width is { } width ? $"width: {width}px; " : "";
        Entry.ComputedStyle =
            $"position: fixed; top: {position.Top}px; left: {position.Left}px; {widthStyle}z-index: 1000;";
        _positioned = true;

        if (Entry.LightDismiss)
        {
            _selfReference = DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync(
                "registerLightDismiss", Entry.Id.ToString(), _surfaceElement, Entry.Anchor, _selfReference);
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
