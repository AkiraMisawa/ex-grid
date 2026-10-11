import { test, expect, alterPage } from './fixtures.mjs';
import { sheet, cell, pressCell, editor, bar, stretchesOf } from './sheet-helpers.mjs';
import { painted, sameColour, resolvedColour, contrast, onDeviceGrid, pixelsApart, stillPictures } from './pixels.mjs';

// ExSheet paints Font and Fill on white Paper (ticket 48; ADR-0071, "Paper and Ink"; SH-39, SH-40,
// DC-58). The Paper is Excel's white and the Ink Excel's black under the light and the dark scheme
// alike; what lies on the Paper keeps its light-scheme look, and what frames it follows the scheme.
//
// /sheet?case=paper opens a Sheet that records colours and emphases, a row's and a column's Fill
// among them (SheetCases), and ?scheme=dark declares the dark scheme for the whole document, as a
// host page in dark mode does; under ?chrome=mud MudBlazor paints its dark palette too. Grounds are
// read from the pixels, text colours from the cascade, which a colour read off antialiased glyphs
// would blur.

// 1280×1000, but in chrome-150 the window Chrome draws at the display scale its command line sets, as
// an OS sets it: a viewport of Playwright's own is emulated at a scale of 1 (deviceScaleFactor || 1),
// which would take the 150% away. Only the bold edge's sweep runs there (VZ-19).
test.use({ viewport: async ({}, use, testInfo) => use(testInfo.project.name === 'chrome-150' ? null : { width: 1280, height: 1000 }) });

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
            // The Selection's outline reads against the Paper. Under the built-in Chrome its shade
            // multiplies (ADR-0071, 2026-10-02; SH-54): C3's yellow Fill × 0.78, as white Paper gives
            // Excel's #C7C7C7. Under MudBlazor's, the primary mixed in, which leaves it light.
            expect(contrast(looks[scheme].outline, WHITE), `the outline against the Paper (${scheme})`).toBeGreaterThanOrEqual(3);
            if (query) {
                expect(luminance(looks[scheme].tinted), `the tinted Paper (${scheme})`).toBeGreaterThan(0.6);
            } else {
                expect(sameColour(looks[scheme].tinted, [0xc7, 0xc7, 0x00], 3), `the shade over C3's yellow Fill (${scheme}): ${looks[scheme].tinted}`).toBe(true);
            }
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
            // The coloured text is the core's under either Chrome: in the cell, and in the bar. Its
            // References are highlights over the layer's one run (ADR-0057, note of 2026-10-01), so
            // the colours are those the grid's stylesheet paints them in. The bar's layer is hidden
            // while the edit is in the cell, and its colours are read off the same highlights.
            await expect.poll(async () => (await stretchesOf(editor(grid))).filter((stretch) => stretch.pointed).map((stretch) => stretch.text)).toEqual(['E8']);
            const stretches = await stretchesOf(editor(grid));
            const barColour = await bar(grid).evaluate((input, first) =>
                getComputedStyle(input.previousElementSibling.firstElementChild, `::highlight(${first})`).color, `${stretches[0].prefix}1`);
            read[scheme] = {
                outline: await colourOf(grid.locator('.ex-reference-outline.ex-reference-1').first(), 'color'),
                shade: await resolvedColour(page, stretches.find((stretch) => stretch.pointed).ground),
                bar: await resolvedColour(page, barColour),
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
        // Case 4 with column A pinned (?pin=1: a case's page pins nothing, as the run's workbook did
        // not). B2 is yellow, and its Fill covers its left gridline, which A2 holds: A2 paints that
        // cover as a layer (.ex-lined), as it would a line of its own.
        await openCase(page, '4&pin=1', 'light', query);
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
        // Each Reference is a highlight over the layer's one run (ADR-0057, note of 2026-10-01).
        await expect.poll(async () => (await stretchesOf(editor(grid))).map((stretch) => stretch.text)).toEqual(['A1', 'B1']);
        const [first, second] = await stretchesOf(editor(grid));
        expect(await resolvedColour(page, first.ink)).toEqual([0x32, 0x6a, 0xc7]);
        expect(await resolvedColour(page, second.ink)).toEqual([0xc0, 0x35, 0x3e]);
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

// What fits, bold and italic (SH-41; ADR-0071, "Bold, and what fits"; ADR-0016). A bold cell is
// judged by the bold widths: whether it is ####, and the text General fits to its column. An italic
// one is judged by the regular widths, its slant leaning into the cell's padding. /sheet?case=bold and
// ?case=fit put columns a hundredth of a pixel from where a number stops fitting, in the Cell Metrics
// the Sheet paints with under either Chrome (SheetCases). What the grid decided is read from the DOM.
// That what it shows is painted whole is read in pixels: the cell as painted, and the same cell with
// its clip and its ellipsis lifted, are one picture over the cell and the pixels either side of it. A
// cut glyph or an ellipsis is their difference. The columns' edges are on Device Pixels (ADR-0090), so
// the pictures' edges are too.

/** Opens a case with the Focus off the cells read and the pointer off the Sheet, in the face the widths are for. */
async function openFit(page, name, query) {
    await openCase(page, name, 'light', query);
    const grid = sheet(page);
    // The Focus outline would lie over A1; A10 holds nothing.
    await pressCell(grid, 'A10');
    // Roboto has arrived under the Wrapper's Chrome.
    await page.evaluate(() => document.fonts.ready);
    await page.mouse.move(0, 0);
    // The second way each cell is pictured, set by stillPictures' mark: its clip and ellipsis lifted.
    await alterPage(page, () => {
        const style = document.createElement('style');
        style.textContent = '.ex-cell[data-fit-picture="unclipped"] { overflow: visible !important; text-overflow: clip !important; }';
        document.head.append(style);
        return () => style.remove();
    });
    return grid;
}

/** How a cell's text lies in it, as laid out: its painted width, its content box and the text's own width. */
function layoutOf(locator) {
    return locator.evaluate((el) => {
        const style = getComputedStyle(el);
        const box = el.getBoundingClientRect();
        const range = document.createRange();
        range.selectNodeContents(el);
        return {
            painted: box.width,
            content: box.width - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight),
            text: range.getBoundingClientRect().width,
        };
    });
}

