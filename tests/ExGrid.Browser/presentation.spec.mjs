import { test, expect, alterPage, circuitQuiet } from './fixtures.mjs';
import { painted, sameColour } from './pixels.mjs';
import { expectActiveDescendant } from './keyboard.mjs';
import { sheet, openSheet, pressCell as pressSheetCell, editor as sheetEditor } from './sheet-helpers.mjs';

// The presentation contract, measured (ADR-0027/0028/0029/0031): tokens win where the
// contract says they win, the painted geometry equals the declared geometry, nothing
// under the Viewport animates, forced colors keep every state tellable, and the grid
// stays an LTR island inside an RTL page. Console errors fail the run, as everywhere.

function grid(page) {
    return page.locator('.ex-grid').first();
}

async function open(page) {
    await page.goto('/features');
    await expect(grid(page).locator('.ex-row').first()).toBeVisible();
}

test('geometry tokens are inline and read-only: an ancestor or stylesheet cannot move them (UX-2)', async ({ page }) => {
    await open(page);
    const before = await grid(page).locator('.ex-row').first()
        .evaluate((el) => getComputedStyle(el).height);
    expect(before).toBe('24px');

    // On body and in the head, which outlive the page: alterPage takes both off as the test
    // ends (ADR-0056).
    await alterPage(page, () => {
        document.body.style.setProperty('--ex-row-height', '60px');
        // A stylesheet rule on the element the token lives on, as UX-2's
        // verification says. (A rule re-declaring the token on a DESCENDANT — or an
        // !important — can still displace it by CSS's own cascade; recorded as the
        // known limitation in ADR-0029's corrections, not silently passed over.)
        const sheet = document.createElement('style');
        sheet.textContent = '.ex-grid { --ex-row-height: 60px; }';
        document.head.append(sheet);
        return () => {
            document.body.style.removeProperty('--ex-row-height');
            sheet.remove();
        };
    });

    const after = await grid(page).locator('.ex-row').first()
        .evaluate((el) => getComputedStyle(el).height);
    expect(after, 'the inline declaration wins; painted row height is unchanged').toBe('24px');
});

test('the painted heights equal the declared metrics exactly (UX-3, ST-3)', async ({ page }) => {
    await open(page);

    // RowHeight=24, HeaderHeight=28, two tiers of Header Groups: the band is 56.
    const measured = await page.evaluate(() => {
        const g = document.querySelector('.ex-grid');
        return {
            row: getComputedStyle(g.querySelector('.ex-row')).height,
            header: getComputedStyle(g.querySelector('.ex-header')).height,
            cellPadding: getComputedStyle(g.querySelector('.ex-cell')).paddingLeft,
        };
    });
    expect(measured.row).toBe('24px');
    expect(measured.header).toBe('56px');
    expect(measured.cellPadding).toBe('8px');
});

// A density change on /density (ADR-0028): each preset's resolved Grid Metrics, from
// GridMetrics.Resolve's table — the row and header tier heights, the cell padding and the
// font size. The page's one Header Group makes the band two tiers tall.
const PRESETS = {
    standard: { row: 32, header: 32, padding: 8, font: 14 },
    comfortable: { row: 40, header: 40, padding: 12, font: 14 },
    excel: { row: 20, header: 20, padding: 4, font: 12 },
    compact: { row: 28, header: 28, padding: 8, font: 14 },
};

/** What the grid on /density painted: every row's box, the header band and its cells, the
 * spacer the row arithmetic sizes, the Selection's outline and the Focus, each beside the
 * cells it stands over. */
function paintedGeometry(page) {
    return grid(page).evaluate((g) => {
        const box = (el) => {
            const r = el.getBoundingClientRect();
            return { left: r.left, top: r.top, right: r.right, bottom: r.bottom, width: r.width, height: r.height };
        };
        const shown = (selector) => [...g.querySelectorAll(selector)].map(box).find((r) => r.width > 0) ?? null;
        const cell = g.querySelector('.ex-cell');
        return {
            token: g.style.getPropertyValue('--ex-row-height'),
            rows: [...g.querySelectorAll('.ex-row')]
                .map((row) => ({ index: Number(row.getAttribute('aria-rowindex')), ...box(row) }))
                .sort((a, b) => a.index - b.index),
            header: box(g.querySelector('.ex-header')),
            leaves: [...g.querySelectorAll('.ex-header-cell')].map(box),
            group: box(g.querySelector('.ex-header-group')),
            padding: getComputedStyle(cell).paddingLeft,
            font: getComputedStyle(cell).fontSize,
            spacer: box(g.querySelector('.ex-spacer')),
            range: shown('.ex-range'),
            focus: shown('.ex-focus'),
            from: box(g.querySelector("[id$='r1c0']")),
            to: box(g.querySelector("[id$='r3c1']")),
        };
    });
}

