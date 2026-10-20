import { test, expect, circuitQuiet } from './fixtures.mjs';

// The Chart Spec on /chart-spec (tickets 70 to 75, ADR-0320, ADR-0321). An American put's 4-step lattice,
// as a node table and a branch table, drawn from the spec in the field: coloured by value in the heat
// bands, sized by the chance of reaching each node, its twenty branches as edges. The tool's answer is
// written under the field; a second button loads a spec with numbers in it.

const chart = (page) => page.locator('.ex-chart');
const answer = (page) => page.locator('#answer');

test.beforeEach(async ({ page }) => {
    await page.goto('/chart-spec');
    await expect(chart(page)).not.toHaveAttribute('aria-busy', 'true');
    await page.mouse.move(0, 0);
});

/** Every circle marker of the nodes series: its centre and radius in the SVG's pixels, and its band class. */
async function markers(page) {
    const paths = chart(page).locator('g.ex-chart-series[data-series="nodes"] path.ex-chart-markers');
    const found = [];
    for (const path of await paths.all()) {
        const d = await path.getAttribute('d');
        const cls = await path.getAttribute('class');
        for (const [, x, y, r] of d.matchAll(/M(-?[\d.]+),(-?[\d.]+)a([\d.]+),/g))
            found.push({ x: Number(x) + Number(r), y: Number(y), r: Number(r), cls });
    }
    return found;
}

test('ADR-0320: the spec\'s colour paints the nodes in heat bands, its size scales them, and both keys are drawn', async ({ page }) => {
    const marks = await markers(page);
    expect(marks).toHaveLength(15);
    // The put is worth most at the lowest spot and nothing at the highest: both ends of the run are painted.
    const bands = new Set(marks.map(m => m.cls.match(/ex-chart-heat-(\d)/)?.[1]));
    expect(bands).toContain('0');
    expect(bands).toContain('8');
    const fill = (cls) => chart(page).locator(`path.ex-chart-heat-${cls}`).first().evaluate(e => getComputedStyle(e).fill);
    expect(await fill(0)).not.toEqual(await fill(8));
    // The root is reached for certain, so it is the largest; every node is drawn at a size of its own share.
    const root = marks.reduce((a, b) => (b.x < a.x ? b : a));
    expect(Math.max(...marks.map(m => m.r))).toBe(root.r);
    expect(Math.min(...marks.map(m => m.r))).toBeLessThan(root.r / 2);
    await expect(chart(page).locator('.ex-chart-colour-key .ex-chart-key-title')).toHaveText('Value');
    await expect(chart(page).locator('.ex-chart-size-key .ex-chart-key-title')).toHaveText('Chance of reaching');
    await expect(answer(page)).toContainText('Nodes: colour is Value');
});

test('ADR-0321: the branch table is drawn as edges between the nodes', async ({ page }) => {
    const d = await chart(page).locator('g.ex-chart-series[data-series="nodes"] path.ex-chart-edges').getAttribute('d');
    expect([...d.matchAll(/M/g)]).toHaveLength(20);
    await expect(answer(page)).toContainText('15 nodes and 20 edges');
});

test('ADR-0320: the tooltip on a node reads its colour\'s and its size\'s values', async ({ page }) => {
    const root = (await markers(page)).reduce((a, b) => (b.x < a.x ? b : a));
    await chart(page).scrollIntoViewIfNeeded();
    const box = await chart(page).locator('svg').first().boundingBox();
    await page.mouse.move(box.x + root.x, box.y + root.y);
    const tooltip = chart(page).locator('.ex-chart-tooltip');
    await expect(tooltip.locator('.ex-chart-tooltip-position')).toHaveText('0:0');
    await expect(tooltip).toContainText('Value ');
    await expect(tooltip).toContainText('Chance of reaching 1');
});

test('ADR-0320: a spec with numbers in it is refused with its path and reason, and no chart is drawn', async ({ page }) => {
    await page.locator('#load-numbers').click();
    await circuitQuiet();
    await expect(answer(page)).toHaveText(/^series\[0\]\.values is not part of a Chart Spec/);
    await expect(chart(page)).toHaveCount(0);
    await page.locator('#load-lattice').click();
    await expect(chart(page)).toHaveCount(1);
});

test('ADR-0320: a spec edited in the field is read again when the field is left', async ({ page }) => {
    const field = page.locator('#spec');
    const text = await field.inputValue();
    await field.fill(text.replace('"scale": "sequential"', '"scale": "diverging"'));
    await field.press('Tab');
    await circuitQuiet();
    await expect(answer(page)).toHaveText(/^series\[0\]\.colour\.centre is missing/);
    await field.fill(text.replace('"scale": "sequential"', '"scale": "diverging", "centre": 5'));
    await field.press('Tab');
    await circuitQuiet();
    await expect(chart(page).locator('path.ex-chart-markers[class*="ex-chart-diverge-"]').first()).toBeAttached();
});
