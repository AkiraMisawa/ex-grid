import { assembleCopy, GridSource, type GridColumn, type GridCopyAnswer, type GridCopyRequest } from '@ex-grid/core';

/** A sample whose amount sits under a header rounded as Excel shows a large number. */
export interface Sample {
    readonly name: string;
    readonly amount: number;
}

export const source = GridSource.from<Sample>([{ name: 'First', amount: 10 }, { name: 'Second', amount: 20 }]);

export const columns: readonly GridColumn<Sample>[] = [
    { name: 'Name', type: 'text', value: row => row.name, header: 'Sample', width: 140 },
    { name: 'Amount', type: 'number', value: row => row.amount, header: '1.23457E+11', width: 180 },
];

// The raw header Excel receives from the HTML table, by column name; a column not named is the same in both formats.
const rawHeaders = new Map([['Amount', '123456789012.345']]);

function columnAt(column: number): GridColumn<Sample> {
    const found = columns[column];
    if (found === undefined) throw new RangeError(`The copy names column ${String(column)}, which this grid does not have.`);
    return found;
}

/** The copy, answered from this small source, which holds the whole result in the current filter and sort order:
 * read synchronously, and refused when the rows moved since the request was taken. */
export function answer(request: GridCopyRequest): GridCopyAnswer {
    const { window, windowStart, rowSequenceVersion } = source.snapshot();
    if (rowSequenceVersion !== request.rowSequenceVersion) return { kind: 'refuse', sentence: 'Nothing was copied: the rows moved. Copy again.' };
    const cell = (row: number, column: number): string => {
        const sample = window[row - windowStart];
        if (sample === undefined) throw new RangeError(`The copy names row ${String(row)}, which the source does not hold.`);
        const { name } = columnAt(column);
        if (name === 'Name') return sample.name;
        if (name === 'Amount') return String(sample.amount);
        throw new RangeError(`No text is written for column ${name}.`);
    };
    const header = (column: number): string => columnAt(column).header ?? columnAt(column).name;
    const headerRaw = (column: number): string | undefined => rawHeaders.get(columnAt(column).name);
    const copy = assembleCopy(request.plan, {
        displayAt: cell, rawAt: cell, headerRawAt: headerRaw, ...(request.withHeaders ? { headerAt: header } : {}),
    });
    return { kind: 'write', text: copy.text, html: copy.html };
}
