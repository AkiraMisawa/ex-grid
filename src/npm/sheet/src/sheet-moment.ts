// The moment a Sheet reads in a time zone (ADR-0121, ADR-0124): the date TODAY answers and the date and time of day
// NOW answers, as the browser's Intl reads them in the zone named. The engine is given both as data and reads no clock
// itself; the Sheet reads them here.

import type { SheetDay, SheetMoment } from '@ex-grid/sheet-engine';

/** The longest a reading of the day stands: a clock change, a daylight-saving shift or a sleeping device makes a day
 * late by at most this (ADR-0121). */
export const longestDayReadingMs = 60 * 60 * 1000;

const readers = new Map<string, Intl.DateTimeFormat>();

/** The zone named, as Intl knows it; a name Intl does not know is refused by name, never read as another zone. */
export function checkTimeZone(zone: string): void {
    readerOf(zone);
}

/** The time zone the browser runs in. */
export function browserTimeZone(): string {
    return new Intl.DateTimeFormat().resolvedOptions().timeZone;
}

/** The date and time of day at `at` in `zone`. */
export function momentIn(zone: string, at: Date): SheetMoment {
    const parts = new Map(readerOf(zone).formatToParts(at).map(part => [part.type, part.value]));
    const part = (type: Intl.DateTimeFormatPartTypes): number => {
        const value = parts.get(type);
        if (value === undefined) throw new Error(`Internal: Intl gave no ${type} for ${at.toISOString()} in ${zone}.`);
        return Number(value);
    };
    return {
        year: part('year'), month: part('month'), day: part('day'),
        hour: part('hour'), minute: part('minute'), second: part('second'), millisecond: at.getUTCMilliseconds(),
    };
}

/** The day of `moment`. */
export function dayOf(moment: SheetMoment): SheetDay {
    return { year: moment.year, month: moment.month, day: moment.day };
}

/** Whether two days, or two absences of one, are the same. */
export function sameDay(a: SheetDay | undefined, b: SheetDay | undefined): boolean {
    return a === b || (a !== undefined && b !== undefined && a.year === b.year && a.month === b.month && a.day === b.day);
}

/** How long until the day is read again: the next midnight in the zone, at most {@link longestDayReadingMs} away, and
 * at least a second. */
export function untilTheDayIsReadAgainMs(moment: SheetMoment): number {
    const pastMidnight = ((moment.hour * 60 + moment.minute) * 60 + moment.second) * 1000 + moment.millisecond;
    return Math.max(1000, Math.min(longestDayReadingMs, 24 * 60 * 60 * 1000 - pastMidnight));
}

/** How long until the minute turns. */
export function untilTheMinuteTurnsMs(at: number): number {
    return 60_000 - (at % 60_000);
}

function readerOf(zone: string): Intl.DateTimeFormat {
    let reader = readers.get(zone);
    if (reader === undefined) {
        // Throws a RangeError for a zone Intl does not know.
        reader = new Intl.DateTimeFormat('en-US', {
            timeZone: zone, year: 'numeric', month: 'numeric', day: 'numeric', hour: 'numeric', minute: 'numeric', second: 'numeric', hourCycle: 'h23',
        });
        readers.set(zone, reader);
    }
    return reader;
}
