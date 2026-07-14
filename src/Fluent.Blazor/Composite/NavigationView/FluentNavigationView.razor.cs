using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Fluent.Blazor.Composite;

public enum NavigationViewPaneDisplayMode
{
    Auto,
    Left,
    Top,
    LeftCompact,
    LeftMinimal
}

public enum NavigationViewDisplayMode
{
    Minimal,
    Compact,
    Expanded
}

public partial class FluentNavigationView : ComponentBase, IDisposable
{
    /// <summary>Sentinel <see cref="FluentNavigationViewItem.Value"/> the built-in Settings row uses,
    /// so it participates in normal single-selection like any other item (highlighted via
    /// <see cref="SelectedValue"/>, arrow-key focus, etc.) instead of being a one-off button bolted
    /// onto the footer. A private nested sentinel type (rather than e.g. a plain string like
    /// "__settings__") so it can never collide with a real caller-supplied item Value by accident,
    /// while still being a stable, comparable reference across renders (it's a static readonly
    /// singleton, not re-allocated per render). Overrides ToString() purely so an app that naively
    /// displays SelectedValue (see the sample page) shows something readable rather than the type
    /// name.</summary>
    public static readonly object SettingsItemValue = new SettingsSentinel();

    private sealed class SettingsSentinel
    {
        public override string ToString() => "Settings";
    }

    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter]
    public NavigationViewPaneDisplayMode PaneDisplayMode { get; set; } = NavigationViewPaneDisplayMode.Auto;

    [Parameter]
    public bool IsPaneOpen { get; set; } = true;

    [Parameter]
    public EventCallback<bool> IsPaneOpenChanged { get; set; }

    [Parameter]
    public object? SelectedValue { get; set; }

    [Parameter]
    public EventCallback<object?> SelectedValueChanged { get; set; }

    [Parameter]
    public EventCallback<object?> ItemInvoked { get; set; }

    [Parameter]
    public bool ShowBackButton { get; set; }

    [Parameter]
    public EventCallback BackRequested { get; set; }

    [Parameter]
    public RenderFragment? PaneHeader { get; set; }

    [Parameter]
    public RenderFragment? MenuItems { get; set; }

    [Parameter]
    public RenderFragment? PaneFooter { get; set; }

    [Parameter]
    public bool ShowSettingsButton { get; set; }

    [Parameter]
    public EventCallback SettingsRequested { get; set; }

    [Parameter]
    public RenderFragment? HeaderContent { get; set; }

    [Parameter]
    public bool AlwaysShowHeader { get; set; } = true;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private ElementReference _rootElement;
    private NavigationViewContext? _context;
    private IJSObjectReference? _module;

    public NavigationViewDisplayMode DisplayMode { get; private set; } = NavigationViewDisplayMode.Expanded;

    private bool IsCompactOrMinimal =>
        PaneDisplayMode is NavigationViewPaneDisplayMode.LeftCompact or NavigationViewPaneDisplayMode.LeftMinimal;

    private string PaneDisplayModeClass => PaneDisplayMode switch
    {
        NavigationViewPaneDisplayMode.LeftCompact => "compact",
        NavigationViewPaneDisplayMode.LeftMinimal => "minimal",
        NavigationViewPaneDisplayMode.Top => "top",
        NavigationViewPaneDisplayMode.Auto => "auto",
        _ => "expanded"
    };

    protected override void OnInitialized()
    {
        _context = new NavigationViewContext(this);
        _context.SelectionChanged += OnContextSelectionChanged;
        _context.ItemClicked += (object? sender1) => OnItemClicked(sender1);
        base.OnInitialized();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                _module = await JS.InvokeAsync<IJSObjectReference>(
                    "import", "./_content/Fluent.Blazor/Composite/NavigationView/navview-interop.js");
            }
            catch (JSException)
            {
                // GetWidthAsync() already tolerates _module being null (falls back to a fixed 800px
                // assumed width for the Auto-mode breakpoint calc below) — better to keep
                // NavigationView usable at a wrong breakpoint than crash the whole component tree if
                // the interop module ever fails to load (e.g. static assets misconfigured).
                _module = null;
            }

            await UpdateDisplayModeAsync();
        }
        await base.OnAfterRenderAsync(firstRender);
    }

    protected override void OnParametersSet()
    {
        if (_context != null && _context.SelectedValue != SelectedValue)
        {
            _context.SelectedValue = SelectedValue;
        }
        base.OnParametersSet();
    }

    private void OnContextSelectionChanged()
    {
        var newValue = _context?.SelectedValue;
        if (!Equals(SelectedValue, newValue))
        {
            SelectedValue = newValue;
            _ = SelectedValueChanged.InvokeAsync(SelectedValue);
            StateHasChanged();
        }
    }

    internal void NotifyContextSelectionChanged(object? newValue)
    {
        OnContextSelectionChanged();
    }

    private async Task OnItemClicked(object? value)
    {
        // Settings behaves like any other selectable item (see SettingsItemValue's own doc comment)
        // right up until the "what happens on click" step, where it fires SettingsRequested instead
        // of ItemInvoked — same split WinUI itself makes between its own SettingsInvoked and
        // ItemInvoked events, so a consumer can route "open settings" differently from ordinary
        // navigation without needing to special-case the Value on their own end.
        if (Equals(value, SettingsItemValue))
        {
            await SettingsRequested.InvokeAsync();
        }
        else
        {
            await ItemInvoked.InvokeAsync(value);
        }

        // Close overlay if in compact/minimal and pane is open
        if (IsCompactOrMinimal && IsPaneOpen)
        {
            await TogglePaneAsync();
        }
    }

    private async Task TogglePaneAsync()
    {
        IsPaneOpen = !IsPaneOpen;
        await IsPaneOpenChanged.InvokeAsync(IsPaneOpen);
        StateHasChanged();
    }

    private async Task UpdateDisplayModeAsync()
    {
        var width = await GetWidthAsync();
        var paneDisplayMode = PaneDisplayMode;

        NavigationViewDisplayMode displayMode;
        if (paneDisplayMode == NavigationViewPaneDisplayMode.Top)
        {
            displayMode = NavigationViewDisplayMode.Minimal;
        }
        else if (paneDisplayMode == NavigationViewPaneDisplayMode.LeftCompact)
        {
            displayMode = NavigationViewDisplayMode.Compact;
        }
        else if (paneDisplayMode == NavigationViewPaneDisplayMode.LeftMinimal)
        {
            displayMode = NavigationViewDisplayMode.Minimal;
        }
        else if (paneDisplayMode == NavigationViewPaneDisplayMode.Left)
        {
            displayMode = NavigationViewDisplayMode.Expanded;
        }
        else // Auto
        {
            var expandedThreshold = 1008.0;
            var compactThreshold = 641.0;

            if (width >= expandedThreshold)
                displayMode = NavigationViewDisplayMode.Expanded;
            else if (width >= compactThreshold)
                displayMode = NavigationViewDisplayMode.Compact;
            else
                displayMode = NavigationViewDisplayMode.Minimal;
        }

        if (DisplayMode != displayMode)
        {
            DisplayMode = displayMode;
            StateHasChanged();
        }

        // Auto close pane when switching to compact/minimal
        if (IsCompactOrMinimal && IsPaneOpen)
        {
            await TogglePaneAsync();
        }
    }

    private async Task<double> GetWidthAsync()
    {
        if (!string.IsNullOrEmpty(_rootElement.Id) && _module != null)
        {
            try
            {
                return await _module.InvokeAsync<double>("getElementWidth", _rootElement);
            }
            catch
            {
                return 800;
            }
        }
        return 800;
    }

    private async Task OnBackClick() => await BackRequested.InvokeAsync();

    public void Dispose()
    {
        _context?.Dispose();
        if (_module is not null)
        {
            try { _module.DisposeAsync().AsTask().Wait(); } catch { }
        }
    }
}