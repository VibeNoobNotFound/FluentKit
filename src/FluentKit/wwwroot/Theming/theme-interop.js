// Loaded via IJSRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/FluentKit/Theming/theme-interop.js")
// Kept deliberately tiny — this is the ONE JS file in the theming layer.

let mediaQuery = null;
let mediaQueryHandler = null;

export function watchSystemPreference(dotNetRef) {
    unwatchSystemPreference();
    mediaQuery = window.matchMedia("(prefers-color-scheme: dark)");
    mediaQueryHandler = (event) => {
        dotNetRef.invokeMethodAsync("OnSystemPreferenceChanged", event.matches ? "dark" : "light");
    };

    mediaQuery.addEventListener("change", mediaQueryHandler);

    return mediaQuery.matches ? "dark" : "light";
}

export function unwatchSystemPreference() {
    if (mediaQuery && mediaQueryHandler) {
        mediaQuery.removeEventListener("change", mediaQueryHandler);
    }

    mediaQuery = null;
    mediaQueryHandler = null;
}

export function applyResolvedTheme(theme) {
    document.documentElement.setAttribute("data-theme", theme);
}

export function getElementWidth(element) {
    if (!element) return 0;
    return element.getBoundingClientRect().width;
}
