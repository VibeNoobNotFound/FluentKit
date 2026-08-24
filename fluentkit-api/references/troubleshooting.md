# Troubleshooting

- A 404 under `_content/FluentKit`: clean the library and app `bin`/`obj`, then rebuild.
- An invisible overlay: verify `IOverlayService` and `FluentOverlayHost` registration.
- Missing theme variables: link `_content/FluentKit/Tokens/tokens.css` before app CSS.
- A stale isolated stylesheet: perform a full clean before investigating CSS.
