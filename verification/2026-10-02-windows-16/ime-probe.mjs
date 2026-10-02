// docs/specs/exsheet/verify-on-windows-16.md, Parts A to D: a real Japanese IME on the Keyboard Field
// (ADR-0080), the fifteenth run's cases again, the keyboard and the clipboard with the IME off, and
// the cases the fifteenth run left, with the real keyboard and mouse. What the grid holds and shows is
// read from the DOM after each step, with every composition event and keydown the page saw, the IME's
// open status and mode, and the page's pictures. This is the fifteenth run's probe
// (verification/2026-10-01-windows-15/ime-probe.mjs) with this run's pages and cases; its method is
// unchanged, and these are added, as the procedure's Setup asks:
//
//   - the Keyboard Field read in every state: whether it holds DOM focus, its value, whether it wears
//     the composing class (ex-key-field-composing, the build's name), its computed opacity, read-only
//     and tab stop, and its box beside the Focus cell's box; the root's tab stop, its focus ring's mark
//     (data-ex-focus-visible) and its outline;
//   - the Focus read from the field's aria-activedescendant (the root's while it has no field);
//   - every grid on the page read too (/sheets has two Sheets, /features a display-only grid);
//   - steps for this run's cases: a press on a column header, at a place in a cell, on a control of
//     the page, on a cell of another grid; a drag inside the Name Box's text; focus given by script to
//     an element of the page (recorded as such); Alt+Tab to a window of the input helper's own and
//     back; the clipboard's text read;
//   - with AX=1, the focused element as Chromium's accessibility tree has it (over the DevTools
//     protocol): its role and name, and what its aria-activedescendant resolves to.
//
//   PAGE=sheet|mud|features|features-mud|sheets|sheets-mud
//   CASES=k1,k2                  only these cases (else the page's Part A, B and D cases)
//   KEYBOARD=japanese|english    the window's keyboard: japanese for Parts A, B and D, english for C
//   AX=1                         read the accessibility tree after each state
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
// DOM and takes the page's pictures; it sends no input, but for one thing, marked in the records: Part
// C's "Tab into the grid from the element before it" gives that element focus by script first (a press
// on it would follow a link or press a button). The IME's candidate window is drawn by the Windows
// Input Experience, outside the browser, and no picture this machine gives holds it (the fifteenth
// run's report): what the IME composes is read from the page's composition events and the fields.
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
const AX = process.env.AX === '1';
const ONLY = process.env.CASES ? process.env.CASES.split(',') : null;
const GAP_MS = 30;
const TYPIST_MS = 150;
const TITLE = `ime16-${PAGE}-${LABEL}-${CHANNEL}`;
const out = { channel: CHANNEL, base: BASE, page: PAGE, label: LABEL, rtt: RTT, keyboardAsked: KEYBOARD, ax: AX, started: new Date().toISOString(), cases: [], messages: [] };
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

// ---- The pages and their grids ---------------------------------------------------------------------

// A grid is named by a selector and which of its matches: the Sheet on /sheet, the left or the right
// Sheet on /sheets, the first (editable) or the second (display-only) grid on /features, and the
// positions grid beside the Sheet on /sheet.
const SHEET = { css: '.ex-grid:has(> .ex-formula-bar)', nth: 0, name: 'sheet' };
const POSITIONS = { css: '#sheet-positions .ex-grid', nth: 0, name: 'positions' };
const LEFT = { css: '#sheet-left .ex-grid:has(> .ex-formula-bar)', nth: 0, name: 'left' };
const RIGHT = { css: '#sheet-right .ex-grid:has(> .ex-formula-bar)', nth: 0, name: 'right' };
const MAIN = { css: '.ex-grid', nth: 0, name: 'main' };
const SECOND = { css: '.ex-grid', nth: 1, name: 'second' };
const PAGES = {
    sheet: { path: '/sheet', grid: SHEET, target: 'D10', ready: { grid: SHEET, cell: 'B12', text: '318.25' }, clip: [SHEET], cells: ['D10', 'D11', 'D12', 'D13', 'E10', 'E11', 'E12', 'F11'] },
    mud: { path: '/sheet?chrome=mud', grid: SHEET, target: 'D10', ready: { grid: SHEET, cell: 'B12', text: '318.25' }, clip: [SHEET], cells: ['D10', 'D11', 'D12', 'D13', 'E10', 'E11', 'E12', 'F11'] },
    features: { path: '/features', grid: MAIN, target: 'C1', ready: { grid: MAIN, cell: 'A1', text: '' }, clip: [MAIN, SECOND], cells: ['A1', 'B1', 'C1', 'C2', 'C3', 'C4'] },
    'features-mud': { path: '/features?chrome=mud', grid: MAIN, target: 'C1', ready: { grid: MAIN, cell: 'A1', text: '' }, clip: [MAIN, SECOND], cells: ['A1', 'B1', 'C1', 'C2', 'C3', 'C4'] },
    sheets: { path: '/sheets', grid: LEFT, target: 'B2', ready: { grid: RIGHT, cell: 'A1', text: '' }, clip: [LEFT, RIGHT], cells: ['B2', 'B3'] },
    'sheets-mud': { path: '/sheets?chrome=mud', grid: LEFT, target: 'B2', ready: { grid: RIGHT, cell: 'A1', text: '' }, clip: [LEFT, RIGHT], cells: ['B2', 'B3'] },
};
const P = PAGES[PAGE];
const TARGET = P.target;
const SHEETLIKE = PAGE === 'sheet' || PAGE === 'mud';

// ---- The cases -------------------------------------------------------------------------------------

// A step: keys ({keys}, sent GAP_MS apart, or {keys, gap} at another pace; {burst}, every key event in
// one SendInput call), the IME switched on or off by its key ({ime}), a press on a cell ({press},
// {press, grid}) or at a place in it ({pressAt, dx}), on a column header ({header}), on the Formula
// Bar's text ({intoBar}), on the Name Box ({intoNameBox}), on an element of the page ({pressOn}), a
// drag inside the Name Box's text ({nbdrag: [from, to]}, character boundaries), focus given by script
// ({focusScript}), Alt+Tab ({alttab}), the clipboard read ({clipboard}), or a wait ({wait}). A state is
// steps, then a reading.
const K = (keys) => ({ keys });
const ON = { ime: 'on' }, OFF = { ime: 'off' };
// One state per key, each named apart: the second a of kana is a2 (a picture is saved under its state's name).
const each = (keys) => [...keys].map((k, i) => ({ state: [...keys].slice(0, i).includes(k) ? `${k}${[...keys].slice(0, i + 1).filter((x) => x === k).length}` : k, steps: [K(k)] }));
const KANA = each('kana');
const ENDS = [{ state: 'space', steps: [K('{SPACE}')] }, { state: 'enter-1', steps: [K('{ENTER}')] }, { state: 'enter-2', steps: [K('{ENTER}')] }];
const F2_ON = [{ state: 'f2', steps: [K('{F2}')] }, { state: 'ime-on', steps: [ON] }];
const IME_ON = { state: 'ime-on', steps: [ON] };
const s = (state, ...steps) => ({ state, steps });

