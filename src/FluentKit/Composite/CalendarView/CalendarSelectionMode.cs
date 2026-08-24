namespace FluentKit.Composite;

/// <summary>
/// Selection behavior for <see cref="FluentCalendarView"/> and <see cref="FluentCalendarDatePicker"/>.
/// Not part of fluent-svelte's original API (it only ever exposed a <c>multiple</c> boolean) — added
/// here as a superset so applications can opt into a third, common date-range picking pattern (à la
/// Fluent UI React's DatePicker range selection) without overloading a bool.
/// </summary>
public enum CalendarSelectionMode
{
    /// <summary>Exactly one date selected at a time. Uses <see cref="FluentCalendarView.Value"/>.</summary>
    Single,

    /// <summary>Any number of independently toggled dates. Uses <see cref="FluentCalendarView.Values"/>.</summary>
    Multiple,

    /// <summary>A contiguous start/end date range. Also uses <see cref="FluentCalendarView.Values"/>,
    /// interpreted as at most two entries: <c>[start]</c> or <c>[start, end]</c> (always kept in
    /// chronological order). First click starts a new range; second click completes it; a third click
    /// starts over.</summary>
    Range
}
