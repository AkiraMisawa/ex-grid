// docs/specs/exsheet/verify-on-windows-12.md on claude/exsheet-ninth-run-b (run as the thirteenth
// Windows run), Part B: ExSheet beside ADR-0058's "What Part B of the ninth Windows run settled" (Q49,
// Q51, Q52, Q53; tickets 55-58), with the real keyboard and mouse, on /sheet, /pointing and
// /pointing?narrow. What the Sheet writes and draws is read from the DOM and from the page's pixels.
// This is the ninth run's Part B probe (verification/2026-10-01-windows-9/pointing-scope-probe.mjs)
// with this run's pages and cases; the method is unchanged.
//
//   PAGE=sheet|pointing|narrow   the page (narrow is /pointing?narrow)
//   CASES=a,b                    only these cases (an addition runs only when named)
//
// Run on Windows from a copy of tests/ExGrid.Browser (for its node_modules), headed, the window at
// the display's own scale (viewport: null):
//
//     set EXGRID_CHANNEL=msedge & set LABEL=server-150 & set RTT=150 & set CONTROL=http://localhost:7298
//     set PAGE=sheet & set INPUT=...\input-server.ps1 & set OUT=...\x.json & set SHOTS=...
//     node pointing-scope-probe.mjs http://localhost:5298
//
// Every input goes through input-server.ps1 (real OS input, SendInput). Playwright opens the page,
// reads the DOM and takes the page's pictures; it sends no input. The page gets one listener of the
// probe's own, on focusin, that notes where DOM focus goes. The record is written to OUT as it goes.
import { chromium } from '@playwright/test';
import { spawn } from 'node:child_process';
import readline from 'node:readline';
import fs from 'node:fs';
import path from 'node:path';

const BASE = process.argv[2] ?? 'http://localhost:5299';
const CHANNEL = process.env.EXGRID_CHANNEL ?? 'chrome';
const LABEL = process.env.LABEL ?? 'wasm';
const PAGE = process.env.PAGE ?? 'sheet';
const OUT = process.env.OUT;
const SHOTS = process.env.SHOTS;
const RTT = Number(process.env.RTT ?? 0);
const CONTROL = process.env.CONTROL;
const ONLY = process.env.CASES ? process.env.CASES.split(',') : null;
const GAP_MS = 30;
const TITLE = `pointing13-${PAGE}-${LABEL}-${CHANNEL}`;
const out = { channel: CHANNEL, base: BASE, page: PAGE, label: LABEL, rtt: RTT, started: new Date().toISOString(), cases: [], messages: [] };
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

// ---- The pages ----------------------------------------------------------------------------------------

// Each page: its path, the Sheet that points (the index-th grid with a Formula Bar), the grids
// registered in its Scope ("positions") and any other grid ("other"), where it says why a press wrote
// nothing, the Sheet's cell the cases type into, and what shows that the Linked Table has landed.
const PAGES = {
    sheet: { path: '/sheet', sheet: 0, positions: '#sheet-positions .ex-grid', refusal: '#sheet-pointing', target: 'D10',
        ready: { sheet: 0, cell: 'B12', text: '318.25' } },
    pointing: { path: '/pointing', sheet: 0, positions: '#pointing-positions .ex-grid', refusal: '#pointing-refused', target: 'C3',
        ready: { sheet: 0, cell: 'B1', text: '8200' } },
    narrow: { path: '/pointing?narrow', sheet: 0, positions: '#pointing-positions .ex-grid', refusal: '#pointing-refused', target: 'C3',
        ready: { sheet: 0, cell: 'B1', text: '8200' } },
};
const P = PAGES[PAGE];

// ---- The cases -------------------------------------------------------------------------------------------

