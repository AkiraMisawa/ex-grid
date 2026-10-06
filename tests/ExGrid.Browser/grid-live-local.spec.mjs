import { test, expect, circuitQuiet } from './fixtures.mjs';
import { expectCodeIsSource } from './demo-code.mjs';

// /grid-live-local (ADR-0141, D8): the in-process path of ExGrid's live data. The page makes a
// million trades, binds them through GridSource.From with the trade's id as the Row Key, and its own
// timer hands the source a Change Batch of amended trades four times a second. What only a browser
// can say: that the page loads on both hosts with a million rows, that a batch reaches the screen
// and marks the cells whose text changed (LV-9), that pausing stops the batches and the marks go,
// that under a sort the painted rows stay in order as the values move (LV-5/LV-7), and that the
// console stays clean throughout (CON-*). LV-15's timing is measure-live.spec.mjs's, and never gates.

test.use({ viewport: { width: 1400, height: 1100 } });

const grid = (page) => page.locator('.ex-grid');
const marked = (page) => grid(page).locator('.ex-viewport .ex-cell.ex-changed');
// The Trade ID cell of the row at a position: column 0, pinned.
const tradeAt = (page, row) => grid(page).locator(`[id$='-r${row}c0']`);
const toggle = (page) => page.locator('#grid-live-local-toggle');
const made = (page) => page.locator('#grid-live-local-made');

/** The number of the last Change Batch the page applied, as its status line says it. */
async function batches(page) {
    const text = await page.locator('#grid-live-local-status').textContent();
    return Number(/^Batch ([\d,]+):/.exec(text)?.[1]?.replace(/,/g, '') ?? 0);
}

async function open(page, query, rows) {
    await page.goto(`/grid-live-local${query}`);
    // Making a million trades takes seconds in the browser on WebAssembly without AOT.
    await expect(made(page)).toContainText(`${rows.toLocaleString('en-US')} trades made in`, { timeout: 120_000 });
    await expect(tradeAt(page, 0)).toBeVisible({ timeout: 60_000 });
    await expect(toggle(page)).toBeEnabled();
}

test('ADR-0141/D8, LV-9: a million trades in this process; a Change Batch reaches the screen and marks what changed, and a pause stops it', async ({ page }) => {
    test.setTimeout(240_000);
    await open(page, '', 1_000_000);
    expect(Number(await grid(page).getAttribute('aria-rowcount'))).toBe(1_000_000);

    // The page's timer applies a batch four times a second, half of it on the trades in view.
    const from = await batches(page);
    await expect.poll(() => batches(page), { timeout: 30_000 }).toBeGreaterThan(from + 2);
    await expect(page.locator('#grid-live-local-status')).toContainText('20 trades amended, applied in');
    await expect.poll(() => marked(page).count(), { message: 'a mark in view', timeout: 60_000 }).toBeGreaterThan(0);
    // A mark is on a value cell: an amended P&L or notional.
    const text = await marked(page).first().textContent();
    expect(text?.trim()).toMatch(/^-?[\d,]+\.\d\d$/);

    // Paused, the page applies nothing more, and the marks go when their second is up.
    await toggle(page).click();
    await expect(toggle(page)).toHaveText("Resume the page's changes");
    const paused = await batches(page);
    await expect(marked(page)).toHaveCount(0, { timeout: 15_000 });
    await circuitQuiet();
    expect(await batches(page)).toBeLessThanOrEqual(paused + 1);

    // Resumed, the batches mark values again.
    await toggle(page).click();
    await expect(toggle(page)).toHaveText("Pause the page's changes");
    await expect.poll(() => marked(page).count(), { message: 'a mark in view again', timeout: 60_000 }).toBeGreaterThan(0);
});

test('ADR-0141 LV-5/LV-7: under a sort by P&L, the painted rows stay in order as batches move the values', async ({ page }) => {
    test.setTimeout(120_000);
    // 2,000 trades and a thousand amended a batch, so every batch moves rows on screen.
    await open(page, '?rows=2000&batch=1000', 2_000);
    const header = grid(page).locator('.ex-header-cell').nth(8); // P&L
    await header.click();
    await expect(header).toHaveAttribute('aria-sort', 'ascending');

    const from = await batches(page);
    await expect.poll(() => batches(page), { timeout: 30_000 }).toBeGreaterThan(from + 3);
    await toggle(page).click();
    await expect(toggle(page)).toHaveText("Resume the page's changes");
    await circuitQuiet();

    // Read in one go, from one painted frame: every painted P&L, top to bottom.
    const painted = await grid(page).evaluate((root) => [...root.querySelectorAll('.ex-viewport .ex-row')]
        .map((row) => row.querySelector('[id$="c8"]'))
        .filter((cell) => cell !== null)
        .map((cell) => ({ row: Number(/-r(\d+)c8$/.exec(cell.id)[1]), value: Number(cell.textContent.trim().replace(/,/g, '')) }))
        .sort((a, b) => a.row - b.row)
        .map((cell) => cell.value));
    expect(painted.length).toBeGreaterThan(10);
    for (let i = 1; i < painted.length; i++) {
        expect(painted[i], `row ${i} of ${JSON.stringify(painted)}`).toBeGreaterThanOrEqual(painted[i - 1]);
    }
});

test('ADR-0141/0069: the code the page shows is the code it runs: GridSource.From by Row Key, a Change Batch, and CellChangedAt', async ({ page }) => {
    await open(page, '?rows=2000', 2_000);
    const code = await expectCodeIsSource(page);
    expect(code['GridLiveLocalPage.razor#trades']).toContain('GridSource.From(trades, t => t.Id)');
    expect(code['GridLiveLocalPage.razor#trades']).toContain('_cellChangedAt = _source.CellChangedAt');
    expect(code['GridLiveLocalPage.razor#batch']).toContain('source.Apply(new GridChangeBatch<DemoPivotTrade>(changed: amended))');
    expect(code['GridLiveLocalPage.razor#grid']).toContain('CellChangedAt="_cellChangedAt" ChangeHighlightDuration="Highlight"');
});