test('UX-3 / ST-3 / ADR-0027 P6, ADR-0028: after each density change the rows, header band and Selection are painted at the resolved Grid Metrics, at every preset', async ({ page }) => {
    await page.goto('/density');
    await expect(grid(page).locator('.ex-row').first()).toBeVisible();
    // A range over rows 1-3 of Book and Trader, the Focus at its top left.
    await grid(page).locator("[id$='r1c0']").click({ force: true });
    await expectActiveDescendant(grid(page), /r1c0$/);
    await grid(page).locator("[id$='r3c1']").click({ force: true, modifiers: ['Shift'] });
    await expect(page.locator('#density-status')).toHaveText('Density: Compact');

    for (const [name, m] of Object.entries(PRESETS)) {
        await page.locator(`#density-${name}`).click();
        await expect.poll(async () => (await paintedGeometry(page)).rows[0]?.height, `${name}: the rows repaint`)
            .toBe(m.row);
        await circuitQuiet();
        const p = await paintedGeometry(page);
        const at = (what) => `${name}: ${what}`;

        // The arithmetic's Row Height: the spacer it sizes is the band plus 200 rows of it.
        expect(p.token, at('the inline Geometry Token')).toBe(`${m.row}px`);
        expect(p.spacer.height, at('the spacer the row arithmetic sizes')).toBe(2 * m.header + 200 * m.row);
        // The painted Row Height: every painted row is that tall and stands one row below the last.
        expect(p.rows.length, at('rows are painted')).toBeGreaterThan(3);
        for (const [i, row] of p.rows.entries()) {
            expect(row.height, at(`row ${row.index} height`)).toBe(m.row);
            if (i > 0) expect(row.top - p.rows[i - 1].top, at(`row ${row.index} pitch`)).toBe(m.row);
        }
        // The band: two tiers, the group one tier tall, the covered leaves one tier, the uncovered
        // leaf (Notional) the band.
        expect(p.header.height, at('the band')).toBe(2 * m.header);
        expect(p.group.height, at('the group tier')).toBe(m.header);
        expect(p.leaves.map((leaf) => leaf.height), at('the leaves')).toEqual([m.header, m.header, 2 * m.header]);
        expect(p.rows[0].top, at('the first row starts under the band')).toBe(p.header.bottom);
        expect(p.padding, at('cell padding')).toBe(`${m.padding}px`);
        expect(p.font, at('font size')).toBe(`${m.font}px`);
        // The Selection's outline covers rows 1-3 of Book and Trader exactly, and the Focus its
        // top left cell: the overlay's arithmetic and the rows' paint are one number.
        expect(p.range, at('the Selection is painted')).not.toBeNull();
        expect({ left: p.range.left, top: p.range.top, right: p.range.right, bottom: p.range.bottom }, at('the Selection'))
            .toEqual({ left: p.from.left, top: p.from.top, right: p.to.right, bottom: p.to.bottom });
        expect(p.range.height, at('the Selection is three rows')).toBe(3 * m.row);
        expect({ left: p.focus.left, top: p.focus.top, width: p.focus.width, height: p.focus.height }, at('the Focus'))
            .toEqual({ left: p.from.left, top: p.from.top, width: p.from.width, height: m.row });
    }
});

test('a Visual Token set on an ancestor recolours the paint (UX-5, the paint half)', async ({ page }) => {
    await open(page);
    const before = await grid(page).locator('.ex-header')
        .evaluate((el) => getComputedStyle(el).backgroundColor);

    await alterPage(page, () => {
        document.body.style.setProperty('--ex-header-background', 'rgb(200, 12, 12)');
        return () => document.body.style.removeProperty('--ex-header-background');
    });

    const after = await grid(page).locator('.ex-header')
        .evaluate((el) => getComputedStyle(el).backgroundColor);
    expect(after).toBe('rgb(200, 12, 12)');
    expect(after).not.toBe(before);
});

test('nothing inside the Viewport transitions or animates (UX-6)', async ({ page }) => {
    await open(page);

    const offenders = await page.evaluate(() =>
        [...document.querySelectorAll('.ex-viewport, .ex-viewport *')]
            .map((el) => ({
                cls: el.className,
                transition: getComputedStyle(el).transitionProperty,
                duration: getComputedStyle(el).transitionDuration,
                animation: getComputedStyle(el).animationName,
            }))
            .filter((s) => s.animation !== 'none'
                || (s.transition !== 'none' && !/^0s(, 0s)*$/.test(s.duration))));
    expect(offenders).toEqual([]);
});

test('forced colors keep every Cell State and Row Kind tellable (UX-7, on /cells)', async ({ page }) => {
    await page.emulateMedia({ forcedColors: 'active' });
    await page.goto('/cells');
    await expect(page.locator('.ex-grid').first().locator('.ex-row').first()).toBeVisible();

    const states = await page.evaluate(() => {
        const normal = document.querySelector('.ex-cell:not([class*=ex-state])');
        const describe = (el) => {
            if (!el) return null;
            const s = getComputedStyle(el);
            return [s.color, s.fontStyle, s.outlineStyle, s.textDecorationLine].join('|');
        };
        return {
            normal: describe(normal),
            stale: describe(document.querySelector('.ex-state-stale')),
            missing: describe(document.querySelector('.ex-state-missing')),
            error: describe(document.querySelector('.ex-state-error')),
            modified: describe(document.querySelector('.ex-state-modified')),
        };
    });
    // Every present state is distinguishable from Normal, and from each other.
    const present = Object.entries(states).filter(([, v]) => v !== null);
    const values = present.map(([, v]) => v);
    expect(new Set(values).size, JSON.stringify(states)).toBe(values.length);
});

