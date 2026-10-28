import { test, expect, circuitQuiet, setRoundTrip } from './fixtures.mjs';

// Blazor's ExChart and ExSurface over their Script Core (ADR-0370 section 1), on /script-chart, on both Blazor hosts:
// the one path no other layer sees whole. The definition is .NET's, its Format crossing as the texts it writes each
// value, and the chart draws it in the browser; what the user changes crosses to the Consumer's handler in .NET and
// comes back as a parameter. What the chart draws and decides is the Script Core's own specs' on the React host, and
// the wire's both halves are the bridge's tests' (chart-blazor's Vitest, ExChart.Components' ScriptChartTests).

const exposure = (page) => page.locator('#script-exposure .ex-chart');
const razorExposure = (page) => page.locator('#exposure .ex-chart');
const surface = (page) => page.locator('#script-surface .ex-chart-surface');

/** The tooltip's lines at a share of a chart's first panel, as the user reads them. */
async function readingAt(page, chart, across) {
    const layer = chart.locator('.ex-chart-hit[data-ex-panel="0"]');
    await layer.scrollIntoViewIfNeeded();
    const box = await layer.boundingBox();
    expect(box, 'the plot is on screen').not.toBeNull();
    await page.mouse.move(box.x + box.width * across, box.y + box.height / 2);
    const tooltip = chart.locator('.ex-chart-tooltip');
    await expect(tooltip).toBeVisible();
    const reading = await tooltip.innerText();
    await page.mouse.move(0, 0);
    await expect(tooltip).toHaveCount(0);
    return reading;
}

test('ADR-0370 section 1: the chart the Script Core draws from .NET\'s definition reads as the Razor component\'s, and a legend click crosses to .NET and back', async ({ page }) => {
    await page.goto('/chart');
    await expect(razorExposure(page).locator('g.ex-chart-series')).toHaveCount(5);
    const razor = await readingAt(page, razorExposure(page), 0.3);
    // The values as the Consumer's Format writes them ("N2"), which only .NET can call.
    expect(razor).toMatch(/\d,?\d*\.\d{2}/);

    await page.goto('/script-chart');
    await expect(exposure(page).locator('g.ex-chart-series')).toHaveCount(5);
    await circuitQuiet();
    expect(await readingAt(page, exposure(page), 0.3)).toBe(razor);

    await exposure(page).locator('g.ex-chart-legend-entry[data-series="ene-before"]').click();
    await expect(page.locator('#script-chart-events')).toHaveText('ene-before hidden');
    await expect(exposure(page).locator('g.ex-chart-series[data-series="ene-before"]')).toHaveCount(0);
});

test('ADR-0274, ADR-0370 section 1: a click on the surface reaches .NET as the axis\' own value, and the slice it hands back is drawn in its written digits', async ({ page }) => {
    await page.goto('/script-chart');
    const box = surface(page).locator('.ex-chart-surface-box');
    await expect(box.locator('circle.ex-chart-surface-point')).toHaveCount(20);
    await circuitQuiet();
    await box.scrollIntoViewIfNeeded();
    const point = box.locator('circle.ex-chart-surface-point').first();
    const frame = await box.boundingBox();
    await page.mouse.click(frame.x + Number(await point.getAttribute('cx')), frame.y + Number(await point.getAttribute('cy')));

    // A strike is a decimal in .NET, written 0.90, and a tenor is text.
    await expect(page.locator('#script-surface-events')).toHaveText(/^Slice: (Strike = 0\.90 \(Decimal\)|Tenor = 1M \(String\))$/);
    const slice = surface(page).locator('.ex-chart-surface-slice-chart .ex-chart');
    await expect(slice.locator('.ex-chart-toolbar')).toHaveAttribute('aria-label', /^Volatility at (0\.90|1M)$/);
    await expect(slice.locator('g.ex-chart-series')).toHaveCount(1);
});

test('ADR-0420 section 3: a chart is written and sampled by the page\'s own module, imported from where the application serves it', async ({ page }) => {
    await page.goto('/script-chart');
    const chart = page.locator('#script-functions .ex-chart');
    await expect(chart.locator('g.ex-chart-series')).toHaveCount(1);
    await circuitQuiet();
    // The position as the module's `years` writes it, and the curve's value as its `bp` writes what its `survival` answered.
    expect(await readingAt(page, chart, 0.5)).toMatch(/^\d+\.\d yrs\n[\s\S]*Survival[\s\S]*\b9\d{3} bp/u);
});

