import { test, expect, setRoundTrip } from './fixtures.mjs';
import { SERVER } from './hosting.mjs';

// What a Blazor Server circuit can fail and a WebAssembly tab cannot (Definition of Done
// §24): keys typed faster than a round trip (ED-22), a paste past the hub's message
// limit (CP-21), a clipboard write the browser rejects (CP-23), the Prerendered paint
// (A11Y-20), and two users over one store (SRV-3). Every test here runs on both hosts
// unless it says why not; on WebAssembly there is no round trip to add, and the same
// test is the case without one.

function grid(page) {
    return page.locator('.ex-grid').first();
}

async function clickCell(page, row, column) {
    // Cells are pointer-events: none — the Viewport is the delegated target (ADR-0004).
    await grid(page).locator(`[id$='r${row}c${column}']`).click({ force: true });
}

async function openFeatures(page) {
    await page.goto('/features');
    await expect(grid(page).locator('.ex-row').first()).toBeVisible();
    // Interactive, not merely painted: on Server the prerendered grid is on screen
    // before its circuit connects, and takes no tab stop until it has (A11Y-20).
    await expect(grid(page)).toHaveAttribute('tabindex', '0');
}

test.describe('keys typed faster than the round trip are neither lost nor reordered (ED-22, SRV-5)', () => {
    test.beforeEach(async ({ page }) => {
        await openFeatures(page);
        await setRoundTrip(150);
    });

    test('a number typed at full speed is committed whole', async ({ page }) => {
        await clickCell(page, 0, 1);                  // Trader, editable
        await page.keyboard.type('1500');
        await page.keyboard.press('Enter');

        await expect(page.locator('#edit-status')).toContainText('Trader=1500');
        await expect(grid(page).locator("[id$='r0c1']")).toHaveText('1500');
    });

    test('continuous entry puts each value in its own cell', async ({ page }) => {
        await clickCell(page, 0, 1);
        await page.keyboard.type('150');
        await page.keyboard.press('Enter');
        await page.keyboard.type('200');
        await page.keyboard.press('Enter');

        await expect(grid(page).locator("[id$='r0c1']")).toHaveText('150');
        await expect(grid(page).locator("[id$='r1c1']")).toHaveText('200');
    });

    test('keys held behind a key that opened nothing replay as ordinary keys', async ({ page }) => {
        await clickCell(page, 0, 0);                  // Book, not editable
        await page.keyboard.type('x');
        await page.keyboard.press('ArrowDown');

        await expect.poll(() => grid(page).getAttribute('aria-activedescendant')).toMatch(/r1c0$/);
        await expect(grid(page).locator('input.ex-editor')).toHaveCount(0);
    });
});

