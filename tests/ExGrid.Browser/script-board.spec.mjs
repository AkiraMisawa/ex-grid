import { test, expect, circuitQuiet, dataRowCount, settled } from './fixtures.mjs';

// Blazor's ExBoard over its Script Core (ADR-0370 section 1), on /board?script=1, on both Blazor hosts: the one path
// no other layer sees whole. The Board crosses to the browser as JSON and each input's Snapshot as its Arrow stream
// (ADR-0065), read there by @ex-grid/data; the confirm crosses back as an output .NET reads against the Snapshot it
// was taken from; and a Diff the user makes crosses to .NET and back. What the Board draws and decides is the Script
// Core's, held by board.spec on the React host; the C# halves are held by ExBoard.Tests' ScriptBoardTests.

const view = (page, id) => page.locator(`[data-ex-view="${id}"]`);
const grid = (page, id) => view(page, id).locator('.ex-grid').first();
const cell = (page, id, text) => grid(page, id).locator('[role=gridcell]').filter({ hasText: new RegExp(`^${text}$`) }).first();

test('ADR-0370 section 1, ADR-0065: a Board drawn from Snapshots sent as Arrow hands its output and its Diff to .NET, and a Diff handed back lays over', async ({ page }) => {
    // Drawn by the Script Core's module, not by the Razor component the same page draws without ?script. The Server
    // host's import map names the module by its fingerprinted address.
    const module = page.waitForResponse(response => /\/_content\/ExBoard\/ex-board\.min\.(?:[a-z0-9]+\.)?js(?:\?|$)/.test(response.url()) && response.ok());
    await page.goto('/board?script=1');
    await module;
    await expect(view(page, 'daily').locator('svg').first()).toBeVisible({ timeout: 30_000 });
    await settled(grid(page, 'blotter'));
    // Both inputs crossed: the blotter's five trades and the limits grid's three desks.
    expect([await dataRowCount(grid(page, 'blotter')), await dataRowCount(grid(page, 'limits'))]).toEqual([5, 3]);

    // At machine speed: the press follows the click with no wait (spine rule 6). The output .NET writes is the one
    // the Razor component writes for the same press (board.spec).
    await cell(page, 'blotter', 'Chen').click({ force: true });
    await view(page, 'blotter').getByRole('button', { name: 'Send to review' }).click();
    await expect(page.locator('#raised')).toHaveText('{"output":"review","input":"trades","version":0,"on":["day","trader"],"values":[["2026-10-16","Chen"]]}');

    await grid(page, 'blotter').locator('[role=columnheader]').filter({ hasText: 'P&L' }).click({ force: true });
    await expect(page.locator('#diff')).toContainText('"sorts":[{"column":"pnl"');
    // A selection after the sort narrows the limits grid, over the other input, to FX's one desk.
    await cell(page, 'blotter', 'FX').click({ force: true });
    await expect.poll(async () => { await settled(grid(page, 'limits')); return dataRowCount(grid(page, 'limits')); }).toBe(1);

    // Drawn again with the Diff read back from its JSON and the same Snapshots in a new dictionary, which cross as
    // the numbers the browser holds: the Board is bound afresh, so the selection and its narrowing are gone, and the
    // sort is laid over it.
    await page.locator('#again').click();
    await expect.poll(async () => { await circuitQuiet(); await settled(grid(page, 'limits')); return dataRowCount(grid(page, 'limits')); }).toBe(3);
    await settled(grid(page, 'blotter'));
    await expect(grid(page, 'blotter').locator('[role=row]:has([role=gridcell])').first()).toContainText('(42,000)');
    await expect(page.locator('.ex-board-dropped')).toHaveCount(0);
});

test('ADR-0343, ADR-0370 section 1: a Board over a source held in .NET fetches through it, narrows it, thins its chart there and hands on what was chosen', async ({ page }) => {
    // /board-server?script=1: 50,000 trades held by the Consumer's C# source, which the browser asks through .NET for
    // a page, a narrowed page and a chart's thinned points; the confirm crosses back with no Snapshot, read as the
    // input declares it. What the Board does over a source is the Script Core's, held by board-server.spec on Razor.
    const line = async () => { await circuitQuiet(); return view(page, 'curve').locator('g.ex-chart-series[data-series="pnl"] path').first().getAttribute('d'); };
    const desks = async () => {
        await circuitQuiet();
        await settled(grid(page, 'desk'));
        return [...new Set((await grid(page, 'desk').locator('[role=gridcell]').allTextContents()).filter(t => /^(Commodities|Credit|Equities|FX|Rates)$/.test(t)))].sort();
    };
    await page.goto('/board-server?script=1');
    await expect(cell(page, 'blotter', 'T-00001')).toBeVisible({ timeout: 30_000 });
    await settled(grid(page, 'blotter'));
    await expect.poll(async () => [...((await line()) ?? '').matchAll(/[ML]/g)].length).toBeGreaterThan(250);
    const whole = await line();

    await cell(page, 'blotter', 'T-00002').click({ force: true });
    await expect.poll(desks).toEqual(['FX']);
    await expect.poll(line).not.toBe(whole);

    await view(page, 'blotter').getByRole('button', { name: 'Send to review' }).click();
    await expect(page.locator('#raised')).toHaveText('{"output":"review","input":"trades","version":1,"on":["id"],"values":[["T-00002"]]}');
    await expect(page.locator('.ex-board-dropped')).toHaveCount(0);
});
