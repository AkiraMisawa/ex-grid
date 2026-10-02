import { test, expect, circuitQuiet } from './fixtures.mjs';
import { codeRegion } from './demo-code.mjs';
import fs from 'node:fs';

// ExPivot over a CSV on /pivot-csv (ADR-0064, ADR-0069), under ExPivot's own markup and under
// ExPivot.MudBlazor's Chrome: a file the user chooses — through Blazor's InputFile, given here a
// file the test writes — or one the page writes in memory, read into a Snapshot under a Schema and
// pivoted. What only a browser can say: that the trade export reads back under the declared Schema
// to exactly the report /pivot paints from the same trades; that a malformed row refuses the whole
// file with the library's sentence naming the row and the column, and nothing is pivoted; that a
// large file paints its progress while it is read and Cancel stops it with nothing read; that an
// unknown file's suggested Schema is shown with what is not clear, applied only once confirmed,
// and read under to exact totals; and that the code the page shows is the code it runs.

// Tall and wide enough for the report and the pane beside it, under the page's controls.
test.use({ viewport: { width: 1400, height: 1100 } });

// The trade export the page declares a Schema for (DemoCsv.TradeExport): a comma between fields,
// ISO dates, money with a thousands separator, in quotes where it has one.
const EXPORT_HEADER = 'Id,Account,Region,Desk,Book,Product,Currency,Trade date,Notional,P&L,Quantity,Confirmed';
const REGIONS = ['Americas', 'EMEA', 'APAC'];
const DESKS = ['Rates', 'Credit', 'FX', 'Equities'];
const two = (n) => String(n).padStart(2, '0');
// One formatter for every record: toLocaleString makes one per call, thirty seconds a million.
const money = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2 });

/** Record `i` of a trade export the test writes, with `quantity` as written if it is given. */
function exportRecord(i, quantity = String(1 + (i % 49))) {
    const notional = money.format((1 + (i % 499)) * 10_000);
    return [
        `T${i}`, String(1000 + (i % 9000)).padStart(6, '0'), REGIONS[i % 3], DESKS[i % 4], 'LDN-RATES-01', 'Swap', 'EUR',
        `2026-${two(1 + (i % 9))}-${two(1 + (i % 28))}`, `"${notional}"`, ((i % 2000) - 1000.75).toFixed(2), quantity,
        i % 2 ? 'TRUE' : 'FALSE',
    ].join(',');
}

/** Writes a trade export of `count` records to `file`, a slice at a time. */
function writeExport(file, count, quantities = new Map()) {
    const fd = fs.openSync(file, 'w');
    try {
        fs.writeSync(fd, `${EXPORT_HEADER}\r\n`);
        for (let from = 1; from <= count; from += 50_000) {
            const lines = [];
            for (let i = from; i < Math.min(count + 1, from + 50_000); i++) {
                lines.push(exportRecord(i, quantities.get(i)));
            }
            fs.writeSync(fd, `${lines.join('\r\n')}\r\n`);
        }
    } finally {
        fs.closeSync(fd);
    }
}

const pivot = (page) => page.locator('.ex-pivot');
const report = (page) => pivot(page).locator('.ex-pivot-sheet > .ex-grid');
const rows = (page) => report(page).locator('.ex-viewport .ex-row');
const pane = (page) => page.getByRole('region', { name: 'PivotTable Fields' });
const status = (page) => page.locator('#csv-status');

/** Every row the report paints, top to bottom, as the texts of its cells, left to right. */
const paintedRows = (page) => rows(page).evaluateAll((painted) => painted
    .sort((a, b) => Number(a.getAttribute('aria-rowindex')) - Number(b.getAttribute('aria-rowindex')))
    .map((row) => [...row.querySelectorAll('[role=gridcell]')]
        .sort((a, b) => Number(a.getAttribute('aria-colindex')) - Number(b.getAttribute('aria-colindex')))
        .map((cell) => cell.textContent.trim())));

async function open(page, chrome) {
    await page.goto(`/pivot-csv?chrome=${chrome}`);
    await expect(page.locator('#csv-sample')).toBeEnabled();
    await expect(status(page)).toHaveText('Choose a file, or a sample.');
}

/** Waits for the report a read put up, its stylesheet landed. */
async function reportShown(page, chrome) {
    await expect(rows(page).first()).toBeVisible({ timeout: 30_000 });
    await expect.poll(() => pivot(page).evaluate((p) => getComputedStyle(p).display)).toBe('flex');
    if (chrome === 'mud') {
        await expect.poll(() => page.locator('.mud-ex-grid').first()
            .evaluate((p) => getComputedStyle(p).getPropertyValue('--ex-pivot-pane-background').trim())).not.toBe('');
    }
}

