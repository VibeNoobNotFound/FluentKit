# Overlays

Register `IOverlayService` and render one `FluentOverlayHost` inside the root
`ThemeProvider`. The host is the portal target for `FluentTooltip`, `FluentFlyout`, menus,
`FluentComboBox`, `FluentAutoSuggestBox`, `FluentContentDialog`, and `FluentTeachingTip`.

ComboBox and AutoSuggestBox dropdowns are portalled so they can escape native labels, clipping
ancestors, and backdrop-filter boundaries. They use the shared `OverlaySurface` with
`OverlayContentLayout.EdgeToEdge`; regular flyouts keep the default padded surface. Applications
that create overlays directly can pass `OverlaySurfaceOptions` to select the content layout or
entrance origin, and `OverlayPositioningOptions` to align an anchored surface at the anchor start
with a main-axis offset. `OverlayAnimationOptions` can override entrance or exit duration and
easing; entrances default to WinUI's fast-out, slow-in motion, so they begin at maximum velocity
and gradually decelerate to rest.

Keep an overlay host mounted for the lifetime of the application. Rendering a second host
or placing it inside a transient page can make an overlay disappear when navigation occurs.
The component's `sample` field in the package-local `api.json` identifies the corresponding
visual sample route.
