using FluentKit.Interop;
using Microsoft.JSInterop;

namespace FluentKit.Tests;

public sealed class JsModuleLifetimeTests
{
    [Fact]
    public async Task TeardownCancellationCompletesTheCallAndDisposesTheModule()
    {
        var module = new TestJsObjectReference
        {
            BlockInvocationsUntilCanceled = true,
            ThrowTaskCanceledExceptionWhenCanceled = true
        };
        var lifetime = new JsModuleLifetime(new TestJsRuntime(module), "./test.js");
        lifetime.Module = module;

        var call = lifetime.InvokeAsync<string>("renderMica").AsTask();
        await WaitUntilAsync(() => module.Invocations.Count == 1);

        await lifetime.DisposeAsync();
        var result = await call;

        Assert.False(result.Succeeded);
        Assert.Equal(1, module.DisposeCount);
    }

    [Fact]
    public async Task LiveOperationCancellationStillPropagates()
    {
        var module = new TestJsObjectReference();
        module.ThrowOnInvoke("renderMica", new OperationCanceledException("JS call timed out"));
        var lifetime = new JsModuleLifetime(new TestJsRuntime(module), "./test.js");
        lifetime.Module = module;

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => lifetime.InvokeAsync<string>("renderMica").AsTask());
    }

    [Fact]
    public async Task DisconnectedInvocationIsAnExpectedLostCircuit()
    {
        var module = new TestJsObjectReference();
        module.ThrowOnInvoke("renderMica", new JSDisconnectedException("circuit disconnected"));
        var lifetime = new JsModuleLifetime(new TestJsRuntime(module), "./test.js");
        lifetime.Module = module;

        var result = await lifetime.InvokeAsync<string>("renderMica");

        Assert.False(result.Succeeded);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(5);
        }

        Assert.True(condition());
    }
}
