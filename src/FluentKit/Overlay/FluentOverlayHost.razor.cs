using Microsoft.AspNetCore.Components;

namespace FluentKit.Overlay;

public partial class FluentOverlayHost : ComponentBase, IDisposable
{
    [Inject] private IOverlayService OverlayService { get; set; } = default!;

    protected override void OnInitialized()
    {
        OverlayService.Changed += StateHasChangedOnMainThread;
    }

    private void StateHasChangedOnMainThread() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        OverlayService.Changed -= StateHasChangedOnMainThread;
    }
}
