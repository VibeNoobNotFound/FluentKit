---
name: fluentkit-api
description: Use FluentKit.Blazor correctly when building a separate Blazor WASM, Server, or MAUI Blazor Hybrid product. Versioned with the FluentKit package.
---

# FluentKit application

This skill is for using FluentKit from another application. It is not the repository
maintainer workflow. The bundle version in `metadata.json` must match the installed
`FluentKit.Blazor` package exactly.

## Required workflow

1. Inspect the app's project files and package lock/assets to identify its
   `FluentKit.Blazor` version and hosting model.
2. Read `references/api.json` for the exact component parameters, callbacks, slots, enums,
   and services in this bundle's version. Do not invent a parameter from a newer release.
3. Follow `references/setup.md` before adding components. The fixed static asset base is
   `_content/FluentKit`, not `_content/FluentKit.Blazor`.
4. Keep `ThemeProvider` at the root, register the theme/accent/overlay services, and render
   one `FluentOverlayHost` for overlays.
5. Prefer token variables and existing component parameters over app-local hard-coded Fluent
   values. Use the sample route in the API JSON to find a runnable example.
6. After changes, build the app and verify static assets and interactive overlays.

## References

- [Setup](references/setup.md)
- [Theming and tokens](references/theming-and-tokens.md)
- [Overlays](references/overlays.md)
- [Troubleshooting](references/troubleshooting.md)
- [Sample routes](references/sample-routes.md)
- [Generated API JSON](references/api.json)
