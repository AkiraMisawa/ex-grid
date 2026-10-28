import { test, expect, alterPage, watchNextKey, keySeenUntouched, scrollRowToTop, circuitQuiet, settled, twoFrames } from './fixtures.mjs';
import { sheet, cell, at, pressCell, clickCell, expectFocusAt } from './sheet-helpers.mjs';
import { sameColour, resolvedColour, paints, painted } from './pixels.mjs';
import { across, along, pattern, expected, isDark } from './line-pixels.mjs';

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

/** Selects B2:C3 with keys, as Part C's case 11 did. Its box is one element in each layer it
 * reaches: two when the pinned boundary runs through it (ADR-0206). */
async function selectB2toC3(page, grid, layers = 1) {
    await pressCell(grid, 'B2');
    await page.keyboard.press('Shift+ArrowRight');
    await page.keyboard.press('Shift+ArrowDown');
    await expectRangeLanded(page, grid, 'B2', 'C3', layers);
}

/**
 * Waits until the range a gesture ends on is the one painted, then parks the pointer and lets the
 * circuit fall quiet, so the pixels read next are of that range. A count of boxes alone is reached
 * half way: B2:C2 already has as many as B2:C3, so a test waiting only for it could read the
 * picture of the first key's answer on the Server host (principle 6). Each box is drawn whole and
 * clipped to its side (ADR-0206), so each covers the whole range.
 */
async function expectRangeLanded(page, grid, from, to, layers) {
    const boxes = grid.locator('.ex-range-single');
    await expect(boxes).toHaveCount(layers);
    for (let i = 0; i < layers; i++) await expectSpans(grid, '.ex-range-single', i, from, to);
    await page.mouse.move(0, 0);
    await circuitQuiet();
}

/**
 * Waits until the nth element matching `selector` covers exactly the cells from..to, to within a
 * pixel. The element and the two cells are read in one evaluation, each time again: a key's answer
 * may scroll the Sheet to keep the range's end in view, so where the cells were when the wait began
 * is not where the range is painted once it has landed.
 */
async function expectSpans(grid, selector, nth, from, to) {
    const [a, b] = [at(from), at(to)];
    await expect.poll(() => grid.evaluate((root, { selector, nth, a, b }) => {
        const rect = (el) => el?.getBoundingClientRect();
        const box = rect(root.querySelectorAll(selector)[nth]);
        const first = rect(root.querySelector(`[id$='-r${a.row}c${a.column}']`));
        const last = rect(root.querySelector(`[id$='-r${b.row}c${b.column}']`));
        if (!box || !first || !last) return 'not painted';
        const want = { x: first.x, y: first.y, width: last.right - first.x, height: last.bottom - first.y };
        const near = (p, q) => Math.abs(p - q) <= 1.5;
        return near(box.x, want.x) && near(box.y, want.y) && near(box.width, want.width) && near(box.height, want.height)
            ? 'covers'
            : JSON.stringify({ box: { x: box.x, y: box.y, width: box.width, height: box.height }, want });
    }, { selector, nth, a, b }), { message: `${selector} covers ${from}:${to}` }).toBe('covers');
}

// ---- The Selection on the Paper (SH-54) ----------------------------------------------------------