// A step: keys ({keys}), a press on a cell of a grid ({press: [grid, address]}), a press on a column
// header ({header: [grid, label]}), a drag between two cells ({drag: [grid, from, to]}), a wait
// ({wait}), or a wait until the positions grid wears ex-pointed-at ({waitPointed}). The last comes
// between keys that make the Sheet point and a press: behind the 150 ms proxy, a press sent sooner
// lands within the round trip after the keys, and is an ordinary press (ADR-0058, "On a circuit"; the
// procedure asks for the wait). On WebAssembly and at 0 ms the grid is pointed at before the wait
// begins. A state is steps, then a reading. A case's `setup` scrolls the positions grid first.
const K = (keys) => ({ keys });
const LOOKUP = '=XLOOKUP(1,A2:A4,B2:B4,,';
const PV_HEADER = [K('='), { waitPointed: true }, { header: ['positions', 'PV'] }];
const CASES = {
    sheet: [
        { id: 'b1', what: `${LOOKUP}1), F2, Left Left, then Tab. Reading: no list after Left Left; Tab commits and moves to E10 (Q49)`, states: [
            { state: 'typed', steps: [K(lit(`${LOOKUP}1)`))] }, { state: 'f2', steps: [K('{F2}')] },
            { state: 'caret-moved', steps: [K('{LEFT}{LEFT}')] }, { state: 'tab', steps: [K('{TAB}')] }] },
        { id: 'b2', what: `${LOOKUP}-1), F2, Left, then Tab (the procedure's keys). Reading: no list; Tab commits (Q49, the caret inside a value)`, states: [
            { state: 'typed', steps: [K(lit(`${LOOKUP}-1)`))] }, { state: 'f2', steps: [K('{F2}')] },
            { state: 'caret-moved', steps: [K('{LEFT}')] }, { state: 'tab', steps: [K('{TAB}')] }] },
        // An addition: b2 with Left twice, which puts the caret between - and 1, as Part A's case 10 did
        // in Excel. The procedure's one Left puts it between 1 and ).
        { id: 'b2x', extra: true, what: `An addition: ${LOOKUP}-1), F2, Left Left (the caret between - and 1, as Part A's case 10), then Tab`, states: [
            { state: 'typed', steps: [K(lit(`${LOOKUP}-1)`))] }, { state: 'f2', steps: [K('{F2}')] },
            { state: 'caret-moved', steps: [K('{LEFT}{LEFT}')] }, { state: 'tab', steps: [K('{TAB}')] }] },
        { id: 'b3', what: `${LOOKUP}, then Home. Reading: the list closes; A10 is pointed at and written (Q51)`, states: [
            { state: 'typed', steps: [K(lit(LOOKUP))] }, { state: 'home', steps: [K('{HOME}')] }] },
        { id: 'b4', what: `${LOOKUP}, then End. Reading: the list closes; nothing is written, and the edit stays open (Q53)`, states: [
            { state: 'typed', steps: [K(lit(LOOKUP))] }, { state: 'end', steps: [K('{END}')] }] },
        { id: 'b4a', what: '=SUM(, then Home; Escape; =SUM(, then End. Reading: =SUM(A10, A10 pointed at; then nothing written, the edit open (Q53)', states: [
            { state: 'typed', steps: [K(lit('=SUM('))] }, { state: 'home', steps: [K('{HOME}')] },
            { state: 'escaped', steps: [K('{ESC}')] },
            { state: 'typed-again', steps: [K(lit('=SUM('))] }, { state: 'end', steps: [K('{END}')] }] },
        { id: 'b5', what: `${LOOKUP}, then Shift+Right. Reading: the list closes; D10:E10 is pointed at and written (Q51)`, states: [
            { state: 'typed', steps: [K(lit(LOOKUP))] }, { state: 'shift-right', steps: [K('+{RIGHT}')] }] },
        { id: 'b6', what: '=Posit, then Home. Reading: the list of names: the caret moves to 0; the edit stays open (Q51, unchanged)', states: [
            { state: 'typed', steps: [K(lit('=Posit'))] }, { state: 'home', steps: [K('{HOME}')] }] },
        // Additions: Part A's groups 1 and 2, typed into ExSheet, so that what Excel answered stands
        // beside what ExSheet does now (the ninth run typed the tenth run's cases the same way).
        ...[['a1', `${LOOKUP}A1`], ['a2', `${LOOKUP}1+`], ['a3', `${LOOKUP}X`], ['a4', `${LOOKUP}AV`], ['a5', `${LOOKUP}Positions`],
            ['a6', `${LOOKUP}"`], ['a7', `${LOOKUP}0,A`], ['a8', `${LOOKUP}0,5`], ['a9', '=SUM(A']].map(([id, text]) => (
            { id, extra: true, what: `An addition, Part A's case ${id.slice(1)}: ${text}`, states: [{ state: 'typed', steps: [K(lit(text))] }] })),
        ...[['a10', `${LOOKUP}-1)`, '{LEFT}{LEFT}'], ['a11', `${LOOKUP}10)`, '{LEFT}{LEFT}'], ['a11a', `${LOOKUP}  )`, '{LEFT}{LEFT}{LEFT}']].map(([id, text, left]) => (
            { id, extra: true, what: `An addition, Part A's case ${id.slice(1)}: ${text}, F2, ${left}, then Tab`, states: [
                { state: 'typed', steps: [K(lit(text))] }, { state: 'f2', steps: [K('{F2}')] },
                { state: 'caret-moved', steps: [K(left)] }, { state: 'tab', steps: [K('{TAB}')] }] })),
    ],
    pointing: [
        { id: 'b7', what: '=, a press on PV\'s header, Down, Down. Reading: =XLOOKUP("R-1", ...), then "R-2"; the dashes on the cell (Q52)', states: [
            { state: 'header', steps: PV_HEADER },
            { state: 'down-1', steps: [K('{DOWN}')] }, { state: 'down-2', steps: [K('{DOWN}')] }] },
        { id: 'b8', what: '=, a press on PV\'s header, Left. Reading: =Positions[Id], passing over Book; the dashes over Id\'s body (Q52)', states: [
            { state: 'header', steps: PV_HEADER }, { state: 'left', steps: [K('{LEFT}')] }] },
        { id: 'b9', what: '=, a press on PV\'s header, Up. Reading: unchanged; nothing told (Q52, an edge)', states: [
            { state: 'header', steps: PV_HEADER }, { state: 'up', steps: [K('{UP}')] }] },
        { id: 'b10', what: '=, a drag down PV\'s data (R-1 to R-9), then Down. Reading: =, the drag\'s reason told; Down points in the Sheet, at C4', states: [
            { state: 'typed', steps: [K('='), { waitPointed: true }] },
            { state: 'drag', steps: [{ drag: ['positions', 'C1', 'C9'] }] },
            { state: 'down', steps: [K('{DOWN}')] }] },
    ],
    narrow: [
        { id: 'b10a', what: 'The grid scrolled to its first column; =, a press on Id\'s header, Right, then Left. Reading: =Positions[PV], the grid scrolled across so that PV\'s dashes lie whole in view, not scrolled down; then =Positions[Id], Id whole in view (ticket 58)', setup: 'first-column', states: [
            { state: 'scrolled', steps: [] },
            { state: 'header', steps: [K('='), { waitPointed: true }, { header: ['positions', 'Id'] }] },
            { state: 'right', steps: [K('{RIGHT}')] }, { state: 'left', steps: [K('{LEFT}')] }] },
        // An addition: b10a with the rows wheeled down first, so that "not scrolled down" is asked of a
        // grid whose vertical offset is not 0 (ticket 58's layer-3 test scrolls 280 px down).
        { id: 'b10ax', extra: true, what: 'An addition: b10a with the grid wheeled down 5 turns first, its first column in view', setup: 'first-column-down', states: [
            { state: 'scrolled', steps: [] },
            { state: 'header', steps: [K('='), { waitPointed: true }, { header: ['positions', 'Id'] }] },
            { state: 'right', steps: [K('{RIGHT}')] }, { state: 'left', steps: [K('{LEFT}')] }] },
        { id: 'b11', what: 'The grid scrolled to its last row and column; =, a press on R-40\'s PV. Reading: the dashes and both column outlines inside the client area, beside both gutters (DC-53)', setup: 'last-row-column', states: [
            { state: 'scrolled', steps: [] },
            { state: 'pressed', steps: [K('='), { waitPointed: true }, { press: ['positions', 'C40'] }] }] },
    ],
};

