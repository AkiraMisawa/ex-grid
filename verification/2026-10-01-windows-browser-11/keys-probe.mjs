// docs/specs/exsheet/verify-on-windows-11.md, Part B: which Ctrl keys a page receives in Chrome and in
// Edge, and which the browser takes for itself.
//
//     node keys-probe.mjs <base URL>        CHANNEL=chrome|msedge, OUT=<record.json>, SHOTS=<folder>,
//                                           PROFILE=<an empty folder for the browser's profile>,
//                                           INPUT=<browser-input.ps1>
//
// The browser is started by Playwright in a profile of its own (launchPersistentContext, headed), with
// seven tabs: other.html?n=1, ?n=2, keys.html, then ?n=4 to ?n=7, so keys.html is the third. Every key
// and every click is real OS input through browser-input.ps1 (SendInput); Playwright only opens the
// tabs, reads the page and the tabs, and puts back what the browser changed between keys.
//
// Before each key: keys.html is the selected tab and its div has DOM focus (a real click on the div when
// it has not). After each key, 500 ms, then: what the page logged; the selected tab (UI Automation's tab
// strip, and which page reports itself visible); the tabs and the browser's windows, against those before
// the key; and a picture of the browser's windows (PrintWindow). Then what the browser opened is closed:
// a tab it opened (page.close()), a window or bubble (Escape, a real key), and keys.html selected again
// (page.bringToFront()).
import { chromium } from '@playwright/test';
import { spawn } from 'node:child_process';
import readline from 'node:readline';
import fs from 'node:fs';
import path from 'node:path';

const BASE = process.argv[2] ?? 'http://localhost:8811';
const CHANNEL = process.env.CHANNEL ?? 'chrome';
const OUT = process.env.OUT;
const SHOTS = process.env.SHOTS;
const PROFILE = process.env.PROFILE;
const out = { channel: CHANNEL, base: BASE, started: new Date().toISOString(), keys: [], messages: [] };
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

// ---- The tabs -------------------------------------------------------------------------------------------

const context = await chromium.launchPersistentContext(PROFILE, { channel: CHANNEL, headless: false, viewport: null, args: ['--window-position=60,60', '--window-size=1500,1000'] });
out.browserVersion = context.browser()?.version() ?? null;
const pages = [];
const first = context.pages()[0] ?? (await context.newPage());
await first.goto(`${BASE}/other.html?n=1`); pages.push(first);
for (const n of [2, 3, 4, 5, 6, 7]) {
    const p = await context.newPage();
    await p.goto(n === 3 ? `${BASE}/keys.html` : `${BASE}/other.html?n=${n}`);
    pages.push(p);
}
const keysPage = pages[2];
keysPage.on('console', (m) => out.messages.push({ t: new Date().toISOString(), type: m.type(), text: m.text() }));
keysPage.on('pageerror', (e) => out.messages.push({ t: new Date().toISOString(), type: 'pageerror', text: String(e) }));
await keysPage.bringToFront();
await sleep(800);
out.userAgent = await keysPage.evaluate(() => navigator.userAgent);
// A persistent context has no Browser object to ask (context.browser() is null); the DevTools protocol answers.
out.browserVersion = (await (await context.newCDPSession(keysPage)).send('Browser.getVersion')).product;
const found = (await input('find keys-verify-11')).match(/hwnd=(\d+) pid=(\d+)/);
const HWND = found[1], PID = found[2];
out.window = { hwnd: Number(HWND), pid: Number(PID) };
await input(`front ${HWND}`);
out.tabsAtStart = json(await input(`tabs ${HWND}`), 'tabs');
out.windowsAtStart = json(await input(`windows ${PID}`), 'windows').map((w) => ({ class: w.class, title: w.title, rect: w.rect }));
save();

// The div's middle in screen pixels, from the window's place and the page's geometry (the div is 640 by
// 220 CSS px, so a pixel or two of error does not matter); the click is checked by where focus lands.
async function divPoint() {
    const g = await keysPage.evaluate(() => {
        const r = document.getElementById('target').getBoundingClientRect();
        return { sx: screenX, sy: screenY, ow: outerWidth, oh: outerHeight, iw: innerWidth, ih: innerHeight, dpr: devicePixelRatio, x: r.left + r.width / 2, y: r.top + r.height / 2 };
    });
    const border = (g.ow - g.iw) / 2;
    return [Math.round((g.sx + border + g.x) * g.dpr), Math.round((g.sy + (g.oh - g.ih) - border + g.y) * g.dpr)];
}
const focusOnDiv = () => keysPage.evaluate(() => document.activeElement?.id === 'target' && document.hasFocus());
async function ready() {
    const tabs = json(await input(`tabs ${HWND}`), 'tabs');
    const selected = tabs.filter((t) => t.selected).map((t) => t.name);
    if (!(selected.length === 1 && selected[0].startsWith('keys-verify-11'))) { await keysPage.bringToFront(); await sleep(400); }
    await input(`front ${HWND}`);
    if (!(await focusOnDiv())) {
        const [x, y] = await divPoint();
        await input(`click ${x} ${y}`);
        await sleep(300);
    }
    return focusOnDiv();
}

// ---- One key ----------------------------------------------------------------------------------------------

