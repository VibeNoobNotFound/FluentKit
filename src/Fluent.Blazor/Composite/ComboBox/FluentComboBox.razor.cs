using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Fluent.Blazor.Composite;

/// <summary>
/// Mirrors fluent-svelte's ComboBox.svelte (dropdown list with an optional editable/searchable
/// text-box mode). Two render modes off <see cref="Editable"/>:
///   - false (default): a FluentButton-shaped trigger showing the selected item's Name, opens a
///     positioned &lt;ul&gt; of ComboBoxItem-equivalent &lt;li&gt; rows on click.
///   - true: a FluentTextBox-shaped trigger the user can type into, filtering/matching items by
///     Name prefix (case-insensitive), same as fluent-svelte's handleInput/searchValue flow.
///
/// Unlike FluentMenuFlyout, this does NOT go through IOverlayService/FluentOverlayHost — the
/// dropdown list needs to be exactly the trigger's width and grow from a specific list index (the
/// menuOffset/menuGrowDirection choreography below), which is easiest to keep as a plain
/// absolutely-positioned child of the component's own root (same as ComboBox.scss's own
/// `position: relative` + `.combo-box-dropdown { position: absolute }`). A portal would only add
/// indirection for zero benefit here since there's no anchor-to-elsewhere requirement.
/// </summary>
public partial class FluentComboBox<TValue> : ComponentBase
{
    /// <summary>Currently selected item's value. Two-way bindable.</summary>
    [Parameter] public TValue? Value { get; set; }

    [Parameter] public EventCallback<TValue?> ValueChanged { get; set; }

    /// <summary>Current text in the search box. Only meaningful when <see cref="Editable"/> is true.</summary>
    [Parameter] public string? SearchValue { get; set; }

    [Parameter] public EventCallback<string?> SearchValueChanged { get; set; }

    [Parameter, EditorRequired] public IReadOnlyList<ComboBoxItem<TValue>> Items { get; set; } = [];

    [Parameter] public string? Placeholder { get; set; }

    /// <summary>Whether the ComboBox can be searched/typed into (fluent-svelte's `editable` prop).</summary>
    [Parameter] public bool Editable { get; set; }

