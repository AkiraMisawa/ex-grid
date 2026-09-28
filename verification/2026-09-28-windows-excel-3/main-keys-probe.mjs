// verify-on-windows-3.md Part C, "What main brought, beside Excel": ExSheet's side of Delete and
// Ctrl+Z, Backspace, Ctrl+D, Ctrl+R, Ctrl+Enter and Ctrl+F on /sheet, the same steps
// main-keys.ps1 takes in Excel. Run on Windows from a copy of tests/ExGrid.Browser (for its
// node_modules), against one host, on one browser:
//
//     set EXGRID_CHANNEL=msedge & set SHOTS=C:\...\shots & node main-keys-probe.mjs http://localhost:6298
//
// Every case opens /sheet afresh, and waits as the suite's fixture does: #demo-interactive
// attached, then no grid aria-busy. Keys and clicks go at a person's pace (150 ms), as
// sheet-vs-excel.spec.mjs does. Each step reads the Name Box (the active cell), the live region
// (the Selection, ADR-0033), aria-activedescendant and the cells it names as shown; each case
// ends by reading the Entries through the Formula Bar. Prints one JSON document.
import { chromium } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

const BASE = process.argv[2] ?? 'http://localhost:5299';
const CHANNEL = process.env.EXGRID_CHANNEL ?? 'chrome';
const SHOTS = process.env.SHOTS;
const HOST = BASE.endsWith(':6298') || BASE.endsWith(':5298') ? 'server' : 'wasm';
const PACE_MS = 150;

const browser = await chromium.launch({ channel: CHANNEL, headless: false });
const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
const out = { channel: CHANNEL, base: BASE, host: HOST, cases: {} };
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
    // A1 opens as 'Item'; wait for it, as sheet-vs-excel.spec.mjs does.
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
        const file = `C-M${name}-exsheet-${HOST}-${CHANNEL}.png`;
        await grid.screenshot({ path: path.join(SHOTS, file) });
        return `shots/${file}`;
    }
    async function typeInto(a1, typed) { await click(a1); await page.keyboard.type(typed); await page.keyboard.press('Enter'); }
    return { page, grid, cell, click, state, entries, shot, typeInto, nameBox };
}

