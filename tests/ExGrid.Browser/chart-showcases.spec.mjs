import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { test, expect, circuitQuiet } from './fixtures.mjs';

// ExChart's Showcases (ADR-0280) on the DemoHost, the components the Docs Site shows at /showcase/exposure,
// /showcase/curves, /showcase/fx-volatility, /showcase/cds-spread and /showcase/equity-index: each opens with its charts drawn and a clean console (the fixture fails a test on
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

// CDS spread vs leverage (ADR-0290, ADR-0291): 200 names beside their scatter, the distressed DSTR at
// row 57 the highest spread by far, with a least-squares and a Theil–Sen line over them.

/** The page point of the highest mark of a series: DSTR on the CDS chart. */
async function highestMark(chart, series) {
    await chart.scrollIntoViewIfNeeded();
    const svg = chart.locator('svg.ex-chart-svg').first();
    const d = await chart.locator(`g.ex-chart-series[data-series="${series}"] path.ex-chart-markers`).first().getAttribute('d');
    // Each marker starts with an absolute move to a point on its outline, a few pixels from its centre.
    let best = null;
    for (const [, x, y] of d.matchAll(/M(-?[\d.]+),(-?[\d.]+)/g)) {
        if (best === null || Number(y) < best[1]) best = [Number(x), Number(y)];
    }
    const box = await svg.boundingBox();
    return [box.x + best[0] + 2, box.y + best[1] + 2];
}

for (const [chrome, query] of CHROMES) {
    test(`ADR-0280, ADR-0290, ADR-0291, CH-9: CDS spread vs leverage opens with the names beside both fits, as recorded (${chrome})`, async ({ page }) => {
        await open(page, `/chart-showcase/cds-spread${query}`);
        await expect(shownChart(page).locator('g.ex-chart-series')).toHaveCount(3);
        await expect(shownChart(page).locator('.ex-chart-notes')).toContainText(/OLS: y = [\d.]+x [+−] [\d.]+, R² = [\d.]+, n = 200/);
        await expect(shownChart(page).locator('.ex-chart-notes')).toContainText(/Theil–Sen: y = [\d.]+x/);
        // The summary as recorded, and the table's heading; its rows — each name's leverage and each
        // position the two fits are sampled at — are counted rather than recorded line by line.
        const chart = shownChart(page);
        await matchesSnapshot(chart.locator('svg.ex-chart-svg'), 'cds-spread-summary.aria.yml');
        await chart.getByRole('button', { name: 'View as table' }).click();
        await matchesSnapshot(chart.locator('table.ex-chart-table thead'), 'cds-spread-table-head.aria.yml');
        await expect(chart.locator('table.ex-chart-table tbody tr')).toHaveCount(2504);
        await chart.getByRole('button', { name: 'View as table' }).click();
        await expect(chart.locator('table.ex-chart-table')).toHaveCount(0);
    });

    test(`ADR-0280, ADR-0290, ADR-0258: on CDS spread vs leverage a click goes to the row, a rectangle narrows and the spread turns logarithmic (${chrome})`, async ({ page }) => {
        await open(page, `/chart-showcase/cds-spread${query}`);
        const chart = shownChart(page);
        const grid = page.locator('.ex-grid').first();

        // DSTR, the highest mark: its tooltip, then a click that moves the grid's Focus to its spread.
        await page.mouse.move(0, 0);
        const [x, y] = await highestMark(chart, 'names');
        await page.mouse.move(x, y);
        await expect(chart.locator('.ex-chart-tooltip-position')).toHaveText('DSTR');
        await page.mouse.down();
        await page.mouse.up();
        await circuitQuiet();
        await expect.poll(() => grid.getAttribute('aria-activedescendant')).toMatch(/r57c2$/);
        await expect(grid.locator('[role=row]').filter({ hasText: 'DSTR' })).toContainText('2,400bp');

        // A rectangle across the lower left narrows both axes, and the page is handed the range.
        const hit = chart.locator('.ex-chart-hit').first();
        const box = await hit.boundingBox();
        await page.mouse.move(box.x + box.width * 0.1, box.y + box.height * 0.95);
        await page.mouse.down();
        await page.mouse.move(box.x + box.width * 0.5, box.y + box.height * 0.6, { steps: 4 });
        await page.mouse.up();
        await circuitQuiet();
        await expect(page.locator('.xs-range')).toHaveText(/^Leverage \d\.\d\dx–\d\.\d\dx, spread [\d,]+–[\d,]+bp$/);
        await page.locator('.xs-reset').click();
        await expect(page.locator('.xs-range')).toHaveText('200 names');

        // The spread on a logarithmic axis, ticked by decades.
        await page.locator('.xs-log').click();
        await circuitQuiet();
        await expect(chart.locator('text.ex-chart-tick:not(.ex-chart-tick-x)', { hasText: /^1,?000(bp)?$/ }).first()).toBeAttached();
    });
}

