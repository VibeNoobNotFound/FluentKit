# FluentKit

A token-accurate Fluent Design (WinUI 3) component library for Blazor — WebAssembly, Server, and
MAUI Blazor Hybrid — built as pure Razor and CSS, with no third-party UI framework underneath it.

Design tokens (color, corner radius, state aliases) are transcribed directly from Microsoft's own
`microsoft-ui-xaml` resource dictionaries, and effects like Mica and Acrylic are rebuilt against
WinUI's actual effect graph rather than approximated from screenshots. See
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for exactly where each component's markup,
styling, or token values were sourced from.

> Status: alpha (`0.1.0-alpha`). APIs may still change between versions. See "Known gaps" below.

## Contents

- [What's in here](#whats-in-here)
- [Getting started](#getting-started)
- [Running the sample](#running-the-sample)
- [Theming](#theming)
- [Project layout](#project-layout)
- [CSS isolation gotcha](#css-isolation-gotcha)
- [Known gaps / next up](#known-gaps--next-up)
- [Contributing](#contributing)
- [License](#license)

## What's in here

**Primitives** — `FluentButton` (4 variants x 4 states), `FluentToggleButton`, `FluentCheckBox`
(including three-state/indeterminate), `FluentRadioButton` + `FluentRadioGroup`,
`FluentToggleSwitch`, `FluentTextBox`, `FluentPasswordBox`, `FluentNumberBox`, `FluentTextBlock`
(full type ramp), `FluentDivider`, `FluentCard`, `FluentExpander`, `FluentIcon`, `FluentIconButton`,
`FluentPersonPicture`, `FluentProgressBar`, `FluentProgressRing`, `FluentInfoBadge`, `FluentInfoBar`,
`FluentListView`, and `FluentSlider`.

**Composite controls** — `FluentTooltip`, `FluentFlyout`, `FluentMenuFlyout` /
`FluentContextMenu`, `FluentComboBox`, `FluentAutoSuggestBox`, `FluentCalendarView` /
`FluentCalendarDatePicker`, `FluentNavigationView`, `FluentContentDialog`, `FluentMenuBar`,
`FluentPivot`, `FluentDropDownButton`, `FluentSplitButton`, and `FluentTeachingTip`.

**Effects** — `FluentMicaPanel` (opaque backdrop material, rebuilt against WinUI's real
`BuildMicaEffectBrush` effect graph: blurred wallpaper -> luminosity blend -> color tint -> noise),
`FluentAcrylicBrush` (translucent, live `backdrop-filter` blur, `Base`/`Thin` kinds), and
`FluentRevealBackground` (pointer-tracked radial-gradient highlight).

**Theming** — light/dark/system theme resolved through `IThemeService`, applied as `data-theme` on
`<html>`, with live updates when the OS `prefers-color-scheme` changes.

**Overlay infrastructure** — `IOverlayService` + `FluentOverlayHost` + `OverlaySurface`, a
portal/teleportation layer for anything that needs to render outside its parent's layout flow
(tooltips, flyouts, context menus, teaching tips).

## Getting started

FluentKit isn't published to NuGet yet, so consume it as a project or repository reference:

```bash
git clone https://github.com/VibeNoobNotFound/Fluent.Blazor.git
```

```xml
<ItemGroup>
  <ProjectReference Include="..\FluentKit\src\FluentKit\FluentKit.csproj" />
</ItemGroup>
```

Then wire up theming and tokens in your host app.

`wwwroot/index.html` (WASM) or `Pages/_Host.cshtml` / `App.razor` (Server):

```html
<link rel="stylesheet" href="_content/FluentKit/Tokens/tokens.css" />
```

`Program.cs`:

```csharp
builder.Services.AddScoped<IThemeService, ThemeService>();
```

Root component (e.g. `App.razor` or `MainLayout.razor`):

```razor
<ThemeProvider>
    <FluentOverlayHost>
        @Body
    </FluentOverlayHost>
</ThemeProvider>
```

`ThemeProvider` resolves and applies the theme on first render; `FluentOverlayHost` is required by
any composite control built on the overlay service (tooltips, flyouts, menus, dialogs, teaching
tips). Then use components as you would any other Razor component:

```razor
<FluentButton Variant="FluentButtonVariant.Accent">Save changes</FluentButton>
```

## Running the sample

`samples/FluentKit.Sample.Wasm` demos every component above, including theme switching, the page
background rendered through `FluentMicaPanel` over a real wallpaper image, Mica Base vs. Base Alt
side by side, and `FluentAcrylicBrush` cards live-blurring that Mica background behind them.

```bash
git clone https://github.com/VibeNoobNotFound/Fluent.Blazor.git
cd Fluent.Blazor
dotnet restore
dotnet run --project samples/FluentKit.Sample.Wasm
```

Requires the .NET 10 SDK.

## Theming

Three modes, matching WinUI: `System` (tracks the OS/browser preference live), `Light`, and `Dark`.

```csharp
@inject IThemeService ThemeService

await ThemeService.SetModeAsync(ThemeMode.Dark);
```

The token layer is split into two pieces so consumers only ever need to link one file:

- `_primitives.css` — theme-independent primitive values (raw color ramps, spacing, corner radius).
- `_semantic.light.css` / `_semantic.dark.css` — semantic aliases transcribed from WinUI's own XAML
  resource dictionaries, structured to mirror each other property-for-property.
- `tokens.css` — the single entry point that imports both layers; this is the only file consumers
  should link directly.

## Project layout

```
src/FluentKit/
  Theming/       IThemeService, ThemeProvider, theme-interop.js
  Primitives/    One folder per primitive component (.razor / .razor.cs / .razor.css)
  Composite/     One folder per composite control, built on Overlay/ where applicable
  Overlay/       IOverlayService, FluentOverlayHost, OverlaySurface
  Effects/       Mica, Acrylic, Reveal
  wwwroot/       Tokens, per-component JS interop modules, icon webfont
samples/
  FluentKit.Sample.Wasm/   Blazor WASM host demoing every component
```

Each component folder follows the same three-file convention: `.razor` for markup, `.razor.cs` for
the code-behind, `.razor.css` for CSS-isolated styles. Components that need pointer/DOM measurement
(drag tracking, overlay positioning, pointer-relative gradients) pair with a small JS interop module
under `wwwroot/`, matched folder-for-folder with the component that consumes it.

## CSS isolation gotcha

A Razor Class Library's own component-scoped stylesheet (`_content/FluentKit/FluentKit.bundle.scp.css`)
is not meant to be linked directly and will 404 if you try. The host app's build generates its own
bundle (`{HostAssemblyName}.styles.css`, served flat from the app's own root) which internally
`@import`s every referenced RCL's bundle. Only link the host app's own generated stylesheet — see
`samples/FluentKit.Sample.Wasm/wwwroot/index.html` for a working example.

Related: any markup built via `RenderTreeBuilder` in a `.cs` file does not get a component's CSS
isolation scope attribute, so `.razor.css` styles silently won't apply to it. Define
dynamically-shown markup as Razor template fields (`RenderFragment x = @<span>...</span>;`) inside
the `.razor` file's `@code` block instead.

## Known gaps / next up

1. Accent color tokens (`--accent-fill-color-default` etc.) are still placeholders (Windows'
   default blue), not derived from the user's actual system accent color — flagged `TODO` in both
   `_semantic.*.css` files.
2. No automated tests yet (`tests/` doesn't exist) — bUnit plus Playwright screenshot tests pinned
   against real WinUI 3 screenshots are planned.
3. No published docs/demo site yet, beyond the sample app.
4. No High Contrast theme (a third theme alongside light/dark, mirroring WinUI's own
   `HighContrast` resource key) — `Theming/` currently only resolves light/dark/system.
5. `overlay-interop.js` only flips vertically (below to above); full 4-direction collision handling
   (left/right flipping too) hasn't been needed yet, but would matter for a dropdown pinned near a
   viewport edge.
6. `FluentTeachingTip`'s beak is positioned from the requested placement, not whatever
   `overlay-interop.js` actually flipped it to — fine as long as there's room, but the beak won't
   flip sides if the tip itself gets flipped.
7. Not yet published to NuGet — consume via project or repository reference for now.

## Contributing

Issues and pull requests are welcome. If you're adding or changing a component, please also update
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) when markup, styling structure, or token values
are derived from an external source.

## License

See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for attribution of design tokens, ported
component structure, and bundled assets (fluent-svelte, microsoft-ui-xaml, Fluent System Icons).
