using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace FluentKit.Composite;

/// <summary>
/// A brand-new control, not a fluent-svelte port (fluent-svelte has no TimePicker) — modeled directly
/// on WinUI 3's TimePicker + TimePickerFlyout (see WinUI-Gallery's TimePickerPage.xaml and
/// microsoft-ui-xaml's control template) rather than any existing web time-picker convention. Key
/// WinUI behaviors reproduced:
///   - Three snap-scrolling columns (Hour, Minute, and AM/PM unless <see cref="ClockIdentifier"/> is
///     TwentyFourHour) with the vertically-centered row being the "selected" one — WinUI's flyout
///     uses a LoopingSelector per column; the web has no equivalent primitive, so this is built as a
///     plain scroll-snap-align:center list per column with IntersectionObserver-free selection
///     tracking done by comparing each item's scroll offset against the container's on scroll (see
///     the JS interop module) — good enough fidelity without inventing a full virtualized looping
///     selector for a first cut.
///   - Values are staged locally while the flyout is open (WinUI: dragging the columns doesn't commit
///     until you tap the accept/checkmark glyph) and only pushed to <see cref="SelectedTime"/> on
///     Accept; Cancel (or Escape, or outside click) discards the in-progress scroll position and
///     reverts to the last committed value.
///   - <see cref="MinuteIncrement"/> matches WinUI's property of the same name — restricts the
///     minute column to that step (e.g. 15 => :00/:15/:30/:45 only).
/// Architecture matches this repo's other trigger+popout composites (FluentCalendarDatePicker,
/// FluentComboBox): self-contained absolutely-positioned popout, not routed through IOverlayService.
/// </summary>
public partial class FluentTimePicker : ComponentBase, IAsyncDisposable
{
    /// <summary>The committed time-of-day. Two-way bindable. Null means "no time picked yet" and
    /// shows <see cref="Placeholder"/> on the trigger button, matching WinUI's own unset TimePicker.</summary>
    [Parameter] public TimeSpan? SelectedTime { get; set; }

    [Parameter] public EventCallback<TimeSpan?> SelectedTimeChanged { get; set; }

    /// <summary>Fires only when the user accepts a new value via the flyout's checkmark button —
    /// mirrors WinUI TimePicker's <c>SelectedTimeChanged</c> event semantics (as distinct from the
    /// two-way bind itself), matching FluentCalendarView's own ValueChanged/Change split convention.</summary>
    [Parameter] public EventCallback<TimeSpan?> TimeChanged { get; set; }

    /// <summary>Optional label shown above the trigger button, matching WinUI TimePicker's <c>Header</c>.</summary>
    [Parameter] public string? Header { get; set; }

    /// <summary>Restricts the minute column to this step. Matches WinUI's <c>MinuteIncrement</c>
    /// (legal range there is 1-30; enforced the same way here via <see cref="OnParametersSet"/>).</summary>
    [Parameter] public int MinuteIncrement { get; set; } = 1;

    /// <summary>12-hour (default, with AM/PM column) or 24-hour. Matches WinUI's <c>ClockIdentifier</c>.</summary>
    [Parameter] public TimeClockIdentifier ClockIdentifier { get; set; } = TimeClockIdentifier.TwelveHour;

    [Parameter] public string Placeholder { get; set; } = "Pick a time";

    [Parameter] public bool Disabled { get; set; }

