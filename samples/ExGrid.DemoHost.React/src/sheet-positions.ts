// The positions the Sheet pages show in a grid beside a Sheet and push into it as the Linked Table Positions
// (ADR-0049): SheetPage.razor's, SheetsPage.razor's and PointingPage.razor's, one record and one look for all three.
import type { GridColumn } from '@ex-grid/core';
import { Value } from '@ex-grid/sheet-engine';

/** The Linked Table's name. */
export const positionsTable = 'Positions';

/** One of a page's positions: what its grid shows and what its Sheet reads, from one source. */
export interface Position {
    readonly id: string;
    readonly book: string;
    readonly pv: number;
}

/** A number as .NET's "N2" in the invariant culture writes it: grouped thousands and two places. */
function n2(value: unknown): string {
    if (typeof value !== 'number') throw new TypeError(`A PV is a number, not ${typeof value}.`);
    return value.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

/** The grid's columns, named as the table's columns are declared, so a column a Sheet names is the grid's column of
 * the same name. */
export const positionColumns: readonly GridColumn<Position>[] = [
    { name: 'Id', type: 'text', value: p => p.id, width: 90 },
    { name: 'Book', type: 'text', value: p => p.book, width: 90 },
    { name: 'PV', type: 'number', value: p => p.pv, width: 120, format: n2 },
];

/** A position as the table's row of Id, Book and PV: what a page pushes, and what its Scope reads a pressed row's key
 * from. */
export const positionRow = (p: Position): readonly Value[] => [Value.fromText(p.id), Value.fromText(p.book), Value.fromNumber(p.pv)];
