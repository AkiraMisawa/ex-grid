import { useCallback, useRef, useState } from 'react';
import {
    GridRuleset, GridSource, type CellPosition, type EditVerdict, type GridClearIntent, type GridColumn, type GridEditIntent, type GridFillIntent,
    type GridPasteIntent, type GridRefusal, type SelectionRange,
} from '@ex-grid/core';
import { samplePositions, type Position } from './Positions';
import { typedNumber, typedWholeNumber } from './TypedNumber';

// Text that is not a number is rejected and the editor stays open; a number the desk would query is accepted and
// flagged.
function quantityRule(_: Position, text: string): EditVerdict {
    const quantity = typedWholeNumber(text);
    if (quantity === undefined) return { kind: 'reject', message: `'${text}' is not a whole number` };
    return Math.abs(quantity) > 100_000 ? { kind: 'flag', message: "Over the desk's size limit of 100,000" } : { kind: 'accept' };
}

function priceRule(_: Position, text: string): EditVerdict {
    const price = typedNumber(text);
    if (price === undefined) return { kind: 'reject', message: `'${text}' is not a price` };
    return price <= 0 ? { kind: 'flag', message: 'A price should be above zero' } : { kind: 'accept' };
}

const whole = new Intl.NumberFormat('en-US');
const money = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

// The editor's verdicts come from the ruleset, so a rule judged at the editor is the rule `evaluate` paints after a
// paste or a fill.
function blotter(): { readonly rules: GridRuleset<Position>; readonly columns: readonly GridColumn<Position>[] } {
    const rules = new GridRuleset<Position>().column('Quantity', quantityRule).column('Price', priceRule);
    const validatorFor = (column: string): (row: Position, text: string) => EditVerdict => {
        const validate = rules.validatorFor(column);
        if (validate === undefined) throw new Error(`The ruleset has no rule for ${column}.`);
        return validate;
    };
    const columns: readonly GridColumn<Position>[] = [
        { name: 'Id', type: 'number', value: p => p.id, width: 70, align: 'left' },
        { name: 'Instrument', type: 'text', value: p => p.instrument, width: 120 },
        { name: 'Trader', type: 'text', value: p => p.trader, width: 120, editable: true },
        { name: 'Quantity', type: 'number', value: p => p.quantity, width: 110, editable: true, validate: validatorFor('Quantity'),
            format: v => whole.format(v as number) },
        { name: 'Price', type: 'number', value: p => p.price, width: 100, editable: true, validate: validatorFor('Price'),
            format: v => money.format(v as number) },
        { name: 'Pnl', type: 'number', value: p => p.pnl, header: 'P&L', width: 130, format: v => money.format(v as number) },
    ];
    return { rules, columns };
}

// A new object with one value changed: the new object is what repaints the row. Text the column cannot hold answers
// undefined.
function edited(row: Position, column: string, text: string): Position | undefined {
    switch (column) {
        case 'Trader': return { ...row, trader: text };
        case 'Quantity': { const quantity = typedWholeNumber(text); return quantity === undefined ? undefined : { ...row, quantity }; }
        case 'Price': { const price = typedNumber(text); return price === undefined ? undefined : { ...row, price }; }
        default: return undefined;
    }
}

// Each cell once: the ranges of a selection may overlap, and a cell covered twice is written once.
function* cells(ranges: readonly SelectionRange[]): Generator<CellPosition> {
    const seen = new Set<string>();
    for (const range of ranges) {
        for (let row = range.topRow; row < range.topRow + range.rowCount; row++) {
            for (let column = range.leftColumn; column < range.leftColumn + range.columnCount; column++) {
                const key = `${String(row)}:${String(column)}`;
                if (seen.has(key)) continue;
                seen.add(key);
                yield { row, column };
            }
        }
    }
}

const mod = (value: number, by: number): number => ((value % by) + by) % by;

const refusalWords: Partial<Record<string, string>> = {
    targetNotEditable: 'Not pasted: the selection covers a column that cannot be edited.',
    singleCellTarget: 'Not pasted: select a range the same shape as what you copied.',
    shapeMismatch: 'Not pasted: the copied block does not divide into the selection.',
};

type Step = readonly (readonly [before: Position, after: Position])[];
type Write = readonly [row: Position, column: string, text: string];

// Every cell is resolved to its row before anything is written, since writing first could reorder the rows. A text
// a cell cannot hold refuses the whole gesture, named, rather than writing the cells around it.
function planned(writes: readonly Write[]): { readonly step: Step } | { readonly unreadable: Write } {
    const latest = new Map<Position, Position>();
    const step: (readonly [Position, Position])[] = [];
    for (const write of writes) {
        const [row, column, text] = write;
        const current = latest.get(row) ?? row;
        const replacement = edited(current, column, text);
        if (replacement === undefined) return { unreadable: write };
        step.push([current, replacement]);
        latest.set(row, replacement);
    }
    return { step };
}

/** The blotter the three Chromes show: its source, its columns and rules, and an undo history of one step per
 * gesture (an edit, a paste, a fill or a clear), so one Ctrl+Z undoes it. The handlers are made once, since a handler
 * made again on every render would be handed to the grid again each time. */
