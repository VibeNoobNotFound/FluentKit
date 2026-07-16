// Loaded via IJSRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/FluentKit/Theming/accent-interop.js")
//
// Two jobs:
//  1. applyAccentPalette — push the C#-computed ramp (see AccentPalette.ToCssVariables) onto
//     :root as CSS custom properties, so every component reading var(--accent-*) updates live.
//  2. extractDominantColor — sample a wallpaper/image's pixels client-side (canvas) and return a
//     single representative [r,g,b], which FluentKit.Theming.AccentColorService then turns into a
//     full ramp via AccentPalette.FromColor. This never touches the network itself; the <img> load
//     is what can fail (wrong URL, blocked by CORS) — callers should treat rejection as "fall back
//     to the default blue", not as a hard error.

export function applyAccentPalette(vars) {
    const root = document.documentElement.style;
    for (const key in vars) {
        if (Object.prototype.hasOwnProperty.call(vars, key)) {
            root.setProperty(key, vars[key]);
        }
    }
}

export function extractDominantColor(imageUrl) {
    return new Promise((resolve, reject) => {
        const img = new Image();
        // Needed so canvas pixel reads don't throw on cross-origin images that DO send
        // CORS headers (e.g. a same-origin dev server still counts as "cross origin" for
        // some blob/data URL edge cases in certain browsers). Images that don't send CORS
        // headers will still fail getImageData below — that's the "reject" path callers
        // should treat as "use the fallback blue".
        img.crossOrigin = "anonymous";

        img.onload = () => {
            try {
                // Downsample hard — we don't need per-pixel fidelity, just a representative
                // color, and a small canvas keeps this fast even for a 4K wallpaper.
                const size = 48;
                const canvas = document.createElement("canvas");
                canvas.width = size;
                canvas.height = size;
                const ctx = canvas.getContext("2d", { willReadFrequently: true });
                ctx.drawImage(img, 0, 0, size, size);

                const { data } = ctx.getImageData(0, 0, size, size);

                // Saturation-weighted average: a plain average tends toward muddy gray for
                // photographic wallpapers (skies, foliage, skin tones all partially cancel
                // out). Weighting each pixel by its own saturation lets vivid accent-worthy
                // pixels (a red door, a blue sky) dominate the result the way a human picking
                // an accent color from a photo would, while a pixel's own weight floor (0.15)
                // keeps fully desaturated images (grayscale wallpapers) from producing NaN.
                let rSum = 0, gSum = 0, bSum = 0, weightSum = 0;
                for (let i = 0; i < data.length; i += 4) {
                    const r = data[i], g = data[i + 1], b = data[i + 2], a = data[i + 3];
                    if (a < 128) continue; // skip transparent pixels

                    const max = Math.max(r, g, b);
                    const min = Math.min(r, g, b);
                    const saturation = max === 0 ? 0 : (max - min) / max;
                    const weight = 0.15 + saturation;

                    rSum += r * weight;
                    gSum += g * weight;
                    bSum += b * weight;
                    weightSum += weight;
                }

                if (weightSum === 0) {
                    reject("image had no readable pixels");
                    return;
                }

                resolve([
                    Math.round(rSum / weightSum),
                    Math.round(gSum / weightSum),
                    Math.round(bSum / weightSum),
                ]);
            } catch (err) {
                // Most commonly a tainted-canvas SecurityError from a cross-origin image
                // without CORS headers.
                reject(err && err.message ? err.message : "canvas read failed");
            }
        };

        img.onerror = () => reject("image failed to load");
        img.src = imageUrl;
    });
}
