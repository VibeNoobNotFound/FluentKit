// Measures a tab button's position relative to its header container, in plain offsetLeft/offsetWidth
// terms (not getBoundingClientRect) so the result is unaffected by the header's own scroll position
// or viewport location — the C# side only needs a value it can hand straight to a CSS transform.
export function measureTab(headerEl, index) {
    const tab = headerEl.children[index];
    if (!tab) {
        return null;
    }

    return { left: tab.offsetLeft, width: tab.offsetWidth };
}
