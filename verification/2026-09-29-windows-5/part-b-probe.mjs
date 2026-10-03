// verify-on-windows-5.md Part B, by hand with the real mouse and keyboard, one host and one browser:
//
//   PART=scroll  item 1: /wide and /features with the built-in Chrome. The pointer is taken off the
//                grid; after 1.5 s at rest, the screen's pixels on the centre line of each gutter
//                are read and the grid is captured. Then the vertical thumb, found in those pixels,
//                is dragged 150 CSS px down, and on /wide the horizontal thumb 150 CSS px right
//   PART=copy    item 2: /sheet, a click on E1, 5, then Enter, Tab or Escape (a fresh page each); a
//                click on B2, Ctrl+C, a click on F6, Ctrl+V. The clipboard is set to a sentinel
//                before the Ctrl+C, so a copy that writes nothing shows
//   PART=paste   item 3: =A1 copied from Notepad (the tab this opens is closed alone), then /sheet,
//                a click on B2, Shift+click on C3, Ctrl+V
//
// Run on Windows from a copy of tests/ExGrid.Browser (for its node_modules), headed, the window at
// the display's own scale (viewport: null):
//
//     set EXGRID_CHANNEL=msedge & set PART=copy & set LABEL=server & set INPUT=...\input-server.ps1
//     set SHOTS=C:\... & node part-b-probe.mjs http://localhost:6298
//
// Every input goes through input-server.ps1 (real OS input). Playwright opens the pages and reads
// the DOM; it sends no input. Prints one JSON document.
import { chromium } from '@playwright/test';
import { spawn } from 'node:child_process';
import readline from 'node:readline';
import path from 'node:path';

const BASE = process.argv[2] ?? 'http://localhost:5299';
const CHANNEL = process.env.EXGRID_CHANNEL ?? 'chrome';
const PART = process.env.PART ?? 'scroll';
const LABEL = process.env.LABEL ?? '';
const SHOTS = process.env.SHOTS;
const PASTE_SOURCE = process.env.PASTE_SOURCE;
const PACE_MS = 250;
const out = { channel: CHANNEL, base: BASE, label: LABEL, part: PART, started: new Date().toISOString(), steps: [] };
const messages = [];

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
if ((await firstLine) !== 'ready') throw new Error('input-server did not start');

const browser = await chromium.launch({ channel: CHANNEL, headless: false, args: ['--window-position=40,40', '--window-size=1600,1000'] });
out.browserVersion = browser.version();
const context = await browser.newContext({ viewport: null });

// Screen pixels for a client point of this tab, calibrated against the page's own mousemove
// (end-probe.mjs, the fourth run).
async function calibrate(page) {
    await page.evaluate(() => { window.__move = null; document.addEventListener('mousemove', (e) => { window.__move = { x: e.clientX, y: e.clientY }; }, true); });
    const g = await page.evaluate(() => ({ sx: window.screenX, sy: window.screenY, ow: window.outerWidth, oh: window.outerHeight, iw: window.innerWidth, ih: window.innerHeight, dpr: window.devicePixelRatio }));
    const border = (g.ow - g.iw) / 2;
    const target = { x: 400, y: 120 };
    let ox = (g.sx + border) * g.dpr, oy = (g.sy + (g.oh - g.ih) - border) * g.dpr;
    const tries = [];
    for (let i = 0; i < 4; i++) {
        await page.evaluate(() => { window.__move = null; });
        const px = Math.round(ox + target.x * g.dpr), py = Math.round(oy + target.y * g.dpr);
        await input(`move ${px} ${py}`);
        await page.waitForTimeout(200);
        const m = await page.evaluate(() => window.__move);
        tries.push({ px, py, client: m });
        if (!m) continue;
        const dx = target.x - m.x, dy = target.y - m.y;
        if (Math.abs(dx) < 1 && Math.abs(dy) < 1) break;
        ox += dx * g.dpr; oy += dy * g.dpr;
    }
    return { dpr: g.dpr, tries, at: (x, y) => [Math.round(ox + x * g.dpr), Math.round(oy + y * g.dpr)] };
}

