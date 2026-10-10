// The Fluxor spike's browser check: W1 (the store pushes the Window), W2 (the store's list handed to
// GridSource.From, writes through the store) and W2src (writes through the source, then the store),
// on the WebAssembly host (5899) or the Server host (5898). Headless Chrome, as EXGRID_HEADLESS asks
// of layer 3 on a Mac; nothing here is about scrollbars.
//
//   node check.mjs host=wasm|server [variants=w1,w2,w2src] [checks=1,2,3,4,5,6,7]
//
// Every wait reads what it waits for (AGENTS.md principle 6): `until` polls a reading until it holds,
// and its timeout is a ceiling on a failure, never a pause. Each check opens a context of its own,
// so on Server each has its own circuit and store, and on WebAssembly its own app.
import { chromium } from 'playwright';
import fs from 'node:fs';

const args = Object.fromEntries(process.argv.slice(2).map((a) => a.split('=')));
const host = args.host ?? 'wasm';
const base = args.base ?? (host === 'wasm' ? 'http://localhost:5899' : 'http://localhost:5898');
const variants = (args.variants ?? 'w1,w2,w2src').split(',');
const checks = (args.checks ?? '1,2,3,4,5,6').split(',').map(Number);
const PATH = { w1: '/w1', w2: '/w2', w2src: '/w2?edits=source', w2g0: '/w2?gather=0' };
const C = { renders: 0, id: 1, book: 2, ccy: 3, date: 4, notional: 5, price: 6, pnl: 7 };
const KEY_FIELD = ':scope > .ex-scroller > .ex-spacer > .ex-viewport > .ex-key-field-layer > input.ex-key-field';

const log = (...a) => console.log(`[${host}]`, ...a);

async function until(read, what, timeout = 20_000) {
    const start = Date.now();
    let last;
    while (Date.now() - start < timeout) {
        last = await read();
        if (last) return last;
        await new Promise((r) => setTimeout(r, 20));
    }
    throw new Error(`timed out waiting for ${what}; last reading ${JSON.stringify(last)}`);
}

function url(variant, query) {
    const path = PATH[variant];
    return base + path + (query ? (path.includes('?') ? '&' : '?') + query : '');
}

async function open(browser, variant, query = '') {
    const context = await browser.newContext({ viewport: { width: 1300, height: 1000 } });
    await context.grantPermissions(['clipboard-read', 'clipboard-write'], { origin: base });
    const page = await context.newPage();
    const problems = [];
    page.on('console', (m) => {
        if (m.type() === 'error' || m.type() === 'warning') problems.push(`${m.type()}: ${m.text()}`);
    });
    page.on('pageerror', (e) => problems.push(`pageerror: ${e.message}`));
    await page.goto(url(variant, query));
    await page.locator('#fg-interactive').waitFor({ state: 'attached', timeout: 60_000 });
    const grid = page.locator('.ex-grid');
    await grid.locator('.ex-row').first().waitFor({ timeout: 60_000 });
    await until(() => grid.evaluate((root, path) => root.querySelector(path)?.getAttribute('tabindex') === '0', KEY_FIELD), 'the grid takes its tab stop');
    return { context, page, grid, problems };
}

const cell = (grid, row, column) => grid.locator(`[id$='-r${row}c${column}']`);
const textAt = async (grid, row, column) => ((await cell(grid, row, column).textContent()) ?? '').trim();
const status = async (page, id) => ((await page.textContent(`#${id}`)) ?? '').trim();
const isMarked = (grid, row, column) => cell(grid, row, column).evaluate((el) => el.classList.contains('ex-changed'));

async function clickCell(page, grid, row, column) {
    await cell(grid, row, column).click({ force: true });
    await until(() => grid.evaluate((root, [path, row, column]) =>
        root.querySelector(path)?.getAttribute('aria-activedescendant')?.endsWith(`-r${row}c${column}`), [KEY_FIELD, row, column]),
        `the Focus on r${row}c${column}`);
}