/** A cell's font, as the cascade resolved it. */
const fontOf = (locator) => locator.evaluate((el) => {
    const style = getComputedStyle(el);
    return { weight: style.fontWeight, style: style.fontStyle, align: style.textAlign };
});

/**
 * Asserts, softly so that every cell is reported, that a cell's text is painted whole. Where it is
 * not, both pictures go with the test's report.
 */
async function expectPaintedWhole(page, locator, what) {
    const box = await locator.boundingBox();
    const scale = await page.evaluate(() => devicePixelRatio);
    const region = onDeviceGrid(box, scale, 12);
    const pictures = await stillPictures(locator, {
        mark: 'data-fit-picture',
        ways: ['painted', 'unclipped'],
        shoot: () => page.screenshot({ clip: region, scale: 'device', animations: 'disabled', caret: 'hide' }),
    });
    const apart = pixelsApart(pictures.painted, pictures.unclipped, 0);
    if (apart.differing > 0) {
        for (const way of ['painted', 'unclipped']) {
            await test.info().attach(`${what}, ${way}`, { body: pictures[way], contentType: 'image/png' });
        }
    }
    expect.soft(apart.differing, `${what}: as painted and unclipped, the same picture (${JSON.stringify(await layoutOf(locator))})`).toBe(0);
}

/**
 * Whether the browser paints system-ui in DejaVu Sans, as Linux does: measured in the page, as the
 * same text set in system-ui and in DejaVu Sans, bold. A face that is not installed falls back to
 * another, which measures differently. The bold edge's sweep records it: its hardest case is that face.
 */
const paintsDejaVu = (page) => page.evaluate(() => {
    const context = new OffscreenCanvas(1, 1).getContext('2d');
    const width = (family) => {
        context.font = `700 14px ${family}`;
        return context.measureText('0123456789%.,+-E').width;
    };
    return width('system-ui') === width('"DejaVu Sans"');
});

