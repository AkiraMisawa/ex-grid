// verify-on-windows-4.md Part C, ExSheet's side: Ctrl+Enter over B2:C3 from B2 (the third run's
// main-keys-probe.mjs case 5, unchanged), and from C3 and from B3 (Part B item 2's two cases); a
// typed entry the engine refuses (-B2 C2); and column widths (SH-26). Run on Windows from a copy of
// tests/ExGrid.Browser (for its node_modules), against one host, on one browser:
//
//     set EXGRID_CHANNEL=msedge & set SHOTS=C:\...\shots & node exsheet-probe.mjs http://localhost:6298
//
// As main-keys-probe.mjs: every case opens /sheet afresh and waits as the suite's fixture does
// (#demo-interactive attached, then no grid aria-busy); keys and clicks go at a person's pace
// (150 ms). Each step reads the Name Box, the live region, aria-activedescendant, the editor, the
// Formula Bar and the cells it names as shown; the Ctrl+Enter cases end by reading the Entries
// through the Formula Bar. Prints one JSON document.
import { chromium } from '@playwright/test';
import path from 'node:path';

const BASE = process.argv[2] ?? 'http://localhost:5299';
const CHANNEL = process.env.EXGRID_CHANNEL ?? 'chrome';
const SHOTS = process.env.SHOTS;
const HOST = BASE.endsWith(':6298') || BASE.endsWith(':5298') ? 'server' : 'wasm';
const PACE_MS = 150;

const browser = await chromium.launch({ channel: CHANNEL, headless: false });
const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
const out = { channel: CHANNEL, base: BASE, host: HOST, browserVersion: browser.version(), cases: {} };
const messages = [];

function parse(a1) {
    const [, letters, digits] = /^([A-Z]+)(\d+)$/.exec(a1);
    let column = 0;
    for (const ch of letters) column = column * 26 + (ch.charCodeAt(0) - 64);
    return { row: Number(digits) - 1, column: column - 1 };
}