async function errorUiShown(page) {
    return page.evaluate(() => getComputedStyle(document.getElementById('blazor-error-ui')).display !== 'none');
}

/** Reads one of the Instruments' readouts: clicks its button, and waits for an answer newer than the one shown. */
async function readout(page, button, pre) {
    const before = await status(page, pre);
    await page.click(`#${button}`);
    await until(async () => {
        const now = await status(page, pre);
        return now.startsWith('{') && now !== before ? now : null;
    }, `${pre} to answer`);
    return JSON.parse(await status(page, pre));
}

async function pricesShown(grid, rows) {
    const texts = [];
    for (let r = 0; r < rows; r++) texts.push(await textAt(grid, r, C.price));
    return texts.join('|');
}

async function settled(page, grid) {
    const expected = await status(page, 'expected-prices');
    const rows = expected.split('|').length;
    return until(async () => (await pricesShown(grid, rows)) === (await status(page, 'expected-prices')), 'the screen to show the newest state', 20_000);
}

async function ids(grid, rows) {
    const out = [];
    for (let r = 0; r < rows; r++) out.push(await textAt(grid, r, C.id));
    return out;
}

/** Counts DOM mutation records per painted trade, and every Change Highlight mark that appears. */
async function observe(page) {
    await page.evaluate(() => {
        const viewport = document.querySelector('.ex-grid .ex-viewport');
        const tradeOf = (node) => {
            const el = node.nodeType === 1 ? node : node.parentElement;
            const row = el?.closest('.ex-row');
            return row?.querySelector("[id$='c1']")?.textContent?.trim() ?? '_outside-rows';
        };
        const state = { records: {}, types: {}, marks: [] };
        const observer = new MutationObserver((records) => {
            for (const record of records) {
                const trade = tradeOf(record.target);
                state.records[trade] = (state.records[trade] ?? 0) + 1;
                state.types[record.type] = (state.types[record.type] ?? 0) + 1;
                if (record.type === 'attributes' && record.attributeName === 'class') {
                    const el = record.target;
                    const was = (record.oldValue ?? '').split(' ').includes('ex-changed');
                    if (el.classList.contains('ex-changed') && !was) {
                        const m = /-r(\d+)c(\d+)$/.exec(el.id ?? '');
                        state.marks.push({ trade, column: m ? Number(m[2]) : -1 });
                    }
                }
            }
        });
        observer.observe(viewport, { subtree: true, childList: true, characterData: true, attributes: true, attributeOldValue: true });
        window.__fg = state;
        window.__fgStop = () => observer.disconnect();
    });
}

// ---- Check 1: live ticks reach the screen, changed cells are marked, unchanged rows do not render

