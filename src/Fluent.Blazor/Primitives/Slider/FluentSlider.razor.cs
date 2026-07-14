using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Fluent.Blazor.Primitives;

public enum FluentSliderOrientation
{
    Horizontal,
    Vertical
}

public enum FluentSliderTickPlacement
{
    None,
    Outside,
    Inline
}

/// <summary>
/// Slider (Slider_themeresources.xaml / fluent-svelte's Slider.svelte). Drag tracking is delegated to
/// wwwroot/Primitives/Slider/slider-interop.js — pointer/touch position comes back as a plain 0-100
/// rail percentage so this component never has to know pixel geometry itself, only how to turn a
/// percentage into a stepped <see cref="Value"/> and back.
///
/// Fixed-size 20px thumb (see .razor.css) — offset math for keeping the thumb from overflowing the
/// rail edges is `halfWidth * (1 - 2*pct/100)`, ported from fluent-svelte's own clamp formula, with
/// the 20px width hardcoded here rather than measured at runtime since it's a fixed CSS value.
/// </summary>
public partial class FluentSlider : ComponentBase, IAsyncDisposable
{
    private const double ThumbWidthPx = 20;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public double Min { get; set; } = 0;
    [Parameter] public double Max { get; set; } = 100;
    [Parameter] public double Step { get; set; } = 1;
    [Parameter] public double Value { get; set; }
    [Parameter] public EventCallback<double> ValueChanged { get; set; }

    [Parameter] public FluentSliderOrientation Orientation { get; set; } = FluentSliderOrientation.Horizontal;

    /// <summary>Flips which end of the rail 0/Max render at, without changing Min/Max semantics.</summary>
    [Parameter] public bool Reverse { get; set; }

    [Parameter] public bool Disabled { get; set; }

    /// <summary>Gap between ticks, in Value units. 0 (default) draws no ticks.</summary>
    [Parameter] public double TickFrequency { get; set; }

    [Parameter] public FluentSliderTickPlacement TickPlacement { get; set; } = FluentSliderTickPlacement.None;

    /// <summary>Shows the live value in a small callout above/beside the thumb while dragging,
    /// hovering, or focused. Matches fluent-svelte's default behavior.</summary>
    [Parameter] public bool ShowTooltip { get; set; } = true;

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private readonly string _sliderId = Guid.NewGuid().ToString("N");
    private ElementReference _railElement;
    private IJSObjectReference? _module;
    private DotNetObjectReference<FluentSlider>? _selfReference;
    private bool _dragging;
    private bool _hovering;
    private bool _focused;

    private bool IsVertical => Orientation == FluentSliderOrientation.Vertical;
    private string OrientationArg => IsVertical ? "vertical" : "horizontal";

    private double ClampedValue => Math.Clamp(Value, Min, Max);

    private double Percent =>
        Max > Min ? (ClampedValue - Min) / (Max - Min) * 100 : 0;

    private bool ShowTooltipNow => ShowTooltip && (_dragging || _hovering || _focused) && !Disabled;

    private IReadOnlyList<double>? Ticks
    {
        get
        {
            if (TickFrequency <= 0 || TickPlacement == FluentSliderTickPlacement.None)
            {
                return null;
            }

            var ticks = new List<double>();
            for (var v = Min; v <= Max + 0.0001; v += TickFrequency)
            {
                ticks.Add(Math.Min(v, Max));
            }
            return ticks;
        }
    }

    private double TickPercent(double tickValue) =>
        Max > Min ? (tickValue - Min) / (Max - Min) * 100 : 0;

