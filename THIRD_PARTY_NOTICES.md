# Third-party notices

## fluent-svelte
Design token structure and visual-state conventions (light/dark alias naming, Acrylic approach)
were informed by [fluent-svelte](https://github.com/tropix126/fluent-svelte) (MIT License,
© tropix126 / Fluent Svelte contributors). No source code was copied verbatim; token *values*
below were independently re-derived from Microsoft's own `microsoft-ui-xaml` repository.
Component markup/CSS *structure* (not literal source) for Expander (clip+slide collapse
technique), ProgressBar (indeterminate keyframe timing), and InfoBar (icon/title/message/action/
close-button layout, default-icon-is-an-InfoBadge simplification) was ported from the equivalent
fluent-svelte components and adapted to Razor + CSS isolation and this project's own token names.
Slider (thumb offset/clamp formula, rail click-to-jump + drag behavior, tick placement, orientation
/reverse handling) was likewise ported from fluent-svelte's `Slider.svelte`, reimplemented as a
Razor component with a small JS interop module (`wwwroot/Primitives/Slider/slider-interop.js`) for
pointer/touch tracking instead of Svelte's reactive bindings.

## microsoft-ui-xaml
Design token values (colors, corner radius, component state aliases) are transcribed from
[microsoft/microsoft-ui-xaml](https://github.com/microsoft/microsoft-ui-xaml)
(`controls/dev/CommonStyles/*.xaml`), MIT License, © Microsoft Corporation.

## Fluent System Icons
`Primitives/Icon/FluentIcon` renders glyphs from the MIT-licensed
[Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons) webfont,
© Microsoft Corporation, shipped directly under `wwwroot/Icons/` (`FluentSystemIcons-Regular`
.css/.woff2/.woff). Unlike the Segoe Fluent Icons font this replaced originally, this font's MIT
license explicitly permits redistribution, so bundling it as a static web asset is fine.

This also replaced a brief intermediate approach using the `Microsoft.FluentUI.AspNetCore.Components.Icons`
NuGet package (SVG-based): that package transitively depends on the full
`Microsoft.FluentUI.AspNetCore.Components` core library, which auto-bundles its own scoped CSS into
any consuming app's `.styles.css` via Blazor's static web asset pipeline — with no way to reference
only the icon data without pulling that CSS along. The webfont has no managed dependency at all, so
it can't leak component styling.
