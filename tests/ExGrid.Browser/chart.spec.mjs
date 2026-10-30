import { test, expect, circuitQuiet } from './fixtures.mjs';

// ExChart on /chart (ticket 14): the exposure profile is two panels of levels on monthly dates,
// 2026-10-30 to 2036-10-30 (121 positions); EPE holds three series, ENE two. The page keeps what the user changed and writes the
// last event raised under the exposure chart (ADR-0272).

const exposure = (page) => page.locator('#exposure .ex-chart');
const layer = (chart, panel) => chart.locator(`.ex-chart-hit[data-ex-panel="${panel}"]`);

test.beforeEach(async ({ page }) => {
    await page.goto('/chart');
    await expect(exposure(page).locator('g.ex-chart-series')).toHaveCount(5);
    await page.mouse.move(0, 0);
});

/** The page point at a fraction of a hit layer's width and height. */
async function pointIn(locator, fx, fy = 0.5) {
    await locator.scrollIntoViewIfNeeded();
    const box = await locator.boundingBox();
    expect(box, 'the plot is on screen').not.toBeNull();
    return [box.x + box.width * fx, box.y + box.height * fy];
}

/** The pointer moved onto a position of a chart's panel, and its tooltip shown. */
async function hover(page, chart, panel, fx, fy = 0.5) {
    const [x, y] = await pointIn(layer(chart, panel), fx, fy);
    await page.mouse.move(x, y);
    await expect(chart.locator('.ex-chart-tooltip')).toBeVisible();
}

test('ADR-0270, CH-4: the tooltip lists every series of the pointer\'s panel, and the guide crosses every panel @behaviour', async ({ page }) => {
    const chart = exposure(page);
    await hover(page, chart, 0, 0.3);
    const tooltip = chart.locator('.ex-chart-tooltip');
    await expect(tooltip.locator('.ex-chart-tooltip-position')).toHaveText(/^\d{4}-\d{2}-\d{2}$/);
    await expect(tooltip.locator('.ex-chart-tooltip-line')).toHaveCount(3);
    await expect(tooltip.locator('.ex-chart-tooltip-value').first()).toHaveText(/^-?[\d,]+\.\d{2}$/);

    const plots = chart.locator('rect.ex-chart-plot');
    const guide = chart.locator('.ex-chart-guide-line');
    await expect(guide).toHaveAttribute('y1', (await plots.first().getAttribute('y')));
    const last = plots.last();
    const bottom = Number(await last.getAttribute('y')) + Number(await last.getAttribute('height'));
    expect(Number(await guide.getAttribute('y2'))).toBeCloseTo(bottom, 1);

    await hover(page, chart, 1, 0.3);
    await expect(tooltip.locator('.ex-chart-tooltip-line')).toHaveCount(2);

    await page.mouse.move(0, 0);
    await expect(tooltip).toHaveCount(0);
    await expect(guide).toHaveCount(0);
});

test('ADR-0271, ADR-0272: a legend click hides a series and raises its key; a second shows it @behaviour', async ({ page }) => {
    const chart = exposure(page);
    const entry = chart.locator('g.ex-chart-legend-entry[data-series="ene-before"]');
    await entry.click();
    await expect(page.locator('#exposure-events')).toHaveText('ene-before hidden');
    await expect(chart.locator('g.ex-chart-series[data-series="ene-before"]')).toHaveCount(0);
    await expect(entry).toHaveAttribute('aria-pressed', 'false');
    await entry.click();
    await expect(page.locator('#exposure-events')).toHaveText('ene-before shown');
    await expect(chart.locator('g.ex-chart-series[data-series="ene-before"]')).toHaveCount(1);
});

test('ADR-0273, ADR-0272: a drag across the plot narrows the range; a double-click returns it @behaviour', async ({ page }) => {
    const chart = exposure(page);
    const plot = layer(chart, 0);
    const [x1, y] = await pointIn(plot, 0.2);
    const [x2] = await pointIn(plot, 0.6);
    await page.mouse.move(x1, y);
    await page.mouse.down();
    await page.mouse.move(x2, y);
    await page.mouse.up();
    await expect(page.locator('#exposure-events')).toHaveText(/^range: \d{4}-\d{2}-\d{2} to \d{4}-\d{2}-\d{2}$/);
    await expect(chart.locator('.ex-chart-reset')).toBeEnabled();

    await plot.dblclick();
    await expect(page.locator('#exposure-events')).toHaveText('range: all');
    await expect(chart.locator('.ex-chart-reset')).toBeDisabled();
});

test('ADR-0272, CH-5: a drag begun before the data is replaced is refused when it ends @behaviour', async ({ page }) => {
    const chart = exposure(page);
    const plot = layer(chart, 0);
    const [x1, y] = await pointIn(plot, 0.2);
    const [x2] = await pointIn(plot, 0.6);
    await page.mouse.move(x1, y);
    await page.mouse.down();
    // Tomorrow's run arrives while the button is held, at machine speed: the click reaches the
    // chart's host before the release does, on either host.
    await page.locator('#next-run').dispatchEvent('click');
    await page.mouse.move(x2, y);
    await page.mouse.up();
    // A move to another position is answered after the release is, so once the tooltip names
    // it, the release has been taken.
    await hover(page, chart, 1, 0.8);
    await circuitQuiet();
    await expect(page.locator('#exposure-events')).not.toHaveText(/^range:/);
    await expect(chart.locator('.ex-chart-reset')).toBeDisabled();
    await expect(chart.locator('rect.ex-chart-band')).toHaveCount(0);
});

test('ADR-0275, CH-8: the chart is an image named by its summary, and View as table holds its data @behaviour', async ({ page }) => {
    const chart = exposure(page);
    await expect(chart.getByRole('img', { name: /^Exposure profile\. Date, 2026-10-30 to 2036-10-30\./ })).toHaveCount(1);
    await expect(chart.getByRole('img', { name: /EPE · After: maximum [\d,.]+ at \d{4}-\d{2}-\d{2}\./ })).toHaveCount(1);
    await chart.getByRole('button', { name: 'View as table' }).click();
    const table = chart.getByRole('table', { name: 'Exposure profile' });
    await expect(table.getByRole('columnheader')).toHaveText(['Date', 'EPE · Before', 'EPE · After', 'PFE95 · After', 'ENE · Before', 'ENE · After']);
    await expect(table.locator('tbody tr')).toHaveCount(121);
    await expect(table.locator('tbody tr').first().getByRole('rowheader')).toHaveText('2026-10-30');
});
