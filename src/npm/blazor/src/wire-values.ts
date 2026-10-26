// A cell's value as .NET sends it across the interop boundary, and what the Script Core reads it as
// (ADR-0350 section 16). JSON has no bigint, no date and no non-finite number, so those arrive tagged; every
// other value is the JSON value itself. The C# half writes what it sends in `ScriptValues.cs`, and the two are
// one contract: a tag this side does not know is refused, never read as some other value.

import { instantOf, plainDateOf, plainDateTimeOf, type RowKey } from '@ex-grid/core';

/** A value as .NET writes it: a JSON value, or one of the tagged values JSON cannot carry. */
export type WireValue = null | string | number | boolean | WireTagged;

/**
 * A value JSON cannot carry, tagged by `$`: `i` a whole number past what a double holds exactly, as its decimal
 * text, read as a `bigint` (C#'s long); `f` a double that is not finite (`NaN`, `Infinity`, `-Infinity`); `d` a
 * DateOnly, `t` a DateTime and `o` a DateTimeOffset, each as .NET's ticks of its clock, with `m` the offset in
 * minutes for `o`.
 */
export interface WireTagged {
    readonly $: 'i' | 'f' | 'd' | 't' | 'o';
    readonly v: string;
    readonly m?: number;
}

/** The value a wire value names, as the core reads a cell's value. */
export function valueOf(wire: unknown): unknown {
    if (wire === null || typeof wire === 'string' || typeof wire === 'number' || typeof wire === 'boolean') return wire;
    if (typeof wire !== 'object') throw new TypeError(`.NET sent a cell value of the type ${typeof wire}, which the bridge does not read.`);
    const tag: unknown = Reflect.get(wire, '$');
    const text: unknown = Reflect.get(wire, 'v');
    if (typeof text !== 'string') throw new TypeError('.NET sent a tagged value without its text.');
    switch (tag) {
        case 'i': return BigInt(text);
        case 'f': return nonFinite(text);
        case 'd': return plainDateOf(BigInt(text));
        case 't': return plainDateTimeOf(BigInt(text));
        case 'o': {
            const offset: unknown = Reflect.get(wire, 'm');
            if (typeof offset !== 'number' || !Number.isInteger(offset)) throw new TypeError('.NET sent a DateTimeOffset without its offset.');
            return zonedOf(BigInt(text), offset);
        }
        default: throw new TypeError(`.NET sent a value tagged ${String(tag)}, which the bridge does not read.`);
    }
}

/** A Row Key as .NET sends it: text, a number, or a whole number past a double's as a tagged `i`. */
export function rowKeyOf(wire: unknown): RowKey {
    const value = valueOf(wire);
    if (typeof value === 'string' || typeof value === 'bigint' || (typeof value === 'number' && !Number.isNaN(value))) return value;
    throw new TypeError(`.NET sent a Row Key that is not text or a number (ADR-0140): ${typeof value}.`);
}

function nonFinite(text: string): number {
    switch (text) {
        case 'NaN': return Number.NaN;
        case 'Infinity': return Number.POSITIVE_INFINITY;
        case '-Infinity': return Number.NEGATIVE_INFINITY;
        default: throw new TypeError(`.NET sent '${text}' as a double that is not finite.`);
    }
}

/**
 * A DateTimeOffset as the Temporal `ZonedDateTime` at its own offset, read through its members as the core reads
 * the browser's or a polyfill's, so it shows in the offset it holds (ADR-0350 section 16) and compares as the
 * instant it names, as C#'s DateTimeOffset compares. `ticks` are of its own clock, at `offsetMinutes` from UTC.
 */
export function zonedOf(ticks: bigint, offsetMinutes: number): object {
    const clock = plainDateTimeOf(ticks);
    const { epochNanoseconds } = instantOf(ticks, offsetMinutes) as { readonly epochNanoseconds: bigint }; // oxlint-disable-line typescript/no-unsafe-type-assertion -- instantOf's Instant names its instant by this member
    const sign = offsetMinutes < 0 ? '-' : '+';
    const whole = Math.abs(offsetMinutes);
    const offset = `${sign}${String(Math.floor(whole / 60)).padStart(2, '0')}:${String(whole % 60).padStart(2, '0')}`;
    const members = Object.fromEntries(['calendarId', 'year', 'month', 'day', 'hour', 'minute', 'second', 'millisecond', 'microsecond', 'nanosecond']
        .map(name => [name, Reflect.get(clock, name)]));
    return Object.freeze({
        ...members,
        [Symbol.toStringTag]: 'Temporal.ZonedDateTime',
        offset,
        timeZoneId: offset,
        epochNanoseconds,
        toString: () => `${String(clock)}${offset}[${offset}]`, // oxlint-disable-line typescript/no-base-to-string -- plainDateTimeOf's value writes its ISO text
    });
}

/**
 * A text that is the same for two values exactly when the core reads them as the same value: the key a column's
 * format texts are held by. A value of a kind the bridge never makes is refused.
 */
export function valueKey(value: unknown): string {
    switch (typeof value) {
        case 'string': return `s${value}`;
        case 'number': return `n${String(value)}`;
        case 'bigint': return `b${value.toString()}`;
        case 'boolean': return value ? 'B1' : 'B0';
        case 'object': {
            if (value === null) return 'z';
            const tag = Object.prototype.toString.call(value);
            const instant: unknown = Reflect.get(value, 'epochNanoseconds');
            if (tag === '[object Temporal.ZonedDateTime]' && typeof instant === 'bigint') {
                return `o${instant.toString()}${String(Reflect.get(value, 'offset'))}`;
            }
            if (tag === '[object Temporal.PlainDate]' || tag === '[object Temporal.PlainDateTime]') return `${tag}${String(value)}`; // oxlint-disable-line typescript/no-base-to-string -- the bridge's own dates write their ISO text
            throw new TypeError(`A value tagged ${tag} is not one the bridge makes.`);
        }
        case 'undefined':
        case 'symbol':
        case 'function': throw new TypeError(`A value of the type ${typeof value} is not one the bridge makes.`);
        default: throw new Error(`Internal: a value of the type ${typeof value}, which JavaScript does not have.`);
    }
}
