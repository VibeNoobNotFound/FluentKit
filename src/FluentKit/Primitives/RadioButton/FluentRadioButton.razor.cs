using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FluentKit.Primitives;

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

    /// <summary>
    /// Full custom rendering for this option, replacing the default circle-and-dot indicator and
    /// <see cref="ChildContent"/> label entirely. Receives whether this option is currently
    /// selected, so the template can style itself accordingly — e.g. a segmented "chip" row (a
    /// toolbar's Bold/Italic/Underline group, or a category picker) instead of a classic
    /// circle-and-label radio. The template still renders inside the same
    /// role="radio"/tabindex/keydown element as the default indicator, so whatever it renders
    /// stays keyboard-operable and announced correctly to a screen reader — you're only replacing
    /// the visual, not the selection semantics (still exactly one selected, never zero, exactly
    /// like the default indicator).
    /// </summary>
    [Parameter]
    public RenderFragment<bool>? ItemTemplate { get; set; }

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