const visibility = () => Promise.all(context.pages().map(async (p) => { try { return { url: p.url(), visible: await p.evaluate(() => document.visibilityState) }; } catch (e) { return { url: p.url(), visible: `not read: ${String(e).slice(0, 80)}` }; } }));
async function one(group, layout, name, command) {
    const row = { group, layout, name };
    row.focusedBefore = await ready();
    const logBefore = await keysPage.evaluate(() => window.__keys.length);
    const tabsBefore = json(await input(`tabs ${HWND}`), 'tabs');
    const winsBefore = json(await input(`windows ${PID}`), 'windows');
    const pagesBefore = context.pages().length;
    row.sent = await input(command);
    await sleep(500);
    row.logged = await keysPage.evaluate((n) => window.__keys.slice(n), logBefore);
    row.pageReceived = row.logged.filter((k) => !['Control', 'Shift', 'Alt'].includes(k.key)).map((k) => `key=${k.key} code=${k.code} ctrl=${k.ctrlKey} shift=${k.shiftKey} alt=${k.altKey}`);
    row.tabs = json(await input(`tabs ${HWND}`), 'tabs');
    row.selectedTab = row.tabs.filter((t) => t.selected).map((t) => t.name);
    row.tabsAdded = row.tabs.filter((t) => !tabsBefore.some((b) => b.name === t.name)).map((t) => t.name);
    row.visiblePages = (await visibility()).filter((v) => v.visible !== 'hidden');
    row.pagesNow = context.pages().map((p) => p.url());
    const winsAfter = json(await input(`windows ${PID}`), 'windows');
    row.windowsAdded = winsAfter.filter((w) => !winsBefore.some((b) => b.hwnd === w.hwnd)).map((w) => ({ class: w.class, title: w.title, rect: w.rect }));
    row.windowTitle = winsAfter.find((w) => String(w.hwnd) === HWND)?.title ?? null;
    row.focusedAfter = await focusOnDiv().catch(() => null);
    if (SHOTS) {
        const file = path.join(SHOTS, `${CHANNEL}-${group}-${layout}-${name}.png`);
        row.shot = await input(`shot ${HWND} ${file}`);
        row.shotFile = path.basename(file);
    }
    row.browserActed = [
        ...(row.selectedTab.length === 1 && !row.selectedTab[0].startsWith('keys-verify-11') ? [`tab selected: ${row.selectedTab[0]}`] : []),
        ...row.tabsAdded.map((t) => `tab opened: ${t}`),
        ...(context.pages().length > pagesBefore ? [`pages ${pagesBefore} -> ${context.pages().length}`] : []),
        ...row.windowsAdded.map((w) => `window: ${w.class} [${w.title}]`),
    ];
    // Close what the browser opened, then keys.html back in front.
    row.closed = [];
    for (const p of context.pages()) if (!pages.includes(p)) { row.closed.push(`page ${p.url()}`); await p.close(); }
    if (row.windowsAdded.length) { row.closed.push(await input('esc')); await sleep(400); }
    // A tab the browser opened that Playwright does not list (a view-source tab may not be one of its
    // pages) is closed with Ctrl+W while it is the selected tab.
    let now = json(await input(`tabs ${HWND}`), 'tabs');
    for (let i = 0; i < 3 && now.filter((t) => t.name).length > 7; i++) {
        const extra = now.find((t) => t.selected && !tabsBefore.some((b) => b.name === t.name));
        if (!extra) break;
        row.closed.push(`tab ${extra.name}: ${await input(`chord ctrl 57`)}`); await sleep(500);
        now = json(await input(`tabs ${HWND}`), 'tabs');
    }
    const sel = json(await input(`tabs ${HWND}`), 'tabs').filter((t) => t.selected).map((t) => t.name);
    if (!(sel.length === 1 && sel[0].startsWith('keys-verify-11'))) { row.closed.push(`keys.html brought to the front (the selected tab was ${sel.join(', ')})`); await keysPage.bringToFront(); await sleep(400); }
    out.keys.push(row);
    save();
    console.log(`${group} ${layout} ${name}: page ${row.pageReceived.join('; ') || '(nothing)'} | browser ${row.browserActed.join('; ') || '(nothing)'}`);
}

// ---- The keys ---------------------------------------------------------------------------------------------

out.layoutUk = await input(`layout ${HWND} uk`);
for (const d of [1, 2, 3, 4, 5]) await one('27', 'uk', `ctrl-${d}`, `chord ctrl ${(0x30 + d).toString(16)}`);
for (const [c, vk] of [['b', 0x42], ['i', 0x49], ['u', 0x55]]) await one('28', 'uk', `ctrl-${c}`, `chord ctrl ${vk.toString(16)}`);
const chars = { '~': 'tilde', '!': 'bang', '@': 'at', '#': 'hash', $: 'dollar', '%': 'percent', '^': 'caret', '&': 'amp', _: 'underscore' };
for (const [c, n] of Object.entries(chars)) await one('29', 'uk', `ctrl-${n}`, `ctrlchar ${c.charCodeAt(0).toString(16)}`);
out.layoutJa = await input(`layout ${HWND} ja`);
for (const [c, n] of Object.entries(chars)) await one('29', 'ja', `ctrl-${n}`, `ctrlchar ${c.charCodeAt(0).toString(16)}`);
out.layoutBack = await input(`layout ${HWND} uk`);
await one('30', 'uk', 'ctrl-tab', 'chord ctrl 09');
await one('30', 'uk', 'ctrl-pagedown', 'chord ctrl 22');

out.finished = new Date().toISOString();
out.pageLogAtEnd = await keysPage.evaluate(() => window.__keys.length);
save();
await context.close();
helper.stdin.write('quit\n');
console.log(JSON.stringify({ channel: CHANNEL, browser: out.browserVersion, keys: out.keys.length, messages: out.messages.length }));
