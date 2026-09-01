// Loaded via IJSRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/FluentKit/Effects/mica-interop.js")
//
// Bakes Mica ONCE into a static <canvas> raster.
//
// High‑quality path: WebGL separable Gaussian blur with shader‑side Bayer dither.
//   - Adaptive kernel size (up to 127) prevents truncation artefacts.
//   - Bayer dither inside the fragment shader eliminates 8‑bit quantisation banding.
//   - True Gaussian, adjustable via `--mica-blur-amount`.
//
// Fallback: mip‑map blur + Floyd‑Steinberg dither (if WebGL unavailable).
//
// Output PNG, cached per image/variant. CSS tokens are the single source of truth.

const blurredCache = new Map(); // imageUrl → blurred (pre‑tint) canvas
const finalCache   = new Map(); // cacheKey  → final tinted data URL

// ---------- Utility functions ----------
function loadImage(url) {
    return new Promise((resolve, reject) => {
        const img = new Image();
        img.onload = () => resolve(img);
        img.onerror = reject;
        img.src = url;
    });
}

function cssNum(style, name, fallback) {
    const value = parseFloat(style.getPropertyValue(name));
    return Number.isFinite(value) ? value : fallback;
}

function drawCover(ctx, img, canvasWidth, canvasHeight) {
    const imageRatio = img.width / img.height;
    const canvasRatio = canvasWidth / canvasHeight;
    let drawWidth, drawHeight, drawX, drawY;
    if (imageRatio > canvasRatio) {
        drawHeight = canvasHeight;
        drawWidth  = canvasHeight * imageRatio;
        drawX      = (canvasWidth - drawWidth) / 2;
        drawY      = 0;
    } else {
        drawWidth  = canvasWidth;
        drawHeight = canvasWidth / imageRatio;
        drawX      = 0;
        drawY      = (canvasHeight - drawHeight) / 2;
    }
    ctx.drawImage(img, drawX, drawY, drawWidth, drawHeight);
}

function makeCanvas(w, h) {
    const c = document.createElement("canvas");
    c.width  = Math.max(1, Math.round(w));
    c.height = Math.max(1, Math.round(h));
    const ctx = c.getContext("2d");
    ctx.imageSmoothingEnabled = true;
    ctx.imageSmoothingQuality = "high";
    return { canvas: c, ctx };
}

// Floyd‑Steinberg dither (only used in mip‑map fallback)
function ditherCanvasFS(canvas) {
    const ctx = canvas.getContext("2d");
    const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
    const data = imageData.data;
    const w = canvas.width, h = canvas.height;
    for (let y = 0; y < h; y++) {
        for (let x = 0; x < w; x++) {
            const i = (y * w + x) * 4;
            const oldR = data[i], oldG = data[i + 1], oldB = data[i + 2];
            const newR = Math.round(oldR), newG = Math.round(oldG), newB = Math.round(oldB);
            data[i] = newR; data[i + 1] = newG; data[i + 2] = newB;
            const eR = oldR - newR, eG = oldG - newG, eB = oldB - newB;
            if (x + 1 < w) {
                const j = i + 4;
                data[j]     += eR * 7 / 16;
                data[j + 1] += eG * 7 / 16;
                data[j + 2] += eB * 7 / 16;
            }
            if (x - 1 >= 0 && y + 1 < h) {
                const j = ((y + 1) * w + (x - 1)) * 4;
                data[j]     += eR * 3 / 16;
                data[j + 1] += eG * 3 / 16;
                data[j + 2] += eB * 3 / 16;
            }
            if (y + 1 < h) {
                const j = ((y + 1) * w + x) * 4;
                data[j]     += eR * 5 / 16;
                data[j + 1] += eG * 5 / 16;
                data[j + 2] += eB * 5 / 16;
            }
            if (x + 1 < w && y + 1 < h) {
                const j = ((y + 1) * w + (x + 1)) * 4;
                data[j]     += eR * 1 / 16;
                data[j + 1] += eG * 1 / 16;
                data[j + 2] += eB * 1 / 16;
            }
        }
    }
    ctx.putImageData(imageData, 0, 0);
}

