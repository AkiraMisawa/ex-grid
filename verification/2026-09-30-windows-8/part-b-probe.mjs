// verify-on-windows-8.md Part B: Part A's cases typed into /sheet with the real keyboard and mouse,
// once into D10 and once into the Formula Bar, and what ExSheet draws read from the DOM and from
// the page's pixels, for comparison with Excel's (verification/2026-09-29-windows-excel-8/).
//
//   MODE=cases   every case of CASES (all when unset), in both surfaces (SURFACES=cell,bar)
//   MODE=fast    case 1 sent as one burst of key events, as fast as by-hand.ps1's SendKeys sends
//                it, every animation frame sampled: whether a frame shows coloured text over
//                other characters (ADR-0057; DC-47). Three times per surface
//   MODE=hc      case 1 in both surfaces, for a run under Windows' high contrast
//   MODE=enter   not in the procedure: =A1+B1 and Enter sent 30 ms after the last key, then a press
//                on D10, three times. In a trial of case 21's set-up, D10 did not take the Focus
//                after that press
//
// Run on Windows from a copy of tests/ExGrid.Browser (for its node_modules), headed, the window at
// the display's own scale (viewport: null):
//
//     set EXGRID_CHANNEL=msedge & set LABEL=server-150 & set RTT=150 & set CONTROL=http://localhost:7298
//     set INPUT=...\input-server.ps1 & set OUT=...\x.json & set SHOTS=... & node part-b-probe.mjs http://localhost:5298
//
// Every input goes through input-server.ps1 (real OS input, SendInput). Playwright opens the page,
// reads the DOM and takes the page's pictures; it sends no input. The record is written to OUT as it
// goes, one case at a time.
import { chromium } from '@playwright/test';
import { spawn } from 'node:child_process';
import readline from 'node:readline';
import fs from 'node:fs';
import path from 'node:path';

const BASE = process.argv[2] ?? 'http://localhost:5299';
const CHANNEL = process.env.EXGRID_CHANNEL ?? 'chrome';
const LABEL = process.env.LABEL ?? 'wasm';
const MODE = process.env.MODE ?? 'cases';
const OUT = process.env.OUT;
const SHOTS = process.env.SHOTS;
const RTT = Number(process.env.RTT ?? 0);
const CONTROL = process.env.CONTROL;
const ONLY = process.env.CASES ? process.env.CASES.split(',') : null;
const SURFACES = (process.env.SURFACES ?? 'cell,bar').split(',');
const GAP_MS = 30;
const TITLE = `partb8-${LABEL}-${CHANNEL}`;
const out = { channel: CHANNEL, base: BASE, label: LABEL, mode: MODE, rtt: RTT, started: new Date().toISOString(), cases: [], messages: [] };
const save = () => { if (OUT) fs.writeFileSync(OUT, JSON.stringify(out, null, 1)); };
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// ---- The input helper ------------------------------------------------------------------------------

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
if ((await firstLine) !== 'ready') throw new Error('input-server did not start');

// Literal text in SendKeys' notation: + ^ % ~ ( ) [ ] { } braced.
const lit = (s) => s.replace(/[+^%~(){}[\]]/g, (m) => `{${m}}`);

// ---- The cases: Part A's, as the procedure writes them ----------------------------------------------

