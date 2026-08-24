# Third-party notices

FluentKit's own code is MIT-licensed (see [LICENSE](LICENSE)). This file documents where component
markup/structure, design tokens, and bundled assets were sourced or ported from, per Anthropic's
practice of full attribution for anything not written from scratch.

## Contents

- [fluent-svelte](#fluent-svelte) — component markup/CSS structure and visual-state conventions
- [microsoft-ui-xaml](#microsoft-ui-xaml) — design token values
- [Windows Community Toolkit](#windows-community-toolkit) — SettingsCard / SettingsExpander
- [Fluent System Icons](#fluent-system-icons) — icon webfont

---

## Fluent Svelte

**[tropix126/fluent-svelte](https://github.com/tropix126/fluent-svelte)** — MIT License,
© tropix126 / Fluent Svelte contributors.

No source code was copied verbatim. Token *values* are independently re-derived from Microsoft's
own `microsoft-ui-xaml` repository (see [below](#microsoft-ui-xaml)); design token *structure* and
visual-state naming conventions (light/dark alias naming, the Acrylic approach) were informed by
fluent-svelte generally. Where a specific component's markup/CSS *structure* (not literal source)
was ported and adapted to Razor + CSS isolation + FluentKit's own token names, it's listed below.

| FluentKit component(s) | Ported from | What carried over / what didn't |
|---|---|---|
| `FluentExpander` | `Expander.svelte` | Clip + slide collapse technique |
| `FluentProgressBar` | `ProgressBar.svelte` | Indeterminate keyframe timing |
| `FluentInfoBar` | `InfoBar.svelte` | Icon/title/message/action/close-button layout; default icon simplified to an `InfoBadge` |
| `FluentSlider` | `Slider.svelte` | Thumb offset/clamp formula, rail click-to-jump + drag, tick placement, orientation/reverse handling — reimplemented as Razor + a small JS interop module (`wwwroot/Primitives/Slider/slider-interop.js`) instead of Svelte's reactive bindings |
| `FluentMenuFlyout` / `FluentMenuFlyoutItem` / `FluentMenuFlyoutDivider`, `FluentContextMenu` | `MenuFlyout*`, `ContextMenu` | Item variants (standard/radio/toggle/cascading), bullet/checkmark glyph conventions, hover-intent delay for cascading submenus, close-on-select semantics. **Positioning reimplemented, not ported** — both reuse FluentKit's own `IOverlayService`/`OverlaySurface` (same infra as `FluentFlyout`/`FluentTooltip`) instead of fluent-svelte's own clamp math; `ContextMenu` anchors to an invisible marker moved to the cursor rather than reimplementing collision-avoidance |
| `FluentComboBox` / `FluentComboBoxItem` | `ComboBox.svelte` / `ComboBoxItem.svelte` | Button vs. editable/searchable trigger modes, starts-with search matching, keyboard nav, selected-row menu-grow choreography. **Deliberately does not** go through `IOverlayService` — needs to match the trigger's exact width and grow from a specific row, so stays a plain absolutely-positioned child of its own root, same as fluent-svelte's `.combo-box-dropdown { position: absolute }` |
| `FluentContentDialog` | `ContentDialog.svelte` | Title/body/optional footer, size presets (min/standard/max), Escape + scrim-click dismissal, half-width end-aligned single-button footer convention. Self-contained rather than `IOverlayService`-based, same reasoning as ComboBox — a modal dialog is always viewport-centered with its own scrim regardless of trigger |
| `FluentTextBox` (`Buttons` slot), `FluentTextBoxButton` | `TextBox.svelte` (`slot="buttons"`), `TextBoxButton.svelte` | Right-aligned button row; subtle-fill icon-only button |
| `FluentNumberBox` | `NumberBox.svelte` | Composes TextBox + TextBoxButton rather than reimplementing input chrome, same as the Svelte original. `NumberBoxSpinButtonMode` (`Compact`: side-by-side +/- always visible; `Expanded`: buttons pop out on focus via `:focus-within`) is **FluentKit's own addition**, not a fluent-svelte concept |
| `FluentAutoSuggestBox` | `AutoSuggestBox.svelte` | Free-typing search-as-you-type, live-filtered dropdown, `QuerySubmitted` vs. `SuggestionChosen` split — conceptually ported, reimplemented against FluentKit's own TextBox/dropdown primitives rather than the Svelte source directly; reuses ComboBox's self-contained dropdown approach |
| `FluentCalendarView` / `FluentCalendarViewItem` | `CalendarView.svelte` / `CalendarViewItem.svelte` | Day/month/year drill-down grid, header-click view cycling, Min/Max/Blackout, roving-tabindex arrow-key nav across page boundaries, `multiple` array-selection. Svelte's `fly`/`fadeScale` transitions have no Blazor equivalent — reproduced as CSS `@keyframes` re-triggered via `@key` on the table/tbody (see doc comment in `FluentCalendarView.razor.cs`) |
| `FluentCalendarDatePicker` | `CalendarDatePicker.svelte` | Composes CalendarView the same way NumberBox composes TextBox. Self-contained/absolutely-positioned rather than wired into a generic Flyout, same `IOverlayService`-avoidance reasoning as ComboBox/NumberBox/AutoSuggestBox |
| `FluentIconButton` | `IconButton.svelte` | Bare icon-only button, no min-width/label padding |
| `FluentPersonPicture` | `PersonPicture.svelte` | Round avatar, `Src`-with-initials-fallback, live 404 fallback, `Badge` slot |
| `FluentMenuBar` / `FluentMenuBarItem` | `MenuBar.svelte` / `MenuBarItem.svelte` | Top-level app menu bar (File/Edit/View-style), click-or-Enter/Space open, hover-to-switch between open siblings, ArrowLeft/ArrowRight roving nav with wraparound — reuses FluentKit's existing `MenuFlyoutItem`/`MenuFlyoutDivider` for dropdown content rather than reimplementing flyout rendering |
| `FluentDropDownButton`, `FluentSplitButton` | — | Not direct ports — thin compositions over FluentKit's own `FluentMenuFlyout` (itself ported, see above) with a button-styled trigger; fluent-svelte doesn't split these out as distinct components from its MenuFlyout either |

**Not ported from fluent-svelte** (fluent-svelte has no equivalent; these are FluentKit's own
implementations, built on its existing patterns where noted):

- `FluentToggleButton` — rides the same Standard/Accent visual ramp as `FluentButton` rather than a dedicated source file; fluent-svelte has no standalone ToggleButton distinct from its ToggleSwitch
- `FluentPasswordBox` — composes `FluentTextBox` + `FluentTextBoxButton` (swaps input `Type` between `password`/`text` on a reveal-eye click), same composition pattern as NumberBox; fluent-svelte's own TextBox handles password mode via a bare `type` prop with no reveal button
- `FluentPivot` / `FluentPivotItem` — horizontal tab strip, one visible pane, arrow-key/Home/End roving nav between headers; built on the same item-self-registers-with-shared-context pattern `MenuBarContext` already established
- `FluentTeachingTip` — anchor-relative persistent callout (title/subtitle, optional action-button row, explicit close, beak toward anchor); built directly on `IOverlayService`/`FluentOverlayHost` the same shape as `FluentFlyout`, rendered `bare` since it supplies its own card chrome and beak
- `FluentRevealBackground` (`Effects/Reveal`) — pointer-tracked radial-gradient spotlight, same effects layer as Mica/Acrylic. The JS module (`wwwroot/Effects/Reveal/reveal-interop.js`) only measures pointer position via `pointermove`/`pointerleave` and writes it into two CSS custom properties; all rendering is a CSS radial-gradient in `FluentRevealBackground.razor.css`
- `FluentTimePicker` — fluent-svelte has no TimePicker, so this is modeled directly on WinUI 3's own `TimePicker`/`TimePickerFlyout` (see [microsoft-ui-xaml](#microsoft-ui-xaml) below), not ported from any web precedent. WinUI's flyout uses a `LoopingSelector` per column (Hour/Minute/AM-PM); the web has no equivalent primitive, so each column is a plain `scroll-snap-align: center` list with selection tracked by comparing scroll offsets in `wwwroot/Composite/TimePicker/FluentTimePicker-interop.js`

---

## Microsoft.UI.XAML

**[microsoft/microsoft-ui-xaml](https://github.com/microsoft/microsoft-ui-xaml)** —
`controls/dev/CommonStyles/*.xaml`, MIT License, © Microsoft Corporation.

Design token values — colors, corner radius, component state aliases — are transcribed directly
from this repository into `_semantic.light.css` / `_semantic.dark.css`.

---

## Windows Community Toolkit

**[CommunityToolkit/Windows](https://github.com/CommunityToolkit/Windows)** — MIT License,
© .NET Foundation and Contributors.

`FluentSettingsCard` and `FluentSettingsExpander` are 1:1 ports of the toolkit's
`CommunityToolkit.WinUI.Controls.SettingsCard` and `SettingsExpander` — these aren't core WinUI 3
controls, they're WCT's own convention for building consistent Settings-app-style UI (icon +
header/description + end-aligned content, optionally made clickable with a trailing chevron; the
expander nests a list of cards underneath a collapsible header).

| FluentKit component | Ported from | What carried over / what didn't |
|---|---|---|
| `FluentSettingsCard` | `SettingsCard` | Leading icon, header/description stack, end-aligned content slot, optional trailing action icon + click behavior, and the `ContentAlignment` modes (`Auto`/`Right`/`Left`/`Vertical`). WinUI's version reacts to available width via three extra `VisualState`s (`RightWrapped`, `RightWrappedNoIcon`, `Vertical`) driven by a `ControlSizeTrigger`; Blazor has no equivalent, so the same breakpoints are reproduced as CSS container queries on the card's own inline size in `FluentSettingsCard.razor.css` instead of C#-side layout code |
| `FluentSettingsExpander` | `SettingsExpander` | The collapsible header (itself a `FluentSettingsCard`-shaped surface) revealing nested `FluentSettingsCard` items, plus `ItemsHeader`/`ItemsFooter` slots. WCT hosts its items via an `ItemsRepeater` bound to an `Items`/`ItemsSource` pair; Blazor has no `ItemsRepeater` equivalent worth reproducing, so items are instead passed as ordinary child markup (`ItemsContent`), the same pattern every other FluentKit composite uses for its children. The expand/collapse animation reuses `FluentExpander`'s clip+slide technique rather than WCT's own transition |

---

## Fluent System Icons

**[microsoft/fluentui-system-icons](https://github.com/microsoft/fluentui-system-icons)** —
MIT License, © Microsoft Corporation.

`Primitives/Icon/FluentIcon` renders glyphs from this webfont, shipped directly under
`wwwroot/Icons/` (`FluentSystemIcons-Regular.css/.woff2/.woff`).

> **Why this instead of Segoe Fluent Icons:** the font this originally replaced. Also replaced a
> brief intermediate approach using the `Microsoft.FluentUI.AspNetCore.Components.Icons` NuGet
> package (SVG-based) — that package transitively depends on the full
> `Microsoft.FluentUI.AspNetCore.Components` core library, which auto-bundles its own scoped CSS
> into any host app's `.styles.css` via Blazor's static web asset pipeline, with no way to
> reference only the icon data without pulling that CSS along. The webfont has no managed
> dependency at all, so it can't leak component styling.
