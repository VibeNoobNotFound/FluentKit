# Troubleshooting

## `_content/FluentKit/...` returns 404

Clean the affected library and sample `bin`/`obj` directories, restore, and rebuild. Verify
that the stylesheet or module path starts with `_content/FluentKit`, not
`_content/FluentKit.Blazor`.

## Tooltips or flyouts do not appear

Confirm that `IOverlayService` is registered and that one `FluentOverlayHost` is rendered
under the root `ThemeProvider`.

## Theme variables are missing

Confirm that `Tokens/tokens.css` is linked before app CSS. The imported token files are
implementation details; link the entry point only.

## CSS changes appear stale

Do a full clean of the library and sample `bin`/`obj` folders before diagnosing an
incremental build. Razor CSS isolation is bundled by the RCL build.
