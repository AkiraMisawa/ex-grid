import { test, expect } from './fixtures.mjs';

// ExPivot on /pivot (ADR-0058/0060/0061/0062), under ExPivot's own markup and under
// ExPivot.MudBlazor's Chrome: what only a browser can say. That a field dragged with the browser's
// own drag and drop lands on the Area it was dropped on; that the report's − button and a double
// click reach the pivot through the grid; that the keyboard goes into a menu and back; that the
// report filter band filters; and, under MudBlazor, that a select's list takes Escape before its
// panel and that the palette reaches the pane in both schemes. Everything is found by role and
// name, which both Chromes give the same, so the same test runs under either (ADR-0060: swapping
// the Chrome changes no behaviour).

// Tall and wide enough for the report, the pane beside it and the details grid under them.
test.use({ viewport: { width: 1400, height: 1100 } });

const pivot = (page) => page.locator('.ex-pivot');
const report = (page) => pivot(page).locator('.ex-grid');
const rows = (page) => report(page).locator('.ex-viewport .ex-row');
const pane = (page) => page.getByRole('region', { name: 'PivotTable Fields' });
const fieldsList = (page) => pane(page).getByRole('list', { name: 'PivotTable Fields' });
const field = (page, caption) => fieldsList(page).getByRole('listitem').filter({ has: page.getByRole('checkbox', { name: caption, exact: true }) });
const areaList = (page, title) => pane(page).getByRole('list', { name: title, exact: true });
const entry = (page, caption) => pane(page).getByRole('button', { name: `Options for ${caption}`, exact: true });

async function open(page, chrome) {
    await page.goto(`/pivot?chrome=${chrome}`);
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

        test(`ADR-0062: a double click on a value lists the trades behind it (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            // Row 1 is Americas' first desk under the Compact form, column 1 its first product.
            // The viewport takes the pointer and finds the cell under it, so the press is forced.
            const cell = rows(page).nth(1).locator('[role=gridcell]').nth(1);

            await cell.dblclick({ force: true });

            await expect(page.locator('#pivot-details-caption')).toContainText(/\d+ trades behind Sum of P&L where Region = Americas, Desk = \w+, Product = \w+\./);
            await expect(page.locator('#pivot-details .ex-viewport .ex-row').first()).toBeVisible();
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

        test(`ADR-0060: the report filter band filters the report (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            const button = page.getByRole('button', { name: 'Filter Currency', exact: true });
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