// ---- What the page shows -----------------------------------------------------------------------------

// Everything read from the DOM in one state. The Sheet: which surface has the keyboard, whether an
// edit is open, the Focus and the Name Box, each surface's field with its layer's spans, the
// completion list. Each other grid: whether it is pointed at, its keyboard Focus and Selection, the
// Reference Outlines and the dashes with the cells each covers (by the row's Id and the column's
// header), its scroller. And the page's line that says why a press wrote nothing.
const READ = (cfg) => {
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
    const sheet = document.querySelectorAll('.ex-grid:has(> .ex-formula-bar)')[cfg.sheet];
    const editor = sheet.querySelector('.ex-viewport input.ex-editor, .ex-viewport .ex-editor input');
    const bar = sheet.querySelector('input.ex-formula-bar-text, .ex-formula-bar-text input');
    const grids = { positions: document.querySelector(cfg.positions), other: cfg.other ? document.querySelector(cfg.other) : null };
    const surface = (field) => {
        if (!field) return null;
        const layer = field.previousElementSibling?.classList.contains('ex-reference-text') ? field.previousElementSibling : null;
        const cs = getComputedStyle(field);
        return {
            value: field.value, selection: [field.selectionStart, field.selectionEnd], box: box(field.getBoundingClientRect()),
            // Added for the extra runs (after the recorded runs had begun): the field's own horizontal
            // scroll, and its scrollable width.
            scrollLeft: field.scrollLeft, scrollWidth: field.scrollWidth, clientWidth: field.clientWidth,
            shown: field.classList.contains('ex-reference-text-shown'), fill: hex(cs.webkitTextFillColor), color: hex(cs.color),
            layer: layer ? {
                text: layer.getAttribute('data-ex-text'), visibility: getComputedStyle(layer).visibility, box: box(layer.getBoundingClientRect()), scrollLeft: layer.scrollLeft,
                spans: [...layer.querySelectorAll('span')].map((s) => {
                    const ss = getComputedStyle(s);
                    return { text: s.textContent, pointed: s.classList.contains('ex-reference-pointed'), cls: s.className, color: hex(ss.color), fill: hex(ss.webkitTextFillColor), background: hex(ss.backgroundColor), box: box(s.getBoundingClientRect()) };
                }),
            } : null,
        };
    };
    const ae = document.activeElement;
    const where = (e) => {
        if (!e) return null;
        if (e === editor) return 'sheet:cell';
        if (e === bar) return 'sheet:bar';
        if (sheet.contains(e)) return `sheet:${e.tagName.toLowerCase()}.${typeof e.className === 'string' ? e.className.split(' ')[0] : ''}`;
        for (const [n, g] of Object.entries(grids)) if (g && g.contains(e)) return `${n}:${e === g ? 'root' : e.tagName.toLowerCase() + '.' + (typeof e.className === 'string' ? e.className.split(' ')[0] : '')}`;
        return `${e.tagName.toLowerCase()}${e.id ? '#' + e.id : ''}`;
    };
    // A grid's painted cells and headers, and what a box covers: the rows (by the Id each shows in the
    // first column) and the columns (by header) whose cells' middles lie inside it.
    const gridRead = (g) => {
        if (!g) return null;
        const heads = [...g.querySelectorAll('.ex-header-cell')].map((h) => ({ name: h.textContent.trim(), r: h.getBoundingClientRect() }));
        const cells = [];
        for (const e of g.querySelectorAll('[id]')) { const m = /-r(\d+)c(\d+)$/.exec(e.id); if (m) cells.push({ row: +m[1], col: +m[2], text: e.textContent, r: e.getBoundingClientRect() }); }
        const idOf = (row) => cells.find((c) => c.row === row && c.col === 0)?.text ?? `row ${row + 1}`;
        const sc = g.querySelector('.ex-scroller'); const scr = sc.getBoundingClientRect();
        const client = { l: scr.left + sc.clientLeft, t: scr.top + sc.clientTop, w: sc.clientWidth, h: sc.clientHeight };
        const covers = (r) => {
            const inside = cells.filter((c) => { const x = (c.r.left + c.r.right) / 2, y = (c.r.top + c.r.bottom) / 2; return x > r.left && x < r.right && y > r.top && y < r.bottom; });
            const rows = [...new Set(inside.map((c) => c.row))].sort((a, b) => a - b);
            const cols = [...new Set(inside.map((c) => c.col))].sort((a, b) => a - b);
            const colName = (c) => heads.find((h) => { const hx = (h.r.left + h.r.right) / 2; const cc = inside.find((x) => x.col === c); return cc && hx > cc.r.left && hx < cc.r.right; })?.name ?? letters(c);
            return { columns: cols.map(colName), rows: rows.length === 0 ? 'none' : rows.length === 1 ? idOf(rows[0]) : `${idOf(rows[0])}..${idOf(rows[rows.length - 1])} (${rows.length})` };
        };
        const drawn = (o) => {
            const cs = getComputedStyle(o); const r = o.getBoundingClientRect();
            return { cls: o.className, color: hex(cs.color), lineColour: hex(cs.outlineStyle !== 'none' ? cs.outlineColor : cs.borderTopColor), outline: `${cs.outlineStyle} ${cs.outlineWidth} ${hex(cs.outlineColor)} offset ${cs.outlineOffset}`,
                border: `${cs.borderTopStyle} ${cs.borderTopWidth} ${hex(cs.borderTopColor)}`, background: hex(cs.backgroundColor), box: box(r), covers: covers(r),
                insideClient: r.left >= client.l - 0.5 && r.top >= client.t - 0.5 && r.right <= client.l + client.w + 0.5 && r.bottom <= client.t + client.h + 0.5 };
        };
        return {
            pointedAt: g.classList.contains('ex-pointed-at'),
            activeDescendant: g.getAttribute('aria-activedescendant'),
            selectionDrawn: g.querySelectorAll('.ex-focus, .ex-range').length,
            sort: [...g.querySelectorAll('.ex-header-cell[aria-sort]')].map((h) => `${h.textContent.trim()} ${h.getAttribute('aria-sort')}`),
            outlines: [...g.querySelectorAll('.ex-reference-outline')].map(drawn),
            dashes: [...g.querySelectorAll('.ex-point-dashes')].map(drawn),
            scroller: { top: sc.scrollTop, left: sc.scrollLeft, clientWidth: sc.clientWidth, clientHeight: sc.clientHeight, offsetWidth: sc.offsetWidth, offsetHeight: sc.offsetHeight, scrollWidth: sc.scrollWidth, scrollHeight: sc.scrollHeight, client },
            headers: heads.map((h) => ({ name: h.name, box: box(h.r) })),
            firstRow: idOf(Math.min(...cells.map((c) => c.row))), lastRow: idOf(Math.max(...cells.map((c) => c.row))),
        };
    };
    const completion = sheet.querySelector('.ex-completion');
    const target = cfg.targetCell;
    return {
        active: where(ae),
        editing: !!editor,
        focus: sheet.getAttribute('aria-activedescendant')?.replace(/^.*-r(\d+)c(\d+)$/, (s, r, c) => letters(+c) + (+r + 1)) ?? null,
        nameBox: sheet.querySelector('input.ex-name-box, .ex-name-box input')?.value ?? null,
        cell: surface(editor), bar: surface(bar),
        completion: completion ? [...completion.querySelectorAll('[role=option]')].map((o) => ({ text: o.textContent.trim(), selected: o.getAttribute('aria-selected') === 'true' })) : null,
        completionElement: completion ? { cls: completion.className, text: completion.textContent.trim().slice(0, 200), box: box(completion.getBoundingClientRect()) } : null,
        hints: [...sheet.querySelectorAll('.ex-argument-hint, [class*="hint"]')].map((h) => ({ cls: h.className, text: h.textContent.trim().slice(0, 200) })),
        sheetOutlines: [...sheet.querySelectorAll('.ex-reference-outline')].map((o) => o.className),
        sheetPoints: [...sheet.querySelectorAll('.ex-point, .ex-point-dashes')].map((o) => o.className),
        positions: gridRead(grids.positions), other: gridRead(grids.other),
        refusal: document.querySelector(cfg.refusal)?.textContent ?? null,
        targetText: [...sheet.querySelectorAll(`[id$="-r${target.row}c${target.col}"]`)].map((e) => e.textContent).join('|'),
        focusLog: (window.__focusLog ?? []).splice(0),
    };
};

