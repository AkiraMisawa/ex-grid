import { useMemo, useState, type ReactElement } from 'react';
import { CellAddress, CellFormatChange, CellRange, Culture, NumberFormat, SheetEdit, Workbook, type WorkbookDocument } from '@ex-grid/sheet-engine';
import { ExSheet } from '@ex-grid/sheet-react';
import { DemoNav } from '../demo-nav';
import { useQuery } from '../router';

/** The page's own Sheet: a small book of positions whose totals are Formulas. */
function seed(): WorkbookDocument {
    const workbook = new Workbook(Culture.of('en-US'));
    const rows: readonly (readonly string[])[] = [
        ['Book', 'Notional', 'Rate', 'Accrued'],
        ['Rates', '1250000', '0.0325', '=B2*C2/4'],
        ['Credit', '830000', '0.0475', '=B3*C3/4'],
        ['FX', '2400000', '0.0110', '=B4*C4/4'],
        ['Total', '=SUM(B2:B4)', '=D5/B5*4', '=SUM(D2:D4)'],
    ];
    const typed = rows.flatMap((row, r) => row.map((text, c) => [new CellAddress(r, c), text] as const));
    workbook.do('Sheet1', SheetEdit.enterMany(typed));
    workbook.do('Sheet1', SheetEdit.setCellFormat([CellRange.parse('A1:D1'), CellRange.parse('A5:D5')], new CellFormatChange({ bold: true })));
    workbook.do('Sheet1', SheetEdit.setCellFormat([CellRange.parse('B2:B5'), CellRange.parse('D2:D5')], new CellFormatChange({ numberFormat: NumberFormat.parse('#,##0.00') })));
    workbook.do('Sheet1', SheetEdit.setCellFormat([CellRange.parse('C2:C5')], new CellFormatChange({ numberFormat: NumberFormat.parse('0.00%') })));
    // Notional's amounts are wider than Excel's default column, as a typed number would have widened it to.
    workbook.do('Sheet1', SheetEdit.setAutomaticColumnWidth(CellRange.wholeColumns(1, 1), 13));
    return workbook.toDocument();
}

/** The page /sheet of the Blazor hosts, as far as the Sheet's Script Core draws it: Formulas, editing, paste,
 * Delete, the fill keys and undo, with ?lang=ja for the Sheet's Japanese (ADR-0370 section 10). */
export function SheetDemo(): ReactElement {
    const document = useMemo(seed, []);
    const japanese = useQuery('lang') === 'ja';
    const [status, setStatus] = useState('The Sheet as opened.');
    const onWorkbookChanged = useMemo(() => (changed: WorkbookDocument): void => {
        const cells = changed.sheets.reduce((sum, sheet) => sum + sheet.cells.length, 0);
        setStatus(`The Workbook Document holds ${String(cells)} cells.`);
    }, []);
    return (
        <>
            <DemoNav route="sheet" />
            <h1>Sheet — ExSheet drawn by ExGrid</h1>
            <p>
                ExSheet holds a Sheet of Entries and computes their Values; one ExGrid paints it, as that grid&apos;s
                Consumer (ADR-0046). Type into a cell, press F2 on a Formula to edit it, paste from Excel, press Delete,
                Ctrl+D or Ctrl+Enter, and Ctrl+Z to undo. The Sheet Toolbar above the Formula Bar sets Bold, the
                alignment and Percent on the Selection, as Excel&apos;s Home tab does (ADR-0100). The status line counts the cells of the Workbook Document the
                page receives after every change (ADR-0048).
            </p>
            <p className="demo-status"><span id="sheet-status">{status}</span></p>
            <ExSheet document={document} viewportWidth={900} viewportHeight={420} onWorkbookChanged={onWorkbookChanged}
                japanese={japanese} showToolbar />
        </>
    );
}
