// docs/specs/exsheet/verify-on-windows-11.md, Part C: ExSheet beside Part A's Excel screenshots, case by
// case, on /sheet?case=<name> (SheetCases), with the real keyboard and mouse. After every step: the
// page's picture of the Sheet (and of Format Cells when it is open), the pixels across each edge the
// case names and along each line, the colours of the cells' text, and what the DOM holds (each named
// cell's text and computed style, the rows' heights, the Selection, Format Cells' tabs and controls).
// This is the fifteenth run's probe (verification/2026-10-01-windows-15/ime-probe.mjs, on
// claude/exsheet-windows-verify-15) with this run's pages and cases; its input helper, its calibration
// of screen pixels against the page's own mousemove, and its pictures are unchanged.
//
//   CASES=1,4,9         only these cases (all when unset)
//   CHROME=builtin|mud  ?chrome=mud adds ExSheet.MudBlazor's Chrome
//   SCHEME=light|dark   &scheme=dark adds the dark scheme
//   ZOOM=100|150        the browser's zoom, set with real keys (Ctrl+= three times: 110, 125, 150)
//
// Run on Windows from a folder with Playwright's node_modules, headed, the window at the display's own
// scale (viewport: null):
//
//     set EXGRID_CHANNEL=chrome & set LABEL=wasm & set INPUT=...\input-server.ps1 & set OUT=...\x.json
//     set SHOTS=... & node beside-excel-probe.mjs http://localhost:5299
//
// Every key and click goes through input-server.ps1 (real OS input: SendInput, a virtual-key and a scan
// code per key). Playwright opens each page, reads the DOM and takes the page's pictures; it sends no
// input. Pixels are read from the picture at the device's own scale, so offsets are device pixels.
import { chromium } from '@playwright/test';
import { spawn } from 'node:child_process';
import readline from 'node:readline';
import zlib from 'node:zlib';
import fs from 'node:fs';
import path from 'node:path';

const BASE = process.argv[2] ?? 'http://localhost:5299';
const CHANNEL = process.env.EXGRID_CHANNEL ?? 'chrome';
const LABEL = process.env.LABEL ?? 'wasm';
const CHROME = process.env.CHROME ?? 'builtin';
const SCHEME = process.env.SCHEME ?? 'light';
const ZOOM = Number(process.env.ZOOM ?? 100);
const OUT = process.env.OUT;
const SHOTS = process.env.SHOTS;
const ONLY = process.env.CASES ? process.env.CASES.split(',') : null;
const CONFIG = `${LABEL}-${CHANNEL}${CHROME === 'mud' ? '-mud' : ''}${SCHEME === 'dark' ? '-dark' : ''}${ZOOM !== 100 ? `-z${ZOOM}` : ''}`;
const TITLE = `beside11c-${CONFIG}`;
const out = { channel: CHANNEL, base: BASE, label: LABEL, chrome: CHROME, scheme: SCHEME, zoom: ZOOM, config: CONFIG, started: new Date().toISOString(), cases: [], messages: [] };
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

// ---- Pictures and pixels ------------------------------------------------------------------------------

/** Decodes the PNG a Chromium screenshot writes (pixels.mjs's decoder): 8-bit RGB or RGBA. */
function decodePng(buffer) {
    let offset = 8, width = 0, height = 0, colourType = 0;
    const data = [];
    while (offset < buffer.length) {
        const length = buffer.readUInt32BE(offset);
        const type = buffer.toString('ascii', offset + 4, offset + 8);
        const chunk = buffer.subarray(offset + 8, offset + 8 + length);
        if (type === 'IHDR') { width = chunk.readUInt32BE(0); height = chunk.readUInt32BE(4); colourType = chunk[9]; }
        else if (type === 'IDAT') data.push(chunk);
        offset += 12 + length;
    }
    const channels = colourType === 6 ? 4 : 3;
    const raw = zlib.inflateSync(Buffer.concat(data));
    const stride = width * channels;
    const px = Buffer.alloc(height * stride);
    for (let y = 0; y < height; y++) {
        const filter = raw[y * (stride + 1)];
        for (let x = 0; x < stride; x++) {
            const a = x >= channels ? px[y * stride + x - channels] : 0;
            const b = y > 0 ? px[(y - 1) * stride + x] : 0;
            const c = x >= channels && y > 0 ? px[(y - 1) * stride + x - channels] : 0;
            let v = raw[y * (stride + 1) + 1 + x];
            if (filter === 1) v += a; else if (filter === 2) v += b; else if (filter === 3) v += Math.floor((a + b) / 2);
            else if (filter === 4) { const p = a + b - c; const pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c); v += pa <= pb && pa <= pc ? a : pb <= pc ? b : c; }
            px[y * stride + x] = v & 0xff;
        }
    }
    return { width, height, at: (x, y) => (x < 0 || y < 0 || x >= width || y >= height ? null : [px[y * stride + x * channels], px[y * stride + x * channels + 1], px[y * stride + x * channels + 2]]) };
}
const hex = (p) => (p ? p.map((v) => v.toString(16).padStart(2, '0')).join('') : 'none');
const lum = ([r, g, b]) => 0.299 * r + 0.587 * g + 0.114 * b;
const dist = (a, b) => Math.max(Math.abs(a[0] - b[0]), Math.abs(a[1] - b[1]), Math.abs(a[2] - b[2]));
function runsOf(list, from) {
    const outRuns = []; let i = 0;
    while (i < list.length) { let j = i; while (j + 1 < list.length && list[j + 1] === list[i]) j++; outRuns.push(i === j ? `${from + i} ${list[i]}` : `${from + i}..${from + j} ${list[i]}`); i = j + 1; }
    return outRuns.join(' | ');
}
// The pixels across one edge of a cell, from 6 device pixels before the edge to 6 after it, at a
// quarter, a half and three quarters along it, as runs of offset and colour. Offset 0 is the first
// device pixel past the bottom (right) edge, so -1 is the cell's last, the gridline the cell holds; for
// a top (left) edge, 0 is the cell's first pixel and -1 the one before it (layer 3's line-pixels.mjs).
function acrossEdge(img, clip, scale, box, side) {
    const horizontal = side === 'top' || side === 'bottom';
    const edge = side === 'bottom' ? box.y + box.h : side === 'right' ? box.x + box.w : side === 'top' ? box.y : box.x;
    const e = Math.round((edge - (horizontal ? clip.y : clip.x)) * scale);
    const o = { at: e };
    const seen = new Set();
    for (const f of [0.25, 0.5, 0.75]) {
        const along = horizontal ? Math.floor((box.x + box.w * f - clip.x) * scale) : Math.floor((box.y + box.h * f - clip.y) * scale);
        const list = [];
        for (let d = -6; d <= 6; d++) list.push(hex(horizontal ? img.at(along, e + d) : img.at(e + d, along)));
        o[`at${f * 100}`] = runsOf(list, -6);
        seen.add(o[`at${f * 100}`]);
    }
    o.same = seen.size === 1;
    return o;
}
// A line along a cell's bottom (right) edge: for each device row (column) from -4 to +3 that has a dark
// pixel (luminance below 128), its pattern over the cell's length less 2 px at each end, its runs (+n on,
// -n off) and its colours.
function alongEdge(img, clip, scale, box, side) {
    const horizontal = side === 'bottom' || side === 'top';
    const edge = side === 'bottom' ? box.y + box.h : side === 'right' ? box.x + box.w : side === 'top' ? box.y : box.x;
    const e = Math.round((edge - (horizontal ? clip.y : clip.x)) * scale);
    const start = Math.ceil(((horizontal ? box.x : box.y) - (horizontal ? clip.x : clip.y)) * scale) + 2;
    const len = Math.floor((horizontal ? box.w : box.h) * scale) - 4;
    const rows = {};
    for (let d = -4; d <= 3; d++) {
        const ps = []; for (let i = 0; i < len; i++) ps.push(horizontal ? img.at(start + i, e + d) : img.at(e + d, start + i));
        const marks = ps.map((p) => (p && lum(p) < 128 ? '#' : '.')).join('');
        if (!marks.includes('#')) continue;
        const runs = []; let i = 0;
        while (i < marks.length) { let j = i; while (j + 1 < marks.length && marks[j + 1] === marks[i]) j++; runs.push(`${marks[i] === '#' ? '+' : '-'}${j - i + 1}`); i = j + 1; }
        const counts = new Map(); for (const p of ps) { const k = hex(p); counts.set(k, (counts.get(k) ?? 0) + 1); }
        rows[d] = { runs: runs.join(' '), colours: [...counts.entries()].sort((a, b) => b[1] - a[1]).slice(0, 4).map(([k, v]) => `${k}:${v}`) };
    }
    return { at: e, from: start, length: len, rows };
}
// A cell's text as painted: its ground (the most frequent pixel 3 device px in from its edges) and the
// colours further than 24 from it, most frequent first; the first is the strokes' core.
function inkOf(img, clip, scale, box) {
    const x0 = Math.ceil((box.x - clip.x) * scale) + 3, y0 = Math.ceil((box.y - clip.y) * scale) + 3;
    const x1 = Math.floor((box.x + box.w - clip.x) * scale) - 3, y1 = Math.floor((box.y + box.h - clip.y) * scale) - 3;
    const all = new Map();
    for (let y = y0; y < y1; y++) for (let x = x0; x < x1; x++) { const p = img.at(x, y); if (p) { const k = hex(p); all.set(k, (all.get(k) ?? 0) + 1); } }
    const ranked = [...all.entries()].sort((a, b) => b[1] - a[1]);
    if (!ranked.length) return null;
    const ground = ranked[0][0];
    const g = [0, 2, 4].map((i) => parseInt(ground.slice(i, i + 2), 16));
    const ink = ranked.filter(([k]) => dist([0, 2, 4].map((i) => parseInt(k.slice(i, i + 2), 16)), g) > 24);
    return { ground, sampled: ink[0]?.[0] ?? 'no text', top: ink.slice(0, 5).map(([k, v]) => `${k}:${v}`), inkPixels: ink.reduce((s, [, v]) => s + v, 0) };
}

