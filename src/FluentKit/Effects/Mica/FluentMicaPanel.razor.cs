using FluentKit.Interop;
using FluentKit.Theming;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FluentKit.Effects;

/// <summary>
/// Approximates WinUI's Mica material — rebuilt from scratch against the actual recipe in
/// microsoft-ui-xaml's SystemBackdropComponentInternal::BuildMicaEffectBrush
/// (dev/dll/SystemBackdrop/SystemBackdropBrushFactory.cpp). This is NOT the same thing as Acrylic
/// (see FluentAcrylicBrush) — Acrylic live-blurs whatever's actually behind it via backdrop-filter;
/// Mica does not.
///
/// PERFORMANCE NOTE: this used to run the blur (CSS `filter`) and the two blend passes (CSS
/// `mix-blend-mode`) live, every repaint. That's expensive for a large/fixed-viewport element and
/// was the source of noticeable jank. It's now baked ONCE per (image, variant, theme) into a static
/// raster via an offscreen &lt;canvas&gt; (see wwwroot/Effects/mica-interop.js) and cached — after
/// that, rendering Mica is just displaying a static image, no different in cost from any other photo
/// background. The recipe itself (blur -> luminosity blend -> color blend) is unchanged, just baked
/// ahead of time instead of recomputed on every paint.
///
///   1. Start from a heavily blurred copy of the desktop wallpaper (ICompositorWithBlurredWallpaper-
///      BackdropBrush — an OS compositor API that samples the wallpaper only, not other windows).
///      Browsers can't reach the real wallpaper, so <see cref="BackgroundImageUrl"/> stands in for
///      it — supply any image (ideally the user's own wallpaper file) and it's blurred the same way.
///   2. LUMINOSITY blend pass: blend TintColor (alpha = LuminosityOpacity) over the blurred
///      wallpaper using true "luminosity" blend mode — keeps the wallpaper's hue/saturation but
///      replaces its per-pixel luminosity with the tint's. Mica's defaults set LuminosityOpacity to
///      1.0, so this pass ends up governing almost the entire final look.
///   3. COLOR blend pass: blend TintColor (alpha = TintOpacity) over the result of step 2 using true
///      "color" blend mode — keeps the TINT's hue/saturation but takes luminosity from step 2's
///      result. This is what gives Mica its "muted, personalized" gradient-ish look: the wallpaper's
///      light/dark shape survives, but its color is replaced by the theme tint.
///
/// With no BackgroundImageUrl supplied (or a format the browser can't decode, e.g. HEIC outside
/// Safari), this degrades to WinUI's own documented fallback: a flat SolidBackgroundFillColorBase
/// (Alt) fill — the same state real Mica falls back to under RDP, low-power GPUs, or Battery Saver.
/// </summary>
public enum MicaVariant
{
    /// <summary>SolidBackgroundFillColorBase — the default Mica used for a window's main surface.</summary>
    Base,

    /// <summary>Mica Alt — stronger tinting of the wallpaper, used for layered/commanding surfaces
    /// (tabbed title bars, nav panes) that need to read as visually deeper than a Base-Mica window.</summary>
    BaseAlt
}

public partial class FluentMicaPanel : ComponentBase, IAsyncDisposable
{
    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    [Inject]
    private IThemeService ThemeService { get; set; } = default!;

    [Parameter]
    public MicaVariant Variant { get; set; } = MicaVariant.Base;

    /// <summary>
    /// Stand-in for the desktop wallpaper WinUI would otherwise sample directly via the OS
    /// compositor. Any image URL works (relative to wwwroot, or absolute). When omitted, the panel
    /// falls back to WinUI's own documented solid-color fallback.
    /// </summary>
    [Parameter]
    public string? BackgroundImageUrl { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private JsModuleLifetime? _interop;
    private string? _lastKey;
    private string? _pendingKey;
    private ElementReference _wallpaperElement;
    private int _disposed;
    private int _renderGeneration;
    private bool _themeHandlerSubscribed;
    private bool _interactive;
    private bool _needsRender = true;

    private JsModuleLifetime Interop => _interop ??= new(
        JS, "./_content/FluentKit/Effects/mica-interop.js");

    private string VariantClass => Variant == MicaVariant.BaseAlt ? "fluent-mica--alt" : "fluent-mica--base";

    protected override Task OnInitializedAsync()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return Task.CompletedTask;
        }

