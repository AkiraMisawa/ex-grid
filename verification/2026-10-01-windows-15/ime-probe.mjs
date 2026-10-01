// docs/specs/exsheet/verify-on-windows-15.md, Part B (a real Japanese IME, ExSheet beside Excel) and
// Part C (ticket 74's readings, typed into ExSheet), with the real keyboard and mouse, on /sheet and
// /sheet?chrome=mud. What the Sheet holds and shows is read from the DOM after each step, with every
// composition event and keydown the page saw, and the page's pictures. This is the thirteenth run's
// probe (verification/2026-10-01-windows-13/pointing-scope-probe.mjs) with this run's pages and cases;
// the method is unchanged, and these are added: the Japanese keyboard for the window, the IME switched
// on and off by its keys (VK_IME_ON, VK_IME_OFF), the IME's open status and mode read after each step,
// and a listener of the probe's own that logs the page's events.
//
//   PAGE=sheet|mud               /sheet, or /sheet?chrome=mud (ExGrid.MudBlazor's Chrome)
//   CASES=i1,i2                  only these cases (Part C's, and i10, run only when named)
//   KEYBOARD=japanese|english    the window's keyboard: japanese for Part B, english for Part C
//
// Run on Windows from a copy of tests/ExGrid.Browser (for its node_modules), headed, the window at
// the display's own scale (viewport: null):
//
//     set EXGRID_CHANNEL=msedge & set LABEL=server-150 & set RTT=150 & set CONTROL=http://localhost:7298
//     set PAGE=sheet & set INPUT=...\input-server.ps1 & set OUT=...\x.json & set SHOTS=...
//     node ime-probe.mjs http://localhost:5298
//
// Every input goes through input-server.ps1 (real OS input: SendInput, a virtual-key and a scan code
// per key, never a Unicode character, so that the IME composes). Playwright opens the page, reads the
// DOM and takes the page's pictures; it sends no input. The IME's candidate window is drawn by the
// Windows Input Experience (TextInputHost), outside the browser: neither the page's picture nor any
// other picture this machine gives holds it (report.md, "The IME's candidate window"), so what
// the IME composes is read from the page's composition events and the field's value.
import { chromium } from '@playwright/test';
import { spawn } from 'node:child_process';
import readline from 'node:readline';
import fs from 'node:fs';
import path from 'node:path';

const BASE = process.argv[2] ?? 'http://localhost:5299';
const CHANNEL = process.env.EXGRID_CHANNEL ?? 'chrome';
const LABEL = process.env.LABEL ?? 'wasm';
const PAGE = process.env.PAGE ?? 'sheet';
const KEYBOARD = process.env.KEYBOARD ?? 'japanese';
const OUT = process.env.OUT;
const SHOTS = process.env.SHOTS;
const RTT = Number(process.env.RTT ?? 0);
const CONTROL = process.env.CONTROL;
const ONLY = process.env.CASES ? process.env.CASES.split(',') : null;
const GAP_MS = 30;
const TYPIST_MS = 150;
const TITLE = `ime15-${PAGE}-${LABEL}-${CHANNEL}`;
const out = { channel: CHANNEL, base: BASE, page: PAGE, label: LABEL, rtt: RTT, keyboardAsked: KEYBOARD, started: new Date().toISOString(), cases: [], messages: [] };
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

const PAGES = {
    sheet: { path: '/sheet', ready: { cell: 'B12', text: '318.25' } },
    mud: { path: '/sheet?chrome=mud', ready: { cell: 'B12', text: '318.25' } },
};
const P = PAGES[PAGE];
const TARGET = 'D10';

// ---- The cases -------------------------------------------------------------------------------------------

