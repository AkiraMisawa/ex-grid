import { test, expect } from './fixtures.mjs';

// The Wrapper verified against a Consumer (Definition of Done §23): /mud-app is an ordinary
// MudBlazor application — MudLayout with an AppBar and a Drawer, MudTabs, a MudDialog, a
// MudSelect in the toolbar, a light/dark switch — with the Wrapper's grids where such an
// application puts its tables. This file asserts the WR-7 clauses that need no seam the
// Wrapper has yet to fill, and it runs on the shared fixture, so every test here also
// holds the console rules (WR-9: CON-1/2/3/6, enforced in fixtures.mjs for every page a
// test drives). The Inner Popup halves of WR-7 — a grid panel's MudSelect against the
// toolbar's, and a dialog grid's Inner Popups above its popovers — wait for the Wrapper's
// own filter panel (WR-1/2), whose operator is the MudSelect they are about.

const positions = (page) => page.locator('#positions-paper .ex-grid');
const orders = (page) => page.locator('#orders-paper .ex-grid');
const history = (page) => page.locator('#history-paper .ex-grid');

// The Fill grid's columns come to 1420px — wider than the main area with the Drawer
// open or closed — and its last column is index 12.
const LAST_COLUMN = 12;

async function open(page) {
    await page.goto('/mud-app');
    await expect(positions(page).locator('.ex-row').first()).toBeVisible();
    await expect(orders(page).locator('.ex-row').first()).toBeVisible();
    // The Wrapper's stylesheet has landed when one of its tokens reaches a root.
    await expect.poll(async () => positions(page).evaluate((g) => getComputedStyle(g).getPropertyValue('--ex-editor-outline').trim()))
        .not.toBe('');
}

// Cells are pointer-events: none by design and the Viewport is the delegated target
// (ADR-0004), so the click is forced and the browser hit-tests it through, as a user's is.
async function clickCell(grid, row, column) {
    await grid.locator(`[id$='-r${row}c${column}']`).click({ force: true });
}

/**
 * The Viewport as the browser draws it against what the grid painted, in one frame's
 * coordinates: the readable area (the scroller's client box — the Scrollbar Gutter taken
 * out by the browser itself), the Focus outline, and the painted cells of one row. The
 * row is named rather than taken from the Focus: a Focus scrolled out of view is not
 * painted, and aria-activedescendant is then rightly empty (ADR-0033).
 */
async function geometry(grid, row) {
    return grid.evaluate((root, row) => {
        const scroller = root.querySelector('.ex-scroller');
        const outer = scroller.getBoundingClientRect();
        const client = {
            left: outer.left + scroller.clientLeft,
            top: outer.top + scroller.clientTop,
            right: outer.left + scroller.clientLeft + scroller.clientWidth,
            bottom: outer.top + scroller.clientTop + scroller.clientHeight,
        };
        const rect = (el) => {
            const r = el.getBoundingClientRect();
            return { left: r.left, top: r.top, right: r.right, bottom: r.bottom, width: r.width, height: r.height };
        };
        const focus = [...root.querySelectorAll('.ex-focus')].map(rect).filter((r) => r.width > 0 && r.height > 0);
        // Column 0 is pinned on every grid this measures, so it is painted whatever the
        // horizontal offset, and its row element holds the row's other painted cells.
        const rowCells = [...root.querySelectorAll(`[id$='-r${row}c0']`)]
            .flatMap((first) => [...first.closest('.ex-row').querySelectorAll('.ex-cell')]);
        return {
            client,
            focus,
            width: root.getBoundingClientRect().width,
            gutter: { width: scroller.offsetWidth - scroller.clientWidth, height: scroller.offsetHeight - scroller.clientHeight },
            overflows: scroller.scrollWidth > scroller.clientWidth,
            cells: rowCells.map(rect),
            rows: [...root.querySelectorAll('.ex-row')].map(rect),
        };
    }, row);
}

/**
 * Whether the columns the grid painted are exactly the ones the readable area shows.
 * The grid paints the scrollable columns overlapping the width it believes it has and no
 * others (ADR-0004), so this is how the arithmetic's idea of the width shows on screen:
 * a strip left blank at the right edge means it thinks the Viewport narrower than it is,
 * a cell that starts beyond the edge means it thinks it wider. Either is a grid whose
 * geometry did not follow the box it was given.
 */
