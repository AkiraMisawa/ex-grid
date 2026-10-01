import { test, expect } from './fixtures.mjs';

// ExPivot on /pivot (ADR-0058/0060/0061/0062/0065), under ExPivot's own markup and under
// ExPivot.MudBlazor's Chrome: what only a browser can say. That a field dragged with the browser's
// own drag and drop lands on the Area it was dropped on; that the report's − button and a double
// click reach the pivot through the grid; that Show Details opens a tab at the report's foot, a
// dialog, or hands the records to the page; that the keyboard goes into a menu and back; that the
// toolbar above the report holds the report filter band, the Layout menu and the pane's toggle,
// and that their popups open under it, over the report; that Defer Layout Update holds the report
// until Update; that the words can be Excel's Japanese edition's; and, under MudBlazor, that a
// select's list takes Escape before its panel and that the palette reaches the pane in both
// schemes. Everything is found by role and name, which both Chromes give the same, so the same
// test runs under either (ADR-0060: swapping the Chrome changes no behaviour).

// Tall and wide enough for the report, the pane beside it and the details grid under them.
test.use({ viewport: { width: 1400, height: 1100 } });

const pivot = (page) => page.locator('.ex-pivot');
// The report's own grid: a details tab's grid stands beside it in the same box, and a dialog's
// outside it.
const report = (page) => pivot(page).locator('.ex-pivot-sheet > .ex-grid');
const rows = (page) => report(page).locator('.ex-viewport .ex-row');
const pane = (page) => page.getByRole('region', { name: 'PivotTable Fields' });
const fieldsList = (page) => pane(page).getByRole('list', { name: 'PivotTable Fields' });
const field = (page, caption) => fieldsList(page).getByRole('listitem').filter({ has: page.getByRole('checkbox', { name: caption, exact: true }) });
const areaList = (page, title) => pane(page).getByRole('list', { name: title, exact: true });
const entry = (page, caption) => pane(page).getByRole('button', { name: `Options for ${caption}`, exact: true });
const toolbarButton = (page, name) => pivot(page).locator('.ex-pivot-toolbar').getByRole('button', { name, exact: true });
// Row 1 is Americas' first desk under the Compact form, column 1 its first product.
const firstValue = (page) => rows(page).nth(1).locator('[role=gridcell]').nth(1);

async function open(page, chrome, query = '') {
    await page.goto(`/pivot?chrome=${chrome}${query}`);
    await expect(rows(page).first()).toBeVisible({ timeout: 30_000 });
    // ExPivot's stylesheet has landed when its root lays the report and the pane out side by
    // side; a Wrapper's, when one of its tokens reaches the paper.
    await expect.poll(() => pivot(page).evaluate((p) => getComputedStyle(p).display)).toBe('flex');
    if (chrome === 'mud') {
        await expect.poll(() => page.locator('.mud-ex-grid').first()
            .evaluate((p) => getComputedStyle(p).getPropertyValue('--ex-pivot-pane-background').trim())).not.toBe('');
    }
    await expect(entry(page, 'Region')).toBeVisible();
}

/** Drops `source` on the Area titled `title`, on its heading: the Area itself, which puts it after
 *  the Area's entries. The Areas stand at the pane's foot, always in view, so nothing scrolls
 *  between the press and the drop — a scroll then would cancel the drag before it began. */
async function dropOnArea(page, source, title) {
    await source.dragTo(areaList(page, title).locator('xpath=..'), { targetPosition: { x: 12, y: 8 } });
}

/** The entries an Area's list holds, by their buttons' captions. */
async function entriesOf(page, title) {
    const names = await areaList(page, title).getByRole('button').evaluateAll((buttons) =>
        buttons.map((b) => b.getAttribute('aria-label')).filter((n) => n?.startsWith('Options for ')));
    return names.map((n) => n.slice('Options for '.length));
}

/** How many rows the report has: the grid says so on its root, whatever it paints. */
const reportRows = async (page) => Number(await report(page).getAttribute('aria-rowcount'));

/** Whether two boxes overlap. */
const overlaps = (a, b) => a.x < b.x + b.width && b.x < a.x + a.width && a.y < b.y + b.height && b.y < a.y + a.height;