// A step: keys ({keys}, sent GAP_MS apart, or {keys, gap} at another pace), the IME switched on or
// off by its key ({ime: 'on' | 'off'}), a press on a cell of the Sheet ({press}), a press near the
// right end of the Formula Bar's field ({intoBar}), a press on the Name Box ({intoNameBox}), or a wait
// ({wait}). A state is steps, then a reading.
const K = (keys) => ({ keys });
const ON = { ime: 'on' }, OFF = { ime: 'off' };
const LOOKUP = '=XLOOKUP(1,A2:A4,B2:B4,,';
// One state per key, each named apart: the second a of kana is a2 (a picture is saved under its state's name).
const each = (keys) => [...keys].map((k, i) => ({ state: [...keys].slice(0, i).includes(k) ? `${k}${[...keys].slice(0, i + 1).filter((x) => x === k).length}` : k, steps: [K(k)] }));
// The IME is switched on where the procedure's keys say it is on: for i1 to i3, with D10 selected,
// which leaves the keyboard on the grid, not in a text field. Chrome gives a focused element that is
// not a text field no input context, and VK_IME_ON there leaves the IME off (seen in a trial before
// the recorded runs): so i1x to i3x, additions, open the edit with F2 first and switch the IME on in the
// Cell Editor, and i1y switches it on in an edit that Escape then closes, as a user who was typing
// Japanese a moment ago. i4 and i8 switch it on again once the keyboard is in the bar or in Find (a
// VK_IME_ON with the IME already on changes nothing).
const KANA = each('kana');
const ENDS = [{ state: 'space', steps: [K('{SPACE}')] }, { state: 'enter-1', steps: [K('{ENTER}')] }, { state: 'enter-2', steps: [K('{ENTER}')] }];
const F2_ON = [{ state: 'f2', steps: [K('{F2}')] }, { state: 'ime-on', steps: [ON] }];
const CASES = [
    { id: 'i1', what: 'D10: the IME on, kana, Space, Enter, Enter. Reading (ED-11): nothing commits or moves while composing; the first Enter ends the composition, the second commits the cell and moves to D11', states: [
        { state: 'ime-on', steps: [ON] }, ...KANA, ...ENDS] },
    { id: 'i1x', extra: true, what: 'An addition: i1 with the edit opened by F2 first and the IME switched on in the Cell Editor: F2, the IME on, kana, Space, Enter, Enter', states: [
        ...F2_ON, ...KANA, ...ENDS] },
    { id: 'i1y', extra: true, what: 'An addition: i1 with the IME left on by an earlier edit: F2, the IME on, Escape (the edit closed), then kana, Space, Enter, Enter', states: [
        ...F2_ON, { state: 'escape', steps: [K('{ESC}')] }, ...KANA, ...ENDS] },
    { id: 'i2', what: 'D10: the IME on, kana, Escape, Escape. Asked: does the first Escape end only the composition, leaving the edit open? The second cancels the edit, and D10 is unchanged', states: [
        { state: 'ime-on', steps: [ON] }, ...KANA, { state: 'escape-1', steps: [K('{ESC}')] }, { state: 'escape-2', steps: [K('{ESC}')] }] },
    { id: 'i2x', extra: true, what: 'An addition: i2 with the edit opened by F2 first: F2, the IME on, kana, Escape, Escape', states: [
        ...F2_ON, ...KANA, { state: 'escape-1', steps: [K('{ESC}')] }, { state: 'escape-2', steps: [K('{ESC}')] }] },
    { id: 'i3', what: 'D10: the IME on, kana, Space, Down, Down, Enter, Enter. Reading: the arrows choose among the candidates; the Focus does not move and nothing points', states: [
        { state: 'ime-on', steps: [ON] }, ...KANA, { state: 'space', steps: [K('{SPACE}')] }, { state: 'down-1', steps: [K('{DOWN}')] }, { state: 'down-2', steps: [K('{DOWN}')] },
        { state: 'enter-1', steps: [K('{ENTER}')] }, { state: 'enter-2', steps: [K('{ENTER}')] }] },
    { id: 'i3x', extra: true, what: 'An addition: i3 with the edit opened by F2 first: F2, the IME on, kana, Space, Down, Down, Enter, Enter', states: [
        ...F2_ON, ...KANA, { state: 'space', steps: [K('{SPACE}')] }, { state: 'down-1', steps: [K('{DOWN}')] }, { state: 'down-2', steps: [K('{DOWN}')] },
        { state: 'enter-1', steps: [K('{ENTER}')] }, { state: 'enter-2', steps: [K('{ENTER}')] }] },
    { id: 'i4', what: 'The Formula Bar: the IME on, a press on D10, a press into the bar\'s end (the IME switched on again there), kana, Space, Enter, Enter. Reading: as i1, in the bar; D10 holds what was chosen', states: [
        { state: 'ime-on', steps: [ON] }, { state: 'pressed-d10', steps: [{ wait: 700 }, { press: TARGET }] }, { state: 'into-bar', steps: [{ intoBar: true }] },
        { state: 'ime-on-in-bar', steps: [ON] }, ...KANA, ...ENDS] },
    { id: 'i5', what: 'D10: the IME off, =A1+; the IME on, a; Escape; the IME off, B1, Enter. Reading: while あ is composed after =A1+, A1 keeps its colour and the composition is shown once, not doubled or hidden (ADR-0057). After Enter, D10 holds =A1+B1', states: [
        { state: 'typed', steps: [OFF, K(lit('=A1+'))] }, { state: 'ime-on', steps: [ON] }, { state: 'a', steps: [K('a')] },
        { state: 'escape', steps: [K('{ESC}')] }, { state: 'ime-off', steps: [OFF] }, { state: 'b1', steps: [K('B1')] }, { state: 'enter', steps: [K('{ENTER}')] }] },
    { id: 'i6', what: 'D10: the IME off, =; the IME on, a; Down; Escape; the IME off, Down. Reading: while composing, Down is the IME\'s: no outline, nothing written. After the composition ends, Down points at D11 (=D11)', states: [
        { state: 'typed', steps: [OFF, K('=')] }, { state: 'ime-on', steps: [ON] }, { state: 'a', steps: [K('a')] },
        { state: 'down-composing', steps: [K('{DOWN}')] }, { state: 'escape', steps: [K('{ESC}')] }, { state: 'ime-off', steps: [OFF] }, { state: 'down', steps: [K('{DOWN}')] }] },
    { id: 'i7', what: 'The Name Box: a press on it, the IME on, kana, Enter, then Escape. Reading: the first Enter ends the composition and goes nowhere', states: [
        { state: 'pressed', steps: [{ intoNameBox: true }] }, { state: 'ime-on', steps: [ON] }, ...KANA,
        { state: 'enter', steps: [K('{ENTER}')] }, { state: 'escape', steps: [K('{ESC}')] }] },
    { id: 'i8', what: 'Find: the IME on, Ctrl+F (the IME switched on again in Find\'s field), kana, Enter, then Escape. Reading: the first Enter ends the composition and finds nothing yet', states: [
        { state: 'ime-on', steps: [ON] }, { state: 'ctrl-f', steps: [K('^{KEYF}')] }, { state: 'ime-on-in-find', steps: [ON] }, ...KANA,
        { state: 'enter', steps: [K('{ENTER}')] }, { state: 'escape', steps: [K('{ESC}')] }] },
    { id: 'i9', what: 'D10: the IME off, =SUM(A2:A4,B2:B4,C2:C4,; the IME on, a; Escape; the IME off, Down. Reading: after Down, =SUM(A2:A4,B2:B4,C2:C4,D11 is written and the caret is inside the Cell Editor\'s visible width (ticket 75)', states: [
        { state: 'typed', steps: [OFF, K(lit('=SUM(A2:A4,B2:B4,C2:C4,'))] }, { state: 'ime-on', steps: [ON] }, { state: 'a', steps: [K('a')] },
        { state: 'escape', steps: [K('{ESC}')] }, { state: 'ime-off', steps: [OFF] }, { state: 'down', steps: [K('{DOWN}')] }] },
    // i10: i1 and i4 at a typist's pace (TYPIST_MS between keys), read once at the end and once 2 s
    // later; behind 150 ms. "No key lost or doubled" is read from the keydowns the page saw.
    { id: 'i10a', extra: true, what: `i10, i1 at a typist's pace: the IME on, then kana, Space, Enter, Enter, ${TYPIST_MS} ms apart. Reading: as at 0 ms; no key lost or doubled`, states: [
        { state: 'ime-on', steps: [ON] }, { state: 'typed', steps: [{ keys: 'kana{SPACE}{ENTER}{ENTER}', gap: TYPIST_MS }] }, { state: 'later', steps: [{ wait: 2000 }] }] },
    { id: 'i10ax', extra: true, what: `i10, i1x at a typist's pace: F2, the IME on, then kana, Space, Enter, Enter, ${TYPIST_MS} ms apart. Reading: as at 0 ms; no key lost or doubled`, states: [
        ...F2_ON, { state: 'typed', steps: [{ keys: 'kana{SPACE}{ENTER}{ENTER}', gap: TYPIST_MS }] }, { state: 'later', steps: [{ wait: 2000 }] }] },
    { id: 'i10b', extra: true, what: `i10, i4 at a typist's pace: the IME on, a press on D10, a press into the bar's end, the IME on again, then kana, Space, Enter, Enter, ${TYPIST_MS} ms apart. Reading: as at 0 ms; no key lost or doubled`, states: [
        { state: 'ime-on', steps: [ON] }, { state: 'into-bar', steps: [{ wait: 700 }, { press: TARGET }, { intoBar: true }, ON] },
        { state: 'typed', steps: [{ keys: 'kana{SPACE}{ENTER}{ENTER}', gap: TYPIST_MS }] }, { state: 'later', steps: [{ wait: 2000 }] }] },
    // Part C: ticket 74's readings, typed with the IME off and the English (UK) keyboard.
    ...[['c1', `${LOOKUP}1.0`, '1 - Exact match or next larger item alone'], ['c2', `${LOOKUP}+1`, 'the same'], ['c3', `${LOOKUP}1 `, 'the same'],
        ['c4', `${LOOKUP}-0`, '0 - Exact match alone'], ['c5', `${LOOKUP}--1`, 'every value, the first selected'], ['c6', `${LOOKUP}50%`, 'every value, the first selected'],
        ['c7', `${LOOKUP}(`, 'nothing'], ['c8', `${LOOKUP}(A`, 'the functions beginning with A'], ['c9', `${LOOKUP}Positions[`, 'the table\'s columns'],
        ['c10', `${LOOKUP}Positions[Id]`, 'every value, the first selected'], ['c11', `${LOOKUP}(1)`, 'every value, the first selected']].map(([id, text, reading]) => (
        { id, extra: true, what: `Part C, ${id}: ${JSON.stringify(text)}. ExSheet as decided (ADR-0058): ${reading}`, text, reading, states: [{ state: 'typed', steps: [K(lit(text))] }] })),
];