function sliceMatchesReadableArea(measured) {
    if (measured.cells.length === 0) return false;
    const covers = Math.max(...measured.cells.map((c) => c.right)) >= measured.client.right - 1;
    const nothingBeyond = measured.cells.every((c) => c.left < measured.client.right - 0.5);
    return covers && nothingBeyond;
}

/** All of the Focus inside the readable area — not behind a scrollbar, not cut off. */
function expectFocusReadable(measured, where) {
    expect(measured.focus, `${where}: the grid painted no Focus outline to check`).toHaveLength(1);
    const [focus] = measured.focus;
    const { client } = measured;
    expect(focus.left, `${where}: the Focus is left of the readable area`).toBeGreaterThanOrEqual(client.left - 1);
    expect(focus.top, `${where}: the Focus is above the readable area`).toBeGreaterThanOrEqual(client.top - 1);
    expect(focus.right, `${where}: the Focus is behind the vertical scrollbar or cut off`).toBeLessThanOrEqual(client.right + 1);
    expect(focus.bottom, `${where}: the Focus is behind the horizontal scrollbar or cut off`).toBeLessThanOrEqual(client.bottom + 1);
}

/**
 * A scrollbar that takes no space makes every readable-area assertion trivially true
 * (AGENTS.md: the gutter trap; scrollbar.spec.mjs guards the same way), so the tests
 * below refuse to pass where the bars take none.
 */
function expectScrollbarsOccupyLayout(measured) {
    const message = `the scrollbars are not occupying layout, so this test proves nothing (measured ${measured.gutter.width}x${measured.gutter.height})`;
    expect(measured.gutter.width, message).toBeGreaterThan(0);
    expect(measured.gutter.height, message).toBeGreaterThan(0);
}

test('WR-7: a grid in a tab that was hidden paints correctly once its tab is shown (ADR-0028)', async ({ page }) => {
    await open(page);

    // The premise: the History grid was mounted while its tab was hidden — its box had
    // no size at all — and is not created by the click. The probe survives only on the
    // same element.
    await expect(history(page)).toHaveCount(1);
    await expect(history(page)).toBeHidden();
    await history(page).evaluate((root) => { root.dataset.probe = 'mounted-hidden'; });

    await page.getByRole('tab', { name: 'History' }).click();

    await expect(history(page).locator('.ex-row').first()).toBeVisible();
    expect(await history(page).evaluate((root) => root.dataset.probe)).toBe('mounted-hidden');

    // The painted row is the declared RowHeight (26), not a preset's and not a guess.
    const row = await history(page).locator('.ex-row').first().evaluate((r) => ({
        css: getComputedStyle(r).height,
        box: r.getBoundingClientRect().height,
    }));
    expect(row.css).toBe('26px');
    expect(row.box).toBeCloseTo(26, 1);

    // The Viewport is the box the tab gave it, the gutter taken out: the rows reach the
    // bottom of the readable area and the painted columns are exactly the readable ones.
    await clickCell(history(page), 2, 1);
    await expect(history(page)).toHaveAttribute('aria-activedescendant', /-r2c1$/);
    await expect.poll(async () => sliceMatchesReadableArea(await geometry(history(page), 2)),
        { message: 'the painted columns are exactly the readable ones once the tab is shown' }).toBe(true);
    const shown = await geometry(history(page), 2);
    expectScrollbarsOccupyLayout(shown);
    expect(shown.overflows, 'the columns overflow the Viewport, so End has somewhere to go').toBe(true);
    expect(Math.max(...shown.rows.map((r) => r.bottom))).toBeGreaterThanOrEqual(shown.client.bottom - 1);

    // End: the last column, whole, against the readable edge — not under the vertical
    // scrollbar, whose width the grid could only know from the browser's report.
    await page.keyboard.press('End');
    await expect(history(page)).toHaveAttribute('aria-activedescendant', new RegExp(`-r2c${LAST_COLUMN}$`));
    await expect.poll(async () => {
        const now = await geometry(history(page), 2);
        return now.focus[0] ? now.focus[0].right - now.client.right : undefined;
    }, { message: 'the last column meets the readable edge' }).toBeCloseTo(0, 0);
    expectFocusReadable(await geometry(history(page), 2), 'after End');

    // Ctrl+End: the far corner, clear of both scrollbars.
    await page.keyboard.press('Control+End');
    await expect(history(page)).toHaveAttribute('aria-activedescendant', new RegExp(`-r299c${LAST_COLUMN}$`));
    await expect.poll(async () => {
        const now = await geometry(history(page), 2);
        return now.focus[0] ? now.focus[0].bottom - now.client.bottom : undefined;
    }, { message: 'the last row meets the readable bottom edge' }).toBeCloseTo(0, 0);
    expectFocusReadable(await geometry(history(page), 2), 'after Ctrl+End');
});