// ---- The cases ----------------------------------------------------------------------------------------

// A step: keys in SendKeys' notation ({keys}), Ctrl with a character's key ({ctrl}), a press on a cell
// ({press}), a drag with Ctrl held from one cell to another ({ctrlDrag}), a press on a control of Format
// Cells by its role and name ({dlg: {role, name}}), text typed into a field of Format Cells after a press
// on it and Ctrl+A ({dlgType: {selector, text}}), the case's page opened again ({reload}), or a wait.
// A state is steps, then a reading.
const K = (keys) => ({ keys });
const C = (ch) => ({ ctrl: ch });
const DLG = (role, name) => ({ dlg: { role, name } });
// The Selection moved to F10, away from the cells a case reads. Part A's park began with Esc, which
// ended Excel's copy marquee; here Esc is left out, because with no edit open it sends the keyboard
// from the Sheet to the page (the fifteenth run saw it; a trial of this run did too).
// Part A parked at G14; the Sheet here shows fewer rows than Excel did, and G14 would scroll it by part
// of a row, which puts row 2's top under the Column Headings. F10 is in view in every case.
const PARK = K('^{HOME}{DOWN 9}{RIGHT 5}');
const edgesOf = (cells, sides = ['top', 'bottom', 'left', 'right']) => cells.flatMap((c) => sides.map((s) => `${c} ${s}`));
const range = (from, to) => {
    const a = at(from), b = at(to); const o = [];
    for (let r = a.row; r <= b.row; r++) for (let c = a.col; c <= b.col; c++) o.push(`${letters(c)}${r + 1}`);
    return o;
};
const STYLES9 = ['Hair', 'Thin', 'Medium', 'Thick', 'Double', 'Dotted', 'Dashed', 'DashDot', 'DashDotDot', 'MediumDashed', 'MediumDashDot', 'MediumDashDotDot', 'SlantedDashDot'];
const B9 = STYLES9.map((s, i) => `B${i + 2}`);
const KEYS16 = [['ctrl-b', '^{KEYB}'], ['ctrl-2', '^{KEY2}'], ['ctrl-i', '^{KEYI}'], ['ctrl-3', '^{KEY3}'], ['ctrl-u', '^{KEYU}'], ['ctrl-4', '^{KEY4}'], ['ctrl-5', '^{KEY5}']];
const KEYS18 = [['tilde', '~'], ['bang', '!'], ['at', '@'], ['hash', '#'], ['dollar', '$'], ['percent', '%'], ['caret', '^']];
const FROM_A1 = { press: 'A1' };

