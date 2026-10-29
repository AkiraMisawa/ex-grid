import { readFile } from 'node:fs/promises';
import { test, expect } from './fixtures.mjs';

// ExChart.MudBlazor on /chart-dock (ADR-0258, ADR-0281): a curve history of eight dates (Date, then
// 1Y, 2Y, 5Y and 10Y under "Rates") in a Chart Dock, which opens each chart the grid's context menu
// makes as a tab beside the grid; and a chart of four curves in the grid Wrapper's paper under
// MudChartChrome.

const grid = (page) => page.locator('.ex-grid').first();
const dock = (page) => page.locator('.mud-ex-chart-dock');
const tabs = (page) => dock(page).locator('.mud-ex-chart-dock-panel .mud-tab');
const paperChart = (page) => page.locator('#mud-chart .ex-chart');

test.beforeEach(async ({ page }) => {
    await page.goto('/chart-dock');
    await expect(grid(page).locator("[id$='r7c0']")).toBeVisible();
    await expect(paperChart(page)).not.toHaveAttribute('aria-busy', 'true');
});

/** Selects from one cell to another, opens the grid's context menu on the first and picks an item. */
async function chart(page, [r1, c1], [r2, c2], words) {
    await grid(page).locator(`[id$='r${r1}c${c1}']`).click({ force: true });
    await grid(page).locator(`[id$='r${r2}c${c2}']`).click({ force: true, modifiers: ['Shift'] });
    await grid(page).locator(`[id$='r${r1}c${c1}']`).click({ button: 'right', force: true });
    await grid(page).locator('[role=menu] [role=menuitem]').filter({ hasText: words }).click();
}

test('ADR-0258, ADR-0281: the Chart Dock opens each chart made from the selection as a tab beside the grid', async ({ page }) => {
    await expect(dock(page).locator('.mud-ex-chart-dock-panel')).toHaveCount(0);
    await chart(page, [0, 1], [3, 2], /^Chart$/);
    await expect(tabs(page)).toHaveText(['Chart 1']);
    await expect(dock(page).locator('.ex-chart g.ex-chart-series')).toHaveCount(4);

    await chart(page, [4, 1], [4, 4], /^Chart$/);
    await expect(tabs(page)).toHaveText(['Chart 1', 'Chart 2']);
    // The panel stands beside the grid, never over it.
    const gridBox = await grid(page).boundingBox();
    const panelBox = await dock(page).locator('.mud-ex-chart-dock-panel').boundingBox();
    expect(panelBox.x).toBeGreaterThanOrEqual(gridBox.x + gridBox.width);

    // Every open tab is a target of "Add to chart".
    await grid(page).locator("[id$='r5c1']").click({ force: true });
    await grid(page).locator("[id$='r5c1']").click({ button: 'right', force: true });
    await expect(grid(page).locator('[role=menu] [role=menuitem]').filter({ hasText: /^Add to/ })).toHaveCount(2);
});

test('ADR-0281: the MudBlazor context menu holds ExChart\'s commands, and Escape closes it', async ({ page }) => {
    const plot = paperChart(page).locator('.ex-chart-hit').first();
    await plot.click({ button: 'right', position: { x: 40, y: 40 } });
    const menu = paperChart(page).locator('.ex-chart-menu[role=menu]');
    await expect(menu.locator('.mud-ex-chart-menu [role=menuitem]')).toHaveText(['Reset range', 'View as table', 'Download CSV', 'Download SVG', 'Download PNG']);
    await expect(menu).toBeFocused();
    await page.keyboard.press('Escape');
    await expect(menu).toHaveCount(0);
});

test('ADR-0281, ADR-0282: Download CSV from the context menu saves the file and closes the menu', async ({ page }) => {
    await paperChart(page).locator('.ex-chart-hit').first().click({ button: 'right', position: { x: 40, y: 40 } });
    const [download] = await Promise.all([
        page.waitForEvent('download'),
        paperChart(page).locator('.ex-chart-menu [role=menuitem]').filter({ hasText: 'Download CSV' }).click(),
    ]);
    expect(download.suggestedFilename()).toBe('curves.csv');
    const lines = (await readFile(await download.path())).toString('utf8').replace(/^﻿/, '').trim().split(/\r?\n/);
    expect(lines).toHaveLength(9);
    await expect(paperChart(page).locator('.ex-chart-menu')).toHaveCount(0);
});

test('ADR-0281, ADR-0282: the toolbar\'s downloads save the PNG', async ({ page }) => {
    const [download] = await Promise.all([
        page.waitForEvent('download'),
        paperChart(page).locator('.mud-ex-chart-downloads [data-ex-command="exchart.download.png"]').click(),
    ]);
    expect(download.suggestedFilename()).toBe('curves.png');
});

test('ADR-0281: the theme\'s Primary, Secondary and Tertiary lead the series, and follow dark mode', async ({ page }) => {
    const colours = async () => paperChart(page).evaluate((root) => {
        const style = getComputedStyle(document.querySelector('.mud-ex-grid'));
        const probe = (value) => {
            const span = document.createElement('span');
            span.style.color = value;
            root.appendChild(span);
            const resolved = getComputedStyle(span).color;
            span.remove();
            return resolved;
        };
        const slot = (n) => getComputedStyle(root.querySelector(`g.ex-chart-series.ex-chart-colour-${n}`)).color;
        return {
            slots: [slot(0), slot(1), slot(2)],
            palette: ['primary', 'secondary', 'tertiary'].map((name) => probe(style.getPropertyValue(`--mud-palette-${name}`))),
        };
    });
    const light = await colours();
    expect(light.slots).toEqual(light.palette);
    await page.locator('#toggle-dark').click();
    await expect.poll(async () => (await colours()).slots).not.toEqual(light.slots);
    const dark = await colours();
    expect(dark.slots).toEqual(dark.palette);
});

test('ADR-0281, ADR-0282: a saved SVG carries the theme\'s Primary on the first slot', async ({ page }) => {
    const [download] = await Promise.all([
        page.waitForEvent('download'),
        paperChart(page).locator('.mud-ex-chart-downloads [data-ex-command="exchart.download.svg"]').click(),
    ]);
    const svg = (await readFile(await download.path())).toString('utf8');
    const primary = await page.evaluate(() => getComputedStyle(document.querySelector('.mud-ex-grid')).getPropertyValue('--mud-palette-primary').trim());
    expect(primary).not.toBe('');
    expect(svg).toContain(`--ex-chart-colour-0:${primary};`);
});
