// Chart Filters' range as text (ADR-0273): typed in the axis' own values — a date as yyyy-MM-dd, a
// category by its label, a number invariantly or by a named position — and written back the same way.
// A value that does not read as one is said, never guessed (spine rule 1).
import type { ChartAxis } from '../model';
import { dateOf, dayNumber, isoDate, wholeDay } from '../day-numbers';
import { roundTrip } from '../invariant-text';
import { elementAt, unreachable } from '../must';

/** What a typed end reads as: the axis' own end where nothing was typed, a position, or nothing it holds. */
export type TypedEnd = { readonly read: true; readonly position: number | null } | { readonly read: false };

const unread: TypedEnd = Object.freeze({ read: false });

// .NET's OrdinalIgnoreCase: each UTF-16 unit by its simple upper case, so no unit becomes two.
function upper(text: string): string {
    return text.split('').map(c => {
        const u = c.toUpperCase();
        return u.length === c.length ? u : c;
    }).join('');
}

// .NET's NumberStyles.Float under the invariant culture, past the trimming already done: a sign, digits
// with one point, and an exponent; no group separator.
const floatText = /^[+-]?(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?$/;

const dateText = /^(\d{4})-(\d{2})-(\d{2})$/;

/** Reads `text` as a position of `axis`; empty or blank text is the axis' own end. */
export function readEnd(axis: ChartAxis, text: string | null): TypedEnd {
    if (text === null || text.trim() === '') {
        return { read: true, position: null };
    }
    const t = text.trim();
    switch (axis.values) {
        case 'dates': {
            const m = dateText.exec(t);
            if (m === null) {
                return unread;
            }
            const [year, month, day] = [Number(m[1]), Number(m[2]), Number(m[3])];
            if (year < 1 || month < 1 || month > 12 || day < 1) {
                return unread;
            }
            const position = dayNumber(year, month, day);
            // A day past its month's end is no date, as DateOnly.TryParseExact refuses 2026-02-30.
            return dateOf(position).day === day ? { read: true, position } : unread;
        }
        case 'labels': {
            let i = axis.labels.indexOf(t);
            if (i < 0) {
                i = axis.labels.findIndex(l => upper(l) === upper(t));
            }
            return i >= 0 ? { read: true, position: i } : unread;
        }
        case 'numbers': {
            const named = axis.named.find(([, label]) => upper(label) === upper(t));
            if (named !== undefined) {
                return { read: true, position: named[0] };
            }
            if (!floatText.test(t)) {
                return unread;
            }
            const value = Number(t);
            return Number.isFinite(value) ? { read: true, position: value } : unread;
        }
        default:
            return unreachable(axis.values);
    }
}

/** A position written as Chart Filters shows it: a date as yyyy-MM-dd, a category's label, a number round-trip. */
export function writeEnd(axis: ChartAxis, position: number): string {
    if (axis.values === 'dates') {
        return isoDate(dateOf(wholeDay(position)));
    }
    if (axis.values === 'labels' && position >= 0 && position < axis.labels.length) {
        return elementAt(axis.labels, Math.trunc(position));
    }
    return roundTrip(position);
}
