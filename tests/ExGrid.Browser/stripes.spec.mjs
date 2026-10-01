import { test, expect, scrollRowToTop } from './fixtures.mjs';
import { painted, sameColour } from './pixels.mjs';

// Row Stripes (ADR-0038) as painted, on /stripes: 28px rows in a 420px Viewport, so rows
// 0-13 are on the first screen. Columns: Name 0 (pinned), Amount 1, Quantity 2, Desk 3.
// The page places a group row at 3 (odd) and at 12 (even), a total at 7 (odd), and a
// missing Amount at 5 (striped) and at 8 (not). What is asserted is the colour the
// browser actually put on the screen, read from a screenshot — a computed style would
// say what the cascade resolved, not whether a pinned cell's opaque ground hid it.

test.beforeEach(async ({ page }) => {
    await page.goto('/stripes');
    await expect(page.locator('.ex-grid .ex-row').first()).toBeVisible();
    // The hover band follows the pointer (ADR-0029); park it off the grid.
    await page.mouse.move(0, 0);
});

function cell(page, row, column) {
    return page.locator(`.ex-grid [id$='r${row}c${column}']`);
}

function rowOf(page, row) {
    return page.locator(`.ex-grid .ex-row[aria-rowindex='${row + 1}']`);
}

/**
 * The painted colour of each cell, a few pixels in from its top-left corner — inside
 * the cell's padding and above its text, so what is read is the ground and not a glyph.
 * The screenshot is decoded on a blank page of the same browser: a canvas is the only
 * PNG decoder at hand, and the page under test is left alone.
 */
async function groundsOf(page, cells) {
    const points = [];
    for (const [row, column] of cells) {
        const box = await cell(page, row, column).boundingBox();
        expect(box, `r${row}c${column} is on screen`).not.toBeNull();
        points.push([box.x + 4, box.y + 4]);
    }
    return pixelsAt(page, points);
}

/** The painted colour at each page point, read from a screenshot as groundsOf reads it. */
async function pixelsAt(page, points) {
    const png = await page.screenshot({ scale: 'css', animations: 'disabled', caret: 'hide' });
    const reader = await page.context().newPage();
    try {
        return await reader.evaluate(async ({ data, points }) => {
            const image = new Image();
            image.src = `data:image/png;base64,${data}`;
            await image.decode();
            const canvas = new OffscreenCanvas(image.width, image.height);
            const context = canvas.getContext('2d');
            context.drawImage(image, 0, 0);
            return points.map(([x, y]) =>
                Array.from(context.getImageData(Math.round(x), Math.round(y), 1, 1).data).join(','));
        }, { data: png.toString('base64'), points });
    } finally {
        await reader.close();
    }
}

/**
 * For each hover band painted, the row it covers exactly, among the given rows; a band on
 * none of them is left out. The band is where the pointer is only once the report has
 * travelled (ADR-0029) — on Server, a round trip after the move.
 */
async function hoverBandRows(page, rows) {
    const bands = await page.locator('.ex-grid .ex-hover-row').all();
    const found = [];
    for (const band of bands) {
        const b = await band.boundingBox();
        if (!b) continue;
        for (const row of rows) {
            const r = await rowOf(page, row).boundingBox();
            if (r && Math.abs(b.y - r.y) < 1 && Math.abs(b.height - r.height) < 1) found.push(row);
        }
    }
    return found;
}

async function isStriped(page, row) {
    return (await rowOf(page, row).getAttribute('class')).split(' ').includes('ex-row-stripe');
}

