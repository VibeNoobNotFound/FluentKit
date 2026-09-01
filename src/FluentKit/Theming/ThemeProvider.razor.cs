using FluentKit.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FluentKit.Theming;

public partial class ThemeProvider : ComponentBase, IAsyncDisposable
{
    [Inject] private IThemeService ThemeService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public RenderFragment? ChildContent { get; set; }

    private JsModuleLifetime? _interop;
    private string? _lastAppliedTheme;
    private int _disposed;
    private bool _themeHandlerSubscribed;

    private JsModuleLifetime Interop => _interop ??= new(
        JS, "./_content/FluentKit/Theming/theme-interop.js");

    protected override Task OnInitializedAsync()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return Task.CompletedTask;
        }

        ThemeService.ThemeChanged += OnThemeChanged;
        _themeHandlerSubscribed = true;

        return Task.CompletedTask;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        // JS interop cannot run while an Interactive Server component is being prerendered.
        // Defer browser preference tracking and the document-level theme attribute until the
        // first interactive render; the cascading provider itself remains fully prerenderable.
        if (!await Interop.EnsureModuleAsync())
        {
            return;
        }

        // Reads prefers-color-scheme and sets up a live listener for OS-level changes.
        await ThemeService.InitializeAsync();

        if (Volatile.Read(ref _disposed) == 0)
        {
            await ApplyThemeAsync();
        }
    }

    private void OnThemeChanged() => ObserveBackgroundTask(OnThemeChangedAsync());

    private async Task OnThemeChangedAsync()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        try
        {
            await ApplyThemeAsync();
            if (Volatile.Read(ref _disposed) == 0)
            {
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (JSDisconnectedException)
        {
            // The circuit may disappear while the asynchronous theme update is in flight.
        }
        catch (OperationCanceledException) when (Volatile.Read(ref _disposed) != 0)
        {
            // Component-owned cancellation is expected during circuit teardown.
        }
    }

    private void ObserveBackgroundTask(Task task)
    {
        _ = task.ContinueWith(
            static (completed, state) =>
            {
                var component = (ThemeProvider)state!;
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

    private async Task ApplyThemeAsync()
    {
        var resolved = ThemeService.ResolvedTheme;
        if (Volatile.Read(ref _disposed) != 0 || resolved == _lastAppliedTheme)
        {
            return;
        }

        if (await Interop.InvokeVoidAsync("applyResolvedTheme", resolved))
        {
            _lastAppliedTheme = resolved;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_themeHandlerSubscribed)
        {
            ThemeService.ThemeChanged -= OnThemeChanged;
            _themeHandlerSubscribed = false;
        }

        if (_interop is not null)
        {
            await _interop.DisposeAsync();
        }
    }
}
