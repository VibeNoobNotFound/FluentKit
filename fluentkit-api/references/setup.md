# FluentKit setup

Install `FluentKit.Blazor` at the version declared by `metadata.json`. Link these files in
the host page:

```html
<link rel="stylesheet" href="_content/FluentKit/Tokens/tokens.css" />
<link rel="stylesheet" href="_content/FluentKit/Icons/FluentSystemIcons-Regular.css" />
```

Register:

```csharp
builder.Services.AddScoped<IThemeService, ThemeService>();
builder.Services.AddScoped<IAccentColorService, AccentColorService>();
builder.Services.AddScoped<IOverlayService, OverlayService>();
```

At the root:

```razor
<ThemeProvider>
    @Body
    <FluentOverlayHost />
</ThemeProvider>
```