test('WR-7: a Drawer toggle resizes the Fill grid and its geometry follows (ADR-0028)', async ({ page }) => {
    await open(page);
    await expect(page.locator('#drawer-status')).toHaveText('Drawer: open');
    const drawerWidth = (await page.locator('#app-drawer').boundingBox()).width;
    expect(drawerWidth).toBeGreaterThan(0);

    await clickCell(positions(page), 2, 1);
    const before = await geometry(positions(page), 2);
    expectScrollbarsOccupyLayout(before);
    expect(before.overflows, 'the columns overflow the Viewport, so End has somewhere to go').toBe(true);
    expect(sliceMatchesReadableArea(before)).toBe(true);

    // Both ways — closed (the grid grows by the Drawer's width) and open again (it
    // shrinks back) — and each time the grid must paint exactly the readable columns and
    // bring the last one whole to the edge. The shrink is the half that catches a stale
    // width: a grid still believing it is wide paints cells past the edge and scrolls the
    // last column only partly into view.
    for (const [state, expected] of [['closed', before.width + drawerWidth], ['open', before.width]]) {
        await page.locator('#drawer-toggle').click();
        await expect(page.locator('#drawer-status')).toHaveText(`Drawer: ${state}`);
        // The main content slides (MudBlazor animates its margin), so the box is waited
        // for; then the paint, which is the grid saying it was told.
        await expect.poll(async () => (await geometry(positions(page), 2)).width, { message: `the Fill grid's width with the Drawer ${state}` })
            .toBeCloseTo(expected, 0);
        await expect.poll(async () => sliceMatchesReadableArea(await geometry(positions(page), 2)),
            { message: `the painted columns are exactly the readable ones with the Drawer ${state}` }).toBe(true);

        // The grid keeps its Focus through the toggle; the keyboard is given back to it
        // without a click, which would move the Focus.
        await positions(page).focus();
        await page.keyboard.press('Home');
        await page.keyboard.press('End');
        await expect(positions(page)).toHaveAttribute('aria-activedescendant', new RegExp(`-r2c${LAST_COLUMN}$`));
        // The last column is right-aligned against the readable edge: the reveal used
        // the width the grid has now. Waited for, because the reveal's scroll lands a
        // frame after the attribute; a reveal computed from a stale width never lands
        // there at all.
        await expect.poll(async () => {
            const now = await geometry(positions(page), 2);
            return now.focus[0] ? now.focus[0].right - now.client.right : undefined;
        }, { message: `the last column meets the readable edge with the Drawer ${state}` }).toBeCloseTo(0, 0);
        const after = await geometry(positions(page), 2);
        expectFocusReadable(after, `after End with the Drawer ${state}`);
        expect(sliceMatchesReadableArea(after)).toBe(true);
    }
});

