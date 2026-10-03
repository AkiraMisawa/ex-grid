import { test, expect, circuitQuiet, scrollRowToTop } from './fixtures.mjs';
import { API_URL } from './hosting.mjs';
import { expectCodeIsSource } from './demo-code.mjs';

// /grid-live (ADR-0068/0069): ExGrid alone over the demo API server's trades. The page pushes a
// Window of GET /api/trades, hears the hub's "these trades changed", reads them again and answers
// CellChangedAt from when each cell's value moved. What only a browser can say: that a mark is
// keyed by row and column and stays on its cell across a scroll (DC-65); that it paints without a
// transition or an animation, under the core's stylesheet and under the Wrapper's, that forced
// colours restate it, and that the grid's live region does not announce it (DC-66); and that the
// page reads its Window from the server and turns the live updates off as it goes (PV-20).
//
// The API server lives for the whole run and other spec files share it: each test starts from
// POST /api/reset, which also turns live updates off, and turns them off again as it ends. The
// trade count is read from /api/status, never assumed. The page changes 1,000 trades every 250 ms,
// so among 20,000 a mark lands in view within a tick or two; a larger reused server takes longer,
// and the waits for a first mark allow for it.

test.use({ viewport: { width: 1400, height: 1100 } });

const grid = (page) => page.locator('.ex-grid');
const marked = (page) => grid(page).locator('.ex-viewport .ex-cell.ex-changed');
// The Trade ID cell of the row at a position: column 0, pinned.
const tradeAt = (page, row) => grid(page).locator(`[id$='-r${row}c0']`);
const toggle = (page) => page.locator('#grid-live-toggle');

async function api(path, init) {
    const response = await fetch(`${API_URL}${path}`, init);
    expect(response.ok, `${path} answered ${response.status}`).toBe(true);
    return response.json();
}

const post = (path, body) => api(path, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
});

test.afterEach(async () => {
    // Whatever the test left, the data holds still for the next file (ADR-0069).
    await post('/api/live', { on: false });
});

async function open(page, chrome) {
    await post('/api/reset');
    await page.goto(`/grid-live?chrome=${chrome}`);
    await expect(tradeAt(page, 0)).toBeVisible({ timeout: 30_000 });
    // Connected to the hub, and the live updates turned on.
    await expect(toggle(page)).toHaveText("Turn the server's live updates off", { timeout: 30_000 });
    if (chrome === 'mud') {
        // The Wrapper's stylesheet has landed when its Change Highlight token reaches the grid.
        await expect.poll(() => grid(page).evaluate((g) => getComputedStyle(g).getPropertyValue('--ex-change-highlight-background').trim()))
            .not.toBe('');
    }
}

/** Turns the live updates off and waits until the page has read again every trade the hub named,
 *  up to the version the server holds now, and the host has said all it will about them
 *  (ADR-0056): from then on the marks on screen are all there will be. */
async function holdStill(page) {
    await toggle(page).click();
    await expect(toggle(page)).toHaveText("Turn the server's live updates on");
    const { version } = await api('/api/status');
    await expect(page.locator('#grid-live-notices')).toContainText(`Read again up to version ${version}.`, { timeout: 15_000 });
    await circuitQuiet();
}

/** The positions of the rows that carry a mark. */
const markedRows = (page) => marked(page).evaluateAll((cells) =>
    cells.map((cell) => Number(/-r(\d+)c\d+$/.exec(cell.id)[1])));

/**
 * What the grid paints now, read from its cells: each painted trade's position, and the columns
 * of its marked cells. Each row element is tagged with the trade it painted, so the next reading
 * can name an element that paints another trade than it did (`reused`).
 */
const reading = (page) => grid(page).evaluate((root) => {
    const painted = {};
    const marks = {};
    const reused = [];
    for (const row of root.querySelectorAll('.ex-viewport .ex-row')) {
        const idCell = row.querySelector('[id$="c0"]');
        if (!idCell) {
            continue;
        }
        const trade = idCell.textContent.trim();
        painted[trade] = Number(/-r(\d+)c0$/.exec(idCell.id)[1]);
        const columns = [...row.querySelectorAll('.ex-cell.ex-changed')]
            .map((cell) => Number(/c(\d+)$/.exec(cell.id)[1]))
            .sort((a, b) => a - b);
        if (columns.length > 0) {
            marks[trade] = columns;
        }
        if (row.__exGridLiveTrade !== undefined && row.__exGridLiveTrade !== trade) {
            reused.push({ was: row.__exGridLiveTrade, now: trade, columns });
        }
        row.__exGridLiveTrade = trade;
    }
    return { painted, marks, reused };
});

