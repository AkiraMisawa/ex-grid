// The calls a sheet case does not make, held here as EngineSurfaceTests.cs holds them on ExSheet.Engine until the
// shared cases have a kind that makes them (ADR-0370 section 2): a name asked about, a Sheet renamed directly, two
// Entries compared, and the package's surface read for a row that can be hidden.
import { describe, expect, it } from 'vitest';
import * as engine from '../src/index';
import { CellRange } from '../src/cell-range';
import { Culture } from '../src/culture';
import { Entry } from '../src/entry';
import { numberFormatColourRgb } from '../src/number-format';
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

    // ADR-0047 (2026-10-10), SH-68: SUBTOTAL's 101 to 111 and AGGREGATE's options 1, 3, 5 and 7 were admitted only while
    // no row can be hidden, and give what 1 to 11 and options 0, 2, 4 and 6 give (tests/cases/sheet/subtotal.json). The
    // day the package's surface can hide a row, this fails: open ADR-0047's note of 2026-10-10 and decide with the user
    // how they pass over a hidden or filtered row. Do not make it pass by changing its expectation.
    it('has no row that can be hidden while SUBTOTAL and AGGREGATE skip nothing', () => {
        const hiding = /hid(e|den|ing)|rowheight|visib|filter(ed|s)?row|autofilter|collapse|outline(level|row)/iu;
        const names = Object.entries(engine).flatMap(([name, exported]) => [
            name,
            ...(typeof exported === 'function'
                ? [
                      ...Object.getOwnPropertyNames(exported),
                      // A function that is no class has no prototype to read.
                      ...(exported.prototype === undefined ? [] : Object.getOwnPropertyNames(exported.prototype)),
                  ].map(member => `${name}.${member}`)
                : []),
        ]);
        expect(names.filter(name => hiding.test(name))).toEqual([]);
    });

    // ADR-0047: whole columns and rows are written as Excel writes them.
    it.each([
        ['B:D', true, false],
        ['3:4', false, true],
        ['A:XFD', true, true],
        ['A1:B2', false, false],
    ] as const)('reads %s as whole columns %s and whole rows %s', (text, columns, rows) => {
        const range = CellRange.parse(text);
        expect(range.isWholeColumns).toBe(columns);
        expect(range.isWholeRows).toBe(rows);
    });

    // ADR-0331: the Sheet shown is one of the Workbook's, and a Sheet of another is refused.
    it('refuses to show a Sheet of another Workbook', () => {
        const workbook = new Workbook(Culture.of('en-US'), ['Sheet1', 'Sheet2']);
        const other = new Workbook(Culture.of('en-US')).shown;
        expect(() => {
            workbook.shown = other;
        }).toThrow(RangeError);
    });

    // ADR-0071, SH-40 (a reading until the eleventh Windows run, case 1): the eight names are Excel's legacy palette.
    it.each([
        ['black', 0x000000],
        ['blue', 0x0000ff],
        ['cyan', 0x00ffff],
        ['green', 0x00ff00],
        ['magenta', 0xff00ff],
        ['red', 0xff0000],
        ['white', 0xffffff],
        ['yellow', 0xffff00],
    ] as const)('paints %s in Excel\'s legacy palette', (colour, rgb) => {
        expect(numberFormatColourRgb(colour)).toBe(rgb);
    });
});
