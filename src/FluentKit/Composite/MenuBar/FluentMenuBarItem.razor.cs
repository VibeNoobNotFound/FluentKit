using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using FluentKit.Overlay;

namespace FluentKit.Composite;

/// <summary>
/// A single top-level entry in a FluentMenuBar (MenuBarItem.svelte) — e.g. "File"/"Edit"/"View" in an
/// app menu bar. Opens its <see cref="Flyout"/> (typically FluentMenuFlyoutItem/FluentMenuFlyoutDivider
/// children, reusing FluentMenuFlyoutSurface + MenuFlyoutCloseContext exactly like FluentMenuFlyoutItem's
/// own cascading submenu does) below itself on click, Enter, or Space.
///
/// Once one bar item's menu is open, hovering a sibling switches straight to its menu instead of
/// requiring another click (svelte's <c>handleMouseEnter</c>/<c>$currentMenu</c> check), and
/// ArrowLeft/ArrowRight walk between items with wraparound (svelte's <c>sideNavigation</c> context).
/// </summary>
public partial class FluentMenuBarItem : ComponentBase, IDisposable
{
    [Inject] private IOverlayService OverlayService { get; set; } = default!;

    [CascadingParameter] private MenuBarContext? Bar { get; set; }

    /// <summary>The item's label.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>The dropdown's contents — typically FluentMenuFlyoutItem/FluentMenuFlyoutDivider
    /// elements. Omit for a plain non-interactive bar entry.</summary>
    [Parameter] public RenderFragment? Flyout { get; set; }

    [Parameter] public bool Disabled { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private ElementReference _element;
    private Guid? _overlayId;
    private MenuFlyoutCloseContext? _closeContext;

    private bool Open => _overlayId is not null;

    protected override void OnInitialized() => Bar?.Register(this);

    public async Task FocusAsync() => await _element.FocusAsync();

    private Task ToggleAsync() => Disabled || Flyout is null
        ? Task.CompletedTask
        : Open ? CloseAsync() : OpenAsync();

    private Task OpenAsync()
    {
        if (Open || Flyout is null)
        {
            return Task.CompletedTask;
        }

        _closeContext = new MenuFlyoutCloseContext();
        _closeContext.RequestCloseAll += () => _ = CloseAsync();

        _overlayId = OverlayService.Show(RenderFlyoutContent, _element, OverlayPlacement.Bottom, lightDismiss: true);
        Bar?.SetOpen(this);
        return Task.CompletedTask;
    }

    private Task CloseAsync()
    {
        if (_overlayId is { } id)
        {
            OverlayService.Close(id);
            _overlayId = null;
            _closeContext = null;
        }

        Bar?.ClearOpen(this);
        return InvokeAsync(StateHasChanged);
    }

    /// <summary>Called by MenuBarContext.SetOpen when a sibling item takes over — tears down this
    /// item's own overlay without touching Bar.CurrentOpenItem (the caller already owns that).</summary>
    internal void ForceClose()
    {
        if (_overlayId is { } id)
        {
            OverlayService.Close(id);
            _overlayId = null;
            _closeContext = null;
        }

        _ = InvokeAsync(StateHasChanged);
    }

    private Task OnMouseEnter() =>
        !Disabled && Flyout is not null && Bar?.CurrentOpenItem is not null && Bar.CurrentOpenItem != this
            ? OpenAsync()
            : Task.CompletedTask;

    private Task OnFocus() =>
        !Open && Bar?.CurrentOpenItem is not null ? OpenAsync() : Task.CompletedTask;

    private Task OnKeyDown(KeyboardEventArgs e) => e.Key switch
    {
        "Enter" or " " => ToggleAsync(),
        "ArrowLeft" => Bar?.FocusAdjacentAsync(this, -1) ?? Task.CompletedTask,
        "ArrowRight" => Bar?.FocusAdjacentAsync(this, 1) ?? Task.CompletedTask,
        "Escape" when Open => CloseAsync(),
        _ => Task.CompletedTask
    };

    private Task HandleFlyoutKeyDown(KeyboardEventArgs e) =>
        e.Key == "Escape" ? CloseAsync() : Task.CompletedTask;

    public void Dispose()
    {
        Bar?.Unregister(this);
        if (_overlayId is { } id)
        {
            OverlayService.Close(id);
        }
    }
}