// ---------- Mip‑map fallback ----------
function blurredCoverCanvasFallback(img, canvasWidth, canvasHeight, blurPx) {
    const { canvas: baseCanvas, ctx: baseCtx } = makeCanvas(canvasWidth, canvasHeight);
    drawCover(baseCtx, img, canvasWidth, canvasHeight);

    const blurFactor = Math.max(2, blurPx / 3.2);
    const tinyW = Math.max(4, Math.round(canvasWidth / blurFactor));
    const tinyH = Math.max(4, Math.round(canvasHeight / blurFactor));

    let cur = baseCanvas, curW = canvasWidth, curH = canvasHeight;
    while (curW / 2 > tinyW && curH / 2 > tinyH) {
        const nextW = Math.max(tinyW, Math.round(curW / 2));
        const nextH = Math.max(tinyH, Math.round(curH / 2));
        const { canvas: stepCanvas, ctx: stepCtx } = makeCanvas(nextW, nextH);
        stepCtx.drawImage(cur, 0, 0, nextW, nextH);
        cur = stepCanvas; curW = nextW; curH = nextH;
    }
    const { canvas: tinyCanvas, ctx: tinyCtx } = makeCanvas(tinyW, tinyH);
    tinyCtx.drawImage(cur, 0, 0, tinyW, tinyH);

    cur = tinyCanvas; curW = tinyW; curH = tinyH;
    while (curW * 2 < canvasWidth && curH * 2 < canvasHeight) {
        const nextW = Math.min(canvasWidth, curW * 2);
        const nextH = Math.min(canvasHeight, curH * 2);
        const { canvas: stepCanvas, ctx: stepCtx } = makeCanvas(nextW, nextH);
        stepCtx.drawImage(cur, 0, 0, nextW, nextH);
        cur = stepCanvas; curW = nextW; curH = nextH;
    }

    const { canvas: outCanvas, ctx: outCtx } = makeCanvas(canvasWidth, canvasHeight);
    outCtx.drawImage(cur, 0, 0, canvasWidth, canvasHeight);

    ditherCanvasFS(outCanvas);
    return outCanvas;
}

// ---------- WebGL high‑quality path ----------
function getGLContext(canvas) {
    return canvas.getContext("webgl2", {
        alpha: false,
        antialias: false,
        preserveDrawingBuffer: true,
        premultipliedAlpha: false,
        powerPreference: "high-performance"
    }) || canvas.getContext("webgl", {
        alpha: false,
        antialias: false,
        preserveDrawingBuffer: true,
        premultipliedAlpha: false,
        powerPreference: "high-performance"
    });
}

function createShader(gl, type, source) {
    const shader = gl.createShader(type);
    gl.shaderSource(shader, source);
    gl.compileShader(shader);
    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
        const info = gl.getShaderInfoLog(shader);
        gl.deleteShader(shader);
        throw new Error("Shader compile error: " + info);
    }
    return shader;
}

function createProgram(gl, vertSrc, fragSrc) {
    const program = gl.createProgram();
    gl.attachShader(program, createShader(gl, gl.VERTEX_SHADER, vertSrc));
    gl.attachShader(program, createShader(gl, gl.FRAGMENT_SHADER, fragSrc));
    gl.linkProgram(program);
    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
        const info = gl.getProgramInfoLog(program);
        gl.deleteProgram(program);
        throw new Error("Program link error: " + info);
    }
    return program;
}

function createTexture(gl, width, height, data) {
    const tex = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, tex);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, width, height, 0, gl.RGBA, gl.UNSIGNED_BYTE, data || null);
    return tex;
}

