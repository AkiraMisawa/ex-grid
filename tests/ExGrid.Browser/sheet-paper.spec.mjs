import { test, expect } from './fixtures.mjs';
import { sheet, cell, pressCell, editor } from './sheet-helpers.mjs';
import { painted, sameColour, resolvedColour, contrast } from './pixels.mjs';

// ExSheet paints Font and Fill on white Paper (ticket 48; ADR-0071, "Paper and Ink"; SH-39, SH-40,
// DC-58). The Paper is Excel's white and the Ink Excel's black under the light and the dark scheme
// alike; what lies on the Paper keeps its light-scheme look, and what frames it follows the scheme.
//
// /sheet?case=paper opens a Sheet that records colours and emphases, a row's and a column's Fill
// among them (SheetCases), and ?scheme=dark declares the dark scheme for the whole document, as a
// host page in dark mode does; under ?chrome=mud MudBlazor paints its dark palette too. Grounds are
// read from the pixels, text colours from the cascade, which a colour read off antialiased glyphs
// would blur.

test.use({ viewport: { width: 1280, height: 1000 } });

const CHROMES = [['built-in', ''], ['MudBlazor', '&chrome=mud']];
const SCHEMES = ['light', 'dark'];

const WHITE = [255, 255, 255];
const BLACK = [0, 0, 0];

async function openCase(page, name, scheme, chrome) {
    await page.goto(`/sheet?case=${name}&scheme=${scheme}${chrome}`);
    await expect(sheet(page).locator('.ex-row').first()).toBeVisible();
    if (chrome) {
        await expect(page.locator('.mud-ex-formula-bar-text, .mud-ex-name-box').first()).toBeAttached();
    }
    // The page pushes its Linked Table 1.5 s after the Sheet opens; every row it repaints is
    // painted again, so the reads wait for it.
    await expect(page.locator('#sheet-positions .ex-row').first()).toBeVisible();
    await page.mouse.move(0, 0);
}

/** The painted ground in the middle of a cell, clear of its text and its gridlines. */
async function groundOf(page, locator) {
    const box = await locator.boundingBox();
    const region = await painted(page, { x: box.x, y: box.y, width: box.width, height: box.height });
    return region.at(box.x + box.width - 4, box.y + 4);
}

/** A colour the cascade resolved for an element, in sRGB bytes. */
async function colourOf(locator, property) {
    const css = await locator.evaluate((el, p) => getComputedStyle(el)[p], property);
    return resolvedColour(locator.page(), css);
}

const luminance = (rgb) => contrast(rgb, BLACK) / 21;