test('a pinned and a scrollable cell of one striped row paint the same ground (UX-15)', async ({ page }) => {
    // Row 9 is striped, row 10 is not; both are detail rows with nothing else on them.
    expect(await isStriped(page, 9)).toBe(true);
    expect(await isStriped(page, 10)).toBe(false);

    const [pinned9, amount9, desk9, pinned10, desk10] =
        await groundsOf(page, [[9, 0], [9, 1], [9, 3], [10, 0], [10, 3]]);
    // The band reads straight across the row: the pinned cell's opaque ground carries it.
    expect(pinned9, 'the pinned cell of a striped row').toBe(desk9);
    expect(amount9).toBe(desk9);
    expect(pinned10, 'the pinned cell of an unstriped row').toBe(desk10);
    // And there is a stripe at all: the default token is faint, not transparent.
    expect(desk9, 'a striped row differs from an unstriped one').not.toBe(desk10);

    // Three rows down, the stripe has moved with its row, not stayed on the screen slot.
    await scrollRowToTop(page.locator('.ex-grid'), 3);
    await expect(rowOf(page, 16)).toBeVisible();
    expect(await isStriped(page, 9)).toBe(true);
    const [pinnedAfter, deskAfter, plainAfter] = await groundsOf(page, [[9, 0], [9, 3], [10, 3]]);
    expect(pinnedAfter).toBe(desk9);
    expect(deskAfter).toBe(desk9);
    expect(plainAfter).toBe(desk10);
});

test('a group or total row paints its own ground over the stripe and still counts (UX-16)', async ({ page }) => {
    // Parity: the roles count, so the rows after them keep their places.
    expect(await isStriped(page, 3)).toBe(true);
    expect(await isStriped(page, 4)).toBe(false);
    expect(await isStriped(page, 5)).toBe(true);
    expect(await isStriped(page, 7)).toBe(true);
    expect(await isStriped(page, 8)).toBe(false);
    expect(await isStriped(page, 13)).toBe(true);

    const [oddGroupPinned, oddGroupDesk, evenGroupPinned, evenGroupDesk, totalPinned, totalDesk, plainPinned, plainDesk, stripeDesk] =
        await groundsOf(page, [[3, 0], [3, 3], [12, 0], [12, 3], [7, 0], [7, 3], [10, 0], [10, 3], [9, 3]]);
    // A group row at an odd position looks exactly like one at an even position: the
    // group's ground replaced the stripe rather than adding to it.
    expect(oddGroupPinned).toBe(evenGroupPinned);
    expect(oddGroupDesk).toBe(evenGroupDesk);
    expect(oddGroupDesk, 'the group ground is not the stripe').not.toBe(stripeDesk);
    // A total row's ground is its rule over the plain surface; no stripe beneath it.
    expect(totalPinned).toBe(plainPinned);
    expect(totalDesk).toBe(plainDesk);
});

/**
 * Each cell's painted ground in device pixels, in its left padding and clear of its text, and the
 * device pixels down through its top edge, from the last one of the row above.
 */
async function edgesOf(page, cells) {
    // From a capture of the whole viewport, as groundsOf reads it. A capture clipped to the grid
    // lost the hover band under headed Chrome on Linux (CI, 2026-10-01): the trace's frames show the
    // band over the row after the move, and the frame of the clipped capture shows the page laid out
    // from the grid's top and no band on any row, as if the pointer, left where it was on the screen,
    // were over another part of the page. Headless Chrome on macOS kept the band.
    const size = page.viewportSize() ?? await page.evaluate(() => ({ width: innerWidth, height: innerHeight }));
    const region = await painted(page, { x: 0, y: 0, ...size });
    const device = (n) => n / region.scale;
    const out = [];
    for (const [row, column] of cells) {
        const box = await cell(page, row, column).boundingBox();
        out.push({
            ground: region.at(box.x + 4, box.y + box.height / 2),
            top: region.down(box.x + 4, box.y - device(0.5), box.y + device(2.5)),
        });
    }
    return out;
}

/**
 * What a tint paints laid once over a ground: the first colour of the element's computed
 * background image — its token, as the cascade resolved it — composited over `ground` on a
 * canvas, as the browser composites one layer over another.
 */
async function onceOver(page, locator, ground) {
    const image = await locator.evaluate((el) => getComputedStyle(el).backgroundImage);
    const tint = image.match(/rgba?\([^)]*\)/)[0];
    return page.evaluate(({ ground, tint }) => {
        const context = new OffscreenCanvas(1, 1).getContext('2d', { willReadFrequently: true });
        context.fillStyle = `rgb(${ground.join(', ')})`;
        context.fillRect(0, 0, 1, 1);
        context.fillStyle = tint;
        context.fillRect(0, 0, 1, 1);
        return Array.from(context.getImageData(0, 0, 1, 1).data.slice(0, 3));
    }, { ground, tint });
}

