using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Fluent.Blazor.Composite;

/// <summary>A single tab within a FluentPivot. See FluentPivot.razor.cs for the overall shape.</summary>
public partial class FluentPivotItem : ComponentBase, IDisposable
{
    [CascadingParameter] private PivotContext? Pivot { get; set; }

    /// <summary>Plain-text tab label — the common case. Ignored if <see cref="TabTemplate"/> is set.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>Custom tab header content (e.g. an icon + text), overriding <see cref="Title"/>.</summary>
    [Parameter] public RenderFragment? TabTemplate { get; set; }

    /// <summary>The pane content shown while this tab is selected.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    [Parameter] public bool Disabled { get; set; }

    internal RenderFragment RenderHeader => TabTemplate ?? (RenderFragment)(builder =>
    {
        builder.AddContent(0, Title);
    });

    protected override void OnInitialized() => Pivot?.Register(this);

    public void Dispose() => Pivot?.Unregister(this);
}
