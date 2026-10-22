import { test, expect } from './fixtures.mjs';

// ExChart's 3D surface on /surface (tickets 30-34): five deltas by eight tenors, with a hole at
// 25C 5Y. The page keeps the view and the slice, and writes the last event raised under the surface
// (ADR-0272).

const surface = (page) => page.locator('#vol .ex-chart-surface');
const box = (page) => surface(page).locator('.ex-chart-surface-box');
const events = (page) => page.locator('#surface-events');

test.beforeEach(async ({ page }) => {
    await page.goto('/surface');
    await expect(box(page).locator('polygon.ex-chart-surface-face')).not.toHaveCount(0);
    await expect(surface(page)).not.toHaveAttribute('aria-busy', 'true');
    await page.mouse.move(0, 0);
});

/** The page point of a grid point's dot, as the 3D entry drew it. */
async function dot(page, index) {
    const circle = box(page).locator('circle.ex-chart-surface-point').nth(index);
    const frame = await box(page).boundingBox();
    const cx = Number(await circle.getAttribute('cx'));
    const cy = Number(await circle.getAttribute('cy'));
    return [frame.x + cx, frame.y + cy];
}

test('CH-7, ADR-0274: a missing value is a hole: its point is not drawn, nor the four faces round it', async ({ page }) => {
    await expect(box(page).locator('circle.ex-chart-surface-point')).toHaveCount(5 * 8 - 1);
    await expect(box(page).locator('polygon.ex-chart-surface-face')).toHaveCount(4 * 7 - 4);
    await expect(box(page)).toHaveAttribute('aria-label', /with 1 hole where the data has no value\.$/);
});

test('ADR-0274: the tooltip names the grid point nearest the pointer, each value through its format', async ({ page }) => {
    const [x, y] = await dot(page, 0);
    await page.mouse.move(x + 2, y + 1);
    const tooltip = surface(page).locator('.ex-chart-surface-tooltip');
    await expect(tooltip.locator('.ex-chart-tooltip-value')).toHaveText(['10P', '1W', /^\d+\.\d{2}%$/]);
    await page.mouse.move(0, 0);
    await expect(tooltip).toHaveCount(0);
});

/** The view the page last wrote, as numbers. */
async function viewOf(page) {
    const text = await events(page).textContent();
    const [, yaw, pitch, zoom, panX, panY] = /^View: yaw (\S+), pitch (\S+), zoom (\S+), pan (\S+) (\S+)$/.exec(text);
    return { yaw: Number(yaw), pitch: Number(pitch), zoom: Number(zoom), panX: Number(panX), panY: Number(panY) };
}

/** The wall polygons' corners, floor first, as the 3D entry drew them. */
async function walls(page) {
    const all = await box(page).locator('.ex-chart-surface-walls polygon.ex-chart-surface-wall').evaluateAll((polygons) =>
        polygons.map((p) => p.getAttribute('points').split(' ').map((xy) => xy.split(',').map(Number))));
    return all;
}

test('ADR-0274, ADR-0272: a drag turns the surface, a turn for the box\'s height, and the view is raised when it ends', async ({ page }) => {
    const before = await box(page).locator('polygon.ex-chart-surface-face').first().getAttribute('points');
    const frame = await box(page).boundingBox();
    await page.mouse.move(frame.x + 40, frame.y + 40);
    await page.mouse.down();
    await page.mouse.move(frame.x + 140, frame.y + 60, { steps: 5 });
    await page.mouse.up();
    await expect(events(page)).toHaveText(/^View: /);
    const view = await viewOf(page);
    expect(view.yaw).toBeCloseTo(-35 + (100 * 360) / frame.height, 0);
    expect(view.pitch).toBeCloseTo(28 + (20 * 360) / frame.height, 0);
    expect(await box(page).locator('polygon.ex-chart-surface-face').first().getAttribute('points')).not.toBe(before);
});

