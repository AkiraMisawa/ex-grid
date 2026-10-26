// The Showcases' number formats, as .NET writes them under the invariant culture: `ToString("0.00")` is
// `fixed(v, 2)` and `ToString("#,##0.00")` is `grouped(v, 2)`.
import { Temporal } from 'temporal-polyfill';

/** `ToString("0.…0")`, `places` decimals. */
export function fixed(value: number, places: number): string {
    if (!Number.isFinite(value)) throw new RangeError(`${String(value)} is not a finite number, so it has no fixed form.`);
    return value.toFixed(places);
}

/** `ToString("#,##0.…0")`: thousands separated by commas, `places` decimals. */
export function grouped(value: number, places: number): string {
    const text = fixed(value, places);
    const [whole, fraction] = text.split('.');
    if (whole === undefined) throw new Error(`Internal: '${text}' has no whole part.`);
    const digits = whole.replace(/^-/, '').replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    return `${whole.startsWith('-') ? '-' : ''}${digits}${fraction === undefined ? '' : `.${fraction}`}`;
}

/** A number cell's value, which the grid hands its column's format as `unknown`. */
export function numberOf(value: unknown): number {
    if (typeof value !== 'number') throw new TypeError(`A figure is a number, not ${typeof value}.`);
    return value;
}

const firstDay = Temporal.PlainDate.from('0001-01-01');

/** The date's `DayNumber`, days since 0001-01-01, as a chart's date axis places it. */
export const dayNumberOf = (date: Temporal.PlainDate): number => firstDay.until(date).days;