const CASES = [
    { id: '1', page: '1', what: 'Rows 1-8: -5 and 5 in [Black]0 ... [Yellow]0; [White]\'s row filled black', cells: range('A1', 'B8'), states: [{ state: 'set', steps: [] }] },
    { id: '2', page: '2', what: 'A1 = -5, B1 = 5 in 0;[Red]-0, Font blue', cells: ['A1', 'B1'], states: [{ state: 'set', steps: [] }] },
    { id: '3b', page: '3b', what: '[Red]0 on abc, TRUE, =1/0; 0;[Red]@ on 5', cells: ['A1', 'A2', 'A3', 'A4'], states: [{ state: 'set', steps: [] }] },
    { id: '3c', page: '3c', what: '-123456789 in 0;[Red]-0, column A narrowed until it shows ####', cells: ['A1'], states: [{ state: 'set', steps: [] }] },
    { id: '4', page: '4', what: 'B2 filled yellow', cells: ['B2', 'D4'], edges: [...edgesOf(['B2']), 'D4 bottom', 'D4 right'], states: [{ state: 'set', steps: [] }] },
    { id: '5', page: '5', what: 'B2 filled white', cells: ['B2', 'D4'], edges: [...edgesOf(['B2']), 'D4 bottom', 'D4 right'], states: [{ state: 'set', steps: [] }] },
    { id: '6', page: '6', what: 'B2 and C2 filled yellow', cells: ['B2', 'C2'], edges: ['B2 right', 'B2 bottom', 'C2 bottom', 'B2 top', 'C2 right', 'B2 left'], states: [{ state: 'set', steps: [] }] },
    { id: '7', page: '7', what: 'The page sets B2\'s right thick red, the case\'s first setting. On B2, Format Cells (Ctrl+1) and its Border tab are opened to read what they show, then Cancel. On C2, as Part A set it by COM, with Format Cells: the Border tab, Thin, More Colours #0000FF, Left, OK', cells: ['B2', 'C2'], edges: ['B2 right', 'C2 left', 'B2 bottom'], states: [
        { state: 'set', steps: [] },
        { state: 'b2-selected', steps: [FROM_A1, K('{DOWN}{RIGHT}')] },
        { state: 'b2-opened', steps: [K('^{KEY1}')] },
        { state: 'b2-border-tab', steps: [DLG('tab', 'Border')] },
        { state: 'b2-cancelled', steps: [DLG('button', 'Cancel')] },
        { state: 'c2-selected', steps: [K('{RIGHT}')] },
        { state: 'c2-opened', steps: [K('^{KEY1}')] },
        { state: 'c2-border-tab', steps: [DLG('tab', 'Border')] },
        { state: 'c2-chosen', steps: [DLG('radio', 'Thin'), { dlgType: { selector: '.ex-format-cells-hex[data-target=Border], .ex-format-cells-hex', text: '#0000FF' } }, DLG('button', 'Left')] },
        { state: 'c2-ok', steps: [DLG('button', 'OK')] },
        { state: 'parked', steps: [PARK] },
    ] },
    { id: '7x', page: '13', extra: true, what: 'An addition: case 7 with both settings made by Format Cells, on ?case=13 (nothing set up). On B2: Ctrl+1, the Border tab, Thick, Red, Right, OK. On C2: Ctrl+1, the Border tab, Thin, More Colours #0000FF, Left, OK. Each read with the Selection parked at G14 as well', cells: ['B2', 'C2'], edges: ['B2 right', 'C2 left', 'B2 bottom'], states: [
        { state: 'b2-selected', steps: [FROM_A1, K('{DOWN}{RIGHT}')] },
        { state: 'b2-chosen', steps: [K('^{KEY1}'), DLG('tab', 'Border'), DLG('radio', 'Thick'), DLG('radio', 'Red'), DLG('button', 'Right')] },
        { state: 'b2-ok', steps: [DLG('button', 'OK')] },
        { state: 'b2-parked', steps: [PARK] },
        { state: 'c2-selected', steps: [K('^{HOME}{DOWN}{RIGHT}{RIGHT}')] },
        { state: 'c2-border-tab', steps: [K('^{KEY1}'), DLG('tab', 'Border')] },
        { state: 'c2-chosen', steps: [DLG('radio', 'Thin'), { dlgType: { selector: '.ex-format-cells-hex[data-target=Border], .ex-format-cells-hex', text: '#0000FF' } }, DLG('button', 'Left')] },
        { state: 'c2-ok', steps: [DLG('button', 'OK')] },
        { state: 'c2-parked', steps: [PARK] },
    ] },
    { id: '8', page: '8', what: 'B2\'s bottom thick black', cells: ['B2', 'B3'], edges: ['B2 bottom', 'A2 bottom', 'C2 bottom'], rows: true, states: [{ state: 'set', steps: [] }] },
    { id: '9', page: '9', what: 'B2:B14\'s bottoms in Excel\'s thirteen styles', cells: [], edges: [...B9.map((c) => `${c} bottom`), 'A2 bottom', 'C2 bottom'], along: B9.map((c) => `${c} bottom`), rows: true, states: [
        { state: 'set', steps: [] },
        // The Sheet shows fewer rows than Excel did: the Focus taken to A16 scrolls B13 and B14 into view.
        { state: 'scrolled', steps: [FROM_A1, K('{DOWN 15}')] },
    ] },
    { id: '10', page: '10', what: 'B2\'s bottom thick black; B3 filled yellow', cells: ['B2', 'B3'], edges: ['B2 bottom', 'B3 bottom', 'B3 right', 'A2 bottom'], states: [{ state: 'set', steps: [] }] },
    { id: '11', page: '11', what: 'B2:C3 with every edge thin black; B2:C3 selected with real keys from A1 (Down, Right, Shift+Right, Shift+Down)', cells: ['B2', 'C2', 'B3', 'C3'], edges: [...edgesOf(['B2', 'C2', 'B3', 'C3']), 'A2 right', 'B1 bottom', 'D2 left', 'B4 top'], states: [
        { state: 'set', steps: [] },
        { state: 'a1', steps: [FROM_A1] },
        { state: 'selected', steps: [K('{DOWN}{RIGHT}+{RIGHT}+{DOWN}')] },
    ] },
    { id: '12', page: '12', what: 'B2 filled yellow, its top thin and its bottom thick. Row 3 selected with real keys (A3, Shift+Space) and a row inserted (Ctrl+Shift+=)', cells: ['B2', 'B3', 'B4', 'A3', 'C3'], edges: ['B2 top', 'B2 bottom', 'B3 bottom', 'B3 left', 'B3 right', 'B4 bottom', 'A2 bottom', 'C2 bottom'], states: [
        { state: 'set', steps: [] },
        { state: 'row3-selected', steps: [FROM_A1, K('{DOWN}{DOWN}+{SPACE}')] },
        { state: 'inserted', steps: [C('+')] },
        { state: 'parked', steps: [PARK] },
    ] },
    { id: '12x', page: '12', extra: true, what: 'An addition: case 12 with ExSheet\'s own insert, the Context Menu\'s "Insert rows above" (ADR-0071: insert is a Consumer command there, not a key). Row 3 selected with real keys (A3, Shift+Space), a right click on A3, the menu item clicked', cells: ['B2', 'B3', 'B4', 'A3', 'C3'], edges: ['B2 top', 'B2 bottom', 'B3 bottom', 'B3 left', 'B3 right', 'B4 bottom', 'A2 bottom', 'C2 bottom'], states: [
        { state: 'row3-selected', steps: [FROM_A1, K('{DOWN}{DOWN}+{SPACE}')] },
        { state: 'menu', steps: [{ rpress: 'A3' }] },
        { state: 'inserted', steps: [{ menu: 'Insert rows above' }] },
        { state: 'parked', steps: [PARK] },
    ] },
    { id: '13', page: '13', what: 'B2:D4 selected with real keys from A1; Ctrl+Shift+& then Ctrl+Shift+_ (the characters on the UK layout). Each read at once and with the Selection parked at G14 (keys), then B2:D4 selected again', cells: [], edges: [...edgesOf(range('B2', 'D4')), 'A2 right', 'A3 right', 'A4 right', 'E2 left', 'B1 bottom', 'B5 top'], states: [
        { state: 'selected', steps: [FROM_A1, K('{DOWN}{RIGHT}+{RIGHT}+{RIGHT}+{DOWN}+{DOWN}')] },
        { state: 'amp', steps: [C('&')] },
        { state: 'amp-parked', steps: [PARK] },
        { state: 'reselected', steps: [K('^{HOME}{DOWN}{RIGHT}+{RIGHT}+{RIGHT}+{DOWN}+{DOWN}')] },
        { state: 'underscore', steps: [C('_')] },
        { state: 'underscore-parked', steps: [PARK] },
    ] },
    { id: '14', page: '14', what: 'B2:C3 selected with real keys from A1, then E5:F6 by a drag with Ctrl held; Ctrl+Shift+&', cells: [], edges: [...edgesOf(['B2', 'C3', 'E5', 'F6']), 'D2 left', 'D5 right', 'B4 top', 'E7 top', 'D4 bottom'], states: [
        { state: 'selected', steps: [FROM_A1, K('{DOWN}{RIGHT}+{RIGHT}+{DOWN}'), { ctrlDrag: ['E5', 'F6'] }] },
        { state: 'amp', steps: [C('&')] },
        { state: 'parked', steps: [PARK] },
    ] },
    { id: '12-1', page: '12-1', what: 'The twelfth run\'s case 1: E5\'s thick red right pasted over B2 while C2 records a thin blue left', cells: ['B2', 'E5'], edges: ['B2 right', 'C2 left', 'E5 right'], states: [{ state: 'set', steps: [] }, { state: 'parked', steps: [FROM_A1, PARK] }] },
    { id: '12-14', page: '12-14', what: 'The twelfth run\'s case 14: an outline on row 3, A3\'s left recorded', cells: ['A3', 'A5'], edges: ['A3 left', 'A5 left', 'A3 top', 'A3 bottom', 'B3 top', 'B3 bottom', 'B3 right'], states: [{ state: 'set', steps: [] }, { state: 'parked', steps: [FROM_A1, PARK] }] },
    { id: '16', page: '16', what: 'A1 = abc, A2 = def. On A1, each of Ctrl+B, Ctrl+2, Ctrl+I, Ctrl+3, Ctrl+U, Ctrl+4 and Ctrl+5: pressed twice (as Part A), then Ctrl+Z twice (its undo)', cells: ['A1', 'A2'], states: [
        { state: 'a1', steps: [FROM_A1] },
        ...KEYS16.flatMap(([n, k]) => [
            { state: `${n}-1`, steps: [K(k)] }, { state: `${n}-2`, steps: [K(k)] },
            { state: `${n}-undo-1`, steps: [K('^{KEYZ}')] }, { state: `${n}-undo-2`, steps: [K('^{KEYZ}')] }]),
    ] },
    { id: '17', page: '17', what: 'A1 = abc (bold), A2 = def. A1:A2 with the Focus on A1 (A1, Shift+Down), Ctrl+B. Then the page opened again, and A2:A1 with the Focus on A2 (A1, Down, Shift+Up), Ctrl+B', cells: ['A1', 'A2'], states: [
        { state: 'focus-a1', steps: [FROM_A1, K('+{DOWN}')] },
        { state: 'focus-a1-ctrl-b', steps: [K('^{KEYB}')] },
        { state: 'reopened', steps: [{ reload: true }] },
        { state: 'focus-a2', steps: [FROM_A1, K('{DOWN}+{UP}')] },
        { state: 'focus-a2-ctrl-b', steps: [K('^{KEYB}')] },
    ] },
    { id: '18', page: '18', what: 'A1 = 1234.5. On A1, Ctrl with each of ~ ! @ # $ % ^ as the character on the UK layout (with the Shift it needs), each followed by Ctrl+Z (its undo)', cells: ['A1'], columns: true, states: [
        { state: 'a1', steps: [FROM_A1] },
        ...KEYS18.flatMap(([n, ch]) => [{ state: n, steps: [C(ch)] }, { state: `${n}-undo`, steps: [K('^{KEYZ}')] }]),
    ] },
    { id: '22', page: null, what: '/sheet: Ctrl+1 on A1; each tab pressed in the order shown; Esc; Ctrl+1 again; Esc', cells: ['A1'], tabs: true, states: [
        { state: 'a1', steps: [FROM_A1] },
        { state: 'opened', steps: [K('^{KEY1}')] },
        { state: 'tabs', tabsVisited: true, steps: [] },
        { state: 'closed', steps: [K('{ESC}')] },
        { state: 'second', steps: [K('^{KEY1}')] },
        { state: 'second-closed', steps: [K('{ESC}')] },
    ] },
    { id: '24', page: '16', what: '/sheet?case=16 (A1 abc, A2 def). Set up with ExSheet\'s own keys and Format Cells: A1 Ctrl+B; Ctrl+1, Fill, Red, OK; Ctrl+1, Border, Thick, Bottom, OK. Then A1:A2 with the Focus on A1 (Shift+Down), Ctrl+1, the Font, Fill and Border tabs pressed; Esc', cells: ['A1', 'A2'], edges: ['A1 bottom'], states: [
        { state: 'a1-bold', steps: [FROM_A1, K('^{KEYB}')] },
        { state: 'a1-fill-chosen', steps: [K('^{KEY1}'), DLG('tab', 'Fill'), DLG('radio', 'Red')] },
        { state: 'a1-filled', steps: [DLG('button', 'OK')] },
        { state: 'a1-border-chosen', steps: [K('^{KEY1}'), DLG('tab', 'Border'), DLG('radio', 'Thick'), DLG('button', 'Bottom')] },
        { state: 'a1-bordered', steps: [DLG('button', 'OK')] },
        { state: 'selected', steps: [K('+{DOWN}')] },
        { state: 'opened', steps: [K('^{KEY1}')] },
        { state: 'tab-font', steps: [DLG('tab', 'Font')] },
        { state: 'tab-fill', steps: [DLG('tab', 'Fill')] },
        { state: 'tab-border', steps: [DLG('tab', 'Border')] },
        { state: 'closed', steps: [K('{ESC}')] },
    ] },
    { id: '25', page: '16', what: '/sheet?case=16 (A1 abc, A2 def). A1 Ctrl+B, A2 Ctrl+I (keys). Then A1:A2 (A1, Shift+Down), Ctrl+1, the Font tab, Red, OK', cells: ['A1', 'A2'], states: [
        { state: 'set', steps: [FROM_A1, K('^{KEYB}{DOWN}^{KEYI}{UP}')] },
        { state: 'selected', steps: [K('+{DOWN}')] },
        { state: 'opened', steps: [K('^{KEY1}')] },
        { state: 'font-red', steps: [DLG('tab', 'Font'), DLG('radio', 'Red')] },
        { state: 'ok', steps: [DLG('button', 'OK')] },
    ] },
];

