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
MenuFlyout/MenuFlyoutItem/MenuFlyoutDivider and ContextMenu port fluent-svelte's `MenuFlyout*` and
`ContextMenu` components (item variants — standard/radio/toggle/cascading — bullet/checkmark glyph
conventions, hover-intent delay for cascading submenus, close-on-select semantics). Positioning was
reimplemented rather than ported: both reuse this project's own IOverlayService/OverlaySurface
infra (the same one FluentFlyout and FluentTooltip are built on) instead of fluent-svelte's bespoke
mousePosition/menuPosition clamp math — ContextMenu in particular anchors to an invisible marker
moved to the cursor position rather than reimplementing its own collision-avoidance.
ComboBox/ComboBoxItem (button vs. editable/searchable text-box trigger modes, starts-with search
matching, keyboard navigation, selected-row menu-grow-direction/offset choreography) port
fluent-svelte's `ComboBox.svelte`/`ComboBoxItem.svelte`. Unlike MenuFlyout/ContextMenu above, this
deliberately does NOT go through IOverlayService — the dropdown needs to be exactly the trigger's
width and grow from a specific list row, so it stays a plain absolutely-positioned child of the
component's own root, same as fluent-svelte's own `.combo-box-dropdown { position: absolute }`.
ContentDialog (Title/body/optional Footer, size presets min/standard/max, Escape + scrim-click
dismissal, half-width end-aligned single-button footer convention) ports fluent-svelte's
`ContentDialog.svelte`. Also self-contained rather than IOverlayService-based, for the same reason
as ComboBox above — a modal dialog is always viewport-centered with its own full-screen scrim,
regardless of what triggered it, which doesn't fit the anchor-relative overlay model at all.
TextBox gained a `Buttons` slot (right-aligned button row, mirrors fluent-svelte's TextBox
`slot="buttons"`) plus a new `TextBoxButton` primitive to fill it — subtle-fill icon-only button,
ported from `TextBoxButton.svelte`. NumberBox composes TextBox + TextBoxButton the same way
fluent-svelte's own `NumberBox.svelte` composes its `TextBox`/`TextBoxButton`, rather than
reimplementing input chrome from scratch. `NumberBoxSpinButtonMode` (Compact: side-by-side +/-
always inside the box; Expanded: no buttons at rest, a bigger stacked up/down control pops out on
focus via CSS `:focus-within`) is not a fluent-svelte concept — it's this project's own addition,
self-contained/absolute-positioned rather than IOverlayService-based for the same reason as
ComboBox/ContentDialog above.

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
