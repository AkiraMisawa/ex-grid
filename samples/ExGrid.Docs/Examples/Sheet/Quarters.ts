import { CellAddress, CellRange, Culture, NumberFormat, SheetEdit, Workbook, type WorkbookDocument } from '@ex-grid/sheet-engine';

/** A quarter of three months and a summary that adds them with 3D References: Jan, Feb and Mar with the same
 * layout, Year reading across them, and Notes outside the span. */
export function quarters(): WorkbookDocument {
    const workbook = new Workbook(Culture.of('en-US'), ['Jan', 'Feb', 'Mar', 'Notes', 'Year']);
    const enter = (sheet: string, address: string, typed: string): void => {
        workbook.do(sheet, SheetEdit.enter(CellAddress.parse(address), typed));
    };
    const months = [['Jan', '48200', '31950'], ['Feb', '51730', '33410'], ['Mar', '56980', '34120']] as const;
    for (const [month, revenue, costs] of months) {
        enter(month, 'A1', 'Revenue');
        enter(month, 'B1', revenue);
        enter(month, 'A2', 'Costs');
        enter(month, 'B2', costs);
        workbook.do(month, SheetEdit.setColumnWidth(CellRange.parse('A:A'), 14));
        workbook.do(month, SheetEdit.setNumberFormat(CellRange.parse('B1:B2'), NumberFormat.parse('#,##0')));
    }
    enter('Notes', 'A1', 'Outside the span: drag it between Jan and Mar and Year reads it too.');

    // Each reads B1 or B2 of every Sheet from Jan to Mar, in tab order.
    const rows = [
        ['Revenue', '=SUM(Jan:Mar!B1)'],
        ['Costs', '=SUM(Jan:Mar!B2)'],
        ['Margin', '=B1-B2'],
        ['Best month', '=MAX(Jan:Mar!B1)'],
        ['Average costs', '=AVERAGE(Jan:Mar!B2)'],
    ];
    rows.forEach((row, r) => {
        row.forEach((typed, c) => {
            workbook.do('Year', SheetEdit.enter(new CellAddress(r, c), typed));
        });
    });
    workbook.do('Year', SheetEdit.setColumnWidth(CellRange.parse('A:A'), 14));
    workbook.do('Year', SheetEdit.setNumberFormat(CellRange.parse('B1:B5'), NumberFormat.parse('#,##0')));
    const year = workbook.find('Year');
    if (year === undefined) throw new Error('The Workbook was made with a Sheet named Year.');
    workbook.shown = year;
    return workbook.toDocument();
}