async function open(pathname, title) {
    const page = await context.newPage();
    page.on('console', (m) => { if (m.type() === 'error' || m.type() === 'warning') messages.push(`${title} ${m.type()}: ${m.text()}`); });
    page.on('pageerror', (e) => messages.push(`${title} pageerror: ${e}`));
    await page.goto(`${BASE}${pathname}`);
    await page.locator('#demo-interactive').waitFor({ state: 'attached', timeout: 60_000 });
    await page.waitForFunction(() => !document.querySelector('.ex-grid[aria-busy]'), null, { timeout: 60_000 });
    await page.locator(".ex-grid [id$='-r0c0']").first().waitFor();
    await page.bringToFront();
    await page.evaluate((x) => { document.title = x; }, title);
    await page.waitForTimeout(400);
    await input(`front ${title}`);
    const cal = await calibrate(page);
    // What the page saw of the real input.
    await page.evaluate(() => {
        window.__seen = [];
        for (const type of ['mousedown', 'mouseup', 'keydown', 'copy', 'paste']) {
            document.addEventListener(type, (e) => window.__seen.push({ t: Math.round(performance.now()), type, key: e.key ?? null, ctrl: e.ctrlKey ?? null,
                target: `${e.target?.tagName?.toLowerCase() ?? ''}.${typeof e.target?.className === 'string' ? e.target.className : ''}`.slice(0, 80) }), true);
        }
    });
    return { page, cal, title };
}

async function grabBox(p, box, name) {
    if (!SHOTS) return null;
    const file = path.join(SHOTS, `B-${name}-${LABEL}-${CHANNEL}.png`);
    const [l, t] = p.cal.at(box.left - 6, box.top - 6);
    const [r, b] = p.cal.at(box.right + 6, box.bottom + 6);
    await input(`grab ${l} ${t} ${r} ${b} ${file}`);
    return `shots/${path.basename(file)}`;
}

// ---- item 1: the grid's own scrollbar -------------------------------------------------------------

const GEOMETRY = () => {
    const root = document.querySelector('.ex-grid');
    const sc = root.querySelector('.ex-scroller');
    const r = sc.getBoundingClientRect(), rr = root.getBoundingClientRect();
    const cs = getComputedStyle(sc);
    const bl = parseFloat(cs.borderLeftWidth), bt = parseFloat(cs.borderTopWidth), br = parseFloat(cs.borderRightWidth), bb = parseFloat(cs.borderBottomWidth);
    const bar = getComputedStyle(sc, '::-webkit-scrollbar');
    const thumb = getComputedStyle(sc, '::-webkit-scrollbar-thumb');
    return {
        left: r.left, top: r.top, right: r.right, bottom: r.bottom, border: [bl, bt, br, bb],
        clientWidth: sc.clientWidth, clientHeight: sc.clientHeight, scrollWidth: sc.scrollWidth, scrollHeight: sc.scrollHeight,
        scrollTop: sc.scrollTop, scrollLeft: sc.scrollLeft,
        verticalGutter: r.width - bl - br - sc.clientWidth, horizontalGutter: r.height - bt - bb - sc.clientHeight,
        root: { left: rr.left, top: rr.top, right: rr.right, bottom: rr.bottom },
        scrollbarWidthToken: getComputedStyle(root).getPropertyValue('--ex-scrollbar-width').trim() || null,
        scrollbarColorToken: getComputedStyle(root).getPropertyValue('--ex-scrollbar-color').trim() || null,
        pseudo: { barWidth: bar.width, barBackground: bar.backgroundColor, thumbBackground: thumb.backgroundColor, thumbBorderRadius: thumb.borderRadius },
        scrollerBackground: cs.backgroundColor,
    };
};