// Tone and Cell State on /tones (ticket 84): the page's theme sets the tone tokens on an element
// around the grid and leaves every Cell State at the grid's default. Rows: a gain 0, a loss 1, and
// a flat value 2, which has no tone. Columns: Book 0 (pinned), then one per state, every cell in
// it in that state.
const TONED = { gain: 0, loss: 1, flat: 2 };
const STATE_COLUMN = { normal: 1, stale: 2, error: 3, modified: 4 };

const rgbOf = (css) => css.match(/\d+(\.\d+)?/g).slice(0, 3).map(Number);

/** How far a painted pixel lies from every mix of a colour with the ground: 0 for anti-aliased
 * text in that colour, whatever its coverage. */
function offLine(pixel, ground, colour) {
    const towards = colour.map((v, i) => v - ground[i]);
    const from = pixel.map((v, i) => v - ground[i]);
    const along = towards.reduce((sum, v) => sum + v * v, 0);
    const coverage = Math.min(1, Math.max(0, from.reduce((sum, v, i) => sum + v * towards[i], 0) / along));
    return Math.hypot(...from.map((v, i) => v - coverage * towards[i]));
}

/** A cell's ground, in its left padding, and its most inked pixels — the farthest from that
 * ground — as painted, in device pixels. */
async function inkOf(page, locator, count = 6) {
    const box = await locator.boundingBox();
    const region = await painted(page, box);
    const ground = region.at(box.x + 2, box.y + box.height / 2);
    const pixels = [];
    for (let j = 0; j < Math.floor(box.height * region.scale); j++) {
        for (let i = 0; i < Math.floor(box.width * region.scale); i++) {
            pixels.push(region.at(box.x + (i + 0.5) / region.scale, box.y + (j + 0.5) / region.scale));
        }
    }
    const distance = (pixel) => pixel.reduce((sum, v, i) => sum + Math.abs(v - ground[i]), 0);
    return { ground, inked: pixels.sort((a, b) => distance(b) - distance(a)).slice(0, count) };
}

test('a toned Stale or Error cell paints in its state\'s colour under a theme that sets the tone tokens (ADR-0006, ADR-0029)', async ({ page }) => {
    await page.goto('/tones');
    const cellAt = (row, column) => page.locator(`.ex-grid [id$='r${row}c${column}']`);
    await expect(cellAt(TONED.loss, STATE_COLUMN.error)).toHaveClass(/ex-tone-negative/);
    await expect(cellAt(TONED.gain, STATE_COLUMN.error)).toHaveClass(/ex-tone-positive/);
    await expect(cellAt(TONED.flat, STATE_COLUMN.error)).not.toHaveClass(/ex-tone-/);
    await page.mouse.move(0, 0);
    const colourOf = async (row, column) => rgbOf(await cellAt(row, column).evaluate((el) => getComputedStyle(el).color));

    // The theme is in effect: an ordinary gain and loss paint the tone's colours, not the text's.
    const text = await colourOf(TONED.flat, STATE_COLUMN.normal);
    const tones = { gain: await colourOf(TONED.gain, STATE_COLUMN.normal), loss: await colourOf(TONED.loss, STATE_COLUMN.normal) };
    expect(tones.gain, 'the theme paints a gain').not.toEqual(text);
    expect(tones.loss, 'the theme paints a loss').not.toEqual(text);

    for (const state of ['stale', 'error']) {
        // The state's own colour, on the flat value, where no tone competes with it.
        const own = await colourOf(TONED.flat, STATE_COLUMN[state]);
        expect(own, `${state} paints a colour of its own`).not.toEqual(text);
        for (const tone of ['gain', 'loss']) {
            const toned = cellAt(TONED[tone], STATE_COLUMN[state]);
            await expect(toned).toHaveClass(new RegExp(`ex-state-${state}`));
            expect(await colourOf(TONED[tone], STATE_COLUMN[state]), `a ${state} ${tone}, as the cascade resolved it`).toEqual(own);
            // And on the screen: its most inked pixels are the state's colour, not the tone's.
            const { ground, inked } = await inkOf(page, toned);
            for (const pixel of inked) {
                expect(offLine(pixel, ground, own) < offLine(pixel, ground, tones[tone]),
                    `a ${state} ${tone}: ${pixel} over ${ground} is the tone's ${tones[tone]}, not the state's ${own}`).toBe(true);
            }
        }
    }
    // The state's other marks stay with its colour: Stale's slant, Error's wavy underline.
    await expect(cellAt(TONED.loss, STATE_COLUMN.stale)).toHaveCSS('font-style', 'italic');
    await expect(cellAt(TONED.loss, STATE_COLUMN.error)).toHaveCSS('text-decoration-style', 'wavy');

    // Modified paints no colour of its own but a mark in the cell's top right corner, which no tone
    // touches: the same on a gain, a loss and a flat value, and absent from an ordinary cell.
    const corner = async (row, column) => {
        const box = await cellAt(row, column).boundingBox();
        const region = await painted(page, box);
        return region.at(box.x + box.width - 1.5, box.y + 1.5);
    };
    const mark = await corner(TONED.flat, STATE_COLUMN.modified);
    expect(sameColour(mark, await corner(TONED.flat, STATE_COLUMN.normal), 8), 'Modified paints its mark').toBe(false);
    for (const tone of ['gain', 'loss']) {
        expect(sameColour(await corner(TONED[tone], STATE_COLUMN.modified), mark, 1), `a modified ${tone} keeps the mark`).toBe(true);
    }
});