for (const chrome of ['builtin', 'mud']) {
    test.describe(`under the ${chrome} Chrome`, () => {
        test(`ADR-0060: a field dragged from the list onto an Area stands there (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            expect(await entriesOf(page, 'Columns')).toEqual(['Product']);

            await dropOnArea(page, field(page, 'Month'), 'Columns');

            await expect.poll(() => entriesOf(page, 'Columns')).toEqual(['Product', 'Month']);
            await expect(page.getByRole('checkbox', { name: 'Month', exact: true })).toBeChecked();
            await expect(page.locator('#pivot-status')).toContainText('1 changes made in the pane');
        });

        test(`ADR-0060: an entry dragged before another reorders, and dragged to the list of fields is removed (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            expect(await entriesOf(page, 'Rows')).toEqual(['Region', 'Desk']);

            // Onto Region's entry: before it.
            await entry(page, 'Desk').dragTo(entry(page, 'Region'));
            await expect.poll(() => entriesOf(page, 'Rows')).toEqual(['Desk', 'Region']);

            await entry(page, 'Desk').dragTo(fieldsList(page));
            await expect.poll(() => entriesOf(page, 'Rows')).toEqual(['Region']);
            await expect(page.getByRole('checkbox', { name: 'Desk', exact: true })).not.toBeChecked();
        });

        test(`ADR-0058: the − button collapses an Item, and the Focus stays on it (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            const toggle = report(page).locator('.ex-pivot-toggle').first();
            await expect(toggle).toHaveAttribute('aria-expanded', 'true');
            const before = await rows(page).count();

            await toggle.click();

            await expect(report(page).locator('.ex-pivot-toggle').first()).toHaveAttribute('aria-expanded', 'false');
            await expect.poll(() => rows(page).count()).toBeLessThan(before);
            await expect(report(page)).toHaveAttribute('aria-activedescendant', /-r0c0$/);
        });

        test(`ADR-0058/0062: a double click on a value opens a tab at the report's foot, titled by the cell, holding the trades behind it (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            const layout = await page.locator('#pivot-status').textContent();

            // The viewport takes the pointer and finds the cell under it, so the press is forced.
            await firstValue(page).dblclick({ force: true });

            const tabs = pivot(page).getByRole('tablist');
            const details = tabs.getByRole('tab', { name: /^Details: Americas \/ \w+ \/ \w+$/ });
            await expect(details).toHaveAttribute('aria-selected', 'true');
            await expect(tabs.getByRole('tab', { name: 'PivotTable', exact: true })).toHaveAttribute('aria-selected', 'false');
            // The keyboard leaves the report the records now cover, for the new tab.
            await expect(details).toBeFocused();
            // The tabs stand at the report's foot, under it.
            const sheet = await pivot(page).locator('.ex-pivot-sheet').boundingBox();
            expect((await tabs.boundingBox()).y).toBeGreaterThanOrEqual(sheet.y + sheet.height - 1);
            // The records are an ExGrid of the source's fields, paged from the source, every one
            // of them Americas'.
            const panel = pivot(page).getByRole('tabpanel');
            await expect(panel).toHaveAttribute('aria-labelledby', await details.getAttribute('id'));
            const records = panel.locator('.ex-grid .ex-viewport .ex-row');
            await expect(records.first()).toBeVisible();
            await expect(panel.getByRole('columnheader', { name: 'Trade date' })).toBeVisible();
            await expect(records.first().locator('[role=gridcell]').first()).toHaveText('Americas');
            // Not part of the Pivot Layout.
            await expect(page.locator('#pivot-status')).toHaveText(layout ?? '');

            // The report's tab brings the report back, and a second Show Details opens a second
            // tab beside the first.
            await tabs.getByRole('tab', { name: 'PivotTable', exact: true }).click();
            await expect(panel).toHaveCount(0);
            await expect(rows(page).first()).toBeVisible();
            await rows(page).nth(2).locator('[role=gridcell]').nth(2).dblclick({ force: true });
            await expect(tabs.getByRole('tab', { name: /^Details: / })).toHaveCount(2);
            const second = tabs.getByRole('tab', { name: /^Details: / }).nth(1);
            await expect(second).toBeFocused();

            // A tab's close button closes it, and the keyboard goes to the tab selected then.
            await tabs.getByRole('button', { name: `Close ${await second.textContent()}`, exact: true }).click();
            await expect(tabs.getByRole('tab', { name: /^Details: / })).toHaveCount(1);
            await expect(details).toHaveAttribute('aria-selected', 'true');
            await expect(details).toBeFocused();
            await tabs.getByRole('button', { name: /^Close Details: Americas/ }).click();
            await expect(pivot(page).getByRole('tablist')).toHaveCount(0);
            await expect(rows(page).first()).toBeVisible();
        });

        test(`ADR-0058: Show Details opens ExPivot's dialog when the page asks for one, which takes the keyboard and closes on Escape (${chrome})`, async ({ page }) => {
            await open(page, chrome, '&details=dialog');

            await firstValue(page).dblclick({ force: true });

            const dialog = page.getByRole('dialog', { name: /^Details: Americas \/ \w+ \/ \w+$/ });
            await expect(dialog).toBeVisible();
            await expect(dialog.getByRole('button', { name: 'Close', exact: true })).toBeFocused();
            await expect(dialog.locator('.ex-grid .ex-viewport .ex-row').first()).toBeVisible();
            await expect(pivot(page).getByRole('tablist')).toHaveCount(0);
            // Modal: what it covers takes neither the keyboard nor the pointer.
            await expect(pivot(page).locator('.ex-pivot-report')).toHaveAttribute('inert', '');
            await expect(pivot(page).locator('.ex-pivot-field-list')).toHaveAttribute('inert', '');

            await page.keyboard.press('Escape');

            await expect(dialog).toHaveCount(0);
            await expect(pivot(page).locator('.ex-pivot-report')).not.toHaveAttribute('inert');
        });

        test(`ADR-0062: a page that listens to Show Details takes the trades, and neither a tab nor a dialog opens (${chrome})`, async ({ page }) => {
            await open(page, chrome, '&details=page');

            await firstValue(page).dblclick({ force: true });

            await expect(page.locator('#pivot-details-caption')).toContainText(/\d+ trades behind Sum of P&L where Region = Americas, Desk = \w+, Product = \w+\./);
            await expect(page.locator('#pivot-details .ex-viewport .ex-row').first()).toBeVisible();
            await expect(pivot(page).getByRole('tablist')).toHaveCount(0);
            await expect(page.getByRole('dialog')).toHaveCount(0);
        });

        test(`ADR-0039/0060: the keyboard goes into a field's menu and back to its entry (${chrome})`, async ({ page }) => {
            await open(page, chrome);

            await entry(page, 'Region').click();
            const menu = page.getByRole('menu', { name: 'Options for Region' });
            await expect(menu).toBeVisible();
            // Move Up is disabled for the first row field; Move Down is the first enabled command.
            await expect(menu.getByRole('menuitem', { name: 'Move Down' })).toBeFocused();
            await expect(entry(page, 'Region')).toHaveAttribute('aria-expanded', 'true');

            await page.keyboard.press('Escape');

            await expect(menu).toHaveCount(0);
            await expect(entry(page, 'Region')).toBeFocused();
            await expect(entry(page, 'Region')).toHaveAttribute('aria-expanded', 'false');
        });

        test(`ADR-0060: a menu drops down under its entry, as wide as the pane and over what follows (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            // The Values Area stands in the pane's right-hand column.
            const opener = entry(page, 'Sum of P&L');
            await opener.click();
            const menu = page.getByRole('menu', { name: 'Options for Sum of P&L' });
            await expect(menu).toBeVisible();

            const field = await pane(page).boundingBox();
            const at = await opener.boundingBox();
            const box = await menu.boundingBox();
            expect(box.width).toBeGreaterThan(field.width - 40);
            expect(box.y).toBeGreaterThanOrEqual(at.y + at.height - 1);
            expect(box.y).toBeLessThan(at.y + at.height + 12);
            // Over what follows: the Values Area's own box did not grow to hold it.
            const area = await areaList(page, 'Values').locator('xpath=..').boundingBox();
            expect(area.y + area.height).toBeLessThan(box.y + box.height);
        });

        test(`ADR-0060: a command from a field's menu moves the field (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            await entry(page, 'Desk').click();

            await page.getByRole('menuitem', { name: 'Move to Column Labels' }).click();

            await expect.poll(() => entriesOf(page, 'Columns')).toEqual(['Product', 'Desk']);
            await expect.poll(() => entriesOf(page, 'Rows')).toEqual(['Region']);
            await expect(page.getByRole('menu')).toHaveCount(0);
        });

        test(`ADR-0060: the toolbar above the report holds the report filter band on its left, then Layout and the Field List's toggle on its right (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            const band = await toolbarButton(page, 'Filter Currency').boundingBox();
            const layout = await toolbarButton(page, 'Layout').boundingBox();
            const toggle = await toolbarButton(page, 'Field List').boundingBox();
            const grid = await report(page).boundingBox();

            expect(band.x + band.width).toBeLessThan(layout.x);
            expect(layout.x + layout.width).toBeLessThan(toggle.x);
            for (const box of [band, layout, toggle]) {
                expect(box.y + box.height).toBeLessThanOrEqual(grid.y + 1);
            }
            // The bundled source cannot be refreshed, so there is no Refresh (ADR-0065).
            await expect(toolbarButton(page, 'Refresh')).toHaveCount(0);
        });

        test(`ADR-0060: the report filter band filters the report (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            const button = toolbarButton(page, 'Filter Currency');
            await expect(button).toContainText('(All)');
            const total = await rows(page).last().locator('[role=gridcell]').last().textContent();

            await button.click();
            const panel = page.getByRole('dialog', { name: 'Filter Currency' });
            await expect(panel).toBeVisible();
            await panel.getByRole('checkbox', { name: '(Select All)' }).uncheck();
            await expect(panel.getByRole('button', { name: 'OK' })).toBeDisabled();
            await panel.getByRole('checkbox', { name: 'USD', exact: true }).check();
            await panel.getByRole('button', { name: 'OK' }).click();

            await expect(panel).toHaveCount(0);
            await expect(button).toContainText('USD');
            await expect(rows(page).last().locator('[role=gridcell]').last()).not.toHaveText(total ?? '');
            await expect(button).toBeFocused();
        });

        test(`ADR-0060: the band's Filter… opens under the toolbar, over the report, and a press beside it closes it (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            const button = toolbarButton(page, 'Filter Currency');

            await button.click();
            const panel = page.getByRole('dialog', { name: 'Filter Currency' });
            await expect(panel.getByRole('checkbox', { name: 'USD', exact: true })).toBeVisible();
            const at = await button.boundingBox();
            const box = await panel.boundingBox();
            expect(box.y).toBeGreaterThanOrEqual(at.y + at.height - 1);
            expect(overlaps(box, await report(page).boundingBox())).toBe(true);
            await panel.getByRole('checkbox', { name: 'USD', exact: true }).uncheck();

            // A press on the report beside the panel lands on the backdrop: it closes the panel,
            // drops its draft, and the keyboard goes back to the button.
            const grid = await report(page).boundingBox();
            await page.mouse.click(grid.x + grid.width - 20, grid.y + grid.height - 20);

            await expect(panel).toHaveCount(0);
            await expect(button).toContainText('(All)');
            await expect(button).toBeFocused();
        });

        test(`ADR-0060: the Layout menu opens under its button, over the report; a choice lays the report out again and the keyboard goes back to Layout (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            const button = toolbarButton(page, 'Layout');

            await button.click();
            const menu = page.getByRole('menu', { name: 'Layout', exact: true });
            await expect(menu).toBeVisible();
            const at = await button.boundingBox();
            const box = await menu.boundingBox();
            expect(box.y).toBeGreaterThanOrEqual(at.y + at.height - 1);
            expect(overlaps(box, await report(page).boundingBox())).toBe(true);
            // The current choices are marked, and a choice that would change nothing is disabled.
            const compact = menu.getByRole('menuitemradio', { name: 'Show in Compact Form' });
            await expect(compact).toHaveAttribute('aria-checked', 'true');
            await expect(compact).toBeDisabled();
            await expect(menu.getByRole('menuitemradio', { name: 'Repeat All Item Labels' })).toBeDisabled();
            await expect(menu.getByRole('menuitemradio', { name: 'Do Not Show Subtotals' })).toBeFocused();

            await page.keyboard.press('Escape');
            await expect(menu).toHaveCount(0);
            await expect(button).toBeFocused();

            await button.click();
            await menu.getByRole('menuitemradio', { name: 'Show in Tabular Form' }).click();

            await expect(menu).toHaveCount(0);
            await expect(report(page).getByRole('columnheader', { name: 'Desk', exact: true })).toBeVisible();
            await expect(button).toBeFocused();
            await expect(page.locator('#pivot-status')).toContainText('1 changes made in the pane');
        });

        test(`ADR-0060: the Field List's toggle hides and shows the pane, and the page binds it (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            const toggle = toolbarButton(page, 'Field List');
            await expect(page.locator('#pivot-field-list-status')).toHaveText('Field List shown: True');

            await toggle.click();

            await expect(pane(page)).toHaveCount(0);
            await expect(page.locator('#pivot-field-list-status')).toHaveText('Field List shown: False');
            await expect(toggle).toHaveAttribute('aria-pressed', 'false');

            await toggle.click();
            await expect(pane(page)).toBeVisible();
            await expect(page.locator('#pivot-field-list-status')).toHaveText('Field List shown: True');
        });

        test(`ADR-0060: while Defer Layout Update is ticked the pane's changes wait for Update (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            const before = await reportRows(page);
            const update = pane(page).getByRole('button', { name: 'Update', exact: true });
            await expect(update).toBeDisabled();

            await pane(page).getByRole('checkbox', { name: 'Defer Layout Update' }).check();
            await page.getByRole('checkbox', { name: 'Month', exact: true }).check();

            await expect.poll(() => entriesOf(page, 'Rows')).toEqual(['Region', 'Desk', 'Month']);
            await expect(update).toBeEnabled();
            expect(await reportRows(page)).toBe(before);
            await expect(page.locator('#pivot-status')).toContainText('0 changes made in the pane');

            await update.click();

            await expect.poll(() => reportRows(page)).toBeGreaterThan(before);
            await expect(page.locator('#pivot-status')).toContainText('1 changes made in the pane');
            await expect(update).toBeDisabled();
        });

        test(`ADR-0059: the words switch speaks Excel's Japanese edition, and back (${chrome})`, async ({ page }) => {
            await open(page, chrome);

            await page.locator('#pivot-words').click();

            const japanese = page.getByRole('region', { name: 'ピボットテーブルのフィールド' });
            await expect(japanese).toBeVisible();
            await expect(japanese.getByRole('button', { name: '合計 / P&L のオプション', exact: true })).toBeVisible();
            await expect(report(page).getByRole('columnheader').first()).toHaveText('行ラベル');
            await expect(report(page).getByRole('columnheader', { name: '総計', exact: true })).toBeVisible();
            await expect(rows(page).last().locator('[role=gridcell]').first()).toHaveText('総計');
            await expect(toolbarButton(page, 'レイアウト')).toBeVisible();
            await expect(toolbarButton(page, 'Currency のフィルター')).toContainText('(すべて)');

            await page.locator('#pivot-words').click();
            await expect(pane(page)).toBeVisible();
            await expect(report(page).getByRole('columnheader').first()).toHaveText('Row Labels');
        });
    });
}

