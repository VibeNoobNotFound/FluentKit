using FluentKit.Interop;
using Microsoft.JSInterop;

namespace FluentKit.Theming;

/// <summary>See <see cref="IAccentColorService"/>. Same DI lifetime story as <see cref="ThemeService"/> (Scoped).</summary>
public sealed class AccentColorService : IAccentColorService, IAsyncDisposable
{
    private readonly JsModuleLifetime _interop;
    private int _disposed;

    public AccentColorService(IJSRuntime js)
    {
        _interop = new(js, "./_content/FluentKit/Theming/accent-interop.js");
    }

    public RgbColor CurrentAccent { get; private set; } = AccentPalette.FallbackBase;

    public AccentPalette CurrentPalette { get; private set; } = AccentPalette.Fallback;

    public event Action? AccentChanged;

    public async Task InitializeAsync()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        // Only apply if nothing has set an explicit accent yet — Initialize is meant to guarantee
        // the --accent-* variables exist on first paint, not to stomp a choice made before it runs.
        if (_interop.Module is not null)
        {
            return;
        }

        await ApplyPaletteAsync(AccentPalette.Fallback);
    }

    public async Task SetAccentAsync(string hexColor)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var color = RgbColor.Parse(hexColor); // throws FormatException on bad input — this is a direct user/dev choice, so fail loudly
        await ApplyPaletteAsync(AccentPalette.FromColor(color));
    }

    public async Task SetAccentFromImageAsync(string imageUrl)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        try
        {
            var result = await _interop.InvokeAsync<int[]>("extractDominantColor", imageUrl);
            if (!result.Succeeded || result.Value is not { Length: >= 3 } rgb)
            {
                return;
            }

            var color = new RgbColor((byte)rgb[0], (byte)rgb[1], (byte)rgb[2]);
            await ApplyPaletteAsync(AccentPalette.FromColor(color));
        }
        catch (JSDisconnectedException)
        {
            // The circuit may disappear while image analysis is in flight.
        }
        catch (OperationCanceledException) when (_interop.IsDisposed)
        {
            // Component/service disposal owns this cancellation.
        }
        catch (JSException)
        {
            if (Volatile.Read(ref _disposed) != 0 || _interop.IsDisposed)
            {
                return;
            }

            // Image failed to load, or the canvas read was blocked (CORS on a cross-origin
            // wallpaper URL, etc). Mirrors real Windows behavior reasonably well: if it can't
            // read the picture, it keeps/falls back to a default rather than leaving the UI
            // with no accent at all.
            await ApplyPaletteAsync(AccentPalette.Fallback);
        }
    }

    private async Task ApplyPaletteAsync(AccentPalette palette)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (!await _interop.InvokeVoidAsync("applyAccentPalette", palette.ToCssVariables()))
        {
            return;
        }

        CurrentAccent = palette.Base;
        CurrentPalette = palette;

        AccentChanged?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _interop.DisposeAsync();
    }
}
