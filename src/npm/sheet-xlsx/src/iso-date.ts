import { serialFromDate } from '@ex-grid/sheet-engine';

// A date cell's ISO 8601 text (`t="d"`, ECMA-376 Part 1, §18.18.11) as the 1900 date system's serial (ADR-0330 §4), as
// ExSheet.Xlsx's IsoDate reads it: invariantly, never by the reader's culture, its time zone or today's date. A date with a
// time is its day's serial and the time's fraction of a day; a time alone is its fraction (a serial below 1, as Excel holds
// a time of day). A cell of Excel's has no time zone, so a written offset (`Z`, `+09:00`) is the writer's own, and the clock
// time written beside it is the one its reader saw: that clock time is carried, and the offset converts nothing.

// [0-9], never \d: ISO 8601 writes no other script's digits. A time follows a date after a T, and stands alone without one.
const shape = /^(?:(?<y>[0-9]{4})-(?<mo>[0-9]{2})-(?<d>[0-9]{2}))?(?<t>T)?(?:(?<h>[0-9]{2}):(?<mi>[0-9]{2})(?::(?<s>[0-9]{2})(?<f>\.[0-9]+)?)?)?(?<z>Z|[+-][0-9]{2}(?::?[0-9]{2})?)?$/;

/** The serial of `text`, or `undefined` when it is not an ISO 8601 date or time, or lies outside 1900 to 9999. */
export function isoDateSerial(text: string): number | undefined {
    const groups = shape.exec(text)?.groups;
    if (groups === undefined) return undefined;
    const { y, mo, d, t, h, mi, s, f } = groups;
    const hasDate = y !== undefined;
    const hasTime = h !== undefined;
    if (!hasDate && !hasTime) return undefined;
    // .NET's (?(y)T): a time after a date takes a T, and a time alone takes none.
    if ((t !== undefined) !== (hasDate && hasTime)) return undefined;

    let time = 0;
    if (hasTime) {
        const hours = Number(h);
        const minutes = Number(mi);
        const seconds = s === undefined ? 0 : Number(s);
        const fraction = f === undefined ? 0 : Number(`0${f}`);
        if (hours > 23 || minutes > 59 || seconds > 59) return undefined;
        time = (hours * 3600 + minutes * 60 + seconds + fraction) / 86400;
    }
    if (!hasDate) return time;

    // The engine's own count of the 1900 date system, its 29 February 1900 (60) included.
    const serial = serialFromDate(Number(y), Number(mo), Number(d));
    return serial === undefined ? undefined : serial + time;
}
