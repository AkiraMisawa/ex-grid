// 3D References against Excel's own answers (ADR-0331 §7, exsheet-workbook tickets 11 and 12), read from
// dotnet/tests/ExSheet.Engine.Tests/ThreeDReadings/excel-readings.json as ThreeDExcelReadingsTests.cs reads it: every case
// the 2026-10-24 Windows run put to Excel. Each Workbook is the run's: Out, then Sheet1 to Sheet5, B2 on SheetN
// holding 10^(N-1) and C3 twice that, so a sum names the Sheets it read.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { CellAddress } from '../src/cell-address';
import { Culture } from '../src/culture';
import { roundHalfToEven } from '../src/dotnet';
import { Entry } from '../src/entry';
import { FormulaSyntaxError } from '../src/formula-syntax-error';
import { SheetEdit } from '../src/sheet-edit';
import { Value } from '../src/value';
import { Workbook, type WorkbookStep } from '../src/workbook';

interface Reading {
    readonly formula: string;
    readonly error?: string;
    readonly number?: number;
    readonly boolean?: boolean;
    readonly text?: string;
}

interface FormulaCase extends Reading {
    readonly setup: string;
    readonly cell: string;
    readonly written: string;
}

interface SheetsCase {
    readonly id: string;
    readonly order?: readonly string[];
    readonly read: Readonly<Record<string, Reading>>;
}

interface Readings {
    readonly formulas: readonly FormulaCase[];
    readonly sheets: readonly SheetsCase[];
    readonly rows: readonly SheetsCase[];
    readonly copies: readonly SheetsCase[];
}

const file = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../dotnet/tests/ExSheet.Engine.Tests/ThreeDReadings/excel-readings.json');
const readings = JSON.parse(fs.readFileSync(file, 'utf8')) as Readings;

const sheetNames = ['Out', 'Sheet1', 'Sheet2', 'Sheet3', 'Sheet4', 'Sheet5'];

/** The forms ExSheet does not read for any Reference, and refuses on entry: the intersection operator, and the range operator between a Reference and anything but its own cells. */
const notRead = [
    '=SUM(Sheet2:Sheet4!B2:C3 Sheet2:Sheet4!B2)',
    '=SUM(Sheet2:Sheet4!B2:Sheet2:Sheet4!C3)',
    '=SUM(Sheet2:Sheet4!A1:INDEX(Sheet2!A:A,2))',
];

function enter(workbook: Workbook, sheet: string, address: string, typed: string): WorkbookStep {
    return workbook.do(sheet, SheetEdit.enter(CellAddress.parse(address), typed));
}

function run(more = false): Workbook {
    const workbook = new Workbook(Culture.of('en-US'), sheetNames);
    // RANDBETWEEN draws only once the host has given a seed (ADR-0180).
    workbook.setSeed(7n);
    for (let n = 1; n <= 5; n++) {
        const value = 10 ** (n - 1);
        enter(workbook, `Sheet${String(n)}`, 'B2', String(value));
        enter(workbook, `Sheet${String(n)}`, 'C3', String(2 * value));
    }
    if (more) {
        enter(workbook, 'Out', 'C1', '1');
        enter(workbook, 'Out', 'C2', '2');
        workbook.do('Sheet2', SheetEdit.setEntries([[CellAddress.parse('D4'), Entry.fromValue(Value.fromText('5'))]]));
        enter(workbook, 'Sheet3', 'D4', 'x');
        enter(workbook, 'Sheet3', 'D5', 'TRUE');
        enter(workbook, 'Sheet4', 'D6', '=1/0');
        enter(workbook, 'Sheet2', 'E1', '=NA()');
        enter(workbook, 'Sheet4', 'E1', '=1/0');
    }
    return workbook;
}

function sheetOf(workbook: Workbook, name: string): NonNullable<ReturnType<Workbook['find']>> {
    const sheet = workbook.find(name);
    if (sheet === undefined) throw new Error(`No Sheet ${name}.`);
    return sheet;
}

function roundTo(x: number, digits: number): number {
    const power = 10 ** digits;
    return roundHalfToEven(x * power) / power;
}

