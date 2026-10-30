import { useLayoutEffect, useReducer, useRef, type ReactElement } from 'react';
import { actionColumn, GridSource, markColumn, type GridColumn, type GridTemplateCell, type MarkRefusalReason } from '@ex-grid/core';
import { templateColumn } from '@ex-grid/react';
import { samplePositions, type Position } from './Positions';

export const source = GridSource.from(samplePositions());

// What is typed in the Note column, by position: the one store of the notes. The column's value reads it back, so a
// note sorts, filters and copies, and a cell painted again after it scrolled out shows the note typed before.
const notes = new Map<number, string>();
const share = new Intl.NumberFormat('en-US', { style: 'percent', maximumFractionDigits: 0 });

// A template's control the keyboard can enter: Space on the cell asks for it, once per request, and it takes the
// keyboard through the grid, which gives it only while the keyboard is still the grid's. ex-interactive lets the
// pointer reach it; tabIndex -1 keeps the grid one tab stop.
function NoteField({ cell }: { readonly cell: GridTemplateCell<Position> }): ReactElement {
    const input = useRef<HTMLInputElement>(null);
    const acted = useRef(0);
    const [, typed] = useReducer((count: number) => count + 1, 0);
    useLayoutEffect(() => {
        if (cell.focusRequest === 0 || cell.focusRequest === acted.current || input.current === null) return;
        acted.current = cell.focusRequest;
        cell.takeKeyboard(input.current);
    }, [cell]);
    const { id } = cell.row;
    return (
        // A position with no note yet shows an empty field: no note is part of the store's contract, not a stand-in.
        <input ref={input} className="ex-interactive" tabIndex={-1} placeholder="note" aria-label={`Note for position ${String(id)}`}
            value={notes.get(id) ?? ''} onChange={event => {
                if (event.target.value === '') notes.delete(id);
                else notes.set(id, event.target.value);
                typed();
            }}
            style={{ width: '100%', height: '100%', boxSizing: 'border-box', border: 0, padding: '0 8px', background: 'transparent', color: 'inherit', font: 'inherit' }} />
    );
}

// A template paints the cell; the value beside it is still what sorts, filters and copies.
export const columns: readonly GridColumn<Position>[] = [
    markColumn(),
    { name: 'Id', type: 'number', value: p => p.id, width: 70, align: 'left' },
    { name: 'Instrument', type: 'text', value: p => p.instrument },
    { name: 'Trader', type: 'text', value: p => p.trader },
    templateColumn<Position>({ name: 'LimitUsed', type: 'number', value: p => p.limitUsed, header: 'Limit used', width: 180 }, cell => (
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, height: '100%', padding: '0 8px' }}>
            <div style={{ flex: 1, height: 6, borderRadius: 3, background: 'rgba(128, 128, 128, 0.2)' }}>
                <div style={{ height: '100%', borderRadius: 3, width: share.format(Math.min(cell.row.limitUsed, 1)),
                    background: cell.row.limitUsed > 1 ? '#d1242f' : '#2f81f7' }} />
            </div>
            <span style={{ width: 36, textAlign: 'right' }}>{share.format(cell.row.limitUsed)}</span>
        </div>
    )),
    // Space enters this one: its control takes the keyboard.
    templateColumn<Position>({ name: 'Note', type: 'text', value: p => notes.get(p.id), width: 140 }, cell => <NoteField cell={cell} />),
    actionColumn<Position>('Actions', [{ name: 'hedge', label: 'Hedge' }, { name: 'close', label: 'Close' }]),
];

/** Hears the rows and the marks: the counts move with either, a filter as much as a press. */
export function subscribeToMarks(listener: () => void): () => void {
    const rows = source.subscribe(listener);
    const marks = source.marks.subscribe(listener);
    return () => { rows(); marks(); };
}

/** The marks counted over the result as it stands: the same object until the rows or the marks move. */
export const markCounts = () => source.marks.counts;

/** What a mark refused says. A press made on rows that moved, or on data since replaced, marks nothing; the grid
 * names why, and the words are yours. Under live data that is a press the network was slower than. */
export const markRefusalWords: Readonly<Record<MarkRefusalReason, string>> = {
    rowLeftTheWindow: 'Nothing marked: the row left the Window while your command waited. Press again on the current data.',
    orderMoved: 'Nothing marked: the rows moved before your press arrived. Press again on the rows as they are now.',
    sourceChanged: 'Nothing marked: the data was replaced before your press arrived. Press again on what is shown now.',
};
