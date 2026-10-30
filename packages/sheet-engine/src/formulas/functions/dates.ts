// DATE, TODAY, YEAR, MONTH, DAY, EOMONTH, EDATE, WEEKDAY, DAYS, NETWORKDAYS, WORKDAY, TIME, HOUR, MINUTE,
// SECOND and NOW over Excel's 1900 serials (C#'s Functions.Dates.cs); DATEDIF and YEARFRAC
// (Functions.DateDifferences.cs).
import { dateFromSerial, dayNumber, daysInMonth, isLeapYear, maximumSerial, serialDayOfWeek, serialFromDate } from '../../date-serial';
import { roundHalfAwayFromZero, toUpperInvariant } from '../../dotnet';
import { defined } from '../../must';
import { Value } from '../../value';
import type { FunctionCall } from '../function-definition';
import { Operand } from '../operand';
import { numberArgument, scalarArgument, textArgument } from './shared';

interface CalendarDay {
    readonly year: number;
    readonly month: number;
    readonly day: number;
}

function serialResult(serial: number): Operand {
    return Operand.of(Value.fromNumber(serial));
}

/**
 * DATE: each argument truncated to a whole number. A year from 0 to 1899 has 1900 added, and a year below 0 or
 * from 10000 is `#NUM!`. Months and days outside their ranges roll over into the years and months beside them;
 * the days run on Excel's calendar, so DATE(1900,2,29) is serial 60 and DATE(1900,2,30) is 1 March. A result
 * outside serials 0 to 31 December 9999 is `#NUM!`.
 */
export function dateFunction(call: FunctionCall): Operand {
    const yearGiven = numberArgument(call, 0);
    if (yearGiven instanceof Operand) return yearGiven;
    const monthGiven = numberArgument(call, 1);
    if (monthGiven instanceof Operand) return monthGiven;
    const dayGiven = numberArgument(call, 2);
    if (dayGiven instanceof Operand) return dayGiven;
    let year = Math.trunc(yearGiven);
    const month = Math.trunc(monthGiven);
    const day = Math.trunc(dayGiven);
    if (year < 0 || year >= 10000) return Operand.ofError('#NUM!');
    if (year < 1900) year += 1900;

    // The first of the month the year and month name, with the months rolled over.
    const months = year * 12 + (month - 1);
    const firstYear = Math.floor(months / 12);
    if (firstYear < 1 || firstYear > 9999) return Operand.ofError('#NUM!');
    const firstMonth = Math.trunc(months - firstYear * 12) + 1;
    const first = serialOfFirst(firstYear, firstMonth);

    const serial = first + (day - 1);
    if (serial < 0 || serial > maximumSerial) return Operand.ofError('#NUM!');
    return serialResult(serial);
}

/** The serial of the first of a month, counted on Excel's calendar from any year from 1: before 1900 it is negative, and from March 1900 it counts the day that never existed. */
function serialOfFirst(year: number, month: number): number {
    const days = dayNumber(year, month, 1) - dayNumber(1899, 12, 31);
    return days >= 60 ? days + 1 : days;
}

/** TODAY: the Sheet Day's serial (ADR-0121); `#GETTING_DATA` while the day is not known. */
export function today(call: FunctionCall): Operand {
    const day = call.evaluator.cells.today;
    const serial = day === undefined ? undefined : serialFromDate(day.year, day.month, day.day);
    return serial === undefined ? Operand.ofError('#GETTING_DATA') : serialResult(serial);
}

export function yearFunction(call: FunctionCall): Operand {
    return datePart(call, date => date.year);
}

export function monthFunction(call: FunctionCall): Operand {
    return datePart(call, date => date.month);
}

export function dayFunction(call: FunctionCall): Operand {
    return datePart(call, date => date.day);
}

/**
 * YEAR, MONTH and DAY: the serial cut down to its day; serial 0 is 0 January 1900, and serial 60 is 29 February
 * 1900. A serial below 0 or after 31 December 9999 is `#NUM!`, -0.5 included (YEAR-008, MONTH-005, DAY-005).
 */
