using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace FluentKit.Composite;

/// <summary>
/// Mirrors WinUI's NumberBox / fluent-svelte's NumberBox.svelte — a validated numeric TextBox with
/// spin up/down buttons wired through FluentTextBox's Buttons slot (the plumbing added for this).
/// Composes FluentTextBox rather than reimplementing input chrome, same as fluent-svelte's own
/// NumberBox does with its TextBox + TextBoxButton "buttons" slot.
///
/// Text is only committed back to <see cref="Value"/> when it parses as a valid double — an
/// in-progress edit like "-" or "3." is kept on screen as-is without round-tripping through Value,
/// same "don't fight the user mid-keystroke" rule ComboBox's editable mode already established.
/// </summary>
public partial class FluentNumberBox : ComponentBase
{
    [Parameter]
    public double Value { get; set; }

    [Parameter]
    public EventCallback<double> ValueChanged { get; set; }

    [Parameter]
    public double Min { get; set; } = double.MinValue;

    [Parameter]
    public double Max { get; set; } = double.MaxValue;

    [Parameter]
    public double Step { get; set; } = 1;

    /// <summary>
    /// Compact: two small +/- buttons side by side, always inside the box. Expanded (default): no
    /// buttons at rest — a bigger stacked up/down control pops out from the box's edge on focus.
    /// </summary>
    [Parameter]
    public NumberBoxSpinButtonMode Mode { get; set; } = NumberBoxSpinButtonMode.Expanded;

    [Parameter]
    public string? Header { get; set; }

    [Parameter]
    public string? Placeholder { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private string? _rawText;
    private double _lastValue;

    protected override void OnParametersSet()
    {
        // Only re-seed the display text from Value when Value actually changed underneath us
        // (external set, or our own step buttons) — never while the user is mid-keystroke on an
        // in-progress string that hasn't parsed back into Value yet.
        if (_rawText is null || Value != _lastValue)
        {
            _rawText = Value.ToString(CultureInfo.InvariantCulture);
            _lastValue = Value;
        }
    }

    private string? DisplayValue => _rawText;

    private bool AtMax => Value >= Max;

    private bool AtMin => Value <= Min;

    private async Task OnTextChangedAsync(string? text)
    {
        _rawText = text;

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            var clamped = Math.Clamp(parsed, Min, Max);
            _lastValue = clamped;

            if (clamped != Value)
            {
                Value = clamped;
                await ValueChanged.InvokeAsync(Value);
            }
        }
        // else: leave the in-progress text on screen, don't touch Value yet.
    }

    private async Task StepUpAsync()
    {
        var next = Math.Clamp(Value + Step, Min, Max);
        _rawText = next.ToString(CultureInfo.InvariantCulture);
        _lastValue = next;
        Value = next;
        await ValueChanged.InvokeAsync(Value);
    }

    private async Task StepDownAsync()
    {
        var next = Math.Clamp(Value - Step, Min, Max);
        _rawText = next.ToString(CultureInfo.InvariantCulture);
        _lastValue = next;
        Value = next;
        await ValueChanged.InvokeAsync(Value);
    }
}
