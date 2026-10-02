// docs/specs/exsheet/verify-on-windows-16.md, Part D, d5: what a screen reader says on /sheet. NVDA is not
// installed on this machine; Narrator, Windows' own, is. Nothing was installed. Narrator shows no
// speech viewer of NVDA's kind, but it keeps a speech recap: the Narrator key with Ctrl+X copies the last
// phrase it spoke to the clipboard, and with Alt+X opens a window listing what it spoke. This script reads
// both. The first try stopped at its start (a window of Narrator's in front, Chrome not brought back, Narrator
// not stopped by Stop-Process); this one retries the way back and stops Narrator with its own keys. Tab into the grid from the element before it (the Revalue button, given focus by script, as in
// Part C), then Down, then Right.
//
//   set EXGRID_CHANNEL=chrome & set INPUT=...\input-server.ps1 & set OUT=...\d5.json
//   node d5-narrator.mjs http://localhost:5299
//
// Playwright opens the page and reads the DOM; every key is real OS input through input-server.ps1.
import { chromium } from '@playwright/test';
import { spawn } from 'node:child_process';
import readline from 'node:readline';
import fs from 'node:fs';

const BASE = process.argv[2] ?? 'http://localhost:5299';
const CHANNEL = process.env.EXGRID_CHANNEL ?? 'chrome';
const OUT = process.env.OUT;
const TITLE = `ime16-d5-${CHANNEL}`;
const out = { channel: CHANNEL, base: BASE, started: new Date().toISOString(), steps: [], messages: [] };
const save = () => { if (OUT) fs.writeFileSync(OUT, JSON.stringify(out, null, 1)); };
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

const helper = spawn('powershell.exe', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', process.env.INPUT], { stdio: ['pipe', 'pipe', 'pipe'] });
const lines = readline.createInterface({ input: helper.stdout });
const waiting = [];
lines.on('line', (l) => { const w = waiting.shift(); if (w) w(l.trim()); });
const firstLine = new Promise((done) => waiting.push(done));
const input = (cmd) => new Promise((done, fail) => {
    waiting.push((l) => (l.startsWith('ok') ? done(l.replace(/ t=\d+$/, '')) : fail(new Error(`${cmd.slice(0, 60)}: ${l}`))));
    helper.stdin.write(cmd + '\n');
});
if ((await firstLine) !== 'ready') throw new Error('input-server did not start');

const browser = await chromium.launch({ channel: CHANNEL, headless: false, args: ['--window-position=40,40', '--window-size=1600,1050'] });
out.browserVersion = browser.version();
const context = await browser.newContext({ viewport: null });
const page = await context.newPage();
page.on('console', (m) => out.messages.push({ type: m.type(), text: m.text() }));
page.on('pageerror', (e) => out.messages.push({ type: 'pageerror', text: String(e) }));
await page.goto(`${BASE}/sheet`);
await page.waitForFunction(() => {
    const g = document.querySelector('.ex-grid:has(> .ex-formula-bar)');
    return g && g.querySelector('input.ex-key-field') && [...g.querySelectorAll('[id$="-r11c1"]')].some((e) => e.textContent.includes('318.25'));
}, null, { timeout: 60_000 });
await page.evaluate((t) => { document.title = t; }, TITLE);
const where = () => page.evaluate(() => {
    const g = document.querySelector('.ex-grid:has(> .ex-formula-bar)');
    const f = g.querySelector('input.ex-key-field');
    const ae = document.activeElement;
    const letters = (c) => { let s = ''; c += 1; while (c > 0) { const m = (c - 1) % 26; s = String.fromCharCode(65 + m) + s; c = Math.floor((c - 1) / 26); } return s; };
    const id = f?.getAttribute('aria-activedescendant');
    return { active: ae === f ? 'key-field' : ae === g ? 'root' : `${ae.tagName.toLowerCase()}${ae.id ? '#' + ae.id : ''}`, focus: id?.replace(/^.*-r(\d+)c(\d+)$/, (x, r, c) => letters(+c) + (+r + 1)) ?? null,
        ring: g.hasAttribute('data-ex-focus-visible'), cell: id ? document.getElementById(id)?.textContent ?? null : null };
});

try {
    await input(`front ${TITLE}`);
    out.keyboard = { set: await input(`english ${TITLE}`) };
    await input('imeoff');
    out.narratorRunningBefore = await input('narratortext 1500');
    // The element before the grid holds focus, given by script.
    out.focusBefore = await page.evaluate(() => { const b = document.querySelector('#sheet-revalue'); b.focus(); return document.activeElement === b; });
    out.narratorStart = await input('narrator start');
    out.frontAfterStart = await input('foreground');
    out.narratorWindowsAtStart = await input('narratortext 3000');
    // Back to Chrome: Narrator's first start put a window of its own in front ("Narrator updates", the
    // first try of d5), and the first attempt may be refused while it stands.
    out.back = [];
    for (let i = 0; i < 4; i++) {
        try { out.back.push(await input(`front ${TITLE}`)); break; } catch (e) { out.back.push(String(e).slice(0, 160)); await sleep(1500); }
    }
    await sleep(3000);
    const step = async (name, keys, wait = 3500) => {
        const r = { step: name, keys };
        if (keys) r.sent = await input(`type 30 ${keys}`);
        await sleep(wait);
        r.page = await where();
        r.front = await input('foreground');
        // The last phrase Narrator spoke, copied by Narrator's own command (Narrator key + Ctrl + X).
        r.copy = await input('narratorkeys 800 CTRL+X');
        r.clipboard = await input('clipboard');
        out.steps.push(r); save();
    };
    await step('before (Chrome back in front, the Revalue button focused)', null);
    await step('tab into the grid', '{TAB}');
    await step('down', '{DOWN}');
    await step('right', '{RIGHT}');
    // The speech recap: Narrator key + Alt + X, then what its window holds.
    out.recapOpen = await input('narratorkeys 2500 ALT+X');
    out.recap = await input('narratortext 6000');
    out.frontWithRecap = await input('foreground');
} finally {
    out.narratorStop = await input('narrator stop').catch((e) => String(e));
    out.keyboard ??= {};
    out.keyboard.after = await input(`front ${TITLE}`).then(() => input('imeoff')).then(() => input(`english ${TITLE}`)).catch((e) => String(e));
    out.finished = new Date().toISOString();
    save();
    helper.stdin.write('quit\n');
    await browser.close();
}
console.log(JSON.stringify({ steps: out.steps.length, browser: out.browserVersion, messages: out.messages.length }));
