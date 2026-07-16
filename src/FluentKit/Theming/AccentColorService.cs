using Microsoft.JSInterop;

namespace FluentKit.Theming;

/// <summary>See <see cref="IAccentColorService"/>. Same DI lifetime story as <see cref="ThemeService"/> (Scoped).</summary>
public sealed class AccentColorService : IAccentColorService, IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;

    public AccentColorService(IJSRuntime js)
    {
        _js = js;
    }

    public RgbColor CurrentAccent { get; private set; } = AccentPalette.FallbackBase;

    public AccentPalette CurrentPalette { get; private set; } = AccentPalette.Fallback;

    public event Action? AccentChanged;

    public async Task InitializeAsync()
    {
        // Only apply if nothing has set an explicit accent yet — Initialize is meant to guarantee
        // the --accent-* variables exist on first paint, not to stomp a choice made before it runs.
        if (_module is not null)
        {
            return;
        }

        await ApplyPaletteAsync(AccentPalette.Fallback);
    }

    public async Task SetAccentAsync(string hexColor)
    {
        var color = RgbColor.Parse(hexColor); // throws FormatException on bad input — this is a direct user/dev choice, so fail loudly
        await ApplyPaletteAsync(AccentPalette.FromColor(color));
    }

    public async Task SetAccentFromImageAsync(string imageUrl)
    {
        var module = await GetModuleAsync();

        try
        {
            var rgb = await module.InvokeAsync<int[]>("extractDominantColor", imageUrl);
            var color = new RgbColor((byte)rgb[0], (byte)rgb[1], (byte)rgb[2]);
            await ApplyPaletteAsync(AccentPalette.FromColor(color));
        }
        catch (JSException)
        {
            // Image failed to load, or the canvas read was blocked (CORS on a cross-origin
            // wallpaper URL, etc). Mirrors real Windows behavior reasonably well: if it can't
            // read the picture, it keeps/falls back to a default rather than leaving the UI
            // with no accent at all.
            await ApplyPaletteAsync(AccentPalette.Fallback);
        }
    }

    private async Task ApplyPaletteAsync(AccentPalette palette)
    {
        var module = await GetModuleAsync();

        CurrentAccent = palette.Base;
        CurrentPalette = palette;

        await module.InvokeVoidAsync("applyAccentPalette", palette.ToCssVariables());

        AccentChanged?.Invoke();
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        _module ??= await _js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/FluentKit/Theming/accent-interop.js");
        return _module;
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }
}
