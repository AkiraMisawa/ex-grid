import { test, expect, alterPage, circuitQuiet, setRoundTrip, dataRowCount, settled, stylesheetsLoaded } from './fixtures.mjs';
import { codeRegion } from './demo-code.mjs';
import { API_URL, SERVER } from './hosting.mjs';

// ExPivot on /pivot (ADR-0059/0061/0062/0063/0066), under ExPivot's own markup and under
// ExPivot.MudBlazor's Chrome: what only a browser can say. That a field dragged with the browser's
// own drag and drop lands on the Area it was dropped on; that the report's − button and a double
// click reach the pivot through the grid; that Show Details opens a tab at the report's foot, a
// dialog, or hands the records to the page; that the keyboard goes into a menu and back; that the
// Pivot Toolbar above the report holds the report filter band, the Layout menu and the pane's
// toggle, and that their popups open under it, over the report; that Defer Layout Update holds the
// report until Update; that the words can be Excel's Japanese edition's; that Month, a part of the
// trade date, is painted Jan to Sep in the calendar's order; that the code the page shows is the
// code it runs; and, under MudBlazor, that a select's list takes Escape before its panel and that
// the palette reaches the pane in both schemes. Everything is found by role and name, which both
// Chromes give the same, so the same test runs under either (ADR-0061: swapping the Chrome changes
// no behaviour).

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
// The Pivot Toolbar's buttons, found by name in the report's column, which both Chromes give the
// same: ExPivot draws the Pivot Toolbar there under its own markup, and MudBlazor's controls under
// the Mud Chrome. That they stand above the report is asserted where it matters.
const toolbarButton = (page, name) => pivot(page).locator('.ex-pivot-report').getByRole('button', { name, exact: true });
// Row 1 is Americas' first desk under the Compact form, column 1 its first product.
const firstValue = (page) => rows(page).nth(1).locator('[role=gridcell]').nth(1);

