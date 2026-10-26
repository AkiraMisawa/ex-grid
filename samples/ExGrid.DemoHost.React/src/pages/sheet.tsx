import { useEffect, useMemo, useRef, useState, type ReactElement } from 'react';
import { CellAddress, Culture, SheetEdit, Value, Workbook, type WorkbookDocument } from '@ex-grid/sheet-engine';
import { ExSheet, type ExSheetApi } from '@ex-grid/sheet-react';
import { DemoNav } from '../demo-nav';
import { useQuery } from '../router';

// The Linked Table SheetPage.razor declares (ADR-0049): its rows are the page's, pushed into the Sheet, never in the
// document. A row is read by its Id, and no Id appears twice in a snapshot.
const table = 'Positions';
const tableColumns = ['Id', 'Book', 'PV'] as const;
const tableKey = 'Id';
const positions: readonly (readonly [string, string, number])[] = [
    ['R-1102', 'Rates', 1250],
    ['R-2210', 'Rates', -420.5],
    ['R-4471', 'FX', 318.25],
    ['R-5093', 'Credit', 97.4],
    ['R-6120', 'FX', -55.75],
];

/** The page's own Sheet, the one SheetPage.razor opens with, so the specs written against that page read the same
 * cells here: a few values, Formulas over them, a date, and Formulas that read the Linked Table. */
function seed(): WorkbookDocument {
    const workbook = new Workbook(Culture.of('en-US'));
    const cells: readonly (readonly [string, string])[] = [
        ['A1', 'Item'], ['B1', 'Qty'], ['C1', 'Price'], ['D1', 'Amount'],
        ['A2', 'Apples'], ['B2', '12'], ['C2', '0.5'], ['D2', '=B2*C2'],
        ['A3', 'Pears'], ['B3', '7'], ['C3', '0.75'], ['D3', '=B3*C3'],
        ['A4', 'Plums'], ['B4', '20'], ['C4', '0.2'], ['D4', '=B4*C4'],
        ['A5', 'Total'], ['B5', '=SUM(B2:B4)'], ['D5', '=SUM(D2:D4)'],
        ['A7', 'As of'], ['B7', '9/26/2026'],
        ['A8', 'Due'], ['B8', '=B7+30'],
        ['A10', 'Positions'],
        ['A11', 'Total PV'], ['B11', `=SUM(${table}[PV])`],
        ['A12', 'PV of R-4471'], ['B12', `=XLOOKUP("R-4471", ${table}[Id], ${table}[PV])`],
        ['A13', 'Count'], ['B13', `=COUNTA(${table}[Id])`],
    ];
    // The declaration travels in the document; the rows never do (ADR-0049).
    workbook.declareLinkedTable(table, tableColumns, tableKey);
    workbook.do('Sheet1', SheetEdit.enterMany(cells.map(([address, typed]) => [CellAddress.parse(address), typed] as const)));
    return workbook.toDocument();
}

/** The page /sheet of the Blazor hosts, as far as the Sheet's Script Core draws it: Formulas, editing, paste,
 * Delete, the fill keys and undo, with ?lang=ja for the Sheet's Japanese (ADR-0370 section 10). */
export function SheetDemo(): ReactElement {
    const document = useMemo(seed, []);
    const japanese = useQuery('lang') === 'ja';
    const [status, setStatus] = useState('The Sheet as opened.');
    const sheet = useRef<ExSheetApi>(null);
    // The Linked Table's first snapshot, 1.5 s after the Sheet opens, as SheetPage.razor pushes it: long enough to
    // see #GETTING_DATA while the data is "on its way".
    useEffect(() => {
        const pushed = setTimeout(() => {
            // The timer is cleared as the page unmounts, so the Sheet stands here: a ref not given it is a defect.
            const api = sheet.current;
            if (api === null) throw new Error('The Sheet was not given to its ref, so its Linked Table cannot be pushed.');
            api.pushLinkedTable(table, positions.map(([id, book, pv]) => [Value.fromText(id), Value.fromText(book), Value.fromNumber(pv)]));
        }, 1500);
        return () => { clearTimeout(pushed); };
    }, []);
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
                Cells (ADR-0071). B11 to B13 read the page&apos;s Linked Table, Positions, which shows #GETTING_DATA
                until the page pushes its rows 1.5 s after the Sheet opens (ADR-0049). The status line counts the
                cells of the Workbook Document the page receives after every change (ADR-0048).
            </p>
            <p className="demo-status"><span id="sheet-status">{status}</span></p>
            {/* Column A is pinned, as SheetPage.razor pins it: the Headings' specs read the pinned layer (ADR-0046). */}
            <ExSheet ref={sheet} document={document} viewportWidth={720} viewportHeight={420} onWorkbookChanged={onWorkbookChanged}
                japanese={japanese} showToolbar pinnedColumnCount={1} />
        </>
    );
}
