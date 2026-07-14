// Flips on the placement's own axis when there isn't room (bottom<->top, right<->left) and clamps
// on the cross axis so the popup never runs off the opposite edge.

const lightDismissHandlers = new Map();

export function computePosition(anchorEl, popupEl, placement, matchAnchorWidth) {
    const anchorRect = anchorEl.getBoundingClientRect();

    // Width has to be applied BEFORE popupRect is measured below — changing width can reflow the
    // popup's height (e.g. text wrapping differently), and everything downstream (vertical flip
    // decision, viewport clamping) needs that final, post-resize rect.
    if (matchAnchorWidth) {
        popupEl.style.width = `${anchorRect.width}px`;
    }

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

        return { top, left, placement: resolvedPlacement, width: matchAnchorWidth ? anchorRect.width : null };
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

    return {
        top, left, placement: resolvedPlacement,
        // Reported back so the C# side can bake it into the same Blazor-managed "style" string as
        // top/left — the width set directly on popupEl.style above would otherwise get wiped out
        // the moment OverlaySurface's next render re-applies its bound `style="@Entry.ComputedStyle"`
        // attribute, since that overwrites the whole inline style, not just the properties it lists.
        width: matchAnchorWidth ? anchorRect.width : null
    };
}

export function registerLightDismiss(overlayId, popupEl, anchorEl, dotNetRef) {
    const handler = (event) => {
        const target = event.target;
        if (popupEl.contains(target) || (anchorEl && anchorEl.contains(target))) {
            return;
        }
        dotNetRef.invokeMethodAsync("OnLightDismiss", overlayId);
    };

    // Capture phase so this runs before the click can be swallowed by stopPropagation elsewhere.
    document.addEventListener("pointerdown", handler, true);
    lightDismissHandlers.set(overlayId, handler);
}

// Same as registerLightDismiss, minus the anchor-exclusion check — for detached overlays (see
// OverlayEntry.IsDetached) there's no anchor element to exempt from the dismiss check, since
// there's nothing on screen the overlay is "attached to". Kept as a separate export rather than
// making anchorEl optional on the call site, so the C# side's intent (targeted vs detached) is
// explicit at the call rather than inferred from a null.
export function registerLightDismissDetached(overlayId, popupEl, dotNetRef) {
    registerLightDismiss(overlayId, popupEl, null, dotNetRef);
}

export function unregisterLightDismiss(overlayId) {
    const handler = lightDismissHandlers.get(overlayId);
    if (handler) {
        document.removeEventListener("pointerdown", handler, true);
        lightDismissHandlers.delete(overlayId);
    }
}