// The Modified mark (ADR-0029's fourth correction, 2026-10-07). It was an inset box-shadow, which
// drew a band along the whole top and right edges rather than a corner, and which a colour in its
// token — ExGrid.MudBlazor's — turned into no mark at all.
test('the Modified mark is a triangle in the top right corner alone, in the colour its token names (UX-20, ADR-0029, ADR-0006)', async ({ page }) => {
    await page.goto('/tones');
    const cellAt = (row, column) => page.locator(`.ex-grid [id$='r${row}c${column}']`);
    await expect(cellAt(TONED.flat, STATE_COLUMN.modified)).toHaveClass(/ex-state-modified/);
    await page.mouse.move(0, 0);

    // Each spot read in the Modified cell and in the Normal cell of the same row, so a column rule,
    // the row's ground or a stripe is the same in both and only the mark can differ.
    const spots = {
        corner: (box) => [box.x + box.width - 1.5, box.y + 1.5],
        'top edge, halfway': (box) => [box.x + box.width / 2, box.y + 1.5],
        'right edge, halfway': (box) => [box.x + box.width - 1.5, box.y + box.height / 2],
        'below the triangle': (box) => [box.x + box.width - 1.5, box.y + 9],
        'left of the triangle': (box) => [box.x + box.width - 9, box.y + 1.5],
    };
    const read = async (column) => {
        const box = await cellAt(TONED.flat, column).boundingBox();
        const region = await painted(page, box);
        return Object.fromEntries(Object.entries(spots).map(([name, at]) => [name, region.at(...at(box))]));
    };
    const modified = await read(STATE_COLUMN.modified);
    const normal = await read(STATE_COLUMN.normal);
    expect(sameColour(modified.corner, normal.corner, 8), 'the corner carries the mark').toBe(false);
    for (const name of ['top edge, halfway', 'right edge, halfway', 'below the triangle', 'left of the triangle']) {
        expect(sameColour(modified[name], normal[name], 2), `${name}: ${modified[name]} against ${normal[name]}`).toBe(true);
    }

    // The token is a colour, as every Cell State token is, and the mark is painted in it. Set
    // above the grid, where a Wrapper's stylesheet sets it, and taken back as the test ends.
    await alterPage(page, () => {
        document.body.style.setProperty('--ex-state-modified-mark', 'rgb(0, 160, 0)');
        return () => document.body.style.removeProperty('--ex-state-modified-mark');
    });
    const box = await cellAt(TONED.flat, STATE_COLUMN.modified).boundingBox();
    const region = await painted(page, box);
    expect(sameColour(region.at(box.x + box.width - 1.5, box.y + 1.5), [0, 160, 0], 8), 'the mark takes its token\'s colour').toBe(true);

    // The mark's cell is its containing block at no specificity: a Pinned Column's cell that is
    // Modified stays sticky. Read in one synchronous step, so no render sees the class.
    const position = await page.evaluate(() => {
        const pinned = document.querySelector('.ex-grid .ex-cell.ex-pinned:not(.ex-state-modified)');
        pinned.classList.add('ex-state-modified');
        const value = getComputedStyle(pinned).position;
        pinned.classList.remove('ex-state-modified');
        return value;
    });
    expect(position).toBe('sticky');
});

test('under a dark scheme the untouched grid stays readable (UX-8)', async ({ page }) => {
    await page.emulateMedia({ colorScheme: 'dark' });
    await open(page);

    const contrast = await page.evaluate(() => {
        const cell = document.querySelector('.ex-cell');
        const s = getComputedStyle(cell);
        const parse = (c) => c.match(/\d+(\.\d+)?/g).slice(0, 3).map(Number);
        const luminance = ([r, g, b]) => {
            const f = (v) => {
                v /= 255;
                return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4;
            };
            return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b);
        };
        const text = luminance(parse(s.color));
        const ground = luminance(parse(getComputedStyle(document.querySelector('.ex-grid')).backgroundColor));
        const ratio = (Math.max(text, ground) + 0.05) / (Math.min(text, ground) + 0.05);
        return { ratio, color: s.color };
    });
    expect(contrast.ratio, JSON.stringify(contrast)).toBeGreaterThanOrEqual(4.5);
});