for (const [chrome, query] of CHROMES) {
    test(`SH-41 (${chrome} Chrome): a bold number is #### exactly when it does not fit at the bold widths (ADR-0071, ADR-0016)`, async ({ page }) => {
        const grid = await openFit(page, 'bold', query);
        // A hundredth under the bold edge: plain, 12% fits, and is painted whole; bold, it is ####,
        // named by its value.
        await expect(cell(grid, 'A1')).toHaveText('12%');
        await expect(cell(grid, 'A2')).toHaveText(/^#+$/);
        await expect(cell(grid, 'A2')).toHaveAttribute('aria-label', '12%');
        expect((await fontOf(cell(grid, 'A1'))).weight).toBe('400');
        expect((await fontOf(cell(grid, 'A2'))).weight).toBe('700');
        await expectPaintedWhole(page, cell(grid, 'A1'), 'A1, plain, a hundredth under the bold edge');
        // A hundredth over it, bold: shown.
        for (const column of 'BCDEFGHI') {
            await expect(cell(grid, `${column}2`)).toHaveText('12%');
            expect((await fontOf(cell(grid, `${column}2`))).weight).toBe('700');
        }
    });

    test(`SH-41/VZ-19 (${chrome} Chrome): a bold number shown a hundredth over the bold edge is painted whole, wherever its column's edges fall on Device Pixels (ADR-0071, ADR-0016, ADR-0090)`, async ({ page }, testInfo) => {
        const grid = await openFit(page, 'bold', query);
        // The #### decision reads the declared width (ADR-0090), and a column is painted up to a Device
        // Pixel narrower than declared. In DejaVu Sans, which Linux paints system-ui in, the core's bold
        // widths leave 12% 0.35px, under that pixel, and the column painted narrower cut it with an
        // ellipsis (found here, 2026-10-10; ticket 18). So a cell that shows a number may use one
        // Device Pixel of its right padding (ADR-0090, 2026-10-11; VZ-19). Read here at 100% and, in
        // chrome-150, at the 150% its command line sets.
        expect(await page.evaluate(() => devicePixelRatio)).toBe(testInfo.project.name === 'chrome-150' ? 1.5 : 1);
        testInfo.annotations.push({ type: 'system-ui', description: await paintsDejaVu(page) ? 'DejaVu Sans' : 'not DejaVu Sans' });
        // Eight columns of one declared width, B to I, at eight offsets. Each is painted at one of the
        // two widths a Device Pixel apart either side of the declared one, as its offset falls (ADR-0090).
        const widths = [];
        for (const column of 'BCDEFGHI') {
            const target = cell(grid, `${column}2`);
            await expect(target).toHaveText('12%');
            widths.push((await layoutOf(target)).painted);
            await expectPaintedWhole(page, target, `${column}2, bold, a hundredth over the bold edge`);
        }
        expect(Math.max(...widths) - Math.min(...widths), `the eight columns' painted widths: ${widths.join(', ')}`).toBeLessThanOrEqual(1);
    });

    test(`SH-41 (${chrome} Chrome): General fits a bold number to the bold widths, and both are painted whole (ADR-0071, ADR-0047)`, async ({ page }) => {
        const grid = await openFit(page, 'fit', query);
        // Between the widths the whole of its General text, 1.23457E+20, takes plain and bold, each cell
        // shows the widest General text its own widths fit, a digit fewer where they do not fit the
        // whole. Under the built-in Chrome the bold widths are the wider (the + is), so the bold cell has
        // the digit fewer; under the Wrapper's, Roboto's bold separator is the narrower, so the plain one does.
        const [plain, bold] = query ? ['1.2346E+20', '1.23457E+20'] : ['1.23457E+20', '1.2346E+20'];
        await expect(cell(grid, 'B1')).toHaveText(plain);
        await expect(cell(grid, 'B2')).toHaveText(bold);
        expect((await fontOf(cell(grid, 'B2'))).weight).toBe('700');
        await expectPaintedWhole(page, cell(grid, 'B1'), 'B1, General, plain');
        await expectPaintedWhole(page, cell(grid, 'B2'), 'B2, General, bold');
    });

    test(`SH-41 (${chrome} Chrome): an italic number that fits is not cut at either edge (ADR-0071, ADR-0016)`, async ({ page }) => {
        const grid = await openFit(page, 'fit', query);
        // A hundredth over the width the regular widths give it, against the cell's right edge and its left.
        for (const [address, align] of [['A1', 'right'], ['A2', 'left']]) {
            const target = cell(grid, address);
            await expect(target).toHaveText('12%');
            expect(await fontOf(target)).toEqual({ weight: '400', style: 'italic', align });
            await expectPaintedWhole(page, target, `${address}, italic, aligned ${align}`);
        }
    });
}