const PART_A = [
    { id: 'k1', what: 'D10: VK_IME_ON. Expected (ADR-0080): the IME\'s open status reads on afterwards (the fifteenth run: on the root it stayed off). Excel: the same', states: [IME_ON] },
    { id: 'k2', what: 'D10: the IME on; kana, Space, Enter, Enter. Expected: ｋ, か, かｎ, かな composed in the Keyboard Field drawn over D10 (every keydown Process, isComposing from the second); no edit open and the Focus on D10 while composing. Space: かな. The first Enter: compositionend, the Cell Editor open holding かな, with the keyboard; the field empty and unseen. The second Enter: D10 かな, the Focus D11, the keyboard back in the field', states: [IME_ON, ...KANA, ...ENDS] },
    { id: 'k3', what: 'D10: the IME on; kana, Escape, Escape. Expected: the first Escape ends the composition (compositionend with \'\'); the Cell Editor open and empty. The second cancels it; D10 unchanged', states: [IME_ON, ...KANA, s('escape-1', K('{ESC}')), s('escape-2', K('{ESC}'))] },
    { id: 'k4', what: 'D10: the IME on; kana, Space, Down, Down, Enter, Enter. Expected: each Down the IME\'s (Process/ArrowDown with isComposing); no Focus move, no outline, nothing written. Then as k2', states: [IME_ON, ...KANA, s('space', K('{SPACE}')), s('down-1', K('{DOWN}')), s('down-2', K('{DOWN}')), s('enter-1', K('{ENTER}')), s('enter-2', K('{ENTER}'))] },
    { id: 'k5', what: 'D10: the IME left on by an earlier edit (F2, the IME on, Escape), then a press on D10, kana, Space, Enter, Enter. Expected: composes from the first key: D10 かな, not k穴 (the fifteenth run\'s i1y)', states: [...F2_ON, s('escape', K('{ESC}')), s('pressed-d10', { wait: 700 }, { press: 'D10' }), ...KANA, ...ENDS] },
    { id: 'k6', what: 'D10: the IME on; kana, Space, kanji, Space, Enter, Enter. Expected: one composition of two clauses, converted together; D10 holds both, as the IME converted them', states: [IME_ON, ...KANA, s('space-1', K('{SPACE}')), ...each('kanji').map((x) => ({ ...x, state: `kanji-${x.state}` })), s('space-2', K('{SPACE}')), s('enter-1', K('{ENTER}')), s('enter-2', K('{ENTER}'))] },
    { id: 'k6x', what: `An addition: k6 with kanji typed at a typist's pace, ${TYPIST_MS} ms between its keys (k6 reads after every key, 600 ms or more apart): the IME on; kana, Space, then kanji in one state; Space, Enter, Enter`, states: [IME_ON, ...KANA, s('space-1', K('{SPACE}')), s('kanji', { keys: 'kanji', gap: TYPIST_MS }), s('space-2', K('{SPACE}')), s('enter-1', K('{ENTER}')), s('enter-2', K('{ENTER}'))] },
    { id: 'k6y', what: `An addition: k6 with kanji typed ${GAP_MS} ms between its keys (in the trial before the recorded runs, at this pace the IME's first key after Space ended the first clause's composition, the edit opened, and a key of the second clause was lost): the IME on; kana, Space, then kanji in one state; Space, Enter, Enter`, states: [IME_ON, ...KANA, s('space-1', K('{SPACE}')), s('kanji', K('kanji')), s('space-2', K('{SPACE}')), s('enter-1', K('{ENTER}')), s('enter-2', K('{ENTER}'))] },
    { id: 'k7', extra: true, what: `D10, Server behind 150 ms: the IME on; at ${TYPIST_MS} ms between keys: kana, Space, Enter, desu, Space, Enter, Enter. Expected: two compositions: the second may start in the Keyboard Field before the Cell Editor has the keyboard; it finishes there, and is appended. D10 かなです, D11. Nothing lost, doubled or reordered`, states: [IME_ON, s('typed', { keys: 'kana{SPACE}{ENTER}desu{SPACE}{ENTER}{ENTER}', gap: TYPIST_MS }), s('later', { wait: 2000 })] },
    { id: 'k8', what: 'D10: the IME on; kana composed; a press on D12. Expected: the composition ends where it was composed: D10 かな, committed by the press as Excel\'s click-away commits; the Focus D12; no edit open; the keyboard in the field', states: [IME_ON, ...KANA, s('pressed-d12', { press: 'D12' })] },
    { id: 'k8x', what: 'An addition: k8 with the press clear of the IME\'s own window (in the trial before the recorded runs, the press on D12 landed on the IME\'s prediction list, which stands under the composition, and never reached the page): the IME on; kana composed; a press on D8, above D10', states: [IME_ON, ...KANA, s('pressed-d8', { press: 'D8' })] },
    { id: 'k9', what: 'D10: the IME on; kana composed; a press on the Formula Bar\'s text. Record where かな goes and where the keyboard is', states: [IME_ON, ...KANA, s('pressed-bar', { intoBar: true })] },
    { id: 'k10', what: 'D10: the IME on; a press on the column header D while kana is composed. Record what is sorted or selected, and where かな goes. ADR-0080: a primary press anywhere in the root ends the composition first, so かな goes to D10', states: [IME_ON, ...KANA, s('pressed-header-d', { header: 'D' })] },
    { id: 'k12', what: 'D10: the IME on, no composition: Space. Record the keydown (Process, or \' \') and what is typed. A Space the listener sees is the grid\'s, and opens Overwrite holding a space (ADR-0037); text the IME inserts without composing is carried into an edit as a composition\'s text is', states: [IME_ON, s('space', K('{SPACE}'))] },
    { id: 'k13', what: 'D10: the IME on, no composition: Down, Right, Tab, Enter, Delete, Ctrl+C, then a press on D12 and Ctrl+V. Expected: each is the grid\'s as with the IME off: the Focus moves, Delete clears, the copy and the paste land. Nothing is typed into the field', states: [IME_ON, s('down', K('{DOWN}')), s('right', K('{RIGHT}')), s('tab', K('{TAB}')), s('enter', K('{ENTER}')), s('delete', K('{DEL}')), s('ctrl-c', K('^{KEYC}'), { clipboard: true }), s('pressed-d12', { press: 'D12' }), s('ctrl-v', K('^{KEYV}'))] },
    { id: 'k13x', what: 'An addition: k13\'s copy and paste with something to copy: the IME off, 150, Enter (D10 150), Up; the IME on; Ctrl+C; a press on D12; Ctrl+V; Delete', states: [s('typed-off', OFF, K('150{ENTER}')), s('up', K('{UP}')), IME_ON, s('ctrl-c', K('^{KEYC}'), { clipboard: true }), s('pressed-d12', { press: 'D12' }), s('ctrl-v', K('^{KEYV}')), s('delete', K('{DEL}'))] },
    { id: 'k15', what: 'D10: the IME on; kana composed; Alt+Tab to another window (a window of the input helper\'s own) and back; then Enter, Enter. Record whether the composition survives, and where its text goes', states: [IME_ON, ...KANA, s('alt-tab-away', { alttab: true }), s('alt-tab-back', { alttab: true }), s('enter-1', K('{ENTER}')), s('enter-2', K('{ENTER}'))] },
    { id: 'k16', what: 'D10: the IME on; kana composed; a press on the composition\'s own text (14 CSS px right of D10\'s left edge, half way down). Record what happens. (The field takes no pointer: the press is on D10\'s row, and ends the composition.)', states: [IME_ON, ...KANA, s('pressed-on-text', { pressAt: 'D10', dx: 14 })] },
];
// k11 on /features, k14 on /sheets.
const FEATURES_A = [
    { id: 'k11', what: '/features: a press on a Book cell (not Editable), the IME on, kana; then a press on a Trader cell, the IME on, kana, Space, Enter, Enter. Expected: Book: the field is read-only, the IME stays off (open status off), kana types nothing and opens nothing, as on the root before. Trader: composes as k2', states: [
        s('pressed-book', { wait: 700 }, { press: 'A1' }), s('ime-on', ON), ...each('kana'), s('pressed-trader', { wait: 700 }, { press: 'B1' }), s('ime-on-2', ON),
        ...KANA.map((x) => ({ ...x, state: `trader-${x.state}` })), s('space', K('{SPACE}')), s('enter-1', K('{ENTER}')), s('enter-2', K('{ENTER}'))] },
];
const SHEETS_A = [
    { id: 'k14', noSelect: true, noReset: true, what: '/sheets: the IME on; a press on the left Sheet\'s B2, kana composed and committed (Enter); a press on the right Sheet\'s B2; kana. Expected: the left edit stands with かな (ADR-0018 section 6); the right Sheet composes in its own field; nothing reaches the left', states: [
        s('pressed-left-b2', { press: 'B2', grid: LEFT }), IME_ON, ...KANA, s('enter', K('{ENTER}')), s('pressed-right-b2', { wait: 700 }, { press: 'B2', grid: RIGHT }),
        ...KANA.map((x) => ({ ...x, state: `right-${x.state}` })), s('later', { wait: 1500 })] },
];

