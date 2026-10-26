// What a reader of another format needs from the engine (ADR-0330 §3, §4) that a shared case does not hold, as
// DocumentFromPartsTests.cs holds it on ExSheet.Engine: a constant moved being the same Entry, the walk over every
// Error Value, and documents built from their parts, which the Script Core builds with `of` where C# has constructors.
import { describe, expect, it } from 'vitest';
import { CellAddress } from '../src/cell-address';
import { Culture } from '../src/culture';
import { Entry } from '../src/entry';
import { SheetDocument } from '../src/sheet-document';
import { SheetDocumentCell } from '../src/sheet-document-parts';
import { errorValues, Value } from '../src/value';
import { Workbook } from '../src/workbook';
import { WorkbookDocument } from '../src/workbook-document';

const enUs = Culture.of('en-US');

function formula(text: string): Entry {
    const entry = Entry.parse(text, Culture.invariant);
    if (entry === undefined) throw new Error(`${text} is no Entry.`);
    return entry;
}

describe('a document from its parts', () => {
    // ADR-0330 §4: a constant moved is itself.
    it('moves a constant to itself', () => {
        const constant = Entry.fromValue(Value.fromNumber(1));
        expect(constant.movedBy(5, 5)).toBe(constant);
    });

    // ADR-0330 §4: an error cell's text is read by the engine's own table, exactly as written: every Error Value a
    // cell can hold, and nothing else.
    it('reads every Error Value a cell can hold by its text, as written', () => {
        for (const error of errorValues.filter(e => e !== '#GETTING_DATA' && e !== '#CIRC!')) {
            expect(Entry.fromErrorText(error)?.constant?.equals(Value.fromError(error))).toBe(true);
            expect(Entry.fromErrorText(error.toLowerCase())).toBeUndefined();
        }
    });

    // ADR-0330 §3: a Workbook Document built from its Sheets opens as the Workbook it describes.
    it('opens a Workbook Document built from its Sheets', () => {
        const first = SheetDocument.of(enUs, 'Trades', [
            new SheetDocumentCell(CellAddress.parse('A2'), formula('=A1*2')),
            new SheetDocumentCell(CellAddress.parse('A1'), Entry.fromValue(Value.fromNumber(21))),
        ]);
        const second = SheetDocument.of(enUs, 'Desks', [new SheetDocumentCell(CellAddress.parse('B1'), formula('=Trades!A2'))]);

        const document = WorkbookDocument.of(enUs, 'Desks', [first, second]);
        const workbook = Workbook.open(WorkbookDocument.fromJson(document.toJson()));

        expect(first.cells.map(c => c.address.toString())).toEqual(['A1', 'A2']);
        expect(document.shown).toBe('Desks');
        expect(document.culture).toBe('en-US');
        expect(workbook.shown.name).toBe('Desks');
        expect(workbook.sheets[1]?.getValue(CellAddress.parse('B1'))?.number).toBe(42);
    });

    // ADR-0330 §3, ADR-0048: a document built from parts is refused as one read from JSON would be: nothing is guessed.
    it('refuses a document built from parts that cannot be one', () => {
        const cell = new SheetDocumentCell(CellAddress.parse('A1'), Entry.fromValue(Value.fromNumber(1)));
        const sheet = SheetDocument.of(enUs, 'Sheet1', []);

        expect(() => SheetDocument.of(enUs, 'Bad[name]', [])).toThrow(RangeError);
        expect(() => SheetDocument.of(enUs, 'Sheet1', [cell, cell])).toThrow(RangeError);
        expect(() => SheetDocument.of(Culture.of('zz-ZZ'), 'Sheet1', [])).toThrow(RangeError);
        expect(() => WorkbookDocument.of(enUs, 'Sheet1', [])).toThrow(RangeError);
        expect(() => WorkbookDocument.of(enUs, 'Sheet1', [sheet, SheetDocument.of(enUs, 'SHEET1', [])])).toThrow(RangeError);
        expect(() => WorkbookDocument.of(enUs, 'Other', [sheet])).toThrow(RangeError);
        expect(() => WorkbookDocument.of(Culture.of('de-DE'), 'Sheet1', [sheet])).toThrow(RangeError);
    });
});
