using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Fluent.Blazor.Primitives;

namespace Fluent.Blazor.Composite;

/// <summary>
/// Mirrors WinUI's NavigationViewItem. Selection comes from the cascaded NavigationViewContext
/// (set by the parent FluentNavigationView) — same pattern as FluentRadioButton/FluentRadioGroup,
/// no direct parent/child method calls. Root element is a real &lt;button&gt;, so unlike
/// FluentRadioButton this needs no manual @onkeydown wiring for Space/Enter — the browser does it.
/// Also registers with an optional cascaded FluentListView (the pane wraps its items in one — see
/// FluentNavigationView.razor's PaneBody/RailBody) purely for arrow-key roving focus via
/// IFocusableListItem; NavigationViewContext still owns which item is selected, ListView only
/// moves focus between rows. Works with no FluentListView ancestor too, same as FluentListViewItem.
/// TODO (not v1): nested/expandable sub-items — WinUI's MenuItems tree. Flat list only for now.
/// </summary>
public partial class FluentNavigationViewItem : ComponentBase, IFocusableListItem, IDisposable
{
    [CascadingParameter]
    private NavigationViewContext? Nav { get; set; }

    [CascadingParameter]
    private FluentListView? List { get; set; }

    private ElementReference _element;

    bool IFocusableListItem.Disabled => Disabled;

    protected override void OnInitialized() => List?.Register(this);

    public void Dispose() => List?.Unregister(this);

    public ValueTask FocusAsync() => _element.FocusAsync();

    private async Task HandleKeyDownAsync(KeyboardEventArgs e)
    {
        if (List is null)
        {
            return;
        }

        switch (e.Key)
        {
            case "ArrowDown":
                await List.FocusAdjacentAsync(this, 1);
                break;
            case "ArrowUp":
                await List.FocusAdjacentAsync(this, -1);
                break;
            case "Home":
                await List.FocusEndAsync(start: true);
                break;
            case "End":
                await List.FocusEndAsync(start: false);
                break;
        }
    }

    [Parameter, EditorRequired]
    public object? Value { get; set; }

    [Parameter, EditorRequired]
    public string Text { get; set; } = default!;

    [Parameter]
    public RenderFragment? Icon { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private bool IsSelected => Nav is not null && Equals(Nav.SelectedValue, Value);

    private async Task SelectAsync()
    {
        if (Disabled || Nav is null)
        {
            return;
        }

        await Nav.SelectItemAsync(Value);
    }
}