export function useEditingDesk() {
    const [{ source, book }] = useState(() => {
        const positions = samplePositions();
        // Every position as the source holds it now, by id, filtered out of view or not: the rules judge them all.
        return { source: GridSource.from(positions), book: new Map(positions.map(p => [p.id, p])) };
    });
    const [{ rules, columns }] = useState(blotter);
    // The ruleset hands the grid new functions after every judgement: kept in state, so the grid paints them.
    const [paint, setPaint] = useState(() => ({ cellState: rules.cellState, cellMessage: rules.cellMessage }));
    const undoSteps = useRef<Step[]>([]);
    const redoSteps = useRef<Step[]>([]);
    const [history, setHistory] = useState({ canUndo: false, canRedo: false });
    const [message, setMessage] = useState('Type into Trader, Quantity or Price.');

    // Judged over every position, not the rows the filter shows: a judgement replaces every mark it held before.
    const judge = useCallback(() => {
        rules.evaluate(book.values(), columns);
        setPaint({ cellState: rules.cellState, cellMessage: rules.cellMessage });
        setHistory({ canUndo: undoSteps.current.length > 0, canRedo: redoSteps.current.length > 0 });
    }, [book, rules, columns]);

    const rowAt = useCallback((cell: CellPosition): Position => {
        const { window, windowStart } = source.snapshot();
        const row = window[cell.row - windowStart];
        if (row === undefined) throw new RangeError(`The source does not hold row ${String(cell.row)}.`);
        return row;
    }, [source]);

    const columnAt = useCallback((at: number): GridColumn<Position> => {
        const column = columns[at];
        if (column === undefined) throw new RangeError(`There is no column ${String(at)}.`);
        return column;
    }, [columns]);

    const replace = useCallback((before: Position, after: Position) => {
        source.replaceRow(before, after);
        book.set(after.id, after);
    }, [source, book]);

    const write = useCallback((step: Step) => {
        for (const [before, after] of step) replace(before, after);
        if (step.length > 0) {
            undoSteps.current.push(step);
            redoSteps.current = [];
        }
        judge();
        setMessage(`${String(step.length)} cell(s) written.`);
    }, [replace, judge]);

    // Writes a gesture's cells, or refuses it whole, saying which text no cell of its column can hold.
    const apply = useCallback((writes: readonly Write[], refuse: () => void) => {
        const plan = planned(writes);
        if ('step' in plan) {
            write(plan.step);
            return;
        }
        refuse();
        const [, column, text] = plan.unreadable;
        setMessage(`Nothing written: '${text}' is not a ${column === 'Quantity' ? 'whole number' : 'price'}.`);
    }, [write]);

    // The editor's text has passed the column's rule, so it is always one the column holds.
    const onEdit = useCallback((edit: GridEditIntent<Position>) => {
        const plan = planned([[edit.row, edit.column, edit.value]]);
        if (!('step' in plan)) throw new Error(`The rule for ${edit.column} accepted '${edit.value}', which the column cannot hold.`);
        write(plan.step);
    }, [write]);

    // Ctrl+V, Ctrl+D, Ctrl+R and Ctrl+Enter all arrive here: one value per target cell.
    const onPaste = useCallback((paste: GridPasteIntent) => {
        apply([...cells(paste.plan.targets)].map(cell => [rowAt(cell), columnAt(cell.column).name, paste.valueFor(cell)] as const), () => { paste.refuse(); });
    }, [apply, rowAt, columnAt]);

    // The fill handle repeats the source range into the target beside it.
    const onFill = useCallback((fill: GridFillIntent) => {
        const { source: from, target } = fill;
        apply([...cells([target])].map(cell => {
            const origin = {
                row: from.topRow + mod(cell.row - from.topRow, from.rowCount),
                column: from.leftColumn + mod(cell.column - from.leftColumn, from.columnCount),
            };
            return [rowAt(cell), columnAt(cell.column).name, String(columnAt(origin.column).value(rowAt(origin)))] as const;
        }), () => { fill.refuse(); });
    }, [apply, rowAt, columnAt]);

    // Delete asks for no value at all. A trader can be blank; a quantity or a price cannot, so those are left alone.
    const onClear = useCallback((clear: GridClearIntent) => {
        const traders = [...cells(clear.targets)].filter(cell => columnAt(cell.column).name === 'Trader');
        const plan = planned(traders.map(cell => [rowAt(cell), 'Trader', ''] as const));
        if (!('step' in plan)) throw new Error('A trader can always be blank.');
        write(plan.step);
    }, [write, rowAt, columnAt]);

    const onRefused = useCallback((refusal: GridRefusal<Position>) => {
        if (refusal.kind === 'paste') setMessage(refusalWords[refusal.reason] ?? `Not pasted: ${refusal.reason}.`);
    }, []);

    const replay = useCallback((from: { current: Step[] }, to: { current: Step[] }, verb: string) => {
        const step = from.current.pop();
        if (step === undefined) return;
        const reversed = step.toReversed().map(([before, after]) => [after, before] as const);
        for (const [now, back] of reversed) replace(now, back);
        to.current.push(reversed);
        judge();
        setMessage(`${verb}: ${String(step.length)} cell(s).`);
    }, [replace, judge]);
    const undo = useCallback(() => { replay(undoSteps, redoSteps, 'Undone'); }, [replay]);
    const redo = useCallback(() => { replay(redoSteps, undoSteps, 'Redone'); }, [replay]);

    return { source, columns, paint, message, ...history, onEdit, onPaste, onFill, onClear, onRefused, undo, redo };
}
