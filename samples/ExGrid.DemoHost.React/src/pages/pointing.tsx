import { useEffect, useMemo, useRef, useState, type ReactElement } from 'react';
import { ExGrid } from '@ex-grid/react';
import { PointingScope } from '@ex-grid/sheet';
import { CellAddress, SheetEdit, Value, Workbook, type WorkbookDocument } from '@ex-grid/sheet-engine';
import { ExSheet, type ExSheetApi } from '@ex-grid/sheet-react';
import { DemoNav } from '../demo-nav';
import { useQuery } from '../router';
import { enUs } from '../sheet-cases';
import { positionColumns, positionsTable, type Position } from '../sheet-positions';

const books = ['Rates', 'FX', 'Credit', 'Equity'];

// More positions than the grid paints at once.
const positions: readonly Position[] = Array.from({ length: 40 }, (_, i) => {
    const book = books[i % books.length];
    if (book === undefined) throw new Error('Every position takes one of the books in turn.');
    return { id: `R-${String(i + 1)}`, book, pv: (i + 1) * 10 };
});

// Id and PV are named as the table's columns are declared; Book is the grid's own. The table has two columns, Id, its key, and PV: a position as its row, in that order.
const tableRow = (p: Position): readonly Value[] => [Value.fromText(p.id), Value.fromNumber(p.pv)];

// The table's total, so the page shows when its snapshot has landed.
function opening(): WorkbookDocument {
    const workbook = new Workbook(enUs);
    workbook.declareLinkedTable(positionsTable, ['Id', 'PV'], 'Id');
    workbook.do('Sheet1', SheetEdit.enter(CellAddress.parse('A1'), 'Total PV'));
    workbook.do('Sheet1', SheetEdit.enter(CellAddress.parse('B1'), `=SUM(${positionsTable}[PV])`));
    return workbook.toDocument();
}

/** The page /pointing of the Blazor hosts, PointingPage.razor: the arrow keys pointing inside the positions grid while
 * a Formula is typed (ADR-0058), with ?narrow laying the grid out narrower than its columns. */
export function PointingDemo(): ReactElement {
    const narrow = useQuery('narrow') !== undefined;
    const document = useMemo(opening, []);
    const sheet = useRef<ExSheetApi>(null);
    const [refusal, setRefusal] = useState('');
    const [scope, pointedAt] = useMemo(() => {
        const made = new PointingScope();
        return [made, made.registerGrid<Position>(positionsTable, tableRow)] as const;
    }, []);
    useEffect(() => {
        const api = sheet.current;
        if (api === null) throw new Error('The Sheet was not given to its ref, so its Linked Table cannot be pushed.');
        api.pushLinkedTable(positionsTable, positions.map(tableRow));
    }, []);
    return (
        <>
            <DemoNav route="pointing" />
            <h1>Pointing — the arrow keys inside another grid</h1>
            <p>
                The Sheet and the positions grid beside it are in one Pointing Scope (ADR-0058). Type <code>=</code> into
                a cell and press a PV cell of the positions grid: the Sheet writes what reads it by its row&apos;s key,
                <code> XLOOKUP(&quot;R-1&quot;, Positions[Id], Positions[PV])</code>. The arrow keys then point inside the
                positions grid, and the grid scrolls to keep the pointed cell in view.
            </p>
            {narrow
                ? (
                    <p id="pointing-narrow">
                        Narrow: the positions grid is laid out narrower than its columns, so it shows both of its own
                        scrollbars, and PV runs under the vertical one until the grid is scrolled right (ADR-0013, ADR-0058).
                    </p>
                )
                : null}
            <div className="demo-side-by-side">
                <ExSheet ref={sheet} document={document} viewportHeight={360} viewportWidth={560} pointingScope={scope}
                    onPointingRefused={refused => { setRefusal(refused.message); }} onEditingChanged={() => { setRefusal(''); }} />
                <div id="pointing-positions">
                    {/* The columns are 300 px together: at 260 they overflow, through the grid's parameter (ADR-0013). */}
                    <ExGrid rows={positions} columns={positionColumns} rowHeight={28} viewportHeight={280} viewportWidth={narrow ? 260 : 330} pointedAt={pointedAt} />
                </div>
            </div>
            {/* Why a press or an arrow key wrote nothing, as the Sheet tells the page (ADR-0058). */}
            <p className="demo-status"><span id="pointing-refused">{refusal}</span></p>
            <p>
                The Linked Table <code>Positions</code> has two columns, <code>Id</code>, its key, and <code>PV</code>.
                Book is the grid&apos;s own: a press on it writes nothing, and ← and → pass over it.
            </p>
        </>
    );
}