// Part B: the fifteenth run's cases, with the same keys.
const PART_B = [
    { id: 'i1x', what: 'Part B (the fifteenth run\'s i1x): F2, the IME on, kana, Space, Enter, Enter', states: [...F2_ON, ...KANA, ...ENDS] },
    { id: 'i2x', what: 'Part B (the fifteenth run\'s i2x): F2, the IME on, kana, Escape, Escape', states: [...F2_ON, ...KANA, s('escape-1', K('{ESC}')), s('escape-2', K('{ESC}'))] },
    { id: 'i3x', what: 'Part B (the fifteenth run\'s i3x): F2, the IME on, kana, Space, Down, Down, Enter, Enter', states: [...F2_ON, ...KANA, s('space', K('{SPACE}')), s('down-1', K('{DOWN}')), s('down-2', K('{DOWN}')), s('enter-1', K('{ENTER}')), s('enter-2', K('{ENTER}'))] },
    { id: 'i4', what: 'Part B (the fifteenth run\'s i4): the IME on, a press on D10, a press into the bar\'s end (the IME switched on again there), kana, Space, Enter, Enter. Reading: as i1, in the bar; D10 holds what was chosen', states: [
        IME_ON, s('pressed-d10', { wait: 700 }, { press: TARGET }), s('into-bar', { intoBar: true }), s('ime-on-in-bar', ON), ...KANA, ...ENDS] },
    { id: 'i5', what: 'Part B (the fifteenth run\'s i5): the IME off, =A1+; the IME on, a; Escape; the IME off, B1, Enter. Reading: while あ is composed after =A1+, A1 keeps its colour and the composition is shown once (ADR-0057). After Enter, D10 holds =A1+B1', states: [
        s('typed', OFF, K(lit('=A1+'))), IME_ON, s('a', K('a')), s('escape', K('{ESC}')), s('ime-off', OFF), s('b1', K('B1')), s('enter', K('{ENTER}'))] },
    { id: 'i6', what: 'Part B (the fifteenth run\'s i6): the IME off, =; the IME on, a; Down; Escape; the IME off, Down. Reading: while composing, Down is the IME\'s. After the composition ends, Down points at D11 (=D11)', states: [
        s('typed', OFF, K('=')), IME_ON, s('a', K('a')), s('down-composing', K('{DOWN}')), s('escape', K('{ESC}')), s('ime-off', OFF), s('down', K('{DOWN}'))] },
    { id: 'i7', what: 'Part B (the fifteenth run\'s i7): a press on the Name Box, the IME on, kana, Enter, then Escape. Reading: the first Enter ends the composition and goes nowhere', states: [
        s('pressed', { intoNameBox: true }), IME_ON, ...KANA, s('enter', K('{ENTER}')), s('escape', K('{ESC}'))] },
    { id: 'i8', what: 'Part B (the fifteenth run\'s i8): the IME on, Ctrl+F (the IME switched on again in Find\'s field), kana, Enter, then Escape. Reading: the first Enter ends the composition and finds nothing yet', states: [
        IME_ON, s('ctrl-f', K('^{KEYF}')), s('ime-on-in-find', ON), ...KANA, s('enter', K('{ENTER}')), s('escape', K('{ESC}'))] },
    { id: 'i9', what: 'Part B (the fifteenth run\'s i9): the IME off, =SUM(A2:A4,B2:B4,C2:C4,; the IME on, a; Escape; the IME off, Down. Reading: =SUM(A2:A4,B2:B4,C2:C4,D11 is written and the caret is inside the Cell Editor\'s visible width (ticket 75)', states: [
        s('typed', OFF, K(lit('=SUM(A2:A4,B2:B4,C2:C4,'))), IME_ON, s('a', K('a')), s('escape', K('{ESC}')), s('ime-off', OFF), s('down', K('{DOWN}'))] },
    { id: 'i10a', extra: true, what: `Part B (the fifteenth run's i10a, behind 150 ms): the IME on, then kana, Space, Enter, Enter, ${TYPIST_MS} ms apart. Reading: as at 0 ms; no key lost or doubled`, states: [
        IME_ON, s('typed', { keys: 'kana{SPACE}{ENTER}{ENTER}', gap: TYPIST_MS }), s('later', { wait: 2000 })] },
    { id: 'i10ax', extra: true, what: `Part B (the fifteenth run's i10ax, behind 150 ms): F2, the IME on, then kana, Space, Enter, Enter, ${TYPIST_MS} ms apart`, states: [
        ...F2_ON, s('typed', { keys: 'kana{SPACE}{ENTER}{ENTER}', gap: TYPIST_MS }), s('later', { wait: 2000 })] },
    { id: 'i10b', extra: true, what: `Part B (the fifteenth run's i10b, behind 150 ms): the IME on, a press on D10, a press into the bar's end, the IME on again, then kana, Space, Enter, Enter, ${TYPIST_MS} ms apart`, states: [
        IME_ON, s('into-bar', { wait: 700 }, { press: TARGET }, { intoBar: true }, ON), s('typed', { keys: 'kana{SPACE}{ENTER}{ENTER}', gap: TYPIST_MS }), s('later', { wait: 2000 })] },
];

// Part D: the cases the fifteenth run left.
const PART_D = [
    { id: 'd1', what: 'The Name Box: a press on D10, a press on the Name Box, the IME on, kana, Enter, Escape. Expected: the press selected D10 (ticket 78). The first composing key replaced the selection: the Name Box reads かな while composing, not D10かな. The first Enter ends the composition and goes nowhere', states: [
        s('pressed-name-box', { intoNameBox: true }), IME_ON, ...KANA, s('enter', K('{ENTER}')), s('escape', K('{ESC}'))] },
    { id: 'd2', extra: true, what: 'The Name Box, Server behind 150 ms: a press on F8, then at once a press on the Name Box, the IME on, kana. Expected: the render naming F8 lands after the press; the first composing key selects the name again, so the Name Box reads かな, not F8かな', states: [
        s('pressed-f8-name-box-k', { press: 'F8' }, { intoNameBox: true }, ON, K('k')), s('a', K('a')), s('n', K('n')), s('a2', K('a')), s('later', { wait: 2000 })] },
    { id: 'd3', what: 'The Name Box: with the keyboard in the grid (D10 selected), a press inside D10 (between 1 and 0) dragged to between D and 1, then released; then Escape. Record the selection after the release. The build selects the whole text at the release of the press that gave the Name Box the keyboard, a drag included', states: [
        s('dragged', { nbdrag: [2, 1] }), s('escape', K('{ESC}'))] },
    { id: 'd4', what: 'D10, the IME off: Escape, Escape (no edit open), a press on a control of the page outside the grid (the Revalue button, the last before the Sheet), Tab back into the grid, then Tab. Expected: the release ended when the keyboard left the grid: the last Tab moves the Focus inside the selection, and does not leave the grid (KB-8)', states: [
        s('escape-1', OFF, K('{ESC}')), s('escape-2', K('{ESC}')), s('pressed-control', { pressOn: '#sheet-revalue' }), s('tab-back', K('{TAB}')), s('tab-again', K('{TAB}'))] },
    { id: 'd4b', what: 'An addition: d4 with the other way back: Escape, Escape, a press on a cell of the positions grid (after the Sheet, display-only), Shift+Tab, then Tab', states: [
        s('escape-1', OFF, K('{ESC}')), s('escape-2', K('{ESC}')), s('pressed-positions', { press: 'A1', grid: POSITIONS }), s('shift-tab-back', K('+{TAB}')), s('tab-again', K('{TAB}'))] },
    { id: 'd4c', what: 'An addition: d4b with Escape in the positions grid before Shift+Tab: Escape, Escape, a press on a cell of the positions grid, Escape there, Shift+Tab back into the Sheet, then Tab', states: [
        s('escape-1', OFF, K('{ESC}')), s('escape-2', K('{ESC}')), s('pressed-positions', { press: 'A1', grid: POSITIONS }), s('escape-in-positions', K('{ESC}')), s('shift-tab-back', K('+{TAB}')), s('tab-again', K('{TAB}'))] },
];