test('ADR-0274: the surface is seen from above, so the walls behind it meet at the floor\'s far corner', async ({ page }) => {
    const [floor, wallY, wallX] = await walls(page);
    // The far corner is the floor's highest on screen; both walls stand on it.
    const far = floor.reduce((best, p) => (p[1] < best[1] ? p : best));
    const on = (wall) => wall.some((p) => Math.abs(p[0] - far[0]) < 0.2 && Math.abs(p[1] - far[1]) < 0.2);
    expect(on(wallY)).toBe(true);
    expect(on(wallX)).toBe(true);
});

test('ADR-0274: a drag to the right brings the near side of the surface to the right, and a drag down tips its top toward the viewer', async ({ page }) => {
    // 10P at 1W is the corner nearest the viewer at the first view.
    const [x0] = await dot(page, 0);
    const frame = await box(page).boundingBox();
    await page.mouse.move(frame.x + 40, frame.y + 40);
    await page.mouse.down();
    await page.mouse.move(frame.x + 60, frame.y + 40, { steps: 4 });
    await page.mouse.up();
    await expect(events(page)).toHaveText(/^View: /);
    const [x1] = await dot(page, 0);
    expect(x1).toBeGreaterThan(x0 + 5);

    // Seen from higher, the floor's far corner rises against its near one: the floor opens up.
    const span = (floor) => Math.max(...floor.map((p) => p[1])) - Math.min(...floor.map((p) => p[1]));
    const open = span((await walls(page))[0]);
    await page.mouse.move(frame.x + 40, frame.y + 40);
    await page.mouse.down();
    await page.mouse.move(frame.x + 40, frame.y + 70, { steps: 4 });
    await page.mouse.up();
    await expect(events(page)).toHaveText(/^View: /);
    await expect.poll(async () => span((await walls(page))[0])).toBeGreaterThan(open + 5);
});

test('ADR-0274: the surface turns below its floor, to a pitch of -90, and seen from below its top is behind it', async ({ page }) => {
    const frame = await box(page).boundingBox();
    await page.mouse.move(frame.x + 40, frame.y + frame.height - 5);
    await page.mouse.down();
    await page.mouse.move(frame.x + 40, frame.y - frame.height, { steps: 8 });
    await page.mouse.up();
    await expect(events(page)).toHaveText(/pitch -90,/);
    await box(page).focus();
    for (let k = 0; k < 6; k++) await page.keyboard.press('ArrowDown');
    await expect(events(page)).toHaveText(/pitch -60,/);
    // Seen from below the far corner of the box's top is its lowest on screen; both walls stand on it.
    const [top, wallY, wallX] = await walls(page);
    const far = top.reduce((best, p) => (p[1] > best[1] ? p : best));
    const on = (wall) => wall.some((p) => Math.abs(p[0] - far[0]) < 0.2 && Math.abs(p[1] - far[1]) < 0.2);
    expect(on(wallY)).toBe(true);
    expect(on(wallX)).toBe(true);
});

test('ADR-0274: a drag with the right button pans and opens no menu, and draws no slice', async ({ page }) => {
    const frame = await box(page).boundingBox();
    await page.mouse.move(frame.x + 40, frame.y + 40);
    await page.mouse.down({ button: 'right' });
    await page.mouse.move(frame.x + 40 + frame.width / 10, frame.y + 40, { steps: 4 });
    await page.mouse.up({ button: 'right' });
    await expect(events(page)).toHaveText(/^View: yaw -35, pitch 28, zoom 1, pan /);
    const view = await viewOf(page);
    expect(view.panX).toBeCloseTo(0.1, 2);
    expect(view.panY).toBe(0);
    await expect(surface(page).locator('.ex-chart-surface-slice-chart')).toHaveCount(0);
});

