namespace Fluent.Blazor.Composite;

/// <summary>Matches fluent-svelte's CalendarViewItem <c>variant</c> prop — day cells are round/small
/// (40px), month/year cells are round/larger (56px), per CalendarViewItem.scss's two <c>.type-</c> sizes.</summary>
public enum CalendarViewItemVariant
{
    Day,
    MonthYear
}