function datePart(call: FunctionCall, part: (date: CalendarDay) => number): Operand {
    const given = numberArgument(call, 0);
    if (given instanceof Operand) return given;
    const serial = Math.floor(given);
    if (serial < 0 || serial > maximumSerial) return Operand.ofError('#NUM!');
    return Operand.of(Value.fromNumber(part(dateFromSerial(serial))));
}

export function eoMonth(call: FunctionCall): Operand {
    return monthsAway(call, true);
}

export function eDate(call: FunctionCall): Operand {
    return monthsAway(call, false);
}

/**
 * EOMONTH and EDATE: the start date is cut down to its day, and the months are truncated to a whole number.
 * EDATE keeps the start's day, or the month's last day where the month is shorter; EOMONTH gives the month's
 * last day. The months are counted on the real calendar, and a day before 1 March 1900 is its own serial
 * (EOMONTH-008, EDATE-008, EOMONTH-010, EDATE-010). EDATE from serial 0 keeps its day 0, the day before the 1st
 * of the month it reaches (EDATE-016, 018 to 020). A start below 0, or a result before 1900 or after 9999, is
 * `#NUM!`; a boolean typed into either argument is `#VALUE!` (EOMONTH-009, EDATE-009, EOMONTH-018, EDATE-023).
 */
function monthsAway(call: FunctionCall, endOfMonth: boolean): Operand {
    for (let i = 0; i < 2; i++) {
        const value = scalarArgument(call, i);
        if (value instanceof Operand) return value;
        if (value?.kind === 'boolean') return Operand.ofError('#VALUE!');
    }
    const startGiven = numberArgument(call, 0);
    if (startGiven instanceof Operand) return startGiven;
    const monthsGiven = numberArgument(call, 1);
    if (monthsGiven instanceof Operand) return monthsGiven;
    const start = Math.floor(startGiven);
    const months = Math.trunc(monthsGiven);
    if (start < 0 || start > maximumSerial) return Operand.ofError('#NUM!');

    const from = dateFromSerial(start);
    const total = from.year * 12 + (from.month - 1) + months;
    const toYear = Math.floor(total / 12);
    if (toYear < 1900 || toYear > 9999) return Operand.ofError('#NUM!');
    const toMonth = Math.trunc(total - toYear * 12) + 1;
    if (!endOfMonth && from.day === 0) return serialResult(defined(serialFromDate(toYear, toMonth, 1), 'the first of a month from 1900 to 9999') - 1);
    const length = daysInMonth(toYear, toMonth);
    return serialResult(defined(serialFromDate(toYear, toMonth, endOfMonth ? length : Math.min(from.day, length)), 'a day from 1900 to 9999'));
}

/**
 * WEEKDAY: the day of the week of the serial, on Excel's calendar (serial 1 is a Sunday), as `return_type`
 * numbers it — 1 or 17 from Sunday, 2 or 11 from Monday, 3 from Monday at 0, and 12 to 16 from Tuesday to
 * Saturday. The serial is cut down to its day. Another type, or a serial outside 0 to 31 December 9999, is
 * `#NUM!`, -0.5 included (WEEKDAY-008).
 */
export function weekday(call: FunctionCall): Operand {
    const given = numberArgument(call, 0);
    if (given instanceof Operand) return given;
    const serial = Math.floor(given);
    if (serial < 0 || serial > maximumSerial) return Operand.ofError('#NUM!');
    let type = 1;
    if (call.has(1)) {
        const typeGiven = numberArgument(call, 1);
        if (typeGiven instanceof Operand) return typeGiven;
        type = typeGiven;
    }
    const d = serialDayOfWeek(serial);
    let number: number;
    switch (Math.trunc(type)) {
        case 1:
        case 17:
            number = d + 1;
            break;
        case 2:
        case 11:
            number = ((d + 6) % 7) + 1;
            break;
        case 3: number = (d + 6) % 7; break;
        case 12: number = ((d + 5) % 7) + 1; break;
        case 13: number = ((d + 4) % 7) + 1; break;
        case 14: number = ((d + 3) % 7) + 1; break;
        case 15: number = ((d + 2) % 7) + 1; break;
        case 16: number = ((d + 1) % 7) + 1; break;
        default: return Operand.ofError('#NUM!');
    }
    return Operand.of(Value.fromNumber(number));
}

