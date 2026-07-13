using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Fluent.Blazor.Overlay;

namespace Fluent.Blazor.Composite;

public enum MenuFlyoutItemVariant
{
    Standard,
    Radio,
    Toggle
}

/// <summary>
/// A single row inside a FluentMenuFlyout/FluentContextMenu (MenuFlyoutItem.svelte). Three input
/// "shapes" share one component, same as the Svelte original: Standard (plain command), Radio/Toggle
/// (adds a bullet/checkmark glyph and a bindable <see cref="Checked"/>), and Cascading (opens a
/// nested submenu instead of closing the tree on click).
///
/// Radio mutual-exclusion is left to the caller (bind each item's own <see cref="Checked"/> and flip
/// siblings off in the click handler) rather than an internal group-value context — a menu's radio
/// items are typically few and this keeps the item itself a plain, independently-bindable control,
/// matching how little state fluent-svelte's own `group` binding actually needs here.
/// </summary>
public partial class FluentMenuFlyoutItem : ComponentBase, IDisposable
{
    [Inject] private IOverlayService OverlayService { get; set; } = default!;

    [CascadingParameter] private MenuFlyoutCloseContext? CloseContext { get; set; }

    [Parameter] public MenuFlyoutItemVariant Variant { get; set; } = MenuFlyoutItemVariant.Standard;

    /// <summary>Marks this item as having a cascading submenu, rendering <see cref="Flyout"/> in a
    /// nested overlay opened to the right (flips left if there's no room) instead of closing the
    /// menu tree when clicked.</summary>
    [Parameter] public bool Cascading { get; set; }

    /// <summary>Content for the cascading submenu. Only used when <see cref="Cascading"/> is true —
    /// typically more &lt;FluentMenuFlyoutItem&gt;/&lt;FluentMenuFlyoutDivider&gt; elements.</summary>
    [Parameter] public RenderFragment? Flyout { get; set; }

    /// <summary>Optional leading icon slot.</summary>
    [Parameter] public RenderFragment? Icon { get; set; }

    /// <summary>The item's label.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Secondary trailing text — e.g. a keyboard accelerator hint ("Ctrl+C").</summary>
    [Parameter] public string? Hint { get; set; }

    /// <summary>Styles the item as the current selection (e.g. the active view in a menu of views),
    /// independent of Radio/Toggle Checked state.</summary>
    [Parameter] public bool Selected { get; set; }

    /// <summary>Bindable checked state for Radio/Toggle variants.</summary>
    [Parameter] public bool Checked { get; set; }

    [Parameter] public EventCallback<bool> CheckedChanged { get; set; }

    /// <summary>Indents the label to line up with sibling items that have icons, even if this one
    /// doesn't render an <see cref="Icon"/>.</summary>
    [Parameter] public bool Indented { get; set; }

    [Parameter] public bool Disabled { get; set; }

    /// <summary>Raised when a non-cascading item is activated (click, Enter, or Space).</summary>
    [Parameter] public EventCallback OnClick { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private ElementReference _itemElement;
    private Guid? _submenuOverlayId;
    private IDisposable? _closeSubscription;
    private CancellationTokenSource? _hoverCts;

    private bool SubmenuOpen => _submenuOverlayId is not null;

    private string VariantClass => Variant switch
    {
        MenuFlyoutItemVariant.Radio => "fluent-menu-flyout-item--radio",
        MenuFlyoutItemVariant.Toggle => "fluent-menu-flyout-item--toggle",
        _ => "fluent-menu-flyout-item--standard"
    };

    private async Task ActivateAsync()
    {
        if (Disabled)
        {
            return;
        }

        if (Cascading)
        {
            await ToggleSubmenuAsync(true);
            return;
        }

        if (Variant is MenuFlyoutItemVariant.Radio or MenuFlyoutItemVariant.Toggle)
        {
            Checked = !Checked;
            await CheckedChanged.InvokeAsync(Checked);
        }

        await OnClick.InvokeAsync();

        // Mirrors fluent-svelte's `close()`: only non-cascading items ever request the tree close.
        if (CloseContext is { Closable: true, CloseOnSelect: true })
        {
            CloseContext.CloseAll();
        }
    }

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (Disabled)
        {
            return;
        }

        switch (e.Key)
        {
            case "Enter":
            case " ":
                await ActivateAsync();
                break;
            case "ArrowRight" when Cascading:
                await ToggleSubmenuAsync(true);
                break;
            case "ArrowLeft" when Cascading && SubmenuOpen:
                await ToggleSubmenuAsync(false);
                break;
        }
    }

    private async Task ToggleSubmenuAsync(bool open)
    {
        if (open == SubmenuOpen)
        {
            return;
        }

        if (open)
        {
            _submenuOverlayId = OverlayService.Show(
                RenderSubmenuContent, _itemElement, OverlayPlacement.Right, lightDismiss: true);

            // Participate in the shared tree's CloseAll() — selecting a leaf deep inside this
            // submenu must collapse this level too, not just its own descendants.
            if (CloseContext is not null)
            {
                _closeSubscription = CloseContext.Subscribe(() => _ = CloseSubmenuInternalAsync());
            }
        }
        else
        {
            await CloseSubmenuInternalAsync();
        }
    }

    private Task CloseSubmenuInternalAsync()
    {
        if (_submenuOverlayId is { } id)
        {
            OverlayService.Close(id);
            _submenuOverlayId = null;
        }

        _closeSubscription?.Dispose();
        _closeSubscription = null;
        return InvokeAsync(StateHasChanged);
    }

    // Hover-to-open, matching fluent-svelte's 500ms intent delay so moving the mouse across a row of
    // sibling items doesn't pop every one of their submenus open along the way.
    private void OnMouseEnter()
    {
        if (!Cascading || Disabled)
        {
            return;
        }

        _hoverCts?.Cancel();
        _hoverCts = new CancellationTokenSource();
        var token = _hoverCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500, token);
                if (!token.IsCancellationRequested)
                {
                    await InvokeAsync(() => ToggleSubmenuAsync(true));
                }
            }
            catch (TaskCanceledException) { }
        });
    }

    private void OnMouseLeave()
    {
        _hoverCts?.Cancel();
    }

    public void Dispose()
    {
        _hoverCts?.Cancel();
        _closeSubscription?.Dispose();
        if (_submenuOverlayId is { } id)
        {
            OverlayService.Close(id);
        }
    }
}