// The pixels of a state: the page's own picture of the grids, and, read from it, the colours of the
// spans of the surface the edit is in, and for each outline and each dashed line in the positions
// grid, its line, its fill and, along each of its four sides, the runs of its dashes.
async function pixels(page, read, clip, file) {
    const shot = await page.screenshot({ clip });
    if (file) fs.writeFileSync(file, shot);
    await sleep(300);
    const shot2 = await page.screenshot({ clip });
    const boxes = [];
    const surf = read.active === 'sheet:bar' ? read.bar : read.cell;
    const lb = surf?.layer?.box;
    for (const s of surf?.layer?.spans ?? []) {
        const l = Math.max(s.box.l, lb.l), t = Math.max(s.box.t, lb.t), r = Math.min(s.box.l + s.box.w, lb.l + lb.w), b = Math.min(s.box.t + s.box.h, lb.t + lb.h);
        boxes.push({ kind: 'text', text: s.text, box: { l, t, w: Math.max(0, r - l), h: Math.max(0, b - t) } });
    }
    for (const g of ['positions', 'other']) {
        for (const o of read[g]?.outlines ?? []) boxes.push({ kind: 'outline', grid: g, covers: o.covers, box: o.box });
        for (const o of read[g]?.dashes ?? []) boxes.push({ kind: 'dashes', grid: g, covers: o.covers, box: o.box, want: o.lineColour });
    }
    return page.evaluate(async ({ a, b, clip, boxes }) => {
        const load = async (b64) => {
            const bytes = Uint8Array.from(atob(b64), (c) => c.charCodeAt(0));
            const bmp = await createImageBitmap(new Blob([bytes], { type: 'image/png' }));
            const cv = new OffscreenCanvas(bmp.width, bmp.height); const ctx = cv.getContext('2d'); ctx.drawImage(bmp, 0, 0);
            return { w: bmp.width, h: bmp.height, d: ctx.getImageData(0, 0, bmp.width, bmp.height).data };
        };
        const A = await load(a); const B = await load(b);
        const s = A.w / clip.width;
        const at = (img, x, y) => { if (x < 0 || y < 0 || x >= img.w || y >= img.h) return null; const i = (y * img.w + x) * 4; return [img.d[i], img.d[i + 1], img.d[i + 2]]; };
        const hx = (p) => '#' + p.map((v) => v.toString(16).padStart(2, '0')).join('');
        const sat = (p) => Math.max(...p) - Math.min(...p);
        const top = (m, n) => [...m.entries()].sort((x, y) => y[1] - x[1]).slice(0, n).map(([k, v]) => `${k}:${v}`);
        const count = (m, k) => m.set(k, (m.get(k) ?? 0) + 1);
        const sum = (h) => parseInt(h.slice(1, 3), 16) + parseInt(h.slice(3, 5), 16) + parseInt(h.slice(5), 16);
        const rect = (bx) => ({ x0: Math.round((bx.l - clip.x) * s), y0: Math.round((bx.t - clip.y) * s), x1: Math.round((bx.l + bx.w - clip.x) * s), y1: Math.round((bx.t + bx.h - clip.y) * s) });
        const res = [];
        for (const bx of boxes) {
            const r = rect(bx.box);
            if (bx.kind === 'text') {
                const all = new Map(), saturated = new Map();
                for (let y = r.y0; y < r.y1; y++) for (let x = r.x0; x < r.x1; x++) { const p = at(A, x, y); if (!p) continue; count(all, hx(p)); if (sat(p) > 60) count(saturated, hx(p)); }
                res.push({ kind: 'text', text: bx.text, ground: top(all, 1)[0] ?? null, saturated: top(saturated, 3), darkest: [...all.keys()].sort((x, y) => sum(x) - sum(y))[0] ?? null });
                continue;
            }
            // The line: the outermost 3 device px inside each edge; the fill: 9 px in and more.
            const line = new Map(), fill = new Map();
            for (let y = r.y0; y < r.y1; y++) for (let x = r.x0; x < r.x1; x++) {
                const p = at(A, x, y); if (!p) continue;
                const d = Math.min(x - r.x0, r.x1 - 1 - x, y - r.y0, r.y1 - 1 - y);
                if (d < 3) count(line, hx(p)); else if (d >= 9) count(fill, hx(p));
            }
            const item = { kind: bx.kind, grid: bx.grid, covers: bx.covers, px: r, line: top(line, 3), fill: top(fill, 2) };
            if (bx.kind === 'dashes') {
                // Along each side, on each line 0 to 7 device px in: the runs of pixels near the dashes'
                // own colour (as the DOM computes it), on the line where most are found, and how far
                // along the side they reach (where the first and the last lie, of the side's length).
                const want = /^#[0-9a-f]{6}/.exec(bx.want ?? '')?.[0];
                const wv = want ? [1, 3, 5].map((i) => parseInt(want.slice(i, i + 2), 16)) : null;
                const near = (p) => p && wv && Math.abs(p[0] - wv[0]) + Math.abs(p[1] - wv[1]) + Math.abs(p[2] - wv[2]) < 90;
                const sides = {};
                for (const side of ['top', 'right', 'bottom', 'left']) {
                    const horizontal = side === 'top' || side === 'bottom';
                    const len = horizontal ? r.x1 - r.x0 : r.y1 - r.y0;
                    const px = (k, i) => {
                        if (side === 'top') return at(A, r.x0 + i, r.y0 + k);
                        if (side === 'bottom') return at(A, r.x0 + i, r.y1 - 1 - k);
                        if (side === 'left') return at(A, r.x0 + k, r.y0 + i);
                        return at(A, r.x1 - 1 - k, r.y0 + i);
                    };
                    let best = { k: -1, runs: 0, hits: 0, first: null, last: null };
                    for (let k = 0; k < 8; k++) {
                        let runs = 0, on = false, hits = 0, first = null, last = null;
                        for (let i = 0; i < len; i++) { const hit = near(px(k, i)); if (hit) { hits++; if (first === null) first = i; last = i; } if (hit && !on) runs++; on = hit; }
                        if (hits > best.hits) best = { k, runs, hits, first, last };
                    }
                    sides[side] = { colour: want ?? null, inset: best.k, runs: best.runs, pixels: best.hits, reach: best.first === null ? null : `${best.first}-${best.last} of ${len}` };
                }
                item.sides = sides;
                let changed = 0; for (let yy = r.y0; yy < r.y1; yy++) for (let x = r.x0; x < r.x1; x++) { const p = at(A, x, yy), q = at(B, x, yy); if (p && q && (p[0] !== q[0] || p[1] !== q[1] || p[2] !== q[2])) changed++; }
                item.changedIn300ms = changed;
            }
            res.push(item);
        }
        return { scale: s, boxes: res };
    }, { a: shot.toString('base64'), b: shot2.toString('base64'), clip, boxes });
}

