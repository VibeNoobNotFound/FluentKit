using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using FluentKit.Theming;
using FluentKit.Overlay;
using FluentKit.Sample.Shared.Shared;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<FluentKit.Sample.Wasm.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Core FluentKit services
builder.Services.AddScoped<IThemeService, ThemeService>();
builder.Services.AddScoped<IAccentColorService, AccentColorService>();
builder.Services.AddScoped<IOverlayService, OverlayService>();

// Gallery-level JS interop (Prism highlighting, clipboard)
builder.Services.AddScoped<GalleryJsInterop>();

await builder.Build().RunAsync();
