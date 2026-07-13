using Microsoft.AspNetCore.Components;
using Fluent.Blazor.Overlay;

namespace Fluent.Blazor.Composite;

/// <summary>
/// Deliberately the simplest possible consumer of IOverlayService/FluentOverlayHost — this exists
/// to prove the overlay infrastructure end to end (Phase 3 exit criteria), not as the final Tooltip.
/// Missing, on purpose, for now: show/hide delay debouncing, and re-showing on scroll.
/// </summary>
public partial class FluentTooltip : ComponentBase, IDisposable
{
    [Inject] private IOverlayService OverlayService { get; set; } = default!;

    [Parameter, EditorRequired] public string Text { get; set; } = default!;
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private ElementReference _anchor;
    private Guid? _overlayId;

    private Task ShowAsync()
    {
        if (_overlayId is not null)
        {
            return Task.CompletedTask;
        }

        _overlayId = OverlayService.Show(TooltipContent, _anchor, OverlayPlacement.Top, lightDismiss: false);
        return Task.CompletedTask;
    }

    private void Hide()
    {
        if (_overlayId is { } id)
        {
            OverlayService.Close(id);
            _overlayId = null;
        }
    }

    public void Dispose() => Hide();
}
