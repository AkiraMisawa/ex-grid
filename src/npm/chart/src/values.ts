import { roundTrip } from './invariant-text';
import type { ChartAxis, ChartSeries, ChartValue, SeriesKind } from './model';
import { dateOf, isoDate, wholeDay } from './day-numbers';
import { elementAt, unreachable } from './must';

/** A chart that cannot be drawn as it was asked for, said by name (spine rule 1). */
export class ChartRefusedError extends Error {
    /** A refusal saying why. */
    constructor(message: string) {
        super(message);
        this.name = 'ChartRefusedError';
    }
}

/** A value as a number, for geometry only (ADR-0251). */
export function toNumber(value: ChartValue): number {
    return typeof value === 'bigint' ? Number(value) : value;
}

/** A value written invariantly, with every digit it was handed in with. */
export function invariantText(value: ChartValue): string {
    return typeof value === 'bigint' ? value.toString() : roundTrip(value);
}

/** A series' value written through its format, or invariantly. */
export function writeValue(series: ChartSeries, value: ChartValue): string {
    return series.format === undefined ? invariantText(value) : series.format(value);
}

/** A Row Key, a node's name or a surface's value written as C# writes an object into a sentence. */
export function writeObject(value: unknown): string {
    if (typeof value === 'number') {
        return roundTrip(value);
    }
    if (typeof value === 'string') {
        return value;
    }
    // C# writes a bool as `True` or `False`.
    if (typeof value === 'boolean') {
        return value ? 'True' : 'False';
    }
    if (typeof value === 'bigint') {
        return value.toString();
    }
    // C# interpolates a null as nothing.
    if (value === null || value === undefined) {
        return '';
    }
    // Any other key is written as data, which names it where C#'s `ToString()` of an object would name only its type.
    return JSON.stringify(value);
}

/** A position written as the axis writes it (ADR-0250). */
export function writePosition(axis: ChartAxis, position: number): string {
    if (axis.format !== undefined) {
        return axis.format(position);
    }
    switch (axis.values) {
        case 'dates':
            return isoDate(dateOf(wholeDay(position)));
        case 'labels':
            return position >= 0 && position < axis.labels.length && position === Math.floor(position)
                ? elementAt(axis.labels, position)
                : roundTrip(position);
        case 'numbers': {
            const named = axis.named.find(n => n[0] === position);
            return named === undefined ? roundTrip(position) : named[1];
        }
        default:
            return unreachable(axis.values);
    }
}

/** The length in years of the interval between two positions, which an Amount per period is divided by. */
export function yearsBetween(axis: ChartAxis, from: number, to: number): number {
    switch (axis.values) {
        case 'dates':
            return (to - from) / axis.daysPerYear;
        case 'numbers':
            return to - from;
        case 'labels':
            throw new Error('A category axis has no length.');
        default:
            return unreachable(axis.values);
    }
}

/** A Series Kind as the C# engine names it in a sentence. */
export function kindName(kind: SeriesKind): string {
    switch (kind) {
        case 'level':
            return 'Level';
        case 'rateOverInterval':
            return 'RateOverInterval';
        case 'amountPerPeriod':
            return 'AmountPerPeriod';
        case 'observation':
            return 'Observation';
        case 'priceRange':
            return 'PriceRange';
        default:
            return unreachable(kind);
    }
}
