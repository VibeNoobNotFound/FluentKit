using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FluentKit.Composite;

public enum NavigationViewPaneDisplayMode
{
    Auto,
    Left,
    Top,
    LeftCompact,
    LeftMinimal,
    LeftFullScreen
}

/// <summary>Which edge the full-screen overlay pane slides in from (and which edge the swipe-to-open
/// gesture listens on) when <see cref="NavigationViewPaneDisplayMode.LeftFullScreen"/> is active.</summary>
public enum NavigationViewFullScreenOrigin
{
    Left,
    Top,
    Right
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
    public bool IsBackButtonVisible { get; set; } = true;

    [Parameter]
    public bool IsBackButtonEnabled { get; set; } = true;

    [Parameter]
    public EventCallback BackRequested { get; set; }

    [Parameter]
    public RenderFragment? PaneHeader { get; set; }

    [Parameter]
    public RenderFragment? PaneHeaderIcon { get; set; }

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

    /// <summary>Which edge the overlay pane slides in from, and which edge the swipe-to-open
    /// gesture listens on, while <see cref="PaneDisplayMode"/> is
    /// <see cref="NavigationViewPaneDisplayMode.LeftFullScreen"/> (including when Auto lands on
    /// it at the narrowest width tier). Ignored in Compact/Minimal, which always swipe/slide
    /// from the left rail.</summary>
    [Parameter]
    public NavigationViewFullScreenOrigin FullScreenOrigin { get; set; } = NavigationViewFullScreenOrigin.Left;

    /// <summary>Enables the edge swipe-to-open gesture for the overlay pane in Compact, Minimal,
    /// and FullScreen modes. The pane tracks the finger 1:1 while dragging (dragging halfway
    /// opens it halfway) and snaps open or closed on release based on distance/velocity.</summary>
    [Parameter]
    public bool EnableSwipeToOpen { get; set; } = true;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private ElementReference _rootElement;
    private ElementReference _overlayElement;
    private ElementReference _closedSwipeRegion;
    private ElementReference _openSwipeRegion;
    private NavigationViewContext? _context;
    private IJSObjectReference? _module;
    private DotNetObjectReference<FluentNavigationView>? _selfReference;

    private NavigationViewPaneDisplayMode _activePaneDisplayMode = NavigationViewPaneDisplayMode.Left;
    private NavigationViewPaneDisplayMode _lastPaneDisplayMode = NavigationViewPaneDisplayMode.Left;
    private bool _isTransitioning;
    private System.Threading.CancellationTokenSource? _transitionCts;

    // What the JS-side swipe watcher was last configured for, so OnAfterRenderAsync only pays
    // for a re-arm (stop + start) when something it actually depends on changed, not on every
    // render (e.g. unrelated ChildContent updates while the pane sits idle).
    private bool _swipeWatcherActive;
    private bool _swipeWatcherIsOpen;
    private NavigationViewFullScreenOrigin _swipeWatcherEdge;
    private bool _swipeWatcherCompactOrMinimal;

    public NavigationViewDisplayMode DisplayMode { get; private set; } = NavigationViewDisplayMode.Expanded;

    private bool IsCompactOrMinimal =>
        _activePaneDisplayMode is NavigationViewPaneDisplayMode.LeftCompact
            or NavigationViewPaneDisplayMode.LeftMinimal
            or NavigationViewPaneDisplayMode.LeftFullScreen;

    /// <summary>The edge the swipe-open gesture should watch and the overlay should slide from.
    /// Compact/Minimal are always left-anchored rails; only FullScreen honors FullScreenOrigin.</summary>
    private NavigationViewFullScreenOrigin EffectiveSwipeEdge =>
        _activePaneDisplayMode == NavigationViewPaneDisplayMode.LeftFullScreen
            ? FullScreenOrigin
            : NavigationViewFullScreenOrigin.Left;

    private string PaneDisplayModeClass => _activePaneDisplayMode switch
    {
        NavigationViewPaneDisplayMode.LeftCompact => "compact",
        NavigationViewPaneDisplayMode.LeftMinimal => "minimal",
        NavigationViewPaneDisplayMode.LeftFullScreen => "minimal",
        NavigationViewPaneDisplayMode.Top => "top",
        NavigationViewPaneDisplayMode.Auto => "auto",
        _ => "expanded"
    };