async function check1(browser, variant) {
    const ticks = 60;
    const { context, page, grid, problems } = await open(browser, variant, `interval=50&perTick=20&ticks=${ticks}&addEvery=10`);
    try {
        const painted = await ids(grid, 20);
        await observe(page);
        await page.click('#counts-reset');
        await until(async () => (await status(page, 'counts')) === 'reset', 'the counts reset');
        await page.click('#feed-start');
        await until(async () => (await status(page, 'ticks')) === `Ticks: ${ticks}` && (await status(page, 'feed-status')) === 'Feed: stopped', 'the feed to end', 60_000);
        await settled(page, grid);
        const counts = await readout(page, 'counts-read', 'counts');
        const seen = await page.evaluate(() => { window.__fgStop(); return window.__fg; });
        const busy = painted.filter((_, i) => i % 2 === 0);
        const quiet = painted.filter((_, i) => i % 2 === 1);
        const sum = (list, map) => list.reduce((s, id) => s + (map[id] ?? 0), 0);
        const quietRenders = sum(quiet, counts.renders);
        const quietMounts = sum(quiet, counts.mounts);
        const quietRecords = sum(quiet, seen.records);
        const busyRenders = sum(busy, counts.renders);
        const busyMounts = sum(busy, counts.mounts);
        const marksOnQuiet = seen.marks.filter((m) => quiet.includes(m.trade)).length;
        const marksOffColumn = seen.marks.filter((m) => m.column !== C.price && m.column !== C.pnl).length;
        const marksOnBusy = seen.marks.filter((m) => busy.includes(m.trade)).length;
        const evidence = {
            ticks, painted: painted.length, busyRows: busy.length, quietRows: quiet.length,
            busyRenders, busyRendersPerRowPerTick: +(busyRenders / busy.length / ticks).toFixed(3), busyMounts,
            quietRenders, quietMounts, quietRecords, busyRecords: sum(busy, seen.records),
            otherRecords: seen.records['_outside-rows'] ?? 0, recordTypes: seen.types,
            marksOnBusy, marksOnQuiet, marksOffColumn, totalRenders: counts.totalRenders, totalMounts: counts.totalMounts,
            problems, errorUi: await errorUiShown(page),
        };
        const pass = quietRenders === 0 && quietMounts === 0 && quietRecords === 0 && busyMounts === 0
            && marksOnBusy > 0 && marksOnQuiet === 0 && marksOffColumn === 0 && problems.length === 0 && !evidence.errorUi;
        return { pass, evidence };
    } finally {
        await context.close();
    }
}

// ---- Check 2: a user edit goes through the store and lands; a following gesture is not refused

async function check2(browser, variant, live) {
    const { context, page, grid, problems } = await open(browser, variant, 'interval=50&perTick=20&ticks=0&addEvery=0');
    try {
        if (live) {
            await page.click('#feed-start');
            await until(async () => Number((await status(page, 'ticks')).split(' ')[1]) >= 3, 'the feed to run');
        }
        const row = 1;   // T00001: odd, never ticked
        const evidence = { live };
        await clickCell(page, grid, row, C.notional);
        await page.keyboard.type('1234567');
        await page.keyboard.press('Enter');
        await until(async () => (await textAt(grid, row, C.notional)) === '1,234,567.00' || (await status(page, 'commit-refused')) !== 'Commit refused: —', 'the edit to land');
        evidence.editLanded = (await textAt(grid, row, C.notional)) === '1,234,567.00';
        evidence.ownEditMarked = await isMarked(grid, row, C.notional);
        evidence.storeAfterEdit = await status(page, 'last-write');

        // At machine speed, on the cell the edit just wrote: 5 Enter ↑ 7 Enter (LV-17, ADR-0142 D1).
        await page.keyboard.press('ArrowUp');
        await page.keyboard.type('5');
        await page.keyboard.press('Enter');
        await page.keyboard.press('ArrowUp');
        await page.keyboard.type('7');
        await page.keyboard.press('Enter');
        await until(async () => (await textAt(grid, row, C.notional)) === '7.00' || (await status(page, 'commit-refused')) !== 'Commit refused: —', 'the second commit to land or be refused');
        evidence.secondCommit = { cell: await textAt(grid, row, C.notional), refusal: await status(page, 'commit-refused') };
        if (evidence.secondCommit.refusal !== 'Commit refused: —') {
            // The refused editor still stands: Escape leaves it, and the store is read.
            await page.keyboard.press('Escape');
        }
        await until(async () => (await grid.locator('input.ex-editor').count()) === 0, 'the editor to close');

        // And a paste over the cell a commit just wrote: 9 Enter ↑ Ctrl+V.
        await page.evaluate(() => navigator.clipboard.write([new ClipboardItem({
            'text/html': new Blob(['<table><tr><td>3</td></tr></table>'], { type: 'text/html' }),
            'text/plain': new Blob(['3\r\n'], { type: 'text/plain' }),
        })]));
        await clickCell(page, grid, row, C.notional);
        await page.keyboard.type('9');
        await page.keyboard.press('Enter');
        await page.keyboard.press('ArrowUp');
        await page.keyboard.press('ControlOrMeta+v');
        await until(async () => (await textAt(grid, row, C.notional)) === '3.00' || (await status(page, 'paste-refused')) !== 'Paste refused: —', 'the paste to land or be refused');
        evidence.paste = { cell: await textAt(grid, row, C.notional), refusal: await status(page, 'paste-refused') };
        evidence.storeAtEnd = await status(page, 'last-write');
        evidence.storeRefused = await status(page, 'store-refused');
        if (live) {
            await page.click('#feed-stop');
            await until(async () => (await status(page, 'feed-status')) === 'Feed: stopped', 'the feed to stop');
        }
        evidence.problems = problems;
        evidence.errorUi = await errorUiShown(page);
        const pass = evidence.editLanded && evidence.secondCommit.cell === '7.00' && evidence.secondCommit.refusal === 'Commit refused: —'
            && evidence.paste.cell === '3.00' && evidence.paste.refusal === 'Paste refused: —' && problems.length === 0 && !evidence.errorUi;
        return { pass, evidence };
    } finally {
        await context.close();
    }
}

