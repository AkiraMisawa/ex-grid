import { useCallback, useLayoutEffect, useRef, useState, type ReactElement } from 'react';
import { actionColumn, type GridActionEvent, type GridColumn, type GridTemplateCell } from '@ex-grid/core';
import { ExGrid, templateColumn } from '@ex-grid/react';
import { DemoNav } from '../demo-nav';
import { demoPositions, type DemoPosition } from '../demo-data';

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

// Held once, never rebuilt per render: a new columns array would paint every row again (ADR-0003).
const columns: readonly GridColumn<DemoPosition>[] = Object.freeze([
    { name: 'Book', type: 'text', value: (r: DemoPosition) => r.book, width: 180 },
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

/** The page /cells of the Blazor hosts, as far as the Script Core draws it: its Template and Action Columns
 * and the keys that enter them. */
export function Cells(): ReactElement {
    const [status, setStatus] = useState('No action pressed yet.');
    const presses = useRef(0);
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
                Everything on this page is something the value alone cannot say. The last columns are two <b>Template
                Columns</b> and two <b>Action Columns</b> (ADR-0020). Cell State and Row Kind, which the Blazor hosts show
                here, join as the Script Core comes to paint them.
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
                columns={columns}
                rows={demoPositions}
                onAction={onAction}
                rowHeight={28}
                viewportHeight={420}
                viewportWidth={1100}
                pinnedColumnCount={1} />
        </>
    );
}