// ---- The page ------------------------------------------------------------------------------------------

function at(address) { const m = /^([A-Z]+)(\d+)$/.exec(address); let col = 0; for (const ch of m[1]) col = col * 26 + ch.charCodeAt(0) - 64; return { row: Number(m[2]) - 1, col: col - 1 }; }
function letters(c) { let s = ''; c += 1; while (c > 0) { const m = (c - 1) % 26; s = String.fromCharCode(65 + m) + s; c = Math.floor((c - 1) / 26); } return s; }
const cellSel = (address) => { const a = at(address); return `[id$="-r${a.row}c${a.col}"]`; };
const query = (name) => [name ? `case=${name}` : null, CHROME === 'mud' ? 'chrome=mud' : null, SCHEME === 'dark' ? 'scheme=dark' : null].filter(Boolean).join('&');
const urlOf = (name) => { const q = query(name); return `${BASE}/sheet${q ? `?${q}` : ''}`; };

const browser = await chromium.launch({ channel: CHANNEL, headless: false, args: ['--window-position=40,20', '--window-size=1700,1360'] });
out.browserVersion = browser.version();
const context = await browser.newContext({ viewport: null });
const page = await context.newPage();
const cdp = await context.newCDPSession(page);
page.on('console', (m) => out.messages.push({ t: new Date().toISOString(), type: m.type(), text: m.text() }));
page.on('pageerror', (e) => out.messages.push({ t: new Date().toISOString(), type: 'pageerror', text: String(e) }));