test('ADR-0410, ADR-0281: MudBlazor\'s toolbar and menu stand in the chart\'s slots, and a range typed and applied at once over a slow circuit is the one read', async ({ page }) => {
    await page.goto('/script-chart-mud');
    const chart = page.locator('.ex-chart').first();
    await expect(chart.locator('g.ex-chart-series')).toHaveCount(5);
    const toolbar = chart.locator('.ex-chart-toolbar-slot [role=toolbar]');
    await expect(toolbar.locator('[data-ex-command="exchart.filters"]')).toHaveClass(/mud-button-root/);
    await circuitQuiet();
    try {
        await setRoundTrip(150);
        // Chart Filters opens on the press, in the browser; its fields are MudBlazor's once their Razor stands.
        await toolbar.locator('[data-ex-command="exchart.filters"]').click();
        const from = chart.locator('[data-ex-chart-from]');
        await expect(from).toBeVisible();
        // Typed and applied at machine speed: Apply reads the fields in the browser, never .NET's copy a round trip behind.
        await from.fill('2027-01-01');
        await chart.locator('[data-ex-chart-to]').fill('2029-12-31');
        await chart.locator('[data-ex-chart-apply]').click();
        await expect(page.locator('#script-chart-mud-events')).toHaveText('range narrowed');
        await circuitQuiet();
        await expect(from).toHaveValue('2027-01-01');

        // Reset range from MudBlazor's menu, in the chart's frame: the range returns, and the fields with it.
        const stage = chart.locator('.ex-chart-stage');
        const box = await stage.boundingBox();
        await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2, { button: 'right' });
        const reset = chart.locator('.ex-chart-menu [role=menuitem][data-ex-command="exchart.reset-range"]');
        await expect(reset).toHaveClass(/mud-button-root/);
        await reset.click();
        await expect(page.locator('#script-chart-mud-events')).toHaveText('range: all');
        await expect(chart.locator('.ex-chart-menu')).toHaveCount(0);
        await circuitQuiet();
        await expect(from).toHaveValue('');
    } finally {
        await setRoundTrip(0);
    }
});

test('ADR-0421, ADR-0258: the Chart Dock over the Script grid opens a chart from the grid\'s menu, and the menu opened at once after it offers that chart', async ({ page }) => {
    await page.goto('/script-chart-dock');
    const grid = page.locator('.ex-grid').first();
    const cell = (row, column) => grid.locator(`[id$='r${row}c${column}']`);
    const items = grid.locator('.ex-popover [role=menuitem]');
    const tabs = page.locator('.mud-ex-chart-dock-panel .mud-tab');
    await expect(cell(7, 0)).toBeVisible();
    await circuitQuiet();
    try {
        await setRoundTrip(150);
        await cell(0, 1).click({ force: true });
        await cell(3, 2).click({ modifiers: ['Shift'], force: true });
        // The menu asks .NET for the bridge's commands as it opens, and stands with them.
        await cell(0, 1).click({ button: 'right', force: true });
        await items.filter({ hasText: /^Chart$/ }).click();
        // At machine speed, while "Chart" still runs in .NET: the menu opened next is held until it has run, so it
        // offers the tab "Chart" opened, never the menu as it stood before.
        await cell(5, 1).click({ button: 'right', force: true });
        await expect(items.filter({ hasText: /^Add to chart/ })).toHaveText(['Add to chart: Chart 1']);
        await expect(tabs).toHaveText(['Chart 1']);
        // The chart in the tab is the Script Core's, its toolbar MudBlazor's.
        await expect(page.locator('.mud-ex-chart-dock-panel .ex-chart [data-ex-command="exchart.filters"]')).toHaveClass(/mud-button-root/);
        await expect(page.locator('.mud-ex-chart-dock-panel .ex-chart g.ex-chart-series')).toHaveCount(4);
    } finally {
        await setRoundTrip(0);
    }
});
