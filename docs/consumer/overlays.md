# Overlays

Register `IOverlayService` and render one `FluentOverlayHost` inside the root
`ThemeProvider`. The host is the portal target for `FluentTooltip`, `FluentFlyout`, menus,
`FluentContentDialog`, and `FluentTeachingTip`.

Keep an overlay host mounted for the lifetime of the application. Rendering a second host
or placing it inside a transient page can make an overlay disappear when navigation occurs.
The [overlay sample pages](../../samples/FluentKit.Sample.Shared/Pages/Composite) show the
supported trigger, placement, close, focus, and keyboard patterns.