// ---- Check 3: an edit is refused when the cell changed upstream while the editor was open

async function check3(browser, variant) {
    const { context, page, grid, problems } = await open(browser, variant, 'interval=50&perTick=20&ticks=0&addEvery=0');
    try {
        const row = 3;
        const before = await textAt(grid, row, C.notional);
        await clickCell(page, grid, row, C.notional);
        await page.keyboard.type('999');
        await page.keyboard.press('F9');   // upstream amends the Focus cell: Notional + 1
        await until(async () => (await status(page, 'upstream-status')).includes('(×1)'), 'the upstream amendment to be dispatched');
        await page.keyboard.press('Enter');
        await until(async () => (await status(page, 'commit-refused')) !== 'Commit refused: —' || (await grid.locator('input.ex-editor').count()) === 0, 'the commit to be refused or land');
        const refusal = await status(page, 'commit-refused');
        const editorValue = (await grid.locator('input.ex-editor').count()) > 0 ? await grid.locator('input.ex-editor').inputValue() : null;
        const shownUnder = await textAt(grid, row, C.notional);
        // The second commit is judged against what the refusal showed, and lands.
        await page.keyboard.press('Enter');
        await until(async () => (await textAt(grid, row, C.notional)) === '999.00', 'the second commit to land', 10_000).catch(() => {});
        const after = await textAt(grid, row, C.notional);
        const evidence = { before, refusal, editorValue, shownUnderEditor: shownUnder, afterSecondEnter: after,
            store: await status(page, 'last-write'), problems, errorUi: await errorUiShown(page) };
        const amended = (Number(before.replaceAll(',', '')) + 1).toLocaleString('en-US', { minimumFractionDigits: 2 });
        const pass = refusal.startsWith('Commit refused: CellChanged') && refusal.includes(amended) && editorValue === '999'
            && after === '999.00' && problems.length === 0 && !evidence.errorUi;
        return { pass, evidence };
    } finally {
        await context.close();
    }
}

// ---- Check 4: paste and fill go through the store

async function dragHandle(page, grid, from, to) {
    const corner = await cell(grid, from[0], from[1]).boundingBox();
    await until(async () => {
        const box = await grid.locator('.ex-fill-handle').boundingBox();
        return box !== null && Math.abs(box.x + box.width / 2 - (corner.x + corner.width)) <= 2
            && Math.abs(box.y + box.height / 2 - (corner.y + corner.height)) <= 2;
    }, 'the fill handle at the Selection');
    const handle = await grid.locator('.ex-fill-handle').boundingBox();
    const target = await cell(grid, to[0], to[1]).boundingBox();
    await page.mouse.move(handle.x + handle.width / 2, handle.y + handle.height / 2);
    await page.mouse.down();
    const x = target.x + target.width / 2 + 3;
    const y = target.y + target.height / 2;
    await page.mouse.move(x, y, { steps: 8 });
    let nudge = 0;
    await until(async () => {
        await page.mouse.move(x + (nudge++ % 2), y);
        return (await grid.locator('.ex-fill-target').count()) === 1;
    }, 'the fill target outline');
    await page.mouse.up();
}

