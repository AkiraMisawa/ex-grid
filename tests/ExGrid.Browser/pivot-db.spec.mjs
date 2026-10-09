import { test, expect, scrollRowToTop, circuitQuiet } from './fixtures.mjs';
import { readClipboard } from './sheet-helpers.mjs';
import { API_URL } from './hosting.mjs';
import { expectCodeIsSource } from './demo-code.mjs';

// /pivot-db (ADR-0065/0066/0069), under ExPivot's own markup and under ExPivot.MudBlazor's Chrome:
// the demo API server's SQLite trades two ways, side by side. "Database → Snapshot" reads the
// trades over Arrow into a Snapshot the page pivots in its own process; "database → server Pivot
// Source" asks the server each question through PivotReportSource.Fetch, and the server computes the report and returns its requested Window.
// What only a browser can say: that both reach the server from either host, read the same trades
// and show the same numbers for the same layout; that the server's Details are paged from it as
// the Details tab scrolls; and that the console stays clean throughout (PV-20).
//
// The API server lives for the whole run, and other files share it: each test starts from
// POST /api/reset, which puts the generated trades back and turns live updates off, so the two
// reads see the same data. The trade count and the Source Version are read from /api/status.

test.use({ viewport: { width: 1400, height: 1100 } });

const section = (page, side) => page.locator(`#pivot-db-${side}`);
const pivot = (page, side) => section(page, side).locator('.ex-pivot');
// The report's own grid: a Details tab's grid stands beside it in the same box.
const report = (page, side) => pivot(page, side).locator('.ex-pivot-sheet > .ex-grid');
const rows = (page, side) => report(page, side).locator('.ex-viewport .ex-row');
const toolbarButton = (page, side, name) => pivot(page, side).locator('.ex-pivot-report').getByRole('button', { name, exact: true });
// Row 1 is Americas' first desk under the Compact form, column 1 its first product.
const firstValue = (page, side) => rows(page, side).nth(1).locator('[role=gridcell]').nth(1);

async function api(path, init) {
    const response = await fetch(`${API_URL}${path}`, init);
    expect(response.ok, `${path} answered ${response.status}`).toBe(true);
    return response.json();
}

async function open(page, chrome) {
    await api('/api/reset', { method: 'POST' });
    await page.goto(`/pivot-db?chrome=${chrome}`);
    await expect(rows(page, 'snapshot').first()).toBeVisible();
    await expect(rows(page, 'server').first()).toBeVisible();
    // ExPivot's stylesheet has landed when its root lays the report and the pane out side by
    // side; a Wrapper's, when one of its tokens reaches the paper.
    await expect.poll(() => pivot(page, 'snapshot').evaluate((p) => getComputedStyle(p).display)).toBe('flex');
    if (chrome === 'mud') {
        await expect.poll(() => page.locator('.mud-ex-grid').first()
            .evaluate((p) => getComputedStyle(p).getPropertyValue('--ex-pivot-pane-background').trim())).not.toBe('');
    }
}

/** What a report paints: how many rows it has, and the text of every painted row's cells. */
const painted = (page, side) => report(page, side).evaluate((root) => ({
    rows: Number(root.getAttribute('aria-rowcount')),
    text: [...root.querySelectorAll('.ex-viewport .ex-row')]
        .map((row) => [...row.querySelectorAll('[role=gridcell]')].map((cell) => cell.textContent.trim()).join(' | ')),
}));

/** Waits until the two reports paint the same rows and the same numbers. */
async function expectSameReports(page) {
    await expect.poll(async () => {
        const [snapshot, server] = await Promise.all([painted(page, 'snapshot'), painted(page, 'server')]);
        return JSON.stringify(snapshot) === JSON.stringify(server) && snapshot.text.length > 3
            ? 'the same'
            : JSON.stringify({ snapshot, server });
    }).toBe('the same');
}

/** How many questions of a kind the page has asked the server, as its status line counts them. */
async function asked(page, kind) {
    const text = await page.locator('#pivot-db-server-status').textContent();
    return Number(new RegExp(`(\\d+) ${kind}`).exec(text)?.[1] ?? NaN);
}

