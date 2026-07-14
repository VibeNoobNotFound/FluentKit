using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using FluentKit.Overlay;

namespace FluentKit.Composite;

/// <summary>
/// Generic positioned content flyout (WinUI 3 Flyout). Click/tap the trigger to open, click outside
/// or press Escape to close. Built entirely on IOverlayService/FluentOverlayHost (Phase 3 infra) —
/// this is the second consumer after FluentTooltip and the base every other overlay composite
/// (MenuFlyout, ComboBox, TeachingTip, DropDownButton...) is expected to follow the same shape of.
/// </summary>
public partial class FluentFlyout : ComponentBase, IDisposable
{
    [Inject] private IOverlayService OverlayService { get; set; } = default!;

    /// <summary>The element that opens the flyout when clicked.</summary>
    [Parameter, EditorRequired] public RenderFragment ChildContent { get; set; } = default!;

    /// <summary>The content shown inside the flyout surface once open.</summary>
    [Parameter, EditorRequired] public RenderFragment FlyoutContent { get; set; } = default!;

    /// <summary>Preferred side of the trigger the flyout opens on; flips automatically if there's no room.</summary>
    [Parameter] public OverlayPlacement Placement { get; set; } = OverlayPlacement.Bottom;

    /// <summary>Closes when clicking outside the flyout. Set false for flyouts that should only be
    /// dismissed programmatically or via an explicit action inside the content.</summary>
    [Parameter] public bool LightDismiss { get; set; } = true;

    /// <summary>Two-way bindable open state, so callers can drive the flyout externally (e.g. open it
    /// from code, or close it from a "Save"/"Cancel" button inside FlyoutContent).</summary>
    [Parameter] public bool IsOpen { get; set; }

    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }

    private ElementReference _anchor;
    private Guid? _overlayId;
    private bool _lastRenderedIsOpen;

    protected override void OnInitialized()
    {
        OverlayService.Changed += OnOverlayServiceChanged;
    }

    private void OnOverlayServiceChanged()
    {
        if (_overlayId is { } id)
        {
            var entry = OverlayService.Active.FirstOrDefault(e => e.Id == id);
            if (entry is null || entry.IsClosing)
            {
                _overlayId = null;
                _ = SetOpenAsync(false);
            }
        }
    }

    protected override void OnParametersSet()
    {
        // Support external/programmatic control: if a caller flips IsOpen between renders (rather
        // than through ToggleAsync), reflect it in the overlay without requiring them to know about
        // IOverlayService at all.
        if (IsOpen != _lastRenderedIsOpen)
        {
            _lastRenderedIsOpen = IsOpen;
            if (IsOpen)
            {
                ShowInternal();
            }
            else
            {
                HideInternal();
            }
        }
    }

    private Task ToggleAsync() => _overlayId is null ? OpenAsync() : CloseAsync();

    private async Task OpenAsync()
    {
        ShowInternal();
        await SetOpenAsync(true);
    }

    private async Task CloseAsync()
    {
        HideInternal();
        await SetOpenAsync(false);
    }

    private void ShowInternal()
    {
        if (_overlayId is not null)
        {
            return;
        }

        _overlayId = OverlayService.Show(RenderFlyoutContent, _anchor, Placement, LightDismiss);
    }

    private void HideInternal()
    {
        if (_overlayId is { } id)
        {
            OverlayService.Close(id);
            _overlayId = null;
        }
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

    public void Dispose()
    {
        OverlayService.Changed -= OnOverlayServiceChanged;
        HideInternal();
    }
}
