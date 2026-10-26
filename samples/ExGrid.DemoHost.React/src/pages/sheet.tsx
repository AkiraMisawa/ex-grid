import { useMemo, useState, type ReactElement } from 'react';
import { CellAddress, CellFormatChange, CellRange, Culture, NumberFormat, SheetEdit, Workbook, type WorkbookDocument } from '@ex-grid/sheet-engine';
import { ExSheet } from '@ex-grid/sheet-react';
import { DemoNav } from '../demo-nav';
import { useQuery } from '../router';

/** The page's own Sheet, the table SheetPage.razor opens with, so the specs written against that page read the
 * same cells here: a small order whose Amount column and total are Formulas. */
function seed(): WorkbookDocument {
    const workbook = new Workbook(Culture.of('en-US'));
    const rows: readonly (readonly string[])[] = [
        ['Item', 'Qty', 'Price', 'Amount'],
        ['Apples', '12', '0.5', '=B2*C2'],
        ['Pears', '7', '0.75', '=B3*C3'],
        ['Plums', '20', '0.2', '=B4*C4'],
        ['Total', '=SUM(B2:B4)', '', '=SUM(D2:D4)'],
    ];
    const typed = rows.flatMap((row, r) => row.flatMap((text, c) => (text === '' ? [] : [[new CellAddress(r, c), text] as const])));
    workbook.do('Sheet1', SheetEdit.enterMany(typed));
    workbook.do('Sheet1', SheetEdit.setCellFormat([CellRange.parse('A1:D1'), CellRange.parse('A5:D5')], new CellFormatChange({ bold: true })));
    workbook.do('Sheet1', SheetEdit.setCellFormat([CellRange.parse('C2:C5'), CellRange.parse('D2:D5')], new CellFormatChange({ numberFormat: NumberFormat.parse('#,##0.00') })));
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
                alignment and Percent on the Selection, as Excel&apos;s Home tab does (ADR-0100), and Ctrl+1 opens Format
                Cells (ADR-0071). The status line counts the cells of the Workbook Document the
                page receives after every change (ADR-0048).
            </p>
            <p className="demo-status"><span id="sheet-status">{status}</span></p>
            {/* Column A is pinned, as SheetPage.razor pins it: the Headings' specs read the pinned layer (ADR-0046). */}
            <ExSheet document={document} viewportWidth={900} viewportHeight={420} onWorkbookChanged={onWorkbookChanged}
                japanese={japanese} showToolbar pinnedColumnCount={1} />
        </>
    );
}
