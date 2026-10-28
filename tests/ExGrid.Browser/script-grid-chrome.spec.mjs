import { test, expect, circuitQuiet, settled, setRoundTrip } from './fixtures.mjs';

// A Chrome written in Razor over the Script Core (ADR-0410), on /script-grid-mud, on both Blazor hosts:
// ExGrid.MudBlazor's column menu rendered in .NET, moved into the Script Core's slot by the adapter's script, and
// wired there. Only a browser shows the Razor standing in the slot and a key typed before it arrives; the wiring's
// logic is layer 2's (src/npm/core/test/slot-wiring.test.ts, src/npm/blazor/test/razor-slots.test.ts).

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
