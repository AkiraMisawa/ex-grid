// ExChart's pointer entry: the eleventh use the JavaScript allowlist permits (ADR-0021, ADR-0255).
//
// One listener set per chart, on the chart's own root, never on document. It reports the position
// the pointer is over only when the pointer moves onto ANOTHER position, and when it leaves; and,
// for a drag across the plot (ADR-0273), the press and the release. Nothing is measured: the
// positions' pixel offsets are computed in C# and handed over with each drawing, with the
// drawing's version; the plot an offset is relative to is the hit layer the pointer is over, whose
// own offsets C# wrote on it as text. Every report carries the version it was taken against, and
// C# drops one taken against an earlier drawing (spine rule 6). Blazor's @onmousemove would cross
// to .NET on every event, a wire round trip each on Server.

// The files entry (ADR-0282), the fifteenth use: two exported functions, each run once on a click
// and never on a render, adding no listener and reading no layout (the SVG carries its own size).

/**
 * Paints an SVG, written in C# on the click, to PNG, and returns its bytes. The canvas is the
 * drawing's size at the screen's Device Pixel, so a PNG taken at 150% is drawn at 150% and not
 * scaled up from 100% (ADR-0090). The browser lays out its text in the fonts the screen shows.
 */
export async function paint(svgText, width, height) {
    const scale = window.devicePixelRatio > 0 ? window.devicePixelRatio : 1;
    const url = URL.createObjectURL(new Blob([svgText], { type: 'image/svg+xml' }));
    try {
        const image = new Image();
        image.decoding = 'sync';
        await new Promise((resolve, reject) => {
            image.onload = resolve;
            image.onerror = () => reject(new Error('the chart\'s SVG could not be painted'));
            image.src = url;
        });
        const canvas = document.createElement('canvas');
        canvas.width = Math.round(width * scale);
        canvas.height = Math.round(height * scale);
        const context = canvas.getContext('2d');
        context.scale(canvas.width / width, canvas.height / height);
        context.drawImage(image, 0, 0, width, height);
        const blob = await new Promise((resolve) => canvas.toBlob(resolve, 'image/png'));
        if (!blob) {
            throw new Error('the chart\'s PNG could not be encoded');
        }
        return new Uint8Array(await blob.arrayBuffer());
    } finally {
        URL.revokeObjectURL(url);
    }
}

/**
 * Saves bytes under a file name, through a Blob and an object URL revoked once the save has
 * started: one click saves (ADR-0282).
 */
export function save(name, type, bytes) {
    const url = URL.createObjectURL(new Blob([bytes], { type }));
    const link = document.createElement('a');
    link.href = url;
    link.download = name;
    link.style.display = 'none';
    document.body.appendChild(link);
    try {
        link.click();
    } finally {
        link.remove();
        setTimeout(() => URL.revokeObjectURL(url), 0);
    }
}

/** The SHA-256 of this source, which the build writes in; the chart refuses a module of another version (ADR-0123). */
export function stamp() {
    return typeof EX_SOURCE_SHA256 === 'string' ? EX_SOURCE_SHA256 : '';
}

/**
 * Attaches the pointer entry to one chart's root and returns its handle. `core` is the chart's
 * .NET reference, which takes OnPointerAsync, OnPressAsync and OnReleaseAsync.
 */
