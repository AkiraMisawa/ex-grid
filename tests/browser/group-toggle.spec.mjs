import { test, expect, alterPage, circuitQuiet, holdAnswers, settled } from './fixtures.mjs';
import { SERVER } from './hosting.mjs';
import { painted, paints, sameColour } from './pixels.mjs';

// A Header Group that opens and closes (ADR-0400), on /group-toggle, on both Blazor hosts and the React host: what
// only a browser shows of it. A real press on the toggle reaches the Consumer once and the columns on screen are the
// ones it pushed (HG-18), two presses before its answer ask the same thing twice (HG-22), the keys keep the Focus on
// its row across the push (HG-23), and the Hidden Mark is painted in its Cell State's token, and stays seen under
// forced colours (HG-24). The rules themselves are layer 1's (tests/cases/columns/header-group-toggle*.json,
// tests/cases/keys/header-group-keys.json) and the DOM's layer 2's (packages/core/test/grid-header-group-toggle.test.ts).

test.beforeEach(async ({ page }) => {
    await page.goto('/group-toggle');
    await expect(toggleGrid(page).locator('.ex-row').first()).toBeVisible();
    await expect(markGrid(page).locator('.ex-row').first()).toBeVisible();
});

const toggleGrid = (page) => page.locator('#group-toggle [role=grid]');
const markGrid = (page) => page.locator('#hidden-marks [role=grid]');
const toggle = (page) => toggleGrid(page).locator('.ex-header-group-toggle');
const heard = (page) => page.locator('#group-toggled');
const cell = (page, row, column) => toggleGrid(page).locator(`[id$='r${row}c${column}']`);

const all = ['Book', 'Trader', 'Notional', 'Trade date', 'Confirmed'];
const closed = ['Book', 'Trader', 'Notional'];
const headers = async (page) => (await toggleGrid(page).locator('.ex-header-cell').allInnerTexts()).map((text) => text.trim());

async function answered(page) {
    await settled(toggleGrid(page));
    await circuitQuiet();
}

test('HG-18 / ADR-0400: each press on the toggle is heard once, and the columns shown are the ones the page pushed @behaviour', async ({ page }) => {
    await expect(toggle(page)).toHaveAttribute('aria-expanded', 'true');
    expect(await headers(page)).toEqual(all);

    await toggle(page).click();
    await expect(heard(page)).toHaveText('Toggled: trade closed');
    await answered(page);
    await expect(toggle(page)).toHaveAttribute('aria-expanded', 'false');
    expect(await headers(page)).toEqual(closed);

    await toggle(page).click();
    await expect(heard(page)).toHaveText('Toggled: trade closed; trade open');
    await answered(page);
    await expect(toggle(page)).toHaveAttribute('aria-expanded', 'true');
    expect(await headers(page)).toEqual(all);
});

test('HG-22 / ADR-0400: two presses before the Consumer answers ask to close twice, and the columns are those of one press @blazor', async ({ page }) => {
    test.skip(!SERVER, 'an answer a round trip away is the Server host\'s; elsewhere the Consumer answers within the press');
    await holdAnswers(true);
    await toggle(page).click();
    await toggle(page).click();
    await holdAnswers(false);

    await expect(heard(page)).toHaveText('Toggled: trade closed; trade closed');
    await answered(page);
    await expect(toggle(page)).toHaveAttribute('aria-expanded', 'false');
    expect(await headers(page)).toEqual(closed);
});

test('HG-23 / ADR-0011, ADR-0400: Alt+Shift+↑ and ↓ keep the Focus on its row, in the group\'s first column when its own is hidden @behaviour', async ({ page }) => {
    const focus = () => toggleGrid(page).getAttribute('aria-activedescendant');
    await cell(page, 2, 3).click({ force: true });
    await expect.poll(focus).toMatch(/r2c3$/);

    await page.keyboard.press('Alt+Shift+ArrowUp');
    await expect(heard(page)).toHaveText('Toggled: trade closed at TradeDate');
    await answered(page);
    expect(await headers(page)).toEqual(closed);
    expect(await focus()).toMatch(/r2c2$/);

    await page.keyboard.press('Alt+Shift+ArrowDown');
    await expect(heard(page)).toHaveText('Toggled: trade closed at TradeDate; trade open at Notional');
    await answered(page);
    expect(await headers(page)).toEqual(all);
    expect(await focus()).toMatch(/r2c2$/);
});

test('HG-24 / ADR-0400, ADR-0029: each Hidden Mark is painted in its Cell State\'s token, and stays seen under forced colours @behaviour', async ({ page }) => {
    const tokens = {
        stale: ['--ex-state-stale-color', [0, 96, 160]],
        missing: ['--ex-state-missing-background', [160, 0, 96]],
        error: ['--ex-state-error-color', [96, 160, 0]],
        modified: ['--ex-state-modified-mark', [200, 120, 0]],
    };
    // Set above the grid, where a Wrapper's stylesheet sets them, each an opaque colour no other paint on the page has.
    await alterPage(page, (set) => {
        for (const [token, [r, g, b]] of set) document.body.style.setProperty(token, `rgb(${r}, ${g}, ${b})`);
        return () => { for (const [token] of set) document.body.style.removeProperty(token); };
    }, Object.values(tokens));
    await page.mouse.move(0, 0);
    await settled(markGrid(page));
    await circuitQuiet();

    // Inside the triangle at the group's top right corner, and the group's own ground a few pixels to its left.
    const read = async (state) => {
        const box = await markGrid(page).locator(`.ex-hidden-mark-${state}`).boundingBox();
        const region = await painted(page, { x: box.x - 6, y: box.y, width: box.width + 6, height: box.height });
        return { mark: region.at(box.x + box.width - 1.5, box.y + 1.5), ground: region.at(box.x - 4, box.y + 1.5) };
    };
    for (const [state, [, colour]] of Object.entries(tokens)) {
        const { mark } = await read(state);
        expect(paints(mark, colour), `the ${state} mark paints ${colour}, its token's, and painted ${mark}`).toBe(true);
    }

    await page.emulateMedia({ forcedColors: 'active' });
    try {
        for (const state of Object.keys(tokens)) {
            const { mark, ground } = await read(state);
            expect(sameColour(mark, ground, 24), `under forced colours the ${state} mark (${mark}) stands off its ground (${ground})`).toBe(false);
        }
    } finally {
        await page.emulateMedia({ forcedColors: 'none' });
    }
});
