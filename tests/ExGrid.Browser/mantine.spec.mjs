import { test, expect } from './fixtures.mjs';
import { REACT } from './hosting.mjs';
import { expectKeyboardOn } from './keyboard.mjs';
import { contrast, painted, resolvedColour } from './pixels.mjs';

// @ex-grid/mantine on /mantine, the React host's alone (ADR-0390 section 7): what only a browser shows of a
// design-system Wrapper. The geometry stays what the grid declared under Mantine's own styles, the theme's colours
// paint the Focus and the selection in both schemes, and a menu drawn in Mantine's buttons is crossed by the
// keyboard as the core's table says. What every Wrapper does with a slot is the contract layer 2 runs against the
// Mantine Chrome; the products' specs are not copied per Wrapper.

test.skip(!REACT, '@ex-grid/mantine is React\'s: the Blazor hosts have no /mantine');

const paper = (page) => page.locator('.demo-paper-a');
const grid = (page) => paper(page).locator('[role=grid]');
const cell = (page, row, column) => grid(page).locator(`[id$='r${row}c${column}']`);

async function open(page) {
    await page.goto('/mantine');
    await expect(grid(page).locator('.ex-row').first()).toBeVisible();
    // The Wrapper's stylesheet has landed when its tokens reach the root.
    await expect.poll(() => grid(page).evaluate((g) => getComputedStyle(g).getPropertyValue('--ex-focus-outline').trim())).not.toBe('');
}

// Cells are pointer-events: none by design; the Viewport is the delegated target (ADR-0004).
const press = (page, row, column, modifiers = []) => cell(page, row, column).click({ force: true, modifiers });

test('UX-3 / ADR-0027: under Mantine\'s styles the painted geometry is what the grid declared, at either Density, in Mantine\'s default stack', async ({ page }) => {
    await open(page);
    const measured = () => grid(page).evaluate((g) => {
        const rows = [...g.querySelectorAll('.ex-viewport > .ex-row')];
        return {
            declared: {
                row: g.style.getPropertyValue('--ex-row-height'),
                header: g.style.getPropertyValue('--ex-header-height'),
                padding: g.style.getPropertyValue('--ex-cell-padding-x'),
            },
            row: getComputedStyle(rows[0]).height,
            pitch: `${rows[1].getBoundingClientRect().top - rows[0].getBoundingClientRect().top}px`,
            header: getComputedStyle(g.querySelector('.ex-header')).height,
            padding: getComputedStyle(rows[0].querySelector('.ex-cell')).paddingLeft,
            font: getComputedStyle(rows[0].querySelector('.ex-cell')).fontFamily,
        };
    });
    for (const density of ['compact', 'standard']) {
        if (density === 'standard') {
            await page.locator('#toggle-dense').click();
            await expect(page.locator('#density-status')).toHaveText('Density: standard');
        }
        const at = await measured();
        expect([at.row, at.pitch, at.header, at.padding], `${density}: ${JSON.stringify(at)}`)
            .toEqual([at.declared.row, at.declared.row, at.declared.header, at.declared.padding]);
        expect(at.font).toMatch(/^-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto/u);
    }
});

