import { test, expect, circuitQuiet, setRoundTrip } from './fixtures.mjs';
import { expectKeyboardOn, activeDescendant, expectTabStopTaken } from './keyboard.mjs';

// The page follows what a move reveals, and a scripted focus asks whether the keyboard is still here
// (ADR-0204, KB-45; SRV-5's popover and Template cases).
//
// /cells holds a grid 420px tall, of 28px rows (?editable=1 makes Book editable, so the grid holds the
// keyboard in its Keyboard Field, ADR-0080; without it the grid is display-only and its root holds the
// keyboard). In a window 360px tall the grid runs past the fold, and a move below it is followed by the
// page, to the nearest edge; a cell already in the window moves nothing, nor does a resize. Opening the
// editor follows the same rule. ?second=1 adds a second grid below, which a press can give the keyboard
// to while a Note's request for it is on its way. /features holds two grids, for a column menu's
// opening focus on its way while the other grid is pressed. /marks holds a million rows in a grid
// 600px tall, for an extension that carries the grid's own Viewport past the Focus.
//
// On the Server host the round trip is set to 150 ms for the scripted-focus cases; on WebAssembly
// there is no round trip, and the same test is the case without one.

const WINDOW = { width: 1280, height: 360 };

/** Waits until the browser has done what the grid asked of it: every round trip landed, then the
 * frames a reveal's repaint takes and the task after the paint in which the page follow runs. */
async function settled(page) {
    await circuitQuiet();
    await page.evaluate(() => new Promise((resolve) => {
        let frames = 0;
        const tick = () => {
            frames += 1;
            if (frames >= 4) {
                setTimeout(resolve, 0);
            } else {
                requestAnimationFrame(tick);
            }
        };
        requestAnimationFrame(tick);
    }));
}

const pageTop = (page) => page.evaluate(() => window.scrollY);

/** Waits until the element stands whole in the window, to the pixel: the page scrolls by whole
 * device pixels, and a box laid out at a fraction of one can be left that fraction outside. */
async function expectInWindow(locator) {
    await expect.poll(() => locator.evaluate((element) => {
        const box = element.getBoundingClientRect();
        return box.top >= -1 && box.bottom <= window.innerHeight + 1;
    })).toBe(true);
}

/** The cell's box against the window: its top and bottom, in CSS pixels. */
const inWindow = (locator) => locator.evaluate((element) => {
    const box = element.getBoundingClientRect();
    return { top: box.top, bottom: box.bottom, height: window.innerHeight };
});

/** /cells in a window shorter than its grid, the grid's top at the window's top. */
async function openTallGrid(page, query = '?editable=1') {
    await page.setViewportSize(WINDOW);
    await page.goto(`/cells${query}`);
    const grid = page.locator('.ex-grid').first();
    await expect(grid.locator('.ex-row').first()).toBeVisible();
    await expectTabStopTaken(grid);
    await grid.evaluate((root) => root.scrollIntoView({ block: 'start' }));
    await grid.locator("[id$='-r0c0']").click({ force: true });
    await expectKeyboardOn(grid);
    await expect.poll(() => activeDescendant(grid)).toMatch(/-r0c0$/);
    return grid;
}

async function arrowTo(page, grid, key, times, row) {
    for (let i = 0; i < times; i++) {
        await page.keyboard.press(key);
    }
    await expect.poll(() => activeDescendant(grid)).toMatch(new RegExp(`-r${row}c0$`));
}

for (const [kind, query] of [['a grid that edits, its Keyboard Field', '?editable=1'], ['a display-only grid, its root', '']]) {
    test(`KB-45: on ${kind}, the arrows bring the Focus into the window at the nearest edge, and a cell in the window moves nothing (ADR-0204)`, async ({ page }) => {
        const grid = await openTallGrid(page, query);
        const start = await pageTop(page);
        const twelve = grid.locator("[id$='-r12c0']");
        expect((await inWindow(twelve)).top, 'row 12 starts below the fold').toBeGreaterThan(WINDOW.height);

        await arrowTo(page, grid, 'ArrowDown', 12, 12);

        // Into the window, at its bottom edge: the nearest one, not the middle.
        await expectInWindow(twelve);
        await settled(page);
        const followed = await inWindow(twelve);
        expect(Math.abs(followed.bottom - followed.height), JSON.stringify(followed)).toBeLessThanOrEqual(1);
        const moved = await pageTop(page);
        expect(moved).toBeGreaterThan(start);

        // Row 11 is in the window: the page stays where it is.
        await arrowTo(page, grid, 'ArrowUp', 1, 11);
        await settled(page);
        expect(await pageTop(page)).toBe(moved);
    });
}

