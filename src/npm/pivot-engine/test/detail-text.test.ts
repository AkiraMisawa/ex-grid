// A record's value as Show Details writes it (ADR-0059), as the Razor component's PivotDetailsSheet.TextOf writes
// it, from the engine's own culture table, never the browser's Intl.
import { describe, expect, it } from 'vitest';
import { formatPivotDetailValue } from '../src/index';

const ticksPerDay = 864_000_000_000n;
// .NET's ticks of a day, counted from 0001-01-01: 719,162 days lie before 1970-01-01.
const ticksOf = (year: number, month: number, day: number): bigint => (719_162n + BigInt(Date.UTC(year, month - 1, day) / 86_400_000)) * ticksPerDay;

describe('formatPivotDetailValue', () => {
    it('ADR-0059: text as it is, a Boolean as TRUE or FALSE, a Blank as nothing, and a double that is not finite as #NUM!', () => {
        expect.hasAssertions();
        expect(formatPivotDetailValue('EU', undefined, 'en-US')).toBe('EU');
        expect(formatPivotDetailValue(true, undefined, 'en-US')).toBe('TRUE');
        expect(formatPivotDetailValue(false, '0.00', 'en-US')).toBe('FALSE');
        expect(formatPivotDetailValue(null, '0.00', 'en-US')).toBe('');
        expect(formatPivotDetailValue(Number.NaN, undefined, 'en-US')).toBe('#NUM!');
    });

    it('ADR-0060: a number in its field\'s format under the locale, or to 15 significant digits', () => {
        expect.hasAssertions();
        expect(formatPivotDetailValue(1234.5, '#,##0.00', 'en-US')).toBe('1,234.50');
        expect(formatPivotDetailValue(1234.5, '#,##0.00', 'de-DE')).toBe('1.234,50');
        expect(formatPivotDetailValue(0.1 + 0.2, undefined, 'en-US')).toBe('0.3');
    });

    it('ADR-0060: a date in its field\'s format, or the short date, with the long time where it has a time of day', () => {
        expect.hasAssertions();
        const day = ticksOf(2026, 7, 2);
        expect(formatPivotDetailValue(day, 'yyyy-MM-dd', 'en-US')).toBe('2026-07-02');
        expect(formatPivotDetailValue(day, undefined, 'en-US')).toBe('7/2/2026');
        expect(formatPivotDetailValue(day + (ticksPerDay / 2n), undefined, 'en-US')).toBe('7/2/2026 12:00:00\u202fPM'); // ICU's narrow space, as .NET writes it
    });
});