const BLOCK = ['B2', 'C2', 'D2', 'B3', 'C3', 'D3', 'B4', 'C4', 'D4', 'B5', 'D5'];
const cases = {
    async 1({ page, click, state, shot }) {
        const steps = [];
        await click('B2'); await click('D4', ['Shift']);
        steps.push({ ...(await state('B2:D4 selected', BLOCK)), shot: await shot('1-B2-D4-selected') });
        await page.keyboard.press('Delete');
        steps.push({ ...(await state('Delete', BLOCK)), shot: await shot('1-Delete') });
        await page.keyboard.press('Control+z');
        steps.push({ ...(await state('then Ctrl+Z', BLOCK)), shot: await shot('1-then-Ctrl-Z') });
        return { steps };
    },
    async 2({ page, click, state, shot }) {
        const steps = [];
        await click('B3');
        steps.push({ ...(await state('B3 selected', ['B3', 'B5'])), shot: await shot('2-B3-selected') });
        await page.keyboard.press('Backspace');
        steps.push({ ...(await state('Backspace, before Enter', ['B3', 'B5'])), shot: await shot('2-Backspace-before-Enter') });
        await page.keyboard.press('Enter');
        steps.push({ ...(await state('then Enter', ['B3', 'B5'])), shot: await shot('2-then-Enter') });
        return { steps };
    },
    async 3({ page, click, state, shot, entries, typeInto }) {
        const steps = [];
        await click('A2');
        for (const v of ['1', '2', '3', '4']) { await page.keyboard.type(v); await page.keyboard.press('Enter'); }
        await typeInto('B2', '=A2*2');
        await click('B2'); await click('B5', ['Shift']);
        const cells = ['A2', 'A3', 'A4', 'A5', 'B2', 'B3', 'B4', 'B5'];
        steps.push({ ...(await state('B2:B5 selected, B2 =A2*2', cells)), shot: await shot('3-B2-B5-selected') });
        await page.keyboard.press('Control+d');
        steps.push({ ...(await state('Ctrl+D', cells)), shot: await shot('3-Ctrl-D') });
        return { steps, entries: await entries(['B2', 'B3', 'B4', 'B5']) };
    },
    async 4({ page, click, state, shot, entries, typeInto }) {
        const steps = [];
        await click('B1');
        for (const v of ['10', '20', '30']) { await page.keyboard.type(v); await page.keyboard.press('Tab'); }
        await page.keyboard.type('40'); await page.keyboard.press('Enter');
        await typeInto('B2', '=B1+1');
        await click('B2'); await click('E2', ['Shift']);
        const cells = ['B1', 'C1', 'D1', 'E1', 'B2', 'C2', 'D2', 'E2'];
        steps.push({ ...(await state('B2:E2 selected, B2 =B1+1', cells)), shot: await shot('4-B2-E2-selected') });
        await page.keyboard.press('Control+r');
        steps.push({ ...(await state('Ctrl+R', cells)), shot: await shot('4-Ctrl-R') });
        return { steps, entries: await entries(['B2', 'C2', 'D2', 'E2']) };
    },
    async 5({ page, click, state, shot, entries }) {
        const steps = [];
        await click('B2'); await click('C3', ['Shift']);
        steps.push({ ...(await state('B2:C3 selected', ['B2', 'C2', 'B3', 'C3'])), shot: await shot('5-B2-C3-selected') });
        await page.keyboard.type('=A1');
        await page.keyboard.press('Control+Enter');
        steps.push({ ...(await state('typed =A1, Ctrl+Enter', ['A1', 'B1', 'A2', 'B2', 'C2', 'B3', 'C3'])), shot: await shot('5-Ctrl-Enter') });
        return { steps, entries: await entries(['B2', 'C2', 'B3', 'C3']) };
    },
    async 6({ page, grid, cell, click, state, shot, nameBox }) {
        const steps = [];
        // A5000 by the Name Box, as a user reaches it; typed there; then Ctrl+Home.
        await nameBox.click();
        await page.keyboard.press('Control+a');
        await page.keyboard.type('A5000');
        await page.keyboard.press('Enter');
        await page.waitForTimeout(500);
        await page.keyboard.type('needle');
        await page.keyboard.press('Enter');
        await page.keyboard.press('Control+Home');
        steps.push({ ...(await state('A5000 holds needle; Ctrl+Home', ['A1'])), a5000Painted: (await cell('A5000').count()) > 0, shot: await shot('6-Ctrl-Home') });
        await page.keyboard.press('Control+f');
        await page.keyboard.type('needle');
        await page.keyboard.press('Enter');
        await page.waitForTimeout(800);
        const panel = grid.locator('.ex-popover-find');
        const inView = async () => {
            if (!(await cell('A5000').count())) return 'not painted';
            const c = await cell('A5000').boundingBox();
            const v = await grid.locator('.ex-viewport').boundingBox();
            return c && v && c.y >= v.y && c.y + c.height <= v.y + v.height ? 'whole in the viewport' : JSON.stringify({ c, v });
        };
        steps.push({
            ...(await state('Ctrl+F, needle, Enter', ['A5000'])),
            panelVisible: await panel.isVisible().catch(() => false),
            panelText: (await panel.innerText().catch(() => '')).trim(),
            panelField: await panel.locator('input').first().inputValue().catch(() => null),
            a5000: await inView(),
            shot: await shot('6-Ctrl-F-needle-Enter'),
        });
        await page.keyboard.press('Escape');
        steps.push({ ...(await state('then Escape', ['A5000'])), panelVisible: await panel.isVisible().catch(() => false), a5000: await inView(), shot: await shot('6-then-Escape') });
        return { steps };
    },
};

for (const id of (process.env.CASES ?? '1,2,3,4,5,6').split(',')) {
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