const SHEET = '.ex-grid:has(> .ex-formula-bar)';
async function open(name) {
    const url = urlOf(name);
    await page.goto(url);
    await page.waitForFunction((sel) => document.querySelector(sel)?.querySelector('.ex-row') && document.querySelector('#sheet-positions .ex-row'), SHEET, { timeout: 60_000 });
    if (!name) {
        // /sheet's own Sheet: its Linked Table's first push lands 1.5 s after it opens (B12 shows 318.25).
        await page.waitForFunction((sel) => [...document.querySelector(sel).querySelectorAll('[id$="-r11c1"]')].some((e) => e.textContent.includes('318.25')), SHEET, { timeout: 20_000 });
    }
    await sleep(1800);
    await page.evaluate((t) => { document.title = t; }, TITLE);
    await page.evaluate(() => { window.__move = null; document.addEventListener('mousemove', (e) => { window.__move = { x: e.clientX, y: e.clientY }; }, true); });
    // Screen pixels are found again on every page: a bar the browser shows above the page part-way
    // through a run moves the page down on the screen (a trial of this run: clicks meant for Format
    // Cells' tabs landed on the Column Headings).
    // The browser's zoom is the run's: a key the page let through can have zoomed it (Ctrl+Shift+=,
    // case 12, is the browser's Ctrl++ when the page does not take it), and the browser keeps a zoom per
    // site. If it is not the run's, Ctrl+0 and, for 150%, Ctrl+= three times set it again, recorded.
    if (out.baseDpr) {
        const want = out.baseDpr * ZOOM / 100;
        const dpr = await page.evaluate(() => devicePixelRatio);
        if (Math.abs(dpr - want) > 0.01) {
            await input(`front ${TITLE}`);
            await input('type 40 ^{KEY0}'); await sleep(600);
            for (let i = 0; ZOOM !== 100 && i < 3; i++) { await input('ctrlchar ='); await sleep(600); }
            out.zoomResets = [...(out.zoomResets ?? []), { url, found: dpr, now: await page.evaluate(() => devicePixelRatio) }];
        }
    }
    if (cal) out.calibrations = [...(out.calibrations ?? []), { url, ...(await calibrate()) }];
    return url;
}

let cal = null;
async function calibrate() {
    const g = await page.evaluate(() => ({ sx: window.screenX, sy: window.screenY, ow: window.outerWidth, oh: window.outerHeight, iw: window.innerWidth, ih: window.innerHeight, dpr: window.devicePixelRatio }));
    const border = Math.max(0, (g.ow - g.iw) / 2);
    const target = { x: 300, y: 8 };
    let ox = (g.sx + border) * g.dpr, oy = (g.sy + (g.oh - g.ih) - border) * g.dpr;
    const tries = [];
    for (let i = 0; i < 6; i++) {
        await page.evaluate(() => { window.__move = null; });
        const px = Math.round(ox + target.x * g.dpr), py = Math.round(oy + target.y * g.dpr);
        await input(`move ${px} ${py}`);
        await sleep(200);
        const m = await page.evaluate(() => window.__move);
        tries.push({ px, py, client: m });
        if (!m) { oy += 20; continue; }
        const dx = target.x - m.x, dy = target.y - m.y;
        if (Math.abs(dx) < 1 && Math.abs(dy) < 1) break;
        ox += dx * g.dpr; oy += dy * g.dpr;
    }
    cal = { dpr: g.dpr, tries, at: (x, y) => [Math.round(ox + x * g.dpr), Math.round(oy + y * g.dpr)] };
    return { dpr: g.dpr, tries };
}
// The pointer, off the Sheet: the page's top-left corner, above the Sheet.
async function park() { const [x, y] = cal.at(4, 4); await input(`move ${x} ${y}`); }

// Format Cells: inside the Sheet under the built-in Chrome, a MudDialog at page level under the Mud one.
const dialog = () => (CHROME === 'mud' ? page.locator('.mud-dialog.mud-ex-sheet-format-cells') : page.locator(`${SHEET} .ex-popover-consumer`));

