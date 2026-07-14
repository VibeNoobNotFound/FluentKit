using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Fluent.Blazor.Primitives;

/// <summary>
/// Mirrors WinUI's ToggleButton — a button that stays visually "pressed" once activated, distinct
/// from <see cref="FluentToggleSwitch"/> (which always pairs its state with On/Off text per Fluent's
/// accessibility guidance) and from plain <see cref="FluentButton"/> (momentary, no persisted state).
/// Typical uses: formatting toolbars (Bold/Italic), a single standalone "Mute" button, etc.
/// </summary>
public partial class FluentToggleButton : ComponentBase
{
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

    private async Task ToggleAsync(MouseEventArgs e)
    {
        if (Disabled)
        {
            return;
        }

        Checked = !Checked;
        await CheckedChanged.InvokeAsync(Checked);
    }
}