async function open() {
    const page = await context.newPage();
    page.on('console', (m) => { if (m.type() === 'error' || m.type() === 'warning') messages.push(`${m.type()}: ${m.text()}`); });
    page.on('pageerror', (e) => messages.push(`pageerror: ${e}`));
    for (const name of ['press', 'type']) {
        const act = page.keyboard[name].bind(page.keyboard);
        page.keyboard[name] = async (...args) => { await act(...args); await page.waitForTimeout(PACE_MS); };
    }
    await page.goto(`${BASE}/sheet`);
    await page.locator('#demo-interactive').waitFor({ state: 'attached', timeout: 60_000 });
    await page.waitForFunction(() => !document.querySelector('.ex-grid[aria-busy]'), null, { timeout: 60_000 });
    const grid = page.locator('.ex-grid').first();
    await grid.locator("[id$='-r0c0']").waitFor();
    const cell = (a1) => { const { row, column } = parse(a1); return grid.locator(`[id$='-r${row}c${column}']`); };
    const nameBox = grid.locator('input.ex-name-box');
    const bar = grid.locator('input.ex-formula-bar-text');
    const editor = grid.locator('input.ex-editor:not(.ex-formula-bar-text)');
    await cell('A1').filter({ hasText: 'Item' }).waitFor();

    async function click(a1, modifiers = []) {
        await cell(a1).waitFor({ state: 'visible', timeout: 10_000 });
        await cell(a1).click({ force: true, modifiers });
        await page.waitForTimeout(PACE_MS + 150);
    }
    async function shown(a1) {
        const c = cell(a1);
        return (await c.count()) ? (await c.innerText()).trim() : null;
    }
    async function state(step, cells = []) {
        await page.waitForTimeout(400);
        const s = {
            step,
            nameBox: await nameBox.inputValue(),
            selection: (await grid.locator('.ex-announce').innerText().catch(() => '')).trim(),
            activeDescendant: ((await grid.getAttribute('aria-activedescendant')) ?? '').replace(/^.*-(r\d+c\d+)$/, '$1'),
            editor: (await editor.count()) ? await editor.inputValue() : null,
            bar: await bar.inputValue(),
            cells: {},
        };
        for (const a of cells) s.cells[a] = await shown(a);
        return s;
    }
    async function entries(cells) {
        const e = {};
        for (const a of cells) { await click(a); e[a] = await bar.inputValue(); }
        return e;
    }
    async function shot(name) {
        if (!SHOTS) return null;
        const file = `C-${name}-exsheet-${HOST}-${CHANNEL}.png`;
        await grid.screenshot({ path: path.join(SHOTS, file) });
        return `shots/${file}`;
    }
    // Whatever the page shows as a message: popovers, alerts, invalid fields, titles on the editor.
    async function shownMessages() {
        return page.evaluate(() => {
            const seen = [];
            const visible = (el) => { const r = el.getBoundingClientRect(); const cs = getComputedStyle(el); return r.width > 0 && r.height > 0 && cs.visibility !== 'hidden' && cs.display !== 'none'; };
            for (const el of document.querySelectorAll('[class*="ex-popover"], [role=alert], [role=status], [role=tooltip], [aria-invalid=true], [popover], .ex-announce')) {
                if (!visible(el) && !el.classList.contains('ex-announce')) continue;
                seen.push({ tag: el.tagName.toLowerCase(), cls: el.className, role: el.getAttribute('role'), ariaInvalid: el.getAttribute('aria-invalid'),
                    title: el.getAttribute('title'), describedBy: el.getAttribute('aria-describedby'), text: (el.innerText ?? '').trim().slice(0, 400), visible: visible(el) });
            }
            const ed = document.querySelector('input.ex-editor:not(.ex-formula-bar-text)');
            const desc = ed?.getAttribute('aria-describedby');
            return { messages: seen, editorDescribedBy: desc ? desc.split(' ').map((id) => (document.getElementById(id)?.innerText ?? '').trim()) : null,
                activeElement: document.activeElement ? `${document.activeElement.tagName.toLowerCase()}.${document.activeElement.className}` : null };
        });
    }
    const header = (letter) => grid.locator('.ex-header-cell', { hasText: new RegExp(`^${letter}$`) }).first();
    async function width(letter) { const b = await header(letter).boundingBox(); return b ? Math.round(b.width * 100) / 100 : null; }
    return { page, grid, cell, click, state, entries, shot, shownMessages, header, width };
}

