import zlib from 'node:zlib';
import { circuitQuiet, test, twoFrames } from './fixtures.mjs';

// What the browser put on the screen, read in device pixels: a screenshot at the device's own
// scale, decoded here. A computed style says what the cascade resolved; it cannot say that a
// layer painted above an outline hid one of its pixels, which is what UX-17/18/19 are about.
// When to read pixels at all, and how, is README.md's "Reading pixels".

/** Decodes the PNG a Chromium screenshot writes: 8-bit RGB or RGBA, not interlaced. */
function decodePng(buffer) {
    let offset = 8;
    let width = 0;
    let height = 0;
    let colourType = 0;
    const data = [];
    while (offset < buffer.length) {
        const length = buffer.readUInt32BE(offset);
        const type = buffer.toString('ascii', offset + 4, offset + 8);
        const chunk = buffer.subarray(offset + 8, offset + 8 + length);
        if (type === 'IHDR') {
            width = chunk.readUInt32BE(0);
            height = chunk.readUInt32BE(4);
            if (chunk[8] !== 8 || chunk[12] !== 0 || (chunk[9] !== 2 && chunk[9] !== 6)) {
                throw new Error('decodePng reads 8-bit, non-interlaced RGB or RGBA only');
            }
            colourType = chunk[9];
        } else if (type === 'IDAT') {
            data.push(chunk);
        }
        offset += 12 + length;
    }
    const channels = colourType === 6 ? 4 : 3;
    const raw = zlib.inflateSync(Buffer.concat(data));
    const stride = width * channels;
    const pixels = Buffer.alloc(height * stride);
    for (let y = 0; y < height; y++) {
        const filter = raw[y * (stride + 1)];
        for (let x = 0; x < stride; x++) {
            const a = x >= channels ? pixels[y * stride + x - channels] : 0;
            const b = y > 0 ? pixels[(y - 1) * stride + x] : 0;
            const c = x >= channels && y > 0 ? pixels[(y - 1) * stride + x - channels] : 0;
            let v = raw[y * (stride + 1) + 1 + x];
            if (filter === 1) v += a;
            else if (filter === 2) v += b;
            else if (filter === 3) v += Math.floor((a + b) / 2);
            else if (filter === 4) {
                const p = a + b - c;
                const pa = Math.abs(p - a);
                const pb = Math.abs(p - b);
                const pc = Math.abs(p - c);
                v += pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
            }
            pixels[y * stride + x] = v & 0xff;
        }
    }
    return {
        width,
        height,
        at: (x, y) => {
            const i = (y * stride) + (x * channels);
            return [pixels[i], pixels[i + 1], pixels[i + 2]];
        },
    };
}

/**
 * A region of the page as painted, in device pixels. Every coordinate given is a page point in
 * CSS px; `across` and `down` answer the device pixels along a line through the region.
 */
export async function painted(page, { x, y, width, height }) {
    const clip = { x, y, width, height };
    const image = decodePng(await page.screenshot({ clip, scale: 'device', animations: 'disabled', caret: 'hide' }));
    const scale = image.width / width;
    const col = (px) => Math.min(image.width - 1, Math.max(0, Math.floor((px - x) * scale)));
    const row = (py) => Math.min(image.height - 1, Math.max(0, Math.floor((py - y) * scale)));
    return {
        scale,
        at: (px, py) => image.at(col(px), row(py)),
        across: (py, fromX, toX) => {
            const out = [];
            for (let i = col(fromX); i <= col(toX); i++) out.push(image.at(i, row(py)));
            return out;
        },
        down: (px, fromY, toY) => {
            const out = [];
            for (let j = row(fromY); j <= row(toY); j++) out.push(image.at(col(px), j));
            return out;
        },
    };
}

/** Whether two painted colours are the same to within `tolerance` on each channel. */
export const sameColour = (a, b, tolerance = 12) => a.every((v, i) => Math.abs(v - b[i]) <= tolerance);