for (const [chrome, query] of CHROMES) {
    for (const scheme of SCHEMES) {
        test(`SH-39/SH-40/DC-58 (${chrome} Chrome, ${scheme} scheme): the Paper is white, the Ink black, and recorded colours read as recorded (ADR-0071, ticket 48)`, async ({ page }) => {
            await openCase(page, 'paper', scheme, query);
            const grid = sheet(page);
            await expect(cell(grid, 'A1')).toHaveText('red');

            // The Paper, under a cell that holds nothing and one that holds text.
            expect(sameColour(await groundOf(page, cell(grid, 'E8')), WHITE, 2), 'the Paper is white').toBe(true);
            expect(sameColour(await groundOf(page, cell(grid, 'C2')), WHITE, 2), 'under text too').toBe(true);
            // The Ink: Automatic text is black.
            expect(await colourOf(cell(grid, 'C2'), 'color')).toEqual(BLACK);

            // Recorded colours, as recorded: a Font colour, and a Number Format's red over a blue Font.
            expect(await colourOf(cell(grid, 'A1'), 'color')).toEqual([255, 0, 0]);
            expect(await colourOf(cell(grid, 'B1'), 'color')).toEqual([255, 0, 0]);
            // A cell's Fill, a row's on a cell that holds nothing, and a column's.
            expect(sameColour(await groundOf(page, cell(grid, 'C3')), [255, 255, 0], 2), 'C3 is yellow').toBe(true);
            expect(sameColour(await groundOf(page, cell(grid, 'D5')), [0, 0xb0, 0xf0], 2), 'row 5 is light blue').toBe(true);
            expect(sameColour(await groundOf(page, cell(grid, 'F9')), [255, 255, 0], 2), 'column F is yellow').toBe(true);
            // Emphases.
            expect(await cell(grid, 'C1').evaluate((el) => getComputedStyle(el).fontWeight)).toBe('700');
            expect(await cell(grid, 'D1').evaluate((el) => getComputedStyle(el).fontStyle)).toBe('italic');
            expect(await cell(grid, 'A2').evaluate((el) => getComputedStyle(el).textDecorationLine)).toBe('underline');
            expect(await cell(grid, 'B2').evaluate((el) => getComputedStyle(el).textDecorationLine)).toBe('line-through');

            // What frames the Paper follows the scheme: the column Headings, the Row Headings and the
            // Formula Bar are dark in the dark scheme and light in the light one, and read in either.
            for (const [name, frame] of [
                ['the column Headings', grid.locator('.ex-header')],
                ['the Row Headings', grid.locator('.ex-row-heading').first()],
                ['the Formula Bar', grid.locator('.ex-formula-bar')],
            ]) {
                const ground = await colourOf(frame, 'backgroundColor');
                const text = await colourOf(frame, 'color');
                if (scheme === 'dark') {
                    expect(luminance(ground), `${name} are dark (${ground})`).toBeLessThan(0.3);
                } else {
                    expect(luminance(ground), `${name} are light (${ground})`).toBeGreaterThan(0.7);
                }
                expect(contrast(ground, text), `${name} read (${text} on ${ground})`).toBeGreaterThanOrEqual(4.5);
            }
        });
    }
}

/** The Focus outline and the single range's outline, and the tinted ground, of a selection B2:C3. */
async function selectionLook(page) {
    const grid = sheet(page);
    await pressCell(grid, 'B2');
    await page.keyboard.press('Shift+ArrowRight');
    await page.keyboard.press('Shift+ArrowDown');
    await expect(grid.locator('.ex-range-single')).toHaveCount(1);
    await page.mouse.move(0, 0);
    // The outline is the border of the range's box of its own, the ::after (ADR-0008, 2026-10-01).
    const outline = await resolvedColour(page, await grid.locator('.ex-range-single').first()
        .evaluate((el) => getComputedStyle(el, '::after').borderTopColor));
    const tinted = await groundOf(page, cell(grid, 'C3'));
    return { outline, tinted };
}

