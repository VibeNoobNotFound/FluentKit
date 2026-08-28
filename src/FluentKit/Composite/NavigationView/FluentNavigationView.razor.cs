using FluentKit.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FluentKit.Composite;

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

public partial class FluentNavigationView : ComponentBase, IDisposable, IAsyncDisposable
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

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private ElementReference _rootElement;
    private ElementReference _itemsContainerElement;
    private ElementReference _indicatorElement;
    private NavigationViewContext? _context;
    private JsModuleLifetime? _interop;
    private DotNetObjectReference<FluentNavigationView>? _selfReference;
    private Action<object?>? _itemClickedHandler;
    private int _disposed;

    // A selection change is recorded here (by OnContextSelectionChanged) and only actually
    // measured/animated from OnAfterRenderAsync, once Blazor has patched the DOM for whatever
    // triggered it (see OnContextSelectionChanged for why this can't happen synchronously).
    // Deliberately just a bool, not a remembered "target key": the JS side always re-resolves
    // the target by querying for .fluent-nav-view-item--selected at animate time, so the
    // indicator can never be pointed at a stale/wrong element - it always follows whatever the
    // DOM currently says is selected, not whatever we think we clicked.
    private bool _hasPendingIndicatorAnimation;

    private JsModuleLifetime Interop => _interop ??= new(
        JS, "./_content/FluentKit/Composite/NavigationView/navview-interop.js");

    private NavigationViewPaneDisplayMode _activePaneDisplayMode = NavigationViewPaneDisplayMode.Left;
    private NavigationViewPaneDisplayMode _lastPaneDisplayMode = NavigationViewPaneDisplayMode.Left;
    private bool _isTransitioning;
    private System.Threading.CancellationTokenSource? _transitionCts;

    public NavigationViewDisplayMode DisplayMode { get; private set; } = NavigationViewDisplayMode.Expanded;

    private bool IsCompactOrMinimal =>
        _activePaneDisplayMode is NavigationViewPaneDisplayMode.LeftCompact or NavigationViewPaneDisplayMode.LeftMinimal;

    private string PaneDisplayModeClass => _activePaneDisplayMode switch
    {
        NavigationViewPaneDisplayMode.LeftCompact => "compact",
        NavigationViewPaneDisplayMode.LeftMinimal => "minimal",
        NavigationViewPaneDisplayMode.Top => "top",
        NavigationViewPaneDisplayMode.Auto => "auto",
        _ => "expanded"
    };

    protected override void OnInitialized()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        _context = new NavigationViewContext(this);
        _context.SelectionChanged += OnContextSelectionChanged;
        _itemClickedHandler = OnItemClickedFromContext;
        _context.ItemClicked += _itemClickedHandler;
        _activePaneDisplayMode = PaneDisplayMode;
        _lastPaneDisplayMode = PaneDisplayMode;
        base.OnInitialized();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        if (firstRender)
        {
            _selfReference = DotNetObjectReference.Create(this);
            if (!await Interop.EnsureModuleAsync())
            {
                return;
            }

            await Interop.InvokeVoidAsync("startObservingResize", _rootElement, _selfReference);

            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            await UpdateDisplayModeAsync();

            // Place the ribbon indicator on whatever is selected at startup, instantly
            // (no slide-in from nowhere on first paint).
            await UpdateIndicatorAsync(animate: false);
            _hasPendingIndicatorAnimation = false;
        }
        else if (_hasPendingIndicatorAnimation)
        {
            // DOM now reflects whatever expand/collapse the triggering click caused, so it's
            // safe to measure real positions.
            _hasPendingIndicatorAnimation = false;
            await UpdateIndicatorAsync(animate: true);
        }
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
            if (token.IsCancellationRequested || Volatile.Read(ref _disposed) != 0) return;

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
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        var newValue = _context?.SelectedValue;

        // Don't touch the DOM here: this handler can fire from a click that *also* expands or
        // collapses a sibling group (e.g. a parent item with Value=null and children), which
        // inserts/removes rows and shifts everything below it. If we measure positions right
        // now, we're measuring the *old* layout, before Blazor has patched the DOM for that
        // change. Defer to OnAfterRenderAsync, and resolve the target there by querying for
        // .fluent-nav-view-item--selected rather than remembering "the item we clicked" - that
        // way the indicator always converges on whatever the DOM says is actually selected.
        _hasPendingIndicatorAnimation = true;

        if (!Equals(SelectedValue, newValue))
        {
            SelectedValue = newValue;
            _ = SelectedValueChanged.InvokeAsync(SelectedValue);
        }

        StateHasChanged();
    }

    /// <summary>Moves the ribbon indicator onto whichever item currently has
    /// .fluent-nav-view-item--selected in the DOM. Never told which item to target - it always
    /// re-resolves that itself, so it can't drift onto a stale/wrong element the way passing a
    /// remembered "target key" around could.</summary>
    private async Task UpdateIndicatorAsync(bool animate)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        await Interop.InvokeVoidAsync("updateNavIndicator", _itemsContainerElement, _indicatorElement, animate);
    }

    internal void NotifyContextSelectionChanged(object? newValue)
    {
        OnContextSelectionChanged();
    }

    private async Task OnItemClicked(object? value)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

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

        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        // Close overlay if in compact/minimal and pane is open
        if (IsCompactOrMinimal && IsPaneOpen)
        {
            await TogglePaneAsync();
        }
    }

    private async Task TogglePaneAsync()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        IsPaneOpen = !IsPaneOpen;
        await IsPaneOpenChanged.InvokeAsync(IsPaneOpen);
        if (Volatile.Read(ref _disposed) == 0)
        {
            StateHasChanged();
        }
    }

    [JSInvokable]
    public async Task OnResize(double width)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        await UpdateDisplayModeWithWidthAsync(width);
    }

    private async Task UpdateDisplayModeAsync()
    {
        var width = await GetWidthAsync();
        if (Volatile.Read(ref _disposed) == 0)
        {
            await UpdateDisplayModeWithWidthAsync(width);
        }
    }

    private async Task UpdateDisplayModeWithWidthAsync(double width)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

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
            if (Volatile.Read(ref _disposed) == 0)
            {
                StateHasChanged();
            }
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
                targetMode = NavigationViewPaneDisplayMode.LeftMinimal;
                if (_activePaneDisplayMode != NavigationViewPaneDisplayMode.LeftMinimal)
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
                if (Volatile.Read(ref _disposed) == 0)
                {
                    StateHasChanged();
                }
            }
        }
        else
        {
            // Auto close pane when switching to compact/minimal
            if (IsCompactOrMinimal && IsPaneOpen)
            {
                if (Volatile.Read(ref _disposed) == 0)
                {
                    await TogglePaneAsync();
                }
            }
        }
    }

    private async Task<double> GetWidthAsync()
    {
        if (Volatile.Read(ref _disposed) == 0)
        {
            var result = await Interop.InvokeAsync<double>("getElementWidth", _rootElement);
            if (result.Succeeded)
            {
                return result.Value;
            }
        }
        return 800;
    }

    private async Task OnBackClick() => await BackRequested.InvokeAsync();

    private void OnItemClickedFromContext(object? value)
    {
        _ = OnItemClicked(value);
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _transitionCts?.Cancel();
        _transitionCts?.Dispose();
        _transitionCts = null;

        var context = _context;
        if (context is not null)
        {
            context.SelectionChanged -= OnContextSelectionChanged;
            if (_itemClickedHandler is not null)
            {
                context.ItemClicked -= _itemClickedHandler;
            }

            context.Dispose();
        }

        var selfReference = Interlocked.Exchange(ref _selfReference, null);

        try
        {
            if (_interop is not null)
            {
                await _interop.DisposeAsync(("stopObservingResize", new object?[] { _rootElement }));
            }
        }
        finally
        {
            selfReference?.Dispose();
        }
    }
}