/** DAYS: `end_date - start_date`, each cut down to its day; a serial outside 0 to 31 December 9999 is `#NUM!`, -0.5 included (DAYS-005). */
export function daysFunction(call: FunctionCall): Operand {
    const endGiven = numberArgument(call, 0);
    if (endGiven instanceof Operand) return endGiven;
    const startGiven = numberArgument(call, 1);
    if (startGiven instanceof Operand) return startGiven;
    const end = Math.floor(endGiven);
    const start = Math.floor(startGiven);
    if (end < 0 || start < 0 || end > maximumSerial || start > maximumSerial) return Operand.ofError('#NUM!');
    return Operand.of(Value.fromNumber(end - start));
}

/**
 * A date argument of NETWORKDAYS and WORKDAY, cut down to its day: a boolean typed is `#VALUE!` (NETWORKDAYS-010,
 * WORKDAY-011); below 0 or past 31 December 9999 is `#NUM!`, -0.5 included (NETWORKDAYS-009, WORKDAY-010).
 * Every serial from 0 is a day and takes the day of the week WEEKDAY gives it (NETWORKDAYS-005).
 */
function workdayDate(call: FunctionCall, index: number): number | Operand {
    const value = scalarArgument(call, index);
    if (value instanceof Operand) return value;
    if (value?.kind === 'boolean') return Operand.ofError('#VALUE!');
    const given = numberArgument(call, index);
    if (given instanceof Operand) return given;
    const number = Math.floor(given);
    if (number < 0 || number > maximumSerial) return Operand.ofError('#NUM!');
    return number;
}

/**
 * The holidays argument: a range's numbers and a typed number, each truncated to its day. Text among them is
 * `#VALUE!`, as Excel's is (WORKDAY-005); a boolean among them is refused until Excel is asked; an Error Value is
 * the result.
 */
function holidaysOf(call: FunctionCall, index: number): Set<number> | Operand {
    const holidays = new Set<number>();
    if (!call.has(index)) return holidays;
    const operand = call.operand(index);
    let values: Iterable<Value>;
    if (operand.isRange) values = call.evaluator.rangeValues(operand);
    else if (operand.scalar !== undefined) values = [operand.scalar];
    else return holidays;
    for (const value of values) {
        if (value.isError) return Operand.of(value);
        if (value.kind !== 'number') return value.kind === 'boolean' ? call.evaluator.refuse() : Operand.ofError('#VALUE!');
        holidays.add(Math.floor(value.number));
    }
    return holidays;
}

function isWeekday(serial: number): boolean {
    const d = serialDayOfWeek(serial);
    return d !== 0 && d !== 6;
}

/** NETWORKDAYS: the days from `start_date` to `end_date`, both counted, that fall from Monday to Friday and are not holidays; negative when the start is after the end. */
export function networkDays(call: FunctionCall): Operand {
    const start = workdayDate(call, 0);
    if (start instanceof Operand) return start;
    const end = workdayDate(call, 1);
    if (end instanceof Operand) return end;
    const holidays = holidaysOf(call, 2);
    if (holidays instanceof Operand) return holidays;
    const [from, to, sign] = start <= end ? [start, end, 1] : [end, start, -1];
    const span = to - from + 1;
    const weeks = Math.trunc(span / 7);
    let counted = weeks * 5;
    for (let serial = from + weeks * 7; serial <= to; serial++) {
        if (isWeekday(serial)) counted++;
    }
    for (const h of holidays) {
        if (h >= from && h <= to && isWeekday(h)) counted--;
    }
    return Operand.of(Value.fromNumber(sign * counted));
}

/**
 * WORKDAY: the day `days` working days after `start_date` (before it when negative), passing over weekends and
 * holidays; `days` truncated, and 0 gives the start itself. A result before serial 0 or past 31 December 9999 is
 * `#NUM!`.
 */
export function workday(call: FunctionCall): Operand {
    const start = workdayDate(call, 0);
    if (start instanceof Operand) return start;
    const daysGiven = numberArgument(call, 1);
    if (daysGiven instanceof Operand) return daysGiven;
    const holidays = holidaysOf(call, 2);
    if (holidays instanceof Operand) return holidays;
    const count = Math.trunc(daysGiven);
    const step = count < 0 ? -1 : 1;
    let remaining = Math.abs(count);
    let serial = start;
    while (remaining > 0) {
        serial += step;
        if (serial < 0 || serial > maximumSerial) return Operand.ofError('#NUM!');
        if (isWeekday(serial) && !holidays.has(serial)) remaining--;
    }
    return serialResult(serial);
}