// Part C: the keyboard and the clipboard with the IME off and English (UK). One case per page, its
// states in sequence. Before and after: the element the procedure names on each side of the grid.
const PART_C = (page) => {
    const sheet = page === 'sheet' || page === 'mud';
    const [first, second, below, pasteTo] = sheet ? ['D10', 'D11', 'D12', 'D13'] : ['C1', 'C2', 'C3', 'C4'];
    const before = sheet ? { css: '#sheet-revalue', nth: 0, name: 'the Revalue button' } : { css: 'nav.demo-nav a', nth: 0, name: 'the navigation\'s link' };
    const after = sheet ? { ...POSITIONS, name: 'the positions grid\'s root' } : { ...SECOND, name: 'the second grid\'s root' };
    const displayOnly = sheet ? POSITIONS : SECOND;
    return [{ id: 'pc', extra: true, what: `Part C on ${page}: ${first} pressed; 150, Enter, 200, Enter at full speed; the arrows, Ctrl+arrows, Home, End, PageDown, Ctrl+Home; F2 and Escape; Ctrl+C of ${first} and Ctrl+V into ${pasteTo}; Tab into the grid from the element before it (${before.name}, given focus by script) and Tab again; Shift+Tab from the element after it (${after.name}, given focus by script: a grid of its own, which takes the first Shift+Tab as its own key, so Escape there releases Tab before the Shift+Tab that is read as the procedure's) and Shift+Tab again; a press on the display-only grid. Expected: as at the base, but the keyboard in the Keyboard Field on a grid that edits, the root on a display-only grid; the root's ring after Tab, not after a click (KB-12); Shift+Tab from after the grid lands in it and Shift+Tab again leaves it (A11Y-4)`, states: [
        s('typed', { burst: '150{ENTER}200{ENTER}' }), s('up', K('{UP}')), s('down', K('{DOWN}')), s('right', K('{RIGHT}')), s('left', K('{LEFT}')),
        s('ctrl-down', K('^{DOWN}')), s('ctrl-up', K('^{UP}')), s('ctrl-right', K('^{RIGHT}')), s('ctrl-left', K('^{LEFT}')), s('home', K('{HOME}')), s('end', K('{END}')), s('pagedown', K('{PGDN}')),
        s('ctrl-home', K('^{HOME}')), s('pressed-first', { wait: 700 }, { press: first }), s('f2', K('{F2}')), s('f2-escape', K('{ESC}')), s('ctrl-c', K('^{KEYC}'), { clipboard: true }), s('pressed-paste-to', { wait: 700 }, { press: pasteTo }), s('ctrl-v', K('^{KEYV}')),
        s('focus-before', { focusScript: before }), s('tab-in', K('{TAB}')), s('tab-again', K('{TAB}')),
        s('focus-after', { focusScript: after }), s('shift-tab-claimed', K('+{TAB}')), s('escape-after', K('{ESC}')), s('shift-tab-in', K('+{TAB}')), s('shift-tab-again', K('+{TAB}')),
        s('pressed-display-only', { wait: 700 }, { press: 'A1', grid: displayOnly })] }];
};

const CASES = PAGE.startsWith('features') ? [...FEATURES_A, ...PART_C(PAGE)] : PAGE.startsWith('sheets') ? SHEETS_A : [...PART_A, ...PART_B, ...PART_D, ...PART_C(PAGE)];

// ---- What the page shows -----------------------------------------------------------------------------