for (const [chrome, query] of CHROMES) {
    test(`SH-39 (${chrome} Chrome): on the Paper the Selection and the Cell Editor keep their light-scheme look in the dark scheme (ADR-0071, ticket 48)`, async ({ page }) => {
        const looks = {};
        const editors = {};
        for (const scheme of SCHEMES) {
            await openCase(page, 'paper', scheme, query);
            const grid = sheet(page);
            looks[scheme] = await selectionLook(page);

            // The Cell Editor in its cell is the Paper and the Ink. Its ground is read from the pixels:
            // while the coloured text shows, the field is see-through over it (ADR-0057).
            await pressCell(grid, 'E7');
            await page.keyboard.type('typed');
            const box = grid.locator('.ex-viewport .ex-editor').first();
            await expect(editor(grid)).toHaveValue('typed');
            editors[scheme] = { ground: await groundOf(page, box), ink: await colourOf(box, 'color') };
            await page.keyboard.press('Escape');
        }

        for (const scheme of SCHEMES) {
            expect(sameColour(editors[scheme].ground, WHITE, 2), `the Cell Editor's ground (${scheme}: ${editors[scheme].ground})`).toBe(true);
            expect(editors[scheme].ink, `the Cell Editor's ink (${scheme})`).toEqual(BLACK);
            // The Selection's outline reads against the Paper, and its tint leaves the Paper light.
            expect(contrast(looks[scheme].outline, WHITE), `the outline against the Paper (${scheme})`).toBeGreaterThanOrEqual(3);
            expect(luminance(looks[scheme].tinted), `the tinted Paper (${scheme})`).toBeGreaterThan(0.6);
        }
        if (!query) {
            // The core's own look takes nothing from the scheme on the Paper: the same pixels in both.
            expect(looks.dark).toEqual(looks.light);
        }
    });

    test(`SH-39 (${chrome} Chrome): Reference Outlines and the pointed shade keep their light-scheme colours on the Paper, and the Formula Bar's References follow the scheme (ADR-0071, ADR-0057)`, async ({ page }) => {
        const read = {};
        for (const scheme of SCHEMES) {
            await openCase(page, 'paper', scheme, query);
            const grid = sheet(page);
            await pressCell(grid, 'E7');
            await page.keyboard.type('=C3+');
            await expect(grid.locator('.ex-reference-outline.ex-reference-1').first()).toBeVisible();
            // Point (ADR-0051): the Reference written by an arrow is shown selected, on the shade.
            await page.keyboard.press('ArrowDown');
            await expect(editor(grid)).toHaveValue('=C3+E8');
            // The coloured text is the core's under either Chrome: in the cell, and in the bar.
            const pointed = grid.locator('.ex-viewport .ex-reference-text .ex-reference-pointed').first();
            await expect(pointed).toBeAttached();
            read[scheme] = {
                outline: await colourOf(grid.locator('.ex-reference-outline.ex-reference-1').first(), 'color'),
                shade: await colourOf(pointed, 'backgroundColor'),
                bar: await colourOf(grid.locator('.ex-formula-bar .ex-reference-text .ex-reference-1').first(), 'color'),
            };
            await page.keyboard.press('Escape');
        }

        // Excel's light-scheme colours on the Paper, in both schemes (ADR-0057, ADR-0071).
        for (const scheme of SCHEMES) {
            expect(read[scheme].outline, `the first Reference's outline (${scheme})`).toEqual([0x32, 0x6a, 0xc7]);
            expect(read[scheme].shade, `the pointed shade (${scheme})`).toEqual([0xc6, 0xc6, 0xc6]);
        }
        // The Formula Bar frames the Paper: its References take the dark scheme's shade there.
        expect(read.light.bar).toEqual([0x32, 0x6a, 0xc7]);
        expect(read.dark.bar, 'the Formula Bar follows the scheme').not.toEqual(read.light.bar);
    });
}

test('SH-39: setting --ex-sheet-paper and --ex-sheet-ink changes the Paper and the Ink (ADR-0071, ADR-0027)', async ({ page }) => {
    await openCase(page, 'paper', 'light', '');
    const grid = sheet(page);
    // On the Sheet's own element, which leaves with the page.
    await page.locator('.ex-sheet').first().evaluate((el) => {
        el.style.setProperty('--ex-sheet-paper', '#fdf6e3');
        el.style.setProperty('--ex-sheet-ink', '#073642');
    });

    await expect.poll(async () => colourOf(cell(grid, 'C2'), 'color')).toEqual([0x07, 0x36, 0x42]);
    expect(sameColour(await groundOf(page, cell(grid, 'E8')), [0xfd, 0xf6, 0xe3], 2), 'the Paper is the token').toBe(true);
    // A recorded colour stays as recorded.
    expect(await colourOf(cell(grid, 'A1'), 'color')).toEqual([255, 0, 0]);
});

const GRIDLINE = [0xe0, 0xe0, 0xe0];
const YELLOW = [255, 255, 0];

