// verify-on-windows-2.md Part C: cells copied from Excel into ExSheet and the other way round,
// through the real Windows clipboard, on one browser. Run on Windows from a copy of
// tests/ExGrid.Browser (for its node_modules), with the DemoHost on EXGRID_BASE_URL:
//
//     set EXGRID_CHANNEL=msedge & node clipboard-probe.mjs
//
// 1. Excel → ExSheet, the source column wide enough: a Formula, a date and #,##0.00.
//    The machine's regional format is en-GB for this run; /sheet is en-US.
// 2. Excel → ExSheet, the source column too narrow (the date shows ########), which ExSheet is to
//    refuse by name (e361956).
// 3. ExSheet → Excel by Ctrl+C (the keyboard route), and 4. by the Context Menu's Copy (the menu
//    route): what the Windows clipboard holds, whether its HTML carries data-ex-grid="invariant",
//    and what Excel writes when it pastes it.
// Excel is driven by excel-driver.ps1 (the first run's), the page by Playwright with the
// installed browser, headed, kept open while Excel pastes. Prints one JSON document.
import { chromium } from '@playwright/test';
import { spawnSync } from 'node:child_process';

const BASE = process.env.EXGRID_BASE_URL ?? 'http://localhost:5299';
const CHANNEL = process.env.EXGRID_CHANNEL ?? 'chrome';
const DRIVER = process.env.EXCEL_DRIVER
    ?? '\\\\wsl.localhost\\Ubuntu-24.04\\home\\akira\\src\\hobby\\ex-grid\\verification\\2026-09-27-windows-excel\\excel-driver.ps1';

function excel(script) {
    const r = spawnSync('powershell.exe', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command',
        `. '${DRIVER}'; $xl = Connect-Excel; ${script}`], { encoding: 'utf8' });
    if (r.status !== 0) throw new Error(`PowerShell failed: ${r.stderr}`);
    return r.stdout.trim();
}
function ps(script) {
    const r = spawnSync('powershell.exe', ['-NoProfile', '-Command', script], { encoding: 'utf8' });
    return r.stdout.trim();
}
const readClipboard = () => JSON.parse(excel(`Add-Type -AssemblyName System.Windows.Forms;
    $h = [System.Windows.Forms.Clipboard]::GetText('Html');
    [pscustomobject]@{ text = [System.Windows.Forms.Clipboard]::GetText(); html = $h; marker = ($h -match 'data-ex-grid="invariant"');
        formats = @([System.Windows.Forms.Clipboard]::GetDataObject().GetFormats()) } | ConvertTo-Json -Compress`));
// The date goes in as its serial number with an m/d/yyyy format, so that it is a date showing
// 9/26/2026 whatever the machine's regional format; typed, 9/26/2026 is text under en-GB.
const excelSource = (wide) => `
    $ws = Reset-Book $xl @{ B1 = '3'; A1 = '=B1*2'; A3 = '1234.5' }
    Set-ComProperty $ws.Range('A2') 'Value2' ([double]46291)
    Set-ComProperty $ws.Range('A2') 'NumberFormat' 'm/d/yyyy'
    Set-ComProperty $ws.Range('A3') 'NumberFormat' '#,##0.00'
    ${wide ? "[void]$ws.Columns.Item(1).AutoFit()" : "$ws.Columns.Item(1).ColumnWidth = 6"}
    [void]$ws.Range('A1:A3').Select()
    Send-Keys $xl '{ESC}' 200
    Send-Keys $xl '^c' 800
    (Get-State $xl @('A1','A2','A3')) | ConvertTo-Json -Depth 4 -Compress`;
const excelPaste = (at, read) => `
    $ws = $xl.ActiveSheet
    [void]$ws.Range('${at}').Select()
    Send-Keys $xl '^v' 1000
    (Get-State $xl @(${read.map((a) => `'${a}'`).join(',')})) | ConvertTo-Json -Depth 4 -Compress`;

