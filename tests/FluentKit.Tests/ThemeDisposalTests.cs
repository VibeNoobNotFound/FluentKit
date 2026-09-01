using Bunit;
using FluentKit.Theming;
using Microsoft.JSInterop;
using Microsoft.Extensions.DependencyInjection;

namespace FluentKit.Tests;

public sealed class ThemeDisposalTests : Bunit.BunitContext
{
    [Fact]
    public async Task ThemeProviderUnsubscribesBeforeHandlingLaterThemeChanges()
    {
        var theme = new TestThemeService();
        var module = new TestJsObjectReference();
        module.Results["watchSystemPreference"] = "dark";
        Services.AddSingleton<IThemeService>(theme);
        Services.AddSingleton<IJSRuntime>(new TestJsRuntime(module));

        var cut = Render<ThemeProvider>();
        Assert.Equal(1, theme.SubscriberCount);

        await cut.Instance.DisposeAsync();
        var applyCount = module.Calls.Count(call => call == "applyResolvedTheme");
        theme.RaiseChanged();
        await Task.Yield();

        Assert.Equal(0, theme.SubscriberCount);
        Assert.Equal(applyCount, module.Calls.Count(call => call == "applyResolvedTheme"));
    }

    [Fact]
    public async Task MicaPanelUnsubscribesBeforeHandlingLaterThemeChanges()
    {
        var theme = new TestThemeService();
        var module = new TestJsObjectReference();
        Services.AddSingleton<IThemeService>(theme);
        Services.AddSingleton<IJSRuntime>(new TestJsRuntime(module));

        var cut = Render<FluentKit.Effects.FluentMicaPanel>();
        Assert.Equal(1, theme.SubscriberCount);

        await cut.Instance.DisposeAsync();
        theme.RaiseChanged();
        await Task.Yield();

        Assert.Equal(0, theme.SubscriberCount);
    }

    [Fact]
    public void MicaPanelAddsTheInitialInteractiveRasterToTheMarkup()
    {
        var theme = new TestThemeService();
        var module = new TestJsObjectReference();
        Services.AddSingleton<IThemeService>(theme);
        Services.AddSingleton<IJSRuntime>(new TestJsRuntime(module));

        var cut = Render<FluentKit.Effects.FluentMicaPanel>(parameters => parameters
            .Add(panel => panel.BackgroundImageUrl, "wallpaper.png"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("fluent-mica__wallpaper", cut.Markup);
            Assert.Contains("renderMicaInto", module.Calls);
        });
    }

    [Fact]
    public async Task MicaThemeRenderCancellationDoesNotCompleteTheRenderKey()
    {
        var theme = new TestThemeService();
        var module = new TestJsObjectReference
        {
            BlockInvocationsUntilCanceled = true,
            ThrowTaskCanceledExceptionWhenCanceled = true
        };
        Services.AddSingleton<IThemeService>(theme);
        Services.AddSingleton<IJSRuntime>(new TestJsRuntime(module));

        var cut = Render<FluentKit.Effects.FluentMicaPanel>();
        cut.Instance.BackgroundImageUrl = "wallpaper.png";
        theme.RaiseChanged();

        await WaitUntilAsync(() => module.Calls.Contains("renderMicaInto"));
        await cut.Instance.DisposeAsync();

        var lastKey = typeof(FluentKit.Effects.FluentMicaPanel)
            .GetField("_lastKey", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(cut.Instance);

        Assert.Null(lastKey);
        Assert.Equal(1, module.DisposeCount);
    }

    [Fact]
    public async Task MicaThemeRenderDisconnectCanRetryTheSameKey()
    {
        var theme = new TestThemeService();
        var module = new TestJsObjectReference();
        module.ThrowOnInvoke("renderMicaInto", new JSDisconnectedException("circuit disconnected"));
        Services.AddSingleton<IThemeService>(theme);
        Services.AddSingleton<IJSRuntime>(new TestJsRuntime(module));

        var cut = Render<FluentKit.Effects.FluentMicaPanel>();
        cut.Instance.BackgroundImageUrl = "retry-wallpaper.png";
        theme.RaiseChanged();
        await WaitUntilAsync(() => module.Calls.Count(call => call == "renderMicaInto") == 1);

        var lastKey = typeof(FluentKit.Effects.FluentMicaPanel)
            .GetField("_lastKey", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Assert.Null(lastKey.GetValue(cut.Instance));

        module.ClearInvokeException("renderMicaInto");
        theme.RaiseChanged();
        await WaitUntilAsync(() => module.Calls.Count(call => call == "renderMicaInto") == 2);

        Assert.Equal("retry-wallpaper.png|True|dark", lastKey.GetValue(cut.Instance));
        await cut.Instance.DisposeAsync();
    }

    private sealed class TestThemeService : IThemeService
    {
        private Action? _themeChanged;

        public ThemeMode Mode { get; private set; } = ThemeMode.System;

        public string ResolvedTheme => Mode == ThemeMode.Light ? "light" : "dark";

        public event Action? ThemeChanged
        {
            add => _themeChanged += value;
            remove => _themeChanged -= value;
        }

        public int SubscriberCount => _themeChanged?.GetInvocationList().Length ?? 0;

        public Task SetModeAsync(ThemeMode mode)
        {
            Mode = mode;
            _themeChanged?.Invoke();
            return Task.CompletedTask;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public void RaiseChanged() => _themeChanged?.Invoke();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(5);
        }

        Assert.True(condition());
    }
}
