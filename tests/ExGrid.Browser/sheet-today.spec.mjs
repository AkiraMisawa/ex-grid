import { test, expect } from './fixtures.mjs';
import { sheet, cell } from './sheet-helpers.mjs';

// ADR-0121 / ADR-0122: TODAY() answers the Sheet Day. With no zone given, it is the day in the
// browser's own zone, which the grid is told at attach — on the Server host too, where .NET runs
// in the server's zone and never uses it. /sheet?case=today shows TODAY() as yyyy-mm-dd in A1.
//
// The two browser zones are 25 hours apart, so at any moment at least one of them is on another
// date than the server's (UTC on CI): a Sheet that took the server's zone fails one of the two
// tests whatever the hour. A day read across a midnight is either side of it, so the answer is
// checked against the zone's date just before and just after it is read.

/** The date in `zone` at this moment, as yyyy-mm-dd. */
function dateIn(zone) {
    return new Intl.DateTimeFormat('en-CA', { timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
}

/** A1's day once TODAY() has an answer, and the zone's dates either side of reading it. */
async function shownDay(page, url, zone) {
    await page.goto(url);
    const a1 = cell(sheet(page), 'A1');
    await expect(a1).toHaveText(/^\d{4}-\d{2}-\d{2}$/);
    const before = dateIn(zone);
    const shown = (await a1.textContent()).trim();
    const after = dateIn(zone);
    return { shown, expected: [before, after] };
}

for (const browserZone of ['Pacific/Kiritimati', 'Pacific/Pago_Pago']) {
    test.describe(`a browser in ${browserZone}`, () => {
        test.use({ freshDocument: true, timezoneId: browserZone });

        test(`ADR-0121/0122: TODAY() is the day in the browser's zone, ${browserZone}`, async ({ page }) => {
            const { shown, expected } = await shownDay(page, '/sheet?case=today', browserZone);
            expect(expected).toContain(shown);
        });

        test(`ADR-0124: NOW() is the moment in the browser's zone, ${browserZone}`, async ({ page }) => {
            await page.goto('/sheet?case=today');
            const a3 = cell(sheet(page), 'A3');
            await expect(a3).toHaveText(/^\d{4}-\d{2}-\d{2}$/);
            const before = dateIn(browserZone);
            const shown = (await a3.textContent()).trim();
            const after = dateIn(browserZone);
            expect([before, after]).toContain(shown);
        });
    });
}

test.describe('a zone the Consumer gives', () => {
    test.use({ freshDocument: true, timezoneId: 'Pacific/Pago_Pago' });

    test('ADR-0121: the Consumer\'s zone goes before the browser\'s', async ({ page }) => {
        // Kiritimati is 25 hours ahead of Pago Pago: always another date than the browser's.
        const { shown, expected } = await shownDay(page, '/sheet?case=today&zone=Pacific/Kiritimati', 'Pacific/Kiritimati');
        expect(expected).toContain(shown);
        expect(shown).not.toBe(dateIn('Pacific/Pago_Pago'));
    });

    test('ADR-0121: a fixed day goes before any zone', async ({ page }) => {
        await page.goto('/sheet?case=today&zone=Pacific/Kiritimati&today=2026-09-30');
        await expect(cell(sheet(page), 'A1')).toHaveText('2026-09-30');
        // TODAY() itself takes the short date format, as Microsoft documents.
        await expect(cell(sheet(page), 'A2')).toHaveText('9/30/2026');
    });
});