// Everything read from the DOM in one state, for the case's grid: which element has the keyboard, the
// Keyboard Field and the root, whether an edit is open, the Focus and the Name Box, the Cell Editor's and
// the Formula Bar's field with its coloured layer, the completion list, Find, the cells the page
// names; every grid on the page in brief; and the events the page saw since the last reading.
const READ = ({ grid: gspec, cells }) => {
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
    const pick = (g) => document.querySelectorAll(g.css)[g.nth ?? 0] ?? null;
    const grids = [...document.querySelectorAll('.ex-grid')];
    const sheet = pick(gspec);
    const field = sheet.querySelector('input.ex-key-field');
    const editor = sheet.querySelector('.ex-viewport input.ex-editor, .ex-viewport .ex-editor input');
    const bar = sheet.querySelector('input.ex-formula-bar-text, .ex-formula-bar-text input');
    const nameBox = sheet.querySelector('input.ex-name-box, .ex-name-box input');
    const find = document.querySelector('.ex-popover-find-body input[type=search], .ex-popover-find-body .mud-ex-grid-find input:not([type=checkbox])');
    const surface = (f) => {
        if (!f) return null;
        const layer = f.previousElementSibling?.classList.contains('ex-reference-text') ? f.previousElementSibling : null;
        const cs = getComputedStyle(f);
        return {
            value: f.value, selection: [f.selectionStart, f.selectionEnd], box: box(f.getBoundingClientRect()),
            scrollLeft: f.scrollLeft, scrollWidth: f.scrollWidth, clientWidth: f.clientWidth,
            shown: f.classList.contains('ex-reference-text-shown'), fill: hex(cs.webkitTextFillColor), color: hex(cs.color),
            layer: layer ? {
                text: layer.getAttribute('data-ex-text'), visibility: getComputedStyle(layer).visibility, box: box(layer.getBoundingClientRect()), scrollLeft: layer.scrollLeft,
                spans: [...layer.querySelectorAll('span')].map((sp) => {
                    const ss = getComputedStyle(sp);
                    return { text: sp.textContent, pointed: sp.classList.contains('ex-reference-pointed'), cls: sp.className, color: hex(ss.color), fill: hex(ss.webkitTextFillColor), background: hex(ss.backgroundColor), box: box(sp.getBoundingClientRect()) };
                }),
            } : null,
        };
    };
    const ae = document.activeElement;
    const roleIn = (g, e) => {
        if (e === g) return 'root';
        if (e.matches('input.ex-key-field')) return 'key-field';
        if (e.matches('.ex-viewport input.ex-editor, .ex-viewport .ex-editor input')) return 'cell';
        if (e.matches('input.ex-formula-bar-text, .ex-formula-bar-text input')) return 'bar';
        if (e.matches('input.ex-name-box, .ex-name-box input')) return 'name-box';
        return `${e.tagName.toLowerCase()}.${typeof e.className === 'string' ? e.className.split(' ')[0] : ''}`;
    };
    const where = (e) => {
        if (!e) return null;
        if (find && e === find) return 'find';
        if (sheet.contains(e)) return roleIn(sheet, e);
        const g = grids.find((x) => x.contains(e));
        if (g) return `grid${grids.indexOf(g)}:${roleIn(g, e)}`;
        return `${e.tagName.toLowerCase()}${e.id ? '#' + e.id : ''}`;
    };
    const idToA1 = (id) => id?.replace(/^.*-r(\d+)c(\d+)$/, (x, r, c) => letters(+c) + (+r + 1)) ?? null;
    const focusId = field?.getAttribute('aria-activedescendant') ?? sheet.getAttribute('aria-activedescendant');
    const focusCell = focusId ? document.getElementById(focusId) : null;
    const cellText = (g, a) => { const m = /^([A-Z]+)(\d+)$/.exec(a); let col = 0; for (const ch of m[1]) col = col * 26 + ch.charCodeAt(0) - 64; return [...g.querySelectorAll(`[id$="-r${Number(m[2]) - 1}c${col - 1}"]`)].map((e) => e.textContent).join('|'); };
    const completion = sheet.querySelector('.ex-completion');
    const outcome = document.querySelector('.ex-find-outcome, .mud-ex-grid-find-outcome');
    const fcs = field ? getComputedStyle(field) : null;
    const rcs = getComputedStyle(sheet);
    const brief = (g, i) => {
        const f = g.querySelector('input.ex-key-field');
        const ed = g.querySelector('.ex-viewport input.ex-editor, .ex-viewport .ex-editor input');
        return { i, in: g.parentElement?.closest('[id]')?.id ?? null, hasKeyboard: g.contains(ae), keyField: f ? { value: f.value, composing: f.classList.contains('ex-key-field-composing'), readOnly: f.readOnly } : null,
            editing: !!ed, editorValue: ed?.value ?? null, focus: idToA1(f?.getAttribute('aria-activedescendant') ?? g.getAttribute('aria-activedescendant')), rootTabIndex: g.getAttribute('tabindex'),
            focusVisibleMark: g.hasAttribute('data-ex-focus-visible'), rootFocusVisible: g.matches(':focus-visible') };
    };
    return {
        active: where(ae),
        editing: !!editor,
        focus: idToA1(focusId),
        keyField: field ? {
            value: field.value, selection: [field.selectionStart, field.selectionEnd], composing: field.classList.contains('ex-key-field-composing'), cls: field.className,
            readOnly: field.readOnly, tabIndex: field.getAttribute('tabindex'), opacity: fcs.opacity, caretColor: hex(fcs.caretColor), outline: `${fcs.outlineStyle} ${fcs.outlineWidth} ${hex(fcs.outlineColor)}`,
            background: hex(fcs.backgroundColor), color: hex(fcs.color), box: box(field.getBoundingClientRect()), layer: field.parentElement?.className ?? null, ariaActiveDescendant: field.getAttribute('aria-activedescendant'),
        } : null,
        focusCellBox: focusCell ? box(focusCell.getBoundingClientRect()) : null,
        root: { tabIndex: sheet.getAttribute('tabindex'), cls: sheet.className, ariaActiveDescendant: sheet.getAttribute('aria-activedescendant'), focusVisibleMark: sheet.hasAttribute('data-ex-focus-visible'),
            matchesFocusVisible: sheet.matches(':focus-visible'), outline: `${rcs.outlineStyle} ${rcs.outlineWidth} ${hex(rcs.outlineColor)}` },
        nameBox: nameBox?.value ?? null, nameBoxField: surface(nameBox),
        cell: surface(editor), bar: surface(bar),
        completion: completion ? [...completion.querySelectorAll('[role=option]')].map((o) => ({ text: o.textContent.trim(), selected: o.getAttribute('aria-selected') === 'true' })) : null,
        hints: [...sheet.querySelectorAll('.ex-argument-hint, [class*="hint"]')].map((h) => ({ cls: h.className, text: h.textContent.trim().slice(0, 200) })),
        outlines: [...sheet.querySelectorAll('.ex-reference-outline')].map((o) => ({ cls: o.className, box: box(o.getBoundingClientRect()) })),
        points: [...sheet.querySelectorAll('.ex-point, .ex-point-dashes')].map((o) => ({ cls: o.className, box: box(o.getBoundingClientRect()) })),
        selected: [...sheet.querySelectorAll('[aria-selected=true][id]')].slice(0, 12).map((e) => idToA1(e.id)),
        sorted: [...sheet.querySelectorAll('[role=columnheader][aria-sort]')].filter((h) => h.getAttribute('aria-sort') !== 'none').map((h) => `${h.textContent.trim()}:${h.getAttribute('aria-sort')}`),
        find: find ? { value: find.value, selection: [find.selectionStart, find.selectionEnd], outcome: outcome?.textContent.trim() ?? null } : null,
        message: [...document.querySelectorAll('.ex-popover-message, .ex-message, [role=alert]')].map((m) => m.textContent.trim().slice(0, 200)).filter(Boolean),
        cells: Object.fromEntries(cells.map((a) => [a, cellText(sheet, a)])),
        grids: grids.map(brief),
        events: (window.__events ?? []).splice(0),
        focusLog: (window.__focusLog ?? []).splice(0),
    };
};

// The pixels of a state: the page's own picture of the grids (and of Find's popover when it is
// open), and, read from it, the colours of the spans of the surface the edit is in, the whole field,
// and the Keyboard Field while it composes.
async function pixels(page, read, clip, file) {
    const shot = await page.screenshot({ clip });
    if (file) fs.writeFileSync(file, shot);
    const boxes = [];
    const surf = read.active === 'bar' ? read.bar : read.cell;
    const lb = surf?.layer?.box;
    for (const sp of surf?.layer?.spans ?? []) {
        const l = Math.max(sp.box.l, lb.l), t = Math.max(sp.box.t, lb.t), r = Math.min(sp.box.l + sp.box.w, lb.l + lb.w), b = Math.min(sp.box.t + sp.box.h, lb.t + lb.h);
        boxes.push({ text: sp.text, box: { l, t, w: Math.max(0, r - l), h: Math.max(0, b - t) } });
    }
    if (surf) boxes.push({ text: '(the whole field)', box: surf.box });
    if (read.keyField?.composing) boxes.push({ text: '(the Keyboard Field)', box: read.keyField.box });
    if (read.focusCellBox) boxes.push({ text: '(the Focus cell)', box: read.focusCellBox });
    return page.evaluate(async ({ a, clip, boxes }) => {
        const bytes = Uint8Array.from(atob(a), (c) => c.charCodeAt(0));
        const bmp = await createImageBitmap(new Blob([bytes], { type: 'image/png' }));
        const cv = new OffscreenCanvas(bmp.width, bmp.height); const ctx = cv.getContext('2d'); ctx.drawImage(bmp, 0, 0);
        const A = { w: bmp.width, h: bmp.height, d: ctx.getImageData(0, 0, bmp.width, bmp.height).data };
        const sc = A.w / clip.width;
        const at = (x, y) => { if (x < 0 || y < 0 || x >= A.w || y >= A.h) return null; const i = (y * A.w + x) * 4; return [A.d[i], A.d[i + 1], A.d[i + 2]]; };
        const hx = (p) => '#' + p.map((v) => v.toString(16).padStart(2, '0')).join('');
        const sat = (p) => Math.max(...p) - Math.min(...p);
        const top = (m, n) => [...m.entries()].sort((x, y) => y[1] - x[1]).slice(0, n).map(([k, v]) => `${k}:${v}`);
        const count = (m, k) => m.set(k, (m.get(k) ?? 0) + 1);
        const sum = (h) => parseInt(h.slice(1, 3), 16) + parseInt(h.slice(3, 5), 16) + parseInt(h.slice(5), 16);
        return { scale: sc, boxes: boxes.map((bx) => {
            const x0 = Math.round((bx.box.l - clip.x) * sc), y0 = Math.round((bx.box.t - clip.y) * sc), x1 = Math.round((bx.box.l + bx.box.w - clip.x) * sc), y1 = Math.round((bx.box.t + bx.box.h - clip.y) * sc);
            const all = new Map(), saturated = new Map();
            for (let y = y0; y < y1; y++) for (let x = x0; x < x1; x++) { const p = at(x, y); if (!p) continue; count(all, hx(p)); if (sat(p) > 60) count(saturated, hx(p)); }
            return { text: bx.text, ground: top(all, 1)[0] ?? null, colours: all.size, saturated: top(saturated, 3), darkest: [...all.keys()].sort((x, y) => sum(x) - sum(y))[0] ?? null };
        }) };
    }, { a: shot.toString('base64'), clip, boxes });
}

