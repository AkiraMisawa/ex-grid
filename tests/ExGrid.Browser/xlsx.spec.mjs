import { test, expect, settled } from './fixtures.mjs';
import { sheet, cell, clickCell, CIRCUIT_SHEET } from './sheet-helpers.mjs';

// Opening and saving an .xlsx at its place in the Input Order (ADR-0330 §10; exsheet-xlsx ticket 06), on
// /xlsx under both Chromes. The page opens the file it is given through ExSheet's OpenAsync, reading it
// with ExSheet.Xlsx; under ?hold=1 each read waits, once the file is chosen, until "Let the read go" is
// pressed, so the opening is under way for as long as the test needs and no clock decides it (ADR-0220).
// A key typed meanwhile, at machine speed, waits for the opening and lands on the Workbook it opened;
// Escape cancels the opening (ADR-0240 §12), and the Workbook stays as it was. The file opened is the
// page's own sample, two Sheets written by ExSheet.Xlsx, read off its download link.

test.use({ viewport: { width: 1280, height: 1000 } });

const XLSX = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

/** The bytes a data: link of the page carries, as a file to choose. */
async function fileOf(page, link, name) {
    const href = await page.locator(link).getAttribute('href');
    return { name, mimeType: XLSX, buffer: Buffer.from(href.slice(href.indexOf(',') + 1), 'base64') };
}

for (const chrome of ['builtin', 'mud']) {
    test.describe(`ADR-0330 .xlsx in the Input Order (${chrome})`, () => {
        async function openPage(page, hold, component = undefined) {
            const query = [hold ? 'hold=1' : null, chrome === 'builtin' ? null : `chrome=${chrome}`, component === undefined ? null : `component=${component}`]
                .filter(Boolean).join('&');
            await page.goto(query ? `/xlsx?${query}` : '/xlsx');
            await expect(cell(sheet(page), 'A1')).toHaveText('Before the file');
            // Enabled once InputFile's script is attached: on the Server host a file set before then is never heard.
            await expect(page.locator('#xlsx-file')).toBeEnabled();
            await expect(page.locator('#xlsx-sample')).toBeAttached();
        }

        const tab = (page, name) => page.locator('.ex-sheet-tab', { hasText: name });
        const notice = (page) => page.locator('.ex-sheet-notice');

        test('XL-1: a key typed while a file opens waits for it, and lands on the Workbook it opened', async ({ page }) => {
            // On the Server host, the Razor component, whose key crosses the circuit behind the file's choice and so
            // after the page's call to OpenAsync. Over the Script Core the opening takes its place when that call
            // reaches the browser, a round trip after the choice, and a key typed before it means the Workbook the
            // page still showed (ADR-0240 §12, ADR-0350 section 6), as XL-2 reads for Escape.
            await openPage(page, true, CIRCUIT_SHEET);
            const grid = sheet(page);
            const sample = await fileOf(page, '#xlsx-sample', 'sample.xlsx');

            // At machine speed, nothing waited for between them: the key is typed as the file is chosen, while it is
            // still to be read. The Workbook before has one Sheet, the one opened two.
            await clickCell(grid, 'A1');
            await page.locator('#xlsx-file').setInputFiles(sample);
            await page.keyboard.press('Control+PageDown');
            await page.locator('#xlsx-release').click();

            // The Sheet's notice said the file opened, and the key taken after it has cleared it.
            await expect(page.locator('#xlsx-said')).toHaveText('Opened sample.xlsx.');
            await expect(tab(page, 'Two')).toHaveAttribute('aria-selected', 'true');
            await expect(page.locator('.ex-sheet-tab')).toHaveText(['One', 'Two']);
            await settled(grid);
            await expect(cell(grid, 'A1')).toHaveText('42');
            await expect(grid).not.toHaveAttribute('data-ex-work', /./);
            await expect(grid).not.toHaveAttribute('aria-busy', /./);
        });

        test('XL-2: Escape typed as a file is chosen cancels the opening, says so, and leaves the Workbook as it was', async ({ page }) => {
            await openPage(page, true);
            const grid = sheet(page);
            const sample = await fileOf(page, '#xlsx-sample', 'sample.xlsx');

            // An Escape means what the page it was typed against showed (ADR-0240 §12): one typed against a Settled
            // Stamp that does not yet name the opening (data-ex-work-placed) is an ordinary key and cancels nothing.
            // So the Escape is typed against a page that shows the opening, as a user who sees it does, and the
            // cancel names it. The read is held (hold=1), so the opening is under way however long that takes; no
            // clock decides it.
            await clickCell(grid, 'A1');
            await page.locator('#xlsx-file').setInputFiles(sample);
            await expect(grid.locator(':scope > [data-ex-work-placed]')).toBeAttached();
            await page.keyboard.press('Escape');

            await expect(notice(page)).toHaveText('Nothing was opened: the opening was cancelled. The Workbook is as it was.');
            await expect(page.locator('#xlsx-said')).toHaveText('sample.xlsx was not opened: the opening was cancelled.');
            await settled(grid);
            await expect(page.locator('.ex-sheet-tab')).toHaveText(['Before']);
            await expect(cell(grid, 'A1')).toHaveText('Before the file');
            await expect(grid).not.toHaveAttribute('data-ex-work', /./);
            await expect(grid).not.toHaveAttribute('aria-busy', /./);
            await expect(page.locator('#xlsx-release')).toHaveCount(0);
        });

        test('XL-3: a Workbook saved, then opened again, comes back as it was saved', async ({ page }) => {
            await openPage(page, false);
            const grid = sheet(page);
            await clickCell(grid, 'B2');
            await page.keyboard.type('=6*7');
            await page.keyboard.press('Enter');
            await settled(grid);
            await expect(cell(grid, 'B2')).toHaveText('42');

            await page.locator('#xlsx-save').click();
            await expect(page.locator('#xlsx-said')).toHaveText(/^Saved [\d,]+ bytes\.$/);
            const saved = await fileOf(page, '#xlsx-saved', 'workbook.xlsx');
            await page.locator('#xlsx-file').setInputFiles(saved);

            await expect(page.locator('#xlsx-said')).toHaveText('Opened workbook.xlsx.');
            await expect(notice(page)).toHaveText('The file was opened.');
            await settled(grid);
            await expect(page.locator('.ex-sheet-tab')).toHaveText(['Before']);
            await expect(cell(grid, 'A1')).toHaveText('Before the file');
            await expect(cell(grid, 'B2')).toHaveText('42');
            await expect(page.locator('#xlsx-report')).toHaveCount(0);
        });
    });
}
