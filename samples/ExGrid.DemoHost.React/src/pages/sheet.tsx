import { useEffect, useMemo, useRef, useState, type ReactElement } from 'react';
import { formulaBarHeight, type GridSelection } from '@ex-grid/core';
import { ExGrid } from '@ex-grid/react';
import { PointingScope, type PointingRefusal } from '@ex-grid/sheet';
import {
    CellAddress, CellColour, CellFill, CellFormatChange, SheetEdit, SheetRefusedError, Workbook, type SheetDay, type WorkbookDocument,
} from '@ex-grid/sheet-engine';
import { ExSheet, type ExSheetApi, type ExSheetToolbarRow } from '@ex-grid/sheet-react';
import { DemoNav } from '../demo-nav';
import { useQuery } from '../router';
import { enUs, sheetCase } from '../sheet-cases';
import { positionColumns, positionRow, positionsTable, type Position } from '../sheet-positions';

// The Linked Table SheetPage.razor declares (ADR-0049): its rows are the page's, pushed into the Sheet, never in the
// document. A row is read by its Id, and no Id appears twice in a snapshot.
const table = positionsTable;
const tableColumns = ['Id', 'Book', 'PV'] as const;
const tableKey = 'Id';

const openingPositions: readonly Position[] = [
    { id: 'R-1102', book: 'Rates', pv: 1250 },
    { id: 'R-2210', book: 'Rates', pv: -420.5 },
    { id: 'R-4471', book: 'FX', pv: 318.25 },
    { id: 'R-5093', book: 'Credit', pv: 97.4 },
    { id: 'R-6120', book: 'FX', pv: -55.75 },
];

/** The page's own Sheet, the one SheetPage.razor opens with, so the specs written against that page read the same
 * cells here: a few values, Formulas over them, a date, and Formulas that read the Linked Table. */
function opening(): WorkbookDocument {
    const workbook = new Workbook(enUs);
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
    for (const [address, typed] of cells) workbook.do('Sheet1', SheetEdit.enter(CellAddress.parse(address), typed));
    return workbook.toDocument();
}

/** `?today=` as a Sheet Day: yyyy-MM-dd, refused by name otherwise. */
function dayOf(text: string): SheetDay {
    const match = /^(\d{4})-(\d{2})-(\d{2})$/u.exec(text);
    if (match === null) throw new RangeError(`?today= is yyyy-MM-dd, not "${text}".`);
    return { year: Number(match[1]), month: Number(match[2]), day: Number(match[3]) };
}

// The Sheet's height includes its toolbar (ADR-0100) and its Sheet Tabs (ADR-0331): the page adds the toolbar's two
// rows and the tabs' strip, each the Formula Bar's height, so that the grid keeps the rows it showed before either
// came, as SheetPage.razor does.
const bandPx = formulaBarHeight('compact');

/** The Focus cell's emphases as the page's line reads them back. */
function fontOf(format: ReturnType<ExSheetApi['cellFormatAt']>): string {
    const { font } = format;
    const emphases = [
        ...(font.bold ? ['bold'] : []), ...(font.italic ? ['italic'] : []),
        ...(font.underline ? ['underline'] : []), ...(font.strikethrough ? ['strikethrough'] : []),
    ];
    return emphases.length === 0 ? 'regular' : emphases.join(' ');
}

/** The page /sheet of the Blazor hosts, SheetPage.razor: its Sheet, its buttons, its status lines and the positions
 * grid beside it in one Pointing Scope, with its queries — ?case=, ?zone=, ?today=, ?pin=, ?toolbar=, ?box=window,
 * ?scheme=dark and ?scope=switch — and ?lang=ja for the Sheet's Japanese (ADR-0370 section 10). */