test('ADR-0039/0061: a MudSelect list inside Value Field Settings takes Escape before its panel', async ({ page }) => {
    await open(page, 'mud');
    await entry(page, 'Sum of P&L').click();
    await page.getByRole('menuitem', { name: 'Value Field Settings…' }).click();
    const panel = page.getByRole('dialog', { name: 'Value Field Settings…' });
    await expect(panel).toBeVisible();

    // The list is the select's, drawn in MudBlazor's provider outside ExPivot's root (ADR-0039).
    await panel.locator('.mud-ex-pivot-aggregation').first().click();
    const list = page.locator('.mud-popover-open');
    await expect(list.locator('.mud-list-item').first()).toBeVisible();
    await expect(pivot(page).locator('.mud-popover-open')).toHaveCount(0);
    await page.keyboard.press('Escape');

    await expect(list).toHaveCount(0);
    await expect(panel).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(panel).toHaveCount(0);
    await expect(entry(page, 'Sum of P&L')).toBeFocused();
});

test('ADR-0061/0030: the palette reaches the pane, the entries and the − button, light and dark', async ({ page }) => {
    await open(page, 'mud');
    const colours = () => page.evaluate(() => {
        const paper = document.querySelector('.mud-ex-grid');
        const palette = (name) => {
            const probe = document.createElement('span');
            probe.style.color = `var(${name})`;
            paper.append(probe);
            const colour = getComputedStyle(probe).color;
            probe.remove();
            return colour;
        };
        return {
            pane: getComputedStyle(document.querySelector('.ex-pivot-field-list')).backgroundColor,
            surface: palette('--mud-palette-surface'),
            toggle: getComputedStyle(document.querySelector('.ex-pivot-toggle')).color,
            secondary: palette('--mud-palette-text-secondary'),
        };
    });

    const light = await colours();
    expect(light.pane).toBe(light.surface);
    expect(light.toggle).toBe(light.secondary);

    await page.locator('#toggle-dark').click();
    await expect(page.locator('#dark-status')).toHaveText('Dark: True');
    await expect.poll(async () => (await colours()).pane).not.toBe(light.pane);
    const dark = await colours();
    expect(dark.pane).toBe(dark.surface);
    expect(dark.toggle).toBe(dark.secondary);
});
