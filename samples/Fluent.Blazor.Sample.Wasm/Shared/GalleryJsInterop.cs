using Microsoft.JSInterop;

namespace Fluent.Blazor.Sample.Wasm.Shared;

/// <summary>
/// Thin wrapper around the JS helpers declared in index.html for Prism.js
/// syntax highlighting and other gallery-level DOM utilities.
/// </summary>
public sealed class GalleryJsInterop : IAsyncDisposable
{
    private readonly IJSRuntime _js;

    public GalleryJsInterop(IJSRuntime js)
    {
        _js = js;
    }

    /// <summary>Highlight a single &lt;code&gt; element by its DOM id.</summary>
    public async ValueTask HighlightElementAsync(string elementId)
    {
        try
        {
            await _js.InvokeVoidAsync("galleryHighlight", elementId);
        }
        catch (JSException) { /* Prism not yet loaded — silently ignore */ }
        catch (TaskCanceledException) { }
    }

    /// <summary>Re-highlight every code element on the page.</summary>
    public async ValueTask HighlightAllAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("galleryHighlightAll");
        }
        catch (JSException) { }
        catch (TaskCanceledException) { }
    }

    /// <summary>Copy text to the system clipboard.</summary>
    public async ValueTask CopyToClipboardAsync(string text)
    {
        try
        {
            await _js.InvokeVoidAsync("navigator.clipboard.writeText", text);
        }
        catch (JSException) { }
        catch (TaskCanceledException) { }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
