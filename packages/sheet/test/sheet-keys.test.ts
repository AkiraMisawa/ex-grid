// Excel's keys as ExSheet declares them and what each does (ADR-0071, ADR-0050 item 14, ADR-0331 §4), ported from
// the pure facts of dotnet/tests/ExSheet.Components/FormatKeyTests.cs, StructureKeyTests.cs and SheetTabsTests.cs.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import type { GridSelection } from '@ex-grid/core';
import { CellAddress, CellFormat, CellFont, CellFill, CellBorders, Culture, NumberFormat, SheetEdit, Workbook } from '@ex-grid/sheet-engine';
import {
    sheetFormatChangeFor, sheetFormatKeyOf, sheetFormatKeysDeclared, sheetStructureEditFor, sheetStructureKeyOf,
    sheetStructureKeysDeclared, sheetTabKeysDeclared, sheetTabStepOf,
} from '../src/index';

const plain = new CellFormat(NumberFormat.general, 'general', CellFont.default, CellFill.none, CellBorders.none);

function selectionOf(...ranges: readonly (readonly [number, number, number, number])[]): GridSelection {
    return {
        ranges: ranges.map(([topRow, leftColumn, rowCount, columnCount]) => ({ topRow, leftColumn, rowCount, columnCount })),
        isEmpty: ranges.length === 0,
        focus: undefined,
        extent: undefined,
    } satisfies Pick<GridSelection, 'ranges' | 'isEmpty' | 'focus' | 'extent'> as unknown as GridSelection;
}

describe('formatting keys', () => {
    it('ADR-0071 / ADR-0050 item 14, SH-42: exactly Excel\'s keys, by the character typed, with the Shift a layout needs', () => {
        expect.hasAssertions();
        const expected = [
            'Control+b', 'Control+B', 'Control+i', 'Control+I', 'Control+u', 'Control+U',
            'Control+2', 'Control+Shift+2', 'Control+3', 'Control+Shift+3', 'Control+4', 'Control+Shift+4',
            'Control+5', 'Control+Shift+5',
            'Control+~', 'Control+Shift+~', 'Control+!', 'Control+Shift+!', 'Control+@', 'Control+Shift+@',
            'Control+#', 'Control+Shift+#', 'Control+$', 'Control+Shift+$', 'Control+%', 'Control+Shift+%',
            'Control+^', 'Control+Shift+^', 'Control+&', 'Control+Shift+&', 'Control+_', 'Control+Shift+_',
            'Control+1', 'Control+Shift+1',
        ];
        expect(sheetFormatKeysDeclared().toSorted()).toEqual(expected.toSorted());
        // Ctrl+Shift+U is another key in Excel.
        expect(sheetFormatKeyOf('Control+Shift+U')).toBeUndefined();
    });

    it.each([
        ['en-GB', '~', 'General'], ['en-GB', '!', '#,##0.00'], ['en-GB', '@', 'h:mm'], ['en-GB', '#', 'd-mmm-yy'],
        ['en-GB', '$', '$#,##0.00_);[Red]($#,##0.00)'], ['en-GB', '%', '0%'], ['en-GB', '^', '0.00E+00'],
        ['en-US', '~', 'General'], ['en-US', '!', '#,##0.00'], ['en-US', '@', 'h:mm AM/PM'], ['en-US', '#', 'd-mmm-yy'],
        ['en-US', '$', '$#,##0.00_);[Red]($#,##0.00)'], ['en-US', '%', '0%'], ['en-US', '^', '0.00E+00'],
        ['ja-JP', '~', 'General'], ['ja-JP', '!', '#,##0.00'], ['ja-JP', '@', 'h:mm'], ['ja-JP', '#', 'd-mmm-yy'],
        ['ja-JP', '$', '$#,##0_);[Red]($#,##0)'], ['ja-JP', '%', '0%'], ['ja-JP', '^', '0.00E+00'],
    ])('ADR-0071, SH-42 (cases 18 to 20): under %s Ctrl+Shift+%s applies %s', (culture, key, code) => {
        expect.hasAssertions();
        const kind = sheetFormatKeyOf(`Control+Shift+${key}`);
        expect(kind).toBeDefined();
        if (kind !== undefined) expect(sheetFormatChangeFor(kind, plain, Culture.of(culture)).numberFormat?.code).toBe(code);
    });

    it('ADR-0071, SH-42 (case 17): a toggle\'s direction follows the Focus cell, and sets only its own emphasis', () => {
        expect.hasAssertions();
        const bold = new CellFormat(NumberFormat.general, 'general', new CellFont({ bold: true }), CellFill.none, CellBorders.none);
        expect(sheetFormatChangeFor('bold', bold, Culture.of('en-US')).bold).toBe(false);
        const change = sheetFormatChangeFor('bold', plain, Culture.of('en-US'));
        expect([change.bold, change.italic, change.underline, change.numberFormat]).toEqual([true, undefined, undefined, undefined]);
        expect(sheetFormatKeyOf('Control+2')).toBe('bold');
    });

    it('ADR-0071, SH-42 (case 13): Ctrl+Shift+& outlines thin and Automatic; Ctrl+Shift+_ clears every edge', () => {
        expect.hasAssertions();
        const outline = sheetFormatChangeFor('outline', plain, Culture.of('en-US')).borders;
        expect([outline?.top?.style, outline?.top?.colour.isAutomatic, outline?.insideHorizontal]).toEqual(['thin', true, undefined]);
        const none = sheetFormatChangeFor('noBorders', plain, Culture.of('en-US')).borders;
        expect([none?.top?.isNone, none?.insideVertical?.isNone]).toEqual([true, true]);
        expect(() => sheetFormatChangeFor('formatCells', plain, Culture.of('en-US'))).toThrow(RangeError);
    });
});

