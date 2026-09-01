using Bunit;
using FluentKit.Composite;
using FluentKit.Overlay;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace FluentKit.Tests;

public sealed class PortalledDropdownTests
{
    [Fact]
    public void OverlayAnimationOptionsUseDeceleratingEntrancesAndAllowCustomTiming()
    {
        using var context = CreateContext();
        var host = context.Render<FluentOverlayHost>();
        var overlays = context.Services.GetRequiredService<IOverlayService>();
        var surface = new OverlaySurfaceOptions
        {
            Animation = new OverlayAnimationOptions
            {
                EntranceDuration = TimeSpan.FromMilliseconds(320),
                EntranceEasing = OverlayAnimationEasing.Standard,
                ExitDuration = TimeSpan.FromMilliseconds(180),
                ExitEasing = OverlayAnimationEasing.Linear
            }
        };

        Assert.Equal(OverlayAnimationEasing.Decelerate, new OverlaySurfaceOptions().Animation.EntranceEasing);

        overlays.Show(builder => builder.AddContent(0, "animated"), default, new OverlayPositioningOptions(),
            surface, OverlayPlacement.Bottom, lightDismiss: false, bare: false, matchAnchorWidth: false,
            scrollAnchorIntoView: false, watchAnchorRemoved: false);

        host.WaitForAssertion(() => Assert.Contains("animated", host.Markup));
        var overlay = host.Find(".fluent-overlay-surface");
        Assert.Contains("fluent-overlay-surface--entrance-easing-standard", overlay.GetAttribute("class"));
        Assert.Contains("fluent-overlay-surface--exit-easing-linear", overlay.GetAttribute("class"));
        Assert.Contains("--fluent-overlay-entrance-duration: 320ms", overlay.GetAttribute("style"));
        Assert.Contains("--fluent-overlay-exit-duration: 180ms", overlay.GetAttribute("style"));
    }

    [Fact]
    public void AnchoredOverlaysWaitForPositioning_WhileDetachedOverlaysEnterImmediately()
    {
        using var context = CreateContext();
        var host = context.Render<FluentOverlayHost>();
        var overlays = context.Services.GetRequiredService<IOverlayService>();

        overlays.Show(builder => builder.AddContent(0, "anchored"), default, OverlayPlacement.Bottom,
            lightDismiss: false);
        host.WaitForAssertion(() => Assert.Contains("anchored", host.Markup));
        Assert.DoesNotContain("fluent-overlay-surface--positioned", host.Markup);

        overlays.ShowDetached(builder => builder.AddContent(0, "detached"), OverlayScreenPlacement.Center,
            lightDismiss: false);
        host.WaitForAssertion(() => Assert.Contains("detached", host.Markup));
        var surfaces = host.FindAll(".fluent-overlay-surface");
        Assert.Equal(2, surfaces.Count);
        Assert.DoesNotContain("fluent-overlay-surface--positioned", surfaces[0].GetAttribute("class"));
        Assert.Contains("fluent-overlay-surface--positioned", surfaces[1].GetAttribute("class"));
    }

    [Theory]
    [InlineData(1, OverlayEntranceOrigin.Top)]
    [InlineData(3, OverlayEntranceOrigin.Center)]
    [InlineData(5, OverlayEntranceOrigin.Bottom)]
    public void ComboBoxUsesSelectedRowRevealOrigin(int value, OverlayEntranceOrigin expectedOrigin)
    {
        using var context = CreateContext();
        var host = context.Render<FluentOverlayHost>();
        var cut = context.Render<FluentComboBox<int>>(parameters => parameters
            .Add(combo => combo.Items, Enumerable.Range(1, 5)
                .Select(number => new ComboBoxItem<int>($"Item {number}", number))
                .ToArray())
            .Add(combo => combo.Value, value));

        cut.Find("button").Click();
        host.WaitForAssertion(() => Assert.Single(host.FindAll("ul.fluent-combo-box-dropdown-list")));

        Assert.Equal(expectedOrigin, context.Services.GetRequiredService<IOverlayService>()
            .Active.Single().SurfaceOptions.EntranceOrigin);
    }

