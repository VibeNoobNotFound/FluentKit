using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Fluent.Blazor.Primitives;

/// <summary>
/// Mirrors WinUI's RadioButton. Works two ways:
/// 1. Standalone: bind <see cref="Checked"/> directly (single independent radio, rare in practice).
/// 2. Grouped (the common case): place inside a &lt;FluentRadioGroup&gt; and set <see cref="Value"/> —
///    selection state then comes from the cascaded group context, matching fluent-svelte's
///    bind:group pattern without relying on Blazor's InputRadioGroup (which requires EditContext).
/// </summary>
public partial class FluentRadioButton : ComponentBase
{
    [CascadingParameter]
    private RadioGroupContext? Group { get; set; }

    [Parameter]
    public object? Value { get; set; }

    [Parameter]
    public bool Checked { get; set; }

    [Parameter]
    public EventCallback<bool> CheckedChanged { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private bool IsSelected => Group is not null
        ? Equals(Group.SelectedValue, Value)
        : Checked;

    private async Task SelectAsync()
    {
        if (Disabled)
        {
            return;
        }

        if (Group is not null)
        {
            await Group.SetValueAsync(Value);
        }
        else
        {
            Checked = true;
            await CheckedChanged.InvokeAsync(true);
        }
    }

    private async Task OnKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key is " " or "Enter")
        {
            await SelectAsync();
        }
    }
}