test('SH-54/DC-59 (built-in Chrome): the outline is 2 Device Pixels of #217346 with a white line inside, the shade #C7C7C7, and a black line inside stays black (ADR-0071, case 11)', async ({ page }) => {
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

test('SH-54 (built-in Chrome): while an edit is open the white line inside the outline is not drawn (ADR-0071, the fourteenth run\'s case 19)', async ({ page }) => {
    const grid = await openCase(page, '11');
    await pressCell(grid, 'C3');
    await page.mouse.move(0, 0);
    // spec-lint: early-read: moving the pointer off the Sheet takes the hover band away but cannot move C3's box
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

test('SH-54/DC-59 (MudBlazor Chrome): the outline is the palette\'s primary and the shade the primary mixed into white, and a black line inside stays black (ADR-0071, case 11)', async ({ page }) => {
    const grid = await openCase(page, '11', '&chrome=mud');
    await selectB2toC3(page, grid);
    const outline = await resolvedColour(page, await grid.locator('.ex-range-single').first()
        .evaluate((el) => getComputedStyle(el, '::after').borderTopColor));
    expect(sameColour(outline, EXCEL_GREEN, 8)).toBe(false);

    // Two Device Pixels of the primary on C3's bottom, the white line inside them, as under the
    // built-in Chrome.
    const c3 = await cell(grid, 'C3').boundingBox();
    const edge = await across(page, c3, 'bottom', 0.5);
    for (const offset of [-1, 0]) expect(sameColour(edge.pixel(offset), outline, 2), `the outline at ${offset}: ${edge.pixel(offset)}`).toBe(true);
    expect(sameColour(edge.pixel(1), outline, 2), `only two Device Pixels of outline: ${edge.pixel(1)}`).toBe(false);
    expect(sameColour(edge.pixel(-2), WHITE, 2), `the white line inside: ${edge.pixel(-2)}`).toBe(true);

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

// ---- The Selection over a Pinned Column (DC-59, SH-54; ADR-0206) -----------------------------------
//
// A range's tint is an element of its own, the pinned tints lie in a shade layer of their own, and on
// the Paper that layer multiplies as one group with the pinned cells beneath it; a range across the
// pinned boundary multiplies on its scrollable side too, which a range's clip had isolated. Read on
// case 11 with ?pin=: pin=3 puts B2:C3 wholly in the Pinned Columns, pin=2 puts the boundary through it.

const PLACES = [['in a Pinned Column', 3], ['across the pinned boundary', 2]];

/** The lines of case 11 inside B2:C3 that are not the Focus's own pixels: C2's bottom (C2 and C3
 * meet), and B3's right (B3 and C3 meet), each read on its gridline pixel and the one before it. */
async function linesInside(page, grid) {
    const c2 = await across(page, await cell(grid, 'C2').boundingBox(), 'bottom', 0.5);
    const b3 = await across(page, await cell(grid, 'B3').boundingBox(), 'right', 0.5);
    return {
        'C2|C3': { line: c2.pixel(-1), shade: c2.pixel(-2) },
        'B3|C3': { line: b3.pixel(-1), shade: b3.pixel(-2) },
    };
}

/** The selection's fill as the page resolves the token: its colour and the alpha the browser holds. */
async function selectionFill(grid) {
    const css = await grid.locator('.ex-range-tint').first().evaluate((el) => getComputedStyle(el).backgroundColor);
    const m = /rgba?\(\s*([\d.]+),\s*([\d.]+),\s*([\d.]+)(?:,\s*([\d.]+))?\)/.exec(css);
    if (!m) throw new Error(`the tint's background is ${css}`);
    return { colour: [Number(m[1]), Number(m[2]), Number(m[3])], alpha: Math.round(Number(m[4] ?? 1) * 255) / 255 };
}

/** What a fill multiplied over an opaque colour paints, each channel exact: under · (1 − a + a · fill). */
const multiplied = ({ colour, alpha }, under) => under.map((u, i) => u * (1 - alpha + alpha * colour[i] / 255));

for (const [place, pin] of PLACES) {
    test(`SH-54/DC-59 (MudBlazor Chrome): ${place}, a black line inside the shade stays black and the shade is the primary mixed into white (ADR-0206, case 11)`, async ({ page }) => {
        const grid = await openCase(page, '11', `&chrome=mud&pin=${pin}`);
        await selectB2toC3(page, grid, pin === 2 ? 2 : 1);
        await expect(grid.locator('.ex-shade-pinned > .ex-range-tint')).toHaveCount(1);
        const fill = await selectionFill(grid);
        // The primary, not a grey: the shade is coloured, so a source-over tint would not leave a
        // black line black (ADR-0206: (16, 13, 41) where it did not multiply).
        expect(Math.max(...fill.colour) - Math.min(...fill.colour), JSON.stringify(fill)).toBeGreaterThan(40);

        // The outline is the palette's primary, read from the box the pinned selection layer draws,
        // and on B3's bottom, a pinned edge at either pin, it is two Device Pixels with the white line
        // inside them, as where nothing is pinned.
        const outline = await resolvedColour(page, await grid.locator('.ex-selection-pinned > .ex-range-single')
            .evaluate((el) => getComputedStyle(el, '::after').borderTopColor));
        expect(sameColour(outline, EXCEL_GREEN, 8), `the outline is not the built-in green: ${outline}`).toBe(false);
        const b3Bottom = await across(page, await cell(grid, 'B3').boundingBox(), 'bottom', 0.5);
        for (const offset of [-1, 0]) expect(sameColour(b3Bottom.pixel(offset), outline, 2), `the outline at ${offset} on B3's bottom (pin=${pin}): ${b3Bottom.pixel(offset)} against ${outline}`).toBe(true);
        expect(sameColour(b3Bottom.pixel(1), outline, 2), `only two Device Pixels of outline on B3's bottom (pin=${pin}): ${b3Bottom.pixel(1)}`).toBe(false);
        expect(sameColour(b3Bottom.pixel(-2), WHITE, 2), `the white line inside on B3's bottom (pin=${pin}): ${b3Bottom.pixel(-2)}`).toBe(true);

        const lines = await linesInside(page, grid);
        for (const [edge, { line, shade }] of Object.entries(lines)) {
            expect(sameColour(line, BLACK, 2), `the line between ${edge} (pin=${pin}): ${line}`).toBe(true);
            expect(paints(shade, multiplied(fill, WHITE)), `the shade beside the line between ${edge} (pin=${pin}): ${shade}, exact ${multiplied(fill, WHITE)}`).toBe(true);
            // The primary mixed into white: lighter than the primary, and of its hue, not a grey —
            // the shade tied to the outline, not only to whatever the token resolved to.
            expect(Math.max(...shade) - Math.min(...shade), `the shade is tinted (${edge}, pin=${pin}): ${shade}`).toBeGreaterThan(8);
            const [r, g, b] = shade;
            const [pr, pg, pb] = outline;
            expect(Math.sign(b - r), `the shade leans as the primary does (${edge}, pin=${pin}): ${shade} against ${outline}`).toBe(Math.sign(pb - pr));
            expect(Math.sign(b - g), `the shade leans as the primary does (${edge}, pin=${pin}): ${shade} against ${outline}`).toBe(Math.sign(pb - pg));
            expect(Math.min(...shade), `the shade is lighter than the primary (${edge}, pin=${pin}): ${shade} against ${outline}`).toBeGreaterThan(Math.max(...outline) - 40);
        }
        // The Focus cell stays unshaded.
        const b2 = await across(page, await cell(grid, 'B2').boundingBox(), 'bottom', 0.25);
        expect(sameColour(b2.pixel(-3), WHITE, 2), `inside the Focus cell: ${b2.pixel(-3)}`).toBe(true);
    });

    test(`SH-54/DC-59 (built-in Chrome): ${place}, the shade is #C7C7C7, a black line inside stays black, and the Focus is unshaded (ADR-0206, case 11)`, async ({ page }) => {
        const grid = await openCase(page, '11', `&pin=${pin}`);
        await selectB2toC3(page, grid, pin === 2 ? 2 : 1);
        await expect(grid.locator('.ex-shade-pinned > .ex-range-tint')).toHaveCount(1);

        const lines = await linesInside(page, grid);
        for (const [edge, { line, shade }] of Object.entries(lines)) {
            expect(sameColour(line, BLACK, 2), `the line between ${edge} (pin=${pin}): ${line}`).toBe(true);
            expect(sameColour(shade, EXCEL_SHADE, 3), `the shade beside the line between ${edge} (pin=${pin}): ${shade}`).toBe(true);
        }
        // C3's bottom: the outline's two Device Pixels, the white line inside them, then the shade,
        // as where nothing is pinned.
        const c3 = await across(page, await cell(grid, 'C3').boundingBox(), 'bottom', 0.5);
        for (const offset of [-1, 0]) expect(sameColour(c3.pixel(offset), EXCEL_GREEN, 2), `the outline at ${offset} on C3's bottom: ${c3.pixel(offset)}`).toBe(true);
        expect(sameColour(c3.pixel(-2), WHITE, 2), `the white line inside: ${c3.pixel(-2)}`).toBe(true);
        expect(sameColour(c3.pixel(-3), EXCEL_SHADE, 3), `the shade inside C3: ${c3.pixel(-3)}`).toBe(true);
        const b2 = await across(page, await cell(grid, 'B2').boundingBox(), 'bottom', 0.25);
        expect(sameColour(b2.pixel(-3), WHITE, 2), `inside the Focus cell: ${b2.pixel(-3)}`).toBe(true);
    });
}

test('DC-59: a Consumer\'s coloured --ex-selection-fill multiplies over a pinned Fill and a pinned line, in a Pinned Column and across the boundary (ADR-0206, ADR-0071)', async ({ page }) => {
    for (const [place, pin] of PLACES) {
        // Case 10: B2's thick black bottom, and B3's yellow Fill below it.
        const grid = await openCase(page, '10', `&pin=${pin}`);
        // A Consumer's coloured token, on body, which outlives the page: alterPage takes it off.
        await alterPage(page, () => {
            document.body.style.setProperty('--ex-selection-fill', 'rgba(0, 0, 255, 0.4)');
            return () => document.body.style.removeProperty('--ex-selection-fill');
        });
        // B2:C4 with the Focus in C4, so B2 and B3 are shaded.
        await pressCell(grid, 'C4');
        await page.keyboard.press('Shift+ArrowLeft');
        await page.keyboard.press('Shift+ArrowUp');
        await page.keyboard.press('Shift+ArrowUp');
        await expectRangeLanded(page, grid, 'B2', 'C4', pin === 3 ? 1 : 2);
        const fill = await selectionFill(grid);
        expect(fill.colour, place).toEqual([0, 0, 255]);

        // B2's thick bottom is three Device Pixels, on B2's last two and B3's first: black, under
        // the shade as over the Paper. Multiplied, blue over black is black; laid over, it would not be.
        // spec-lint: early-read: expectRangeLanded above waited for the range's boxes and for circuitQuiet(); the lint does not know that helper
        const b2 = await across(page, await cell(grid, 'B2').boundingBox(), 'bottom', 0.5);
        for (const offset of [-2, -1, 0]) {
            expect(sameColour(b2.pixel(offset), BLACK, 2), `B2's thick bottom at ${offset} (${place}): ${b2.pixel(offset)}`).toBe(true);
        }
        // B3's yellow, darkened by the blue rather than tinted with it: (153, 153, 0), where a tint
        // laid over would be (153, 153, 102).
        const yellow = [255, 255, 0];
        // spec-lint: early-read: the range landed and the circuit went quiet in expectRangeLanded; nothing was pressed since
        const b3 = await across(page, await cell(grid, 'B3').boundingBox(), 'bottom', 0.5);
        expect(paints(b3.pixel(-4), multiplied(fill, yellow)), `B3's Fill under the shade (${place}): ${b3.pixel(-4)}`).toBe(true);
        // And the Paper beside it, C3, in the same shade over white either way.
        // spec-lint: early-read: the range landed and the circuit went quiet in expectRangeLanded; nothing was pressed since
        const c3 = await across(page, await cell(grid, 'C3').boundingBox(), 'bottom', 0.5);
        expect(paints(c3.pixel(-4), multiplied(fill, WHITE)), `C3's Paper under the shade (${place}): ${c3.pixel(-4)}`).toBe(true);
    }
});

/**
 * Reads a line on a Sheet cell's `side` against case 9's table, as sheet-borders.spec.mjs does:
 * which device pixels across the gridline are dark, and the dash pattern along it.
 */
async function expectLine(page, grid, style, side) {
    const row = STYLES_IN_ORDER.indexOf(style) + 2;
    await scrollRowToTop(grid, Math.max(0, row - 2));
    const target = cell(grid, `${side === 'bottom' ? 'B' : 'D'}${row}`);
    await expect(target).toBeVisible();
    await page.mouse.move(0, 0);
    // spec-lint: early-read: moving the pointer off the Sheet takes the hover band away but cannot move the target cell's box
    const box = await target.boundingBox();
    const { scale, pixel } = await across(page, box, side, 0.5);
    const want = expected(style);
    for (const offset of want.light) {
        const pixels = await along(page, box, side, offset);
        expect(pixels.filter(isDark).length, `${style} on ${side} leaves ${offset} clear (scale ${scale})`).toBe(0);
    }
    if (want.dark) {
        for (const offset of want.dark) {
            expect(isDark(pixel(offset)), `${style} on ${side} at ${offset} (scale ${scale}): ${pixel(offset)}`).toBe(true);
            const pixels = await along(page, box, side, offset);
            expect(pixels.every(isDark), `${style} on ${side} is solid along ${offset} (scale ${scale})`).toBe(true);
        }
    } else {
        for (const offset of want.rows) {
            const lengths = pattern(await along(page, box, side, offset)).slice(0, want.pattern.length);
            expect(lengths.length, `${style} on ${side} along ${offset}: runs read`).toBeGreaterThanOrEqual(3);
            expect(lengths, `${style} on ${side} along ${offset} (scale ${scale})`).toEqual(want.pattern.slice(0, lengths.length));
        }
    }
}

const STYLES_IN_ORDER = ['Hair', 'Thin', 'Medium', 'Thick', 'Double', 'Dotted', 'Dashed', 'DashDot', 'DashDotDot',
    'MediumDashed', 'MediumDashDot', 'MediumDashDotDot', 'SlantedDashDot'];

// The sub-pixel gate (ADR-0206). A blend above the sticky pinned cells may make the browser read its
// backdrop from an offscreen surface, which at a fraction of a Device Pixel could blur every line the
// Viewport holds. So with a pinned tint on screen and the page a third of a pixel across and down, the
// lines outside the range — a pinned column's and a scrollable one's — keep their exact Device Pixels.
// If this fails, the work stops and returns to the user as a decision; offsets are never tuned to it.
test('DC-59: with a pinned tint on screen and the page a third of a pixel across and down, the lines outside the range keep their exact Device Pixels (ADR-0206, the sub-pixel gate)', async ({ page }) => {
    // The thirteen styles on B's bottoms and D's rights, with A and B pinned.
    const grid = await openCase(page, 'lines', '&pin=2');
    // Padding on the page's own element around the Sheet, which leaves with the page (as
    // sheet-borders.spec.mjs shifts it): a margin of a third of a pixel would collapse.
    await page.locator('.demo-side-by-side').evaluate((el) => {
        el.style.paddingTop = '0.33px';
        el.style.paddingLeft = '0.33px';
    });
    await devicePixelTold(grid);
    // A2:A14, the style names beside the lines: a pinned range, its tint in the shade layer, and
    // nothing of it in the scrollable layer. A second range, F3, takes the Focus, so A2:A14 is one
    // of several and carries no outline: a single range's outline lies one pixel outside it, on B's
    // first pixels, where it would be read as part of a line.
    await pressCell(grid, 'A2');
    for (let i = 0; i < 12; i++) await page.keyboard.press('Shift+ArrowDown');
    // The twelfth key answered, and nothing else on its way, before F3's place is read. The last keys'
    // answers each scroll the Sheet a little to keep A14 in view, so on the Server host a place read
    // while they were still landing was a row off by the time of the press, which took F4 (found
    // here, 3 runs of 6; principle 6).
    await expectSpans(grid, '.ex-selection-pinned > .ex-range-single', 0, 'A2', 'A14');
    await circuitQuiet();
    await clickCell(grid, 'F3', { modifiers: ['ControlOrMeta'] });
    await expectFocusAt(grid, 'F3');
    await expect(grid.locator('.ex-range-single')).toHaveCount(0);
    await expect(grid.locator('.ex-shade-pinned > .ex-range-tint')).toHaveCount(1);
    await expect(grid.locator('.ex-selection > .ex-range-tint')).toHaveCount(0);
    // The blend this gates is on: ExSheet's Paper multiplies the shade layer. Were its rule to stop
    // matching, the lines would keep their pixels for want of a blend, and this would gate nothing.
    expect(await grid.locator('.ex-shade-pinned').evaluate((el) => getComputedStyle(el).mixBlendMode)).toBe('multiply');
    for (const style of ['Thin', 'Thick', 'Double', 'Dotted', 'MediumDashDotDot']) {
        for (const side of ['bottom', 'right']) {
            await expect(grid.locator('.ex-shade-pinned > .ex-range-tint'), 'the pinned tint stays on screen').toHaveCount(1);
            await expectLine(page, grid, style, side);
        }
    }
});

test('DC-59: scrolling with column A selected, its shade stays on column A down and across, and never over the cells that pass beneath it (ADR-0206)', async ({ page }) => {
    const grid = await openCase(page, '11', '&pin=1');
    await pressCell(grid, 'A5');
    await page.keyboard.press('Control+Space');
    await expect(grid.locator('.ex-shade-pinned > .ex-range-tint')).toHaveCount(1);
    await page.mouse.move(0, 0);

    for (const [down, sideways] of [[0, 0], [0, 150], [40, 150], [40, 0]]) {
        await scrollRowToTop(grid, down);
        await grid.locator('.ex-scroller').evaluate((scroller, left) => { scroller.scrollLeft = left; }, sideways);
        await expect.poll(() => grid.locator('.ex-scroller').evaluate((s) => s.scrollLeft)).toBe(sideways);
        // The browser tells the grid of the offset in the frame after it is written: read once the grid has
        // heard it, and settled from it.
        await twoFrames(page);
        await circuitQuiet();
        await settled(grid);
        // A row clear of the Focus's, two below the top of the readable area, and the first cell of it
        // wholly clear of column A's edge, whichever column has scrolled there: read at their middles,
        // away from every gridline.
        // A jump of forty rows is a fling: its rows are Placeholders until they settle (ADR-0004).
        await expect(grid.locator('.ex-row.ex-placeholder')).toHaveCount(0);
        const row = down + 3;
        const box = await cell(grid, `A${row}`).boundingBox();
        const nextCell = () => grid.evaluate((root, { row, edge }) => {
            for (let column = 1; column < 64; column++) {
                const el = root.querySelector(`[id$='-r${row - 1}c${column}']`);
                const rect = el?.getBoundingClientRect();
                if (rect && rect.left >= edge + 1) return { x: rect.left, width: rect.width };
            }
            return null;
        }, { row, edge: box.x + box.width });
        await expect.poll(nextCell, { message: `a painted cell beside column A in row ${row}` }).not.toBeNull();
        const next = await nextCell();
        const region = await painted(page, { x: box.x, y: box.y, width: next.x + next.width - box.x, height: box.height });
        const y = box.y + box.height / 2;
        const inA = region.at(box.x + box.width / 2, y);
        const beside = region.at(next.x + next.width / 2, y);
        expect(sameColour(inA, EXCEL_SHADE, 3), `A${row} is shaded, ${down} rows down and ${sideways}px across: ${inA}`).toBe(true);
        // Whatever column lies beside A at this offset is not: the scrollable layer holds no part of
        // the range, and the pinned tint travels with column A, not with what passes beneath it.
        expect(sameColour(beside, WHITE, 2), `the cell beside column A, ${down} rows down and ${sideways}px across: ${beside}`).toBe(true);
    }
});

test('DC-59: under forced colors no tint is painted over a pinned range, and its outline is (ADR-0206)', async ({ page }) => {
    const grid = await openCase(page, '11', '&pin=3');
    await selectB2toC3(page, grid);
    await page.emulateMedia({ forcedColors: 'active' });
    await expect.poll(() => grid.locator('.ex-selection-pinned > .ex-range').first().evaluate((el) => getComputedStyle(el, '::after').borderTopStyle)).toBe('solid');
    const read = await grid.evaluate((root) => ({
        tints: [...root.querySelectorAll('.ex-range-tint')].map((el) => getComputedStyle(el).display),
        blend: getComputedStyle(root.querySelector('.ex-shade-pinned')).mixBlendMode,
    }));
    expect(read.tints.length).toBeGreaterThan(0);
    expect(read.tints.every((display) => display === 'none'), JSON.stringify(read)).toBe(true);
    expect(read.blend).toBe('normal');
    // Inside C3, selected and pinned, the ground is the unselected cells' own.
    const c3 = await across(page, await cell(grid, 'C3').boundingBox(), 'bottom', 0.5);
    const e8 = await across(page, await cell(grid, 'E8').boundingBox(), 'bottom', 0.5);
    expect(c3.pixel(-5), 'C3 is not tinted').toEqual(e8.pixel(-5));
});

for (const [chrome, query] of [['built-in', ''], ['MudBlazor', '&chrome=mud']]) {
    test(`DC-59/SH-39 (${chrome} Chrome, dark scheme): across the pinned boundary the shade is the same on both sides and a black line inside stays black, as in the light scheme (ADR-0206, ADR-0071)`, async ({ page }) => {
        const grid = await openCase(page, '11', `${query}&pin=2&scheme=dark`);
        await selectB2toC3(page, grid, 2);
        await expect(grid.locator('.ex-shade-pinned > .ex-range-tint')).toHaveCount(1);
        const lines = await linesInside(page, grid);
        for (const [edge, { line }] of Object.entries(lines)) {
            expect(sameColour(line, BLACK, 2), `the line between ${edge}: ${line}`).toBe(true);
        }
        // B3 pinned and C3 scrollable, side by side under one shade over the white Paper.
        const pinned = lines['B3|C3'].shade;
        const scrollable = (await across(page, await cell(grid, 'C2').boundingBox(), 'bottom', 0.5)).pixel(-2);
        expect(sameColour(pinned, scrollable, 1), `pinned ${pinned} against scrollable ${scrollable}`).toBe(true);
        if (chrome === 'built-in') {
            expect(sameColour(pinned, EXCEL_SHADE, 3), `the shade over the Paper: ${pinned}`).toBe(true);
        }
    });
}

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

// VZ-16's change of resolution is not driven from here. A CDP device-metrics override moves
// devicePixelRatio in Playwright's Chromium and fires the resolution query's change, but in the
// branded Chrome and Edge CI runs it fires no change at all (every run, both hosts), so the test
// could not tell the grid from the browser. What C# does with a change is DevicePixelTests'; the
// report at attach is device-pixel-225.spec.mjs's; a real zoom is read by hand on Windows.

// ---- The Row Headings' edge (Part C) ----------------------------------------------------------------

test('Part C (ticket 146): the Row Headings\' edge is Excel\'s #ABABAB on a light page (ADR-0071)', async ({ page }) => {
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

// ---- Ctrl+Plus and Ctrl+Minus (SH-53; case 12) --------------------------------------------------------

test('SH-53 (Part C, case 12): Ctrl+Shift+= over a whole row inserts a row above it, Ctrl+Minus deletes it, and neither reaches the page (ADR-0050 item 14)', async ({ page }) => {
    const grid = await openCase(page, '3c');
    await expect(cell(grid, 'A1')).toHaveAccessibleName('-123456789');
    await pressCell(grid, 'A1');
    await page.keyboard.press('Shift+Space');
    // spec-lint: early-read: devicePixelRatio is the browser's zoom, which selecting a row cannot change
    const zoom = await page.evaluate(() => devicePixelRatio);

    await watchNextKey(page, '+');
    await page.keyboard.press('Control+Shift+Equal');
    // spec-lint: early-read: the keydown listener records the key inside the press's own dispatch, so the answer is final once the press returns
    expect(await keySeenUntouched(page), 'the page never saw the key').toBeNull();
    await expect(cell(grid, 'A2')).toHaveAccessibleName('-123456789');
    await expect(cell(grid, 'A1')).toHaveText('');

    await watchNextKey(page, '-');
    await page.keyboard.press('Control+Minus');
    // spec-lint: early-read: the keydown listener records the key inside the press's own dispatch, so the answer is final once the press returns
    expect(await keySeenUntouched(page)).toBeNull();
    await expect(cell(grid, 'A1')).toHaveAccessibleName('-123456789');
    // keySeenUntouched above is what says the page never had the key; the ratio is read only to show
    // nothing zoomed meanwhile.
    expect(await page.evaluate(() => devicePixelRatio)).toBe(zoom);
});

test('SH-53: Ctrl+Shift+= over a whole column inserts a column to its left, and Ctrl+Minus deletes it (ADR-0050 item 14)', async ({ page }) => {
    const grid = await openCase(page, '3c');
    await pressCell(grid, 'A1');
    await page.keyboard.press('Control+Space');

    await page.keyboard.press('Control+Shift+Equal');
    await expect(cell(grid, 'B1')).toHaveAccessibleName('-123456789');
    await expect(cell(grid, 'A1')).toHaveText('');

    await page.keyboard.press('Control+Minus');
    await expect(cell(grid, 'A1')).toHaveAccessibleName('-123456789');
});