// A control of Format Cells brought into view as a user brings it: Format Cells is drawn inside the
// Sheet's box (ADR-0050 item 16) and scrolls when its tab is taller than that box, so the wheel is
// turned over it, a notch at a time, until the control's middle is inside the part that shows.
async function intoView(loc, what) {
    const notches = [];
    for (let i = 0; i < 30; i++) {
        const v = await loc.evaluate((el) => {
            let c = el.parentElement;
            while (c && !(c.scrollHeight > c.clientHeight + 1 && /(auto|scroll)/.test(getComputedStyle(c).overflowY))) c = c.parentElement;
            const r = el.getBoundingClientRect(); const mid = r.top + r.height / 2;
            if (!c) return { inside: mid > 0 && mid < innerHeight, below: mid >= innerHeight };
            const cr = c.getBoundingClientRect();
            return { inside: mid > cr.top + 2 && mid < cr.bottom - 2, below: mid >= cr.bottom - 2, box: { x: cr.left + cr.width / 2, y: cr.top + cr.height / 2 } };
        });
        if (v.inside) return notches.length ? `the wheel turned ${notches.length} notch${notches.length > 1 ? 'es' : ''} ${notches[0] < 0 ? 'down' : 'up'} over Format Cells to show ${what}; ` : '';
        if (!v.box) throw new Error(`${what} is outside the window and nothing scrolls it`);
        const [x, y] = cal.at(v.box.x, v.box.y);
        const delta = v.below ? -120 : 120;
        await input(`wheel ${x} ${y} ${delta} v 1`);
        notches.push(delta);
        await sleep(200);
    }
    throw new Error(`${what} did not come into view`);
}
async function clickBox(box, what) {
    if (!box) throw new Error(`${what} has no box`);
    const [x, y] = cal.at(box.x + box.width / 2, box.y + box.height / 2);
    await input(`click ${x} ${y}`);
    return `a click at (${x}, ${y}) on ${what}`;
}
async function doStep(st, c) {
    if (st.keys !== undefined) {
        await input(`type 40 ${st.keys}`);
        // Format Cells opening: MudBlazor's dialog moves into place after it appears, so nothing is read
        // or pressed in it for 0.9 s.
        if (st.keys.includes('^{KEY1}')) await sleep(900);
        return `keys ${st.keys}`;
    }
    if (st.ctrl !== undefined) { await input(`ctrlchar ${st.ctrl}`); return `Ctrl with '${st.ctrl}' as the character on the UK layout`; }
    if (st.press) {
        const b = await page.locator(`${SHEET} ${cellSel(st.press)}`).first().boundingBox();
        const d = await clickBox(b, `the middle of ${st.press}`);
        // The press is checked: the Name Box names the cell, and the Focus is on it.
        const ok = await (async () => { for (let i = 0; i < 20; i++) { const r = await page.evaluate((sel) => document.querySelector(sel).querySelector('input.ex-name-box, .ex-name-box input')?.value, SHEET); if (r === st.press) return true; await sleep(100); } return false; })();
        if (!ok) throw new Error(`the press meant for ${st.press} left the Name Box at ${await page.evaluate((sel) => document.querySelector(sel).querySelector('input.ex-name-box, .ex-name-box input')?.value, SHEET)}`);
        await sleep(300);
        return d;
    }
    if (st.rpress) {
        const b = await page.locator(`${SHEET} ${cellSel(st.rpress)}`).first().boundingBox();
        const [x, y] = cal.at(b.x + b.width / 2, b.y + b.height / 2);
        await input(`rclick ${x} ${y}`); await sleep(500);
        return `a right click at (${x}, ${y}) on the middle of ${st.rpress}`;
    }
    if (st.menu) {
        const loc = page.getByRole('menuitem', { name: st.menu, exact: true }).first();
        for (let i = 0; i < 30 && !(await loc.count()); i++) await sleep(100);
        const d = await clickBox(await loc.boundingBox(), `the menu item '${st.menu}'`);
        await sleep(600);
        return d;
    }
    if (st.ctrlDrag) {
        const a = await page.locator(`${SHEET} ${cellSel(st.ctrlDrag[0])}`).first().boundingBox();
        const b = await page.locator(`${SHEET} ${cellSel(st.ctrlDrag[1])}`).first().boundingBox();
        const [x1, y1] = cal.at(a.x + a.width / 2, a.y + a.height / 2), [x2, y2] = cal.at(b.x + b.width / 2, b.y + b.height / 2);
        await input(`ctrldrag ${x1} ${y1} ${x2} ${y2} 20`);
        return `a drag with Ctrl held from ${st.ctrlDrag[0]} (${x1}, ${y1}) to ${st.ctrlDrag[1]} (${x2}, ${y2})`;
    }
    if (st.dlg) {
        const root = dialog();
        const loc = st.dlg.role === 'radio' || st.dlg.role === 'checkbox' ? root.getByLabel(st.dlg.name, { exact: true }) : root.getByRole(st.dlg.role, { name: st.dlg.name, exact: true });
        for (let i = 0; i < 40 && !(await loc.count()); i++) await sleep(100);
        const n = await loc.count();
        let which = null;
        for (let i = 0; i < n && !which; i++) { const b = await loc.nth(i).boundingBox(); if (b && b.width > 0) which = loc.nth(i); }
        if (!which) throw new Error(`no ${st.dlg.role} '${st.dlg.name}' in Format Cells`);
        const what = `${st.dlg.role} '${st.dlg.name}' of Format Cells (${n} so named)`;
        // A tab the tab strip does not show (MudBlazor's strip scrolls sideways when the dialog is too
        // narrow for all five) is reached as the keyboard reaches it: the arrows from the tab shown, which
        // has the keyboard when Format Cells opens.
        if (st.dlg.role === 'tab') {
            const v = await which.evaluate((el) => {
                const r = el.getBoundingClientRect(); const mid = r.left + r.width / 2;
                let c = el.parentElement;
                while (c && !/(hidden|auto|scroll)/.test(getComputedStyle(c).overflowX)) c = c.parentElement;
                const cr = c ? c.getBoundingClientRect() : { left: 0, right: innerWidth };
                const tabs = [...el.closest('[role=tablist]').querySelectorAll('[role=tab]')];
                return { shown: mid > cr.left && mid < cr.right, to: tabs.indexOf(el), from: tabs.findIndex((x) => x.getAttribute('aria-selected') === 'true') };
            });
            if (!v.shown) {
                const keys = v.to > v.from ? `{RIGHT ${v.to - v.from}}` : `{LEFT ${v.from - v.to}}`;
                await input(`type 120 ${keys}`); await sleep(600);
                return `keys ${keys} from the tab shown, to ${what}, which the tab strip did not show`;
            }
        }
        // Still, before the press: MudBlazor's dialog grows into place as it opens, and a press read
        // from a box mid-way lands on its backdrop, which closes it.
        for (let i = 0, last = null; i < 30; i++) { const b = JSON.stringify(await which.boundingBox()); if (b === last) break; last = b; await sleep(120); }
        const scrolled = await intoView(which, what);
        const geo = () => page.evaluate((sel) => ({ scrollY, top: document.querySelector(sel)?.getBoundingClientRect().top }), SHEET);
        const before = await geo();
        const d = scrolled + await clickBox(await which.boundingBox(), what);
        if (process.env.DEBUG) out.debug = [...(out.debug ?? []), { what, before, box: await which.boundingBox().catch(() => null), after: await geo() }];
        await sleep(400);
        if (st.dlg.role === 'button' && (st.dlg.name === 'OK' || st.dlg.name === 'Cancel')) {
            for (let i = 0; i < 30 && await dialog().count(); i++) await sleep(100);
            if (await dialog().count()) throw new Error(`Format Cells is still open after ${d}`);
        }
        return d;
    }
    if (st.dlgType) {
        const loc = dialog().locator(st.dlgType.selector).first();
        const scrolled = await intoView(loc, `the field ${st.dlgType.selector}`);
        const d = scrolled + await clickBox(await loc.boundingBox(), `the field ${st.dlgType.selector}`);
        await sleep(200);
        await input('type 40 ^{KEYA}');
        await input(`type 40 ${st.dlgType.text.replace(/[+^%~(){}[\]]/g, (m) => `{${m}}`)}`);
        await sleep(300);
        return `${d}; Ctrl+A; typed ${st.dlgType.text}`;
    }
    if (st.reload) { const u = await open(c.page); return `the page opened again (${u})`; }
    if (st.wait) { await sleep(st.wait); return `a wait of ${st.wait} ms`; }
    throw new Error(`no step ${JSON.stringify(st)}`);
}

