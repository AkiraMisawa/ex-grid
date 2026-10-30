// Layer 1: an Excel number format code as a grid built from a source's shape reads it (tests/cases/shaped,
// kind "formatCode"; ADR-0342, ADR-0120, ADR-0370 sections 5 and 10), run against the Script Core's FormatCode,
// with values as @ex-grid/data's Snapshot holds them.
import { decimalFromText, ticksFromText } from '@ex-grid/data';
import { describe, expect, it } from 'vitest';
import { FormatCode, type FormatCodeValue } from '../src/shaped/format-code';
import { caseFiles } from './cases';

interface FormatCodeText {
    readonly value: unknown;
    readonly locale: string;
    readonly is: string;
}

interface FormatCodeCase {
    readonly code: string;
    readonly refused?: string;
    readonly isDate?: boolean;
    readonly texts?: readonly FormatCodeText[];
}

/** A value as a case writes it: a JSON number a double, `{"decimal"}` a Decimal, `{"integer"}` a bigint,
 * `{"double"}` a double JSON cannot hold, `{"date"}` a Date's ticks. */
function valueOf(written: unknown): FormatCodeValue {
    if (typeof written === 'number') return written;
    if (typeof written !== 'object' || written === null) throw new Error(`${JSON.stringify(written)} is not a value a format code case writes.`);
    const entries: readonly (readonly [string, unknown])[] = Object.entries(written);
    const [entry] = entries;
    if (entry === undefined || entries.length !== 1 || typeof entry[1] !== 'string') {
        throw new Error(`${JSON.stringify(written)} is not a value a format code case writes.`);
    }
    const [kind, text] = entry;
    switch (kind) {
        case 'decimal': return decimalFromText(text);
        case 'integer': return BigInt(text);
        case 'double': return Number(text);
        case 'date': return ticksFromText(text);
        default: throw new Error(`${JSON.stringify(written)} is not a value a format code case writes.`);
    }
}

function must<T>(value: T | undefined, key: string): T {
    if (value === undefined) throw new Error(`The case needs "${key}".`);
    return value;
}

for (const file of caseFiles<FormatCodeCase>('shaped', 'formatCode')) {
    describe(`shaped/${file.file}`, () => {
        for (const c of file.cases) {
            it(c.name, () => {
                expect.hasAssertions();
                const reading = FormatCode.tryParse(c.code);
                if (c.refused !== undefined) {
                    expect(reading).toEqual({ reason: c.refused });
                    return;
                }
                if (!('format' in reading)) throw new Error(`"${c.code}" was refused: it ${reading.reason}.`);
                const { format } = reading;
                expect(format.code).toBe(c.code);
                expect(format.isDate).toBe(must(c.isDate, 'isDate'));
                const texts = must(c.texts, 'texts');
                expect(texts.length, 'a case that reads a code writes a value with it').toBeGreaterThan(0);
                for (const text of texts) {
                    expect(format.format(valueOf(text.value), text.locale), JSON.stringify(text)).toBe(text.is);
                }
            });
        }
    });
}

describe('FormatCode', () => {
    it('ADR-0342: a date code writes a Date\'s ticks, and refuses a number rather than show it as other text', () => {
        const reading = FormatCode.tryParse('yyyy-mm-dd');
        if (!('format' in reading)) throw new Error('yyyy-mm-dd was refused.');
        expect(() => reading.format.format(1.5, 'invariant')).toThrow(TypeError);
        expect(() => reading.format.format(-1n, 'invariant')).toThrow(RangeError);
    });

    it('ADR-0370 section 10: a locale whose calendar is not the Gregorian is refused for a date, and one with no names for a name', () => {
        const date = FormatCode.tryParse('yyyy-mm-dd');
        const named = FormatCode.tryParse('d mmm yyyy');
        if (!('format' in date) || !('format' in named)) throw new Error('A date code was refused.');
        const ticks = ticksFromText('2026-10-20T09:05:07');
        expect(() => date.format.format(ticks, 'th-TH')).toThrow(RangeError);
        expect(date.format.format(ticks, 'fr-FR')).toBe('2026-10-20');
        expect(() => named.format.format(ticks, 'fr-FR')).toThrow(/no names in the core's table/u);
    });

    it('ADR-0370 section 10: a locale that is no BCP 47 tag is refused', () => {
        const reading = FormatCode.tryParse('0.00');
        if (!('format' in reading)) throw new Error('0.00 was refused.');
        expect(() => reading.format.format(1, 'not a tag!')).toThrow(RangeError);
    });
});
