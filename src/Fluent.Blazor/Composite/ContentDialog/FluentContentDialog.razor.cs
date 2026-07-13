using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Fluent.Blazor.Composite;

/// <summary>
/// Mirrors fluent-svelte's ContentDialog.svelte (WinUI's ContentDialog) — a modal dialog centered
/// over a full-viewport "smoke" scrim, with an optional Title, a scrollable body (ChildContent),
/// and an optional Footer (typically 1-2 FluentButton instances laid out in an equal-width row,
/// same as ContentDialog.scss's `.content-dialog-footer { grid-auto-flow: column }`).
///
/// Deliberately NOT built on IOverlayService/FluentOverlayHost like FluentFlyout/FluentMenuFlyout/
/// FluentTooltip — those all position relative to a trigger ElementReference, whereas a modal
/// dialog is always viewport-centered regardless of what invoked it, and needs its own full-screen
/// scrim underneath (which the anchor-relative overlay model has no concept of). Self-contained,
/// same reasoning as FluentComboBox's dropdown.
/// </summary>
public partial class FluentContentDialog : ComponentBase
{
    /// <summary>Two-way bindable open state.</summary>
    [Parameter] public bool IsOpen { get; set; }

    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }

    /// <summary>Title text shown as the dialog header (TextBlock Subtitle variant). Omit for a
    /// dialog with no header — just the body content.</summary>
    [Parameter] public string? Title { get; set; }

    [Parameter] public ContentDialogSize Size { get; set; } = ContentDialogSize.Standard;

    /// <summary>Whether the dialog can be dismissed via the Escape key or by clicking the scrim.
    /// Set false to force the user through an explicit footer action (e.g. a destructive confirm
    /// dialog with no implicit "cancel by clicking away").</summary>
    [Parameter] public bool Closable { get; set; } = true;

    /// <summary>Whether the scrim behind the dialog darkens the rest of the page
    /// (SmokeFillColorDefault). Svelte's `darken` prop — false gives a fully transparent backdrop,
    /// still blocking interaction, just not visually dimmed.</summary>
    [Parameter] public bool Darken { get; set; } = true;

    [Parameter, EditorRequired] public RenderFragment ChildContent { get; set; } = default!;

    /// <summary>Optional footer row, typically 1-2 FluentButton instances. Omit entirely for a
    /// dialog with no footer (matches svelte's `$$slots.footer` conditional render).</summary>
    [Parameter] public RenderFragment? Footer { get; set; }

    [Parameter] public EventCallback OnBackdropClick { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private string SizeClass => Size switch
    {
        ContentDialogSize.Min => "fluent-content-dialog--min",
        ContentDialogSize.Max => "fluent-content-dialog--max",
        _ => "fluent-content-dialog--standard"
    };

    private async Task CloseAsync()
    {
        if (IsOpen)
        {
            IsOpen = false;
            await IsOpenChanged.InvokeAsync(false);
        }
    }

    private Task OnSmokeKeyDownAsync(KeyboardEventArgs e)
        => e.Key == "Escape" && Closable ? CloseAsync() : Task.CompletedTask;

    private async Task OnBackdropClickAsync()
    {
        await OnBackdropClick.InvokeAsync();
        if (Closable)
        {
            await CloseAsync();
        }
    }
}
