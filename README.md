# Fluent.Blazor — starter scaffold

This is the Phase 0 + Phase 1 + first Phase 2 component + Phase 3 skeleton from the project plan,
hand-authored file by file. **Important caveat:** this sandbox has no .NET SDK installed and no
access to nuget.org, so none of this has actually been run through `dotnet build`. Treat it as a
carefully-written first draft, not a verified-working build — the very first thing to do is open it
in Rider and let the compiler find anything I got wrong.

## What's here

- `src/Fluent.Blazor/` — the RCL. Tokens (light+dark, transcribed from real WinUI XAML), theming
  service, `FluentButton` (all 4 variants × 4 states), and the overlay/portal infrastructure
  (`IOverlayService` + `FluentOverlayHost` + `OverlaySurface`) with `FluentTooltip` as the
  proof-of-concept consumer.
- `samples/Fluent.Blazor.Sample.Wasm/` — a Blazor WASM host wiring it all together, with a demo
  page exercising every variant, theme switching, and the tooltip/overlay flow.

## To run it

```bash
cd Fluent.Blazor
dotnet restore
dotnet run --project samples/Fluent.Blazor.Sample.Wasm
```

Or just open `Fluent.Blazor.sln` in Rider and hit run on the WASM sample's run configuration.

## Things to double-check on first build (likely rough edges)

1. **Package versions.** I pinned `8.0.10` for the ASP.NET Core Components packages — bump these to
   whatever's actually latest in the 8.x line when you restore; NuGet wasn't reachable from here to
   confirm the current patch version.
2. **The accent color tokens are placeholders.** `--accent-fill-color-default` in both theme files
   is a fixed hex (Windows' default blue), not derived from the user's actual system accent — that's
   flagged with a `TODO` comment in both `_semantic.*.css` files and is explicitly out of scope until
   you build the accent pipeline mentioned in the original plan.
3. **`overlay-interop.js` only flips vertically (top↔bottom).** Left/right placement and full
   4-direction collision handling is the obvious next increment once ComboBox/ContextMenu need it.
4. **`FluentTooltip` has no show/hide delay.** It's intentionally the simplest possible consumer of
   the overlay infra, not a finished Tooltip — see the comment at the top of `FluentTooltip.razor.cs`.
5. **CSS isolation + RenderFragments gotcha, already fixed once, worth remembering:** any markup you
   build via `RenderTreeBuilder` in a `.cs` file does *not* get a component's scope attribute, so its
   `.razor.css` styles silently won't apply. Always define dynamically-shown marks as Razor template
   fields (`RenderFragment x = @<span>...</span>;`) inside the `.razor` file's `@code` block instead —
   `FluentTooltip` is set up this way on purpose, as a pattern to copy for every future overlay
   consumer (Flyout, ComboBox, ContextMenu, etc.).

## Next up (not built yet)

Per the plan: `FluentCheckBox` and `FluentToggleSwitch` next (same pattern as Button, fastest
components to add), then the first real Playwright screenshot test pinned against an actual WinUI 3
screenshot, then start on `FluentFlyout` using the same `IOverlayService` pattern `FluentTooltip`
already proved out.