test('WR-7: the two grids on the main area stay independent (DOM-4, ADR-0018)', async ({ page }) => {
    await open(page);

    await clickCell(positions(page), 1, 1);
    await clickCell(orders(page), 1, 1);
    const positionsFocus = await positions(page).getAttribute('aria-activedescendant');
    expect(positionsFocus).toMatch(/-r1c1$/);

    // Keys in Orders move Orders alone: the capture-phase listener is on each root, never
    // on the document (ADR-0018).
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('ArrowRight');
    await expect(orders(page)).toHaveAttribute('aria-activedescendant', /-r3c2$/);
    expect(await positions(page).getAttribute('aria-activedescendant')).toBe(positionsFocus);

    // And back the other way.
    const ordersFocus = await orders(page).getAttribute('aria-activedescendant');
    await clickCell(positions(page), 1, 1);
    await page.keyboard.press('ArrowDown');
    // Shift+Arrow extends the selection by moving the Focus (ADR-0012).
    await page.keyboard.press('Shift+ArrowRight');
    await expect(positions(page)).toHaveAttribute('aria-activedescendant', /-r2c2$/);
    expect(await orders(page).getAttribute('aria-activedescendant')).toBe(ordersFocus);

    // Each paints its own Focus, and each root has its own id space.
    await expect(positions(page).locator('.ex-focus')).toHaveCount(1);
    await expect(orders(page).locator('.ex-focus')).toHaveCount(1);
    expect(positionsFocus.split('-r')[0]).not.toBe(ordersFocus.split('-r')[0]);

    // Nothing leaks onto the page: no --ex-* on :root, no global, and each root carries
    // its own geometry — Compact from the paper's Dense for Positions, the declared 24
    // for Orders, the declared 26 for the History grid in its hidden tab.
    const leaks = await page.evaluate(() => ({
        rootVars: [...document.documentElement.style].filter((name) => name.startsWith('--ex-')),
        globals: Object.keys(window).filter((key) => key.toLowerCase().startsWith('exgrid')),
        heights: ['#positions-paper', '#orders-paper', '#history-paper'].map((paper) =>
            document.querySelector(`${paper} .ex-grid`).style.getPropertyValue('--ex-row-height')),
    }));
    expect(leaks.rootVars).toEqual([]);
    expect(leaks.globals).toEqual([]);
    expect(leaks.heights).toEqual(['28px', '24px', '26px']);
});

test("WR-7: the toolbar's MudSelect and the grids never interfere (ADR-0018/0039)", async ({ page }) => {
    await open(page);

    // A selection in each grid, and a Focus.
    await clickCell(positions(page), 3, 2);
    await page.keyboard.press('Shift+ArrowRight');
    await page.keyboard.press('Shift+ArrowDown');
    await clickCell(orders(page), 1, 1);
    const snapshot = async () => ({
        positions: await positions(page).evaluate((root) => ({
            active: root.getAttribute('aria-activedescendant'),
            ranges: [...root.querySelectorAll('.ex-range')].map((r) => r.getAttribute('style')),
            scroll: [root.querySelector('.ex-scroller').scrollLeft, root.querySelector('.ex-scroller').scrollTop],
        })),
        orders: await orders(page).evaluate((root) => ({
            active: root.getAttribute('aria-activedescendant'),
            ranges: [...root.querySelectorAll('.ex-range')].map((r) => r.getAttribute('style')),
            scroll: [root.querySelector('.ex-scroller').scrollLeft, root.querySelector('.ex-scroller').scrollTop],
        })),
    });
    const before = await snapshot();
    expect(before.positions.ranges.length).toBeGreaterThan(0);

    const select = page.locator('#currency-select .mud-select').first();
    const list = page.locator('.mud-popover-open');

    // By key: the list is the select's, drawn in MudBlazor's provider outside every grid
    // root, and the keys that work it reach no grid. MudBlazor 9's select takes the value
    // on each arrow and closes on Escape — and that Escape is the select's alone: a grid
    // that heard it would treat it as Leave (ADR-0012).
    await select.click();
    await expect(list.locator('.mud-list-item')).toHaveText(['USD', 'EUR', 'GBP', 'JPY']);
    await expect(page.locator('.ex-grid .mud-popover-open')).toHaveCount(0);
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('ArrowDown');
    await expect(page.locator('#currency-status')).toHaveText('Reporting currency: GBP');
    await page.keyboard.press('Escape');
    await expect(list).toHaveCount(0);
    expect(await snapshot()).toEqual(before);

    // By pointer.
    await select.click();
    await list.locator('.mud-list-item', { hasText: 'JPY' }).click();
    await expect(page.locator('#currency-status')).toHaveText('Reporting currency: JPY');
    await expect(list).toHaveCount(0);
    expect(await snapshot()).toEqual(before);

    // And the other direction: the grid takes its keyboard back and the select keeps
    // its value.
    await positions(page).focus();
    await page.keyboard.press('ArrowDown');
    await expect(positions(page)).toHaveAttribute('aria-activedescendant', /-r5c3$/);
    await expect(page.locator('#currency-status')).toHaveText('Reporting currency: JPY');
    expect(await orders(page).getAttribute('aria-activedescendant')).toBe(before.orders.active);
});

