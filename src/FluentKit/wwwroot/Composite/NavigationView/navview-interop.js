const observers = new Map();
const swipeWatchers = new Map();

export function startObservingResize(el, dotNetHelper) {
    if (!el || !dotNetHelper) return;

    stopObservingResize(el);

    const observer = new ResizeObserver(entries => {
        for (const entry of entries) {
            dotNetHelper.invokeMethodAsync('OnResize', entry.contentRect.width);
        }
    });

    observer.observe(el);
    observers.set(el, observer);
}

export function stopObservingResize(el) {
    if (!el) return;

    const observer = observers.get(el);
    if (observer) {
        observer.disconnect();
        observers.delete(el);
    }
}

function isRenderedVisible(el) {
    if (!el || el.getClientRects().length === 0) return false;

    const style = getComputedStyle(el);
    if (style.display === 'none' || style.visibility === 'hidden') return false;

    const overlay = el.closest('.fluent-nav-view-pane-overlay');
    return !overlay || overlay.classList.contains('fluent-nav-view-pane-overlay--open');
}

function nearestVisibleAncestorItem(itemEl) {
    let current = itemEl;

    for (let i = 0; i < 32 && current && !isRenderedVisible(current); i++) {
        const childrenWrapper = current.closest('.fluent-nav-view-item-children');
        const parentItem = childrenWrapper?.previousElementSibling;
        if (!parentItem?.classList?.contains('fluent-nav-view-item')) {
            return null;
        }
        current = parentItem;
    }

    return isRenderedVisible(current) ? current : null;
}

function selectionAnchor(selectedItem) {
    return isRenderedVisible(selectedItem)
        ? selectedItem
        : nearestVisibleAncestorItem(selectedItem);
}

/** Applies the item-local selection indicator to every rendered menu copy. */
export function syncNavItemAnchors(rootEl) {
    if (!rootEl) return;

    rootEl.querySelectorAll('.fluent-nav-view-item--selection-anchor')
        .forEach(item => item.classList.remove('fluent-nav-view-item--selection-anchor'));

    rootEl.querySelectorAll('.fluent-nav-view-item--selected')
        .forEach(selectedItem => selectionAnchor(selectedItem)
            ?.classList.add('fluent-nav-view-item--selection-anchor'));
}

export function getElementWidth(el) {
    return el ? el.getBoundingClientRect().width : 800;
}

const DRAG_SLOP_PX = 8;
const COMMIT_THRESHOLD = 0.5;
const FLICK_VELOCITY_PX_MS = 0.5;

function edgeGeometry(edge, overlayEl) {
    const rect = overlayEl.getBoundingClientRect();
    if (edge === 'top') {
        return { axis: 'Y', size: rect.height, openSign: 1 };
    }
    if (edge === 'right') {
        return { axis: 'X', size: rect.width, openSign: -1 };
    }
    return { axis: 'X', size: rect.width, openSign: 1 };
}

function setDragTransform(overlayEl, axis, offsetPx) {
    overlayEl.style.transform = axis === 'Y'
        ? `translateY(${offsetPx}px)`
        : `translateX(${offsetPx}px)`;
    overlayEl.style.opacity = '1';
}

function clearDragTransform(overlayEl) {
    overlayEl.style.transform = '';
    overlayEl.style.opacity = '';
}

function clamp(value, min, max) {
    return Math.max(min, Math.min(max, value));
}

/**
 * Watches only the currently valid gesture region: the closed edge strip when opening,
 * or the overlay header when closing. Navigation items therefore retain native scrolling
 * and ordinary pointer taps are left alone until a drag is confirmed.
 */