// ---- The page ------------------------------------------------------------------------------------------

const browser = await chromium.launch({ channel: CHANNEL, headless: false, args: ['--window-position=40,40', '--window-size=1600,1050'] });
out.browserVersion = browser.version();
const context = await browser.newContext({ viewport: null });
const page = await context.newPage();
page.on('console', (m) => out.messages.push({ t: new Date().toISOString(), type: m.type(), text: m.text() }));
page.on('pageerror', (e) => out.messages.push({ t: new Date().toISOString(), type: 'pageerror', text: String(e) }));
await page.goto(`${BASE}${P.path}`);
const at = (address) => { const m = /^([A-Z]+)(\d+)$/.exec(address); let col = 0; for (const ch of m[1]) col = col * 26 + ch.charCodeAt(0) - 64; return { row: Number(m[2]) - 1, col: col - 1 }; };
const CFG = { sheet: P.sheet, positions: P.positions, other: P.other ?? null, refusal: P.refusal, targetCell: at(P.target) };
await page.waitForFunction(({ sheet, cell, text }) => {
    const g = document.querySelectorAll('.ex-grid:has(> .ex-formula-bar)')[sheet];
    const m = /^([A-Z]+)(\d+)$/.exec(cell); let col = 0; for (const ch of m[1]) col = col * 26 + ch.charCodeAt(0) - 64;
    const t = g && [...g.querySelectorAll(`[id$="-r${Number(m[2]) - 1}c${col - 1}"]`)].map((e) => e.textContent).join('');
    return t && t.includes(text);
}, P.ready, { timeout: 60_000 });
await page.bringToFront();
await page.evaluate((t) => { document.title = t; }, TITLE);
// The probe's own listener: where DOM focus goes, in order, between two readings.
await page.evaluate(() => {
    window.__focusLog = [];
    document.addEventListener('focusin', (e) => {
        const t = e.target; const g = t.closest?.('.ex-grid');
        const grid = !g ? 'page' : g.querySelector(':scope > .ex-formula-bar') ? `sheet${[...document.querySelectorAll('.ex-grid:has(> .ex-formula-bar)')].indexOf(g)}` : (g.parentElement?.id || 'grid');
        window.__focusLog.push({ t: Math.round(performance.now()), grid, el: `${t.tagName.toLowerCase()}.${typeof t.className === 'string' ? t.className.split(' ')[0] : ''}` });
    }, true);
});
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