// The runs of a line of screen pixels ("rrggbb*n,..."), read against the colour at the track's far
// end: the runs that differ from it by more than 24 in a channel are the thumb.
function parseLine(answer, word) {
    const runs = answer.slice(`ok ${word} `.length).split(',').map((s) => { const [c, n] = s.split('*'); return { c, n: Number(n) }; });
    return runs;
}
function thumbIn(runs, dpr, trackColour) {
    const rgb = (c) => [0, 2, 4].map((i) => parseInt(c.slice(i, i + 2), 16));
    const track = rgb(trackColour);
    let at = 0, first = null, last = null, colours = new Map();
    for (const { c, n } of runs) {
        const v = rgb(c);
        if (v.some((x, i) => Math.abs(x - track[i]) > 24)) {
            if (first === null) first = at;
            last = at + n;
            colours.set(c, (colours.get(c) ?? 0) + n);
        }
        at += n;
    }
    const main = [...colours.entries()].sort((a, b) => b[1] - a[1])[0]?.[0] ?? null;
    return first === null
        ? { visible: false, track: trackColour, lengthPx: at }
        : { visible: true, track: trackColour, colour: main, fromPx: first, toPx: last, fromCss: +(first / dpr).toFixed(1), toCss: +(last / dpr).toFixed(1), lengthCss: +((last - first) / dpr).toFixed(1), trackCss: +(at / dpr).toFixed(1) };
}

async function gutters(p, g) {
    const { cal } = p;
    const res = {};
    if (g.verticalGutter > 0) {
        const x = g.left + g.border[0] + g.clientWidth + g.verticalGutter / 2;
        const [sx, sy1] = cal.at(x, g.top + g.border[1]);
        const [, sy2] = cal.at(x, g.top + g.border[1] + g.clientHeight);
        const runs = parseLine(await input(`col ${sx} ${sy1} ${sy2}`), 'col');
        // At the far end of the track: the last run, which the thumb reaches only when it fills it.
        res.vertical = { line: { x: sx, from: sy1, to: sy2 }, runs, thumb: thumbIn(runs, cal.dpr, runs.at(-1).c) };
    }
    if (g.horizontalGutter > 0) {
        const y = g.top + g.border[1] + g.clientHeight + g.horizontalGutter / 2;
        const [sx1, sy] = cal.at(g.left + g.border[0], y);
        const [sx2] = cal.at(g.left + g.border[0] + g.clientWidth, y);
        const runs = parseLine(await input(`row ${sy} ${sx1} ${sx2}`), 'row');
        res.horizontal = { line: { y: sy, from: sx1, to: sx2 }, runs, thumb: thumbIn(runs, cal.dpr, runs.at(-1).c) };
    }
    return res;
}

