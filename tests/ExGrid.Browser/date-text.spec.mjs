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

// What each locale's test read, for the last test to compare.
const seen = {};

for (const locale of ['en-US', 'en-GB', 'ja-JP']) {
    test.describe(`under ${locale}`, () => {
        // A context of the locale's own: the browser's language is fixed when the context is made.
        test.use({ locale, freshDocument: true });

        test(`ADR-0006 (2026-10-01): a date column without a Format shows yyyy-MM-dd HH:mm:ss under ${locale}`, async ({ page }) => {
            const texts = await tradeDates(page);
            for (const text of texts) expect(text).toMatch(ISO_DATE_TIME);
            seen[locale] = texts;
        });
    });
}

test('ADR-0006 (2026-10-01): the same dates read the same under en-US, en-GB and ja-JP', () => {
    // Alone, under --grep or --last-failed, the locales' tests have not run in this worker.
    test.skip(Object.keys(seen).length < 3, 'runs after the three locales\' tests, whose texts it compares');
    expect(seen['en-GB']).toEqual(seen['en-US']);
    expect(seen['ja-JP']).toEqual(seen['en-US']);
});
