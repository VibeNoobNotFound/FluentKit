using Microsoft.AspNetCore.Components;

namespace FluentKit.Composite;

public partial class FluentNavigationViewItem : ComponentBase, IDisposable
{
    [CascadingParameter]
    private NavigationViewContext? CascadedContext { get; set; }

    [Parameter, EditorRequired]
    public object? Value { get; set; }

    [Parameter, EditorRequired]
    public string Text { get; set; } = string.Empty;

    [Parameter]
    public RenderFragment? Icon { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public bool IsExpanded { get; set; }

    [Parameter]
    public EventCallback<bool> IsExpandedChanged { get; set; }

    [Parameter]
    public bool? IsSelectable { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private NavigationViewContext? Context { get; set; }
    private ElementReference _element;
    private bool _hasChildren;
    private bool _lastIsExpandedForNotify;

    private bool RealIsSelectable => IsSelectable ?? !_hasChildren;

    // Compute selection based on context's SelectedValue
    private bool IsSelected => Context?.SelectedValue != null && Equals(Context.SelectedValue, Value);

    protected override void OnInitialized()
    {
        Context = CascadedContext;
        if (Context != null)
        {
            Context.SelectionChanged += OnSelectionChanged;
        }
        _hasChildren = ChildContent != null;
        _lastIsExpandedForNotify = IsExpanded;
    }

    protected override void OnParametersSet()
    {
        // Catches expand/collapse driven by the consumer's own bound state (@bind-IsExpanded set
        // from outside, e.g. "collapse all" logic or an externally-controlled accordion), not
        // just the in-component toggle in HandleClickAsync below. Either way, the rendered
        // branch changed and the navigation view must re-resolve its visible selection anchor.
        if (_lastIsExpandedForNotify != IsExpanded)
        {
            _lastIsExpandedForNotify = IsExpanded;
            Context?.NotifyExpansionChanged();
        }
        base.OnParametersSet();
    }

    private void OnSelectionChanged()
    {
        // Re-render when selection changes in the context
        StateHasChanged();
    }

    private async Task HandleClickAsync()
    {
        if (Disabled || Context is null) return;

        if (_hasChildren)
        {
            IsExpanded = !IsExpanded;
            await IsExpandedChanged.InvokeAsync(IsExpanded);
            Context.NotifyExpansionChanged();
            if (RealIsSelectable)
            {
                Context.SelectValue(Value);
            }
        }
        else
        {
            if (RealIsSelectable)
            {
                Context.SelectValue(Value);
            }
        }
    }

    // Mouse handlers (optional)
    private void HandleMouseEnter() { }
    private void HandleMouseLeave() { }
    private void HandleMouseDown() { }
    private void HandleMouseUp() { }

    public void Dispose()
    {
        if (Context != null)
        {
            Context.SelectionChanged -= OnSelectionChanged;
        }
    }
}
