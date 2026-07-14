// Reports the NavigationView root's own rendered width, in plain CSS pixels, for
// FluentNavigationView's Auto-mode breakpoint calc (Expanded/Compact/Minimal at 1008px/641px) —
// getBoundingClientRect() rather than offsetWidth so it reflects the true rendered box even if the
// component ever picks up a border/padding via AdditionalAttributes.
export function getElementWidth(el) {
    if (!el) {
        return 800;
    }

    return el.getBoundingClientRect().width;
}