/** Every trade painted in both readings carries the same marked columns in both. */
function expectSameMarks(before, after, when) {
    let shared = 0;
    for (const trade of Object.keys(after.painted)) {
        if (!(trade in before.painted)) {
            continue;
        }
        shared++;
        expect(after.marks[trade] ?? [], `${trade}'s marks ${when}`).toEqual(before.marks[trade] ?? []);
    }
    return shared;
}

async function scrollTo(page, row) {
    await scrollRowToTop(grid(page), row);
    await expect(tradeAt(page, row)).toBeVisible({ timeout: 15_000 });
    await expect(grid(page).locator('.ex-viewport .ex-placeholder')).toHaveCount(0, { timeout: 15_000 });
}

test('DC-65/ADR-0068: a mark stays on its cell across a scroll, and never moves with an element to another row', async ({ page }) => {
    test.setTimeout(150_000);
    await open(page, 'builtin');
    // Marks that outlast the test, so that what is compared is where they are, not when they end.
    await page.locator('#grid-live-highlight').selectOption('60000');
    // A few rows down, so that a marked row can move three rows and stay in view.
    await scrollTo(page, 5);
    await expect.poll(async () => (await markedRows(page)).some((row) => row >= 5 && row <= 17),
        { message: 'a mark landed in rows 5 to 17', timeout: 90_000 }).toBe(true);
    await holdStill(page);
    const before = await reading(page);
    expect(Object.keys(before.marks).length, JSON.stringify(before.marks)).toBeGreaterThan(0);

    // Three rows up: the rows at the foot go, three come in at the top, and every trade still
    // painted keeps exactly its marked cells, whatever element paints it now. An element that
    // paints another trade than before carries that trade's marks, never the ones it had.
    await scrollTo(page, 2);
    // Read once the host has said all it will about the rows that came in (ADR-0056).
    await circuitQuiet();
    const after = await reading(page);
    expect(expectSameMarks(before, after, 'after a scroll of three rows')).toBeGreaterThan(10);
    expect(Object.keys(after.marks).some((trade) => trade in before.marks), 'a marked trade is still in view').toBe(true);
    for (const { was, now, columns } of after.reused) {
        if (now in before.painted) {
            expect(columns, `the element that painted ${was} now paints ${now}`).toEqual(before.marks[now] ?? []);
        }
    }
    test.info().annotations.push({ type: 'row elements reused for another trade', description: String(after.reused.length) });

    // Far away and back: the Window is read again, so every row is a new instance in a new element,
    // and the marks come back on the same cells, from what the page knows about each trade.
    await scrollTo(page, 400);
    await scrollTo(page, 5);
    await circuitQuiet();
    const back = await reading(page);
    expect(expectSameMarks(before, back, 'after the Window was read again')).toBeGreaterThan(10);
    expect(Object.keys(back.marks).sort()).toEqual(Object.keys(before.marks).sort());
});

