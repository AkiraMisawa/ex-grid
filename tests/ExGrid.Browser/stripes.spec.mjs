import { test, expect } from './fixtures.mjs';

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
    await page.locator('.ex-grid .ex-scroller').evaluate((s) => { s.scrollTop = 3 * 28; });
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

test('a Cell State ground and the overlays paint over the stripe (UX-16)', async ({ page }) => {
    // A missing Amount on a striped row (5) and on an unstriped one (8): the state's
    // ground shows on both, over whatever the row is.
    const [missingStriped, stripeBeside, missingPlain, plainBeside] =
        await groundsOf(page, [[5, 1], [5, 3], [8, 1], [8, 3]]);
    expect(missingStriped, 'missing is visible on a striped row').not.toBe(stripeBeside);
    expect(missingPlain, 'missing is visible on an unstriped row').not.toBe(plainBeside);

    // The hover band paints over a striped row…
    const [before] = await groundsOf(page, [[11, 3]]);
    const box = await cell(page, 11, 3).boundingBox();
    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
    // One band in the pinned layer and one in the scrollable (ADR-0008's two layers).
    await expect(page.locator('.ex-grid .ex-hover-row').first()).toBeVisible();
    const [hovered] = await groundsOf(page, [[11, 3]]);
    expect(hovered, 'the hover band over a stripe').not.toBe(before);

    // …and so do the selection and the Focus band.
    await cell(page, 13, 3).click({ force: true });
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