test('ADR-0274: the arrow keys turn the surface, + zooms and Shift with an arrow pans', async ({ page }) => {
    await box(page).focus();
    await page.keyboard.press('ArrowRight');
    await expect(events(page)).toHaveText('View: yaw -30, pitch 28, zoom 1, pan 0 0');
    // Down tips the top toward the viewer, as a drag down does.
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('+');
    await page.keyboard.press('Shift+ArrowLeft');
    await expect(events(page)).toHaveText('View: yaw -30, pitch 33, zoom 1.25, pan -0.05 0');
    await page.keyboard.press('ArrowUp');
    await expect(events(page)).toHaveText('View: yaw -30, pitch 28, zoom 1.25, pan -0.05 0');
    await surface(page).locator('.ex-chart-surface-reset').click();
    await expect(events(page)).toHaveText('View: yaw -35, pitch 28, zoom 1, pan 0 0');
});

test('ADR-0274: the wheel zooms the surface by how far it moved', async ({ page }) => {
    const frame = await box(page).boundingBox();
    await page.mouse.move(frame.x + frame.width / 2, frame.y + frame.height / 2);
    await page.mouse.wheel(0, -100);
    await expect(events(page)).toHaveText('View: yaw -35, pitch 28, zoom 1.1, pan 0 0');
    // A trackpad's small steps add up to the same zoom as one step as far.
    for (let k = 0; k < 4; k++) await page.mouse.wheel(0, -25);
    await expect(events(page)).toHaveText('View: yaw -35, pitch 28, zoom 1.21, pan 0 0');
});

test('ADR-0274: a click on a grid point draws its slice beside the surface, and new data keeps it', async ({ page }) => {
    const [x, y] = await dot(page, 0);
    await page.mouse.click(x, y);
    await expect(events(page)).toHaveText(/^Slice: [XY] (10P|1W)$/);
    const chart = surface(page).locator('.ex-chart-surface-slice-chart .ex-chart');
    await expect(chart.locator('g.ex-chart-series')).toHaveCount(1);
    await expect(box(page).locator('.ex-chart-surface-slice polyline')).toHaveCount(1);
    const title = await chart.locator('.ex-chart-toolbar').getAttribute('aria-label');
    expect(title).toMatch(/^Volatility at (10P|1W)$/);

    await page.locator('#next-run').click();
    await expect(chart.locator('.ex-chart-toolbar')).toHaveAttribute('aria-label', title);
    await expect(box(page).locator('.ex-chart-surface-slice polyline')).toHaveCount(1);
});

test('ADR-0274: the slice through the hole breaks there, and the 2D chart has a gap', async ({ page }) => {
    // Turned square and seen from above, the 5Y line runs left to right; a click just right of its
    // first point falls on it.
    await box(page).focus();
    for (let k = 0; k < 7; k++) await page.keyboard.press('ArrowRight');
    for (let k = 0; k < 13; k++) await page.keyboard.press('ArrowDown');
    await expect(events(page)).toHaveText(/pitch 90/);
    const [x, y] = await dot(page, 6 * 5);
    await page.mouse.click(x + 6, y);
    await expect(events(page)).toHaveText('Slice: Y 5Y');
    await expect(box(page).locator('.ex-chart-surface-slice polyline')).toHaveCount(2);
});

test('ADR-0282: the drawing is handed over as SVG text with its colours resolved', async ({ page }) => {
    await page.locator('#svg-text').click();
    await expect(page.locator('#surface-svg')).toHaveText(/^SVG: \d+ characters, without classes$/);
});

test('ADR-0257: a surface the page interpolated names its method, draws no dot between the quotes, and keeps its hole', async ({ page }) => {
    const other = page.locator('#interpolated .ex-chart-surface');
    const otherBox = other.locator('.ex-chart-surface-box');
    await expect(otherBox.locator('polygon.ex-chart-surface-face')).not.toHaveCount(0);
    await expect(other.locator('.ex-chart-notes')).toHaveText('Interpolated by the Consumer: bilinear.');
    await expect(otherBox.locator('circle.ex-chart-surface-point')).toHaveCount(0);
    expect(await otherBox.locator('polygon.ex-chart-surface-face').count()).toBeLessThan(12 * 12);

    await otherBox.scrollIntoViewIfNeeded();
    const frame = await otherBox.boundingBox();
    const face = otherBox.locator('polygon.ex-chart-surface-face').last();
    const [fx, fy] = (await face.getAttribute('points')).split(' ')[0].split(',').map(Number);
    await page.mouse.move(frame.x + fx, frame.y + fy);
    await expect(other.locator('.ex-chart-surface-tooltip .ex-chart-tooltip-note')).toHaveText('interpolated, bilinear');
});