const one = (keys) => [{ state: 'typed', keys }];
const CASES = [
    { id: '1', states: one(lit('=A1+B1+C1+D1+E1+F1+G1+H1+I1+J1')) },
    { id: '2', states: one(lit('=A1+A1')) },
    { id: '3', states: one(lit('=A1+$A$1')) },
    { id: '4', states: one(lit('=A1+B1+A1')) },
    { id: '5', states: one(lit('=B2:A1')) },
    { id: '6', states: one(lit('=A1:B2+B2')) },
    // A reading after F2 is added (not in the procedure): typed into the Formula Bar, F2 was seen to
    // end the edit, and the keys after it then act on the grid.
    { id: '7', states: [{ state: 'typed', keys: lit('=A1+B1') }, { state: 'after-F2', keys: '{F2}', addition: true }, { state: 'after-deletion', keys: '{HOME}{RIGHT}{DEL 3}' }] },
    // Not in the procedure, and run only when named: case 7's keys one at a time, a reading after
    // each, to see which key ends the edit when they are typed into the Formula Bar.
    { id: '7k', extra: true, states: [{ state: 'typed', keys: lit('=A1+B1') }, { state: 'F2', keys: '{F2}' }, { state: 'HOME', keys: '{HOME}' }, { state: 'RIGHT', keys: '{RIGHT}' },
        { state: 'DEL-1', keys: '{DEL}' }, { state: 'DEL-2', keys: '{DEL}' }, { state: 'DEL-3', keys: '{DEL}' }] },
    { id: '8', states: one(lit('=Sheet1!A1')) },
    { id: '9', excluded: 'Part A adds a second sheet, Sheet2, and activates Sheet1. /sheet holds one Sheet, and ExSheet has no second sheet to add (ADR-0046: a Reference qualified with another name names no cells).' },
    { id: '10a', states: one(lit('=SUM(A:A)')) },
    { id: '10b', states: one(lit('=SUM(1:1)')) },
    { id: '11', states: one(lit('=SUM(Positions[PV])')) },
    { id: '12', states: one(lit('=SUM(Positions[PV])+SUM(Positions[Id])')) },
    { id: '13', states: one(lit('=SUM(A1,')) },
    { id: '14', states: one(lit('=A1+')) },
    { id: '15', states: one(lit('="A1"&B1')) },
    { id: '16', states: one(lit('=LOG10(A1)')) },
    { id: '17', states: one(lit('=a1')) },
    { id: '18', states: one('A1') },
    { id: '19', states: [{ state: 'pointing', keys: '={DOWN}' }] },
    { id: '20', states: [{ state: 'first-pointing', keys: '={DOWN}' }, { state: 'second-pointing', keys: '{+}{DOWN}' }] },
    { id: '20x', states: [{ state: 'first-pointing', keys: '={DOWN}' }, { state: 'second-pointing', keys: '{+}{DOWN}{DOWN}' }] },
    { id: '21', special: '21' },
    { id: '22', states: one(lit('=D10')) },
    { id: '23', excluded: 'Part A sets the sheet\'s zoom to 400%. /sheet has no zoom of its own; the browser\'s page zoom scales the whole page, which is not what Excel\'s sheet zoom does.' },
    { id: '24', states: one(lit('+A1')) },
    { id: '25', states: one(lit('-B2')) },
    { id: '26', states: one(lit('=SUM(A1:')) },
    { id: '27', states: one(lit('=SUM(Nope[PV]')) },
    { id: '28', states: one(lit('=SUM(Positions[Nope]')) },
    { id: '29', states: [{ state: 'pointing', keys: lit('=SUM(') + '{DOWN}' }] },
    { id: '30', states: [{ state: 'pointing', keys: lit('=1+') + '{DOWN}' }] },
    { id: '31', states: [{ state: 'pointing', keys: '={DOWN}{DOWN}' }] },
    { id: '32', states: [{ state: 'pointing', keys: lit('=D11+') + '{DOWN}{DOWN}' }, { state: 'after-5', keys: '5' }] },
];

// ---- What the page shows -----------------------------------------------------------------------------

