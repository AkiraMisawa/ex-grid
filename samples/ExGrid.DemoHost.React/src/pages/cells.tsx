import { useCallback, useLayoutEffect, useMemo, useRef, useState, type ReactElement } from 'react';
import {
    actionColumn, GridSource, markColumn, type CellPosition, type CellState, type GridActionEvent, type GridColumn, type GridEditIntent, type GridTemplateCell,
    type RowKind, type RowMarkIntent, type RowMarks,
} from '@ex-grid/core';
import { ExGrid, templateColumn } from '@ex-grid/react';
import { DemoNav } from '../demo-nav';
import { demoPositions, type DemoPosition } from '../demo-data';
import { useQuery } from '../router';

/**
 * The other half of ADR-0020's seam: a control the Consumer owns. `ex-interactive` turns pointer events back
 * on for it, and `tabindex="-1"` keeps the grid one tab stop. Space on its cell asks it to take the keyboard,
 * once per request, which it does through the grid, granted only while the keyboard is still the grid's
 * (ADR-0037, ADR-0204).
 */
function NoteInput({ cell }: { readonly cell: GridTemplateCell<DemoPosition> }): ReactElement {
    const input = useRef<HTMLInputElement>(null);
    const acted = useRef(0);
    useLayoutEffect(() => {
        if (cell.focusRequest === 0 || cell.focusRequest === acted.current || input.current === null) return;
        acted.current = cell.focusRequest;
        cell.takeKeyboard(input.current);
    }, [cell]);
    return <input ref={input} className="ex-interactive demo-note" tabIndex={-1} placeholder="note" aria-label={`Note for ${cell.row.book}`} />;
}

const book: GridColumn<DemoPosition> = { name: 'Book', type: 'text', value: (r: DemoPosition) => r.book, width: 180 };

// Held once, never rebuilt per render: a new columns array would paint every row again (ADR-0003).
const columns: readonly GridColumn<DemoPosition>[] = Object.freeze([
    book,
    { name: 'CloseOfBusiness', type: 'number', value: (r: DemoPosition) => r.closeOfBusiness, header: 'Close of business' },
    { name: 'Intraday', type: 'number', value: (r: DemoPosition) => r.intraday },
    // The value is still what sorting and copy read, never the template (ADR-0020).
    templateColumn<DemoPosition>({ name: 'LimitUsed', type: 'number', value: r => r.limitUsed, header: 'Limit used', width: 160 }, cell => (
        <div className="demo-bar">
            <div className="demo-bar-fill" style={{ width: `${Math.trunc(cell.row.limitUsed * 100)}%` }} />
        </div>
    )),
    templateColumn<DemoPosition>({ name: 'Note', type: 'text', value: r => r.book, header: 'Note', width: 140 }, cell => <NoteInput cell={cell} />),
    actionColumn<DemoPosition>('Actions', [{ name: 'open', label: 'Open' }]),
    // Several actions in one cell: Space enters it and the arrows choose, the Interactive mode (ADR-0037).
    // Declared, never counted from what is painted.
    actionColumn<DemoPosition>('Review', [
        { name: 'approve', label: 'Approve' }, { name: 'query', label: 'Query' }, { name: 'escalate', label: 'Escalate' },
    ], { header: 'Review' }),
]);

// The Row Kind the Consumer computed its subtotals with; the grid is only told the role (ADR-0024).
const kindOf = (row: DemoPosition): RowKind => row.kind;

// The books the page's batch did not reach, and the one a desk overrode: the Consumer's own vocabulary, mapped
// onto the grid's five Cell States on the way in (ADR-0006). One lookup for the page's life, so the same answer
// paints no cell again; a Consumer whose metadata moves hands in a new function, as a new row is a new row.
const staleBooks = new Set(['EMEA · Credit', 'EMEA · FX']);
const overriddenBooks = new Set(['Americas · Rates']);

