using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Fluent.Blazor.Primitives;

/// <summary>
/// Mirrors WinUI's CheckBox, including the three-state (indeterminate) mode used for
/// "select all" style parent checkboxes. Built as a native ARIA checkbox rather than
/// &lt;input type="checkbox"&gt; so the indeterminate visual state is fully CSS-driven
/// (native inputs only expose indeterminate via JS interop on the DOM property, not an attribute).
/// </summary>
public partial class FluentCheckBox : ComponentBase
{
    [Parameter]
    public bool Checked { get; set; }

    [Parameter]
    public EventCallback<bool> CheckedChanged { get; set; }

    /// <summary>
    /// Third state, matching WinUI's IsThreeState CheckBox. When true, the glyph renders as a dash
    /// regardless of <see cref="Checked"/>, and clicking cycles unchecked -> checked -> indeterminate.
    /// </summary>
    [Parameter]
    public bool Indeterminate { get; set; }

    [Parameter]
    public EventCallback<bool> IndeterminateChanged { get; set; }

    [Parameter]
    public bool ThreeState { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private string AriaChecked => Indeterminate ? "mixed" : Checked ? "true" : "false";

    private async Task ToggleAsync()
    {
        if (Disabled)
        {
            return;
        }

        if (ThreeState)
        {
            // unchecked -> checked -> indeterminate -> unchecked
            if (Indeterminate)
            {
                Indeterminate = false;
                Checked = false;
            }
            else if (Checked)
            {
                Indeterminate = true;
            }
            else
            {
                Checked = true;
            }

            await IndeterminateChanged.InvokeAsync(Indeterminate);
        }
        else
        {
            Checked = !Checked;
        }

        await CheckedChanged.InvokeAsync(Checked);
    }

    private async Task OnKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key is " " or "Enter")
        {
            await ToggleAsync();
        }
    }
}
