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
