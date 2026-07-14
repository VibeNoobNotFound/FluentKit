using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using FluentKit.Overlay;

namespace FluentKit.Composite;

/// <summary>
/// Right-click context menu attached to arbitrary wrapped content (ContextMenu.svelte). Reuses
/// FluentMenuFlyout's whole shape (IOverlayService + MenuFlyoutCloseContext + FluentMenuFlyoutSurface)
/// rather than re-solving positioning: instead of porting fluent-svelte's own bespoke
/// mousePosition/menuPosition clamp math, this points OverlayService.Show at a 0×0 fixed-position
/// "cursor anchor" div moved to the click coordinates — the existing overlay-interop.js
/// computePosition() (bottom-of-anchor, flips to top / clamps horizontally when there's no room)
/// then does the exact same collision-avoidance job for free, with no new JS.
/// </summary>
public partial class FluentContextMenu : ComponentBase, IDisposable
{
    [Inject] private IOverlayService OverlayService { get; set; } = default!;

    /// <summary>The content that reveals the context menu on right-click.</summary>
    [Parameter, EditorRequired] public RenderFragment ChildContent { get; set; } = default!;

    /// <summary>The menu's items — FluentMenuFlyoutItem/FluentMenuFlyoutDivider elements.</summary>
    [Parameter, EditorRequired] public RenderFragment MenuItems { get; set; } = default!;

    [Parameter] public bool CloseOnSelect { get; set; } = true;

    [Parameter] public bool IsOpen { get; set; }

    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private ElementReference _cursorAnchor;
    private string _anchorStyle = "position:fixed; inset-block-start:0; inset-inline-start:0; inline-size:0; block-size:0;";
    private Guid? _overlayId;
    private MenuFlyoutCloseContext? _context;

    private async Task OnContextMenu(MouseEventArgs e)
    {
        // Re-anchor at the new cursor position first so the DOM reflects it before OverlaySurface
        // measures the anchor's getBoundingClientRect() on its own next render.
        _anchorStyle =
            $"position:fixed; inset-block-start:{e.ClientY.ToString(System.Globalization.CultureInfo.InvariantCulture)}px; " +
            $"inset-inline-start:{e.ClientX.ToString(System.Globalization.CultureInfo.InvariantCulture)}px; inline-size:0; block-size:0;";

        // A second right-click while already open just repositions it (matches fluent-svelte:
        // it recomputes menuPosition from the new mousePosition rather than toggling closed).
        HideInternal();
        ShowInternal();
        await Task.CompletedTask;
    }

    private void ShowInternal()
    {
        if (_overlayId is not null)
        {
            return;
        }

        _context = new MenuFlyoutCloseContext { Closable = true, CloseOnSelect = CloseOnSelect };
        _context.RequestCloseAll += () => _ = CloseAsync();

        _overlayId = OverlayService.Show(RenderMenuContent, _cursorAnchor, OverlayPlacement.Bottom, lightDismiss: true);
        _lastRenderedIsOpen = true;
        _ = SetOpenAsync(true);
    }

    private void HideInternal()
    {
        if (_overlayId is { } id)
        {
            OverlayService.Close(id);
            _overlayId = null;
            _context = null;
        }
    }

    private bool _lastRenderedIsOpen;

    protected override void OnParametersSet()
    {
        // Support programmatic close (e.g. a "Cancel" button inside MenuItems binds IsOpen).
        if (!IsOpen && _lastRenderedIsOpen)
        {
            _lastRenderedIsOpen = false;
            HideInternal();
        }
    }

    private async Task CloseAsync()
    {
        HideInternal();
        await SetOpenAsync(false);
    }

    private async Task SetOpenAsync(bool value)
    {
        _lastRenderedIsOpen = value;
        if (IsOpen != value)
        {
            IsOpen = value;
            await IsOpenChanged.InvokeAsync(value);
        }
    }

    private Task HandleContentKeyDown(KeyboardEventArgs args)
        => args.Key == "Escape" ? CloseAsync() : Task.CompletedTask;

    public void Dispose() => HideInternal();
}
