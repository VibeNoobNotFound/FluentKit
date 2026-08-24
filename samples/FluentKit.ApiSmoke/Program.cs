using FluentKit.Overlay;
using FluentKit.Theming;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<FluentKit.ApiSmoke.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped<IThemeService, ThemeService>();
builder.Services.AddScoped<IAccentColorService, AccentColorService>();
builder.Services.AddScoped<IOverlayService, OverlayService>();
await builder.Build().RunAsync();
