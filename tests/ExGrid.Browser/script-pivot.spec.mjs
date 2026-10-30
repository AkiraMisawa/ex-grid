import { test, expect, circuitQuiet, settled, setRoundTrip } from './fixtures.mjs';

// Blazor's ExPivot over its Script Core (ADR-0370 section 1), on /script-pivot, on both Blazor hosts: the one path
// no other layer sees whole. The report is built in the browser from the Leaf Aggregates the C# source answers in
// .NET, so every question crosses the bridge as PivotJson and comes back, a layout the user makes crosses to the
// Consumer's handler, and a layout or a source of the Consumer's crosses back. The engines' answers are the shared
// cases' (tests/cases/pivot), and the Script Core's behaviour is its own specs' on the React host. What MudPivotChrome
// draws over it is pivot.spec.mjs's, whose /pivot draws Script ExPivot (ADR-0370 section 6, step 2); what stays here
// is what /pivot does not cross: the Consumer's handlers in .NET, Space on MudBlazor's checkbox, the native select of
// Value Field Settings, and a key typed as a Razor tab or dialog opens.

test.use({ viewport: { width: 1400, height: 1100 } });

const pivot = (page) => page.locator('.ex-pivot');
const report = (page) => pivot(page).locator('.ex-pivot-sheet > .ex-grid');
const rows = (page) => report(page).locator('.ex-viewport .ex-row');
const pane = (page) => page.getByRole('region', { name: 'PivotTable Fields' });
const tick = (page, caption) => pane(page).getByRole('checkbox', { name: caption, exact: true });
// Row 1 is Americas' first desk under the Compact form, column 1 its first product.
const firstValue = (page) => rows(page).nth(1).locator('[role=gridcell]').nth(1);
const grandTotal = (page) => rows(page).last().locator('[role=gridcell]').last();

async function open(page, path) {
    await page.goto(path);
    await expect(rows(page).first()).toBeVisible({ timeout: 30_000 });
    await expect(pivot(page)).not.toHaveAttribute('aria-busy');
    await settled(report(page));
    await circuitQuiet();
}

async function figures(page) {
    return { first: await firstValue(page).textContent(), total: await grandTotal(page).textContent() };
}

test('ADR-0370 section 1: a new source of the Consumer\'s in .NET is asked again, and the report built in the browser follows it', async ({ page }) => {
    await open(page, '/script-pivot');
    const before = await figures(page);

    await page.locator('#script-pivot-revalue').click();
    await expect(firstValue(page)).not.toHaveText(before.first);
    await expect(pivot(page)).not.toHaveAttribute('aria-busy');
    await settled(report(page));
    expect((await figures(page)).total).not.toBe(before.total);
});

test('ADR-0059, ADR-0370 section 1: a field ticked reaches the Consumer\'s layout in .NET, and the Consumer\'s own layout replaces it', async ({ page }) => {
    await open(page, '/script-pivot');
    await expect(page.locator('#script-pivot-layout')).toHaveText('Rows: Region, Desk');

    await tick(page, 'Book').check();
    await expect(page.locator('#script-pivot-layout')).toHaveText('Rows: Region, Desk, Book');
    await expect(tick(page, 'Book')).toBeChecked();

    await page.locator('#script-pivot-reset').click();
    await expect(tick(page, 'Book')).not.toBeChecked();
    await expect(page.locator('#script-pivot-layout')).toHaveText('Rows: Region, Desk');
});

test('ADR-0060, ADR-0063, ADR-0130, ADR-0201, ADR-0370 section 10: the Consumer\'s words, its Show Details handler, its Selection handler and its Selection Summary handler, all in .NET, reach the pivot in the browser', async ({ page }) => {
    await open(page, '/script-pivot?details=dotnet');
    await expect(report(page).getByText('Row Labels', { exact: true })).toBeVisible();

    await page.locator('#script-pivot-japanese').click();
    await expect(report(page).getByText('行ラベル', { exact: true })).toBeVisible();

    await firstValue(page).dblclick({ force: true });
    await expect(page.locator('#script-pivot-details')).toHaveText(/^詳細: .+: [1-9]\d* records$/);
    await expect(pivot(page).locator('.ex-pivot-details-panel')).toHaveCount(0);
    // The cell double clicked is the report's Selection, as .NET heard it (ADR-0201's note of 2026-10-27).
    await expect(page.locator('#script-pivot-selection')).toHaveText('Focus: row 1, column 1; 1 range(s)');

    // Two cells summed in the browser are the Selection Summary .NET hears, in the strip's own text (ADR-0130).
    await page.keyboard.press('Shift+ArrowRight');
    await expect(page.locator('#script-pivot-summary')).toHaveText(/^Sum: \S+$/);
    await expect(report(page).locator('.ex-summary')).toContainText(await page.locator('#script-pivot-summary').innerText());
});

