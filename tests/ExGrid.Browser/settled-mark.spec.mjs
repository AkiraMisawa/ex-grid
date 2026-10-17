import { test, expect, setRoundTrip, settled } from './fixtures.mjs';
import { SERVER } from './hosting.mjs';
import { sheet, openSheet, cell, pressCell, editor, expectFocusAt } from './sheet-helpers.mjs';

// The Settled Mark (ADR-0220): the root carries `data-ex-settled` once every input its key gate has
// taken has been handled and its effect is in the DOM. These tests drive at machine speed and read
// once, straight after the mark, with no expectation that retries: a reading the mark let through
// too early fails here, where elsewhere a retry would hide it.

test.use({ viewport: { width: 1280, height: 1000 } });

test.beforeEach(async ({ page }) => {
    await openSheet(page);
});

// Counts the changes of the root's mark from now on, in the page.
async function countMarkChanges(grid) {
    await grid.evaluate((root) => {
        root.__settledChanges = 0;
        new MutationObserver((records) => {
            root.__settledChanges += records.length;
        }).observe(root, { attributes: true, attributeFilter: ['data-ex-settled'] });
    });
    return () => grid.evaluate((root) => root.__settledChanges);
}

// The Focus cell's id, read once, as it is now.
const focusNow = (grid) => grid.evaluate((root) => root.querySelector('.ex-cell.ex-focus-cell, [aria-selected="true"].ex-focus-cell')?.id
    ?? root.getAttribute('aria-activedescendant') ?? root.querySelector('.ex-key-field')?.getAttribute('aria-activedescendant'));

test('ADR-0220: a key takes the mark off at once, and it comes back once the key\'s move is painted', async ({ page }) => {
    const grid = sheet(page);
    // A round trip longer than the reading after the key takes, so that the key cannot be answered
    // before it on the Server host.
    await setRoundTrip(150);
    await pressCell(grid, 'A2');
    await settled(grid);
    const changes = await countMarkChanges(grid);

    await page.keyboard.press('ArrowDown');
    // Off in the keydown itself: a reading straight after the key never finds the mark of before. On
    // WebAssembly the key is answered, and painted, before the press returns, so the mark is back.
    if (SERVER) {
        // spec-lint: early-read: early on purpose: the test is that the mark is off in the keydown itself, before any answer
        expect(await grid.evaluate((root) => root.hasAttribute('data-ex-settled'))).toBe(false);
    }
    await settled(grid);
    await expectFocusAt(grid, 'A3');
    // Off and back on: once each.
    expect(await changes()).toBe(2);
    await setRoundTrip(0);
});

test('ADR-0220: twenty arrows typed at once, read once after the mark, find the Focus where the last one left it', async ({ page }) => {
    const grid = sheet(page);
    await setRoundTrip(150);
    await pressCell(grid, 'B2');
    await settled(grid);
    const changes = await countMarkChanges(grid);

    for (let i = 0; i < 20; i++) {
        await page.keyboard.press('ArrowDown');
    }
    await settled(grid);
    const active = await grid.evaluate((root) => root.querySelector('.ex-focus')?.getAttribute('style') ?? '');
    const name = await grid.locator('.ex-name-box').inputValue();
    expect(name, 'the Name Box read once, straight after the mark').toBe('B22');
    expect(active).not.toBe('');
    // The mark goes once as the burst starts and once as it ends, with a round trip to hold the
    // keys together; with none, each key may be a burst of its own.
    if (SERVER) {
        expect(await changes()).toBe(2);
    } else {
        expect(await changes()).toBeLessThanOrEqual(40);
    }
    await setRoundTrip(0);
});

test('ADR-0220: after Ctrl+End reveals a far cell, the offset read once after the mark is the reveal\'s, never the repaint\'s pixel', async ({ page }) => {
    const grid = sheet(page);
    await pressCell(grid, 'A1');
    await settled(grid);
    await page.keyboard.press('ControlOrMeta+ArrowDown');
    await page.keyboard.press('ControlOrMeta+ArrowDown');
    await settled(grid);
    const first = await grid.evaluate((root) => root.querySelector(':scope > .ex-scroller').scrollTop);
    await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => requestAnimationFrame(resolve)))));
    const later = await grid.evaluate((root) => root.querySelector(':scope > .ex-scroller').scrollTop);
    expect(first, 'the offset does not move after the mark').toBe(later);
});

test('ADR-0220: an edit opened by a key and committed by Enter is read once after the mark', async ({ page }) => {
    const grid = sheet(page);
    await setRoundTrip(150);
    await pressCell(grid, 'D10');
    await settled(grid);
    await page.keyboard.press('x');
    await settled(grid);
    expect(await editor(grid).count()).toBe(1);
    await page.keyboard.press('Enter');
    await settled(grid);
    expect(await editor(grid).count()).toBe(0);
    expect(await cell(grid, 'D10').textContent()).toBe('x');
    expect(await grid.locator('.ex-name-box').inputValue()).toBe('D11');
    await setRoundTrip(0);
});
