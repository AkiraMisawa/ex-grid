import { test, expect, scrollRowToTop, circuitQuiet, setRoundTrip, dataRowCount, setTicked } from './fixtures.mjs';
import { readClipboard } from './sheet-helpers.mjs';
import { API_URL, REACT, SERVER } from './hosting.mjs';
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

// The questions that bring the server pivot's report: a report Window on the Blazor hosts, which ask the server's
// report through PivotReportSource.Fetch, and the Leaf Aggregates on the React host, whose pivot builds the report
// from them (ADR-0370 section 3).
const REPORT_QUESTION = REACT ? 'aggregate' : 'Window';

/** How many questions of a kind the page has asked the server, as its status line counts them. */
async function asked(page, kind) {
    const text = await page.locator('#pivot-db-server-status').textContent();
    return Number(new RegExp(`(\\d+) ${kind}`).exec(text)?.[1] ?? NaN);
}

test('LV-24/ADR-0151: a server report scrolls beyond its first Window with bounded rendered rows', async ({ page }) => {
    await open(page, 'builtin');
    const grid = report(page, 'server');
    await setTicked(section(page, 'server').getByRole('checkbox', { name: 'Trade ID', exact: true }));
    const status = await api('/api/status');
    await expect.poll(() => dataRowCount(grid)).toBeGreaterThanOrEqual(status.trades);
    const count = await dataRowCount(grid);
    expect(await rows(page, 'server').count()).toBeLessThan(100);
    const last = count - 1;
    await scrollRowToTop(grid, last);
    await expect(grid.locator(`[id$='-r${last}c0']`)).toContainText('Grand Total');
    expect(await rows(page, 'server').count()).toBeLessThan(100);
    expect(await asked(page, REPORT_QUESTION)).toBeGreaterThan(1);
});

