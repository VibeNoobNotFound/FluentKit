// Interop for FluentTimePicker's three snap-scroll columns (Hour / Minute / AM-PM).
//
// Layout contract (must match FluentTimePicker.razor.css):
//   - Each column is a <ul> with one leading spacer <li>, then one <li> per value, then one
//     trailing spacer <li>.
//   - Spacer block-size: 80px, item block-size: 40px (columns viewport: 200px = 5 * 40px).
//   - scroll-snap-type: y mandatory / scroll-snap-align: center on each real item.
//
// Because the leading spacer offsets everything by 80px, the item whose *center* sits at the
// column's own center (scrollTop + 100px, since viewport is 200px tall) is the "settled" one.
// index = round((scrollTop + 100 - 80 - 20) / 40) = round((scrollTop) / 40)
// (100 - 80 - 20 cancels out to 0, since half the viewport (100) minus the spacer (80) minus
// half an item (20) is exactly 0 — i.e. scrollTop alone, divided by item height, gives the index
// of the item currently centered).

const ITEM_HEIGHT = 40;
const SETTLE_DEBOUNCE_MS = 120;

/** @type {WeakMap<Element, { timer: number|null, dotNetRef: any, column: string }>} */
const state = new WeakMap();

function nearestIndex(el) {
    const raw = el.scrollTop / ITEM_HEIGHT;
    return Math.max(0, Math.round(raw));
}

function onScroll(el) {
    const entry = state.get(el);
    if (!entry) return;

    if (entry.timer !== null) {
        clearTimeout(entry.timer);
    }

    entry.timer = setTimeout(() => {
        entry.timer = null;
        const index = nearestIndex(el);
        entry.dotNetRef.invokeMethodAsync("OnColumnSettled", entry.column, index);
    }, SETTLE_DEBOUNCE_MS);
}

/**
 * Attach scroll-settle tracking to a column. Safe to call multiple times per element across
 * re-opens of the flyout (each open re-creates the <ul> via @if in the .razor, so a fresh
 * listener is attached to a fresh element each time — no explicit detach needed, the old
 * element and its listener are simply garbage-collected once removed from the DOM).
 */
export function attachColumn(el, column, dotNetRef) {
    if (!el) return;

    state.set(el, { timer: null, dotNetRef, column });
    el.addEventListener("scroll", () => onScroll(el), { passive: true });
}

/** Instantly (no smooth animation — matches WinUI's flyout re-opening already centered) scrolls
 * a column so the item at `index` sits centered against the highlight band. */
export function scrollToIndex(el, index) {
    if (!el) return;
    el.scrollTop = index * ITEM_HEIGHT;
}
