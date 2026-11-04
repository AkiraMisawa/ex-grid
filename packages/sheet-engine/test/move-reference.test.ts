// ADR-0480, section 1 (SH-69): what a Reference Outline dragged by its edge or a corner square leaves in the Formula,
// written as the Reference was. New to the Script Core: the C# engine never had the gesture.
import { describe, expect, it } from 'vitest';
import { CellRange } from '../src/cell-range';
import { moveReference } from '../src/formula-entry';
import { SheetSize } from '../src/sheet-size';

const range = (text: string): CellRange => CellRange.parse(text);

describe('ADR-0480 / SH-69: a Reference moved or resized keeps its form', () => {
    it.each([
        ['A1', 'C3', 'C3'],
        ['$A$1', 'C3', '$C$3'],
        ['A$1', 'C3', 'C$3'],
        ['$A1', 'C3', '$C3'],
        ['a1', 'b2', 'B2'],
        ['A1:A5', 'C1:C5', 'C1:C5'],
        ['$A$1:B2', 'C3:D4', '$C$3:D4'],
        ['A$1:$B2', 'B1:C2', 'B$1:$C2'],
        ['Sheet1!A1:A5', 'B2:B6', 'Sheet1!B2:B6'],
        ["'Q1 Sales'!$A$1", 'B2', "'Q1 Sales'!$B$2"],
    ])('ADR-0480: %s moved names %s as %s', (reference, to, written) => {
        expect(moveReference(reference, range(to))).toBe(written);
    });

    it('ADR-0480: a one-cell Reference resized to a range writes both ends in its form', () => {
        expect(moveReference('$A$1', range('A1:B3'))).toBe('$A$1:$B$3');
        expect(moveReference('A$1', range('A1:B3'))).toBe('A$1:B$3');
    });

    it('ADR-0480: a range resized to one cell writes it once, in its first end\'s form', () => {
        expect(moveReference('$A$1:B5', range('A1'))).toBe('$A$1');
        expect(moveReference('A1:$B$5', range('C3'))).toBe('C3');
    });

    it('ADR-0480: whole columns and rows that still span the Sheet stay whole, each end keeping its $', () => {
        expect(moveReference('A:A', range('C1:C1048576'))).toBe('C:C');
        expect(moveReference('$A:B', range('C1:D1048576'))).toBe('$C:D');
        expect(moveReference('1:$2', range('A4:XFD5'))).toBe('4:$5');
        expect(moveReference('Sheet1!A:A', range('B1:B1048576'))).toBe('Sheet1!B:B');
    });

    it('ADR-0480 / ADR-0460: "every row" is the Sheet\'s size, not Excel\'s', () => {
        const tenByTen = new SheetSize(10, 10);
        expect(moveReference('A:A', range('C1:C10'), tenByTen)).toBe('C:C');
        expect(moveReference('2:2', range('A5:J5'), tenByTen)).toBe('5:5');
    });

    it('ADR-0480: whole columns resized off the Sheet\'s rows are written as their cells, the columns\' $ kept', () => {
        expect(moveReference('$A:A', range('A5:B1048576'))).toBe('$A5:B1048576');
        expect(moveReference('$1:1', range('B1:XFD1'))).toBe('B$1:XFD1');
    });

    it('ADR-0480: text that is not one Reference, or a range past the Sheet, is refused by name', () => {
        expect(() => moveReference('', range('A1'))).toThrow(RangeError);
        expect(() => moveReference('A1+B1', range('A1'))).toThrow(RangeError);
        expect(() => moveReference('SUM', range('A1'))).toThrow(RangeError);
        expect(() => moveReference('A1', range('K1'), new SheetSize(10, 10))).toThrow(RangeError);
    });
});
