// The calls a shared case does not make, held here as EngineSurfaceTests.cs, CellAddressTests.cs,
// CompletionTriggerTests.cs and FormulaReferencesTests.cs hold them on ExSheet.Engine (ADR-0370 section 2): a name asked about, a Sheet renamed
// directly, two Entries or two keys compared, a walk over every column, prefix or declared function, a list read for
// one name in it, and the package's surface read for a row that can be hidden.
import { describe, expect, it } from 'vitest';
import * as engine from '../src/index';
import { CellRange } from '../src/cell-range';
import { Culture } from '../src/culture';
import { DeclaredFunction } from '../src/declared-function';
import { Entry } from '../src/entry';
import { NumberFormat, numberFormatColourRgb } from '../src/number-format';
import { CellAddress, columnCount } from '../src/cell-address';
import { formulaReferences } from '../src/formula-entry';
import { Sheet } from '../src/sheet';
import { SheetDocument } from '../src/sheet-document';
import { SheetEdit } from '../src/sheet-edit';
import { Value } from '../src/value';
import { Workbook } from '../src/workbook';

/** The serial number of 26 September of the year. */
function twentySixthOfSeptember(year: number): number {
    return (Date.UTC(year, 8, 26) - Date.UTC(1899, 11, 30)) / 86_400_000;
}

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

    // ADR-0046 (2026-09-28)/0048: an older document's widths read as custom, as Excel marks the user's and an entry's
    // alike; a Sheet opens them by their kind (column-width.json).
    it.each([
        '{"version":4,"culture":"en-US","name":"Sheet1","columnWidths":[{"at":"B:C","width":20}],"cells":[]}',
        '{"version":5,"culture":"en-US","name":"Sheet1","columnWidths":[{"at":"B:B","width":20,"custom":true},{"at":"C:C","width":11,"custom":false}],"cells":[]}',
    ])('reads the widths of %s as custom', json => {
        const widths = SheetDocument.fromJson(json).columnWidths;
        expect(widths.length).toBeGreaterThan(0);
        for (const width of widths) expect(width.isCustom).toBe(true);
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

    // ADR-0047/0048: a day and a month name without a year is that day of the current year, as Excel reads it
    // (TYPED-022). The year is the machine's clock's, which a sheet case does not set.
    it.each([
        ['en-US', '26-Sep'],
        ['en-US', 'Sep 26'],
        ['en-US', '9/26'],
        ['de-DE', '26-Sep'],
    ] as const)('reads %s %s as 26 September of the current year', (culture, typed) => {
        const sheet = new Workbook(Culture.of(culture)).shown;
        const a1 = CellAddress.parse('A1');
        // The year is read on both sides of the entry, so a run across New Year's midnight holds either way.
        const before = new Date().getFullYear();
        sheet.enter(a1, typed);
        const after = new Date().getFullYear();
        expect([twentySixthOfSeptember(before), twentySixthOfSeptember(after)]).toContain(sheet.getValue(a1)?.number);
        expect(sheet.getNumberFormat(a1).code).toBe('d-mmm');
    });

    // ADR-0049, ADR-0331 §9: a table is never undeclared: the Workbook, which holds the tables, has no API for it.
    it('has no way to undeclare a Linked Table', () => {
        // Every name a Workbook answers to: its class's statics, its prototype chain's members, and its own fields.
        const workbook = new Workbook(Culture.of('en-US'));
        const members = [...Object.getOwnPropertyNames(Workbook).map(n => `static ${n}`), ...Object.getOwnPropertyNames(workbook)];
        for (let p: object | null = Workbook.prototype; p !== null && p !== Object.prototype; p = Object.getPrototypeOf(p) as object | null) {
            members.push(...Object.getOwnPropertyNames(p));
        }
        const names = members.filter(n => /linkedtable|undeclare/iu.test(n));
        expect(names.toSorted()).toEqual(['declareLinkedTable', 'linkedTableColumns', 'linkedTables', 'pushLinkedTable']);
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

    // ADR-0046: Columns A to XFD are the Column Headings.
    it('reads every column name back as its column', () => {
        for (let column = 0; column < columnCount; column++) {
            expect(CellAddress.tryParseColumn(CellAddress.columnName(column))).toBe(column);
        }
        expect(CellAddress.columnName(columnCount - 1)).toBe('XFD');
    });

    // ADR-0046: addresses order row by row, as the rows a change repaints.
    it('orders addresses row by row', () => {
        const sorted = ['B2', 'A2', 'C1', 'A1'].map(a => CellAddress.parse(a)).toSorted((a, b) => a.compareTo(b));
        expect(sorted.map(a => a.toString())).toEqual(['A1', 'C1', 'A2', 'B2']);
    });

    // ADR-0057 / SH-30: a structured reference's key compares equal however the table and the column were cased.
    it('keys a structured reference alike however it is cased', () => {
        const columns = formulaReferences('=SUM(Positions[PV])+SUM(positions[pv])+SUM(POSITIONS[[Pv]])', 'Sheet1').map(r => r.linkedColumn);
        expect(columns).toHaveLength(3);
        const [first] = columns;
        if (first === undefined) throw new Error('The first answer names no column.');
        for (const column of columns) {
            if (column === undefined) throw new Error('An answer names no column.');
            expect(column.equals(first)).toBe(true);
            expect(column.key).toBe(first.key);
        }
    });

    // ADR-0057 / SH-30: another column, or another table's column of the same name, is another key.
    it.each(['=SUM(Positions[PV])+SUM(Positions[Id])', '=SUM(Positions[PV])+SUM(Accounts[PV])'])('keys %s apart', text => {
        const [first, second] = formulaReferences(text, 'Sheet1').map(r => r.linkedColumn);
        if (first === undefined || second === undefined) throw new Error(`${text} names fewer than two columns.`);
        expect(first.equals(second)).toBe(false);
        expect(first.key).not.toBe(second.key);
    });

    // ADR-0057 reading: an unfinished Formula is coloured as far as it goes: every prefix of a Formula as it is typed
    // is answered, and none throws.
    it('answers every prefix of a Formula being typed', () => {
        const formulas = [
            '=SUM(A1,Sheet1!B2:C3,"x""y",Positions[[PV]],LOG10(D4))+\'Sheet 1\'!E5*$F$6%-G:G&1:1',
            "=IF(Sheet2!A1<>#N/A,XLOOKUP(A1,Positions[Id],Positions[a'[b]),'It''s'!B2)",
            '=-$XFD$1048576^2/Sheet1!#REF!',
        ];
        for (const formula of formulas) {
            for (let typed = 0; typed <= formula.length; typed++) {
                const text = formula.slice(0, typed);
                for (const sheetName of ['Sheet1', 'Sheet 1', "It's"]) {
                    let end = 1;
                    for (const answer of formulaReferences(text, sheetName)) {
                        // In the text, after the =, in the order written, none overlapping another, and either cells or a column.
                        expect(answer.start).toBeGreaterThanOrEqual(end);
                        expect(answer.length).toBeGreaterThanOrEqual(1);
                        expect(answer.start + answer.length).toBeLessThanOrEqual(text.length);
                        expect(answer.cells === undefined).not.toBe(answer.linkedColumn === undefined);
                        end = answer.start + answer.length;
                    }
                }
            }
        }
    });

    // ADR-0058, SH-36: unrestricted arguments do not declare a choice list.
    it('declares no values for an unrestricted argument', () => {
        const listing = new Set(['XLOOKUP', 'VLOOKUP', 'HLOOKUP', 'MATCH', 'YEARFRAC', 'SUBTOTAL', 'AGGREGATE']);
        for (const declared of DeclaredFunction.all.filter(f => !listing.has(f.name))) {
            for (let index = 0; index < 8; index++) expect(declared.valuesOf(index)).toEqual([]);
        }
    });

    // ADR-0058 (Q54), SH-36: outside a value-list argument a letter lists the functions and Linked Tables it begins, as
    // before: at another argument, after an operator, inside a call of its own, and inside a grouping parenthesis,
    // which holds an expression of its own (decided with the user 2026-10-01, not asked of Excel).
    it.each([
        ['=A|', 'AVERAGE'],
        ['=1+A|', 'AVERAGE'],
        ['=SUM(A|', 'AVERAGE'],
        ['=XLOOKUP(1,A|', 'AVERAGE'],
        ['=XLOOKUP(1,A2:A4,B2:B4,P|', 'Positions'],
        ['=XLOOKUP(1,A2:A4,B2:B4,,0,1,P|', 'Positions'],
        ['=XLOOKUP(1,A2:A4,B2:B4,,SUM(A|', 'AVERAGE'],
        ['=XLOOKUP(1,A2:A4,B2:B4,,(A|', 'AVERAGE'],
        ['=XLOOKUP(1,A2:A4,B2:B4,,ROUND(1,X|', 'XLOOKUP'],
    ] as const)('lists names for %s outside a value-list argument', (marked, listed) => {
        const workbook = new Workbook(Culture.of('en-US'));
        workbook.declareLinkedTable('Positions', ['Id', 'PV']);
        const caret = marked.indexOf('|');
        const completion = workbook.shown.complete(marked.replace('|', ''), caret);
        if (completion === undefined) throw new Error(`${marked} lists nothing.`);
        expect(completion.candidates.map(c => c.name)).toContain(listed);
        expect(completion.candidates.filter(c => c.kind === 'argumentValue')).toEqual([]);
    });
});
