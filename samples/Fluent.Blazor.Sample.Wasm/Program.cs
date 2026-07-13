using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Fluent.Blazor.Theming;
using Fluent.Blazor.Overlay;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Fluent.Blazor.Sample.Wasm.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The two DI registrations every host of this library needs, regardless of WASM/Server/MAUI Hybrid.
builder.Services.AddScoped<IThemeService, ThemeService>();
builder.Services.AddScoped<IOverlayService, OverlayService>();

await builder.Build().RunAsync();
