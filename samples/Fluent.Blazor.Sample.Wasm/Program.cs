using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Fluent.Blazor.Theming;
using Fluent.Blazor.Overlay;
using Fluent.Blazor.Sample.Wasm.Shared;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Fluent.Blazor.Sample.Wasm.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Core Fluent.Blazor services
builder.Services.AddScoped<IThemeService, ThemeService>();
builder.Services.AddScoped<IOverlayService, OverlayService>();

// Gallery-level JS interop (Prism highlighting, clipboard)
builder.Services.AddScoped<GalleryJsInterop>();

await builder.Build().RunAsync();