// What the DOM holds now.
const READ = ({ sheetSel, cells, mud }) => {
    const g = document.querySelector(sheetSel);
    const box = (r) => ({ x: r.left, y: r.top, w: r.width, h: r.height });
    const cellOf = (a) => { const m = /^([A-Z]+)(\d+)$/.exec(a); let col = 0; for (const ch of m[1]) col = col * 26 + ch.charCodeAt(0) - 64; return g.querySelector(`[id$="-r${Number(m[2]) - 1}c${col - 1}"]`); };
    const style = (e) => { if (!e) return null; const cs = getComputedStyle(e); return { color: cs.color, background: cs.backgroundColor, weight: cs.fontWeight, italic: cs.fontStyle, decoration: cs.textDecorationLine, align: cs.textAlign }; };
    const o = { dpr: devicePixelRatio, cells: {} };
    for (const a of cells) {
        const e = cellOf(a);
        const inner = e?.querySelector('*');
        o.cells[a] = e ? { text: e.textContent, box: box(e.getBoundingClientRect()), cls: e.className, style: style(e), inner: inner ? { tag: inner.tagName.toLowerCase(), cls: inner.className?.baseVal ?? inner.className, style: style(inner) } : null } : null;
    }
    o.focus = g.getAttribute('aria-activedescendant')?.replace(/^.*-r(\d+)c(\d+)$/, (s, r, c) => { let n = +c + 1, l = ''; while (n > 0) { const k = (n - 1) % 26; l = String.fromCharCode(65 + k) + l; n = Math.floor((n - 1) / 26); } return l + (+r + 1); }) ?? null;
    o.nameBox = g.querySelector('input.ex-name-box, .ex-name-box input')?.value ?? null;
    o.bar = g.querySelector('input.ex-formula-bar-text, .ex-formula-bar-text input')?.value ?? null;
    o.editing = !!g.querySelector('.ex-viewport input.ex-editor, .ex-viewport .ex-editor input');
    o.ranges = [...g.querySelectorAll('.ex-range-single, .ex-range, [class*="ex-range"]')].map((r) => ({ cls: r.className, box: box(r.getBoundingClientRect()) })).slice(0, 12);
    o.rowHeights = [...g.querySelectorAll('.ex-viewport .ex-row')].slice(0, 18).map((r) => Math.round(r.getBoundingClientRect().height * 100) / 100);
    const heads = [...g.querySelectorAll('[class*="ex-column-heading"], .ex-header-cell, [role=columnheader]')].slice(0, 10);
    o.columnWidths = heads.map((h) => ({ text: h.textContent.trim(), w: Math.round(h.getBoundingClientRect().width * 100) / 100 }));
    const ae = document.activeElement;
    o.active = ae ? `${ae.tagName.toLowerCase()}${ae.id ? '#' + ae.id : ''}${typeof ae.className === 'string' && ae.className ? '.' + ae.className.split(' ').slice(0, 2).join('.') : ''}${ae.getAttribute('aria-label') ? `[${ae.getAttribute('aria-label')}]` : ''}${ae.getAttribute('role') === 'tab' ? `[tab ${ae.textContent.trim()}]` : ''}` : null;
    const root = mud ? document.querySelector('.mud-dialog.mud-ex-sheet-format-cells') : g.querySelector('.ex-popover-consumer');
    if (root) {
        const label = (i) => i.getAttribute('aria-label') ?? i.closest('label')?.textContent.trim().replace(/\s+/g, ' ').slice(0, 50) ?? i.id;
        const inputs = [...root.querySelectorAll('input')];
        o.dialog = {
            box: box(root.getBoundingClientRect()),
            tabs: [...root.querySelectorAll('[role=tab]')].map((t) => `${t.textContent.trim()}${t.getAttribute('aria-selected') === 'true' ? ' (selected)' : ''}`),
            legends: [...root.querySelectorAll('legend, .mud-input-label, h6')].map((l) => l.textContent.trim()).filter(Boolean),
            controls: inputs.filter((i) => !(i.type === 'radio' && i.closest('.ex-format-cells-palette, [class*="palette"]') && !i.checked)).map((i) => `${i.type} [${label(i)}]${i.type === 'radio' || i.type === 'checkbox' ? (i.checked ? ' checked' : '') : ` value=[${i.value}]`}${i.disabled ? ' disabled' : ''}${i.indeterminate ? ' indeterminate' : ''}`),
            swatches: inputs.filter((i) => i.type === 'radio' && i.closest('.ex-format-cells-palette, [class*="palette"]')).length,
            radiosInOrder: inputs.filter((i) => i.type === 'radio' && !i.closest('.ex-format-cells-palette, [class*="palette"]')).map((i) => label(i)),
            buttons: [...root.querySelectorAll('button')].map((b) => `${b.textContent.trim().replace(/\s+/g, ' ').slice(0, 30)}${b.getAttribute('aria-pressed') ? ` pressed=${b.getAttribute('aria-pressed')}` : ''}${b.disabled ? ' disabled' : ''}`).filter((t) => t),
            selects: [...root.querySelectorAll('select')].map((s) => `${s.className}=${s.value}`),
            preview: [...root.querySelectorAll('svg.ex-format-cells-preview line, svg[aria-label=Preview] line')].map((l) => `${l.getAttribute('data-edge')} ${l.getAttribute('stroke')} w${l.getAttribute('stroke-width')}${l.getAttribute('stroke-dasharray') ? ' dash ' + l.getAttribute('stroke-dasharray') : ''}${l.getAttribute('class') ? ' ' + l.getAttribute('class') : ''}`),
            alert: [...root.querySelectorAll('[role=alert]')].map((a) => a.textContent.trim()).filter(Boolean),
        };
    }
    return o;
};

