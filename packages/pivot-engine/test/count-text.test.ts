// A count as the Field List and the leaf cap's refusal write it (ADR-0370 section 3): C#'s "N0" in the
// pivot's locale, from the engine's own culture table, never the browser's Intl.
import { describe, expect, it } from 'vitest';
import { formatPivotCount } from '../src/index';

describe('formatPivotCount', () => {
    it('ADR-0370 section 3: a count is written with the locale\'s group separator, as C#\'s N0 writes it', () => {
        expect.hasAssertions();
        expect(formatPivotCount(1_020_000, 'en-US')).toBe('1,020,000');
        expect(formatPivotCount(1_020_000, 'de-DE')).toBe('1.020.000');
        expect(formatPivotCount(1_020_000, 'invariant')).toBe('1,020,000');
        expect(formatPivotCount(0, 'ja-JP')).toBe('0');
    });

    it('ADR-0370 section 3: a count that is not a whole number of at least 0 is refused, and so is a locale the table does not hold', () => {
        expect.hasAssertions();
        expect(() => formatPivotCount(-1, 'en-US')).toThrow(RangeError);
        expect(() => formatPivotCount(1.5, 'en-US')).toThrow(RangeError);
        expect(() => formatPivotCount(1, 'xx-XX')).toThrow(RangeError);
    });
});