const BLOCK = ['B2', 'C2', 'B3', 'C3'];
const cases = {
    // The third run's case 5, unchanged: =A1 over B2:C3 from B2.
    async 'ctrl-enter-from-B2'({ page, click, state, shot, entries }) {
        const steps = [];
        await click('B2'); await click('C3', ['Shift']);
        steps.push({ ...(await state('B2:C3 selected', BLOCK)), shot: await shot('M5-B2-C3-selected') });
        await page.keyboard.type('=A1');
        await page.keyboard.press('Control+Enter');
        steps.push({ ...(await state('typed =A1, Ctrl+Enter', ['A1', 'B1', 'A2', 'B2', 'C2', 'B3', 'C3'])), shot: await shot('M5-Ctrl-Enter') });
        return { steps, entries: await entries(BLOCK) };
    },
    // Part B item 2 in ExSheet: C3, Shift+click B2, =B2+$A$1, Ctrl+Enter.
    async 'ctrl-enter-from-C3'({ page, click, state, shot, entries }) {
        const steps = [];
        await click('C3'); await click('B2', ['Shift']);
        steps.push({ ...(await state('C3, Shift+click B2', BLOCK)), shot: await shot('B2-from-C3-selected') });
        await page.keyboard.type('=B2+$A$1');
        await page.keyboard.press('Control+Enter');
        steps.push({ ...(await state('typed =B2+$A$1, Ctrl+Enter', BLOCK)), shot: await shot('B2-from-C3-Ctrl-Enter') });
        return { steps, entries: await entries(BLOCK) };
    },
    // And from B3, reached by Enter inside B2:C3.
    async 'ctrl-enter-from-B3'({ page, click, state, shot, entries }) {
        const steps = [];
        await click('B2'); await click('C3', ['Shift']);
        await page.keyboard.press('Enter');
        steps.push({ ...(await state('B2, Shift+click C3, Enter', BLOCK)), shot: await shot('B2-from-B3-selected') });
        await page.keyboard.type('=B2+$A$1');
        await page.keyboard.press('Control+Enter');
        steps.push({ ...(await state('typed =B2+$A$1, Ctrl+Enter', BLOCK)), shot: await shot('B2-from-B3-Ctrl-Enter') });
        return { steps, entries: await entries(BLOCK) };
    },
    // A typed entry the engine refuses: -B2 C2 into F2, then Enter.
    async refused({ page, click, state, shot, shownMessages }) {
        const steps = [];
        await click('F2');
        await page.keyboard.type('-B2 C2');
        steps.push({ ...(await state('F2, typed -B2 C2', ['F2'])), shot: await shot('R-typed') });
        await page.keyboard.press('Enter');
        await page.waitForTimeout(600);
        steps.push({ ...(await state('then Enter', ['F2', 'F3'])), ...(await shownMessages()), shot: await shot('R-then-Enter') });
        await page.screenshot({ path: SHOTS ? path.join(SHOTS, `C-R-then-Enter-page-exsheet-${HOST}-${CHANNEL}.png`) : undefined });
        await page.keyboard.press('Escape');
        steps.push({ ...(await state('then Escape', ['F2'])), ...(await shownMessages()), shot: await shot('R-then-Escape') });
        return { steps };
    },
    // Column widths (SH-26): F is empty in /sheet.
    async widths({ page, click, state, shot, width, header, cell }) {
        const steps = [];
        const cells = ['F2', 'F3', 'F4'];
        const w = async (step) => ({ ...(await state(step, cells)), widthF: await width('F'), widthE: await width('E'),
            f2Overflows: await cell('F2').evaluate((el) => el.scrollWidth > el.clientWidth).catch(() => null) });
        steps.push({ ...(await w('empty column F')), shot: await shot('W-empty') });
        await click('F2'); await page.keyboard.type('1234567890'); await page.keyboard.press('Enter');
        steps.push({ ...(await w('typed 1234567890 in F2')), shot: await shot('W-F2') });
        await page.keyboard.type('12345678901'); await page.keyboard.press('Enter');
        steps.push({ ...(await w('typed 12345678901 in F3')), shot: await shot('W-F3') });
        const grip = await header('F').locator('.ex-resize-grip').boundingBox();
        const x = grip.x + grip.width / 2, y = grip.y + grip.height / 2;
        await page.mouse.move(x, y); await page.mouse.down();
        await page.mouse.move(x - 27, y, { steps: 6 });   // about 40 screen pixels at 150%
        await page.mouse.up();
        await page.waitForTimeout(500);
        steps.push({ ...(await w('F dragged 27 CSS px narrower')), shot: await shot('W-dragged') });
        await click('F4'); await page.keyboard.type('123456789012'); await page.keyboard.press('Enter');
        steps.push({ ...(await w('typed 123456789012 in F4')), shot: await shot('W-F4') });
        return { steps };
    },
};

for (const id of (process.env.CASES ?? 'ctrl-enter-from-B2,ctrl-enter-from-C3,ctrl-enter-from-B3,refused,widths').split(',')) {
    const t0 = Date.now();
    let h;
    try {
        h = await open();
        out.cases[id] = { ...(await cases[id](h)), ms: Date.now() - t0 };
    } catch (e) {
        out.cases[id] = { error: String(e).slice(0, 600), ms: Date.now() - t0 };
    }
    await h?.page.close();
}
out.console = messages;
await browser.close();
console.log(JSON.stringify(out, null, 2));