for (const chrome of ['builtin', 'mud']) {
    test(`DC-66/ADR-0068: marks paint without animating, forced colours restate them, and the live region says nothing (${chrome})`, async ({ page }) => {
        test.setTimeout(150_000);
        await open(page, chrome);
        await page.locator('#grid-live-highlight').selectOption('60000');
        // The grid's one live region, watched while marks come and go (ADR-0033): the page's own
        // observer, on the test's own grid, taken off before the test ends.
        await grid(page).evaluate((root) => {
            const region = root.querySelector('.ex-announce');
            root.__exGridLiveSaid = { initial: region.textContent, mutations: [] };
            root.__exGridLiveObserver = new MutationObserver((records) =>
                root.__exGridLiveSaid.mutations.push(...records.map((record) => record.type)));
            root.__exGridLiveObserver.observe(region, { childList: true, characterData: true, subtree: true, attributes: true });
        });
        await expect.poll(() => marked(page).count(), { message: 'a mark in view', timeout: 90_000 }).toBeGreaterThan(0);
        // Two seconds more of notices, eight of them, with marks painted and taken away meanwhile.
        const heard = async () => Number((/Heard ([\d,]+) notices/.exec(await page.locator('#grid-live-notices').textContent()) ?? [0, '0'])[1].replace(/,/g, ''));
        const from = await heard();
        await expect.poll(heard, { timeout: 30_000 }).toBeGreaterThanOrEqual(from + 8);
        await expect.poll(() => marked(page).count()).toBeGreaterThan(0);

        // UX-6's check with marks showing: nothing under the Viewport transitions or animates.
        const offenders = await grid(page).evaluate((root) =>
            [...root.querySelectorAll('.ex-viewport, .ex-viewport *')]
                .map((el) => ({
                    cls: el.className,
                    transition: getComputedStyle(el).transitionProperty,
                    duration: getComputedStyle(el).transitionDuration,
                    animation: getComputedStyle(el).animationName,
                }))
                .filter((s) => s.animation !== 'none'
                    || (s.transition !== 'none' && !/^0s(, 0s)*$/.test(s.duration))));
        expect(offenders).toEqual([]);

        // The mark is painted: the token's colour, as a layer over exactly what the cell paints
        // without it, on a marked cell and on no other (ADR-0068). Since ExSheet's Cell Format a cell
        // paints layers of its own — a Pinned Column's cell its row's rule (ticket 92) — so each
        // marked cell is read beside an unmarked cell of its own kind.
        const paint = await grid(page).evaluate((root) => {
            // A background-image's layers: its value split at the commas outside parentheses.
            const layersOf = (cell) => {
                const value = getComputedStyle(cell).backgroundImage;
                const layers = [];
                let depth = 0;
                let from = 0;
                for (let i = 0; i < value.length; i++) {
                    if (value[i] === '(') {
                        depth++;
                    } else if (value[i] === ')') {
                        depth--;
                    } else if (value[i] === ',' && depth === 0) {
                        layers.push(value.slice(from, i).trim());
                        from = i + 1;
                    }
                }
                layers.push(value.slice(from).trim());
                return layers;
            };
            const mark = root.querySelector('.ex-viewport .ex-cell.ex-changed');
            const row = mark.closest('.ex-row');
            const pinned = mark.classList.contains('ex-pinned');
            const plain = [...row.querySelectorAll('.ex-cell:not(.ex-changed)')]
                .find((cell) => cell.classList.contains('ex-pinned') === pinned);
            // The page's trade ids never change, so no Pinned Column's cell is marked here. The row's
            // is marked for one task, read and unmarked again, before any render could land.
            const pinnedCell = row.querySelector('.ex-cell.ex-pinned:not(.ex-changed)');
            const pinnedPlain = layersOf(pinnedCell);
            pinnedCell.classList.add('ex-changed');
            const pinnedMarked = layersOf(pinnedCell);
            pinnedCell.classList.remove('ex-changed');
            return {
                marked: layersOf(mark),
                unmarked: plain ? layersOf(plain) : null,
                pinnedMarked,
                pinnedPlain,
                token: getComputedStyle(root).getPropertyValue('--ex-change-highlight-background').trim(),
            };
        });
        const [markLayer] = paint.marked;
        expect(markLayer, JSON.stringify(paint)).toMatch(/^linear-gradient\(/);
        // The mark is the top layer, and beneath it the cell paints what its neighbour paints.
        expect(paint.unmarked, JSON.stringify(paint)).not.toBeNull();
        expect(paint.marked.slice(1), JSON.stringify(paint)).toEqual(paint.unmarked);
        expect(paint.unmarked, JSON.stringify(paint)).not.toContain(markLayer);
        // A Pinned Column's cell keeps its row's rule beneath the mark.
        expect(paint.pinnedMarked, JSON.stringify(paint)).toEqual([markLayer, ...paint.pinnedPlain]);
        if (chrome === 'mud') {
            // The Wrapper maps the token onto its palette: the warning colour at 25% (ADR-0068). The shipped stylesheet is minified (ADR-0123), which writes 0.25 as .25.
            expect(paint.token, JSON.stringify(paint)).toMatch(/(^|[^0-9])0?\.25\)$/);
            expect(markLayer, JSON.stringify(paint)).toContain(', 0.25)');
            // And turns the row's rule on, so the rule the mark keeps is one that shows.
            expect(paint.pinnedPlain[0], JSON.stringify(paint)).toMatch(/^linear-gradient\(to top, (?!rgba\(0, 0, 0, 0\))/);
        } else {
            // The core's default: a 40% tint of the system colour Mark, never opaque, so the value
            // reads through it (ADR-0068).
            expect(paint.token).toBe('');
            expect(markLayer, JSON.stringify(paint)).toMatch(/[/,] 0\.4\)/);
        }

        // The live region said nothing while the marks came and went.
        const said = await grid(page).evaluate((root) => {
            root.__exGridLiveObserver.disconnect();
            const result = { ...root.__exGridLiveSaid, now: root.querySelector('.ex-announce').textContent };
            delete root.__exGridLiveObserver;
            delete root.__exGridLiveSaid;
            return result;
        });
        expect(said.mutations, 'mutations of the live region').toEqual([]);
        expect(said.now).toBe(said.initial);

        // Forced colours discard the tint, and the forced-colors block restates the mark as a dashed
        // outline, which no Cell State uses.
        await holdStill(page);
        await expect.poll(() => marked(page).count()).toBeGreaterThan(0);
        await page.emulateMedia({ forcedColors: 'active' });
        const forced = await grid(page).evaluate((root) => {
            const mark = root.querySelector('.ex-viewport .ex-cell.ex-changed');
            const plain = mark.closest('.ex-row').querySelector('.ex-cell:not(.ex-changed)');
            const outline = (el) => `${getComputedStyle(el).outlineStyle} ${getComputedStyle(el).outlineWidth}`;
            return { marked: outline(mark), unmarked: outline(plain) };
        });
        expect(forced.marked).toBe('dashed 2px');
        expect(forced.unmarked).not.toContain('dashed');
    });
}

