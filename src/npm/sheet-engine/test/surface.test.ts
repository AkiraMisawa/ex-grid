// The calls a sheet case does not make, held here as EngineSurfaceTests.cs holds them on ExSheet.Engine until the
// shared cases have a kind that makes them (ADR-0370 section 2): a name asked about, a Sheet renamed directly, two
// Entries compared.
import { describe, expect, it } from 'vitest';
import { Culture } from '../src/culture';
import { Entry } from '../src/entry';
import { Sheet } from '../src/sheet';
import { Workbook } from '../src/workbook';

describe('the engine surface', () => {
    // ADR-0046: Excel's rules for a Sheet's name, asked about and refused by a direct rename.
    it.each(['', 'a:b', "'quoted", 'History', 'history', '12345678901234567890123456789012'])('refuses the name %j', name => {
        expect(Sheet.isValidName(name)).toBe(false);
        expect(Sheet.whyNotAName(name)).toBeTypeOf('string');
        const sheet = new Workbook(Culture.of('en-US')).shown;
        expect(() => sheet.rename(name)).toThrow(RangeError);
    });

    // ADR-0046: names Excel accepts.
    it.each(['My Sheet', "It's", '1234567890123456789012345678901', 'History2'])('accepts the name %j', name => {
        expect(Sheet.isValidName(name)).toBe(true);
        expect(Sheet.whyNotAName(name)).toBeUndefined();
        expect(new Workbook(Culture.of('en-US'), [name]).shown.name).toBe(name);
    });

    // ADR-0047: two Formulas that differ only in whitespace are different Entries.
    it('keeps whitespace as part of the Entry', () => {
        expect(Entry.fromFormula('=A1+1').equals(Entry.fromFormula('= A1+1'))).toBe(false);
    });
});
