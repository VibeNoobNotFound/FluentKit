# Using FluentKit in an application

FluentKit is a .NET 10 Razor Class Library for Blazor WebAssembly, Blazor Server, and
MAUI Blazor Hybrid. Install the `FluentKit.Blazor` package and keep the package version
aligned with the matching FluentKit release documentation.

The generated [API reference](../reference/api.md) describes the actual public assembly
surface. The [live sample](https://vibenoobnotfound.github.io/FluentKit/) is the runnable
visual reference; its source is in `samples/FluentKit.Sample.Shared`.

## Required setup

1. Add the package:

   ```bash
   dotnet add package FluentKit.Blazor --version 0.2.4
   ```

2. Link the public token and icon stylesheets. The static web asset base is always
   `_content/FluentKit`, even though the NuGet package id is `FluentKit.Blazor`:

   ```html
   <link rel="stylesheet" href="_content/FluentKit/Tokens/tokens.css" />
   <link rel="stylesheet" href="_content/FluentKit/Icons/FluentSystemIcons-Regular.css" />
   ```

3. Register the core services in `Program.cs`:

   ```csharp
   builder.Services.AddScoped<IThemeService, ThemeService>();
   builder.Services.AddScoped<IAccentColorService, AccentColorService>();
   builder.Services.AddScoped<IOverlayService, OverlayService>();
   ```

4. Put `ThemeProvider` around the application and render exactly one
   `FluentOverlayHost` near the root:

   ```razor
   <ThemeProvider>
       @Body
       <FluentOverlayHost />
   </ThemeProvider>
   ```

`FluentOverlayHost` is required by tooltips, flyouts, menus, dialogs, and teaching tips.

## Hosting notes

- **WebAssembly:** put the stylesheet links in `wwwroot/index.html`.
- **Server:** put them in the host document or the app's root layout.
- **MAUI Hybrid:** put them in the `BlazorWebView` host page (`wwwroot/index.html`).

For a server-side redirect/disconnect disposal verification using the Aula consumer, see
[server-circuit-disposal.md](server-circuit-disposal.md).

The component API is the same across hosts. Only the host page and static-asset serving
configuration differ.

## Choosing a control

Start with the closest primitive, then use a composite control when it owns behavior such
as an overlay, picker, navigation model, or selection state. Prefer FluentKit tokens and
component parameters over app-local CSS values. Every component currently demonstrated by
the sample app is listed in [component-selection.md](component-selection.md).

## Versioning

The API skill and generated API reference are released with the same `v{version}` tag
as the NuGet package. For an existing app, inspect its `PackageReference` or lock file and
use the matching release bundle; do not use the latest skill against an older package.
