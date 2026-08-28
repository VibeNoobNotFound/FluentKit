using Microsoft.JSInterop;

namespace FluentKit.Interop;

internal static class JsModuleDisposal
{
    public static async ValueTask DisposeAsync(IJSObjectReference? module)
    {
        if (module is null)
        {
            return;
        }

        try
        {
            await module.DisposeAsync().ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
            // A server-side circuit may already be gone when the component is disposed.
        }
    }
}
