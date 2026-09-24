import { test, expect, record } from './fixtures.mjs';

// The drag gestures with a real mouse (ADR-0011/0016/0032), the large-paste and
// off-screen-paste rules (ADR-0014/0015) with the paste's own number (PST-6), and the
// remaining measured UX rows. The other observational numbers are in
// observational.spec.mjs. Console errors fail the run, as everywhere.

function grid(page) {
    return page.locator('.ex-grid').first();
}

async function open(page) {
    await page.goto('/features');
    await expect(grid(page).locator('.ex-row').first()).toBeVisible();
}

async function headerBox(page) {
    return grid(page).locator('.ex-header').boundingBox();
}

test('a leaf drag reorders within its group and clamps at its edge (HG-14/HG-15)', async ({ page }) => {
    await open(page);
    const box = await headerBox(page);
    const leafY = box.y + 42; // the leaf tier of a 28px × 2 band

    // Drag Book (0-110) far right, way past its group's edge (Who = Book, Trader).
    await page.mouse.move(box.x + 55, leafY);
    await page.mouse.down();
    await page.mouse.move(box.x + 600, leafY, { steps: 8 });
    // The indicator clamps at the group's edge: Book+Trader end at 230px.
    const indicator = await grid(page).locator('.ex-drop-indicator').boundingBox();
    expect(indicator.x - box.x).toBeLessThanOrEqual(231);
    await page.mouse.up();

    // The order moved within the group only — Book after Trader, nothing else.
    await expect(page.locator('#order-status')).toContainText(
        'Order: Trader,Book,Notional,Narrow,TradeDate,Confirmed');
});

test('grabbing a group rectangle moves the whole group (HG-14)', async ({ page }) => {
    await open(page);
    const box = await headerBox(page);
    const tierY = box.y + 14; // the upper tier

    // Grab Who's rectangle (over Book+Trader) and drag past What.
    await page.mouse.move(box.x + 100, tierY);
    await page.mouse.down();
    await page.mouse.move(box.x + 700, tierY, { steps: 8 });
    await page.mouse.up();

    await expect(page.locator('#order-status')).toContainText(
        'Order: Notional,Narrow,TradeDate,Confirmed,Book,Trader');
});

test('a resize drag with a real mouse applies on release (ADR-0016)', async ({ page }) => {
    await open(page);
    const cell = await grid(page).locator('.ex-header-cell').nth(1).boundingBox();

    // The grip is the cell's right-hand 5px, in the leaf tier.
    await page.mouse.move(cell.x + cell.width - 2, cell.y + cell.height - 10);
    await page.mouse.down();
    await page.mouse.move(cell.x + cell.width + 38, cell.y + cell.height - 10, { steps: 4 });
    await expect(grid(page).locator('.ex-resize-guide')).toBeVisible();
    await page.mouse.up();

    await expect(page.locator('#width-status')).toContainText('Trader=160');
    // The click that ended the drag did not sort.
    await expect(grid(page).locator('.ex-header-cell').nth(1)).toHaveAttribute('aria-sort', 'none');
});

