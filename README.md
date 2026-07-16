<div align="center">

# FluentKit

**A token-accurate Fluent Design (WinUI 3) component library for Blazor**

WebAssembly · Server · MAUI Blazor Hybrid — pure Razor and CSS, no third-party UI framework underneath

[![NuGet](https://img.shields.io/nuget/v/FluentKit.Blazor.svg?label=NuGet)](https://www.nuget.org/packages/FluentKit.Blazor)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![CI](https://github.com/VibeNoobNotFound/FluentKit/actions/workflows/ci.yml/badge.svg)](https://github.com/VibeNoobNotFound/FluentKit/actions/workflows/ci.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/)

[Live sample](https://vibenoobnotfound.github.io/FluentKit/) · [Getting started](#getting-started) · [Known gaps](#known-gaps--next-up) · [Third-party notices](THIRD_PARTY_NOTICES.md)

</div>
<img width="1912" height="1242" alt="image" src="https://github.com/user-attachments/assets/299a9235-6fc5-4eb4-9c92-64560ebd6cae" />

---

Design tokens (color, corner radius, state aliases) are transcribed directly from Microsoft's own
`microsoft-ui-xaml` resource dictionaries, and effects like Mica and Acrylic are rebuilt against
WinUI's actual effect graph rather than approximated from screenshots. See
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for exactly where each component's markup,
styling, or token values were sourced from.

> **Status:** alpha — see the NuGet badge above for the current published version. APIs may still
> change between versions — see [Known gaps](#known-gaps--next-up).

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

<table>
<tr>
<td width="50%" valign="top">

### Primitives — Buttons

- `FluentButton` (4 variants × 4 states)
- `FluentToggleButton`
- `FluentIconButton`

</td>
<td width="50%" valign="top">

### Primitives — Input

- `FluentTextBox`
- `FluentPasswordBox`
- `FluentCheckBox` (incl. indeterminate)
- `FluentRadioButton` / `FluentRadioGroup`
- `FluentToggleSwitch`
- `FluentSlider`

</td>
</tr>
<tr>
<td width="50%" valign="top">

### Primitives — Display

- `FluentTextBlock` (full type ramp)
- `FluentDivider`
- `FluentCard`
- `FluentExpander`
- `FluentIcon`
- `FluentPersonPicture`

</td>
<td width="50%" valign="top">

### Primitives — Status & progress / collections

- `FluentProgressBar`
- `FluentProgressRing`
- `FluentInfoBadge`
- `FluentInfoBar`
- `FluentListView`

</td>
</tr>
<tr>
<td width="50%" valign="top">

### Composite — Overlays

- `FluentTooltip`
- `FluentFlyout`
- `FluentMenuFlyout` / `FluentContextMenu`
- `FluentContentDialog`
- `FluentTeachingTip`

</td>
<td width="50%" valign="top">

### Composite — Pickers & inputs

- `FluentComboBox`
- `FluentAutoSuggestBox`
- `FluentNumberBox`
- `FluentCalendarView` / `FluentCalendarDatePicker`
- `FluentTimePicker`

</td>
</tr>
<tr>
<td width="50%" valign="top">

### Composite — Buttons with menus

- `FluentDropDownButton`
- `FluentSplitButton`

</td>
<td width="50%" valign="top">

### Composite — Navigation

- `FluentNavigationView`
- `FluentMenuBar`
- `FluentPivot`

</td>
</tr>
<tr>
<td width="50%" valign="top">

### Composite — Settings UI

- `FluentSettingsCard`
- `FluentSettingsExpander`

1:1 ports of the Windows Community Toolkit's `SettingsCard` / `SettingsExpander` — see
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md#windows-community-toolkit).

</td>
<td width="50%" valign="top">

### Tooling

**`FluentKit.IconGenerator`** — a Roslyn source generator (referenced as an analyzer, not a
runtime dependency) that emits strongly-typed `FluentIconNames` constants from the bundled
`FluentSystemIcons-Regular.css`, used by the sample app's icon browser and available to consumers
for compile-time-checked `FluentIcon` glyph names.

</td>
</tr>
<tr>
<td width="50%" valign="top">

### Effects

- **`FluentMicaPanel`** — opaque backdrop material: blurred wallpaper → luminosity blend → color tint → noise, rebuilt against WinUI's real `BuildMicaEffectBrush` graph
- **`FluentAcrylicBrush`** — translucent, live `backdrop-filter` blur, `Base`/`Thin` kinds
- **`FluentRevealBackground`** — pointer-tracked radial-gradient highlight

</td>
<td width="50%" valign="top">

### Theming

Light / dark / system, resolved through `IThemeService`, applied as `data-theme` on `<html>`, with
live updates on OS `prefers-color-scheme` changes.

### Overlay infrastructure

`IOverlayService` + `FluentOverlayHost` + `OverlaySurface` — a portal layer for anything rendering
outside its parent's layout flow (tooltips, flyouts, context menus, teaching tips).

</td>
</tr>
</table>

## Getting started

**1. Install**

Recommended — from NuGet:

```bash
dotnet add package FluentKit.Blazor
```

`dotnet add package` pins the exact current version automatically — the current published version
is always shown in the NuGet badge at the top of this README. If editing your `.csproj` by hand,
pin a specific version rather than using a floating wildcard, for reproducible builds:

```xml
<PackageReference Include="FluentKit.Blazor" Version="0.2.0" /> <!-- match the badge above -->
```

Alternatively, to track `main` directly, consume it as a project or repository reference instead:

```bash
git clone https://github.com/VibeNoobNotFound/FluentKit.git
```

```xml
<ItemGroup>
  <ProjectReference Include="..\FluentKit\src\FluentKit\FluentKit.csproj" />
</ItemGroup>
```

**2. Link the Token & Icon stylesheets**

`wwwroot/index.html` (WASM) or `Pages/_Host.cshtml` / `App.razor` (Server):

```html
<link rel="stylesheet" href="_content/FluentKit/Tokens/tokens.css" />
<link rel="stylesheet" href="_content/FluentKit/Icons/FluentSystemIcons-Regular.css" />
```

**3. Register Core FluentKit services**

`Program.cs`:

```csharp
builder.Services.AddScoped<IThemeService, ThemeService>();
builder.Services.AddScoped<IAccentColorService, AccentColorService>();
builder.Services.AddScoped<IOverlayService, OverlayService>(); // For Flyouts, Tooltips etc...
```

**4. Wrap your root component**

```razor
<ThemeProvider>
      @*For Mica Background
        <FluentMicaPanel Variant="@_backgroundVariant"
              BackgroundImageUrl="@_wallpaperUrl"
              style="position:fixed; inset:0; z-index:-1; opacity:0.72;" /> *@
        @Body
    <FluentOverlayHost />
</ThemeProvider>
```

`ThemeProvider` resolves and applies the theme on first render; `FluentOverlayHost` is required by
any composite control built on the overlay service (tooltips, flyouts, menus, dialogs, teaching tips).

**5. Use components**

```razor
<FluentButton Variant="FluentButtonVariant.Accent">Save changes</FluentButton>
```

## Running the sample

The demo pages themselves live in `samples/FluentKit.Sample.Shared` (a Razor class library) and
are hosted by two thin platform entry points, so the exact same pages run on both WASM and MAUI:

- `samples/FluentKit.Sample.Wasm` — Blazor WASM host, deployed to GitHub Pages
- `samples/Fluentkit.Sample.Maui` — MAUI Blazor Hybrid host (Android by default; iOS/MacCatalyst
  on macOS runners, Windows on Windows runners — see the `TargetFrameworks` conditions in its
  `.csproj`)

Both demo every component above, including theme switching (persisted to `localStorage`), the
page background rendered through `FluentMicaPanel` over a real wallpaper image, Mica Base vs. Base
Alt side by side, `FluentAcrylicBrush` cards live-blurring that Mica background behind them, and an
icon browser (`Pages/Design/IconBrowserPage.razor`) generated from the same icon names
`FluentKit.IconGenerator` emits at compile time.

```bash
git clone https://github.com/VibeNoobNotFound/FluentKit.git
cd FluentKit
dotnet restore FluentKit.slnx
dotnet run --project samples/FluentKit.Sample.Wasm
```

To run the MAUI sample instead (requires the MAUI workload — `dotnet workload install maui-android`
at minimum):

```bash
dotnet run --project samples/Fluentkit.Sample.Maui --framework net10.0-android
```

Requires the **.NET 10 SDK**. A hosted WASM build is also published to
[vibenoobnotfound.github.io/FluentKit](https://vibenoobnotfound.github.io/FluentKit/).

## Theming

Three modes, matching WinUI: `System` (tracks the OS/browser preference live), `Light`, and `Dark`.

```csharp
@inject IThemeService ThemeService

await ThemeService.SetModeAsync(ThemeMode.Dark);
```

The token layer is split so consumers only ever need to link one file:

| File | Purpose |
|---|---|
| `_primitives.css` | Theme-independent primitive values (raw color ramps, spacing, corner radius) |
| `_semantic.light.css` / `_semantic.dark.css` | Semantic aliases transcribed from WinUI's own XAML resource dictionaries, mirrored property-for-property |
| `tokens.css` | Single entry point importing both layers — **the only file consumers should link directly** |

## Project layout

```
src/
  FluentKit/                    The library (Microsoft.NET.Sdk.Razor, RCL)
    Theming/                    IThemeService, ThemeProvider, theme-interop.js
    Primitives/                 One folder per primitive component (.razor / .razor.cs / .razor.css)
    Composite/                  One folder per composite control, built on Overlay/ where applicable
    Overlay/                    IOverlayService, FluentOverlayHost, OverlaySurface
    Effects/                    Mica, Acrylic, Reveal
    wwwroot/                    Tokens, per-component JS interop modules, icon webfont
  FluentKit.IconGenerator/      Roslyn source generator, referenced as an analyzer only
                                 (emits FluentIconNames.g.cs from wwwroot/Icons/*.css)
samples/
  FluentKit.Sample.Shared/      Razor class library holding every demo page — Primitives/,
                                 Composite/, Effects/, Design/ (icon browser) — plus the shared
                                 nav shell/settings page, referenced by both hosts below
  FluentKit.Sample.Wasm/        Blazor WASM host for FluentKit.Sample.Shared, deployed to Pages
  Fluentkit.Sample.Maui/        MAUI Blazor Hybrid host for FluentKit.Sample.Shared
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
`samples/FluentKit.Sample.Wasm/wwwroot/index.html` for a working example. (This is a WASM/Server
concern only — the MAUI Blazor Hybrid host, `samples/Fluentkit.Sample.Maui`, doesn't build a
`.styles.css` bundle the same way; its `BlazorWebView` resolves `_content/...` static web assets
directly.)

> **Related:** any markup built via `RenderTreeBuilder` in a `.cs` file does not get a component's
> CSS isolation scope attribute, so `.razor.css` styles silently won't apply to it. Define
> dynamically-shown markup as Razor template fields (`RenderFragment x = @<span>...</span>;`) inside
> the `.razor` file's `@code` block instead.

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

## Contributing

Issues and pull requests are welcome. If you're adding or changing a component, please also update
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) when markup, styling structure, or token values
are derived from an external source.

## License

MIT — see [LICENSE](LICENSE). See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for attribution
of design tokens, ported component structure, and bundled assets (fluent-svelte, microsoft-ui-xaml,
Windows Community Toolkit, Fluent System Icons).
