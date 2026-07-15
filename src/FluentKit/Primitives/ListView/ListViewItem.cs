namespace FluentKit.Primitives;

/// <summary>
/// One row of data in a data-bound <see cref="FluentListView{TValue}"/> — same Name/Value/Disabled
/// shape as <see cref="Composite.ComboBoxItem{TValue}"/> and
/// <see cref="Composite.AutoSuggestBoxItem{TValue}"/>, kept as its own type (rather than shared)
/// since ListView lives in Primitives while those two are Composite, and each already treats its
/// own record as the "one row of data" contract for that control.
/// </summary>
/// <typeparam name="TValue">Type of the underlying value the row represents.</typeparam>
public sealed record ListViewItem<TValue>(string Name, TValue Value, bool Disabled = false);
