// The calls a sheet case does not make, held here as EngineSurfaceTests.cs holds them on ExSheet.Engine until the
// shared cases have a kind that makes them (ADR-0370 section 2): a name asked about, a Sheet renamed directly, two
// Entries compared, and the package's surface read for a row that can be hidden.
import { describe, expect, it } from 'vitest';
import * as engine from '../src/index';
import { CellRange } from '../src/cell-range';
import { Culture } from '../src/culture';
import { Entry } from '../src/entry';
import { NumberFormat, numberFormatColourRgb } from '../src/number-format';
import { CellAddress } from '../src/cell-address';
import { Sheet } from '../src/sheet';
import { SheetEdit } from '../src/sheet-edit';
import { Value } from '../src/value';
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

    // ADR-0047, third run: text after a sign without '=' that is a Formula past the limits (refused, formula-depth.json)
    // spells a signed Formula, so text that spells one keeps its apostrophe.
    it.each([
        ['-', 'ABS(', '1', ')', 65],
        ['-', '(', '1', ')', 300],
        ['+', '-', '1', '', 300],
    ] as const)('reads %s%s past the limits as a signed Formula', (sign, open, inner, close, levels) => {
        expect(Entry.isSignedFormula(sign + open.repeat(levels) + inner + close.repeat(levels))).toBe(true);
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

    // ADR-0331: names follow Excel's rules, unique without regard to case, and a Workbook holds at least one Sheet.
    it.each([[['Sheet1', 'SHEET1']], [['a:b']], [[]]] as const)('refuses a Workbook of the names %j', names => {
        expect(() => new Workbook(Culture.of('en-US'), names)).toThrow(RangeError);
    });

    // ADR-0331: every Sheet is under the Workbook's culture, and a name no Sheet has finds none.
    it('puts every Sheet under the Workbook\'s culture, and finds no missing name', () => {
        const workbook = new Workbook(Culture.of('en-US'), ['Inputs', 'Totals']);
        for (const sheet of workbook.sheets) expect(sheet.culture).toBe(workbook.culture);
        expect(workbook.find('Missing')).toBeUndefined();
    });

    // ADR-0331: an edit naming no Sheet of the Workbook is refused, and changes nothing.
    it('refuses an edit naming no Sheet', () => {
        const workbook = new Workbook(Culture.of('en-US'), ['Sheet1', 'Sheet2']);
        expect(() => workbook.do('Sheet3', SheetEdit.enter(CellAddress.parse('A1'), '1'))).toThrow(RangeError);
        for (const sheet of workbook.sheets) expect([...sheet.entryAddresses]).toEqual([]);
    });

    // ADR-0046: a width that is no finite number is refused, as one outside Excel's range is.
    it.each([Number.NaN, Number.POSITIVE_INFINITY])('refuses the column width %s', width => {
        const sheet = new Workbook(Culture.of('en-US')).shown;
        const columns = CellRange.parse('B:B');
        expect(() => SheetEdit.setColumnWidth(columns, width)).toThrow(RangeError);
        expect(() => sheet.setColumnWidth(columns, width)).toThrow(RangeError);
        expect(() => SheetEdit.setAutomaticColumnWidth(columns, width)).toThrow(RangeError);
        expect(() => sheet.setAutomaticColumnWidth(columns, width)).toThrow(RangeError);
        expect(sheet.getColumnWidth(1)).toBeUndefined();
    });

    // ADR-0047 (2026-10-13): a refusal is #VALUE! outside the engine, equal to Excel's own as a Value; a refused cell's
    // #VALUE! is no constant, which a Formula could catch, and Excel's own #VALUE! is one.
    it('keeps a refusal equal to #VALUE! and refuses it as a constant', () => {
        const sheet = new Workbook(Culture.of('en-US')).shown;
        sheet.enter(CellAddress.parse('B1'), '=MROUND(6.05,0.1)');
        const refused = sheet.getValue(CellAddress.parse('B1'));
        if (refused === undefined) throw new Error('B1 holds no Value.');
        expect(refused.isRefused).toBe(true);
        expect(refused.equals(Value.fromError('#VALUE!'))).toBe(true);
        expect(() => Entry.fromValue(refused)).toThrow(RangeError);
        expect(Entry.fromValue(Value.fromError('#VALUE!')).constant?.error).toBe('#VALUE!');
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

    // ADR-0071, ticket 103: d-mmm-yy typed under en-GB, which spells built-in 15 otherwise, is a code of its own.
    it('reads d-mmm-yy under en-GB as a code of its own', () => {
        expect(NumberFormat.tryParseLocal('d-mmm-yy', Culture.of('en-GB')).format?.code).toBe('d\\-mmm\\-yy');
    });
});