async function scrollCase(pathname) {
    const name = pathname.slice(1);
    const p = await open(pathname, `partb-${LABEL}-${CHANNEL}-scroll-${name}`);
    const { page, cal } = p;
    const step = { item: 1, page: pathname, calibration: { dpr: cal.dpr, tries: cal.tries } };
    out.steps.push(step);
    // The pointer off the grid, on the page's heading, and left there.
    const h1 = await page.locator('h1').boundingBox();
    const [hx, hy] = cal.at(h1.x + 4, h1.y + h1.height / 2);
    await input(`move ${hx} ${hy}`);
    await page.waitForTimeout(1500);
    const g = await page.evaluate(GEOMETRY);
    step.atRest = { geometry: g, gutters: await gutters(p, g), shot: await grabBox(p, g.root, `scroll-${name}-at-rest`) };
    const v = step.atRest.gutters.vertical?.thumb;
    if (v?.visible) {
        const x = g.left + g.border[0] + g.clientWidth + g.verticalGutter / 2;
        const y0 = g.top + g.border[1] + (v.fromCss + v.toCss) / 2;
        const [sx, sy] = cal.at(x, y0);
        const [, sy2] = cal.at(x, y0 + 150);
        await page.evaluate(() => { window.__seen = []; });
        const answer = await input(`drag ${sx} ${sy} ${sx} ${sy2} 15 16`);
        await page.waitForTimeout(1200);
        await page.waitForFunction(() => !document.querySelector('.ex-grid[aria-busy]'), null, { timeout: 30_000 }).catch(() => {});
        const g2 = await page.evaluate(GEOMETRY);
        step.verticalDrag = { from: { css: [x, y0], screen: [sx, sy] }, to: { css: [x, y0 + 150], screen: [sx, sy2] }, answer,
            scrollTopBefore: g.scrollTop, scrollTopAfter: g2.scrollTop, seen: await page.evaluate(() => window.__seen),
            gutters: await gutters(p, g2), shot: await grabBox(p, g2.root, `scroll-${name}-after-vertical-drag`) };
    } else {
        step.verticalDrag = { skipped: 'no thumb found in the vertical gutter\'s pixels' };
    }
    const h = step.atRest.gutters.horizontal?.thumb;
    if (h?.visible) {
        const g1 = await page.evaluate(GEOMETRY);
        const hg = await gutters(p, g1);
        const ht = hg.horizontal.thumb;
        const y = g1.top + g1.border[1] + g1.clientHeight + g1.horizontalGutter / 2;
        const x0 = g1.left + g1.border[0] + (ht.fromCss + ht.toCss) / 2;
        const [sx, sy] = cal.at(x0, y);
        const [sx2] = cal.at(x0 + 150, y);
        await page.evaluate(() => { window.__seen = []; });
        const answer = await input(`drag ${sx} ${sy} ${sx2} ${sy} 15 16`);
        await page.waitForTimeout(1200);
        const g2 = await page.evaluate(GEOMETRY);
        step.horizontalDrag = { from: { css: [x0, y], screen: [sx, sy] }, to: { css: [x0 + 150, y], screen: [sx2, sy] }, answer,
            scrollLeftBefore: g1.scrollLeft, scrollLeftAfter: g2.scrollLeft, seen: await page.evaluate(() => window.__seen),
            gutters: await gutters(p, g2), shot: await grabBox(p, g2.root, `scroll-${name}-after-horizontal-drag`) };
    } else {
        step.horizontalDrag = { skipped: step.atRest.gutters.horizontal ? 'no thumb found in the horizontal gutter\'s pixels' : 'no horizontal gutter' };
    }
    await page.close();
}

// ---- items 2 and 3: /sheet ------------------------------------------------------------------------

function parse(a1) {
    const [, letters, digits] = /^([A-Z]+)(\d+)$/.exec(a1);
    let column = 0;
    for (const ch of letters) column = column * 26 + (ch.charCodeAt(0) - 64);
    return { row: Number(digits) - 1, column: column - 1 };
}

