import {
    BorderChange, BorderLine, CellAddress, CellColour, CellFill, CellFormatChange, CellRange, Culture, NumberFormat, SheetEdit, Workbook,
    type CellFormatChangeParts, type WorkbookDocument,
} from '@ex-grid/sheet-engine';

const navy = CellColour.fromRgb(0x1F3864);
const white = CellColour.fromRgb(0xFFFFFF);
const band = CellColour.fromRgb(0xDDEBF7);

/** A quarterly profit and loss statement, formatted as a finance team formats one. */
export function quarterlyPnl(): WorkbookDocument {
    const sheet = 'Sheet1';
    const workbook = new Workbook(Culture.of('en-US'), [sheet]);
    const rows = [
        ['USD thousands', 'Q1', 'Q2', 'Q3', 'Q4', 'FY'],
        ['Revenue', '4820', '5135', '4970', '5610', '=SUM(B2:E2)'],
        ['Cost of sales', '-2410', '-2520', '-2505', '-2690', '=SUM(B3:E3)'],
        ['Gross profit', '=B2+B3', '=C2+C3', '=D2+D3', '=E2+E3', '=F2+F3'],
        ['Gross margin', '=B4/B2', '=C4/C2', '=D4/D2', '=E4/E2', '=F4/F2'],
        ['Operating expenses', '-1630', '-1710', '-1695', '-1820', '=SUM(B6:E6)'],
        ['Operating profit', '=B4+B6', '=C4+C6', '=D4+D6', '=E4+E6', '=F4+F6'],
        ['Operating margin', '=B7/B2', '=C7/C2', '=D7/D2', '=E7/E2', '=F7/F2'],
    ];
    rows.forEach((row, r) => {
        row.forEach((typed, c) => {
            workbook.do(sheet, SheetEdit.enter(new CellAddress(r, c), typed));
        });
    });

    const format = (range: string, change: CellFormatChangeParts): void => {
        workbook.do(sheet, SheetEdit.setCellFormat([CellRange.parse(range)], new CellFormatChange(change)));
    };

    workbook.do(sheet, SheetEdit.setColumnWidth(CellRange.parse('A:A'), 22));
    workbook.do(sheet, SheetEdit.setColumnWidth(CellRange.parse('B:F'), 10));

    // Thousands with negatives in brackets, and margins as percentages.
    format('B2:F7', { numberFormat: NumberFormat.parse('#,##0;(#,##0)') });
    format('B5:F5', { numberFormat: NumberFormat.parse('0.0%'), italic: true });
    format('B8:F8', { numberFormat: NumberFormat.parse('0.0%'), italic: true });
    format('A5', { italic: true });
    format('A8', { italic: true });

    // A navy heading row, the totals in bold over a thin line, and the result underlined twice.
    format('A1:F1', { bold: true, fontColour: white, fill: CellFill.solid(navy) });
    format('B1:F1', { alignment: 'center' });
    format('A4:F4', { bold: true, borders: new BorderChange({ top: new BorderLine('thin') }) });
    format('A7:F7', {
        bold: true,
        fill: CellFill.solid(band),
        borders: new BorderChange({ top: new BorderLine('thin'), bottom: new BorderLine('double') }),
    });
    format('F1:F8', { bold: true });
    return workbook.toDocument();
}
