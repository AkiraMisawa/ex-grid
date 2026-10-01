import { test, expect } from './fixtures.mjs';
import { API_URL } from './hosting.mjs';
import { expectCodeIsSource } from './demo-code.mjs';

// /pivot-live (ADR-0066/0067/0068), under ExPivot's own markup and under ExPivot.MudBlazor's Chrome:
// live data both ways. In the page's own process, a timer the page owns folds Change Batches into
// the bundled source; on the demo API server, live updates keep changing the trades, and the hub's
// notices reach the server's source through NotifyChanged. What only a browser can say: that the
// values the data changed are marked in both reports, and that a layout change marks nothing
// (PV-36); that the page turns the server's live updates on, and off again as it goes; and that the
// console stays clean throughout (PV-20).
//
// The API server lives for the whole run, and other files share it: each test starts from
// POST /api/reset, and turns live updates off again as it ends.

test.use({ viewport: { width: 1400, height: 1100 } });

const section = (page, side) => page.locator(`#pivot-live-${side}`);
const pivot = (page, side) => section(page, side).locator('.ex-pivot');
const report = (page, side) => pivot(page, side).locator('.ex-pivot-sheet > .ex-grid');
const rows = (page, side) => report(page, side).locator('.ex-viewport .ex-row');
const marked = (page, side) => report(page, side).locator('.ex-viewport .ex-cell.ex-changed');

async function api(path, init) {
    const response = await fetch(`${API_URL}${path}`, init);
    expect(response.ok, `${path} answered ${response.status}`).toBe(true);
    return response.json();
}

const post = (path, body) => api(path, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
});

test.afterEach(async () => {
    // Whatever the test left, the data holds still for the next file (ADR-0068).
    await post('/api/live', { on: false });
});

async function open(page, chrome) {
    await post('/api/reset');
    await page.goto(`/pivot-live?chrome=${chrome}`);
    await expect(rows(page, 'local').first()).toBeVisible({ timeout: 60_000 });
    await expect(rows(page, 'server').first()).toBeVisible({ timeout: 60_000 });
    await expect.poll(() => pivot(page, 'local').evaluate((p) => getComputedStyle(p).display)).toBe('flex');
    if (chrome === 'mud') {
        await expect.poll(() => page.locator('.mud-ex-grid').first()
            .evaluate((p) => getComputedStyle(p).getPropertyValue('--ex-pivot-pane-background').trim())).not.toBe('');
    }
    // Connected to the hub, and the server's live updates turned on.
    await expect(page.locator('#pivot-live-server-toggle')).toHaveText("Turn the server's live updates off", { timeout: 30_000 });
}

/** The number of the last Change Batch the page applied, as its status line says it. */
async function batches(page) {
    const text = await page.locator('#pivot-live-local-status').textContent();
    return Number(/^Batch ([\d,]+):/.exec(text)?.[1]?.replace(/,/g, '') ?? 0);
}

for (const chrome of ['builtin', 'mud']) {
    test.describe(`under the ${chrome} Chrome`, () => {
        test(`PV-36/ADR-0066: Change Batches folded into the bundled source mark the values they changed, and a collapse marks nothing (${chrome})`, async ({ page }) => {
            test.setTimeout(90_000);
            await open(page, chrome);
            // The page's timer applies a batch four times a second, and ExPivot redraws from the
            // newest Snapshot, marking the values whose painted text changed.
            const from = await batches(page);
            await expect.poll(() => batches(page)).toBeGreaterThan(from + 2);
            await expect.poll(() => marked(page, 'local').count(), { timeout: 15_000 }).toBeGreaterThan(0);
            const text = await marked(page, 'local').first().textContent();
            expect(text?.trim()).toMatch(/^-?[\d,]+$/);

            // Paused, the page folds in nothing more, and the marks go when their second is up.
            await page.locator('#pivot-live-local-toggle').click();
            await expect(page.locator('#pivot-live-local-toggle')).toHaveText("Resume the page's changes");
            const paused = await batches(page);
            await expect(marked(page, 'local')).toHaveCount(0, { timeout: 10_000 });
            expect(await batches(page)).toBeLessThanOrEqual(paused + 1);

            // A collapse lays the report out again from the answer held: no data changed, so no
            // value is marked (ADR-0066), however long after.
            const toggle = report(page, 'local').locator('.ex-pivot-toggle').first();
            await expect(toggle).toHaveAttribute('aria-expanded', 'true');
            await toggle.click();
            await expect(report(page, 'local').locator('.ex-pivot-toggle').first()).toHaveAttribute('aria-expanded', 'false');
            await expect(marked(page, 'local')).toHaveCount(0);
            await page.waitForTimeout(600);
            await expect(marked(page, 'local')).toHaveCount(0);

            // Resumed, the batches mark values again.
            await page.locator('#pivot-live-local-toggle').click();
            await expect.poll(() => marked(page, 'local').count(), { timeout: 15_000 }).toBeGreaterThan(0);
        });

        test(`PV-36/ADR-0066: the server's changing data marks the values it changed, and the page turns its live updates off (${chrome})`, async ({ page }) => {
            test.setTimeout(90_000);
            await open(page, chrome);
            expect((await api('/api/live')).on).toBe(true);
            // The hub says each version the data moves on to, and ExPivot asks the server again.
            await expect(page.locator('#pivot-live-server-status')).toContainText('the hub last said version', { timeout: 15_000 });
            await expect.poll(() => marked(page, 'server').count(), { timeout: 30_000 }).toBeGreaterThan(0);

            // Off from the page: the server's data holds still, and the marks go.
            await page.locator('#pivot-live-server-toggle').click();
            await expect(page.locator('#pivot-live-server-toggle')).toHaveText("Turn the server's live updates on");
            expect((await api('/api/live')).on).toBe(false);
            await expect(marked(page, 'server')).toHaveCount(0, { timeout: 10_000 });

            // On again, and the page left: it turns them off as it goes.
            await page.locator('#pivot-live-server-toggle').click();
            await expect(page.locator('#pivot-live-server-toggle')).toHaveText("Turn the server's live updates off");
            expect((await api('/api/live')).on).toBe(true);
            await page.goto('/');
            await expect.poll(async () => (await api('/api/live')).on, { timeout: 15_000 }).toBe(false);
        });
    });
}

test('ADR-0068/0066: the code the page shows is the code it runs: a Change Batch by Record Key, and a notice passed on', async ({ page }) => {
    await open(page, 'builtin');
    const code = await expectCodeIsSource(page);
    expect(code['DemoPivotData.cs#fields']).toContain('.Key("Id", t => t.Id)');
    expect(code['PivotLivePage.razor#batch']).toContain('_local.Apply(DemoPivotData.Fields.Batch(changed: amended))');
    expect(code['PivotLivePage.razor#server']).toContain('server.NotifyChanged(version)');
    expect(code['PivotLivePage.razor#pivots']).toContain('RedrawInterval="RedrawInterval" ChangeHighlightDuration="Highlight"');
});
