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
        if (!layer || !root.contains(layer)) {
            return null;
        }
        const left = Number(layer.getAttribute('data-ex-x'));
        const top = Number(layer.getAttribute('data-ex-y'));
        const panel = Number(layer.getAttribute('data-ex-panel'));
        if (!Number.isFinite(left) || !Number.isFinite(top) || !Number.isInteger(panel)) {
            return null;
        }
        return { x: left + event.offsetX, y: top + event.offsetY, panel };
    };

    const leave = () => {
        if (lastIndex === -1 || !core) {
            return;
        }
        lastIndex = -1;
        lastPanel = -1;
        core.invokeMethodAsync('OnPointerAsync', version, -1, -1, 0).catch(fail);
    };

    const onMove = (event) => {
        if (version < 0 || !core) {
            return;
        }
        const at = locate(event);
        if (!at) {
            if (!pressed) {
                leave();
            }
            return;
        }
        const index = nearest(at.x);
        if (index === lastIndex && at.panel === lastPanel) {
            return;
        }
        lastIndex = index;
        lastPanel = at.panel;
        core.invokeMethodAsync('OnPointerAsync', version, index, at.panel, at.y).catch(fail);
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
        const at = locate(event);
        core.invokeMethodAsync('OnReleaseAsync', version, at ? nearest(at.x) : -1).catch(fail);
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