async function check4(browser, variant) {
    const { context, page, grid, problems } = await open(browser, variant, 'interval=50&perTick=20&ticks=0&addEvery=0');
    try {
        const evidence = {};
        // Paste a 2×1 block into Notional rows 5 and 6.
        await page.evaluate(() => navigator.clipboard.write([new ClipboardItem({
            'text/html': new Blob(['<table><tr><td>111</td></tr><tr><td>222</td></tr></table>'], { type: 'text/html' }),
            'text/plain': new Blob(['111\r\n222\r\n'], { type: 'text/plain' }),
        })]));
        await clickCell(page, grid, 5, C.notional);
        await page.keyboard.press('Shift+ArrowDown');
        await page.keyboard.press('ControlOrMeta+v');
        await until(async () => (await textAt(grid, 6, C.notional)) === '222.00' || (await status(page, 'paste-refused')) !== 'Paste refused: —', 'the paste');
        evidence.paste = { r5: await textAt(grid, 5, C.notional), r6: await textAt(grid, 6, C.notional), store: await status(page, 'last-write') };

        // Ctrl+D: Book rows 7 to 9 take row 7's.
        const book7 = await textAt(grid, 7, C.book);
        await clickCell(page, grid, 7, C.book);
        await page.keyboard.press('Shift+ArrowDown');
        await page.keyboard.press('Shift+ArrowDown');
        await page.keyboard.press('ControlOrMeta+d');
        await until(async () => (await textAt(grid, 9, C.book)) === book7 || (await status(page, 'paste-refused')) !== 'Paste refused: —', 'Ctrl+D');
        evidence.fillKey = { r8: await textAt(grid, 8, C.book), r9: await textAt(grid, 9, C.book), expected: book7, store: await status(page, 'last-write') };

        // A fill-handle drag: Price row 11 down to row 13.
        const price11 = await textAt(grid, 11, C.price);
        await clickCell(page, grid, 11, C.price);
        await dragHandle(page, grid, [11, C.price], [13, C.price]);
        await until(async () => (await textAt(grid, 13, C.price)) === price11 || (await status(page, 'paste-refused')) !== 'Paste refused: —', 'the fill drag');
        evidence.fillDrag = { r12: await textAt(grid, 12, C.price), r13: await textAt(grid, 13, C.price), expected: price11, store: await status(page, 'last-write') };

        // Delete: Book row 15 cleared.
        await clickCell(page, grid, 15, C.book);
        await page.keyboard.press('Delete');
        await until(async () => (await textAt(grid, 15, C.book)) === '' || (await status(page, 'paste-refused')) !== 'Paste refused: —', 'the clear');
        evidence.clear = { r15: await textAt(grid, 15, C.book), store: await status(page, 'last-write') };
        evidence.pasteRefused = await status(page, 'paste-refused');
        evidence.problems = problems;
        evidence.errorUi = await errorUiShown(page);
        const pass = evidence.paste.r5 === '111.00' && evidence.paste.r6 === '222.00'
            && evidence.fillKey.r8 === book7 && evidence.fillKey.r9 === book7
            && evidence.fillDrag.r12 === price11 && evidence.fillDrag.r13 === price11
            && evidence.clear.r15 === '' && evidence.pasteRefused === 'Paste refused: —' && problems.length === 0 && !evidence.errorUi;
        return { pass, evidence };
    } finally {
        await context.close();
    }
}

// ---- Check 5: under a sort, a tick that moves a row drops the Selection; one that changes values keeps it

const selectionRanges = (grid) => grid.locator('.ex-selection .ex-range').count();

