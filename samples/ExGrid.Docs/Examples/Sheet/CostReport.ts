import { CellAddress, CellFormatChange, CellRange, Culture, NumberFormat, SheetEdit, Workbook, type WorkbookDocument } from '@ex-grid/sheet-engine';
import { costCentreColumns, costCentreKey, costCentresTable } from './CostCentre';

// One cost centre's line, read from the table by its code.
const line = (code: string, row: number): readonly string[] => [
    code,
    `=XLOOKUP(A${String(row)}, CostCentres[Code], CostCentres[Budget])`,
    `=XLOOKUP(A${String(row)}, CostCentres[Code], CostCentres[Actual])`,
    `=B${String(row)}-C${String(row)}`,
    `=D${String(row)}/B${String(row)}`,
];

/** A variance report that reads the ledger's cost centres as a Linked Table. It records the table's declaration, never
 * its rows. */
export function costReport(): WorkbookDocument {
    const sheet = 'Sheet1';
    const workbook = new Workbook(Culture.of('en-US'), [sheet]);
    workbook.declareLinkedTable(costCentresTable, costCentreColumns, costCentreKey);
    const rows: readonly (readonly string[])[] = [
        ['Cost centre', 'Budget', 'Actual', 'Remaining', 'Remaining %'],
        ...['CC-1100', 'CC-2300', 'CC-3100'].map((code, i) => line(code, i + 2)),
        [],
        ['All cost centres', '=SUM(CostCentres[Budget])', '=SUM(CostCentres[Actual])', '=B6-C6', '=D6/B6'],
        ['Centres reporting', '=COUNTA(CostCentres[Code])'],
    ];
    rows.forEach((row, r) => {
        row.forEach((typed, c) => {
            workbook.do(sheet, SheetEdit.enter(new CellAddress(r, c), typed));
        });
    });
    workbook.do(sheet, SheetEdit.setColumnWidth(CellRange.parse('A:A'), 18));
    workbook.do(sheet, SheetEdit.setColumnWidth(CellRange.parse('B:E'), 12));
    workbook.do(sheet, SheetEdit.setNumberFormat(CellRange.parse('B2:D6'), NumberFormat.parse('#,##0;[Red](#,##0)')));
    workbook.do(sheet, SheetEdit.setNumberFormat(CellRange.parse('E2:E6'), NumberFormat.parse('0.0%;[Red]-0.0%')));
    workbook.do(sheet, SheetEdit.setCellFormat([CellRange.parse('A1:E1'), CellRange.parse('A6:E6')], new CellFormatChange({ bold: true })));
    return workbook.toDocument();
}