// Everything read from the DOM in one state: which surface has the keyboard; each surface's field,
// its layer and the layer's spans with their computed colours; the Reference Outlines and Point's
// dashes over the Sheet, with the cells each covers; the positions grid's outlines, with the column
// each covers; the Focus, the Name Box, whether an edit is open, and the completion list.
const READ = () => {
    const hex = (css) => {
        if (!css) return null;
        let m = /^rgba?\(([\d.]+),\s*([\d.]+),\s*([\d.]+)(?:,\s*([\d.]+))?\)$/.exec(css);
        let r, g, b, a = 1;
        if (m) { [r, g, b] = [m[1], m[2], m[3]].map(Number); a = m[4] === undefined ? 1 : Number(m[4]); }
        else if ((m = /^color\(srgb ([\d.e-]+) ([\d.e-]+) ([\d.e-]+)(?: \/ ([\d.]+))?\)$/.exec(css))) { [r, g, b] = [m[1], m[2], m[3]].map((x) => Math.round(Number(x) * 255)); a = m[4] === undefined ? 1 : Number(m[4]); }
        else return css;
        const h = '#' + [r, g, b].map((x) => Math.max(0, Math.min(255, Math.round(x))).toString(16).padStart(2, '0')).join('');
        return a === 1 ? h : `${h}/${+a.toFixed(3)}`;
    };
    const letters = (c) => { let s = ''; c += 1; while (c > 0) { const m = (c - 1) % 26; s = String.fromCharCode(65 + m) + s; c = Math.floor((c - 1) / 26); } return s; };
    const box = (r) => ({ l: +r.left.toFixed(2), t: +r.top.toFixed(2), w: +r.width.toFixed(2), h: +r.height.toFixed(2) });
    const grid = document.querySelector('.ex-grid:has(> .ex-formula-bar)');
    const cells = [];
    for (const e of grid.querySelectorAll('[id]')) {
        const m = /-r(\d+)c(\d+)$/.exec(e.id);
        if (m) cells.push({ row: +m[1], col: +m[2], r: e.getBoundingClientRect() });
    }
    // The part of the Viewport the user sees: the scroller's client area. A cell counts as in view
    // when its middle is there (the grid renders a few cells beyond it).
    const sc = grid.querySelector('.ex-scroller'); const scr = sc.getBoundingClientRect();
    const view = { left: scr.left + sc.clientLeft, top: scr.top + sc.clientTop, right: scr.left + sc.clientLeft + sc.clientWidth, bottom: scr.top + sc.clientTop + sc.clientHeight };
    const inView = (c) => { const x = (c.r.left + c.r.right) / 2, y = (c.r.top + c.r.bottom) / 2; return x > view.left && x < view.right && y > view.top && y < view.bottom; };
    const covered = (r) => cells.filter((c) => { const x = (c.r.left + c.r.right) / 2, y = (c.r.top + c.r.bottom) / 2; return inView(c) && x > r.left && x < r.right && y > r.top && y < r.bottom; });
    const rangeOf = (list) => {
        if (list.length === 0) return 'none in view';
        const rows = list.map((c) => c.row), cols = list.map((c) => c.col);
        const a = letters(Math.min(...cols)) + (Math.min(...rows) + 1), b = letters(Math.max(...cols)) + (Math.max(...rows) + 1);
        return `${a === b ? a : `${a}:${b}`} (${list.length} in view)`;
    };
    const editor = grid.querySelector('.ex-viewport input.ex-editor, .ex-viewport .ex-editor input');
    const bar = grid.querySelector('input.ex-formula-bar-text, .ex-formula-bar-text input');
    const surface = (field) => {
        if (!field) return null;
        const layer = field.previousElementSibling?.classList.contains('ex-reference-text') ? field.previousElementSibling : null;
        const cs = getComputedStyle(field);
        return {
            value: field.value, selection: [field.selectionStart, field.selectionEnd], box: box(field.getBoundingClientRect()), scrollLeft: field.scrollLeft,
            shown: field.classList.contains('ex-reference-text-shown'), fill: hex(cs.webkitTextFillColor), color: hex(cs.color),
            layer: layer ? {
                text: layer.getAttribute('data-ex-text'), drawn: layer.textContent, visibility: getComputedStyle(layer).visibility,
                box: box(layer.getBoundingClientRect()), lineScrollLeft: layer.querySelector('.ex-reference-text-line')?.scrollLeft ?? null,
                spans: [...layer.querySelectorAll('span')].map((s) => {
                    const ss = getComputedStyle(s);
                    return { text: s.textContent, cls: s.className, color: hex(ss.color), fill: hex(ss.webkitTextFillColor), background: hex(ss.backgroundColor), box: box(s.getBoundingClientRect()) };
                }),
            } : null,
        };
    };
    const ae = document.activeElement;
    const outlineOf = (o) => {
        const cs = getComputedStyle(o); const r = o.getBoundingClientRect();
        return { cls: o.className, color: hex(cs.color), outline: `${cs.outlineStyle} ${cs.outlineWidth} ${hex(cs.outlineColor)} offset ${cs.outlineOffset}`, background: hex(cs.backgroundColor), box: box(r), cells: rangeOf(covered(r)) };
    };
    const pgrid = document.querySelector('#sheet-positions .ex-grid');
    const heads = pgrid ? [...pgrid.querySelectorAll('[role=columnheader]')].map((h) => ({ name: h.textContent.trim(), r: h.getBoundingClientRect() })) : [];
    const completion = grid.querySelector('.ex-completion');
    return {
        active: ae === editor ? 'cell' : ae === bar ? 'bar' : `${ae?.tagName?.toLowerCase()}.${typeof ae?.className === 'string' ? ae.className : ''}`,
        editing: document.querySelector('#sheet-undo')?.disabled ?? null,
        view: box({ left: view.left, top: view.top, width: view.right - view.left, height: view.bottom - view.top }),
        focus: grid.getAttribute('aria-activedescendant')?.replace(/^.*-r(\d+)c(\d+)$/, (s, r, c) => letters(+c) + (+r + 1)) ?? null,
        nameBox: grid.querySelector('input.ex-name-box, .ex-name-box input')?.value ?? null,
        cell: surface(editor), bar: surface(bar),
        outlines: [...grid.querySelectorAll('.ex-reference-outline')].map(outlineOf),
        points: [...grid.querySelectorAll('.ex-point')].map(outlineOf),
        focusOutline: [...grid.querySelectorAll('.ex-focus')].map(outlineOf),
        editorOutline: (() => { const e = grid.querySelector('.ex-viewport input.ex-editor'); if (!e) return null; const cs = getComputedStyle(e); return `${cs.outlineStyle} ${cs.outlineWidth} ${hex(cs.outlineColor)} offset ${cs.outlineOffset}`; })(),
        positions: pgrid ? [...pgrid.querySelectorAll('.ex-reference-outline')].map((o) => {
            const d = outlineOf(o); const r = o.getBoundingClientRect();
            d.columns = heads.filter((h) => { const x = (h.r.left + h.r.right) / 2; return x > r.left && x < r.right; }).map((h) => h.name);
            delete d.cells; return d;
        }) : null,
        completion: completion ? [...completion.querySelectorAll('[role=option]')].map((o) => o.textContent.trim()) : null,
        d10: [...grid.querySelectorAll('[id$="-r9c3"]')].map((e) => e.textContent).join('|'),
    };
};