test('inside an RTL ancestor the grid stays an LTR island (DIR-2/DIR-3)', async ({ page }) => {
    await open(page);
    // The grid's parent is #app on WebAssembly and body on the Server host, neither of which
    // leaving the page clears: alterPage puts the direction back as the test ends (ADR-0056).
    await alterPage(page, () => {
        const parent = document.querySelector('.ex-grid').parentElement;
        const was = parent.getAttribute('dir');
        parent.setAttribute('dir', 'rtl');
        return () => (was === null ? parent.removeAttribute('dir') : parent.setAttribute('dir', was));
    });

    // The overlay still lands on its cell to within a pixel.
    await grid(page).locator("[id$='r1c1']").click({ force: true });
    // On a Server circuit the Focus is painted a round trip after the click.
    await expectActiveDescendant(grid(page), /r1c1$/);
    const alignment = await page.evaluate(() => {
        const g = document.querySelector('.ex-grid');
        const cell = g.querySelector("[id$='r1c1']");
        const focus = [...g.querySelectorAll('.ex-focus')]
            .map((el) => el.getBoundingClientRect())
            .find((r) => r.width > 0);
        const c = cell.getBoundingClientRect();
        return { dx: Math.abs(focus.left - c.left), dy: Math.abs(focus.top - c.top) };
    });
    expect(alignment.dx).toBeLessThanOrEqual(1);
    expect(alignment.dy).toBeLessThanOrEqual(1);

    // Columns still flow left to right: the first header starts left of the second.
    const headers = await page.evaluate(() =>
        [...document.querySelectorAll('.ex-grid .ex-header-cell')].slice(0, 2)
            .map((el) => el.getBoundingClientRect().left));
    expect(headers[0]).toBeLessThan(headers[1]);

    // And the Arabic value renders inside its cell (DIR-3): present, not displaced.
    const arabic = grid(page).locator('.ex-cell', { hasText: 'عمر' }).first();
    await expect(arabic).toBeVisible();
});

test('a Header Group label is centred, and nothing aligns it vertically (HG-13, ADR-0032)', async ({ page }) => {
    await open(page);
    const group = grid(page).locator('.ex-header-group').first();
    await expect(group).toBeVisible();

    const painted = await group.evaluate((el) => {
        const style = getComputedStyle(el);
        return { textAlign: style.textAlign, verticalAlign: style.verticalAlign };
    });

    expect(painted.textAlign).toBe('center');
    // The other half of HG-13 is the absence of a knob, which the layer-1 surface test
    // holds; what a browser can say is that nothing is being aligned vertically here —
    // the row height is fixed (ADR-0013), so the option would have nothing to do.
    expect(painted.verticalAlign).toBe('baseline');
});

test("the editor's box is exactly the cell's (ED-9)", async ({ page }) => {
    await open(page);
    await grid(page).locator("[id$='r0c1']").click({ force: true });
    await page.keyboard.press('F2');
    // Painted by the render F2 asked for — a round trip away on the Server host.
    await expect(grid(page).locator('.ex-editor')).toHaveCount(1);

    const boxes = await page.evaluate(() => {
        const g = document.querySelector('.ex-grid');
        const cell = g.querySelector("[id$='r0c1']").getBoundingClientRect();
        const editor = g.querySelector('.ex-editor').getBoundingClientRect();
        return { cell, editor };
    });
    expect(Math.abs(boxes.editor.left - boxes.cell.left)).toBeLessThanOrEqual(1);
    expect(Math.abs(boxes.editor.top - boxes.cell.top)).toBeLessThanOrEqual(1);
    expect(Math.abs(boxes.editor.width - boxes.cell.width)).toBeLessThanOrEqual(1);
    expect(Math.abs(boxes.editor.height - boxes.cell.height)).toBeLessThanOrEqual(1);
    await page.keyboard.press('Escape');
});

test('the pointer leaving the grid stops the auto-scroll (SL-14/SL-15)', async ({ page }) => {
    await open(page);
    const g = grid(page);
    const box = await g.locator('.ex-scroller').boundingBox();

    // Drag from a top cell into the bottom band and hold. The drag's move handler arrives with
    // the render that answered the press (ADR-0008): on a circuit a move made before it is not
    // heard, and a pointer then held still in the band scrolls nothing (CI, Server host, chrome,
    // 2026-10-02). So the drag moves once the host has said all it will about the press.
    await page.mouse.move(box.x + 60, box.y + 60);
    await page.mouse.down();
    await circuitQuiet();
    await page.mouse.move(box.x + 60, box.y + box.height - 6, { steps: 4 });
    await expect.poll(() => g.locator('.ex-scroller').evaluate((el) => el.scrollTop)).toBeGreaterThan(0);

    // Leave the grid with the button still down: the scroll stops where it was.
    await page.mouse.move(box.x + 60, box.y + box.height + 200, { steps: 4 });
    const atLeave = await g.locator('.ex-scroller').evaluate((el) => el.scrollTop);
    await page.waitForTimeout(800);
    const afterWait = await g.locator('.ex-scroller').evaluate((el) => el.scrollTop);
    expect(afterWait, 'no runaway scroll after the pointer left (SL-14)').toBe(atLeave);

    // Return with the button released: the drag is over and stays over.
    await page.mouse.up();
    await page.mouse.move(box.x + 60, box.y + box.height - 6);
    await page.waitForTimeout(400);
    expect(await g.locator('.ex-scroller').evaluate((el) => el.scrollTop)).toBe(afterWait);
});

