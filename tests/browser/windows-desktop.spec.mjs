import { test, expect, chromeHost } from './fixtures.mjs';
import { sheet, openSheet, cell, pressCell, expectFocusAt, editor } from './sheet-helpers.mjs';
import { expectKeyboardOn } from './keyboard.mjs';
import { decodePng, sameColour } from './pixels.mjs';
import { onWindowsDesktop, osKeys, osKeysLandOn } from './os-input.mjs';

// The rows the Definition of Done once checked by hand on a Windows desktop, run by CI's Windows
// runner instead (ADR-0450): the browser's own keys, and a real browser zoom over a real display
// scale. Each key here goes through Windows' own input queue (os-input.mjs), so the browser meets it
// before the page does, as it meets a person's; a key Playwright presses reaches the page alone, and
// the tests that press those are the ones these stand beside (format-keys.spec.mjs,
// sheet-toolbar.spec.mjs, device-pixel-225.spec.mjs). Every test skips itself off a Windows desktop.

// The OS draws the window; Playwright's viewport would emulate one, and pin the scale at 1.
test.use({ viewport: null });

test.beforeEach(() => {
    test.skip(!onWindowsDesktop, 'Keys through the OS input queue, and a real display scale, need a Windows desktop (ADR-0450)');
});

/** The Focus cell's Font as /sheet reads it back. */
const font = (page) => page.locator('#sheet-focus-font');

/** The Sheet Toolbar's band, and the KeyTips on it. */
const toolbar = (page) => page.locator('.ex-sheet-toolbar');
const keyTips = (page) => toolbar(page).locator('.ex-sheet-keytip');

// The window's tabs, which Ctrl+1 to Ctrl+8 name by place. With the OS drawing the window, each test of
// this file opens pages of its own, and they stay open after it; the window's first tab is the page the
// harness opened for the file, which no test here uses. A test that counts tabs closes the others first,
// and checks the places it counts on. Playwright lists a context's pages in the order it opened them,
// which is the order of the window's tabs: the control below fails if it is not.
async function onlyTabs(context, ...keep) {
    const [harness] = context.pages();
    for (const other of context.pages().filter((p) => p !== harness && !keep.includes(p))) {
        await other.close();
    }
}
const tabOf = (context, page) => context.pages().indexOf(page) + 1;

// The method's own control: keys sent through the OS meet the browser before the page, so a test that
// passes here would fail if the page let the browser have them. On two plain pages that take no key, the
// window's second and third tabs, Ctrl+2 from the third switches to the second, which F24 then reaches;
// and Alt released alone puts the keyboard in the browser's menu, which takes F23 until Escape gives it
// back. Without this, every test below could pass on keys that reached the page alone, as Playwright's do.
test('ADR-0450: keys through the OS meet the browser first: Ctrl+2 on a page that leaves it switches tabs, and Alt left to the browser takes the next key @behaviour', async ({ context }) => {
    await onlyTabs(context);
    const second = await context.newPage();
    await second.setContent('<title>second tab</title><p>second</p>');
    const third = await context.newPage();
    await third.setContent('<title>third tab</title><p>third</p>');
    expect([tabOf(context, second), tabOf(context, third)]).toEqual([2, 3]);

    const switched = await osKeysLandOn(third, second, 'ctrl+2', 'f24');
    expect(switched.from, 'the third tab saw Ctrl+2 and left it to the browser').toEqual(['Control', '2']);
    expect(switched.to, 'F24 went to the tab Ctrl+2 switched to').toEqual(['F24']);

    const menu = await osKeysLandOn(second, second, 'alt', 'f23', 'escape', 'f24');
    expect(menu.to, 'the page saw Alt, never F23, which the browser\'s menu took, then F24 once Escape gave the keyboard back').toEqual(['Alt', 'F24']);
});