// The clip every picture is taken of: the Sheet with its Formula Bar and the grids beside or beneath it.
const clip = await page.evaluate((cfg) => {
    const els = [document.querySelectorAll('.ex-grid:has(> .ex-formula-bar)')[cfg.sheet], document.querySelector(cfg.positions), cfg.other && document.querySelector(cfg.other)].filter(Boolean);
    const rs = els.map((e) => e.getBoundingClientRect());
    const l = Math.floor(Math.min(...rs.map((r) => r.left))) - 4, t = Math.floor(Math.min(...rs.map((r) => r.top))) - 4;
    return { x: l, y: t, width: Math.ceil(Math.max(...rs.map((r) => r.right))) + 4 - l, height: Math.ceil(Math.max(...rs.map((r) => r.bottom))) + 4 - t };
}, CFG);
out.clip = clip;

// Where things are, in screen pixels.
const gridSel = (which) => (which === 'positions' ? P.positions : which === 'other' ? P.other : null);
async function centreOf(which, address) {
    const r = await page.evaluate(([sel, sheetIdx, a, header]) => {
        const g = sel ? document.querySelector(sel) : document.querySelectorAll('.ex-grid:has(> .ex-formula-bar)')[sheetIdx];
        let e;
        if (header) e = [...g.querySelectorAll('.ex-header-cell')].find((h) => h.textContent.trim() === header);
        else e = [...g.querySelectorAll(`[id$="-r${a.row}c${a.col}"]`)][0];
        if (!e) return null;
        const b = e.getBoundingClientRect();
        return { x: (b.left + b.right) / 2, y: (b.top + b.bottom) / 2, box: [b.left, b.top, b.width, b.height] };
    }, [gridSel(which), P.sheet, address.startsWith('header:') ? null : at(address), address.startsWith('header:') ? address.slice(7) : null]);
    if (!r) throw new Error(`${which} ${address} is not painted`);
    return cal.at(r.x, r.y);
}
async function editorCentre() {
    const r = await page.evaluate((i) => { const g = document.querySelectorAll('.ex-grid:has(> .ex-formula-bar)')[i]; const e = g.querySelector('.ex-viewport input.ex-editor, .ex-viewport .ex-editor input'); if (!e) return null; const b = e.getBoundingClientRect(); return { x: b.left + Math.min(b.width - 6, 40), y: (b.top + b.bottom) / 2 }; }, P.sheet);
    if (!r) throw new Error('no Cell Editor');
    return cal.at(r.x, r.y);
}
async function until(fn, arg, ms = 5000, step = 50) {
    const t0 = Date.now();
    while (Date.now() - t0 < ms) { const v = await page.evaluate(fn, arg); if (v) return Date.now() - t0; await sleep(step); }
    return null;
}
const stateFn = (i) => { const g = document.querySelectorAll('.ex-grid:has(> .ex-formula-bar)')[i]; return { editing: !!g.querySelector('.ex-viewport input.ex-editor, .ex-viewport .ex-editor input'), completion: !!g.querySelector('.ex-completion'), sheetHasFocus: g.contains(document.activeElement), focus: g.getAttribute('aria-activedescendant') }; };
const state = () => page.evaluate(stateFn, P.sheet);

