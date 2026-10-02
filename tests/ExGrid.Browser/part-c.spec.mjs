import { test, expect, alterPage, watchNextKey, keySeenUntouched } from './fixtures.mjs';
import { sheet, cell, pressCell } from './sheet-helpers.mjs';
import { sameColour, resolvedColour } from './pixels.mjs';
import { across } from './line-pixels.mjs';

// What Part C of the eleventh Windows run left to the next PR, as decided on 2026-10-02 (ADR-0090;
// ADR-0071, "What was decided after Part C"; ADR-0050 item 14; ADR-0016): read on /sheet?case=…, the
// run's own set-ups, in device pixels where the criterion is about pixels. The tests named DC-59 run
// at a real 150% too, in the chrome-150 project.

const WHITE = [255, 255, 255];
const BLACK = [0, 0, 0];
const GRIDLINE = [0xe0, 0xe0, 0xe0];
const EXCEL_GREEN = [0x21, 0x73, 0x46];
const EXCEL_SHADE = [0xc7, 0xc7, 0xc7];
const HEADINGS_EDGE = [0xab, 0xab, 0xab];

/** Opens a case's Sheet once the browser has told it its Device Pixel, with the pointer off it. */
async function openCase(page, name, query = '') {
    await page.goto(`/sheet?case=${name}${query}`);
    const grid = sheet(page);
    await expect(grid.locator('.ex-row').first()).toBeVisible();
    await expect(page.locator('#sheet-positions .ex-row').first()).toBeVisible();
    await devicePixelTold(grid);
    await page.mouse.move(0, 0);
    return grid;
}

/** Waits until the grid carries the Device Pixel the browser has now (ADR-0090). */
async function devicePixelTold(grid) {
    await expect.poll(() => grid.evaluate((root) => {
        const told = /--ex-dp:\s*([\d.eE+-]+)px/.exec(root.getAttribute('style') ?? '')?.[1];
        return told !== undefined && Math.abs(Number(told) * devicePixelRatio - 1) < 1e-6;
    })).toBe(true);
}

/** Selects B2:C3 with keys, as Part C's case 11 did. */
async function selectB2toC3(page, grid) {
    await pressCell(grid, 'B2');
    await page.keyboard.press('Shift+ArrowRight');
    await page.keyboard.press('Shift+ArrowDown');
    await expect(grid.locator('.ex-range-single')).toHaveCount(1);
    await page.mouse.move(0, 0);
}

// ---- The Selection on the Paper (SH-49) ----------------------------------------------------------

test('SH-49/DC-59 (built-in Chrome): the outline is 2 Device Pixels of #217346 with a white line inside, the shade #C7C7C7, and a black line inside stays black (ADR-0071, case 11)', async ({ page }) => {
    const grid = await openCase(page, '11');
    await selectB2toC3(page, grid);
    const scale = await page.evaluate(() => devicePixelRatio);

    const outline = await resolvedColour(page, await grid.locator('.ex-range-single').first()
        .evaluate((el) => getComputedStyle(el, '::after').borderTopColor));
    expect(outline).toEqual(EXCEL_GREEN);

    // C3's bottom, across its gridline: the gridline's pixel and the one past it are the outline, the
    // first one inside is the white line, and the next is the shade over the Paper.
    const c3 = await cell(grid, 'C3').boundingBox();
    for (const side of ['bottom', 'right']) {
        const { pixel } = await across(page, c3, side, 0.5);
        for (const offset of [-1, 0]) expect(sameColour(pixel(offset), EXCEL_GREEN, 2), `the outline at ${offset} on C3's ${side} (scale ${scale}): ${pixel(offset)}`).toBe(true);
        expect(sameColour(pixel(1), EXCEL_GREEN, 2), `only two Device Pixels of outline on C3's ${side}: ${pixel(1)}`).toBe(false);
        expect(sameColour(pixel(-2), WHITE, 2), `the white line inside on C3's ${side}: ${pixel(-2)}`).toBe(true);
        expect(sameColour(pixel(-3), EXCEL_SHADE, 3), `the shade inside on C3's ${side}: ${pixel(-3)}`).toBe(true);
    }

    // The thin black line between C2 and C3 stays black over the shade, as Excel's does.
    const c2 = await cell(grid, 'C2').boundingBox();
    const inside = await across(page, c2, 'bottom', 0.5);
    expect(sameColour(inside.pixel(-1), BLACK, 2), `the line between C2 and C3: ${inside.pixel(-1)}`).toBe(true);
    expect(sameColour(inside.pixel(-2), EXCEL_SHADE, 3), `the shade above it: ${inside.pixel(-2)}`).toBe(true);
    // The Focus cell stays unshaded.
    const b2 = await cell(grid, 'B2').boundingBox();
    const focus = await across(page, b2, 'bottom', 0.25);
    expect(sameColour(focus.pixel(-3), WHITE, 2), `inside the Focus cell: ${focus.pixel(-3)}`).toBe(true);
});