test('the Blazor error UI never appears (CON-5)', async ({ page }) => {
    await open(page);
    const display = await page.locator('#blazor-error-ui')
        .evaluate((el) => getComputedStyle(el).display);
    expect(display).toBe('none');
});

// ---------------------------------------------------------------------------------------------
// UX-4 (ADR-0029): the classes the core paints under its root are exactly ADR-0029's — the stable
// table, the internal list with what later ADRs entered in either there, and the internal names,
// prefixes and families its section of 2026-10-11 lists. Transcribed from the ADR, never from the
// stylesheet or the markup: a class the grid paints that nothing names is one a Wrapper could take
// for a contract, or one no decision ever made, and it fails the walk by name.

/** ADR-0029's stable table, with the entries added there by ADR-0043, ADR-0050/0051, ADR-0052,
 * ADR-0068 and ADR-0125. */
const STABLE_CLASSES = [
    'ex-grid', 'ex-loading',
    'ex-header', 'ex-header-cell', 'ex-header-group',
    'ex-align-left', 'ex-align-center', 'ex-align-right',
    'ex-row', 'ex-placeholder', 'ex-row-group', 'ex-row-total', 'ex-row-stripe',
    'ex-cell', 'ex-cell-numeric', 'ex-pinned',
    'ex-state-stale', 'ex-state-missing', 'ex-state-error', 'ex-state-modified',
    'ex-tone-positive', 'ex-tone-negative',
    'ex-formula-bar-borrowed', 'ex-changed',
    'ex-range', 'ex-focus',
    'ex-action', 'ex-interactive', 'ex-action-chosen',
    'ex-mark', 'ex-mark-all',
    'ex-editor', 'ex-editing',
    'ex-resize-guide', 'ex-drop-indicator',
    'ex-row-heading', 'ex-headings-corner', 'ex-formula-bar', 'ex-name-box', 'ex-fill-handle',
    'ex-size-tip',
];

/** ADR-0029's internal list, with the entries added there by ADR-0050/0051, ADR-0057, ADR-0058,
 * ADR-0130 and ADR-0170. */
const INTERNAL_CLASSES = [
    'ex-scroller', 'ex-spacer', 'ex-viewport', 'ex-gap', 'ex-selection', 'ex-selection-pinned',
    'ex-header-groups-pinned', 'ex-focus-row', 'ex-pager', 'ex-status', 'ex-mark-count',
    'ex-resize-grip', 'ex-menu-button', 'ex-popover', 'ex-popover-list', 'ex-popover-actions',
    'ex-editor-pinned', 'ex-announce',
    'ex-summary', 'ex-summary-figure', 'ex-summary-pending', 'ex-summary-declined',
    'ex-summary-menu', 'ex-summary-menu-item', 'ex-summary-menu-shown', 'ex-summary-menu-tick',
    'ex-formula-bar-text', 'ex-name-box-form', 'ex-fill-target',
    'ex-completion', 'ex-completion-list', 'ex-completion-item', 'ex-completion-selected', 'ex-completion-hint',
    'ex-point', 'ex-range-single',
    'ex-reference-outline', ...[1, 2, 3, 4, 5, 6, 7].map((n) => `ex-reference-${n}`),
    'ex-reference-text', 'ex-reference-text-line', 'ex-reference-text-cell', 'ex-reference-text-bar',
    'ex-reference-text-shown', 'ex-reference-pointed',
    'ex-pointed-at', 'ex-point-dashes',
    'ex-copied-range',
    // ADR-0029, "The classes UX-4's walk found unnamed" (2026-10-11): internal, every one.
    'ex-ceiling-probe', 'ex-focus-wrap', 'ex-hashes', 'ex-hover-row', 'ex-message', 'ex-select-all',
    'ex-value-list', 'ex-mark-result', 'ex-editor-away', 'ex-point-on-reference',
    'ex-key-field', 'ex-key-field-layer', 'ex-key-field-held', 'ex-key-field-composing',
    // The same section: the selection's tint and the pinned shade layer (ADR-0206).
    'ex-range-tint', 'ex-shade-pinned',
];

/** The same section's prefixes: every popover part not named above, and every part of Find. */
const INTERNAL_PREFIXES = ['ex-popover-', 'ex-find-'];

/** And its families, generated from a Sheet's document data: the Fill, the Font and the Border
 * sides (a side, a style and a colour), each named by its colour as six hex digits. */
const INTERNAL_FAMILIES = [
    /^ex-lined$/,
    /^ex-fill-[0-9a-f]{6}$/,
    /^ex-font-(x|[0-9a-f]{6})b?i?u?s?$/,
    /^ex-l[trbl]-[a-z]+-[0-9a-f]{6}$/,
];

const NAMED_CLASSES = new Set([...STABLE_CLASSES, ...INTERNAL_CLASSES]);

/** Whether ADR-0029 names a class: in the stable table, by an internal name, by an internal
 * prefix, or as one of the internal families. */
const named = (name) => NAMED_CLASSES.has(name)
    || INTERNAL_PREFIXES.some((prefix) => name.startsWith(prefix))
    || INTERNAL_FAMILIES.some((family) => family.test(name));