function createFramebuffer(gl, tex) {
    const fb = gl.createFramebuffer();
    gl.bindFramebuffer(gl.FRAMEBUFFER, fb);
    gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, tex, 0);
    gl.bindFramebuffer(gl.FRAMEBUFFER, null);
    return fb;
}

function gaussianWeights(sigma, kernelSize) {
    const weights = new Float32Array(kernelSize);
    let sum = 0;
    const half = Math.floor(kernelSize / 2);
    for (let i = 0; i < kernelSize; i++) {
        const x = i - half;
        const w = Math.exp(-(x * x) / (2 * sigma * sigma));
        weights[i] = w;
        sum += w;
    }
    for (let i = 0; i < kernelSize; i++) weights[i] /= sum;
    return weights;
}

function blurredCoverCanvasWebGL(img, canvasWidth, canvasHeight, blurPx) {
    const sigma = Math.max(0.5, blurPx / 3.5);
    const kernelSize = Math.min(127,
        Math.max(5, Math.ceil(sigma * 3.5) * 2 + 1)
    );

    const glCanvas = document.createElement("canvas");
    glCanvas.width  = canvasWidth;
    glCanvas.height = canvasHeight;
    const gl = getGLContext(glCanvas);
    if (!gl) {
        console.warn("WebGL not available, falling back to mip‑map blur + dither");
        return blurredCoverCanvasFallback(img, canvasWidth, canvasHeight, blurPx);
    }

    gl.viewport(0, 0, canvasWidth, canvasHeight);
    gl.clearColor(0, 0, 0, 0);

    // Vertex shader
    const vertSrc = `
        attribute vec2 a_position;
        varying vec2 v_texCoord;
        void main() {
            gl_Position = vec4(a_position, 0.0, 1.0);
            v_texCoord = (a_position + 1.0) / 2.0;
        }
    `;

    // Pass‑through shader (unchanged)
    const passThroughFragSrc = `
        precision highp float;
        varying vec2 v_texCoord;
        uniform sampler2D u_image;
        void main() {
            gl_FragColor = texture2D(u_image, v_texCoord);
        }
    `;

    // ---------- WebGL 1.0 compatible Bayer dither (if‑else, no dynamic indexing) ----------
    const bayerDitherFunc = `
        float bayerDither(vec2 coord) {
            int x = int(mod(floor(coord.x), 4.0));
            int y = int(mod(floor(coord.y), 4.0));
            float v;

            if (y == 0) {
                if (x == 0) v = 0.0;
                else if (x == 1) v = 8.0;
                else if (x == 2) v = 2.0;
                else v = 10.0;
            } else if (y == 1) {
                if (x == 0) v = 12.0;
                else if (x == 1) v = 4.0;
                else if (x == 2) v = 14.0;
                else v = 6.0;
            } else if (y == 2) {
                if (x == 0) v = 3.0;
                else if (x == 1) v = 11.0;
                else if (x == 2) v = 1.0;
                else v = 9.0;
            } else {
                if (x == 0) v = 15.0;
                else if (x == 1) v = 7.0;
                else if (x == 2) v = 13.0;
                else v = 5.0;
            }
            // Scale to [-0.5/255, 0.5/255] – invisible but breaks banding
            return (v / 16.0 - 0.5) / 255.0;
        }
    `;

    // Blur fragment shader with dither
    const blurFragSrc = (direction, kernelSize) => {
        const weights = gaussianWeights(sigma, kernelSize);
        const half = Math.floor(kernelSize / 2);
        let sampleCode = "";
        for (let i = 0; i < kernelSize; i++) {
            const offset = i - half;
            const texOffset = direction === 'h'
                ? `vec2(${offset}.0 / u_textureSize.x, 0.0)`
                : `vec2(0.0, ${offset}.0 / u_textureSize.y)`;
            sampleCode += `  color += texture2D(u_image, v_texCoord + ${texOffset}) * ${weights[i].toFixed(7)};\n`;
        }
        return `
            precision highp float;
            varying vec2 v_texCoord;
            uniform sampler2D u_image;
            uniform vec2 u_textureSize;
            ${bayerDitherFunc}
            void main() {
                vec4 color = vec4(0.0);
                ${sampleCode}
                // Dither before writing to 8‑bit framebuffer – breaks banding
                color.r += bayerDither(gl_FragCoord.xy);
                color.g += bayerDither(gl_FragCoord.xy + 0.5);
                color.b += bayerDither(gl_FragCoord.xy + 1.0);
                gl_FragColor = clamp(color, 0.0, 1.0);
            }
        `;
    };

    const passThroughProgram = createProgram(gl, vertSrc, passThroughFragSrc);
    const hBlurProgram = createProgram(gl, vertSrc, blurFragSrc('h', kernelSize));
    const vBlurProgram = createProgram(gl, vertSrc, blurFragSrc('v', kernelSize));

    // Full‑screen quad
    const positions = new Float32Array([-1, -1, 1, -1, -1, 1, 1, 1]);
    const posBuffer = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, posBuffer);
    gl.bufferData(gl.ARRAY_BUFFER, positions, gl.STATIC_DRAW);

    function drawQuad(program, uniforms = {}) {
        gl.useProgram(program);
        const aPos = gl.getAttribLocation(program, "a_position");
        gl.enableVertexAttribArray(aPos);
        gl.bindBuffer(gl.ARRAY_BUFFER, posBuffer);
        gl.vertexAttribPointer(aPos, 2, gl.FLOAT, false, 0, 0);
        for (const [name, value] of Object.entries(uniforms)) {
            const loc = gl.getUniformLocation(program, name);
            if (loc) gl.uniform1i(loc, value);
        }
        gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
    }

    // Upload source image (cover‑fit)
    const { canvas: srcCanvas, ctx: srcCtx } = makeCanvas(canvasWidth, canvasHeight);
    drawCover(srcCtx, img, canvasWidth, canvasHeight);
    const srcImageData = srcCtx.getImageData(0, 0, canvasWidth, canvasHeight);
    const srcTex = createTexture(gl, canvasWidth, canvasHeight, srcImageData.data);

    // Ping‑pong textures
    const texA = createTexture(gl, canvasWidth, canvasHeight, null);
    const texB = createTexture(gl, canvasWidth, canvasHeight, null);
    const fbA  = createFramebuffer(gl, texA);
    const fbB  = createFramebuffer(gl, texB);

    // Pass 1: copy source → texA
    gl.bindFramebuffer(gl.FRAMEBUFFER, fbA);
    gl.activeTexture(gl.TEXTURE0);
    gl.bindTexture(gl.TEXTURE_2D, srcTex);
    drawQuad(passThroughProgram, { u_image: 0 });

    // Pass 2: horizontal blur texA → texB
    gl.bindFramebuffer(gl.FRAMEBUFFER, fbB);
    gl.activeTexture(gl.TEXTURE0);
    gl.bindTexture(gl.TEXTURE_2D, texA);
    gl.useProgram(hBlurProgram);
    gl.uniform2f(gl.getUniformLocation(hBlurProgram, "u_textureSize"), canvasWidth, canvasHeight);
    drawQuad(hBlurProgram, { u_image: 0 });

    // Pass 3: vertical blur texB → texA
    gl.bindFramebuffer(gl.FRAMEBUFFER, fbA);
    gl.activeTexture(gl.TEXTURE0);
    gl.bindTexture(gl.TEXTURE_2D, texB);
    gl.useProgram(vBlurProgram);
    gl.uniform2f(gl.getUniformLocation(vBlurProgram, "u_textureSize"), canvasWidth, canvasHeight);
    drawQuad(vBlurProgram, { u_image: 0 });

    // Read back final result
    const outPixelData = new Uint8Array(canvasWidth * canvasHeight * 4);
    gl.bindFramebuffer(gl.FRAMEBUFFER, fbA);
    gl.readPixels(0, 0, canvasWidth, canvasHeight, gl.RGBA, gl.UNSIGNED_BYTE, outPixelData);

    const { canvas: outCanvas, ctx: outCtx } = makeCanvas(canvasWidth, canvasHeight);
    const outImageData = outCtx.createImageData(canvasWidth, canvasHeight);
    outImageData.data.set(outPixelData);
    outCtx.putImageData(outImageData, 0, 0);

    // Clean up WebGL resources
    gl.deleteTexture(srcTex);
    gl.deleteTexture(texA);
    gl.deleteTexture(texB);
    gl.deleteFramebuffer(fbA);
    gl.deleteFramebuffer(fbB);
    gl.deleteProgram(passThroughProgram);
    gl.deleteProgram(hBlurProgram);
    gl.deleteProgram(vBlurProgram);
    gl.deleteBuffer(posBuffer);

    return outCanvas;
}

