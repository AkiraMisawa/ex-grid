import { test, expect, circuitQuiet, dataRowCount, settled } from './fixtures.mjs';

// The Board on /board (ADR-0340, ExBoard ticket 05). A blotter of five trades, a pivot by trader and a
// chart by day over the same input, and a limits grid over a second input; a link from the blotter to
// the other three on "desk". A click on a blotter row narrows them to its desk; a Ctrl+click adds the
// next row's desk; a sort of the blotter drops the selection, and the narrowing goes with it (ADR-0011).

const view = (page, id) => page.locator(`[data-ex-view="${id}"]`);
const grid = (page, id) => view(page, id).locator('.ex-grid').first();
const cell = (page, id, text) => grid(page, id).locator('[role=gridcell]').filter({ hasText: new RegExp(`^${text}$`) }).first();

/** The x positions of the chart's markers, from its observation series' path. */
async function points(page) {
    const d = await view(page, 'daily').locator('path.ex-chart-markers').first().getAttribute('d');
    return [...(d ?? '').matchAll(/M/g)].length;
}

/** What every view shows, once the page and the host are still. */
async function shown(page) {
    await circuitQuiet();
    for (const id of ['blotter', 'byTrader', 'limits'])
        await settled(grid(page, id));
    return {
        limits: await dataRowCount(grid(page, 'limits')),
        traders: (await grid(page, 'byTrader').locator('[role=rowheader], [role=gridcell]').allTextContents()).filter(t => /^[A-Z][a-z]+$/.test(t)),
        points: await points(page),
    };
}

test.beforeEach(async ({ page }) => {
    await page.goto('/board');
    await expect(view(page, 'daily').locator('svg').first()).toBeVisible();
    await settled(grid(page, 'blotter'));
});

test('ADR-0340: a selected row narrows the pivot, the chart and a grid over another input to its desk', async ({ page }) => {
    await expect.poll(() => shown(page)).toEqual({ limits: 3, traders: ['Aoki', 'Brandt', 'Chen', 'Dubois'], points: 5 });

    await cell(page, 'blotter', 'FX').click({ force: true });

    await expect.poll(() => shown(page)).toEqual({ limits: 1, traders: ['Aoki', 'Brandt'], points: 2 });
    await expect(cell(page, 'limits', 'FX')).toBeVisible();
});

test('ADR-0340: a Ctrl+click adds the next row\'s desk; a sort drops the selection and its narrowing (ADR-0011)', async ({ page }) => {
    await cell(page, 'blotter', 'FX').click({ force: true });
    await cell(page, 'blotter', 'Credit').click({ force: true, modifiers: ['ControlOrMeta'] });

    await expect.poll(() => shown(page)).toEqual({ limits: 2, traders: ['Aoki', 'Brandt', 'Dubois'], points: 3 });

    await grid(page, 'blotter').locator('[role=columnheader]').filter({ hasText: 'P&L' }).click({ force: true });

    await expect.poll(() => shown(page)).toEqual({ limits: 3, traders: ['Aoki', 'Brandt', 'Chen', 'Dubois'], points: 5 });
});
