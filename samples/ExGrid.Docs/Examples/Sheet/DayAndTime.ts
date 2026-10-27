import { CellAddress, CellRange, Culture, NumberFormat, SheetEdit, Workbook, type WorkbookDocument } from '@ex-grid/sheet-engine';

/** The day, the time and the quarter's end: TODAY and NOW, as the Sheet Day and the zone give them. It holds the
 * Formulas only: no day and no time. */
export function dayAndTime(): WorkbookDocument {
    const sheet = 'Sheet1';
    const workbook = new Workbook(Culture.of('en-US'), [sheet]);
    const rows = [
        ['Today', '=TODAY()'],
        ['Now', '=NOW()'],
        ['Quarter end', '=EOMONTH(TODAY(), MOD(3 - MONTH(TODAY()), 3))'],
        ['Days left in the quarter', '=B3-B1'],
    ];
    rows.forEach((row, r) => {
        row.forEach((typed, c) => { workbook.do(sheet, SheetEdit.enter(new CellAddress(r, c), typed)); });
    });
    workbook.do(sheet, SheetEdit.setColumnWidth(CellRange.parse('A:A'), 24));
    workbook.do(sheet, SheetEdit.setColumnWidth(CellRange.parse('B:B'), 18));
    workbook.do(sheet, SheetEdit.setNumberFormat(CellRange.parse('B1'), NumberFormat.parse('yyyy-mm-dd')));
    workbook.do(sheet, SheetEdit.setNumberFormat(CellRange.parse('B2'), NumberFormat.parse('yyyy-mm-dd hh:mm')));
    workbook.do(sheet, SheetEdit.setNumberFormat(CellRange.parse('B3'), NumberFormat.parse('yyyy-mm-dd')));
    workbook.do(sheet, SheetEdit.setNumberFormat(CellRange.parse('B4'), NumberFormat.parse('0')));
    return workbook.toDocument();
}