// Equity index daily bars (ADR-0300 to ADR-0304): 2,608 sessions from 2016-01-04 to 2025-12-31 beside
// their candles, volume and 25-day average; the intervals in a second view.

/** A click at the middle of the candles' plot. */
async function clickMiddleCandle(page) {
    const layer = shownChart(page).locator('.ex-chart-hit[data-ex-panel="0"]');
    await layer.scrollIntoViewIfNeeded();
    const box = await layer.boundingBox();
    await page.mouse.click(box.x + box.width * 0.5, box.y + box.height * 0.5);
    await circuitQuiet();
}

for (const [chrome, query] of CHROMES) {
    test(`ADR-0280, ADR-0300, ADR-0302, CH-9: Equity index merges ten years into months beside its rows, as recorded (${chrome})`, async ({ page }) => {
        await open(page, `/chart-showcase/equity-index${query}`);
        const chart = shownChart(page);
        await expect(page.locator('.ex-grid').first().locator('[role=row]').filter({ hasText: '20,203.78' }).first()).toBeVisible();
        await expect(chart.locator('.ex-chart-notes')).toContainText('Northgate 225: monthly candles, merged from rows to fit.');
        await expect(chart.locator('path.ex-chart-rising')).toHaveCount(2);
        await expect(chart.locator('path.ex-chart-falling')).toHaveCount(2);
        await matchesSnapshot(chart.locator('svg.ex-chart-svg'), 'equity-index-summary.aria.yml');
    });

    test(`ADR-0280, ADR-0302, ADR-0258: on Equity index a merged candle narrows to its rows, and a day's candle goes to its row (${chrome})`, async ({ page }) => {
        await open(page, `/chart-showcase/equity-index${query}`);
        const chart = shownChart(page);
        // The average hidden, the candle is the only series a click can name.
        await chart.locator('.ex-chart-legend-entry', { hasText: '25-day average' }).click();
        await circuitQuiet();
        await page.mouse.move(0, 0);
        await clickMiddleCandle(page);
        await expect(page.locator('.xs-events')).toHaveText(/^(\d{4})-(\d{2})-\d{2} to \1-\2-\d{2}$/);
        await expect(chart.locator('.ex-chart-notes li', { hasText: 'merged' })).toHaveCount(0);
        await expect(page.locator('.xs-reset')).toBeEnabled();

        await clickMiddleCandle(page);
        await expect(page.locator('.xs-events')).toHaveText(/^Went to \d{4}-\d{2}-\d{2}$/);
        const day = (await page.locator('.xs-events').textContent()).slice('Went to '.length);
        const grid = page.locator('.ex-grid').first();
        await expect.poll(() => grid.getAttribute('aria-activedescendant')).toMatch(/c4$/);
        const active = await grid.getAttribute('aria-activedescendant');
        await expect(grid.locator(`[id="${active}"]`).locator('xpath=ancestor::*[@role="row"][1]')).toContainText(day);

        await page.locator('.xs-reset').click();
        await expect(chart.locator('.ex-chart-notes')).toContainText('monthly candles, merged from rows to fit.');
    });

    test(`ADR-0280, ADR-0301, ADR-0300: Equity index packs its sessions and takes the page's rise and fall colours (${chrome})`, async ({ page }) => {
        await open(page, `/chart-showcase/equity-index${query}`);
        const chart = shownChart(page);
        await expect(chart.locator('.ex-chart-notes li', { hasText: 'packed' })).toHaveCount(0);
        await page.locator('.xs-packed').click();
        await expect(chart.locator('.ex-chart-notes')).toContainText('Dates are packed');

        const rising = chart.locator('path.ex-chart-rising').first();
        const colours = page.locator('.xs-colours').locator('.xs-view, .mud-toggle-item');
        await colours.filter({ hasText: 'Green rises, red falls' }).click();
        await expect.poll(() => rising.evaluate(p => getComputedStyle(p).stroke)).toBe('rgb(46, 125, 50)');
        await colours.filter({ hasText: 'Red rises, blue falls' }).click();
        await expect.poll(() => rising.evaluate(p => getComputedStyle(p).stroke)).toBe('rgb(211, 47, 47)');
    });

    test(`ADR-0280, ADR-0303, ADR-0304, CH-9: Equity index's intervals, a Bid–Ask band and 95% CI whiskers, as recorded (${chrome})`, async ({ page }) => {
        await open(page, `/chart-showcase/equity-index${query}`);
        await page.locator('.xs-bar').locator('.xs-view, .mud-toggle-item').filter({ hasText: 'Intervals' }).click();
        const charts = page.locator('.ex-chart').filter({ visible: true });
        await expect(charts).toHaveCount(3);
        await expect(page.locator('.ex-chart[aria-busy="true"]')).toHaveCount(0);
        await circuitQuiet();
        await expect(charts.nth(0).locator('path.ex-chart-interval-band')).toHaveCount(1);
        await expect(charts.nth(1).locator('path.ex-chart-whiskers')).toHaveCount(1);
        await expect(charts.nth(2).locator('path.ex-chart-whiskers')).not.toHaveCount(0);
        for (const [k, name] of [[0, 'equity-curve'], [1, 'equity-forecasts'], [2, 'equity-strategies']]) {
            const chart = charts.nth(k);
            await matchesSnapshot(chart.locator('svg.ex-chart-svg'), `${name}-summary.aria.yml`);
            await chart.getByRole('button', { name: 'View as table' }).click();
            await matchesSnapshot(chart.locator('table.ex-chart-table'), `${name}-table.aria.yml`);
            await chart.getByRole('button', { name: 'View as table' }).click();
            await expect(chart.locator('table.ex-chart-table')).toHaveCount(0);
        }
    });
}