// ---- What the page shows -----------------------------------------------------------------------------

// Everything read from the DOM in one state: which element has the keyboard, whether an edit is open,
// the Focus and the Name Box, the Cell Editor's and the Formula Bar's field with its coloured layer,
// the completion list and the argument hint, Find's field and its outcome, D10's and D11's text, and
// the events the page saw since the last reading.
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
    const sheet = document.querySelector('.ex-grid:has(> .ex-formula-bar)');
    const editor = sheet.querySelector('.ex-viewport input.ex-editor, .ex-viewport .ex-editor input');
    const bar = sheet.querySelector('input.ex-formula-bar-text, .ex-formula-bar-text input');
    const nameBox = sheet.querySelector('input.ex-name-box, .ex-name-box input');
    const find = document.querySelector('.ex-popover-find-body input[type=search], .ex-popover-find-body .mud-ex-grid-find input:not([type=checkbox])');
    const surface = (field) => {
        if (!field) return null;
        const layer = field.previousElementSibling?.classList.contains('ex-reference-text') ? field.previousElementSibling : null;
        const cs = getComputedStyle(field);
        return {
            value: field.value, selection: [field.selectionStart, field.selectionEnd], box: box(field.getBoundingClientRect()),
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
        if (e === nameBox) return 'sheet:name-box';
        if (find && e === find) return 'find';
        if (sheet.contains(e)) return `sheet:${e.tagName.toLowerCase()}.${typeof e.className === 'string' ? e.className.split(' ')[0] : ''}`;
        return `${e.tagName.toLowerCase()}${e.id ? '#' + e.id : ''}`;
    };
    const cellText = (a) => { const m = /^([A-Z]+)(\d+)$/.exec(a); let col = 0; for (const ch of m[1]) col = col * 26 + ch.charCodeAt(0) - 64; return [...sheet.querySelectorAll(`[id$="-r${Number(m[2]) - 1}c${col - 1}"]`)].map((e) => e.textContent).join('|'); };
    const completion = sheet.querySelector('.ex-completion');
    const outcome = document.querySelector('.ex-find-outcome, .mud-ex-grid-find-outcome');
    return {
        active: where(ae),
        editing: !!editor,
        focus: sheet.getAttribute('aria-activedescendant')?.replace(/^.*-r(\d+)c(\d+)$/, (s, r, c) => letters(+c) + (+r + 1)) ?? null,
        nameBox: nameBox?.value ?? null, nameBoxField: surface(nameBox),
        cell: surface(editor), bar: surface(bar),
        completion: completion ? [...completion.querySelectorAll('[role=option]')].map((o) => ({ text: o.textContent.trim(), selected: o.getAttribute('aria-selected') === 'true' })) : null,
        completionElement: completion ? { cls: completion.className, text: completion.textContent.trim().slice(0, 300), box: box(completion.getBoundingClientRect()) } : null,
        hints: [...sheet.querySelectorAll('.ex-argument-hint, [class*="hint"]')].map((h) => ({ cls: h.className, text: h.textContent.trim().slice(0, 200) })),
        outlines: [...sheet.querySelectorAll('.ex-reference-outline')].map((o) => ({ cls: o.className, box: box(o.getBoundingClientRect()) })),
        points: [...sheet.querySelectorAll('.ex-point, .ex-point-dashes')].map((o) => ({ cls: o.className, box: box(o.getBoundingClientRect()) })),
        find: find ? { value: find.value, selection: [find.selectionStart, find.selectionEnd], outcome: outcome?.textContent.trim() ?? null } : null,
        message: [...document.querySelectorAll('.ex-popover-message, .ex-message, [role=alert]')].map((m) => m.textContent.trim().slice(0, 200)).filter(Boolean),
        d10: cellText('D10'), d11: cellText('D11'), e10: cellText('E10'),
        events: (window.__events ?? []).splice(0),
        focusLog: (window.__focusLog ?? []).splice(0),
    };
};

