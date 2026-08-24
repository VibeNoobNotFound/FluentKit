# Icons and static assets

Link the icon font from the fixed RCL route:

```html
<link rel="stylesheet" href="_content/FluentKit/Icons/FluentSystemIcons-Regular.css" />
```

Use `FluentIcon` and the generated `FluentIconNames` constants when a compile-time checked
glyph name is useful. Do not copy the icon font into the host app.

All library assets use `_content/FluentKit/...`; this route is intentionally independent of
the `FluentKit.Blazor` package id.
