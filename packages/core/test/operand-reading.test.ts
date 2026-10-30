// Layer 1: where the Script Core's reading of a typed operand differs from the C# core's, and the culture it
// reads in (ADR-0370 section 10). The answers both cores give are the shared cases of
// tests/cases/chrome/operand-reading.json.
import { Temporal } from 'temporal-polyfill';
import { describe, expect, it } from 'vitest';
import { gridCulture } from '../src/chrome/grid-culture';
import { operandText, readOperand } from '../src/chrome/operand-reading';

describe('gridCulture', () => {
    it('ADR-0370 section 10: a locale that is no BCP 47 tag is refused by name, never read as some other culture', () => {
        expect(() => gridCulture('not a tag!')).toThrow(/'not a tag!'/u);
        expect(() => gridCulture('')).toThrow(RangeError);
    });

    it('ADR-0370 section 10: "invariant" reads as .NET\'s InvariantCulture', () => {
        const invariant = gridCulture('invariant');
        expect([invariant.decimal, invariant.group, invariant.negativeSign]).toEqual(['.', ',', '-']);
        expect(invariant.grouped(1234567n)).toBe('1,234,567');
    });

    it('ADR-0370 section 10: a culture\'s separators and date order come from Intl', () => {
        const de = gridCulture('de-DE');
        expect([de.decimal, de.group]).toEqual([',', '.']);
        expect(de.dayFirst).toBe(true);
        expect(gridCulture('ja-JP').yearFirst).toBe(true);
        expect(gridCulture('en-US').dayFirst).toBe(false);
        // Grouped even where ICU leaves four digits alone, as C#'s N0 groups them.
        expect(gridCulture('es-ES').grouped(1234n)).toBe('1.234');
    });
});

const date = (text: string, locale: string): ReturnType<typeof readOperand> =>
    readOperand('date', text, gridCulture(locale), 'dateTime', Temporal);

describe('a date the Script Core reads otherwise than .NET', () => {
    it.each([
        ['5 January 2026', 'en-GB'],
        ['Jan 5, 2026', 'en-US'],
        ['1/5/2026 1:00 PM', 'en-US'],
        ['05/01/26', 'en-GB'],
    ])('ADR-0350 section 16: %j under %s is refused, where C#\'s parser reads it', (text, locale) => {
        // A month's name, a 12-hour clock and a two-digit year are read by no rule the cores share, so the
        // Script Core refuses them rather than read them its own way (principle 1).
        expect(date(text, locale).refusal).toBe('notReadable');
    });

    it('ticket 97: under ja-JP, a year-last date with one order that is a date reads as that one, day first as well as month first', () => {
        expect(String(date('13/01/2026', 'ja-JP').value)).toBe('2026-01-13T00:00:00');
        expect(String(date('01/13/2026', 'ja-JP').value)).toBe('2026-01-13T00:00:00');
    });

    it('ADR-0350 section 16: a date operand needs Temporal, and its absence is said, not worked around', () => {
        expect(() => readOperand('date', '2026-01-05', gridCulture('en-US'))).toThrow(/Temporal/u);
    });
});

describe('a number the Script Core holds as a double', () => {
    it('ADR-0350 section 16: a fraction a double cannot hold exactly is refused, never filtered on as the nearest double', () => {
        expect(readOperand('number', '0.1', gridCulture('invariant')).value).toBe(0.1);
        expect(readOperand('number', '0.12345678901234567890', gridCulture('invariant')).refusal).toBe('notReadable');
    });

    it('ADR-0370 section 10: a culture whose minus is U+2212 also reads the hyphen typed for it', () => {
        const sv = gridCulture('sv-SE');
        expect(sv.negativeSign).toBe('\u2212');
        expect(readOperand('number', '-1,5', sv).value).toBe(-1.5);
        expect(readOperand('number', '\u22121,5', sv).value).toBe(-1.5);
    });
});

describe('the text a condition reopens in', () => {
    it('ticket 96: a moment with a fraction of a second reopens in a text that reads back as the same moment', () => {
        const moment = Temporal.ZonedDateTime.from('2026-01-05T10:00:00.25+09:00[+09:00]');
        const text = operandText(moment, gridCulture('invariant'));
        expect(text).toBe('2026-01-05T10:00:00.25+09:00');
        const read = readOperand('date', text, gridCulture('invariant'), 'dateTimeOffset', Temporal).value;
        expect(Temporal.ZonedDateTime.compare(read as Temporal.ZonedDateTime, moment)).toBe(0);
    });

    it('ticket 96: a date and time with a fraction of a second reopens in ISO\'s T form, which reads back exactly', () => {
        const typed = Temporal.PlainDateTime.from('2026-01-05T10:00:00.25');
        const text = operandText(typed, gridCulture('de-DE'));
        expect(text).toBe('2026-01-05T10:00:00.25');
        const read = readOperand('date', text, gridCulture('de-DE'), 'dateTime', Temporal).value;
        expect(Temporal.PlainDateTime.compare(read as Temporal.PlainDateTime, typed)).toBe(0);
    });

    it('ticket 96: a moment on the second reopens in the spaced form a cell shows', () => {
        const moment = Temporal.ZonedDateTime.from('2026-01-05T10:00:00+09:00[+09:00]');
        expect(operandText(moment, gridCulture('invariant'))).toBe('2026-01-05 10:00:00 +09:00');
    });
});
