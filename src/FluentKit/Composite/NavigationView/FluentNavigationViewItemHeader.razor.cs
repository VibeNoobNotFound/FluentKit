using Microsoft.AspNetCore.Components;

namespace FluentKit.Composite;

public partial class FluentNavigationViewItemHeader : ComponentBase
{
    [Parameter, EditorRequired]
    public string Text { get; set; } = string.Empty;
}
