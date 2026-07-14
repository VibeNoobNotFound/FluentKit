using Microsoft.AspNetCore.Components;

namespace FluentKit.Primitives;

/// <summary>
/// Cascaded context a FluentRadioGroup exposes to its child FluentRadioButtons, mirroring the
/// "group" binding fluent-svelte's RadioButton exposes via bind:group — Blazor has no native
/// equivalent to Svelte's cross-component two-way binding, so a small explicit context does the job.
/// </summary>
public sealed class RadioGroupContext
{
    private readonly Func<object?, Task> _setValue;

    internal RadioGroupContext(object? selectedValue, Func<object?, Task> setValue)
    {
        SelectedValue = selectedValue;
        _setValue = setValue;
    }

    public object? SelectedValue { get; }

    public Task SetValueAsync(object? value) => _setValue(value);
}