function expectValue(value: Value | undefined, excel: Reading, because: string): void {
    const shown = value === undefined ? 'blank' : value.toString();
    if (excel.error !== undefined) {
        expect(value?.kind === 'error' && !value.isRefused && value.error === excel.error, `${because}: ${shown}, Excel ${excel.error}`).toBe(true);
    } else if (excel.number !== undefined) {
        expect(value?.kind, `${because}: ${shown}, Excel ${String(excel.number)}`).toBe('number');
        expect(roundTo((value as Value).number, 9), because).toBe(roundTo(excel.number, 9));
    } else if (excel.boolean !== undefined) {
        expect(value?.kind === 'boolean' && value.boolean === excel.boolean, `${because}: ${shown}, Excel ${String(excel.boolean)}`).toBe(true);
    } else if (excel.text !== undefined) {
        expect(value?.kind === 'text' && value.text === excel.text, `${because}: ${shown}, Excel "${excel.text}"`).toBe(true);
    } else {
        expect(value, `${because}: ${shown}, Excel blank`).toBeUndefined();
    }
}

/** Excel's answer in a cell of the record against the engine's, the Formula's text and its Value. */
function expectCell(workbook: Workbook, cell: string, excel: Reading, because: string): void {
    const [name = '', at = ''] = cell.split('!');
    const sheet = sheetOf(workbook, name);
    const address = CellAddress.parse(at);
    // ExSheet writes a Reference to a Sheet that is gone #REF!, where Excel writes #REF! before its cells (ticket 12's answer 11).
    const formula = excel.formula.replaceAll('#REF!B2:C3', '#REF!').replaceAll('#REF!B2', '#REF!');
    const written = sheet.getEntry(address)?.formula ?? ''; // oxlint-disable-line ex-grid/no-stand-in-default -- a cell with no Formula is written as nothing, as the C# test reads it
    expect(written, `${because}: ${cell} is written ${written}, Excel ${formula}`).toBe(formula);
    expectValue(sheet.getValue(address), excel, `${because}: ${cell}`);
}

function names(workbook: Workbook): string[] {
    return workbook.sheets.map(s => s.name);
}

/** The Sheet a part of a case's id names: `out`, or a Sheet's number. */
function sheetNamed(part: string | undefined): string {
    return part === 'out' ? 'Out' : `Sheet${String(part)}`;
}

/** The Sheet operation a case of the record names, as the engine's own: its steps, in the order done. */
function sheetOperation(workbook: Workbook, id: string): WorkbookStep[] {
    const parts = id.split('-');
    const at = (name: string): number => names(workbook).indexOf(name);
    switch (parts[0]) {
        case 'move': {
            const moved = sheetNamed(parts[1]);
            const other = sheetNamed(parts[3]);
            let to: number;
            if (parts[2] === 'to') to = parts[3] === 'end' ? workbook.sheets.length - 1 : 0;
            else if (parts[2] === 'before') to = at(other) - (at(moved) < at(other) ? 1 : 0);
            else to = at(other) + (at(moved) < at(other) ? 0 : 1);
            return [workbook.do(moved, SheetEdit.moveSheet(to))];
        }
        case 'delete':
            return parts.slice(1).filter(p => p !== 'then').map(part => workbook.do(sheetNamed(part), SheetEdit.deleteSheet()));
        case 'insert':
            return [
                workbook.do(sheetNamed(parts[2]), SheetEdit.insertSheet('New', parts[1] === 'before')),
                enter(workbook, 'New', 'B2', '7'),
            ];
        case 'copy': {
            const source = sheetNamed(parts[1]);
            const target = sheetNamed(parts[3]);
            return [workbook.do(source, SheetEdit.copySheet(at(target) + (parts[2] === 'after' ? 1 : 0)))];
        }
        case 'rename':
            return [workbook.do(sheetNamed(parts[1]), SheetEdit.rename('Mid'))];
        case undefined:
        default:
            throw new Error(id);
    }
}

