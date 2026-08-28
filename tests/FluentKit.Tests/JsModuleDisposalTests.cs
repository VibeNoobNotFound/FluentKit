using FluentKit.Composite;
using FluentKit.Effects;
using FluentKit.Interop;
using FluentKit.Overlay;
using FluentKit.Primitives;
using FluentKit.Theming;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FluentKit.Tests;

public sealed class JsModuleDisposalTests
{
    [Fact]
    public async Task DisconnectedModuleDisposalIsIgnored()
    {
        var module = new TestJsObjectReference { DisposeException = new JSDisconnectedException("circuit disconnected") };

        await JsModuleDisposal.DisposeAsync(module);

        Assert.Equal(1, module.DisposeCount);
    }

    [Fact]
    public async Task NonDisconnectedModuleDisposalPropagates()
    {
        var expected = new InvalidOperationException("module failure");
        var module = new TestJsObjectReference { DisposeException = expected };

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => JsModuleDisposal.DisposeAsync(module).AsTask());

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task EveryComponentModuleOwnerIgnoresDisconnectedDisposal()
    {
        foreach (var owner in CreateComponentOwners())
        {
            var module = new TestJsObjectReference { DisposeException = new JSDisconnectedException("circuit disconnected") };
            SetModule(owner, module);

            await owner.DisposeAsync();
            await owner.DisposeAsync();

            Assert.Equal(1, module.DisposeCount);
        }
    }

    [Fact]
    public async Task EveryComponentModuleOwnerIgnoresTeardownCancellationOnDisposal()
    {
        foreach (var owner in CreateComponentOwners())
        {
            var module = new TestJsObjectReference
            {
                DisposeException = new TaskCanceledException("circuit teardown")
            };
            SetModule(owner, module);

            await owner.DisposeAsync();

            Assert.Equal(1, module.DisposeCount);
        }
    }

    [Fact]
    public async Task BrowserCleanupIsAttemptedBeforeModuleDisposal()
    {
        var module = new TestJsObjectReference();
        var reveal = new FluentRevealBackground();
        SetModule(reveal, module);

        await reveal.DisposeAsync();

        Assert.Equal("stopTracking", module.Calls[0]);
        Assert.Equal("dispose", module.Calls[1]);
    }

    [Fact]
    public async Task OwnedBrowserResourcesAreUnregisteredBeforeModuleDisposal()
    {
        var overlay = new OverlaySurface
        {
            Entry = new OverlayEntry { Content = _ => { } }
        };
        var overlayModule = new TestJsObjectReference();
        SetModule(overlay, overlayModule);
        SetPrivateField(overlay, "_selfReference", DotNetObjectReference.Create(overlay));
        await overlay.DisposeAsync();
        AssertBeforeDispose(overlayModule, "unregisterLightDismiss");

        var autoSuggest = new FluentAutoSuggestBox<string>();
        var autoSuggestModule = new TestJsObjectReference();
        SetModule(autoSuggest, autoSuggestModule);
        SetPrivateField(autoSuggest, "_heightObserved", true);
        await autoSuggest.DisposeAsync();
        AssertBeforeDispose(autoSuggestModule, "unobserveAutoHeight");

        var timePicker = new FluentTimePicker();
        var timePickerModule = new TestJsObjectReference();
        SetModule(timePicker, timePickerModule);
        SetPrivateField(timePicker, "_listenersAttached", true);
        await timePicker.DisposeAsync();
        AssertBeforeDispose(timePickerModule, "detachColumn");

        var slider = new FluentSlider();
        var sliderModule = new TestJsObjectReference();
        SetModule(slider, sliderModule);
        await slider.DisposeAsync();
        AssertBeforeDispose(sliderModule, "stopDrag");

        var navigationView = new FluentNavigationView();
        var navigationModule = new TestJsObjectReference();
        SetModule(navigationView, navigationModule);
        await navigationView.DisposeAsync();
        AssertBeforeDispose(navigationModule, "stopObservingResize");
    }

    [Fact]
    public async Task NonDisconnectedBrowserCleanupFailurePropagatesAndModuleIsStillDisposed()
    {
        var expected = new InvalidOperationException("cleanup failure");
        var module = new TestJsObjectReference();
        module.ThrowOnInvoke("stopTracking", expected);
        var reveal = new FluentRevealBackground();
        SetModule(reveal, module);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => reveal.DisposeAsync().AsTask());

