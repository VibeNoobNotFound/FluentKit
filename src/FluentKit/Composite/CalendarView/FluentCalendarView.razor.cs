using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FluentKit.Composite;

/// <summary>
/// Ported from fluent-svelte's CalendarView.svelte — lets a user browse and pick a date across three
/// drill levels (days/months/years). Supports single-select (<see cref="Value"/>), multi-select
/// (<see cref="Values"/>), and range-select (also <see cref="Values"/>, interpreted as a chronological
/// [start, end] pair) via <see cref="SelectionMode"/> — svelte's source only ever had a boolean
/// <c>multiple</c> prop; <see cref="Multiple"/> is kept as a back-compat alias for
/// <c>SelectionMode="CalendarSelectionMode.Multiple"</c>, and Range is a net-new addition on top of
/// the ported behavior.
///
/// Animations: fluent-svelte uses Svelte transition directives (fadeScale on view switch, fly on
/// page turn). Blazor has no direct equivalent, so the same visual effect is reproduced with plain
/// CSS keyframe animations in FluentCalendarView.razor.css, re-triggered every render by keying the
/// table/tbody on (View, Page) via @key — changing a @key forces Blazor to tear down and recreate
/// the element, which restarts the CSS animation named by the relevant direction class
/// (fluent-calendar-view-table--anim-up/down, fluent-calendar-view-body--anim-up/down).
///
/// Remaining deviation: arrow-key navigation now walks off the current page's edge, auto-pages, and
/// refocuses the newly revealed cell (matching svelte) — see OnGridKeyDownAsync.
/// </summary>
public partial class FluentCalendarView : ComponentBase
{
    /// <summary>The currently selected date, or null. Two-way bindable. Ignored when <see cref="Multiple"/> is true.</summary>
    [Parameter] public DateTime? Value { get; set; }

    [Parameter] public EventCallback<DateTime?> ValueChanged { get; set; }

    /// <summary>Mirrors fluent-svelte's <c>on:change</c> — fires whenever a day is picked/unpicked,
    /// same payload as ValueChanged. Kept separate so applications can distinguish "value changed
    /// because the user just clicked a day" from any other source of a Value update.</summary>
    [Parameter] public EventCallback<DateTime?> Change { get; set; }

    /// <summary>Enables multi-date selection, matching fluent-svelte's <c>multiple</c> prop. When true,
    /// clicking days toggles membership in <see cref="Values"/> instead of setting <see cref="Value"/>.
    /// Superseded by <see cref="SelectionMode"/> — kept for back-compat; setting this true without
    /// touching <see cref="SelectionMode"/> is equivalent to <c>SelectionMode="CalendarSelectionMode.Multiple"</c>.</summary>
    [Parameter] public bool Multiple { get; set; }

    /// <summary>Single (default), Multiple, or Range. Net-new on top of fluent-svelte's original
    /// single/multiple-only API. See <see cref="CalendarSelectionMode"/> for what each does.</summary>
    [Parameter] public CalendarSelectionMode SelectionMode { get; set; } = CalendarSelectionMode.Single;

    /// <summary>The currently selected dates when <see cref="Mode"/> is Multiple or Range. Two-way
    /// bindable. In Range mode this holds at most two entries, always chronologically ordered:
    /// <c>[start]</c> while a range is being picked, or <c>[start, end]</c> once complete.</summary>
    [Parameter] public IReadOnlyList<DateTime> Values { get; set; } = Array.Empty<DateTime>();

    [Parameter] public EventCallback<IReadOnlyList<DateTime>> ValuesChanged { get; set; }

    /// <summary>Mirrors fluent-svelte's <c>on:change</c> payload for multi-select mode; also used for
    /// Range mode, firing after both the start and (later) the completing end click.</summary>
    [Parameter] public EventCallback<IReadOnlyList<DateTime>> ValuesChange { get; set; }

    [Parameter] public DateTime? Min { get; set; }