const boxIn = (page, id) => page.locator(`#${id} .ex-chart-surface-box`);

test('ADR-0310: a surface carries its colour bar, nine bands with their values beside them', async ({ page }) => {
    await expect(box(page).locator('rect.ex-chart-surface-bar-band')).toHaveCount(9);
    await expect(box(page).locator('.ex-chart-surface-bar text')).not.toHaveCount(0);
    await expect(box(page).locator('polygon.ex-chart-surface-face.ex-chart-heat-0')).not.toHaveCount(0);
});

test('ADR-0310: colours that diverge from zero paint below it red and above it blue, and the bar marks zero', async ({ page }) => {
    const pnl = boxIn(page, 'pnl');
    await expect(pnl.locator('polygon.ex-chart-surface-face')).toHaveCount(6 * 4);
    await expect(pnl.locator('polygon.ex-chart-surface-face[class*="ex-chart-heat-"]')).toHaveCount(0);
    await expect(pnl.locator('.ex-chart-surface-bar text').filter({ hasText: /^0$/ })).toHaveCount(1);
    const fill = (k) => pnl.locator(`rect.ex-chart-surface-bar-band.ex-chart-diverge-${k}`).evaluate((e) => getComputedStyle(e).fill);
    await expect.poll(() => fill(0)).toBe('rgb(178, 24, 43)');
    await expect.poll(() => fill(4)).toBe('rgb(247, 247, 247)');
    await expect.poll(() => fill(8)).toBe('rgb(33, 102, 172)');
});

test('ADR-0311: a mesh is drawn as its own triangles, with a dot at each node and none where the domain has a hole', async ({ page }) => {
    const mesh = boxIn(page, 'mesh');
    await expect(mesh.locator('polygon.ex-chart-surface-face')).toHaveCount(2 * 15);
    await expect(mesh.locator('circle.ex-chart-surface-point')).toHaveCount(25);
    const corners = await mesh.locator('polygon.ex-chart-surface-face').first().getAttribute('points');
    expect(corners.trim().split(/\s+/)).toHaveLength(3);
});

test('ADR-0311: a node is read in the tooltip, and a click on it slices through its position, cutting the triangles', async ({ page }) => {
    const section = page.locator('#mesh .ex-chart-surface');
    const mesh = boxIn(page, 'mesh');
    await expect(section).not.toHaveAttribute('aria-busy', 'true');
    await mesh.scrollIntoViewIfNeeded();
    const circle = mesh.locator('circle.ex-chart-surface-point').nth(12);
    const frame = await mesh.boundingBox();
    const [x, y] = [frame.x + Number(await circle.getAttribute('cx')), frame.y + Number(await circle.getAttribute('cy'))];
    await page.mouse.move(x, y);
    await expect(section.locator('.ex-chart-surface-tooltip .ex-chart-tooltip-value')).toHaveCount(3);
    await page.mouse.click(x, y);
    await expect(section.locator('.ex-chart-surface-slice-chart .ex-chart g.ex-chart-series')).toHaveCount(1);
    await expect(mesh.locator('.ex-chart-surface-slice polyline')).not.toHaveCount(0);
    await expect(section.locator('.ex-chart-surface-slice-chart')).toContainText("linear on the Consumer's triangles (P1)");
});

test('ADR-0312: an axis and the vertical scale placed by their logarithm are labelled at round values', async ({ page }) => {
    const log = boxIn(page, 'log');
    await expect(log.locator('.ex-chart-surface-labels text').filter({ hasText: /^10Y$/ })).toHaveCount(1);
    await expect(log.locator('.ex-chart-surface-walls text').filter({ hasText: /^10$/ })).toHaveCount(1);
    await expect(page.locator('#log .ex-chart-surface-box')).toHaveAttribute('aria-label', /The vertical scale is logarithmic\./);
});
