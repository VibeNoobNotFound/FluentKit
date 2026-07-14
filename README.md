# Fluent.Blazor — starter scaffold

A Fluent 2 / WinUI 3 design system for Blazor, built as pure Razor + CSS. See `fluent-blazor-plan.md`
for the full roadmap this follows.

## What's here

- `src/Fluent.Blazor/` — the RCL.
  - `Theming/` — light/dark/system theme, resolved via `IThemeService` + `theme-interop.js`
    (`prefers-color-scheme`, live-updating), applied as `data-theme` on `<html>`.
  - `wwwroot/Tokens/` — the token layer (`tokens.css` is the single entry point consumers link).
    Primitives (`_primitives.css`) are theme-independent; semantics (`_semantic.{light,dark}.css`)
    are transcribed from real WinUI XAML resource dictionaries, structured to mirror each other 1:1.
  - `Primitives/` — `FluentButton` (4 variants × 4 states), `FluentToggleButton`, `FluentCheckBox`
    (incl. three-state/indeterminate), `FluentRadioButton` + `FluentRadioGroup`, `FluentToggleSwitch`,
    `FluentTextBox`, `FluentPasswordBox`, `FluentTextBlock` (full type ramp), `FluentDivider`,
    `FluentIconButton`, `FluentPersonPicture`.
  - `Composite/FluentTooltip` — the proof-of-concept consumer of the overlay infra.
  - `Overlay/` — `IOverlayService` + `FluentOverlayHost` + `OverlaySurface`, Blazor's answer to
    portal/teleportation for anything that needs to render outside its parent's layout flow
    (tooltips today; flyouts/combo boxes/context menus next).
  - `Effects/Mica/FluentMicaPanel` — approximates WinUI's real Mica material, rebuilt against the
    actual effect graph in `SystemBackdropBrushFactory.cpp` (`BuildMicaEffectBrush`): a heavily
    blurred wallpaper image, run through a luminosity-blend pass then a color-blend tint pass
    (both real WinUI defaults: `TintOpacity`/`LuminosityOpacity`), plus noise. Opaque, static —
    does NOT use `backdrop-filter`. Web content can't sample the desktop directly, so
    `BackgroundImageUrl` lets the host app supply a stand-in image; with none supplied it falls
    back to WinUI's own documented `SolidBackgroundFillColorBase(Alt)` fill. See the doc comment
    on `FluentMicaPanel` for the full derivation.
  - `Effects/Acrylic/FluentAcrylicBrush` — approximates WinUI's in-app Acrylic. Translucent, and a
    different mechanism from Mica: it live-blurs whatever's actually rendered behind it via CSS
    `backdrop-filter`, the same way in-app Acrylic blurs live content behind a flyout/nav pane.
    `Kind.Base` (more opaque) / `Kind.Thin` (more see-through), matching `DesktopAcrylicKind`.
  - `Effects/Reveal/FluentRevealBackground` — pointer-tracked radial-gradient highlight (WinUI's
    Reveal). JS interop only measures pointer position; the gradient itself is pure CSS driven by
    two custom properties it writes.
  - `Composite/` — beyond `FluentTooltip`, now also `FluentFlyout`, `FluentMenuFlyout`/
    `FluentContextMenu`, `FluentComboBox`, `FluentAutoSuggestBox`, `FluentCalendarView`/
    `FluentCalendarDatePicker`, `FluentNavigationView`, `FluentContentDialog`, `FluentMenuBar`,
    `FluentPivot`, `FluentDropDownButton`, `FluentSplitButton`, and `FluentTeachingTip`.
- `samples/Fluent.Blazor.Sample.Wasm/` — a Blazor WASM host demoing all of the above: theme
  switching, the whole page background running through `FluentMicaPanel` over a real wallpaper
  image, Mica Base vs. Base Alt side by side, and `FluentAcrylicBrush` cards live-blurring that
  Mica background behind them.

## To run it

```bash
cd Fluent.Blazor
dotnet restore
dotnet run --project samples/Fluent.Blazor.Sample.Wasm
```

## CSS isolation gotcha (already hit once, worth remembering)

A Razor Class Library's own component-scoped stylesheet (`_content/Fluent.Blazor/Fluent.Blazor.bundle.scp.css`)
is **not** meant to be linked directly and 404s if you try. The *host app's* build generates its own
bundle (`{HostAssemblyName}.styles.css`, served flat from the app's own root) which internally
`@import`s every referenced RCL's bundle. Only link the host app's own generated stylesheet —
see `samples/Fluent.Blazor.Sample.Wasm/wwwroot/index.html` for the working example.

Related: any markup built via `RenderTreeBuilder` in a `.cs` file does *not* get a component's CSS
isolation scope attribute, so `.razor.css` styles silently won't apply to it. Always define
dynamically-shown markup as Razor template fields (`RenderFragment x = @<span>...</span>;`) inside
the `.razor` file's `@code` block instead — `FluentTooltip` is set up this way on purpose.

## Known gaps / next up

Every component in the plan's Phase 2–5 priority table is now built. What's actually left:
1. The accent color tokens (`--accent-fill-color-default` etc.) are still placeholders (Windows'
   default blue), not derived from the user's actual system accent — flagged with `TODO` in both
   `_semantic.*.css` files.
2. No automated tests yet (`tests/` doesn't exist) — Phase 6 in the plan calls for bUnit plus
   Playwright screenshot tests pinned against real WinUI 3 screenshots.
3. No `docs/` demo site yet.
4. No High Contrast theme dictionary (§7 of the plan calls for a third theme alongside light/dark,
   mirroring WinUI's own `HighContrast` resource key) — `Theming/` currently only resolves
   light/dark/system.
5. `overlay-interop.js` only flips vertically (below → above) — full 4-direction collision handling
   (left/right flipping too) hasn't been needed yet by any built component, but would matter for a
   ComboBox/AutoSuggestBox dropdown pinned near a viewport edge.
6. `FluentTeachingTip`'s beak is positioned from the *requested* placement, not whatever
   `overlay-interop.js` actually flipped it to — fine as long as there's room, but the beak won't
   flip sides if the tip itself gets flipped. Fixing this needs the overlay layer to report the
   resolved placement back to the component (see `OverlayEntry`), not just the computed style.
