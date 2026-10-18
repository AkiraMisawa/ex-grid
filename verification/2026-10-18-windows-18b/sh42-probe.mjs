// docs/specs/exsheet/verify-on-windows-17.md, Part D, SH-42: Ctrl+1 to Ctrl+5 on /sheet's D10, with several
// tabs open in the window. The eleventh run's keys-probe.mjs (verification/2026-10-01-windows-browser-11/), which
// asked a standalone keys.html, with /sheet in keys.html's place and the Sheet's answer read besides.
//
//     node sh42-probe.mjs <base URL>      CHANNEL=chrome|msedge, OUT=<record.json>, SHOTS=<folder>,
//                                         PROFILE=<an empty folder for the browser's profile>,
//                                         INPUT=<browser-input.ps1 of the eleventh run>
//
// The browser is started by Playwright in a profile of its own (launchPersistentContext, headed), with seven
// tabs: six small pages titled tab-<n>-verify-17 and /sheet third, titled sheet-verify-17. Every key and press
// is real OS input through browser-input.ps1 (SendInput). Before each key: /sheet is the selected tab, D10 the
// Focus, and the keyboard in the Sheet's Keyboard Field (a real press on D10 when it is not). After each key,
// 700 ms, then: what the page logged; the Sheet's answer (an open dialog and its title, D10's weight, slant
// and lines, the page's own read of the Focus cell's font, the Focus, where DOM focus is); the selected tab;
// the tabs and the browser's windows added; a picture of the browser's windows. Then what the key did is put
// back: a dialog closed with Escape, a format with Ctrl+Z, a tab the browser opened closed, /sheet selected.
import { chromium } from '@playwright/test';
import { spawn } from 'node:child_process';
import readline from 'node:readline';
import fs from 'node:fs';
import path from 'node:path';

const BASE = process.argv[2] ?? 'http://localhost:5318';
const CHANNEL = process.env.CHANNEL ?? 'chrome';
const OUT = process.env.OUT;
const SHOTS = process.env.SHOTS;
const PROFILE = process.env.PROFILE;
const LABEL = process.env.LABEL ?? 'wasm';
const out = { channel: CHANNEL, base: BASE, label: LABEL, started: new Date().toISOString(), keys: [], messages: [] };
const save = () => { if (OUT) fs.writeFileSync(OUT, JSON.stringify(out, null, 1)); };
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

