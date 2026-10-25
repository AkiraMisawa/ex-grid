import { test, expect, circuitQuiet } from './fixtures.mjs';

// Scatter on /scatter (tickets 54 to 59, ADR-0290, ADR-0291). The CDS chart holds 200 names, the
// distressed DSTR at row 57 the highest spread by far, beside a grid of Name, Leverage and 5Y spread;
// a least-squares and a Theil–Sen line over them. Old Faithful holds 272 eruptions; the curve-fitting
// chart switches its polynomial's degree by buttons. The page writes the range it was handed under the
// checkbox.

const cds = (page) => page.locator('.scatter-side-by-side .ex-chart');
const grid = (page) => page.locator('.ex-grid').first();
const curve = (page) => page.locator('.ex-chart').last();

test.beforeEach(async ({ page }) => {
    await page.goto('/scatter');
    // The pointer entry hears the pointer only once it holds the chart's first drawing.
    await expect(cds(page)).not.toHaveAttribute('aria-busy', 'true');
    await page.mouse.move(0, 0);
});

/** The page point of the highest mark of a series: DSTR on the CDS chart. */
async function highestMark(chart, series) {
    await chart.scrollIntoViewIfNeeded();
    const svg = chart.locator('svg').first();
    const d = await chart.locator(`g.ex-chart-series[data-series="${series}"] path.ex-chart-markers`).first().getAttribute('d');
    // Each marker starts with an absolute move to a point on its outline, a few pixels from its centre.
    let best = null;
    for (const [, x, y] of d.matchAll(/M(-?[\d.]+),(-?[\d.]+)/g)) {
        if (best === null || Number(y) < best[1]) best = [Number(x), Number(y)];
    }
    const box = await svg.boundingBox();
    return [box.x + best[0] + 2, box.y + best[1] + 2];
}

test('ADR-0290: the tooltip reads the one point nearest the pointer, and nothing away from every point', async ({ page }) => {
    const chart = cds(page);
    const [x, y] = await highestMark(chart, 'names');
    await page.mouse.move(x, y);
    const tooltip = chart.locator('.ex-chart-tooltip');
    await expect(tooltip.locator('.ex-chart-tooltip-position')).toHaveText('DSTR');
    await expect(tooltip.locator('.ex-chart-tooltip-line')).toHaveCount(1);
    await expect(tooltip.locator('.ex-chart-tooltip-value')).toHaveText('5.60x, 2400bp');
    // Far from every mark, the plot's top-left corner reads nothing.
    const hit = await chart.locator('.ex-chart-hit').first().boundingBox();
    await page.mouse.move(hit.x + 3, hit.y + 3);
    await expect(tooltip).toHaveCount(0);
});

test('ADR-0290, ADR-0258: a click on a point moves the grid\'s Focus to its row', async ({ page }) => {
    const [x, y] = await highestMark(cds(page), 'names');
    await page.mouse.move(x, y);
    await expect(cds(page).locator('.ex-chart-tooltip')).toBeVisible();
    await page.mouse.down();
    await page.mouse.up();
    await circuitQuiet();
    await expect.poll(() => grid(page).getAttribute('aria-activedescendant')).toMatch(/r57c2$/);
});

test('ADR-0290: a rectangle dragged across the plot narrows both axes, and the page is handed the range', async ({ page }) => {
    const chart = cds(page);
    const hit = chart.locator('.ex-chart-hit').first();
    await hit.scrollIntoViewIfNeeded();
    const box = await hit.boundingBox();
    await page.mouse.move(box.x + box.width * 0.1, box.y + box.height * 0.95);
    await page.mouse.down();
    await page.mouse.move(box.x + box.width * 0.5, box.y + box.height * 0.6, { steps: 4 });
    await expect(chart.locator('.ex-chart-band')).toHaveCount(1);
    await page.mouse.up();
    await circuitQuiet();
    await expect(page.locator('#cds-range')).toHaveText(/^Leverage \d\.\d\d–\d\.\d\d, spread \d+–\d+bp$/);
    await expect(chart.locator('.ex-chart-band')).toHaveCount(0);
    // DSTR stands above the rectangle and is no longer drawn: the highest mark is now an ordinary name.
    const [x, y] = await highestMark(chart, 'names');
    await page.mouse.move(x, y);
    await expect(chart.locator('.ex-chart-tooltip-position')).not.toHaveText('DSTR');
});

test('ADR-0290: the spread on a logarithmic axis is ticked by decades', async ({ page }) => {
    const chart = cds(page);
    await page.locator('#log-spread').check();
    await circuitQuiet();
    await expect(chart.locator('text.ex-chart-tick:not(.ex-chart-tick-x)', { hasText: /^1000$/ }).first()).toBeAttached();
    await expect(chart.locator('.ex-chart-notes')).not.toContainText('stays proportional');
});
