// Excel's 1900 date system (ADR-0047): serial 1 is 1 January 1900, and serial 60 is 29 February 1900, a day
// that never existed. Excel keeps it for compatibility with Lotus 1-2-3; matching Excel's serials is the
// promise, and every serial from 1 March 1900 onward disagrees with Excel if that day is left out. Serial 0
// is written as 0 January 1900, as Excel writes it.

/** The last day Excel shows: 31 December 9999. */
export const maximumSerial = 2_958_465;

/** Days from 0001-01-01 to the date, in the proleptic Gregorian calendar, as `DateOnly.DayNumber` counts them. */
export function dayNumber(year: number, month: number, day: number): number {
    const y = month <= 2 ? year - 1 : year;
    const era = Math.floor(y / 400);
    const yearOfEra = y - era * 400;
    const dayOfYear = Math.floor((153 * (month + (month > 2 ? -3 : 9)) + 2) / 5) + day - 1;
    const dayOfEra = yearOfEra * 365 + Math.floor(yearOfEra / 4) - Math.floor(yearOfEra / 100) + dayOfYear;
    // Days from 0000-03-01, less those to 0001-01-01.
    return era * 146097 + dayOfEra - 306;
}

/** The date of a day number (`DateOnly.FromDayNumber`). */
export function fromDayNumber(number: number): { year: number; month: number; day: number } {
    const z = number + 306;
    const era = Math.floor(z / 146097);
    const dayOfEra = z - era * 146097;
    const yearOfEra = Math.floor((dayOfEra - Math.floor(dayOfEra / 1460) + Math.floor(dayOfEra / 36524) - Math.floor(dayOfEra / 146096)) / 365);
    const dayOfYear = dayOfEra - (365 * yearOfEra + Math.floor(yearOfEra / 4) - Math.floor(yearOfEra / 100));
    const mp = Math.floor((5 * dayOfYear + 2) / 153);
    const day = dayOfYear - Math.floor((153 * mp + 2) / 5) + 1;
    const month = mp < 10 ? mp + 3 : mp - 9;
    return { year: yearOfEra + era * 400 + (month <= 2 ? 1 : 0), month, day };
}

/** Whether a year of the Gregorian calendar is a leap year. */
export function isLeapYear(year: number): boolean {
    return (year % 4 === 0 && year % 100 !== 0) || year % 400 === 0;
}

/** How many days a month of a year has (`DateTime.DaysInMonth`). */
export function daysInMonth(year: number, month: number): number {
    if (month === 2) return isLeapYear(year) ? 29 : 28;
    return month === 4 || month === 6 || month === 9 || month === 11 ? 30 : 31;
}

const base = dayNumber(1899, 12, 31);

/** The serial of a calendar day, or `undefined` outside 1900-01-01 … 9999-12-31. 1900-02-29 is 60. */
export function serialFromDate(year: number, month: number, day: number): number | undefined {
    if (year === 1900 && month === 2 && day === 29) return 60;
    if (year < 1900 || year > 9999 || month < 1 || month > 12 || day < 1 || day > daysInMonth(year, month)) return undefined;
    const serial = dayNumber(year, month, day) - base;
    return serial >= 60 ? serial + 1 : serial;
}

/** The calendar day of a whole serial, 0 to `maximumSerial`. */
export function dateFromSerial(serial: number): { year: number; month: number; day: number } {
    if (serial === 0) return { year: 1900, month: 1, day: 0 };
    if (serial === 60) return { year: 1900, month: 2, day: 29 };
    return fromDayNumber(base + (serial > 60 ? serial - 1 : serial));
}

/** The weekday Excel gives a serial, 0 for Sunday: Excel counts 1 January 1900 as a Sunday. */
export function serialDayOfWeek(serial: number): number {
    return (serial + 6) % 7;
}
