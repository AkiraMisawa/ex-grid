import type { CSSProperties, ReactElement } from 'react';
import type { CellState, CellTone, GridColumn } from '@ex-grid/core';
import { ExGrid } from '@ex-grid/react';
import { DemoNav } from '../demo-nav';

/** One row of this page: a value, painted once in each state. */
interface ToneRow {
    readonly book: string;
    readonly amount: number;
}

const rows: readonly ToneRow[] = Object.freeze([
    { book: 'Gain', amount: 1_250 },
    { book: 'Loss', amount: -830 },
    { book: 'Flat', amount: 0 },
]);

/** The states, each with the name its column takes, as the Blazor page names them. */
const states: readonly (readonly [CellState, string])[] = Object.freeze([
    ['normal', 'Normal'], ['stale', 'Stale'], ['error', 'Error'], ['modified', 'Modified'], ['missing', 'Missing'],
]);

// The tone rule: one comparison, declared once per column (ADR-0006). Zero has no tone.
function sign(value: unknown): CellTone {
    if (typeof value !== 'number') throw new TypeError(`An amount is a number, not ${typeof value}.`);
    return value < 0 ? 'negative' : value > 0 ? 'positive' : 'none';
}

// Held once, never rebuilt per render (ADR-0003).
const columns: readonly GridColumn<ToneRow>[] = Object.freeze([
    { name: 'Book', type: 'text', value: (r: ToneRow) => r.book, width: 120 },
    ...states.map(([, name]): GridColumn<ToneRow> => ({ name, type: 'number', value: r => r.amount, width: 110, tone: sign })),
]);

const stateByColumn: ReadonlyMap<string, CellState> = new Map(states.map(([state, name]) => [name, state]));

// One map probe per cell, held once: a new function would paint every row again (ADR-0003). The Book column is in
// no state; any other column is a state's, and one without would be this page's defect, not an ordinary cell.
function stateOf(_row: ToneRow, column: GridColumn<ToneRow>): CellState {
    if (column.name === 'Book') return 'normal';
    const state = stateByColumn.get(column.name);
    if (state === undefined) throw new Error(`Column '${column.name}' names no Cell State on this page.`);
    return state;
}

/** The page /tones of the Blazor hosts: a tone rule's gains and losses in a theme's colours, and a Cell State that
 * outranks them on the same cell (ADR-0006, ADR-0029). */
export function Tones(): ReactElement {
    return (
        <>
            <DemoNav route="tones" />
            <h1>Tone and Cell State</h1>
            <p>
                A tone is what a column&apos;s rule says a value means — a gain, a loss — and the theme says what colour that
                is (ADR-0006, ADR-0029). The bare grid paints a toned cell as an ordinary one; this page&apos;s theme, set on
                the element around the grid, paints a gain green and a loss orange. A Cell State is what the Consumer said
                about one cell, and it outranks the tone: in each column below every cell is in that column&apos;s state, and a
                stale or an error value keeps its state&apos;s colour whatever colour the theme gives its tone, as the flat
                value, which has no tone, shows it. A modified value keeps its mark, and a missing one its ground.
            </p>
            {/* The theme: Visual Tokens set on an ancestor, the route a Consumer or a Wrapper takes (ADR-0029). The tone's
                only; every Cell State keeps the grid's default. */}
            <div id="tones-theme" style={{ '--ex-tone-positive-color': '#1b7f3b', '--ex-tone-negative-color': '#d35400' } as CSSProperties}>
                <ExGrid columns={columns} rows={rows} cellState={stateOf} rowHeight={28} viewportHeight={160}
                    viewportWidth={720} pinnedColumnCount={1} />
            </div>
        </>
    );
}