// ---- The page ------------------------------------------------------------------------------------------

// The window Alt+Tab goes to, opened before the browser comes to the front, so that it stands next in
// the order Alt+Tab follows (k15). Nothing of the user's is brought forward.
out.altTarget = { open: await input('alttarget open') };
const browser = await chromium.launch({ channel: CHANNEL, headless: false, args: ['--window-position=40,40', '--window-size=1600,1050'] });
out.browserVersion = browser.version();
const context = await browser.newContext({ viewport: null });
const page = await context.newPage();
page.on('console', (m) => out.messages.push({ t: new Date().toISOString(), type: m.type(), text: m.text() }));
page.on('pageerror', (e) => out.messages.push({ t: new Date().toISOString(), type: 'pageerror', text: String(e) }));
await page.goto(`${BASE}${P.path}`);
const at = (address) => { const m = /^([A-Z]+)(\d+)$/.exec(address); let col = 0; for (const ch of m[1]) col = col * 26 + ch.charCodeAt(0) - 64; return { row: Number(m[2]) - 1, col: col - 1 }; };
// Ready: the grid's cell shows its text, and every grid that edits has its Keyboard Field.
await page.waitForFunction(({ ready, all }) => {
    const pick = (g) => document.querySelectorAll(g.css)[g.nth ?? 0] ?? null;
    const g = pick(ready.grid);
    const m = /^([A-Z]+)(\d+)$/.exec(ready.cell); let col = 0; for (const ch of m[1]) col = col * 26 + ch.charCodeAt(0) - 64;
    const t = g && [...g.querySelectorAll(`[id$="-r${Number(m[2]) - 1}c${col - 1}"]`)].map((e) => e.textContent).join('');
    return typeof t === 'string' && t.includes(ready.text) && !!g.querySelector('[id$="-r0c0"]') && all.every((x) => pick(x)?.querySelector('input.ex-key-field'));
}, { ready: P.ready, all: PAGE.startsWith('sheets') ? [LEFT, RIGHT] : [P.grid] }, { timeout: 60_000 });
await page.bringToFront();
await page.evaluate((t) => { document.title = t; }, TITLE);
// The probe's own listeners, in the capture phase on the window: where DOM focus goes, and every
// keydown, composition event and input event the page sees, in order, with the field's value.
await page.evaluate((gspec) => {
    window.__focusLog = []; window.__events = [];
    const name = (t) => {
        if (!(t instanceof Element)) return String(t?.nodeName ?? t);
        const grids = [...document.querySelectorAll('.ex-grid')];
        const main = document.querySelectorAll(gspec.css)[gspec.nth ?? 0];
        const g = grids.find((x) => x.contains(t));
        let role;
        if (t.closest('.ex-popover-find-body')) return 'find';
        if (!g) return `${t.tagName.toLowerCase()}${t.id ? '#' + t.id : ''}`;
        if (t === g) role = 'root';
        else if (t.matches('input.ex-key-field')) role = 'key-field';
        else if (t.matches('.ex-viewport input.ex-editor, .ex-viewport .ex-editor input')) role = 'cell';
        else if (t.matches('input.ex-formula-bar-text, .ex-formula-bar-text input')) role = 'bar';
        else if (t.matches('input.ex-name-box, .ex-name-box input')) role = 'name-box';
        else role = `${t.tagName.toLowerCase()}.${typeof t.className === 'string' ? t.className.split(' ')[0] : ''}`;
        return g === main ? role : `grid${grids.indexOf(g)}:${role}`;
    };
    window.addEventListener('focusin', (e) => { window.__focusLog.push({ t: Math.round(performance.now()), el: name(e.target) }); }, true);
    window.addEventListener('focusout', (e) => { window.__focusLog.push({ t: Math.round(performance.now()), out: name(e.target), to: e.relatedTarget ? name(e.relatedTarget) : null }); }, true);
    for (const type of ['mousedown', 'mouseup', 'click']) {
        window.addEventListener(type, (e) => { window.__events.push({ t: Math.round(performance.now()), type, target: name(e.target), x: Math.round(e.clientX), y: Math.round(e.clientY), button: e.button, detail: e.detail, defaultPrevented: e.defaultPrevented || undefined }); }, true);
    }
    for (const type of ['keydown', 'keyup', 'compositionstart', 'compositionupdate', 'compositionend', 'beforeinput', 'input', 'copy', 'paste']) {
        window.addEventListener(type, (e) => {
            if (type === 'keyup' && e.key !== 'Process' && e.keyCode !== 229) return;   // a keyup only when the IME has it
            const r = { t: Math.round(performance.now()), type, target: name(e.target) };
            if (type === 'keydown' || type === 'keyup') Object.assign(r, { key: e.key, code: e.code, keyCode: e.keyCode, isComposing: e.isComposing, ctrl: e.ctrlKey || undefined, shift: e.shiftKey || undefined, alt: e.altKey || undefined });
            else if (type.startsWith('composition')) r.data = e.data;
            else if (type === 'copy' || type === 'paste') r.text = e.clipboardData?.getData('text/plain')?.slice(0, 80) ?? null;
            else Object.assign(r, { inputType: e.inputType, data: e.data, isComposing: e.isComposing });
            if (e.target instanceof HTMLInputElement) r.value = e.target.value.slice(0, 80);
            r.defaultPrevented = e.defaultPrevented || undefined;
            window.__events.push(r);
        }, true);
    }
}, P.grid);
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

// The accessibility tree's view of the focused element (AX=1), over the DevTools protocol.
let cdp = null;
async function axRead() {
    try {
        if (!cdp) { cdp = await context.newCDPSession(page); await cdp.send('DOM.enable'); await cdp.send('Accessibility.enable'); }
        const { result } = await cdp.send('Runtime.evaluate', { expression: 'document.activeElement' });
        if (!result.objectId) return null;
        const { node } = await cdp.send('DOM.describeNode', { objectId: result.objectId });
        const one = async (backendNodeId) => (await cdp.send('Accessibility.getPartialAXTree', { backendNodeId, fetchRelatives: false })).nodes.find((n) => n.backendDOMNodeId === backendNodeId) ?? null;
        const ax = await one(node.backendNodeId);
        const prop = (n, k) => n?.properties?.find((p) => p.name === k)?.value;
        const r = { tag: node.localName, cls: (node.attributes ?? []).reduce((acc, v, i, arr) => (i % 2 === 0 && v === 'class' ? arr[i + 1] : acc), null), role: ax?.role?.value ?? null, name: ax?.name?.value ?? null, focused: prop(ax, 'focused')?.value ?? null, ignored: ax?.ignored ?? null };
        const ad = prop(ax, 'activedescendant');
        if (ad?.relatedNodes?.length) {
            const rel = ad.relatedNodes[0];
            const n2 = await one(rel.backendDOMNodeId);
            r.activeDescendant = { idref: rel.idref ?? null, role: n2?.role?.value ?? null, name: n2?.name?.value ?? null, selected: prop(n2, 'selected')?.value ?? null };
        } else r.activeDescendant = null;
        return r;
    } catch (e) { return { error: String(e).slice(0, 200) }; }
}