// No edit open: the keyboard brought back to the Sheet if it left, then Escape until the Sheet says
// no edit is open (a completion list takes the first).
async function reset() {
    let escapes = 0; let back = false;
    for (let i = 0; i < 6; i++) {
        const s = await state();
        if (!s.editing && !s.completion) break;
        if (s.editing && !s.sheetHasFocus) { const [x, y] = await editorCentre(); await input(`click ${x} ${y}`); back = true; await sleep(RTT ? 500 : 250); }
        await input('type 0 {ESC}'); escapes++; await sleep(RTT ? 500 : 250);
    }
    return { escapes, keyboardBroughtBack: back };
}
// The target cell has the Focus and no edit is open. A press on it while it has the Focus, soon
// after the last, would be a double-click and open an edit: it is pressed only when the Focus is
// elsewhere or the keyboard is not the Sheet's.
async function selectTarget() {
    const t = CFG.targetCell;
    const ready = () => page.evaluate(([i, t]) => {
        const g = document.querySelectorAll('.ex-grid:has(> .ex-formula-bar)')[i];
        const nb = g.querySelector('input.ex-name-box, .ex-name-box input');
        return new RegExp(`-r${t.row}c${t.col}$`).test(g.getAttribute('aria-activedescendant') ?? '') && g.contains(document.activeElement)
            && !g.querySelector('.ex-viewport input.ex-editor, .ex-viewport .ex-editor input') && nb?.value === (t.name);
    }, [P.sheet, { ...t, name: P.target }]);
    if (await ready()) return 0;
    const [x, y] = await centreOf('sheet', P.target);
    await input(`click ${x} ${y}`);
    const t0 = Date.now();
    while (Date.now() - t0 < 5000) { if (await ready()) return Date.now() - t0; await sleep(50); }
    throw new Error(`${P.target} did not take the Focus: ${JSON.stringify(await state())}`);
}
// The target cell holds nothing before a case: a case that entered something is cleared with Delete.
async function cleanTarget() {
    const r = await page.evaluate(READ, CFG);
    const barText = r.bar?.value ?? '';
    if (r.targetText === '' && barText === '') return '';
    await input('type 0 {DEL}');
    await until((i) => { const g = document.querySelectorAll('.ex-grid:has(> .ex-formula-bar)')[i]; return (g.querySelector('input.ex-formula-bar-text, .ex-formula-bar-text input')?.value ?? '') === ''; }, P.sheet, 5000);
    return `held ${JSON.stringify(barText)} (${JSON.stringify(r.targetText)}), cleared with Delete`;
}
// The layer of the surface the edit is in has caught up with its field (or there is none).
const settledFn = () => {
    const f = document.activeElement;
    if (!(f instanceof HTMLInputElement)) return true;
    const layer = f.previousElementSibling?.classList.contains('ex-reference-text') ? f.previousElementSibling : null;
    return !layer || layer.getAttribute('data-ex-text') === f.value;
};

async function doStep(st) {
    if (st.keys !== undefined) { await input(`type ${GAP_MS} ${st.keys}`); return `keys ${st.keys}`; }
    if (st.press) { const [x, y] = await centreOf(st.press[0], st.press[1]); await input(`click ${x} ${y}`); return `a press at (${x}, ${y}), the middle of ${st.press[0]} ${st.press[1]}`; }
    if (st.shiftPress) { const [x, y] = await centreOf(st.shiftPress[0], st.shiftPress[1]); await input(`shiftclick ${x} ${y}`); return `a Shift+press at (${x}, ${y}), the middle of ${st.shiftPress[0]} ${st.shiftPress[1]}`; }
    if (st.header) { const [x, y] = await centreOf(st.header[0], `header:${st.header[1]}`); await input(`click ${x} ${y}`); return `a press at (${x}, ${y}), the middle of ${st.header[0]}'s ${st.header[1]} header`; }
    if (st.drag) {
        const [x1, y1] = await centreOf(st.drag[0], st.drag[1]); const [x2, y2] = await centreOf(st.drag[0], st.drag[2]);
        await input(`drag ${x1} ${y1} ${x2} ${y2} 20`); return `a drag from (${x1}, ${y1}) to (${x2}, ${y2}), the middles of ${st.drag[0]} ${st.drag[1]} and ${st.drag[2]}, in 20 steps of 15 ms`;
    }
    if (st.hover) { const [x, y] = await centreOf(st.hover[0], st.hover[1]); await input(`move ${x} ${y}`); return `the pointer moved to (${x}, ${y}), the middle of ${st.hover[0]} ${st.hover[1]}, no press`; }
    if (st.intoEditor) { const [x, y] = await editorCentre(); await input(`click ${x} ${y}`); return `a press at (${x}, ${y}), inside the Sheet's Cell Editor`; }
    if (st.pressSheet) { await selectTarget(); return `a press on the Sheet's ${P.target}`; }
    if (st.wait) { await sleep(st.wait); return `a wait of ${st.wait} ms`; }
    if (st.waitPointed) {
        const ms = await until((sel) => document.querySelector(sel)?.classList.contains('ex-pointed-at'), P.positions, 3000, 5);
        return ms === null ? 'the positions grid was not pointed at within 3 s' : `the positions grid pointed at after ${ms} ms`;
    }
    throw new Error(`no step ${JSON.stringify(st)}`);
}

