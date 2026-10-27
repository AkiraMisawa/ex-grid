import { useCallback, useRef, useState } from 'react';
import {
    actionColumn, GridSource, type CellChangedAt, type EditVerdict, type GridActionEvent, type GridColumn, type GridEditIntent, type GridOverwriteNotice,
    type InMemoryGridSource,
} from '@ex-grid/core';
import { samplePositions, type Position } from './Positions';
import { typedNumber } from './TypedNumber';

const limit = 150;
const money = (value: number): string => value.toFixed(2);

// Text that is not a positive number never reaches onEdit: the editor stays open with the message.
function validatePrice(_: Position, text: string): EditVerdict {
    const price = typedNumber(text);
    return price === undefined || price <= 0 ? { kind: 'reject', message: 'Enter a positive price.' } : { kind: 'accept' };
}

export const columns: readonly GridColumn<Position>[] = [
    { name: 'Id', type: 'number', value: p => p.id, width: 80, align: 'left' },
    { name: 'Trader', type: 'text', value: p => p.trader, width: 120, editable: true },
    { name: 'Price', type: 'number', value: p => p.price, width: 100, editable: true, format: v => money(v as number), validate: validatePrice },
    actionColumn('Actions', [{ name: 'approve', label: 'Approve' }], { width: 110 }),
];

// Declared to the grid, F9 reaches this page even while a cell is being edited, and leaves the edit open.
export const feedKeys: readonly string[] = ['F9'];

interface Desk {
    readonly source: InMemoryGridSource<Position>;
    readonly cellChangedAt: CellChangedAt<Position>;
}

// Every position as the source holds it now, by id, filtered out of view or not: the feed moves them all.
type Book = Map<number, Position>;

function openingDesk(): Desk & { readonly book: Book } {
    const positions = samplePositions(3).map((p, at) => ({ ...p, price: 100 + at * 10 }));
    // Keyed by id, so the feed hands over new versions of the same positions.
    const source = GridSource.from(positions, { rowKey: p => p.id });
    // This feed moves only when F9 is pressed, so each change is shown as it comes. A feed that ticks many times a
    // second keeps the default, which gathers its changes.
    source.gatherInterval = 0;
    // The source's own Change Highlight: one function for the source's life.
    const { cellChangedAt } = source;
    if (cellChangedAt === undefined) throw new Error('A source with a Row Key offers a Change Highlight.');
    return { source, cellChangedAt, book: new Map(positions.map(p => [p.id, p])) };
}

/** The desk the three Chromes show: a feed, a user's write and the page's decisions on both. The handlers are made
 * once, since a handler made again on every render would be handed to the grid again each time. */
export function useLiveWrites(): Desk & {
    readonly feed: string;
    readonly result: string;
    readonly advancePrices: () => void;
    readonly onEdit: (edit: GridEditIntent<Position>) => void;
    readonly onOverwriteNotice: (notice: GridOverwriteNotice) => void;
    readonly onAction: (action: GridActionEvent<Position>) => void;
} {
    const [{ source, cellChangedAt, book }] = useState(openingDesk);
    const [feed, setFeed] = useState('Prices start at 100, 110 and 120.');
    const [result, setResult] = useState('Type a Price, press F9 while the editor is open, then Enter.');
    const feeds = useRef(0);

    const onEdit = useCallback((edit: GridEditIntent<Position>) => {
        let written: Position;
        if (edit.column === 'Price') {
            const price = typedNumber(edit.value);
            if (price === undefined) throw new Error(`The Price rule accepted '${edit.value}', which is not a price.`);
            // This desk's rule, judged here: refused before the handler returns, the editor stays open with the
            // typing and this message.
            if (price > limit) {
                edit.refuse(`${money(price)} is above this desk's limit of ${money(limit)}. Enter a lower price, or press Escape.`);
                return;
            }
            // edit.row is the row as the source holds it now, so a field the feed changed meanwhile stays.
            written = { ...edit.row, price };
        } else {
            written = { ...edit.row, trader: edit.value };
        }
        source.replaceRow(edit.row, written);
        book.set(written.id, written);
        setResult(`Written: ${edit.column} ${edit.value} on position ${String(edit.row.id)}.`);
    }, [source, book]);

    // The commit landed over a value the feed changed under the editor. The grid holds no sentence for it: the page
    // says what was written over.
    const onOverwriteNotice = useCallback((notice: GridOverwriteNotice) => {
        setResult(said => `${said} It replaced ${notice.replacedText}: the feed changed ${notice.column} while you typed, from the ${notice.seenText} you saw.`);
    }, []);

    // Stands in for a live feed: a Change Batch with a new version of every position, each price up by 10.
    const advancePrices = useCallback(() => {
        const changed = [...book.values()].map(p => ({ ...p, price: p.price + 10 }));
        for (const p of changed) book.set(p.id, p);
        source.apply({ changed });
        feeds.current += 1;
        setFeed(`Feed update ${String(feeds.current)}: every price up by 10.`);
    }, [source, book]);

    // The grid acts on the row the press was made on, as it is now. Whether the desk may approve it is this page's
    // business rule, and the page says why it declines.
    const onAction = useCallback((action: GridActionEvent<Position>) => {
        setResult(action.row.price > limit
            ? `Not approved: position ${String(action.row.id)} is at ${money(action.row.price)}, above this desk's limit of ${money(limit)}.`
            : `Approved: position ${String(action.row.id)} at ${money(action.row.price)}.`);
    }, []);

    return { source, cellChangedAt, feed, result, advancePrices, onEdit, onOverwriteNotice, onAction };
}
