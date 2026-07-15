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

