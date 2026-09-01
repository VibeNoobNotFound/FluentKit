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
