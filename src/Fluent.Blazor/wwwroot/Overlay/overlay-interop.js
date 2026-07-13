// Flips on the placement's own axis when there isn't room (bottom<->top, right<->left) and clamps
// on the cross axis so the popup never runs off the opposite edge.

const lightDismissHandlers = new Map();

export function computePosition(anchorEl, popupEl, placement) {
    const anchorRect = anchorEl.getBoundingClientRect();
    const popupRect = popupEl.getBoundingClientRect();
    const gap = 4;
    const viewportPadding = 8;

    let resolvedPlacement = placement;
    let top;
    let left;

    if (placement === "left" || placement === "right") {
        // Horizontal placement: position along the anchor's left/right edge, align top edges,
        // then clamp vertically so the popup stays on-screen (e.g. a full-height nav rail anchor).
        if (placement === "left") {
            left = anchorRect.left - popupRect.width - gap;
            if (left < viewportPadding) {
                left = anchorRect.right + gap;
                resolvedPlacement = "right";
            }
        } else {
            left = anchorRect.right + gap;
            if (left + popupRect.width > window.innerWidth - viewportPadding) {
                left = anchorRect.left - popupRect.width - gap;
                resolvedPlacement = "left";
            }
        }

        top = anchorRect.top;
        const maxTop = window.innerHeight - popupRect.height - viewportPadding;
        top = Math.min(Math.max(top, viewportPadding), Math.max(maxTop, viewportPadding));

        return { top, left, placement: resolvedPlacement };
    }

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

    left = anchorRect.left;
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
