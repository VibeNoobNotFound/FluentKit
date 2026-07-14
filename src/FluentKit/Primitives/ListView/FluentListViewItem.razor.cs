using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FluentKit.Primitives;

/// <summary>
/// Single row inside a FluentListView. Selection is intentionally NOT owned here (see
/// FluentListView's doc comment) — Selected is a plain bool the caller drives, OnSelect fires on
/// click/Enter/Space (native &lt;button&gt; gives Space/Enter for free, same reasoning as
/// FluentNavigationViewItem). Root element registers with the cascaded FluentListView purely for
/// arrow-key roving focus (via IFocusableListItem); it works fine with no FluentListView ancestor
/// too — List is optional.
/// </summary>
public partial class FluentListViewItem : ComponentBase, IFocusableListItem, IDisposable
{
    [CascadingParameter]
    private FluentListView? List { get; set; }

    [Parameter]
    public bool Selected { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public EventCallback OnSelect { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private ElementReference _element;

    bool IFocusableListItem.Disabled => Disabled;

    protected override void OnInitialized() => List?.Register(this);

    public void Dispose() => List?.Unregister(this);

    public ValueTask FocusAsync() => _element.FocusAsync();

    private async Task HandleClickAsync()
    {
        if (Disabled)
        {
            return;
        }

        await OnSelect.InvokeAsync();
    }

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
}