/** The lengths, in device pixels, of the runs of `colour` along a line of pixels. */
export function runsOf(pixels, colour, tolerance = 48) {
    const runs = [];
    let run = 0;
    for (const pixel of pixels) {
        if (sameColour(pixel, colour, tolerance)) {
            run++;
        } else if (run > 0) {
            runs.push(run);
            run = 0;
        }
    }
    if (run > 0) runs.push(run);
    return runs;
}

/** A CSS colour as the page resolves it, in sRGB bytes — whatever syntax the cascade kept it in. */
export function resolvedColour(page, css) {
    return page.evaluate((colour) => {
        const context = new OffscreenCanvas(1, 1).getContext('2d', { willReadFrequently: true });
        context.fillStyle = colour;
        context.fillRect(0, 0, 1, 1);
        return Array.from(context.getImageData(0, 0, 1, 1).data.slice(0, 3));
    }, css);
}

/** WCAG's contrast ratio between two sRGB colours. */
export function contrast(a, b) {
    const luminance = (rgb) => {
        const [r, g, bl] = rgb.map((v) => {
            const c = v / 255;
            return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
        });
        return 0.2126 * r + 0.7152 * g + 0.0722 * bl;
    };
    const la = luminance(a);
    const lb = luminance(b);
    return (Math.max(la, lb) + 0.05) / (Math.min(la, lb) + 0.05);
}

// ---------------------------------------------------------------------------------------------
// The one allowance for rounding

/**
 * The exact colour `over`, at opacity `alpha`, paints on an opaque `under`: each channel as a
 * real number, not rounded, in sRGB bytes as the browser blends them. `alpha` is the one the
 * browser holds, a whole number of 255ths: rgba(255, 255, 255, 0.12) is held as 31/255.
 */
export const blend = (over, alpha, under) => under.map((u, i) => u + (over[i] - u) * alpha);

/**
 * Whether a painted pixel is the colour `exact` names, each channel given as its exact value.
 * This is the one allowance for rounding, and it is not a tolerance. A pixel holds whole bytes,
 * so a channel whose exact value lies between two bytes is either of them, depending on the
 * path that painted it: MudBlazor's dark row rule is 78.53 in red and green on its ground, and
 * Chrome on Linux painted it 79 on a pinned cell and 78 on the scrollable cell beside it
 * (ticket 92). Both are that colour, and no other byte is. A whole exact value admits only
 * itself, so this never widens a comparison of opaque colours, and the requirement is the one
 * the exact value states. Two paints that ought to be one paint are not compared through this:
 * they are compared with each other, exactly (ticket 92 found its defect that way).
 */
export function paints(pixel, exact) {
    return exact.every((value, i) => {
        const whole = Math.round(value);
        return Math.abs(value - whole) < 1e-6
            ? pixel[i] === whole
            : pixel[i] === Math.floor(value) || pixel[i] === Math.ceil(value);
    });
}

// ---------------------------------------------------------------------------------------------
// Where to read

/**
 * The edges of a CSS box that do not lie on the device pixel grid at `scale`, each named: none
 * for a box whose every edge does. A device pixel an edge cuts through is painted with both
 * sides blended, so a reading of it says nothing about either; a test that reads at an edge
 * checks first that the edge is on the grid, and fails there, saying so, if it is not. Chrome
 * lays out in 64ths of a CSS px, so an edge off the grid is off by a 64th or more.
 */
export function offDevicePixels({ x, y, width, height }, scale) {
    const edges = { left: x, top: y, right: x + width, bottom: y + height };
    return Object.entries(edges)
        .filter(([, at]) => Math.abs(at * scale - Math.round(at * scale)) > 1e-3)
        .map(([edge, at]) => `${edge} at ${at} CSS px, ${+(at * scale).toFixed(4)} device px`);
}

/**
 * The centres, in CSS px, of the device pixels a box covers whole: where a reading of its own
 * paint can be trusted. A pixel its edge cuts through is left out. Read them through a region
 * that `onDeviceGrid` gave `painted`, so the region's pixels are the page's.
 */
