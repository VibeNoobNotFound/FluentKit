namespace FluentKit.Composite;

/// <summary>The three drill levels fluent-svelte's CalendarView cycles through — clicking the header
/// goes Days → Months → Years (and Years is a dead end, its header button is disabled); picking a
/// month drops back to Days, picking a year drops back to Months.</summary>
public enum CalendarViewMode
{
    Days,
    Months,
    Years
}
