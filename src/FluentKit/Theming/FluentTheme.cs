namespace FluentKit.Theming;

/// <summary>
/// The three theme modes WinUI itself exposes. "System" tracks the OS/browser preference live;
/// Light/Dark are explicit overrides the user picked.
/// </summary>
public enum ThemeMode
{
    System,
    Light,
    Dark
}

/// <summary>
/// DI-registered, not component state — so Blazor Server (one instance can serve many circuits)
/// and MAUI Hybrid (single process, single user) both get correct, independent behavior.
/// Register as Scoped in Server/WASM, Scoped is fine in MAUI Hybrid too (one scope per app instance).
/// </summary>
public interface IThemeService
{
    ThemeMode Mode { get; }

    /// <summary>The resolved theme actually being applied — "System" resolves to Light or Dark here.</summary>
    string ResolvedTheme { get; }

    event Action? ThemeChanged;

    Task SetModeAsync(ThemeMode mode);

    /// <summary>Called once by ThemeProvider on first render to read prefers-color-scheme via JS interop.</summary>
    Task InitializeAsync();
}
