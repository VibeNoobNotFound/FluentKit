using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

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
public partial class FluentCalendarDatePicker : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

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
    private ElementReference _popoutElement;
    private bool _open;

    // Same enter/exit animation shape as AutoSuggestBox/_closing pattern.
    private bool _closing;
    private int _closeGeneration;
    private IJSObjectReference? _module;

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
        if (!_open) return;
        _open = false;
        _closing = true;
        var generation = ++_closeGeneration;
        _ = FinishClosingAsync(generation);
    }

    private async Task FinishClosingAsync(int generation)
    {
        try
        {
            _module ??= await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/Fluent.Blazor/Overlay/overlay-interop.js");
            await _module.InvokeVoidAsync("waitForExitAnimation", _popoutElement);
        }
        catch (JSDisconnectedException) { return; }
        catch (ObjectDisposedException) { return; }

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

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
            await _module.DisposeAsync();
    }
}
