import { test, expect, circuitQuiet } from './fixtures.mjs';

// The selection bridge on /chart-bridge (ticket 20, ADR-0258): a curve history of eight dates, its
// columns Date, Note, then 1Y, 2Y, 5Y and 10Y under "Rates" at declared positions, then Spread with
// none. The page shows each chart the bridge hands it, registers every open chart for "Add to chart",
// and writes a refusal under the grid.

const grid = (page) => page.locator('.ex-grid').first();
const charts = (page) => page.locator('section.bridge-chart');
const chart = (page, name) => page.locator(`section.bridge-chart[data-chart="${name}"] .ex-chart`);
const items = (page) => grid(page).locator('[role=menu] button[role=menuitem]');

test.beforeEach(async ({ page }) => {
    await page.goto('/chart-bridge');
    await expect(grid(page).locator("[id$='r7c0']")).toBeVisible();
});

/** Selects from one cell to another, as a click and a Shift+click do. */
async function select(page, [r1, c1], [r2, c2] = [r1, c1]) {
    await grid(page).locator(`[id$='r${r1}c${c1}']`).click({ force: true });
    if (r2 !== r1 || c2 !== c1) await grid(page).locator(`[id$='r${r2}c${c2}']`).click({ force: true, modifiers: ['Shift'] });
}

/** Opens the context menu on a cell of the selection and picks an item by its words. */
async function choose(page, [row, column], words) {
    await grid(page).locator(`[id$='r${row}c${column}']`).click({ button: 'right', force: true });
    await items(page).filter({ hasText: words }).click();
}

test('ADR-0258: tenor columns chart one curve a row, from the one item "Chart"', async ({ page }) => {
    await select(page, [0, 2], [3, 3]);
    await grid(page).locator("[id$='r1c2']").click({ button: 'right', force: true });
    await expect(items(page).filter({ hasText: /^(Chart|Series in columns|Series in rows)$/ })).toHaveText(['Chart']);
    await items(page).filter({ hasText: /^Chart$/ }).click();
    await expect(charts(page)).toHaveCount(1);
    await expect(chart(page, 'Chart 1').locator('g.ex-chart-series')).toHaveCount(4);
});

test('ADR-0258: one cell holds dates and tenors, so the menu offers both, and columns chart the history', async ({ page }) => {
    await select(page, [2, 4]);
    await grid(page).locator("[id$='r2c4']").click({ button: 'right', force: true });
    await expect(items(page).filter({ hasText: /^Series in/ })).toHaveText(['Series in columns', 'Series in rows']);
    await items(page).filter({ hasText: 'Series in columns' }).click();
    // The Note column nobody chose is left out (ADR-0251).
    await expect(chart(page, 'Chart 1').locator('g.ex-chart-series')).toHaveCount(5);
});

test('ADR-0251: a chosen text column is refused by name, and nothing is charted', async ({ page }) => {
    await select(page, [0, 0], [3, 2]);
    await choose(page, [1, 1], 'Series in columns');
    await expect(page.locator('#bridge-refusal')).toHaveText('Note cannot be plotted: not a number column.');
    await expect(charts(page)).toHaveCount(0);
});

test('ADR-0258: Add to chart adds a curve, and refuses a curve into a history', async ({ page }) => {
    await select(page, [0, 2], [1, 5]);
    await choose(page, [0, 2], /^Chart$/);
    await expect(chart(page, 'Chart 1').locator('g.ex-chart-series')).toHaveCount(2);

    await select(page, [4, 2], [4, 5]);
    await choose(page, [4, 3], 'Add to chart: Chart 1');
    await expect(chart(page, 'Chart 1').locator('g.ex-chart-series')).toHaveCount(3);
    await expect(charts(page)).toHaveCount(1);

    await select(page, [5, 4]);
    await choose(page, [5, 4], 'Series in columns');
    await expect(chart(page, 'Chart 2').locator('g.ex-chart-series')).toHaveCount(5);
    await select(page, [6, 2], [6, 3]);
    await choose(page, [6, 2], 'Add to chart: Chart 2');
    await expect(page.locator('#bridge-refusal')).toHaveText("The selection charts curves across declared positions, but 'Chart 2' is drawn along Date.");
    await expect(chart(page, 'Chart 2').locator('g.ex-chart-series')).toHaveCount(5);
});

test('ADR-0258: a chart keeps its rows; a deleted row is a gap with a note, and a recalculation is redrawn', async ({ page }) => {
    await select(page, [1, 2]);
    await choose(page, [1, 2], 'Series in columns');
    await expect(chart(page, 'Chart 1').locator('g.ex-chart-series')).toHaveCount(5);
    const path = chart(page, 'Chart 1').locator('g.ex-chart-series path').first();
    const before = await path.getAttribute('d');

    await page.locator('#recalculate').click();
    await expect(path).not.toHaveAttribute('d', before ?? '');
    await page.locator('#drop-row').click();
    await expect(chart(page, 'Chart 1').locator('.ex-chart-notes li')).toHaveText(['1 of the selected rows no longer exists.']);
    await expect(chart(page, 'Chart 1').locator('g.ex-chart-series')).toHaveCount(5);
});

test('ADR-0258, M3-Q6: a click on a point moves the grid\'s Focus to its cell', async ({ page }) => {
    await select(page, [0, 2], [1, 5]);
    await choose(page, [0, 2], /^Chart$/);
    // The pointer entry hears the pointer only once it holds the chart's first drawing.
    await expect(chart(page, 'Chart 1')).not.toHaveAttribute('aria-busy', 'true');
    const hit = chart(page, 'Chart 1').locator('.ex-chart-hit').first();
    await hit.scrollIntoViewIfNeeded();
    const box = await hit.boundingBox();
    expect(box, 'the plot is on screen').not.toBeNull();
    // The 10Y position, at the plot's right edge.
    const [x, y] = [box.x + box.width - 2, box.y + box.height / 2];
    await page.mouse.move(x, y);
    await expect(chart(page, 'Chart 1').locator('.ex-chart-tooltip')).toBeVisible();
    await page.mouse.down();
    await page.mouse.up();
    await circuitQuiet();
    await expect.poll(() => grid(page).getAttribute('aria-activedescendant')).toMatch(/r[01]c5$/);
});
