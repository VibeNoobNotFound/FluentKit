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
AutoSuggestBox (free-typing search-as-you-type, live-filtered dropdown, QuerySubmitted vs.
SuggestionChosen split) is conceptually ported from fluent-svelte's `AutoSuggestBox.svelte`, though
reimplemented against this project's own TextBox/dropdown primitives rather than the svelte source
directly — composes FluentTextBox the same way NumberBox/ComboBox's editable mode do, and reuses
ComboBox's self-contained absolutely-positioned dropdown approach (own root, not IOverlayService)
for the same width/anchoring reasons.
CalendarView/CalendarViewItem (day/month/year drill-down grid, header-click view cycling, Min/Max/
Blackout, roving-tabindex arrow-key navigation that walks across page boundaries, `multiple`
array-selection) ports fluent-svelte's `CalendarView.svelte` and `CalendarViewItem.svelte`. Svelte's
`fly`/`fadeScale` transition directives (page-turn slide, view-switch zoom) have no direct Blazor
equivalent, so they're reproduced as CSS `@keyframes` animations re-triggered via `@key` on the
table/tbody each time the page or view changes (see FluentCalendarView.razor.cs doc comment).
CalendarDatePicker composes CalendarView the same way
NumberBox composes TextBox, ported from `CalendarDatePicker.svelte`, but self-contained/
absolutely-positioned rather than wired into a generic Flyout — same IOverlayService-avoidance
reasoning as ComboBox/NumberBox/AutoSuggestBox above.

IconButton (bare icon-only button, no min-width/label padding) and PersonPicture (round avatar with
Src-with-initials-fallback, live 404 fallback, Badge slot) port fluent-svelte's `IconButton.svelte`
and `PersonPicture.svelte`. MenuBar/MenuBarItem (top-level app menu bar — File/Edit/View-style —
click-or-Enter/Space open, hover-to-switch between open siblings, ArrowLeft/ArrowRight roving
navigation with wraparound) ports fluent-svelte's `MenuBar.svelte`/`MenuBarItem.svelte`, reusing
this project's existing MenuFlyoutItem/MenuFlyoutDivider for each item's dropdown content rather
than reimplementing flyout rendering.

IconButton (bare icon-only button, no min-width/label padding) and PersonPicture (round avatar with
Src-with-initials-fallback, live 404 fallback, Badge slot) port fluent-svelte's `IconButton.svelte`
and `PersonPicture.svelte`. MenuBar/MenuBarItem (top-level app menu bar — File/Edit/View-style —
click-or-Enter/Space open, hover-to-switch between open siblings, ArrowLeft/ArrowRight roving
navigation with wraparound) ports fluent-svelte's `MenuBar.svelte`/`MenuBarItem.svelte`, reusing
this project's existing MenuFlyoutItem/MenuFlyoutDivider for each item's dropdown content rather
than reimplementing flyout rendering.

ToggleButton rides the same Standard/Accent visual ramp as FluentButton (unchecked looks like a
plain Button, checked switches to the Accent ramp) rather than porting a dedicated fluent-svelte
source file — fluent-svelte doesn't have a standalone ToggleButton distinct from its ToggleSwitch,
so this one is this project's own composition of tokens already established by Button/ToggleSwitch.
PasswordBox composes FluentTextBox + FluentTextBoxButton (swapping the underlying input's `Type`
between "password"/"text" on a reveal-eye click) the same way NumberBox composes TextBox +
TextBoxButton — not a fluent-svelte port, since fluent-svelte's own TextBox handles password mode
via a bare `type` prop with no reveal button.
Pivot/PivotItem (horizontal tab strip, one visible content pane, arrow-key/Home/End roving
navigation between tab headers) is this project's own implementation — fluent-svelte has no direct
equivalent — built on the same item-self-registers-with-a-shared-context pattern MenuBarContext
already established, rather than a live DOM query.
DropDownButton and SplitButton are thin compositions over the existing FluentMenuFlyout (itself
ported from fluent-svelte's `MenuFlyoutWrapper.svelte`, see above) with a button-styled trigger in
place of MenuFlyout's plain trigger span — not separate fluent-svelte ports, since fluent-svelte
doesn't split these out as distinct components from its MenuFlyout either.
TeachingTip (anchor-relative persistent callout — title/subtitle, optional action-button row,
explicit close button, a beak/pointer toward the anchor) is this project's own component, built
directly on IOverlayService/FluentOverlayHost the same shape as FluentFlyout, rendered `bare` since
it supplies its own card chrome and beak rather than the default flyout surface. fluent-svelte has
no TeachingTip equivalent to port from.
Reveal (Effects/Reveal) is a from-scratch implementation of WinUI's Reveal highlight — a
pointer-tracked radial-gradient spotlight, same effects layer as Mica/Acrylic. The JS module
(`wwwroot/Effects/Reveal/reveal-interop.js`) only measures pointer position via `pointermove`/
`pointerleave` and writes it into two CSS custom properties; all actual rendering is a CSS
radial-gradient in `FluentRevealBackground.razor.css`. Not ported from fluent-svelte, which doesn't
implement Reveal (it isn't part of Fluent Svelte's own component set).

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
