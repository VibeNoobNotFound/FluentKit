using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace FluentKit.Composite;

/// <summary>
/// Mirrors WinUI's Pivot/TabView (a horizontal strip of headers, one visible content pane at a
/// time, arrow-key roving navigation between headers). Named FluentPivot rather than FluentTabView
/// since that's the more fundamental WinUI 3 control the two largely share behavior with — TabView
/// additionally supports closeable/add-new tabs, which can be layered on top of this later without
/// changing the core header-strip/content-pane shape.
///
/// The selection indicator (the blue underline) is a single absolutely-positioned div, not a
/// border on each tab — sliding it between tabs on selection change needs its actual pixel
/// position, which CSS alone can't derive from variable-width tab content, so
/// wwwroot/Composite/Pivot/pivot-interop.js measures the selected button's offset/width and this
/// re-renders with that baked into an inline transform/width (CSS transition handles the actual
/// slide animation once those numbers land).
/// </summary>
public partial class FluentPivot : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public int SelectedIndex { get; set; }

    [Parameter] public EventCallback<int> SelectedIndexChanged { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private readonly PivotContext _context;
    private ElementReference _headerElement;
    private IJSObjectReference? _module;

    // Guards against re-measuring (and therefore re-rendering) every single render pass — only the
    // selected tab or the item count actually changing warrants a new measurement. Without this,
    // OnAfterRenderAsync's own StateHasChanged call after a measurement would trigger another
    // render, which would measure again, which would StateHasChanged again... (the exact shape of
    // the FluentPivotItem.OnParametersSet bug from earlier — see that file's git history.)
    private int _measuredIndex = -1;
    private int _measuredCount = -1;

    private double _indicatorLeft;
    private double _indicatorWidth;
    private bool _hasIndicator;

    public FluentPivot()
    {
        _context = new PivotContext(() => InvokeAsync(StateHasChanged));
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _module = await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/FluentKit/Composite/Pivot/pivot-interop.js");
        }

        if (_module is null || _context.Items.Count == 0)
        {
            return;
        }

        if (_measuredIndex == SelectedIndex && _measuredCount == _context.Items.Count)
        {
            return;
        }

        _measuredIndex = SelectedIndex;
        _measuredCount = _context.Items.Count;

        if (SelectedIndex < 0 || SelectedIndex >= _context.Items.Count)
        {
            return;
        }

        var rect = await _module.InvokeAsync<TabRect?>("measureTab", _headerElement, SelectedIndex);
        if (rect is not null)
        {
            _indicatorLeft = rect.Left;
            _indicatorWidth = rect.Width;
            _hasIndicator = true;
            StateHasChanged();
        }
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

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }

    private sealed class TabRect
    {
        public double Left { get; set; }
        public double Width { get; set; }
    }
}