        ThemeService.ThemeChanged += OnThemeChanged;
        _themeHandlerSubscribed = true;
        return Task.CompletedTask;
    }

    protected override Task OnParametersSetAsync()
    {
        if (_interactive && Volatile.Read(ref _disposed) == 0)
        {
            // The new parameters are not in the DOM until this lifecycle pass completes. Defer
            // the browser-side paint to OnAfterRenderAsync so a newly-created wallpaper element is
            // a valid target, especially when a Server app changes its image at runtime.
            _needsRender = true;
        }

        return Task.CompletedTask;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        // Keep the normal fallback surface prerenderable in Interactive Server. The Mica raster
        // requires a browser canvas, so defer that work until the circuit is interactive.
        if (firstRender)
        {
            _interactive = true;
        }

        if (!_interactive)
        {
            return;
        }

        if (await PaintPendingRasterAsync() && Volatile.Read(ref _disposed) == 0)
        {
            // OnAfterRenderAsync does not schedule a render after its awaited work completes.
            // The raster paint is performed by JavaScript, but scheduling the follow-up render keeps
            // the component lifecycle synchronized with the completed browser-side work.
            await InvokeAsync(StateHasChanged);
        }
    }

    private void OnThemeChanged() => ObserveBackgroundTask(OnThemeChangedAsync());

    private async Task OnThemeChangedAsync()
    {
        if (!_interactive || Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        try
        {
            await RenderAsync();
            if (Volatile.Read(ref _disposed) == 0)
            {
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (JSDisconnectedException)
        {
            // The circuit may disappear while the asynchronous Mica render is in flight.
        }
        catch (OperationCanceledException) when (Volatile.Read(ref _disposed) != 0)
        {
            // Component-owned cancellation is expected during circuit teardown.
        }
    }

    private void ObserveBackgroundTask(Task task)
    {
        _ = task.ContinueWith(
            static (completed, state) =>
            {
                var component = (FluentMicaPanel)state!;
                var exception = completed.Exception?.GetBaseException();
                if (exception is not null &&
                    exception is not JSDisconnectedException &&
                    (exception is not OperationCanceledException || Volatile.Read(ref component._disposed) == 0))
                {
                    _ = component.DispatchExceptionAsync(exception);
                }
            },
            this,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task<bool> RenderAsync()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return false;
        }

        if (string.IsNullOrEmpty(BackgroundImageUrl))
        {
            var changed = _lastKey is not null;
            Interlocked.Increment(ref _renderGeneration);
            _pendingKey = null;
            _lastKey = null;
            return changed;
        }

        if (!await Interop.EnsureModuleAsync())
        {
            if (Volatile.Read(ref _disposed) == 0)
            {
                var changed = _lastKey is not null;
                Interlocked.Increment(ref _renderGeneration);
                _pendingKey = null;
                _lastKey = null;
                return changed;
            }

            return false;
        }

        var isBase = Variant == MicaVariant.Base;
        var key = $"{BackgroundImageUrl}|{isBase}|{ThemeService.ResolvedTheme}";

        // Same (image, variant, theme) as last time — nothing to redo, the cached raster is already
        // what's on screen. This is what stops a resize/re-render from re-baking Mica every time.
        if (key == _lastKey)
        {
            return false;
        }
        if (key == _pendingKey)
        {
            return false;
        }

        _pendingKey = key;
        var generation = Interlocked.Increment(ref _renderGeneration);

        // If I didnt do, Mica doesnt get properly rendered on first load.
        try
        {
            await Task.Delay(700, Interop.DisposalToken);

            if (Volatile.Read(ref _disposed) != 0 || generation != Volatile.Read(ref _renderGeneration))
            {
                return false;
            }

            // Keep the potentially-large PNG data URL inside the browser. Returning it through
            // IJSRuntime would send the whole raster over the Server circuit and can exceed the
            // SignalR message budget; renderMicaInto only returns success after applying it to the
            // already-rendered wallpaper element.
            var result = await Interop.InvokeVoidAsync("renderMicaInto", _wallpaperElement, key,
                BackgroundImageUrl, isBase);
            if (!result)
            {
                return false;
            }

            // Guard against a stale response landing after a newer request already changed things.
            if (Volatile.Read(ref _disposed) == 0 && generation == Volatile.Read(ref _renderGeneration))
            {
                _lastKey = key;
                return true;
            }

            return false;
        }
        catch (OperationCanceledException) when (Volatile.Read(ref _disposed) != 0)
        {
            // The delayed render or JS call was canceled by component disposal.
            return false;
        }
        finally
        {
            if (key == _pendingKey)
            {
                _pendingKey = null;
            }
        }
    }

    private async Task<bool> PaintPendingRasterAsync()
    {
        if (!_needsRender)
        {
            return false;
        }

        _needsRender = false;
        return await RenderAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_themeHandlerSubscribed)
        {
            ThemeService.ThemeChanged -= OnThemeChanged;
            _themeHandlerSubscribed = false;
        }

        Interlocked.Increment(ref _renderGeneration);
        if (_interop is not null)
        {
            await _interop.DisposeAsync();
        }
    }
}
