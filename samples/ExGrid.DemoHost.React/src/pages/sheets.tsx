import { useEffect, useMemo, useRef, useState, type ReactElement } from 'react';
import { ExGrid } from '@ex-grid/react';
import { PointingScope, type PointingRefusal } from '@ex-grid/sheet';
import { CellAddress, SheetEdit, Workbook, type WorkbookDocument } from '@ex-grid/sheet-engine';
import { ExSheet, type ExSheetApi } from '@ex-grid/sheet-react';
import { DemoNav } from '../demo-nav';
import { enUs } from '../sheet-cases';
import { positionColumns, positionRow, positionsTable, type Position } from '../sheet-positions';

const positions: readonly Position[] = [
    { id: 'R-1102', book: 'Rates', pv: 1250 },
    { id: 'R-4471', book: 'FX', pv: 318.25 },
    { id: 'R-5093', book: 'Credit', pv: 97.4 },
];

// Each Sheet names itself in A1 and holds a number and a Formula over it, so a key that crossed from one to the other
// would show in the other's cells.
function opening(name: string, value: number): WorkbookDocument {
    const workbook = new Workbook(enUs);
    workbook.do('Sheet1', SheetEdit.enter(CellAddress.parse('A1'), name));
    workbook.do('Sheet1', SheetEdit.enter(CellAddress.parse('B1'), String(value)));
    workbook.do('Sheet1', SheetEdit.enter(CellAddress.parse('C1'), '=B1*2'));
    // The declaration travels in the document; the rows never do (ADR-0049).
    workbook.declareLinkedTable(positionsTable, ['Id', 'Book', 'PV'], 'Id');
    return workbook.toDocument();
}

const cellsOf = (document: WorkbookDocument): number => document.sheets.reduce((sum, sheet) => sum + sheet.cells.length, 0);

/** One side of the page: a Sheet and the grid beneath it, a Pointing Scope of their own (ADR-0058). */
function Side({ side, document, onCount, onRefused }: {
    readonly side: 'left' | 'right';
    readonly document: WorkbookDocument;
    readonly onCount: (cells: number) => void;
    readonly onRefused: (refusal: PointingRefusal) => void;
}): ReactElement {
    const sheet = useRef<ExSheetApi>(null);
    // Made together, so that a memo React makes twice and drops leaves no grid registered in the Scope it keeps.
    const [scope, pointedAt] = useMemo(() => {
        const made = new PointingScope();
        return [made, made.registerGrid<Position>(positionsTable, positionRow)] as const;
    }, []);
    useEffect(() => {
        const api = sheet.current;
        if (api === null) throw new Error('The Sheet was not given to its ref, so its Linked Table cannot be pushed.');
        api.pushLinkedTable(positionsTable, positions.map(positionRow));
    }, []);
    return (
        <div id={`sheet-${side}`}>
            <ExSheet ref={sheet} document={document} viewportHeight={360} viewportWidth={520} pointingScope={scope} onPointingRefused={onRefused}
                onWorkbookChanged={changed => { onCount(cellsOf(changed)); }} />
            <div id={`sheet-${side}-positions`}>
                <ExGrid rows={positions} columns={positionColumns} rowHeight={28} viewportHeight={200} viewportWidth={330} pointedAt={pointedAt} />
            </div>
        </div>
    );
}

/** The page /sheets of the Blazor hosts, SheetsPage.razor: two ExSheets on one page, each with its own Sheet, undo
 * stack, Formula Bar and Pointing Scope (ADR-0018, ADR-0048, ADR-0058). */
export function SheetsDemo(): ReactElement {
    const left = useMemo(() => opening('Left', 10), []);
    const right = useMemo(() => opening('Right', 20), []);
    const [leftCells, setLeftCells] = useState(cellsOf(left));
    const [rightCells, setRightCells] = useState(cellsOf(right));
    const [pointing, setPointing] = useState('');
    return (
        <>
            <DemoNav route="sheets" />
            <h1>Two sheets — independent instances</h1>
            <p>
                Two ExSheets on one page, each with its own Sheet, its own undo stack and its own Formula Bar (ADR-0018,
                ADR-0048). Keys typed into one never reach the other, a completion list opens in the one being typed in,
                and Ctrl+Z undoes only the Sheet that has the keyboard.
            </p>
            <p>
                Both Sheets read the page&apos;s positions as the Linked Table <code>Positions</code> (ADR-0049), and
                beneath each is a grid showing them. Each Sheet and the grid beneath it are a Pointing Scope of their own
                (ADR-0058): <code>=SUM(Positions[PV])</code> typed into one outlines the PV column under that one only.
            </p>
            <div className="demo-side-by-side">
                <Side side="left" document={left} onCount={setLeftCells} onRefused={refusal => { setPointing(`Left: ${refusal.message}`); }} />
                <Side side="right" document={right} onCount={setRightCells} onRefused={refusal => { setPointing(`Right: ${refusal.message}`); }} />
            </div>
            <p className="demo-status"><span id="sheets-status">{`Left: ${String(leftCells)} cells; right: ${String(rightCells)} cells.`}</span></p>
            <p className="demo-status"><span id="sheets-pointing">{pointing}</span></p>
        </>
    );
}
