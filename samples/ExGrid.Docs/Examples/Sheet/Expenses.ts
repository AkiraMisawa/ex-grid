import { CellAddress, CellFormatChange, CellRange, Culture, NumberFormat, SheetEdit, Workbook, type WorkbookDocument } from '@ex-grid/sheet-engine';

/** A quarter's travel and expenses claim: what the Examples open on. Entries only: the Values are computed when the
 * Sheet opens. */
export function expenses(): WorkbookDocument {
    const sheet = 'Sheet1';
    const workbook = new Workbook(Culture.of('en-US'), [sheet]);
    const rows = [
        ['Item', 'Qty', 'Unit cost', 'Amount'],
        ['Flights LHR-JFK', '2', '1240', '=B2*C2'],
        ['Hotel, nights', '6', '289.5', '=B3*C3'],
        ['Client dinner', '1', '412.8', '=B4*C4'],
        ['Taxis', '9', '38.25', '=B5*C5'],
        ['Conference pass', '1', '1650', '=B6*C6'],
        ['Total', '', '', '=SUM(D2:D6)'],
    ];
    rows.forEach((row, r) => {
        row.forEach((typed, c) => {
            if (typed !== '') workbook.do(sheet, SheetEdit.enter(new CellAddress(r, c), typed));
        });
    });

    workbook.do(sheet, SheetEdit.setColumnWidth(CellRange.parse('A:A'), 18));
    workbook.do(sheet, SheetEdit.setNumberFormat(CellRange.parse('C2:D7'), NumberFormat.parse('#,##0.00')));
    workbook.do(sheet, SheetEdit.setCellFormat([CellRange.parse('A1:D1'), CellRange.parse('A7:D7')], new CellFormatChange({ bold: true })));
    return workbook.toDocument();
}
