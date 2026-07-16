using Microsoft.JSInterop;

namespace FluentKit.Sample.Shared.Shared;

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

    /// <summary>
    /// Push raw source text into a &lt;code&gt; element and re-run Prism on it — used to
    /// keep a live editor's highlighted overlay in sync with a plain-text &lt;textarea&gt;
    /// on every keystroke.
    /// </summary>
    public async ValueTask LiveHighlightAsync(string codeElementId, string code)
    {
        try
        {
            await _js.InvokeVoidAsync("liveHighlight", codeElementId, code);
        }
        catch (JSException) { }
        catch (TaskCanceledException) { }
    }

    /// <summary>
    /// Wires up scroll-position syncing between a live editor's &lt;textarea&gt; and the
    /// highlighted &lt;pre&gt; stacked beneath it. Safe to call on every render — the JS
    /// side only attaches its listener once per textarea.
    /// </summary>
    public async ValueTask LiveHighlightSyncScrollAsync(string textareaId, string preId)
    {
        try
        {
            await _js.InvokeVoidAsync("liveHighlightSyncScroll", textareaId, preId);
        }
        catch (JSException) { }
        catch (TaskCanceledException) { }
    }

    /// <summary>Read a raw string value from localStorage, or null if missing/unavailable.</summary>
    public async ValueTask<string?> GetStorageItemAsync(string key)
    {
        try
        {
            return await _js.InvokeAsync<string?>("galleryStorageGet", key);
        }
        catch (JSException) { return null; }
        catch (TaskCanceledException) { return null; }
    }

    /// <summary>Write a raw string value to localStorage. Silently no-ops on failure (e.g. quota exceeded).</summary>
    public async ValueTask SetStorageItemAsync(string key, string value)
    {
        try
        {
            await _js.InvokeVoidAsync("galleryStorageSet", key, value);
        }
        catch (JSException) { }
        catch (TaskCanceledException) { }
    }

    /// <summary>Remove a key from localStorage.</summary>
    public async ValueTask RemoveStorageItemAsync(string key)
    {
        try
        {
            await _js.InvokeVoidAsync("galleryStorageRemove", key);
        }
        catch (JSException) { }
        catch (TaskCanceledException) { }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