test('SH-49 (built-in Chrome): while an edit is open the white line inside the outline is not drawn (ADR-0071, the fourteenth run\'s case 19)', async ({ page }) => {
    const grid = await openCase(page, '11');
    await pressCell(grid, 'C3');
    await page.mouse.move(0, 0);
    const c3 = await cell(grid, 'C3').boundingBox();
    const before = await across(page, c3, 'bottom', 0.5);
    expect(sameColour(before.pixel(-2), WHITE, 2), `the white line before the edit: ${before.pixel(-2)}`).toBe(true);
    expect(sameColour(before.pixel(-1), EXCEL_GREEN, 2), `the outline before the edit: ${before.pixel(-1)}`).toBe(true);

    await page.keyboard.press('F2');
    await expect(grid.locator('.ex-viewport .ex-editor')).toBeVisible();
    await expect(grid).toHaveClass(/ex-editing/);
    const ring = await grid.locator('.ex-focus').first().evaluate((el) => getComputedStyle(el, '::after').boxShadow);
    expect(ring).toBe('none');
    await page.keyboard.press('Escape');
});

test('SH-49/DC-59 (MudBlazor Chrome): the outline is the palette\'s primary and the shade the primary mixed into white, and a black line inside stays black (ADR-0071, case 11)', async ({ page }) => {
    const grid = await openCase(page, '11', '&chrome=mud');
    await selectB2toC3(page, grid);
    const outline = await resolvedColour(page, await grid.locator('.ex-range-single').first()
        .evaluate((el) => getComputedStyle(el, '::after').borderTopColor));
    expect(sameColour(outline, EXCEL_GREEN, 8)).toBe(false);

    const c2 = await cell(grid, 'C2').boundingBox();
    const inside = await across(page, c2, 'bottom', 0.5);
    expect(sameColour(inside.pixel(-1), BLACK, 2), `the line between C2 and C3: ${inside.pixel(-1)}`).toBe(true);
    const shade = inside.pixel(-2);
    // The primary mixed into white: lighter than the primary, and of its hue, not a grey.
    expect(Math.max(...shade) - Math.min(...shade), `the shade is tinted: ${shade}`).toBeGreaterThan(8);
    const [r, g, b] = shade;
    const [pr, pg, pb] = outline;
    expect(Math.sign(b - r), `the shade leans as the primary does: ${shade} against ${outline}`).toBe(Math.sign(pb - pr));
    expect(Math.sign(b - g)).toBe(Math.sign(pb - pg));
    expect(Math.min(...shade)).toBeGreaterThan(Math.max(...outline) - 40);
});

// ---- Device Pixels (VZ-16, VZ-17) -----------------------------------------------------------------

test('VZ-17/DC-59: a vertical gridline is one Device Pixel of #E0E0E0, and every column edge lies on a Device Pixel (ADR-0090)', async ({ page }) => {
    const grid = await openCase(page, '11');
    const scale = await page.evaluate(() => devicePixelRatio);
    // An empty row: its cells' right edges are gridlines alone.
    for (const address of ['C8', 'D8', 'E8', 'F8']) {
        const box = await cell(grid, address).boundingBox();
        const right = box.x + box.width;
        expect(Math.abs(right * scale - Math.round(right * scale)), `${address}'s right edge at ${right} CSS px (scale ${scale})`).toBeLessThan(0.02);
        const { pixel } = await across(page, box, 'right', 0.5);
        expect(sameColour(pixel(-1), GRIDLINE, 2), `${address}'s gridline (scale ${scale}): ${pixel(-1)}`).toBe(true);
        expect(sameColour(pixel(-2), WHITE, 2), `the Paper before it: ${pixel(-2)}`).toBe(true);
        expect(sameColour(pixel(0), WHITE, 2), `the Paper after it: ${pixel(0)}`).toBe(true);
    }
});

test('VZ-16: a change of resolution is told to the grid, and back again (ADR-0090, ADR-0021\'s eighth entry)', async ({ page }) => {
    const grid = await openCase(page, '11');
    const before = await page.evaluate(() => devicePixelRatio);
    const cdp = await page.context().newCDPSession(page);
    const size = page.viewportSize() ?? await page.evaluate(() => ({ width: innerWidth, height: innerHeight }));
    try {
        // A ratio between the stylesheet's steps, as a 150% display zoomed to 150% has. The pixels
        // there are device-pixel-225.spec.mjs's, which Playwright emulates from the start: an
        // override made here moves the ratio behind its screenshots' back.
        await cdp.send('Emulation.setDeviceMetricsOverride', { width: size.width, height: size.height, deviceScaleFactor: 2.25, mobile: false });
        await expect.poll(() => page.evaluate(() => devicePixelRatio)).toBe(2.25);
        await devicePixelTold(grid);
    } finally {
        await cdp.send('Emulation.clearDeviceMetricsOverride');
        await cdp.detach();
    }
    await expect.poll(() => page.evaluate(() => devicePixelRatio)).toBe(before);
    await devicePixelTold(grid);
});