        Assert.Same(expected, actual);
        Assert.Equal(1, module.DisposeCount);
    }

    [Fact]
    public async Task NavigationViewUnsubscribesContextHandlers()
    {
        var navigationView = new FluentNavigationView();
        var selectionChanged = 0;
        navigationView.SelectedValueChanged = EventCallback.Factory.Create<object?>(
            navigationView, _ => selectionChanged++);

        typeof(FluentNavigationView)
            .GetMethod("OnInitialized", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(navigationView, null);
        var context = (NavigationViewContext)typeof(FluentNavigationView)
            .GetField("_context", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(navigationView)!;

        await navigationView.DisposeAsync();
        context.SelectValue("after-dispose");

        Assert.Equal(0, selectionChanged);
    }

    [Fact]
    public async Task ComponentDotNetReferencesAreDisposed()
    {
        var overlay = new OverlaySurface
        {
            Entry = new OverlayEntry { Content = _ => { } }
        };
        var overlayReference = DotNetObjectReference.Create(overlay);
        var overlayModule = new TestJsObjectReference();
        SetPrivateField(overlay, "_selfReference", overlayReference);
        SetModule(overlay, overlayModule);
        await overlay.DisposeAsync();
        AssertDisposed(overlayReference);

        var slider = new FluentSlider();
        var sliderReference = DotNetObjectReference.Create(slider);
        var sliderModule = new TestJsObjectReference();
        SetPrivateField(slider, "_selfReference", sliderReference);
        SetModule(slider, sliderModule);
        await slider.DisposeAsync();
        AssertDisposed(sliderReference);

        var timePicker = new FluentTimePicker();
        var timeReference = DotNetObjectReference.Create(timePicker);
        var timeModule = new TestJsObjectReference();
        SetPrivateField(timePicker, "_selfReference", timeReference);
        SetModule(timePicker, timeModule);
        await timePicker.DisposeAsync();
        AssertDisposed(timeReference);

        var navigationView = new FluentNavigationView();
        var navigationReference = DotNetObjectReference.Create(navigationView);
        var navigationModule = new TestJsObjectReference();
        SetPrivateField(navigationView, "_selfReference", navigationReference);
        SetModule(navigationView, navigationModule);
        await navigationView.DisposeAsync();
        AssertDisposed(navigationReference);
    }

    private static void AssertDisposed<T>(DotNetObjectReference<T> reference) where T : class
    {
        Assert.Throws<ObjectDisposedException>(() => _ = reference.Value);
    }

    private static IEnumerable<IAsyncDisposable> CreateComponentOwners()
    {
        yield return new ThemeProvider();
        yield return new ThemeService(new TestJsRuntime(new TestJsObjectReference()));
        yield return new AccentColorService(new TestJsRuntime(new TestJsObjectReference()));
        yield return new FluentMicaPanel();
        yield return new FluentRevealBackground();
        yield return new FluentAutoSuggestBox<string>();
        yield return new FluentPivot();
        yield return new FluentTimePicker();
        yield return new FluentSlider();
        yield return new FluentNavigationView();

        var overlay = new OverlaySurface
        {
            Entry = new OverlayEntry { Content = _ => { } }
        };
        yield return overlay;
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        var field = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private static void SetModule(object instance, TestJsObjectReference module)
    {
        var field = instance.GetType().GetField("_interop", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);

        var lifetime = field!.GetValue(instance) as JsModuleLifetime;
        if (lifetime is null)
        {
            lifetime = new JsModuleLifetime(new TestJsRuntime(module), "./test-module.js");
            lifetime.Module = module;
            field.SetValue(instance, lifetime);
        }
        else
        {
            lifetime.Module = module;
        }
        Assert.True(ReferenceEquals(module, lifetime.Module), instance.GetType().FullName);
    }

    private static void AssertBeforeDispose(TestJsObjectReference module, string cleanupIdentifier)
    {
        var cleanupIndex = module.Calls.IndexOf(cleanupIdentifier);
        var disposeIndex = module.Calls.IndexOf("dispose");
        Assert.True(cleanupIndex >= 0);
        Assert.True(cleanupIndex < disposeIndex);
    }
}
