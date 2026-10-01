import { test, expect } from './fixtures.mjs';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

// ExPivot on /pivot-risk (ADR-0059, ADR-0068), under ExPivot's own markup and under
// ExPivot.MudBlazor's Chrome: a rate-delta report with desks and curves in Rows and tenors in
// Columns. What only a browser can say: that the tenors are painted in the order the page's Order
// Key gives them, ON to 30Y, with 18M and 1Y6M two Items side by side; that the Filter… list of the
// tenor follows the same key; that without the key they fall back to the labels' order; and that
// every total painted is the sum of what it totals, down to the page's own sum of the positions.

// Wide enough for every tenor column beside the pane: the report grid paints only the columns in
// view (ADR-0004), and the order is read from what it paints.
test.use({ viewport: { width: 2400, height: 1100 } });

const TENORS = ['ON', 'TN', '1W', '1M', '3M', '6M', '9M', '1Y', '18M', '1Y6M', '2Y', '3Y', '5Y', '7Y', '10Y', '15Y', '20Y', '30Y'];

const PAGES = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../samples/ExGrid.DemoPages');

/** A region of the page's source, read as DemoCode.cs reads it to show it under "The code": the
 *  lines between `#region The code: name` and the next `#endregion`, less the indentation they share. */
function codeRegion(file, region) {
    const at = [path.join(PAGES, file), path.join(PAGES, 'Pages', file)].find((p) => fs.existsSync(p));
    const lines = fs.readFileSync(at, 'utf8').replace(/\r\n?/g, '\n').split('\n');
    const marker = (line) => line.trim().replace(/^@\*\s*(.*?)\s*\*@$/, '$1');
    const start = lines.findIndex((line) => marker(line) === `#region The code: ${region}`);
    const end = lines.findIndex((line, i) => i > start && marker(line).startsWith('#endregion'));
    if (start < 0 || end < 0) {
        throw new Error(`${file} has no region 'The code: ${region}'`);
    }
    const body = lines.slice(start + 1, end);
    const indent = Math.min(...body.filter((line) => line.trim()).map((line) => line.length - line.trimStart().length));
    return body.map((line) => (line.length >= indent ? line.slice(indent) : line.trimStart())).join('\n');
}

const pivot = (page) => page.locator('.ex-pivot');
const report = (page) => pivot(page).locator('.ex-pivot-sheet > .ex-grid');
const rows = (page) => report(page).locator('.ex-viewport .ex-row');
const pane = (page) => page.getByRole('region', { name: 'PivotTable Fields' });
const entry = (page, caption) => pane(page).getByRole('button', { name: `Options for ${caption}`, exact: true });

async function open(page, chrome) {
    await page.goto(`/pivot-risk?chrome=${chrome}`);
    await expect(rows(page).first()).toBeVisible({ timeout: 30_000 });
    await expect.poll(() => pivot(page).evaluate((p) => getComputedStyle(p).display)).toBe('flex');
    if (chrome === 'mud') {
        await expect.poll(() => page.locator('.mud-ex-grid').first()
            .evaluate((p) => getComputedStyle(p).getPropertyValue('--ex-pivot-pane-background').trim())).not.toBe('');
    }
    await expect(entry(page, 'Tenor')).toBeVisible();
}

/** The column headers the report paints, in their columns' order, after checking that it paints
 *  every column it has: none is left out of view. */
async function paintedHeaders(page) {
    const grid = report(page);
    const headers = await grid.locator('[role=columnheader]').evaluateAll((cells) => cells
        .map((cell) => ({ index: Number(cell.getAttribute('aria-colindex')), text: cell.textContent.trim() }))
        .sort((a, b) => a.index - b.index));
    expect(headers.map((h) => h.index), 'every column painted, once').toEqual(
        Array.from({ length: Number(await grid.getAttribute('aria-colcount')) }, (_, i) => i + 1));
    return headers.map((h) => h.text);
}

/** The report's rows as painted: each one's label, whether it is a group row (it has a − or +
 *  button), and its values, numbers by column, null where the cell is empty. */
async function paintedRows(page) {
    return rows(page).evaluateAll((painted) => painted
        .sort((a, b) => Number(a.getAttribute('aria-rowindex')) - Number(b.getAttribute('aria-rowindex')))
        .map((row) => {
            const cells = [...row.querySelectorAll('[role=gridcell]')]
                .sort((a, b) => Number(a.getAttribute('aria-colindex')) - Number(b.getAttribute('aria-colindex')));
            return {
                label: cells[0].querySelector('.ex-pivot-label-text')?.textContent.trim() ?? cells[0].textContent.trim(),
                group: cells[0].querySelector('.ex-pivot-toggle') !== null,
                values: cells.slice(1).map((cell) => {
                    const text = cell.textContent.trim();
                    return text === '' ? null : Number(text.replace(/,/g, '').replace('−', '-'));
                }),
            };
        }));
}

const sum = (values) => values.reduce((total, v) => total + (v ?? 0), 0);

