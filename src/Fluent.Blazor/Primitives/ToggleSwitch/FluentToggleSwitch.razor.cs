using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Fluent.Blazor.Primitives;

/// <summary>
/// Mirrors WinUI's ToggleSwitch, including the optional Header/OnContent/OffContent text WinUI
/// shows alongside the physical switch (unlike a plain CheckBox, ToggleSwitch always communicates
/// its current state via text, not just position, per Fluent's accessibility guidance).
/// </summary>
public partial class FluentToggleSwitch : ComponentBase
{
    [Parameter]
    public bool Checked { get; set; }

    [Parameter]
    public EventCallback<bool> CheckedChanged { get; set; }

    [Parameter]
    public string? Header { get; set; }

    [Parameter]
    public string OnContent { get; set; } = "On";

    [Parameter]
    public string OffContent { get; set; } = "Off";

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private async Task ToggleAsync()
    {
        if (Disabled)
        {
            return;
        }

        Checked = !Checked;
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
