import { CellAddress, CellFormatChange, CellRange, Culture, NumberFormat, SheetEdit, Workbook, type WorkbookDocument } from '@ex-grid/sheet-engine';

/** Quarterly sales by region, with a Formula of every supported kind and the answers it refuses to approximate. */
export function regionalSales(): WorkbookDocument {
    const sheet = 'Sheet1';
    const workbook = new Workbook(Culture.of('en-US'), [sheet]);
    const rows = [
        ['Region', 'Q1', 'Q2', 'Q3', 'Q4', 'Total', 'Average', 'Best quarter', 'Status'],
        ['North', '182400', '195300', '201750', '188900', '=SUM(B2:E2)', '=AVERAGE(B2:E2)', '=MAX(B2:E2)', '=IF(F2>=750000, "On target", "Below")'],
        ['South', '143800', '151200', '139650', '160400', '=SUM(B3:E3)', '=AVERAGE(B3:E3)', '=MAX(B3:E3)', '=IF(F3>=750000, "On target", "Below")'],
        ['West', '211300', '198750', '224600', '236100', '=SUM(B4:E4)', '=AVERAGE(B4:E4)', '=MAX(B4:E4)', '=IF(F4>=750000, "On target", "Below")'],
        ['Total', '=SUM(B2:B4)', '=SUM(C2:C4)', '=SUM(D2:D4)', '=SUM(E2:E4)', '=SUM(F2:F4)'],
        [],
        ['Look up a region', 'South', '', 'Refused, not approximated'],
        ['Its total', '=XLOOKUP(B7, A2:A4, F2:F4, "Not found")', '', 'Unsupported', '=IRR(B2:E2)'],
        ['Share of all sales', '=ROUND(B8/F5, 3)', '', 'Circular', '=E9+1'],
        ['Regions reporting', '=COUNTA(A2:A4)', '', 'Divide by zero', '=B2/0'],
        ['', '', '', 'Caught', '=IFERROR(E10, 0)'],
    ];
    rows.forEach((row, r) => {
        row.forEach((typed, c) => {
            if (typed !== '') workbook.do(sheet, SheetEdit.enter(new CellAddress(r, c), typed));
        });
    });

    workbook.do(sheet, SheetEdit.setColumnWidth(CellRange.parse('A:A'), 22));
    workbook.do(sheet, SheetEdit.setColumnWidth(CellRange.parse('B:I'), 11));
    workbook.do(sheet, SheetEdit.setNumberFormat(CellRange.parse('B2:H5'), NumberFormat.parse('#,##0')));
    workbook.do(sheet, SheetEdit.setNumberFormat(CellRange.parse('B8'), NumberFormat.parse('#,##0')));
    workbook.do(sheet, SheetEdit.setNumberFormat(CellRange.parse('B9'), NumberFormat.parse('0.0%')));
    workbook.do(sheet, SheetEdit.setAlignment(CellRange.parse('B1:H1'), 'right'));
    workbook.do(sheet, SheetEdit.setCellFormat([CellRange.parse('A1:I1'), CellRange.parse('A5:F5'), CellRange.parse('D7')], new CellFormatChange({ bold: true })));
    return workbook.toDocument();
}
