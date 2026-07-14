using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Fluent.Blazor.Composite;

/// <summary>
/// Mirrors WinUI's Pivot/TabView (a horizontal strip of headers, one visible content pane at a
/// time, arrow-key roving navigation between headers). Named FluentPivot rather than FluentTabView
/// since that's the more fundamental WinUI 3 control the two largely share behavior with — TabView
/// additionally supports closeable/add-new tabs, which can be layered on top of this later without
/// changing the core header-strip/content-pane shape.
/// </summary>
public partial class FluentPivot : ComponentBase
{
    [Parameter] public int SelectedIndex { get; set; }

    [Parameter] public EventCallback<int> SelectedIndexChanged { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private readonly PivotContext _context;

    public FluentPivot()
    {
        _context = new PivotContext(() => InvokeAsync(StateHasChanged));
    }

    private async Task SelectAsync(int index)
    {
        if (index == SelectedIndex || index < 0 || index >= _context.Items.Count || _context.Items[index].Disabled)
        {
            return;
        }

        SelectedIndex = index;
        await SelectedIndexChanged.InvokeAsync(SelectedIndex);
    }

    private async Task OnKeyDown(KeyboardEventArgs e, int index)
    {
        var count = _context.Items.Count;
        if (count == 0)
        {
            return;
        }

        int? target = e.Key switch
        {
            "ArrowRight" => (index + 1) % count,
            "ArrowLeft" => (index - 1 + count) % count,
            "Home" => 0,
            "End" => count - 1,
            _ => null
        };

        if (target is int t)
        {
            await SelectAsync(t);
        }
    }
}