for (const [chrome, query] of CHROMES) {
    test(`SH-39 (${chrome} Chrome): a pinned cell that draws a line layer keeps its row gridline beneath it (ADR-0071, ADR-0050 item 15, ticket 90)`, async ({ page }) => {
        // /sheet pins column A. In case 4, B2 is yellow, and its Fill covers its left gridline, which
        // A2 holds: A2 paints that cover as a layer (.ex-lined), as it would a line of its own.
        await openCase(page, '4', 'light', query);
        const grid = sheet(page);
        const a2 = cell(grid, 'A2');
        await expect(a2).toHaveClass(/\bex-pinned\b/);
        await expect(a2).toHaveClass(/\bex-lined\b/);
        await expect(cell(grid, 'A1')).not.toHaveClass(/\bex-lined\b/);

        const box = await a2.boundingBox();
        const above = await cell(grid, 'A1').boundingBox();
        const region = await painted(page, { x: box.x, y: above.y, width: box.width, height: box.y + box.height - above.y });
        const middle = box.x + box.width / 2;
        const lastRow = (b) => b.y + b.height - 0.5;
        // A1 draws no layer: its own gridline, as ticket 48 painted it.
        expect(sameColour(region.at(middle, lastRow(above)), GRIDLINE, 2), `A1's row gridline (${region.at(middle, lastRow(above))})`).toBe(true);
        // A2 keeps its row gridline beneath the layer, where it held only the Paper before.
        expect(sameColour(region.at(middle, lastRow(box)), GRIDLINE, 2), `A2's row gridline (${region.at(middle, lastRow(box))})`).toBe(true);
        // The cover is still over A2's right gridline, and the Paper is still the ground above the line.
        expect(sameColour(region.at(box.x + box.width - 0.5, box.y + box.height / 2), YELLOW, 2), 'B2\'s Fill over the gridline A2 holds').toBe(true);
        expect(sameColour(region.at(middle, box.y + box.height / 2 - 2), WHITE, 2), 'the Paper inside A2').toBe(true);
    });
}

// The Cell Editor keeps the edited cell's Fill and Font (ticket 88; ADR-0050 item 15, ADR-0071): its
// field and the coloured text beneath it (ADR-0057) are read in the cell's Font, on its Fill, in the
// Font's own colour — the editor shows the Entry, not the formatted Value — and the box stays the
// cell's. Under the MudBlazor Chrome the field is the Wrapper's control in the core's box.

/** The look a surface is drawn in: its colour, weight, slant and decoration, as the cascade resolved them. */
async function lookOf(locator) {
    const look = await locator.evaluate((el) => {
        const style = getComputedStyle(el);
        return { color: style.color, weight: style.fontWeight, style: style.fontStyle, decoration: style.textDecorationLine };
    });
    return { ...look, color: await resolvedColour(locator.page(), look.color) };
}

/** The open Cell Editor's box: the core's input, or the box a Chrome's control stands in. */
const editorBox = (grid) => grid.locator('.ex-viewport .ex-editor').first();

/** The coloured text beneath the Cell Editor's field. */
const editorLayer = (grid) => grid.locator('.ex-viewport .ex-reference-text').first();