test('LV-24/ADR-0151: a server report scrolls beyond its first Window with bounded rendered rows', async ({ page }) => {
    await open(page, 'builtin');
    const grid = report(page, 'server');
    await section(page, 'server').getByRole('checkbox', { name: 'Trade ID', exact: true }).check();
    const status = await api('/api/status');
    await expect.poll(async () => Number(await grid.getAttribute('aria-rowcount'))).toBeGreaterThanOrEqual(status.trades);
    const count = Number(await grid.getAttribute('aria-rowcount'));
    expect(await rows(page, 'server').count()).toBeLessThan(100);
    const last = count - 1;
    await scrollRowToTop(grid, last);
    await expect(grid.locator(`[id$='-r${last}c0']`)).toContainText('Grand Total');
    expect(await rows(page, 'server').count()).toBeLessThan(100);
    expect(await asked(page, 'Window')).toBeGreaterThan(1);
});

test('LV-24/ADR-0151: a report that shrinks under the Window scrolled to shows its last rows, and scrolls on', async ({ page }) => {
    await open(page, 'builtin');
    // Computed in the page over the Snapshot, and on the server: the same Window, asked of each.
    for (const side of ['snapshot', 'server']) {
        const grid = report(page, side);
        const rowCount = async () => Number(await grid.getAttribute('aria-rowcount'));
        // The first layout's rows: the regions and their desks, and the Grand Total.
        const first = await rowCount();
        const tradeId = section(page, side).getByRole('checkbox', { name: 'Trade ID', exact: true });
        await tradeId.check();
        await expect.poll(rowCount).toBeGreaterThan(1000);
        const count = await rowCount();
        // Scrolled to the last row, thousands of rows below the end of the first layout's report.
        await scrollRowToTop(grid, count - 1);
        await expect(grid.locator(`[id$='-r${count - 1}c0']`)).toContainText('Grand Total');

        // Trade ID out of the rows again: the report shrinks to the first layout's, which ends long
        // before the Window the grid stands on. The grid shows the new report's last rows, its Grand
        // Total among them — not an error, and not an empty Window.
        await tradeId.uncheck();
        await expect.poll(rowCount).toBe(first);
        await expect(grid.locator(`[id$='-r${first - 1}c0']`)).toContainText('Grand Total');
        await circuitQuiet();
        await expect(grid.locator(`[id$='-r${first - 1}c0']`)).toContainText('Grand Total');
        expect(await rows(page, side).count()).toBeGreaterThan(0);

        // And it scrolls on: back at the top, the first region's row is painted where it belongs.
        await scrollRowToTop(grid, 0);
        await expect(grid.locator("[id$='-r0c0']")).toContainText('Americas');
    }
    // Whatever the host still had to say has been said before the console's verdict is read: no
    // console error, no page error, no unhandled exception in the host's log (CON-1, CON-2, CON-6).
    await circuitQuiet();
});

