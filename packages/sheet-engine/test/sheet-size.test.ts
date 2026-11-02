// ADR-0450: a Workbook given a smaller Sheet Size holds nothing past it. Every way content could land past the edge is
// refused by name, a spill past it is #SPILL!, a document holding an Entry past it is refused when opened, and a
// Reference past it reads the empty cell it names.
import { describe, expect, it } from 'vitest';
import { CellAddress } from '../src/cell-address';
import { CellRange } from '../src/cell-range';
import { Culture } from '../src/culture';
import { SheetRefusedError } from '../src/sheet-change';
import { NumberFormat } from '../src/number-format';
import { SheetEdit } from '../src/sheet-edit';
import { SheetSize } from '../src/sheet-size';
import { Workbook } from '../src/workbook';

const tenByTen = new SheetSize(10, 10);
const at = (text: string): CellAddress => CellAddress.parse(text);
const range = (text: string): CellRange => CellRange.parse(text);

function small(size = tenByTen): Workbook {
    return new Workbook(Culture.of('en-US'), ['Sheet1'], size);
}

function refusalOf(action: () => unknown): string {
    try {
        action();
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the refusal is what the test reads
        if (error instanceof SheetRefusedError) return error.refusal.message;
        throw error;
    }
    throw new Error('The edit was not refused.');
}

