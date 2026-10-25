import { test, expect } from './fixtures.mjs';

// Candles and intervals on /chart-candles (tickets 60 to 66): ten years of a synthetic daily index,
// 2016-01-04 to 2025-12-31, with volume in a panel below and a 25-day average, and a scatter of
// strategies with intervals both ways. The tooltip's readings, a merged candle's click, packed sessions
// and the band and whiskers are held by the engine's tests and by chart-showcases.spec.mjs.

const index = (page) => page.locator('#index .ex-chart');
const layer = (chart, panel) => chart.locator(`.ex-chart-hit[data-ex-panel="${panel}"]`);

test.beforeEach(async ({ page }) => {
    await page.goto('/chart-candles');
    await expect(index(page).locator('.ex-chart-rising').first()).toBeAttached();
    await page.mouse.move(0, 0);
});

test('ADR-0300, ADR-0302: ten years merge into months, rising hollow and falling filled, with volume below', async ({ page }) => {
    const chart = index(page);
    await expect(chart.locator('.ex-chart-notes')).toContainText('Index: monthly candles, merged from rows to fit.');
    // Candles and volume bars: one path each for rising and falling, in each of the two panels.
    await expect(chart.locator('path.ex-chart-rising')).toHaveCount(2);
    await expect(chart.locator('path.ex-chart-falling')).toHaveCount(2);
    await expect(layer(chart, 1)).toBeAttached();
    // Shape first: a rising body is the background's colour, a falling one the series' own.
    const fills = await chart.locator('path.ex-chart-candles').evaluateAll(ps => ps.map(p => getComputedStyle(p).fill));
    expect(new Set(fills.slice(0, 2)).size, 'rising and falling bodies differ').toBe(2);
});

test('ADR-0304: a scatter\'s intervals run both ways through each mark, and the tooltip names both', async ({ page }) => {
    const chart = page.locator('#estimates .ex-chart');
    await chart.scrollIntoViewIfNeeded();
    const whiskers = await chart.locator('path.ex-chart-whiskers').getAttribute('d');
    expect(whiskers.match(/V/g)).toHaveLength(6);
    expect(whiskers.match(/H/g)).toHaveLength(6);
    // The highest mark is Trend's.
    const d = await chart.locator('path.ex-chart-markers').first().getAttribute('d');
    let best = null;
    for (const [, x, y] of d.matchAll(/M(-?[\d.]+),(-?[\d.]+)/g)) {
        if (best === null || Number(y) < best[1]) best = [Number(x), Number(y)];
    }
    const box = await chart.locator('svg').first().boundingBox();
    await page.mouse.move(box.x + best[0] + 2, box.y + best[1] + 2);
    const tooltip = chart.locator('.ex-chart-tooltip');
    await expect(tooltip.locator('.ex-chart-tooltip-position')).toHaveText('Trend');
    await expect(tooltip).toContainText('y [-0.1, 14.3] 95% CI');
    await expect(tooltip).toContainText('x [12.9, 16.5] 95% CI');
    await expect(chart.locator('.ex-chart-legend')).toContainText('Strategies (95% CI)');
});