test('pasting onto an off-screen selection works, and the indicator showed first (PST-3)', async ({ page, context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    await open(page);
    await grid(page).locator("[id$='r0c1']").click({ force: true });
    await page.keyboard.press('Shift+ArrowDown');

    // Scroll the selection far off screen: the status line says so.
    await grid(page).locator('.ex-scroller').evaluate((el) => { el.scrollTop = 6000; });
    await expect(page.locator('.ex-status')).toContainText('outside the visible range');

    await page.evaluate(() => navigator.clipboard.writeText('offscreen'));
    await page.keyboard.press('ControlOrMeta+V');

    await expect(page.locator('#paste-status')).toContainText('2 cells from 1x1');
});

test('a ~10MB paste parses without freezing the grid (PST-5, PST-6 recorded)', async ({ page, context }, testInfo) => {
    test.setTimeout(120000);
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    await open(page);
    await grid(page).locator("[id$='r0c1']").click({ force: true });
    await page.keyboard.press('ControlOrMeta+Shift+ArrowDown'); // the whole column, 400 rows

    await page.evaluate(() => {
        // 400 rows × one wide cell ≈ 10 MB of TSV.
        const cell = 'x'.repeat(26000);
        const rows = Array.from({ length: 400 }, () => cell);
        return navigator.clipboard.writeText(rows.join('\n'));
    });

    const before = Date.now();
    await page.keyboard.press('ControlOrMeta+V');
    await expect(page.locator('#paste-status')).toContainText('400 cells from 400x1', { timeout: 30000 });
    const parseMs = Date.now() - before;

    // The grid answers a key within 500ms of the paste completing (PST-5).
    const keyBefore = Date.now();
    await page.keyboard.press('ArrowUp'); // the Focus sits on the last row after Ctrl+Shift+Down
    await expect
        .poll(async () => grid(page).getAttribute('aria-activedescendant'), { timeout: 500 })
        .toMatch(/r398c1$/);
    const keyMs = Date.now() - keyBefore;

    record(testInfo.project.name, {
        'PST-6': { sourceCells: 400, bytes: 26001 * 400, parseAndIntentMs: parseMs, nextKeyMs: keyMs },
    });
});

test('the focus outline holds 3:1 against the cell ground under the default theme (UX-9)', async ({ page }) => {
    await open(page);
    await grid(page).locator("[id$='r1c1']").click({ force: true });

    const contrast = await page.evaluate(() => {
        const focus = [...document.querySelectorAll('.ex-focus')]
            .find((el) => el.getBoundingClientRect().width > 0);
        const parse = (c) => c.match(/\d+(\.\d+)?/g).slice(0, 3).map(Number);
        const luminance = ([r, g, b]) => {
            const f = (v) => {
                v /= 255;
                return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4;
            };
            return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b);
        };
        const outline = luminance(parse(getComputedStyle(focus).outlineColor));
        const ground = luminance(parse(getComputedStyle(document.querySelector('.ex-grid')).backgroundColor));
        return (Math.max(outline, ground) + 0.05) / (Math.min(outline, ground) + 0.05);
    });
    expect(contrast).toBeGreaterThanOrEqual(3);
});

test('narrowing the scrollbar by token changes the gutter and the geometry follows (UX-10)', async ({ page }) => {
    await open(page);
    await grid(page).locator("[id$='r1c1']").click({ force: true });

    const gutterBefore = await grid(page).locator('.ex-scroller')
        .evaluate((el) => el.offsetWidth - el.clientWidth);
    await page.evaluate(() => {
        document.body.style.setProperty('--ex-scrollbar-width', '8px');
        document.body.style.setProperty('--ex-scrollbar-color', 'rgba(120,120,120,0.6)');
    });
    await page.waitForTimeout(200);
    const gutterAfter = await grid(page).locator('.ex-scroller')
        .evaluate((el) => el.offsetWidth - el.clientWidth);
    expect(gutterAfter).toBe(8);
    expect(gutterAfter).not.toBe(gutterBefore === 8 ? -1 : gutterBefore);

    // The Focus stays inside the readable area after the change — the scrollbar
    // suite's own invariant, re-checked here.
    await page.keyboard.press('End');
    const fits = await page.evaluate(() => {
        const scroller = document.querySelector('.ex-scroller');
        const outer = scroller.getBoundingClientRect();
        const right = outer.left + scroller.clientLeft + scroller.clientWidth;
        const focus = [...document.querySelectorAll('.ex-focus')]
            .map((el) => el.getBoundingClientRect())
            .find((r) => r.width > 0);
        return { fits: focus.right <= right + 1, focusRight: focus.right, right };
    });
    expect(fits.fits, JSON.stringify(fits)).toBe(true);
});

test('a composing IME keydown is never taken (ED-11, the listener guard)', async ({ page }) => {
    await open(page);
    await grid(page).locator("[id$='r0c1']").click({ force: true });
    const before = await grid(page).getAttribute('aria-activedescendant');

    // A synthetic composing keydown: the capture listener must let it pass — taking
    // Enter or an arrow mid-composition breaks typing in any language that needs one.
    const results = await page.evaluate(() => {
        const root = document.querySelector('.ex-grid');
        const send = (init) => {
            const event = new KeyboardEvent('keydown', { bubbles: true, cancelable: true, ...init });
            root.dispatchEvent(event);
            return event.defaultPrevented;
        };
        return {
            composingEnter: send({ key: 'Enter', isComposing: true }),
            composingArrow: send({ key: 'ArrowDown', isComposing: true }),
            keyCode229: send({ key: 'Process', keyCode: 229 }),
        };
    });
    expect(results).toEqual({ composingEnter: false, composingArrow: false, keyCode229: false });
    // And the Focus did not move under the half-finished word.
    expect(await grid(page).getAttribute('aria-activedescendant')).toBe(before);
});
