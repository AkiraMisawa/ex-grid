import type { ReactElement } from 'react';
import type { CellState, GridColumn, RowKind } from '@ex-grid/core';
import { ExGrid } from '@ex-grid/react';
import { DemoNav } from '../demo-nav';

/** One row of this page's result. The role is stored on the row because this Consumer computed the subtotals
 * itself; the grid is only told the role (ADR-0024). */
interface StripeRow {
    readonly name: string;
    readonly amount: number;
    readonly quantity: number;
    readonly desk: string;
    readonly kind: RowKind;
    readonly amountMissing: boolean;
}

// Positions chosen for what they show: a group row at an odd position (it would otherwise be striped) and one at
// an even position, to compare the two; a total at an odd position; a missing value on a striped row and on an
// unstriped one.
const oddGroup = 3;
const evenGroup = 12;
const oddTotal = 7;
const stripedMissing = 5;
const plainMissing = 8;

const desks = ['Rates', 'Credit', 'FX', 'Equity'] as const;

function deskAt(i: number): string {
    const desk = desks[i % desks.length];
    if (desk === undefined) throw new Error(`Internal: no desk at ${i}.`);
    return desk;
}

function kindAt(i: number): RowKind {
    if (i === oddGroup || i === evenGroup) return 'group';
    return i === oddTotal ? 'total' : 'detail';
}

const rows: readonly StripeRow[] = Object.freeze(Array.from({ length: 200 }, (_, i): StripeRow => ({
    name: `Book ${String(i).padStart(3, '0')}`,
    amount: 1_000 + (i * 37.5),
    quantity: 10 + (i % 17),
    desk: deskAt(i),
    kind: kindAt(i),
    amountMissing: i === stripedMissing || i === plainMissing,
})));

// Held once, never rebuilt per render: a new array or a new function would paint every row again (ADR-0003).
const columns: readonly GridColumn<StripeRow>[] = Object.freeze([
    { name: 'Name', type: 'text', value: (r: StripeRow) => r.name, width: 160 },
    { name: 'Amount', type: 'number', value: (r: StripeRow) => (r.amountMissing ? undefined : r.amount), width: 140 },
    { name: 'Quantity', type: 'number', value: (r: StripeRow) => r.quantity, width: 120 },
    { name: 'Desk', type: 'text', value: (r: StripeRow) => r.desk, width: 160 },
]);

const kindOf = (row: StripeRow): RowKind => row.kind;

const stateOf = (row: StripeRow, column: GridColumn<StripeRow>): CellState =>
    (column.name === 'Amount' && row.amountMissing ? 'missing' : 'normal');

/** The page /stripes of the Blazor hosts: Row Stripes under Row Kind, a Cell State and the hover band (ADR-0038). */
export function Stripes(): ReactElement {
    return (
        <>
            <DemoNav route="stripes" />
            <h1>Row Stripes</h1>
            <p>
                Every second row of the result wears a stripe (ADR-0038). It is decided from the row&apos;s position in the
                whole result, never from its place on the screen, so it stays with its row however far the grid scrolls.
                The pinned <i>Name</i> column is striped with the rest of the row. A group or total row — and a missing
                value — paints its own ground over the stripe, and still counts, so the rows after it keep their places.
                The hover band, the selection and the Focus paint above it all.
            </p>
            <ExGrid
                columns={columns}
                rows={rows}
                cellState={stateOf}
                rowKind={kindOf}
                stripeRows
                highlightHoverRow
                rowHeight={28}
                viewportHeight={420}
                viewportWidth={720}
                pinnedColumnCount={1} />
        </>
    );
}
