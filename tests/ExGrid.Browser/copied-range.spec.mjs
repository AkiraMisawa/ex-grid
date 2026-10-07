import { test, expect } from './fixtures.mjs';
import { painted, sameColour } from './pixels.mjs';

// The Copied Range (ADR-0170): after a copy lands, the grid outlines what it copied with dashes
// that stand still, for as long as the clipboard still holds it. What only a browser can show: the
// dashes as painted over the Selection's own outline, the real clipboardchange telling the grid
// that something else wrote the clipboard, and a browser without that event drawing nothing.
// Layer 2 (CopiedRangeTests) pins every end the grid sees for itself.

test.beforeEach(async ({ context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
});

async function open(page, query = '') {
    await page.goto(`/features${query}`);
    await expect(page.locator('.ex-grid').first().locator('.ex-row').first()).toBeVisible();
}

const grid = (page, n = 0) => page.locator('.ex-grid').nth(n);
const outlines = (page, n = 0) => grid(page, n).locator('.ex-copied-range');

async function clickCell(page, row, column, n = 0) {
    // Cells are pointer-events: none; the Viewport takes the press (ADR-0004).
    await grid(page, n).locator(`[id$='r${row}c${column}']`).click({ force: true });
}

/** A write by the page's own script, standing for anyone but the grid — another application, a
 * text field — awaited until the clipboard has told the page of it, so its event cannot arrive
 * after a copy the test makes next. The one-shot listener leaves nothing behind. */
async function writeElsewhere(page, text) {
    await page.evaluate((written) => new Promise((resolve) => {
        const timer = setTimeout(resolve, 2000);
        navigator.clipboard.addEventListener('clipboardchange', () => {
            clearTimeout(timer);
            resolve();
        }, { once: true });
        navigator.clipboard.writeText(written);
    }), text);
}

test('a copy is outlined with dashes that stand still and read over the Selection\'s outline (ADR-0170, CP-26, UX-6)', async ({ page }) => {
    await open(page);
    await writeElsewhere(page, 'SENTINEL');
    await clickCell(page, 1, 1);
    await page.keyboard.press('Shift+ArrowRight');
    await expect(outlines(page)).toHaveCount(0);

    // The Selection's outline alone: one solid line along the top edge.
    const range = grid(page).locator('.ex-range-single');
    await expect(range).toHaveCount(1);
    const box = await range.boundingBox();
    const topEdge = async () => {
        const region = await painted(page, { x: box.x - 4, y: box.y - 4, width: box.width + 8, height: 8 });
        return region.across(box.y - 1, box.x + 6, box.x + box.width - 6);
    };
    const ink = [0, 0, 0];
    const ground = [255, 255, 255];
    const solid = await topEdge();
    expect(solid.filter((p) => sameColour(p, ground, 40)).length, 'the outline alone is solid').toBe(0);

    await page.keyboard.press('ControlOrMeta+C');

    // On WebAssembly the event route landed it at once; on the Server host every copy takes the
    // asynchronous route, and lands a round trip later (ADR-0005).
    await expect(outlines(page)).toHaveCount(1);
    await expect.poll(() => page.evaluate(() => navigator.clipboard.readText())).not.toBe('SENTINEL');

    const dashes = await outlines(page).first().evaluate((el) => {
        const s = getComputedStyle(el, '::after');
        return { style: s.outlineStyle, animation: s.animationName, transition: s.transitionDuration };
    });
    expect(dashes).toEqual({ style: 'dashed', animation: 'none', transition: '0s' });
    // Painted over the Selection's outline, the line now breaks: ink and ground both, in runs.
    const broken = await topEdge();
    const inked = broken.filter((p) => sameColour(p, ink, 60)).length;
    const gaps = broken.filter((p) => sameColour(p, ground, 40)).length;
    expect(inked, JSON.stringify(broken.slice(0, 24))).toBeGreaterThan(broken.length / 5);
    expect(gaps, JSON.stringify(broken.slice(0, 24))).toBeGreaterThan(broken.length / 5);
});

test('a write by anything but the grid\'s own copy drops the outline: another grid\'s copy, a script\'s write (ADR-0170, CP-27)', async ({ page }) => {
    await open(page);
    await clickCell(page, 0, 1);
    await page.keyboard.press('ControlOrMeta+C');
    await expect(outlines(page, 0)).toHaveCount(1);

    // The second instance copies: the clipboard is its now, and the first grid's outline goes.
    await clickCell(page, 0, 0, 1);
    await page.keyboard.press('ControlOrMeta+C');
    await expect(outlines(page, 1)).toHaveCount(1);
    await expect(outlines(page, 0)).toHaveCount(0);

    // A write that is no grid's — a text field's, another application's.
    await writeElsewhere(page, 'elsewhere');
    await expect(outlines(page, 1)).toHaveCount(0);
});

test('Escape takes the outline away and the grid keeps the keyboard; a paste leaves it (ADR-0170, ADR-0012, CP-28, CP-31)', async ({ page }) => {
    await open(page);
    await clickCell(page, 0, 1);
    await page.keyboard.press('ControlOrMeta+C');
    await expect(outlines(page)).toHaveCount(1);

    // A paste elsewhere in the grid: the clipboard still holds the copy.
    await clickCell(page, 3, 1);
    await page.keyboard.press('ControlOrMeta+V');
    await expect(page.locator('#paste-status')).toContainText('1 cells from 1x1');
    await expect(outlines(page)).toHaveCount(1);

    await page.keyboard.press('Escape');

    await expect(outlines(page)).toHaveCount(0);
    expect(await grid(page).evaluate((root) => root.contains(document.activeElement))).toBe(true);
});

test('a copied cell whose value moves upstream loses the outline; a row replaced with the same copied text keeps it (ADR-0170, ADR-0142, CP-30)', async ({ page }) => {
    await open(page, '?upstream=1');
    await expect(page.locator('#upstream-status')).toBeVisible();

    // Trader of the first row: F9 replaces the row with a new instance, Trader unchanged.
    await clickCell(page, 0, 1);
    await page.keyboard.press('ControlOrMeta+C');
    await expect(outlines(page)).toHaveCount(1);
    await page.keyboard.press('F9');
    await expect(page.locator('#upstream-status')).toContainText('×1');
    await expect(outlines(page)).toHaveCount(1);

    // Notional of the first row: F9 moves it up by one, and the clipboard holds the old value.
    await clickCell(page, 0, 2);
    await page.keyboard.press('ControlOrMeta+C');
    await expect(outlines(page)).toHaveCount(1);
    await page.keyboard.press('F9');
    await expect(page.locator('#upstream-status')).toContainText('×2');

    await expect(outlines(page)).toHaveCount(0);
});

test.describe('a browser that cannot say the clipboard changed', () => {
    test.use({ freshDocument: true });

    test('draws no outline, and the copy still lands (ADR-0170, CP-32)', async ({ page, context }) => {
        await context.grantPermissions(['clipboard-read', 'clipboard-write']);
        // The event's interface removed before the grid attaches, as a browser before 143 has none.
        await page.addInitScript(() => {
            delete globalThis.ClipboardChangeEvent;
        });
        await open(page);
        await page.evaluate(() => navigator.clipboard.writeText('SENTINEL'));
        await clickCell(page, 0, 1);

        await page.keyboard.press('ControlOrMeta+C');

        await expect.poll(() => page.evaluate(() => navigator.clipboard.readText())).not.toBe('SENTINEL');
        await page.waitForTimeout(300);
        await expect(outlines(page)).toHaveCount(0);
    });
});

test('on a Sheet the dashes are its outline\'s colour, with and without a Wrapper (ADR-0170, ADR-0071)', async ({ page }) => {
    for (const query of ['', '?chrome=mud']) {
        await page.goto(`/sheet${query}`);
        const sheet = page.locator('.ex-sheet .ex-grid').first();
        await expect(sheet.locator('.ex-row').first()).toBeVisible();
        await sheet.locator("[id$='r1c1']").click({ force: true });
        await page.keyboard.press('Shift+ArrowRight');
        await page.keyboard.press('ControlOrMeta+C');
        await expect(sheet.locator('.ex-copied-range')).not.toHaveCount(0);

        const colours = await sheet.evaluate((root) => ({
            outline: getComputedStyle(root.querySelector('.ex-range-single'), '::after').borderTopColor,
            dashes: getComputedStyle(root.querySelector('.ex-copied-range'), '::after').outlineColor,
        }));
        expect(colours.dashes, `${query || 'no Wrapper'}: ${JSON.stringify(colours)}`).toBe(colours.outline);
        await page.keyboard.press('Escape');
    }
});