// The pixels of a state: the page's own picture of the Sheet (and of Find's popover when it is
// open), and, read from it, the colours of the spans of the surface the edit is in.
async function pixels(page, read, clip, file) {
    const shot = await page.screenshot({ clip });
    if (file) fs.writeFileSync(file, shot);
    const boxes = [];
    const surf = read.active === 'sheet:bar' ? read.bar : read.cell;
    const lb = surf?.layer?.box;
    for (const s of surf?.layer?.spans ?? []) {
        const l = Math.max(s.box.l, lb.l), t = Math.max(s.box.t, lb.t), r = Math.min(s.box.l + s.box.w, lb.l + lb.w), b = Math.min(s.box.t + s.box.h, lb.t + lb.h);
        boxes.push({ text: s.text, box: { l, t, w: Math.max(0, r - l), h: Math.max(0, b - t) } });
    }
    // The field itself, where the composition is drawn by the browser: its saturated and dark pixels.
    if (surf) boxes.push({ text: '(the whole field)', box: surf.box });
    return page.evaluate(async ({ a, clip, boxes }) => {
        const bytes = Uint8Array.from(atob(a), (c) => c.charCodeAt(0));
        const bmp = await createImageBitmap(new Blob([bytes], { type: 'image/png' }));
        const cv = new OffscreenCanvas(bmp.width, bmp.height); const ctx = cv.getContext('2d'); ctx.drawImage(bmp, 0, 0);
        const A = { w: bmp.width, h: bmp.height, d: ctx.getImageData(0, 0, bmp.width, bmp.height).data };
        const s = A.w / clip.width;
        const at = (x, y) => { if (x < 0 || y < 0 || x >= A.w || y >= A.h) return null; const i = (y * A.w + x) * 4; return [A.d[i], A.d[i + 1], A.d[i + 2]]; };
        const hx = (p) => '#' + p.map((v) => v.toString(16).padStart(2, '0')).join('');
        const sat = (p) => Math.max(...p) - Math.min(...p);
        const top = (m, n) => [...m.entries()].sort((x, y) => y[1] - x[1]).slice(0, n).map(([k, v]) => `${k}:${v}`);
        const count = (m, k) => m.set(k, (m.get(k) ?? 0) + 1);
        const sum = (h) => parseInt(h.slice(1, 3), 16) + parseInt(h.slice(3, 5), 16) + parseInt(h.slice(5), 16);
        return { scale: s, boxes: boxes.map((bx) => {
            const x0 = Math.round((bx.box.l - clip.x) * s), y0 = Math.round((bx.box.t - clip.y) * s), x1 = Math.round((bx.box.l + bx.box.w - clip.x) * s), y1 = Math.round((bx.box.t + bx.box.h - clip.y) * s);
            const all = new Map(), saturated = new Map();
            for (let y = y0; y < y1; y++) for (let x = x0; x < x1; x++) { const p = at(x, y); if (!p) continue; count(all, hx(p)); if (sat(p) > 60) count(saturated, hx(p)); }
            return { text: bx.text, ground: top(all, 1)[0] ?? null, saturated: top(saturated, 3), darkest: [...all.keys()].sort((x, y) => sum(x) - sum(y))[0] ?? null };
        }) };
    }, { a: shot.toString('base64'), clip, boxes });
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
await page.waitForFunction(({ cell, text }) => {
    const g = document.querySelector('.ex-grid:has(> .ex-formula-bar)');
    const m = /^([A-Z]+)(\d+)$/.exec(cell); let col = 0; for (const ch of m[1]) col = col * 26 + ch.charCodeAt(0) - 64;
    const t = g && [...g.querySelectorAll(`[id$="-r${Number(m[2]) - 1}c${col - 1}"]`)].map((e) => e.textContent).join('');
    return t && t.includes(text);
}, P.ready, { timeout: 60_000 });
await page.bringToFront();
await page.evaluate((t) => { document.title = t; }, TITLE);
// The probe's own listeners, in the capture phase on the window: where DOM focus goes, and every
// keydown and composition event (and input event) the page sees, in order, with the field's value.
await page.evaluate(() => {
    window.__focusLog = []; window.__events = [];
    const name = (t) => {
        if (!(t instanceof Element)) return String(t?.nodeName ?? t);
        const sheet = document.querySelector('.ex-grid:has(> .ex-formula-bar)');
        if (t.matches('.ex-viewport input.ex-editor, .ex-viewport .ex-editor input')) return 'cell';
        if (t.matches('input.ex-formula-bar-text, .ex-formula-bar-text input')) return 'bar';
        if (t.matches('input.ex-name-box, .ex-name-box input')) return 'name-box';
        if (t.closest('.ex-popover-find-body')) return 'find';
        if (sheet?.contains(t)) return `sheet:${t.tagName.toLowerCase()}.${typeof t.className === 'string' ? t.className.split(' ')[0] : ''}`;
        return `${t.tagName.toLowerCase()}${t.id ? '#' + t.id : ''}`;
    };
    window.addEventListener('focusin', (e) => { window.__focusLog.push({ t: Math.round(performance.now()), el: name(e.target) }); }, true);
    for (const type of ['keydown', 'compositionstart', 'compositionupdate', 'compositionend', 'beforeinput', 'input']) {
        window.addEventListener(type, (e) => {
            const r = { t: Math.round(performance.now()), type, target: name(e.target) };
            if (type === 'keydown') Object.assign(r, { key: e.key, code: e.code, keyCode: e.keyCode, isComposing: e.isComposing, ctrl: e.ctrlKey || undefined, shift: e.shiftKey || undefined });
            else if (type.startsWith('composition')) r.data = e.data;
            else Object.assign(r, { inputType: e.inputType, data: e.data, isComposing: e.isComposing });
            if (e.target instanceof HTMLInputElement) r.value = e.target.value.slice(0, 80);
            // Whether something before this listener's turn had already prevented the event's default.
            r.defaultPrevented = e.defaultPrevented || undefined;
            window.__events.push(r);
        }, true);
    }
});
await sleep(500);
await input(`front ${TITLE}`);
out.keyboard = { before: await input(`layout ${TITLE}`) };
out.keyboard.set = await input(`${KEYBOARD} ${TITLE}`);
await input('imeoff');
out.keyboard.ime = await input(`imestate ${TITLE}`);
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

// The clip every picture is taken of: the Sheet with its Formula Bar, and Find's popover when it is
// open, with 4 px around.
const clipNow = () => page.evaluate(() => {
    const els = [document.querySelector('.ex-grid:has(> .ex-formula-bar)'), document.querySelector('.ex-popover-find-body')?.closest('[class*="popover"]')].filter(Boolean);
    const rs = els.map((e) => e.getBoundingClientRect());
    const l = Math.max(0, Math.floor(Math.min(...rs.map((r) => r.left))) - 4), t = Math.max(0, Math.floor(Math.min(...rs.map((r) => r.top))) - 4);
    return { x: l, y: t, width: Math.min(innerWidth, Math.ceil(Math.max(...rs.map((r) => r.right))) + 4) - l, height: Math.min(innerHeight, Math.ceil(Math.max(...rs.map((r) => r.bottom))) + 4) - t };
});

// Where things are, in screen pixels.
async function boxOf(selector, which = 'centre') {
    const r = await page.evaluate(([sel, which]) => {
        const g = document.querySelector('.ex-grid:has(> .ex-formula-bar)');
        const e = g.querySelector(sel); if (!e) return null;
        const b = e.getBoundingClientRect();
        return { x: which === 'end' ? b.right - 8 : (b.left + b.right) / 2, y: (b.top + b.bottom) / 2 };
    }, [selector, which]);
    if (!r) throw new Error(`${selector} is not on the page`);
    return cal.at(r.x, r.y);
}
const cellSel = (address) => { const a = at(address); return `[id$="-r${a.row}c${a.col}"]`; };
async function until(fn, arg, ms = 5000, step = 50) {
    const t0 = Date.now();
    while (Date.now() - t0 < ms) { const v = await page.evaluate(fn, arg); if (v) return Date.now() - t0; await sleep(step); }
    return null;
}
const stateFn = () => {
    const g = document.querySelector('.ex-grid:has(> .ex-formula-bar)');
    return { editing: !!g.querySelector('.ex-viewport input.ex-editor, .ex-viewport .ex-editor input'), completion: !!g.querySelector('.ex-completion'),
        find: !!document.querySelector('.ex-popover-find-body'), sheetHasFocus: g.contains(document.activeElement),
        inNameBox: document.activeElement?.matches?.('input.ex-name-box, .ex-name-box input') ?? false, focus: g.getAttribute('aria-activedescendant') };
};
const state = () => page.evaluate(stateFn);

// No edit open and the IME off. Escape until the Sheet says no edit, no list and no Find is open, and
// the keyboard is not left in the Name Box (an IME composition takes the first Escape or two). Then,
// if the IME is still on, F2 opens an edit, VK_IME_OFF switches the IME off there, and Escape closes
// the edit: Chrome gives the grid itself no input context, so VK_IME_OFF with the keyboard on the grid
// leaves the IME on (seen in a trial before the recorded runs, when the next case's keys composed).
async function reset() {
    let escapes = 0;
    const s0 = await state();
    if (s0.editing || s0.completion || s0.find || s0.inNameBox) await input('imeoff');
    for (let i = 0; i < 8; i++) {
        const s = await state();
        if (!s.editing && !s.completion && !s.find && !s.inNameBox) break;
        await input('type 0 {ESC}'); escapes++; await sleep(RTT ? 500 : 250);
    }
    let ime = await input(`imestate ${TITLE}`);
    let offInAnEdit = false;
    if (/open=True/.test(ime)) {
        await input('type 0 {F2}'); await sleep(RTT ? 600 : 300);
        await input('imeoff'); await sleep(200);
        await input('type 0 {ESC}'); await sleep(RTT ? 600 : 300);
        offInAnEdit = true;
        ime = await input(`imestate ${TITLE}`);
    }
    return { escapes, offInAnEdit, ime };
}
// D10 has the Focus and no edit is open. A press on it while it has the Focus, soon after the last,
// would be a double-click and open an edit: it is pressed only when the Focus is elsewhere or the
// keyboard is not the Sheet's.
async function selectTarget() {
    const t = at(TARGET);
    const ready = () => page.evaluate(([t, name]) => {
        const g = document.querySelector('.ex-grid:has(> .ex-formula-bar)');
        const nb = g.querySelector('input.ex-name-box, .ex-name-box input');
        return new RegExp(`-r${t.row}c${t.col}$`).test(g.getAttribute('aria-activedescendant') ?? '') && g.contains(document.activeElement)
            && !document.activeElement.matches('input.ex-name-box, .ex-name-box input')
            && !g.querySelector('.ex-viewport input.ex-editor, .ex-viewport .ex-editor input') && nb?.value === name;
    }, [t, TARGET]);
    if (await ready()) return 0;
    const [x, y] = await boxOf(cellSel(TARGET));
    await input(`click ${x} ${y}`);
    const t0 = Date.now();
    while (Date.now() - t0 < 5000) { if (await ready()) return Date.now() - t0; await sleep(50); }
    throw new Error(`${TARGET} did not take the Focus: ${JSON.stringify(await state())}`);
}
// D10 and D11 hold nothing before a case: a case that entered something is cleared with Delete.
async function cleanTargets() {
    const did = [];
    for (const a of ['D11', 'D10']) {
        const text = await page.evaluate((sel) => [...document.querySelector('.ex-grid:has(> .ex-formula-bar)').querySelectorAll(sel)].map((e) => e.textContent).join(''), cellSel(a));
        if (a === 'D11' && text !== '') { await input('type 0 {DOWN}'); await sleep(RTT ? 500 : 200); await input('type 0 {DEL}'); await sleep(RTT ? 600 : 300); await input('type 0 {UP}'); await sleep(RTT ? 500 : 200); did.push(`D11 held ${JSON.stringify(text)}, cleared with Delete`); }
        if (a === 'D10' && text !== '') { await input('type 0 {DEL}'); await sleep(RTT ? 600 : 300); did.push(`D10 held ${JSON.stringify(text)}, cleared with Delete`); }
    }
    return did;
}
// The layer of the surface the edit is in has caught up with its field (or there is none).
const settledFn = () => {
    const f = document.activeElement;
    if (!(f instanceof HTMLInputElement)) return true;
    const layer = f.previousElementSibling?.classList.contains('ex-reference-text') ? f.previousElementSibling : null;
    return !layer || layer.getAttribute('data-ex-text') === f.value;
};

async function doStep(st) {
    if (st.keys !== undefined) { const gap = st.gap ?? GAP_MS; await input(`type ${gap} ${st.keys}`); return `keys ${st.keys}${st.gap ? `, ${gap} ms apart` : ''}`; }
    if (st.ime) { await input(st.ime === 'on' ? 'imeon' : 'imeoff'); return `the IME ${st.ime} (${st.ime === 'on' ? 'VK_IME_ON' : 'VK_IME_OFF'})`; }
    if (st.press) { const [x, y] = await boxOf(cellSel(st.press)); await input(`click ${x} ${y}`); return `a press at (${x}, ${y}), the middle of the Sheet's ${st.press}`; }
    if (st.intoBar) { const [x, y] = await boxOf('input.ex-formula-bar-text, .ex-formula-bar-text input', 'end'); await input(`click ${x} ${y}`); return `a press at (${x}, ${y}), 8 px inside the right end of the Formula Bar's field`; }
    if (st.intoNameBox) { const [x, y] = await boxOf('input.ex-name-box, .ex-name-box input'); await input(`click ${x} ${y}`); return `a press at (${x}, ${y}), the middle of the Name Box`; }
    if (st.wait) { await sleep(st.wait); return `a wait of ${st.wait} ms`; }
    throw new Error(`no step ${JSON.stringify(st)}`);
}

async function take(caseId, stateName, did) {
    const t0 = Date.now();
    const base = RTT ? 900 : 600;
    await sleep(base);
    let waited = base;
    let settled = await page.evaluate(settledFn);
    if (!settled) { const ms = await until(settledFn, null, 2000, 50); waited += ms ?? 2000; settled = ms !== null; if (settled) await sleep(150); }
    const read = await page.evaluate(READ);
    const ime = await input(`imestate ${TITLE}`);
    const clip = await clipNow();
    const file = SHOTS ? path.join(SHOTS, `${PAGE}-${caseId}-${stateName}-${LABEL}-${CHANNEL}.png`) : null;
    const px = await pixels(page, read, clip, file);
    return { state: stateName, did, readAtMs: waited, settled, ime, read, clip, pixels: px, shot: file ? path.basename(file) : null, tookMs: Date.now() - t0 };
}

async function runCase(c) {
    const rec = { case: c.id, what: c.what, started: new Date().toISOString(), states: [] };
    rec.before = await reset();
    rec.focusMs = await selectTarget();
    rec.targetsBefore = await cleanTargets();
    await sleep(300);
    rec.beforeRead = await page.evaluate(READ);
    for (const s of c.states) {
        const did = []; for (const st of s.steps) did.push(await doStep(st));
        rec.states.push(await take(c.id, s.state, did));
    }
    rec.after = await reset();
    await sleep(300);
    rec.afterRead = await page.evaluate(READ);
    // What D10 holds as its Entry: the Formula Bar's text with D10 selected.
    await selectTarget(); await sleep(RTT ? 600 : 300);
    rec.d10Entry = await page.evaluate(() => document.querySelector('.ex-grid:has(> .ex-formula-bar)').querySelector('input.ex-formula-bar-text, .ex-formula-bar-text input')?.value ?? null);
    return rec;
}

try {
    const list = CASES.filter((c) => (!ONLY && !c.extra) || ONLY?.includes(c.id));
    for (const c of list) {
        try { out.cases.push(await runCase(c)); } catch (e) { out.cases.push({ case: c.id, error: String(e) }); await reset().catch(() => {}); }
        save();
    }
} finally {
    if (CONTROL) await fetch(`${CONTROL}/?rtt=0`, { method: 'POST' }).catch(() => {});
    // The keyboard put back: the IME off and English (UK), which with one input method for every
    // window is the desktop's too.
    out.keyboard.after = await input('imeoff').then(() => input(`english ${TITLE}`)).catch((e) => String(e));
    out.keyboard.imeAfter = await input(`imestate ${TITLE}`).catch((e) => String(e));
    out.finished = new Date().toISOString();
    save();
    helper.stdin.write('quit\n');
    await browser.close();
}
console.log(JSON.stringify({ page: PAGE, label: LABEL, channel: CHANNEL, cases: out.cases.length, errors: out.cases.filter((c) => c.error).length, messages: out.messages.length, browser: out.browserVersion }));
