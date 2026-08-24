# Theming and tokens

`ThemeProvider` resolves light, dark, and system themes through `IThemeService` and applies
the result as `data-theme` on `<html>`. Register `IThemeService` and
`IAccentColorService`, then place `ThemeProvider` at the application root.

Link only `_content/FluentKit/Tokens/tokens.css`. It is the public token entry point and
imports the implementation layers. Component CSS should consume the existing custom
properties rather than hard-code colors, dimensions, typography, radii, or durations.

The design sample pages under `samples/FluentKit.Sample.Shared/Pages/Design` show the token
families, color modes, spacing, geometry, and icon names in use.
