import { test, expect, circuitQuiet, settled } from './fixtures.mjs';

// The Board on /board-server (ADR-0343, ExBoard ticket 12): its one input, 50,000 trades, is held on a
// server and fetched a page at a time. The blotter's selected rows narrow the desk grid and the pivot by
// desk: the server is handed the condition, and both are asked again. A selection reaching rows not fetched
// narrows nothing, and the Board says so under the blotter.

const view = (page, id) => page.locator(`[data-ex-view="${id}"]`);
const grid = (page, id) => view(page, id).locator('.ex-grid').first();
const cell = (page, id, text) => grid(page, id).locator('[role=gridcell]').filter({ hasText: new RegExp(`^${text}$`) }).first();

/** The desks the desk grid and the pivot show, once the page and the host are still. */
async function desks(page) {
    await circuitQuiet();
    for (const id of ['blotter', 'desk', 'byDesk'])
        await settled(grid(page, id));
    const shown = async id => [...new Set((await grid(page, id).locator('[role=rowheader], [role=gridcell]').allTextContents())
        .filter(t => /^(Commodities|Credit|Equities|FX|Rates)$/.test(t)))].sort();
    return { desk: await shown('desk'), pivot: await shown('byDesk') };
}

const all = ['Commodities', 'Credit', 'Equities', 'FX', 'Rates'];

test.beforeEach(async ({ page }) => {
    await page.goto('/board-server');
    await expect(cell(page, 'blotter', 'T-00001')).toBeVisible();
    await settled(grid(page, 'blotter'));
});

test('ADR-0343: a selected row hands the server its desk, and the desk grid and the pivot are asked again under it', async ({ page }) => {
    await expect.poll(() => desks(page)).toEqual({ desk: all, pivot: all });

    await cell(page, 'blotter', 'T-00002').click({ force: true });

    await expect.poll(() => desks(page)).toEqual({ desk: ['FX'], pivot: ['FX'] });
});

test('ADR-0343: a selection past the rows fetched narrows nothing and hands nothing on, and says so', async ({ page }) => {
    await cell(page, 'blotter', 'T-00002').click({ force: true });
    await expect.poll(() => desks(page)).toEqual({ desk: ['FX'], pivot: ['FX'] });

    await page.keyboard.press('ControlOrMeta+a');

    const said = 'The selection reaches rows not fetched from the source, so it narrows nothing and hands nothing on.';
    await expect(view(page, 'blotter').locator('.ex-board-link-status')).toHaveText(said);
    await expect.poll(() => desks(page)).toEqual({ desk: all, pivot: all });
    await view(page, 'blotter').getByRole('button', { name: 'Send to review' }).click();
    await expect(view(page, 'blotter').locator('.ex-board-confirm-status')).toHaveText(said);
    await expect(page.locator('#raised')).toHaveText('');
});

test('ADR-0343: the confirm hands on the fetched rows chosen, with the version they were read at', async ({ page }) => {
    await cell(page, 'blotter', 'T-00003').click({ force: true });
    await view(page, 'blotter').getByRole('button', { name: 'Send to review' }).click();

    await expect(page.locator('#raised')).toHaveText('{"output":"review","input":"trades","version":1,"on":["id"],"values":[["T-00003"]]}');
});

test('ADR-0343: a sort is the grid\'s query, executed by the server, and drops the selection and its narrowing (ADR-0011)', async ({ page }) => {
    await cell(page, 'blotter', 'T-00002').click({ force: true });
    await expect.poll(() => desks(page)).toEqual({ desk: ['FX'], pivot: ['FX'] });

    const trade = grid(page, 'blotter').locator('[role=columnheader]').filter({ hasText: 'Trade' });
    await trade.click({ force: true });
    await trade.click({ force: true });

    await expect(cell(page, 'blotter', 'T-50000')).toBeVisible();
    await expect.poll(() => desks(page)).toEqual({ desk: all, pivot: all });
});