export function wholePixelCentres({ x, y, width, height }, scale) {
    const centres = (from, length) => {
        const out = [];
        for (let i = Math.ceil(from * scale - 1e-3); i < Math.floor((from + length) * scale + 1e-3); i++) {
            out.push((i + 0.5) / scale);
        }
        return out;
    };
    return { xs: centres(x, width), ys: centres(y, height) };
}

/**
 * A CSS box grown by `margin` CSS px on each side and out to the device pixels it touches. As
 * `painted`'s region, each pixel of its picture is then one device pixel of the page, and `at`
 * reads the pixel it is pointed at. A region whose corner falls inside a device pixel leaves
 * the browser to decide which pixel comes first.
 */
export function onDeviceGrid({ x, y, width, height }, scale, margin = 0) {
    const left = Math.floor((x - margin) * scale + 1e-3) / scale;
    const top = Math.floor((y - margin) * scale + 1e-3) / scale;
    const right = Math.ceil((x + width + margin) * scale - 1e-3) / scale;
    const bottom = Math.ceil((y + height + margin) * scale - 1e-3) / scale;
    return { x: left, y: top, width: right - left, height: bottom - top };
}

// ---------------------------------------------------------------------------------------------
// Pictures compared with each other

/**
 * How two PNG pictures of one thing differ: how many pixels differ at all, and how many by more
 * than `threshold` on some channel. Pictures of different sizes are not compared.
 */
export function pixelsApart(a, b, threshold) {
    const one = decodePng(a);
    const other = decodePng(b);
    if (one.width !== other.width || one.height !== other.height) {
        throw new Error(`pictures of ${one.width}×${one.height} and ${other.width}×${other.height} px are not of one thing`);
    }
    let differing = 0;
    let apart = 0;
    for (let y = 0; y < one.height; y++) {
        for (let x = 0; x < one.width; x++) {
            const p = one.at(x, y);
            const q = other.at(x, y);
            const d = Math.max(...p.map((v, i) => Math.abs(v - q[i])));
            differing += d > 0 ? 1 : 0;
            apart += d > threshold ? 1 : 0;
        }
    }
    return { differing, apart };
}

// The page watched while stillPictures takes its pictures. Every change to the document is
// kept except the test's own mark and Playwright's preparation for a screenshot, and so is every
// scroll. Playwright makes the caret transparent on every field with an inline style before a
// shot, and writes back what was there after it — which leaves `style=""` where there was none;
// given the screenshot option `style`, it also lays a <style> under <html> for the shot.
function watchThePage(marked, mark) {
    const records = [];
    const observer = new MutationObserver((list) => records.push(...list));
    observer.observe(document, {
        subtree: true, childList: true, attributes: true, attributeOldValue: true, characterData: true,
    });
    const scrolled = [];
    const onScroll = (event) => scrolled.push(event.target);
    document.addEventListener('scroll', onScroll, { capture: true, passive: true });

    const name = (node) => {
        const element = node instanceof Element ? node : node.parentElement;
        if (!element) {
            return node === document ? 'the document' : node.nodeName;
        }
        return element.tagName.toLowerCase() + (element.id ? `#${element.id}` : '')
            + [...element.classList].slice(0, 3).map((c) => `.${c}`).join('');
    };
    // An inline style as its declarations, the caret's colour aside.
    const declarations = (css) => {
        const probe = document.createElement('div');
        probe.setAttribute('style', css ?? '');
        const style = probe.style;
        return Array.from({ length: style.length }, (_, i) => style[i])
            .filter((property) => property !== 'caret-color').sort()
            .map((property) => `${property}: ${style.getPropertyValue(property)} ${style.getPropertyPriority(property)}`)
            .join('; ');
    };
    const clip = (text) => (text === null ? 'none' : JSON.stringify(text.length > 80 ? `${text.slice(0, 80)}…` : text));

    return {
        stop() {
            records.push(...observer.takeRecords());
            observer.disconnect();
            document.removeEventListener('scroll', onScroll, { capture: true });
            const changes = [];
            const styles = new Map();
            for (const record of records) {
                if (record.type === 'attributes' && record.target === marked && record.attributeName === mark) {
                    continue;
                }
                if (record.type === 'attributes' && record.attributeName === 'style') {
                    if (!styles.has(record.target)) {
                        styles.set(record.target, []);
                    }
                    styles.get(record.target).push(record.oldValue);
                } else if (record.type === 'attributes') {
                    changes.push(`${record.attributeName} on ${name(record.target)}, ${clip(record.oldValue)} → `
                        + `${clip(record.target.getAttribute(record.attributeName))} now`);
                } else if (record.type === 'childList') {
                    const nodes = [...record.addedNodes, ...record.removedNodes];
                    const playwrights = record.target === document.documentElement
                        && nodes.every((node) => node.nodeName === 'STYLE' && !node.isConnected);
                    if (!playwrights) {
                        changes.push(`children of ${name(record.target)}: ${record.addedNodes.length} added, `
                            + `${record.removedNodes.length} removed`);
                    }
                } else {
                    changes.push(`text in ${name(record.target)}`);
                }
            }
            // Each value a style held in turn, the last being what it holds now: a step that changed
            // a declaration other than the caret's colour is not Playwright's.
            for (const [element, olds] of styles) {
                const values = [...olds, element.getAttribute('style')];
                for (let i = 1; i < values.length; i++) {
                    if (declarations(values[i - 1]) !== declarations(values[i])) {
                        changes.push(`style on ${name(element)}, ${clip(values[i - 1])} → ${clip(values[i])}`);
                    }
                }
            }
            changes.push(...[...new Set(scrolled)].map((target) => `a scroll of ${name(target)}`));
            return changes;
        },
    };
}

