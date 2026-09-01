using FluentKit.Overlay;
using FluentKit.Sample.Server.Components;
using FluentKit.Sample.Server.Services;
using FluentKit.Sample.Shared;
using FluentKit.Sample.Shared.Services;
using FluentKit.Sample.Shared.Shared;
using FluentKit.Theming;

var builder = WebApplication.CreateBuilder(args);
// Ensure Rider/debug launches load this project's RCL static-web-asset manifest as well as
// published deployments, rather than returning empty matches for _content/* files.
builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddScoped<IThemeService, ThemeService>();
builder.Services.AddScoped<IAccentColorService, AccentColorService>();
builder.Services.AddScoped<IOverlayService, OverlayService>();
builder.Services.AddScoped<GalleryJsInterop>();
builder.Services.AddScoped<IRazorPlaygroundCompiler, UnavailableRazorPlaygroundCompiler>();

var app = builder.Build();
app.UseAntiforgery();
// MapStaticAssets supplies fingerprinted production endpoints. StaticFiles also serves the
// development static-web-asset file provider that Rider uses for project and RCL assets.
app.UseStaticFiles();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(MainLayout).Assembly)
    .AddInteractiveServerRenderMode();
app.Run();