describe('a Sheet Size (ADR-0450)', () => {
    it('is Excel\'s unless given', () => {
        const workbook = new Workbook(Culture.of('en-US'));
        expect(workbook.size.equals(SheetSize.excel)).toBe(true);
        expect(SheetSize.excel.lastCell.toString()).toBe('XFD1048576');
        expect(tenByTen.lastCell.toString()).toBe('J10');
    });

    it.each([[0, 10], [10, 0], [1_048_577, 10], [10, 16_385], [1.5, 10]])('refuses %d rows by %d columns', (rows, columns) => {
        expect(() => new SheetSize(rows, columns)).toThrow(RangeError);
    });

    it('takes an Entry inside it and refuses one past it, naming the edge', () => {
        const workbook = small();
        workbook.do('Sheet1', SheetEdit.enter(at('J10'), '7'));
        expect(workbook.shown.getValue(at('J10'))?.toString()).toBe('7');
        expect(refusalOf(() => workbook.do('Sheet1', SheetEdit.enter(at('K1'), '1')))).toBe("K1 runs past the Sheet's edge (J10).");
        expect(refusalOf(() => workbook.do('Sheet1', SheetEdit.enter(at('A11'), '1')))).toBe("A11 runs past the Sheet's edge (J10).");
        expect(workbook.shown.entryAddresses.map(String)).toEqual(['J10']);
    });

    it('refuses a paste whose block runs past it, whether copied inside or typed elsewhere', () => {
        const workbook = small();
        workbook.do('Sheet1', SheetEdit.enter(at('A1'), '1'));
        workbook.do('Sheet1', SheetEdit.enter(at('B2'), '2'));
        const block = workbook.shown.copy(range('A1:B2')).block;
        if (block === undefined) throw new Error('The copy holds a block.');
        expect(refusalOf(() => workbook.do('Sheet1', SheetEdit.paste(block, at('J5')))))
            .toBe("A block of 2 × 2 at J5 would run past the Sheet's edge (J10).");
        workbook.do('Sheet1', SheetEdit.paste(block, at('I9')));
        expect(workbook.shown.getValue(at('J10'))?.toString()).toBe('2');
        expect(refusalOf(() => workbook.do('Sheet1', SheetEdit.pasteText([['a', 'b'], ['c', 'd']], at('A10')))))
            .toBe("A10:B11 runs past the Sheet's edge (J10).");
    });

    it('refuses a fill asked past it', () => {
        const workbook = small();
        workbook.do('Sheet1', SheetEdit.enter(at('A1'), '1'));
        expect(refusalOf(() => workbook.do('Sheet1', SheetEdit.fill(range('A1'), range('A2:A11'), 'down'))))
            .toBe("A2:A11 runs past the Sheet's edge (J10).");
        workbook.do('Sheet1', SheetEdit.fill(range('A1'), range('A2:A10'), 'down'));
        expect(workbook.shown.getValue(at('A10'))?.toString()).toBe('1');
    });

    it('refuses an insertion that would push an Entry past it, and takes one that pushes only empty cells', () => {
        const workbook = small();
        workbook.do('Sheet1', SheetEdit.enter(at('J10'), 'last'));
        expect(refusalOf(() => workbook.do('Sheet1', SheetEdit.insertRows(4)))).toMatch(/would push J10, which holds an Entry, off the Sheet\.$/u);
        expect(refusalOf(() => workbook.do('Sheet1', SheetEdit.insertColumns(1)))).toMatch(/would push J10, which holds an Entry, off the Sheet\.$/u);
        const roomy = small();
        roomy.do('Sheet1', SheetEdit.enter(at('C3'), 'kept'));
        roomy.do('Sheet1', SheetEdit.insertRows(1));
        roomy.do('Sheet1', SheetEdit.insertColumns(1));
        expect(roomy.shown.entryAddresses.map(String)).toEqual(['D4']);
    });

    it('refuses rows or columns inserted past it, and takes whole columns formatted inside it', () => {
        const workbook = small();
        expect(refusalOf(() => workbook.do('Sheet1', SheetEdit.insertRows(10)))).toBe("A11:XFD11 runs past the Sheet's edge (J10).");
        expect(refusalOf(() => workbook.do('Sheet1', SheetEdit.setColumnWidth(CellRange.wholeColumns(10, 10), 12)))).toBe("K1:K1048576 runs past the Sheet's edge (J10).");
        workbook.do('Sheet1', SheetEdit.setColumnWidth(CellRange.wholeColumns(0, 9), 12));
    });

    it('shows #SPILL! for an array that would pass it, and spills it when the size grows', () => {
        const workbook = small();
        workbook.do('Sheet1', SheetEdit.enter(at('A8'), '=SEQUENCE(5)'));
        expect(workbook.shown.getValue(at('A8'))?.toString()).toBe('#SPILL!');
        expect(workbook.shown.getValue(at('A10'))).toBeUndefined();
        workbook.resize(new SheetSize(12, 10));
        expect(workbook.shown.getValue(at('A8'))?.toString()).toBe('1');
        expect(workbook.shown.getValue(at('A12'))?.toString()).toBe('5');
        workbook.resize(tenByTen);
        expect(workbook.shown.getValue(at('A8'))?.toString()).toBe('#SPILL!');
    });

    it('reads a Reference past it as the empty cell it names', () => {
        const workbook = small();
        workbook.do('Sheet1', SheetEdit.enter(at('A1'), '=ISBLANK(K1)'));
        workbook.do('Sheet1', SheetEdit.enter(at('B1'), '=SUM(C:C)+K20'));
        workbook.do('Sheet1', SheetEdit.enter(at('C3'), '4'));
        expect(workbook.shown.getValue(at('A1'))?.toString()).toBe('TRUE');
        expect(workbook.shown.getValue(at('B1'))?.toString()).toBe('4');
    });

    it('refuses a document with an Entry past it, naming the cell, and opens one with only a Cell Format past it', () => {
        const holding = new Workbook(Culture.of('en-US'), ['Data']);
        holding.do('Data', SheetEdit.enter(at('B2'), '1'));
        holding.do('Data', SheetEdit.enter(at('L12'), '2'));
        const document = holding.toSheetDocument('Data');
        expect(() => Workbook.open(document, tenByTen)).toThrow("Data!L12 holds an Entry, and a Sheet of 10 rows by 10 columns ends at J10.");
        const formatted = new Workbook(Culture.of('en-US'), ['Data']);
        formatted.do('Data', SheetEdit.enter(at('B2'), '1'));
        formatted.do('Data', SheetEdit.setNumberFormat(range('L12'), NumberFormat.parse('0.00')));
        const opened = Workbook.open(formatted.toSheetDocument('Data'), tenByTen);
        expect(opened.size.equals(tenByTen)).toBe(true);
        expect(opened.shown.getNumberFormat(at('L12')).code).toBe('0.00');
    });

    it('refuses a size that would leave an Entry outside, and keeps the one it had', () => {
        const workbook = small(new SheetSize(20, 20));
        workbook.do('Sheet1', SheetEdit.enter(at('O15'), 'x'));
        expect(() => workbook.resize(tenByTen)).toThrow('Sheet1!O15 holds an Entry, and a Sheet of 10 rows by 10 columns ends at J10.');
        expect(workbook.size.rows).toBe(20);
    });

    it('holds every Sheet added to the Workbook to the same size', () => {
        const workbook = small();
        workbook.do('Sheet1', SheetEdit.insertSheet());
        const added = workbook.sheets[1];
        if (added === undefined) throw new Error('A Sheet was added.');
        expect(refusalOf(() => workbook.do(added.name, SheetEdit.enter(at('K1'), '1')))).toBe("K1 runs past the Sheet's edge (J10).");
    });
});