// SH-42: Ctrl+1 to Ctrl+5 reach /sheet before the browser's tab switching. Ctrl+N switches to the
// window's Nth tab unless the page takes the key, so the Sheet's tab is never the one a key names: one
// sheet is tab 2 and takes Ctrl+1, the other is tab 6, after three plain tabs, and takes Ctrl+2 to Ctrl+5.
// A key the browser took leaves its tab, and F24 after it goes to the tab it switched to (os-input.mjs).
test('SH-42/ADR-0071/ADR-0450: Ctrl+1 to Ctrl+5 through the OS reach /sheet before the browser switches tabs @behaviour', async ({ page, context }) => {
    await onlyTabs(context, page);
    await openSheet(page);
    for (const n of [3, 4, 5]) {
        const plain = await context.newPage();
        await plain.setContent(`<title>tab ${n}</title><p>tab ${n}</p>`);
    }
    const sixth = await context.newPage();
    await openSheet(sixth);
    expect([tabOf(context, page), tabOf(context, sixth)]).toEqual([2, 6]);

    const grid = sheet(sixth);
    await pressCell(grid, 'A2');
    await expect(font(sixth)).toHaveText('regular');
    for (const [digit, emphasis] of [['2', 'bold'], ['3', 'italic'], ['4', 'underline'], ['5', 'strikethrough']]) {
        await osKeys(sixth, `ctrl+${digit}`);
        await expect(font(sixth), `Ctrl+${digit} sets ${emphasis}`).toHaveText(emphasis);
        await osKeys(sixth, `ctrl+${digit}`);
        await expect(font(sixth), `Ctrl+${digit} again takes ${emphasis} off`).toHaveText('regular');
    }
    await expect(cell(grid, 'A2')).toHaveText('Apples');
    await expectKeyboardOn(grid);

    const other = sheet(page);
    await pressCell(other, 'C2');
    await osKeys(page, 'ctrl+1');
    await expect(other.locator('.ex-popover-consumer'), 'Ctrl+1 opens Format Cells').toBeVisible();
    await osKeys(page, 'escape');
    await expect(other.locator('.ex-popover-consumer')).toHaveCount(0);
    await expectKeyboardOn(other);
});

// SH-51: Ctrl+F1 reaches /sheet, which hides the toolbar the page binds and shows it again.
test('SH-51/ADR-0100/ADR-0450: Ctrl+F1 through the OS hides the toolbar and shows it again @behaviour', async ({ page }) => {
    await openSheet(page);
    const grid = sheet(page);
    await pressCell(grid, 'B2');

    await osKeys(page, 'ctrl+f1');
    await expect(toolbar(page)).toHaveCount(0);
    await expectKeyboardOn(grid);
    await osKeys(page, 'ctrl+f1');
    await expect(toolbar(page)).toBeVisible();
    await expectKeyboardOn(grid);
});

