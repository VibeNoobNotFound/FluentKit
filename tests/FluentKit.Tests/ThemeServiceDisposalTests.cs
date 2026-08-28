using FluentKit.Theming;
using Microsoft.JSInterop;

namespace FluentKit.Tests;

public sealed class ThemeServiceDisposalTests
{
    [Fact]
    public async Task ServiceUnwatchesBeforeDisposingModuleAndDotNetReference()
    {
        var module = new TestJsObjectReference();
        module.Results["watchSystemPreference"] = "dark";
        var service = new ThemeService(new TestJsRuntime(module));

        await service.InitializeAsync();
        var reference = module.Invocations
            .Last(invocation => invocation.Identifier == "watchSystemPreference")
            .Arguments![0] as DotNetObjectReference<ThemeService>;
        Assert.NotNull(reference);

        await service.DisposeAsync();

        Assert.True(module.Calls.IndexOf("unwatchSystemPreference") < module.Calls.IndexOf("dispose"));
        Assert.Equal(1, module.DisposeCount);
        Assert.Throws<ObjectDisposedException>(() => _ = reference!.Value);
    }

    [Fact]
    public async Task ServiceStillDisposesManagedReferenceWhenBrowserCleanupFails()
    {
        var module = new TestJsObjectReference();
        module.Results["watchSystemPreference"] = "dark";
        var expected = new InvalidOperationException("cleanup failure");
        module.ThrowOnInvoke("unwatchSystemPreference", expected);
        var service = new ThemeService(new TestJsRuntime(module));

        await service.InitializeAsync();
        var reference = module.Invocations
            .Last(invocation => invocation.Identifier == "watchSystemPreference")
            .Arguments![0] as Microsoft.JSInterop.DotNetObjectReference<ThemeService>;

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DisposeAsync().AsTask());

        Assert.Same(expected, actual);
        Assert.Equal(1, module.DisposeCount);
        Assert.Throws<ObjectDisposedException>(() => _ = reference!.Value);
    }

    [Fact]
    public async Task InitializationDisconnectDoesNotEscape()
    {
        var module = new TestJsObjectReference();
        module.Results["watchSystemPreference"] = "dark";
        module.ThrowOnInvoke("watchSystemPreference", new Microsoft.JSInterop.JSDisconnectedException("circuit disconnected"));
        var service = new ThemeService(new TestJsRuntime(module));

        await service.InitializeAsync();
        await service.DisposeAsync();

        Assert.Equal(1, module.DisposeCount);
    }

    [Fact]
    public async Task ServiceIgnoresDisconnectedUnwatch()
    {
        var module = new TestJsObjectReference();
        module.Results["watchSystemPreference"] = "dark";
        module.ThrowOnInvoke("unwatchSystemPreference", new Microsoft.JSInterop.JSDisconnectedException("circuit disconnected"));
        module.DisposeException = new Microsoft.JSInterop.JSDisconnectedException("circuit disconnected");
        var service = new ThemeService(new TestJsRuntime(module));

        await service.InitializeAsync();
        await service.DisposeAsync();

        Assert.Equal(1, module.DisposeCount);
    }
}
