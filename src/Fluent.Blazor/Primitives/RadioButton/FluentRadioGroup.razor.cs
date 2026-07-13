using Microsoft.AspNetCore.Components;

namespace Fluent.Blazor.Primitives;

/// <summary>
/// Groups a set of FluentRadioButton children so only one can be selected at a time — the
/// container-level equivalent of WinUI's RadioButtons list control, minus the automatic layout
/// (wrap child FluentRadioButtons in your own StackPanel-equivalent div for spacing/orientation).
/// </summary>
public partial class FluentRadioGroup : ComponentBase
{
    [Parameter]
    public object? Value { get; set; }

    [Parameter]
    public EventCallback<object?> ValueChanged { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private RadioGroupContext? _context;

    protected override void OnParametersSet()
    {
        _context = new RadioGroupContext(Value, async newValue =>
        {
            Value = newValue;
            await ValueChanged.InvokeAsync(newValue);
            StateHasChanged();
        });
    }
}
