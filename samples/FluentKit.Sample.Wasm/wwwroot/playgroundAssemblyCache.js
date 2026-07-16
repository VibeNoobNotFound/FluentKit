// Blazor WASM caches every boot resource it downloads (including the fingerprinted
// _framework/*.dll files) in the browser's Cache Storage API. Since .NET 10 removed
// blazor.boot.json (the manifest is now inlined into dotnet.js instead), there's no
// static file left to read to discover the *.dll fingerprinted filenames. Rather than
// guessing names or re-fetching over HTTP, we read the exact bytes Blazor itself already
// downloaded and cached at startup — no network round trip, no fingerprint matching.

export async function listCachedFrameworkAssemblies() {
    const urls = [];
    try {
        const cacheNames = await caches.keys();
        for (const cacheName of cacheNames) {
            const cache = await caches.open(cacheName);
            const requests = await cache.keys();
            for (const req of requests) {
                if (req.url.indexOf("/_framework/") !== -1 && req.url.endsWith(".dll")) {
                    urls.push(req.url);
                }
            }
        }
    } catch (e) {
        // Cache Storage API unavailable (e.g. private browsing) — caller falls back.
        console.warn("[Playground] Cache Storage read failed:", e);
    }
    return urls;
}

export async function getCachedAssemblyBytes(url) {
    try {
        const cacheNames = await caches.keys();
        for (const cacheName of cacheNames) {
            const cache = await caches.open(cacheName);
            const response = await cache.match(url);
            if (response) {
                const buffer = await response.arrayBuffer();
                return new Uint8Array(buffer);
            }
        }
    } catch (e) {
        console.warn("[Playground] Cache Storage read failed for", url, e);
    }
    return null;
}