// The pixels of a state: the page's own picture of the Sheet and the positions grid, and, read from
// it, for each span of the surface the edit is in the colours its text is drawn in, and for each
// outline the colour of its line, of its fill, and along its top edge the runs of Point's dashes.
async function pixels(page, read, clip, file, second) {
    const shot = await page.screenshot({ clip });
    if (file) fs.writeFileSync(file, shot);
    const shot2 = second ? await (async () => { await sleep(300); return page.screenshot({ clip }); })() : null;
    const boxes = [];
    const surf = read.active === 'bar' ? read.bar : read.cell;
    // A span is read only where it shows: inside its layer's box (the Cell Editor's text scrolls).
    const lb = surf?.layer?.box;
    for (const s of surf?.layer?.spans ?? []) {
        const l = Math.max(s.box.l, lb.l), t = Math.max(s.box.t, lb.t), r = Math.min(s.box.l + s.box.w, lb.l + lb.w), b = Math.min(s.box.t + s.box.h, lb.t + lb.h);
        boxes.push({ kind: 'text', text: s.text, box: { l, t, w: Math.max(0, r - l), h: Math.max(0, b - t) }, shownFraction: s.box.w > 0 ? +(Math.max(0, r - l) / s.box.w).toFixed(2) : 0 });
    }
    const v = read.view;
    const inside = (b) => { const l = Math.max(b.l, v.l), t = Math.max(b.t, v.t), r = Math.min(b.l + b.w, v.l + v.w), bb = Math.min(b.t + b.h, v.t + v.h); return { l, t, w: Math.max(0, r - l), h: Math.max(0, bb - t) }; };
    for (const o of read.outlines) boxes.push({ kind: 'outline', cells: o.cells, box: inside(o.box) });
    for (const o of read.positions ?? []) boxes.push({ kind: 'outline', cells: o.columns.join(','), box: o.box });
    for (const o of read.points) boxes.push({ kind: 'point', cells: o.cells, box: inside(o.box) });
    return page.evaluate(async ({ a, b, clip, boxes }) => {
        const load = async (b64) => {
            const bytes = Uint8Array.from(atob(b64), (c) => c.charCodeAt(0));
            const bmp = await createImageBitmap(new Blob([bytes], { type: 'image/png' }));
            const cv = new OffscreenCanvas(bmp.width, bmp.height); const ctx = cv.getContext('2d'); ctx.drawImage(bmp, 0, 0);
            return { w: bmp.width, h: bmp.height, d: ctx.getImageData(0, 0, bmp.width, bmp.height).data };
        };
        const A = await load(a); const B = b ? await load(b) : null;
        const s = A.w / clip.width;
        const at = (img, x, y) => { if (x < 0 || y < 0 || x >= img.w || y >= img.h) return null; const i = (y * img.w + x) * 4; return [img.d[i], img.d[i + 1], img.d[i + 2]]; };
        const hx = (p) => '#' + p.map((v) => v.toString(16).padStart(2, '0')).join('');
        const sat = (p) => Math.max(...p) - Math.min(...p);
        const top = (m, n) => [...m.entries()].sort((x, y) => y[1] - x[1]).slice(0, n).map(([k, v]) => `${k}:${v}`);
        const count = (m, k) => m.set(k, (m.get(k) ?? 0) + 1);
        const rect = (bx) => ({ x0: Math.round((bx.l - clip.x) * s), y0: Math.round((bx.t - clip.y) * s), x1: Math.round((bx.l + bx.w - clip.x) * s), y1: Math.round((bx.t + bx.h - clip.y) * s) });
        const res = [];
        for (const bx of boxes) {
            const r = rect(bx.box);
            if (bx.kind === 'text') {
                const all = new Map(), saturated = new Map();
                for (let y = r.y0; y < r.y1; y++) for (let x = r.x0; x < r.x1; x++) { const p = at(A, x, y); if (!p) continue; count(all, hx(p)); if (sat(p) > 60) count(saturated, hx(p)); }
                res.push({ kind: 'text', text: bx.text, shownFraction: bx.shownFraction, ground: top(all, 1)[0] ?? null, saturated: top(saturated, 3), darkest: [...all.keys()].sort((x, y) => parseInt(x.slice(1, 3), 16) + parseInt(x.slice(3, 5), 16) + parseInt(x.slice(5), 16) - (parseInt(y.slice(1, 3), 16) + parseInt(y.slice(3, 5), 16) + parseInt(y.slice(5), 16)))[0] ?? null });
            } else {
                // The line: the outermost 3 device pixels inside each edge. The fill: inside 6 px of
                // the edges. Along the top edge, 4 to 6 px in, the runs of the colour found there
                // most often that is not the fill's (Point's dashes lie just inside the line).
                const line = new Map(), fill = new Map();
                for (let y = r.y0; y < r.y1; y++) for (let x = r.x0; x < r.x1; x++) {
                    const p = at(A, x, y); if (!p) continue;
                    const d = Math.min(x - r.x0, r.x1 - 1 - x, y - r.y0, r.y1 - 1 - y);
                    if (d < 3) count(line, hx(p)); else if (d >= 9) count(fill, hx(p));
                }
                const item = { kind: bx.kind, cells: bx.cells, line: top(line, 3), fill: top(fill, 2) };
                if (bx.kind === 'point') {
                    const fillTop = top(fill, 1)[0]?.split(':')[0];
                    const band = new Map();
                    for (let y = r.y0 + 4; y < r.y0 + 7; y++) for (let x = r.x0; x < r.x1; x++) { const p = at(A, x, y); if (p && hx(p) !== fillTop) count(band, hx(p)); }
                    const dash = top(band, 1)[0]?.split(':')[0];
                    let runs = 0, on = false, y = r.y0 + 5;
                    for (let x = r.x0; x < r.x1; x++) { const p = at(A, x, y); const hit = p && hx(p) === dash; if (hit && !on) runs++; on = hit; }
                    item.dashes = { colour: dash ?? null, runsAlongTop: runs, band: top(band, 3) };
                    if (B) { let changed = 0; for (let yy = r.y0; yy < r.y1; yy++) for (let x = r.x0; x < r.x1; x++) { const p = at(A, x, yy), q = at(B, x, yy); if (p && q && (p[0] !== q[0] || p[1] !== q[1] || p[2] !== q[2])) changed++; } item.changedIn300ms = changed; }
                }
                res.push(item);
            }
        }
        return { scale: s, boxes: res };
    }, { a: shot.toString('base64'), b: shot2?.toString('base64') ?? null, clip, boxes });
}

