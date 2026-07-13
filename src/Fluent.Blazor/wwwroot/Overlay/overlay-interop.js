// v1 scope: flips vertically (bottom<->top) and clamps horizontally so the popup never runs off
// the left/right edge. Left/Right placements and full 4-direction flip are a follow-up —
// this is enough for ToolTip/Flyout to prove the FluentOverlayHost pattern end to end.

const lightDismissHandlers = new Map();

export function computePosition(anchorEl, popupEl, placement) {
    const anchorRect = anchorEl.getBoundingClientRect();
    const popupRect = popupEl.getBoundingClientRect();
    const gap = 4;
    const viewportPadding = 8;

    let resolvedPlacement = placement;
    let top;

    if (placement === "top") {
        top = anchorRect.top - popupRect.height - gap;
        if (top < viewportPadding) {
            top = anchorRect.bottom + gap;
            resolvedPlacement = "bottom";
        }
    } else {
        // default: bottom
        top = anchorRect.bottom + gap;
        if (top + popupRect.height > window.innerHeight - viewportPadding) {
            top = anchorRect.top - popupRect.height - gap;
            resolvedPlacement = "top";
        }
    }

    let left = anchorRect.left;
    const maxLeft = window.innerWidth - popupRect.width - viewportPadding;
    left = Math.min(Math.max(left, viewportPadding), Math.max(maxLeft, viewportPadding));

    return { top, left, placement: resolvedPlacement };
}

export function registerLightDismiss(overlayId, popupEl, anchorEl, dotNetRef) {
    const handler = (event) => {
        const target = event.target;
        if (popupEl.contains(target) || anchorEl.contains(target)) {
            return;
        }
        dotNetRef.invokeMethodAsync("OnLightDismiss", overlayId);
    };

    // Capture phase so this runs before the click can be swallowed by stopPropagation elsewhere.
    document.addEventListener("pointerdown", handler, true);
    lightDismissHandlers.set(overlayId, handler);
}

export function unregisterLightDismiss(overlayId) {
    const handler = lightDismissHandlers.get(overlayId);
    if (handler) {
        document.removeEventListener("pointerdown", handler, true);
        lightDismissHandlers.delete(overlayId);
    }
}