/**
 * Notes every class worn under a grid root from now until the test ends, the root's own
 * included, each with where it was first seen: a MutationObserver on the body, so a class that
 * comes and goes — a Placeholder, a guide held during a drag, a popover — is caught as it is
 * painted, without a wait. A class outside the grid's namespace counts only outside a cell: a
 * Template Column's markup is the Consumer's (ADR-0020). Through alterPage, which takes the
 * observer and its record off as the test ends (ADR-0056).
 */
async function watchClassesWorn(page) {
    await alterPage(page, () => {
        const worn = new Map();
        const note = (element) => {
            if (!(element instanceof Element) || !element.closest('.ex-grid')) {
                return;
            }
            const inCell = element.closest('.ex-cell') !== null;
            for (const name of element.classList) {
                if ((name.startsWith('ex-') || !inCell) && !worn.has(name)) {
                    worn.set(name, `${element.tagName.toLowerCase()} on ${location.pathname}${location.search}`);
                }
            }
        };
        const walk = (node) => {
            if (node instanceof Element) {
                note(node);
                node.querySelectorAll('*').forEach(note);
            }
        };
        const observer = new MutationObserver((records) => {
            for (const record of records) {
                if (record.type === 'attributes') {
                    note(record.target);
                } else {
                    record.addedNodes.forEach(walk);
                }
            }
        });
        observer.observe(document.body, { subtree: true, childList: true, attributes: true, attributeFilter: ['class'] });
        document.querySelectorAll('.ex-grid').forEach(walk);
        window.__exClassesWorn = worn;
        return () => {
            observer.disconnect();
            delete window.__exClassesWorn;
        };
    });
}