// A group or total row is one tint deep on every cell, as its token says (ADR-0024; ticket 85). A
// scrollable cell is transparent over the row, a pinned one opaque: a tint painted on the row and
// on the cell both is two layers deep on the first and one on the second. The group rows stand at
// a striped position (3) and an unstriped one (12), the total row at a striped one (7).
test('a group or total row paints its tint once, on its pinned and its scrollable cells alike (ADR-0024, ADR-0038)', async ({ page }) => {
    // The grid's own ground: a detail row at an even position, with nothing on it.
    const [{ ground: plain }] = await edgesOf(page, [[10, 3]]);

    for (const row of [3, 12]) {
        const cells = await edgesOf(page, [[row, 0], [row, 1], [row, 2], [row, 3]]);
        const grounds = cells.map((c) => c.ground);
        expect(grounds.every((g) => sameColour(g, grounds[0], 1)),
            `group row ${row}: pinned ${grounds[0]}, scrollable ${grounds.slice(1).join(' / ')}`).toBe(true);
        // At the shade the token intends: one layer of it over the ground, not two.
        const once = await onceOver(page, cell(page, row, 3), plain);
        expect(grounds.every((g) => sameColour(g, once, 2)),
            `group row ${row}: the tint once over ${plain} is ${once}; painted ${grounds.join(' / ')}`).toBe(true);
    }

    // A total row's tint is its rule along its top edge, over the plain ground; the row above is plain.
    const [pinned, ...scrollable] = await edgesOf(page, [[7, 0], [7, 1], [7, 2], [7, 3]]);
    for (const [i, edge] of scrollable.entries()) {
        expect(edge.top.every((pixel, j) => sameColour(pixel, pinned.top[j], 1)),
            `total row, column ${i + 1}: down through the top edge ${JSON.stringify(edge.top)}, pinned ${JSON.stringify(pinned.top)}`).toBe(true);
        expect(sameColour(edge.ground, plain, 1), `total row, column ${i + 1}: the ground below the rule`).toBe(true);
    }
    // Once, wherever the edge falls among device pixels: the darkness the rule adds down through
    // the edge is one layer's of it over the plain ground.
    const rule = await onceOver(page, cell(page, 7, 3), plain);
    const darkness = (pixels) => pixels.reduce((sum, pixel) => sum + pixel.reduce((s, v, i) => s + plain[i] - v, 0) / 3, 0);
    expect(Math.abs(darkness(pinned.top) - darkness([rule])) <= 3,
        `the rule once over ${plain} is ${rule}; painted ${JSON.stringify(pinned.top)}`).toBe(true);
});

test('the hover band reads the same over a group row\'s pinned and scrollable cells (ADR-0024, ADR-0029)', async ({ page }) => {
    // Read unhovered first: the band can stand where a real cursor left it, but not on row 12.
    await expect.poll(() => hoverBandRows(page, [12])).toEqual([]);
    const [{ ground: before }] = await edgesOf(page, [[12, 3]]);
    const box = await cell(page, 12, 2).boundingBox();
    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
    // One band in the pinned layer and one in the scrollable (ADR-0008's two layers).
    await expect.poll(() => hoverBandRows(page, [12])).toEqual([12, 12]);

    const grounds = (await edgesOf(page, [[12, 0], [12, 1], [12, 3]])).map((c) => c.ground);
    // The read itself left the band where it was: a band gone by now says the capture moved the
    // pointer, not that the cells cover the band.
    expect(await hoverBandRows(page, [12]), 'the band still stands on row 12 after the read').toEqual([12, 12]);
    expect(grounds.every((g) => sameColour(g, grounds[0], 1)),
        `hovered group row: pinned ${grounds[0]}, scrollable ${grounds.slice(1).join(' / ')}`).toBe(true);
    expect(sameColour(grounds[2], before, 1), `the band paints over the group row (${before} → ${grounds[2]})`).toBe(false);
});

