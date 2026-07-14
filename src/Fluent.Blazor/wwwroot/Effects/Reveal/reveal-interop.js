// Reveal tracks pointer position via a SINGLE shared, rAF-throttled listener across every tracked
// element, not a pointermove/pointerleave pair per element like the first version had. Two reasons:
//
// 1. Performance — the old version called getBoundingClientRect() (a layout-forcing call) on every
//    raw 'pointermove' event, which fires far faster than the screen can repaint. That's what made
//    it feel laggy: dozens of synchronous layout reflows per second, each one blocking the main
//    thread right when it needed to be free to paint the next frame. This version measures at most
//    once per animation frame, no matter how many mousemove events fired in between.
//
// 2. Real WinUI Reveal border light isn't "am I directly over this element" — nearby controls
//    (e.g. list items) pick up a proximity glow on their border before the pointer actually enters
//    them. PROXIMITY (px) lets the light fade in as the cursor approaches an element's edge, not
//    just once it's literally inside the element's box — this is what answers "it should also
//    follow the cursor even if it's not hovering it, when near it".
const PROXIMITY = 48;

const tracked = new Map();
let rafHandle = null;
let lastX = -Infinity;
let lastY = -Infinity;
let listenerInstalled = false;

function ensureGlobalListener() {
    if (listenerInstalled) {
        return;
    }
    listenerInstalled = true;

    document.addEventListener('pointermove', (e) => {
        lastX = e.clientX;
        lastY = e.clientY;
        scheduleUpdate();
    }, { passive: true });
}

function scheduleUpdate() {
    if (rafHandle !== null) {
        return;
    }
    rafHandle = requestAnimationFrame(() => {
        rafHandle = null;
        updateAll();
    });
}

function updateAll() {
    for (const entry of tracked.values()) {
        const rect = entry.element.getBoundingClientRect();

        // Distance from the pointer to the nearest point on the element's own box — 0 once the
        // pointer is inside it, growing as it moves away in any direction.
        const dx = Math.max(rect.left - lastX, 0, lastX - rect.right);
        const dy = Math.max(rect.top - lastY, 0, lastY - rect.bottom);
        const distance = Math.sqrt(dx * dx + dy * dy);

        if (distance > PROXIMITY) {
            entry.element.style.setProperty('--reveal-opacity', '0');
            continue;
        }

        entry.element.style.setProperty('--reveal-x', `${lastX - rect.left}px`);
        entry.element.style.setProperty('--reveal-y', `${lastY - rect.top}px`);

        // Unitless 0–100 position within the element's own box (clamped, since the pointer can be
        // outside the box while still within PROXIMITY of it). Kept separate from --reveal-x/y
        // above (which stay px, for the radial-gradient position) because the :active tilt below
        // needs a plain number it can multiply by a deg value in calc() — you can't do that with a
        // percentage. This is what makes the click-press tilt toward whichever side was clicked.
        const px = rect.width === 0 ? 50 : Math.min(100, Math.max(0, ((lastX - rect.left) / rect.width) * 100));
        const py = rect.height === 0 ? 50 : Math.min(100, Math.max(0, ((lastY - rect.top) / rect.height) * 100));
        entry.element.style.setProperty('--reveal-px', px.toFixed(2));
        entry.element.style.setProperty('--reveal-py', py.toFixed(2));

        // 1 while inside the element, tapering linearly to 0 at PROXIMITY px away, instead of a
        // hard on/off — the fade itself is what makes the approach read as "the light is
        // following the cursor" rather than a light that just switches on at the boundary.
        const opacity = distance === 0 ? 1 : Math.max(0, 1 - distance / PROXIMITY);
        entry.element.style.setProperty('--reveal-opacity', opacity.toFixed(3));
    }
}

export function startTracking(id, element) {
    ensureGlobalListener();
    tracked.set(id, { element });
}

export function stopTracking(id) {
    tracked.delete(id);
}
