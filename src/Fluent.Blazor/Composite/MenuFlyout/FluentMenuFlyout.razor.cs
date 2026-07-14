using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Fluent.Blazor.Overlay;

namespace Fluent.Blazor.Composite;

/// <summary>
/// Command menu opened from a trigger element (MenuFlyoutWrapper.svelte). Built on the same
/// IOverlayService/FluentOverlayHost infra as FluentFlyout — this is effectively FluentFlyout with a
/// MenuFlyoutCloseContext threaded through so its FluentMenuFlyoutItem children (and their own
/// cascading submenus) know how to collapse the whole tree on selection.
///
/// Note: fluent-svelte's MenuFlyoutWrapper also exposes an `alignment` (start/center/end) prop
/// alongside placement. That's not something OverlayPlacement/overlay-interop.js currently model
/// (they only pick which side of the anchor and clamp to the viewport) — left out here rather than
/// half-implemented; add it to OverlayPlacement/computePosition if a consumer needs it.
/// </summary>
public partial class FluentMenuFlyout : ComponentBase, IDisposable
{
    [Inject] private IOverlayService OverlayService { get; set; } = default!;

    /// <summary>The element that opens the menu when clicked.</summary>
    [Parameter, EditorRequired] public RenderFragment ChildContent { get; set; } = default!;

    /// <summary>The menu's items — FluentMenuFlyoutItem/FluentMenuFlyoutDivider elements.</summary>
    [Parameter, EditorRequired] public RenderFragment MenuItems { get; set; } = default!;

    [Parameter] public OverlayPlacement Placement { get; set; } = OverlayPlacement.Bottom;

    /// <summary>When true, the menu is resized to exactly match the trigger's width (e.g.
    /// DropDownButton) instead of sizing to its own content.</summary>
    [Parameter] public bool MatchAnchorWidth { get; set; }

    /// <summary>Whether the menu can be dismissed by conventional interaction at all.</summary>
    [Parameter] public bool Closable { get; set; } = true;

    /// <summary>Whether selecting a standard/radio/toggle item closes the menu. Only applies if
    /// <see cref="Closable"/> is true.</summary>
    [Parameter] public bool CloseOnSelect { get; set; } = true;

    [Parameter] public bool IsOpen { get; set; }

    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }

    private ElementReference _anchor;
    private Guid? _overlayId;
    private bool _lastRenderedIsOpen;
    private MenuFlyoutCloseContext? _context;

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
                _context = null;
                _ = SetOpenAsync(false);
            }
        }
    }

    protected override void OnParametersSet()
    {
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

        _context = new MenuFlyoutCloseContext { Closable = Closable, CloseOnSelect = CloseOnSelect };
        _context.RequestCloseAll += () => _ = CloseAsync();

        _overlayId = OverlayService.Show(RenderMenuContent, _anchor, Placement, Closable, matchAnchorWidth: MatchAnchorWidth);
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
        => args.Key == "Escape" && Closable ? CloseAsync() : Task.CompletedTask;

    public void Dispose()
    {
        OverlayService.Changed -= OnOverlayServiceChanged;
        HideInternal();
    }
}