    [Parameter] public DateTime? Max { get; set; }

    /// <summary>Dates the user cannot select — rendered with a diagonal strike, matching
    /// fluent-svelte's <c>blackout</c> prop.</summary>
    [Parameter] public IReadOnlyList<DateTime>? Blackout { get; set; }

    /// <summary>The current drill level. Two-way bindable — clicking the header or a month/year cell
    /// changes it, same as fluent-svelte letting the user manually change view via header clicks.</summary>
    [Parameter] public CalendarViewMode View { get; set; } = CalendarViewMode.Days;

    [Parameter] public EventCallback<CalendarViewMode> ViewChanged { get; set; }

    /// <summary>0 = Sunday .. 6 = Saturday, matching fluent-svelte's <c>weekStart</c>.</summary>
    [Parameter] public int WeekStart { get; set; }

    /// <summary>Shows a small overline label (month abbreviation / year) on the first day of each
    /// month or first month of each year, matching fluent-svelte's <c>headers</c> prop.</summary>
    [Parameter] public bool Headers { get; set; }

    /// <summary>Locale used for month/weekday names and header text. Defaults to the current culture
    /// if unset, matching fluent-svelte inferring from <c>navigator.language</c>.</summary>
    [Parameter] public string? Locale { get; set; }

    /// <summary>INTERNAL USE — applies the floating/flyout-shadow presentation used when embedded in
    /// CalendarDatePicker's popout, mirroring fluent-svelte's <c>__floating</c> prop.</summary>
    [Parameter] public bool Floating { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private DateTime Page { get; set; }

    private DateTime Today { get; } = DateTime.Today;

    /// <summary>"up" | "down" | "neutral" — drives which CSS animation class is applied to the page
    /// body (tbody) on the next re-render. Mirrors svelte's pageAnimationDirection.</summary>
    private string _pageAnimDirection = "neutral";

    /// <summary>Same idea but for the days/months/years view switch, mirrors viewAnimationDirection.</summary>
    private string _viewAnimDirection = "neutral";

    /// <summary>Bumped on every page/view change so @key forces a DOM remount, which is what actually
    /// restarts the CSS animation (a class name alone won't re-trigger on an unchanged element).</summary>
    private int _renderGeneration;

    private readonly FluentCalendarViewItem?[] _dayItemRefs = new FluentCalendarViewItem?[42];
    private readonly FluentCalendarViewItem?[] _monthItemRefs = new FluentCalendarViewItem?[16];
    private readonly FluentCalendarViewItem?[] _yearItemRefs = new FluentCalendarViewItem?[16];

    protected override void OnInitialized()
    {
        var anchor = Value ?? (Mode != CalendarSelectionMode.Single && Values.Count > 0 ? Values[0] : (DateTime?)null) ?? Today;
        if (Min.HasValue && anchor < Min.Value)
        {
            anchor = Min.Value;
        }
        else if (Max.HasValue && anchor > Max.Value)
        {
            anchor = Max.Value;
        }

        Page = new DateTime(anchor.Year, anchor.Month, 1);
    }

    /// <summary>Resolves the legacy <see cref="Multiple"/> bool and the newer <see cref="SelectionMode"/>
    /// enum into one effective mode: an explicit non-default SelectionMode always wins; otherwise
    /// Multiple=true maps to Multiple, and everything else is Single.</summary>
    private CalendarSelectionMode Mode => SelectionMode != CalendarSelectionMode.Single
        ? SelectionMode
        : (Multiple ? CalendarSelectionMode.Multiple : CalendarSelectionMode.Single);

    private DateTime? RangeStart => Values.Count > 0 ? Values[0] : null;

    private DateTime? RangeEnd => Values.Count > 1 ? Values[1] : null;

    private CultureInfo Culture => string.IsNullOrEmpty(Locale) ? CultureInfo.CurrentCulture : new CultureInfo(Locale);

    private string HeaderText => View switch
    {
        CalendarViewMode.Days => Page.ToString("MMMM yyyy", Culture),
        CalendarViewMode.Months => Page.ToString("yyyy", Culture),
        _ => DecadeLabel()
    };

    private string DecadeLabel()
    {
        var start = Page.Year / 10 * 10;
        return $"{start} - {start + 9}";
    }

    private DateTime GetPageByOffset(int offset) => GetPageByOffset(offset, Page, View);

    private static DateTime GetPageByOffset(int offset, DateTime page, CalendarViewMode view) => view switch
    {
        CalendarViewMode.Days => page.AddMonths(offset),
        CalendarViewMode.Months => new DateTime(page.Year + offset, 1, 1),
        _ => new DateTime((page.Year / 10 * 10) + (offset * 10), 1, 1)
    };

    private bool PrevDisabled => Min.HasValue && Min.Value >= Page;

    private bool NextDisabled => Max.HasValue && Max.Value < GetPageByOffset(1);

    private void GoPage(int amount, string? directionOverride = null)
    {
        Page = GetPageByOffset(amount);
        _pageAnimDirection = directionOverride ?? (amount <= -1 ? "up" : amount >= 1 ? "down" : "neutral");
        _renderGeneration++;
    }

    /// <summary>Cooldown gate for wheel/swipe-driven paging: a single trackpad "scroll" gesture fires
    /// many small onwheel deltas in quick succession, and a single finger swipe likewise fires many
    /// touchmove events — without this we'd fly through several months per gesture instead of the
    /// intended "one gesture = one page" feel (matching how a real WinUI/touch calendar's paging
    /// snap-scroll behaves, not a free-scrolling list). Reset once enough time has passed that a new
    /// gesture is presumably starting.</summary>
    private DateTime _lastGesturePage = DateTime.MinValue;

    private static readonly TimeSpan GesturePageCooldown = TimeSpan.FromMilliseconds(350);

    /// <summary>Wheel-to-page: scrolling with the pointer/trackpad over the calendar turns the page
    /// instead of scrolling the surrounding document. Bound with @onwheel:preventDefault on the table
    /// wrapper (see FluentCalendarView.razor) so the page behind the calendar never moves at all —
    /// the browser only lets preventDefault suppress scroll if it's called from a non-passive
    /// listener, which is what Blazor's :preventDefault modifier wires up for us server/wasm-side.
    /// Vertical wheel delta (deltaY) is used regardless of axis, since trackpads commonly report
    /// two-finger vertical scroll as the natural "turn the page" gesture, matching scroll-to-navigate
    /// UIs elsewhere (e.g. image galleries, PDF viewers).</summary>
    private void OnWheel(WheelEventArgs e)
    {
        if (Math.Abs(e.DeltaY) < 1)
        {
            return;
        }

        GestureGoPage(e.DeltaY > 0 ? 1 : -1);
    }

    /// <summary>Touch-swipe-to-page companion to <see cref="OnWheel"/>: tracks the first touch point's
    /// horizontal travel and, once it crosses a small threshold, turns the page the same way a
    /// left/right swipe would in a native mobile calendar (swipe left = next, swipe right = previous)
    /// — horizontal rather than vertical here, since a vertical swipe on a touchscreen is much more
    /// likely to be the user trying to scroll the surrounding page, which we don't want to hijack.</summary>
    private double? _touchStartX;
    private double? _touchStartY;
    private bool _touchIsHorizontalSwipe;

    private const double SwipeThresholdPx = 40;

    private void OnTouchStart(TouchEventArgs e)
    {
        _touchStartX = e.Touches.Length > 0 ? e.Touches[0].ClientX : null;
        _touchStartY = e.Touches.Length > 0 ? e.Touches[0].ClientY : null;
        _touchIsHorizontalSwipe = false;
    }

    /// <summary>Deliberately does NOT unconditionally preventDefault touchmove (see markup): Blazor's
    /// @ontouchmove:preventDefault is a static per-element attribute, not something we can flip per
    /// gesture, and blocking every touchmove would also swallow a user's ordinary vertical scroll of
    /// the surrounding page whenever their finger happens to start over the calendar. Instead the
    /// wrapper is given `touch-action: pan-y` in CSS, which tells the browser up front "let vertical
    /// scrolling pass through natively, and leave horizontal gestures to JS" — the browser then only
    /// suppresses its own native scroll on the horizontal axis, exactly the axis this handler cares
    /// about, without any interop needed to decide per-touch.</summary>
    private void OnTouchMove(TouchEventArgs e)
    {
        if (_touchStartX is not { } startX || _touchStartY is not { } startY || e.Touches.Length == 0)
        {
            return;
        }

        var deltaX = e.Touches[0].ClientX - startX;
        var deltaY = e.Touches[0].ClientY - startY;

        if (!_touchIsHorizontalSwipe && Math.Abs(deltaX) < SwipeThresholdPx)
        {
            return;
        }

        // Once movement is clearly more vertical than horizontal, treat this touch as a page-scroll
        // gesture, not a calendar swipe, and stop paying attention to it for the rest of this touch.
        if (!_touchIsHorizontalSwipe && Math.Abs(deltaY) > Math.Abs(deltaX))
        {
            _touchStartX = null;
            _touchStartY = null;
            return;
        }

        _touchIsHorizontalSwipe = true;
        _touchStartX = null; // handle the swipe so we don't re-trigger every subsequent touchmove tick
        GestureGoPage(deltaX < 0 ? 1 : -1);
    }

    private void OnTouchEnd(TouchEventArgs e)
    {
        _touchStartX = null;
        _touchStartY = null;
        _touchIsHorizontalSwipe = false;
    }

    /// <summary>Shared by OnWheel/OnTouchMove: same GoPage the prev/next buttons use (so the existing
    /// slide animation plays), gated by a short cooldown and clamped to Min/Max the same way the
    /// buttons already are via PrevDisabled/NextDisabled.</summary>
    private void GestureGoPage(int amount)
    {
        var now = DateTime.UtcNow;
        if (now - _lastGesturePage < GesturePageCooldown)
        {
            return;
        }

        if ((amount < 0 && PrevDisabled) || (amount > 0 && NextDisabled))
        {
            return;
        }

        _lastGesturePage = now;
        GoPage(amount);
        StateHasChanged();
    }

    private async Task SetViewAsync(CalendarViewMode view)
    {
        var previous = View;
        _viewAnimDirection =
            (previous == CalendarViewMode.Days && view == CalendarViewMode.Months) ||
            (previous == CalendarViewMode.Months && view == CalendarViewMode.Years)
                ? "up"
                : (previous == CalendarViewMode.Years && view == CalendarViewMode.Months) ||
                  (previous == CalendarViewMode.Months && view == CalendarViewMode.Days)
                    ? "down"
                    : "neutral";
        _pageAnimDirection = "neutral";
        _renderGeneration++;

        View = view;
        await ViewChanged.InvokeAsync(view);
    }

    private async Task HeaderClicked() =>
        await SetViewAsync(View == CalendarViewMode.Days ? CalendarViewMode.Months : CalendarViewMode.Years);

    private static List<DateTime> GetMonthDays(int year, int month)
    {
        var days = new List<DateTime>();
        var length = DateTime.DaysInMonth(year, month);
        for (var i = 1; i <= length; i++)
        {
            days.Add(new DateTime(year, month, i));
        }

        return days;
    }

    private List<DateTime> GetCalendarDays(DateTime date)
    {
        var year = date.Year;
        var month = date.Month;
        var firstWeekday = (int)new DateTime(year, month, 1).DayOfWeek;
        const int calendarRows = 6;

        var lastMonth = month - 1;
        var lastMonthYear = year;
        var nextMonth = month + 1;
        var nextMonthYear = year;

        var daysBefore = (firstWeekday - WeekStart + 7) % 7;
        var days = new List<DateTime>();
        if (daysBefore > 0)
        {
            if (lastMonth == 0)
            {
                lastMonth = 12;
                lastMonthYear = year - 1;
            }

            var prevMonthDays = GetMonthDays(lastMonthYear, lastMonth);
            days.AddRange(prevMonthDays.Skip(Math.Max(0, prevMonthDays.Count - daysBefore)));
        }

        days.AddRange(GetMonthDays(year, month));

        if (nextMonth == 13)
        {
            nextMonth = 1;
            nextMonthYear = year + 1;
        }

        var daysAfter = 7 * calendarRows - days.Count;
        days.AddRange(GetMonthDays(nextMonthYear, nextMonth).Take(daysAfter));

        return days;
    }

    private static List<DateTime> GetCalendarMonths(DateTime date)
    {
        var months = new List<DateTime>();
        for (var m = 1; m <= 12; m++)
        {
            months.Add(new DateTime(date.Year, m, 1));
        }

        for (var m = 1; m <= 4; m++)
        {
            months.Add(new DateTime(date.Year + 1, m, 1));
        }

        return months;
    }

    private static List<DateTime> GetCalendarYears(DateTime date)
    {
        var decadeStart = date.Year / 10 * 10;
        var years = new List<DateTime>();

        if (decadeStart % 20 == 0)
        {
            years.Add(new DateTime(decadeStart - 2, 1, 1));
            years.Add(new DateTime(decadeStart - 1, 1, 1));
            for (var i = 0; i < 12; i++)
            {
                years.Add(new DateTime(decadeStart + i, 1, 1));
            }

            for (var i = 0; i < 2; i++)
            {
                years.Add(new DateTime(decadeStart + 12 + i, 1, 1));
            }
        }
        else
        {
            for (var i = 0; i < 12; i++)
            {
                years.Add(new DateTime(decadeStart + i, 1, 1));
            }

            for (var i = 0; i < 4; i++)
            {
                years.Add(new DateTime(decadeStart + 12 + i, 1, 1));
            }
        }

        // fluent-svelte always renders exactly 4 rows x 4 cols (16 cells) regardless of how many
        // extra lead-in years the decadeStart%20==0 branch above adds — trim to that fixed window.
        return years.Take(16).ToList();
    }

    private static bool SameDay(DateTime a, DateTime b) => a.Date == b.Date;

    private static bool SameMonth(DateTime a, DateTime b) => a.Year == b.Year && a.Month == b.Month;

    private static bool SameYear(DateTime a, DateTime b) => a.Year == b.Year;

    private static bool SameDecade(DateTime a, DateTime b) => a.Year / 10 == b.Year / 10;

    private bool IsBlackout(DateTime day) => Blackout is not null && Blackout.Any(b => SameDay(b, day));

    private bool IsSelectedDay(DateTime day) => Mode switch
    {
        CalendarSelectionMode.Multiple => Values.Any(v => SameDay(v, day)),
        CalendarSelectionMode.Range => IsRangeStartDay(day) || IsRangeEndDay(day),
        _ => Value.HasValue && SameDay(Value.Value, day)
    };

    private bool IsRangeStartDay(DateTime day) => RangeStart.HasValue && SameDay(RangeStart.Value, day);

    private bool IsRangeEndDay(DateTime day) => RangeEnd.HasValue && SameDay(RangeEnd.Value, day);

    /// <summary>Strictly between start and end, exclusive of both edges (edges get the round pill
    /// via IsRangeStartDay/IsRangeEndDay + the --range-start/--range-end CSS instead).</summary>
    private bool IsInRange(DateTime day) =>
        RangeStart.HasValue && RangeEnd.HasValue && day.Date > RangeStart.Value.Date && day.Date < RangeEnd.Value.Date;

    private async Task SelectDayAsync(DateTime day)
    {
        if ((Min.HasValue && Min.Value > day) || (Max.HasValue && Max.Value < day) || IsBlackout(day))
        {
            return;
        }

        if (Mode == CalendarSelectionMode.Multiple)
        {
            var next = Values.Any(v => SameDay(v, day))
                ? Values.Where(v => !SameDay(v, day)).ToList()
                : Values.Append(day).ToList();

            Values = next;
            await ValuesChanged.InvokeAsync(next);
            await ValuesChange.InvokeAsync(next);
            return;
        }

        if (Mode == CalendarSelectionMode.Range)
        {
            await SelectRangeDayAsync(day);
            return;
        }

        Value = Value.HasValue && SameDay(Value.Value, day) ? null : day;
        await ValueChanged.InvokeAsync(Value);
        await Change.InvokeAsync(Value);
    }

    /// <summary>Click 1 (no range yet, or a completed [start, end] already on the books): starts a
    /// fresh range at that day. Click 2 (only a start is set): completes the range, swapping the two
    /// dates first if the user picked an end earlier than the start.</summary>
    private async Task SelectRangeDayAsync(DateTime day)
    {
        List<DateTime> next;

        if (!RangeStart.HasValue || RangeEnd.HasValue)
        {
            next = new List<DateTime> { day };
        }
        else
        {
            next = day.Date < RangeStart.Value.Date
                ? new List<DateTime> { day, RangeStart.Value }
                : new List<DateTime> { RangeStart.Value, day };
        }

        Values = next;
        await ValuesChanged.InvokeAsync(next);
        await ValuesChange.InvokeAsync(next);
    }

    private void SelectMonth(DateTime month)
    {
        Page = new DateTime(month.Year, month.Month, 1);
        _ = SetViewAsync(CalendarViewMode.Days);
    }

    private void SelectYear(DateTime year)
    {
        Page = new DateTime(year.Year, Page.Month, 1);
        _ = SetViewAsync(CalendarViewMode.Months);
    }

    /// <summary>Roving-tabindex arrow-key navigation. Moves focus within the current page's grid, and
    /// when a move would walk off the rendered edge, pages automatically and refocuses the newly
    /// revealed cell in the new page — matching fluent-svelte's cross-page keyboard walk.
    /// <paramref name="columns"/> is 7 for the day grid, 4 for the month/year grids.</summary>
    private async Task OnGridKeyDownAsync(KeyboardEventArgs e, int index, int columns, int count, FluentCalendarViewItem?[] refs)
    {
        var delta = e.Key switch
        {
            "ArrowLeft" => -1,
            "ArrowRight" => 1,
            "ArrowUp" => -columns,
            "ArrowDown" => columns,
            "Home" => -(index % columns),
            "End" => columns - 1 - index % columns,
            _ => 0
        };

        if (delta == 0)
        {
            return;
        }

        var newIndex = index + delta;

        if (newIndex >= 0 && newIndex < count && newIndex < refs.Length)
        {
            if (refs[newIndex] is { } target)
            {
                await target.ButtonRef.FocusAsync();
            }

            return;
        }

        // Walked off the current page — figure out which cell on the *next* page corresponds to the
        // date we were trying to reach, page there, then focus it once re-rendered.
        var pagingForward = newIndex >= count;
        var pageOffset = pagingForward ? 1 : -1;

        GoPage(pageOffset, "neutral");
        StateHasChanged();
        await Task.Yield();

        var targetIndex = pagingForward ? newIndex - count : count + newIndex;
        var newRefs = columns == 7 ? _dayItemRefs : refs == _monthItemRefs ? _monthItemRefs : _yearItemRefs;

        if (targetIndex >= 0 && targetIndex < newRefs.Length && newRefs[targetIndex] is { } newTarget)
        {
            await newTarget.ButtonRef.FocusAsync();
        }
    }
}