test('KB-45: while extending, the page follows the Extent, and the Focus may be left behind (ADR-0204, ADR-0052)', async ({ page }) => {
    const grid = await openTallGrid(page);

    for (let i = 0; i < 12; i++) {
        await page.keyboard.press('Shift+ArrowDown');
    }
    // The Focus stays on row 0; the range's far end is row 12, which the page brings in.
    const extent = grid.locator("[id$='-r12c0']");
    await expectInWindow(extent);
    await expect.poll(() => activeDescendant(grid)).toMatch(/-r0c0$/);
});

test('KB-45: extending past the grid\'s own Viewport still brings the Extent into the window, the Focus left off the painted rows (ADR-0204, ADR-0052)', async ({ page }) => {
    // /marks holds 1,000,000 rows in a grid 600px tall, its top at the window's top and its lower
    // part below the fold. Shift+PageDown carries the Extent, and the grid's own Viewport with it, a
    // page down: the Focus on row 0 leaves the painted rows, and the Extent stands at the grid's top,
    // in the window, so nothing has been followed yet. From there each Shift+↓ walks the Extent
    // down the grid, past the fold, and the page follows it there.
    await page.setViewportSize(WINDOW);
    await page.goto('/marks');
    const grid = page.locator('.ex-grid').first();
    const first = grid.locator("[id$='-r0c1']");
    await expect(first).toBeVisible();
    await expectTabStopTaken(grid);
    await grid.evaluate((root) => root.scrollIntoView({ block: 'start' }));
    await first.click({ force: true });
    await expectKeyboardOn(grid);
    await expect.poll(() => activeDescendant(grid)).toMatch(/-r0c1$/);
    const start = await pageTop(page);

    await page.keyboard.press('Shift+PageDown');
    await expect(first).toHaveCount(0);
    // Where the page down put the Extent, read from what the grid says is selected.
    const region = grid.locator('.ex-announce');
    await expect(region).toHaveText(/ rows by 1 columns selected, Index 1 to Index \d+$/);
    const down = Number((await region.textContent()).match(/to Index (\d+)$/)[1]) - 1;
    const target = grid.locator(`[id$='-r${down + 12}c1']`);
    await expect(target).not.toBeInViewport();
    expect(await pageTop(page)).toBe(start);

    for (let i = 0; i < 12; i++) {
        await page.keyboard.press('Shift+ArrowDown');
    }

    await expectInWindow(target);
    expect(await pageTop(page)).toBeGreaterThan(start);
    await expect(first).toHaveCount(0);
    await expectKeyboardOn(grid);
});

test('KB-45: a resize is not followed — the page stays where it is when the window shrinks over the Focus (ADR-0204)', async ({ page }) => {
    const grid = await openTallGrid(page);
    await arrowTo(page, grid, 'ArrowDown', 11, 11);
    const eleven = grid.locator("[id$='-r11c0']");
    await expectInWindow(eleven);
    await settled(page);
    const before = await pageTop(page);

    await page.setViewportSize({ width: WINDOW.width, height: 240 });
    await settled(page);

    expect(await pageTop(page)).toBe(before);
    await expect(eleven).not.toBeInViewport();
    await expectKeyboardOn(grid);
});

test('KB-45: the editor opens in the window, at the nearest edge, on a cell scrolled out of it (ADR-0204)', async ({ page }) => {
    const grid = await openTallGrid(page);
    // The page scrolled on past the Focus cell, which now stands above the window. The page's scroll
    // is the harness's to put back.
    await page.evaluate(() => window.scrollBy(0, 200));
    const first = grid.locator("[id$='-r0c0']");
    await expect(first).not.toBeInViewport();
    await expectKeyboardOn(grid);

    await page.keyboard.type('x');

    const editor = grid.locator('input.ex-editor');
    await expect(editor).toHaveValue('x');
    await expectInWindow(editor);
    await settled(page);
    const opened = await inWindow(editor);
    expect(Math.abs(opened.top), JSON.stringify(opened)).toBeLessThanOrEqual(1);
    await page.keyboard.press('Escape');
    await expect(editor).toHaveCount(0);
});

