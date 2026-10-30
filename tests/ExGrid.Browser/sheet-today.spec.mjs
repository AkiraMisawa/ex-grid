import { test, expect, settled, circuitQuiet } from './fixtures.mjs';
import { sheet, cell } from './sheet-helpers.mjs';

// ADR-0121 / ADR-0122: TODAY() answers the Sheet Day. With no zone given, it is the day in the
// browser's own zone, which the grid is told at attach — on the Server host too, where .NET runs
// in the server's zone and never uses it. /sheet?case=today shows TODAY() as yyyy-mm-dd in A1.
//
// The two browser zones are 25 hours apart, so at any moment at least one of them is on another
// date than the server's (UTC on CI): a Sheet that took the server's zone fails one of the two
// tests whatever the hour. A day read across a midnight is either side of it, so the answer is
// checked against the zone's date before the page is opened and the latest date it can show once
// it is read.
//
// A moment is shown rounded to the nearest second, as Excel shows it (NumberFormat's FormatDate), so
// NOW() read in a minute's last half second already shows the next minute, and the next day at
// midnight: the latest a NOW() reading can show is the clock half a second on. TODAY() is a day, and
// is not rounded. The bounds read this machine's clock, which is the one NOW() reads on either host
// as long as the host runs here, as it does locally and on CI.
const nowShownAheadMs = 500;

/** The minute in `zone` at `at` (this moment by default), as yyyy-mm-dd hh:mm. */
function minuteIn(zone, at = Date.now()) {
    const parts = Object.fromEntries(new Intl.DateTimeFormat('en-CA', {
        timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23',
    }).formatToParts(new Date(at)).map((p) => [p.type, p.value]));
    return `${parts.year}-${parts.month}-${parts.day} ${parts.hour}:${parts.minute}`;
}

/** The date in `zone` at `at` (this moment by default), as yyyy-mm-dd. */
function dateIn(zone, at = Date.now()) {
    return new Intl.DateTimeFormat('en-CA', { timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date(at));
}

/** The latest minute a NOW() read by now can show, in `zone`. */
const latestMinuteShown = (zone) => minuteIn(zone, Date.now() + nowShownAheadMs);

/** The latest date a NOW() read by now can show, in `zone`. */
const latestDateShown = (zone) => dateIn(zone, Date.now() + nowShownAheadMs);

/** Expects `shown` between `before` and `after`: the strings order as the dates and minutes do. */
const within = (shown, before, after) => expect(before <= shown && shown <= after, `${shown} between ${before} and ${after}`).toBe(true);

/**
 * Opens `url` and expects the day `address` shows, once it has one, between the zone's date before
 * the page was opened and the latest date it can show once read: `dateIn` for TODAY(),
 * `latestDateShown` for a NOW(). Returns the day shown.
 */
async function expectShownDay(page, url, address, zone, latest) {
    const before = dateIn(zone);
    await page.goto(url);
    const shownCell = cell(sheet(page), address);
    await expect(shownCell).toHaveText(/^\d{4}-\d{2}-\d{2}$/);
    const shown = (await shownCell.textContent()).trim();
    within(shown, before, latest(zone));
    return shown;
}

for (const browserZone of ['Pacific/Kiritimati', 'Pacific/Pago_Pago']) {
    test.describe(`a browser in ${browserZone}`, () => {
        test.use({ freshDocument: true, timezoneId: browserZone });

        test(`ADR-0121/0122: TODAY() is the day in the browser's zone, ${browserZone} @behaviour`, async ({ page }) => {
            await expectShownDay(page, '/sheet?case=today', 'A1', browserZone, dateIn);
        });

        test(`ADR-0124: NOW() is the moment in the browser's zone, ${browserZone} @behaviour`, async ({ page }) => {
            await expectShownDay(page, '/sheet?case=today', 'A3', browserZone, latestDateShown);
        });
    });
}

test.describe('a zone the Consumer gives', () => {
    test.use({ freshDocument: true, timezoneId: 'Pacific/Pago_Pago' });

    test('ADR-0121: the Consumer\'s zone goes before the browser\'s @behaviour', async ({ page }) => {
        // Kiritimati is 25 hours ahead of Pago Pago: always another date than the browser's.
        const shown = await expectShownDay(page, '/sheet?case=today&zone=Pacific/Kiritimati', 'A1', 'Pacific/Kiritimati', dateIn);
        expect(shown).not.toBe(dateIn('Pacific/Pago_Pago'));
    });

    test('SH-60 / ADR-0124: NOW() is the moment in the Consumer\'s zone, not the browser\'s @behaviour', async ({ page }) => {
        const shown = await expectShownDay(page, '/sheet?case=today&zone=Pacific/Kiritimati', 'A3', 'Pacific/Kiritimati', latestDateShown);
        expect(shown).not.toBe(dateIn('Pacific/Pago_Pago'));
    });

    test('ADR-0121: a fixed day goes before any zone @behaviour', async ({ page }) => {
        await page.goto('/sheet?case=today&zone=Pacific/Kiritimati&today=2026-09-30');
        await expect(cell(sheet(page), 'A1')).toHaveText('2026-09-30');
        // TODAY() itself takes the short date format, as Microsoft documents.
        await expect(cell(sheet(page), 'A2')).toHaveText('9/30/2026');
    });
});

test.describe('NOW() over time', () => {
    test.use({ freshDocument: true, timezoneId: 'Pacific/Pago_Pago' });

    test('SH-60 / ADR-0124: NOW() moves on as the minute turns, in the Consumer\'s zone @behaviour', async ({ page }) => {
        // Each reading lies between the zone's minute before the wait for it and the latest minute it
        // can show once it is read: the strings order as the minutes do.
        const zone = 'Pacific/Kiritimati';
        const opened = minuteIn(zone);
        await page.goto(`/sheet?case=today&zone=${zone}`);
        const a4 = cell(sheet(page), 'A4');
        await expect(a4).toHaveText(/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$/);
        const first = (await a4.textContent()).trim();
        within(first, opened, latestMinuteShown(zone));

        // The next minute, once it has turned. The wait is the clock's, at most a minute and the
        // timer's lag.
        await expect(a4).not.toHaveText(first, { timeout: 75_000 });
        const shown = (await a4.textContent()).trim();
        within(shown, first, latestMinuteShown(zone));
        expect(shown > first).toBe(true);
    });
});

test.describe('a browser that does not say its zone', () => {
    test.use({ freshDocument: true });

    test('SH-60 / ADR-0121/0124: with no zone known, TODAY() and NOW() are #GETTING_DATA, and a zone the Consumer gives answers them @behaviour', async ({ page }) => {
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
