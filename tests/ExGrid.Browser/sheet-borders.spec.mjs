import { test, expect, scrollRowToTop } from './fixtures.mjs';
import { sheet, cell, pressCell } from './sheet-helpers.mjs';
import { sameColour, resolvedColour } from './pixels.mjs';
import { STYLES, across, along, pattern, expected, isDark, isLight } from './line-pixels.mjs';

// ExSheet's Borders as Excel draws them (ticket 49; ADR-0071; ADR-0050 item 15; DC-59, SH-46), read
// in device pixels on /sheet?case=…, the eleventh, twelfth and fourteenth Windows runs' set-ups
// (SheetCases): Excel's thirteen line styles on the Paper with its gridlines, a Fill over the
// gridlines, two Fills meeting, a line over a Fill, a double line's middle over a Fill, the dashes,
// the Selection over lines, the line two cells both record, a line on column A's left, and rows that
// keep their one height.
//
// The line tests run at 100% here and at 150% in the chrome-150 project, whose display scale is set
// on Chrome's command line as an OS sets it; Excel's pixels at both zooms are case 9's table, and
// the fourteenth run's cases 16 to 18 at a 150% display.

const WHITE = [255, 255, 255];
const GRIDLINE = [0xe0, 0xe0, 0xe0];
const YELLOW = [255, 255, 0];
const LIGHT_BLUE = [0x00, 0xb0, 0xf0];
const RED = [255, 0, 0];

/** Opens a case's Sheet, with the pointer off it. */
async function openCase(page, name) {
    await page.goto(`/sheet?case=${name}`);
    await expect(sheet(page).locator('.ex-row').first()).toBeVisible();
    // The page pushes its Linked Table 1.5 s after the Sheet opens, which repaints rows: reads wait.
    await expect(page.locator('#sheet-positions .ex-row').first()).toBeVisible();
    await page.mouse.move(0, 0);
}

/**
 * Scrolls the Sheet so that the row above `address` stands at its top, and the page to it. At 150%
 * the Sheet's extent is compressed under the Layout Ceiling (ADR-0053), so the slice is painted at
 * a fraction of a pixel unless the grid puts it on one: these reads are made scrolled, not only at
 * the top.
 */
async function reveal(page, address) {
    const grid = sheet(page);
    const row = Number(/\d+$/.exec(address)[0]);
    await scrollRowToTop(grid, Math.max(0, row - 2));
    const target = cell(grid, address);
    await expect(target).toBeVisible();
    await target.scrollIntoViewIfNeeded();
    await page.mouse.move(0, 0);
    return target;
}

/**
 * Reads a line in `style` on a Sheet cell's `side`, scrolled to it, against case 9's table: which
 * device pixels across the gridline are dark, and the dash pattern along it.
 */
async function expectLine(page, style, side) {
    // The styles stand on rows 2 to 14: on B's bottom, and on D's right.
    const target = await reveal(page, `${side === 'bottom' ? 'B' : 'D'}${STYLES.indexOf(style) + 2}`);
    const box = await target.boundingBox();
    const { scale, pixel } = await across(page, box, side, 0.5);
    const want = expected(style);
    for (const offset of want.light) {
        const pixels = await along(page, box, side, offset);
        expect(pixels.filter(isDark).length, `${style} leaves ${offset} clear`).toBe(0);
    }
    if (want.dark) {
        for (const offset of want.dark) expect(isDark(pixel(offset)), `${style} at ${offset} (scale ${scale})`).toBe(true);
        // Excel's double: two lines with the gridline's pixel white between them, the Paper rather
        // than the gridline (case 9).
        if (style === 'Double') expect(sameColour(pixel(-1), WHITE, 4), 'the gridline between a double line\'s two is white').toBe(true);
        for (const offset of want.dark) {
            const pixels = await along(page, box, side, offset);
            expect(pixels.every(isDark), `${style} is solid along ${offset}`).toBe(true);
        }
    } else {
        for (const offset of want.rows) {
            const lengths = pattern(await along(page, box, side, offset)).slice(0, want.pattern.length);
            expect(lengths.length, `${style} along ${offset}: runs read`).toBeGreaterThanOrEqual(3);
            expect(lengths, `${style} along ${offset} (scale ${scale})`).toEqual(want.pattern.slice(0, lengths.length));
        }
    }
}

