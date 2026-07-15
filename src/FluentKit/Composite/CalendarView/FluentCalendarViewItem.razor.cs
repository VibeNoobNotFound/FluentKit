using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FluentKit.Composite;

/// <summary>
/// Ported from fluent-svelte's CalendarViewItem.svelte — a single round day/month/year cell button.
/// Stateless: FluentCalendarView owns all selection/paging state and just tells each cell what to
/// look like. <c>@onmousedown:preventDefault</c> is set unconditionally (same trick AutoSuggestBox's
/// dropdown rows use) so clicking a cell inside CalendarDatePicker's self-contained popout doesn't
/// blur the picker's trigger button before OnClick fires — that blur would otherwise closed the
/// popout via focusout before the date selection registers.
/// </summary>
public partial class FluentCalendarViewItem : ComponentBase
{
    [Parameter] public bool Selected { get; set; }

    [Parameter] public bool Disabled { get; set; }

    [Parameter] public bool Blackout { get; set; }

    [Parameter] public bool Current { get; set; }

    [Parameter] public bool OutOfRange { get; set; }

    /// <summary>Range-selection mode only: true when this cell lies strictly between the range's
    /// start and end (exclusive of both edges), rendered with a continuous band background instead
    /// of the round single/multiple selection pill.</summary>
    [Parameter] public bool InRange { get; set; }

    /// <summary>Range-selection mode only: true when this cell is the range's start date.</summary>
    [Parameter] public bool RangeStart { get; set; }

    /// <summary>Range-selection mode only: true when this cell is the range's end date.</summary>
    [Parameter] public bool RangeEnd { get; set; }

    [Parameter] public CalendarViewItemVariant Variant { get; set; } = CalendarViewItemVariant.Day;

    /// <summary>Small overline label shown above the cell content — the month abbreviation on the
    /// 1st day of a month (Days view) or the year on January (Months view), gated by
    /// FluentCalendarView's <c>Headers</c> parameter.</summary>
    [Parameter] public string? Header { get; set; }

    [Parameter] public int TabIndex { get; set; } = -1;

    [Parameter] public RenderFragment? ChildContent { get; set; }

    [Parameter] public EventCallback OnClick { get; set; }

    [Parameter] public EventCallback<KeyboardEventArgs> OnKeyDown { get; set; }

    /// <summary>The underlying button's DOM reference, exposed so FluentCalendarView can roving-tabindex
    /// focus a neighboring cell on arrow-key navigation via <c>ButtonRef.FocusAsync()</c> — capturing a
    /// child component with @ref gives you the component instance, and this field is how that instance
    /// exposes its own internal element reference back up.</summary>
    public ElementReference ButtonRef;
}