// Binomial lattice (ADR-0280): an option priced on a CRR lattice, its nodes in a grid and on a chart coloured
// by value, early exercise ringed, and the lattice price by N against Black–Scholes. The page prices; the
// figures below are the CRR and Black–Scholes prices of a one-year at-the-money option at 20% and 5%.

async function openLattice(page, route) {
    await page.goto(route);
    await expect(page.locator('#demo-interactive')).toBeAttached();
    await expect(page.locator('.ex-grid [role=gridcell]').first()).toBeVisible();
    await expect(page.locator('.ex-chart')).toHaveCount(2);
    await expect(page.locator('.ex-chart[aria-busy="true"]')).toHaveCount(0);
    await circuitQuiet();
}

const latticeCard = (page, label) => page.getByRole('group', { name: label, exact: true }).locator('.xs-bin-value');

for (const [chrome, query] of CHROMES) {
    test(`ADR-0280: Binomial lattice prices a European call on five steps beside Black–Scholes (${chrome})`, async ({ page }) => {
        await openLattice(page, `/chart-showcase/binomial-lattice${query}`);
        await expect(latticeCard(page, 'Lattice price (N = 5)')).toHaveText('10.8059');
        await expect(latticeCard(page, 'Black–Scholes, European')).toHaveText('10.4506');
        // Twelve diagonals, the ring and eight bands; 21 nodes, none exercised early.
        const lattice = page.locator('.xs-bin-lattice .ex-chart');
        await expect(lattice.locator('g.ex-chart-series')).toHaveCount(21);
        await expect(lattice.locator('g.ex-chart-series[data-series="early"] .ex-chart-markers').first()).toHaveAttribute('d', '');
        await expect(page.locator('.ex-grid [role=gridcell]').filter({ hasText: /^Exercise$/ })).toHaveCount(0);
    });

    test(`ADR-0280: Binomial lattice rings the nodes an American put is exercised at, and its rows say so (${chrome})`, async ({ page }) => {
        await openLattice(page, `/chart-showcase/binomial-lattice${query}`);
        await page.locator('[aria-label="Option type"]').getByText('Put', { exact: true }).click();
        await page.locator('[aria-label="Exercise"]').getByText('American', { exact: true }).click();
        await circuitQuiet();
        await expect(latticeCard(page, 'Lattice price (N = 5)')).toHaveText('6.3678');
        await expect(latticeCard(page, 'Black–Scholes, European')).toHaveText('5.5735');
        const ring = page.locator('.xs-bin-lattice g.ex-chart-series[data-series="early"] .ex-chart-markers').first();
        await expect(ring).not.toHaveAttribute('d', '');
        await expect(page.locator('.xs-bin-ring')).toHaveText('early exercise');
        await expect(page.locator('.ex-grid [role=gridcell]').filter({ hasText: /^Exercise$/ }).first()).toBeAttached();
    });

    test(`ADR-0280: Binomial lattice's switch puts the page in the dark scheme and back (${chrome})`, async ({ page }) => {
        await openLattice(page, `/chart-showcase/binomial-lattice${query}`);
        await page.locator('[aria-label="Scheme"]').getByText('Dark', { exact: true }).click();
        await expect(page).toHaveURL(/[?&]scheme=dark/);
        await expect.poll(() => page.evaluate(() => getComputedStyle(document.documentElement).colorScheme)).toBe('dark');
        await page.locator('[aria-label="Scheme"]').getByText('Light', { exact: true }).click();
        await expect(page).not.toHaveURL(/scheme=dark/);
    });
}
