// The reference Find All (ADR-0470), the counterpart of C#'s GridFindAll: every cell a Find would stop on, in the
// order a forward walk from the start meets them, answered a page at a time with the exact count. `GridSource.from`
// answers with it and a remote source is held to it, as to Find (ADR-0055).
//
// What is matched is Find's: the displayed text, by Find's rule (`matcherOf`), within the scope when one is given and
// across the request's columns in their order. The grid lists the headers it searches itself, before the rows
// (ADR-0333 §3, ADR-0470 §1), so the Consumer is asked only about rows, and the list's positions here count rows'
// matches alone.

import { itemAt } from '../must';
import { inScope, matcherOf, type GridFindPattern } from './find';
import type { SelectionRange } from '../selection/range';

/**
 * A Find All (ADR-0470 §2): what a Find step would look for, over every row, and the range of the list the grid wants
 * now. The grid asks for the range it shows, a page at a time, under the Row Sequence Version it read the positions
 * in; the Consumer answers that range and the exact count of the whole list.
 */
export interface GridFindAllRequest extends GridFindPattern {
    /** The selected ranges, when more than one cell is selected, as a Find step's. Absent: every row. */
    readonly scope?: readonly SelectionRange[] | undefined;
    /** The visible columns' names in the grid's current order: position `i` here is column `i` of every position in
     * this request and its answer. */
    readonly columns: readonly string[];
    /** The order the positions above were read in (ADR-0011). An answer under another version is discarded, and the
     * list with it. */
    readonly rowSequenceVersion: number;
    /** The first entry of the list wanted, counted from 0 in walk order. */
    readonly start: number;
    /** How many entries are wanted from `start`: the answer holds this many, or every one left past `start`. */
    readonly count: number;
}

/** One cell of a Find All's list: where it is, and the text it displays, which is what matched. */
export interface GridFindAllMatch {
    /** The row's position in the order the request was read in. */
    readonly row: number;
    /** The column's name, one of the request's `columns`. */
    readonly column: string;
    /** The cell's displayed text, as the user reads it. */
    readonly text: string;
    /** What the list calls the row, in the Consumer's words (a trade's id, say), in place of its position; absent,
     * the list names it by its position in the order shown (ADR-0470 §3). */
    readonly rowLabel?: string | undefined;
}

/**
 * The Consumer's answer to a Find All (ADR-0470 §2): the entries of the range asked and the exact count of the whole
 * list, which is never an estimate and never capped, read under one Row Sequence Version.
 */
export interface GridFindAllPage {
    /** How many cells match, exactly, over every row in the scope. */
    readonly total: number;
    /** The entries from the request's `start`, in walk order: `count` of them, or every one left past `start`. */
    readonly matches: readonly GridFindAllMatch[];
    /** The order the rows were read in. Under another than the request's, the grid drops the list as the order
     * having moved. */
    readonly rowSequenceVersion: number;
}

/** A page of a Find All's list, frozen and checked: `matches` at most as many as the range asked. */
export function findAllPage(total: number, matches: readonly GridFindAllMatch[], rowSequenceVersion: number): GridFindAllPage {
    if (!Number.isInteger(total) || total < 0) throw new RangeError(`A Find All counts 0 matches or more, not ${total}.`);
    if (matches.length > total) throw new RangeError(`A Find All page holds ${matches.length} matches of a list of ${total}.`);
    return Object.freeze({
        total, rowSequenceVersion,
        matches: Object.freeze(matches.map(({ row, column, text, rowLabel }) => Object.freeze(rowLabel === undefined ? { row, column, text } : { row, column, text, rowLabel }))),
    });
}

/**
 * Every match of a Find All over one set of rows, found once and read a page at a time: the reference source keeps
 * one while the rows, the pattern, the scope and the columns stand, so a list scrolled through is walked once. Each
 * match is held as its cell's ordinal in row-major order, a number for millions of matches rather than an object.
 */