test('UX-9 / ADR-0390: the Focus outline and the selection fill are the theme\'s primary, and the outline reads 3:1 in light and dark', async ({ page }) => {
    await open(page);
    const colours = async () => {
        const css = await grid(page).evaluate((g) => {
            const focus = [...g.querySelectorAll('.ex-focus')].find((el) => el.getBoundingClientRect().width > 0);
            const tint = g.querySelector('.ex-range-tint');
            return {
                focus: getComputedStyle(focus, '::after').borderTopColor,
                fill: tint === null ? null : getComputedStyle(tint).backgroundColor,
                ground: getComputedStyle(g).backgroundColor,
                primary: getComputedStyle(g).getPropertyValue('--mantine-primary-color-filled'),
            };
        });
        return { ...css, focus: await resolvedColour(page, css.focus), ground: await resolvedColour(page, css.ground), primary: await resolvedColour(page, css.primary) };
    };
    let lightGround;
    for (const dark of [false, true]) {
        if (dark) {
            await page.locator('#toggle-dark').click();
            await expect(page.locator('#dark-status')).toHaveText('Dark: True');
        }
        await press(page, 2, 1);
        await expect(grid(page).locator('.ex-focus')).not.toHaveCount(0);
        const one = await colours();
        if (dark) {
            // The scheme reached the grid with no render: the ground is Mantine's dark body.
            expect(one.ground, JSON.stringify(one)).not.toEqual(lightGround);
        } else {
            lightGround = one.ground;
            expect(one.focus, JSON.stringify(one)).toEqual(one.primary);
        }
        expect(contrast(one.focus, one.ground), `the outline against the ground, dark=${dark} (${JSON.stringify(one)})`).toBeGreaterThanOrEqual(3);
        // As painted: the outline's top lies a pixel above the cell (ADR-0008), over the ground beside it.
        await page.mouse.move(0, 0);
        await expect(grid(page).locator('.ex-hover-row')).toHaveCount(0);
        const box = await cell(page, 2, 1).boundingBox();
        const pixels = await painted(page, { x: box.x, y: box.y - 4, width: box.width, height: box.height + 4 });
        const seen = { outline: pixels.at(box.x + box.width / 2, box.y - 1), ground: pixels.at(box.x + 6, box.y + 6) };
        expect(contrast(seen.outline, seen.ground), `painted, dark=${dark} (${JSON.stringify(seen)})`).toBeGreaterThanOrEqual(3);

        await press(page, 3, 2, ['Shift']);
        await expect(grid(page).locator('.ex-range-tint')).not.toHaveCount(0);
        const range = await colours();
        expect(range.fill, JSON.stringify(range)).not.toBe('rgba(0, 0, 0, 0)');
    }
});

test('KB-30 / ADR-0390: a column menu drawn in Mantine\'s buttons is crossed by the arrows, Home and End, and Enter runs its item', async ({ page }) => {
    await open(page);
    await press(page, 1, 0);
    await page.keyboard.press('Alt+ArrowDown');
    const menu = grid(page).locator('.ex-popover [role=menu]');
    await expect(menu).toBeVisible();
    const items = await menu.locator('button[role=menuitem]:not([disabled])').allTextContents();
    // Book's commands: its two sorts, and Clear filter, not enabled while nothing filters it.
    expect(items).toEqual(['Sort ascending', 'Sort descending']);
    const active = () => page.evaluate(() => document.activeElement?.textContent?.trim() ?? null);
    await expect.poll(active).toBe(items[0]);
    for (const [key, lands] of [['ArrowDown', 1], ['ArrowDown', 0], ['End', 1], ['Home', 0], ['ArrowUp', 1]]) {
        await page.keyboard.press(key);
        await expect.poll(active, `${key} lands on ${items[lands]}`).toBe(items[lands]);
    }
    await page.keyboard.press('Enter');
    await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
    await expect(grid(page).locator('.ex-header-cell[aria-sort=descending]')).toHaveCount(1);
    await expectKeyboardOn(grid(page));
});

// @ex-grid/chart-mantine on /mantine-chart: what only a browser shows of the Chart Dock under Mantine. The tabs, the
// targets, the refusals and the slots' commands are the contracts layer 2 runs against MantineChartDock.
test('ADR-0258, ADR-0390: the Mantine Chart Dock opens a chart beside the grid, never over it, and the theme\'s primary leads its series in light and dark', async ({ page }) => {
    await page.goto('/mantine-chart');
    const curves = page.locator('.mantine-ex-grid [role=grid]');
    const at = (row, column) => curves.locator(`[id$='r${row}c${column}']`);
    await expect(at(7, 0)).toBeVisible();
    await at(0, 0).click({ force: true });
    await at(3, 2).click({ force: true, modifiers: ['Shift'] });
    await at(0, 0).click({ button: 'right', force: true });
    // Dates and tenors both: the menu offers the two orientations, and Series in columns charts 1Y and 2Y along the dates.
    await curves.locator('[role=menu] [role=menuitem]').filter({ hasText: /^Series in columns$/ }).click();
    const panel = page.locator('.mantine-ex-chart-dock-panel');
    await expect(panel.locator('[role=tab]')).toHaveText(['Chart 1']);
    const chart = panel.locator('.ex-chart');
    await expect(chart.locator('g.ex-chart-series')).toHaveCount(2);
    await expect(chart.locator('[role=toolbar]')).toHaveAccessibleName('Chart 1');

    // Beside the grid, never over it, and the chart whole inside the panel.
    const [gridBox, panelBox, plotBox] = await Promise.all([curves.boundingBox(), panel.boundingBox(), chart.locator('svg.ex-chart-svg').boundingBox()]);
    expect(panelBox.x, JSON.stringify({ gridBox, panelBox })).toBeGreaterThanOrEqual(gridBox.x + gridBox.width);
    expect(plotBox.x + plotBox.width, JSON.stringify({ panelBox, plotBox })).toBeLessThanOrEqual(panelBox.x + panelBox.width);

    const colours = async () => {
        const css = await chart.evaluate((root) => {
            const theme = getComputedStyle(root.closest('.mantine-ex-chart'));
            return {
                series: getComputedStyle(root.querySelector('g.ex-chart-series.ex-chart-colour-0')).color,
                ground: getComputedStyle(root).backgroundColor,
                primary: theme.getPropertyValue('--mantine-primary-color-filled'),
                body: theme.getPropertyValue('--mantine-color-body'),
            };
        });
        return Object.fromEntries(await Promise.all(Object.entries(css).map(async ([name, colour]) => [name, await resolvedColour(page, colour)])));
    };
    const light = await colours();
    expect([light.series, light.ground], JSON.stringify(light)).toEqual([light.primary, light.body]);
    await page.locator('#toggle-dark').click();
    await expect(page.locator('#dark-status')).toHaveText('Dark: True');
    // The scheme reaches the chart: its ground is Mantine's dark body.
    await expect.poll(async () => (await colours()).ground).not.toEqual(light.ground);
    const dark = await colours();
    expect([dark.series, dark.ground], JSON.stringify(dark)).toEqual([dark.primary, dark.body]);
});