test('a paste past a Server hub\'s message limit arrives whole (CP-21)', async ({ page, context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    await openFeatures(page);
    // Two cells of about 100 KB each in the HTML flavour, as a spreadsheet puts on the
    // clipboard: some 200 KB, six times the 32 KB a hub accepts in one message.
    await page.evaluate(async () => {
        const cell = (c) => c.repeat(100_000);
        const html = `<table><tr><td>${cell('A')}</td></tr><tr><td>${cell('B')}</td></tr></table>`;
        const text = `${cell('A')}\r\n${cell('B')}\r\n`;
        await navigator.clipboard.write([new ClipboardItem({
            'text/html': new Blob([html], { type: 'text/html' }),
            'text/plain': new Blob([text], { type: 'text/plain' }),
        })]);
    });
    await clickCell(page, 0, 1);
    await page.keyboard.press('Shift+ArrowDown');

    await page.keyboard.press('ControlOrMeta+V');

    await expect(page.locator('#paste-status')).toContainText('2 cells from 2x1');
    // The circuit is still there: the grid still answers a key.
    await page.keyboard.press('ArrowDown');
    await expect.poll(() => grid(page).getAttribute('aria-activedescendant')).toMatch(/r2c1$/);
});

test('a clipboard write the browser rejects is refused by name (CP-23)', async ({ page }) => {
    await openFeatures(page);
    // The browser's rejection, as Chromium raises it for a denied permission or a lost
    // activation. Stubbed rather than provoked: CDP's permission override does not reach
    // a Playwright browser context, and what is under test is the grid's answer to the
    // rejection, not the browser's reasons for it.
    await page.evaluate(() => {
        navigator.clipboard.write = () => Promise.reject(
            new DOMException('Write permission denied.', 'NotAllowedError'));
    });
    await clickCell(page, 0, 1);

    await grid(page).locator("[id$='r0c1']").click({ button: 'right', force: true });
    await grid(page).locator('[role=menu] button[role=menuitem]').filter({ hasText: /^Copy$/ }).click();

    await expect(page.locator('#copy-status')).toContainText('ClipboardUnavailable');
});

test('keys typed together in a menu run the item the keys chose (ADR-0039, SRV-5)', async ({ page, context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    await openFeatures(page);
    await clickCell(page, 0, 1);
    await page.keyboard.press('Shift+ArrowDown');
    await page.keyboard.press('Shift+F10');
    const menu = grid(page).locator('.ex-popover[role=menu]');
    await expect(menu).toBeVisible();
    await expect.poll(() => page.evaluate(() => document.activeElement?.textContent?.trim())).toBe('Copy');
    await page.evaluate(() => navigator.clipboard.writeText('SENTINEL'));
    await setRoundTrip(150);

    // ↓ chooses "Copy with headers"; Enter, typed straight after, runs it. The menu's own
    // DOM focus moves a round trip after the ↓, and a key resolved against the item that
    // holds focus when it lands would run "Copy" instead — the other copy, quietly.
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('Enter');

    await expect.poll(() => page.evaluate(() => navigator.clipboard.readText()), { timeout: 5000 })
        .toMatch(/^Trader\r?\n/);
});

test('keys typed straight after a key that opens a menu reach the menu (KB-33, ADR-0010/0039)', async ({ page }) => {
    await openFeatures(page);
    await clickCell(page, 1, 0);
    await expect(grid(page)).toHaveAttribute('aria-activedescendant', /r1c0$/);
    await setRoundTrip(150);

    // Alt+↓ opens Book's menu a round trip later; ↓ and Enter typed with it are the
    // menu's — Sort descending — not the grid's, which would move the Focus.
    await page.keyboard.press('Alt+ArrowDown');
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('Enter');

    // Descending, not ascending: had the ↓ reached the grid, the Enter would have run the
    // menu's first item. (The sort then drops the selection, as ADR-0011 has it.)
    await expect(grid(page).locator('.ex-header-cell').first()).toHaveAttribute('aria-sort', 'descending');
});

for (const chrome of ['builtin', 'mud']) {
    test(`letters typed straight after E are the search's, never a command's (ADR-0044, SRV-5, ${chrome})`, async ({ page }) => {
        await page.goto(`/features?chrome=${chrome}`);
        await expect(grid(page)).toHaveAttribute('tabindex', '0');
        await clickCell(page, 1, 0);
        await page.keyboard.press('Alt+ArrowDown');
        const search = grid(page).locator('.ex-popover input[type=search], .ex-popover .mud-ex-grid-filter-search input');
        await expect(search).toBeVisible();
        await expect.poll(() => page.evaluate(() => document.activeElement?.closest('[role=menu]') !== null)).toBe(true);
        await setRoundTrip(150);

        // E asks for the search box, which takes DOM focus a round trip later; the S and O
        // typed meanwhile still land on the command, where they would sort and close.
        await page.keyboard.press('e');
        await page.keyboard.type('so');

        await expect.poll(() => page.evaluate(() => document.activeElement?.closest('.ex-popover [role=dialog], .ex-popover[role=dialog]') !== null
            && document.activeElement?.tagName === 'INPUT')).toBe(true);
        await page.waitForTimeout(600);
        await expect(grid(page).locator('.ex-popover')).toHaveCount(1);
        await expect(grid(page).locator('.ex-header-cell[aria-sort=ascending], .ex-header-cell[aria-sort=descending]')).toHaveCount(0);
    });
}

for (const [what, column, open, closes] of [
    ['the operator list', 2, async (page) => grid(page).getByRole('combobox', { name: 'Operator' }).click(), true],
    ['the date calendar', 4, async (page) => grid(page).locator('.mud-ex-grid-filter-operand button').first().click(), false],
]) {
    test(`an Escape pressed straight after ${what} opens is not the grid's (KB-35, ADR-0039)`, async ({ page }) => {
        await page.goto('/features?chrome=mud');
        await expect(grid(page)).toHaveAttribute('tabindex', '0');
        await clickCell(page, 1, column);
        await page.keyboard.press('Alt+ArrowDown');
        await expect(grid(page).locator('.mud-ex-grid-filter')).toBeVisible();
        // Tab from the commands into the panel's operator (ADR-0044), and on to the date's
        // field for the calendar.
        await expect.poll(() => page.evaluate(() => !!document.activeElement?.closest('[role=menu]'))).toBe(true);
        await page.keyboard.press('Tab');
        await expect.poll(() => page.evaluate(() => document.activeElement?.getAttribute('role'))).toBe('combobox');
        if (column === 4) {
            await page.keyboard.press('Tab');
        }
        await setRoundTrip(150);

        await open(page);
        await expect(page.locator('.mud-popover-open')).not.toHaveCount(0);
        await page.keyboard.press('Escape');

        // Had the grid taken it, the panel would close a round trip later. Absence cannot
        // be waited for, so four round trips are given for it to happen — then the panel
        // must still stand.
        await page.waitForTimeout(600);
        await expect(grid(page).locator('.mud-ex-grid-filter')).toBeVisible();
        // Whether the popup closes on it is the design system's: MudBlazor's select closes
        // its list; its date picker ignores an Escape while DOM focus is still on the
        // button that opened the calendar — which, a round trip in, it is.
        if (closes) {
            await expect(page.locator('.mud-popover-open')).toHaveCount(0);
        }
    });
}

// The Wrapper's filter panel, on Notional (a condition, so an operator), with the keyboard
// moved into the operator by Tab from the commands above it (ADR-0044).
async function openMudNotionalPanel(page) {
    await page.goto('/features?chrome=mud');
    await expect(grid(page)).toHaveAttribute('tabindex', '0');
    await clickCell(page, 1, 2);
    await page.keyboard.press('Alt+ArrowDown');
    const panel = grid(page).locator('.mud-ex-grid-filter');
    await expect(panel).toBeVisible();
    await expect.poll(() => page.evaluate(() => !!document.activeElement?.closest('[role=menu]'))).toBe(true);
    await page.keyboard.press('Tab');
    await expect.poll(() => page.evaluate(() => document.activeElement?.getAttribute('role'))).toBe('combobox');
    return panel;
}

test('a value typed and Enter pressed at once apply the condition (KB-31, SRV-5)', async ({ page }) => {
    const panel = await openMudNotionalPanel(page);
    const before = await grid(page).getAttribute('aria-rowcount');
    await panel.getByRole('combobox', { name: 'Operator' }).click();
    await page.locator('.mud-popover-open .mud-list-item', { hasText: /^>$/ }).click();
    await expect(page.locator('.mud-popover-open .mud-list-item')).toHaveCount(0);
    await setRoundTrip(150);

    // Apply is unavailable until the value is given (WR-2), and the value reaches the
    // circuit a round trip after it is typed. The Enter typed with it is the user's
    // request to apply that value, not a key to drop because the button had not caught up.
    const operand = panel.locator('.mud-ex-grid-filter-operand input');
    await operand.fill('3000000');
    await operand.press('Enter');

    await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
    await expect.poll(() => grid(page).getAttribute('aria-rowcount')).not.toBe(before);
});

test('Escape, Escape straight after an Inner Popup closes it and then the panel (ADR-0039, SRV-5)', async ({ page }) => {
    const panel = await openMudNotionalPanel(page);
    await panel.getByRole('combobox', { name: 'Operator' }).click();
    await expect(page.locator('.mud-popover-open .mud-list-item').first()).toBeVisible();
    // The report that the list is open has reached the key gate before the round trip grows.
    await page.waitForTimeout(300);
    await setRoundTrip(150);

    // The first is the list's; the second, typed before the list's closing could be
    // reported back, is the grid's (ADR-0039).
    await page.keyboard.press('Escape');
    await page.keyboard.press('Escape');

    await expect(page.locator('.mud-popover-open')).toHaveCount(0);
    await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
});

test('a menu taking the keyboard a round trip late keeps the scroll the user gave it (ADR-0039/0040, SRV-5)', async ({ page }) => {
    await page.goto('/features?chrome=mud');
    const short = page.locator('.ex-grid').nth(1);
    await expect(short).toHaveAttribute('tabindex', '0');
    await setRoundTrip(150);

    // The second grid is 140px tall, so its column menu scrolls (UX-11). Opened by pointer,
    // it takes the keyboard a round trip later; the user has already scrolled to its end.
    await short.locator('.ex-menu-button').first().click();
    const popover = short.locator('.ex-popover');
    await expect(popover.locator('[role=menuitem]').first()).toBeVisible();
    const last = popover.locator('[role=menuitem]').last();
    await last.scrollIntoViewIfNeeded();

    // Two round trips for the focus request to land; the view must not have moved.
    await page.waitForTimeout(600);
    await expect(last).toBeInViewport();
});

test('a Prerendered grid is busy and takes no tab stop until its circuit connects (A11Y-20)', async ({ page }) => {
    test.skip(!SERVER, 'WebAssembly has no prerender: its grid is interactive from its first paint');
    // The document as the server sends it, before any script has run.
    const html = await (await page.request.get('/features')).text();
    const root = html.match(/<div class="ex-grid[^"]*"[^>]*>/)?.[0] ?? '';

    expect(root).toContain('ex-loading');
    expect(root).toContain('aria-busy="true"');
    expect(root).not.toContain('tabindex');

    await openFeatures(page);
    await expect(grid(page)).not.toHaveAttribute('aria-busy', /.*/);
    await expect(grid(page)).not.toHaveClass(/ex-loading/);
});

test.describe('two users, one store (SRV-3, ADR-0018)', () => {
    test.skip(!SERVER, 'WebAssembly runs one user per process: there is nobody to share the store with');
    // The second user's refusal is an exception by decision (ADR-0018), caught by the
    // page's ErrorBoundary, which logs it: the fixture asserts it is there.
    test.use({ expectedHostLog: [/ErrorBoundary: .*ADR-0018/] });

    test('a store change reaches both, a sort reaches one, and a shared source is refused', async ({ page, browser }) => {
        const other = await browser.newContext();
        const second = await other.newPage();
        try {
            await page.goto('/shared');
            await second.goto('/shared');
            for (const p of [page, second]) {
                await expect(grid(p)).toHaveAttribute('tabindex', '0');
            }
            const notional = (p) => grid(p).locator("[id$='r0c2']");
            const before = await notional(page).textContent();

            // The second user revalues; the first user's grid follows.
            await second.locator('#revalue').click();
            await expect(notional(page)).not.toHaveText(before ?? '');
            await expect(notional(second)).toHaveText((await notional(page).textContent()) ?? '');

            // The first user sorts; the second user's order and selection stay theirs.
            await clickCell(second, 3, 0);
            await expect.poll(() => grid(second).getAttribute('aria-activedescendant')).toMatch(/r3c0$/);
            const secondFirstBook = await grid(second).locator("[id$='r0c0']").textContent();
            const secondActive = await grid(second).getAttribute('aria-activedescendant');
            await grid(page).locator('.ex-header-cell').nth(2)
                .click({ position: { x: 30, y: 14 }, force: true });
            await expect(grid(page).locator('.ex-header-cell').nth(2)).toHaveAttribute('aria-sort', 'ascending');
            await expect(grid(second).locator("[id$='r0c0']")).toHaveText(secondFirstBook ?? '');
            expect(await grid(second).getAttribute('aria-activedescendant')).toBe(secondActive);

            // One bundled source for everyone: the first user may attach it; the second
            // is refused by name, and says so rather than showing the first user's grid.
            await page.locator('#attach-shared').click();
            await expect(page.locator('#one-source-grid .ex-grid')).toBeVisible();
            await second.locator('#attach-shared').click();
            await expect(second.locator('#shared-refusal')).toContainText('ADR-0018');
            await expect(second.locator('#one-source-grid')).toHaveCount(0);
        } finally {
            await other.close();
        }
    });
});
