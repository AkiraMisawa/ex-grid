import { test, expect, circuitQuiet } from './fixtures.mjs';
import { API_URL } from './hosting.mjs';
import { expectCodeIsSource } from './demo-code.mjs';
import { blend, contrast, onDeviceGrid, painted, paints, wholePixelCentres } from './pixels.mjs';

// /pivot-live (ADR-0067/0068/0069), under ExPivot's own markup and under ExPivot.MudBlazor's Chrome:
// live data both ways. In the page's own process, a timer the page owns folds Change Batches into
// the bundled source; on the demo API server, live updates keep changing the trades, and the hub's
// notices reach the server's source through NotifyChanged. What only a browser can say: that the
// values the data changed are marked in both reports, and that a layout change marks nothing
// (PV-36); that a server cut off leaves a Stale Report whose numbers are painted muted, and still
// readable, until Retry brings the newest back (PV-37, ADR-0067's decision of 2026-10-09); that the
// page turns the server's live updates on, and off again as it goes; and that the console stays
// clean throughout (PV-20).
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
    // Whatever the test left, the data holds still for the next file (ADR-0069).
    await post('/api/live', { on: false });
});

async function open(page, chrome, scheme = 'light') {
    await post('/api/reset');
    await page.goto(`/pivot-live?chrome=${chrome}${scheme === 'dark' ? '&scheme=dark' : ''}`);
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

/** A computed colour as its sRGB channels, each its exact value in bytes, and its alpha. */
function channelsOf(css) {
    const rgb = /^rgba?\(([\d.]+),\s*([\d.]+),\s*([\d.]+)(?:,\s*([\d.]+))?\)$/.exec(css);
    if (rgb) {
        return { rgb: [Number(rgb[1]), Number(rgb[2]), Number(rgb[3])], alpha: rgb[4] === undefined ? 1 : Number(rgb[4]) };
    }
    const srgb = /^color\(srgb ([\d.e-]+) ([\d.e-]+) ([\d.e-]+)(?: \/ ([\d.]+))?\)$/.exec(css);
    if (srgb) {
        return { rgb: [srgb[1], srgb[2], srgb[3]].map((v) => Number(v) * 255), alpha: srgb[4] === undefined ? 1 : Number(srgb[4]) };
    }
    throw new Error(`a computed colour this file does not read: ${css}`);
}

/** What text in the computed colour `css` paints on a device pixel it covers whole, over the opaque
 * `ground`: its own channels, or, translucent, blended at the alpha the browser holds — a whole
 * number of 255ths (pixels.mjs, `blend`). */
function inkOver(css, ground) {
    const { rgb, alpha } = channelsOf(css);
    return alpha === 1 ? rgb : blend(rgb, Math.round(alpha * 255) / 255, ground);
}

/** The colour `css` resolves to inside `cell`, as the cascade there resolves it. */
const probedColour = (cell, css) => cell.evaluate((element, colour) => {
    const probe = document.createElement('span');
    probe.style.color = colour;
    element.appendChild(probe);
    const resolved = getComputedStyle(probe).color;
    probe.remove();
    return resolved;
}, css);

/**
 * The ground of `cells`, read in the first one's left padding, and the most inked pixel of their
 * text — the one farthest from that ground — as painted, over the cells that stand wholly inside
 * `within` across. Read on the device pixels each cell covers whole, two in from its edges, where a
 * column's or a row's rule would be: a digit's stroke covers some pixel whole, and that pixel is
 * the text's own colour (README.md, "Reading pixels").
 */
async function inkOf(page, cells, within) {
    const scale = await page.evaluate(() => window.devicePixelRatio);
    const bounds = await within.boundingBox();
    let ground = null;
    let inked = null;
    let farthest = -1;
    let read = 0;
    for (const cell of await cells.all()) {
        const box = await cell.boundingBox();
        if (!box || box.x < bounds.x || box.x + box.width > bounds.x + bounds.width) {
            continue;
        }
        read++;
        const region = await painted(page, onDeviceGrid(box, scale));
        const { xs, ys } = wholePixelCentres(box, scale);
        ground ??= region.at(xs[2], ys[Math.floor(ys.length / 2)]);
        for (const y of ys.slice(2, -2)) {
            for (const x of xs.slice(2, -2)) {
                const pixel = region.at(x, y);
                const distance = pixel.reduce((sum, v, i) => sum + Math.abs(v - ground[i]), 0);
                if (distance > farthest) {
                    farthest = distance;
                    inked = pixel;
                }
            }
        }
    }
    expect(read, 'a value cell stands wholly in view').toBeGreaterThan(0);
    return { ground, inked };
}

/** The number of the last Change Batch the page applied, as its status line says it. */
async function batches(page) {
    const text = await page.locator('#pivot-live-local-status').textContent();
    return Number(/^Batch ([\d,]+):/.exec(text)?.[1]?.replace(/,/g, '') ?? 0);
}

for (const chrome of ['builtin', 'mud']) {
    test.describe(`under the ${chrome} Chrome`, () => {
        test(`PV-36/ADR-0067: Change Batches folded into the bundled source mark the values they changed, and a collapse marks nothing (${chrome})`, async ({ page }) => {
            test.setTimeout(90_000);
            await open(page, chrome);
            // Only the page's own batches change anything here. The server's live updates, which
            // the page turned on, are turned off, so that once the batches are paused the circuit
            // goes quiet and a reading can see all the host will say (ADR-0056).
            await page.locator('#pivot-live-server-toggle').click();
            await expect(page.locator('#pivot-live-server-toggle')).toHaveText("Turn the server's live updates on");
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
            await circuitQuiet();
            expect(await batches(page)).toBeLessThanOrEqual(paused + 1);

            // A collapse lays the report out again from the answer held: no data changed, so no
            // value is marked (ADR-0067), however long after: on the Server host once the circuit
            // is quiet; the fixed wait is the page's own time, all there is on WebAssembly.
            const toggle = report(page, 'local').locator('.ex-pivot-toggle').first();
            await expect(toggle).toHaveAttribute('aria-expanded', 'true');
            await toggle.click();
            await expect(report(page, 'local').locator('.ex-pivot-toggle').first()).toHaveAttribute('aria-expanded', 'false');
            await expect(marked(page, 'local')).toHaveCount(0);
            await page.waitForTimeout(600);
            await circuitQuiet();
            await expect(marked(page, 'local')).toHaveCount(0);

            // Resumed, the batches mark values again.
            await page.locator('#pivot-live-local-toggle').click();
            await expect.poll(() => marked(page, 'local').count(), { timeout: 15_000 }).toBeGreaterThan(0);
        });

        for (const scheme of ['light', 'dark']) {
            test(`PV-37/ADR-0067/UX-8: a server cut off leaves a Stale Report whose value cells paint muted and readable, its labels as they were, until Retry brings the newest back (${chrome}, ${scheme})`, async ({ page }) => {
                test.setTimeout(90_000);
                await open(page, chrome, scheme);
                // Only the server's report is read here: the page's own batches are paused, so that once
                // the server's live updates are off too the circuit goes quiet (ADR-0056).
                await page.locator('#pivot-live-local-toggle').click();
                await expect(page.locator('#pivot-live-local-toggle')).toHaveText("Resume the page's changes");
                const shell = pivot(page, 'server').locator('.ex-pivot-report');
                const notice = pivot(page, 'server').locator('.ex-pivot-stale[role=status]');
                // The first row — the first region's, with its subtotals — its value cells and its label.
                const values = rows(page, 'server').first().locator('.ex-cell-numeric');
                const label = rows(page, 'server').first().locator('[role=gridcell]').first();
                await expect(values.first()).toBeVisible();
                const colourOf = (cell) => cell.evaluate((element) => getComputedStyle(element).color);
                const ink = await colourOf(values.first());
                await expect(shell).not.toHaveClass(/ex-pivot-report-stale/);

                // Cut off: the question the hub's next notice asks fails, and the report stays, stale.
                await page.locator('#pivot-live-server-cut').click();
                await expect(page.locator('#pivot-live-server-cut')).toHaveText('Reconnect the server');
                await expect(notice).toContainText('Showing the data as of', { timeout: 30_000 });
                await expect(notice).toContainText('The page cut the server off.');
                await expect(shell).toHaveClass(/ex-pivot-report-stale/);
                // Nothing more is asked once the live updates are off; the marks of the last answer end.
                await page.locator('#pivot-live-server-toggle').click();
                await expect(page.locator('#pivot-live-server-toggle')).toHaveText("Turn the server's live updates on");
                await expect(marked(page, 'server')).toHaveCount(0, { timeout: 10_000 });
                await page.mouse.move(0, 0);
                await circuitQuiet();
                await expect(notice).toContainText('The page cut the server off.');

                // Decided: every value cell takes the stale colour — ExPivot's default, or under MudBlazor
                // the token as the Wrapper maps it onto the palette — and the labels keep the ink.
                const stale = await colourOf(values.first());
                expect(stale, 'the value cells are painted in another colour than the ink').not.toBe(ink);
                for (const cell of await values.all()) {
                    expect(await colourOf(cell)).toBe(stale);
                }
                expect(await colourOf(label), 'a label keeps the ink').toBe(ink);
                if (chrome === 'mud') {
                    expect(stale, "the Wrapper's mapping of the token").toBe(await probedColour(values.first(), 'var(--ex-pivot-stale-value-color)'));
                }
                // Painted: the numbers' most inked pixel is the stale colour exactly, over the cells' ground
                // — the first row is a group row, so the ground is its tint, the least contrast the report
                // has — and that colour keeps the readable contrast body text keeps (UX-8, measured from the
                // computed colours over the painted ground), muted beside the ink's.
                const within = report(page, 'server').locator('.ex-scroller');
                // A screenshot reads only what the viewport shows, and the server's report is the page's
                // second: under the built-in Chrome it starts below the fold. Its first row is brought into
                // view before its pixels are read.
                await values.first().scrollIntoViewIfNeeded();
                const muted = await inkOf(page, values, within);
                const staleInk = inkOver(stale, muted.ground);
                expect(paints(muted.inked, staleInk), `${muted.inked} over ${muted.ground}: the stale colour paints ${staleInk}`).toBe(true);
                expect(paints(muted.inked, inkOver(ink, muted.ground)), 'not the ink').toBe(false);
                const readable = contrast(staleInk, muted.ground);
                expect(readable, `${staleInk} over ${muted.ground}`).toBeGreaterThanOrEqual(4.5);
                expect(readable, 'muted beside the ink').toBeLessThan(contrast(inkOver(ink, muted.ground), muted.ground));

                // Reconnected and retried: the newest is shown, the notice goes, and the mark with it.
                await page.locator('#pivot-live-server-cut').click();
                await expect(page.locator('#pivot-live-server-cut')).toHaveText('Cut the server off');
                await notice.getByRole('button', { name: 'Retry' }).click();
                await expect(shell).not.toHaveClass(/ex-pivot-report-stale/);
                await expect(notice).toHaveText('');
                await expect(marked(page, 'server')).toHaveCount(0, { timeout: 10_000 });
                await page.mouse.move(0, 0);
                await circuitQuiet();
                expect(await colourOf(values.first())).toBe(ink);
                await values.first().scrollIntoViewIfNeeded();
                const current = await inkOf(page, values, within);
                const currentInk = inkOver(ink, current.ground);
                expect(paints(current.inked, currentInk), `${current.inked} over ${current.ground}: the ink paints ${currentInk}`).toBe(true);
            });
        }

        test(`PV-36/ADR-0067: the server's changing data marks the values it changed, and the page turns its live updates off (${chrome})`, async ({ page }) => {
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

test('ADR-0069/0067: the code the page shows is the code it runs: a Change Batch by Record Key, and a notice passed on', async ({ page }) => {
    await open(page, 'builtin');
    const code = await expectCodeIsSource(page);
    expect(code['DemoPivotData.cs#fields']).toContain('.Key("Id", t => t.Id)');
    expect(code['PivotLivePage.razor#batch']).toContain('_local.Apply(DemoPivotData.Fields.Batch(changed: amended))');
    expect(code['PivotLivePage.razor#server']).toContain('server.NotifyChanged(version)');
    expect(code['PivotLivePage.razor#pivots']).toContain('RedrawInterval="RedrawInterval" ChangeHighlightDuration="Highlight"');
});
