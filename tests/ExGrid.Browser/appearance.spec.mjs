import { test, expect } from './fixtures.mjs';
import { painted, runsOf, sameColour } from './pixels.mjs';

// A per-cell appearance as the browser painted it (ADR-0050 item 15; ADR-0063; DC-58 and DC-59),
// on /appearance, read in device pixels from a screenshot at the device's own scale. Three grids:
// Roboto with ExGrid.MudBlazor's regular and bold widths (#appearance-bold), italic text against
// both edges (#appearance-italic), and Excel's thirteen line styles (#appearance-borders) — in
// column B on each cell's bottom, in column D on its right, in the order of the eleventh Windows
// run's case 9; and in column C a red thick line over a yellow Fill.
//
// The line tests run at 100% here and at 150% in the chrome-150 project (playwright.config.mjs),
// which sets the display scale on Chrome's command line as an OS does, so the browser lays out in
// device pixels; Excel's pixels at both zooms are the eleventh run's case 9 table.

const STYLES = ['Hair', 'Thin', 'Medium', 'Thick', 'Double', 'Dotted', 'Dashed', 'DashDot', 'DashDotDot',
    'MediumDashed', 'MediumDashDot', 'MediumDashDotDot', 'SlantedDashDot'];

const BLACK = [0, 0, 0];
const RED = [255, 0, 0];
const YELLOW = [255, 255, 0];
const luminance = ([r, g, b]) => 0.299 * r + 0.587 * g + 0.114 * b;
const isDark = (pixel) => luminance(pixel) < 96;
const isLight = (pixel) => luminance(pixel) > 200;

function cellOf(page, grid, row, column) {
    return page.locator(`#${grid} .ex-grid [id$='r${row}c${column}']`);
}

async function open(page) {
    await page.goto('/appearance');
    await expect(cellOf(page, 'appearance-borders', 0, 0)).toHaveText('Hair');
    await expect(cellOf(page, 'appearance-bold', 1, 2)).toHaveText('1234567890');
    // Roboto has arrived, so the bold grid is painted in the font its widths are for.
    await page.evaluate(() => document.fonts.ready);
    // The hover band follows the pointer (ADR-0029); park it off the grids.
    await page.mouse.move(0, 0);
}

/**
 * The device-pixel rows across a cell's bottom edge (or columns across its right edge), at a
 * point along it: index 0 is the first device pixel past the edge, so -1 is the cell's last —
 * the gridline the cell holds — and +1 the pixel after the first one past it.
 */
async function across(page, box, side, at = 0.5) {
    const scale = await page.evaluate(() => devicePixelRatio);
    const margin = 6;
    const region = side === 'bottom'
        ? await painted(page, { x: box.x, y: box.y + box.height - margin, width: box.width, height: 2 * margin })
        : await painted(page, { x: box.x + box.width - margin, y: box.y, width: 2 * margin, height: box.height });
    const edge = side === 'bottom' ? box.y + box.height : box.x + box.width;
    const along = side === 'bottom' ? box.x + box.width * at : box.y + box.height * at;
    const pixel = (offset) => {
        const point = edge + (offset + 0.5) / scale;
        return side === 'bottom' ? region.at(along, point) : region.at(point, along);
    };
    return { scale, pixel };
}

