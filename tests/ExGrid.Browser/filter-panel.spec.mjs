import { test, expect, settled, circuitQuiet } from './fixtures.mjs';
import { expectKeyboardOn } from './keyboard.mjs';

// WR-1 on /filter-panel, once per Chrome: the Wrapper's filter panel uses the whole
// FilterPanelContext — a value list with a search and a Blank entry, the condition form where a
// column declares it or where the answer is TooMany, Apply and Cancel — and the same choices made
// through either panel produce the same Filter, which the page writes out below the grid
// (ADR-0009/0023/0030/0044). Columns: Book 0, Trader 1 (one trade in seven has none), Id 2 (more
// distinct values than the source lists), Notional 3 (the condition form only).

const grid = (page) => page.locator('.ex-grid').first();
const spec = (page) => page.locator('#filter-spec');
const popover = (page) => grid(page).locator('.ex-popover');

// Each Chrome's own controls for the same choice, as popovers.spec.mjs names them.
const PANEL = {
    builtin: {
        list: (panel) => panel.locator('.ex-popover-list'),
        entries: (panel) => panel.locator('.ex-popover-list label'),
        selectAll: (panel) => panel.locator('.ex-select-all'),
        entry: (panel, name) => panel.getByRole('checkbox', { name, exact: true }),
        search: (panel) => panel.locator('input[type=search]'),
        greaterThan: (page, panel) => panel.locator('select').first().selectOption('GreaterThan'),
        operand: (panel) => panel.locator('input:not([type])'),
        apply: (panel) => panel.locator('.ex-popover-actions button', { hasText: 'OK' }),
        cancel: (panel) => panel.locator('.ex-popover-actions button', { hasText: 'Cancel' }),
    },
    mud: {
        list: (panel) => panel.locator('.mud-ex-grid-filter-values'),
        entries: (panel) => panel.locator('.mud-ex-grid-filter-value'),
        selectAll: (panel) => panel.locator('.mud-ex-grid-filter-all input'),
        entry: (panel, name) => panel.getByRole('checkbox', { name, exact: true }),
        search: (panel) => panel.locator('.mud-ex-grid-filter-search input'),
        // A native select in MudBlazor's outline, so that the choice reaches the core in the browser
        // (MudExGridScriptFilterPanel, spine rule 6).
        greaterThan: (page, panel) => panel.getByRole('combobox', { name: 'Operator' }).selectOption('greaterThan'),
        operand: (panel) => panel.locator('.mud-ex-grid-filter-operand input'),
        apply: (panel) => panel.locator('.mud-ex-grid-filter-apply'),
        cancel: (panel) => panel.locator('.mud-ex-grid-filter-cancel'),
    },
};

/** Opens the popover of `column` from a cell in it, and returns it once its filter has its answer. */
async function openPanel(page, column) {
    await grid(page).locator(`[id$='r0c${column}']`).click({ force: true });
    await page.keyboard.press('Alt+ArrowDown');
    await expect(popover(page)).toBeVisible();
    return popover(page);
}

/** What the source holds once every answer has landed. */
async function appliedSpec(page) {
    await expect(popover(page)).toHaveCount(0);
    await settled(grid(page));
    await circuitQuiet();
    return spec(page).textContent();
}

for (const chrome of ['builtin', 'mud']) {
    test.describe(`under the ${chrome} Chrome`, () => {
        test.beforeEach(async ({ page }) => {
            await page.goto(`/filter-panel?chrome=${chrome}`);
            await expect(grid(page).locator('.ex-row').first()).toBeVisible();
            await expect(spec(page)).toHaveText('No filter.');
        });

        test(`WR-1 / ADR-0009/0023: the value list ends with the Blank entry, its search narrows it, and Apply sends what was ticked (${chrome})`, async ({ page }) => {
            const ui = PANEL[chrome];
            const panel = await openPanel(page, 1);
            await expect(ui.entries(panel)).toHaveText(['Ishikawa', 'Novak', 'Osei', '(Blanks)']);

            await ui.search(panel).fill('nov');
            await expect(ui.entries(panel)).toHaveText(['Novak']);
            await ui.search(panel).fill('');
            await expect(ui.entries(panel)).toHaveCount(4);

            // Every value ticked at first; (Select All) clears them, read before the next tick, which
            // would otherwise find Novak still ticked and press nothing.
            await ui.selectAll(panel).click();
            for (const name of ['Ishikawa', 'Novak', 'Osei', '(Blanks)']) {
                await expect(ui.entry(panel, name)).not.toBeChecked();
            }
            await ui.entry(panel, 'Novak').check();
            await ui.entry(panel, '(Blanks)').check();
            await ui.apply(panel).click();

            expect(await appliedSpec(page)).toBe('Trader: And [In (Novak, (blank))]');
            await expectKeyboardOn(grid(page));
        });

        test(`WR-1 / ADR-0009: a column whose values are TooMany gets the condition form, and Apply sends its condition (${chrome})`, async ({ page }) => {
            const ui = PANEL[chrome];
            const panel = await openPanel(page, 2);
            await expect(ui.operand(panel)).toBeVisible();
            await expect(ui.list(panel)).toHaveCount(0);

            await ui.greaterThan(page, panel);
            await ui.operand(panel).fill('1000');
            await ui.apply(panel).click();

            expect(await appliedSpec(page)).toBe('Id: And [GreaterThan 1000]');
            await expect(grid(page)).toHaveAttribute('aria-rowcount', '200');
        });

        test(`WR-1 / ADR-0009: a column that declares the condition form gets it alone, and Cancel sends nothing (${chrome})`, async ({ page }) => {
            const ui = PANEL[chrome];
            const panel = await openPanel(page, 3);
            await expect(ui.operand(panel)).toBeVisible();
            await expect(ui.list(panel)).toHaveCount(0);

            await ui.greaterThan(page, panel);
            await ui.operand(panel).fill('2000000');
            await ui.cancel(panel).click();

            expect(await appliedSpec(page)).toBe('No filter.');
        });
    });
}