    [Parameter] public string? Locale { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private ElementReference _root;
    private ElementReference _hourColumnRef;
    private ElementReference _minuteColumnRef;
    private ElementReference _periodColumnRef;
    private IJSObjectReference? _module;
    private DotNetObjectReference<FluentTimePicker>? _selfReference;
    private bool _listenersAttached;

    private bool _open;
    private bool _closing;
    private int _closeGeneration;
    private const int ExitAnimationMs = 150; // matches --duration-fast, same convention as FluentCalendarDatePicker

    // Staged (uncommitted) selection while the flyout is open — only written back to SelectedTime on
    // Accept. Defaults to "now" the first time the flyout opens with no prior value, matching WinUI
    // (an unset TimePicker's flyout opens centered on the current time, not midnight).
    private int _stagedHour;
    private int _stagedMinute;
    private bool _stagedIsPm;

    private CultureInfo Culture => string.IsNullOrEmpty(Locale) ? CultureInfo.CurrentCulture : new CultureInfo(Locale);

    private bool Is24Hour => ClockIdentifier == TimeClockIdentifier.TwentyFourHour;

    protected override void OnParametersSet()
    {
        if (MinuteIncrement is < 1 or > 30)
        {
            MinuteIncrement = Math.Clamp(MinuteIncrement, 1, 30);
        }
    }

    /// <summary>Formatted trigger-button label, e.g. "3:45 PM" / "15:45" — short time pattern per
    /// clock identifier, respecting <see cref="Culture"/> for the separator/AM-PM string itself.</summary>
    private string DisplayLabel => SelectedTime is { } t
        ? FormatTime(t.Hours, t.Minutes)
        : Placeholder;

    private string FormatTime(int hour24, int minute)
    {
        var dt = new DateTime(2000, 1, 1, hour24, minute, 0);
        return dt.ToString(Is24Hour ? "HH:mm" : "h:mm tt", Culture);
    }

    // ----- Column data -----

    /// <summary>1-12 for a 12-hour clock, 0-23 for 24-hour — matches WinUI's own hour range per
    /// ClockIdentifier (12-hour never shows a 0/"12 AM" is still displayed as 12, not 0).</summary>
    private IReadOnlyList<int> HourValues => Is24Hour
        ? Enumerable.Range(0, 24).ToList()
        : Enumerable.Range(1, 12).ToList();

    private IReadOnlyList<int> MinuteValues => Enumerable.Range(0, 60 / MinuteIncrement).Select(i => i * MinuteIncrement).ToList();

    private static readonly string[] PeriodValues = { "AM", "PM" };

    private string HourLabel(int h) => h.ToString(Culture);

    private string MinuteLabel(int m) => m.ToString("D2", Culture);

    // ----- Open/close -----

    private async Task ToggleOpenAsync()
    {
        if (Disabled) return;

        if (_open || _closing)
        {
            await ClosePopoutAsync(commit: false);
        }
        else
        {
            await OpenPopoutAsync();
        }
    }

    private async Task OpenPopoutAsync()
    {
        var anchor = SelectedTime ?? DateTime.Now.TimeOfDay;
        var hour24 = anchor.Hours;

        _stagedIsPm = hour24 >= 12;
        _stagedHour = Is24Hour ? hour24 : ToTwelveHour(hour24);
        _stagedMinute = RoundToIncrement(anchor.Minutes);

        _closeGeneration++;
        _open = true;
        _closing = false;
        StateHasChanged();

        // Scroll each column to its staged value once the popout's DOM exists (next render), so the
        // flyout opens with the current/previous time already centered rather than scrolled to 0 —
        // matching WinUI opening the flyout pre-scrolled to the existing SelectedTime.
        await Task.Yield();
        await ScrollColumnsToStagedAsync();
    }

    private async Task ClosePopoutAsync(bool commit)
    {
        if (!_open && !_closing) return;

        if (commit)
        {
            await CommitStagedAsync();
        }

        _open = false;
        _closing = true;
        var generation = ++_closeGeneration;
        StateHasChanged();

        await Task.Delay(ExitAnimationMs);

        if (generation == _closeGeneration && _closing)
        {
            _closing = false;
            _listenersAttached = false; // popout's <ul> columns are removed from the DOM (see razor's @if) — any
                                          // JS scroll listeners on them are already gone with the elements; this
                                          // just keeps our own bookkeeping in sync so the next open re-attaches.
            StateHasChanged();
        }
    }

    private async Task CommitStagedAsync()
    {
        var hour24 = Is24Hour ? _stagedHour : ToTwentyFourHour(_stagedHour, _stagedIsPm);
        var next = new TimeSpan(hour24, _stagedMinute, 0);

        SelectedTime = next;
        await SelectedTimeChanged.InvokeAsync(next);
        await TimeChanged.InvokeAsync(next);
    }

    private async Task OnAcceptClickedAsync() => await ClosePopoutAsync(commit: true);

    private async Task OnCancelClickedAsync() => await ClosePopoutAsync(commit: false);

    /// <summary>WinUI's flyout footer includes no explicit "Now" button in the default template, but
    /// the gallery's docs call out that re-opening always re-centers on "now" when unset — exposed
    /// here as an explicit convenience button instead, since a web popout doesn't have the same
    /// "just re-open it" muscle memory as a native flyout.</summary>
    private async Task OnNowClickedAsync()
    {
        var now = DateTime.Now.TimeOfDay;
        var hour24 = now.Hours;
        _stagedIsPm = hour24 >= 12;
        _stagedHour = Is24Hour ? hour24 : ToTwelveHour(hour24);
        _stagedMinute = RoundToIncrement(now.Minutes);
        await ScrollColumnsToStagedAsync();
    }

    /// <summary>Clicking a visible (but not yet centered) row selects it immediately — the column
    /// then animates into place so the clicked row ends up centered against the highlight band,
    /// rather than requiring the user to scroll-drag it there themselves. Matches WinUI's own
    /// TimePickerFlyout, where tapping any visible row in a column snaps that column to it.</summary>
    private async Task OnHourItemClickedAsync(int hour)
    {
        _stagedHour = hour;
        var index = Is24Hour ? hour : HourValues.ToList().IndexOf(hour);
        await EnsureModuleAsync();
        await _module!.InvokeVoidAsync("scrollToIndex", _hourColumnRef, index, true);
    }

    private async Task OnMinuteItemClickedAsync(int minute)
    {
        _stagedMinute = minute;
        var index = MinuteValues.ToList().IndexOf(minute);
        await EnsureModuleAsync();
        await _module!.InvokeVoidAsync("scrollToIndex", _minuteColumnRef, index, true);
    }

    private async Task OnPeriodItemClickedAsync(bool isPm)
    {
        _stagedIsPm = isPm;
        await EnsureModuleAsync();
        await _module!.InvokeVoidAsync("scrollToIndex", _periodColumnRef, isPm ? 1 : 0, true);
    }

    private static int ToTwelveHour(int hour24)
    {
        var h = hour24 % 12;
        return h == 0 ? 12 : h;
    }

    private static int ToTwentyFourHour(int hour12, bool isPm)
    {
        var h = hour12 % 12;
        return isPm ? h + 12 : h;
    }

    private int RoundToIncrement(int minute)
    {
        var steps = (int)Math.Round(minute / (double)MinuteIncrement);
        var rounded = steps * MinuteIncrement;
        return rounded >= 60 ? 60 - MinuteIncrement : rounded;
    }

    // ----- Scroll-driven selection (JS interop) -----

    private async Task EnsureModuleAsync()
    {
        _module ??= await JS.InvokeAsync<IJSObjectReference>(
            "import", "./_content/FluentKit/Composite/TimePicker/FluentTimePicker-interop.js");
    }

    private async Task ScrollColumnsToStagedAsync()
    {
        await EnsureModuleAsync();
        _selfReference ??= DotNetObjectReference.Create(this);

        if (!_listenersAttached)
        {
            await _module!.InvokeVoidAsync("attachColumn", _hourColumnRef, "hour", _selfReference);
            await _module.InvokeVoidAsync("attachColumn", _minuteColumnRef, "minute", _selfReference);
            if (!Is24Hour)
            {
                await _module.InvokeVoidAsync("attachColumn", _periodColumnRef, "period", _selfReference);
            }

            _listenersAttached = true;
        }

        var hourIndex = Is24Hour ? _stagedHour : HourValues.ToList().IndexOf(_stagedHour);
        var minuteIndex = MinuteValues.ToList().IndexOf(_stagedMinute);

        await _module!.InvokeVoidAsync("scrollToIndex", _hourColumnRef, hourIndex);
        await _module.InvokeVoidAsync("scrollToIndex", _minuteColumnRef, minuteIndex);

        if (!Is24Hour)
        {
            await _module.InvokeVoidAsync("scrollToIndex", _periodColumnRef, _stagedIsPm ? 1 : 0);
        }
    }

    /// <summary>Invoked from JS (see interop module) on scroll-settle for each column — updates the
    /// staged value only, exactly matching WinUI's "scrolling doesn't commit until Accept" behavior.
    /// Called via [JSInvokable] rather than polling scroll position from .NET on every frame, since a
    /// snap-scroll's "settled" moment is a scrollend/debounce concern that's cheaper and jank-free to
    /// resolve entirely in JS and report across the interop boundary just once per settle.</summary>
    [JSInvokable]
    public void OnColumnSettled(string column, int index)
    {
        switch (column)
        {
            case "hour":
                _stagedHour = HourValues[Math.Clamp(index, 0, HourValues.Count - 1)];
                break;
            case "minute":
                _stagedMinute = MinuteValues[Math.Clamp(index, 0, MinuteValues.Count - 1)];
                break;
            case "period":
                _stagedIsPm = index == 1;
                break;
        }

        StateHasChanged();
    }

    private Task OnFocusOutAsync(FocusEventArgs e) => ClosePopoutAsync(commit: false);

    private Task OnKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
        {
            return ClosePopoutAsync(commit: false);
        }

        if (e.Key == "Enter" && _open)
        {
            return ClosePopoutAsync(commit: true);
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit already torn down (Blazor Server navigation/disconnect) — nothing to clean up.
            }
        }

        _selfReference?.Dispose();
    }
}