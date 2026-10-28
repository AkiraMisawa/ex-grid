import { test, expect, circuitQuiet, settled, setRoundTrip } from './fixtures.mjs';

// A Chrome written in Razor over the Script Core (ADR-0410), on /script-grid-mud, on both Blazor hosts:
// ExGrid.MudBlazor's column menu, filter panel and find panel rendered in .NET, moved into the Script Core's slots by
// the adapter's script, and wired there. Only a browser shows the Razor standing in the slot and a key typed before it
// arrives; the wiring's logic is layer 2's (src/npm/core/test/slot-wiring.test.ts, src/npm/blazor/test/razor-slots.test.ts).

test.beforeEach(async ({ page }) => {
    await page.goto('/script-grid-mud');
    await expect(grid(page).locator('.ex-row').first()).toBeVisible();
});

test.afterEach(async () => {
    await setRoundTrip(0);
});

function grid(page) {
    return page.locator('.ex-grid').first();
}

const cell = (page, row, column) => grid(page).locator(`[id$='r${row}c${column}']`);

test('ADR-0030: in MudExGridPaper the Script grid takes the paper\'s cascaded defaults, its Density and Row Stripes, under its Roboto', async ({ page }) => {
    await settled(grid(page));
    // The paper's cascaded defaults crossed to the browser and the core took them: Standard's 32 px rows, every second
    // one striped. Roboto's widths come in the same value; what the core charges by them is layer 1's
    // (src/npm/blazor/test/wire-metrics.test.ts, tests/ExGrid.Components/ScriptGridCellMetricsTests.cs).
    const rows = grid(page).locator('.ex-viewport .ex-row');
    await expect(rows.first()).toHaveCSS('height', '32px');
    await expect(rows.nth(0)).not.toHaveClass(/\bex-row-stripe\b/);
    await expect(rows.nth(1)).toHaveClass(/\bex-row-stripe\b/);
    expect(await cell(page, 0, 0).evaluate(e => getComputedStyle(e).fontFamily)).toContain('Roboto');
});

test('ADR-0410: the column menu is MudBlazor\'s, and keys typed as it opens are the menu\'s before its Razor arrives', async ({ page }) => {
    await settled(grid(page));
    await cell(page, 0, 0).click({ force: true });

    // Opened and read once it stands: MudBlazor's buttons, in the core's popover, and the first takes the keyboard.
    await page.keyboard.press('Alt+ArrowDown');
    await settled(grid(page));
    const items = grid(page).locator('.ex-popover [data-ex-menu-item]');
    await expect(items.first()).toHaveClass(/mud-button-root/);
    await expect(items.first()).toHaveAttribute('role', 'menuitem');
    await expect(items.first()).toBeFocused();
    await page.keyboard.press('Escape');
    await settled(grid(page));
    await expect(grid(page).locator('.ex-popover')).toHaveCount(0);

    // Opened again and run at once, a round trip before the Razor stands in the slot on Server: End and Enter are
    // answered against the place the core keeps for the menu, and run its last command, Hide.
    await setRoundTrip(150);
    await page.keyboard.press('Alt+ArrowDown');
    await page.keyboard.press('End');
    await page.keyboard.press('Enter');
    await settled(grid(page));
    await circuitQuiet();
    await expect(page.locator('#script-mud-hidden')).toHaveText('Hidden: Book');
    await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
});

test('ADR-0410: the filter panel is MudBlazor\'s, and what is typed and chosen in it at machine speed over a slow circuit is what applies', async ({ page }) => {
    await settled(grid(page));
    const books = grid(page).locator('.ex-row [id$="c0"]');
    const notionals = grid(page).locator('.ex-row [id$="c2"]');

    // Book's value list, searched and applied by Enter while the Razor is a round trip from standing on Server: E,
    // typed as the menu opens, waits for the panel and puts the keyboard in its search box, MudBlazor's field.
    await setRoundTrip(150);
    await cell(page, 0, 0).click({ force: true });
    await page.keyboard.press('Alt+ArrowDown');
    await page.keyboard.press('e');
    await page.keyboard.type('-00');
    await page.keyboard.press('Enter');
    await settled(grid(page));
    await circuitQuiet();
    await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
    await expect(books).toHaveCount(9);
    for (const text of await books.allTextContents()) expect(text).toContain('-00');

    // Notional's conditions: the operator chosen from MudBlazor's outline of a native select, a value typed and Enter,
    // each at once after the last. Above the largest but one of the rows shown, only the largest stays.
    const numbers = async () => (await notionals.allTextContents()).map(text => Number(text.replace(/[^0-9.-]/g, '')));
    const before = (await numbers()).toSorted((a, b) => a - b);
    const threshold = before.at(-2);
    await cell(page, 0, 2).click({ force: true });
    await page.keyboard.press('Alt+ArrowDown');
    await page.keyboard.press('Tab');
    const operator = grid(page).locator('.ex-popover select[data-ex-filter-operator="first"]');
    await expect(operator).toBeFocused();
    await operator.selectOption('greaterThan');
    await page.keyboard.press('Tab');
    await page.keyboard.type(String(threshold));
    await page.keyboard.press('Enter');
    await settled(grid(page));
    await circuitQuiet();
    await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
    expect(await numbers()).toEqual(before.filter(value => value > threshold));
});

test('ADR-0410 / ADR-0055: Ctrl+F opens MudBlazor\'s find panel, and a text typed and entered at machine speed over a slow circuit is what the source finds', async ({ page }) => {
    await settled(grid(page));
    const books = (await grid(page).locator('.ex-row [id$="c0"]').allTextContents()).map(text => text.trim());
    const target = books[4];
    // The step from the first row finds the first row after it whose Book holds the text.
    const found = books.findIndex((text, index) => index > 0 && text.includes(target));

    // Typed and entered as the panel opens, while its Razor is a round trip from standing on Server: the keys wait for
    // MudBlazor's field, which takes the keyboard, and the step is the source's, answered in .NET.
    await setRoundTrip(150);
    await cell(page, 0, 0).click({ force: true });
    await page.keyboard.press('Control+f');
    await page.keyboard.type(target);
    await page.keyboard.press('Enter');
    await settled(grid(page));
    await circuitQuiet();
    const field = grid(page).locator('.ex-popover input[data-ex-find-field="find"]');
    await expect(field).toHaveClass(/mud-input/);
    await expect(field).toBeFocused();
    await expect(field).toHaveValue(target);
    await expect.poll(() => grid(page).getAttribute('aria-activedescendant')).toMatch(new RegExp(`r${found}c0$`));
});