// The clip every picture is taken of: the page's grids, and Find's popover when it is open, with 4 px around.
const clipNow = () => page.evaluate((specs) => {
    const pick = (g) => document.querySelectorAll(g.css)[g.nth ?? 0] ?? null;
    const els = [...specs.map(pick), document.querySelector('.ex-popover-find-body')?.closest('[class*="popover"]')].filter(Boolean);
    const rs = els.map((e) => e.getBoundingClientRect());
    const l = Math.max(0, Math.floor(Math.min(...rs.map((r) => r.left))) - 4), t = Math.max(0, Math.floor(Math.min(...rs.map((r) => r.top))) - 4);
    return { x: l, y: t, width: Math.min(innerWidth, Math.ceil(Math.max(...rs.map((r) => r.right))) + 4) - l, height: Math.min(innerHeight, Math.ceil(Math.max(...rs.map((r) => r.bottom))) + 4) - t };
}, P.clip);

// Where things are, in screen pixels.
async function boxOf(gspec, selector, which = 'centre', dx = 0) {
    const r = await page.evaluate(([gspec, sel, which, dx]) => {
        const g = gspec ? document.querySelectorAll(gspec.css)[gspec.nth ?? 0] : document;
        const e = g?.querySelector(sel); if (!e) return null;
        const b = e.getBoundingClientRect();
        const x = which === 'end' ? b.right - 8 : which === 'at' ? b.left + dx : which === 'third' ? b.left + b.width * 0.35 : (b.left + b.right) / 2;
        return { x, y: (b.top + b.bottom) / 2 };
    }, [gspec, selector, which, dx]);
    if (!r) throw new Error(`${selector} is not on the page`);
    return cal.at(r.x, r.y);
}
const cellSel = (address) => { const a = at(address); return `[id$="-r${a.row}c${a.col}"]`; };
async function until(fn, arg, ms = 5000, step = 50) {
    const t0 = Date.now();
    while (Date.now() - t0 < ms) { const v = await page.evaluate(fn, arg); if (v) return Date.now() - t0; await sleep(step); }
    return null;
}
const stateFn = (gspec) => {
    const g = document.querySelectorAll(gspec.css)[gspec.nth ?? 0];
    const f = g.querySelector('input.ex-key-field');
    return { editing: !!g.querySelector('.ex-viewport input.ex-editor, .ex-viewport .ex-editor input'), completion: !!g.querySelector('.ex-completion'),
        find: !!document.querySelector('.ex-popover-find-body'), sheetHasFocus: g.contains(document.activeElement), composing: !!f?.classList.contains('ex-key-field-composing'),
        inNameBox: document.activeElement?.matches?.('input.ex-name-box, .ex-name-box input') ?? false, focus: f?.getAttribute('aria-activedescendant') ?? g.getAttribute('aria-activedescendant') };
};
const state = () => page.evaluate(stateFn, P.grid);