test.describe('DC-59: lines on the Sheet', () => {
    for (const style of STYLES) {
        for (const side of ['bottom', 'right']) {
            test(`DC-59/SH-46: a ${style} line on a Sheet cell's ${side} edge is drawn as Excel draws it, centred on the gridline, at the run's scale (ADR-0071, case 9)`, async ({ page }, testInfo) => {
                await openCase(page, 'lines');
                expect(await page.evaluate(() => devicePixelRatio)).toBe(testInfo.project.name === 'chrome-150' ? 1.5 : 1);
                await expectLine(page, style, side);
            });
        }
    }

    // Where the page puts the Sheet is the page's: a line of text above it in another font puts it
    // at a fraction of a device pixel. CI's Linux fonts did, at 150% on the Server host, and a dotted
    // line on a right edge read 2, 1, 3, 1. The lines are still on the device pixels there.
    test('DC-59: the lines stay on the device pixels wherever the page puts the Sheet, a third of a pixel across and down (ADR-0071, case 9)', async ({ page }) => {
        await openCase(page, 'lines');
        // The page's own element around the Sheet, which leaves with the page. Padding, not a
        // margin: a margin of a third of a pixel collapses into its neighbour's, and the Sheet stayed
        // where it was down the page.
        await page.locator('.demo-side-by-side').evaluate((el) => {
            el.style.paddingTop = '0.33px';
            el.style.paddingLeft = '0.33px';
        });
        for (const style of ['Thin', 'Thick', 'Double', 'Dotted', 'MediumDashDotDot']) {
            for (const side of ['bottom', 'right']) {
                await expectLine(page, style, side);
            }
        }
    });

    test('DC-59: rows keep their one height where Excel raises them for a medium or a thick line (ADR-0071, ADR-0013, cases 8 and 9)', async ({ page }) => {
        await openCase(page, 'lines');
        const heights = await sheet(page).locator('.ex-row').evaluateAll((rows) => rows.map((row) => row.getBoundingClientRect().height));
        expect(heights.length).toBeGreaterThan(10);
        expect(new Set(heights)).toEqual(new Set([28]));
    });
});