test('every class under the root is one ADR-0029 names, stable or internal (UX-4)', async ({ page, context }) => {
    test.setTimeout(180_000);
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    const at = (row, column, n = 0) => page.locator('.ex-grid').nth(n).locator(`[id$='r${row}c${column}']`);

    // /features: the editable, source-bound grid with Header Groups, a Focus band and the
    // clipboard. Watched from its first paint.
    await open(page);
    await watchClassesWorn(page);
    const features = grid(page);
    // A range, copied: its dashes stand while the clipboard holds it (ADR-0170). Only a browser
    // with the clipboardchange event draws them — Chrome and Edge from 143 — and the grid asks the
    // browser as below. An older build, Playwright's own Chromium among them, draws none: the walk
    // says so in an annotation and goes on without them, and the installed Chrome and Edge reach
    // them.
    const clipboardWatched = await page.evaluate(() => typeof globalThis.ClipboardChangeEvent === 'function'
        && typeof navigator.clipboard?.addEventListener === 'function');
    await at(0, 1).click({ force: true });
    await page.keyboard.press('Shift+ArrowDown');
    await page.keyboard.press('ControlOrMeta+C');
    if (clipboardWatched) {
        await expect(features.locator('.ex-copied-range')).not.toHaveCount(0);
    } else {
        test.info().annotations.push({
            type: 'UX-4: not reached',
            description: `ex-copied-range: this browser (${context.browser()?.version() ?? 'unknown'}) has no clipboardchange event, so the grid draws no Copied Range (ADR-0170)`,
        });
    }
    // A second range beside it, two cells wide: a range of one cell is painted as the Focus
    // alone, with no ex-range of its own.
    await at(3, 2).click({ force: true, modifiers: ['ControlOrMeta'] });
    await page.keyboard.press('Shift+ArrowRight');
    await expect(features.locator('.ex-range')).toHaveCount(2);
    // The Cell Editor, typed into, and left.
    await at(0, 1).click({ force: true });
    await page.keyboard.type('X');
    await expect(features.locator('input.ex-editor')).toHaveValue('X');
    await expect(features).toHaveClass(/\bex-editing\b/);
    await page.keyboard.press('Escape');
    await expect(features.locator('input.ex-editor')).toHaveCount(0);
    // A flagged value and its message on the Focus (ADR-0034).
    await at(0, 2).click({ force: true });
    await page.keyboard.type('-5');
    await page.keyboard.press('Enter');
    await expect(at(0, 2)).toHaveClass(/ex-state-error/);
    await at(0, 2).click({ force: true });
    await expect(features.locator('.ex-message')).toHaveCount(1);
    // The Context Menu, the column menu and the find panel, each opened and closed.
    await at(0, 1).click({ button: 'right', force: true });
    await expect(features.locator('[role=menu]')).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(features.locator('[role=menu]')).toHaveCount(0);
    await features.locator('.ex-menu-button').first().click();
    await expect(features.locator('.ex-popover')).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(features.locator('.ex-popover')).toHaveCount(0);
    await at(0, 0).click({ force: true });
    await page.keyboard.press('ControlOrMeta+f');
    await expect(features.locator('.ex-popover-find')).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(features.locator('.ex-popover-find')).toHaveCount(0);
    // A resize held on its guide, and a header held over its drop indicator (gestures.spec.mjs).
    const trader = await features.locator('.ex-header-cell').nth(1).boundingBox();
    await page.mouse.move(trader.x + trader.width - 2, trader.y + trader.height - 10);
    await page.mouse.down();
    await page.mouse.move(trader.x + trader.width + 38, trader.y + trader.height - 10, { steps: 4 });
    await expect(features.locator('.ex-resize-guide')).toBeVisible();
    await page.mouse.up();
    const header = await features.locator('.ex-header').boundingBox();
    await page.mouse.move(header.x + 55, header.y + 42);
    await page.mouse.down();
    await page.mouse.move(header.x + 600, header.y + 42, { steps: 8 });
    await expect(features.locator('.ex-drop-indicator')).toBeVisible();
    await page.mouse.up();

    // /cells: Cell States, Row Kinds, a pinned column, Templates and Actions; Space enters a cell
    // with several actions and one is chosen (ADR-0037).
    await page.goto('/cells');
    const cells = grid(page);
    await expect(cells.locator('.ex-row').first()).toBeVisible();
    await at(0, 0).click({ force: true });
    for (let i = 0; i < 6; i++) {
        await page.keyboard.press('ArrowRight');
    }
    await expect(cells).toHaveAttribute('aria-activedescendant', /r0c6$/);
    await page.keyboard.press(' ');
    await expect(cells.locator('.ex-action-chosen')).toHaveCount(1);
    await page.keyboard.press('Escape');

    // /tones: a gain and a loss; /stripes: the stripes and the hover band.
    await page.goto('/tones');
    await expect(grid(page).locator('.ex-tone-negative').first()).toBeAttached();
    await page.goto('/stripes');
    await expect(grid(page).locator('.ex-row-stripe').first()).toBeAttached();
    await at(2, 1).hover({ force: true });
    await expect(grid(page).locator('.ex-hover-row').first()).toBeAttached();
    await page.mouse.move(0, 0);

    // /marks: the Mark Column and its header.
    await page.goto('/marks');
    await expect(grid(page).locator('.ex-mark-all')).toBeAttached({ timeout: 15_000 });

    // /appearance: a Consumer's Fills, Fonts and Borders, painted as the core's own classes.
    await page.goto('/appearance');
    await expect(page.locator("#appearance-borders .ex-grid [id$='r0c0']")).toHaveText('Hair');

    // /sheet: the Headings, the Formula Bar and its Name Box, the fill handle, completion, and a
    // Formula's Reference Outlines and coloured text (ADR-0050/0051/0057).
    await openSheet(page);
    const sheetGrid = sheet(page);
    await pressSheetCell(sheetGrid, 'F2');
    await expect(sheetGrid.locator('.ex-fill-handle')).toBeAttached();
    await page.keyboard.type('=SU');
    await expect(sheetGrid.locator('.ex-completion')).toBeVisible();
    await page.keyboard.press('Escape');
    await page.keyboard.press('Escape');
    await expect(sheetEditor(sheetGrid)).toHaveCount(0);
    await pressSheetCell(sheetGrid, 'F2');
    await page.keyboard.type('=A1+B2:C3');
    await expect(sheetGrid.locator('.ex-reference-outline')).toHaveCount(2);
    await page.keyboard.press('Escape');
    await expect(sheetEditor(sheetGrid)).toHaveCount(0);

    const worn = new Map(await page.evaluate(() => [...window.__exClassesWorn]));
    // The walk reached what it set out to: a list that stopped exercising a state would pass
    // while checking nothing. What it does not put on — Placeholder rows, the pager, the Size Tip,
    // the change mark, and a loading grid, met only in passing — is named in the README.
    const reached = ['ex-grid', 'ex-header', 'ex-header-cell', 'ex-header-group', 'ex-row', 'ex-cell',
        'ex-cell-numeric', 'ex-pinned', 'ex-state-error', 'ex-state-stale', 'ex-state-missing', 'ex-state-modified',
        'ex-row-group', 'ex-row-total', 'ex-row-stripe', 'ex-tone-negative', 'ex-tone-positive', 'ex-status', 'ex-range', 'ex-focus', 'ex-editor', 'ex-editing', 'ex-action', 'ex-action-chosen',
        'ex-mark', 'ex-mark-all', 'ex-resize-guide', 'ex-drop-indicator', 'ex-row-heading', 'ex-headings-corner',
        'ex-formula-bar', 'ex-name-box', 'ex-fill-handle', 'ex-popover', ...(clipboardWatched ? ['ex-copied-range'] : []),
        'ex-completion', 'ex-reference-outline'];
    expect(reached.filter((name) => !worn.has(name)), 'states the walk set out to reach and did not').toEqual([]);

    // Each kind of name the section lists was met, so none of them is a pattern that matches nothing.
    const met = (test) => [...worn.keys()].some(test);
    expect({
        prefixes: INTERNAL_PREFIXES.filter((prefix) => !met((name) => name.startsWith(prefix) && !NAMED_CLASSES.has(name))),
        families: INTERNAL_FAMILIES.filter((family) => !met((name) => family.test(name))).map(String),
    }, 'internal prefixes and families the walk never met').toEqual({ prefixes: [], families: [] });

    const unnamed = [...worn].filter(([name]) => !named(name)).map(([name, where]) => `${name} (${where})`).sort();
    expect(unnamed, 'classes painted under the root that ADR-0029 names nowhere').toEqual([]);
});

