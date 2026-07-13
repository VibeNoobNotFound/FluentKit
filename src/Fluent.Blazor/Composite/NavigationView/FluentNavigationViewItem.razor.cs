using Microsoft.AspNetCore.Components;

namespace Fluent.Blazor.Composite;

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

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private NavigationViewContext? Context { get; set; }
    private ElementReference _element;
    private bool _hasChildren;

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
        }
        else
        {
            // Update context selection and notify parent view
            Context.SelectValue(Value);
            // The view will handle ItemInvoked and overlay close via its own logic
            if (Context is NavigationViewContext ctx)
            {
                // We need to notify the NavigationView that an item was clicked
                // The context can call the view's method, but we can use a callback.
                // Since we don't have a direct reference, we can let the view subscribe to SelectionChanged.
                // We'll add an event for item clicked, or we can use the existing SelectionChanged event.
                // But SelectionChanged only fires when the value changes, not on every click of the same item.
                // For item invoked, we should call ItemInvoked even if the same item is clicked.
                // We'll handle that in the NavigationView by observing clicks.
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