// ---- The Row Headings' edge (Part C) ----------------------------------------------------------------

test('SH-46 (Part C): the Row Headings\' edge is Excel\'s #ABABAB on a light page (ADR-0071)', async ({ page }) => {
    const grid = await openCase(page, '11');
    const a8 = await cell(grid, 'A8').boundingBox();
    const { pixel } = await across(page, a8, 'left', 0.5);
    expect(sameColour(pixel(-1), HEADINGS_EDGE, 2), `the Headings' edge: ${pixel(-1)}`).toBe(true);
});

// ---- #### fills the cell (FN-12e; case 3c) -----------------------------------------------------------

test('FN-12e (Part C, case 3c): #### fills its cell with whole # in the face it is painted in, and no more fit (ADR-0016)', async ({ page }) => {
    const grid = await openCase(page, '3c');
    const target = cell(grid, 'A1');
    await expect(target.locator('.ex-hashes')).toHaveCount(1);
    const fill = await target.evaluate((el) => {
        const span = el.querySelector('.ex-hashes');
        const text = span.firstChild;
        const one = document.createRange();
        one.setStart(text, 0);
        one.setEnd(text, 1);
        const hash = one.getBoundingClientRect().width;
        const lines = [...span.getClientRects()];
        const style = getComputedStyle(el);
        const box = el.getBoundingClientRect();
        return {
            hash,
            first: { left: lines[0].left, right: lines[0].right, width: lines[0].width, top: lines[0].top },
            second: lines[1] ? { top: lines[1].top } : null,
            contentLeft: box.left + parseFloat(style.paddingLeft),
            contentRight: box.right - parseFloat(style.paddingRight),
            bottom: box.bottom,
            colour: style.color,
        };
    });
    const count = Math.round(fill.first.width / fill.hash);
    // Whole # only, inside the content box, and no room left for one more.
    expect(fill.first.right, JSON.stringify(fill)).toBeLessThanOrEqual(fill.contentRight + 0.5);
    expect(fill.first.left, JSON.stringify(fill)).toBeGreaterThanOrEqual(fill.contentLeft - 0.5);
    expect(fill.first.width + fill.hash, JSON.stringify(fill)).toBeGreaterThan(fill.contentRight - fill.contentLeft);
    // The rest of the run lies on a line the cell hides.
    expect(fill.second, JSON.stringify(fill)).not.toBeNull();
    expect(fill.second.top, JSON.stringify(fill)).toBeGreaterThanOrEqual(fill.bottom - 0.5);
    // The run keeps its section's colour, as Excel's does (case 3c).
    expect(fill.colour).toBe('rgb(255, 0, 0)');
    expect(count, JSON.stringify(fill)).toBeGreaterThan(6);
});

// ---- Ctrl+Plus and Ctrl+Minus (SH-48; case 12) --------------------------------------------------------

test('SH-48 (Part C, case 12): Ctrl+Shift+= over a whole row inserts a row above it, Ctrl+Minus deletes it, and neither reaches the page (ADR-0050 item 14)', async ({ page }) => {
    const grid = await openCase(page, '3c');
    await expect(cell(grid, 'A1')).toHaveAccessibleName('-123456789');
    await pressCell(grid, 'A1');
    await page.keyboard.press('Shift+Space');
    const zoom = await page.evaluate(() => devicePixelRatio);

    await watchNextKey(page, '+');
    await page.keyboard.press('Control+Shift+Equal');
    expect(await keySeenUntouched(page), 'the page never saw the key').toBeNull();
    await expect(cell(grid, 'A2')).toHaveAccessibleName('-123456789');
    await expect(cell(grid, 'A1')).toHaveText('');

    await watchNextKey(page, '-');
    await page.keyboard.press('Control+Minus');
    expect(await keySeenUntouched(page)).toBeNull();
    await expect(cell(grid, 'A1')).toHaveAccessibleName('-123456789');
    expect(await page.evaluate(() => devicePixelRatio)).toBe(zoom);
});

test('SH-48: over a part of a row Ctrl+Minus changes nothing and says why (ADR-0050 item 14)', async ({ page }) => {
    const grid = await openCase(page, '3c');
    await pressCell(grid, 'A1');

    await page.keyboard.press('Control+Minus');

    await expect(page.locator('.ex-sheet-notice').first()).toContainText('Nothing was deleted');
    await expect(cell(grid, 'A1')).toHaveAccessibleName('-123456789');
});
