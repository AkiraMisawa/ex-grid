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

// The files entry (ADR-0282), the fifteenth use: its three jobs (read the tokens, paint to PNG, save),
// each run once on a click and never on a render, adding no listener and reading no layout (the SVG
// carries its own size). A PNG is painted and saved here, never handed back to .NET: on Server its
// bytes would be one message from the browser, larger than a circuit takes, and the circuit would close.

/**
 * The values the chart's Visual Tokens have on screen, read from its root's computed style on the
 * click, so a saved file has the theme's colours: a file has no page to inherit them from. `names`
 * are the tokens' names without their prefix; the last value returned is the root's font family.
 */
export function tokens(root, names) {
    const style = getComputedStyle(root);
    return [...names.map((name) => style.getPropertyValue(`--_ex-chart-${name}`).trim()), style.fontFamily];
}

/**
 * Paints an SVG, written in C# on the click, to PNG, and returns its bytes. The canvas is the
 * drawing's size at the screen's Device Pixel, so a PNG taken at 150% is drawn at 150% and not
 * scaled up from 100% (ADR-0090). The browser lays out its text in the fonts the screen shows.
 */
async function paint(svgText, width, height) {
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

/** Paints an SVG to PNG and saves it under a file name (ADR-0282). */
export async function savePng(name, svgText, width, height) {
    save(name, 'image/png', await paint(svgText, width, height));
}

/**
 * Saves a 3D surface's drawing as it stands, as SVG or PNG: `drawing` is the surface's handle, whose
 * svgText the 3D entry writes (ADR-0255). The text stays in the browser. False when it has no drawing.
 */
export async function saveDrawing(drawing, name, format, width, height) {
    const svgText = drawing?.svgText();
    if (!svgText) {
        return false;
    }
    if (format === 'png') {
        await savePng(name, svgText, width, height);
    } else {
        save(name, 'image/svg+xml', new TextEncoder().encode(svgText));
    }
    return true;
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
    // A scatter's marks (ADR-0290), as x, y and panel in turn, how near the pointer must be to one,
    // and the lattice a drag is reported on; null on a chart of lines.
    let marks = null;
    // Its edges (ADR-0321), as the two ends and the panel in turn, found where no mark is in reach.
    let edges = null;
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

    // The mark nearest the point, in its panel and within reach; where none is, the nearest edge in
    // reach, counted after the marks (ADR-0321); or -1. It reads only the positions .NET handed over
    // with the drawing; nothing is measured (ADR-0255).
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
        if (best >= 0 || !edges) {
            return best;
        }
        for (let i = 0; i < edges.length; i += 5) {
            if (edges[i + 4] !== point.panel) {
                continue;
            }
            const [x1, y1, x2, y2] = [edges[i], edges[i + 1], edges[i + 2], edges[i + 3]];
            const length = (x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1);
            const t = length === 0 ? 0 : Math.max(0, Math.min(1, ((point.x - x1) * (x2 - x1) + (point.y - y1) * (y2 - y1)) / length));
            const dx = x1 + t * (x2 - x1) - point.x;
            const dy = y1 + t * (y2 - y1) - point.y;
            const distance = dx * dx + dy * dy;
            if (distance <= bestDistance) {
                best = marks.length / 3 + i / 5;
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
         * as x, y and panel in turn, how near the pointer must come to one, the drag's lattice, and its
         * edges as the two ends and the panel in turn.
         */
        draw(newVersion, newXs, newMarks, newReach, newCell, newEdges) {
            version = newVersion;
            xs = Array.isArray(newXs) ? newXs : [];
            marks = Array.isArray(newMarks) ? newMarks : null;
            edges = marks && Array.isArray(newEdges) ? newEdges : null;
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

// ---- ExChart's 3D entry: the fourteenth use the JavaScript allowlist permits (ADR-0021, ADR-0255,
// ADR-0274).
//
// Turning a surface follows the pointer at the frame rate, which a round trip per frame cannot. .NET
// decides the mesh — its labels, its values and holes, its vertical scale and ticks, each face's
// colour band — and hands it over whole with its version. This entry only projects it, orders the
// faces far to near, fits the drawing to its box and draws it as SVG elements of its own box. It
// formats no number and measures nothing: positions are the events' own offsets on the box, and
// colours are Visual Tokens the stylesheet resolves. It reports, with the mesh's version, the grid
// point nearest the pointer when it changes (the tooltip), the tenor or strike a click fell on (the
// slice), and the view — angles, zoom and centre — when a gesture ends, so the view is the
// Consumer's (ADR-0272). A report against an earlier mesh is dropped in C# (spine rule 6).

const SVG_NS = 'http://www.w3.org/2000/svg';

/** How far, in CSS pixels, the pointer may be from a grid point and still read it. */
const SURFACE_REACH = 32;

/** How far a press may move and still be a click, not a turn. */
const SURFACE_CLICK = 4;

/** How far a drag turns the surface: a whole turn for a drag the box's height, either way (as Plotly's turntable and three.js's OrbitControls do). */
const SURFACE_TURN = 360;

/** How far one wheel step of 100 pixels zooms, and the wheel zooms in proportion to how far it moved. */
const SURFACE_WHEEL = Math.log(1.1) / 100;

/**
 * Attaches the 3D entry to one surface's box and returns its handle. `core` is the surface's .NET
 * reference, which takes OnPointAsync, OnSliceAsync and OnViewAsync.
 */
export function attachSurface(box, core) {
    const svg = document.createElementNS(SVG_NS, 'svg');
    svg.setAttribute('class', 'ex-chart-surface-svg');
    svg.setAttribute('aria-hidden', 'true');
    box.appendChild(svg);

    let version = -1;
    let mesh = null;
    let view = { yaw: -35, pitch: 28, zoom: 1, panX: 0, panY: 0 };
    let viewRevision = -1;
    let slice = null;
    let width = 0;
    let height = 0;
    let points = [];
    let lines = { rows: [], columns: [] };
    let steps = null;
    let drag = null;
    let lastPoint = '';

    // The view is reported latest-wins with one report in flight, so the last view always arrives
    // whatever the round trip, and a burst of wheel steps costs no burst of crossings.
    let sending = false;
    let unsent = false;
    const sendView = () => {
        if (!core || version < 0) {
            return;
        }
        if (sending) {
            unsent = true;
            return;
        }
        sending = true;
        unsent = false;
        core.invokeMethodAsync('OnViewAsync', version, view.yaw, view.pitch, view.zoom, view.panX, view.panY)
            .catch(fail)
            .finally(() => {
                sending = false;
                if (unsent) {
                    sendView();
                }
            });
    };

    const fail = (error) => {
        if (core) {
            console.error('[ex-chart] the surface failed to take a report', error);
        }
    };

    const redraw = () => {
        const drawn = drawSurface(svg, mesh, view, slice, width, height);
        points = drawn.points;
        lines = drawn.lines;
        steps = drawn.steps;
    };

    const nearest = (x, y) => {
        let best = null;
        let bestDistance = SURFACE_REACH * SURFACE_REACH;
        for (const p of points) {
            const d = (p.x - x) ** 2 + (p.y - y) ** 2;
            if (d <= bestDistance) {
                best = p;
                bestDistance = d;
            }
        }
        return best;
    };

    const reportPoint = (point, x, y) => {
        const key = point ? String(point.k) : '';
        if (key === lastPoint || !core || version < 0) {
            return;
        }
        lastPoint = key;
        core.invokeMethodAsync('OnPointAsync', version, point ? point.k : -1, x, y).catch(fail);
    };

    // The positions are the event's offsets on the box itself: the drawing takes no pointer events,
    // so the box is always the target.
    const own = (event) => event.target === box;

    const onDown = (event) => {
        // One pointer at a time: a second finger, or a second button, does not join the gesture.
        if (drag || (event.button !== 0 && event.button !== 2) || !mesh || !own(event)) {
            return;
        }
        drag = { id: event.pointerId, x: event.offsetX, y: event.offsetY, view: { ...view }, pan: event.shiftKey || event.button === 2, moved: false, version };
        box.setPointerCapture?.(event.pointerId);
    };

    const onMove = (event) => {
        if (drag && event.pointerId !== drag.id) {
            return;
        }
        if (drag) {
            const dx = event.offsetX - drag.x;
            const dy = event.offsetY - drag.y;
            if (!drag.moved && dx * dx + dy * dy < SURFACE_CLICK * SURFACE_CLICK) {
                return;
            }
            drag.moved = true;
            if (drag.pan) {
                view = { ...drag.view, panX: drag.view.panX + dx / width, panY: drag.view.panY + dy / height };
            } else {
                // The surface is held where it was pressed: a drag to the right brings its near side
                // to the right, a drag down tips its top toward the viewer.
                const degrees = SURFACE_TURN / Math.max(1, height);
                view = { ...drag.view, yaw: turn(drag.view.yaw + dx * degrees), pitch: clamp(drag.view.pitch + dy * degrees, -90, 90) };
            }
            reportPoint(null, 0, 0);
            redraw();
            return;
        }
        if (!own(event)) {
            return;
        }
        reportPoint(nearest(event.offsetX, event.offsetY), event.offsetX, event.offsetY);
    };

    const onUp = (event) => {
        if (drag && event.pointerId !== drag.id) {
            return;
        }
        const pressed = drag;
        drag = null;
        if (!pressed || pressed.version !== version) {
            return;
        }
        if (pressed.moved) {
            sendView();
            return;
        }
        if (pressed.pan) {
            return;
        }
        const point = nearest(pressed.x, pressed.y);
        if (point && core) {
            const axis = sliceAxis(point, pressed.x, pressed.y);
            core.invokeMethodAsync('OnSliceAsync', version, axis, point.k).catch(fail);
        }
    };

    const onCancel = (event) => {
        if (drag && event.pointerId !== drag.id) {
            return;
        }
        if (drag?.moved) {
            sendView();
        }
        drag = null;
    };

    const onLeave = () => {
        if (!drag) {
            reportPoint(null, 0, 0);
        }
    };

    // A click on a grid point falls on two lines, a tenor's and a strike's; the one nearer the
    // pointer is the one it fell on. A tenor's line runs along x, so a click on it slices that
    // tenor (axis 'y' held fixed). A mesh's node has no grid line through it: the lines are those a
    // step along x and along y draws through it, and the slice cuts its triangles (ADR-0311).
    const sliceAxis = (point, x, y) => {
        const nx = lines.columns.length;
        if (nx > 0) {
            const row = distanceToLine(lines.rows[Math.floor(point.k / nx)], x, y);
            const column = distanceToLine(lines.columns[point.k % nx], x, y);
            return column < row ? 'x' : 'y';
        }
        const off = (d) => Math.abs((x - point.x) * d[1] - (y - point.y) * d[0]) / (Math.hypot(d[0], d[1]) || 1);
        return off(steps.y) < off(steps.x) ? 'x' : 'y';
    };

    const onKey = (event) => {
        if (!mesh || event.altKey || event.ctrlKey || event.metaKey) {
            return;
        }
        const step = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] }[event.key];
        if (step && event.shiftKey) {
            view = { ...view, panX: view.panX + step[0] * 0.05, panY: view.panY + step[1] * 0.05 };
        } else if (step) {
            // An arrow turns the surface as a drag the same way would.
            view = { ...view, yaw: turn(view.yaw + step[0] * 5), pitch: clamp(view.pitch + step[1] * 5, -90, 90) };
        } else if (event.key === '+' || event.key === '=') {
            view = { ...view, zoom: clamp(view.zoom * 1.25, 0.5, 8) };
        } else if (event.key === '-') {
            view = { ...view, zoom: clamp(view.zoom / 1.25, 0.5, 8) };
        } else {
            return;
        }
        event.preventDefault();
        redraw();
        sendView();
    };

    const onWheel = (event) => {
        if (!mesh || event.deltaY === 0) {
            return;
        }
        event.preventDefault();
        // A trackpad sends many small steps and a mouse a few large ones; each zooms by how far it
        // moved, in pixels whatever the event's unit.
        const pixels = event.deltaY * (event.deltaMode === 1 ? 16 : event.deltaMode === 2 ? Math.max(1, height) : 1);
        view = { ...view, zoom: clamp(view.zoom * Math.exp(-pixels * SURFACE_WHEEL), 0.5, 8) };
        redraw();
        sendView();
    };

    // The right button pans, so it opens no menu over the drawing.
    const onMenu = (event) => {
        if (mesh && own(event)) {
            event.preventDefault();
        }
    };

    box.addEventListener('pointerdown', onDown);
    box.addEventListener('pointermove', onMove);
    box.addEventListener('pointerup', onUp);
    box.addEventListener('pointercancel', onCancel);
    box.addEventListener('pointerleave', onLeave);
    box.addEventListener('keydown', onKey);
    box.addEventListener('wheel', onWheel, { passive: false });
    box.addEventListener('contextmenu', onMenu);

    return {
        /**
         * A new drawing: its version, the mesh, the view C# holds with its revision, the slice, and
         * the box's size. A gesture begun against another mesh is ended unreported (ADR-0272).
         */
        draw(newVersion, newMesh, newView, newViewRevision, newSlice, newWidth, newHeight) {
            if (newVersion !== version) {
                drag = null;
                lastPoint = '';
            }
            // The view C# holds is taken only when C# changed it — a Consumer's parameter, a reset —
            // which bumps its revision. Otherwise it is an echo of a report, and the view here, where
            // the keys and the drags land, is as new or newer: taking it would undo a key pressed
            // while the report crossed (spine rule 6).
            if (newViewRevision !== viewRevision) {
                viewRevision = newViewRevision;
                view = { ...newView };
                drag = null;
            }
            version = newVersion;
            mesh = newMesh;
            slice = newSlice;
            width = newWidth;
            height = newHeight;
            redraw();
        },
        /**
         * The drawing as it stands, as SVG text that carries its own size and its colours resolved,
         * for a file (ADR-0282): painted off the page, it has no stylesheet to read.
         */
        /** The drawing's text as UTF-8 bytes, which cross to .NET as a stream whatever its size. */
        svgBytes() {
            const text = this.svgText();
            return text ? new TextEncoder().encode(text) : null;
        },
        svgText() {
            const copy = svg.cloneNode(true);
            copy.setAttribute('xmlns', SVG_NS);
            copy.setAttribute('width', String(width));
            copy.setAttribute('height', String(height));
            copy.setAttribute('viewBox', `0 0 ${width} ${height}`);
            copy.removeAttribute('aria-hidden');
            const from = svg.querySelectorAll('*');
            const to = copy.querySelectorAll('*');
            const rootStyle = getComputedStyle(svg);
            copy.setAttribute('style', `font-family:${rootStyle.fontFamily};font-size:${rootStyle.fontSize};background:${getComputedStyle(box).backgroundColor}`);
            for (let k = 0; k < from.length; k++) {
                const s = getComputedStyle(from[k]);
                const props = ['fill', 'fill-opacity', 'stroke', 'stroke-width', 'stroke-opacity', 'opacity', 'paint-order', 'stroke-linejoin', 'font-weight'];
                to[k].setAttribute('style', props.map((p) => `${p}:${s.getPropertyValue(p)}`).join(';'));
                to[k].removeAttribute('class');
            }
            return new XMLSerializer().serializeToString(copy);
        },
        dispose() {
            box.removeEventListener('pointerdown', onDown);
            box.removeEventListener('pointermove', onMove);
            box.removeEventListener('pointerup', onUp);
            box.removeEventListener('pointercancel', onCancel);
            box.removeEventListener('pointerleave', onLeave);
            box.removeEventListener('keydown', onKey);
            box.removeEventListener('wheel', onWheel);
            box.removeEventListener('contextmenu', onMenu);
            svg.remove();
            core = null;
        },
    };
}

const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));

/** An angle brought into (-180, 180]. */
const turn = (degrees) => {
    const d = ((degrees % 360) + 360) % 360;
    return d > 180 ? d - 360 : d;
};

const distanceToLine = (line, x, y) => {
    if (!line || line.length === 0) {
        return Infinity;
    }
    let best = Infinity;
    for (let k = 0; k < line.length; k++) {
        const p = line[k];
        const q = line[k + 1];
        if (!q) {
            best = Math.min(best, Math.hypot(p[0] - x, p[1] - y));
            continue;
        }
        const dx = q[0] - p[0];
        const dy = q[1] - p[1];
        const length = dx * dx + dy * dy;
        const t = length === 0 ? 0 : clamp(((x - p[0]) * dx + (y - p[1]) * dy) / length, 0, 1);
        best = Math.min(best, Math.hypot(p[0] + t * dx - x, p[1] + t * dy - y));
    }
    return best;
};

const svgElement = (name, attrs, text) => {
    const e = document.createElementNS(SVG_NS, name);
    for (const [k, v] of Object.entries(attrs)) {
        e.setAttribute(k, v);
    }
    if (text !== undefined) {
        e.textContent = text;
    }
    return e;
};

const f1 = (v) => v.toFixed(1);

// The strip at the right of a surface's drawing that holds its colour bar (ADR-0310).
const SURFACE_BAR = 80;

// Draws the mesh into the svg and returns the points where they were projected, each grid line
// through them, and how a step along x and along y is projected, for the reports. A hole is a hole:
// a face with a hole at a corner is not drawn, the hole has no point, and the lines through it break
// there. Where each point lies on the floor and on the vertical scale, and each face's colour band,
// are .NET's (ADR-0255, ADR-0312); this projects, orders and draws them.
function drawSurface(svg, mesh, view, slice, W, H) {
    svg.setAttribute('width', String(W));
    svg.setAttribute('height', String(H));
    svg.setAttribute('viewBox', `0 0 ${W} ${H}`);
    svg.replaceChildren();
    const none = { points: [], lines: { rows: [], columns: [] }, steps: null };
    if (!mesh || mesh.nodes.length === 0 || W <= SURFACE_BAR || H <= 0) {
        return none;
    }
    const nodes = mesh.nodes;
    const { zMin, zMax } = mesh;
    const plotW = W - SURFACE_BAR;
    const a = (view.yaw * Math.PI) / 180;
    const b = (view.pitch * Math.PI) / 180;
    // World: x across the floor, y along it, z up; each within [-0.5, 0.5], z squashed.
    const world = (x, y, z) => [x - 0.5, y - 0.5, ((z - zMin) / (zMax - zMin || 1) - 0.5) * 0.6];
    // Turned by the yaw about the vertical, then seen from the front (−y) and above by the pitch,
    // or below where it is negative: screen x, screen y down, and depth, larger farther. The axes
    // stay right-handed, x to y counterclockwise seen from above, so the picture is never a mirror.
    const raw = ([x, y, z]) => {
        const x1 = x * Math.cos(a) - y * Math.sin(a);
        const y1 = x * Math.sin(a) + y * Math.cos(a);
        return [x1, -(y1 * Math.sin(b) + z * Math.cos(b)), y1 * Math.cos(b) - z * Math.sin(b)];
    };
    // Fitted to the box at every angle by the projected corners of the box the surface stands in,
    // with room round it for the labels; then zoomed about the centre and panned.
    const corners = [];
    for (const cx of [-0.5, 0.5]) for (const cy of [-0.5, 0.5]) for (const cz of [-0.3, 0.3]) corners.push(raw([cx, cy, cz]));
    const minX = Math.min(...corners.map((c) => c[0]));
    const maxX = Math.max(...corners.map((c) => c[0]));
    const minY = Math.min(...corners.map((c) => c[1]));
    const maxY = Math.max(...corners.map((c) => c[1]));
    const s = Math.max(1, Math.min((plotW * 0.78) / (maxX - minX), (H * 0.8) / (maxY - minY))) * view.zoom;
    const cx = plotW / 2 + view.panX * W;
    const cy = H / 2 + view.panY * H;
    const project = (p) => {
        const [x, y, depth] = raw(p);
        return [cx + (x - (minX + maxX) / 2) * s, cy + (y - (minY + maxY) / 2) * s, depth];
    };
    const P = (x, y, z) => project(world(x, y, z));
    const node = (k) => P(nodes[k].x, nodes[k].y, nodes[k].z);
    const poly = (pts, cls) => svgElement('polygon', { points: pts.map((p) => `${f1(p[0])},${f1(p[1])}`).join(' '), class: cls });
    const line = (p, q, cls) => svgElement('line', { x1: f1(p[0]), y1: f1(p[1]), x2: f1(q[0]), y2: f1(q[1]), class: cls });
    const heat = mesh.bar.diverging ? 'ex-chart-diverge-' : 'ex-chart-heat-';

    // The floor and the two walls behind the surface, each with its grid; which walls are behind
    // depends on where the viewer stands. Seen from below, the box's top is behind it instead of
    // its floor. The grid's x and y lines run up the walls too, so a value is read across to them.
    const backX = Math.sin(a) > 0 ? 1 : 0;
    const backY = Math.cos(a) > 0 ? 1 : 0;
    // The vertical's labels, and its title, stand on the outer side of the edge they mark.
    const right = P(backX, 1 - backY, zMin)[0] > P(0.5, 0.5, zMin)[0];
    const zBack = view.pitch >= 0 ? zMin : zMax;
    const walls = svgElement('g', { class: 'ex-chart-surface-walls' });
    walls.append(poly([P(0, 0, zBack), P(1, 0, zBack), P(1, 1, zBack), P(0, 1, zBack)], 'ex-chart-surface-wall'));
    const upright = Math.abs(view.pitch) < 89;
    if (upright) {
        walls.append(poly([P(0, backY, zMin), P(1, backY, zMin), P(1, backY, zMax), P(0, backY, zMax)], 'ex-chart-surface-wall'));
        walls.append(poly([P(backX, 0, zMin), P(backX, 1, zMin), P(backX, 1, zMax), P(backX, 0, zMax)], 'ex-chart-surface-wall'));
        for (const t of mesh.xLines) walls.append(line(P(t.value, backY, zMin), P(t.value, backY, zMax), 'ex-chart-surface-grid'));
        for (const t of mesh.yLines) walls.append(line(P(backX, t.value, zMin), P(backX, t.value, zMax), 'ex-chart-surface-grid'));
        for (const tick of mesh.zTicks) {
            walls.append(line(P(0, backY, tick.value), P(1, backY, tick.value), 'ex-chart-surface-grid'));
            walls.append(line(P(backX, 0, tick.value), P(backX, 1, tick.value), 'ex-chart-surface-grid'));
            const at = P(backX, 1 - backY, tick.value);
            walls.append(svgElement('text', { x: f1(at[0] + (right ? 8 : -8)), y: f1(at[1] + 4), 'text-anchor': right ? 'start' : 'end', class: 'ex-chart-surface-label' }, tick.label));
        }
    }
    for (const t of mesh.xLines) walls.append(line(P(t.value, 0, zBack), P(t.value, 1, zBack), 'ex-chart-surface-grid'));
    for (const t of mesh.yLines) walls.append(line(P(0, t.value, zBack), P(1, t.value, zBack), 'ex-chart-surface-grid'));
    svg.append(walls);

    // The faces, the farthest first, each in its colour band.
    const faces = mesh.faces.map((face) => {
        const pts = face.corners.map(node);
        return { pts, depth: pts.reduce((sum, p) => sum + p[2], 0) / pts.length, band: face.band };
    });
    faces.sort((p, q) => q.depth - p.depth);
    const surface = svgElement('g', { class: 'ex-chart-surface-faces' });
    for (const face of faces) {
        surface.append(poly(face.pts, `ex-chart-surface-face ${heat}${face.band}`));
    }
    svg.append(surface);

    // The points, and a grid's lines through them, broken at a hole.
    const points = [];
    const projected = new Array(nodes.length);
    const dots = svgElement('g', { class: 'ex-chart-surface-points' });
    for (let k = 0; k < nodes.length; k++) {
        if (nodes[k].z === null || nodes[k].z === undefined) {
            continue;
        }
        const p = node(k);
        projected[k] = p;
        points.push({ k, x: p[0], y: p[1] });
        // A dot marks a value the data holds; an interpolated one is the Consumer's surface between them.
        if (!nodes[k].interpolated) {
            dots.append(svgElement('circle', { cx: f1(p[0]), cy: f1(p[1]), r: '2.4', class: 'ex-chart-surface-point' }));
        }
    }
    svg.append(dots);
    const through = (line) => line.map((k) => projected[k]).filter((p) => p);
    const rows = mesh.rows.map(through);
    const columns = mesh.columns.map(through);

    // The slice drawn on the surface, so the 2D chart beside it is seen to be this line.
    if (slice && slice.length > 0) {
        const g = svgElement('g', { class: 'ex-chart-surface-slice' });
        for (const run of slice) {
            g.append(svgElement('polyline', { points: run.map((q) => P(q.x, q.y, q.z)).map((p) => `${f1(p[0])},${f1(p[1])}`).join(' ') }));
        }
        svg.append(g);
    }

    // The axes' labels along the front edges of the floor.
    const frontY = 1 - backY;
    const frontX = 1 - backX;
    const labels = svgElement('g', { class: 'ex-chart-surface-labels' });
    const label = (p, text, cls) => labels.append(svgElement('text', { x: f1(p[0]), y: f1(p[1] + 4), 'text-anchor': 'middle', class: cls }, text));
    const out = (front, by) => front + (front === 0 ? -by : by);
    for (const t of mesh.xLines) if (t.label !== null && t.label !== undefined) label(P(t.value, out(frontY, 0.08), zMin), t.label, 'ex-chart-surface-label');
    for (const t of mesh.yLines) if (t.label !== null && t.label !== undefined) label(P(out(frontX, 0.08), t.value, zMin), t.label, 'ex-chart-surface-label');
    label(P(0.5, out(frontY, 0.22), zMin), mesh.xTitle, 'ex-chart-surface-label ex-chart-surface-title');
    label(P(out(frontX, 0.26), 0.5, zMin), mesh.yTitle, 'ex-chart-surface-label ex-chart-surface-title');
    if (upright && mesh.zTitle) {
        const at = P(backX, 1 - backY, zMax + (zMax - zMin) * 0.12);
        labels.append(svgElement('text', { x: f1(at[0] + (right ? 8 : -8)), y: f1(at[1] + 4), 'text-anchor': right ? 'start' : 'end', class: 'ex-chart-surface-label ex-chart-surface-title' }, mesh.zTitle));
    }
    svg.append(labels);

    // The colour bar, which does not turn with the surface: its bands low to high, each the colour a
    // face takes, and its values (ADR-0310).
    const bar = svgElement('g', { class: 'ex-chart-surface-bar' });
    const top = H * 0.1;
    const bottom = H * 0.9;
    const left = W - SURFACE_BAR + 8;
    const count = mesh.bar.bands;
    const band = (bottom - top) / count;
    for (let k = 0; k < count; k++) {
        bar.append(svgElement('rect', { x: f1(left), y: f1(bottom - (k + 1) * band), width: '12', height: f1(band), class: `ex-chart-surface-bar-band ${heat}${k}` }));
    }
    bar.append(svgElement('rect', { x: f1(left), y: f1(top), width: '12', height: f1(bottom - top), class: 'ex-chart-surface-bar-frame' }));
    const span = mesh.bar.to - mesh.bar.from || 1;
    for (const tick of mesh.bar.ticks) {
        const y = bottom - ((tick.value - mesh.bar.from) / span) * (bottom - top);
        bar.append(line([left + 12, y], [left + 16, y], 'ex-chart-surface-bar-tick'));
        bar.append(svgElement('text', { x: f1(left + 19), y: f1(y + 4), class: 'ex-chart-surface-label' }, tick.label));
    }
    svg.append(bar);

    // How a step along x and along y is drawn at a point, for a click on a mesh, which has no grid line.
    const o = project(world(0.5, 0.5, zMin));
    const ox = project(world(0.6, 0.5, zMin));
    const oy = project(world(0.5, 0.6, zMin));
    const steps = { x: [ox[0] - o[0], ox[1] - o[1]], y: [oy[0] - o[0], oy[1] - o[1]] };

    return { points, lines: { rows, columns }, steps };
}
