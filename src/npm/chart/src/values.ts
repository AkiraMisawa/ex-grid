import { FormatCode } from '@ex-grid/core';
import { roundTrip } from './invariant-text';
import type { AxisValues, ChartAxis, ChartSeries, ChartValue, SeriesKind } from './model';
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

/** A series' value written through its format or its format code (ADR-0420), or invariantly. */
export function writeValue(series: ChartSeries, value: ChartValue): string {
    if (series.format !== undefined) {
        return series.format(value);
    }
    return series.formatCode === undefined ? invariantText(value) : codeOf(series.formatCode).format(value, 'invariant');
}

/**
 * An axis' or a series' Excel format code, read by the family's reader both cores write a code with (ADR-0420,
 * ADR-0342): the reader, or why it cannot be read, a phrase completing "the format code …".
 */
export function readCode(code: string): FormatCode | string {
    const reading = FormatCode.tryParse(code);
    return 'format' in reading ? reading.format : reading.reason;
}

// Each code read once, as C# reads it once as the definition is made: a writer is asked at every tick and pointer move.
const readings = new Map<string, FormatCode | string>();

// A code the definition checked: one it could not read never reaches a writer.
function codeOf(code: string): FormatCode {
    let reading = readings.get(code);
    if (reading === undefined) {
        reading = readCode(code);
        readings.set(code, reading);
    }
    if (typeof reading === 'string') {
        throw new Error(`Internal: the format code "${code}" was written with, which the definition refuses: it ${reading}.`);
    }
    return reading;
}

/** Whether the axis' format or format code writes its positions, in place of its own writing. */
export function axisWrites(axis: ChartAxis): boolean {
    return axis.format !== undefined || axis.formatCode !== undefined;
}

const ticksPerDay = 864_000_000_000;

// A position through a code: a Dates axis' day number as the date and time it is, as .NET ticks; a Numbers axis'
// as the number. A fraction of a day's ticks is rounded half away from zero, as C# rounds them.
function writeCodePosition(code: FormatCode, values: AxisValues, position: number): string {
    if (values !== 'dates') {
        return code.format(position, 'invariant');
    }
    const day = Math.floor(position);
    const ticks = BigInt(day) * BigInt(ticksPerDay) + BigInt(Math.round((position - day) * ticksPerDay));
    return code.format(ticks, 'invariant');
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
    if (axis.formatCode !== undefined) {
        return writeCodePosition(codeOf(axis.formatCode), axis.values, position);
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
