import { test, expect, scrollRowToTop, dataRowCount, setTicked } from './fixtures.mjs';
import { readClipboard } from './sheet-helpers.mjs';
import { API_URL, SERVER } from './hosting.mjs';
import { expectCodeIsSource } from './demo-code.mjs';

// /pivot-db (ADR-0065/0066/0069), under ExPivot's own markup and under ExPivot.MudBlazor's Chrome:
// the demo API server's SQLite trades two ways, side by side. "Database → Snapshot" reads the
// trades over Arrow into a Snapshot the page pivots in its own process; "database → server Pivot
// Source" asks the server each question through a fetching Pivot Source: the server answers Leaf Aggregates and the
// page builds the report (ADR-0370 section 3).
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

/** Opens /pivot-db. */
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

/** What a report paints: how many rows it has, and the text of every painted row's cells. Both
 * reports stand under the same header, so the counts compare as ARIA states them (ADR-0033). */
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

// The question that brings PV-20's server pivot its report: the Leaf Aggregates, from which the pivot builds the
// report in the browser (ADR-0370 section 3).
const REPORT_QUESTION = 'aggregate';

/** How many questions of a kind the page has asked the server, as its status line counts them. */
async function asked(page, kind) {
    const text = await page.locator('#pivot-db-server-status').textContent();
    return Number(new RegExp(`([\\d,]+) ${kind}`).exec(text)?.[1]?.replaceAll(',', '') ?? NaN);
}

test('LV-28/ADR-0152: remote whole-column Copy and Summary include rows outside the Window', async ({ page, context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    await open(page, 'builtin');
    await setTicked(section(page, 'snapshot').getByRole('checkbox', { name: 'Trade ID', exact: true }));
    await expect.poll(() => dataRowCount(report(page, 'snapshot'))).toBeGreaterThan(1000);
    await page.locator('#pivot-db-same-layout').click();
    await expectSameReports(page);
    const copies = {}, summaries = {};
    for (const side of ['snapshot', 'server']) {
        const grid = report(page, side);
        // spec-lint: early-read: expectSameReports above waits by expect.poll until both reports paint the same; the lint cannot see into the file's own function
        const count = await dataRowCount(grid);
        // spec-lint: early-read: expectSameReports above waits by expect.poll until both reports paint the same; the lint cannot see into the file's own function
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
        test(`PV-20/ADR-0069: the database read over Arrow and asked of the server's Pivot Source shows the same numbers (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            const status = await api('/api/status');
            // The whole database, read into the page's own Snapshot at the server's version.
            await expect(page.locator('#pivot-db-snapshot-status'))
                .toContainText(`Read ${status.trades.toLocaleString('en-US')} trades at version ${status.version} `);
            await expectSameReports(page);
            expect(await asked(page, REPORT_QUESTION)).toBeGreaterThanOrEqual(1);
            // A server's source can be refreshed; the bundled source is refreshed by a new source
            // instead, and shows no Refresh (ADR-0066).
            await expect(toolbarButton(page, 'server', 'Refresh')).toBeVisible();
            await expect(toolbarButton(page, 'snapshot', 'Refresh')).toHaveCount(0);
            // The server's data holds still while live updates are off: a Refresh asks again and
            // shows the same numbers.
            const aggregates = await asked(page, REPORT_QUESTION);
            await toolbarButton(page, 'server', 'Refresh').click();
            await expect.poll(() => asked(page, REPORT_QUESTION)).toBe(aggregates + 1);
            await expectSameReports(page);
        });

        test(`PV-20/ADR-0069: a layout changed on one, shown on the other, gives the same numbers there too (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            await expectSameReports(page);
            const before = await painted(page, 'snapshot');
            // Month into Rows, in the Snapshot pivot's own pane: answered in this process.
            await setTicked(section(page, 'snapshot').getByRole('checkbox', { name: 'Month', exact: true }));
            await expect.poll(async () => (await painted(page, 'snapshot')).rows).toBeGreaterThan(before.rows);
            // The same layout on the server's pivot: a GROUP BY over three fields, answered in SQL.
            const aggregates = await asked(page, REPORT_QUESTION);
            await page.locator('#pivot-db-same-layout').click();
            await expect.poll(() => asked(page, REPORT_QUESTION)).toBeGreaterThan(aggregates);
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
                details[side] = { grid, rows: await dataRowCount(grid) };
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
    expect(code['DemoServerPivot.cs#source']).toContain('PivotSource.Fetch(fields, features,');
    expect(code['DemoServerPivot.cs#source']).toContain('MaxDetailsPage = 10_000');
});