for (const chrome of ['builtin', 'mud']) {
    test.describe(`under the ${chrome} Chrome`, () => {
        test(`ADR-0064: /pivot's trades written as the trade export read back under the declared Schema to the very report /pivot paints (${chrome})`, async ({ page }) => {
            await page.goto(`/pivot?chrome=${chrome}`);
            await expect(rows(page).first()).toBeVisible({ timeout: 30_000 });
            // Every row of the report is painted, so the two are compared whole.
            const fromRecords = await paintedRows(page);
            expect(fromRecords).toHaveLength(Number(await report(page).getAttribute('aria-rowcount')));
            expect(fromRecords.at(-1)[0]).toBe('Grand Total');

            await open(page, chrome);
            await page.locator('#csv-sample').click();

            await expect(status(page)).toHaveText(/^trades-2000\.csv: 2,000 rows read in [\d.]+ s\.$/);
            await expect(page.locator('#csv-shown')).toHaveText('The report shows trades-2000.csv: 2,000 rows, read under the declared Schema.');
            await expect(page.locator('#csv-refusal')).toHaveText('');
            await reportShown(page, chrome);
            // The same layout over the same trades, read from text: every label and every value,
            // money summed exactly, the same.
            expect(await report(page).getAttribute('aria-rowcount')).toBe(String(fromRecords.length));
            expect(await paintedRows(page)).toEqual(fromRecords);
            // The whole file was read: the bar is full.
            const progress = page.locator('#csv-progress');
            expect(await progress.getAttribute('value')).toBe(await progress.getAttribute('max'));
        });

        test(`ADR-0064: a file with a malformed row is refused whole, by its row and column, and nothing is pivoted (${chrome})`, async ({ page }, testInfo) => {
            const file = testInfo.outputPath('trades-malformed.csv');
            writeExport(file, 5, new Map([[3, '12.5']]));
            await open(page, chrome);

            await page.locator('#csv-file').setInputFiles(file);

            // The library's own sentence: the row among the data records, the column, the value
            // and the line of the file.
            await expect(page.locator('#csv-refusal')).toHaveText("Row 3, column 'Quantity': '12.5' is not an integer (line 4).");
            await expect(page.locator('#csv-refusal')).toHaveAttribute('role', 'alert');
            await expect(status(page)).toHaveText('trades-malformed.csv was refused: nothing was read.');
            await circuitQuiet();
            await expect(pivot(page)).toHaveCount(0);

            // The page's own sample, with row 1,234 edited by hand.
            await page.locator('#csv-sample-malformed').click();

            await expect(page.locator('#csv-refusal')).toHaveText(/^Row 1,234, column 'Notional': '[\d,]+O\.00' is not a number \(line 1,235\)\.$/);
            await expect(status(page)).toHaveText('trades-malformed.csv was refused: nothing was read.');
            await circuitQuiet();
            await expect(pivot(page)).toHaveCount(0);
        });

        test(`ADR-0064: a large file paints its progress while it is read, and Cancel stops it with nothing read (${chrome})`, async ({ page }, testInfo) => {
            test.setTimeout(120_000);
            const file = testInfo.outputPath('trades-large.csv');
            writeExport(file, 1_000_000);
            const size = fs.statSync(file).size;
            await open(page, chrome);

            await page.locator('#csv-file').setInputFiles(file);

            await expect(status(page)).toHaveText('Reading trades-large.csv under the declared Schema…');
            // The bar is the file's bytes, and moves as they are read; the line counts the rows.
            const progress = page.locator('#csv-progress');
            await expect(progress).toHaveAttribute('max', String(size));
            await expect.poll(async () => Number(await progress.getAttribute('value')), { timeout: 60_000 }).toBeGreaterThan(0);
            await expect(page.locator('#csv-read')).toHaveText(/^[1-9][\d,]* rows, [\d.]+ of [\d.]+ MB$/);
            // Nothing else is chosen meanwhile.
            await expect(page.locator('#csv-file')).toBeDisabled();
            await expect(page.locator('#csv-sample')).toBeDisabled();

            await page.locator('#csv-cancel').click();

            await expect(status(page)).toHaveText(/^Cancelled after [\d,]+ rows: nothing was read\.$/);
            // And nothing read after it, once the host has said all it will (ADR-0056).
            await circuitQuiet();
            expect(Number(await progress.getAttribute('value'))).toBeLessThan(size);
            await expect(pivot(page)).toHaveCount(0);
            await expect(page.locator('#csv-refusal')).toHaveText('');
            await expect(page.locator('#csv-cancel')).toBeDisabled();
            await expect(page.locator('#csv-file')).toBeEnabled();
        });

        test(`ADR-0064: an unknown file's suggested Schema is shown with what is not clear, and the file is read under it only once confirmed (${chrome})`, async ({ page }, testInfo) => {
            // A spreadsheet set to German: semicolons, day-first dates, a decimal comma.
            const file = testInfo.outputPath('desk-export.csv');
            fs.writeFileSync(file, `${[
                'Account;Desk;Notional;Trade date',
                '00123;Rates;1.234,50;02.01.2026',
                '00456;Credit;2.000,25;15.03.2026',
                '00123;Rates;10.000,00;28.02.2026',
                '00789;FX;-500,75;01.04.2026',
                '00456;Credit;3,10;30.06.2026',
                '00789;FX;1.000.000,00;12.12.2026',
            ].join('\r\n')}\r\n`);
            await open(page, chrome);

            await page.locator('#csv-unknown-file').setInputFiles(file);

            const suggestion = page.locator('#csv-suggestion');
            await expect(suggestion.getByRole('heading')).toHaveText('The Schema suggested for desk-export.csv');
            await expect(page.locator('#csv-suggestion-file')).toContainText('From its first 6 rows: a semicolon between fields, a header row first, UTF-8.');
            const column = (name) => suggestion.locator(`tr[data-column="${name}"]`);
            await expect(column('Account').getByRole('combobox')).toHaveValue('Text');
            await expect(column('Account')).toContainText('leading zeros');
            await expect(column('Desk').getByRole('combobox')).toHaveValue('Text');
            await expect(column('Notional').getByRole('combobox')).toHaveValue('Decimal');
            await expect(column('Notional')).toContainText("decimal point ',', thousands '.'");
            await expect(column('Notional')).toContainText('is read as the decimal point');
            await expect(column('Trade date').getByRole('combobox')).toHaveValue('Date');
            // A proposal: nothing is read until the user confirms it.
            await circuitQuiet();
            await expect(pivot(page)).toHaveCount(0);
            await expect(status(page)).toHaveText('A Schema is suggested for desk-export.csv: confirm it to read the file.');

            await page.locator('#csv-confirm').click();

            await expect(status(page)).toHaveText(/^desk-export\.csv: 6 rows read in [\d.]+ s\.$/);
            await expect(suggestion).toHaveCount(0);
            await expect(page.locator('#csv-shown')).toHaveText('The report shows desk-export.csv: 6 rows, read under the confirmed Schema.');
            // One field per column, and no layout yet: the user builds the report.
            await expect(pivot(page).locator('.ex-pivot-empty')).toBeVisible();
            await pane(page).getByRole('checkbox', { name: 'Desk', exact: true }).check();
            await pane(page).getByRole('checkbox', { name: 'Notional', exact: true }).check();
            // The decimal comma read as the decimal point, the full stops as thousands: exact sums.
            await expect.poll(() => paintedRows(page)).toEqual([
                ['Credit', '2003.35'],
                ['FX', '999499.25'],
                ['Rates', '11234.5'],
                ['Grand Total', '1012737.1'],
            ]);
            // The account numbers were read as text, so they keep their zeros.
            await pane(page).getByRole('checkbox', { name: 'Account', exact: true }).check();
            await expect(rows(page).filter({ hasText: '00123' })).toHaveCount(1);
        });

        test(`ADR-0064: the page's two samples, one under the declared Schema and one under a suggested one, read the same trades to the same total (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            await page.locator('#csv-sample').click();
            await expect(status(page)).toHaveText(/^trades-2000\.csv: 2,000 rows read/);
            await reportShown(page, chrome);
            // Sum of P&L, in the first layout's #,##0.
            const declared = Number((await paintedRows(page)).at(-1).at(-1).replace(/,/g, ''));

            await page.locator('#csv-unknown-sample').click();
            const suggestion = page.locator('#csv-suggestion');
            await expect(suggestion.getByRole('heading')).toHaveText('The Schema suggested for trades-de.csv');
            await expect(suggestion.locator('tr[data-column="Account"]')).toContainText('leading zeros');
            await page.locator('#csv-confirm').click();
            await expect(status(page)).toHaveText(/^trades-de\.csv: 2,000 rows read/);
            await pane(page).getByRole('checkbox', { name: 'Desk', exact: true }).check();
            await pane(page).getByRole('checkbox', { name: 'P&L', exact: true }).check();

            // Sum of P&L by desk, in the default General format, which keeps the cents: the report
            // is asked again for each tick, so it is read once its total row carries the sum.
            const suggested = async () => {
                const total = (await paintedRows(page)).at(-1);
                return total?.[0] === 'Grand Total' && total.length === 2 ? Math.round(Number(total[1])) : null;
            };
            await expect.poll(suggested).toBe(declared);
        });

        test(`ADR-0069: the code the page shows is the code it runs (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            const shown = (file, region) => page.locator(`.demo-code code[data-file="${file}"][data-region="${region}"]`).textContent();

            const schema = await shown('DemoCsv.cs', 'schema');
            expect(schema).toBe(codeRegion('DemoCsv.cs', 'schema'));
            expect(schema).toContain('public static readonly CsvSchema TradeExport = new(');
            const input = await shown('PivotCsvPage.razor', 'input');
            expect(input).toBe(codeRegion('PivotCsvPage.razor', 'input'));
            expect(input).toContain('<InputFile');
            const choose = await shown('PivotCsvPage.razor', 'choose');
            expect(choose).toBe(codeRegion('PivotCsvPage.razor', 'choose'));
            expect(choose).toContain('OpenReadStream(MaxFileSize, token)');
            const read = await shown('PivotCsvPage.razor', 'read');
            expect(read).toBe(codeRegion('PivotCsvPage.razor', 'read'));
            expect(read).toContain('catch (SnapshotException refused)');
            const suggest = await shown('PivotCsvPage.razor', 'suggest');
            expect(suggest).toBe(codeRegion('PivotCsvPage.razor', 'suggest'));
            expect(suggest).toContain('CsvSchema.SuggestAsync(');
        });
    });
}