const helper = spawn('powershell.exe', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', process.env.INPUT], { stdio: ['pipe', 'pipe', 'pipe'] });
const lines = readline.createInterface({ input: helper.stdout });
const waiting = [];
lines.on('line', (l) => { const w = waiting.shift(); if (w) w(l.trim()); });
const firstLine = new Promise((done) => waiting.push(done));
function input(cmd) {
    return new Promise((done, fail) => {
        waiting.push((l) => (l.startsWith('ok') ? done(l.replace(/ t=\d+$/, '')) : fail(new Error(`${cmd.slice(0, 60)}: ${l}`))));
        helper.stdin.write(cmd + '\n');
    });
}
const json = (answer, word) => JSON.parse(answer.slice(`ok ${word} `.length));
if ((await firstLine) !== 'ready') throw new Error('browser-input did not start');

const TITLE = 'sheet-verify-17';
const SHEET = '.ex-grid:has(> .ex-formula-bar)';
const context = await chromium.launchPersistentContext(PROFILE, { channel: CHANNEL, headless: false, viewport: null, args: ['--window-position=60,60', '--window-size=1600,1050'] });
const pages = [];
const small = (n) => `data:text/html,<title>tab-${n}-verify-17</title><p>tab ${n}</p>`;
const first = context.pages()[0] ?? (await context.newPage());
await first.goto(small(1)); pages.push(first);
for (const n of [2, 3, 4, 5, 6, 7]) {
    const p = await context.newPage();
    if (n === 3) {
        await p.goto(`${BASE}/sheet`);
        await p.waitForFunction(() => { const g = document.querySelector('.ex-grid:has(> .ex-formula-bar)'); return !!g && [...g.querySelectorAll('[id$="-r11c1"]')].map((e) => e.textContent).join('').includes('318.25') && !!g.querySelector('input.ex-key-field'); }, null, { timeout: 60_000 });
        await p.evaluate((t) => { document.title = t; }, TITLE);
    } else await p.goto(small(n));
    pages.push(p);
}
const sheet = pages[2];
sheet.on('console', (m) => out.messages.push({ t: new Date().toISOString(), type: m.type(), text: m.text() }));
sheet.on('pageerror', (e) => out.messages.push({ t: new Date().toISOString(), type: 'pageerror', text: String(e) }));
await sheet.bringToFront();
await sleep(800);
await sheet.evaluate(() => {
    window.__keys = [];
    window.addEventListener('keydown', (e) => window.__keys.push({ key: e.key, code: e.code, ctrlKey: e.ctrlKey, shiftKey: e.shiftKey, altKey: e.altKey, target: e.target?.className?.split?.(' ')[0] ?? e.target?.tagName }), true);
});
out.userAgent = await sheet.evaluate(() => navigator.userAgent);
out.browserVersion = (await (await context.newCDPSession(sheet)).send('Browser.getVersion')).product;
const found = (await input(`find ${TITLE}`)).match(/hwnd=(\d+) pid=(\d+)/);
const HWND = found[1], PID = found[2];
out.window = { hwnd: Number(HWND), pid: Number(PID) };
await input(`front ${HWND}`);
out.tabsAtStart = json(await input(`tabs ${HWND}`), 'tabs');
out.layoutUk = await input(`layout ${HWND} uk`);
save();

const STATE = () => {
    const g = document.querySelector('.ex-grid:has(> .ex-formula-bar)');
    const f = g.querySelector('input.ex-key-field');
    const id = f?.getAttribute('aria-activedescendant');
    const d10 = [...g.querySelectorAll('[id$="-r9c3"]')].find((e) => e.closest('.ex-viewport'));
    const t = d10?.querySelector('*') ?? d10;
    const cs = t ? getComputedStyle(t) : null;
    const dialogs = [...document.querySelectorAll('[role=dialog]')].map((d) => (d.getAttribute('aria-label') || d.querySelector('h1,h2,h3,[class*=title]')?.textContent || d.textContent).trim().slice(0, 80));
    return { focus: id?.replace(/^.*-r(\d+)c(\d+)$/, (x, r, c) => String.fromCharCode(65 + Number(c)) + (Number(r) + 1)) ?? null,
        keyboard: document.activeElement === f ? 'key-field' : `${document.activeElement?.tagName.toLowerCase()}.${String(document.activeElement?.className ?? '').split(' ')[0]}`,
        hasFocus: document.hasFocus(), visible: document.visibilityState, dialogs,
        d10: cs ? { weight: cs.fontWeight, style: cs.fontStyle, lines: cs.textDecorationLine } : null,
        focusFont: document.querySelector('#sheet-focus-font')?.textContent ?? null,
        keytips: [...document.querySelectorAll('.ex-sheet-keytip')].map((k) => k.textContent) };
};
async function d10Point() {
    const g = await sheet.evaluate((sel) => {
        const c = [...document.querySelector(sel).querySelectorAll('[id$="-r9c3"]')].find((e) => e.closest('.ex-viewport'));
        const r = c.getBoundingClientRect();
        return { sx: screenX, sy: screenY, ow: outerWidth, oh: outerHeight, iw: innerWidth, ih: innerHeight, dpr: devicePixelRatio, x: r.left + r.width / 2, y: r.top + r.height / 2 };
    }, SHEET);
    const border = (g.ow - g.iw) / 2;
    return [Math.round((g.sx + border + g.x) * g.dpr), Math.round((g.sy + (g.oh - g.ih) - border + g.y) * g.dpr)];
}
async function ready() {
    const sel = json(await input(`tabs ${HWND}`), 'tabs').filter((t) => t.selected).map((t) => t.name);
    if (!(sel.length === 1 && sel[0].startsWith(TITLE))) { await sheet.bringToFront(); await sleep(400); }
    await input(`front ${HWND}`);
    const s = await sheet.evaluate(STATE);
    if (!(s.focus === 'D10' && s.keyboard === 'key-field' && s.hasFocus)) {
        const [x, y] = await d10Point();
        await input(`click ${x} ${y}`);
        await sleep(600);
    }
    return sheet.evaluate(STATE);
}

const visibility = () => Promise.all(context.pages().map(async (p) => { try { return { url: p.url().slice(0, 60), visible: await p.evaluate(() => document.visibilityState) }; } catch (e) { return { url: p.url().slice(0, 60), visible: 'not read' }; } }));
async function one(name, command) {
    const row = { name };
    row.before = await ready();
    const logBefore = await sheet.evaluate(() => window.__keys.length);
    const tabsBefore = json(await input(`tabs ${HWND}`), 'tabs');
    const winsBefore = json(await input(`windows ${PID}`), 'windows');
    const pagesBefore = context.pages().length;
    row.sent = await input(command);
    await sleep(700);
    row.logged = await sheet.evaluate((n) => window.__keys.slice(n), logBefore);
    row.pageReceived = row.logged.filter((k) => !['Control', 'Shift', 'Alt'].includes(k.key)).map((k) => `key=${k.key} code=${k.code} ctrl=${k.ctrlKey} target=${k.target}`);
    row.after = await sheet.evaluate(STATE);
    row.tabs = json(await input(`tabs ${HWND}`), 'tabs');
    row.selectedTab = row.tabs.filter((t) => t.selected).map((t) => t.name);
    row.tabsAdded = row.tabs.filter((t) => !tabsBefore.some((b) => b.name === t.name)).map((t) => t.name);
    row.visiblePages = (await visibility()).filter((v) => v.visible !== 'hidden');
    const winsAfter = json(await input(`windows ${PID}`), 'windows');
    row.windowsAdded = winsAfter.filter((w) => !winsBefore.some((b) => b.hwnd === w.hwnd)).map((w) => ({ class: w.class, title: w.title, rect: w.rect }));
    if (SHOTS) { const file = path.join(SHOTS, `sh42-${LABEL}-${CHANNEL}-${name}.png`); row.shot = await input(`shot ${HWND} ${file}`); row.shotFile = path.basename(file); }
    row.browserActed = [
        ...(row.selectedTab.length === 1 && !row.selectedTab[0].startsWith(TITLE) ? [`tab selected: ${row.selectedTab[0]}`] : []),
        ...row.tabsAdded.map((t) => `tab opened: ${t}`),
        ...(context.pages().length > pagesBefore ? [`pages ${pagesBefore} -> ${context.pages().length}`] : []),
        ...row.windowsAdded.map((w) => `window: ${w.class} [${w.title}]`),
    ];
    row.sheetActed = [
        ...row.after.dialogs.filter((d) => !row.before.dialogs.includes(d)).map((d) => `dialog: ${d}`),
        ...(JSON.stringify(row.after.d10) !== JSON.stringify(row.before.d10) ? [`D10 ${JSON.stringify(row.before.d10)} -> ${JSON.stringify(row.after.d10)}`] : []),
        ...(row.after.focusFont !== row.before.focusFont ? [`focus font ${row.before.focusFont} -> ${row.after.focusFont}`] : []),
    ];
    // Put back what the key did.
    row.closed = [];
    if (row.after.dialogs.length) { row.closed.push(await input('esc')); await sleep(600); }
    if (JSON.stringify(row.after.d10) !== JSON.stringify(row.before.d10)) { await ready(); row.closed.push(`Ctrl+Z: ${await input('chord ctrl 5a')}`); await sleep(600); row.restored = (await sheet.evaluate(STATE)).d10; }
    for (const p of context.pages()) if (!pages.includes(p)) { row.closed.push(`page ${p.url()}`); await p.close(); }
    if (row.windowsAdded.length) { row.closed.push(await input('esc')); await sleep(400); }
    const sel = json(await input(`tabs ${HWND}`), 'tabs').filter((t) => t.selected).map((t) => t.name);
    if (!(sel.length === 1 && sel[0].startsWith(TITLE))) { row.closed.push(`/sheet brought to the front (the selected tab was ${sel.join(', ')})`); await sheet.bringToFront(); await sleep(400); }
    out.keys.push(row);
    save();
    console.log(`${name}: page ${row.pageReceived.join('; ') || '(nothing)'} | sheet ${row.sheetActed.join('; ') || '(nothing)'} | browser ${row.browserActed.join('; ') || '(nothing)'}`);
}

for (const d of [1, 2, 3, 4, 5]) await one(`ctrl-${d}`, `chord ctrl ${(0x30 + d).toString(16)}`);
out.finished = new Date().toISOString();
save();
await context.close();
helper.stdin.write('quit\n');
console.log(JSON.stringify({ channel: CHANNEL, label: LABEL, browser: out.browserVersion, keys: out.keys.length, messages: out.messages.length }));