// The clip every picture is taken of: the Sheet from its top-left corner to H16 (as Part A's pictures
// hold A1 to H16 with the headings), and Format Cells when it is open, with 4 px around.
const clipNow = (mud) => page.evaluate(([sheetSel, mud]) => {
    const g = document.querySelector(sheetSel);
    const m = (r, c) => g.querySelector(`[id$="-r${r}c${c}"]`);
    const corner = m(15, 7) ?? [...g.querySelectorAll('.ex-viewport .ex-row')].pop();
    const rs = [g.getBoundingClientRect()];
    const gr = rs[0], cr = corner?.getBoundingClientRect();
    const sheetRect = { left: gr.left, top: gr.top, right: cr ? Math.min(gr.right, cr.right) : gr.right, bottom: cr ? Math.min(gr.bottom, cr.bottom) : gr.bottom };
    const all = [sheetRect];
    const d = mud ? document.querySelector('.mud-dialog.mud-ex-sheet-format-cells') : g.querySelector('.ex-popover-consumer');
    if (d) all.push(d.getBoundingClientRect());
    const l = Math.max(0, Math.floor(Math.min(...all.map((r) => r.left))) - 4), t = Math.max(0, Math.floor(Math.min(...all.map((r) => r.top))) - 4);
    return { x: l, y: t, width: Math.min(innerWidth, Math.ceil(Math.max(...all.map((r) => r.right))) + 4) - l, height: Math.min(innerHeight, Math.ceil(Math.max(...all.map((r) => r.bottom))) + 4) - t };
}, [SHEET, mud]);

async function take(c, stateName, did, extra = {}) {
    await park();
    await sleep(700);
    const read = await page.evaluate(READ, { sheetSel: SHEET, cells: [...new Set([...(c.cells ?? []), ...(c.edges ?? []).map((e) => e.split(' ')[0])])], mud: CHROME === 'mud' });
    const clip = await clipNow(CHROME === 'mud');
    // At a zoom of the browser's own, Playwright's screenshot is not in device pixels (a trial of this
    // run: 1.5 px per zoomed CSS px, resampled, and a clip not where the page's boxes say). So the
    // browser's surface is taken as it is through the DevTools protocol (Page.captureScreenshot), the
    // whole window in device pixels, and every reading is made in it; the picture is cut to the clip
    // afterwards.
    const view = await page.evaluate(() => ({ x: 0, y: 0, width: innerWidth, height: innerHeight }));
    const shotClip = ZOOM !== 100 ? view : clip;
    const buffer = ZOOM !== 100
        ? Buffer.from((await cdp.send('Page.captureScreenshot', { format: 'png', fromSurface: true })).data, 'base64')
        : await page.screenshot({ clip, scale: 'device', animations: 'disabled', caret: 'hide' });
    const file = SHOTS ? path.join(SHOTS, `${c.id}-${stateName}-${CONFIG}.png`) : null;
    if (file) fs.writeFileSync(file, buffer);
    const img = decodePng(buffer);
    const scale = img.width / shotClip.width;
    const boxOf = (a) => { const b = read.cells[a]?.box; return b ? { x: b.x, y: b.y, w: b.w, h: b.h } : null; };
    const pixels = { scale, edges: {}, along: {}, ink: {} };
    for (const e of c.edges ?? []) { const [a, side] = e.split(' '); const b = boxOf(a); if (b) pixels.edges[e] = acrossEdge(img, shotClip, scale, b, side); }
    for (const e of c.along ?? []) { const [a, side] = e.split(' '); const b = boxOf(a); if (b) pixels.along[e] = alongEdge(img, shotClip, scale, b, side); }
    for (const a of c.cells ?? []) { const b = boxOf(a); if (b) pixels.ink[a] = inkOf(img, shotClip, scale, b); }
    return { state: stateName, did, read, clip, shotClip, pixels, shot: file ? path.basename(file) : null, ...extra };
}

async function runCase(c) {
    const rec = { case: c.id, what: c.what, page: c.page === null ? '/sheet' : `?case=${c.page}`, started: new Date().toISOString(), states: [] };
    rec.url = await open(c.page);
    rec.dpr = await page.evaluate(() => devicePixelRatio);
    for (const s of c.states) {
        if (s.tabsVisited) {
            // Case 22: each tab in the order the dialog shows them, pressed with the mouse.
            const names = await dialog().getByRole('tab').allTextContents();
            for (const n of names.map((t) => t.trim())) {
                const d = await doStep(DLG('tab', n), c);
                rec.states.push(await take(c, `tab-${n.toLowerCase()}`, [d]));
            }
            continue;
        }
        const did = [];
        try { for (const st of s.steps) did.push(await doStep(st, c)); }
        catch (e) { rec.error = String(e); rec.states.push(await take(c, `${s.state}-failed`, did)); return rec; }
        rec.states.push(await take(c, s.state, did));
    }
    // Nothing left open for the next case's page.
    if (await dialog().count()) await input('type 40 {ESC}');
    return rec;
}

try {
    await open(CASES.find((c) => (!ONLY && !c.extra) || ONLY?.includes(c.id)).page);
    await page.bringToFront();
    await sleep(300);
    await input(`front ${TITLE}`);
    out.keyboard = { before: await input(`layout ${TITLE}`), set: await input(`english ${TITLE}`) };
    out.baseDpr = await page.evaluate(() => devicePixelRatio);
    await input('imeoff');
    if (ZOOM !== 100) {
        // The browser's zoom: Ctrl+= three times (110%, 125%, 150%), real keys, as a user zooms.
        const press = [];
        for (let i = 0; i < 3; i++) { press.push(await input('ctrlchar =')); await sleep(600); }
        out.zoomKeys = press;
    }
    out.calibration = await calibrate();
    out.page = await page.evaluate(() => ({ dpr: devicePixelRatio, inner: [innerWidth, innerHeight], dark: matchMedia('(prefers-color-scheme: dark)').matches, forcedColors: matchMedia('(forced-colors: active)').matches, ua: navigator.userAgent }));
    save();
    const list = CASES.filter((c) => (!ONLY && !c.extra) || ONLY?.includes(c.id));
    for (const c of list) {
        try { out.cases.push(await runCase(c)); } catch (e) { out.cases.push({ case: c.id, error: String(e) }); await input('type 40 {ESC}').catch(() => {}); }
        save();
    }
} finally {
    out.keyboard = { ...(out.keyboard ?? {}), after: await input('imeoff').then(() => input(`english ${TITLE}`)).catch((e) => String(e)) };
    out.finished = new Date().toISOString();
    save();
    helper.stdin.write('quit\n');
    await browser.close();
}
console.log(JSON.stringify({ config: CONFIG, cases: out.cases.length, errors: out.cases.filter((c) => c.error).length, messages: out.messages.length, browser: out.browserVersion }));