// ---- The page ------------------------------------------------------------------------------------------

const browser = await chromium.launch({ channel: CHANNEL, headless: false, args: ['--window-position=40,40', '--window-size=1600,1050'] });
out.browserVersion = browser.version();
// Playwright emulates forced-colors: none and prefers-color-scheme: light unless told not to. With
// EMULATION=system neither is emulated, and the page sees what Windows gives the browser (the run
// under high contrast; the first one, at 18:51, ran under Playwright's emulation).
const EMULATION = process.env.EMULATION ?? 'playwright';
const context = await browser.newContext(EMULATION === 'system' ? { viewport: null, forcedColors: null, colorScheme: null } : { viewport: null });
out.emulation = EMULATION;
const page = await context.newPage();
page.on('console', (m) => out.messages.push({ t: new Date().toISOString(), type: m.type(), text: m.text() }));
page.on('pageerror', (e) => out.messages.push({ t: new Date().toISOString(), type: 'pageerror', text: String(e) }));
await page.goto(`${BASE}/sheet`);
await page.locator('#demo-interactive').waitFor({ state: 'attached', timeout: 60_000 });
await page.waitForFunction(() => {
    const g = document.querySelector('.ex-grid:has(> .ex-formula-bar)');
    const b12 = g && [...g.querySelectorAll('[id$="-r11c1"]')].map((e) => e.textContent).join('');
    return b12 && b12.includes('318.25');
}, null, { timeout: 60_000 });
await page.bringToFront();
await page.evaluate((t) => { document.title = t; }, TITLE);
await sleep(500);
await input(`front ${TITLE}`);
out.keyboard = { before: await input(`layout ${TITLE}`) };
out.keyboard.set = await input(`english ${TITLE}`);
await input('imeoff');
out.page = await page.evaluate(() => ({ dpr: devicePixelRatio, inner: [innerWidth, innerHeight], forcedColors: matchMedia('(forced-colors: active)').matches, dark: matchMedia('(prefers-color-scheme: dark)').matches, ua: navigator.userAgent }));

// Screen pixels for a client point of this tab, calibrated against the page's own mousemove.
await page.evaluate(() => { window.__move = null; document.addEventListener('mousemove', (e) => { window.__move = { x: e.clientX, y: e.clientY }; }, true); });
const cal = await (async () => {
    const g = await page.evaluate(() => ({ sx: window.screenX, sy: window.screenY, ow: window.outerWidth, oh: window.outerHeight, iw: window.innerWidth, ih: window.innerHeight, dpr: window.devicePixelRatio }));
    const border = (g.ow - g.iw) / 2;
    const target = { x: 400, y: 120 };
    let ox = (g.sx + border) * g.dpr, oy = (g.sy + (g.oh - g.ih) - border) * g.dpr;
    const tries = [];
    for (let i = 0; i < 5; i++) {
        await page.evaluate(() => { window.__move = null; });
        const px = Math.round(ox + target.x * g.dpr), py = Math.round(oy + target.y * g.dpr);
        await input(`move ${px} ${py}`);
        await sleep(200);
        const m = await page.evaluate(() => window.__move);
        tries.push({ px, py, client: m });
        if (!m) continue;
        const dx = target.x - m.x, dy = target.y - m.y;
        if (Math.abs(dx) < 1 && Math.abs(dy) < 1) break;
        ox += dx * g.dpr; oy += dy * g.dpr;
    }
    return { dpr: g.dpr, tries, at: (x, y) => [Math.round(ox + x * g.dpr), Math.round(oy + y * g.dpr)] };
})();
out.calibration = { dpr: cal.dpr, tries: cal.tries };
if (CONTROL) {
    const r = await fetch(`${CONTROL}/?rtt=${RTT}`, { method: 'POST' });
    out.roundTripSet = { status: r.status, now: await (await fetch(CONTROL)).text() };
}
save();

