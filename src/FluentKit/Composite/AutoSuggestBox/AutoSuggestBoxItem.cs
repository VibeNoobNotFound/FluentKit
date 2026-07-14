namespace FluentKit.Composite;

/// <summary>
/// One suggestion row in a <see cref="FluentAutoSuggestBox{TValue}"/>. Same shape as
/// <see cref="ComboBoxItem{TValue}"/> (Name/Value/Disabled) — kept as its own type rather than
/// reusing ComboBoxItem directly since AutoSuggestBox's "Value" is conceptually a query result
/// payload rather than a fixed option, even though the record looks identical today.
/// </summary>
/// <typeparam name="TValue">Type of the underlying value the suggestion represents.</typeparam>
public sealed record AutoSuggestBoxItem<TValue>(string Name, TValue Value, bool Disabled = false);