// SH-52: Alt released alone, and F10, show the KeyTips and leave the browser's menu shut. A menu the
// browser opened would take the letters after it, and F24 behind them, so a letter's command run on
// the Sheet says the key was the page's. The letters each row shows are sheet-toolbar.spec.mjs's,
// pressed by Playwright; here they are what the browser let through. Under both Chromes.
for (const chrome of ['builtin', 'mud']) {
    test(`SH-52/ADR-0100/ADR-0450: Alt released alone, and F10, through the OS show the KeyTips and leave the browser's menu shut (${chrome}) ${chromeHost(chrome)}`, async ({ page }) => {
        await openSheet(page, chrome);
        const grid = sheet(page);
        await pressCell(grid, 'A4');

        await osKeys(page, 'alt', 'h', '1');
        await expect(font(page), 'Alt, H, 1 sets bold').toHaveText('bold');
        await expect(keyTips(page)).toHaveCount(0);
        await expectKeyboardOn(grid);

        await osKeys(page, 'f10', 'h', '2');
        await expect(font(page), 'F10, H, 2 sets italic').toHaveText('bold italic');
        await expect(keyTips(page)).toHaveCount(0);
        await expect(cell(grid, 'A4')).toHaveText('Plums');
        await expectKeyboardOn(grid);
        await osKeys(page, 'down');
        await expectFocusAt(grid, 'A5');
    });

    test(`SH-52/ADR-0100/ADR-0450: while an edit is open, Alt and F10 through the OS start no KeyTips, and the edit keeps the keyboard (${chrome}) ${chromeHost(chrome)}`, async ({ page }) => {
        await openSheet(page, chrome);
        const grid = sheet(page);
        await pressCell(grid, 'A4');
        await osKeys(page, 'f2');
        await expect(editor(grid)).toBeFocused();

        const seen = await osKeys(page, 'alt', 'f10');
        expect(seen, 'the page saw Alt and F10').toEqual(['Alt', 'F10']);
        await expect(editor(grid)).toBeFocused();
        await expect(keyTips(page)).toHaveCount(0);
        await osKeys(page, 'escape');
        await expect(editor(grid)).toHaveCount(0);
        await expect(cell(grid, 'A4')).toHaveText('Plums');
        await expectKeyboardOn(grid);
    });
}

// VZ-16: a real browser zoom is told to the grid as it changes, and at a real 2.25 — the display at
// 150%, the browser's zoom at 150% — a thin line is one Device Pixel and a gridline two. The zoom keys
// are pressed on /wide, which only displays: the Sheet claims Ctrl+- and Ctrl+= for its structure
// keys. Chrome's and Edge's zoom is the origin's, so /sheet?case=11 opens at it. The picture is the
// compositor's (Page.captureScreenshot): Playwright's own screenshot places its clip by a scale it
// emulates, which a real zoom is not.

/** The grid's Device Pixel as its root's inline token says it, and the page's devicePixelRatio. */
function toldScale(grid) {
    return grid.evaluate((root) => {
        const style = root.getAttribute('style');
        if (style === null) {
            throw new Error('The grid root has no inline style, where its Grid Metrics are emitted (ADR-0028)');
        }
        const told = /--ex-dp:\s*([0-9.]+)/.exec(style);
        return { dpr: devicePixelRatio, dp: told ? Number(told[1]) : null };
    });
}

/**
 * The device pixels across an edge at `at` CSS px, along `along` (an x for axis 'x', a y for 'y'), from
 * the compositor's picture. Its clip is in the window's own units, before the browser's zoom, and from
 * the document's origin: a CSS px is `zoom` of them, and the page's scroll is added
 * (verification/2026-10-25-windows-19, part G). The clip's origin is a multiple of 4 CSS px, a whole
 * 9 Device Pixels at 2.25, as device-pixel-225.spec.mjs reads it.
 */
async function runAcross(page, client, { at, along, axis, zoom, scroll }) {
    const start = Math.floor(at / 4) * 4 - 8;
    const line = Math.floor(along / 4) * 4;
    const css = axis === 'x' ? { x: start, y: line, width: 16, height: 4 } : { x: line, y: start, width: 4, height: 16 };
    const clip = {
        x: (css.x + scroll.x) * zoom,
        y: (css.y + scroll.y) * zoom,
        width: css.width * zoom,
        height: css.height * zoom,
        scale: 1,
    };
    const { data } = await client.send('Page.captureScreenshot', { format: 'png', clip });
    const image = decodePng(Buffer.from(data, 'base64'));
    const pixels = [];
    if (axis === 'x') {
        for (let x = 0; x < image.width; x++) pixels.push(image.at(x, Math.floor(image.height / 2)));
    } else {
        for (let y = 0; y < image.height; y++) pixels.push(image.at(Math.floor(image.width / 2), y));
    }
    return pixels.filter((pixel) => !sameColour(pixel, [255, 255, 255], 2));
}

