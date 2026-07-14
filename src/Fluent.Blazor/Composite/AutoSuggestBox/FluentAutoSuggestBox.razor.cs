using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Fluent.Blazor.Composite;

/// <summary>
/// Mirrors WinUI's AutoSuggestBox / fluent-svelte's AutoSuggestBox.svelte — a free-typing TextBox
/// with a live-filtered suggestion dropdown underneath, distinct from ComboBox's editable mode in
/// that the typed text is NOT required to match any item: <see cref="Text"/> is always whatever
/// the user typed, <see cref="QuerySubmitted"/> fires with that raw text on Enter with no
/// suggestion highlighted, and picking a suggestion (click, or Enter while one IS highlighted)
/// fires <see cref="SuggestionChosen"/> separately and just replaces Text with the suggestion's
/// Name — there's no persistent "selected value" the way ComboBox has <c>Value</c>.
///
/// Composes FluentTextBox (same "don't reimplement input chrome" rule NumberBox follows) and,
/// like ComboBox/ContentDialog/NumberBox's Expanded popout, stays self-contained/absolutely
/// positioned rather than going through IOverlayService — the dropdown needs to be exactly the
/// trigger's width, same reasoning as ComboBox's own dropdown.
/// </summary>
public partial class FluentAutoSuggestBox<TValue> : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>The raw typed text. Two-way bindable — always reflects what's in the box, never
    /// snapped back to a suggestion's Name unless the user actually picks one.</summary>
    [Parameter] public string? Text { get; set; }

    [Parameter] public EventCallback<string?> TextChanged { get; set; }

    [Parameter, EditorRequired] public IReadOnlyList<AutoSuggestBoxItem<TValue>> Items { get; set; } = [];

    /// <summary>
    /// Optional custom match predicate over (typed text, candidate item). Defaults to a
    /// case-insensitive substring match against <see cref="AutoSuggestBoxItem{TValue}.Name"/> —
    /// deliberately "contains" rather than ComboBox's "starts-with", since AutoSuggestBox is meant
    /// for search-as-you-type against arbitrary positions in the name, not prefix completion.
    /// </summary>
    [Parameter] public Func<string, AutoSuggestBoxItem<TValue>, bool>? Filter { get; set; }

    /// <summary>Upper bound on how many matches are shown at once.</summary>
    [Parameter] public int MaxSuggestions { get; set; } = 8;

    /// <summary>Fired when the user picks a suggestion — click, or Enter while one is highlighted
    /// via arrow keys. Text is set to the suggestion's Name as part of the same interaction.</summary>
    [Parameter] public EventCallback<AutoSuggestBoxItem<TValue>> SuggestionChosen { get; set; }

    /// <summary>Fired on Enter when no suggestion is highlighted — the raw typed text is the
    /// query, same as WinUI's QuerySubmitted with a null ChosenSuggestion.</summary>
    [Parameter] public EventCallback<string?> QuerySubmitted { get; set; }

    /// <summary>Optional custom row content for each suggestion. Falls back to a plain
    /// <c>&lt;span&gt;@item.Name&lt;/span&gt;</c>, same default ComboBox's ItemTemplate uses.</summary>
    [Parameter] public RenderFragment<AutoSuggestBoxItem<TValue>>? ItemTemplate { get; set; }

    [Parameter] public string? Header { get; set; }

    /// <summary>
    /// When true, shows a search icon on the right of the box (via TextBox's Buttons slot). Once
    /// there's text in the box, that icon swaps to a "clear" icon that empties Text on click —
    /// same idea as the search field on the demo TextBox above, just wired in as an opt-in here.
    /// </summary>
    [Parameter] public bool EnableSearchIcon { get; set; }

    [Parameter] public string? Placeholder { get; set; }

    [Parameter] public bool Disabled { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private bool _open;
    private int _highlightedIndex = -1;

    // Same enter/exit animation shape as OverlaySurface (see its own comments for the full
    // rationale), just self-contained here instead of going through IOverlayService — the dropdown
    // is a plain `@if`-conditional <ul>, not an overlay entry, since (like ComboBox) it needs to be
    // exactly the trigger's width rather than independently positioned/measured.
    private bool _closing;
    // Bumped on every open/close request; a pending FinishClosingAsync only applies its own result if
    // the generation it captured is still current, so a rapid close-then-reopen (e.g. arrow-key-close
    // then immediately typing again) can't have a stale exit-animation wait clobber a state the user
    // has already moved past.
    private int _closeGeneration;
    private List<AutoSuggestBoxItem<TValue>> _lastMatches = [];
    private ElementReference _dropdownElement;
    private IJSObjectReference? _module;

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
            return Items.Where(i => predicate(Text, i)).Take(MaxSuggestions).ToList();
        }
    }

    private static bool DefaultFilter(string text, AutoSuggestBoxItem<TValue> item) =>
        item.Name.Contains(text, StringComparison.OrdinalIgnoreCase);

    private void OpenDropdown()
    {
        _closeGeneration++;
        _open = true;
        _closing = false;
    }

    private void CloseDropdown()
    {
        if (!_open)
        {
            return;
        }

        // Snapshot now, before Text/Items can change further — e.g. ClearAsync blanks Text in the
        // same call that closes the dropdown, which would make the live Matches property go empty
        // immediately and the list would vanish instead of fading out with its last contents.
        _lastMatches = Matches;
        _open = false;
        _closing = true;
        var generation = ++_closeGeneration;
        _ = FinishClosingAsync(generation);
    }

    private async Task FinishClosingAsync(int generation)
    {
        try
        {
            _module ??= await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/Fluent.Blazor/Overlay/overlay-interop.js");
            await _module.InvokeVoidAsync("waitForExitAnimation", _dropdownElement);
        }
        catch (JSDisconnectedException)
        {
            return;
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        // Only the most recent close request gets to actually unmount the list — if the user reopened
        // (or closed again) while this was waiting, that newer request already owns _closing/_open.
        if (generation == _closeGeneration && _closing)
        {
            _closing = false;
            StateHasChanged();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }

    private async Task OnTextChangedAsync(string? text)
    {
        Text = text;
        _highlightedIndex = -1;
        await TextChanged.InvokeAsync(Text);

        if (Matches.Count > 0)
        {
            OpenDropdown();
        }
        else
        {
            CloseDropdown();
        }
    }

    private void OnFocusIn()
    {
        if (!Disabled && Matches.Count > 0)
        {
            OpenDropdown();
        }
    }

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
                return;

            case "ArrowUp":
                if (matches.Count == 0)
                {
                    return;
                }

                OpenDropdown();
                _highlightedIndex = _highlightedIndex - 1 < 0 ? matches.Count - 1 : _highlightedIndex - 1;
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
}
