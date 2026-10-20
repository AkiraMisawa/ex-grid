import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { test, expect, circuitQuiet } from './fixtures.mjs';

// ExChart's Showcases (ADR-0280) on the DemoHost, the components the Docs Site shows at /showcase/exposure,
// /showcase/curves and /showcase/fx-volatility: each opens with its charts drawn and a clean console (the fixture fails a test on
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
    // The view's grid shows its rows, under either Chrome: a stretched grid given no width shows none.
    await expect(page.locator('.ex-grid [role=gridcell]').filter({ visible: true }).first()).toBeVisible();
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
        // Rows the Viewport shows whole under either Chrome: a click on a row the Viewport cuts off
        // scrolls the grid to it, and the rows above then leave the rendered window.
        await grid.locator("[id$='r1c0']").click({ force: true });
        await grid.locator("[id$='r3c5']").click({ force: true, modifiers: ['Shift'] });
        await grid.locator("[id$='r3c5']").click({ button: 'right', force: true });
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

// FX volatility: an EURUSD surface beside its quotes. Four quotes are missing: three lie between quotes
// of their expiry and are filled by the page's spline, marked interpolated, so they carry no dot; 10C at
// 5Y lies outside them and is a hole, which takes the one face at that corner with it.

/** The FX volatility Showcase's surface. */
const fxSurface = (page) => page.locator('.ex-chart-surface');
const fxBox = (page) => fxSurface(page).locator('.ex-chart-surface-box');

/** Waits until a chart is on screen and none is busy, and, on the Server host, until the circuit has said all it will. */
async function settledFx(page) {
    await expect(page.locator('.ex-chart').filter({ visible: true }).first()).toBeVisible();
    await expect(page.locator('.ex-chart[aria-busy="true"]')).toHaveCount(0);
    await circuitQuiet();
}

async function openFx(page, query) {
    await page.goto(`/chart-showcase/fx-volatility${query}`);
    await expect(page.locator('#demo-interactive')).toBeAttached();
    await expect(page.locator('.ex-grid [role=gridcell]').filter({ visible: true }).first()).toBeVisible();
    await expect(fxBox(page).locator('polygon.ex-chart-surface-face')).not.toHaveCount(0);
    await settledFx(page);
}

/** Switches the FX volatility Showcase to a view with the bar's switch. */
async function fxView(page, name) {
    await page.locator('.xs-bar').locator('.xs-view, .mud-toggle-item').filter({ hasText: name }).click();
    await settledFx(page);
}

for (const [chrome, query] of CHROMES) {
    test(`ADR-0280, ADR-0274, ADR-0257, CH-9: FX volatility shows its quotes beside the surface, the filled quotes marked and the hole kept, as recorded (${chrome})`, async ({ page }) => {
        await openFx(page, query);
        const grid = page.locator('.ex-grid').first();
        await expect(grid.locator('[role=row]').filter({ hasText: '1Y' })).toContainText('9.19%');

        await expect(fxBox(page).locator('circle.ex-chart-surface-point')).toHaveCount(5 * 8 - 4);
        await expect(fxBox(page).locator('polygon.ex-chart-surface-face')).toHaveCount(4 * 7 - 1);
        await expect(fxSurface(page).locator('.ex-chart-notes li')).toHaveText([
            '10C at 5Y has no quote and lies outside its expiry\'s quotes, so it is a hole: nothing is extrapolated.',
            'Interpolated by the Consumer: natural cubic spline across delta, per expiry.',
        ]);

        await matchesSnapshot(fxBox(page), 'fx-surface-summary.aria.yml');
        await fxSurface(page).getByRole('button', { name: 'View as table' }).click();
        await matchesSnapshot(fxSurface(page).locator('table.ex-chart-table'), 'fx-surface-table.aria.yml');
        await fxSurface(page).getByRole('button', { name: 'View as table' }).click();
        await expect(fxSurface(page).locator('table.ex-chart-table')).toHaveCount(0);
    });

    test(`ADR-0280, ADR-0274: a click on the FX volatility surface slices it beside the surface (${chrome})`, async ({ page }) => {
        await openFx(page, query);
        // Beside the grid on a wide screen, below it on a narrow one.
        await fxBox(page).scrollIntoViewIfNeeded();
        const circle = fxBox(page).locator('circle.ex-chart-surface-point').first();
        const frame = await fxBox(page).boundingBox();
        const cx = Number(await circle.getAttribute('cx'));
        const cy = Number(await circle.getAttribute('cy'));
        await page.mouse.click(frame.x + cx, frame.y + cy);
        const slice = fxSurface(page).locator('.ex-chart-surface-slice-chart .ex-chart');
        await expect(slice.locator('.ex-chart-toolbar')).toHaveAttribute('aria-label', /^Volatility at (10P|1W)$/);
        await expect(slice.locator('g.ex-chart-series')).toHaveCount(1);
        await expect(fxBox(page).locator('.ex-chart-surface-slice polyline')).toHaveCount(1);
    });

    test(`ADR-0280, ADR-0282: the FX volatility surface saves its drawing as SVG and PNG under its name (${chrome})`, async ({ page }) => {
        await openFx(page, query);
        await expect(fxSurface(page).locator('.ex-chart-download-png')).toBeEnabled();
        const take = async (button) => {
            const [download] = await Promise.all([page.waitForEvent('download'), fxSurface(page).locator(button).click()]);
            return { name: download.suggestedFilename(), bytes: fs.readFileSync(await download.path()) };
        };
        const svg = await take('.ex-chart-download-svg');
        expect(svg.name).toBe('EURUSD volatility.svg');
        const error = await page.evaluate((text) => new DOMParser().parseFromString(text, 'image/svg+xml').querySelector('parsererror')?.textContent ?? null,
            svg.bytes.toString('utf8'));
        expect(error).toBeNull();
        const png = await take('.ex-chart-download-png');
        expect(png.name).toBe('EURUSD volatility.png');
        expect(png.bytes.subarray(1, 4).toString('latin1')).toBe('PNG');
    });

    test(`ADR-0280, ADR-0257, CH-9: FX volatility's term structure, smile and forwards, each chart's summary and table as recorded (${chrome})`, async ({ page }) => {
        await openFx(page, query);

        // The term structure at the money: every tenor quoted, one series.
        await fxView(page, 'Term structure');
        await expect(shownChart(page).locator('g.ex-chart-series')).toHaveCount(1);
        await matchesRecord(page, 'fx-term-atm');

        // The smile at 1Y, whose 25C has no quote: the quotes, and the slice through the filled value.
        await fxView(page, 'Smile');
        await expect(shownChart(page).locator('g.ex-chart-series')).toHaveCount(2);
        await matchesRecord(page, 'fx-smile-1y');

        // The deposits and forwards: the two rates in one panel, the forward points in another.
        await fxView(page, 'Deposits and forwards');
        await expect(shownChart(page).locator('g.ex-chart-series')).toHaveCount(3);
        await matchesRecord(page, 'fx-forwards');

        // And back: the surface comes back as it opens.
        await fxView(page, 'Surface');
        await expect(fxBox(page).locator('polygon.ex-chart-surface-face')).toHaveCount(4 * 7 - 1);
    });
}