// The clip every picture is taken of: the Sheet, Formula Bar and Name Box included, and the positions
// grid beside it.
const clip = await page.evaluate(() => {
    const a = document.querySelector('.ex-grid:has(> .ex-formula-bar)').getBoundingClientRect();
    const b = document.querySelector('#sheet-positions .ex-grid').getBoundingClientRect();
    const l = Math.floor(Math.min(a.left, b.left)) - 4, t = Math.floor(Math.min(a.top, b.top)) - 4;
    return { x: l, y: t, width: Math.ceil(Math.max(a.right, b.right)) + 4 - l, height: Math.ceil(Math.max(a.bottom, b.bottom)) + 4 - t };
});
out.clip = clip;

async function centreOf(address) {
    const m = /^([A-Z]+)(\d+)$/.exec(address); let col = 0; for (const ch of m[1]) col = col * 26 + ch.charCodeAt(0) - 64;
    const r = await page.evaluate(([row, c]) => {
        const g = document.querySelector('.ex-grid:has(> .ex-formula-bar)');
        const e = g.querySelector(`[id$="-r${row}c${c}"]`); const b = e.getBoundingClientRect();
        return { x: (b.left + b.right) / 2, y: (b.top + b.bottom) / 2 };
    }, [Number(m[2]) - 1, col - 1]);
    return cal.at(r.x, r.y);
}
async function barEnd() {
    const r = await page.evaluate(() => { const b = document.querySelector('.ex-grid:has(> .ex-formula-bar) input.ex-formula-bar-text').getBoundingClientRect(); return { x: b.right - 4, y: (b.top + b.bottom) / 2 }; });
    return cal.at(r.x, r.y);
}
async function until(fn, ms = 5000, step = 50) {
    const t0 = Date.now();
    while (Date.now() - t0 < ms) { const v = await page.evaluate(fn); if (v) return Date.now() - t0; await sleep(step); }
    return null;
}
const state = () => page.evaluate(() => ({ editing: document.querySelector('#sheet-undo')?.disabled, focus: document.querySelector('.ex-grid:has(> .ex-formula-bar)').getAttribute('aria-activedescendant'), completion: !!document.querySelector('.ex-grid:has(> .ex-formula-bar) .ex-completion') }));

// No edit open: Escape until the Sheet says none is (a completion list takes the first).
async function reset() {
    let escapes = 0;
    for (let i = 0; i < 6; i++) {
        const s = await state();
        if (!s.editing && !s.completion) break;
        await input('type 0 {ESC}'); escapes++; await sleep(RTT ? 400 : 200);
    }
    return escapes;
}
async function selectD10() {
    const already = await page.evaluate(() => {
        const g = document.querySelector('.ex-grid:has(> .ex-formula-bar)');
        return /-r9c3$/.test(g.getAttribute('aria-activedescendant') ?? '') && !document.querySelector('#sheet-undo').disabled;
    });
    // A press on D10 while it has the Focus, soon after the last, would be a double-click and
    // open an edit (seen in the first full run): D10 is pressed only when the Focus is elsewhere.
    if (already) return 0;
    const [x, y] = await centreOf('D10');
    await input(`click ${x} ${y}`);
    const ms = await until(() => {
        const g = document.querySelector('.ex-grid:has(> .ex-formula-bar)');
        const nb = g.querySelector('input.ex-name-box, .ex-name-box input');
        return /-r9c3$/.test(g.getAttribute('aria-activedescendant') ?? '') && nb?.value === 'D10' && !document.querySelector('#sheet-undo').disabled;
    });
    if (ms === null) { const r = await page.evaluate(READ); throw new Error(`D10 did not take the Focus: focus ${r.focus}, name box ${r.nameBox}, editing ${r.editing}, active ${r.active}`); }
    return ms;
}
// D10 holds nothing before a case: a case that left something there is cleared with Delete (and
// the record says so).
async function cleanD10() {
    const text = await page.evaluate(() => [...document.querySelector('.ex-grid:has(> .ex-formula-bar)').querySelectorAll('[id$="-r9c3"]')].map((e) => e.textContent).join(''));
    const bar = await page.evaluate(() => document.querySelector('.ex-grid:has(> .ex-formula-bar) input.ex-formula-bar-text').value);
    if (text === '' && bar === '') return '';
    await input('type 0 {DEL}');
    await until(() => document.querySelector('.ex-grid:has(> .ex-formula-bar) input.ex-formula-bar-text').value === '', 5000);
    return `held ${JSON.stringify(bar)} (${JSON.stringify(text)}), cleared with Delete`;
}
async function intoBar() {
    const [x, y] = await barEnd();
    await input(`click ${x} ${y}`);
    return until(() => document.activeElement === document.querySelector('.ex-grid:has(> .ex-formula-bar) input.ex-formula-bar-text'), 3000);
}
// The layer of the surface the edit is in has caught up with its field (or there is none).
const settledFn = () => {
    const g = document.querySelector('.ex-grid:has(> .ex-formula-bar)');
    const f = document.activeElement;
    if (!(f instanceof HTMLInputElement) || !g.contains(f)) return true;
    const layer = f.previousElementSibling?.classList.contains('ex-reference-text') ? f.previousElementSibling : null;
    return !layer || layer.getAttribute('data-ex-text') === f.value;
};