    /// <summary>
    /// CSS custom properties for the fill/thumb position. Thumb offset uses the fixed 20px thumb
    /// width so it never runs past the rail edges: halfWidth * (1 - 2*pct/100).
    ///
    /// Any user-supplied `style` (e.g. for sizing: style="height: 150px;") is merged in here rather
    /// than left in AdditionalAttributes. Blazor attribute splatting resolves duplicate attribute
    /// names by source order — since @attributes is written after this style attribute in the
    /// markup, a caller-supplied style would otherwise silently replace this whole value (wiping out
    /// --slider-percent/--slider-thumb-offset-x/y) instead of combining with it. See SplattedAttributes.
    /// </summary>
    private string ContainerStyle
    {
        get
        {
            var pct = Percent;
            var halfWidth = ThumbWidthPx / 2;
            var offset = halfWidth * (1 - (2 * pct / 100));
            var axis = IsVertical ? "--slider-thumb-offset-y" : "--slider-thumb-offset-x";
            var computed = $"--slider-percent:{pct.ToString(System.Globalization.CultureInfo.InvariantCulture)}%; {axis}:{offset.ToString(System.Globalization.CultureInfo.InvariantCulture)}px;";

            if (AdditionalAttributes is not null &&
                AdditionalAttributes.TryGetValue("style", out var userStyleObj) &&
                userStyleObj is string userStyle &&
                !string.IsNullOrWhiteSpace(userStyle))
            {
                var trimmed = userStyle.Trim();
                if (!trimmed.EndsWith(';'))
                {
                    trimmed += ";";
                }
                return $"{trimmed} {computed}";
            }

            return computed;
        }
    }

    /// <summary>
    /// AdditionalAttributes with "style" removed, so it can be splatted onto the root div without
    /// clobbering ContainerStyle (which already folds any user style in — see above).
    /// </summary>
    private IReadOnlyDictionary<string, object>? SplattedAttributes =>
        AdditionalAttributes is null || !AdditionalAttributes.ContainsKey("style")
            ? AdditionalAttributes
            : AdditionalAttributes
                .Where(kv => !string.Equals(kv.Key, "style", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

    private string DisplayValue => ClampedValue.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    private async Task EnsureModuleAsync()
    {
        _module ??= await JS.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Fluent.Blazor/Primitives/Slider/slider-interop.js");
    }

    private async Task SetFromPercentAsync(double pct)
    {
        pct = Math.Clamp(pct, 0, 100);
        var raw = Min + (pct / 100 * (Max - Min));
        var stepped = Step > 0 ? Math.Round((raw - Min) / Step) * Step + Min : raw;
        var clamped = Math.Clamp(stepped, Min, Max);

        if (Math.Abs(clamped - Value) > 0.0000001)
        {
            Value = clamped;
            await ValueChanged.InvokeAsync(Value);
        }
    }

    private async Task OnPointerDown(PointerEventArgs e)
    {
        if (Disabled)
        {
            return;
        }

        await EnsureModuleAsync();

        var pct = await _module!.InvokeAsync<double>(
            "getPercentAt", _railElement, OrientationArg, Reverse, e.ClientX, e.ClientY);
        await SetFromPercentAsync(pct);

        _selfReference ??= DotNetObjectReference.Create(this);
        await _module.InvokeVoidAsync(
            "startDrag", _sliderId, _railElement, OrientationArg, Reverse, _selfReference);

        _dragging = true;
    }

    [JSInvokable]
    public async Task OnDragPercent(double pct)
    {
        await SetFromPercentAsync(pct);
        StateHasChanged();
    }

    [JSInvokable]
    public void OnDragEnd()
    {
        _dragging = false;
        StateHasChanged();
    }

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (Disabled)
        {
            return;
        }

        var step = Step > 0 ? Step : 1;
        double? next = e.Key switch
        {
            "ArrowRight" => ClampedValue + (Reverse ^ IsVertical ? -step : step),
            "ArrowUp" => ClampedValue + step,
            "ArrowLeft" => ClampedValue + (Reverse ^ IsVertical ? step : -step),
            "ArrowDown" => ClampedValue - step,
            "PageUp" => ClampedValue + step * 10,
            "PageDown" => ClampedValue - step * 10,
            "Home" => Min,
            "End" => Max,
            _ => null
        };

        if (next is null)
        {
            return;
        }

        var clamped = Math.Clamp(next.Value, Min, Max);
        if (Math.Abs(clamped - Value) > 0.0000001)
        {
            Value = clamped;
            await ValueChanged.InvokeAsync(Value);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("stopDrag", _sliderId);
            await _module.DisposeAsync();
        }

        _selfReference?.Dispose();
    }
}