// A focus the grid takes a round trip after a gesture is not taken once the keyboard has moved to
// another grid meanwhile (ADR-0204, SRV-5): the column menu's opening focus under either Chrome, and
// a Template cell's control taking the keyboard through HandKeyboardToAsync.

for (const chrome of ['builtin', 'mud']) {
    test(`SRV-5/KB-45: a column menu's opening focus on its way does not take the keyboard from a grid pressed meanwhile (ADR-0204, ${chrome} Chrome)`, async ({ page }) => {
        await page.goto(chrome === 'mud' ? '/features?chrome=mud' : '/features');
        const first = page.locator('.ex-grid').first();
        const second = page.locator('.ex-grid').nth(1);
        await expectTabStopTaken(first);
        await expectTabStopTaken(second);
        await first.locator("[id$='-r0c1']").click({ force: true });
        await expectKeyboardOn(first);
        await setRoundTrip(150);

        // Alt+↓ opens the menu, and its opening focus is a round trip on its way; the other grid is
        // pressed straight after.
        await page.keyboard.press('Alt+ArrowDown');
        await second.locator("[id$='-r1c0']").click({ force: true });

        await expect(first.locator('.ex-popover').first()).toBeVisible();
        await circuitQuiet();
        await expectKeyboardOn(second);
        // The next key is the second grid's.
        await page.keyboard.press('ArrowDown');
        await expect.poll(() => activeDescendant(second)).toMatch(/-r2c0$/);
        await expectKeyboardOn(second);
        await setRoundTrip(0);
    });
}

test('SRV-5/KB-45: a Template cell\'s request for the keyboard on its way does not take it from a grid pressed meanwhile (ADR-0204)', async ({ page }) => {
    await page.goto('/cells?second=1');
    const cells = page.locator('.ex-grid').first();
    const second = page.locator('#second-grid .ex-grid');
    await expectTabStopTaken(cells);
    await expectTabStopTaken(second);
    await cells.locator("[id$='-r0c0']").click({ force: true });
    for (let i = 0; i < 4; i++) {
        await page.keyboard.press('ArrowRight');
    }
    await expect.poll(() => activeDescendant(cells)).toMatch(/-r0c4$/);
    await setRoundTrip(150);

    // Space asks the Note's field to take the keyboard, a round trip and more away; the other grid
    // is pressed straight after.
    await page.keyboard.press(' ');
    await second.locator("[id$='-r1c0']").click({ force: true });

    await circuitQuiet();
    await expectKeyboardOn(second);
    await expect(cells.locator("[id$='-r0c4'] input.demo-note")).not.toBeFocused();
    await page.keyboard.press('ArrowDown');
    await expect.poll(() => activeDescendant(second)).toMatch(/-r2c0$/);
    await setRoundTrip(0);
});

test('KB-23/ADR-0204: with nothing pressed meanwhile, Space still gives a Template cell\'s control the keyboard, through the grid', async ({ page }) => {
    await page.goto('/cells?second=1');
    const cells = page.locator('.ex-grid').first();
    await expectTabStopTaken(cells);
    await cells.locator("[id$='-r0c0']").click({ force: true });
    for (let i = 0; i < 4; i++) {
        await page.keyboard.press('ArrowRight');
    }
    await expect.poll(() => activeDescendant(cells)).toMatch(/-r0c4$/);
    await setRoundTrip(150);

    await page.keyboard.press(' ');

    const note = cells.locator("[id$='-r0c4'] input.demo-note");
    await expect(note).toBeFocused();
    await setRoundTrip(0);
    await page.keyboard.type('memo');
    await expect(note).toHaveValue('memo');
    await page.keyboard.press('Escape');
    await expectKeyboardOn(cells);
});