// No edit open and the IME off. Escape until the grid says no edit, no list, no Find and no
// composition is open, and the keyboard is not left in the Name Box. Then, if the IME is still on,
// VK_IME_OFF; and if that leaves it on, F2 opens an edit, VK_IME_OFF switches the IME off there, and
// Escape closes the edit (the fifteenth run's way, when the keyboard was on the root).
async function reset() {
    let escapes = 0;
    const s0 = await state();
    if (s0.editing || s0.completion || s0.find || s0.inNameBox || s0.composing) await input('imeoff');
    for (let i = 0; i < 8; i++) {
        const st = await state();
        if (!st.editing && !st.completion && !st.find && !st.inNameBox && !st.composing) break;
        await input('type 0 {ESC}'); escapes++; await sleep(RTT ? 500 : 250);
    }
    let ime = await input(`imestate ${TITLE}`);
    let offByKey = false, offInAnEdit = false;
    if (/open=True/.test(ime)) {
        await input('imeoff'); await sleep(200); offByKey = true;
        ime = await input(`imestate ${TITLE}`);
    }
    if (/open=True/.test(ime) && SHEETLIKE) {
        await input('type 0 {F2}'); await sleep(RTT ? 600 : 300);
        await input('imeoff'); await sleep(200);
        await input('type 0 {ESC}'); await sleep(RTT ? 600 : 300);
        offInAnEdit = true;
        ime = await input(`imestate ${TITLE}`);
    }
    return { escapes, offByKey, offInAnEdit, ime };
}
// The target has the Focus, alone, and no edit is open. A press on it while it has the Focus, soon
// after the last, would be a double-click and open an edit: it is pressed only when the Focus is
// elsewhere, the selection is more than it, or the keyboard is not the grid's.
async function selectTarget() {
    const t = at(TARGET);
    const ready = () => page.evaluate(([gspec, t, name, sheetlike]) => {
        const g = document.querySelectorAll(gspec.css)[gspec.nth ?? 0];
        const f = g.querySelector('input.ex-key-field');
        const nb = g.querySelector('input.ex-name-box, .ex-name-box input');
        const focus = f?.getAttribute('aria-activedescendant') ?? g.getAttribute('aria-activedescendant') ?? '';
        const selected = g.querySelectorAll('[aria-selected=true][id]').length;
        return new RegExp(`-r${t.row}c${t.col}$`).test(focus) && g.contains(document.activeElement) && selected <= 1
            && !document.activeElement.matches('input.ex-name-box, .ex-name-box input')
            && !g.querySelector('.ex-viewport input.ex-editor, .ex-viewport .ex-editor input') && (!sheetlike || nb?.value === name);
    }, [P.grid, t, TARGET, SHEETLIKE]);
    if (await ready()) return 0;
    const [x, y] = await boxOf(P.grid, cellSel(TARGET));
    await input(`click ${x} ${y}`);
    const t0 = Date.now();
    while (Date.now() - t0 < 5000) { if (await ready()) return Date.now() - t0; await sleep(50); }
    throw new Error(`${TARGET} did not take the Focus: ${JSON.stringify(await state())}`);
}
// The cells a case writes hold nothing before the next: D10:F13 cleared with one Delete when any holds
// something (a press on D10, a Shift+press on F13, Delete; the next press on D10 makes it the selection).
async function cleanTargets() {
    if (!SHEETLIKE) return [];
    const texts = await page.evaluate(([gspec, sels]) => {
        const g = document.querySelectorAll(gspec.css)[gspec.nth ?? 0];
        return sels.map((sel) => [...g.querySelectorAll(sel)].map((e) => e.textContent).join(''));
    }, [P.grid, ['D10', 'D11', 'D12', 'D13', 'E10', 'E11', 'E12', 'E13', 'F10', 'F11', 'F12', 'F13'].map(cellSel)]);
    const held = texts.map((t, i) => [['D10', 'D11', 'D12', 'D13', 'E10', 'E11', 'E12', 'E13', 'F10', 'F11', 'F12', 'F13'][i], t]).filter(([, t]) => t !== '');
    if (held.length === 0) return [];
    const [x1, y1] = await boxOf(P.grid, cellSel('D10')); const [x2, y2] = await boxOf(P.grid, cellSel('F13'));
    await input(`click ${x1} ${y1}`); await sleep(RTT ? 700 : 350);
    await input(`shiftclick ${x2} ${y2}`); await sleep(RTT ? 700 : 350);
    await input('type 0 {DEL}'); await sleep(RTT ? 700 : 350);
    const [x3, y3] = await boxOf(P.grid, cellSel('D12'));
    await input(`click ${x3} ${y3}`); await sleep(RTT ? 700 : 350);
    return [`${held.map(([a, t]) => `${a} held ${JSON.stringify(t)}`).join(', ')}: D10:F13 cleared with Delete`];
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
    if (st.burst !== undefined) { const r = await input(`burst ${st.burst}`); return `keys ${st.burst} at full speed (every key event in one SendInput call: ${r})`; }
    if (st.ime) { await input(st.ime === 'on' ? 'imeon' : 'imeoff'); return `the IME ${st.ime} (${st.ime === 'on' ? 'VK_IME_ON' : 'VK_IME_OFF'})`; }
    if (st.press) { const g = st.grid ?? P.grid; const [x, y] = await boxOf(g, cellSel(st.press)); await input(`click ${x} ${y}`); return `a press at (${x}, ${y}), the middle of ${g.name}'s ${st.press}`; }
    if (st.pressAt) { const [x, y] = await boxOf(P.grid, cellSel(st.pressAt), 'at', st.dx); await input(`click ${x} ${y}`); return `a press at (${x}, ${y}), ${st.dx} CSS px right of ${st.pressAt}'s left edge and half way down`; }
    if (st.header) {
        const r = await page.evaluate(([gspec, text]) => {
            const h = [...document.querySelectorAll(gspec.css)[gspec.nth ?? 0].querySelectorAll('[role=columnheader].ex-header-cell')].find((x) => x.textContent.trim() === text);
            if (!h) return null;
            const b = h.getBoundingClientRect(); return { x: b.left + b.width * 0.35, y: (b.top + b.bottom) / 2 };
        }, [P.grid, st.header]);
        if (!r) throw new Error(`no header ${st.header}`);
        const [hx, hy] = cal.at(r.x, r.y);
        await input(`click ${hx} ${hy}`); return `a press at (${hx}, ${hy}), on column ${st.header}'s header, a third of the way across (clear of its menu button)`;
    }
    if (st.intoBar) { const [x, y] = await boxOf(P.grid, 'input.ex-formula-bar-text, .ex-formula-bar-text input', 'end'); await input(`click ${x} ${y}`); return `a press at (${x}, ${y}), 8 px inside the right end of the Formula Bar's field`; }
    if (st.intoNameBox) { const [x, y] = await boxOf(P.grid, 'input.ex-name-box, .ex-name-box input'); await input(`click ${x} ${y}`); return `a press at (${x}, ${y}), the middle of the Name Box`; }
    if (st.nbdrag) {
        // Character boundaries of the Name Box's text, from the field's own font (a canvas measures
        // the text; no layout is changed), its padding and border, and its scroll.
        const r = await page.evaluate(([gspec, marks]) => {
            const g = document.querySelectorAll(gspec.css)[gspec.nth ?? 0];
            const f = g.querySelector('input.ex-name-box, .ex-name-box input');
            const cs = getComputedStyle(f); const b = f.getBoundingClientRect();
            const ctx = new OffscreenCanvas(10, 10).getContext('2d'); ctx.font = `${cs.fontStyle} ${cs.fontWeight} ${cs.fontSize} ${cs.fontFamily}`;
            const x0 = b.left + parseFloat(cs.borderLeftWidth) + parseFloat(cs.paddingLeft) - f.scrollLeft;
            const left = cs.textAlign === 'center' ? x0 + ((b.width - parseFloat(cs.borderLeftWidth) - parseFloat(cs.paddingLeft) - parseFloat(cs.paddingRight) - parseFloat(cs.borderRightWidth)) - ctx.measureText(f.value).width) / 2 : x0;
            return { value: f.value, align: cs.textAlign, xs: marks.map((m) => left + ctx.measureText(f.value.slice(0, m)).width), y: (b.top + b.bottom) / 2 };
        }, [P.grid, st.nbdrag]);
        const [x1, y1] = cal.at(r.xs[0], r.y); const [x2, y2] = cal.at(r.xs[1], r.y);
        await input(`drag ${x1} ${y1} ${x2} ${y2} 8`);
        return `a press in the Name Box at (${x1}, ${y1}), after ${st.nbdrag[0]} characters of ${JSON.stringify(r.value)}, dragged to (${x2}, ${y2}), after ${st.nbdrag[1]}, in 8 steps, and released`;
    }
    if (st.pressOn) { const [x, y] = await boxOf(null, st.pressOn); await input(`click ${x} ${y}`); return `a press at (${x}, ${y}), the middle of ${st.pressOn}`; }
    if (st.focusScript) { const w = await page.evaluate((g) => { const e = document.querySelectorAll(g.css)[g.nth ?? 0]; e.focus(); return document.activeElement === e; }, st.focusScript); return `focus given by script to ${st.focusScript.name} (${st.focusScript.css}): ${w ? 'it holds focus' : 'it did not take focus'}`; }
    if (st.alttab) { const r = await input('alttab'); return `Alt+Tab: ${r}`; }
    if (st.clipboard) { await sleep(300); const r = await input('clipboard'); return `the clipboard read: ${r}`; }
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
    const read = await page.evaluate(READ, { grid: P.grid, cells: P.cells });
    const ime = await input(`imestate ${TITLE}`);
    const front = await input('foreground');
    const ax = AX ? await axRead() : undefined;
    const clip = await clipNow();
    const file = SHOTS ? path.join(SHOTS, `${PAGE}-${caseId}-${stateName}-${LABEL}-${CHANNEL}.png`) : null;
    const px = await pixels(page, read, clip, file);
    return { state: stateName, did, readAtMs: waited, settled, ime, front, ax, read, clip, pixels: px, shot: file ? path.basename(file) : null, tookMs: Date.now() - t0 };
}

async function runCase(c) {
    const rec = { case: c.id, what: c.what, started: new Date().toISOString(), states: [] };
    if (!c.noSelect) {
        rec.before = await reset();
        rec.focusMs = await selectTarget();
        rec.targetsBefore = await cleanTargets();
        if (rec.targetsBefore.length) rec.focusMs2 = await selectTarget();
    }
    await sleep(300);
    rec.beforeRead = await page.evaluate(READ, { grid: P.grid, cells: P.cells });
    rec.imeBefore = await input(`imestate ${TITLE}`);
    if (AX) rec.axBefore = await axRead();
    // A step that fails ends the case, and keeps every state read before it.
    for (const st of c.states) {
        const did = [];
        try { for (const step of st.steps) did.push(await doStep(step)); }
        catch (e) { rec.error = `state ${st.state}: ${String(e)}`; rec.failedDid = did; break; }
        rec.states.push(await take(c.id, st.state, did));
    }
    if (!c.noReset) {
        rec.after = await reset();
        await sleep(300);
        rec.afterRead = await page.evaluate(READ, { grid: P.grid, cells: P.cells });
        if (SHEETLIKE) {
            // What D10 holds as its Entry: the Formula Bar's text with D10 selected.
            await selectTarget(); await sleep(RTT ? 600 : 300);
            rec.d10Entry = await page.evaluate((gspec) => document.querySelectorAll(gspec.css)[gspec.nth ?? 0].querySelector('input.ex-formula-bar-text, .ex-formula-bar-text input')?.value ?? null, P.grid);
        }
    }
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
    out.altTarget.closed = await input('alttarget close').catch((e) => String(e));
    out.finished = new Date().toISOString();
    save();
    helper.stdin.write('quit\n');
    await browser.close();
}
console.log(JSON.stringify({ page: PAGE, label: LABEL, channel: CHANNEL, cases: out.cases.length, errors: out.cases.filter((c) => c.error).length, messages: out.messages.length, browser: out.browserVersion }));