describe('insert and delete keys', () => {
    const extent = { rowCount: 1_048_576, columnCount: 16_384 };

    it('ADR-0050 item 14, SH-53: the insert and delete keys are Ctrl with + (with or without Shift) and Ctrl with -', () => {
        expect.hasAssertions();
        expect(sheetStructureKeysDeclared()).toEqual(['Control++', 'Control+Shift++', 'Control+-']);
        expect(sheetStructureKeyOf('Control+Shift++')).toBe('insert');
        expect(sheetStructureKeyOf('Control+=')).toBeUndefined();
    });

    it('SH-53 (Part C, case 12): over whole rows the keys insert above and delete them; over whole columns, to the left', () => {
        expect.hasAssertions();
        const workbook = new Workbook(Culture.of('en-US'));
        workbook.do('Sheet1', SheetEdit.enter(CellAddress.parse('A3'), '5'));
        const rows = sheetStructureEditFor('insert', selectionOf([2, 0, 3, extent.columnCount]), extent);
        expect(rows).toBeDefined();
        if (rows !== undefined) workbook.do('Sheet1', rows);
        expect(workbook.shown.entryAddresses.map(a => a.toString())).toEqual(['A6']);
        expect(sheetStructureEditFor('delete', selectionOf([0, 1, extent.rowCount, 2]), extent)).toBeDefined();
    });

    it.each([
        ['a range', [[1, 1, 2, 2]]],
        ['a cell', [[0, 0, 1, 1]]],
        ['every cell', [[0, 0, 1_048_576, 16_384]]],
        ['several ranges', [[0, 0, 1, 16_384], [3, 0, 1, 16_384]]],
    ] as const)('SH-53: %s is neither whole rows nor whole columns alone, and the key refuses it', (_, ranges) => {
        expect.hasAssertions();
        expect(sheetStructureEditFor('insert', selectionOf(...ranges), extent)).toBeUndefined();
    });
});

describe('Sheet Tab keys', () => {
    it('ADR-0331 §4: Ctrl+PageUp and Ctrl+PageDown move one Sheet, and Shift+F11 is declared beside them', () => {
        expect.hasAssertions();
        expect(sheetTabKeysDeclared()).toEqual(['Control+PageUp', 'Control+PageDown', 'Shift+F11']);
        expect([sheetTabStepOf('Control+PageUp'), sheetTabStepOf('Control+PageDown'), sheetTabStepOf('Shift+F11')]).toEqual([-1, 1, undefined]);
    });
});

describe('the keys the Keyboard page is held to', () => {
    it('ADR-0110: the keys the Sheet declares to its grid are those tests/cases/sheet/sheet-keys.json lists, which the Docs Site reads', () => {
        expect.hasAssertions();
        const file = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../tests/cases/sheet/sheet-keys.json');
        const listed = (JSON.parse(fs.readFileSync(file, 'utf8')) as { readonly declared: readonly string[] }).declared;
        const declared = [...sheetFormatKeysDeclared(), ...sheetStructureKeysDeclared(), ...sheetTabKeysDeclared()];
        expect(declared.toSorted()).toEqual(listed.toSorted());
    });
});