test('ADR-0410: MudPivotChrome draws the Script pivot\'s report filter and its Filter…, where an Item unticked with Space on MudBlazor\'s checkbox and OK reach the pivot', async ({ page }) => {
    await open(page, '/script-pivot?chrome=mud');
    const button = pivot(page).getByRole('button', { name: 'Filter Currency', exact: true });
    await expect(button).toHaveClass(/mud-button-root/);
    const before = await grandTotal(page).textContent();
    await button.click();
    const panel = pivot(page).locator('.mud-ex-pivot-item-filter');
    // Space on MudBlazor's checkbox is the browser's own, which the pivot hears; MudBlazor's would untick it in .NET
    // alone.
    const usd = panel.getByRole('checkbox', { name: 'USD', exact: true });
    await usd.focus();
    await page.keyboard.press('Space');
    await expect(usd).not.toBeChecked();
    await panel.getByRole('button', { name: 'OK' }).click();
    await settled(report(page));
    await expect(panel).toHaveCount(0);
    await expect(grandTotal(page)).not.toHaveText(before);
});

test('ADR-0410: MudPivotChrome draws the Script pivot\'s Value Field Settings…, whose caption follows the Aggregation chosen in its native select, and OK reaches the pivot', async ({ page }) => {
    await open(page, '/script-pivot?chrome=mud');
    await pane(page).getByRole('button', { name: 'Options for Sum of P&L', exact: true }).click();
    await pivot(page).getByRole('menuitem', { name: 'Value Field Settings…' }).click();
    const panel = pivot(page).locator('.mud-ex-pivot-value-settings');
    await panel.locator('select.mud-ex-pivot-aggregation').selectOption('count');
    await expect(panel.getByRole('textbox', { name: 'Custom Name' })).toHaveValue('Count of P&L');
    await panel.getByRole('button', { name: 'OK' }).click();
    await settled(report(page));
    await expect(panel).toHaveCount(0);
    await expect(pane(page).getByRole('button', { name: 'Options for Count of P&L', exact: true })).toBeVisible();
});

test('ADR-0410: MudPivotChrome draws the Script pivot\'s sheet tabs, and Show Details opens one that takes a key typed as it opens over a slow circuit', async ({ page }) => {
    await open(page, '/script-pivot?chrome=mud');
    const details = pivot(page).getByRole('tablist').getByRole('tab', { name: /^Details: Americas \/ \w+ \/ \w+$/ });
    await setRoundTrip(150);
    try {
        await firstValue(page).dblclick({ force: true });
        // Typed a round trip before the tabs' Razor stands on Server: the key waits in the pivot's order until the new
        // tab holds the keyboard, and is taken there, never by the report the records now cover.
        await page.keyboard.press('ArrowDown');
        await settled(report(page));
        await circuitQuiet();
    } finally {
        await setRoundTrip(0);
    }
    await expect(details).toHaveAttribute('aria-selected', 'true');
    await expect(details).toHaveClass(/mud-button-root/);
    await expect(details).toBeFocused();
    await expect(page.locator('#script-pivot-selection')).toHaveText('Focus: row 1, column 1; 1 range(s)');
});

test('ADR-0410: MudPivotChrome draws the Script pivot\'s Show Details dialog, and Enter typed as it opens over a slow circuit presses its Close', async ({ page }) => {
    await open(page, '/script-pivot?chrome=mud&details=dialog');
    const dialog = page.getByRole('dialog', { name: /^Details: Americas \/ \w+ \/ \w+$/ });
    await setRoundTrip(150);
    try {
        await firstValue(page).dblclick({ force: true });
        // Typed a round trip before the dialog's Razor stands on Server: the key waits in the pivot's order until Close
        // holds the keyboard, and presses it.
        await expect(dialog).toBeVisible();
        await page.keyboard.press('Enter');
        await settled(report(page));
        await circuitQuiet();
    } finally {
        await setRoundTrip(0);
    }
    await expect(dialog).toHaveCount(0);
});