describe('3D References against Excel (ADR-0331 §7)', () => {
    // Ticket 12's answers 1 and 6: what each function, and each form around one, answers.
    it.each(readings.formulas.map(c => [`${c.formula} (${c.setup}, ${c.cell})`, c] as const))('%s', (_, excel) => {
        const workbook = run(excel.setup === 'more');
        const cell = CellAddress.parse(excel.cell);
        if (notRead.includes(excel.formula)) {
            expect(() => workbook.do('Out', SheetEdit.enter(cell, excel.formula))).toThrow(FormulaSyntaxError);
            return;
        }
        workbook.do('Out', SheetEdit.enter(cell, excel.formula));
        const out = sheetOf(workbook, 'Out');
        expect(out.getEntry(cell)?.formula).toBe(excel.written);
        expectValue(out.getValue(cell), excel, excel.formula);
    });

    // Ticket 12's answers 2 and 3: the span as Sheets are moved, deleted, inserted and copied.
    it.each(readings.sheets.map(c => [c.id, c] as const))('%s', (id, excel) => {
        const workbook = run();
        enter(workbook, 'Out', 'A1', '=SUM(Sheet2:Sheet4!B2)');
        enter(workbook, 'Out', 'A2', '=SUM(Sheet4:Sheet2!B2)');
        enter(workbook, 'Out', 'A3', '=SUM(Sheet2:Sheet3!B2)');
        enter(workbook, 'Out', 'A4', '=SUM(Sheet3:Sheet3!B2)');
        const before = names(workbook);
        const steps = sheetOperation(workbook, id);
        expect(names(workbook)).toEqual(excel.order);
        for (const [cell, reading] of Object.entries(excel.read)) expectCell(workbook, cell, reading, id);
        // Undone, the Sheets and every Formula are as they were.
        for (const step of steps.toReversed()) step.undo();
        expect(names(workbook)).toEqual(before);
        const out = sheetOf(workbook, 'Out');
        expect(out.getEntry(CellAddress.parse('A1'))?.formula).toBe('=SUM(Sheet2:Sheet4!B2)');
        expect(out.getEntry(CellAddress.parse('A3'))?.formula).toBe('=SUM(Sheet2:Sheet3!B2)');
        expect(out.getValue(CellAddress.parse('A1'))?.number).toBe(1110);
    });

    // Ticket 12's answer 4: rows and columns inserted or deleted on a Sheet of a span never rewrite it.
    it.each(readings.rows.map(c => [c.id, c] as const))('%s', (id, excel) => {
        const workbook = run();
        enter(workbook, 'Out', 'A1', '=SUM(Sheet2:Sheet4!B2)');
        enter(workbook, 'Out', 'A2', '=SUM(Sheet2:Sheet4!B2:C3)');
        enter(workbook, 'Out', 'A3', '=SUM(Sheet2:Sheet4!$B$2)');
        enter(workbook, 'Sheet3', 'A1', '=SUM(Sheet2:Sheet4!B2)');
        enter(workbook, 'Sheet1', 'A1', '=SUM(Sheet2:Sheet4!B2)');
        enter(workbook, 'Out', 'A5', '=Sheet3!B2');
        const [where = '', verb, axis, at = ''] = id.split('-');
        // Cells shifted down or left under B2 alone: not a Sheet operation ExSheet has.
        if (axis === 'cells') return;
        const edit = (): SheetEdit => {
            if (axis === 'row') return verb === 'insert' ? SheetEdit.insertRows(Number(at) - 1) : SheetEdit.deleteRows(Number(at) - 1);
            if (axis === 'col') return verb === 'insert' ? SheetEdit.insertColumns(at.charCodeAt(0) - 65) : SheetEdit.deleteColumns(at.charCodeAt(0) - 65);
            throw new Error(id);
        };
        const targets = where === 'all' ? ['Sheet2', 'Sheet3', 'Sheet4'] : [where === 'out' ? 'Out' : `Sheet${where.slice(-1)}`];
        for (const name of targets) workbook.do(name, edit());
        for (const [cell, reading] of Object.entries(excel.read)) expectCell(workbook, cell, reading, id);
    });

    // Ticket 12's answers 5 and 12: a copy of a Sheet whose own Formulas hold a span, and its own name.
    it.each(readings.copies.map(c => [c.id, c] as const))('%s', (id, excel) => {
        const workbook = run();
        const parts = id.split('-');
        const source = `Sheet${String(parts[1])}`;
        enter(workbook, source, 'A1', '=SUM(Sheet2:Sheet4!B2)');
        enter(workbook, source, 'A2', `=${source}!B2`);
        enter(workbook, source, 'A3', '=Sheet1!B2');
        enter(workbook, source, 'A4', '=B2');
        enter(workbook, source, 'A5', `=SUM(${source}!B2:C3)`);
        enter(workbook, source, 'A6', `=SUM(${source}:Sheet5!B2)`);
        const at = (name: string): number => names(workbook).indexOf(name);
        let index: number;
        if (parts[2] === 'to') index = parts[3] === 'end' ? workbook.sheets.length : 0;
        else if (parts[2] === 'before') index = at(source);
        else index = at(source) + 1;
        if (parts[2] === 'after' && parts[3] !== parts[1]) index = at(`Sheet${String(parts[3])}`) + 1;
        workbook.do(source, SheetEdit.copySheet(index));
        expect(names(workbook)).toEqual(excel.order);
        for (const [cell, reading] of Object.entries(excel.read)) expectCell(workbook, cell, reading, id);
    });
});