test('LV-28/ADR-0152: remote whole-column Copy and Summary include rows outside the Window', async ({ page, context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    await open(page, 'builtin');
    await section(page, 'snapshot').getByRole('checkbox', { name: 'Trade ID', exact: true }).check();
    await expect.poll(async () => Number(await report(page, 'snapshot').getAttribute('aria-rowcount'))).toBeGreaterThan(1000);
    await page.locator('#pivot-db-same-layout').click();
    await expectSameReports(page);
    const copies = {}, summaries = {};
    for (const side of ['snapshot', 'server']) {
        const grid = report(page, side);
        const count = Number(await grid.getAttribute('aria-rowcount'));
        expect(await rows(page, side).count()).toBeLessThan(100);
        await grid.locator("[id$='-r1c1']").click({ force: true });
        await page.keyboard.press('Control+Space');
        await expect(grid.locator('.ex-summary')).toContainText('Sum:');
        summaries[side] = await grid.locator('.ex-summary').innerText();
        await page.evaluate(() => navigator.clipboard.writeText('SENTINEL'));
        await page.keyboard.press('ControlOrMeta+C');
        await expect.poll(async () => (await readClipboard(page))['text/plain'] ?? 'SENTINEL').not.toBe('SENTINEL');
        copies[side] = (await readClipboard(page))['text/plain'];
        expect(copies[side].replace(/\r?\n$/, '').split(/\r?\n/)).toHaveLength(count);
    }
    expect(copies.server).toBe(copies.snapshot);
    expect(summaries.server).toBe(summaries.snapshot);
});

for (const chrome of ['builtin', 'mud']) {
    test.describe(`under the ${chrome} Chrome`, () => {
        test(`PV-20/ADR-0069: the database read over Arrow and asked through PivotReportSource.Fetch shows the same numbers (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            const status = await api('/api/status');
            // The whole database, read into the page's own Snapshot at the server's version.
            await expect(page.locator('#pivot-db-snapshot-status'))
                .toContainText(`Read ${status.trades.toLocaleString('en-US')} trades at version ${status.version} `);
            await expectSameReports(page);
            expect(await asked(page, 'Window')).toBeGreaterThanOrEqual(1);
            // A server's source can be refreshed; the bundled source is refreshed by a new source
            // instead, and shows no Refresh (ADR-0066).
            await expect(toolbarButton(page, 'server', 'Refresh')).toBeVisible();
            await expect(toolbarButton(page, 'snapshot', 'Refresh')).toHaveCount(0);
            // The server's data holds still while live updates are off: a Refresh asks again and
            // shows the same numbers.
            const aggregates = await asked(page, 'Window');
            await toolbarButton(page, 'server', 'Refresh').click();
            await expect.poll(() => asked(page, 'Window')).toBe(aggregates + 1);
            await expectSameReports(page);
        });

        test(`PV-20/ADR-0069: a layout changed on one, shown on the other, gives the same numbers there too (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            await expectSameReports(page);
            const before = await painted(page, 'snapshot');
            // Month into Rows, in the Snapshot pivot's own pane: answered in this process.
            await section(page, 'snapshot').getByRole('checkbox', { name: 'Month', exact: true }).check();
            await expect.poll(async () => (await painted(page, 'snapshot')).rows).toBeGreaterThan(before.rows);
            // The same layout on the server's pivot: a GROUP BY over three fields, answered in SQL.
            const aggregates = await asked(page, 'Window');
            await page.locator('#pivot-db-same-layout').click();
            await expect.poll(() => asked(page, 'Window')).toBeGreaterThan(aggregates);
            await expectSameReports(page);
            await expect(section(page, 'server').getByRole('checkbox', { name: 'Month', exact: true })).toBeChecked();
        });

        test(`PV-20/ADR-0066: Show Details pages the server's records as the Details tab scrolls (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            await expectSameReports(page);
            const cell = await firstValue(page, 'server').textContent();

            // The same cell in both: the records behind it, in a tab at the report's foot.
            const details = {};
            for (const side of ['snapshot', 'server']) {
                await firstValue(page, side).dblclick({ force: true });
                const tab = pivot(page, side).getByRole('tablist').getByRole('tab', { name: /^Details: Americas \/ / });
                await expect(tab).toHaveAttribute('aria-selected', 'true');
                const grid = pivot(page, side).getByRole('tabpanel').locator('.ex-grid');
                await expect(grid.locator('.ex-viewport .ex-row').first()).toBeVisible();
                // Trade ID first, then Region: every record behind the cell is Americas'.
                await expect(grid.locator('.ex-viewport .ex-row').first().locator('[role=gridcell]').nth(1)).toHaveText('Americas');
                details[side] = { grid, rows: Number(await grid.getAttribute('aria-rowcount')) };
            }
            expect(details.server.rows, `the trades behind ${cell}`).toBe(details.snapshot.rows);
            expect(details.server.rows).toBeGreaterThan(20);

            // The server's records come a page at a time: scrolled to its last rows, the Details
            // grid asks for another page, and the page asks the server for it.
            const pages = await asked(page, 'details');
            expect(pages).toBeGreaterThanOrEqual(1);
            const last = details.server.rows - 5;
            await scrollRowToTop(details.server.grid, last);
            await expect(details.server.grid.locator(`[id$='-r${last}c1']`)).toHaveText('Americas');
            expect(await asked(page, 'details')).toBeGreaterThan(pages);
        });
    });
}

test('ADR-0069/0065: the code the page shows is the code it runs, the Arrow request read whole', async ({ page }) => {
    await open(page, 'builtin');
    const code = await expectCodeIsSource(page);
    // The browser's HttpClient turns response streaming off for the Arrow request (ADR-0065).
    expect(code['PivotDbPage.razor#snapshot']).toContain('request.SetBrowserResponseStreamingEnabled(false);');
    expect(code['PivotDbPage.razor#snapshot']).toContain('SnapshotArrow.ReadAsync(');
    expect(code['PivotDbPage.razor#snapshot']).toContain('PivotSource.From(snapshot, fields)');
    expect(code['DemoServerPivot.cs#fetch']).toContain('PivotReportSource.Fetch(fields, features,');
    expect(code['DemoServerPivot.cs#fetch']).toContain('MaxDetailsPage = 10_000');
});
