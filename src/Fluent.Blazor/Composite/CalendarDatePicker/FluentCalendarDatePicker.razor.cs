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

    private CultureInfo Culture => string.IsNullOrEmpty(Locale) ? CultureInfo.CurrentCulture : new CultureInfo(Locale);

    private Task ToggleOpenAsync()
    {
        if (!Disabled)
        {
            _open = !_open;
        }

        return Task.CompletedTask;
    }

    private async Task OnDateChangedAsync(DateTime? value)
    {
        Value = value;
        await ValueChanged.InvokeAsync(value);
        _open = false;
    }

    private Task OnFocusOutAsync(FocusEventArgs e)
    {
        _open = false;
        return Task.CompletedTask;
    }

    private Task OnKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
        {
            _open = false;
        }

        return Task.CompletedTask;
    }
}