export class FindAllMatches<Row> {
    readonly #rows: readonly Row[];
    readonly #columns: readonly string[];
    readonly #texts: readonly (((row: Row) => string) | undefined)[];
    readonly #ordinals: readonly number[];
    readonly #rowLabel: ((row: Row) => string) | undefined;

    private constructor(rows: readonly Row[], columns: readonly string[], texts: readonly (((row: Row) => string) | undefined)[],
        ordinals: readonly number[], rowLabel: ((row: Row) => string) | undefined) {
        this.#rows = rows;
        this.#columns = columns;
        this.#texts = texts;
        this.#ordinals = ordinals;
        this.#rowLabel = rowLabel;
    }

    /**
     * Walks `rows` once (ADR-0470 §2), by rows and across a row in the request's column order. `textOf` answers a
     * column's displayed text by name, or `undefined` for a column with no text to search or one not known, which is
     * skipped, as a step skips it. An empty pattern matches nothing. `rowLabel`, where given, names each match's row in
     * the Consumer's words.
     */
    static of<Row>(rows: readonly Row[], request: Omit<GridFindAllRequest, 'start' | 'count' | 'rowSequenceVersion'>,
        textOf: (column: string) => ((row: Row) => string) | undefined, rowLabel?: (row: Row) => string): FindAllMatches<Row> {
        const columns = Object.freeze([...request.columns]);
        const texts = columns.map(textOf);
        const matches = matcherOf(request);
        const ordinals: number[] = [];
        if (request.text !== '') {
            const columnCount = columns.length;
            rows.forEach((item, row) => {
                texts.forEach((text, column) => {
                    if (text === undefined) return;
                    if (request.scope !== undefined && !inScope(request.scope, row, column)) return;
                    if (matches(text(item))) ordinals.push(row * columnCount + column);
                });
            });
        }
        return new FindAllMatches(rows, columns, texts, Object.freeze(ordinals), rowLabel);
    }

    /** How many cells match. */
    get total(): number {
        return this.#ordinals.length;
    }

    /** The entries from `start`, `count` of them or every one left, under `rowSequenceVersion`. A range past the end
     * holds none; a range outside the list's positions is refused. */
    page(start: number, count: number, rowSequenceVersion: number): GridFindAllPage {
        if (!Number.isInteger(start) || start < 0) throw new RangeError(`A Find All page starts at an entry at or after 0, not ${start}.`);
        if (!Number.isInteger(count) || count < 1) throw new RangeError(`A Find All page asks for 1 entry or more, not ${count}.`);
        const columnCount = this.#columns.length;
        const matches = this.#ordinals.slice(start, start + count).map(ordinal => {
            const row = Math.floor(ordinal / columnCount);
            const column = ordinal % columnCount;
            const text = itemAt(this.#texts, column);
            if (text === undefined) throw new Error('Internal: a Find All match in a column with no text.');
            const item = itemAt(this.#rows, row);
            const match = { row, column: itemAt(this.#columns, column), text: text(item) };
            if (this.#rowLabel === undefined) return match;
            const label: unknown = this.#rowLabel(item);
            if (typeof label !== 'string') throw new TypeError(`findAllRowLabel answers text for every row, not ${typeof label} (ADR-0470 §3).`);
            return { ...match, rowLabel: label };
        });
        return findAllPage(this.total, matches, rowSequenceVersion);
    }
}

/**
 * One page of the reference Find All over `rows` (ADR-0470 §2), read under `rowSequenceVersion`: the whole list walked
 * and the range asked read from it. A source that answers many pages of one list keeps a {@link FindAllMatches}
 * instead, as `GridSource.from` does.
 */
export function findAll<Row>(rows: readonly Row[], rowSequenceVersion: number, request: GridFindAllRequest,
    textOf: (column: string) => ((row: Row) => string) | undefined): GridFindAllPage {
    return FindAllMatches.of(rows, request, textOf).page(request.start, request.count, rowSequenceVersion);
}
