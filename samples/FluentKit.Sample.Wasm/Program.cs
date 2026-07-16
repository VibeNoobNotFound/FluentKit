using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using FluentKit.Theming;
using FluentKit.Overlay;
using FluentKit.Sample.Shared.Shared;
using FluentKit.Sample.Shared.Services;
using FluentKit.Sample.Wasm.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<FluentKit.Sample.Wasm.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Core FluentKit services
builder.Services.AddScoped<IThemeService, ThemeService>();
builder.Services.AddScoped<IAccentColorService, AccentColorService>();
builder.Services.AddScoped<IOverlayService, OverlayService>();

// Gallery-level JS interop (Prism highlighting, clipboard)
builder.Services.AddScoped<GalleryJsInterop>();

// Live Playground: fetches its own assemblies back from _framework/ over HTTP, so it needs
// an HttpClient rooted at the app's own origin.
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<IRazorPlaygroundCompiler, RazorPlaygroundCompiler>();

await builder.Build().RunAsync();