export function startSwipeWatcher(rootEl, overlayEl, closedRegionEl, openRegionEl, dotNetHelper, isOpen, edge) {
    if (!rootEl || !overlayEl || !dotNetHelper) return;

    stopSwipeWatcher(rootEl);

    const regionEl = isOpen ? openRegionEl : closedRegionEl;
    if (!regionEl) return;

    const state = {
        dotNetHelper,
        overlayEl,
        regionEl,
        edge,
        isOpen,
        armed: false,
        dragging: false,
        pointerId: null,
        startX: 0,
        startY: 0,
        axis: 'X',
        size: 0,
        openSign: 1,
        lastOffset: 0,
        lastTime: 0,
        velocity: 0
    };

    const onPointerDown = event => {
        if (!event.isPrimary || (event.pointerType === 'mouse' && event.button !== 0)) return;
        if (state.pointerId !== null) return;

        const geometry = edgeGeometry(state.edge, state.overlayEl);
        state.axis = geometry.axis;
        state.size = geometry.size;
        state.openSign = geometry.openSign;
        state.armed = true;
        state.dragging = false;
        state.pointerId = event.pointerId;
        state.startX = event.clientX;
        state.startY = event.clientY;
        state.velocity = 0;
    };

    const promoteToConfirmedDrag = event => {
        state.dragging = true;
        state.lastOffset = state.isOpen ? 0 : -state.openSign * state.size;
        state.lastTime = performance.now();
        state.overlayEl.classList.add('fluent-nav-view-pane-overlay--dragging');
        setDragTransform(state.overlayEl, state.axis, state.lastOffset);

        try {
            state.regionEl.setPointerCapture(event.pointerId);
        } catch {
            // Pointer capture is unavailable in a few older WebView implementations.
        }
    };

    const onPointerMove = event => {
        if (event.pointerId !== state.pointerId || (!state.dragging && !state.armed)) return;

        const dx = event.clientX - state.startX;
        const dy = event.clientY - state.startY;
        const primaryDelta = state.axis === 'Y' ? dy : dx;
        const crossDelta = state.axis === 'Y' ? dx : dy;

        if (!state.dragging) {
            if (Math.abs(primaryDelta) < DRAG_SLOP_PX && Math.abs(crossDelta) < DRAG_SLOP_PX) return;
            if (Math.abs(crossDelta) > Math.abs(primaryDelta)) {
                state.armed = false;
                state.pointerId = null;
                return;
            }

            const openingDelta = state.openSign * primaryDelta;
            const meaningfulDirection = state.isOpen ? openingDelta < 0 : openingDelta > 0;
            if (!meaningfulDirection) {
                state.armed = false;
                state.pointerId = null;
                return;
            }

            state.armed = false;
            promoteToConfirmedDrag(event);
        }

        event.preventDefault();

        const openedOffset = 0;
        const closedOffset = -state.openSign * state.size;
        const delta = state.axis === 'Y' ? dy : dx;
        const offset = state.isOpen
            ? clamp(openedOffset + delta, Math.min(openedOffset, closedOffset), Math.max(openedOffset, closedOffset))
            : clamp(closedOffset + delta, Math.min(openedOffset, closedOffset), Math.max(openedOffset, closedOffset));

        const now = performance.now();
        const elapsed = now - state.lastTime;
        if (elapsed > 0) {
            state.velocity = (offset - state.lastOffset) / elapsed;
        }
        state.lastOffset = offset;
        state.lastTime = now;
        setDragTransform(state.overlayEl, state.axis, offset);
    };

    const finishDrag = event => {
        if (event.pointerId !== state.pointerId) return;

        state.armed = false;
        const wasDragging = state.dragging;
        state.dragging = false;
        state.pointerId = null;
        if (!wasDragging) return;

        state.overlayEl.classList.remove('fluent-nav-view-pane-overlay--dragging');

        const openedOffset = 0;
        const closedOffset = -state.openSign * state.size;
        const totalTravel = Math.abs(closedOffset);
        const openness = totalTravel > 0
            ? 1 - Math.abs(state.lastOffset - openedOffset) / totalTravel
            : 0;
        const openingVelocity = state.openSign * state.velocity;
        const willBeOpen = Math.abs(openingVelocity) >= FLICK_VELOCITY_PX_MS
            ? openingVelocity > 0
            : openness >= COMMIT_THRESHOLD;

        clearDragTransform(state.overlayEl);
        state.isOpen = willBeOpen;
        state.dotNetHelper.invokeMethodAsync('OnSwipeCommit', willBeOpen);
    };

    const onPointerUp = event => finishDrag(event);
    const onPointerCancel = event => finishDrag(event);

    regionEl.addEventListener('pointerdown', onPointerDown, { passive: true });
    regionEl.addEventListener('pointermove', onPointerMove, { passive: false });
    regionEl.addEventListener('pointerup', onPointerUp, { passive: true });
    regionEl.addEventListener('pointercancel', onPointerCancel, { passive: true });

    state.handlers = { onPointerDown, onPointerMove, onPointerUp, onPointerCancel };
    swipeWatchers.set(rootEl, state);
}

export function stopSwipeWatcher(rootEl) {
    if (!rootEl) return;

    const state = swipeWatchers.get(rootEl);
    if (!state) return;

    state.regionEl.removeEventListener('pointerdown', state.handlers.onPointerDown);
    state.regionEl.removeEventListener('pointermove', state.handlers.onPointerMove);
    state.regionEl.removeEventListener('pointerup', state.handlers.onPointerUp);
    state.regionEl.removeEventListener('pointercancel', state.handlers.onPointerCancel);

    if (state.dragging) {
        clearDragTransform(state.overlayEl);
        state.overlayEl.classList.remove('fluent-nav-view-pane-overlay--dragging');
    }

    swipeWatchers.delete(rootEl);
}
