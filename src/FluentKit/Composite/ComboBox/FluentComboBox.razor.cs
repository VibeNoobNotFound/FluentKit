using FluentKit.Overlay;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FluentKit.Composite;

/// <summary>
/// A WinUI-style selectable list with optional editable search. Its dropdown is rendered through
/// <see cref="IOverlayService"/> so it can escape labels, clipping ancestors, and backdrop-filter
/// roots while retaining the trigger's width and selected-row alignment.
/// </summary>
public partial class FluentComboBox<TValue> : ComponentBase, IDisposable
{
    [Inject] private IOverlayService OverlayService { get; set; } = default!;

    /// <summary>Currently selected item's value. Two-way bindable.</summary>
    [Parameter] public TValue? Value { get; set; }
    [Parameter] public EventCallback<TValue?> ValueChanged { get; set; }
    /// <summary>Current editable search text.</summary>
    [Parameter] public string? SearchValue { get; set; }
    [Parameter] public EventCallback<string?> SearchValueChanged { get; set; }
    [Parameter, EditorRequired] public IReadOnlyList<ComboBoxItem<TValue>> Items { get; set; } = [];
    /// <summary>Optional custom content for each dropdown row.</summary>
    [Parameter] public RenderFragment<ComboBoxItem<TValue>>? ItemTemplate { get; set; }
    /// <summary>Optional custom content for the selected non-editable trigger label.</summary>
    [Parameter] public RenderFragment<ComboBoxItem<TValue>>? SelectedTemplate { get; set; }
    [Parameter] public string? Placeholder { get; set; }
    /// <summary>Whether the ComboBox permits typing and prefix matching.</summary>
    [Parameter] public bool Editable { get; set; }
    [Parameter] public bool Disabled { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private const int ItemHeight = 36;
    private const int MaxVisibleItems = 14;

    private readonly string _dropdownId = $"fluent-combo-box-{Guid.NewGuid():N}";
    private bool _open;
    private bool _searchValueInitialized;
    private ElementReference _rootElement;
    private Guid? _overlayId;

    private string DropdownId => _dropdownId;
    private string AriaExpanded => _open.ToString().ToLowerInvariant();
    private IEnumerable<ComboBoxItem<TValue>> SelectableItems => Items.Where(item => !item.Disabled);
    private ComboBoxItem<TValue>? Selection => Items.FirstOrDefault(item => EqualityComparer<TValue>.Default.Equals(item.Value, Value!));

    private int MenuOffsetPx
    {
        get
        {
            var selectedIndex = Selection is null ? -1 : Items.ToList().IndexOf(Selection);
            var fallbackIndex = Items.Count > MaxVisibleItems ? MaxVisibleItems / 2 : Items.Count / 2;
            return -ItemHeight * (selectedIndex >= 0 ? selectedIndex : fallbackIndex);
        }
    }

    private OverlayPositioningOptions Positioning => Editable
        ? new() { MainAxisOffset = -4 }
        : new() { Alignment = OverlayAnchorAlignment.AnchorStart, MainAxisOffset = MenuOffsetPx - 6 };

    protected override void OnInitialized() => OverlayService.Changed += OnOverlayServiceChanged;

    protected override void OnParametersSet()
    {
        if (Editable && !_searchValueInitialized)
        {
            _searchValueInitialized = true;
            if (string.IsNullOrEmpty(SearchValue) && Selection is not null)
            {
                SearchValue = Selection.Name;
            }
        }

        RefreshOpenDropdown();
    }

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (_overlayId is { } id)
        {
            OverlayService.Update(id, _rootElement, Positioning, OverlayPlacement.Bottom, lightDismiss: true,
                matchAnchorWidth: true);
        }

        return Task.CompletedTask;
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
            _overlayId = null;
            if (_open)
            {
                _open = false;
                _ = InvokeAsync(StateHasChanged);
            }
        }
    }

    private Task ToggleOpenAsync()
    {
        if (!Disabled)
        {
            if (_open)
            {
                CloseDropdown();
            }
            else
            {
                OpenDropdown();
            }
        }

        return Task.CompletedTask;
    }

    private void OpenDropdown()
    {
        if (Disabled || Items.Count == 0 || _open)
        {
            return;
        }

        _open = true;
        _overlayId = OverlayService.Show(RenderDropdown, _rootElement, Positioning, OverlayPlacement.Bottom,
            lightDismiss: true, bare: false, matchAnchorWidth: true, scrollAnchorIntoView: false,
            watchAnchorRemoved: false);
        StateHasChanged();
    }

    private void CloseDropdown()
    {
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
        if (!_open || _overlayId is not { } id)
        {
            return;
        }

        OverlayService.Update(id, _rootElement, Positioning, OverlayPlacement.Bottom, lightDismiss: true,
            matchAnchorWidth: true);
        OverlayService.RefreshContent(id);
    }

    private async Task SelectAsync(ComboBoxItem<TValue> item)
    {
        if (item.Disabled)
        {
            return;
        }

        Value = item.Value;
        if (Editable)
        {
            SearchValue = item.Name;
        }

        CloseDropdown();
        await ValueChanged.InvokeAsync(Value);
        if (Editable)
        {
            await SearchValueChanged.InvokeAsync(SearchValue);
        }
    }

    private async Task OnSearchInputAsync(ChangeEventArgs e)
    {
        var text = e.Value?.ToString() ?? string.Empty;
        SearchValue = text;

        var match = SelectableItems.FirstOrDefault(item => item.Name.StartsWith(text, StringComparison.OrdinalIgnoreCase));
        Value = match is not null ? match.Value : text.Length == 0 ? default : Value;
        RefreshOpenDropdown();

        await SearchValueChanged.InvokeAsync(SearchValue);
        if (match is not null || text.Length == 0)
        {
            await ValueChanged.InvokeAsync(Value);
        }
    }

    private async Task OnKeyDownAsync(KeyboardEventArgs e)
    {
        var editableClosed = Editable && !_open;

        switch (e.Key)
        {
            case "Escape":
            case "Tab":
                CloseDropdown();
                return;

            case "Enter":
            case " " when _open:
                if (Selection is not null)
                {
                    await SelectAsync(Selection);
                }
                return;

            case "ArrowDown" when !editableClosed:
                await MoveSelectionAsync(+1);
                if (Editable && !_open)
                {
                    OpenDropdown();
                }
                return;

            case "ArrowUp" when !editableClosed:
                await MoveSelectionAsync(-1);
                return;

            case "Home":
                await JumpSelectionAsync(toFirst: true);
                return;

            case "End":
                await JumpSelectionAsync(toFirst: false);
                return;

            case "ArrowDown" or "ArrowUp" when Editable && !_open:
                OpenDropdown();
                return;
        }
    }

    private async Task MoveSelectionAsync(int direction)
    {
        var selectable = SelectableItems.ToList();
        var currentIndex = Selection is null ? -1 : selectable.IndexOf(Selection);
        var nextIndex = currentIndex + direction;
        if (nextIndex >= 0 && nextIndex < selectable.Count)
        {
            await SelectSilentlyAsync(selectable[nextIndex]);
        }
    }

    private async Task JumpSelectionAsync(bool toFirst)
    {
        var selectable = SelectableItems.ToList();
        if (selectable.Count > 0)
        {
            await SelectSilentlyAsync(toFirst ? selectable[0] : selectable[^1]);
        }
    }

    private async Task SelectSilentlyAsync(ComboBoxItem<TValue> item)
    {
        Value = item.Value;
        if (Editable)
        {
            SearchValue = item.Name;
        }

        RefreshOpenDropdown();
        await ValueChanged.InvokeAsync(Value);
        if (Editable)
        {
            await SearchValueChanged.InvokeAsync(SearchValue);
        }
    }

    public void Dispose()
    {
        if (OverlayService is not null)
        {
            OverlayService.Changed -= OnOverlayServiceChanged;
            if (_overlayId is { } id)
            {
                OverlayService.Close(id);
            }
        }
    }
}