    protected override void OnInitialized()
    {
        _context = new NavigationViewContext(this);
        _context.SelectionChanged += OnContextSelectionChanged;
        _context.ItemClicked += (object? sender1) => OnItemClicked(sender1);
        _context.ExpansionChanged += OnContextExpansionChanged;
        _activePaneDisplayMode = PaneDisplayMode;
        _lastPaneDisplayMode = PaneDisplayMode;
        base.OnInitialized();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _selfReference = DotNetObjectReference.Create(this);
            try
            {
                _module = await JS.InvokeAsync<IJSObjectReference>(
                    "import", "./_content/FluentKit/Composite/NavigationView/navview-interop.js");
                await _module.InvokeVoidAsync("startObservingResize", _rootElement, _selfReference);
            }
            catch (JSException)
            {
                // GetWidthAsync() already tolerates _module being null
                _module = null;
            }

            await UpdateDisplayModeAsync();
        }

        await SyncNavItemAnchorsAsync();
        await SyncSwipeGestureAsync();

        await base.OnAfterRenderAsync(firstRender);
    }

    protected override void OnParametersSet()
    {
        if (_context != null && _context.SelectedValue != SelectedValue)
        {
            _context.SelectedValue = SelectedValue;
        }

        if (PaneDisplayMode != _lastPaneDisplayMode)
        {
            HandleModeChange(PaneDisplayMode, _lastPaneDisplayMode);
            _lastPaneDisplayMode = PaneDisplayMode;
        }

        base.OnParametersSet();
    }

    private void HandleModeChange(NavigationViewPaneDisplayMode newMode, NavigationViewPaneDisplayMode oldMode)
    {
        _transitionCts?.Cancel();
        _transitionCts?.Dispose();
        _transitionCts = new System.Threading.CancellationTokenSource();
        var token = _transitionCts.Token;

        if (oldMode == NavigationViewPaneDisplayMode.Left && newMode == NavigationViewPaneDisplayMode.LeftCompact)
        {
            // Transitioning from Left (Expanded) to LeftCompact (Compact)
            _isTransitioning = true;
            // Keep the active layout as Left, but close the pane (which triggers width transition)
            _activePaneDisplayMode = NavigationViewPaneDisplayMode.Left;
            IsPaneOpen = false;

            _ = CompleteTransitionAfterDelayAsync(NavigationViewPaneDisplayMode.LeftCompact, 250, token);
        }
        else if (oldMode == NavigationViewPaneDisplayMode.LeftCompact && newMode == NavigationViewPaneDisplayMode.Left)
        {
            // Transitioning from LeftCompact (Compact) to Left (Expanded)
            _isTransitioning = true;
            // First set active layout to Left but closed (rail)
            _activePaneDisplayMode = NavigationViewPaneDisplayMode.Left;
            IsPaneOpen = false;

            _ = CompleteTransitionAfterDelayAsync(NavigationViewPaneDisplayMode.Left, 50, token, openPaneAfter: true);
        }
        else
        {
            // Direct switch for other modes
            _activePaneDisplayMode = newMode;
            _isTransitioning = false;
        }
    }

    private async Task CompleteTransitionAfterDelayAsync(NavigationViewPaneDisplayMode targetMode, int delayMs, System.Threading.CancellationToken token, bool openPaneAfter = false)
    {
        try
        {
            await Task.Delay(delayMs, token);
            if (token.IsCancellationRequested) return;

            _activePaneDisplayMode = targetMode;
            if (openPaneAfter)
            {
                IsPaneOpen = true;
                await IsPaneOpenChanged.InvokeAsync(IsPaneOpen);
            }
            _isTransitioning = false;
            StateHasChanged();
        }
        catch (TaskCanceledException)
        {
            // Ignored
        }
    }

    private void OnContextSelectionChanged()
    {
        var newValue = _context?.SelectedValue;

        if (!Equals(SelectedValue, newValue))
        {
            SelectedValue = newValue;
            _ = SelectedValueChanged.InvokeAsync(SelectedValue);
        }

        StateHasChanged();
    }

    private void OnContextExpansionChanged()
    {
        StateHasChanged();
    }

    private async Task SyncNavItemAnchorsAsync()
    {
        if (_module is null) return;
        try
        {
            await _module.InvokeVoidAsync("syncNavItemAnchors", _rootElement);
        }
        catch (JSException) { }
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
        // ItemInvoked events, so an application can route "open settings" differently from ordinary
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

    /// <summary>(Re)configures the JS-side edge-swipe watcher whenever something it depends on has
    /// actually changed since the last render — the current mode class, open/closed state, or the
    /// origin edge. JS owns the live drag (finger-tracked transform on <c>_overlayElement</c>) so
    /// it stays at input latency instead of round-tripping every pointer move through .NET; only
    /// the final open/closed decision comes back via <see cref="OnSwipeCommit"/>.</summary>
    private async Task SyncSwipeGestureAsync()
    {
        if (_module is null) return;

        var shouldBeActive = EnableSwipeToOpen && IsCompactOrMinimal;
        var edge = EffectiveSwipeEdge;

        if (!shouldBeActive)
        {
            if (_swipeWatcherActive)
            {
                try { await _module.InvokeVoidAsync("stopSwipeWatcher", _rootElement); }
                catch (JSException) { }
                _swipeWatcherActive = false;
            }
            return;
        }

        var unchanged = _swipeWatcherActive
            && _swipeWatcherIsOpen == IsPaneOpen
            && _swipeWatcherEdge == edge
            && _swipeWatcherCompactOrMinimal == IsCompactOrMinimal;
        if (unchanged) return;

        try
        {
            await _module.InvokeVoidAsync(
                "startSwipeWatcher",
                _rootElement,
                _overlayElement,
                _closedSwipeRegion,
                _openSwipeRegion,
                _selfReference,
                IsPaneOpen,
                edge.ToString().ToLowerInvariant());
            _swipeWatcherActive = true;
            _swipeWatcherIsOpen = IsPaneOpen;
            _swipeWatcherEdge = edge;
            _swipeWatcherCompactOrMinimal = IsCompactOrMinimal;
        }
        catch (JSException)
        {
            _swipeWatcherActive = false;
        }
    }

    /// <summary>Called by the JS swipe watcher once a drag ends (or a tap outside during an
    /// in-progress drag cancels it) with the final decision already made client-side (past the
    /// distance/velocity threshold or not). Blazor only needs to commit the resulting state —
    /// the drag itself never touched .NET.</summary>
    [JSInvokable]
    public async Task OnSwipeCommit(bool isOpen)
    {
        if (IsPaneOpen == isOpen) return;
        IsPaneOpen = isOpen;
        await IsPaneOpenChanged.InvokeAsync(IsPaneOpen);
        StateHasChanged();
    }

    [JSInvokable]
    public async Task OnResize(double width)
    {
        await UpdateDisplayModeWithWidthAsync(width);
    }

    private async Task UpdateDisplayModeAsync()
    {
        var width = await GetWidthAsync();
        await UpdateDisplayModeWithWidthAsync(width);
    }

    private async Task UpdateDisplayModeWithWidthAsync(double width)
    {
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
        else if (paneDisplayMode == NavigationViewPaneDisplayMode.LeftFullScreen)
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

        if (PaneDisplayMode == NavigationViewPaneDisplayMode.Auto)
        {
            var expandedThreshold = 1008.0;
            var compactThreshold = 641.0;

            NavigationViewPaneDisplayMode targetMode;
            bool targetPaneOpen = IsPaneOpen;

            if (width >= expandedThreshold)
            {
                targetMode = NavigationViewPaneDisplayMode.Left;
                if (_activePaneDisplayMode != NavigationViewPaneDisplayMode.Left)
                {
                    targetPaneOpen = true;
                }
            }
            else if (width >= compactThreshold)
            {
                targetMode = NavigationViewPaneDisplayMode.LeftCompact;
                if (_activePaneDisplayMode != NavigationViewPaneDisplayMode.LeftCompact)
                {
                    targetPaneOpen = false;
                }
            }
            else
            {
                targetMode = NavigationViewPaneDisplayMode.LeftFullScreen;
                if (_activePaneDisplayMode != NavigationViewPaneDisplayMode.LeftFullScreen)
                {
                    targetPaneOpen = false;
                }
            }

            if (_activePaneDisplayMode != targetMode)
            {
                _activePaneDisplayMode = targetMode;
                if (IsPaneOpen != targetPaneOpen)
                {
                    IsPaneOpen = targetPaneOpen;
                    await IsPaneOpenChanged.InvokeAsync(IsPaneOpen);
                }
                StateHasChanged();
            }
        }
        else
        {
            // Auto close pane when switching to compact/minimal
            if (IsCompactOrMinimal && IsPaneOpen)
            {
                await TogglePaneAsync();
            }
        }
    }

    private async Task<double> GetWidthAsync()
    {
        if (_module != null)
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
        if (_context is not null)
        {
            _context.SelectionChanged -= OnContextSelectionChanged;
            _context.ExpansionChanged -= OnContextExpansionChanged;
        }
        _context?.Dispose();
        _selfReference?.Dispose();
        if (_module is not null)
        {
            try
            {
                if (_swipeWatcherActive)
                {
                    _module.InvokeVoidAsync("stopSwipeWatcher", _rootElement);
                }
                _module.InvokeVoidAsync("stopObservingResize", _rootElement);
                _module.DisposeAsync().AsTask().Wait();
            }
            catch { }
        }
    }
}
