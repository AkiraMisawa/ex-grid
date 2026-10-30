import { test, expect, alterPage, circuitQuiet, twoFrames, settled, chromeHost } from './fixtures.mjs';
import { sheet, cell, pressCell, clickCell, expectFocusAt, editor, bar, stretchesOf } from './sheet-helpers.mjs';
import { painted, sameColour, resolvedColour, contrast, blend, onDeviceGrid, wholePixelCentres, pixelsApart, stillPictures, edgePixels, onDevicePixels, acrossEdges, paints } from './pixels.mjs';

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
// Excel's outline green (the eleventh Windows run, case 11; the readings of 2026-10-12, R4).
const GREEN = [0x21, 0x73, 0x46];

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
        test(`SH-39/SH-40/DC-58 (${chrome} Chrome, ${scheme} scheme): the Paper is white, the Ink black, and recorded colours read as recorded (ADR-0071, ticket 48) ${chromeHost(chrome)}`, async ({ page }) => {
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
    await expect(grid.locator('.ex-hover-row')).toHaveCount(0);
    // B2:C2 is a single range too: read once Shift+ArrowDown has been answered, or C3 can still be
    // untinted on the Server host (SH-39 on main, 2026-10-21, msedge: C3 read its own yellow).
    await settled(grid);
    // The outline is the border of the range's box of its own, the ::after (ADR-0008, 2026-10-01).
    const outline = await resolvedColour(page, await grid.locator('.ex-range-single').first()
        .evaluate((el) => getComputedStyle(el, '::after').borderTopColor));
    const tinted = await groundOf(page, cell(grid, 'C3'));
    return { outline, tinted };
}

for (const [chrome, query] of CHROMES) {
    test(`SH-39 (${chrome} Chrome): on the Paper the Selection and the Cell Editor keep their light-scheme look in the dark scheme (ADR-0071, ticket 48) ${chromeHost(chrome)}`, async ({ page }) => {
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

    test(`SH-39 (${chrome} Chrome): Reference Outlines and the pointed shade keep their light-scheme colours on the Paper, and the Formula Bar's References follow the scheme (ADR-0071, ADR-0057) ${chromeHost(chrome)}`, async ({ page }) => {
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

// The Focus among several ranges on the Paper (ADR-0008, 2026-10-12): the core's thin border of one
// Device Pixel inside the cell, in the Focus outline's colour (2026-10-12, decided with the user:
// #217346 on the Paper, as the readings of 2026-10-12 measured Excel's, R4), in either scheme. Under
// ExSheet.MudBlazor it is the palette's primary, which the Focus outline is there: this branch's
// reading, as SH-54's outline is. Read where something
// above the selection layer stands beside the cell — the Headings, a Pinned Column, the header — and
// in the open: lying inside the cell, no side is covered. Beside the Headings and under the header the
// Heading Mark lies past the gridline, in the same colour (ADR-0008, 2026-10-13): two Device Pixels on
// the Heading's edge, with the cell's own first Device Pixel between them and the border, as R4 read
// Excel's at A1 (case (2) below). The page's own scale here, and a real 150%
// under the chrome-150 project, as an OS sets it (Playwright's emulated scale is not read: its
// pixels are the emulation's, device-pixel-225.spec.mjs's note).
for (const [chrome, query] of CHROMES) {
    test(`UX-19/SH-39 (${chrome} Chrome): on the Paper the Focus among several ranges is one Device Pixel inside its cell in the Focus outline's colour, beside the Headings, a Pinned Column and the header and in the open, in either scheme (ADR-0008, 2026-10-12; ADR-0071) ${chromeHost(chrome)}`, async ({ page }, testInfo) => {
        for (const scheme of SCHEMES) {
            await page.goto(`/sheet?case=paper&pin=1&scheme=${scheme}${query}`);
            const grid = sheet(page);
            await expect(cell(grid, 'A1')).toHaveText('red');
            await expect(page.locator('#sheet-positions .ex-row').first()).toBeVisible();
            // The Focus outline's colour, one cell selected: #217346 under the built-in Chrome.
            await clickCell(grid, 'D9');
            await expectFocusAt(grid, 'D9');
            const outline = await resolvedColour(page, await grid.locator('.ex-focus').first().evaluate((el) => getComputedStyle(el, '::after').borderTopColor));
            if (!query) expect(outline, `${scheme}: the Focus outline on the Paper`).toEqual(GREEN);
            const found = {};
            // A8 beside the Headings and pinned, B8 beside the pinned A, E1 under the header, E8 in the open.
            for (const address of ['A8', 'B8', 'E1', 'E8']) {
                await clickCell(grid, 'B3');
                await clickCell(grid, 'C4', { modifiers: ['Shift'] });
                await expect(grid.locator('.ex-range-single')).toHaveCount(1);
                await clickCell(grid, address, { modifiers: ['ControlOrMeta'] });
                await expectFocusAt(grid, address);
                await expect(grid.locator('.ex-focus.ex-focus-among-ranges')).toHaveCount(1);
                await expect(grid.locator('.ex-range-single')).toHaveCount(0);
                await page.mouse.move(0, 0);
                await circuitQuiet();
                await twoFrames(page);
                const border = await resolvedColour(page, await grid.locator('.ex-focus-among-ranges').evaluate((el) => getComputedStyle(el, '::after').borderTopColor));
                expect(border, `${scheme}, ${address}: the border is the Focus outline's colour`).toEqual(outline);
                found[address] = await edgePixels(page, await onDevicePixels(page, cell(grid, address)), border);
            }
            // The Heading Mark past the edge a Heading stands at: A8's Row Heading, E1's column Heading.
            const marked = { A8: { left: [-2, -1] }, E1: { top: [-2, -1] } };
            for (const [address, edges] of Object.entries(found)) {
                // A gridline is whole Device Pixels: one at 100% and 150% (ADR-0090).
                const gridline = Math.max(1, Math.floor(edges.scale + 1e-9));
                // The display's scale, as its project sets it: under chrome-150 the reading is at 150%.
                expect(edges.scale).toBe(testInfo.project.name === 'chrome-150' ? 1.5 : 1);
                const mark = marked[address] ?? {};
                expect(edges, `${scheme}, ${address}: ${JSON.stringify(edges)}`).toEqual({
                    scale: edges.scale,
                    left: [...(mark.left ?? []), 1],
                    top: [...(mark.top ?? []), 1],
                    right: [gridline + 1],
                    bottom: [gridline + 1],
                });
            }
        }
    });
}

// The readings of 2026-10-12 (R4) put Excel's active cell among several areas in three selections,
// at Excel's 100% and 150% zoom on a 150% display, and read four short lines across its edges. Here
// the same three on the Paper, on a blank Sheet with nothing pinned as Excel's was, read on the same
// lines: the border, one device pixel of #217346 a device pixel inside the cell; the white line
// between it and the gridline; and, past the gridline, a neighbour in the area shaded #C7C7C7 (white
// Paper × 0.78) and one outside it white. R4 read them on a 150% display, at Excel's 100% and 150%
// zoom, which chrome-150 lays out; at 100% they are read the same, by generalisation (no run read
// Excel on a 100% display). The gridline's
// own pixel round the cell is not read: R4 read it #AFAFAF, and the Paper paints its gridline there
// (#E0E0E0), a difference no decision has taken up. The Headings beside A1, which R4 read as Excel's
// two device pixels of #217346, are the Heading Mark (ADR-0008, 2026-10-13), read past A1's left and top
// edges as R4 read them: the mark, then A1's own white Device Pixel, then the border.
test('UX-19/SH-54 (built-in Chrome): R4\'s three selections on the Paper: the active cell\'s border, the white line outside it and what lies past its gridline (ADR-0008, 2026-10-12; ADR-0071) @behaviour', async ({ page }, testInfo) => {
    await page.goto('/sheet?case=13');
    const grid = sheet(page);
    await expect(grid.locator('.ex-row').first()).toBeVisible();
    await expect(page.locator('#sheet-positions .ex-row').first()).toBeVisible();
    const scale = await page.evaluate(() => devicePixelRatio);
    expect(scale).toBe(testInfo.project.name === 'chrome-150' ? 1.5 : 1);
    const gridline = Math.max(1, Math.floor(scale + 1e-9));
    const SHADE = WHITE.map((v) => v * 0.78);
    const readAt = async (focus, beyond, what, headings = []) => {
        await page.mouse.move(0, 0);
        await circuitQuiet();
        await twoFrames(page);
        const box = await onDevicePixels(page, cell(grid, focus));
        const edges = await edgePixels(page, box, GREEN);
        // Past an edge a Heading stands at, the Heading Mark's two Device Pixels (R4 (2), at A1).
        const past = (side) => (headings.includes(side) ? [-2, -1] : []);
        expect(edges, `${what}: the border at ${focus}`).toEqual({
            scale, left: [...past('left'), 1], top: [...past('top'), 1], right: [gridline + 1], bottom: [gridline + 1],
        });
        const { at } = await acrossEdges(page, box);
        // The white line between the border and the gridline, on every side.
        for (const [side, index] of [['left', 0], ['top', 0], ['right', gridline], ['bottom', gridline]]) {
            expect(at(side, index), `${what}: white between the border and the gridline, ${side} of ${focus}`).toEqual(WHITE);
        }
        // Past the gridline on the right and the bottom: shaded where the area goes on, else white.
        for (const side of ['right', 'bottom']) {
            const pixel = at(side, -1);
            const want = beyond[side] ? SHADE : WHITE;
            expect(paints(pixel, want), `${what}: past ${focus}'s ${side} gridline ${JSON.stringify(pixel)}, want ${JSON.stringify(want)}`).toBe(true);
        }
    };

    // (1) A1:B2, then D4:E6 with Ctrl: D4 is the active cell, and E4 and D5 are in its area.
    await clickCell(grid, 'A1');
    await clickCell(grid, 'B2', { modifiers: ['Shift'] });
    await clickCell(grid, 'D4', { modifiers: ['ControlOrMeta'] });
    await clickCell(grid, 'E6', { modifiers: ['ControlOrMeta', 'Shift'] });
    await expectFocusAt(grid, 'D4');
    await expect(grid.locator('.ex-focus.ex-focus-among-ranges')).toHaveCount(1);
    await readAt('D4', { right: true, bottom: true }, '(1)');
    // (2) Enter six times: D5, D6, E4, E5, E6, then A1, in A1:B2 (as R4 recorded, ADR-0052).
    for (let i = 0; i < 6; i++) await page.keyboard.press('Enter');
    await expectFocusAt(grid, 'A1');
    await expect(grid.locator('.ex-focus.ex-focus-among-ranges')).toHaveCount(1);
    await readAt('A1', { right: true, bottom: true }, '(2)', ['left', 'top']);
    // (3) A1:B2, then D4 alone with Ctrl: D4's area is D4, so E4 and D5 are white.
    await clickCell(grid, 'C9');
    await clickCell(grid, 'A1');
    await clickCell(grid, 'B2', { modifiers: ['Shift'] });
    await clickCell(grid, 'D4', { modifiers: ['ControlOrMeta'] });
    await expectFocusAt(grid, 'D4');
    await expect(grid.locator('.ex-focus.ex-focus-among-ranges')).toHaveCount(1);
    await readAt('D4', { right: false, bottom: false }, '(3)');
});

// Run 18a read R4's first selection again with D4 filled yellow (focus-among-ranges.md 2,
// verification/2026-10-14-windows-readings-18a, at Excel's 100% and 150% zoom on a 150% display): the
// border one device pixel of #217346 as before; between it and the gridline, on the left and the top a
// white device pixel, a line of its own, and on the right and the bottom the Fill. Here the same, on
// the Paper (ADR-0008, run 18a): the white line is drawn along the border's whole top and left, and
// the Fill shows on the right and the bottom. The gridline's own pixel round the cell is not read, as
// in R4's test above: run 18a read it as the Fill under the shade, #C7C700, which is the line the Paper
// does not draw (ADR-0008, 2026-10-13, observed and not followed). While D4 is edited the Cell Editor
// covers the cell, and the border and the white line inside it with it, and draws no outline of its
// own while the keyboard is in the Sheet (2026-10-16, decided with the user): every device pixel of
// the cell is the editor's ground, the cell's Fill, and every one past its edges is as it was before
// the edit, as Excel's cell showed no border once F2 was pressed (focus-among-ranges.md 3). Once the
// keyboard has gone to the page the edit stands and is told apart (ED-27): the editor's outline, 1px
// of --ex-editor-outline's colour, a whole device pixel inside the cell, over the Fill, on every side.
test('UX-19/SH-54 (built-in Chrome): run 18a\'s D4 filled yellow among several ranges: the border, a white line outside it on the top and the left, the Fill on the right and the bottom, and no outline round the Cell Editor until the keyboard leaves (ADR-0008, run 18a; ADR-0018 section 6, ED-27; ADR-0071) @behaviour', async ({ page }, testInfo) => {
    await page.goto('/sheet?case=18a-2');
    const grid = sheet(page);
    await expect(grid.locator('.ex-row').first()).toBeVisible();
    await expect(page.locator('#sheet-positions .ex-row').first()).toBeVisible();
    const scale = await page.evaluate(() => devicePixelRatio);
    expect(scale).toBe(testInfo.project.name === 'chrome-150' ? 1.5 : 1);
    const gridline = Math.max(1, Math.floor(scale + 1e-9));
    const FILL = [255, 255, 0];
    const SHADE = WHITE.map((v) => v * 0.78);

    // R4's first selection: A1:B2, then D4:E6 with Ctrl, D4 the active cell and E4 and D5 in its area.
    await clickCell(grid, 'A1');
    await clickCell(grid, 'B2', { modifiers: ['Shift'] });
    await clickCell(grid, 'D4', { modifiers: ['ControlOrMeta'] });
    await clickCell(grid, 'E6', { modifiers: ['ControlOrMeta', 'Shift'] });
    await expectFocusAt(grid, 'D4');
    await expect(grid.locator('.ex-focus.ex-focus-among-ranges')).toHaveCount(1);
    await page.mouse.move(0, 0);
    await circuitQuiet();
    await twoFrames(page);
    const box = await onDevicePixels(page, cell(grid, 'D4'));
    expect(await edgePixels(page, box, GREEN), 'the border at D4').toEqual({
        scale, left: [1], top: [1], right: [gridline + 1], bottom: [gridline + 1],
    });
    const { at } = await acrossEdges(page, box);
    const expectPaints = (side, index, want, what) => {
        const pixel = at(side, index);
        expect(paints(pixel, want), `${what}, ${side} of D4 at ${index}: ${JSON.stringify(pixel)}, want ${JSON.stringify(want)}`).toBe(true);
    };
    for (const side of ['left', 'top']) {
        expectPaints(side, 0, WHITE, 'the white line between the border and the gridline');
        expectPaints(side, 2, FILL, 'the Fill inside the border');
    }
    for (const side of ['right', 'bottom']) {
        expectPaints(side, gridline, FILL, 'the Fill between the border and the gridline');
        expectPaints(side, gridline + 2, FILL, 'the Fill inside the border');
        expectPaints(side, -1, SHADE, 'E4 and D5, in the area, past the gridline');
    }
    // The white line along the border's whole top and left: the cell's first device row from its left
    // edge to the border's right side, and its first device column from its top to the border's
    // bottom, every pixel read whole.
    const region = await painted(page, onDeviceGrid(box, scale, 1));
    const reach = (from, length) => length - (gridline + 1) / scale - from;
    const top = wholePixelCentres({ x: box.x, y: box.y, width: reach(0, box.width), height: 1 / scale }, scale);
    const left = wholePixelCentres({ x: box.x, y: box.y, width: 1 / scale, height: reach(0, box.height) }, scale);
    expect(top.ys).toHaveLength(1);
    expect(left.xs).toHaveLength(1);
    const notWhite = [
        ...top.xs.map((x) => [x, top.ys[0]]),
        ...left.ys.map((y) => [left.xs[0], y]),
    ].filter(([x, y]) => !paints(region.at(x, y), WHITE)).map(([x, y]) => `${x},${y}: ${JSON.stringify(region.at(x, y))}`);
    expect(notWhite, 'the white line on the top and the left, its whole length').toEqual([]);

    // What lies past D4's edges before the edit, which the edit leaves as it is.
    const OUT = [-2, -1];
    const outside = Object.fromEntries(['left', 'top', 'right', 'bottom'].map((side) => [side, OUT.map((i) => at(side, i))]));

    // F2: the Cell Editor covers D4, and the border and the white line inside it with it.
    await page.keyboard.press('F2');
    await expect(editor(grid)).toBeFocused();
    await expect(grid).toHaveClass(/ex-editing/);
    await page.mouse.move(0, 0);
    await circuitQuiet();
    await twoFrames(page);
    const edited = await onDevicePixels(page, cell(grid, 'D4'));
    expect(edited, 'D4 where it was').toEqual(box);
    const editorBox = await onDevicePixels(page, grid.locator('.ex-viewport .ex-editor').first());
    expect(editorBox, 'the editor\'s box is D4\'s').toEqual(box);
    const inEdit = await acrossEdges(page, edited);
    for (const side of ['left', 'top', 'right', 'bottom']) {
        for (let index = 0; index <= 4; index++) {
            const pixel = inEdit.at(side, index);
            expect(paints(pixel, FILL), `no outline, the editor shows D4's Fill: ${side} at ${index}: ${JSON.stringify(pixel)}`).toBe(true);
        }
        expect(OUT.map((i) => inEdit.at(side, i)), `past D4's ${side} edge, as before the edit`).toEqual(outside[side]);
    }

    // The keyboard goes to the page: the edit stands, and its editor is outlined 1px wide, in the
    // token's colour over the Fill, on the cell's first device pixel on every side.
    await page.locator('h1').click();
    await expect(editor(grid)).not.toBeFocused();
    await expect(editor(grid)).toHaveCount(1);
    const ink = await editor(grid).evaluate((field) => {
        const style = getComputedStyle(field.closest('.ex-editor'));
        return { width: style.outlineWidth, offset: style.outlineOffset, color: style.outlineColor };
    });
    // 1px, which the browser draws on whole device pixels: one at a scale of 1 and at 1.5 alike
    // (0.666667px there), with the offset its width inward.
    const devicePixels = (css) => +(parseFloat(css) * scale).toFixed(3);
    expect({ width: devicePixels(ink.width), offset: devicePixels(ink.offset) }, `the editor whose keyboard is elsewhere: ${JSON.stringify(ink)}`)
        .toEqual({ width: gridline, offset: -gridline });
    const rgba = ink.color.match(/^rgba?\((\d+), (\d+), (\d+)(?:, ([\d.]+))?\)$/);
    expect(rgba, `the outline's colour, ${ink.color}, as rgb() or rgba()`).not.toBeNull();
    const [r, g, b] = rgba.slice(1, 4).map(Number);
    const alpha = Math.round((rgba[4] === undefined ? 1 : Number(rgba[4])) * 255) / 255;
    const OUTLINE = blend([r, g, b], alpha, FILL);
    await page.mouse.move(0, 0);
    await circuitQuiet();
    await twoFrames(page);
    const away = await acrossEdges(page, await onDevicePixels(page, cell(grid, 'D4')));
    for (const side of ['left', 'top', 'right', 'bottom']) {
        const pixel = away.at(side, 0);
        expect(paints(pixel, OUTLINE), `the 1px outline, ${side} of D4 at 0: ${JSON.stringify(pixel)}, want ${JSON.stringify(OUTLINE)}`).toBe(true);
        for (let index = 2; index <= 4; index++) {
            expect(paints(away.at(side, index), FILL), `inside the outline, ${side} at ${index}`).toBe(true);
        }
        expect(OUT.map((i) => away.at(side, i)), `past D4's ${side} edge, as before the edit`).toEqual(outside[side]);
    }

    // Back on D4's text: the keyboard returns to the edit, and the outline goes again.
    await editor(grid).click();
    await expect(editor(grid)).toBeFocused();
    await expect.poll(() => editor(grid).evaluate((field) => getComputedStyle(field.closest('.ex-editor')).outlineWidth)).toBe('0px');
    await page.keyboard.press('Escape');
    await expect(editor(grid)).toHaveCount(0);
});

test('SH-39: setting --ex-sheet-paper and --ex-sheet-ink changes the Paper and the Ink (ADR-0071, ADR-0027) @behaviour', async ({ page }) => {
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
    test(`SH-39 (${chrome} Chrome): a pinned cell that draws a line layer keeps its row gridline beneath it (ADR-0071, ADR-0050 item 15, ticket 90) ${chromeHost(chrome)}`, async ({ page }) => {
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
        test(`DC-58/SH-39 (${chrome} Chrome, ${scheme} scheme): the Cell Editor keeps the edited cell's Font, in the Font's own colour, and its box (ADR-0050 item 15, ADR-0071, ticket 88) ${chromeHost(chrome)}`, async ({ page }) => {
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
                await expect(grid.locator('.ex-hover-row')).toHaveCount(0);

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

    test(`DC-58/SH-39 (${chrome} Chrome): the Cell Editor keeps a Fill as its ground, and ADR-0057's coloured References read over it (ADR-0050 item 15, ADR-0071, ticket 88) ${chromeHost(chrome)}`, async ({ page }) => {
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
// its ellipsis lifted and its clip moved out past the pixels pictured, are one picture over the cell
// and the pixels either side of it. A cut glyph or an ellipsis is their difference. The columns' edges
// are on Device Pixels (ADR-0090), so the pictures' edges are too.
//
// The clip is moved out, not lifted. At 150%, with LCD antialiasing on as CI's Linux has it, lifting
// the clip of a row's last number had Chrome paint the whole row's text in grayscale: every glyph
// differed, and none was cut (I2, found in CI, 2026-10-11). Lifting another cell's clip changed
// nothing, and neither did moving any cell's clip out.

/** How far either side of a cell its pictures reach, in CSS pixels; the unclipped picture's clip is there. */
const AROUND = 12;

/** Opens a case with the Focus off the cells read and the pointer off the Sheet, in the face the widths are for. */
async function openFit(page, name, query) {
    await openCase(page, name, 'light', query);
    const grid = sheet(page);
    // The Focus outline would lie over A1; A10 holds nothing.
    await pressCell(grid, 'A10');
    // Roboto has arrived under the Wrapper's Chrome.
    await page.evaluate(() => document.fonts.ready);
    await page.mouse.move(0, 0);
    // The second way each cell is pictured, set by stillPictures' mark: its ellipsis lifted, and its
    // clip moved out to the edge of the pictures.
    await alterPage(page, (around) => {
        const style = document.createElement('style');
        style.textContent = '.ex-cell[data-fit-picture="unclipped"] { overflow: clip !important; '
            + `overflow-clip-margin: ${around}px !important; text-overflow: clip !important; }`;
        document.head.append(style);
        return () => style.remove();
    }, AROUND);
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
    const region = onDeviceGrid(box, scale, AROUND);
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
    test(`SH-41 (${chrome} Chrome): a bold number is #### exactly when it does not fit at the bold widths (ADR-0071, ADR-0016) ${chromeHost(chrome)}`, async ({ page }) => {
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

    test(`SH-41/VZ-19 (${chrome} Chrome): a bold number shown a hundredth over the bold edge is painted whole, wherever its column's edges fall on Device Pixels (ADR-0071, ADR-0016, ADR-0090) ${chromeHost(chrome)}`, async ({ page }, testInfo) => {
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

    test(`SH-41 (${chrome} Chrome): General fits a bold number to the bold widths, and both are painted whole (ADR-0071, ADR-0047) ${chromeHost(chrome)}`, async ({ page }) => {
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

    test(`SH-41 (${chrome} Chrome): an italic number that fits is not cut at either edge (ADR-0071, ADR-0016) ${chromeHost(chrome)}`, async ({ page }) => {
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
