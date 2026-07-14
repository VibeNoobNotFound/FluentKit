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