test('a Cell State ground and the overlays paint over the stripe (UX-16)', async ({ page }) => {
    // A missing Amount on a striped row (5) and on an unstriped one (8): the state's
    // ground shows on both, over whatever the row is.
    const [missingStriped, stripeBeside, missingPlain, plainBeside] =
        await groundsOf(page, [[5, 1], [5, 3], [8, 1], [8, 3]]);
    expect(missingStriped, 'missing is visible on a striped row').not.toBe(stripeBeside);
    expect(missingPlain, 'missing is visible on an unstriped row').not.toBe(plainBeside);

    // The hover band paints over a striped row…
    // Read unhovered: the band can stand somewhere already — a real OS cursor over where the
    // window opened moves it (the fifth Windows run) — but not on row 11.
    await expect.poll(() => hoverBandRows(page, [11])).toEqual([]);
    const [before] = await groundsOf(page, [[11, 3]]);
    const box = await cell(page, 11, 3).boundingBox();
    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
    // One band in the pinned layer and one in the scrollable (ADR-0008's two layers), both on
    // row 11 before a pixel is read: a band merely visible may still stand on the row a real
    // cursor put it on, and reach row 11 only after the screenshot (UX-16, fifth Windows run).
    await expect.poll(() => hoverBandRows(page, [11])).toEqual([11, 11]);
    const [hovered] = await groundsOf(page, [[11, 3]]);
    expect(hovered, 'the hover band over a stripe').not.toBe(before);

    // …and so does the selection. A range of two, read at the cell that is not its Focus: the
    // Focus's own cell is left untinted (ADR-0008, 2026-09-29).
    await cell(page, 12, 3).click({ force: true });
    await cell(page, 13, 3).click({ force: true, modifiers: ['Shift'] });
    await expect(page.locator('.ex-grid .ex-range')).toHaveCount(1);
    await page.mouse.move(0, 0);
    const [selected, unselected] = await groundsOf(page, [[13, 3], [11, 3]]);
    expect(selected, 'the selection over a stripe').not.toBe(unselected);
});

test('under forced colours no stripe is painted (UX-16)', async ({ page }) => {
    await page.emulateMedia({ forcedColors: 'active' });
    // The class stays — it is the grid's statement about the row — but nothing paints it.
    expect(await isStriped(page, 9)).toBe(true);
    // One computed layer per declared one — the rule and the stripe — each of them none.
    const layers = await rowOf(page, 9).evaluate((row) => getComputedStyle(row).backgroundImage);
    expect(layers.split(',').map((layer) => layer.trim())).toEqual(['none', 'none']);

    const [pinned9, desk9, pinned10, desk10] = await groundsOf(page, [[9, 0], [9, 3], [10, 0], [10, 3]]);
    expect(pinned9).toBe(pinned10);
    expect(desk9).toBe(desk10);
});

// The grid draws its own scrollbar (ADR-0029). Any rule on ::-webkit-scrollbar makes Chrome's
// and Edge's bar a custom one, which paints no native part, so a stylesheet that meant to leave
// the native bar alone painted an empty gutter: the thumb worked and could not be seen (the
// fourth Windows run, 2026-09-29). Read from the screen, as the stripes are.
test('the vertical scrollbar paints a thumb in its gutter (ADR-0029, UX-10)', async ({ page }) => {
    const scroller = page.locator('.ex-grid .ex-scroller');
    const box = await scroller.boundingBox();
    const gutter = await scroller.evaluate((el) => el.offsetWidth - el.clientWidth);
    expect(gutter, 'the bar occupies layout').toBeGreaterThan(0);
    const x = box.x + box.width - gutter / 2;
    // At the top of a long list the thumb is at the top of the track, and the bottom of the
    // track is empty.
    const [thumb, track] = await pixelsAt(page, [[x, box.y + 12], [x, box.y + box.height - gutter - 4]]);
    expect(thumb, 'the thumb is painted: it differs from the empty track below it').not.toBe(track);
});