for (const [chrome, query] of CHROMES) {
    for (const scheme of SCHEMES) {
        test(`DC-58/SH-39 (${chrome} Chrome, ${scheme} scheme): the Cell Editor keeps the edited cell's Font, in the Font's own colour, and its box (ADR-0050 item 15, ADR-0071, ticket 88)`, async ({ page }) => {
            await openCase(page, 'paper', scheme, query);
            const grid = sheet(page);
            const normal = { weight: '400', style: 'normal', decoration: 'none' };
            const cases = [
                ['A1', { ...normal, color: [255, 0, 0] }, 'a Font colour, in the Pinned Column'],
                ['B1', { ...normal, color: [0, 0, 255] }, '-5 in 0;[Red]-0 over a blue Font: the Entry, in the Font\'s blue'],
                ['C1', { ...normal, color: BLACK, weight: '700' }, 'bold'],
                ['D1', { ...normal, color: BLACK, style: 'italic' }, 'italic'],
                ['A2', { ...normal, color: BLACK, decoration: 'underline' }, 'underline'],
                ['B2', { ...normal, color: BLACK, decoration: 'line-through' }, 'strikethrough'],
            ];
            for (const [address, look, what] of cases) {
                await pressCell(grid, address);
                const rowOf = () => cell(grid, address).locator('xpath=..').boundingBox();
                const row = await rowOf();
                const cellBox = await cell(grid, address).boundingBox();
                await page.keyboard.press('F2');
                await expect(editor(grid)).toBeFocused();
                await page.mouse.move(0, 0);

                for (const [name, surface] of [['field', editor(grid)], ['coloured text', editorLayer(grid)]]) {
                    expect(await lookOf(surface), `${address} (${what}): the ${name}`).toEqual(look);
                }
                // On the Paper, as the cell is.
                expect(sameColour(await groundOf(page, editorBox(grid)), WHITE, 2), `${address}: the editor's ground`).toBe(true);
                // No geometry moves: the box is the cell's, and the row keeps its height.
                const box = await editorBox(grid).boundingBox();
                for (const side of ['x', 'y', 'width', 'height']) {
                    expect(Math.abs(box[side] - cellBox[side]), `${address}: the editor's ${side}`).toBeLessThanOrEqual(0.5);
                }
                expect((await rowOf()).height, `${address}: the row's height`).toBe(row.height);
                await page.keyboard.press('Escape');
                await expect(grid.locator('.ex-viewport .ex-editor')).toHaveCount(0);
            }
        });
    }

    test(`DC-58/SH-39 (${chrome} Chrome): the Cell Editor keeps a Fill as its ground, and ADR-0057's coloured References read over it (ADR-0050 item 15, ADR-0071, ticket 88)`, async ({ page }) => {
        await openCase(page, 'paper', 'light', query);
        const grid = sheet(page);
        // C3 records a yellow Fill and an Automatic Font.
        await pressCell(grid, 'C3');
        await page.keyboard.type('x');
        await expect(editor(grid)).toHaveValue('x');
        await page.mouse.move(0, 0);
        expect(sameColour(await groundOf(page, editorBox(grid)), [255, 255, 0], 2), 'the Fill is the editor\'s ground').toBe(true);
        expect((await lookOf(editor(grid))).color, 'the Ink').toEqual(BLACK);

        // A Formula: the field turns see-through over the coloured text, which is read on the Fill.
        await page.keyboard.press('Escape');
        await page.keyboard.type('=A1+B1');
        await expect(editor(grid)).toHaveValue('=A1+B1');
        await expect(editor(grid)).toHaveClass(/\bex-reference-text-shown\b/);
        await page.mouse.move(0, 0);
        const layer = editorLayer(grid);
        expect((await lookOf(layer.locator('.ex-reference-1'))).color).toEqual([0x32, 0x6a, 0xc7]);
        expect((await lookOf(layer.locator('.ex-reference-2'))).color).toEqual([0xc0, 0x35, 0x3e]);
        const box = await editorBox(grid).boundingBox();
        expect(sameColour(await groundOf(page, editorBox(grid)), [255, 255, 0], 2), 'still on the Fill').toBe(true);
        // As painted: each Reference's colour is on the screen inside the editor, over the yellow.
        const region = await painted(page, box);
        const pixels = [];
        for (let y = box.y + 1; y < box.y + box.height - 1; y += 1 / region.scale) {
            pixels.push(...region.across(y, box.x, box.x + box.width - 1));
        }
        for (const [name, colour] of [['the first Reference', [0x32, 0x6a, 0xc7]], ['the second Reference', [0xc0, 0x35, 0x3e]]]) {
            expect(pixels.filter((pixel) => sameColour(pixel, colour, 40)).length, `${name}, painted over the Fill`).toBeGreaterThanOrEqual(3);
        }
        await page.keyboard.press('Escape');
    });
}
