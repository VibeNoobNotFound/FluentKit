using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Fluent.Blazor.Composite;

/// <summary>
/// Ported from fluent-svelte's CalendarView.svelte — lets a user browse and pick a date across three
/// drill levels (days/months/years). Supports both single-select (<see cref="Value"/>) and
/// multi-select (<see cref="Multiple"/> + <see cref="Values"/>), matching the svelte source's
/// <c>value</c>/<c>multiple</c> props.
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
    /// same payload as ValueChanged. Kept separate so consumers can distinguish "value changed
    /// because the user just clicked a day" from any other source of a Value update.</summary>
    [Parameter] public EventCallback<DateTime?> Change { get; set; }

    /// <summary>Enables multi-date selection, matching fluent-svelte's <c>multiple</c> prop. When true,
    /// clicking days toggles membership in <see cref="Values"/> instead of setting <see cref="Value"/>.</summary>
    [Parameter] public bool Multiple { get; set; }

    /// <summary>The currently selected dates when <see cref="Multiple"/> is true. Two-way bindable.</summary>
    [Parameter] public IReadOnlyList<DateTime> Values { get; set; } = Array.Empty<DateTime>();

    [Parameter] public EventCallback<IReadOnlyList<DateTime>> ValuesChanged { get; set; }

    /// <summary>Mirrors fluent-svelte's <c>on:change</c> payload for multi-select mode.</summary>
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
        var anchor = Value ?? (Multiple && Values.Count > 0 ? Values[0] : (DateTime?)null) ?? Today;
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

    private bool IsSelectedDay(DateTime day) => Multiple
        ? Values.Any(v => SameDay(v, day))
        : Value.HasValue && SameDay(Value.Value, day);

    private async Task SelectDayAsync(DateTime day)
    {
        if ((Min.HasValue && Min.Value > day) || (Max.HasValue && Max.Value < day) || IsBlackout(day))
        {
            return;
        }

        if (Multiple)
        {
            var next = Values.Any(v => SameDay(v, day))
                ? Values.Where(v => !SameDay(v, day)).ToList()
                : Values.Append(day).ToList();

            Values = next;
            await ValuesChanged.InvokeAsync(next);
            await ValuesChange.InvokeAsync(next);
            return;
        }

        Value = Value.HasValue && SameDay(Value.Value, day) ? null : day;
        await ValueChanged.InvokeAsync(Value);
        await Change.InvokeAsync(Value);
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
