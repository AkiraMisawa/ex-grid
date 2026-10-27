import { test, expect, circuitQuiet, settled } from './fixtures.mjs';

// Blazor's ExPivot over its Script Core (ADR-0370 section 1), on /script-pivot, on both Blazor hosts: the one path
// no other layer sees whole. The report is built in the browser from the Leaf Aggregates the C# source answers in
// .NET, so every question crosses the bridge as PivotJson and comes back, a layout the user makes crosses to the
// Consumer's handler, and a layout or a source of the Consumer's crosses back. The engines' answers are the shared
// cases' (tests/cases/pivot), and the Script Core's behaviour is its own specs' on the React host.

test.use({ viewport: { width: 1400, height: 1100 } });

const pivot = (page) => page.locator('.ex-pivot');
const report = (page) => pivot(page).locator('.ex-pivot-sheet > .ex-grid');
const rows = (page) => report(page).locator('.ex-viewport .ex-row');
const pane = (page) => page.getByRole('region', { name: 'PivotTable Fields' });
const tick = (page, caption) => pane(page).getByRole('checkbox', { name: caption, exact: true });
// Row 1 is Americas' first desk under the Compact form, column 1 its first product.
const firstValue = (page) => rows(page).nth(1).locator('[role=gridcell]').nth(1);
const grandTotal = (page) => rows(page).last().locator('[role=gridcell]').last();

async function open(page, path) {
    await page.goto(path);
    await expect(rows(page).first()).toBeVisible({ timeout: 30_000 });
    await expect(pivot(page)).not.toHaveAttribute('aria-busy');
    await settled(report(page));
    await circuitQuiet();
}

async function figures(page) {
    return { first: await firstValue(page).textContent(), total: await grandTotal(page).textContent() };
}

test('ADR-0370 section 1: the report built in the browser from .NET\'s Leaf Aggregates is the Razor component\'s, and a new source is asked again', async ({ page }) => {
    await open(page, '/pivot?chrome=builtin');
    const razor = await figures(page);

    await open(page, '/script-pivot');
    expect(await figures(page)).toEqual(razor);

    await page.locator('#script-pivot-revalue').click();
    await expect(firstValue(page)).not.toHaveText(razor.first);
    await expect(pivot(page)).not.toHaveAttribute('aria-busy');
    await settled(report(page));
    expect((await figures(page)).total).not.toBe(razor.total);
});

test('ADR-0059, ADR-0370 section 1: a field ticked reaches the Consumer\'s layout in .NET, and the Consumer\'s own layout replaces it', async ({ page }) => {
    await open(page, '/script-pivot');
    await expect(page.locator('#script-pivot-layout')).toHaveText('Rows: Region, Desk');

    await tick(page, 'Book').check();
    await expect(page.locator('#script-pivot-layout')).toHaveText('Rows: Region, Desk, Book');
    await expect(tick(page, 'Book')).toBeChecked();

    await page.locator('#script-pivot-reset').click();
    await expect(tick(page, 'Book')).not.toBeChecked();
    await expect(page.locator('#script-pivot-layout')).toHaveText('Rows: Region, Desk');
});

test('ADR-0060, ADR-0063, ADR-0370 section 10: the Consumer\'s words and its Show Details handler, both in .NET, reach the pivot in the browser', async ({ page }) => {
    await open(page, '/script-pivot?details=dotnet');
    await expect(report(page).getByText('Row Labels', { exact: true })).toBeVisible();

    await page.locator('#script-pivot-japanese').click();
    await expect(report(page).getByText('行ラベル', { exact: true })).toBeVisible();

    await firstValue(page).dblclick({ force: true });
    await expect(page.locator('#script-pivot-details')).toHaveText(/^詳細: .+: [1-9]\d* records$/);
    await expect(pivot(page).locator('.ex-pivot-details-panel')).toHaveCount(0);
});
