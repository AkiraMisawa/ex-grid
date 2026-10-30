// Which changes widen the columns whose numbers no longer fit (ADR-0071, case 17; ADR-0100), as the rules of
// C#'s SheetWidening say them. Which columns, and to what width, is held by dotnet/tests/ExSheet.Components'
// FormatWideningTests against the grid's text estimate, which this package does not have yet.
import { describe, expect, it } from 'vitest';
import { CellFill, CellFormatChange, NumberFormat } from '@ex-grid/sheet-engine';
import { widensAsAHomeTabButtonDoes, widensAsAKeyDoes, widensAsFormatCellsOkDoes } from '../src/index';
import { colourFromRgb } from '../src/cell-colour';

const red = colourFromRgb(0xFF0000);

describe('widening', () => {
    it('ADR-0071 (ticket 58): a Number Format key, Ctrl+5 and Ctrl+U widen; Ctrl+B, Ctrl+I and a Fill do not (runs 14 and 17)', () => {
        expect.hasAssertions();
        expect(widensAsAKeyDoes(new CellFormatChange({ numberFormat: NumberFormat.parse('0.00') }))).toBe(true);
        expect(widensAsAKeyDoes(new CellFormatChange({ strikethrough: true }))).toBe(true);
        expect(widensAsAKeyDoes(new CellFormatChange({ underline: false }))).toBe(true);
        expect(widensAsAKeyDoes(new CellFormatChange({ bold: true }))).toBe(false);
        expect(widensAsAKeyDoes(new CellFormatChange({ italic: true }))).toBe(false);
        expect(widensAsAKeyDoes(new CellFormatChange({ fill: CellFill.solid(red) }))).toBe(false);
    });

    it('ADR-0071, ADR-0100 (run 18a, widening case 5): the Home tab\'s Font Color widens, as a key does otherwise', () => {
        expect.hasAssertions();
        expect(widensAsAHomeTabButtonDoes(new CellFormatChange({ fontColour: red }))).toBe(true);
        expect(widensAsAKeyDoes(new CellFormatChange({ fontColour: red }))).toBe(false);
        expect(widensAsAHomeTabButtonDoes(new CellFormatChange({ bold: true }))).toBe(false);
    });

    it('ADR-0071 (decided 2026-10-12): Format Cells\' OK widens for any part of the Font, and never for a Fill or an alignment', () => {
        expect.hasAssertions();
        expect(widensAsFormatCellsOkDoes(new CellFormatChange({ bold: true }))).toBe(true);
        expect(widensAsFormatCellsOkDoes(new CellFormatChange({ italic: false }))).toBe(true);
        expect(widensAsFormatCellsOkDoes(new CellFormatChange({ fill: CellFill.solid(red) }))).toBe(false);
        expect(widensAsFormatCellsOkDoes(new CellFormatChange({ alignment: 'center' }))).toBe(false);
    });
});