const stateOf = (row: DemoPosition, column: GridColumn<DemoPosition>): CellState => {
    if (column.name === 'Intraday') {
        if (staleBooks.has(row.book)) return 'stale';
        if (overriddenBooks.has(row.book)) return 'modified';
    }
    if (column.name === 'CloseOfBusiness') {
        if (row.book === 'Americas · Commodity') return 'error';
        if (row.book === 'Americas · Equity') return 'missing';
    }
    return 'normal';
};

// ?editable=1: Book takes typing, so the grid holds the keyboard in its Keyboard Field (ADR-0080) beside an
// Interactive cell.
const editableColumns: readonly GridColumn<DemoPosition>[] = Object.freeze([{ ...book, editable: true }, ...columns.slice(1)]);
// ?dblclick=1: a Mark Column follows the others (ADR-0043), each array held once as the others are.
const mark = markColumn<DemoPosition>({ width: 60 });
const markedColumns: readonly GridColumn<DemoPosition>[] = Object.freeze([...columns, mark]);
const editableMarkedColumns: readonly GridColumn<DemoPosition>[] = Object.freeze([...editableColumns, mark]);

// ?second=1: a second grid below, which another grid's press can give the keyboard to while a request for it is
// on its way (ADR-0204, SRV-5).
const secondColumns: readonly GridColumn<DemoPosition>[] = Object.freeze([
    book, { name: 'Intraday', type: 'number', value: (r: DemoPosition) => r.intraday },
]);

/** The Blazor page binds a Window, which holds no query, so nothing on it can be searched and Ctrl+F opens
 * nothing (FD-2): the rows' own source, less its find. The rest is the source's, its marks excepted, which this
 * page hears itself. */
function unsearched<Row>(rows: GridSource<Row>): GridSource<Row> {
    return {
        subscribe: listener => rows.subscribe(listener),
        snapshot: () => rows.snapshot(),
        setColumns: given => { rows.setColumns(given); },
        setSorts: sorts => { rows.setSorts(sorts); },
        setFilter: filter => { rows.setFilter(filter); },
        needRange: range => rows.needRange(range),
        rows: (range, signal) => rows.rows(range, signal),
        distinctValues: (column, signal) => rows.distinctValues(column, signal),
    };
}

/** The page holds no marks: every intent is reported in the action line, and the boxes stay empty, as on the
 * Blazor page. */
function heardMarks(say: (text: string) => void): RowMarks<DemoPosition> {
    return {
        isMarked: () => false,
        counts: undefined,
        onMarkIntent: (intent: RowMarkIntent<DemoPosition>) => {
            say(intent.kind === 'oneRow' ? `Mark pressed on ${intent.row.book}.` : `Mark intent heard: ${intent.kind}, held by no one on this page.`);
            return Promise.resolve();
        },
        setRowKind: () => undefined,
        subscribe: () => () => undefined,
    };
}

/** The page /cells of the Blazor hosts, as far as the Script Core draws it: its Template and Action Columns
 * and the keys that enter them. */