test('WR-7: a grid in a MudDialog opens its popovers whole inside its box, and its Inner Popups above the dialog (ADR-0040/0039)', async ({ page }) => {
    await open(page);
    await page.locator('#open-dialog').click();
    const dialogGrid = page.locator('#dialog-paper .ex-grid');
    await expect(dialogGrid.locator('.ex-row').first()).toBeVisible();

    await dialogGrid.locator('.ex-menu-button').last().click();
    const popover = dialogGrid.locator('.ex-popover');
    await expect(popover).toHaveCount(1);
    await expect(popover.locator('[role=menuitem]').first()).toBeVisible();

    // Every corner of the menu is the menu's own: nothing — the dialog's surface, its
    // actions, its scrolled-away overflow — covers or cuts it. The ancestors whose overflow
    // would cut it are named, so a failure says where.
    const measured = await popover.evaluate((p) => {
        const r = p.getBoundingClientRect();
        const root = p.closest('.ex-grid').getBoundingClientRect();
        const corners = [[r.left + 2, r.top + 2], [r.right - 2, r.top + 2], [r.left + 2, r.bottom - 2], [r.right - 2, r.bottom - 2]]
            .map(([x, y]) => {
                const hit = document.elementFromPoint(x, y);
                return { at: [Math.round(x), Math.round(y)], own: p.contains(hit), hit: hit ? `${hit.tagName.toLowerCase()}.${hit.className}` : 'nothing' };
            });
        const clippedBy = [];
        for (let el = p.parentElement; el; el = el.parentElement) {
            const style = getComputedStyle(el);
            const box = el.getBoundingClientRect();
            if (style.overflow !== 'visible' && (box.bottom < r.bottom - 0.5 || box.right < r.right - 0.5 || box.top > r.top + 0.5 || box.left > r.left + 0.5)) {
                clippedBy.push(`${el.tagName.toLowerCase()}.${el.className} (overflow: ${style.overflow}, bottom ${Math.round(box.bottom)} against the menu's ${Math.round(r.bottom)})`);
            }
        }
        return { corners, clippedBy, bottom: r.bottom, gridBottom: root.bottom, scrolls: p.scrollHeight > p.clientHeight };
    });
    expect(measured.corners.filter((c) => !c.own),
        `corners of the menu that something else covers or clips; clipped by: ${measured.clippedBy.join('; ') || 'nothing'}`).toEqual([]);
    // Inside the grid's box (ADR-0040) — and, in a grid this short, bounded: the menu
    // scrolls rather than reaching past it. The last item is there, a scroll away.
    expect(measured.bottom).toBeLessThanOrEqual(measured.gridBottom + 0.5);
    expect(measured.scrolls, 'the premise: the menu is taller than the room the grid gives it').toBe(true);
    const last = popover.locator('[role=menuitem]').last();
    await last.scrollIntoViewIfNeeded();
    await expect(last).toBeInViewport();
    await page.keyboard.press('Escape');
    await expect(popover).toHaveCount(0);

    // The filter panel's Inner Popup — the operator's list — stands above the dialog: its
    // first entry is what the pointer would press.
    await dialogGrid.locator('.ex-menu-button').nth(3).click(); // Notional: a condition, so an operator
    await popover.locator('[role=menuitem]', { hasText: 'Filter' }).click();
    const operator = popover.getByRole('combobox', { name: 'Operator' });
    await expect(operator).toBeVisible();
    await operator.click();
    const item = page.locator('.mud-popover-open .mud-list-item').first();
    await expect(item).toBeVisible();
    const onTop = await item.evaluate((el) => {
        const r = el.getBoundingClientRect();
        return el.contains(document.elementFromPoint(r.left + r.width / 2, r.top + r.height / 2));
    });
    expect(onTop, 'the Inner Popup is above the dialog').toBe(true);

    await page.keyboard.press('Escape');
    await page.keyboard.press('Escape');
    await expect(popover).toHaveCount(0);
    await page.locator('#close-dialog').click();
    await expect(page.locator('#dialog-paper')).toHaveCount(0);
});

