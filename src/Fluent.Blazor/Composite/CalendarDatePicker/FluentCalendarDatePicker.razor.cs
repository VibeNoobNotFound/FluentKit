using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Fluent.Blazor.Composite;

/// <summary>
/// Ported from fluent-svelte's CalendarDatePicker.svelte — a button that shows the picked date (or
/// a placeholder) and pops FluentCalendarView open beneath it on click.
///
/// Per this project's own stated convention (NumberBox's Expanded popout, ComboBox/AutoSuggestBox's
/// dropdown), this is self-contained/absolutely-positioned rather than routed through
/// IOverlayService — fluent-svelte's own version wires CalendarView into its generic Flyout wrapper,
/// but every other "needs exact trigger width/position, not a generic floating panel" composite in
/// this codebase has deliberately skipped the overlay service, and a date picker popout is the same
/// shape of problem.
///
/// Closing behavior mirrors fluent-svelte: picking a date closes the popout (its CalendarView listens
/// for <c>on:change</c> and sets <c>open = false</c>); Escape closes it; and losing focus entirely
/// closes it. The latter relies on FluentCalendarViewItem's cells (and this component's own header/
/// pagination buttons) using <c>@onmousedown:preventDefault</c> so clicking inside the popout never
/// actually blurs the trigger button in the first place — without that, every internal focus change
/// would incorrectly bubble a focusout up to this root and close the popout mid-click, the same
/// problem AutoSuggestBox's dropdown solved the same way.
/// </summary>
public partial class FluentCalendarDatePicker : ComponentBase
{
    [Parameter] public DateTime? Value { get; set; }

    [Parameter] public EventCallback<DateTime?> ValueChanged { get; set; }

    [Parameter] public DateTime? Min { get; set; }

    [Parameter] public DateTime? Max { get; set; }

    [Parameter] public IReadOnlyList<DateTime>? Blackout { get; set; }

    [Parameter] public int WeekStart { get; set; }

    [Parameter] public string? Locale { get; set; }

    [Parameter] public string Placeholder { get; set; } = "Pick a date";

    [Parameter] public bool Disabled { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private ElementReference _root;
    private bool _open;

    // Exit-animation state — keeps the popout in the DOM while the CSS scale-out plays.
    // Uses Task.Delay(150) matching --duration-fast rather than JS animationend, since:
    //   a) The popout element is the non-animated outer wrapper (@ref is on it, not the inner
    //      animated div), so waitForExitAnimation would listen on the wrong element and always
    //      fall through to its 400ms safety-net timeout — causing visible lag on every close.
    //   b) The calendar popout is a fixed-size widget; it needs no JS height measurement.
    //   c) Task.Delay fires in Blazor's own sync context with no JS round-trip overhead.
    private bool _closing;
    private int _closeGeneration;

    private const int ExitAnimationMs = 150; // matches --duration-fast

    private CultureInfo Culture => string.IsNullOrEmpty(Locale) ? CultureInfo.CurrentCulture : new CultureInfo(Locale);

    private void OpenPopout()
    {
        if (Disabled) return;
        _closeGeneration++;
        _open = true;
        _closing = false;
    }

    private void ClosePopout()
    {
        if (!_open && !_closing) return;
        _open = false;
        _closing = true;
        var generation = ++_closeGeneration;
        _ = FinishClosingAsync(generation);
    }

    private async Task FinishClosingAsync(int generation)
    {
        await Task.Delay(ExitAnimationMs);

        if (generation == _closeGeneration && _closing)
        {
            _closing = false;
            StateHasChanged();
        }
    }

    private Task ToggleOpenAsync()
    {
        if (_open || _closing)
            ClosePopout();
        else
            OpenPopout();
        return Task.CompletedTask;
    }

    private async Task OnDateChangedAsync(DateTime? value)
    {
        Value = value;
        await ValueChanged.InvokeAsync(value);
        ClosePopout();
    }

    private Task OnFocusOutAsync(FocusEventArgs e)
    {
        ClosePopout();
        return Task.CompletedTask;
    }

    private Task OnKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
            ClosePopout();
        return Task.CompletedTask;
    }
}
