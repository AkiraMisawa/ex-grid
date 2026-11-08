import { GridSource, type CellAppearance, type GridColumn } from '@ex-grid/core';
import { samplePositions, type Position } from './Positions';

export const source = GridSource.from(samplePositions(40));
const whole = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
const money = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export const columns: readonly GridColumn<Position>[] = [
    { name: 'Id', type: 'number', value: p => p.id },
    { name: 'Desk', type: 'text', value: p => p.desk },
    { name: 'Instrument', type: 'text', value: p => p.instrument },
    { name: 'Quantity', type: 'number', value: p => p.quantity, format: v => whole.format(v as number) },
    { name: 'Price', type: 'number', value: p => p.price, format: v => money.format(v as number) },
];

// The cells each draft changed, by position and column. An application finds them by comparing each draft with the
// original.
const firstDraft = new Set(['5001 Quantity', '5002 Price', '5004 Quantity', '5004 Price', '5007 Desk', '5009 Price']);
const secondDraft = new Set(['5002 Price', '5003 Quantity', '5004 Price', '5006 Instrument', '5007 Desk', '5010 Quantity']);

// The colours are the Consumer's, as 0xRRGGBB. Dark text keeps them legible under either colour scheme.
const ink = 0x1d2733;
const first: CellAppearance = { fill: 0xcfe2ff, fontColour: ink };
const second: CellAppearance = { fill: 0xffe0b0, fontColour: ink };
const both: CellAppearance = { ...first, lowerRightFill: 0xffe0b0 };
const none: CellAppearance = {};

const changed = (position: Position, column: GridColumn<Position>): readonly [boolean, boolean] => {
    const key = `${String(position.id)} ${column.name}`;
    return [firstDraft.has(key), secondDraft.has(key)];
};

/** Two drafts start from the same original rows. A cell the first draft changed is filled blue, one the second
 * changed is filled amber, and one both changed is split: the first draft above the diagonal, the second below it
 * (ADR-0490). */
export const cellAppearance = (position: Position, column: GridColumn<Position>): CellAppearance => {
    const [inFirst, inSecond] = changed(position, column);
    return inFirst ? (inSecond ? both : first) : inSecond ? second : none;
};

/** The words, because colour is never the only signal. */
export const cellMessage = (position: Position, column: GridColumn<Position>): string | undefined => {
    const [inFirst, inSecond] = changed(position, column);
    if (inFirst && inSecond) return 'Changed in both drafts.';
    if (inFirst) return 'Changed in the first draft.';
    return inSecond ? 'Changed in the second draft.' : undefined;
};
