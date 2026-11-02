// Temporary probe (ADR-0450, ED-30): a real Microsoft IME composing through SendInput, on one channel: in a plain
// field, then on /sheet of the React host (the sixteenth run's k2: D10, kana, Space, Enter, Enter).
//     node ime.mjs <chrome|msedge> [base url of the React host]
import { chromium } from 'playwright-core';
import { spawn } from 'node:child_process';
import readline from 'node:readline';
import { fileURLToPath } from 'node:url';

const channel = process.argv[2];
const base = process.argv[3];
const helper = spawn('powershell.exe', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', fileURLToPath(new URL('./input-server.ps1', import.meta.url))], { stdio: ['pipe', 'pipe', 'inherit'] });
const lines = readline.createInterface({ input: helper.stdout });
const waiting = [];
lines.on('line', (l) => { const w = waiting.shift(); if (w) w(l.trim()); else console.log('helper:', l); });
const ask = (cmd) => new Promise((done, fail) => {
    const timer = setTimeout(() => fail(new Error(`no answer to ${cmd}`)), 20_000);
    waiting.push((l) => { clearTimeout(timer); console.log(`> ${cmd}\n< ${l}`); done(l); });
    helper.stdin.write(cmd + '\n');
});
await new Promise((done) => waiting.push(done));

const browser = await chromium.launch({ channel, headless: false, args: ['--window-size=1400,900'] });
const context = await browser.newContext({ viewport: null });
try {
    // A plain field.
    const TITLE = `imeprobe-${channel}`;
    const page = await context.newPage();
    await page.setContent(`<!doctype html><title>${TITLE}</title><input id=f style="font-size:24px;width:600px"><script>
window.log = [];
for (const t of ['keydown', 'keyup']) addEventListener(t, (e) => log.push(t + ' ' + e.key + ' kc=' + e.keyCode + ' composing=' + e.isComposing), true);
for (const t of ['compositionstart', 'compositionupdate', 'compositionend']) addEventListener(t, (e) => log.push(t + ' ' + JSON.stringify(e.data)), true);
</script>`);
    await page.bringToFront();
    await page.click('#f');
    const read = async (label) => console.log(`--- ${channel} ${label}: ${JSON.stringify(await page.evaluate(() => ({ log: log.splice(0), value: document.getElementById('f').value })))}`);
    await ask(`front ${TITLE}`);
    await ask(`japanese ${TITLE}`);
    await ask('imeon');
    await ask(`imestate ${TITLE}`);
    await ask('type 60 nihon{SPACE}');
    await page.waitForTimeout(500);
    await read('nihon space');
    await ask('type 60 {ENTER}');
    await page.waitForTimeout(500);
    await read('enter');
    await page.close();

    // /sheet on the React host: k2.
    if (base) {
        const sheet = await context.newPage();
        await sheet.goto(`${base}/sheet`);
        const grid = sheet.locator('.ex-grid:has(> .ex-formula-bar)').first();
        const cellAt = (row, column) => grid.locator(`[id$='-r${row}c${column}']`);
        await cellAt(11, 1).filter({ hasText: '318.25' }).waitFor({ timeout: 30_000 });
        const title = await sheet.title();
        await sheet.bringToFront();
        const box = await cellAt(9, 3).boundingBox();
        await sheet.mouse.click(box.x + box.width / 2, box.y + box.height / 2);
        await sheet.evaluate(() => {
            window.log = [];
            for (const t of ['keydown']) addEventListener(t, (e) => log.push(`${t} ${e.key} ${e.isComposing ? 'composing ' : ''}@${document.activeElement?.className ?? ''}`), true);
            for (const t of ['compositionstart', 'compositionend']) addEventListener(t, (e) => log.push(`${t} ${JSON.stringify(e.data)} @${e.target.className}`), true);
        });
        const state = async (label) => console.log(`--- ${channel} sheet ${label}: ${JSON.stringify(await sheet.evaluate(() => ({
            log: log.splice(0),
            active: `${document.activeElement?.tagName}.${document.activeElement?.className}`,
            activeValue: document.activeElement?.value,
            nameBox: document.querySelector('.ex-name-box input, .ex-name-box')?.value ?? document.querySelector('.ex-name-box')?.textContent,
        })))} D10=${JSON.stringify(await cellAt(9, 3).textContent())}`);
        await state('pressed D10');
        await ask(`front ${title}`);
        await ask(`japanese ${title}`);
        await ask('imeon');
        await ask(`imestate ${title}`);
        for (const [label, keys] of [['k', 'k'], ['a', 'a'], ['n', 'n'], ['a2', 'a'], ['space', '{SPACE}'], ['enter-1', '{ENTER}'], ['enter-2', '{ENTER}']]) {
            await ask(`type 60 ${keys}`);
            await sheet.waitForTimeout(600);
            await state(label);
        }
    }
} finally {
    helper.stdin.end('quit\n');
    await browser.close();
}
