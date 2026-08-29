# FluentKit setup

Install the `FluentKit.Blazor` package at the version selected by the consumer application.
Link these public assets in the host page:

```html
<link rel="stylesheet" href="_content/FluentKit/Tokens/tokens.css" />
<link rel="stylesheet" href="_content/FluentKit/Icons/FluentSystemIcons-Regular.css" />
```

Register the core services:

```csharp
builder.Services.AddScoped<IThemeService, ThemeService>();
builder.Services.AddScoped<IAccentColorService, AccentColorService>();
builder.Services.AddScoped<IOverlayService, OverlayService>();
```

At the application root, render one long-lived overlay host inside the theme provider:

```razor
<ThemeProvider>
    @Body
    <FluentOverlayHost />
</ThemeProvider>
```
