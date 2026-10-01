import { test, expect, scrollRowToTop } from './fixtures.mjs';
import { sheet, cell, pressCell } from './sheet-helpers.mjs';
import { sameColour, resolvedColour } from './pixels.mjs';
import { STYLES, across, along, pattern, expected, isDark, isLight } from './line-pixels.mjs';

// ExSheet's Borders as Excel draws them (ticket 49; ADR-0071; ADR-0050 item 15; DC-59, SH-46), read
// in device pixels on /sheet?case=…, the eleventh and twelfth Windows runs' set-ups (SheetCases):
// Excel's thirteen line styles on the Paper with its gridlines, a Fill over the gridlines, a line
// over a Fill, the Selection over lines, the line two cells both record, a line on column A's left,
// and rows that keep their one height.
//
// The line tests run at 100% here and at 150% in the chrome-150 project, whose display scale is set
// on Chrome's command line as an OS sets it; Excel's pixels at both zooms are case 9's table.

const WHITE = [255, 255, 255];
const GRIDLINE = [0xe0, 0xe0, 0xe0];
const YELLOW = [255, 255, 0];
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

test.describe('DC-59: lines on the Sheet', () => {
    for (const [index, style] of STYLES.entries()) {
        for (const side of ['bottom', 'right']) {
            test(`DC-59/SH-46: a ${style} line on a Sheet cell's ${side} edge is drawn as Excel draws it, centred on the gridline, at the run's scale (ADR-0071, case 9)`, async ({ page }, testInfo) => {
                await openCase(page, 'lines');
                expect(await page.evaluate(() => devicePixelRatio)).toBe(testInfo.project.name === 'chrome-150' ? 1.5 : 1);
                // The styles stand on rows 2 to 14: on B's bottom, and on D's right.
                const target = await reveal(page, `${side === 'bottom' ? 'B' : 'D'}${index + 2}`);
                const box = await target.boundingBox();
                const { scale, pixel } = await across(page, box, side, 0.5);
                const want = expected(style, scale);
                for (const offset of want.light) {
                    const pixels = await along(page, box, side, offset);
                    expect(pixels.filter(isDark).length, `${style} leaves ${offset} clear`).toBe(0);
                }
                if (want.dark) {
                    for (const offset of want.dark) expect(isDark(pixel(offset)), `${style} at ${offset} (scale ${scale})`).toBe(true);
                    // Excel's double: two lines with the gridline's pixel white between them, the
                    // Paper rather than the gridline (case 9).
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
            });
        }
    }

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

    test('SH-46/DC-59: inside the Selection the lines stay drawn over its shade, and its outline covers the outer ones on its bottom and right (ADR-0071, case 11)', async ({ page }) => {
        // Under ExSheet.MudBlazor's Chrome, whose outline is the palette's primary: the built-in
        // one draws it in the Ink, as black as the lines, and could not show which is on top.
        await openCase(page, '11&chrome=mud');
        const grid = sheet(page);
        await pressCell(grid, 'B2');
        await page.keyboard.press('Shift+ArrowRight');
        await page.keyboard.press('Shift+ArrowDown');
        await expect(grid.locator('.ex-range-single')).toHaveCount(1);
        await page.mouse.move(0, 0);

        // The edges inside B2:C3, between B2 and C2 and between C2 and C3, over the shade: still
        // the lines', dark, on their gridline.
        const c2 = await cell(grid, 'C2').boundingBox();
        const inside = await across(page, c2, 'bottom', 0.5);
        expect(isDark(inside.pixel(-1)), 'the line between C2 and C3').toBe(true);
        expect(isDark(inside.pixel(-2)), 'the shade above it').toBe(false);
        const b3 = await cell(grid, 'B3').boundingBox();
        const between = await across(page, b3, 'right', 0.5);
        expect(isDark(between.pixel(-1)), 'the line between B3 and C3').toBe(true);

        // The outer lines on the bottom and the right lie on C3's own last pixels, under the
        // outline: they are the outline's colour, not the line's black.
        const outline = await resolvedColour(page, await grid.locator('.ex-range-single').first().evaluate((el) => getComputedStyle(el).outlineColor));
        const c3 = await cell(grid, 'C3').boundingBox();
        for (const side of ['bottom', 'right']) {
            const { pixel } = await across(page, c3, side, 0.5);
            expect(sameColour(pixel(-1), outline, 8), `the outline over C3's ${side} line (${pixel(-1)} against ${outline})`).toBe(true);
        }
    });

    // Excel's outline lies on the gridline and the pixel outside it (case 11: -1..0 on B2's top), so
    // it covers the outer lines on all four sides. ADR-0008 draws the Selection's outline inside the
    // range (UX-18), so the line on its top and left edges, which the cells above and to the left
    // hold, stays drawn beside it. Moving the outline is ADR-0008's decision, not this ticket's.
    test.fixme('SH-46: the Selection\'s outline covers the outer lines on its top and left as well (case 11; needs a decision on ADR-0008\'s outline)', async () => {});

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
