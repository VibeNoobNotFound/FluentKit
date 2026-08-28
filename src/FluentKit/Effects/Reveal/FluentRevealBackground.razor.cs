using FluentKit.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FluentKit.Effects;

/// <summary>
/// Approximates WinUI's Reveal highlight — a soft radial-gradient spotlight that tracks the pointer
/// across a control's border/background, used e.g. on NavigationView items and ListView rows to
/// give hover feedback a sense of "light" rather than a flat color swap. Same effects layer as
/// Mica/Acrylic, but unlike those two this one's actual visual work is a single CSS radial-gradient
/// positioned via two custom properties (<c>--reveal-x</c>/<c>--reveal-y</c>) — the JS module
/// (wwwroot/Effects/Reveal/reveal-interop.js) only measures pointer position, it does no rendering
/// itself. <see cref="Bordered"/> additionally reveals along a 1px border ring (WinUI's
/// "RevealBorderBrush" behavior), for use around a control's edge rather than its fill.
/// </summary>
public partial class FluentRevealBackground : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Also renders the highlight along a thin border ring instead of just the fill.</summary>
    [Parameter] public bool Bordered { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private readonly string _id = Guid.NewGuid().ToString("N");
    private ElementReference _element;
    private IJSObjectReference? _module;
    private int _disposed;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && Volatile.Read(ref _disposed) == 0)
        {
            var module = await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/FluentKit/Effects/Reveal/reveal-interop.js");

            if (Volatile.Read(ref _disposed) != 0)
            {
                await JsModuleDisposal.DisposeAsync(module);
                return;
            }

            _module = module;
            await module.InvokeVoidAsync("startTracking", _id, _element);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        var module = Interlocked.Exchange(ref _module, null);
        if (module is null)
        {
            return;
        }

        try
        {
            try
            {
                await module.InvokeVoidAsync("stopTracking", _id).ConfigureAwait(false);
            }
            catch (JSDisconnectedException)
            {
                // The browser-side tracking state is unreachable after circuit teardown.
            }
        }
        finally
        {
            await JsModuleDisposal.DisposeAsync(module);
        }
    }
}
