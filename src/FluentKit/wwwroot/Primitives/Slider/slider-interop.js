// Pointer/touch drag tracking for FluentSlider. Percent math lives here (not in fluent-svelte's
// original per-instance Svelte closures) because Blazor caches JS modules by URL, so every
// FluentSlider instance shares this module — drag state MUST be keyed per-slider-id, not held in a
// module-level singleton, or two sliders dragged in quick succession (or a stray leftover pointerup
// from a previous drag) would corrupt each other.

const dragState = new Map();

function computePercent(clientX, clientY, railRect, orientation, reverse) {
    let pct;
    if (orientation === "vertical") {
        // Vertical rail: 0% is visually at the bottom by default (matches WinUI/fluent-svelte).
        pct = ((railRect.bottom - clientY) / railRect.height) * 100;
    } else {
        pct = ((clientX - railRect.left) / railRect.width) * 100;
    }

    pct = Math.min(100, Math.max(0, pct));
    return reverse ? 100 - pct : pct;
}

function eventPoint(event) {
    if (event.touches && event.touches.length > 0) {
        return { x: event.touches[0].clientX, y: event.touches[0].clientY };
    }
    if (event.changedTouches && event.changedTouches.length > 0) {
        return { x: event.changedTouches[0].clientX, y: event.changedTouches[0].clientY };
    }
    return { x: event.clientX, y: event.clientY };
}

// Called on pointerdown/touchstart from the Blazor side, which already has clientX/clientY off its
// own event args — no need to round-trip a native browser Event object across the interop boundary.
export function getPercentAt(railEl, orientation, reverse, clientX, clientY) {
    const rect = railEl.getBoundingClientRect();
    return computePercent(clientX, clientY, rect, orientation, reverse);
}

export function startDrag(sliderId, railEl, orientation, reverse, dotNetRef) {
    stopDrag(sliderId);

    const move = (event) => {
        const point = eventPoint(event);
        const rect = railEl.getBoundingClientRect();
        const pct = computePercent(point.x, point.y, rect, orientation, reverse);
        dotNetRef.invokeMethodAsync("OnDragPercent", pct);
        if (event.cancelable) {
            event.preventDefault();
        }
    };

    const end = () => {
        stopDrag(sliderId);
        dotNetRef.invokeMethodAsync("OnDragEnd");
    };

    window.addEventListener("mousemove", move);
    window.addEventListener("touchmove", move, { passive: false });
    window.addEventListener("mouseup", end);
    window.addEventListener("touchend", end);
    window.addEventListener("touchcancel", end);

    dragState.set(sliderId, { move, end });
}

export function stopDrag(sliderId) {
    const existing = dragState.get(sliderId);
    if (!existing) {
        return;
    }

    window.removeEventListener("mousemove", existing.move);
    window.removeEventListener("touchmove", existing.move);
    window.removeEventListener("mouseup", existing.end);
    window.removeEventListener("touchend", existing.end);
    window.removeEventListener("touchcancel", existing.end);

    dragState.delete(sliderId);
}