    [Fact]
    public void EditableComboBoxUsesTopRevealOrigin()
    {
        using var context = CreateContext();
        var host = context.Render<FluentOverlayHost>();
        var cut = context.Render<FluentComboBox<int>>(parameters => parameters
            .Add(combo => combo.Items, ComboItems)
            .Add(combo => combo.Editable, true));

        cut.Find("input").Focus();
        host.WaitForAssertion(() => Assert.Single(host.FindAll("ul.fluent-combo-box-dropdown-list")));

        Assert.Equal(OverlayEntranceOrigin.Top, context.Services.GetRequiredService<IOverlayService>()
            .Active.Single().SurfaceOptions.EntranceOrigin);
    }

    [Fact]
    public void ComboBox_PortalsListboxOutsideNativeLabel_AndClosesBeforeCallbackReturns()
    {
        using var context = CreateContext();
        var host = context.Render<FluentOverlayHost>();
        var selectionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var selectionMayFinish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var selectionCount = 0;
        var selectedValue = 0;

        var label = context.Render(builder =>
        {
            builder.OpenElement(0, "label");
            builder.OpenComponent<FluentComboBox<int>>(1);
            builder.AddAttribute(2, nameof(FluentComboBox<int>.Items), ComboItems);
            builder.AddAttribute(3, nameof(FluentComboBox<int>.ValueChanged),
                EventCallback.Factory.Create<int>(this, async value =>
                {
                    selectedValue = value;
                    selectionCount++;
                    selectionStarted.TrySetResult();
                    await selectionMayFinish.Task;
                }));
            builder.CloseComponent();
            builder.CloseElement();
        });

        label.Find("button").Click();
        host.WaitForAssertion(() => Assert.Single(host.FindAll("ul.fluent-combo-box-dropdown-list")));
        Assert.Empty(label.FindAll("ul.fluent-combo-box-dropdown-list"));
        Assert.Equal(OverlayContentLayout.EdgeToEdge,
            GetOverlays(context).Active.Single().SurfaceOptions.ContentLayout);
        Assert.Equal(OverlayEntranceOrigin.Center,
            GetOverlays(context).Active.Single().SurfaceOptions.EntranceOrigin);

        host.FindAll("li.fluent-combo-box-item")[1].Click();

        Assert.True(selectionStarted.Task.IsCompleted);
        Assert.Equal(1, selectionCount);
        Assert.Equal(2, selectedValue);
        Assert.Equal("false", label.Find("button").GetAttribute("aria-expanded"), ignoreCase: true);
        Assert.Empty(label.FindAll("ul.fluent-combo-box-dropdown-list"));

        selectionMayFinish.TrySetResult();
    }

    [Fact]
    public void AutoSuggestBox_PortalsListbox_AndSynchronizesLightDismissState()
    {
        using var context = CreateContext();
        var overlays = context.Services.GetRequiredService<IOverlayService>();
        var host = context.Render<FluentOverlayHost>();
        var label = context.Render(builder =>
        {
            builder.OpenElement(0, "label");
            builder.OpenComponent<FluentAutoSuggestBox<int>>(1);
            builder.AddAttribute(2, nameof(FluentAutoSuggestBox<int>.Text), "app");
            builder.AddAttribute(3, nameof(FluentAutoSuggestBox<int>.Items), SuggestionItems);
            builder.CloseComponent();
            builder.CloseElement();
        });

        label.Find(".fluent-autosuggest-root").TriggerEvent("onfocusin", new FocusEventArgs());

        host.WaitForAssertion(() => Assert.Single(host.FindAll("ul.fluent-autosuggest-dropdown-list")));
        Assert.Empty(label.FindAll("ul.fluent-autosuggest-dropdown-list"));
        Assert.Equal("true", label.Find("input").GetAttribute("aria-expanded"), ignoreCase: true);
        Assert.Equal(OverlayContentLayout.EdgeToEdge, overlays.Active.Single().SurfaceOptions.ContentLayout);

        overlays.Close(overlays.Active.Single().Id);

        label.WaitForAssertion(() =>
            Assert.Equal("false", label.Find("input").GetAttribute("aria-expanded"), ignoreCase: true));
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddScoped<IOverlayService, OverlayService>();
        return context;
    }

    private static IOverlayService GetOverlays(BunitContext context)
        => context.Services.GetRequiredService<IOverlayService>();

    private static readonly IReadOnlyList<ComboBoxItem<int>> ComboItems =
    [
        new("One", 1),
        new("Two", 2)
    ];

    private static readonly IReadOnlyList<AutoSuggestBoxItem<int>> SuggestionItems =
    [
        new("Apple", 1),
        new("Pineapple", 2)
    ];
}
