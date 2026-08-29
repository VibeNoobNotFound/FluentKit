---
name: fluentkit-api
description: Use the exact FluentKit.Blazor API and integration guidance shipped by the package referenced by the current Blazor or MAUI Blazor application.
---

# FluentKit application integration

This contract is loaded from the exact `FluentKit.Blazor` package restored by the consumer
project. The installed bootstrap skill has already resolved this file; do not replace it with
an online or newer reference.

## Required workflow

1. Identify the consumer project's hosting model and the package version in use.
2. Read `api.json` before using component parameters, callbacks, slots, enums, or services.
3. Read `references/setup.md` and `references/component-selection.md` before adding components.
4. Keep `ThemeProvider` at the root, register the theme, accent, and overlay services, and
   render one `FluentOverlayHost` for overlays.
5. Use the component-selection, theming, overlay, icon, and troubleshooting references when
   the task touches those areas. Use the sample route in `api.json` to find a runnable example.
6. Preserve the fixed static asset base `_content/FluentKit`; it is not
   `_content/FluentKit.Blazor`.
7. Prefer FluentKit tokens and the exact component parameters over app-local hard-coded Fluent
   values.
8. Build the consumer after changes and verify static assets and interactive overlays when
   relevant.

The manifest at `manifest.json` identifies the package, API files, references, and checksums.
The package version is authoritative for this contract.