test("WR-9: the application's own controls — theme, tabs, dialog — keep the console clean (CON-1/2/3/6)", async ({ page }) => {
    // Every test in this file holds the console rules through the fixture; this one
    // walks the controls the others do not touch, so their paths are held too.
    await open(page);
    await page.locator('#theme-toggle').click();
    await expect(page.locator('#dark-status')).toHaveText('Dark: True');
    await page.getByRole('tab', { name: 'History' }).click();
    await expect(history(page).locator('.ex-row').first()).toBeVisible();
    await page.getByRole('tab', { name: 'Summary' }).click();
    await expect(history(page)).toBeHidden();
    await page.locator('#open-dialog').click();
    await expect(page.locator('#dialog-paper .ex-row').first()).toBeVisible();
    await page.locator('#close-dialog').click();
    await expect(page.locator('#dialog-paper')).toHaveCount(0);
    await page.locator('#theme-toggle').click();
    await expect(page.locator('#dark-status')).toHaveText('Dark: False');
    await expect(page.locator('#blazor-error-ui')).toBeHidden();
});

test('WR-5 (setup): /features?chrome=mud runs its grids under MudGridChrome, and the column menu opens (FN-17, UX-11)', async ({ page }) => {
    await page.goto('/features?chrome=mud');
    const grid = page.locator('.ex-grid').first();
    await expect(grid.locator('.ex-row').first()).toBeVisible();
    // What the Mud Chrome needs came with it: MudBlazor's popover provider.
    await expect(page.locator('.mud-popover-provider')).toHaveCount(1);

    // The swap took: the Cell Editor is the Wrapper's bare input, not the core's. Without
    // this the rest would pass just as well against the built-in Chrome.
    await clickCell(grid, 0, 1); // Trader, editable
    await page.keyboard.type('X');
    await expect(grid.locator('.ex-editor input.mud-ex-editor')).toHaveValue('X');
    await page.keyboard.press('Escape');
    await expect(grid.locator('.ex-editor')).toHaveCount(0);

    // The column menu opens where UX-11 wants it, and is the first grid's alone. Its
    // content is still the core's own while MudGridChrome.ColumnMenu returns null.
    await grid.locator('.ex-menu-button').last().click();
    const popover = page.locator('.ex-popover');
    await expect(popover).toHaveCount(1);
    await expect(popover).toBeVisible();
    await expect(popover.locator('[role=menuitem]').first()).toBeVisible();
    expect(await page.locator('.ex-grid').nth(1).locator('.ex-popover').count()).toBe(0);
    await popover.press('Escape');
    await expect(popover).toHaveCount(0);
});

test("WR-6: Striped paints the palette's table-stripe colour in both schemes, and a switch re-renders no row (ADR-0038/0030, RR-1)", async ({ page }) => {
    await open(page);

    // The stripe the grid paints and the colour MudBlazor's own striped tables paint,
    // each resolved by the browser through a probe inside the grid's root.
    const colours = () => positions(page).evaluate((root) => {
        const probe = (value) => {
            const el = document.createElement('div');
            el.style.backgroundColor = value;
            root.appendChild(el);
            const colour = getComputedStyle(el).backgroundColor;
            el.remove();
            return colour;
        };
        const striped = root.querySelector('.ex-row.ex-row-stripe');
        return {
            token: probe('var(--ex-row-stripe-background)'),
            palette: probe('var(--mud-palette-table-striped)'),
            rowImage: striped ? getComputedStyle(striped).backgroundImage : null,
        };
    });
    const parity = () => positions(page).evaluate((root) => [...root.querySelectorAll('.ex-row')].map((row) =>
        (Number(row.getAttribute('aria-rowindex')) - 1) % 2 === 1 === row.classList.contains('ex-row-stripe')));

    const light = await colours();
    expect(light.token).toBe(light.palette);
    expect(light.rowImage, 'a striped row paints the stripe').toContain(light.token);
    expect(await parity()).not.toContain(false);

    // Mark the rows: an element a render re-created loses the mark.
    const count = await positions(page).evaluate((root) => {
        const rows = [...root.querySelectorAll('.ex-row')];
        rows.forEach((row, i) => { row.dataset.probe = String(i); });
        return rows.length;
    });
    await page.locator('#theme-toggle').click();
    await expect(page.locator('#dark-status')).toHaveText('Dark: True');

    const dark = await colours();
    expect(dark.token).toBe(dark.palette);
    expect(dark.token, 'the stripe recoloured with the scheme').not.toBe(light.token);
    expect(dark.rowImage).toContain(dark.token);
    const probes = await positions(page).evaluate((root) => [...root.querySelectorAll('.ex-row')].map((row) => row.dataset.probe));
    expect(probes).toEqual([...Array(count).keys()].map(String));
});