// ---------- Public blur function ----------
function blurredCoverCanvas(img, canvasWidth, canvasHeight, blurPx) {
    return blurredCoverCanvasWebGL(img, canvasWidth, canvasHeight, blurPx);
}

// ---------- Main entry point ----------
export async function renderMica(cacheKey, imageUrl, isBase) {
    const cachedFinal = finalCache.get(cacheKey);
    if (cachedFinal) return cachedFinal;

    const style = getComputedStyle(document.documentElement);
    const tint = (isBase
        ? style.getPropertyValue("--mica-alt-tint-color")
        : style.getPropertyValue("--mica-tint-color")).trim();
    const tintOpacity       = cssNum(style, isBase ? "--mica-alt-tint-opacity" : "--mica-tint-opacity", 0.8);
    const luminosityOpacity = cssNum(style, "--mica-luminosity-opacity", 1.0);
    const blurPx            = cssNum(style, "--mica-blur-amount", 56);

    let blurred = blurredCache.get(imageUrl);
    if (!blurred) {
        const img = await loadImage(imageUrl);
        const maxDim = 1280;
        const vw = window.innerWidth || 1600;
        const vh = window.innerHeight || 900;
        const scale = Math.min(1, maxDim / Math.max(vw, vh));
        const canvasWidth  = Math.max(64, Math.round(vw * scale));
        const canvasHeight = Math.max(64, Math.round(vh * scale));

        blurred = blurredCoverCanvas(img, canvasWidth, canvasHeight, blurPx);
        blurredCache.set(imageUrl, blurred);
    }

    const { canvas: outCanvas, ctx: outCtx } = makeCanvas(blurred.width, blurred.height);
    outCtx.drawImage(blurred, 0, 0);

    outCtx.globalCompositeOperation = "luminosity";
    outCtx.fillStyle = `rgba(${tint}, ${luminosityOpacity})`;
    outCtx.fillRect(0, 0, outCanvas.width, outCanvas.height);

    outCtx.globalCompositeOperation = "color";
    outCtx.fillStyle = `rgba(${tint}, ${tintOpacity})`;
    outCtx.fillRect(0, 0, outCanvas.width, outCanvas.height);

    const dataUrl = outCanvas.toDataURL("image/png");
    finalCache.set(cacheKey, dataUrl);
    return dataUrl;
}

// Server-safe entry point. The baked PNG can be hundreds of kilobytes, so never return it through
// Blazor Server's SignalR circuit. Keep it in this browser-side cache and paint the already-mounted
// wallpaper element directly instead.
export async function renderMicaInto(element, cacheKey, imageUrl, isBase) {
    if (!element) return false;

    const dataUrl = await renderMica(cacheKey, imageUrl, isBase);
    if (!dataUrl) return false;

    element.style.backgroundImage = `url("${dataUrl}")`;
    return true;
}
