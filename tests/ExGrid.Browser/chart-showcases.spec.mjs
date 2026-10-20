import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { test, expect, circuitQuiet } from './fixtures.mjs';

// ExChart's Showcases (ADR-0280) on the DemoHost, the components the Docs Site shows at /showcase/exposure
// and /showcase/curves: each opens with its charts drawn and a clean console (the fixture fails a test on
// an unexpected console message), under the built-in Chrome and MudBlazor's. CH-9: each chart's
// accessible summary and "View as table" match the recorded ARIA snapshot, the same file under either
// Chrome, because swapping the Chrome must not change what the chart says (spine rule 4). The snapshots
// are in chart-showcases.spec.mjs-snapshots/; the data behind them is fixed (samples/ExChart.Showcases/Data).
//
// Each snapshot is exact: every figure and date as the chart says it. --update-snapshots would write
// numbers as patterns (\d+), which would let a wrong figure pass, so a snapshot is recorded with
// EXGRID_RECORD_ARIA=1, which writes what the chart says now; read the diff before committing it.

const RECORD = process.env.EXGRID_RECORD_ARIA === '1';
const SNAPSHOTS = path.join(path.dirname(fileURLToPath(import.meta.url)), 'chart-showcases.spec.mjs-snapshots');

async function matchesSnapshot(locator, name) {
    if (RECORD) {
        await expect(locator).toHaveCount(1);
        fs.mkdirSync(SNAPSHOTS, { recursive: true });
        fs.writeFileSync(path.join(SNAPSHOTS, name), `${await locator.ariaSnapshot()}\n`);
        return;
    }
    await expect(locator).toMatchAriaSnapshot({ name });
}

const CHROMES = [['built-in', ''], ['MudBlazor', '?chrome=mud']];

/** The chart on screen: the selected tab's, or the view's own. */
const shownChart = (page) => page.locator('.ex-chart').filter({ visible: true });

async function open(page, route) {
    await page.goto(route);
    await expect(page.locator('#demo-interactive')).toBeAttached();
    await settledCharts(page);
}

/** Waits until a chart is on screen and none is busy, and, on the Server host, until the circuit has said all it will. */
async function settledCharts(page) {
    await expect(shownChart(page)).toHaveCount(1);
    await expect(page.locator('.ex-chart[aria-busy="true"]')).toHaveCount(0);
    await circuitQuiet();
}

/** The tabs of the charts made from the grid, under either Chrome. */
const chartTabs = (page) => page.locator('.xs-charts [role=tab], .mud-ex-chart-dock-panel .mud-tab');

/** Switches the Curves Showcase to a view with the bar's switch. */
async function view(page, name) {
    await page.locator('.xs-bar').locator('.xs-view, .mud-toggle-item').filter({ hasText: name }).click();
    await settledCharts(page);
}

/** CH-9: the shown chart's summary, and its "View as table", against the recorded snapshots. */
async function matchesRecord(page, name) {
    const chart = shownChart(page);
    await matchesSnapshot(chart.locator('svg.ex-chart-svg'), `${name}-summary.aria.yml`);
    await chart.getByRole('button', { name: 'View as table' }).click();
    await matchesSnapshot(chart.locator('table.ex-chart-table'), `${name}-table.aria.yml`);
    await chart.getByRole('button', { name: 'View as table' }).click();
    await expect(chart.locator('table.ex-chart-table')).toHaveCount(0);
}

for (const [chrome, query] of CHROMES) {
    test(`ADR-0280, CH-9: Exposure profile opens on EPE before and after, its summary and table as recorded (${chrome})`, async ({ page }) => {
        await open(page, `/chart-showcase/exposure${query}`);
        await expect(chartTabs(page)).toHaveText(['Chart 1']);
        await expect(shownChart(page).locator('g.ex-chart-series')).toHaveCount(2);
        await matchesRecord(page, 'exposure-epe');
    });

    test(`ADR-0280, ADR-0258: Exposure profile charts a selection into a tab, and a recalculation redraws it (${chrome})`, async ({ page }) => {
        await open(page, `/chart-showcase/exposure${query}`);
        const grid = page.locator('.ex-grid').first();
        await grid.locator("[id$='r1c0']").click({ force: true });
        await grid.locator("[id$='r6c5']").click({ force: true, modifiers: ['Shift'] });
        await grid.locator("[id$='r1c0']").click({ button: 'right', force: true });
        await grid.locator('[role=menu] [role=menuitem]').filter({ hasText: /^Chart$/ }).click();
        await expect(chartTabs(page)).toHaveText(['Chart 1', 'Chart 2']);
        await settledCharts(page);
        // EPE before, after and its Diff, and ENE before and after: the Diff crosses no zero here.
        await expect(shownChart(page).locator('g.ex-chart-series')).toHaveCount(5);

        const summary = shownChart(page).locator('svg.ex-chart-svg title');
        const before = await summary.textContent();
        await page.locator('.xs-recalculate').click();
        await expect(page.locator('.xs-bar')).toContainText('Hedge ×1.5');
        await expect(summary).not.toHaveText(before);
    });

    test(`ADR-0280, CH-9: Curves switches between its three views, each chart's summary and table as recorded (${chrome})`, async ({ page }) => {
        await open(page, `/chart-showcase/curves${query}`);

        // The CDS curve: the spread and the hazard rate at every pillar, in a panel each.
        await expect(chartTabs(page)).toHaveText(['Chart 1']);
        await expect(shownChart(page).locator('g.ex-chart-series')).toHaveCount(2);
        await matchesRecord(page, 'curves-cds');

        // By reference entity: three curves' spreads, and their hazard rates in a second tab.
        await view(page, 'By reference entity');
        await expect(chartTabs(page)).toHaveText(['Chart 1', 'Chart 2']);
        await expect(shownChart(page).locator('g.ex-chart-series')).toHaveCount(3);
        await matchesRecord(page, 'curves-entities-hazards');

        // Rates: the zero curve, its pillars the quotes and the line between them Interpolated.
        await view(page, 'Rates');
        await expect(chartTabs(page)).toHaveCount(0);
        await matchesRecord(page, 'curves-rates-zero');
        await page.locator('.xs-chart-views').locator('.xs-view, .mud-toggle-item').filter({ hasText: /^Forwards$/ }).click();
        await settledCharts(page);
        await matchesRecord(page, 'curves-rates-forwards');

        // And back: a view comes back as it opens.
        await view(page, 'CDS curve');
        await expect(chartTabs(page)).toHaveText(['Chart 1']);
        await matchesRecord(page, 'curves-cds');
    });
}