async function open(page, chrome, query = '') {
    await page.goto(`/pivot?chrome=${chrome}${query}`);
    await expect(rows(page).first()).toBeVisible({ timeout: 30_000 });
    // The Wrapper's token can arrive before MudBlazor's own stylesheet. Geometry read in
    // that gap belonged to unstyled icons and changed before the raw mouse press (Hunt,
    // 2026-10-20). Finish loading the page's styles before any test takes its coordinates.
    await stylesheetsLoaded(page);
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

// The same, inside one of several pivots on a page (/pivot?second=1, PV-10).
const nthPivot = (page, n) => page.locator('.ex-pivot').nth(n);
const reportIn = (one) => one.locator('.ex-pivot-sheet > .ex-grid');
const paneIn = (one) => one.getByRole('region', { name: 'PivotTable Fields' });
const fieldsIn = (one) => paneIn(one).getByRole('list', { name: 'PivotTable Fields' });
const fieldIn = (one, caption) => fieldsIn(one).getByRole('listitem').filter({ has: one.page().getByRole('checkbox', { name: caption, exact: true }) });
const areaIn = (one, title) => paneIn(one).getByRole('list', { name: title, exact: true });
const entryIn = (one, caption) => paneIn(one).getByRole('button', { name: `Options for ${caption}`, exact: true });
async function entriesIn(one, title) {
    const names = await areaIn(one, title).getByRole('button').evaluateAll((buttons) =>
        buttons.map((b) => b.getAttribute('aria-label')).filter((n) => n?.startsWith('Options for ')));
    return names.map((n) => n.slice('Options for '.length));
}

/** How many rows the report has: the grid says so on its root, whatever it paints. */
const reportRows = (page) => dataRowCount(report(page));

/** Whether two boxes overlap. */
const overlaps = (a, b) => a.x < b.x + b.width && b.x < a.x + a.width && a.y < b.y + b.height && b.y < a.y + a.height;

/** Puts DOM focus on the element `selector` finds, in the task that draws it — where a press on it
 *  would put it, before anything the host sends after that render can land — and logs, from here
 *  on, each element that takes DOM focus: `report`, `records` (a details grid's root),
 *  `records-inside` (anything else in a details grid, its scroller among them), `tab`, `close` (the
 *  dialog's Close), or its tag. Through alterPage, which takes the observer and the listener off
 *  as the test ends (ADR-0056). */
async function focusOnArrival(page, selector) {
    await alterPage(page, (selector) => {
        const log = [];
        const heard = (event) => {
            const target = event.target;
            if (!(target instanceof Element)) {
                return;
            }
            const records = target.closest('.ex-pivot-dialog .ex-grid, .ex-pivot-details-panel .ex-grid');
            log.push(records ? (records === target ? 'records' : 'records-inside')
                : target.closest('.ex-pivot-sheet > .ex-grid') ? 'report'
                : target.closest('[role=tab]') ? 'tab'
                : target.textContent.trim() === 'Close' ? 'close'
                : target.tagName.toLowerCase());
        };
        const observer = new MutationObserver(() => {
            const arrived = document.querySelector(selector);
            if (arrived instanceof HTMLElement) {
                observer.disconnect();
                arrived.focus({ preventScroll: true });
            }
        });
        observer.observe(document.body, { childList: true, subtree: true });
        window.__pivotFocusLog = log;
        document.addEventListener('focusin', heard, true);
        return () => {
            observer.disconnect();
            document.removeEventListener('focusin', heard, true);
            delete window.__pivotFocusLog;
        };
    }, selector);
}

/** What focusOnArrival has heard take DOM focus, in order. */
const focusLog = (page) => page.evaluate(() => window.__pivotFocusLog);

for (const chrome of ['builtin', 'mud']) {
    test.describe(`under the ${chrome} Chrome`, () => {
        test(`ADR-0061: a field dragged from the list onto an Area stands there (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            expect(await entriesOf(page, 'Columns')).toEqual(['Product']);

            await dropOnArea(page, field(page, 'Month'), 'Columns');

            await expect.poll(() => entriesOf(page, 'Columns')).toEqual(['Product', 'Month']);
            await expect(page.getByRole('checkbox', { name: 'Month', exact: true })).toBeChecked();
            await expect(page.locator('#pivot-status')).toContainText('1 changes made in the pane');
        });

        test(`ADR-0061: an entry dragged before another reorders, and dragged to the list of fields is removed (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            expect(await entriesOf(page, 'Rows')).toEqual(['Region', 'Desk']);

            // Onto Region's entry: before it.
            await entry(page, 'Desk').dragTo(entry(page, 'Region'));
            await expect.poll(() => entriesOf(page, 'Rows')).toEqual(['Desk', 'Region']);

            await entry(page, 'Desk').dragTo(fieldsList(page));
            await expect.poll(() => entriesOf(page, 'Rows')).toEqual(['Region']);
            await expect(page.getByRole('checkbox', { name: 'Desk', exact: true })).not.toBeChecked();
        });

        test(`ADR-0061: entries dropped on the report are removed from Rows and Columns (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            let changes = 0;
            for (const [caption, area, remaining] of [['Desk', 'Rows', ['Region']], ['Product', 'Columns', []]]) {
                await entry(page, caption).dragTo(report(page), { targetPosition: { x: 80, y: 60 } });
                await expect.poll(() => entriesOf(page, area)).toEqual(remaining);
                await expect(field(page, caption).getByRole('checkbox')).not.toBeChecked();
                // The page counts LayoutChanged, which is raised for the layout the report shows
                // (ADR-0061). The pane shows a removal at once, but the report only once its
                // question is answered, and a drop made while that question is out cancels it, so
                // the first removal would never be shown nor counted (ADR-0066). Each removal is
                // counted before the next drop.
                await expect(page.locator('#pivot-status')).toContainText(`${++changes} changes made in the pane`);
            }
        });

        test(`ADR-0061: a report removal waits for Defer Layout Update (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            const before = await reportRows(page);
            await pane(page).getByRole('checkbox', { name: 'Defer Layout Update' }).check();
            await entry(page, 'Desk').dragTo(report(page), { targetPosition: { x: 80, y: 60 } });
            await expect.poll(() => entriesOf(page, 'Rows')).toEqual(['Region']);
            await circuitQuiet();
            expect(await reportRows(page)).toBe(before);
            await expect(page.locator('#pivot-status')).toContainText('0 changes made in the pane');
            await pane(page).getByRole('button', { name: 'Update', exact: true }).click();
            await expect.poll(() => reportRows(page)).toBeLessThan(before);
            await expect(page.locator('#pivot-status')).toContainText('1 changes made in the pane');
        });

        test(`ADR-0061: an unused field and a details grid do not accept report removal (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            await field(page, 'Month').dragTo(report(page), { targetPosition: { x: 80, y: 60 } });
            await circuitQuiet();
            await expect(field(page, 'Month').getByRole('checkbox')).not.toBeChecked();
            await firstValue(page).dblclick({ force: true });
            const details = pivot(page).getByRole('tabpanel').getByRole('grid');
            await expect(details).toBeVisible();
            await entry(page, 'Desk').dragTo(details, { targetPosition: { x: 80, y: 60 } });
            await circuitQuiet();
            expect(await entriesOf(page, 'Rows')).toEqual(['Region', 'Desk']);
            await expect(page.locator('#pivot-status')).toContainText('0 changes made in the pane');
        });

        test(`ADR-0061: cancelling over the report clears the removal indication without removing (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            const from = await entry(page, 'Desk').boundingBox();
            const to = await report(page).boundingBox();
            await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2);
            await page.mouse.down();
            await page.mouse.move(to.x + 80, to.y + 60, { steps: 10 });
            await expect(pivot(page).locator('.ex-pivot-report-drop-remove')).toHaveText('Remove Field');
            await page.keyboard.press('Escape');
            await page.mouse.up();
            await expect(pivot(page).locator('.ex-pivot-report-drop-remove')).toHaveCount(0);
            await circuitQuiet();
            expect(await entriesOf(page, 'Rows')).toEqual(['Region', 'Desk']);
            await expect(page.locator('#pivot-status')).toContainText('0 changes made in the pane');
        });

        test(`PV-10 / ADR-0061: Σ Values dropped on the report stays where it is, and so does every other field (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            // A second Value Field brings Σ Values into Columns, after Product.
            await field(page, 'Notional').getByRole('checkbox').check();
            await expect.poll(() => entriesOf(page, 'Columns')).toEqual(['Product', 'Σ Values']);
            const values = await entriesOf(page, 'Values');
            expect(values).toHaveLength(2);
            await expect(page.locator('#pivot-status')).toContainText('1 changes made in the pane');

            await entry(page, 'Σ Values').dragTo(report(page), { targetPosition: { x: 80, y: 60 } });
            await circuitQuiet();
            expect(await entriesOf(page, 'Columns')).toEqual(['Product', 'Σ Values']);
            expect(await entriesOf(page, 'Values')).toEqual(values);
            expect(await entriesOf(page, 'Rows')).toEqual(['Region', 'Desk']);
            await expect(page.locator('#pivot-status')).toContainText('1 changes made in the pane');
        });

        test.describe('with room for the whole page', () => {
            // Tall enough that the page's own links, both pivots' reports and both panes stand in
            // the window at once: a scroll between the press and the drop would cancel the drag.
            test.use({ viewport: { width: 1400, height: 2200 } });

            test(`PV-10 / ADR-0061: a drag from outside the pivot, dropped on the report, an Area or the list of fields, changes nothing — after a cancelled drag too (${chrome})`, async ({ page }) => {
                await open(page, chrome);
                // A drag of the pivot's own, cancelled over the report: it leaves nothing behind
                // for a later drop to take.
                const from = await entry(page, 'Desk').boundingBox();
                const to = await report(page).boundingBox();
                await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2);
                await page.mouse.down();
                await page.mouse.move(to.x + 80, to.y + 60, { steps: 10 });
                await expect(pivot(page).locator('.ex-pivot-report-drop-remove')).toHaveText('Remove Field');
                await page.keyboard.press('Escape');
                await page.mouse.up();

                // A link of the page's, dragged with the browser's own drag and drop: every target
                // takes the drop (each prevents dragover's default), and none of them changes anything.
                const link = page.getByRole('link', { name: 'All pages', exact: true });
                await link.dragTo(report(page), { targetPosition: { x: 80, y: 60 } });
                await link.dragTo(areaList(page, 'Rows').locator('xpath=..'), { targetPosition: { x: 12, y: 8 } });
                await link.dragTo(entry(page, 'Region'));
                await link.dragTo(fieldsList(page));
                await circuitQuiet();
                expect(await entriesOf(page, 'Rows')).toEqual(['Region', 'Desk']);
                expect(await entriesOf(page, 'Columns')).toEqual(['Product']);
                expect(await entriesOf(page, 'Filters')).toEqual(['Currency']);
                await expect(page.locator('#pivot-status')).toContainText('0 changes made in the pane');
            });

            test(`PV-10 / ADR-0061: a field or an entry dragged from another pivot changes neither pivot (${chrome})`, async ({ page }) => {
                await page.goto(`/pivot?chrome=${chrome}&second=1`);
                const [first, second] = [nthPivot(page, 0), nthPivot(page, 1)];
                for (const one of [first, second]) {
                    await expect(reportIn(one).locator('.ex-viewport .ex-row').first()).toBeVisible({ timeout: 30_000 });
                    await expect(entryIn(one, 'Region')).toBeVisible();
                }
                await expect.poll(() => first.evaluate((p) => getComputedStyle(p).display)).toBe('flex');
                const unchanged = async () => {
                    for (const one of [first, second]) {
                        expect(await entriesIn(one, 'Rows')).toEqual(['Region', 'Desk']);
                        expect(await entriesIn(one, 'Columns')).toEqual(['Product']);
                        await expect(fieldIn(one, 'Month').getByRole('checkbox')).not.toBeChecked();
                    }
                    await expect(page.locator('#pivot-status')).toContainText('0 changes made in the pane');
                    await expect(page.locator('#pivot-second-status')).toHaveText('0 changes made in the second pane');
                };

                // The first pivot's entry onto the second's report, which would remove it from its own.
                await entryIn(first, 'Desk').dragTo(reportIn(second), { targetPosition: { x: 80, y: 60 } });
                // The first's field onto the second's Columns, which would place it there.
                await fieldIn(first, 'Month').dragTo(areaIn(second, 'Columns').locator('xpath=..'), { targetPosition: { x: 12, y: 8 } });
                await circuitQuiet();
                await unchanged();
                // The second's entry onto the first's report, onto an entry of the first's, and onto
                // the first's list of fields.
                await entryIn(second, 'Desk').dragTo(reportIn(first), { targetPosition: { x: 80, y: 60 } });
                await entryIn(second, 'Desk').dragTo(entryIn(first, 'Region'));
                await entryIn(second, 'Region').dragTo(fieldsIn(first));
                await circuitQuiet();
                await unchanged();

                // Each pivot still takes a drag of its own: the second's Desk dropped on its own report.
                await entryIn(second, 'Desk').dragTo(reportIn(second), { targetPosition: { x: 80, y: 60 } });
                await expect.poll(() => entriesIn(second, 'Rows')).toEqual(['Region']);
                await expect(page.locator('#pivot-second-status')).toHaveText('1 changes made in the second pane');
                expect(await entriesIn(first, 'Rows')).toEqual(['Region', 'Desk']);
            });
        });

        test(`ADR-0059: the − button collapses an Item, and the Focus stays on it (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            const toggle = report(page).locator('.ex-pivot-toggle').first();
            await expect(toggle).toHaveAttribute('aria-expanded', 'true');
            const before = await rows(page).count();

            await toggle.click();

            await expect(report(page).locator('.ex-pivot-toggle').first()).toHaveAttribute('aria-expanded', 'false');
            await expect.poll(() => rows(page).count()).toBeLessThan(before);
            await expect(report(page)).toHaveAttribute('aria-activedescendant', /-r0c0$/);
        });

        test(`ADR-0059/ADR-0011 (PV-13): a double click on an Item's label collapses and expands it over a round trip, and the Focus stays on it (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            const label = report(page).locator("[id$='-r4c0']");
            const toggle = label.locator('.ex-pivot-toggle');
            await label.click({ force: true });
            await expect(report(page)).toHaveAttribute('aria-activedescendant', /-r4c0$/);
            await expect(toggle).toHaveAttribute('aria-expanded', 'true');
            // Over a circuit's round trip an earlier render's after-render could run with the toggled
            // report in hand but not yet handed to the grid: the Focus was asked under a version the grid
            // did not have, and dropped as stale, leaving the report with none.
            // It came about one time in three, so the pair is made three times.
            await setRoundTrip(150);

            for (let round = 0; round < 3; round++) {
                await label.dblclick({ force: true });
                await expect(toggle).toHaveAttribute('aria-expanded', 'false');
                await expect(report(page)).toHaveAttribute('aria-activedescendant', /-r4c0$/);
                await label.dblclick({ force: true });
                await expect(toggle).toHaveAttribute('aria-expanded', 'true');
                await expect(report(page)).toHaveAttribute('aria-activedescendant', /-r4c0$/);
            }
        });

        test(`ADR-0059/0063: a double click on a value opens a tab at the report's foot, titled by the cell, holding the trades behind it (${chrome})`, async ({ page }) => {
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
            // Labelled by its tab: the tab itself under ExPivot's markup, the tab's title inside
            // it under MudTabs, which gives its tab element an id of its own.
            const labelledBy = await panel.getAttribute('aria-labelledby');
            expect(await details.evaluate((tab, id) => tab.id === id || tab.querySelector(`[id="${id}"]`) !== null, labelledBy)).toBe(true);
            await expect(panel).toHaveAccessibleName(/^Details: Americas \/ \w+ \/ \w+$/);
            const records = panel.locator('.ex-grid .ex-viewport .ex-row');
            await expect(records.first()).toBeVisible();
            await expect(panel.getByRole('columnheader', { name: 'Trade date' })).toBeVisible();
            await expect(records.first().locator('[role=gridcell]').first()).toHaveText('Americas');
            // The records cover the report, which is not painted meanwhile: the report's own header
            // would otherwise stand over the records' headings.
            await expect(report(page)).toBeHidden();
            const heading = await panel.getByRole('columnheader', { name: 'Trade date' }).boundingBox();
            expect(await page.evaluate(([x, y]) => document.elementFromPoint(x, y)?.closest('.ex-pivot-details-panel') !== null,
                [heading.x + heading.width / 2, heading.y + heading.height / 2])).toBe(true);
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

        test(`ADR-0059: Show Details opens ExPivot's dialog when the page asks for one, which takes the keyboard and closes on Escape (${chrome})`, async ({ page }) => {
            await open(page, chrome, '&details=dialog');

            await firstValue(page).dblclick({ force: true });

            const dialog = page.getByRole('dialog', { name: /^Details: Americas \/ \w+ \/ \w+$/ });
            await expect(dialog).toBeVisible();
            await expect(dialog.getByRole('button', { name: 'Close', exact: true })).toBeFocused();
            await expect(dialog.locator('.ex-grid .ex-viewport .ex-row').first()).toBeVisible();
            // And no tab beside it, once the host has said all it will (ADR-0056).
            await circuitQuiet();
            await expect(pivot(page).getByRole('tablist')).toHaveCount(0);
            // Modal: what it covers takes neither the keyboard nor the pointer.
            await expect(pivot(page).locator('.ex-pivot-report')).toHaveAttribute('inert', '');
            await expect(pivot(page).locator('.ex-pivot-field-list')).toHaveAttribute('inert', '');

            await page.keyboard.press('Escape');

            await expect(dialog).toHaveCount(0);
            await expect(pivot(page).locator('.ex-pivot-report')).not.toHaveAttribute('inert');
        });

        test(`ADR-0070: Escape in the dialog's grid peels the grid's own layers, then closes the dialog, and the arrows move the report's Focus again (${chrome})`, async ({ page }) => {
            await open(page, chrome, '&details=dialog');
            await firstValue(page).dblclick({ force: true });
            await expect(report(page)).toHaveAttribute('aria-activedescendant', /-r1c1$/);
            const dialog = page.getByRole('dialog', { name: /^Details: Americas \/ \w+ \/ \w+$/ });
            const records = dialog.locator('.ex-grid');
            await expect(records.locator('.ex-viewport .ex-row').first()).toBeVisible();
            // Close takes the keyboard as the dialog opens (PV-14), and the press below takes it
            // from there. A press made before Close has it is PV-41's case, below.
            await expect(dialog.getByRole('button', { name: 'Close', exact: true })).toBeFocused();

            // Into the records: their grid holds the keyboard, and the arrows are its own.
            await records.locator('.ex-viewport .ex-row').first().locator('[role=gridcell]').first().click({ force: true });
            await expect(records).toBeFocused();
            await page.keyboard.press('ArrowDown');
            await expect(records).toHaveAttribute('aria-activedescendant', /-r1c0$/);

            // The grid's own layer first: Escape closes its Context Menu, and the dialog stands.
            await page.keyboard.press('Shift+F10');
            const menu = records.locator('.ex-popover');
            await expect(menu).toBeVisible();
            await page.keyboard.press('Escape');
            await expect(menu).toHaveCount(0);
            await circuitQuiet();
            await expect(dialog).toBeVisible();
            await expect(records).toBeFocused();

            // Nothing left to dismiss in the grid: the dialog closes, and the report has the keyboard.
            await page.keyboard.press('Escape');

            await expect(dialog).toHaveCount(0);
            await expect(pivot(page).locator('.ex-pivot-report')).not.toHaveAttribute('inert');
            await expect(report(page)).toBeFocused();
            await page.keyboard.press('ArrowDown');
            await expect(report(page)).toHaveAttribute('aria-activedescendant', /-r2c1$/);
        });

        test(`ADR-0070 (decided with the user, 2026-10-13): Escape in the dialog's grid, then ↓ typed at once, closes the dialog and moves the report's Focus, as typed slowly (${chrome})`, async ({ page }) => {
            await open(page, chrome, '&details=dialog');
            await firstValue(page).dblclick({ force: true });
            await expect(report(page)).toHaveAttribute('aria-activedescendant', /-r1c1$/);
            const dialog = page.getByRole('dialog', { name: /^Details: Americas \/ \w+ \/ \w+$/ });
            const records = dialog.locator('.ex-grid');
            await expect(dialog.getByRole('button', { name: 'Close', exact: true })).toBeFocused();
            await records.locator('.ex-viewport .ex-row').first().locator('[role=gridcell]').first().click({ force: true });
            await expect(records).toBeFocused();
            await expect(records).toHaveAttribute('aria-activedescendant', /-r0c0$/);
            await setRoundTrip(80);

            // On a circuit the dialog stands a round trip and more after the Escape: the ↓ typed meanwhile
            // is the report's, passed out of the records' grid, held and handed on with the keyboard.
            await page.keyboard.press('Escape');
            await page.keyboard.press('ArrowDown');

            await expect(report(page)).toHaveAttribute('aria-activedescendant', /-r2c1$/);
            await expect(dialog).toHaveCount(0);
            await expect(report(page)).toBeFocused();
            await circuitQuiet();
            await expect(report(page)).toHaveAttribute('aria-activedescendant', /-r2c1$/);
        });

        test(`ADR-0070 (decided with the user, 2026-10-13): Escape on the dialog's Close, then ↓, → typed at once, closes it and moves the report's Focus down and right, in that order (${chrome})`, async ({ page }) => {
            await open(page, chrome, '&details=dialog');
            await firstValue(page).dblclick({ force: true });
            await expect(report(page)).toHaveAttribute('aria-activedescendant', /-r1c1$/);
            const dialog = page.getByRole('dialog', { name: /^Details: / });
            await expect(dialog.getByRole('button', { name: 'Close', exact: true })).toBeFocused();
            await setRoundTrip(80);

            await page.keyboard.press('Escape');
            await page.keyboard.press('ArrowDown');
            await page.keyboard.press('ArrowRight');

            await expect(report(page)).toHaveAttribute('aria-activedescendant', /-r2c2$/);
            await expect(dialog).toHaveCount(0);
            await expect(report(page)).toBeFocused();
        });

        test(`ADR-0070/0012: Escape held in the dialog's grid closes the dialog once, and its repeats leave the report the keyboard (${chrome})`, async ({ page }) => {
            await open(page, chrome, '&details=dialog');
            await firstValue(page).dblclick({ force: true });
            await expect(report(page)).toHaveAttribute('aria-activedescendant', /-r1c1$/);
            const dialog = page.getByRole('dialog', { name: /^Details: Americas \/ \w+ \/ \w+$/ });
            const records = dialog.locator('.ex-grid');
            await expect(records.locator('.ex-viewport .ex-row').first()).toBeVisible();
            // Close takes the keyboard as the dialog opens (PV-14), and the press below takes it
            // from there. A press made before Close has it is PV-41's case, below.
            await expect(dialog.getByRole('button', { name: 'Close', exact: true })).toBeFocused();
            await records.locator('.ex-viewport .ex-row').first().locator('[role=gridcell]').first().click({ force: true });
            await expect(records).toBeFocused();

            // Held: the first keydown is the press; Playwright sends the ones after it as the
            // browser sends a held key's repeats, and they reach the report once it has the keyboard.
            await page.keyboard.down('Escape');
            await expect(dialog).toHaveCount(0);
            await expect(report(page)).toBeFocused();
            await page.keyboard.down('Escape');
            await page.keyboard.down('Escape');
            await page.keyboard.up('Escape');

            // Once every answer to the repeats has landed (ADR-0056).
            await circuitQuiet();
            await expect(report(page)).toBeFocused();
            await page.keyboard.press('ArrowDown');
            await expect(report(page)).toHaveAttribute('aria-activedescendant', /-r2c1$/);
        });

        test(`ADR-0070: however the dialog closes — Escape on Close, Close, the backdrop — the arrows move the report's Focus again (${chrome})`, async ({ page }) => {
            await open(page, chrome, '&details=dialog');
            const dialog = page.getByRole('dialog', { name: /^Details: / });
            const close = dialog.getByRole('button', { name: 'Close', exact: true });
            const ways = {
                // Close holds the keyboard as the dialog opens, and the frame hears its Escape.
                escape: () => page.keyboard.press('Escape'),
                close: () => close.click(),
                // Beside the dialog, on the backdrop over the report.
                backdrop: () => page.locator('.ex-pivot-dialog-backdrop').click({ position: { x: 8, y: 8 } }),
            };
            for (const [how, closeIt] of Object.entries(ways)) {
                await firstValue(page).dblclick({ force: true });
                await expect(dialog).toBeVisible();
                await expect(close).toBeFocused();

                await closeIt();

                await expect(dialog, how).toHaveCount(0);
                await expect(report(page), how).toBeFocused();
                await page.keyboard.press('ArrowDown');
                await expect(report(page), how).toHaveAttribute('aria-activedescendant', /-r2c1$/);
            }
        });

        test(`ADR-0070: Escape in a details tab's grid closes nothing; the selected tab closed hands the keyboard to the tab selected next, and the last one back to the report (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            await firstValue(page).dblclick({ force: true });
            const tabs = pivot(page).getByRole('tablist');
            const details = tabs.getByRole('tab', { name: /^Details: / });
            const panel = pivot(page).getByRole('tabpanel');
            const records = panel.locator('.ex-grid');
            await expect(records.locator('.ex-viewport .ex-row').first()).toBeVisible();
            // The new tab takes the keyboard on its tab (ADR-0070), and the press below takes it
            // from there, as the second tab's is checked below. A press made before the tab has it
            // is PV-41's case, below.
            await expect(details).toBeFocused();

            // A tab is a sheet of its own, and Escape does not close a sheet: its grid keeps the
            // keyboard and releases Tab, as any grid's Escape with nothing to dismiss does (KB-8,
            // ADR-0012 rewritten 2026-10-01), and the tab stays. A press can engage an Interactive
            // cell, so the press's outcome is read before Escape; the release is then taken at the
            // Escape's keydown, and a Tab typed at once leaves (ADR-0230 section 1).
            await records.locator('.ex-viewport .ex-row').first().locator('[role=gridcell]').first().click({ force: true });
            await expect(records).toBeFocused();
            await settled(records);
            await page.keyboard.press('Escape');
            await settled(records);
            await circuitQuiet();
            await expect(records).toBeFocused();
            await expect(details).toHaveCount(1);
            await expect(panel).toBeVisible();
            await page.keyboard.press('Tab');
            await expect(records).not.toBeFocused();
            await circuitQuiet();
            await expect(details).toHaveCount(1);

            // A second tab, from the report once it is shown again (on Server, a round trip after
            // its tab is pressed); closed while selected, it hands the keyboard to the tab selected
            // next.
            await tabs.getByRole('tab', { name: 'PivotTable', exact: true }).click();
            await expect(panel).toHaveCount(0);
            await expect(rows(page).first()).toBeVisible();
            await rows(page).nth(2).locator('[role=gridcell]').nth(2).dblclick({ force: true });
            await expect(details).toHaveCount(2);
            await expect(details.nth(1)).toBeFocused();
            await tabs.getByRole('button', { name: `Close ${await details.nth(1).textContent()}`, exact: true }).click();
            await expect(details).toHaveCount(1);
            await expect(details).toHaveAttribute('aria-selected', 'true');
            await expect(details).toBeFocused();

            // The last one closed takes the tabs away: the report's grid has the keyboard back, on
            // the cell it left.
            await tabs.getByRole('button', { name: /^Close Details: Americas/ }).click();
            await expect(tabs).toHaveCount(0);
            await expect(report(page)).toBeFocused();
            await expect(report(page)).toHaveAttribute('aria-activedescendant', /-r2c2$/);
            await page.keyboard.press('ArrowDown');
            await expect(report(page)).toHaveAttribute('aria-activedescendant', /-r3c2$/);
        });

        // ExGrid's HandKeyboardToAsync, as ExPivot asks it (PV-41), and the scroller's hand-off to
        // its root (ADR-0033, A11Y-20). The control that takes the keyboard as a details view opens
        // takes it from the report's grid only while it is still the report's. The records'
        // scroller is focused in the task that draws them, as a press there would focus it. That is
        // before the control's request can land: on the Server host it comes a round trip after
        // that render. So the request finds the keyboard in the records, and leaves it there, and
        // their grid hands it to its root once the root is a tab stop, which comes later still. On
        // WebAssembly the request can land first, in the same turn; the end is the same either way.

        test(`ADR-0070/0033 (PV-41): the keyboard put in the dialog's records before Close's request lands stays there, on their root (${chrome})`, async ({ page }) => {
            await open(page, chrome, '&details=dialog');
            await focusOnArrival(page, '.ex-pivot-dialog .ex-grid .ex-scroller');

            await firstValue(page).dblclick({ force: true });
            const dialog = page.getByRole('dialog', { name: /^Details: / });
            const records = dialog.locator('.ex-grid');
            await expect(records.locator('.ex-viewport .ex-row').first()).toBeVisible();

            // Once Close's request and the records' own hand-off have landed (ADR-0056).
            await circuitQuiet();
            await expect(records).toBeFocused();
            await expect(dialog.getByRole('button', { name: 'Close', exact: true })).not.toBeFocused();
            const log = await focusLog(page);
            expect(log.at(-1)).toBe('records');
            if (SERVER) {
                expect(log).not.toContain('close');
            }
            // The keys are the records' own: the first places their Focus on their first cell.
            await page.keyboard.press('ArrowDown');
            await expect(records).toHaveAttribute('aria-activedescendant', /-r0c0$/);
            await expect(dialog).toBeVisible();
        });

        test(`ADR-0070/0033 (PV-41): the keyboard put in a new tab's records before the tab's request lands stays there, on their root (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            await focusOnArrival(page, '.ex-pivot-details-panel .ex-grid .ex-scroller');

            await firstValue(page).dblclick({ force: true });
            const details = pivot(page).getByRole('tablist').getByRole('tab', { name: /^Details: / });
            const records = pivot(page).getByRole('tabpanel').locator('.ex-grid');
            await expect(records.locator('.ex-viewport .ex-row').first()).toBeVisible();

            await circuitQuiet();
            await expect(records).toBeFocused();
            await expect(details).not.toBeFocused();
            await expect(details).toHaveAttribute('aria-selected', 'true');
            const log = await focusLog(page);
            expect(log.at(-1)).toBe('records');
            if (SERVER) {
                expect(log).not.toContain('tab');
            }
            await page.keyboard.press('ArrowDown');
            await expect(records).toHaveAttribute('aria-activedescendant', /-r0c0$/);
        });

        test(`ADR-0063: a page that listens to Show Details takes the trades, and neither a tab nor a dialog opens (${chrome})`, async ({ page }) => {
            await open(page, chrome, '&details=page');

            await firstValue(page).dblclick({ force: true });

            await expect(page.locator('#pivot-details-caption')).toContainText(/\d+ trades behind Sum of P&L where Region = Americas, Desk = \w+, Product = \w+\./);
            await expect(page.locator('#pivot-details .ex-viewport .ex-row').first()).toBeVisible();
            await circuitQuiet();
            await expect(pivot(page).getByRole('tablist')).toHaveCount(0);
            await expect(page.getByRole('dialog')).toHaveCount(0);
        });

        test(`ADR-0039/0061: the keyboard goes into a field's menu and back to its entry (${chrome})`, async ({ page }) => {
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

        test(`ADR-0061 (decided with the user, 2026-10-13): Escape in a field's menu, then Enter typed at once, runs no command and opens the menu again from its entry, as typed slowly (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            await entry(page, 'Region').click();
            const menu = page.getByRole('menu', { name: 'Options for Region' });
            await expect(menu.getByRole('menuitem', { name: 'Move Down' })).toBeFocused();
            await setRoundTrip(80);

            // On a circuit the menu stands a round trip after the Escape, Move Down holding the keyboard:
            // the Enter typed meanwhile is the entry's, which the keyboard goes back to, not Move Down's.
            await page.keyboard.press('Escape');
            await page.keyboard.press('Enter');

            await expect(menu.getByRole('menuitem', { name: 'Move Down' })).toBeFocused();
            await expect(entry(page, 'Region')).toHaveAttribute('aria-expanded', 'true');
            await circuitQuiet();
            await expect.poll(() => entriesOf(page, 'Rows')).toEqual(['Region', 'Desk']);
            await expect(menu).toHaveCount(1);
            await page.keyboard.press('Escape');
            await expect(menu).toHaveCount(0);
            await expect(entry(page, 'Region')).toBeFocused();
        });

        test(`ADR-0061 (decided with the user, 2026-10-13): with a 150 ms round trip, Enter on a field's entry and Enter typed at once press the menu's first command, as typed slowly (${chrome})`, async ({ page }) => {
            test.skip(!SERVER, 'WebAssembly opens the menu in the task of the press: no key is typed while it opens');
            await open(page, chrome);
            await entry(page, 'Region').click();
            const menu = page.getByRole('menu', { name: 'Options for Region' });
            await expect(menu.getByRole('menuitem', { name: 'Move Down' })).toBeFocused();
            await page.keyboard.press('Escape');
            await expect(menu).toHaveCount(0);
            await expect(entry(page, 'Region')).toBeFocused();
            await setRoundTrip(150);

            // The menu takes the keyboard a round trip after the first Enter: the second, typed on the
            // entry meanwhile, is held for the menu and presses its first command, Move Down, rather
            // than pressing the entry again and closing the menu.
            await page.keyboard.press('Enter');
            await page.keyboard.press('Enter');

            await expect.poll(() => entriesOf(page, 'Rows')).toEqual(['Desk', 'Region']);
            await expect(menu).toHaveCount(0);
            // The keyboard goes back to the pane's entries, as after Move Down pressed by hand.
            const focusedEntry = () => page.evaluate(() => document.activeElement?.getAttribute('aria-label') ?? '');
            await expect.poll(focusedEntry).toMatch(/^Options for /);
            await circuitQuiet();
            await expect.poll(() => entriesOf(page, 'Rows')).toEqual(['Desk', 'Region']);
            await expect(menu).toHaveCount(0);
            await expect.poll(focusedEntry).toMatch(/^Options for /);
        });

        test(`ADR-0061 (decided with the user, 2026-10-13): with a 150 ms round trip, letters typed straight after Enter on a report filter are typed into its Filter…'s search, as typed slowly (${chrome})`, async ({ page }) => {
            test.skip(!SERVER, 'WebAssembly opens the panel in the task of the press: no key is typed while it opens');
            await open(page, chrome);
            const button = toolbarButton(page, 'Filter Currency');
            await button.focus();
            await expect(button).toBeFocused();
            await setRoundTrip(150);

            // The panel's search takes the keyboard a round trip after Enter: the letters typed on the
            // button meanwhile are carried through the report's grid and typed into the search.
            await page.keyboard.press('Enter');
            await page.keyboard.type('US');

            const panel = page.getByRole('dialog', { name: 'Filter Currency' });
            const search = panel.locator('input').first();
            await expect(search).toHaveValue('US');
            await expect(search).toBeFocused();
            await expect(panel.getByRole('checkbox', { name: 'USD', exact: true })).toBeVisible();
            await expect(panel.getByRole('checkbox', { name: 'EUR', exact: true })).toHaveCount(0);
            await circuitQuiet();
            await expect(search).toHaveValue('US');
            await page.keyboard.press('Escape');
            await expect(panel).toHaveCount(0);
            await expect(button).toBeFocused();
        });

        test(`ADR-0061 (PV-11): pressing the report dismisses a field menu and keeps the cell and keyboard (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            await entry(page, 'Region').click();
            const menu = page.getByRole('menu', { name: 'Options for Region' });
            await expect(menu.getByRole('menuitem', { name: 'Move Down' })).toBeFocused();

            await firstValue(page).click({ force: true });

            await expect(menu).toHaveCount(0);
            await expect(report(page)).toBeFocused();
            await expect(report(page)).toHaveAttribute('aria-activedescendant', /-r1c1$/);
            await circuitQuiet();
            await expect(report(page)).toBeFocused();
            await expect.poll(() => entriesOf(page, 'Rows')).toEqual(['Region', 'Desk']);
        });

        test(`ADR-0061 (PV-11): a disabled item or padding keeps Escape inside the field menu (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            const menu = page.getByRole('menu', { name: 'Options for Region' });
            for (const target of ['disabled item', 'padding']) {
                await entry(page, 'Region').click();
                await expect(menu.getByRole('menuitem', { name: 'Move Down' })).toBeFocused();
                if (target === 'disabled item') {
                    const disabled = menu.getByRole('menuitem', { name: 'Move Up', exact: true });
                    await expect(disabled).toBeDisabled();
                    await disabled.click({ force: true });
                } else {
                    await menu.click({ position: { x: 2, y: 2 } });
                }
                await page.keyboard.press('Escape');
                await expect(menu).toHaveCount(0);
                await expect(entry(page, 'Region')).toBeFocused();
                await expect.poll(() => entriesOf(page, 'Rows')).toEqual(['Region', 'Desk']);
            }
        });

        test(`ADR-0061 (PV-11): focus outside a field menu dismisses it without taking the keyboard back (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            const menu = page.getByRole('menu', { name: 'Options for Region' });
            const search = pane(page).getByRole(chrome === 'builtin' ? 'searchbox' : 'textbox', { name: 'Search', exact: true });
            for (const destination of [report(page), search]) {
                await entry(page, 'Region').click();
                await expect(menu.getByRole('menuitem', { name: 'Move Down' })).toBeFocused();
                // Focus alone, with no pointer event that could hide a missing focus boundary.
                await destination.focus();
                await expect(menu).toHaveCount(0);
                await circuitQuiet();
                await expect(destination).toBeFocused();
            }
            await search.fill('Region');
            await expect(fieldsList(page).getByRole('checkbox')).toHaveCount(1);
            await expect(fieldsList(page).getByRole('checkbox', { name: 'Region', exact: true })).toBeVisible();
        });

        test(`ADR-0061 (PV-11): the field menu opener still toggles and another entry opens its own menu (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            const opener = entry(page, 'Region');
            const menu = page.getByRole('menu', { name: 'Options for Region' });
            await opener.click();
            await expect(menu.getByRole('menuitem', { name: 'Move Down' })).toBeFocused();
            await opener.focus();
            await circuitQuiet();
            await expect(menu).toBeVisible();
            // Hold the press through a render opportunity: it must not dismiss early and make
            // the release reopen a menu it was meant to close (ADR-0056).
            await opener.hover();
            await page.mouse.down();
            await circuitQuiet();
            await expect(menu).toBeVisible();
            await page.mouse.up();
            await expect(menu).toHaveCount(0);
            await expect(opener).toBeFocused();

            await opener.click();
            await expect(menu.getByRole('menuitem', { name: 'Move Down' })).toBeFocused();
            await entry(page, 'Product').click();
            const next = page.getByRole('menu', { name: 'Options for Product' });
            await expect(menu).toHaveCount(0);
            await expect(next.getByRole('menuitem', { name: 'Move to Report Filter', exact: true })).toBeFocused();
            await circuitQuiet();
            await expect(next).toBeVisible();
            await page.keyboard.press('Escape');
            await expect(next).toHaveCount(0);
            await expect(entry(page, 'Product')).toBeFocused();
        });

        test(`ADR-0061 (PV-11): a press on a pane caption dismisses only the field menu (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            await setRoundTrip(150);
            await entry(page, 'Region').click();
            await expect(page.getByRole('menuitem', { name: 'Move Down' })).toBeFocused();
            // This caption takes no DOM focus: a focusin handler alone cannot dismiss the menu.
            await pane(page).getByText('Drag fields between areas below:', { exact: true }).click();
            await expect(page.getByRole('menu')).toHaveCount(0);
            await circuitQuiet();
            await expect(entry(page, 'Region')).not.toBeFocused();
            await expect.poll(() => entriesOf(page, 'Rows')).toEqual(['Region', 'Desk']);

            await entry(page, 'Region').click();
            await page.getByRole('menuitem', { name: 'Field Settings…', exact: true }).click();
            const settings = page.getByRole('dialog', { name: 'Field Settings…', exact: true });
            await expect(settings).toBeVisible();
            // On Server, rendering the panel precedes its opening focus request (ADR-0039).
            await expect(settings.locator(':focus')).toHaveCount(1);
            await firstValue(page).click({ force: true });
            await expect(report(page)).toBeFocused();
            await circuitQuiet();
            await expect(settings).toBeVisible();
            await settings.getByRole('button', { name: 'Cancel', exact: true }).click();
            await expect(settings).toHaveCount(0);
        });

        test(`ADR-0061 (PV-11): back-to-back field menu dismissal and opening keep the new keyboard behind a round trip (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            await entry(page, 'Region').click();
            await expect(page.getByRole('menuitem', { name: 'Move Down' })).toBeFocused();
            await setRoundTrip(150);
            await firstValue(page).click({ force: true });
            await entry(page, 'Product').click();
            const menu = page.getByRole('menu', { name: 'Options for Product' });
            await expect(menu.getByRole('menuitem', { name: 'Move to Report Filter', exact: true })).toBeFocused();
            await circuitQuiet();
            await expect(menu).toBeVisible();
            await expect(menu.getByRole('menuitem', { name: 'Move to Report Filter', exact: true })).toBeFocused();
            await page.keyboard.press('Escape');
            await expect(menu).toHaveCount(0);
            await expect(entry(page, 'Product')).toBeFocused();
        });

        test(`ADR-0061: a menu drops down under its entry, as wide as the pane and over what follows (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            await setRoundTrip(150);
            // The Values Area stands in the pane's right-hand column.
            const opener = entry(page, 'Sum of P&L');
            await opener.click();
            const menu = page.getByRole('menu', { name: 'Options for Sum of P&L' });
            await expect(menu).toBeVisible();
            // Opening focus can scroll the body after the menu first renders on Server.
            await expect(menu.locator(':focus')).toHaveCount(1);
            await circuitQuiet();
            const { field, at, box, area } = await pane(page).evaluate(p => ({
                field: p.getBoundingClientRect().toJSON(),
                at: p.querySelector('button[aria-label="Options for Sum of P&L"]').getBoundingClientRect().toJSON(),
                box: p.querySelector('[role="menu"][aria-label="Options for Sum of P&L"]').getBoundingClientRect().toJSON(),
                area: p.querySelector('[role="list"][aria-label="Values"]').parentElement.getBoundingClientRect().toJSON(),
            }));
            expect(box.width).toBeGreaterThan(field.width - 40);
            expect(box.y).toBeGreaterThanOrEqual(at.y + at.height - 1);
            expect(box.y).toBeLessThan(at.y + at.height + 12);
            // Over what follows: the Values Area's own box did not grow to hold it.
            expect(area.y + area.height).toBeLessThan(box.y + box.height);
        });

        test(`ADR-0061: a command from a field's menu moves the field (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            await entry(page, 'Desk').click();

            await page.getByRole('menuitem', { name: 'Move to Column Labels' }).click();

            await expect.poll(() => entriesOf(page, 'Columns')).toEqual(['Product', 'Desk']);
            await expect.poll(() => entriesOf(page, 'Rows')).toEqual(['Region']);
            await expect(page.getByRole('menu')).toHaveCount(0);
        });

        test(`ADR-0061: the Pivot Toolbar above the report holds the report filter band on its left, then Layout and the Field List's toggle on its right (${chrome})`, async ({ page }) => {
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
            // The bundled source cannot be refreshed, so there is no Refresh (ADR-0066).
            await expect(toolbarButton(page, 'Refresh')).toHaveCount(0);
        });

        test(`ADR-0061: the report filter band filters the report (${chrome})`, async ({ page }) => {
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

        test(`ADR-0061: the band's Filter… opens under the Pivot Toolbar, over the report, and a press beside it closes it (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            const button = toolbarButton(page, 'Filter Currency');

            await button.click();
            const panel = page.getByRole('dialog', { name: 'Filter Currency' });
            await expect(panel.getByRole('checkbox', { name: 'USD', exact: true })).toBeVisible();
            await expect(panel).toBeVisible();
            const at = await button.boundingBox();
            const box = await panel.boundingBox();
            expect(box.y).toBeGreaterThanOrEqual(at.y + at.height - 1);
            expect(overlaps(box, await report(page).boundingBox())).toBe(true);
            await panel.getByRole('checkbox', { name: 'USD', exact: true }).uncheck();

            // A press on the report beside the panel lands on the backdrop: it closes the panel,
            // drops its draft, and the keyboard goes back to the button.
            // spec-lint: early-read: unchecking a box in the panel changes its draft only, and cannot move the report's box
            const grid = await report(page).boundingBox();
            await page.mouse.click(grid.x + grid.width - 20, grid.y + grid.height - 20);

            await expect(panel).toHaveCount(0);
            await expect(button).toContainText('(All)');
            await expect(button).toBeFocused();
        });

        test(`ADR-0061: the Layout menu opens under its button, over the report; a choice lays the report out again and the keyboard goes back to Layout (${chrome})`, async ({ page }) => {
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

        test(`ADR-0240 §6: a click on Layout ▾ and Enter typed at once press the menu's first enabled choice, and the keyboard goes back to Layout, as typed slowly (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            const button = toolbarButton(page, 'Layout');
            const menu = page.getByRole('menu', { name: 'Layout', exact: true });
            await setRoundTrip(150);

            // The click takes its place in the report's Input Order, and its answer opens the menu and
            // gives its first enabled choice, Do Not Show Subtotals, the keyboard. The Enter typed
            // meanwhile is held behind it and handed on there, where it presses the choice at its keydown.
            await button.click();
            await page.keyboard.press('Enter');

            await expect(page.locator('#pivot-status')).toContainText('1 changes made in the pane');
            await expect(menu).toHaveCount(0);
            await expect(button).toBeFocused();
            await circuitQuiet();
            await expect(button).toBeFocused();
            await setRoundTrip(0);
            await button.click();
            await expect(menu.getByRole('menuitemradio', { name: 'Do Not Show Subtotals' })).toHaveAttribute('aria-checked', 'true');
        });

        test(`ADR-0061: the Field List's toggle hides and shows the pane, and the page binds it (${chrome})`, async ({ page }) => {
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

        test(`ADR-0061: a cramped Field List keeps its heading close button reachable while scrolling (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            await setRoundTrip(150);
            await alterPage(page, () => {
                const root = document.querySelector('.ex-pivot');
                const before = root.getAttribute('style');
                root.style.setProperty('--ex-pivot-field-list-width', '160px');
                root.style.height = '260px';
                return () => root.setAttribute('style', before);
            });
            const body = pane(page).locator('.ex-pivot-pane-body, .mud-ex-pivot-pane-body');
            await body.evaluate(b => { b.scrollTop = b.scrollHeight; });
            await expect.poll(() => body.evaluate(b => b.scrollTop)).toBeGreaterThan(0);
            await entry(page, 'Region').click();
            await page.getByRole('menuitem', { name: 'Field Settings…', exact: true }).click();
            const settings = page.getByRole('dialog', { name: 'Field Settings…', exact: true });
            await expect(settings).toBeVisible();
            await expect(settings.locator(':focus')).toHaveCount(1);
            for (const offset of [0, 120]) {
                await body.evaluate((b, offset) => { b.scrollTop = offset; }, offset);
                await circuitQuiet();
                await expect.poll(() => body.evaluate(b => b.scrollTop)).toBe(offset);
                await expect.poll(() => body.evaluate(b => {
                    const opener = b.querySelector('[aria-label="Options for Region"]').getBoundingClientRect();
                    const panel = b.querySelector('[role="dialog"]').getBoundingClientRect();
                    return Math.abs(panel.top - opener.bottom);
                })).toBeLessThan(8);
            }
            // The panel's last action remains reachable through the body scroller.
            await settings.getByRole('button', { name: 'Cancel', exact: true }).click({ trial: true });
            const close = pane(page).getByRole('button', { name: 'Hide Field List', exact: true });
            await expect.poll(() => close.evaluate(b => {
                const box = b.getBoundingClientRect();
                const region = b.closest('[role=region]').getBoundingClientRect();
                return box.left >= region.left && box.right <= region.right && box.top >= region.top
                    && box.bottom <= region.bottom && b.contains(document.elementFromPoint(box.x + box.width / 2, box.y + box.height / 2));
            })).toBe(true);
            await close.click();
            await expect(pane(page)).toHaveCount(0);
            await expect(page.locator('#pivot-field-list-status')).toHaveText('Field List shown: False');
            await toolbarButton(page, 'Field List').click();
            await expect(pane(page)).toBeVisible();
            await expect(page.getByRole('dialog', { name: 'Field Settings…', exact: true })).toHaveCount(0);
            expect(await entriesOf(page, 'Rows')).toEqual(['Region', 'Desk']);
        });

        test(`ADR-0061: while Defer Layout Update is ticked the pane's changes wait for Update (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            const before = await reportRows(page);
            const update = pane(page).getByRole('button', { name: 'Update', exact: true });
            await expect(update).toBeDisabled();

            await pane(page).getByRole('checkbox', { name: 'Defer Layout Update' }).check();
            await page.getByRole('checkbox', { name: 'Month', exact: true }).check();

            await expect.poll(() => entriesOf(page, 'Rows')).toEqual(['Region', 'Desk', 'Month']);
            await expect(update).toBeEnabled();
            // The report did not move, once the host has said all it will (ADR-0056).
            await circuitQuiet();
            expect(await reportRows(page)).toBe(before);
            await expect(page.locator('#pivot-status')).toContainText('0 changes made in the pane');

            await update.click();

            await expect.poll(() => reportRows(page)).toBeGreaterThan(before);
            await expect(page.locator('#pivot-status')).toContainText('1 changes made in the pane');
            await expect(update).toBeDisabled();
        });

        test(`ADR-0060: the words switch speaks Excel's Japanese edition, and back (${chrome})`, async ({ page }) => {
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

        test(`ADR-0060: Month is the month of the trade date, painted Jan to Sep in the calendar's order, in either words (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            await entry(page, 'Product').click();
            await page.getByRole('menuitem', { name: 'Remove Field' }).click();
            await expect.poll(() => entriesOf(page, 'Columns')).toEqual([]);
            // A date part is declared Date, so a ticked one goes to Rows; from there, to Columns.
            await page.getByRole('checkbox', { name: 'Month', exact: true }).check();
            await expect.poll(() => entriesOf(page, 'Rows')).toEqual(['Region', 'Desk', 'Month']);
            await entry(page, 'Month').click();

            await page.getByRole('menuitem', { name: 'Move to Column Labels' }).click();

            await expect.poll(() => entriesOf(page, 'Columns')).toEqual(['Month']);
            // The trades run from 2 January to late September: nine Items, by the calendar, which
            // no order of their labels gives.
            const headers = report(page).getByRole('columnheader');
            await expect(headers).toHaveText(['Row Labels', 'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Grand Total']);

            await page.locator('#pivot-words').click();

            await expect(headers).toHaveText(['行ラベル', '1月', '2月', '3月', '4月', '5月', '6月', '7月', '8月', '9月', '総計']);
        });

        test(`ADR-0069: the code the page shows is the code it runs: the fields declared with PivotFields.Of, Month a part of the trade date (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            const shown = (file, region) => page.locator(`.demo-code code[data-file="${file}"][data-region="${region}"]`).textContent();

            const fields = await shown('DemoPivotData.cs', 'fields');
            expect(fields).toBe(codeRegion('DemoPivotData.cs', 'fields'));
            expect(fields).toContain('PivotFields.Of<DemoPivotTrade>()');
            expect(fields).toContain('.Month("Month", of: "TradeDate")');
            expect(await shown('PivotPage.razor', 'pivot')).toBe(codeRegion('PivotPage.razor', 'pivot'));
            expect(await shown('PivotPage.razor', 'save')).toBe(codeRegion('PivotPage.razor', 'save'));
        });
    });
}

// ExGrid's ReturnKeyboardAsync, as ExPivot asks it (DC-61): the conditions only a browser can show.
// On the Server host the request lands a round trip after the dialog or the tab went, and what the
// user chose in that time keeps the keyboard. On WebAssembly there is no round trip to add, and the
// same test is the case without one.

test('ADR-0070 (DC-61): a control focused while the report\'s keyboard is on its way back keeps it', async ({ page }) => {
    await open(page, 'builtin', '&details=dialog');
    await firstValue(page).dblclick({ force: true });
    const dialog = page.getByRole('dialog', { name: /^Details: / });
    const records = dialog.locator('.ex-grid');
    await expect(records.locator('.ex-viewport .ex-row').first()).toBeVisible();
    // Close takes the keyboard as the dialog opens (PV-14), and the press below takes it from
    // there; a press made before Close has it is PV-41's case.
    await expect(dialog.getByRole('button', { name: 'Close', exact: true })).toBeFocused();
    await records.locator('.ex-viewport .ex-row').first().locator('[role=gridcell]').first().click({ force: true });
    await expect(records).toBeFocused();
    const delayed = await setRoundTrip(150);

    // The Escape that closes the dialog, and at once a control of the page's, which the dialog left
    // reachable: it is outside the pivot.
    await page.keyboard.press('Escape');
    await page.locator('#pivot-save').focus();

    await expect(dialog).toHaveCount(0);
    // Past the report's request, which on Server comes two round trips after the Escape: there,
    // once the circuit is quiet (ADR-0056); the fixed wait is the page's own time on WebAssembly.
    // spec-lint: fixed-wait: the report's request for the keyboard is carried to it by the Consumer, as the dialog closes, and is not an input the report's gate took, which the Settled Mark does not cover (ADR-0220, As built); circuitQuiet() proves it on the Server host, and on WebAssembly nothing marks that the request will not come
    await page.waitForTimeout(delayed ? 1000 : 100);
    await circuitQuiet();
    await expect(page.locator('#pivot-save')).toBeFocused();
    await expect(report(page)).not.toBeFocused();
});

test('ADR-0070/0018 (DC-61): a second grid pressed while the keyboard is on its way back to the first keeps it', async ({ page }) => {
    // /pivot-db stands two pivots side by side: two report grids, each with its tabs (ADR-0069).
    const reset = await fetch(`${API_URL}/api/reset`, { method: 'POST' });
    expect(reset.ok).toBe(true);
    await page.goto('/pivot-db');
    const first = page.locator('#pivot-db-snapshot .ex-pivot');
    const second = page.locator('#pivot-db-server .ex-pivot');
    const reportOf = (p) => p.locator('.ex-pivot-sheet > .ex-grid');
    const rowsOf = (p) => reportOf(p).locator('.ex-viewport .ex-row');
    await expect(rowsOf(first).first()).toBeVisible({ timeout: 60_000 });
    await expect(rowsOf(second).first()).toBeVisible({ timeout: 60_000 });
    await rowsOf(first).nth(1).locator('[role=gridcell]').nth(1).dblclick({ force: true });
    const tabs = first.getByRole('tablist');
    await expect(tabs.getByRole('tab', { name: /^Details: / })).toBeFocused();
    const delayed = await setRoundTrip(150);

    // The first pivot's last tab closed: its report asks for the keyboard back. Meanwhile the user
    // presses a cell of the second pivot's report.
    await tabs.getByRole('button', { name: /^Close Details: / }).click();
    await rowsOf(second).nth(2).locator('[role=gridcell]').nth(1).click({ force: true });

    await expect(tabs).toHaveCount(0);
    // spec-lint: fixed-wait: the report's request for the keyboard follows the Close, a click Blazor hears on its own, and is not an input either grid's gate took, which the Settled Mark does not cover (ADR-0220, As built); circuitQuiet() proves it on the Server host, and on WebAssembly nothing marks that the request will not come
    await page.waitForTimeout(delayed ? 1000 : 100);
    await circuitQuiet();
    await expect(reportOf(second)).toBeFocused();
    await expect(reportOf(second)).toHaveAttribute('aria-activedescendant', /-r2c1$/);
    await page.keyboard.press('ArrowDown');
    await expect(reportOf(second)).toHaveAttribute('aria-activedescendant', /-r3c1$/);
    await expect(reportOf(first)).toHaveAttribute('aria-activedescendant', /-r1c1$/);
});

for (const chrome of ['builtin', 'mud']) {
    test(`PV-7 / ADR-0060: a number format the engine cannot write is refused by name, in the sample and on OK, and the report keeps its own (${chrome})`, async ({ page }) => {
        await open(page, chrome);
        const before = await report(page).locator('.ex-viewport').innerText();
        await entry(page, 'Sum of P&L').click();
        await page.getByRole('menuitem', { name: 'Value Field Settings…' }).click();
        const panel = page.getByRole('dialog', { name: 'Value Field Settings…' });
        await expect(panel).toBeVisible();

        await panel.getByRole('textbox', { name: 'Number format' }).fill('N999999999');
        await expect(panel.locator('.ex-pivot-sample, .mud-ex-pivot-sample')).toHaveText('This number format cannot be used.');
        await panel.getByRole('button', { name: 'OK', exact: true }).click();
        await expect(panel.getByRole('alert')).toHaveText('This number format cannot be used.');
        await expect(panel).toBeVisible();
        await settled(report(page));
        expect(await report(page).locator('.ex-viewport').innerText()).toBe(before);
    });
}

test('ADR-0039/0062: a MudSelect list inside Value Field Settings takes Escape before its panel', async ({ page }) => {
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
    await circuitQuiet();
    await expect(panel).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(panel).toHaveCount(0);
    await expect(entry(page, 'Sum of P&L')).toBeFocused();
});

for (const chrome of ['builtin', 'mud']) {
    test(`ADR-0360: with no report drawn, Field Settings' first control takes the keyboard (${chrome})`, async ({ page }) => {
        // The source's first answer waits for the page's button: the pane stands, and no report grid
        // does, so the keyboard cannot be handed on through one.
        await page.goto(`/pivot?chrome=${chrome}&first=held`);
        await stylesheetsLoaded(page);
        await expect(entry(page, 'Region')).toBeVisible({ timeout: 30_000 });
        await expect(report(page)).toHaveCount(0);

        await entry(page, 'Region').click();
        await page.getByRole('menuitem', { name: 'Field Settings…', exact: true }).click();
        const settings = page.getByRole('dialog', { name: 'Field Settings…', exact: true });
        await expect(settings).toBeVisible();
        // The panel's first control: under MudBlazor the sort order's select, inside the
        // `display: contents` box the Chrome named, which cannot take focus itself; under ExPivot's own
        // markup, the first subtotals choice, named itself.
        const first = chrome === 'mud' ? settings.locator('.mud-ex-pivot-sort input') : settings.getByRole('radio').first();
        await expect(first).toBeFocused();

        await page.keyboard.press('Escape');
        await expect(settings).toHaveCount(0);
        await expect(entry(page, 'Region')).toBeFocused();

        await page.locator('#pivot-answer-first').click();
        await expect(rows(page).first()).toBeVisible();
    });
}

test('ADR-0062/0030: the palette reaches the pane, the entries and the − button, light and dark', async ({ page }) => {
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

test('PV-18 / ADR-0062/0030: every Visual Token ExPivot reads is the palette entry the Wrapper maps it to, inside the report, light and dark', async ({ page }) => {
    await open(page, 'mud');
    // Each token ExPivot's stylesheet reads with a default (a Visual Token; a Geometry Token is read
    // without one, ADR-0027), resolved inside ExPivot's root, against the palette entry the Wrapper's
    // paper rule names for it, resolved there too: four properties a probe can take, so a colour, a
    // shadow, a radius and a size are each compared.
    const readings = () => page.evaluate(() => {
        const sheets = [...document.styleSheets];
        const rulesOf = (sheet) => { try { return [...sheet.cssRules]; } catch { return []; } };
        // Known by what they hold, not by name, which a host may fingerprint: ExPivot's writes rules
        // against its own classes, the Wrapper's maps --ex-pivot-color on the paper.
        const core = sheets.filter((s) => rulesOf(s).some((r) => r.selectorText?.startsWith('.ex-pivot')));
        const wrapper = sheets.filter((s) => rulesOf(s).some((r) => r.selectorText === '.mud-ex-grid' && r.style.getPropertyValue('--ex-pivot-color')));
        const tokens = [...new Set(core.flatMap(rulesOf).flatMap((r) => [...r.cssText.matchAll(/var\((--ex-pivot-[a-z0-9-]+)\s*,/g)].map((m) => m[1])))];
        const paper = wrapper.flatMap(rulesOf).find((r) => r.selectorText === '.mud-ex-grid' && r.style.getPropertyValue('--ex-pivot-color'));
        const root = document.querySelector('.ex-pivot');
        const probe = (value) => {
            const span = document.createElement('span');
            span.style.color = value;
            span.style.boxShadow = value;
            span.style.borderTopLeftRadius = value;
            span.style.fontSize = value;
            root.append(span);
            const style = getComputedStyle(span);
            const read = [style.color, style.boxShadow, style.borderTopLeftRadius, style.fontSize].join(' | ');
            span.remove();
            return read;
        };
        return {
            sheets: [core.length, wrapper.length],
            tokens: Object.fromEntries(tokens.map((token) => {
                const mapped = paper?.style.getPropertyValue(token).trim() ?? '';
                return [token, { mapped, shown: probe(`var(${token})`), palette: mapped ? probe(mapped) : null }];
            })),
        };
    });

    const light = await readings();
    expect(light.sheets, 'both stylesheets are on the page').toEqual([1, 1]);
    expect(Object.keys(light.tokens).length).toBeGreaterThan(10);
    for (const [token, { mapped, shown, palette }] of Object.entries(light.tokens)) {
        expect(mapped, `${token} is mapped`).toMatch(/var\(--mud-|^transparent$/);
        expect(shown, `${token}, light`).toBe(palette);
    }

    await page.locator('#toggle-dark').click();
    await expect(page.locator('#dark-status')).toHaveText('Dark: True');
    await expect.poll(async () => (await readings()).tokens['--ex-pivot-color'].shown).not.toBe(light.tokens['--ex-pivot-color'].shown);
    const dark = await readings();
    for (const [token, { shown, palette }] of Object.entries(dark.tokens)) {
        expect(shown, `${token}, dark`).toBe(palette);
    }
});