const out = { channel: CHANNEL, base: BASE };
const browser = await chromium.launch({ channel: CHANNEL, headless: false });
const context = await browser.newContext();
await context.grantPermissions(['clipboard-read', 'clipboard-write'], { origin: BASE });
const page = await context.newPage();
const messages = [];
page.on('console', (m) => { if (m.type() === 'error' || m.type() === 'warning') messages.push(`${m.type()}: ${m.text()}`); });
page.on('pageerror', (e) => messages.push(`pageerror: ${e}`));
await page.goto(`${BASE}/sheet`);
await page.locator('#demo-interactive').waitFor({ state: 'attached' }).catch(() => {});
await page.waitForFunction(() => !document.querySelector('.ex-grid[aria-busy]'));
const grid = page.locator('.ex-grid').first();
await grid.locator("[id$='-r0c0']").waitFor();
const cell = (row, col) => grid.locator(`[id$='-r${row}c${col}']`);
const bar = grid.locator('input.ex-formula-bar-text');
const nameBox = grid.locator('input.ex-name-box');
const notice = page.locator('.ex-sheet-notice').first();
async function click(row, col, options = {}) {
    await cell(row, col).waitFor({ state: 'visible', timeout: 10_000 });
    await cell(row, col).click({ force: true, ...options });
    await page.waitForTimeout(250);
}
async function read(row, col) {
    await click(row, col);
    return { shown: (await cell(row, col).innerText()).trim(), bar: await bar.inputValue(), nameBox: await nameBox.inputValue() };
}
// The page's asynchronous clipboard needs the document focused, and Excel took the foreground to
// copy or paste: the browser's window is brought back first, and whether it has focus is recorded.
const focusLog = [];
async function focusBrowser(label) {
    await page.bringToFront();
    let focused = await page.evaluate(() => document.hasFocus());
    if (!focused) {
        const title = (await page.title()).replace(/'/g, "''");
        ps(`$w = New-Object -ComObject WScript.Shell; [void]$w.AppActivate('${title}')`);
        await page.waitForTimeout(600);
        focused = await page.evaluate(() => document.hasFocus());
    }
    focusLog.push({ step: label, focused });
    return focused;
}
async function pasteAt(row, col) {
    await focusBrowser(`paste at r${row}c${col}`);
    await click(row, col);
    await page.keyboard.press('Control+V');
    await page.waitForTimeout(1200);
}

// 1. Excel → ExSheet, wide.
out.excelSourceWide = JSON.parse(excel(excelSource(true)));
out.clipboardFromExcelWide = readClipboard();
// The notice is read straight after the paste: a click on a cell clears it.
await pasteAt(1, 5);
const noticeWide = (await notice.innerText()).trim();
out.pastedIntoExSheetWide = { notice: noticeWide, F2: await read(1, 5), F3: await read(2, 5), F4: await read(3, 5) };

// 2. Excel → ExSheet, narrow: the date shows ########.
out.excelSourceNarrow = JSON.parse(excel(excelSource(false)));
out.clipboardFromExcelNarrow = readClipboard();
await pasteAt(6, 5);
const noticeNarrow = (await notice.innerText()).trim();
out.pastedIntoExSheetNarrow = { notice: noticeNarrow, F7: await read(6, 5), F8: await read(7, 5), F9: await read(8, 5) };

// 3 and 4. ExSheet → Excel. The same three kinds typed at E2:E4, E4 given #,##0.00 by the page's button.
await focusBrowser('typing E2:E4');
await click(1, 4);
for (const typed of ['=B2*2', '9/26/2026', '1234.5']) {
    await page.keyboard.type(typed);
    await page.keyboard.press('Enter');
    await page.waitForTimeout(300);
}
await click(3, 4);
await page.locator('#sheet-money').click();
await page.waitForTimeout(300);
out.exSheetSource = { E2: await read(1, 4), E3: await read(2, 4), E4: await read(3, 4) };

// 3. The keyboard route.
await focusBrowser('keyboard copy');
await click(1, 4);
await click(3, 4, { modifiers: ['Shift'] });
await page.keyboard.press('Control+C');
await page.waitForTimeout(1000);
out.clipboardFromExSheetKeyboard = readClipboard();
out.pastedIntoExcelKeyboard = JSON.parse(excel(excelPaste('D1', ['D1', 'D2', 'D3'])));

// 4. The menu route: a copy from the Context Menu, which fires no copy event.
await focusBrowser('menu copy');
await click(1, 4);
await click(3, 4, { modifiers: ['Shift'] });
await cell(1, 4).click({ force: true, button: 'right' });
await page.getByRole('menuitem', { name: /^Copy$/ }).click();
await page.waitForTimeout(1000);
out.clipboardFromExSheetMenu = readClipboard();
out.pastedIntoExcelMenu = JSON.parse(excel(excelPaste('H1', ['H1', 'H2', 'H3'])));
out.focus = focusLog;
out.console = messages;

await browser.close();
console.log(JSON.stringify(out, null, 2));