// @ex-grid/pivot-mantine on /pivot-mantine: what only a browser shows of ExPivot under Mantine. What each slot draws
// and calls back is the contract layer 2 runs against mantinePivotChrome; here, the pane's layout under Mantine's
// styles, the mark the stylesheet draws on the current choice, and the theme's colours in both schemes.
test('ADR-0061, ADR-0390: under Mantine the Field List keeps its Areas whole at the pane\'s foot, the current layout choice is marked, and the theme paints the pivot in light and dark', async ({ page }) => {
    await page.goto('/pivot-mantine');
    const pivot = page.locator('.mantine-ex-pivot');
    const pane = pivot.locator('.ex-pivot-field-list');
    await expect(pivot.locator('.ex-pivot-sheet [role=grid] .ex-row').first()).toBeVisible();
    await expect(pane.locator('.mantine-ex-pivot-areas')).toBeVisible();

    // The four Areas stand whole inside the pane, under the list of fields, which scrolls on its own.
    const [paneBox, areasBox, fieldsBox] = await Promise.all([pane, pane.locator('.mantine-ex-pivot-areas'), pane.locator('.mantine-ex-pivot-fields')].map(at => at.boundingBox()));
    expect(areasBox.y + areasBox.height, JSON.stringify({ paneBox, areasBox })).toBeLessThanOrEqual(paneBox.y + paneBox.height);
    expect(areasBox.y, JSON.stringify({ fieldsBox, areasBox })).toBeGreaterThanOrEqual(fieldsBox.y + fieldsBox.height);

    // The mark is the stylesheet's, off aria-checked: the current form carries it, the others none.
    await pivot.getByRole('button', { name: /^Layout/ }).click();
    const mark = (words) => pivot.locator('[role=menuitemradio]').filter({ hasText: new RegExp(`^${words}$`) })
        .evaluate((item) => getComputedStyle(item.querySelector('.mantine-ex-pivot-menu-mark'), '::before').content);
    expect([await mark('Show in Compact Form'), await mark('Show in Outline Form')]).toEqual(['"✓"', 'none']);
    await page.keyboard.press('Escape');
    await expect(pivot.locator('.ex-pivot-popup')).toHaveCount(0);

    const colours = async () => {
        const css = await pane.evaluate((element) => {
            const theme = getComputedStyle(element.closest('.mantine-ex-pivot'));
            return { pane: getComputedStyle(element).backgroundColor, body: theme.getPropertyValue('--mantine-color-body') };
        });
        return Object.fromEntries(await Promise.all(Object.entries(css).map(async ([name, colour]) => [name, await resolvedColour(page, colour)])));
    };
    const light = await colours();
    expect(light.pane, JSON.stringify(light)).toEqual(light.body);
    await page.locator('#toggle-dark').click();
    await expect(page.locator('#dark-status')).toHaveText('Dark: True');
    await expect.poll(async () => (await colours()).pane).not.toEqual(light.pane);
    const dark = await colours();
    expect(dark.pane, JSON.stringify(dark)).toEqual(dark.body);
});
