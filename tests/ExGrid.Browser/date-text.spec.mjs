import { test, expect } from './fixtures.mjs';

// ADR-0006's note of 2026-10-01 (ticket 94): a date column that declares no Format shows its values
// in one ISO form by type, whatever the culture the code runs under — on WebAssembly the browser's
// language, on the Server host the server's. /virtual's "Trade date" is a DateTime column with no
// Format, so each cell reads yyyy-MM-dd HH:mm:ss, and the same under every locale.

const ISO_DATE_TIME = /^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$/;

// The first rows' Trade date texts, once the page's first answer has replaced the Placeholders.
async function tradeDates(page) {
    await page.goto('/virtual');
    const grid = page.locator('.ex-grid').first();
    const headers = (await grid.locator('.ex-header-cell').allTextContents()).map((text) => text.trim());
    const column = headers.findIndex((text) => text.startsWith('Trade date'));
    expect(column, `a Trade date header among ${headers.join(', ')}`).toBeGreaterThanOrEqual(0);
    const rows = grid.locator('.ex-row');
    await expect(rows.first().locator('.ex-cell').nth(column)).toHaveText(ISO_DATE_TIME);
    const texts = [];
    for (let row = 0; row < 5; row++) texts.push((await rows.nth(row).locator('.ex-cell').nth(column).textContent()).trim());
    return texts;
}

// The Formula Bar's full value of /virtual?bar=1's first Trade date, once the cell is pressed
// (ADR-0051; ticket 95).
async function barOfFirstTradeDate(page) {
    await page.goto('/virtual?bar=1');
    const grid = page.locator('.ex-grid').first();
    const headers = (await grid.locator('.ex-header-cell').allTextContents()).map((text) => text.trim());
    const column = headers.findIndex((text) => text.startsWith('Trade date'));
    const cell = grid.locator('.ex-row').first().locator('.ex-cell').nth(column);
    await expect(cell).toHaveText(ISO_DATE_TIME);
    // Cells are pointer-events: none by design, and the Viewport is the delegated target
    // (ADR-0004), so the click goes through to it as a user's does.
    await cell.click({ force: true });
    const bar = grid.locator('.ex-formula-bar-text');
    await expect(bar).toHaveValue(ISO_DATE_TIME);
    expect(await bar.inputValue()).toBe((await cell.textContent()).trim());
    return bar.inputValue();
}

// What each locale's tests read, for the last test to compare.
const seen = {};
const bars = {};

for (const locale of ['en-US', 'en-GB', 'ja-JP']) {
    test.describe(`under ${locale}`, () => {
        // A context of the locale's own: the browser's language is fixed when the context is made.
        test.use({ locale, freshDocument: true });

        test(`ADR-0006 (2026-10-01): a date column without a Format shows yyyy-MM-dd HH:mm:ss under ${locale}`, async ({ page }) => {
            const texts = await tradeDates(page);
            for (const text of texts) expect(text).toMatch(ISO_DATE_TIME);
            seen[locale] = texts;
        });

        test(`ADR-0006 (ticket 95): the Formula Bar shows an unformatted date in its ISO form under ${locale}`, async ({ page }) => {
            bars[locale] = await barOfFirstTradeDate(page);
        });
    });
}

test('ADR-0006 (2026-10-01): the same dates read the same under en-US, en-GB and ja-JP', () => {
    // Alone, under --grep or --last-failed, the locales' tests have not run in this worker.
    test.skip(Object.keys(seen).length < 3, 'runs after the three locales\' tests, whose texts it compares');
    expect(seen['en-GB']).toEqual(seen['en-US']);
    expect(seen['ja-JP']).toEqual(seen['en-US']);
});

test('ADR-0006 (ticket 95): the Formula Bar reads the same date the same under en-US, en-GB and ja-JP', () => {
    test.skip(Object.keys(bars).length < 3, 'runs after the three locales\' tests, whose bars it compares');
    expect(bars['en-GB']).toBe(bars['en-US']);
    expect(bars['ja-JP']).toBe(bars['en-US']);
});
