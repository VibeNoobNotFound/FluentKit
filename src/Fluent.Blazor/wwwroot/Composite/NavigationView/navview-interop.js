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

export function getElementWidth(el) {
    if (!el) {
        return 800;
    }

    return el.getBoundingClientRect().width;
}