for (const chrome of ['builtin', 'mud']) {
    test.describe(`under the ${chrome} Chrome`, () => {
        test(`ADR-0059: the tenors stand in the Order Key's order, ON to 30Y, with 18M and 1Y6M two Items side by side (${chrome})`, async ({ page }) => {
            await open(page, chrome);

            const headers = await paintedHeaders(page);

            expect(headers).toEqual(['Row Labels', ...TENORS, 'Grand Total']);
            // Both are 18 months: the key orders them together and merges nothing. 18M is the flow
            // and treasury desks' spelling, 1Y6M the options desk's, and each carries its own.
            const table = await paintedRows(page);
            const at = (label) => table.find((row) => row.label === label);
            const m18 = TENORS.indexOf('18M');
            expect(at('Rates Options').values[m18]).toBeNull();
            expect(at('Rates Options').values[m18 + 1]).not.toBeNull();
            expect(at('Rates Flow').values[m18]).not.toBeNull();
            expect(at('Rates Flow').values[m18 + 1]).toBeNull();
        });

        test(`ADR-0059: the tenor's Filter… lists its Items in the Order Key's order (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            await entry(page, 'Tenor').click();

            await page.getByRole('menuitem', { name: 'Filter…' }).click();

            // The Items, each a checkbox named by its label, read top to bottom as painted.
            const items = page.getByRole('group', { name: 'Tenor', exact: true });
            await expect(items.getByRole('checkbox', { name: '30Y', exact: true })).toBeAttached();
            await expect(items.getByRole('checkbox')).toHaveCount(TENORS.length + 1);
            const tops = [];
            for (const tenor of TENORS) {
                tops.push((await items.getByRole('checkbox', { name: tenor, exact: true }).boundingBox()).y);
            }
            const select = (await items.getByRole('checkbox', { name: '(Select All)', exact: true }).boundingBox()).y;
            expect([select, ...tops]).toEqual([select, ...tops].toSorted((a, b) => a - b));
            expect(new Set(tops).size).toBe(TENORS.length);

            await page.keyboard.press('Escape');
            await expect(items).toHaveCount(0);
        });

        test(`ADR-0059: without the Order Key the tenors fall back to the order of their labels (${chrome})`, async ({ page }) => {
            await open(page, chrome);

            await page.locator('#risk-order-key').uncheck();

            // The labels' order under the report's culture: 10Y before 1M, ON and TN after every
            // tenor that starts with a digit — what the key is there to prevent.
            const labels = [...TENORS].sort(new Intl.Collator('en-US').compare);
            await expect.poll(() => paintedHeaders(page)).toEqual(['Row Labels', ...labels, 'Grand Total']);
            expect(labels.indexOf('10Y')).toBeLessThan(labels.indexOf('1M'));

            await page.locator('#risk-order-key').check();
            await expect.poll(() => paintedHeaders(page)).toEqual(['Row Labels', ...TENORS, 'Grand Total']);
        });

        test(`ADR-0059: every total painted is the sum of what it totals, and the report's is the page's own sum of the positions (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            expect(await paintedHeaders(page)).toHaveLength(TENORS.length + 2);

            const table = await paintedRows(page);
            const grand = table.at(-1);
            expect(grand.label).toBe('Grand Total');
            // Three desks, each a group row carrying its subtotal at the top, over its curves.
            const desks = table.filter((row) => row.group);
            expect(desks.map((row) => row.label)).toEqual(['Rates Flow', 'Rates Options', 'Treasury']);
            const curves = table.filter((row) => !row.group && row !== grand);
            expect(curves).toHaveLength(8);

            // Across: each row's Grand Total is the sum of its tenors.
            for (const row of table) {
                expect(row.values.at(-1), `${row.label}'s Grand Total`).toBe(sum(row.values.slice(0, -1)));
            }
            // Down: each desk's subtotal is the sum of its curves, and the Grand Total row the sum
            // of every curve. PV01s are whole dollars, so nothing painted is rounded.
            for (let column = 0; column < grand.values.length; column++) {
                for (const desk of desks) {
                    const from = table.indexOf(desk) + 1;
                    const next = table.findIndex((row, i) => i >= from && (row.group || row === grand));
                    const own = table.slice(from, next).map((row) => row.values[column]);
                    expect(desk.values[column], `${desk.label}, column ${column + 1}`).toBe(own.every((v) => v === null) ? null : sum(own));
                }
                expect(grand.values[column], `Grand Total, column ${column + 1}`).toBe(sum(curves.map((row) => row.values[column])));
            }
            // The page sums the positions itself; the engine computed the report from the same ones.
            const net = /net PV01, summed by the page itself: (-?[\d,]+) USD/.exec(await page.locator('#risk-net').textContent());
            expect(grand.values.at(-1)).toBe(Number(net[1].replace(/,/g, '')));
        });

        test(`ADR-0068: the code the page shows is the code it runs, the README's Order Key among it (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            const shown = (file, region) => page.locator(`.demo-code code[data-file="${file}"][data-region="${region}"]`).textContent();

            const tenors = await shown('DemoRiskData.cs', 'tenors');
            expect(tenors).toBe(codeRegion('DemoRiskData.cs', 'tenors'));
            expect(tenors).toContain('public static IComparable? Months(string tenor)');
            const fields = await shown('DemoRiskData.cs', 'fields');
            expect(fields).toBe(codeRegion('DemoRiskData.cs', 'fields'));
            expect(fields).toContain('.Text("Tenor", p => p.Tenor, orderKey: Tenors.Months)');
            expect(await shown('PivotRiskPage.razor', 'pivot')).toBe(codeRegion('PivotRiskPage.razor', 'pivot'));
        });
    });
}
