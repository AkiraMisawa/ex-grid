// A date on a Dates axis is its day number, as C#'s `DateOnly.DayNumber` counts it: days since
// 0001-01-01 in the proleptic Gregorian calendar (ADR-0251). The arithmetic here is the calendar's own,
// so the two cores put a month's first day on the same position.
import { elementAt } from './must';

/** A calendar date: its year, its month from 1 and its day from 1. */
export interface CivilDate {
    readonly year: number;
    readonly month: number;
    readonly day: number;
}

// Day 0 of the count is 1970-01-01's distance from 0001-01-01.
const epoch = 719_162;

/** The day number of a date. */
export function dayNumber(year: number, month: number, day: number): number {
    // Howard Hinnant's days_from_civil, counted from 1970-01-01.
    const y = month <= 2 ? year - 1 : year;
    const era = Math.floor(y / 400);
    const yoe = y - era * 400;
    const mp = (month + 9) % 12;
    const doy = Math.floor((153 * mp + 2) / 5) + day - 1;
    const doe = yoe * 365 + Math.floor(yoe / 4) - Math.floor(yoe / 100) + doy;
    return era * 146_097 + doe - 719_468 + epoch;
}

/** The date of a whole day number. */
export function dateOf(dayNumberValue: number): CivilDate {
    if (!Number.isSafeInteger(dayNumberValue) || dayNumberValue < 0 || dayNumberValue > 3_652_058) {
        throw new RangeError(`${String(dayNumberValue)} is not a day number between 0001-01-01 and 9999-12-31.`);
    }
    // Howard Hinnant's civil_from_days.
    const z = dayNumberValue - epoch + 719_468;
    const era = Math.floor(z / 146_097);
    const doe = z - era * 146_097;
    const yoe = Math.floor((doe - Math.floor(doe / 1460) + Math.floor(doe / 36_524) - Math.floor(doe / 146_096)) / 365);
    const doy = doe - (365 * yoe + Math.floor(yoe / 4) - Math.floor(yoe / 100));
    const mp = Math.floor((5 * doy + 2) / 153);
    const day = doy - Math.floor((153 * mp + 2) / 5) + 1;
    const month = mp < 10 ? mp + 3 : mp - 9;
    return { year: yoe + era * 400 + (month <= 2 ? 1 : 0), month, day };
}

/** A position as C#'s `(int)` cast takes it to a day: its whole part, towards zero. */
export function wholeDay(position: number): number {
    return Math.trunc(position);
}

function daysInMonth(year: number, month: number): number {
    return dayNumber(month === 12 ? year + 1 : year, month === 12 ? 1 : month + 1, 1) - dayNumber(year, month, 1);
}

/** C#'s `DateOnly.AddMonths`: the same day `months` later, or the month's last where it has fewer days. */
export function addMonths(date: CivilDate, months: number): CivilDate {
    const index = date.year * 12 + (date.month - 1) + months;
    const year = Math.floor(index / 12);
    const month = index - year * 12 + 1;
    return { year, month, day: Math.min(date.day, daysInMonth(year, month)) };
}

/** The day of the week as C#'s `DayOfWeek` numbers it: Sunday 0 to Saturday 6. 0001-01-01 was a Monday. */
export function dayOfWeek(dayNumberValue: number): number {
    return (dayNumberValue + 1) % 7;
}

const monthNames = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'] as const;

const two = (n: number): string => String(n).padStart(2, '0');
const four = (n: number): string => String(n).padStart(4, '0');

/** `yyyy-MM-dd`. */
export function isoDate(date: CivilDate): string {
    return `${four(date.year)}-${two(date.month)}-${two(date.day)}`;
}

/** `yyyy-MM`. */
export function yearMonth(date: CivilDate): string {
    return `${four(date.year)}-${two(date.month)}`;
}

/** `yyyy`. */
export function yearOnly(date: CivilDate): string {
    return four(date.year);
}

/** `MMM yyyy`, the month abbreviated as the invariant culture abbreviates it. */
export function monthYear(date: CivilDate): string {
    return `${elementAt(monthNames, date.month - 1)} ${four(date.year)}`;
}