// The Sheet as shown: the Name Box, the Formula Bar, the editor, the active descendant, the
// ranges painted and the cells each covers, and the named cells' text.
const SHEET = (cells) => {
    const grid = document.querySelector('.ex-grid');
    const byId = (a) => grid.querySelector(`[id$='-r${a.row}c${a.column}']`);
    const shown = {};
    for (const [name, a] of Object.entries(cells)) { const el = byId(a); shown[name] = el ? el.innerText.trim() : null; }
    const rects = {};
    for (const el of grid.querySelectorAll('[id]')) {
        const m = /-r(\d+)c(\d+)$/.exec(el.id);
        if (!m) continue;
        const r = el.getBoundingClientRect();
        if (r.width > 0) rects[`${m[1]},${m[2]}`] = r;
    }
    const letters = (c) => { let s = ''; c++; while (c > 0) { const k = (c - 1) % 26; s = String.fromCharCode(65 + k) + s; c = Math.floor((c - 1) / 26); } return s; };
    const covered = (box) => {
        const inside = Object.entries(rects).filter(([, r]) => r.left >= box.left - 1.5 && r.right <= box.right + 1.5 && r.top >= box.top - 1.5 && r.bottom <= box.bottom + 1.5)
            .map(([k]) => k.split(',').map(Number));
        if (!inside.length) return null;
        const rows = inside.map((x) => x[0]), cols = inside.map((x) => x[1]);
        const a = `${letters(Math.min(...cols))}${Math.min(...rows) + 1}`, b = `${letters(Math.max(...cols))}${Math.max(...rows) + 1}`;
        return a === b ? a : `${a}:${b}`;
    };
    const ranges = [...grid.querySelectorAll('.ex-selection .ex-range')].map((el) => covered(el.getBoundingClientRect()));
    const focus = grid.querySelector('.ex-focus');
    const editor = grid.querySelector('input.ex-editor:not(.ex-formula-bar-text)');
    const ad = grid.getAttribute('aria-activedescendant') ?? '';
    const m = /-r(\d+)c(\d+)$/.exec(ad);
    const active = document.activeElement;
    return {
        nameBox: grid.querySelector('input.ex-name-box')?.value ?? null,
        formulaBar: grid.querySelector('input.ex-formula-bar-text')?.value ?? null,
        editor: editor ? editor.value : null,
        activeDescendant: m ? `${letters(Number(m[2]))}${Number(m[1]) + 1}` : ad || null,
        ranges, focus: focus ? covered(focus.getBoundingClientRect()) : null,
        announce: (grid.querySelector('.ex-announce')?.innerText ?? '').trim(),
        documentFocus: active ? `${active.tagName.toLowerCase()}${active.className ? '.' + String(active.className).split(' ').join('.') : ''}`.slice(0, 80) : null,
        documentSelection: (() => { const s = document.getSelection(); return s ? { type: s.type, rangeCount: s.rangeCount, anchor: s.anchorNode ? (s.anchorNode.nodeName + (s.anchorNode.parentElement ? ' in ' + s.anchorNode.parentElement.tagName.toLowerCase() + '.' + s.anchorNode.parentElement.className : '')).slice(0, 80) : null } : null; })(),
        shown,
    };
};

async function sheetState(p, label, names) {
    await p.page.waitForTimeout(PACE_MS);
    const cells = Object.fromEntries(names.map((n) => [n, parse(n)]));
    return { step: label, ...(await p.page.evaluate(SHEET, cells)) };
}

async function cellPoint(p, a1) {
    const { row, column } = parse(a1);
    const box = await p.page.locator(`.ex-grid [id$='-r${row}c${column}']`).first().boundingBox();
    return p.cal.at(box.x + box.width / 2, box.y + box.height / 2);
}
async function clickCell(p, a1, shift = false) {
    const [x, y] = await cellPoint(p, a1);
    const a = await input(`${shift ? 'shiftclick' : 'click'} ${x} ${y}`);
    await p.page.waitForTimeout(PACE_MS + 150);
    return a;
}
async function keys(p, k) {
    const a = await input(`keys ${k}`);
    await p.page.waitForTimeout(PACE_MS);
    return a;
}
async function sheetBox(p) {
    return p.page.evaluate(() => { const r = document.querySelector('.ex-grid').getBoundingClientRect(); return { left: r.left, top: r.top, right: r.right, bottom: r.bottom }; });
}
async function clip() { return json(await input('clip'), 'clip'); }

