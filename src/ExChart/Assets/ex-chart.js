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
    // A scatter's marks (ADR-0290), as x, y and panel in turn, how near the pointer must be to one,
    // and the lattice a drag is reported on; null on a chart of lines.
    let marks = null;
    let reach = 0;
    let cell = 4;
    let lastCell = '';
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

    // The mark nearest the point, in its panel and within reach, or -1. It reads only the positions
    // .NET handed over with the drawing; nothing is measured (ADR-0255).
    const nearestMark = (point) => {
        let best = -1;
        let bestDistance = reach * reach;
        for (let i = 0; i < marks.length; i += 3) {
            if (marks[i + 2] !== point.panel) {
                continue;
            }
            const dx = marks[i] - point.x;
            const dy = marks[i + 1] - point.y;
            const distance = dx * dx + dy * dy;
            if (distance <= bestDistance) {
                best = i / 3;
                bestDistance = distance;
            }
        }
        return best;
    };
    const snap = (value) => (Math.floor(value / cell) + 0.5) * cell;
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
        if (marks && pressed) {
            const key = `${Math.floor(point.x / cell)},${Math.floor(point.y / cell)}`;
            if (key !== lastCell) {
                lastCell = key;
                core.invokeMethodAsync('OnDragAsync', version, snap(point.x), snap(point.y)).catch(fail);
            }
        }
        const index = marks ? nearestMark(point) : nearest(point.x);
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
        lastCell = `${Math.floor(at.x / cell)},${Math.floor(at.y / cell)}`;
        event.target.setPointerCapture?.(event.pointerId);
        core.invokeMethodAsync('OnPressAsync', version, marks ? nearestMark(at) : nearest(at.x), snap(at.x), snap(at.y), at.panel).catch(fail);
    };

    const onUp = (event) => {
        if (!pressed || !core) {
            return;
        }
        pressed = false;
        const point = locate(event);
        over = point ? { layer: event.target.closest('.ex-chart-hit'), x: event.offsetX, y: event.offsetY } : null;
        const index = point ? (marks ? nearestMark(point) : nearest(point.x)) : -1;
        core.invokeMethodAsync('OnReleaseAsync', version, index, point ? snap(point.x) : -1, point ? snap(point.y) : -1).catch(fail);
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
        /**
         * A new drawing: its version and its positions' offsets, left to right; on a scatter, its marks
         * as x, y and panel in turn, how near the pointer must come to one, and the drag's lattice.
         */
        draw(newVersion, newXs, newMarks, newReach, newCell) {
            version = newVersion;
            xs = Array.isArray(newXs) ? newXs : [];
            marks = Array.isArray(newMarks) ? newMarks : null;
            reach = Number.isFinite(newReach) ? newReach : 0;
            cell = Number.isFinite(newCell) && newCell > 0 ? newCell : 4;
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