export function SheetDemo(): ReactElement {
    const caseName = useQuery('case');
    const zone = useQuery('zone');
    const fixedDay = useQuery('today');
    const pin = useQuery('pin');
    const toolbarAsked = useQuery('toolbar');
    const inWindowBox = useQuery('box') === 'window';
    const dark = useQuery('scheme') === 'dark';
    const scopeSwitch = useQuery('scope') === 'switch';
    const japanese = useQuery('lang') === 'ja';
    const document = useMemo(() => (caseName === undefined ? opening() : sheetCase(caseName, enUs)), [caseName]);
    const today = useMemo(() => (fixedDay === undefined ? undefined : dayOf(fixedDay)), [fixedDay]);
    // The page's own Sheet pins column A; a run's case does not, as Excel's workbook in the run had no frozen panes.
    const pinned = pin === undefined ? (caseName === undefined ? 1 : 0) : Number(pin);
    // A run's case is drawn as Excel drew it, without the toolbar, unless ?toolbar=on asks.
    const [toolbarShown, setToolbarShown] = useState(toolbarAsked !== 'off' && (caseName === undefined || toolbarAsked === 'on'));
    const [inScope, setInScope] = useState(true);
    const [editing, setEditing] = useState(false);
    const [status, setStatus] = useState('');
    const [pointing, setPointing] = useState('');
    const [focusFont, setFocusFont] = useState('');
    const [focusNumberFormat, setFocusNumberFormat] = useState('');
    const [positions, setPositions] = useState(openingPositions);
    const sheet = useRef<ExSheetApi>(null);
    const selection = useRef<GridSelection | undefined>(undefined);
    // Made together, so that a memo React makes twice and drops leaves no grid registered in the Scope it keeps.
    const [scope, pointedAt] = useMemo(() => {
        const made = new PointingScope();
        return [made, made.registerGrid<Position>(table, positionRow)] as const;
    }, []);

    const api = (): ExSheetApi => {
        const current = sheet.current;
        if (current === null) throw new Error('The Sheet is not mounted.');
        return current;
    };

    const readFocus = (): void => {
        const focus = selection.current?.focus;
        if (sheet.current === null || focus === undefined) {
            setFocusFont('');
            setFocusNumberFormat('');
            return;
        }
        const format = sheet.current.cellFormatAt(new CellAddress(focus.row, focus.column));
        setFocusFont(fontOf(format));
        setFocusNumberFormat(format.numberFormat.code);
    };

    const count = (changed: WorkbookDocument): void => {
        const cells = changed.sheets.reduce((sum, each) => sum + each.cells.length, 0);
        setStatus(`${String(cells)} cells in the Sheet Document.`);
        readFocus();
    };

    useEffect(() => { count(document); }, [document]);

    // The Linked Table's first snapshot, 1.5 s after the Sheet opens, as SheetPage.razor pushes it: long enough to see
    // #GETTING_DATA while the data is "on its way".
    const pushed = useRef(false);
    useEffect(() => {
        // Declaring what the document already declares changes nothing; a case opened without it declares it here.
        api().declareLinkedTable(table, tableColumns, tableKey);
        const timer = setTimeout(() => {
            pushed.current = true;
            api().pushLinkedTable(table, positions.map(positionRow));
        }, 1500);
        return () => { clearTimeout(timer); };
    }, []);
    useEffect(() => {
        if (pushed.current) api().pushLinkedTable(table, positions.map(positionRow));
    }, [positions]);

    // A command ExSheet refuses changes nothing and says why (ADR-0048): an insertion that would push an Entry off the
    // Sheet, or any command while an edit is open.
    const command = (run: (handle: ExSheetApi) => void): void => {
        try {
            run(api());
            count(api().workbook.toDocument());
        } catch (error) {
            if (!(error instanceof SheetRefusedError)) throw error;
            setStatus(error.message);
        }
    };

    // The page's own row of the Sheet Toolbar (ADR-0100), its action Excel's light green under the Selection.
    const toolbar = useMemo((): readonly (ExSheetToolbarRow | 'default')[] => [
        'default',
        {
            label: 'This page', keyTip: 'Y',
            items: [{
                text: 'Mark reviewed', keyTip: 'R',
                onPress: () => { command(handle => { handle.setCellFormat(new CellFormatChange({ fill: CellFill.solid(CellColour.fromRgb(0xE2EFDA)) })); }); },
            }],
        },
    ], []);

    const revalue = (): void => {
        setPositions(positions.map(p => ({ ...p, pv: Math.round(p.pv * 1.01 * 100) / 100 })));
    };

    const sheetHeight = 420 + bandPx + (toolbarShown ? 2 * bandPx : 0);
    return (
        <>
            {dark ? <style>{':root { color-scheme: dark; }'}</style> : null}
            <DemoNav route="sheet" />
            <h1>Sheet — ExSheet drawn by ExGrid</h1>
            <p>
                ExSheet holds a Sheet of Entries and computes their Values; one ExGrid paints it, as that grid&apos;s
                Consumer (ADR-0046). Type into a cell, press F2 on a Formula to edit it, read the Entry in the Formula
                Bar, type an address such as <code>D200</code> into the Name Box, and use Ctrl+arrow to jump to the end of
                a block. The status line counts the cells of the Sheet Document the page receives after every change
                (ADR-0048).
            </p>
            <p>
                The grid on the right shows this page&apos;s positions, and the Sheet reads the same data as the Linked
                Table <code>Positions</code> (ADR-0049): by key in B12, by column in B11. The page pushes its first
                snapshot a moment after the Sheet opens, so those cells show <code>#GETTING_DATA</code> until it lands;
                <i> Revalue</i> changes the data both show. While <code>=SUM(Positions[PV])</code> is edited, the PV column
                of the grid on the right is outlined in the colour its Reference wears (ADR-0057, ADR-0058).
            </p>
            <div className="demo-toolbar">
                <button id="sheet-undo" type="button" disabled={editing} onClick={() => { command(handle => { handle.undo(); }); }}>Undo</button>
                <button id="sheet-redo" type="button" disabled={editing} onClick={() => { command(handle => { handle.redo(); }); }}>Redo</button>
                <button id="sheet-format-cells" type="button" disabled={editing} onClick={() => { command(handle => { handle.openFormatCells(); }); }}>Format Cells…</button>
                <button id="sheet-insert-row" type="button" disabled={editing} onClick={() => { command(handle => { handle.do(SheetEdit.insertRows(1)); }); }}>Insert a row above row 2</button>
                <button id="sheet-revalue" type="button" onClick={revalue}>Revalue the positions by +1%</button>
                {/* ?scope=switch: a button that takes the Sheet out of its Pointing Scope and gives it back, leaving the
                    keyboard where it is (its press takes no focus) (ADR-0240, ADR-0058). */}
                {scopeSwitch
                    ? (
                        <button id="sheet-scope-switch" type="button" onMouseDown={event => { event.preventDefault(); }} onClick={() => { setInScope(!inScope); }}>
                            {inScope ? 'Leave the Pointing Scope' : 'Join the Pointing Scope'}
                        </button>
                    )
                    : null}
            </div>
            <p className="demo-status"><span id="sheet-status">{status}</span></p>
            {/* ?box=window gives the Sheet a box that follows the window's height, the window less 332 px, so a
                popover's room changes with the window (DC-60, ADR-0040). */}
            <div className="demo-side-by-side" style={inWindowBox ? { height: 'calc(100vh - 332px)', minHeight: 0 } : undefined}>
                <ExSheet ref={sheet} document={document} viewportWidth={720} viewportHeight={inWindowBox ? 'stretch' : sheetHeight}
                    pinnedColumnCount={pinned} japanese={japanese} showToolbar={toolbarShown} onShowToolbarChanged={setToolbarShown}
                    toolbar={toolbar} timeZone={zone} today={today}
                    pointingScope={inScope ? scope : undefined} onPointingRefused={(refusal: PointingRefusal) => { setPointing(refusal.message); }}
                    onWorkbookChanged={count} onFormatKeyRefused={refusal => { setStatus(refusal.message); }}
                    onEditingChanged={open => { setEditing(open); setPointing(''); }}
                    onSelectionChanged={changed => { selection.current = changed; readFocus(); }} />
                <div id="sheet-positions">
                    <ExGrid rows={positions} columns={positionColumns} rowHeight={28} viewportHeight={420} viewportWidth={330} pointedAt={pointedAt} />
                </div>
            </div>
            {/* Why a press on the positions grid wrote nothing, as the Sheet tells the page (ADR-0058). */}
            <p className="demo-status"><span id="sheet-pointing">{pointing}</span></p>
            <p className="demo-status">
                Focus cell: <span id="sheet-focus-font">{focusFont}</span>,{' '}
                <span id="sheet-focus-number-format">{focusNumberFormat}</span>
            </p>
            <p>
                The Sheet Toolbar above the Formula Bar is ExSheet&apos;s own (ADR-0100): its first row is Excel&apos;s
                Home tab&apos;s formatting commands, and its second row is this page&apos;s, with an action of its own.
                Ctrl+F1 hides and shows it, and the page holds whether it is shown.
            </p>
        </>
    );
}
