const observers = new Map();

export function startObservingResize(el, dotNetHelper) {
    if (!el || !dotNetHelper) return;
    
    stopObservingResize(el);

    const observer = new ResizeObserver(entries => {
        for (let entry of entries) {
            const width = entry.contentRect.width;
            dotNetHelper.invokeMethodAsync('OnResize', width);
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

// --- Nav view "ribbon" selection indicator -------------------------------------------------
// Ported from PyQt-Fluent-Widgets' ScaleSlideAnimation (qfluentwidgets/common/animation.py),
// which itself reimplements the WinUI 3 NavigationView selection indicator's squash-and-stretch
// behavior: the pill doesn't just fade/teleport between items, it stretches to bridge the two
// positions partway through the animation, then contracts back down to full size while catching
// up to the destination for the rest. A single continuous ease-in-out curve drives the whole
// thing (see NAV_EASE) so there's no velocity jump partway through.
//
// The indicator's target is ALWAYS resolved by querying for .fluent-nav-view-item--selected
// inside the given container, never by remembering "the item that was clicked" or a value key
// passed in earlier. That class is set by the same render that reflects the real SelectedValue,
// so this can't drift onto a stale element the way a remembered target could (e.g. when a click
// both expands/collapses a sibling group *and* changes selection, shifting layout out from under
// a previously-computed position). Every call just asks "where is .selected right now?" and
// animates there from wherever the indicator visually is right now - self-correcting by design.
//
// Position/size are driven purely with `transform: translateY() scaleY()`, not `top`/`height` -
// scaling/translating is compositor-only (no layout reflow per frame), which keeps the animation
// smooth in both directions. The indicator's own box stays a fixed NAV_INDICATOR_SIZE tall in
// CSS; scaleY temporarily stretches it, anchored at its own top edge via transform-origin.

const NAV_INDICATOR_SIZE = 16; // px - must match the fixed block-size in FluentNavigationView.razor.css
const NAV_INDICATOR_DURATION = 500; // ms - long enough for the stretch to read as fluid, not flashy
// A single continuous ease-in-out curve applied across the *entire* animation (rather than two
// different curves stitched together partway through) so velocity never jumps mid-flight.
const NAV_EASE = 'cubic-bezier(0.65, 0, 0.35, 1)';

const TRANSFORM_RE = /translateY\(([-\d.]+)px\)/;

function selectedNavItem(container) {
    return container.querySelector('.fluent-nav-view-item--selected');
}

function navIndicatorTargetTop(container, itemEl) {
    const containerRect = container.getBoundingClientRect();
    const itemRect = itemEl.getBoundingClientRect();
    // The indicator is an absolutely-positioned CHILD of this same scrolling container, so its
    // rendered position is (container's viewport top) - scrollTop + (its own top offset). Both
    // itemRect.top and containerRect.top are current, post-scroll viewport coordinates, so their
    // difference alone only tells us the item's *currently visible* offset - it doesn't account
    // for the scrollTop the indicator's own resting position is equally subject to. Without
    // adding container.scrollTop back in here, every position comes out short by exactly however
    // far the list has been scrolled: invisible while scrolled to the top, but once you scroll
    // down (e.g. to reach a later section) the indicator lands scrollTop px too high - often
    // right on top of whatever's still near the top of the visible list, like a section header.
    return (itemRect.top - containerRect.top) + container.scrollTop
        + (itemRect.height / 2) - (NAV_INDICATOR_SIZE / 2);
}

function cancelNavIndicatorAnimation(indicator) {
    if (indicator._navAnim) {
        indicator._navAnim.cancel();
        indicator._navAnim = null;
    }
}

/** Reads the indicator's own current on-screen top, from whatever we last set its transform to.
 *  Returns null if the indicator has never been placed (still at its initial hidden state). */
function currentIndicatorTop(indicator) {
    if (indicator.style.opacity !== '1') return null;
    const match = TRANSFORM_RE.exec(indicator.style.transform || '');
    return match ? parseFloat(match[1]) : null;
}

function placeIndicator(indicator, top) {
    indicator.style.transform = `translateY(${top}px) scaleY(1)`;
    indicator.style.opacity = '1';
}

/** Moves the shared ribbon indicator onto whichever item is currently .fluent-nav-view-item--selected
 *  inside `container`. Pass animate=false for instant placement (first paint, or when there's no
 *  sensible position to slide from); animate=true slides + squash/stretches from wherever the
 *  indicator currently sits. */
export function updateNavIndicator(container, indicator, animate) {
    if (!container || !indicator) return;

    const toEl = selectedNavItem(container);
    if (!toEl) {
        cancelNavIndicatorAnimation(indicator);
        indicator.style.opacity = '0';
        return;
    }

    const toTop = navIndicatorTargetTop(container, toEl);
    const fromTop = animate ? currentIndicatorTop(indicator) : null;

    cancelNavIndicatorAnimation(indicator);

    if (fromTop === null || Math.abs(fromTop - toTop) < 0.5) {
        // No sensible "from" (first placement) or effectively no movement - just snap.
        placeIndicator(indicator, toTop);
        return;
    }

    const dim = NAV_INDICATOR_SIZE;
    const dist = Math.abs(toTop - fromTop);
    const midScale = (dist + dim) / dim;
    const isForward = toTop > fromTop;

    // The indicator's leading edge (top) stays anchored at whichever position is physically
    // higher on screen (the smaller of the two `top` values) for the whole animation's first
    // part, while it stretches down to bridge the gap; then it catches up to the destination
    // while shrinking back down. Moving down, that anchor is the start position (already the
    // higher one); moving up, it's the destination (which is now the higher one).
    const anchorTop = isForward ? fromTop : toTop;

    const keyframes = [
        { transform: `translateY(${anchorTop}px) scaleY(1)`, offset: 0 },
        { transform: `translateY(${anchorTop}px) scaleY(${midScale})`, offset: 0.4 },
        { transform: `translateY(${toTop}px) scaleY(1)`, offset: 1 }
    ];

    indicator.style.opacity = '1';
    const anim = indicator.animate(keyframes, {
        duration: NAV_INDICATOR_DURATION,
        easing: NAV_EASE,
        fill: 'forwards'
    });
    indicator._navAnim = anim;
    anim.onfinish = () => {
        // Bake the final state into the inline style so the next call's currentIndicatorTop()
        // read reflects it, even though fill:'forwards' animations get cleared by a later .cancel().
        placeIndicator(indicator, toTop);
        indicator._navAnim = null;
    };
}

export function getElementWidth(el) {
    if (!el) {
        return 800;
    }

    return el.getBoundingClientRect().width;
}

// --- Edge swipe-to-open for the overlay pane (Compact/Minimal/FullScreen) -----------------
//
// Design: JS owns the entire live drag. Blazor is only told the final outcome (open or closed)
// once the gesture ends, via OnSwipeCommit - never per-frame. Round-tripping every pointermove
// through .NET (even with JS interop's relatively low overhead) would add latency a swipe gesture
// can't hide the way a click can; a drag that doesn't track the finger 1:1 in real time reads as
// broken rather than just "a bit slow". So the pane's `transform` is written directly as an
// inline style here for the whole duration of the drag, and only reconciled back into Blazor's
// normal CSS-transition-driven --open/closed classes at the very end.
//
// Two distinct gestures share this watcher:
//   1. Open gesture: pane is closed, a touch starts within EDGE_ZONE_PX of the watched edge and
//      drags inward -> pane follows the finger from 0% to 100% open.
//   2. Close gesture: pane is already open (rendered full since the CSS --open class put it at
//      its resting transform) and a touch starts anywhere on the overlay and drags back toward
//      the watched edge -> pane follows the finger from 100% back down.
// Both resolve the same way: on release, if dragged past COMMIT_THRESHOLD of the travel distance
// (or with fast enough velocity in the commit direction, so a quick flick doesn't need to cross
// the full 50% line) the gesture commits to the opposite of its start state; otherwise it snaps
// back to where it started.

const EDGE_ZONE_PX = 32; // how close to the edge a touch must start to begin an *open* drag
const DRAG_SLOP_PX = 8; // movement required before an armed pointerdown promotes to a real drag
const COMMIT_THRESHOLD = 0.5; // fraction of travel distance past which release commits the open/close
const FLICK_VELOCITY_PX_MS = 0.5; // px/ms - a fast flick commits even if under the distance threshold

const swipeWatchers = new Map();

/** axis/sign metadata per edge - how the pane's own size maps to a translate distance, and which
 *  direction (+1/-1) counts as "opening" along that axis. */
function edgeGeometry(edge, overlayEl) {
    if (edge === 'top') {
        return { axis: 'Y', size: overlayEl.getBoundingClientRect().height, openSign: 1 };
    }
    if (edge === 'right') {
        return { axis: 'X', size: overlayEl.getBoundingClientRect().width, openSign: -1 };
    }
    // left (default)
    return { axis: 'X', size: overlayEl.getBoundingClientRect().width, openSign: 1 };
}

function setDragTransform(overlayEl, axis, offsetPx) {
    overlayEl.style.transform = axis === 'Y'
        ? `translateY(${offsetPx}px)`
        : `translateX(${offsetPx}px)`;
    overlayEl.style.opacity = '1'; // stays visible/interactive-looking for the whole drag
}

/** Removes the inline transform/opacity this watcher set during a drag, handing control back to
 *  the CSS --open/closed classes (whichever Blazor's render currently applies) so the normal
 *  transition takes over for the final snap. */
function clearDragTransform(overlayEl) {
    overlayEl.style.transform = '';
    overlayEl.style.opacity = '';
}

export function startSwipeWatcher(rootEl, overlayEl, dotNetHelper, isOpen, edge) {
    if (!rootEl || !overlayEl || !dotNetHelper) return;

    stopSwipeWatcher(rootEl);

    const state = {
        dotNetHelper,
        overlayEl,
        edge,
        isOpen,
        armed: false,
        dragging: false,
        pointerId: null,
        startX: 0,
        startY: 0,
        startTime: 0,
        axis: 'X',
        size: 0,
        openSign: 1,
        lastOffset: 0,
        lastTime: 0,
        velocity: 0
    };

    const onPointerDown = (e) => {
        if (e.pointerType === 'mouse' && e.button !== 0) return;

        const geo = edgeGeometry(state.edge, state.overlayEl);
        state.axis = geo.axis;
        state.size = geo.size;
        state.openSign = geo.openSign;

        if (!state.isOpen) {
            // Only arm an "open" drag if the touch begins right at the watched edge.
            const rect = rootEl.getBoundingClientRect();
            const withinEdge =
                state.edge === 'top' ? (e.clientY - rect.top) <= EDGE_ZONE_PX :
                state.edge === 'right' ? (rect.right - e.clientX) <= EDGE_ZONE_PX :
                (e.clientX - rect.left) <= EDGE_ZONE_PX;
            if (!withinEdge) return;
        }
        // else: pane is open, a close-drag can arm from anywhere - the whole root is being
        // watched (see addEventListener below), and when open the overlay covers the relevant
        // area, so a touch anywhere on it naturally lands here too via bubbling.

        // Armed, but NOT yet a confirmed drag: nothing is touched (no transform, no pointer
        // capture, no preventDefault) until onPointerMove sees real movement past DRAG_SLOP_PX.
        // This is deliberate - grabbing pointer capture or writing a transform on every single
        // pointerdown would turn ordinary taps on nav items into drags, since a tap is also a
        // pointerdown+pointerup with ~0px of travel. Only promoting to a real drag after the
        // slop threshold lets a plain tap fall through untouched to the item's own click handler.
        state.armed = true;
        state.dragging = false;
        state.pointerId = e.pointerId;
        state.startX = e.clientX;
        state.startY = e.clientY;
        state.startTime = performance.now();
        state.velocity = 0;
    };

    const promoteToConfirmedDrag = (e) => {
        state.dragging = true;
        state.lastOffset = state.isOpen ? 0 : -state.openSign * state.size;
        state.lastTime = performance.now();

        state.overlayEl.classList.add('fluent-nav-view-pane-overlay--dragging');
        // Closed pane isn't in the DOM's --open state, so it has no size/visibility yet from CSS
        // alone during the drag; give it a starting transform matching "fully closed" before the
        // first pointermove paints an in-between position, so there's no jump on the first frame.
        setDragTransform(state.overlayEl, state.axis, state.lastOffset);

        try { state.overlayEl.setPointerCapture(e.pointerId); } catch { }
    };

    const onPointerMove = (e) => {
        if (!state.dragging && !state.armed) return;

        if (!state.dragging) {
            // Still just "armed" from pointerdown - decide whether this gesture is actually a
            // drag along our watched axis, or something else (a vertical scroll crossing a
            // left/right edge zone, a tap that jittered a couple px, etc.) that should be left
            // alone. Only the primary axis's movement counts toward the slop distance; sideways
            // movement on the other axis doesn't arm a drag (e.g. scrolling vertically past a
            // left-edge zone shouldn't hijack into an open-drag).
            const dx = e.clientX - state.startX;
            const dy = e.clientY - state.startY;
            const primaryDelta = state.axis === 'Y' ? dy : dx;
            const crossDelta = state.axis === 'Y' ? dx : dy;

            if (Math.abs(primaryDelta) < DRAG_SLOP_PX && Math.abs(crossDelta) < DRAG_SLOP_PX) {
                return; // not enough movement yet either way - keep waiting
            }
            if (Math.abs(crossDelta) > Math.abs(primaryDelta)) {
                // Moving mostly across the other axis (e.g. vertical scroll on a left/right-edge
                // watcher) - this isn't our gesture, disarm and let the browser handle it normally.
                state.armed = false;
                return;
            }
            // Only promote if movement is actually in the direction that makes sense: opening
            // further (when closed) or moving toward closed (when open). A closed pane touched at
            // the left edge but dragged further left (off-screen), for instance, shouldn't arm.
            const openingDelta = state.openSign * primaryDelta;
            const meaningfulDirection = state.isOpen ? openingDelta < 0 : openingDelta > 0;
            if (!meaningfulDirection) {
                state.armed = false;
                return;
            }

            state.armed = false;
            promoteToConfirmedDrag(e);
        }

        e.preventDefault(); // stop the browser treating this as a page/content scroll mid-drag

        const delta = state.axis === 'Y' ? (e.clientY - state.startY) : (e.clientX - state.startX);

        const openedOffset = 0; // resting transform in the --open class, both axes
        const closedOffset = -state.openSign * state.size;

        // Offset lives in the same screen-space coordinate system as `delta` and the CSS
        // transform itself (translateX/Y px), so the raw delta is added directly here - it is
        // NOT re-signed by openSign first. openSign only matters for direction *decisions*
        // (was this movement "opening" or "closing"? - see the slop/arming check above and the
        // velocity sign check in finishDrag), not for where the pane should actually sit on
        // screen. Multiplying delta by openSign before adding it to an offset that's already in
        // screen-space double-flips the sign for the right edge (openSign=-1), which made a
        // left-drag on a right-anchored pane move it further *away* from open instead of toward it.
        let offset;
        if (state.isOpen) {
            offset = clamp(openedOffset + delta,
                Math.min(openedOffset, closedOffset), Math.max(openedOffset, closedOffset));
        } else {
            offset = clamp(closedOffset + delta,
                Math.min(openedOffset, closedOffset), Math.max(openedOffset, closedOffset));
        }

        const now = performance.now();
        const dt = now - state.lastTime;
        if (dt > 0) {
            state.velocity = (offset - state.lastOffset) / dt; // px/ms, signed toward opening = +
        }
        state.lastOffset = offset;
        state.lastTime = now;

        setDragTransform(state.overlayEl, state.axis, offset);
    };

    const finishDrag = () => {
        state.armed = false;
        if (!state.dragging) return;
        state.dragging = false;
        state.overlayEl.classList.remove('fluent-nav-view-pane-overlay--dragging');

        const openedOffset = 0;
        const closedOffset = -state.openSign * state.size;
        const totalTravel = Math.abs(closedOffset);
        // How far along the closed->open travel the release point sits, 0 = fully closed,
        // 1 = fully open - measured against the shared endpoints so both drag directions
        // (open-drag starting closed, close-drag starting open) use the same commit math.
        const openness = totalTravel > 0 ? 1 - Math.abs(state.lastOffset - openedOffset) / totalTravel : 0;

        // state.velocity is the raw offset's rate of change (screen-space px/ms), so it isn't
        // opening-positive on its own - offset decreases toward open on the right edge but
        // increases toward open on the left edge (openSign flips which). Multiply by openSign to
        // get a direction-agnostic "positive = opening" velocity for the flick check below.
        const openingVelocity = state.openSign * state.velocity;
        let willBeOpen;
        if (Math.abs(openingVelocity) >= FLICK_VELOCITY_PX_MS) {
            willBeOpen = openingVelocity > 0;
        } else {
            willBeOpen = openness >= COMMIT_THRESHOLD;
        }

        clearDragTransform(state.overlayEl);
        state.isOpen = willBeOpen;

        dotNetHelper.invokeMethodAsync('OnSwipeCommit', willBeOpen);
    };

    const onPointerUp = () => finishDrag();
    const onPointerCancel = () => finishDrag();

    // Both open-drags (start near the edge, pane closed) and close-drags (start anywhere while
    // open) begin as an ordinary pointerdown bubbling up from wherever the touch actually landed,
    // so a single set of listeners on rootEl covers both - onPointerDown itself decides whether
    // to actually start a drag based on state.isOpen and, for the closed case, edge proximity.
    rootEl.addEventListener('pointerdown', onPointerDown, { passive: true });
    rootEl.addEventListener('pointermove', onPointerMove, { passive: false });
    rootEl.addEventListener('pointerup', onPointerUp, { passive: true });
    rootEl.addEventListener('pointercancel', onPointerCancel, { passive: true });

    state.handlers = { onPointerDown, onPointerMove, onPointerUp, onPointerCancel };
    swipeWatchers.set(rootEl, state);
}

export function stopSwipeWatcher(rootEl) {
    if (!rootEl) return;
    const state = swipeWatchers.get(rootEl);
    if (!state) return;

    rootEl.removeEventListener('pointerdown', state.handlers.onPointerDown);
    rootEl.removeEventListener('pointermove', state.handlers.onPointerMove);
    rootEl.removeEventListener('pointerup', state.handlers.onPointerUp);
    rootEl.removeEventListener('pointercancel', state.handlers.onPointerCancel);

    if (state.dragging) {
        clearDragTransform(state.overlayEl);
        state.overlayEl.classList.remove('fluent-nav-view-pane-overlay--dragging');
    }

    swipeWatchers.delete(rootEl);
}

function clamp(value, min, max) {
    return Math.max(min, Math.min(max, value));
}

