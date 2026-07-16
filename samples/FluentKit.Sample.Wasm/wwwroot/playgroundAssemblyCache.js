// .NET 10 removed blazor.boot.json as a separate static file — the boot manifest is now
// inlined directly into dotnet.js as a literal JSON blob (wrapped in /*json-start*/ ...
// /*json-end*/ markers) passed to `ft.withConfig({...})`. Rather than fetching a manifest
// file that no longer exists, we fetch dotnet.js itself (already loaded by the page, so
// it's guaranteed reachable) and read the fingerprinted assembly filenames straight out of
// its embedded config.

const JSON_START = "/*json-start*/";
const JSON_END = "/*json-end*/";

function findDotnetJsCandidates() {
    // Dynamic imports (which is how dotnet.js is loaded) show up in the Resource Timing
    // API even though they never appear as a <script> tag in the DOM.
    const fromTiming = performance.getEntriesByType("resource")
        .map(e => e.name)
        .filter(u =>
            /\/dotnet(\.[A-Za-z0-9_-]+)?\.js(\?.*)?$/.test(u) &&
            u.indexOf("dotnet.native") === -1 &&
            u.indexOf("dotnet.runtime") === -1);

    // Fallback in case the timing buffer was cleared or missed it: the conventional
    // unfingerprinted path also works on hosts that don't fingerprint this file.
    return [...new Set([...fromTiming, "_framework/dotnet.js"])];
}

export async function getFrameworkAssemblyManifest() {
    for (const url of findDotnetJsCandidates()) {
        try {
            const response = await fetch(url, { cache: "force-cache" });
            if (!response.ok) {
                continue;
            }

            const text = await response.text();
            const start = text.indexOf(JSON_START);
            const end = text.indexOf(JSON_END);
            if (start === -1 || end === -1) {
                continue;
            }

            const config = JSON.parse(text.substring(start + JSON_START.length, end));
            const fileNames = [];
            const buckets = ["coreAssembly", "assembly"];
            for (const bucket of buckets) {
                const entries = config.resources && config.resources[bucket];
                if (Array.isArray(entries)) {
                    for (const entry of entries) {
                        if (entry && typeof entry.name === "string" && entry.name.endsWith(".dll")) {
                            fileNames.push(entry.name);
                        }
                    }
                }
            }

            if (fileNames.length > 0) {
                return fileNames;
            }
        } catch (e) {
            console.warn("[Playground] Failed to read manifest from", url, e);
        }
    }

    return [];
}