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