const secondsPerDay = 86_400;

/** TIME: the fraction of a day the hour, minute and second make, each truncated; past 24 hours it wraps. An argument above 32767, or a time before midnight, is `#NUM!`. */
export function time(call: FunctionCall): Operand {
    const hourGiven = numberArgument(call, 0);
    if (hourGiven instanceof Operand) return hourGiven;
    const minuteGiven = numberArgument(call, 1);
    if (minuteGiven instanceof Operand) return minuteGiven;
    const secondGiven = numberArgument(call, 2);
    if (secondGiven instanceof Operand) return secondGiven;
    const hour = Math.trunc(hourGiven);
    const minute = Math.trunc(minuteGiven);
    const second = Math.trunc(secondGiven);
    if (hour > 32767 || minute > 32767 || second > 32767) return Operand.ofError('#NUM!');
    const total = hour * 3600 + minute * 60 + second;
    if (total < 0) return Operand.ofError('#NUM!');
    return Operand.of(Value.fromNumber(total % secondsPerDay / secondsPerDay));
}

export function hourFunction(call: FunctionCall): Operand {
    return timePart(call, seconds => Math.trunc(seconds / 3600));
}

export function minuteFunction(call: FunctionCall): Operand {
    return timePart(call, seconds => Math.trunc(seconds / 60) % 60);
}

export function secondFunction(call: FunctionCall): Operand {
    return timePart(call, seconds => seconds % 60);
}

/**
 * HOUR, MINUTE and SECOND: the serial's time of day, rounded to the nearest second, as Excel shows a time; a
 * serial below 0 or past 31 December 9999 is `#NUM!` (HOUR-005, MINUTE-002, MINUTE-003, SECOND-003).
 */
function timePart(call: FunctionCall, part: (seconds: number) => number): Operand {
    const serial = numberArgument(call, 0);
    if (serial instanceof Operand) return serial;
    if (serial < 0 || serial >= maximumSerial + 1) return Operand.ofError('#NUM!');
    const seconds = roundHalfAwayFromZero((serial - Math.floor(serial)) * secondsPerDay) % secondsPerDay;
    return Operand.of(Value.fromNumber(part(seconds)));
}

/** NOW (ADR-0124): the moment the Sheet was given for this recalculation, as a serial with the time of day; `#GETTING_DATA` while it is not known. */
export function now(call: FunctionCall): Operand {
    const moment = call.evaluator.cells.now;
    if (moment === undefined) return Operand.ofError('#GETTING_DATA');
    const serial = serialFromDate(moment.year, moment.month, moment.day);
    if (serial === undefined) return Operand.ofError('#NUM!');
    const milliseconds = ((moment.hour * 60 + moment.minute) * 60 + moment.second) * 1000 + moment.millisecond;
    return Operand.of(Value.fromNumber(serial + milliseconds / 1000 / secondsPerDay));
}

// ---- DATEDIF and YEARFRAC, pinned by the 2026-10-03 Windows observations (ADR-0047) ------------------------

export function dateDif(call: FunctionCall): Operand {
    const start = differenceDate(call, 0, true);
    if (start instanceof Operand) return start;
    const end = differenceDate(call, 1, true);
    if (end instanceof Operand) return end;
    const unit = textArgument(call, 2);
    if (unit instanceof Operand) return unit;
    if (start > end) return Operand.ofError('#NUM!');
    const first = dateFromSerial(start);
    const last = dateFromSerial(end);
    const beforeAnniversary = last.month < first.month || (last.month === first.month && last.day < first.day);
    const years = last.year - first.year - (beforeAnniversary ? 1 : 0);
    const months = (last.year - first.year) * 12 + last.month - first.month - (last.day < first.day ? 1 : 0);
    switch (toUpperInvariant(unit)) {
        case 'Y': return Operand.of(Value.fromNumber(years));
        case 'M': return Operand.of(Value.fromNumber(months));
        case 'D': return Operand.of(Value.fromNumber(end - start));
        case 'YM': return Operand.of(Value.fromNumber(months % 12));
        // Every observed YD interval precedes its first completed anniversary. Which year's leap day survives
        // removing one or more years is not observed, so those intervals are refused.
        case 'YD': return years === 0 ? Operand.of(Value.fromNumber(end - start)) : call.evaluator.refuse();
        case 'MD': return call.evaluator.refuse();
        default: return Operand.ofError('#NUM!');
    }
}

