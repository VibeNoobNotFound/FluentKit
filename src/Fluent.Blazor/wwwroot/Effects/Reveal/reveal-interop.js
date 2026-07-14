// Tracks pointer position over a Reveal-enabled element and pushes it in as CSS custom properties
// (--reveal-x / --reveal-y, element-relative px) so the actual highlight rendering stays pure CSS
// (a radial-gradient positioned via those two variables) — this module's only job is measurement.
const state = new Map();

export function startTracking(id, element) {
    stopTracking(id);

    const onMove = (e) => {
        const rect = element.getBoundingClientRect();
        element.style.setProperty('--reveal-x', `${e.clientX - rect.left}px`);
        element.style.setProperty('--reveal-y', `${e.clientY - rect.top}px`);
        element.style.setProperty('--reveal-opacity', '1');
    };

    const onLeave = () => {
        element.style.setProperty('--reveal-opacity', '0');
    };

    element.addEventListener('pointermove', onMove);
    element.addEventListener('pointerleave', onLeave);

    state.set(id, { element, onMove, onLeave });
}

export function stopTracking(id) {
    const entry = state.get(id);
    if (!entry) {
        return;
    }

    entry.element.removeEventListener('pointermove', entry.onMove);
    entry.element.removeEventListener('pointerleave', entry.onLeave);
    state.delete(id);
}
