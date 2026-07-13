namespace Fluent.Blazor.Composite;

/// <summary>
/// One selectable entry in a <see cref="FluentComboBox{TValue}"/>. Mirrors fluent-svelte's
/// ComboBox <c>Item</c> interface (name/value/disabled) — <c>Name</c> is the display text used both
/// for the visible label and (in <see cref="FluentComboBox{TValue}.Editable"/> mode) the
/// starts-with search match against <see cref="FluentComboBox{TValue}.SearchValue"/>.
/// </summary>
/// <typeparam name="TValue">Type of the underlying value the item represents.</typeparam>
public sealed record ComboBoxItem<TValue>(string Name, TValue Value, bool Disabled = false);