export function yearFrac(call: FunctionCall): Operand {
    const startGiven = differenceDate(call, 0, false);
    if (startGiven instanceof Operand) return startGiven;
    const endGiven = differenceDate(call, 1, false);
    if (endGiven instanceof Operand) return endGiven;
    let basis = 0;
    if (call.has(2)) {
        const given = numberArgument(call, 2);
        if (given instanceof Operand) return given;
        basis = given;
    }
    basis = Math.trunc(basis);
    if (basis < 0 || basis > 4) return Operand.ofError('#NUM!');
    const [start, end] = startGiven > endGiven ? [endGiven, startGiven] : [startGiven, endGiven];
    const first = dateFromSerial(start);
    const last = dateFromSerial(end);
    let result: number;
    switch (basis) {
        case 0: result = thirtyDayCount(first, last, false) / 360; break;
        case 1: result = (end - start) / actualYearLength(first, last); break;
        case 2: result = (end - start) / 360; break;
        case 3: result = (end - start) / 365; break;
        default: result = thirtyDayCount(first, last, true) / 360; break;
    }
    return Operand.of(Value.fromNumber(result));
}

/**
 * Basis 0's order matters: adjust the 31st before February ends. In the observed February-end to March-31
 * interval, the end stays 31, then the start becomes 30. Basis 4 only clamps the 31st. Both retain January 0
 * and Excel's fictitious February 29.
 */
function thirtyDayCount(first: CalendarDay, last: CalendarDay, european: boolean): number {
    let firstDay = Math.min(first.day, 30);
    let lastDay = last.day;
    if (european || firstDay >= 30) lastDay = Math.min(lastDay, 30);
    if (!european && februaryEnd(first)) {
        firstDay = 30;
        if (februaryEnd(last)) lastDay = 30;
    }
    return (last.year - first.year) * 360 + (last.month - first.month) * 30 + lastDay - firstDay;
}

function februaryEnd(date: CalendarDay): boolean {
    return date.month === 2 && date.day >= daysInMonth(date.year, 2);
}

/**
 * Actual/actual: at most one anniversary uses the containing year's length or a leap day in the interval.
 * Longer intervals use the average Gregorian length of all years touched (MS-OI29500, 2.1.1072).
 */
function actualYearLength(first: CalendarDay, last: CalendarDay): number {
    if (first.year === last.year) return isLeapYear(first.year) ? 366 : 365;
    const withinAnniversary = last.year === first.year + 1
        && (last.month < first.month || (last.month === first.month && last.day <= first.day));
    if (withinAnniversary) {
        const startsBeforeLeapDay = isLeapYear(first.year) && (first.month < 2 || (first.month === 2 && first.day <= 29));
        const endsAfterLeapDay = isLeapYear(last.year) && (last.month > 2 || (last.month === 2 && last.day >= 29));
        return startsBeforeLeapDay || endsAfterLeapDay ? 366 : 365;
    }
    let total = 0;
    for (let y = first.year; y <= last.year; y++) total += isLeapYear(y) ? 366 : 365;
    return total / (last.year - first.year + 1);
}

/**
 * Dates accept numeric text and date text under the Sheet's culture, and discard the time. A serial below 0 is
 * `#NUM!` (DATEDIF-045, YEARFRAC-056, DATEDIF-048, YEARFRAC-058).
 */
function differenceDate(call: FunctionCall, index: number, allowBoolean: boolean): number | Operand {
    const value = scalarArgument(call, index);
    if (value instanceof Operand) return value;
    if (!allowBoolean && value?.kind === 'boolean') return Operand.ofError('#VALUE!');
    const number = numberArgument(call, index);
    if (number instanceof Operand) return number;
    if (number < 0 || Math.trunc(number) > maximumSerial) return Operand.ofError('#NUM!');
    return Math.trunc(number);
}