test('LV-24/ADR-0151: a report that shrinks under the Window scrolled to shows its last rows, and scrolls on', async ({ page }) => {
    await open(page, 'builtin');
    // Computed in the page over the Snapshot, and on the server: the same Window, asked of each.
    for (const side of ['snapshot', 'server']) {
        const grid = report(page, side);
        const rowCount = () => dataRowCount(grid);
        // The first layout's rows: the regions and their desks, and the Grand Total.
        const first = await rowCount();
        const tradeId = section(page, side).getByRole('checkbox', { name: 'Trade ID', exact: true });
        await setTicked(tradeId);
        await expect.poll(rowCount).toBeGreaterThan(1000);
        const count = await rowCount();
        // Scrolled to the last row, thousands of rows below the end of the first layout's report.
        await scrollRowToTop(grid, count - 1);
        await expect(grid.locator(`[id$='-r${count - 1}c0']`)).toContainText('Grand Total');

        // Trade ID out of the rows again: the report shrinks to the first layout's, which ends long
        // before the Window the grid stands on. The grid shows the new report's last rows, its Grand
        // Total among them — not an error, and not an empty Window.
        await setTicked(tradeId, false);
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

// ADR-0182/0183 (LV-34, FN-3a): the server's report is asked for the rows on screen and 60 either side,
// and asked for the next Window ten rows before the one held runs out. Counted in the questions the
// page's status line says it has sent, never in time. On the Server host the circuit runs behind the
// latency proxy at 150 ms; on WebAssembly the browser sends each question itself, and the Window it
// names is read from the request. A request reaches the test as an event of its own, after the page
// has counted it, so the Window is waited for, not read in passing; and the listener goes with the
// test, since the page is handed on to the next one (ADR-0056).
test('LV-34/FN-3a/ADR-0182/0183: a server report scrolls within the Window it read ahead asking nothing, and asks once ten rows before it runs out', async ({ page }) => {
    await open(page, 'builtin');
    const windowsAsked = [];
    const askedWindow = (request) => {
        if (request.method() === 'POST' && /\/api\/pivot\/reports\/[0-9a-f]{32}\/window$/.test(request.url())) {
            windowsAsked.push(JSON.parse(request.postData() ?? '{}').window);
        }
    };
    page.on('request', askedWindow);
    await setRoundTrip(150);
    try {
        const grid = report(page, 'server');
        const placeholders = rows(page, 'server').and(page.locator('.ex-placeholder'));
        await setTicked(section(page, 'server').getByRole('checkbox', { name: 'Trade ID', exact: true }));
        await expect.poll(() => dataRowCount(grid)).toBeGreaterThan(1000);
        await circuitQuiet();

        // Row 500 at the top: past the Window, a jump the grid asks about once it settles (ADR-0004).
        await scrollRowToTop(grid, 500);
        await expect(placeholders).toHaveCount(0);
        await circuitQuiet();
        const onScreen = await rows(page, 'server').count();
        const atFiveHundred = await asked(page, REPORT_QUESTION);
        if (!SERVER) {
            await expect.poll(() => windowsAsked.at(-1)).toEqual({ start: 440, count: onScreen + 120 });
        }

        // Down five rows at a time to row 550: ten rows below the rows on screen is still in the
        // Window, which reaches sixty rows past the rows the grid asked for at 500. Nothing is asked.
        for (let row = 505; row <= 550; row += 5) {
            await scrollRowToTop(grid, row);
            await expect(placeholders).toHaveCount(0);
            await circuitQuiet();
            expect(await asked(page, REPORT_QUESTION), `asked at row ${row}`).toBe(atFiveHundred);
        }

        // Row 555: every row on screen is held, but the tenth row below them is not. The next Window
        // is asked for, once, and the rows on screen stay painted while it is out.
        await scrollRowToTop(grid, 555);
        await expect.poll(() => asked(page, REPORT_QUESTION)).toBe(atFiveHundred + 1);
        await expect(placeholders).toHaveCount(0);
        await circuitQuiet();
        expect(await asked(page, REPORT_QUESTION)).toBe(atFiveHundred + 1);
        if (!SERVER) {
            await expect.poll(() => windowsAsked.at(-1)).toEqual({ start: 495, count: onScreen + 120 });
        }
    } finally {
        page.off('request', askedWindow);
        await setRoundTrip(0);
    }
    // Whatever the host still had to say has been said before the console's verdict is read.
    await circuitQuiet();
});

// ADR-0182/0183 (LV-34, FN-3a): the question asked early stays in flight while the scroll goes on within
// its Window, however far past the Window held; and it is asked without the loading indication until rows
// on screen wait for it. The test holds the answer at the demo API's Window gate and opens it itself —
// never a timer (principle 6) — so the question is in flight for as long as the test says. The gate is
// where both hosts' questions arrive: on WebAssembly the browser sends them, on the Server host the
// server does, where no browser route reaches (SRV-2). The page's tally counts the questions; the gate
// reads the Window each names. On the Server host a reading that must see what the circuit has to say
// first waits for it to go quiet; a question held at the gate is between the host and the API, not on
// the circuit, so the circuit goes quiet around it.
test('LV-34/FN-3a/ADR-0182/0183: a question asked early stays in flight while the scroll stays in its Window, and the report dims only once rows on screen wait for it', async ({ page }) => {
    // open() resets the API, which opens the gate and forgets the Windows asked before.
    await open(page, 'builtin');
    const gate = (hold) => api('/api/pivot/window-gate', {
        method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ hold }),
    });
    const windowsAsked = async () => (await api('/api/pivot/window-gate')).windows;
    const lastWindow = async () => (await windowsAsked()).at(-1);
    try {
        const grid = report(page, 'server');
        const placeholders = rows(page, 'server').and(page.locator('.ex-placeholder'));
        await setTicked(section(page, 'server').getByRole('checkbox', { name: 'Trade ID', exact: true }));
        await expect.poll(() => dataRowCount(grid)).toBeGreaterThan(1000);
        await circuitQuiet();

        // Row 500 at the top: rows 440 on are asked for and held.
        await scrollRowToTop(grid, 500);
        await expect(placeholders).toHaveCount(0);
        await circuitQuiet();
        const onScreen = await rows(page, 'server').count();
        await expect.poll(lastWindow).toEqual({ start: 440, count: onScreen + 120 });
        const base = await asked(page, REPORT_QUESTION);
        for (let row = 505; row <= 550; row += 5) {
            await scrollRowToTop(grid, row);
        }
        await circuitQuiet();
        expect(await asked(page, REPORT_QUESTION)).toBe(base);

        // Row 555: the tenth row below the rows on screen is not held. The next Window is asked for, and
        // its answer held at the gate. Every row on screen is still held, and the report is not dimmed.
        const sentBefore = (await windowsAsked()).length;
        await gate(true);
        await scrollRowToTop(grid, 555);
        await expect.poll(() => asked(page, REPORT_QUESTION)).toBe(base + 1);
        await expect.poll(lastWindow).toEqual({ start: 495, count: onScreen + 120 });
        await circuitQuiet();
        await expect(placeholders).toHaveCount(0);
        await expect(grid).not.toHaveClass(/\bex-loading\b/);

        // On, five rows at a time, past the Window held and within the question's: it stays in flight,
        // and nothing else is asked. Once rows on screen are past the Window held, they wait for it as
        // Placeholders, and the report is dimmed.
        for (let row = 560; row <= 600; row += 5) {
            await scrollRowToTop(grid, row);
            await circuitQuiet();
            expect(await asked(page, REPORT_QUESTION), `asked at row ${row}`).toBe(base + 1);
        }
        await expect(placeholders).not.toHaveCount(0);
        await expect(grid).toHaveClass(/\bex-loading\b/);
        expect(await windowsAsked()).toHaveLength(sentBefore + 1);

        // The answer lands: the rows are painted, the dimming goes, and at row 600 the Window answered
        // holds the rows on screen and ten either side, so nothing more is asked.
        await gate(false);
        await expect(placeholders).toHaveCount(0);
        await expect(grid).not.toHaveClass(/\bex-loading\b/);
        await circuitQuiet();
        expect(await asked(page, REPORT_QUESTION)).toBe(base + 1);

        // The new Window reaches sixty rows past the rows asked for at 555: at 605 the tenth row below the
        // rows on screen is its last, and nothing is asked; at 606 it leaves it, and one question more is.
        await scrollRowToTop(grid, 605);
        await circuitQuiet();
        expect(await asked(page, REPORT_QUESTION)).toBe(base + 1);
        await scrollRowToTop(grid, 606);
        await expect.poll(() => asked(page, REPORT_QUESTION)).toBe(base + 2);
        await expect.poll(lastWindow).toEqual({ start: 546, count: onScreen + 120 });
        await expect(placeholders).toHaveCount(0);
    } finally {
        await gate(false);
    }
    // Whatever the host still had to say has been said before the console's verdict is read.
    await circuitQuiet();
});

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
        test(`PV-20/ADR-0069: the database read over Arrow and asked through PivotReportSource.Fetch shows the same numbers (${chrome})`, async ({ page }) => {
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
    expect(code['DemoServerPivot.cs#fetch']).toContain('PivotReportSource.Fetch(fields, features,');
    expect(code['DemoServerPivot.cs#fetch']).toContain('MaxDetailsPage = 10_000');
});
