using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FluentKit.Primitives;

/// <summary>
/// Mirrors WinUI's TextBox. The focus/underline behavior (an accent-colored bottom edge that only
/// appears once focused) is pure CSS via :focus-within on the container in
/// FluentTextBox.razor.css — see TextBox.scss in fluent-svelte for the reference implementation
/// this was ported from (that one uses an ::after pseudo-element the same way).
/// </summary>
public partial class FluentTextBox : ComponentBase
{
    [Parameter]
    public string? Value { get; set; }

    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    [Parameter]
    public string? Header { get; set; }

    [Parameter]
    public string? Placeholder { get; set; }

    [Parameter]
    public string Type { get; set; } = "text";

    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// Optional right-aligned button slot (mirrors fluent-svelte's TextBox <c>slot="buttons"</c>).
    /// Used e.g. by NumberBox for its spin up/down buttons, or by consumers for a clear/search icon.
    /// </summary>
    [Parameter]
    public RenderFragment? Buttons { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private async Task OnInputAsync(ChangeEventArgs e)
    {
        Value = e.Value?.ToString();
        await ValueChanged.InvokeAsync(Value);
    }
}
