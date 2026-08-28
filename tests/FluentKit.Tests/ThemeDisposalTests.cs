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
}
