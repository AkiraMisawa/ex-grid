import { test, expect, settled, circuitQuiet } from './fixtures.mjs';
import { sheet, cell } from './sheet-helpers.mjs';

// ADR-0121 / ADR-0122: TODAY() answers the Sheet Day. With no zone given, it is the day in the
// browser's own zone, which the grid is told at attach — on the Server host too, where .NET runs
// in the server's zone and never uses it. /sheet?case=today shows TODAY() as yyyy-mm-dd in A1.
//
// The two browser zones are 25 hours apart, so at any moment at least one of them is on another
// date than the server's (UTC on CI): a Sheet that took the server's zone fails one of the two
// tests whatever the hour. A day read across a midnight is either side of it, so the answer is
// checked against the zone's date just before and just after it is read.

/** The minute in `zone` `ahead` milliseconds from this moment, as yyyy-mm-dd hh:mm. */
function minuteIn(zone, ahead = 0) {
    const parts = Object.fromEntries(new Intl.DateTimeFormat('en-CA', {
        timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23',
    }).formatToParts(new Date(Date.now() + ahead)).map((p) => [p.type, p.value]));
    return `${parts.year}-${parts.month}-${parts.day} ${parts.hour}:${parts.minute}`;
}

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

    test('SH-60 / ADR-0124: NOW() is the moment in the Consumer\'s zone, not the browser\'s', async ({ page }) => {
        await page.goto('/sheet?case=today&zone=Pacific/Kiritimati');
        const a3 = cell(sheet(page), 'A3');
        await expect(a3).toHaveText(/^\d{4}-\d{2}-\d{2}$/);
        const before = dateIn('Pacific/Kiritimati');
        const shown = (await a3.textContent()).trim();
        const after = dateIn('Pacific/Kiritimati');
        expect([before, after]).toContain(shown);
        expect(shown).not.toBe(dateIn('Pacific/Pago_Pago'));
    });

    test('ADR-0121: a fixed day goes before any zone', async ({ page }) => {
        await page.goto('/sheet?case=today&zone=Pacific/Kiritimati&today=2026-09-30');
        await expect(cell(sheet(page), 'A1')).toHaveText('2026-09-30');
        // TODAY() itself takes the short date format, as Microsoft documents.
        await expect(cell(sheet(page), 'A2')).toHaveText('9/30/2026');
    });
});

test.describe('NOW() over time', () => {
    test.use({ freshDocument: true, timezoneId: 'Pacific/Pago_Pago' });

    test('SH-60 / ADR-0124: NOW() moves on as the minute turns, in the Consumer\'s zone', async ({ page }) => {
        // Each reading lies between the zone's minute before the wait for it and after it is read: the
        // strings order as the minutes do. A time is shown rounded to the nearest second, as Excel shows
        // it, so a moment read in the last half second of a minute shows the next one: the minute after is
        // the minute half a second on. The timer due at the turn reads such a moment when it fires a few
        // milliseconds early, as one run by a clock coarser than the one NOW() reads may.
        const zone = 'Pacific/Kiritimati';
        const roundsUpTo = 500;
        const within = (shown, before, after) => expect(before <= shown && shown <= after, `${shown} between ${before} and ${after}`).toBe(true);
        const opened = minuteIn(zone);
        await page.goto(`/sheet?case=today&zone=${zone}`);
        const a4 = cell(sheet(page), 'A4');
        await expect(a4).toHaveText(/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$/);
        const first = (await a4.textContent()).trim();
        within(first, opened, minuteIn(zone, roundsUpTo));

        // The next minute, once it has turned. The wait is the clock's, at most a minute and the
        // timer's lag.
        await expect(a4).not.toHaveText(first, { timeout: 75_000 });
        const shown = (await a4.textContent()).trim();
        within(shown, first, minuteIn(zone, roundsUpTo));
        expect(shown > first).toBe(true);
    });
});

test.describe('a browser that does not say its zone', () => {
    test.use({ freshDocument: true });

    test('SH-60 / ADR-0121/0124: with no zone known, TODAY() and NOW() are #GETTING_DATA, and a zone the Consumer gives answers them', async ({ page }) => {
        // The zone is unread before the grid attaches, so the grid is never told one (ADR-0122).
        await page.addInitScript(() => {
            const resolvedOptions = Intl.DateTimeFormat.prototype.resolvedOptions;
            Intl.DateTimeFormat.prototype.resolvedOptions = function () {
                return { ...resolvedOptions.call(this), timeZone: undefined };
            };
        });
        await page.goto('/sheet?case=today');
        const grid = sheet(page);
        await expect(cell(grid, 'A1')).toHaveText('#GETTING_DATA');
        await expect(cell(grid, 'A3')).toHaveText('#GETTING_DATA');
        await expect(cell(grid, 'A4')).toHaveText('#GETTING_DATA');
        await settled(grid);
        await circuitQuiet();
        await expect(cell(grid, 'A3')).toHaveText('#GETTING_DATA');

        await page.goto('/sheet?case=today&zone=Pacific/Kiritimati');
        await expect(cell(sheet(page), 'A3')).toHaveText(/^\d{4}-\d{2}-\d{2}$/);
        await expect(cell(sheet(page), 'A4')).toHaveText(/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$/);
    });
});
