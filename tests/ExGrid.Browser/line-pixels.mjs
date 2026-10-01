import { painted } from './pixels.mjs';

// Excel's lines as the browser painted them, read across and along a cell's edge in device pixels
// (ADR-0050 item 15, ADR-0063; DC-59). The same reading appearance.spec.mjs makes of the core's
// own /appearance page, for a page of ExSheet's: its Paper, its gridlines and its Headings around
// the lines.

/** Excel's thirteen line styles, in the order the eleventh Windows run's case 9 drew them. */
export const STYLES = ['Hair', 'Thin', 'Medium', 'Thick', 'Double', 'Dotted', 'Dashed', 'DashDot', 'DashDotDot',
    'MediumDashed', 'MediumDashDot', 'MediumDashDotDot', 'SlantedDashDot'];

export const luminance = ([r, g, b]) => 0.299 * r + 0.587 * g + 0.114 * b;
export const isDark = (pixel) => luminance(pixel) < 96;
export const isLight = (pixel) => luminance(pixel) > 200;

/**
 * The device-pixel rows across a cell's bottom edge (or columns across its right edge, or its
 * left or top), at a point along it: index 0 is the first device pixel past the bottom (right)
 * edge, so -1 is the cell's last — the gridline the cell holds — and +1 the pixel after the first
 * one past it. For a top (left) edge, 0 is the cell's first pixel and -1 the one before it.
 */
export async function across(page, box, side, at = 0.5) {
    const scale = await page.evaluate(() => devicePixelRatio);
    const margin = 6;
    const horizontal = side === 'bottom' || side === 'top';
    const edge = side === 'bottom' ? box.y + box.height : side === 'right' ? box.x + box.width : side === 'top' ? box.y : box.x;
    const region = horizontal
        ? await painted(page, { x: box.x, y: edge - margin, width: box.width, height: 2 * margin })
        : await painted(page, { x: edge - margin, y: box.y, width: 2 * margin, height: box.height });
    const along = horizontal ? box.x + box.width * at : box.y + box.height * at;
    const pixel = (offset) => {
        const point = edge + (offset + 0.5) / scale;
        return horizontal ? region.at(along, point) : region.at(point, along);
    };
    return { scale, pixel };
}

/** The pixels along one device row (column) of a cell's bottom (right) edge, from its start. */
export async function along(page, box, side, offset) {
    const scale = await page.evaluate(() => devicePixelRatio);
    const length = side === 'bottom' ? box.width : box.height;
    const region = side === 'bottom'
        ? await painted(page, { x: box.x, y: box.y + box.height - 4, width: box.width, height: 8 })
        : await painted(page, { x: box.x + box.width - 4, y: box.y, width: 8, height: box.height });
    const line = side === 'bottom' ? box.y + box.height + (offset + 0.5) / scale : box.x + box.width + (offset + 0.5) / scale;
    const out = [];
    for (let i = 0; i < Math.floor(length * scale); i++) {
        const point = (side === 'bottom' ? box.x : box.y) + (i + 0.5) / scale;
        out.push(side === 'bottom' ? region.at(point, line) : region.at(line, point));
    }
    return out;
}

/** Alternating on and off lengths along a line, from its first dark pixel; the last, cut by the
 * cell's end, is left out. A dash pattern starts on at the cell's own edge. */
export function pattern(pixels) {
    const first = pixels.findIndex(isDark);
    const lengths = [];
    let run = 0;
    let on = true;
    for (let i = first; i >= 0 && i < pixels.length; i++) {
        if (isDark(pixels[i]) === on) {
            run++;
        } else {
            lengths.push(run);
            run = 1;
            on = !on;
        }
    }
    return lengths;
}

/**
 * Excel's pixels (the eleventh Windows run, case 9): which device pixels across the gridline each
 * style takes, counted as `across` counts them, and its dash pattern along it. A long dash is 8
 * pixels at 100% and 9 at 150%; every other length is the same at both.
 */
export function expected(style, scale) {
    const dash = scale >= 1.5 ? 9 : 8;
    switch (style) {
        case 'Thin': return { dark: [-1], light: [-2, 0] };
        case 'Medium': return { dark: [-2, -1], light: [-3, 0] };
        case 'Thick': return { dark: [-2, -1, 0], light: [-3, 1] };
        case 'Double': return { dark: [-2, 0], light: [-3, -1, 1] };
        case 'Hair': return { rows: [-1], light: [-2, 0], pattern: [1, 1, 1, 1] };
        case 'Dotted': return { rows: [-1], light: [-2, 0], pattern: [2, 2, 2, 2] };
        case 'Dashed': return { rows: [-1], light: [-2, 0], pattern: [3, 1, 3, 1] };
        case 'DashDot': return { rows: [-1], light: [-2, 0], pattern: [dash, 3, 3, 3, dash] };
        case 'DashDotDot': return { rows: [-1], light: [-2, 0], pattern: [dash, 3, 3, 3, 3, 3, dash] };
        case 'MediumDashed': return { rows: [-2, -1], light: [-3, 0], pattern: [dash, 3, dash, 3] };
        case 'MediumDashDot': return { rows: [-2, -1], light: [-3, 0], pattern: [dash, 3, 3, 3, dash] };
        case 'MediumDashDotDot': return { rows: [-2, -1], light: [-3, 0], pattern: [dash, 3, 3, 3, 3, 3, dash] };
        // Two rows with their own patterns: 11, 1, 5, 1 above the gridline, and on it the same
        // period a pixel earlier and narrower (case 9, at both zooms).
        case 'SlantedDashDot': return { rows: [-2], light: [-3, 0], pattern: [11, 1, 5, 1, 11] };
        default: throw new Error(style);
    }
}