async function check5(browser, variant) {
    const { context, page, grid, problems } = await open(browser, variant, 'interval=50&perTick=20&ticks=0&addEvery=0');
    try {
        const evidence = {};
        // P&L ascending, by one click on its header.
        const firstBefore = await textAt(grid, 0, C.id);
        await grid.locator('.ex-header-cell').filter({ hasText: 'P&L' }).first().click({ position: { x: 30, y: 12 }, force: true });
        const pnlOrder = async () => {
            const pnl = [];
            for (let r = 0; r < 8; r++) pnl.push(Number((await textAt(grid, r, C.pnl)).replaceAll(',', '')));
            return pnl.every((v, i) => i === 0 || pnl[i - 1] <= v);
        };
        await until(async () => (await grid.locator('.ex-header-cell').nth(C.pnl).getAttribute('aria-sort')) === 'ascending' && (await pnlOrder()), 'the rows in P&L order');
        evidence.sortedFirst = [await textAt(grid, 0, C.id), await textAt(grid, 1, C.id), firstBefore];
        evidence.ascending = await pnlOrder();

        await clickCell(page, grid, 2, C.price);
        await page.keyboard.press('Shift+ArrowDown');
        await until(async () => (await selectionRanges(grid)) === 1, 'a Selection');
        evidence.selectedBefore = await selectionRanges(grid);

        // F7: the first ten positions' prices move, P&L unchanged — values only.
        const price0 = await textAt(grid, 0, C.price);
        const order = await ids(grid, 12);
        await page.keyboard.press('F7');
        await until(async () => (await textAt(grid, 0, C.price)) !== price0, 'F7\'s prices on screen');
        await until(async () => (await textAt(grid, 9, C.price)) !== '' , 'row 9');
        evidence.orderKeptAfterValues = JSON.stringify(await ids(grid, 12)) === JSON.stringify(order);
        evidence.selectedAfterValues = await selectionRanges(grid);

        // F8: the trade at position 10 takes the largest P&L — under P&L ascending it moves to the end.
        const moved = await textAt(grid, 10, C.id);
        await page.keyboard.press('F8');
        await until(async () => (await textAt(grid, 10, C.id)) !== moved, 'F8\'s row to move');
        evidence.movedTrade = moved;
        evidence.selectedAfterMove = await selectionRanges(grid);
        evidence.problems = problems;
        evidence.errorUi = await errorUiShown(page);
        const pass = evidence.ascending && evidence.selectedBefore === 1 && evidence.orderKeptAfterValues
            && evidence.selectedAfterValues === 1 && evidence.selectedAfterMove === 0 && problems.length === 0 && !evidence.errorUi;
        return { pass, evidence };
    } finally {
        await context.close();
    }
}

// ---- Check 6: the managed heap and what stays reachable over 200 ticks of 1,000 trades, twice

async function check6(browser, variant) {
    const ticks = 200;
    const { context, page, grid, problems } = await open(browser, variant, `interval=20&perTick=50&ticks=${ticks}&addEvery=10`);
    try {
        const readings = [];
        readings.push({ at: 'start', ...(await readout(page, 'census-count', 'census')) });
        const rounds = Number(args.rounds ?? 2);
        for (let round = 1; round <= rounds; round++) {
            await page.click('#feed-start');
            await until(async () => (await status(page, 'ticks')) === `Ticks: ${ticks * round}` && (await status(page, 'feed-status')) === 'Feed: stopped', `round ${round} of the feed`, 120_000);
            await settled(page, grid);
            readings.push({ at: `after ${ticks * round} ticks`, ...(await readout(page, 'census-count', 'census')) });
        }
        // Then the grid goes, by Blazor's navigation in the same app or circuit, so the store stays:
        // what is still reachable with no grid on the page is the store's, or Fluxor's.
        await page.click('#home');
        await page.locator('#home-census-count').waitFor();
        const errorUi = await errorUiShown(page);
        readings.push({ at: 'grid gone (Home)', ...(await readout(page, 'home-census-count', 'home-census')) });
        const evidence = { readings, problems, errorUi };
        const first = readings[1];
        const last = readings[readings.length - 2];
        evidence.heapGrowthAfterFirstRound = last.heapBytes - first.heapBytes;
        const pass = readings.every((r) => r.oldStatesAlive <= 2) && Math.abs(evidence.heapGrowthAfterFirstRound) < 0.1 * first.heapBytes
            && problems.length === 0 && !evidence.errorUi;
        return { pass, evidence };
    } finally {
        await context.close();
    }
}