test('PV-20/ADR-0069: the Window is read from the server as the grid scrolls, and leaving turns the live updates off', async ({ page }) => {
    test.setTimeout(120_000);
    await open(page, 'builtin');
    const status = await api('/api/status');
    expect(status.live.on).toBe(true);
    await expect(grid(page)).toHaveAttribute('aria-rowcount', /^\d+$/);
    // Every trade the server holds is a row of the grid, though the page holds a Window of them.
    expect(Number(await grid(page).getAttribute('aria-rowcount'))).toBe(status.trades);
    await expect(page.locator('#grid-live-window')).toContainText(`of ${status.trades.toLocaleString('en-US')}`);

    // Far down, past what the first page read: the grid asks for the rows, and the page reads them.
    const far = Math.min(status.trades - 30, 12_000);
    await scrollTo(page, far);
    const ids = await grid(page).locator('.ex-viewport [id$="c0"]').allTextContents();
    expect(ids.length).toBeGreaterThan(10);
    // In TradeId order, as the server pages them.
    expect([...ids].sort()).toEqual(ids);

    // The page turned the live updates on, and turns them off as it goes.
    await page.goto('/');
    await expect.poll(async () => (await api('/api/live')).on, { timeout: 15_000 }).toBe(false);
});

test('ADR-0069/0068: the code the page shows is the code it runs: the hub\'s notice read again, and CellChangedAt answered', async ({ page }) => {
    await open(page, 'builtin');
    const code = await expectCodeIsSource(page);
    expect(code['GridLivePage.razor#notices']).toContain('hub.On<string, string[]>("TradesChanged"');
    expect(code['GridLivePage.razor#notices']).toContain('"api/trades/by-id"');
    expect(code['GridLivePage.razor#grid']).toContain('CellChangedAt="_cellChangedAt" ChangeHighlightDuration="Highlight"');
    expect(code['GridLivePage.razor#window']).toContain('api/trades?start=');
});
