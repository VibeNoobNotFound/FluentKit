using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Fluent.Blazor.Primitives;

/// <summary>
/// Small icon-only button meant to live inside FluentTextBox's <see cref="FluentTextBox.Buttons"/>
/// slot — mirrors fluent-svelte's TextBoxButton.svelte. Reuses the subtle-fill ramp (same as
/// FluentButton's Subtle variant) rather than the full bordered Button chrome, since it sits inside
/// the textbox's own border.
///
/// Also supports press-and-hold repeat (used by NumberBox's spin buttons): holding the pointer down
/// re-invokes <see cref="OnClick"/> repeatedly after an initial delay, accelerating the interval on
/// each repeat down to a floor, same "hold to speed up" behavior as WinUI's own NumberBox / OS
/// spinner controls. A plain tap still only fires OnClick once (the immediate @onclick), since the
/// repeat loop's first extra invocation only lands after <see cref="InitialHoldDelayMs"/>.
/// </summary>
public partial class FluentTextBoxButton : ComponentBase, IDisposable
{
    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public EventCallback<MouseEventArgs> OnClick { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Delay before hold-repeat kicks in after pointerdown, in milliseconds.</summary>
    [Parameter]
    public int InitialHoldDelayMs { get; set; } = 450;

    /// <summary>Interval between repeats at the start of a hold, in milliseconds.</summary>
    [Parameter]
    public int RepeatStartIntervalMs { get; set; } = 180;

    /// <summary>Floor the repeat interval accelerates down to, in milliseconds.</summary>
    [Parameter]
    public int RepeatMinIntervalMs { get; set; } = 40;

    /// <summary>Multiplier applied to the interval after every repeat (below 1 = speeding up).</summary>
    [Parameter]
    public double RepeatAccelerationFactor { get; set; } = 0.85;

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private CancellationTokenSource? _holdCts;

    private void OnPointerDown(PointerEventArgs e)
    {
        if (Disabled)
        {
            return;
        }

        _holdCts?.Cancel();
        _holdCts = new CancellationTokenSource();
        _ = HoldRepeatLoopAsync(_holdCts.Token);
    }

    private void StopHold(PointerEventArgs e) => CancelHold();

    private void CancelHold()
    {
        _holdCts?.Cancel();
        _holdCts = null;
    }

    private async Task HoldRepeatLoopAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(InitialHoldDelayMs, token);

            var interval = RepeatStartIntervalMs;
            while (!token.IsCancellationRequested && !Disabled)
            {
                await InvokeAsync(() => OnClick.InvokeAsync());
                await Task.Delay(interval, token);
                interval = Math.Max(RepeatMinIntervalMs, (int)(interval * RepeatAccelerationFactor));
            }
        }
        catch (TaskCanceledException)
        {
            // Expected — pointer released/left before the delay/interval elapsed.
        }
    }

    public void Dispose()
    {
        _holdCts?.Cancel();
        _holdCts?.Dispose();
        _holdCts = null;
    }
}