// ---- Check 7 (Server): one store per circuit, and the feed's thread reaching the grid safely

async function check7(browser) {
    const a = await open(browser, 'w2', 'interval=50&perTick=20&ticks=40&addEvery=10');
    const b = await open(browser, 'w1', 'interval=50&perTick=20&ticks=0&addEvery=0');
    try {
        const evidence = {};
        const bNotional = await textAt(b.grid, 1, C.notional);
        await clickCell(a.page, a.grid, 1, C.notional);
        await a.page.keyboard.type('4242');
        await a.page.keyboard.press('Enter');
        await until(async () => (await textAt(a.grid, 1, C.notional)) === '4,242.00', 'A\'s edit');
        await a.page.click('#feed-start');
        await until(async () => (await status(a.page, 'ticks')) === 'Ticks: 40' && (await status(a.page, 'feed-status')) === 'Feed: stopped', 'A\'s feed', 60_000);
        await settled(a.page, a.grid);
        evidence.aTicks = await status(a.page, 'ticks');
        evidence.bTicks = await status(b.page, 'ticks');
        evidence.bNotionalUnchanged = (await textAt(b.grid, 1, C.notional)) === bNotional;
        evidence.bVersion = await status(b.page, 'state-version');
        evidence.problems = [...a.problems, ...b.problems];
        evidence.errorUi = (await errorUiShown(a.page)) || (await errorUiShown(b.page));
        const pass = evidence.aTicks === 'Ticks: 40' && evidence.bTicks === 'Ticks: 0' && evidence.bNotionalUnchanged
            && evidence.problems.length === 0 && !evidence.errorUi;
        return { pass, evidence };
    } finally {
        await a.context.close();
        await b.context.close();
    }
}

const browser = await chromium.launch({ channel: 'chrome', headless: true });
const results = {};
try {
    for (const variant of variants) {
        results[variant] = {};
        for (const n of checks) {
            if (n === 7) continue;
            const runs = n === 2 ? [['2-paused', () => check2(browser, variant, false)], ['2-live', () => check2(browser, variant, true)]]
                : [[String(n), () => ({ 1: check1, 3: check3, 4: check4, 5: check5, 6: check6 })[n](browser, variant)]];
            for (const [name, run] of runs) {
                try {
                    results[variant][name] = await run();
                } catch (error) {
                    results[variant][name] = { pass: false, error: String(error.stack ?? error) };
                }
                log(variant, name, results[variant][name].pass ? 'PASS' : 'FAIL', JSON.stringify(results[variant][name]));
            }
        }
    }
    if (checks.includes(7) && host === 'server') {
        try {
            results.server7 = await check7(browser);
        } catch (error) {
            results.server7 = { pass: false, error: String(error.stack ?? error) };
        }
        log('7', results.server7.pass ? 'PASS' : 'FAIL', JSON.stringify(results.server7));
    }
} finally {
    await browser.close();
}
fs.mkdirSync('out', { recursive: true });
const file = `out/${host}-${variants.join('_')}-${checks.join('')}-${new Date().toISOString().replace(/[:.]/g, '-')}.json`;
fs.writeFileSync(file, JSON.stringify({ host, base, at: new Date().toISOString(), results }, null, 2));
log('written', file);
