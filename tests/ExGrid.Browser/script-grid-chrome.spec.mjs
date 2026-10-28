import { test, expect, circuitQuiet, settled, setRoundTrip } from './fixtures.mjs';
import { activeDescendant, expectKeyboardOn } from './keyboard.mjs';

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
    // Read where the grid keeps it: its Keyboard Field's, as Trader is editable (ADR-0080).
    await expect.poll(() => activeDescendant(grid(page))).toMatch(new RegExp(`r${found}c0$`));
});

test('ADR-0410 / SRV-7: the Cell Editor is MudBlazor\'s field, and a text typed and entered at machine speed over a slow circuit is what commits', async ({ page }) => {
    await settled(grid(page));
    // Typed onto Trader as the editor opens, while its Razor is a round trip from standing on Server: the keys wait for
    // MudBlazor's field, which the core reads in the browser, so the Enter commits every one of them.
    await setRoundTrip(150);
    await cell(page, 2, 1).click({ force: true });
    await page.keyboard.type('Osei Lee');
    await page.keyboard.press('Enter');
    await settled(grid(page));
    await circuitQuiet();
    await expect(page.locator('#script-mud-edited')).toHaveText('Edited: Trader = Osei Lee');

    // The field the edit was typed in is MudBlazor's, inside the core's box over the cell.
    await page.keyboard.press('F2');
    await settled(grid(page));
    const field = grid(page).locator('.ex-editor input.mud-ex-editor');
    await expect(field).toBeFocused();
    await expect(field).toHaveAttribute('aria-label', 'Trader');
});

test('ADR-0410 / ADR-0051: the completion list is MudBlazor\'s, the arrows move its choice without waiting on a slow circuit, and a click accepts a candidate', async ({ page }) => {
    await settled(grid(page));
    await setRoundTrip(150);
    await cell(page, 4, 1).click({ force: true });
    await page.keyboard.type('M');
    await settled(grid(page));
    await circuitQuiet();
    const items = grid(page).locator('.ex-completion .mud-ex-completion-item');
    await expect(items).toHaveText(['Marsh', 'Martens', 'Mira Holt']);
    await expect(items.nth(0)).toHaveAttribute('aria-selected', 'true');
    // The choice is the core's, marked on MudBlazor's list in the browser: .NET is asked to draw nothing for it, so the
    // contents still carry the number of the drawing that listed the answer.
    const list = grid(page).locator('.ex-completion [data-ex-version]');
    const drawing = await list.getAttribute('data-ex-version');
    if (drawing === null) throw new Error('The completion\'s contents carry no drawing number.');
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('ArrowDown');
    await settled(grid(page));
    await circuitQuiet();
    await expect(items.nth(2)).toHaveAttribute('aria-selected', 'true');
    await expect(items.nth(0)).toHaveAttribute('aria-selected', 'false');
    await expect(list).toHaveAttribute('data-ex-version', drawing);
    await items.nth(1).click();
    await page.keyboard.press('Enter');
    await settled(grid(page));
    await circuitQuiet();
    await expect(page.locator('#script-mud-edited')).toHaveText('Edited: Trader = Martens');
});

test('ADR-0410 / ADR-0051: the Formula Bar\'s fields are MudBlazor\'s, and what is typed in either and entered at machine speed over a slow circuit is what the grid hears', async ({ page }) => {
    await settled(grid(page));
    await cell(page, 3, 1).click({ force: true });
    await settled(grid(page));
    const text = grid(page).locator('input.mud-ex-formula-bar-text');
    await expect(grid(page).locator('input.mud-ex-name-box')).toHaveCount(1);
    await expect(text).not.toHaveValue('');
    // Typed into MudBlazor's field the moment it takes the keyboard, over a round trip: the core reads the field in the
    // browser, so the Enter commits every key (SRV-7).
    await setRoundTrip(150);
    await text.click();
    await page.keyboard.press('ControlOrMeta+A');
    await page.keyboard.type('Mira Holt');
    await page.keyboard.press('Enter');
    await settled(grid(page));
    await circuitQuiet();
    await expect(page.locator('#script-mud-edited')).toHaveText('Edited: Trader = Mira Holt');

    // The Name Box's Enter is the form's implicit submission around MudBlazor's field: the typed text reaches
    // OnNameBoxEntered whole, and the keyboard is the grid's again.
    await grid(page).locator('input.mud-ex-name-box').click();
    await page.keyboard.type('B7');
    await page.keyboard.press('Enter');
    await settled(grid(page));
    await circuitQuiet();
    await expect(page.locator('#script-mud-named')).toHaveText('Named: B7');
    await expectKeyboardOn(grid(page));
});

test('ADR-0410 section 6 / ADR-0037: a Template cell is drawn in Razor from its row, and Space pressed at once over a slow circuit hands its field the keyboard through the grid', async ({ page }) => {
    await settled(grid(page));
    // Note is column 3: each cell's field was drawn in .NET from the trade the browser named, and stands in its cell.
    const note = cell(page, 2, 3).locator('input.demo-note');
    const [book, trader] = await Promise.all([cell(page, 2, 0).textContent(), cell(page, 2, 1).textContent()]);
    await expect(note).toHaveAttribute('aria-label', `Note for ${book}, ${trader}`);

    await cell(page, 2, 0).click({ force: true });
    await setRoundTrip(150);
    await page.keyboard.press('ArrowRight');
    await page.keyboard.press('ArrowRight');
    await page.keyboard.press('ArrowRight');
    await page.keyboard.press('Space');
    await settled(grid(page));
    await circuitQuiet();
    await expect(note).toBeFocused();
    await page.keyboard.type('hedge');
    await expect(note).toHaveValue('hedge');
});