test.describe('SH-46: Fills and lines beside Excel\'s', () => {
    test('SH-46: a Fill covers the four gridlines at its edges, and the pixel past each is the neighbour\'s Paper (ADR-0071, case 4)', async ({ page }) => {
        await openCase(page, '4');
        const box = await cell(sheet(page), 'B2').boundingBox();
        const read = async (side) => (await across(page, box, side, 0.5)).pixel;
        // The bottom and right gridlines are B2's own last pixels; the top and left are B1's and
        // A2's, which paint them in B2's colour.
        const bottom = await read('bottom');
        const right = await read('right');
        const top = await read('top');
        const left = await read('left');
        expect(sameColour(bottom(-1), YELLOW, 2), 'the bottom gridline').toBe(true);
        expect(sameColour(right(-1), YELLOW, 2), 'the right gridline').toBe(true);
        expect(sameColour(top(-1), YELLOW, 2), 'the top gridline').toBe(true);
        expect(sameColour(left(-1), YELLOW, 2), 'the left gridline').toBe(true);
        for (const [name, pixel] of [['below', bottom(0)], ['right', right(0)], ['above', top(-2)], ['left', left(-2)]]) {
            expect(sameColour(pixel, WHITE, 2), `the neighbour's Paper ${name}`).toBe(true);
        }
        // An unfilled cell's gridline, for comparison: Excel's #e0e0e0.
        const plain = await across(page, await cell(sheet(page), 'D4').boundingBox(), 'bottom', 0.5);
        expect(sameColour(plain.pixel(-1), GRIDLINE, 2), `a gridline is Excel's (${plain.pixel(-1)})`).toBe(true);
    });

    test('SH-46: a white Fill takes the gridlines round its cell away (ADR-0071, case 5)', async ({ page }) => {
        await openCase(page, '5');
        const box = await cell(sheet(page), 'B2').boundingBox();
        for (const side of ['bottom', 'right', 'top', 'left']) {
            const { pixel } = await across(page, box, side, 0.5);
            expect(sameColour(pixel(-1), WHITE, 2), `B2's ${side} gridline is white`).toBe(true);
        }
    });

    test('SH-46: between two filled cells the gridline is the Fill\'s (ADR-0071, case 6)', async ({ page }) => {
        await openCase(page, '6');
        const box = await cell(sheet(page), 'B2').boundingBox();
        const { pixel } = await across(page, box, 'right', 0.5);
        for (const offset of [-3, -2, -1, 0, 1, 2]) expect(sameColour(pixel(offset), YELLOW, 2), `yellow at ${offset}`).toBe(true);
    });

    test('SH-46/DC-59: between two filled cells one above the other the gridline is the lower cell\'s Fill, and each Fill still covers its other gridlines (ADR-0071, the fourteenth run\'s case 16)', async ({ page }, testInfo) => {
        await openCase(page, '14-16');
        expect(await page.evaluate(() => devicePixelRatio)).toBe(testInfo.project.name === 'chrome-150' ? 1.5 : 1);
        const grid = sheet(page);
        const b2 = await cell(grid, 'B2').boundingBox();
        const b3 = await cell(grid, 'B3').boundingBox();
        // Excel: column B runs yellow, then light blue from the gridline between B2 and B3 on, which is
        // one device pixel at a 150% display.
        const between = (await across(page, b2, 'bottom', 0.5)).pixel;
        expect(sameColour(between(-1), LIGHT_BLUE, 2), `the gridline between B2 and B3 is B3's (${between(-1)})`).toBe(true);
        expect(sameColour(between(0), LIGHT_BLUE, 2), 'B3 after it').toBe(true);
        expect(sameColour(between(-2), YELLOW, 2), `B2 up to it (${between(-2)})`).toBe(true);
        // The other gridlines round each cell are its own Fill's: above B2 and left of it, B1's and
        // A2's pixels; right of it, B2's own, C2 being unfilled; and round B3 the same.
        for (const [name, box, side, colour] of [
            ['above B2', b2, 'top', YELLOW], ['left of B2', b2, 'left', YELLOW], ['right of B2', b2, 'right', YELLOW],
            ['left of B3', b3, 'left', LIGHT_BLUE], ['right of B3', b3, 'right', LIGHT_BLUE], ['below B3', b3, 'bottom', LIGHT_BLUE],
        ]) {
            const { pixel } = await across(page, box, side, 0.5);
            expect(sameColour(pixel(-1), colour, 2), `the gridline ${name} (${pixel(-1)})`).toBe(true);
        }
    });

    test('SH-46/DC-59: between two filled cells side by side the gridline is the right cell\'s Fill (ADR-0071, read from the fourteenth run\'s case 16)', async ({ page }) => {
        await openCase(page, 'fills');
        const { pixel } = await across(page, await cell(sheet(page), 'B2').boundingBox(), 'right', 0.5);
        expect(sameColour(pixel(-1), LIGHT_BLUE, 2), `the gridline between B2 and C2 is C2's (${pixel(-1)})`).toBe(true);
        expect(sameColour(pixel(0), LIGHT_BLUE, 2), 'C2 after it').toBe(true);
        expect(sameColour(pixel(-2), YELLOW, 2), `B2 up to it (${pixel(-2)})`).toBe(true);
    });

    test('SH-46/DC-59: a double line\'s middle pixel is the Fill its gridline would show — dark, the Fill, dark (ADR-0071, the fourteenth run\'s case 17)', async ({ page }) => {
        await openCase(page, '14-17');
        const { pixel } = await across(page, await cell(sheet(page), 'B2').boundingBox(), 'bottom', 0.5);
        expect(isDark(pixel(-2)), 'the line above the gridline').toBe(true);
        expect(sameColour(pixel(-1), YELLOW, 2), `the gridline between the two is B2's Fill (${pixel(-1)})`).toBe(true);
        expect(isDark(pixel(0)), 'the line past the gridline').toBe(true);
        expect(sameColour(pixel(-3), YELLOW, 2), 'B2 above the line').toBe(true);
        expect(sameColour(pixel(1), WHITE, 2), 'B3\'s Paper below it').toBe(true);
    });

    test('SH-46/DC-59: a double line between Fills shows the lower or right cell\'s in its middle, else the upper or left cell\'s (ADR-0071, read from the fourteenth run\'s cases 16 and 17)', async ({ page }) => {
        await openCase(page, 'fills');
        const grid = sheet(page);
        // [cell, side, the middle pixel, the cell's own ground before the line, the neighbour's after it]
        for (const [address, side, middle, before, after] of [
            ['E2', 'bottom', YELLOW, WHITE, YELLOW],
            ['B8', 'bottom', LIGHT_BLUE, YELLOW, LIGHT_BLUE],
            ['B5', 'right', LIGHT_BLUE, YELLOW, LIGHT_BLUE],
            ['E5', 'right', YELLOW, WHITE, YELLOW],
        ]) {
            const { pixel } = await across(page, await cell(grid, address).boundingBox(), side, 0.5);
            expect(isDark(pixel(-2)), `${address}'s ${side}: the line before the gridline`).toBe(true);
            expect(sameColour(pixel(-1), middle, 2), `${address}'s ${side}: the middle pixel (${pixel(-1)})`).toBe(true);
            expect(isDark(pixel(0)), `${address}'s ${side}: the line past the gridline`).toBe(true);
            expect(sameColour(pixel(-3), before, 2), `${address}'s ${side}: its own ground (${pixel(-3)})`).toBe(true);
            expect(sameColour(pixel(1), after, 2), `${address}'s ${side}: the neighbour's ground (${pixel(1)})`).toBe(true);
        }
    });

    test('SH-46/DC-59: medium dashed is 9 on and 3 off, and dashed 3 on and 1 off, in device pixels at the run\'s scale (ADR-0071, the fourteenth run\'s case 18)', async ({ page }, testInfo) => {
        await openCase(page, '14-18');
        expect(await page.evaluate(() => devicePixelRatio)).toBe(testInfo.project.name === 'chrome-150' ? 1.5 : 1);
        const grid = sheet(page);
        for (const [address, rows, want] of [['B2', [-2, -1], [9, 3, 9, 3, 9]], ['B4', [-1], [3, 1, 3, 1, 3, 1, 3]]]) {
            const box = await cell(grid, address).boundingBox();
            for (const offset of rows) {
                const lengths = pattern(await along(page, box, 'bottom', offset));
                expect(lengths.slice(0, want.length), `${address} along ${offset}`).toEqual(want);
            }
        }
    });

    test('SH-46/DC-59: a thick line lies above the Fill below it, a pixel into the filled cell (ADR-0071, case 10)', async ({ page }) => {
        await openCase(page, '10');
        const box = await cell(sheet(page), 'B2').boundingBox();
        const { pixel } = await across(page, box, 'bottom', 0.5);
        for (const offset of [-2, -1, 0]) expect(isDark(pixel(offset)), `the line at ${offset}`).toBe(true);
        expect(sameColour(pixel(1), YELLOW, 2), 'B3\'s Fill after it').toBe(true);
        expect(sameColour(pixel(-3), WHITE, 2), 'B2\'s Paper above it').toBe(true);
    });

    test('SH-46: where both cells record a line on an edge, the left cell\'s is drawn and the other\'s not at all (ADR-0071, the twelfth run\'s case 1)', async ({ page }) => {
        await openCase(page, '12-1');
        const box = await cell(sheet(page), 'B2').boundingBox();
        const { pixel } = await across(page, box, 'right', 0.5);
        // B2's thick red right: a pixel each side of the gridline; C2's own thin blue left lies
        // under it, and nothing of it shows.
        for (const offset of [-2, -1, 0]) expect(sameColour(pixel(offset), RED, 8), `red at ${offset}`).toBe(true);
        expect(sameColour(pixel(1), WHITE, 2), 'the Paper past it').toBe(true);
    });

    test('SH-46/DC-59: the Selection\'s outline lies on the gridline and a pixel outside the range on all four sides, over the outer lines, and the lines inside stay drawn over its shade (ADR-0071, ADR-0008, case 11)', async ({ page }) => {
        // Under ExSheet.MudBlazor's Chrome, whose outline is the palette's primary: the built-in
        // one draws it in the Ink, as black as the lines, and could not show which is on top.
        await openCase(page, '11&chrome=mud');
        const grid = sheet(page);
        await pressCell(grid, 'B2');
        await page.keyboard.press('Shift+ArrowRight');
        await page.keyboard.press('Shift+ArrowDown');
        await expect(grid.locator('.ex-range-single')).toHaveCount(1);
        await page.mouse.move(0, 0);
        const scale = await page.evaluate(() => devicePixelRatio);

        // The edges inside B2:C3, between C2 and C3 and between B3 and C3, over the shade: still
        // the lines', dark, on their gridline.
        const c2 = await cell(grid, 'C2').boundingBox();
        const inside = await across(page, c2, 'bottom', 0.5);
        expect(isDark(inside.pixel(-1)), 'the line between C2 and C3').toBe(true);
        expect(isDark(inside.pixel(-2)), 'the shade above it').toBe(false);
        const b3 = await cell(grid, 'B3').boundingBox();
        const between = await across(page, b3, 'right', 0.5);
        expect(isDark(between.pixel(-1)), 'the line between B3 and C3').toBe(true);

        // Excel's outline (case 11: -1..0 on B2's top, -3..-2 on its left, -2..-1 on C3's bottom):
        // on each outer edge the gridline's pixel and the one outside it are the outline's, so the
        // outer line under it is covered, and the range's own first pixel inside it is not.
        const outline = await resolvedColour(page, await grid.locator('.ex-range-single').first()
            .evaluate((el) => getComputedStyle(el, '::after').borderTopColor));
        const b2 = await cell(grid, 'B2').boundingBox();
        const c3 = await cell(grid, 'C3').boundingBox();
        const isOutline = (pixel) => sameColour(pixel, outline, 8);
        for (const [name, box, side, on, off] of [
            ['top', b2, 'top', [-2, -1], 0],
            ['left', b2, 'left', [-2, -1], 0],
            ['bottom', c3, 'bottom', [-1, 0], -3],
            ['right', c3, 'right', [-1, 0], -3],
        ]) {
            const { pixel } = await across(page, box, side, 0.5);
            for (const offset of on) {
                expect(isOutline(pixel(offset)), `the outline on the ${name} at ${offset} (${pixel(offset)} against ${outline}, scale ${scale})`).toBe(true);
            }
            expect(isOutline(pixel(off)), `inside the range, past the outline's ${name} (scale ${scale})`).toBe(false);
        }
    });

    test('SH-46: a line on column A\'s left lies under the Row Headings\' edge (ADR-0071, the twelfth run\'s case 14)', async ({ page }) => {
        await openCase(page, '12-14');
        const grid = sheet(page);
        // Row 3 is outlined, A3's left included; row 5 has no line, for comparison.
        const a3 = await cell(grid, 'A3').boundingBox();
        const a5 = await cell(grid, 'A5').boundingBox();
        const lined = (await across(page, a3, 'left', 0.5)).pixel;
        const plain = (await across(page, a5, 'left', 0.5)).pixel;
        // The pixel before A's first is the Headings' edge: the same with the line as without it.
        expect(lined(-1), 'the Headings\' edge').toEqual(plain(-1));
        // Nothing of a thin line shows inside A3.
        expect(isDark(lined(0)), 'A3\'s first pixel').toBe(false);
        // The row's own top and bottom lines are drawn.
        const { pixel } = await across(page, a3, 'bottom', 0.5);
        expect(isDark(pixel(-1)), 'row 3\'s bottom line').toBe(true);
        expect(isLight(pixel(-2)), 'the Paper above it').toBe(true);
    });
});
