# Overlays

Tooltips, flyouts, menus, dialogs, and teaching tips require `IOverlayService` and one
long-lived `FluentOverlayHost` under `ThemeProvider`. Keep the host mounted across navigation
and do not create one host per page.
