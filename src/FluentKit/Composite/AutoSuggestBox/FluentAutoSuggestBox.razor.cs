using FluentKit.Overlay;
using FluentKit.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace FluentKit.Composite;

/// <summary>
/// A free-typing text box with live suggestions. The suggestion list is rendered through
/// <see cref="IOverlayService"/> so it is not constrained by labels or acrylic ancestors.
/// </summary>
public partial class FluentAutoSuggestBox<TValue> : ComponentBase, IAsyncDisposable
{
    [Inject] private IOverlayService OverlayService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>The raw typed text. Two-way bindable.</summary>
    [Parameter] public string? Text { get; set; }
    [Parameter] public EventCallback<string?> TextChanged { get; set; }
    [Parameter, EditorRequired] public IReadOnlyList<AutoSuggestBoxItem<TValue>> Items { get; set; } = [];
    /// <summary>Optional predicate used to filter suggestions from the entered text.</summary>
    [Parameter] public Func<string, AutoSuggestBoxItem<TValue>, bool>? Filter { get; set; }
    /// <summary>Maximum number of matching suggestions displayed at once.</summary>
    [Parameter] public int MaxSuggestions { get; set; } = 8;
    /// <summary>Raised after a suggestion is selected.</summary>
    [Parameter] public EventCallback<AutoSuggestBoxItem<TValue>> SuggestionChosen { get; set; }
    /// <summary>Raised when Enter submits text without a highlighted suggestion.</summary>
    [Parameter] public EventCallback<string?> QuerySubmitted { get; set; }
    /// <summary>Optional custom content for each suggestion row.</summary>
    [Parameter] public RenderFragment<AutoSuggestBoxItem<TValue>>? ItemTemplate { get; set; }
    [Parameter] public string? Header { get; set; }
    /// <summary>Whether to display the search/clear affordance.</summary>
    [Parameter] public bool EnableSearchIcon { get; set; }
    [Parameter] public string? Placeholder { get; set; }
    [Parameter] public bool Disabled { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private readonly string _dropdownId = $"fluent-autosuggest-{Guid.NewGuid():N}";
    private bool _open;
    private int _highlightedIndex = -1;
    private List<AutoSuggestBoxItem<TValue>> _lastMatches = [];
    private ElementReference _rootElement;
    private ElementReference _dropdownElement;
    private Guid? _overlayId;
    private JsModuleLifetime? _interop;
    private bool _heightObserved;
    private int _disposed;

    private string DropdownId => _dropdownId;
    private IReadOnlyDictionary<string, object> TextBoxAttributes
    {
        get
        {
            var attributes = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["role"] = "combobox",
                ["aria-autocomplete"] = "list",
                ["aria-haspopup"] = "listbox",
                ["aria-controls"] = DropdownId,
                ["aria-expanded"] = _open.ToString().ToLowerInvariant()
            };

            if (AdditionalAttributes is not null)
            {
                foreach (var attribute in AdditionalAttributes)
                {
                    attributes[attribute.Key] = attribute.Value;
                }
            }

            return attributes;
        }
    }
    private List<AutoSuggestBoxItem<TValue>> DisplayMatches => _open ? Matches : _lastMatches;

    private List<AutoSuggestBoxItem<TValue>> Matches
    {
        get
        {
            if (string.IsNullOrEmpty(Text))
            {
                return [];
            }

            var predicate = Filter ?? DefaultFilter;
            return Items.Where(item => predicate(Text, item)).Take(MaxSuggestions).ToList();
        }
    }

    private static bool DefaultFilter(string text, AutoSuggestBoxItem<TValue> item) =>
        item.Name.Contains(text, StringComparison.OrdinalIgnoreCase);

    private JsModuleLifetime Interop => _interop ??= new(JS, "./_content/FluentKit/Overlay/overlay-interop.js");

    protected override void OnInitialized() => OverlayService.Changed += OnOverlayServiceChanged;

    protected override void OnParametersSet() => SynchronizeDropdown();

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (_overlayId is { } id)
        {
            OverlayService.Update(id, _rootElement, OverlayPlacement.Bottom, lightDismiss: true,
                matchAnchorWidth: true);
        }

