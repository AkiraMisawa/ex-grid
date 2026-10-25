// Layer 1: what a filter condition's typed operand reads as under a culture, and the text an operand reopens
// in (tests/cases/chrome, kind "operand"; ADR-0023, ADR-0370 section 10), run against the Script Core's
// readOperand, operandText and refusalText with a Temporal polyfill as the browser's Temporal.
import { Temporal } from 'temporal-polyfill';
import { describe, expect, it } from 'vitest';
import { gridCulture } from '../src/chrome/grid-culture';
import { operandText, readOperand, refusalText, type ReadDate } from '../src/chrome/operand-reading';
import type { ColumnType, DateType } from '../src/query/columns';
import { caseFiles } from './cases';
import { value } from './query-values';

interface OperandCase {
    readonly culture: string;
    readonly type?: ColumnType;
    readonly dateType?: DateType;
    readonly text?: string;
    readonly reads?: unknown;
    readonly refused?: string;
    readonly readings?: readonly [unknown, unknown];
    readonly read?: unknown;
    readonly says?: string;
    readonly operandText?: unknown;
    readonly is?: string;
    readonly readsBack?: ColumnType;
}

for (const file of caseFiles<OperandCase>('chrome', 'operand')) {
    describe(`chrome/${file.file}`, () => {
        for (const c of file.cases) {
            it(c.name, () => {
                expect.hasAssertions();
                const culture = gridCulture(c.culture);
                if ('operandText' in c) {
                    const operand = operandValue(c.operandText);
                    const text = operandText(operand, culture);
                    expect(text).toBe(c.is);
                    if (c.readsBack !== undefined) {
                        const back = readOperand(c.readsBack, text, culture, c.dateType, Temporal);
                        expect(back.refusal).toBe('none');
                        expectSame(c.operandText, back.value);
                    }
                    return;
                }
                const text = must(c.text, 'text');
                const reading = readOperand(must(c.type, 'type'), text, culture, c.dateType, Temporal);
                if ('reads' in c) {
                    expect(reading.refusal).toBe('none');
                    expectSame(c.reads, reading.value);
                } else {
                    expect(reading.refusal).toBe(c.refused);
                    expect(reading.value).toBeUndefined();
                    if (c.readings !== undefined) {
                        const [first, second] = must(reading.readings, 'the two readings');
                        expectSame(c.readings[0], first);
                        expectSame(c.readings[1], second);
                    }
                    if (c.read !== undefined) expectSame(c.read, reading.read);
                }
                const says = refusalText(reading, text, culture);
                if (c.says !== undefined) expect(says).toBe(c.says);
            });
        }
    });
}

/** An operand as a case writes it: `{"number": text}` the number, a bigint past the safe integers; anything
 * else as a query case writes it. */
function operandValue(written: unknown): unknown {
    const number = numberText(written);
    if (number === undefined) return value(written);
    const asNumber = Number(number);
    return Number.isInteger(asNumber) && !Number.isSafeInteger(asNumber) ? BigInt(number) : asNumber;
}

function numberText(written: unknown): string | undefined {
    if (typeof written !== 'object' || written === null || !('number' in written)) return undefined;
    return String(written.number);
}

/** The value a case writes, and the one read: a number by the number it is, a date by its ISO text (a date
 * read but not yet held to the column's type by its fields), anything else by identity. */
function expectSame(written: unknown, actual: unknown): void {
    const number = numberText(written);
    if (number !== undefined) {
        if (typeof actual === 'bigint') expect(actual).toBe(BigInt(number));
        else expect(actual).toBe(Number(number));
        return;
    }
    if (written === null) {
        expect(actual).toBeUndefined();
        return;
    }
    const expected = value(written);
    if (isReadDate(actual)) {
        expect(readDateText(actual)).toBe(isoOf(expected));
        return;
    }
    if (typeof expected === 'object') {
        expect(isoOf(actual)).toBe(isoOf(expected));
        expect(Object.getPrototypeOf(actual)).toBe(Object.getPrototypeOf(expected));
        return;
    }
    expect(actual).toBe(expected);
}

/** A Temporal value's ISO text, as its own toJSON writes it. */
function isoOf(temporal: unknown): string {
    const text: unknown = JSON.parse(JSON.stringify(temporal));
    if (typeof text !== 'string') throw new TypeError(`${JSON.stringify(temporal)} is not a Temporal value.`);
    return text;
}

function isReadDate(actual: unknown): actual is ReadDate {
    return typeof actual === 'object' && actual !== null && Object.getPrototypeOf(actual) === Object.prototype && 'nanosecond' in actual;
}

/** A date as read, in the text the Temporal value of its fields writes: a ZonedDateTime in its offset's zone
 * where an offset was written. */
function readDateText({ year, month, day, hour, minute, second, nanosecond, offset }: ReadDate): string {
    const local = Temporal.PlainDateTime.from({ year, month, day, hour, minute, second, nanosecond }).toString();
    if (offset === undefined) return local;
    const zone = offset === 'Z' ? '+00:00' : offset;
    return Temporal.ZonedDateTime.from(`${local}${zone}[${zone}]`).toString();
}

function must<T>(present: T | undefined, what: string): T {
    if (present === undefined) throw new Error(`The case gives no ${what}.`);
    return present;
}
