// excel-behaviours.md item 18: cells copied from Excel and pasted into ExSheet, and the other way
// round, through the real Windows clipboard. Run on Windows from a copy of tests/ExGrid.Browser
// (for its node_modules), with the DemoHost on EXGRID_BASE_URL (default http://localhost:5299):
//
//     node clipboard-probe.mjs
//
// Excel is driven by excel-driver.ps1 through PowerShell, the page by Playwright with the
// installed Chrome, headed. The browser stays open while Excel pastes, so the page's clipboard
// write is not lost with its process. Prints one JSON document.
import { chromium } from '@playwright/test';
import { spawnSync } from 'node:child_process';

const BASE = process.env.EXGRID_BASE_URL ?? 'http://localhost:5299';
const DRIVER = process.env.EXCEL_DRIVER
    ?? '\\\\wsl.localhost\\Ubuntu-24.04\\home\\akira\\src\\hobby\\ex-grid\\verification\\2026-09-27-windows-excel\\excel-driver.ps1';

function excel(script) {
    const r = spawnSync('powershell.exe', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command',
        `. '${DRIVER}'; $xl = Connect-Excel; ${script}`], { encoding: 'utf8' });
    if (r.status !== 0) throw new Error(`PowerShell failed: ${r.stderr}`);
    return r.stdout.trim();
}

const out = {};

// 1. Excel: a Formula, a date and a number formatted #,##0.00, copied with Ctrl+C.
out.excelSource = JSON.parse(excel(`
    $ws = Reset-Book $xl @{ B1 = '3'; A1 = '=B1*2'; A2 = '9/26/2026'; A3 = '1234.5' }
    Set-ComProperty $ws.Range('A3') 'NumberFormat' '#,##0.00'
    # EXCEL_SOURCE_WIDE=1 fits column A to its text, so the date shows instead of ########.
    if ($env:EXCEL_SOURCE_WIDE -eq '1') { [void]$ws.Columns.Item(1).AutoFit() }
    [void]$ws.Range('A1:A3').Select()
    Send-Keys $xl '^c' 800
    (Get-State $xl @('A1','A2','A3')) | ConvertTo-Json -Depth 4 -Compress`));
out.clipboardFromExcel = excel(`Add-Type -AssemblyName System.Windows.Forms; [pscustomobject]@{ text = [System.Windows.Forms.Clipboard]::GetText(); html = [System.Windows.Forms.Clipboard]::GetText('Html') } | ConvertTo-Json -Compress`);

const browser = await chromium.launch({ channel: 'chrome', headless: false });
const context = await browser.newContext();
await context.grantPermissions(['clipboard-read', 'clipboard-write'], { origin: BASE });
const page = await context.newPage();
const messages = [];
page.on('console', (m) => { if (m.type() === 'error' || m.type() === 'warning') messages.push(`${m.type()}: ${m.text()}`); });
page.on('pageerror', (e) => messages.push(`pageerror: ${e}`));
await page.goto(`${BASE}/sheet`);
const grid = page.locator('.ex-grid').first();
await grid.locator('.ex-row').first().waitFor();
const cell = (row, col) => grid.locator(`[id$='r${row}c${col}']`);
// Cells are pointer-events: none by design (ADR-0004); the click goes through to the Viewport.
async function click(row, col, options = {}) {
    await cell(row, col).waitFor({ state: 'visible', timeout: 10_000 });
    await cell(row, col).click({ force: true, ...options });
    await page.waitForTimeout(150);
}
const bar = grid.locator('input.ex-formula-bar-text');
const nameBox = grid.locator('input.ex-name-box');
async function read(row, col) {
    await click(row, col);
    return { shown: (await cell(row, col).innerText()).trim(), bar: await bar.inputValue(), nameBox: await nameBox.inputValue() };
}

// 2. ExSheet: paste at F2 (row 1, column 5) and read F2:F4.
await click(1, 5);
await page.keyboard.press('Control+V');
await page.waitForTimeout(800);
out.pastedIntoExSheet = { F2: await read(1, 5), F3: await read(2, 5), F4: await read(3, 5) };
out.noticeAfterPaste = (await page.locator('.ex-sheet-notice').first().innerText()).trim();

// 3. ExSheet: the same three kinds typed at E2:E4 (column H is outside the 720-pixel viewport), E4
//    given #,##0.00 by the page's button, copied.
await click(1, 4);
await page.keyboard.type('=B2*2'); await page.keyboard.press('Enter'); await page.waitForTimeout(300);
await page.keyboard.type('9/26/2026'); await page.keyboard.press('Enter'); await page.waitForTimeout(300);
await page.keyboard.type('1234.5'); await page.keyboard.press('Enter'); await page.waitForTimeout(300);
await page.screenshot({ path: 'clipboard-probe-typed.png' });
await click(3, 4);
await page.locator('#sheet-money').click();
await page.waitForTimeout(300);
out.exSheetSource = { E2: await read(1, 4), E3: await read(2, 4), E4: await read(3, 4) };
await click(1, 4);
await click(3, 4, { modifiers: ['Shift'] });
await page.keyboard.press('Control+C');
await page.waitForTimeout(800);
out.clipboardFromExSheet = excel(`Add-Type -AssemblyName System.Windows.Forms; [pscustomobject]@{ text = [System.Windows.Forms.Clipboard]::GetText(); html = [System.Windows.Forms.Clipboard]::GetText('Html') } | ConvertTo-Json -Compress`);

// 4. Excel: paste at D1 and read D1:D3, while the browser is still open.
out.pastedIntoExcel = JSON.parse(excel(`
    $ws = $xl.ActiveSheet
    [void]$ws.Range('D1').Select()
    Send-Keys $xl '^v' 1000
    (Get-State $xl @('D1','D2','D3')) | ConvertTo-Json -Depth 4 -Compress`));
out.console = messages;

await browser.close();
console.log(JSON.stringify(out, null, 2));
