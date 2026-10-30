import { startsWithIgnoreCase } from './text-casing';

// Whether a Number Format shows a date (ADR-0330 §4), as ExSheet.Xlsx's DateFormats says it: the cells whose serials a
// workbook on the 1904 date system moves to 1900's. A format that shows only a time of day, or a duration (`[h]:mm`), is
// not one: its number is the same in both systems, and moving it would change an elapsed time.

/** The built-in formats that show a date (ECMA-376 Part 1, §18.8.30), East Asian ones included. */
const builtInDates: ReadonlySet<number> = new Set([14, 15, 16, 17, 22, 27, 28, 29, 30, 31, 34, 35, 36, 50, 51, 52, 53, 54, 55, 56, 57, 58]);

/** Whether the format `id` shows a date; a custom one by its `code`. */
export function showsDate(id: number, code: string | undefined): boolean {
    return code === undefined ? builtInDates.has(id) : codeShowsDate(code);
}

/** `char.ToLowerInvariant` of one ASCII letter of a format code; any other character as it is. */
function lower(c: string): string {
    return c >= 'A' && c <= 'Z' ? c.toLowerCase() : c;
}

/**
 * Whether a format code's first section shows a part of a date: a year (`y`, `e`), a day (`d`), or a month (`m` in a
 * section with no hour or second, where Excel reads it as minutes). An elapsed time in brackets (`[h]`, `[mm]`, `[ss]`)
 * makes the section a duration, so an `m` beside it is minutes. Text in quotes, an escaped character, what else stands in
 * brackets and `General` are not read as parts.
 */
export function codeShowsDate(code: string): boolean {
    let hasMonthOrMinute = false;
    let hasTime = false;
    for (let i = 0; i < code.length; i++) {
        const c = code.charAt(i);
        switch (c) {
            case ';':
                return !hasTime && hasMonthOrMinute;
            case '"': {
                const end = code.indexOf('"', i + 1);
                i = end >= 0 ? end : code.length;
                continue;
            }
            case '\\':
            case '_':
            case '*':
                i++;
                continue;
            case '[': {
                let close = code.indexOf(']', i + 1);
                if (close < 0) close = code.length;
                // [h], [mm], [ss]: an elapsed time, so the section is a duration, and an m beside it is minutes.
                if (isElapsed(code.slice(i + 1, close))) hasTime = true;
                i = close;
                continue;
            }
            default:
                break;
        }
        if (startsWithIgnoreCase(code, i, 'General')) {
            i += 6;
            continue;
        }
        if (startsWithIgnoreCase(code, i, 'AM/PM')) {
            hasTime = true;
            i += 4;
            continue;
        }
        if (startsWithIgnoreCase(code, i, 'A/P')) {
            hasTime = true;
            i += 2;
            continue;
        }
        switch (lower(c)) {
            case 'y':
            case 'd':
                return true;
            case 'e':
                if (i + 1 >= code.length || (code.charAt(i + 1) !== '+' && code.charAt(i + 1) !== '-')) return true;
                break;
            case 'm':
                hasMonthOrMinute = true;
                break;
            case 'h':
            case 's':
                hasTime = true;
                break;
            default:
                break;
        }
    }
    return !hasTime && hasMonthOrMinute;
}

/** Whether a bracket's content is an elapsed time: one letter of `h`, `m` or `s`, repeated. */
function isElapsed(inside: string): boolean {
    if (inside.length === 0) return false;
    const letter = lower(inside.charAt(0));
    if (letter !== 'h' && letter !== 'm' && letter !== 's') return false;
    for (const c of inside) {
        if (lower(c) !== letter) return false;
    }
    return true;
}
