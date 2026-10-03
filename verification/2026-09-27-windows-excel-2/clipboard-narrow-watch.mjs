// Whether ExSheet shows its refusal for a paste from a too-narrow Excel column, and for how long:
// the notice, the Name Box and F7:F9 polled every 50 ms for three seconds after Ctrl+V.
import { chromium } from '@playwright/test';
import { spawnSync } from 'node:child_process';
const BASE = process.env.EXGRID_BASE_URL ?? 'http://localhost:5299';
const CHANNEL = process.env.EXGRID_CHANNEL ?? 'chrome';
const DRIVER = '\\\\wsl.localhost\\Ubuntu-24.04\\home\\akira\\src\\hobby\\ex-grid\\verification\\2026-09-27-windows-excel\\excel-driver.ps1';
const excel = (script) => {
    const r = spawnSync('powershell.exe', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', `. '${DRIVER}'; $xl = Connect-Excel; ${script}`], { encoding: 'utf8' });
    if (r.status !== 0) throw new Error(r.stderr);
    return r.stdout.trim();
};
excel(`$ws = Reset-Book $xl @{ B1 = '3'; A1 = '=B1*2'; A3 = '1234.5' }
    Set-ComProperty $ws.Range('A2') 'Value2' ([double]46291); Set-ComProperty $ws.Range('A2') 'NumberFormat' 'm/d/yyyy'
    Set-ComProperty $ws.Range('A3') 'NumberFormat' '#,##0.00'; $ws.Columns.Item(1).ColumnWidth = 6
    [void]$ws.Range('A1:A3').Select(); Send-Keys $xl '{ESC}' 200; Send-Keys $xl '^c' 800`);
const browser = await chromium.launch({ channel: CHANNEL, headless: false });
const context = await browser.newContext();
await context.grantPermissions(['clipboard-read', 'clipboard-write'], { origin: BASE });
const page = await context.newPage();
await page.goto(`${BASE}/sheet`);
await page.waitForFunction(() => !document.querySelector('.ex-grid[aria-busy]'));
const grid = page.locator('.ex-grid').first();
await grid.locator("[id$='-r0c0']").waitFor();
await page.bringToFront();
await grid.locator("[id$='-r6c5']").click({ force: true });
await page.waitForTimeout(300);
const messages = [];
page.on('console', (m) => messages.push(`${m.type()}: ${m.text()}`));
page.on('pageerror', (e) => messages.push(`pageerror: ${e}`));
await page.evaluate(() => {
    window.__pastes = [];
    document.addEventListener('paste', (e) => {
        const t = e.target;
        window.__pastes.push({
            target: t && (t.className || t.tagName), activeElement: document.activeElement && (document.activeElement.className || document.activeElement.tagName),
            types: [...e.clipboardData.types], text: e.clipboardData.getData('text/plain'),
            htmlLength: e.clipboardData.getData('text/html').length,
            htmlCells: [...e.clipboardData.getData('text/html').matchAll(/<td[^>]*>([^<]*)<\/td>/g)].map((m) => m[1]),
        });
    }, true);
    window.__notices = [];
    const n = document.querySelector('.ex-sheet-notice');
    new MutationObserver(() => window.__notices.push({ at: performance.now(), text: n.textContent })).observe(n, { childList: true, characterData: true, subtree: true });
});
const seen = [];
await page.keyboard.press('Control+V');
const t0 = Date.now();
while (Date.now() - t0 < 3000) {
    const s = await page.evaluate(() => ({
        notice: document.querySelector('.ex-sheet-notice')?.textContent.trim() ?? null,
        nameBox: document.querySelector('input.ex-name-box')?.value,
        F7_F9: [6, 7, 8].map((r) => document.querySelector(`[id$='-r${r}c5']`)?.textContent.trim()),
        selected: [...document.querySelectorAll('[aria-selected="true"]')].map((e) => e.id.replace(/^.*-(r\d+c\d+)$/, '$1')).join(' '),
        live: [...document.querySelectorAll('[aria-live]')].map((e) => e.textContent.trim()).filter((t) => t).join(' | '),
    }));
    const last = seen[seen.length - 1];
    if (!last || JSON.stringify(last.s) !== JSON.stringify(s)) seen.push({ ms: Date.now() - t0, s });
    await page.waitForTimeout(50);
}
console.log(JSON.stringify({ channel: CHANNEL, focused: await page.evaluate(() => document.hasFocus()), pastes: await page.evaluate(() => window.__pastes), notices: await page.evaluate(() => window.__notices), messages, seen }, null, 1));
await browser.close();
