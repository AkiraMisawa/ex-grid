// Layer 1: the text a value cell displays (tests/cases/cells, kind "displayText"), run against the Script
// Core's textFor, which reads displayText where the column has no format.
import { describe, expect, it } from 'vitest';
import { displayText, rawText, textFor } from '../src/cells/display-text';
import { caseFiles } from './cases';
import { formatOf, value } from './query-values';

interface DisplayTextCase {
    readonly texts: readonly { readonly value: unknown; readonly format?: string; readonly is: string }[];
}

for (const file of caseFiles<DisplayTextCase>('cells', 'displayText')) {
    describe(`cells/${file.file}`, () => {
        for (const c of file.cases) {
            it(c.name, () => {
                expect.hasAssertions();
                for (const text of c.texts) {
                    const column = text.format === undefined ? { name: 'Value' } : { name: 'Value', format: formatOf(text.format) };
                    expect(textFor(column, value(text.value)), JSON.stringify(text)).toBe(text.is);
                }
            });
        }
    });
}

describe('displayText', () => {
    it('ADR-0006: a number is JavaScript\'s own shortest text for it, which keeps no trailing zeros', () => {
        expect(displayText(1.5)).toBe('1.5');
        expect(displayText(1.0)).toBe('1');
        expect(displayText(10n ** 20n)).toBe('100000000000000000000');
    });

    it('ADR-0350: a number reads as Excel\'s General, 15 significant digits; a bigint every digit, and the raw form the whole number', () => {
        expect(displayText(0.1 + 0.2)).toBe('0.3');
        expect(displayText(-0)).toBe('0');
        // Past 1e21 and below 1e-6 JavaScript writes an exponent, as before, now of the rounded number.
        expect(displayText(1 / 3 * 1e22)).toBe('3.33333333333333e+21');
        expect(displayText(2 ** 53 + 2)).toBe('9007199254740990');
        expect(displayText(9007199254740993n)).toBe('9007199254740993');
        expect([Number.NaN, Number.POSITIVE_INFINITY, Number.NEGATIVE_INFINITY].map(displayText)).toEqual(['NaN', 'Infinity', '-Infinity']);
        expect(rawText(0.1 + 0.2)).toBe('0.30000000000000004');
    });

    it('ADR-0350: a year outside 0 to 9999 is written as ISO\'s extended year, rather than four digits that drop one', () => {
        expect(displayText(value({ dateOnly: '+012345-01-02' }))).toBe('+012345-01-02');
        expect(displayText(value({ dateOnly: '-000001-01-02' }))).toBe('-000001-01-02');
    });

    it('ADR-0350: an Instant reads in UTC, and a PlainTime as HH:mm:ss', async () => {
        const { Temporal } = await import('temporal-polyfill');
        expect(displayText(Temporal.Instant.from('2026-01-02T03:04:05.678+09:00'))).toBe('2026-01-01 18:04:05 +00:00');
        expect(displayText(Temporal.PlainTime.from('07:08:09.5'))).toBe('07:08:09');
    });

    it('ADR-0350: a date held in another calendar shows the ISO day it names, the day its filter and sort read', async () => {
        // The full polyfill, for its calendars other than ISO's.
        const { Temporal } = await import('temporal-polyfill/full');
        expect(displayText(Temporal.PlainDate.from('2026-01-02').withCalendar('hebrew'))).toBe('2026-01-02');
        expect(displayText(Temporal.PlainDateTime.from('2026-01-02T03:04:05').withCalendar('japanese'))).toBe('2026-01-02 03:04:05');
    });

    it('ADR-0006: an invalid Date names no instant, and is refused rather than shown as a text every other one shares', () => {
        expect(() => displayText(new Date(Number.NaN))).toThrow(TypeError);
    });

    it('ADR-0006: a value whose tag names a date it does not hold is refused, never written as some other day', () => {
        const forged = { [Symbol.toStringTag]: 'Temporal.PlainDate', year: 2026, month: 'Jan', day: 1 };
        expect(() => displayText(forged)).toThrow(TypeError);
    });

    it('ADR-0006: a format that answers anything but text is refused, naming the column', () => {
        expect(() => textFor({ name: 'Amount', format: () => 3 as unknown as string }, 3)).toThrow("Column 'Amount''s format answered number");
    });
});

describe('rawText', () => {
    it('ADR-0005: the clipboard\'s raw form is locale-free and whole, in the forms the C# core writes', async () => {
        const { Temporal } = await import('temporal-polyfill');
        expect([rawText(null), rawText(undefined), rawText(true), rawText(false)]).toEqual(['', '', 'TRUE', 'FALSE']);
        expect([rawText(1234567.891), rawText(-0.5), rawText(10n ** 20n), rawText('1,234')]).toEqual(['1234567.891', '-0.5', '100000000000000000000', '1,234']);
        expect(rawText(Temporal.PlainDate.from('2026-03-04'))).toBe('2026-03-04');
        expect(rawText(Temporal.PlainDateTime.from('2026-03-04T05:06:07.5'))).toBe('2026-03-04T05:06:07');
        expect(rawText(Temporal.PlainTime.from('05:06:07'))).toBe('05:06:07');
        expect(rawText(Temporal.ZonedDateTime.from('2026-03-04T05:06:07+09:00[Asia/Tokyo]'))).toBe('2026-03-04T05:06:07+09:00');
        // An instant holds no offset of its own, and is written in UTC's.
        expect(rawText(Temporal.Instant.from('2026-03-04T05:06:07Z'))).toBe('2026-03-04T05:06:07+00:00');
        expect(rawText(new Date(Date.UTC(2026, 2, 4, 5, 6, 7)))).toBe('2026-03-04T05:06:07+00:00');
    });
});