async function take(caseId, stateName, did, extra = {}) {
    const t0 = Date.now();
    const base = RTT ? 900 : 600;
    await sleep(base);
    let waited = base;
    let settled = await page.evaluate(settledFn);
    if (!settled) { const ms = await until(settledFn, null, 4000, 50); waited += ms ?? 4000; settled = ms !== null; if (settled) await sleep(150); }
    const read = await page.evaluate(READ, CFG);
    if (extra.cursor) {
        read.cursorUnderPointer = await page.evaluate(() => { const m = window.__move; if (!m) return null; const e = document.elementFromPoint(m.x, m.y); return { at: m, element: e ? `${e.tagName.toLowerCase()}.${typeof e.className === 'string' ? e.className.split(' ').join('.') : ''}` : null, cursor: e ? getComputedStyle(e).cursor : null }; });
        read.osCursor = await input('cursor');
    }
    const file = SHOTS ? path.join(SHOTS, `${PAGE}-${caseId}-${stateName}-${LABEL}-${CHANNEL}.png`) : null;
    const px = await pixels(page, read, clip, file);
    return { state: stateName, did, readAtMs: waited, settled, read, pixels: px, shot: file ? path.basename(file) : null, tookMs: Date.now() - t0 };
}

// The narrow view's set-ups, with the real wheel over the positions grid's body, which gives it no
// Focus: its first column in view (the horizontal wheel turned left 10 times); that, and the rows
// wheeled down 5 turns; or its last row and its last column (the wheel turned down 15 times, then the
// horizontal wheel right 10 times). The pointer then leaves the grid. Its scroll offsets and its
// headers are recorded.
async function scrollSetUp(rec, how) {
    const body = await page.evaluate((sel) => { const r = document.querySelector(sel).querySelector('.ex-scroller').getBoundingClientRect(); return { x: r.left + 100, y: r.top + 120 }; }, P.positions);
    const [bx, by] = cal.at(body.x, body.y);
    const did = [];
    const turn = async (delta, axis, n) => { await input(`wheel ${bx} ${by} ${delta} ${axis} ${n}`); did.push(`${n} turns of the ${axis === 'h' ? 'horizontal ' : ''}wheel, each ${delta}`); await sleep(500); };
    if (how === 'first-column' || how === 'first-column-down') await turn(-120, 'h', 10);
    if (how === 'first-column-down') await turn(-120, 'v', 5);
    if (how === 'last-row-column') { await turn(-120, 'v', 15); await turn(120, 'h', 10); }
    await sleep(500);
    const [px, py] = cal.at(clip.x + 10, clip.y + clip.height + 20);
    await input(`move ${px} ${py}`);
    rec.scrolled = `over (${bx}, ${by}) in the positions grid: ${did.join(', then ')}; the pointer then moved off it`;
    rec.scroller = await page.evaluate((sel) => { const sc = document.querySelector(sel).querySelector('.ex-scroller'); return { top: sc.scrollTop, left: sc.scrollLeft, clientWidth: sc.clientWidth, clientHeight: sc.clientHeight, scrollWidth: sc.scrollWidth, scrollHeight: sc.scrollHeight, offsetWidth: sc.offsetWidth, offsetHeight: sc.offsetHeight }; }, P.positions);
    rec.headers = await page.evaluate((sel) => [...document.querySelector(sel).querySelectorAll('.ex-header-cell')].map((h) => { const b = h.getBoundingClientRect(); return { name: h.textContent.trim(), left: +b.left.toFixed(2), width: +b.width.toFixed(2) }; }), P.positions);
}

async function runCase(c) {
    const rec = { case: c.id, what: c.what, started: new Date().toISOString(), states: [] };
    rec.before = await reset();
    if (c.setup) await scrollSetUp(rec, c.setup);
    rec.focusMs = await selectTarget();
    rec.targetBefore = await cleanTarget();
    await page.evaluate(() => { window.__focusLog = []; });
    for (const s of c.states) {
        if (s.until) {
            for (let n = 1; n <= 6; n++) {
                const did = []; for (const st of s.steps) did.push(await doStep(st));
                const r = await take(c.id, `${s.state}-${n}`, did);
                rec.states.push(r);
                const sel = r.read.completion?.find((o) => o.selected)?.text ?? '';
                if (sel === s.until || sel.startsWith(`${s.until} `)) break;
                if (!r.read.completion) { r.note = 'no list; no further Down is pressed'; break; }
            }
            continue;
        }
        const did = []; for (const st of s.steps) did.push(await doStep(st));
        rec.states.push(await take(c.id, s.state, did, { cursor: s.cursor }));
    }
    rec.after = await reset();
    rec.afterRead = await page.evaluate(READ, CFG);
    return rec;
}

try {
    const list = CASES[PAGE].filter((c) => (!ONLY && !c.extra) || ONLY?.includes(c.id));
    for (const c of list) {
        try { out.cases.push(await runCase(c)); } catch (e) { out.cases.push({ case: c.id, error: String(e) }); await reset().catch(() => {}); }
        save();
    }
} finally {
    if (CONTROL) await fetch(`${CONTROL}/?rtt=0`, { method: 'POST' }).catch(() => {});
    out.keyboard.after = await input(`layout ${TITLE}`).catch((e) => String(e));
    out.finished = new Date().toISOString();
    save();
    helper.stdin.write('quit\n');
    await browser.close();
}
console.log(JSON.stringify({ page: PAGE, label: LABEL, channel: CHANNEL, cases: out.cases.length, errors: out.cases.filter((c) => c.error).length, messages: out.messages.length, browser: out.browserVersion }));
