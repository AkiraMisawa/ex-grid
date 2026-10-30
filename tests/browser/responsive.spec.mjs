import { test, expect, circuitQuiet } from './fixtures.mjs';

test('RT-1 (ADR-0045): a container narrows and widens without changing columns or saved pinning @blazor', async ({ page }) => {
    await page.goto('/responsive');
    for (const layout of ['flex', 'grid'])
        await expect(page.locator(`#responsive-${layout} .ex-row`).first()).toBeVisible();
    const windowWidth = await page.evaluate(() => window.innerWidth);
    await page.locator('#narrow').click();
    for (const layout of ['flex', 'grid']) {
        const frame = page.locator(`#responsive-${layout}`);
        const grid = frame.locator('.ex-grid');
        await expect.poll(async () => (await frame.locator('.ex-scroller').boundingBox()).width).toBeCloseTo(200, 0);
        await expect(grid.locator('.ex-cell.ex-pinned')).toHaveCount(0);
        const headers = await grid.locator('.ex-header-cell').evaluateAll(cells => cells.map(c => ({ text: c.childNodes[0].textContent.trim(), width: c.getBoundingClientRect().width })));
        expect(headers.map(c => c.width)).toEqual([120, 140]);
        expect(headers.map(c => c.text)).toEqual(['Book', 'Trader']);
        const scroller = grid.locator('.ex-scroller');
        await scroller.evaluate(el => { el.scrollLeft = el.scrollWidth; });
        await circuitQuiet();
        await expect.poll(async () => {
            const last = await grid.getByRole('columnheader', { name: 'Id Column menu', exact: true }).boundingBox();
            const view = await scroller.boundingBox();
            return last.x >= view.x && last.x + last.width <= view.x + view.width;
        }).toBe(true);
        expect((await grid.getByRole('columnheader', { name: 'Id Column menu', exact: true }).boundingBox()).width).toBe(180);
        expect((await grid.getByRole('columnheader', { name: 'Notional Column menu', exact: true }).boundingBox()).width).toBe(180);
    }
    await page.locator('#wide').click();
    for (const layout of ['flex', 'grid']) {
        const grid = page.locator(`#responsive-${layout} .ex-grid`);
        await expect.poll(async () => (await grid.locator('.ex-scroller').boundingBox()).width).toBeCloseTo(760, 0);
        await expect(grid.locator('.ex-cell.ex-pinned').first()).toBeVisible();
        expect(await grid.locator('.ex-header-cell').evaluateAll(cells => cells.map(c => c.getBoundingClientRect().width))).toEqual([120, 140, 180, 180]);
    }
    await expect(page.locator('#responsive-state')).toHaveText('Requested pinned columns: 2; layout notifications: 0');
    expect(await page.evaluate(() => window.innerWidth)).toBe(windowWidth);
    expect((await page.locator('#responsive-fixed .ex-scroller').boundingBox()).width).toBe(600);
});
