namespace FluentKit.Composite;

/// <summary>
/// Mirrors WinUI TimePicker/TimePickerFlyout's <c>ClockIdentifier</c> property — which in WinUI is a
/// loosely-typed string ("12HourClock" / "24HourClock") since it's really a Windows.Globalization
/// clock system identifier. Re-exposed here as a proper enum (FluentKit convention — see
/// <see cref="CalendarViewMode"/>, <see cref="CalendarSelectionMode"/>) since a string parameter with
/// exactly two legal values isn't worth the stringly-typed footgun for a net-new (non-ported)
/// control that owes WinUI fidelity, not fluent-svelte source compatibility.
/// </summary>
public enum TimeClockIdentifier
{
    /// <summary>12-hour clock with an AM/PM column. Default, matches WinUI's own default.</summary>
    TwelveHour,

    /// <summary>24-hour clock, no AM/PM column, hour range 00-23.</summary>
    TwentyFourHour
}
