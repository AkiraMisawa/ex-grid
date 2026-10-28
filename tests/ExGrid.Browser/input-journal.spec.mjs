import { test, expect, setRoundTrip, settled } from './fixtures.mjs';
import { sheet, openSheet, pressCell } from './sheet-helpers.mjs';

// The Input Journal (ADR-0240 section 10): each grid keeps, on its root, a record of what its script
// numbered, held and called the core with, and what the stamp answered, which a failed test attaches.
// These tests read it from a grid that passed, to show it says what happened and nothing more.

test.use({ viewport: { width: 1280, height: 1000 } });

// The journal is the Razor grid's: ExSheet over the Script Core keeps none, so the file reads the Razor
// component, which still ships (ADR-0370 section 6, step 2).
test.beforeEach(async ({ page }) => {
    await openSheet(page, 'builtin', 'razor');
});

// The journal of the grid's root, read once, as it is now.
const journalOf = (grid) => grid.evaluate((root) => root[Symbol.for('ex-grid.input-journal')]());

// How many entries the journal has taken so far, the ones its ring has let go included.
const taken = (journal) => journal.dropped + journal.entries.length;

// The entries taken since `from` were taken.
const since = (journal, from) => journal.entries.slice(from - journal.dropped);

test('ADR-0240 §10: twenty arrows typed at once are journaled as twenty numbered keys, each answered', async ({ page }) => {
    const grid = sheet(page);
    // A round trip, so that the keys are numbered and sent before the first is answered on the Server host.
    await setRoundTrip(150);
    await pressCell(grid, 'A2');
    await settled(grid);
    const before = taken(await journalOf(grid));

    for (let i = 0; i < 20; i++) {
        await page.keyboard.press('ArrowDown');
    }
    await settled(grid);

    const entries = since(await journalOf(grid), before);
    const keys = entries.filter((e) => e.what === 'call' && e.method === 'OnKeyAsync' && e.args[0] === 'ArrowDown');
    expect(keys).toHaveLength(20);
    // The field reading follows the input number (ADR-0240 section 5).
    const numbers = keys.map((e) => e.args.at(-2));
    expect(numbers.every((number) => Number.isSafeInteger(number) && number > 0)).toBe(true);
    expect(numbers).toEqual([...numbers].sort((a, b) => a - b));
    expect(entries.filter((e) => e.what === 'numbered').map((e) => e.input)).toEqual(expect.arrayContaining(numbers));
    // Every call was replied to, none failed, and the stamp answered the last number.
    expect(entries.filter((e) => e.what === 'failed')).toEqual([]);
    expect(entries.filter((e) => e.what === 'replied').map((e) => e.call))
        .toEqual(expect.arrayContaining(keys.map((e) => e.call)));
    expect(entries.filter((e) => e.what === 'answered').at(-1).input).toBe(numbers.at(-1));
    expect(entries.at(-1).what).toBe('settled');
});

test('ADR-0240 §10: a pointer moved across the rows adds nothing to the journal', async ({ page }) => {
    const grid = sheet(page);
    await pressCell(grid, 'A2');
    await settled(grid);
    const box = await grid.locator('.ex-viewport').boundingBox();
    const before = taken(await journalOf(grid));

    await page.mouse.move(box.x + 40, box.y + 30);
    await page.mouse.move(box.x + 40, box.y + box.height - 30, { steps: 30 });
    await settled(grid);

    const added = since(await journalOf(grid), before);
    expect(added.filter((e) => e.what === 'call')).toEqual([]);
});