    [Parameter] public bool Disabled { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private bool _open;
    private ElementReference _rootElement;

    private const int ItemHeight = 36;
    private const int MaxVisibleItems = 14; // 504px max-block-size / 36px itemHeight, per ComboBox.scss

    private IEnumerable<ComboBoxItem<TValue>> SelectableItems => Items.Where(i => !i.Disabled);

    private ComboBoxItem<TValue>? Selection =>
        Items.FirstOrDefault(i => EqualityComparer<TValue>.Default.Equals(i.Value, Value!));

    /// <summary>
    /// Which edge the dropdown visually grows from, purely cosmetic (drives the clip-path grow
    /// animation direction in FluentComboBox.razor.css) — same three-way split as fluent-svelte's
    /// menuGrowDirection: grows from the selected row if it's near an edge of the list, otherwise
    /// from the center.
    /// </summary>
    private string GrowDirectionClass
    {
        get
        {
            if (Editable)
            {
                return "top";
            }

            var mid = Items.Count / 2;
            var selectedIndex = Selection is null ? -1 : Items.ToList().IndexOf(Selection);

            if (selectedIndex < 0 || selectedIndex == mid)
            {
                return "center";
            }

            return selectedIndex < mid ? "top" : "bottom";
        }
    }

    /// <summary>
    /// Vertical offset (px) so the dropdown opens with the selected row aligned under the trigger,
    /// rather than always starting from row 0 — same idea as fluent-svelte's `menuOffset`.
    /// </summary>
    private int MenuOffsetPx
    {
        get
        {
            var selectedIndex = Selection is null ? -1 : Items.ToList().IndexOf(Selection);
            var fallbackIndex = Items.Count > MaxVisibleItems ? MaxVisibleItems / 2 : Items.Count / 2;
            return -ItemHeight * (selectedIndex >= 0 ? selectedIndex : fallbackIndex);
        }
    }

    private bool _searchValueInitialized;

    protected override void OnParametersSet()
    {
        // Mirrors the svelte onMount exactly: seed the search box from the current selection ONCE,
        // on first render only. The old version re-checked IsNullOrEmpty(SearchValue) on every
        // render, so backspacing the box to empty (a perfectly valid "no match" state) would get
        // silently overwritten back to the selected item's name on the next render — this is the
        // bug where clearing the editable box snaps back to the current selection.
        if (Editable && !_searchValueInitialized)
        {
            _searchValueInitialized = true;
            if (string.IsNullOrEmpty(SearchValue) && Selection is not null)
            {
                SearchValue = Selection.Name;
            }
        }
    }

    private async Task ToggleOpenAsync()
    {
        if (Disabled)
        {
            return;
        }

        await SetOpenAsync(!_open);
    }

    private async Task SetOpenAsync(bool value)
    {
        if (_open == value)
        {
            return;
        }

        _open = value;
        StateHasChanged();

        if (_open && Editable)
        {
            // Let the dropdown render, then focus/select the search input the way svelte's
            // `await tick(); searchInputElement.focus()` does.
            await Task.Yield();
        }
    }

    private async Task SelectAsync(ComboBoxItem<TValue> item)
    {
        if (item.Disabled)
        {
            return;
        }

        Value = item.Value;
        await ValueChanged.InvokeAsync(Value);

        if (Editable)
        {
            SearchValue = item.Name;
            await SearchValueChanged.InvokeAsync(SearchValue);
        }

        await SetOpenAsync(false);
    }

    private async Task OnSearchInputAsync(ChangeEventArgs e)
    {
        var text = e.Value?.ToString() ?? string.Empty;
        SearchValue = text;
        await SearchValueChanged.InvokeAsync(SearchValue);

        var match = SelectableItems.FirstOrDefault(
            i => i.Name.StartsWith(text, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
        {
            Value = match.Value;
            await ValueChanged.InvokeAsync(Value);
        }
        else if (text.Length == 0)
        {
            Value = default;
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
                await SetOpenAsync(false);
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
                    await SetOpenAsync(true);
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
                await SetOpenAsync(true);
                return;
        }
    }

    private async Task MoveSelectionAsync(int direction)
    {
        var selectable = SelectableItems.ToList();
        if (selectable.Count == 0)
        {
            return;
        }

        var currentIndex = Selection is null ? -1 : selectable.IndexOf(Selection);
        var nextIndex = currentIndex + direction;

        if (nextIndex < 0 || nextIndex >= selectable.Count)
        {
            return;
        }

        await SelectSilentlyAsync(selectable[nextIndex]);
    }

    private async Task JumpSelectionAsync(bool toFirst)
    {
        var selectable = SelectableItems.ToList();
        if (selectable.Count == 0)
        {
            return;
        }

        await SelectSilentlyAsync(toFirst ? selectable[0] : selectable[^1]);
    }

    /// <summary>Updates Value/SearchValue for keyboard navigation without closing the dropdown or
    /// treating it as a final "select" action (mirrors svelte's direct `value = ...` assignments
    /// inside handleKeyboardNavigation, as distinct from the explicit `selectItem` call on Enter).</summary>
    private async Task SelectSilentlyAsync(ComboBoxItem<TValue> item)
    {
        Value = item.Value;
        await ValueChanged.InvokeAsync(Value);

        if (Editable)
        {
            SearchValue = item.Name;
            await SearchValueChanged.InvokeAsync(SearchValue);
        }
    }

    private async Task OnFocusLostAsync()
    {
        // Closest Blazor equivalent of svelte's externalMouseEvents/outermousedown dismiss —
        // <ul tabindex> loses focus when the user clicks elsewhere, since nothing inside the
        // dropdown intercepts focus permanently.
        await SetOpenAsync(false);
    }
}
