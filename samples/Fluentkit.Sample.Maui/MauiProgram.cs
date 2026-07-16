using Microsoft.Extensions.Logging;
using FluentKit.Theming;
using FluentKit.Overlay;
using FluentKit.Sample.Shared.Shared;
using FluentKit.Sample.Shared.Services;
using Fluentkit.Sample.Maui.Services;

namespace Fluentkit.Sample.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => { fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); });

        builder.Services.AddMauiBlazorWebView();

        // Core FluentKit services (same registrations as the Wasm sample's Program.cs)
        builder.Services.AddScoped<IThemeService, ThemeService>();
        builder.Services.AddScoped<IAccentColorService, AccentColorService>();
        builder.Services.AddScoped<IOverlayService, OverlayService>();

        // Gallery-level JS interop (Prism highlighting, clipboard, localStorage helpers)
        builder.Services.AddScoped<GalleryJsInterop>();

        // a Wasm implementation (which fetches its own assemblies over HTTP from "_framework/",
        // an approach specific to Blazor WebAssembly), so PlaygroundPage's
        // @inject IRazorPlaygroundCompiler Compiler had nothing to resolve and threw as soon as
        // the page loaded. See Services/RazorPlaygroundCompiler.cs for the on-disk-assembly
        // implementation used here instead.
        builder.Services.AddScoped<IRazorPlaygroundCompiler, RazorPlaygroundCompiler>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}