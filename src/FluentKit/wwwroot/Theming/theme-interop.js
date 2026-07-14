// Loaded via IJSRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/FluentKit/Theming/theme-interop.js")
// Kept deliberately tiny — this is the ONE JS file in the theming layer.

let mediaQuery = null;

export function watchSystemPreference(dotNetRef) {
    mediaQuery = window.matchMedia("(prefers-color-scheme: dark)");

    mediaQuery.addEventListener("change", (event) => {
        dotNetRef.invokeMethodAsync("OnSystemPreferenceChanged", event.matches ? "dark" : "light");
    });

    return mediaQuery.matches ? "dark" : "light";
}

export function applyResolvedTheme(theme) {
    document.documentElement.setAttribute("data-theme", theme);
}

export function getElementWidth(element) {
    if (!element) return 0;
    return element.getBoundingClientRect().width;
}