async function take(caseId, surface, stateName, did, extra) {
    const t0 = Date.now();
    await sleep(600);
    let settled = await page.evaluate(settledFn);
    let waited = 600;
    if (!settled) { const ms = await until(settledFn, 4000, 50); waited += ms ?? 4000; settled = ms !== null; if (settled) await sleep(150); }
    const read = await page.evaluate(READ);
    const file = SHOTS ? path.join(SHOTS, `${caseId}-${surface}-${stateName}-${LABEL}-${CHANNEL}.png`) : null;
    const px = await pixels(page, read, clip, file, true);
    return { state: stateName, addition: !!extra?.addition, did, readAtMs: waited, settled, read, pixels: px, shot: file ? path.basename(file) : null, tookMs: Date.now() - t0 };
}

async function runCase(c, surface) {
    const rec = { case: c.id, surface, started: new Date().toISOString(), states: [] };
    rec.escapesBefore = await reset();
    rec.focusMs = await selectD10();
    rec.d10Before = await cleanD10();
    if (surface === 'bar') rec.barMs = await intoBar();
    for (const s of c.states) {
        await input(`type ${GAP_MS} ${s.keys}`);
        rec.states.push(await take(c.id, surface, s.state, s.keys, s));
    }
    rec.escapesAfter = await reset();
    rec.after = await state();
    rec.d10After = await page.evaluate(() => [...document.querySelector('.ex-grid:has(> .ex-formula-bar)').querySelectorAll('[id$="-r9c3"]')].map((e) => e.textContent).join('|'));
    return rec;
}

// Case 21: =A1+B1 entered into D10 with keys (Part A wrote it through COM), then its four states.
async function runCase21() {
    const rec = { case: '21', surface: 'cell, then the Formula Bar (the case\'s own states)', started: new Date().toISOString(), states: [] };
    rec.escapesBefore = await reset();
    await selectD10();
    await input(`type ${GAP_MS} ${lit('=A1+B1')}`);
    await sleep(600);
    rec.beforeEnter = await page.evaluate(READ);
    await input('type 0 {ENTER}');
    rec.enteredMs = await until(() => !document.querySelector('#sheet-undo').disabled, 5000);
    await sleep(300);
    rec.afterEnter = await page.evaluate(READ);
    rec.setUp = '=A1+B1 typed into D10 and entered with Enter';
    await selectD10();
    rec.states.push(await take('21', 'cell', 'selected', 'a click on D10, no edit'));
    await input('type 0 {F2}');
    rec.states.push(await take('21', 'cell', 'F2', '{F2}'));
    await input('type 0 {ESC}'); await until(() => !document.querySelector('#sheet-undo').disabled, 5000);
    const [x, y] = await centreOf('D10');
    await input(`dblclick ${x} ${y}`);
    rec.states.push(await take('21', 'cell', 'double-click', `{ESC}, then a double-click at (${x}, ${y}), D10's middle`));
    await input('type 0 {ESC}'); await until(() => !document.querySelector('#sheet-undo').disabled, 5000);
    const [bx, by] = await barEnd();
    await input(`click ${bx} ${by}`);
    rec.states.push(await take('21', 'bar', 'formula-bar-click', `{ESC}, then a click at (${bx}, ${by}), 4 CSS px inside the Formula Bar's right end`));
    rec.escapesAfter = await reset();
    await selectD10();
    await input('type 0 {DEL}');
    rec.cleanUp = { ms: await until(() => [...document.querySelector('.ex-grid:has(> .ex-formula-bar)').querySelectorAll('[id$="-r9c3"]')].map((e) => e.textContent).join('') === '', 5000), keys: 'a click on D10, then Delete' };
    rec.d10After = await page.evaluate(() => [...document.querySelector('.ex-grid:has(> .ex-formula-bar)').querySelectorAll('[id$="-r9c3"]')].map((e) => e.textContent).join('|'));
    return rec;
}