export function attach(root, core) {
    let version = -1;
    let xs = [];
    let lastIndex = -1;
    let lastPanel = -1;
    let pressed = false;
    // Where the pointer last was, as the browser told it: the layer it was over and its offsets
    // there. A new drawing re-reports it, so a pointer at rest is answered against the drawing
    // shown, whichever of the move and the drawing reached the page first (spine rule 6).
    let over = null;

    // The position nearest an offset: positions are sorted left to right, so the boundary between
    // two is halfway between them.
    const nearest = (x) => {
        let lo = 0;
        let hi = xs.length - 1;
        if (hi < 0) {
            return -1;
        }
        while (lo < hi) {
            const mid = (lo + hi) >> 1;
            if (x < (xs[mid] + xs[mid + 1]) / 2) {
                hi = mid;
            } else {
                lo = mid + 1;
            }
        }
        return lo;
    };

    const fail = (error) => {
        if (core) {
            console.error('[ex-chart] the chart failed to take a pointer report', error);
        }
    };

    // Where on the drawing the pointer is: the hit layer's own offsets, which C# wrote, plus the
    // event's offsets within it. During a drag the pointer is captured, so the target stays the
    // layer the press was on even when the pointer has left it.
    const locate = (event) => {
        const layer = event.target instanceof Element ? event.target.closest('.ex-chart-hit') : null;
        return layer ? at(layer, event.offsetX, event.offsetY) : null;
    };

    const at = (layer, offsetX, offsetY) => {
        if (!layer.isConnected || !root.contains(layer)) {
            return null;
        }
        const left = Number(layer.getAttribute('data-ex-x'));
        const top = Number(layer.getAttribute('data-ex-y'));
        const panel = Number(layer.getAttribute('data-ex-panel'));
        if (!Number.isFinite(left) || !Number.isFinite(top) || !Number.isInteger(panel)) {
            return null;
        }
        return { x: left + offsetX, y: top + offsetY, panel };
    };

    const leave = () => {
        over = null;
        if (lastIndex === -1 || !core) {
            return;
        }
        lastIndex = -1;
        lastPanel = -1;
        core.invokeMethodAsync('OnPointerAsync', version, -1, -1, 0).catch(fail);
    };

    const report = (point) => {
        const index = nearest(point.x);
        if (index === lastIndex && point.panel === lastPanel) {
            return;
        }
        lastIndex = index;
        lastPanel = point.panel;
        core.invokeMethodAsync('OnPointerAsync', version, index, point.panel, point.y).catch(fail);
    };

    const onMove = (event) => {
        if (!core) {
            return;
        }
        const point = locate(event);
        if (!point) {
            if (!pressed) {
                leave();
            }
            return;
        }
        if (!pressed) {
            over = { layer: event.target.closest('.ex-chart-hit'), x: event.offsetX, y: event.offsetY };
        }
        if (version >= 0) {
            report(point);
        }
    };

    const onDown = (event) => {
        if (event.button !== 0 || version < 0 || !core) {
            return;
        }
        const at = locate(event);
        if (!at) {
            return;
        }
        pressed = true;
        event.target.setPointerCapture?.(event.pointerId);
        core.invokeMethodAsync('OnPressAsync', version, nearest(at.x)).catch(fail);
    };

    const onUp = (event) => {
        if (!pressed || !core) {
            return;
        }
        pressed = false;
        const point = locate(event);
        over = point ? { layer: event.target.closest('.ex-chart-hit'), x: event.offsetX, y: event.offsetY } : null;
        core.invokeMethodAsync('OnReleaseAsync', version, point ? nearest(point.x) : -1).catch(fail);
    };

    const onLeave = () => {
        if (!pressed) {
            leave();
        }
    };

    root.addEventListener('pointermove', onMove);
    root.addEventListener('pointerdown', onDown);
    root.addEventListener('pointerup', onUp);
    root.addEventListener('pointercancel', onUp);
    root.addEventListener('pointerleave', onLeave);

    return {
        /** A new drawing: its version and its positions' offsets, left to right. */
        draw(newVersion, newXs) {
            version = newVersion;
            xs = Array.isArray(newXs) ? newXs : [];
            lastIndex = -1;
            lastPanel = -1;
            const point = over && !pressed && core ? at(over.layer, over.x, over.y) : null;
            if (point && version >= 0) {
                report(point);
            }
        },
        dispose() {
            root.removeEventListener('pointermove', onMove);
            root.removeEventListener('pointerdown', onDown);
            root.removeEventListener('pointerup', onUp);
            root.removeEventListener('pointercancel', onUp);
            root.removeEventListener('pointerleave', onLeave);
            core = null;
        },
    };
}