const SEEN = ['B2', 'E1', 'F1', 'E2', 'F6'];
async function copyCase(end) {
    const key = { Enter: '{ENTER}', Tab: '{TAB}', Escape: '{ESC}' }[end];
    const p = await open('/sheet', `partb-${LABEL}-${CHANNEL}-copy-${end}`);
    const steps = [];
    const record = { item: 2, end, calibration: { dpr: p.cal.dpr, tries: p.cal.tries }, steps };
    out.steps.push(record);
    steps.push(await sheetState(p, 'opened', SEEN));
    await clickCell(p, 'E1');
    await input('imeoff');
    steps.push(await sheetState(p, 'click E1', SEEN));
    await keys(p, '5');
    steps.push(await sheetState(p, 'typed 5', SEEN));
    await keys(p, key);
    await p.page.waitForTimeout(500);
    steps.push(await sheetState(p, `pressed ${end}`, SEEN));
    await clickCell(p, 'B2');
    steps.push(await sheetState(p, 'click B2', SEEN));
    const sentinel = `SENTINEL-${Date.now()}`;
    await input(`setclip ${sentinel}`);
    await p.page.evaluate(() => { window.__seen = []; });
    await keys(p, '^c');
    await p.page.waitForTimeout(1000);
    const afterCopy = await clip();
    steps.push({ ...(await sheetState(p, 'Ctrl+C', SEEN)), sentinel, clipboard: afterCopy, clipboardChanged: afterCopy.text !== sentinel, seen: await p.page.evaluate(() => window.__seen) });
    await clickCell(p, 'F6');
    steps.push(await sheetState(p, 'click F6', SEEN));
    await p.page.evaluate(() => { window.__seen = []; });
    await keys(p, '^v');
    await p.page.waitForTimeout(1000);
    const pasted = await sheetState(p, 'Ctrl+V', SEEN);
    steps.push({ ...pasted, seen: await p.page.evaluate(() => window.__seen), shot: await grabBox(p, await sheetBox(p), `copy-after-${end}`) });
    Object.assign(record, { f6ShowsB2: pasted.shown.F6 !== null && pasted.shown.F6 === steps[0].shown.B2, b2: steps[0].shown.B2, f6: pasted.shown.F6 });
    await p.page.close();
}

const BLOCK = ['A1', 'B1', 'C1', 'B2', 'C2', 'B3', 'C3'];
async function pasteCase() {
    const steps = [];
    const record = { item: 3, steps };
    out.steps.push(record);
    const copied = json(await input(`notepadcopy ${PASTE_SOURCE}`), 'notepadcopy');
    steps.push({ step: '=A1 copied from Notepad (Ctrl+A, Ctrl+C), its tab closed (Ctrl+W)', ...copied });
    const p = await open('/sheet', `partb-${LABEL}-${CHANNEL}-paste`);
    steps.push(await sheetState(p, 'opened', BLOCK));
    await clickCell(p, 'B2');
    await clickCell(p, 'C3', true);
    steps.push({ ...(await sheetState(p, 'click B2, Shift+click C3', BLOCK)), clipboard: await clip(), shot: await grabBox(p, await sheetBox(p), 'paste-B2-C3-selected') });
    await p.page.evaluate(() => { window.__seen = []; });
    await keys(p, '^v');
    await p.page.waitForTimeout(1000);
    const pasted = await sheetState(p, 'Ctrl+V', BLOCK);
    steps.push({ ...pasted, seen: await p.page.evaluate(() => window.__seen), shot: await grabBox(p, await sheetBox(p), 'paste-Ctrl-V') });
    // The Entries, through the Formula Bar, once the Selection has been read.
    const entries = {};
    for (const a of ['B2', 'C2', 'B3', 'C3']) { await clickCell(p, a); entries[a] = (await sheetState(p, `click ${a}`, [])).formulaBar; }
    Object.assign(record, { entries, clipboardAfter: await clip() });
    await p.page.close();
}

try {
    if (PART === 'scroll') { await scrollCase('/wide'); await scrollCase('/features'); }
    else if (PART === 'copy') { for (const end of ['Enter', 'Tab', 'Escape']) await copyCase(end); }
    else if (PART === 'paste') await pasteCase();
    else throw new Error(`unknown PART ${PART}`);
} catch (e) {
    out.error = String(e.stack ?? e).slice(0, 1200);
}
out.console = messages;
out.ended = new Date().toISOString();
await browser.close();
helper.stdin.write('quit\n');
console.log(JSON.stringify(out, null, 2));