        return ObserveHeightAsync();
    }

    private async Task ObserveHeightAsync()
    {
        if (Volatile.Read(ref _disposed) != 0 || !_open || _heightObserved || !await Interop.EnsureModuleAsync())
        {
            return;
        }

        if (await Interop.InvokeVoidAsync("observeAutoHeight", _dropdownElement))
        {
            _heightObserved = true;
        }
    }

    private void OnOverlayServiceChanged()
    {
        if (_overlayId is not { } id)
        {
            return;
        }

        var entry = OverlayService.Active.FirstOrDefault(candidate => candidate.Id == id);
        if (entry is null || entry.IsClosing)
        {
            _lastMatches = Matches;
            _overlayId = null;
            if (_open)
            {
                _open = false;
                _ = InvokeAsync(StateHasChanged);
            }
        }
    }

    private void SynchronizeDropdown()
    {
        if (Disabled || Matches.Count == 0)
        {
            CloseDropdown();
            return;
        }

        if (_open)
        {
            RefreshOpenDropdown();
        }
    }

    private void OpenDropdown()
    {
        if (Disabled || _open || Matches.Count == 0)
        {
            return;
        }

        _open = true;
        _overlayId = OverlayService.Show(RenderDropdown, _rootElement, OverlayPlacement.Bottom,
            lightDismiss: true, matchAnchorWidth: true);
        StateHasChanged();
    }

    private void CloseDropdown()
    {
        if (!_open && _overlayId is null)
        {
            return;
        }

        _lastMatches = Matches;
        StopHeightObservation();
        var overlayId = _overlayId;
        _overlayId = null;
        _open = false;
        if (overlayId is { } id)
        {
            OverlayService.Close(id);
        }

        StateHasChanged();
    }

    private void RefreshOpenDropdown()
    {
        if (_overlayId is not { } id)
        {
            return;
        }

        OverlayService.RefreshContent(id);
    }

    private void StopHeightObservation()
    {
        if (!_heightObserved)
        {
            return;
        }

        _heightObserved = false;
        ObserveBackgroundTask(StopHeightObservationAsync());
    }

    private async Task StopHeightObservationAsync()
    {
        if (_interop is not null && await _interop.EnsureModuleAsync())
        {
            await _interop.InvokeVoidAsync("unobserveAutoHeight", _dropdownElement);
        }
    }

    private void ObserveBackgroundTask(Task task)
    {
        _ = task.ContinueWith(
            static (completed, state) =>
            {
                var component = (FluentAutoSuggestBox<TValue>)state!;
                var exception = completed.Exception?.GetBaseException();
                if (exception is not null &&
                    exception is not JSDisconnectedException &&
                    (exception is not OperationCanceledException || Volatile.Read(ref component._disposed) == 0))
                {
                    _ = component.DispatchExceptionAsync(exception);
                }
            },
            this,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task OnTextChangedAsync(string? text)
    {
        Text = text;
        _highlightedIndex = -1;
        if (Matches.Count > 0)
        {
            OpenDropdown();
            RefreshOpenDropdown();
        }
        else
        {
            CloseDropdown();
        }

        await TextChanged.InvokeAsync(Text);
    }

    private void OnFocusIn() => OpenDropdown();
    private void OnFocusLost() => CloseDropdown();

    private async Task ClearAsync()
    {
        Text = string.Empty;
        _highlightedIndex = -1;
        CloseDropdown();
        await TextChanged.InvokeAsync(Text);
    }

    private async Task ChooseAsync(AutoSuggestBoxItem<TValue> item)
    {
        if (item.Disabled)
        {
            return;
        }

        Text = item.Name;
        _highlightedIndex = -1;
        CloseDropdown();
        await TextChanged.InvokeAsync(Text);
        await SuggestionChosen.InvokeAsync(item);
    }

    private async Task OnKeyDownAsync(KeyboardEventArgs e)
    {
        var matches = Matches;

        switch (e.Key)
        {
            case "Escape":
                CloseDropdown();
                _highlightedIndex = -1;
                return;

            case "ArrowDown":
                if (matches.Count == 0)
                {
                    return;
                }

                OpenDropdown();
                _highlightedIndex = _highlightedIndex + 1 >= matches.Count ? 0 : _highlightedIndex + 1;
                RefreshOpenDropdown();
                return;

            case "ArrowUp":
                if (matches.Count == 0)
                {
                    return;
                }

                OpenDropdown();
                _highlightedIndex = _highlightedIndex - 1 < 0 ? matches.Count - 1 : _highlightedIndex - 1;
                RefreshOpenDropdown();
                return;

            case "Enter":
                if (_open && _highlightedIndex >= 0 && _highlightedIndex < matches.Count)
                {
                    await ChooseAsync(matches[_highlightedIndex]);
                }
                else
                {
                    CloseDropdown();
                    await QuerySubmitted.InvokeAsync(Text);
                }
                return;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (OverlayService is not null)
        {
            OverlayService.Changed -= OnOverlayServiceChanged;
            if (_overlayId is { } id)
            {
                OverlayService.Close(id);
            }
        }

        if (_interop is not null)
        {
            if (_heightObserved)
            {
                await _interop.DisposeAsync(("unobserveAutoHeight", new object?[] { _dropdownElement }));
            }
            else
            {
                await _interop.DisposeAsync();
            }
        }
    }
}