export function Cells(): ReactElement {
    const editable = useQuery('editable') === '1';
    const listensToLeave = useQuery('leave') === '1';
    const listensToDoubleClick = useQuery('dblclick') === '1';
    const second = useQuery('second') === '1';
    const [status, setStatus] = useState('No action pressed yet.');
    const source = useMemo(() => unsearched(GridSource.from(demoPositions)), []);
    const secondSource = useMemo(() => unsearched(GridSource.from(demoPositions)), []);
    const marks = useMemo(() => heardMarks(setStatus), []);
    // ?dblclick=1 listens to a double click where no edit opens (ADR-0063); what was heard and how often is a count
    // a test can read (DC-63).
    const [heardDoubleClick, setHeardDoubleClick] = useState('No double click heard yet.');
    const doubleClicks = useRef(0);
    const onCellDoubleClick = useCallback(({ row, column }: CellPosition) => {
        doubleClicks.current += 1;
        setHeardDoubleClick(`Double click heard on row ${row}, column ${column}. Heard so far: ${doubleClicks.current}.`);
    }, []);
    const presses = useRef(0);
    // ?leave=1 listens to the Escape that leaves the grid (onLeave, ADR-0070), and does nothing with it but count
    // it, which DC-62 reads below the grid.
    const [heardLeave, setHeardLeave] = useState('No leave heard yet.');
    const leaves = useRef(0);
    const onLeave = useCallback(() => {
        leaves.current += 1;
        setHeardLeave(`Leave heard. Heard so far: ${leaves.current}.`);
    }, []);
    // An edit is reported and not kept: the page shows what was asked.
    const onEdit = useCallback(({ row, column, value }: GridEditIntent<DemoPosition>) => {
        setStatus(`Edited '${column}' on ${row.book} to ${String(value)}.`);
    }, []);
    // The count makes "fired exactly once" observable: a held Space must not fire again per auto-repeat, and
    // Enter must never fire at all (ADR-0020, ADR-0037).
    const onAction = useCallback(({ row, column, action }: GridActionEvent<DemoPosition>) => {
        presses.current += 1;
        setStatus(`Pressed '${action}' in column '${column}' on ${row.book}. Presses so far: ${presses.current}.`);
    }, []);
    return (
        <>
            <DemoNav route="cells" />
            <h1>Cells and rows — actions and a template</h1>
            <p>
                Everything on this page is something the value alone cannot say. The region rows and the last row are a
                group and a total row, painted from their <b>Row Kind</b> (ADR-0024); <i>Intraday</i> and <i>Close of
                business</i> carry a <b>Cell State</b> on the books this batch did not reach, one a desk overrode, and two
                the page has nothing for (ADR-0006). The last columns are two <b>Template Columns</b> and two <b>Action
                Columns</b> (ADR-0020).
            </p>
            <p>
                Everything here is reachable from the keyboard (ADR-0037). <b>Space</b> on an <i>Open</i> cell fires it.
                Space on a <i>Review</i> cell enters it: <b>←</b> and <b>→</b> choose, Space fires the chosen action,
                {' '}<b>Esc</b> leaves, and Enter or Tab leave without firing anything. Space on a <i>Note</i> cell puts
                the caret in the field; Esc brings the keyboard back to the grid.
            </p>
            <p className="demo-status">
                <span id="action-status">{status}</span>
            </p>
            <ExGrid
                columns={listensToDoubleClick ? (editable ? editableMarkedColumns : markedColumns) : editable ? editableColumns : columns}
                source={source}
                // The Blazor page pushes its Window and nobody answers a Selection Summary there: no strip here either
                // (ADR-0130).
                showSelectionSummary={false}
                cellState={stateOf}
                rowKind={kindOf}
                onAction={onAction}
                {...(editable ? { onEdit } : {})}
                {...(listensToLeave ? { onLeave } : {})}
                {...(listensToDoubleClick ? { onCellDoubleClick, marks } : {})}
                rowHeight={28}
                viewportHeight={420}
                viewportWidth={1100}
                pinnedColumnCount={1} />
            {/* Below the grid, so a line that changes between the two presses of a double click never moves the rows
                under the pointer. */}
            {listensToDoubleClick ? (
                <p className="demo-status">
                    <span id="double-click-status">{heardDoubleClick}</span>
                </p>
            ) : null}
            {listensToLeave ? (
                <p className="demo-status">
                    <span id="leave-status">{heardLeave}</span>
                </p>
            ) : null}
            {second ? (
                <>
                    <h2>A second grid</h2>
                    <div id="second-grid">
                        <ExGrid columns={secondColumns} source={secondSource} showSelectionSummary={false} rowHeight={28}
                            viewportHeight={200} viewportWidth={480} />
                    </div>
                </>
            ) : null}
        </>
    );
}