test('VZ-16/ADR-0090/ADR-0450: a real browser zoom is told to the grid as it changes, and at a real 2.25 a thin line is one Device Pixel and a gridline two @behaviour', async ({ page }) => {
    await page.goto('/wide');
    const wide = page.locator('.ex-grid').first();
    await expect(wide.locator('.ex-row').first()).toBeVisible();
    const display = await page.evaluate(() => devicePixelRatio);
    test.skip(display !== 1.5, `VZ-16 is read on a display at 150%, and this one is at ${display * 100}%`);
    await expect.poll(() => toldScale(wide)).toEqual({ dpr: 1.5, dp: 1 / 1.5 });

    // The browser's zoom steps, 110%, 125% and 150%, each told to the grid as it is taken.
    for (const zoom of [1.1, 1.25, 1.5]) {
        await osKeys(page, 'ctrl+plus');
        await expect.poll(async () => {
            const { dpr, dp } = await toldScale(wide);
            return Math.abs(dpr - 1.5 * zoom) < 0.001 && Math.abs(dp - 1 / dpr) < 1e-6;
        }, `at a zoom of ${zoom * 100}% the page is at ${1.5 * zoom}, and the grid is told 1/${1.5 * zoom}`).toBe(true);
    }

    await page.goto('/sheet?case=11');
    const grid = sheet(page);
    await expect(grid.locator('.ex-row').first()).toBeVisible();
    await expect.poll(() => toldScale(grid)).toEqual({ dpr: 2.25, dp: 1 / 2.25 });
    await cell(grid, 'E8').scrollIntoViewIfNeeded();
    await page.mouse.move(0, 0);
    await expect(cell(grid, 'E8')).toBeInViewport();

    const client = await page.context().newCDPSession(page);
    const boxes = await grid.evaluate((root) => {
        const box = (address) => {
            const [, column, row] = /^([A-Z])(\d+)$/.exec(address);
            const id = `-r${Number(row) - 1}c${column.charCodeAt(0) - 65}`;
            const found = [...root.querySelectorAll(`[id$="${id}"]`)].find((e) => e.closest('.ex-viewport'));
            const { left, top, width, height } = found.getBoundingClientRect();
            return { x: left, y: top, width, height };
        };
        return { b2: box('B2'), e8: box('E8'), scroll: { x: scrollX, y: scrollY } };
    });
    const read = (at, along, axis) => runAcross(page, client, { at, along, axis, zoom: 1.5, scroll: boxes.scroll });
    const { b2, e8 } = boxes;

    // B2's thin Border: one Device Pixel of black, beside what is left of the gridline under it.
    for (const [name, run] of [
        ['right', await read(b2.x + b2.width, b2.y + b2.height / 2, 'x')],
        ['bottom', await read(b2.y + b2.height, b2.x + b2.width / 2, 'y')],
    ]) {
        const black = run.filter((pixel) => sameColour(pixel, [0, 0, 0], 2));
        expect(black.length, `B2's thin ${name} Border: ${JSON.stringify(run)}`).toBe(1);
        for (const pixel of run.filter((p) => !black.includes(p))) {
            expect(sameColour(pixel, [0xe0, 0xe0, 0xe0], 2), `B2's thin ${name} Border is not blended: ${JSON.stringify(run)}`).toBe(true);
        }
    }

    // E8's gridlines: two whole Device Pixels of #E0E0E0, as at 200%, not 2.25 blended over three.
    for (const [name, run] of [
        ['right', await read(e8.x + e8.width, e8.y + e8.height / 2, 'x')],
        ['bottom', await read(e8.y + e8.height, e8.x + e8.width / 2, 'y')],
    ]) {
        expect(run.length, `E8's ${name} gridline: ${JSON.stringify(run)}`).toBe(2);
        for (const pixel of run) expect(sameColour(pixel, [0xe0, 0xe0, 0xe0], 2), `E8's ${name} gridline: ${JSON.stringify(run)}`).toBe(true);
    }
});