/**
 * Pictures of `subject` drawn several ways, taken while nothing but the test changed the page.
 * Each way is drawn by setting the attribute `mark` on `on` (the subject, unless named) to the
 * way, which a stylesheet the test laid over the page through alterPage keys on. The mark is
 * the test's only write; draw both ways in a colour of the test's choosing when the question is
 * where the ink stands rather than what colour it is (README.md, "Reading pixels").
 *
 * Before the first mark it waits for the circuit to be quiet, and from the first mark to the
 * last picture it watches the page. Any change to the document other than the mark and
 * Playwright's own preparation for a screenshot, and any scroll, means the pictures may be of
 * two different pages: they are all taken again, up to `attempts` times in all, and a retake is
 * noted on the test. A page that never holds still fails the test, naming what changed.
 * Answers the pictures by way; the mark is taken off.
 */
export async function stillPictures(subject, {
    mark, ways, on = subject, attempts = 3,
    shoot = (thing) => thing.screenshot({ animations: 'disabled', caret: 'hide' }),
}) {
    const page = subject.page();
    const seen = [];
    for (let attempt = 1; attempt <= attempts; attempt++) {
        await circuitQuiet();
        const watch = await on.evaluateHandle(watchThePage, mark);
        const pictures = {};
        let changes;
        try {
            for (const way of ways) {
                await on.evaluate((element, [name, value]) => element.setAttribute(name, value), [mark, way]);
                await twoFrames(page);
                pictures[way] = await shoot(subject);
            }
        } finally {
            changes = await watch.evaluate((watcher) => watcher.stop()).catch(() => []);
            await watch.dispose().catch(() => { });
            await on.evaluate((element, name) => element.removeAttribute(name), mark).catch(() => { });
        }
        if (changes.length === 0) {
            return pictures;
        }
        seen.push(`attempt ${attempt}: ${changes.slice(0, 8).join('; ')}${changes.length > 8 ? `; and ${changes.length - 8} more` : ''}`);
        test.info().annotations.push({ type: 'stillPictures: taken again', description: seen.at(-1) });
    }
    throw new Error(`the page did not hold still while it was pictured ${ways.join(' and ')}, in ${attempts} attempts:\n`
        + seen.join('\n'));
}