// Every animation frame from here: whenever a field's text is transparent or its layer shows, the
// layer's text, as written and as drawn, against the field's value (DC-47's frame).
const FRAMES_ON = () => {
    const frames = { sampled: 0, shown: 0, wrong: [] };
    const sample = () => {
        for (const layer of document.querySelectorAll('.ex-reference-text')) {
            const field = layer.nextElementSibling;
            if (!(field instanceof HTMLInputElement)) continue;
            const transparent = getComputedStyle(field).webkitTextFillColor === 'rgba(0, 0, 0, 0)';
            const visible = getComputedStyle(layer).visibility === 'visible';
            if (transparent || visible) {
                frames.shown++;
                const text = layer.getAttribute('data-ex-text');
                if (text !== field.value || layer.textContent !== field.value) frames.wrong.push({ t: Math.round(performance.now()), value: field.value, text, drawn: layer.textContent, transparent, visible });
            }
        }
        frames.sampled++;
        window.__framesReq = requestAnimationFrame(sample);
    };
    window.__frames = frames;
    window.__framesReq = requestAnimationFrame(sample);
};
const FRAMES_OFF = () => { cancelAnimationFrame(window.__framesReq); const f = window.__frames; delete window.__frames; delete window.__framesReq; return f; };

async function runFast(surface, rep) {
    const text = '=A1+B1+C1+D1+E1+F1+G1+H1+I1+J1';
    const rec = { case: '1 (fast)', surface, rep, started: new Date().toISOString() };
    rec.escapesBefore = await reset();
    await selectD10();
    if (surface === 'bar') rec.barMs = await intoBar();
    await page.evaluate(FRAMES_ON);
    rec.burst = await input(`burst ${lit(text)}`);
    rec.valueMs = await (async () => { const t0 = Date.now(); while (Date.now() - t0 < 8000) { if ((await page.evaluate(() => document.activeElement?.value)) === text) return Date.now() - t0; await sleep(20); } return null; })();
    rec.colouredMs = await until(() => { const f = document.activeElement; return f?.classList.contains('ex-reference-text-shown'); }, 8000, 20);
    await sleep(500);
    rec.frames = await page.evaluate(FRAMES_OFF);
    rec.read = await page.evaluate(READ);
    rec.escapesAfter = await reset();
    return rec;
}

try {
    if (MODE === 'cases' || MODE === 'hc') {
        const list = MODE === 'hc' ? CASES.filter((c) => c.id === '1') : CASES.filter((c) => (!ONLY && !c.extra) || ONLY?.includes(c.id));
        for (const c of list) {
            if (c.excluded) { out.cases.push({ case: c.id, excluded: c.excluded }); save(); continue; }
            if (c.special === '21') {
                try { out.cases.push(await runCase21()); } catch (e) { out.cases.push({ case: '21', error: String(e) }); await reset().catch(() => {}); }
                save(); continue;
            }
            for (const surface of SURFACES) {
                try { out.cases.push(await runCase(c, surface)); } catch (e) { out.cases.push({ case: c.id, surface, error: String(e) }); await reset().catch(() => {}); }
                save();
            }
        }
    } else if (MODE === 'enter') {
        for (let rep = 1; rep <= 3; rep++) {
            const rec = { case: '21 set-up (Enter 30 ms after the last key)', rep, started: new Date().toISOString() };
            rec.escapesBefore = await reset();
            rec.focusMs = await selectD10();
            rec.d10Before = await cleanD10();
            await input(`type ${GAP_MS} ${lit('=A1+B1')}{ENTER}`);
            await sleep(1000);
            rec.afterEnter = await page.evaluate(READ);
            const [x, y] = await centreOf('D10');
            await input(`click ${x} ${y}`);
            rec.pressedAt = [x, y];
            await sleep(2000);
            rec.afterPress = await page.evaluate(READ);
            rec.escapes = await reset();
            rec.focusAfterMs = await selectD10().catch((e) => String(e));
            rec.cleared = await cleanD10();
            out.cases.push(rec); save();
        }
    } else if (MODE === 'fast') {
        for (const surface of SURFACES) for (let rep = 1; rep <= 3; rep++) { out.cases.push(await runFast(surface, rep)); save(); }
    }
} finally {
    if (CONTROL) await fetch(`${CONTROL}/?rtt=0`, { method: 'POST' }).catch(() => {});
    out.keyboard.after = await input(`layout ${TITLE}`).catch((e) => String(e));
    out.finished = new Date().toISOString();
    save();
    helper.stdin.write('quit\n');
    await browser.close();
}
console.log(JSON.stringify({ label: LABEL, channel: CHANNEL, mode: MODE, cases: out.cases.length, errors: out.cases.filter((c) => c.error).length, messages: out.messages.length, browser: out.browserVersion }));
