import { test, expect } from './fixtures.mjs';

// Candles and intervals on /chart-candles (tickets 60 to 66): ten years of a synthetic daily index,
// 2016-01-04 to 2025-12-31, with volume in a panel below and a 25-day average; a yield curve with a
// Bid–Ask band; rate forecasts with 95% CI whiskers. The page writes the last event under the index.

const index = (page) => page.locator('#index .ex-chart');
const layer = (chart, panel) => chart.locator(`.ex-chart-hit[data-ex-panel="${panel}"]`);

test.beforeEach(async ({ page }) => {
    await page.goto('/chart-candles');
    await expect(index(page).locator('.ex-chart-rising').first()).toBeAttached();
    await page.mouse.move(0, 0);
});

async function pointIn(locator, fx, fy = 0.5) {
    await locator.scrollIntoViewIfNeeded();
    const box = await locator.boundingBox();
    expect(box, 'the plot is on screen').not.toBeNull();
    return [box.x + box.width * fx, box.y + box.height * fy];
}

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

test('ADR-0300: the tooltip reads a candle\'s four values, its change and its volume', async ({ page }) => {
    const chart = index(page);
    const [x, y] = await pointIn(layer(chart, 0), 0.5);
    await page.mouse.move(x, y);
    const tooltip = chart.locator('.ex-chart-tooltip');
    await expect(tooltip).toBeVisible();
    await expect(tooltip).toContainText(/O [\d,]+\.\d{2} H [\d,]+\.\d{2} L [\d,]+\.\d{2} C [\d,]+\.\d{2}/);
    await expect(tooltip).toContainText(/Chg [+-][\d,]+\.\d{2} \([+-]\d+\.\d{2}%\)/);
    await expect(tooltip).toContainText(/Vol [\d,]+/);
    await expect(tooltip).toContainText(/rows/);
});

test('ADR-0302: a click on a merged candle narrows to its month, and the days come back', async ({ page }) => {
    const chart = index(page);
    // The average hidden, the candle is the only series a click can name.
    await chart.locator('.ex-chart-legend-entry', { hasText: '25-day average' }).click();
    const [x, y] = await pointIn(layer(chart, 0), 0.5);
    await page.mouse.click(x, y);
    await expect(page.locator('#index-events')).toHaveText(/^range: (\d{4})-(\d{2})-\d{2} to \1-\2-\d{2}$/);
    await expect(chart.locator('.ex-chart-notes li', { hasText: 'merged' })).toHaveCount(0);
    // One row's candle now: a click names it.
    const [x2, y2] = await pointIn(layer(chart, 0), 0.5);
    await page.mouse.click(x2, y2);
    await expect(page.locator('#index-events')).toHaveText(/^clicked index at \d{4}-\d{2}-\d{2}$/);
});

test('ADR-0301: packed sessions say so', async ({ page }) => {
    const chart = index(page);
    await expect(chart.locator('.ex-chart-notes li', { hasText: 'packed' })).toHaveCount(0);
    await page.locator('#packed').check();
    await expect(chart.locator('.ex-chart-notes')).toContainText('Dates are packed');
});

test('ADR-0303: a Bid–Ask band and 95% CI whiskers, named in the tooltip', async ({ page }) => {
    const curve = page.locator('#curve .ex-chart');
    await expect(curve.locator('path.ex-chart-interval-band')).toHaveCount(1);
    const forecasts = page.locator('#forecasts .ex-chart');
    await expect(forecasts.locator('path.ex-chart-whiskers')).toHaveCount(1);
    const [x, y] = await pointIn(layer(forecasts, 0), 0.5);
    await page.mouse.move(x, y);
    await expect(forecasts.locator('.ex-chart-tooltip')).toContainText(/\[\d\.\d{2}, \d\.\d{2}\] 95% CI/);
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