/** The pixels along one device row (column) of a cell's bottom (right) edge, from its start. */
async function along(page, box, side, offset) {
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
function pattern(pixels) {
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

// Excel's pixels (the eleventh Windows run, case 9): which device pixels across the gridline each
// style takes, counted as `across` counts them, and its dash pattern along it. A long dash is 8
// pixels at 100% and 9 at 150%; every other length is the same at both.
function expected(style, scale) {
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

test.describe('DC-59: lines', () => {
    for (const [index, style] of STYLES.entries()) {
        for (const side of ['bottom', 'right']) {
            test(`DC-59: a ${style} line on a cell's ${side} edge is drawn as Excel draws it, centred on the gridline (ADR-0050 item 15, case 9)`, async ({ page }, testInfo) => {
                await open(page);
                // The 150% run is the display scale chrome-150 sets, not a default it fell back from.
                expect(await page.evaluate(() => devicePixelRatio)).toBe(testInfo.project.name === 'chrome-150' ? 1.5 : 1);
                const cell = cellOf(page, 'appearance-borders', index, side === 'bottom' ? 1 : 3);
                await cell.scrollIntoViewIfNeeded();
                const box = await cell.boundingBox();
                const { scale, pixel } = await across(page, box, side, 0.5);
                const want = expected(style, scale);
                for (const offset of want.light) {
                    // A dashed line is read where it is on, so "light" is about the rows beside it.
                    const pixels = await along(page, box, side, offset);
                    expect(pixels.filter(isDark).length, `${style} leaves ${offset} clear`).toBe(0);
                }
                if (want.dark) {
                    for (const offset of want.dark) expect(isDark(pixel(offset)), `${style} at ${offset} (scale ${scale})`).toBe(true);
                    if (style === 'Double') expect(isLight(pixel(-1)), 'the gridline between a double line\'s two').toBe(true);
                    // Solid: every pixel along the dark rows, the whole length of the cell.
                    for (const offset of want.dark) {
                        const pixels = await along(page, box, side, offset);
                        expect(pixels.every(isDark), `${style} is solid along ${offset}`).toBe(true);
                    }
                } else {
                    for (const offset of want.rows) {
                        // As many whole runs as the cell's length holds: a right edge is one row high.
                        const lengths = pattern(await along(page, box, side, offset)).slice(0, want.pattern.length);
                        expect(lengths.length, `${style} along ${offset}: runs read`).toBeGreaterThanOrEqual(3);
                        expect(lengths, `${style} along ${offset} (scale ${scale})`).toEqual(want.pattern.slice(0, lengths.length));
                    }
                }
            });
        }
    }

    test('DC-59: a line lies above the Fill, and a thick one reaches one pixel into the filled cell below (ADR-0063, case 10)', async ({ page }) => {
        await open(page);
        const cell = cellOf(page, 'appearance-borders', 3, 2);
        await cell.scrollIntoViewIfNeeded();
        const box = await cell.boundingBox();
        const { pixel } = await across(page, box, 'bottom', 0.5);
        expect(sameColour(pixel(-2), RED)).toBe(true);
        expect(sameColour(pixel(-1), RED)).toBe(true);
        expect(sameColour(pixel(0), RED), 'the pixel past the gridline is the line\'s, over the Fill').toBe(true);
        expect(sameColour(pixel(1), YELLOW), 'the Fill after it').toBe(true);
    });

    test('DC-59: the Focus and the Selection are drawn above a line (ADR-0063, case 11)', async ({ page }) => {
        await open(page);
        const grid = page.locator('#appearance-borders .ex-grid');
        const lined = cellOf(page, 'appearance-borders', 3, 2);
        const filled = cellOf(page, 'appearance-borders', 4, 2);
        await lined.scrollIntoViewIfNeeded();
        await lined.click({ force: true });
        await expect(grid.locator('.ex-focus')).toHaveCount(1);
        await page.mouse.move(0, 0);
        const box = await lined.boundingBox();
        let { pixel } = await across(page, box, 'bottom', 0.5);
        // The Focus outline lies inside its cell, over the line's two pixels there; the pixel past
        // the gridline is the next cell's, and stays the line's.
        expect(sameColour(pixel(-1), RED), 'the Focus covers the line in its cell').toBe(false);
        expect(isDark(pixel(-1))).toBe(true);
        expect(sameColour(pixel(0), RED)).toBe(true);

        await filled.click({ force: true, modifiers: ['Shift'] });
        await page.waitForFunction(() => [...document.querySelectorAll('#appearance-borders .ex-range')]
            .some((range) => range.getBoundingClientRect().height > 30));
        await page.mouse.move(0, 0);
        ({ pixel } = await across(page, box, 'bottom', 0.5));
        // Inside the range and outside the Focus, the Selection's tint lies over the line.
        const tinted = pixel(0);
        expect(sameColour(tinted, RED), 'the Selection tints the line').toBe(false);
        expect(tinted[0]).toBeGreaterThan(tinted[1] + 100);
    });
});

test('DC-58: a bold number that fits at the regular widths and not at the bold ones is #### (ADR-0050 item 15, ADR-0016)', async ({ page }) => {
    await open(page);
    const regular = cellOf(page, 'appearance-bold', 0, 1);
    const bold = cellOf(page, 'appearance-bold', 1, 1);
    const boldWide = cellOf(page, 'appearance-bold', 1, 2);
    const regularWide = cellOf(page, 'appearance-bold', 0, 2);
    await boldWide.scrollIntoViewIfNeeded();

    await expect(regular).toHaveText('1234567890');
    await expect(bold).toHaveText(/^#+$/);
    await expect(bold).toHaveAttribute('aria-label', '1234567890');
    await expect(boldWide).toHaveText('1234567890');
    expect(await boldWide.evaluate((e) => getComputedStyle(e).fontWeight)).toBe('700');
    expect(await regular.evaluate((e) => getComputedStyle(e).fontFamily)).toContain('Roboto');

    // What is shown is whole: no ellipsis, and the bold ink is the bold face's, wider than the
    // regular one's for the same digits.
    for (const cell of [regular, boldWide]) {
        expect(await cell.evaluate((e) => e.scrollWidth <= e.clientWidth), 'nothing is cut').toBe(true);
    }
    // The ink in each device row of a cell: how much of it is dark, and its first and last column.
    const ink = async (cell) => {
        const box = await cell.boundingBox();
        const region = await painted(page, box);
        let dark = 0;
        let first = Infinity;
        let last = -Infinity;
        let columns = 0;
        for (let y = box.y + 0.5 / region.scale; y < box.y + box.height; y += 1 / region.scale) {
            const pixels = region.across(y, box.x, box.x + box.width - 0.01);
            columns = pixels.length;
            pixels.forEach((p, i) => {
                if (isDark(p)) {
                    dark++;
                    first = Math.min(first, i);
                    last = Math.max(last, i);
                }
            });
        }
        return { dark, first, last, columns };
    };
    const regularInk = await ink(regularWide);
    const boldInk = await ink(boldWide);
    expect(boldInk.dark, 'the bold face is painted').toBeGreaterThan(regularInk.dark * 1.15);
    expect(boldInk.last).toBeLessThan(boldInk.columns - 1);
    // The #### is painted, inside its cell.
    const hashes = await ink(bold);
    expect(hashes.dark).toBeGreaterThan(0);
    expect(hashes.first).toBeGreaterThan(0);
    expect(hashes.last).toBeLessThan(hashes.columns - 1);
});

test('DC-58: italic text is not cut at either edge of its cell (ADR-0063, "Bold, and what fits")', async ({ page }) => {
    await open(page);
    for (const [row, column] of [[0, 0], [1, 0], [0, 1], [1, 1]]) {
        const cell = cellOf(page, 'appearance-italic', row, column);
        await cell.scrollIntoViewIfNeeded();
        expect(await cell.evaluate((e) => getComputedStyle(e).fontStyle)).toBe('italic');
        const box = await cell.boundingBox();
        const region = await painted(page, box);
        // Every device row through the text: the ink's leftmost and rightmost columns.
        let first = Infinity;
        let last = -Infinity;
        let columns = 0;
        for (let y = box.y + 1; y < box.y + box.height - 1; y += 1 / region.scale) {
            const pixels = region.across(y, box.x, box.x + box.width - 0.01);
            columns = pixels.length;
            pixels.forEach((p, i) => {
                if (luminance(p) < 160) {
                    first = Math.min(first, i);
                    last = Math.max(last, i);
                }
            });
        }
        expect(Number.isFinite(first), `r${row}c${column} has ink`).toBe(true);
        // A slant cut by the cell's clip would reach its edge pixel; whole, it stops short of it.
        expect(first, `r${row}c${column}: clear of the left edge`).toBeGreaterThan(0);
        expect(last, `r${row}c${column}: clear of the right edge`).toBeLessThan(columns - 1);
    }
});

// Lines keep a row's tint (ADR-0050 item 15 with ADR-0024, ADR-0038 and ADR-0006), on
// #appearance-tints: columns A to E pinned and F to J scrolling. In each block the first cell is
// plain, the second has a dashed bottom line, the third a yellow Fill, the fourth no line of its own
// but the Fill beside it over its gridline, and the fifth the Fill and the dashed line. Rows 1
// (striped), 3 (group), 5 (total) and 7 (Missing, striped) carry them; the even rows between are
// plain. A cell with lines must paint exactly what its twin without them paints, away from them.

const TINTS = 'appearance-tints';
const [PLAIN, LINED, FILLED, BESIDE, BOTH] = [0, 1, 2, 3, 4];
const BLOCKS = { pinned: 0, scrolling: 5 };
const [STRIPED_ROW, PLAIN_ROW, GROUP_ROW, TOTAL_ROW, MISSING_ROW] = [1, 2, 3, 5, 7];

/** Reads #appearance-tints as painted: a cell's centre; the device pixels down through its top
 * edge, from the last of the row above to its own third; and the left half of the device rows
 * through its bottom edge, where its dashed line lies (its right edge may be a cover). Read across
 * the edges rather than at them, because a box is snapped to device pixels where its edge is not. */
async function tintGrounds(page) {
    const grid = page.locator(`#${TINTS} .ex-grid`);
    await expect(cellOf(page, TINTS, MISSING_ROW, 9)).toBeVisible();
    await grid.scrollIntoViewIfNeeded();
    await page.mouse.move(0, 0);
    const region = await painted(page, await grid.boundingBox());
    const device = (n) => n / region.scale;
    return async (row, column) => {
        const box = await cellOf(page, TINTS, row, column).boundingBox();
        const middle = box.x + box.width / 2;
        const bottom = box.y + box.height;
        return {
            centre: region.at(middle, box.y + box.height / 2),
            top: region.down(middle, box.y - device(0.5), box.y + device(2.5)),
            bottom: [2.5, 1.5, 0.5].flatMap((up) => region.across(bottom - device(up), box.x, middle)),
        };
    };
}

/** In one row and block, each cell with lines (or a cover) paints its twin's ground beneath them,
 * and a dashed line is still the line's own black, over the tint. */
async function expectTwins(read, row, block, what) {
    const at = (offset) => read(row, BLOCKS[block] + offset);
    const plain = await at(PLAIN);
    const filled = await at(FILLED);
    for (const [offset, twin, cell] of [[LINED, plain, 'a dashed line'], [BESIDE, plain, 'the Fill beside it'], [BOTH, filled, 'a Fill and a dashed line']]) {
        const lined = await at(offset);
        expect(sameColour(lined.centre, twin.centre, 1), `${what}, ${block}: ${cell} keeps the ground ${twin.centre} (${lined.centre})`).toBe(true);
        expect(lined.top.every((pixel, i) => sameColour(pixel, twin.top[i], 1)),
            `${what}, ${block}: ${cell} keeps the top edge ${JSON.stringify(twin.top)} (${JSON.stringify(lined.top)})`).toBe(true);
    }
    for (const offset of [LINED, BOTH]) {
        const { bottom } = await at(offset);
        expect(bottom.some((pixel) => sameColour(pixel, BLACK, 2)), `${what}, ${block}: the dashes are black, over the tint`).toBe(true);
    }
    return { plain, filled };
}

test('ADR-0050 item 15 / ADR-0024: a lined cell of a group or total row keeps the row\'s tint, beneath its lines and above its Fill', async ({ page }) => {
    await open(page);
    const read = await tintGrounds(page);
    const unstriped = await read(PLAIN_ROW, BLOCKS.pinned + PLAIN);

    for (const block of Object.keys(BLOCKS)) {
        const group = await expectTwins(read, GROUP_ROW, block, 'a group row');
        // There is a tint to keep: the group's ground is not a plain row's, and lies over the Fill.
        expect(sameColour(group.plain.centre, unstriped.centre, 2), `${block}: the group's ground`).toBe(false);
        expect(sameColour(group.filled.centre, YELLOW, 2), `${block}: the group's tint over the Fill`).toBe(false);

        const total = await expectTwins(read, TOTAL_ROW, block, 'a total row');
        // A total row's tint is its rule, along its top edge, over the Fill as well.
        expect(total.plain.top.some((pixel) => !sameColour(pixel, total.plain.centre, 2)), `${block}: the total row's rule`).toBe(true);
        expect(total.filled.top.some((pixel) => !sameColour(pixel, YELLOW, 2)), `${block}: the total row's rule over the Fill`).toBe(true);
    }
});

test('ADR-0050 item 15 / ADR-0038 (UX-15): a lined pinned cell of a striped row keeps the stripe, beneath its lines and above its Fill', async ({ page }) => {
    await open(page);
    const read = await tintGrounds(page);
    const unstriped = await read(PLAIN_ROW, BLOCKS.pinned + PLAIN);

    const pinned = await expectTwins(read, STRIPED_ROW, 'pinned', 'a striped row');
    expect(sameColour(pinned.plain.centre, unstriped.centre, 2), 'the pinned stripe').toBe(false);
    expect(sameColour(pinned.filled.centre, YELLOW, 2), 'the pinned stripe over the Fill').toBe(false);
    // The scrolling cells are transparent over the row's own stripe, with lines as without them.
    await expectTwins(read, STRIPED_ROW, 'scrolling', 'a striped row');
});

test('ADR-0050 item 15 / ADR-0006: a lined Missing cell keeps its state\'s tint, beneath its lines and above its Fill', async ({ page }) => {
    await open(page);
    const read = await tintGrounds(page);

    for (const block of Object.keys(BLOCKS)) {
        const missing = await expectTwins(read, MISSING_ROW, block, 'a Missing row');
        // The state's tint is there to keep: the row is striped like row 1, and Missing paints over that.
        const striped = await read(STRIPED_ROW, BLOCKS[block] + PLAIN);
        expect(sameColour(missing.plain.centre, striped.centre, 2), `${block}: Missing's ground`).toBe(false);
        expect(sameColour(missing.filled.centre, YELLOW, 2), `${block}: Missing's tint over the Fill`).toBe(false);
    }
});
