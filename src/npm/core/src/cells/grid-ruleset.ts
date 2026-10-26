// The bundled ruleset (ADR-0034): ownership and driving are the Consumer's, the implementation ships with the
// library, as GridSource does (ADR-0001). The grid never references it.

import type { GridColumn } from '../dom/grid-column';
import { foundAt, notFound, type GridFindResult } from '../finding/find';
import type { CellState } from '../paint/cell-classes';
import type { CellPosition } from '../selection/cell';
import { defined } from '../must';
import { checkedKey, quoted, type RowKey, type RowKeyOf } from '../source/row-key';
import { entryFor } from './display-text';
import { checkedVerdict, type EditVerdict } from './edit-intent';

/** One row rule's complaint about a row: which column wears it, and why. */
export interface RuleViolation {
    /** The column whose cell is marked. */
    readonly column: string;
    /** The sentence the cell says when the Focus or the pointer settles on it. */
    readonly message: string;
}

/** One cell a {@link Breach} involves: the row by its Row Key (ADR-0140), wherever it stands, and the column by
 * name. */
export interface BreachCell {
    /** The row's Row Key, as the ruleset's key function reads it. */
    readonly rowKey: RowKey;
    /** The column whose cell is marked. */
    readonly column: string;
}

/**
 * One set rule's complaint over the whole collection (ADR-0381): the rule's name, the sentence every involved cell
 * says, and the cells it involves. The message names the other side of the breach in the Consumer's own words, a
 * trade id rather than a row position, which a sort changes.
 */
export interface Breach {
    /** The rule's name. */
    readonly rule: string;
    /** The sentence each involved cell says. */
    readonly message: string;
    /** The cells the breach involves: at least one. */
    readonly cells: readonly BreachCell[];
}

/**
 * One step to the next or the previous error cell (ADR-0381 section 3), asked of the Consumer as a Find step is
 * (ADR-0055): the grid knows only the cells it paints, so a step through those alone would skip every error outside
 * the Window without saying so.
 */
export interface GridErrorStepRequest {
    /** Whether this step runs backward: the previous error cell, Shift+F8. */
    readonly backward: boolean;
    /** The Focus the step starts from: it begins at the cell after it (before it, backward) and considers it last,
     * so a lone error cell finds itself. Absent: from the start (the end, backward). */
    readonly from?: CellPosition | undefined;
    /** The visible columns' names in the grid's current order: position `i` here is column `i` of `from` and of
     * the answer. */
    readonly columns: readonly string[];
    /** The order the positions were read in (ADR-0011). An answer the grid receives under another version moves
     * nothing. */
    readonly rowSequenceVersion: number;
}

/**
 * The bundled ruleset (ADR-0034). It holds both kinds of rule and answers both display channels, so one
 * implementation runs whether a change arrived by a single edit, by a paste, or from program code.
 *
 * Column rules are asked at the editor through {@link GridRuleset.validatorFor}, which is what a column's
 * `validate` is set to. Row rules span the row and can only be judged after the value is applied, since applying
 * is the Consumer's (ADR-0007), so they are Flag-only and are judged by {@link GridRuleset.evaluate}.
 *
 * What it knows is keyed by row instance, and replaced whole by each `evaluate`: the Consumer hands over new
 * instances when it applies, so a map that accumulated would answer for rows nobody can see.
 */
export class GridRuleset<Row extends object> {
    readonly #columnRules = new Map<string, (row: Row, text: string) => EditVerdict>();
    readonly #rowRules: ((row: Row) => Iterable<RuleViolation>)[] = [];
    readonly #setRules: ((rows: readonly Row[]) => Iterable<Breach>)[] = [];
    readonly #rowKey: RowKeyOf<Row> | undefined;
    #marked: (row: Row) => ReadonlyMap<string, string> | undefined = () => undefined;
    #breaches: readonly Breach[] = Object.freeze([]);
    #cellState: (row: Row, column: GridColumn<Row>) => CellState = () => 'normal';
    #cellMessage: (row: Row, column: GridColumn<Row>) => string | undefined = () => undefined;

    /**
     * A ruleset keyed by row instance, or, given the Source's Row Key function (ADR-0140), by Row Key (ADR-0381):
     * only a ruleset with one may hold {@link GridRuleset.set} rules, whose Breaches name rows by their keys.
     */
    constructor(rowKey?: RowKeyOf<Row>) {
        const given: unknown = rowKey;
        if (given !== undefined && typeof given !== 'function') throw new TypeError('A ruleset\'s Row Key is a function of the row (ADR-0140, ADR-0381).');
        this.#rowKey = rowKey;
    }

    /** A rule on one column's committed text, asked at the editor. The intended split is the ADR's: an unparseable
     * text rejects, a parseable value a rule dislikes flags; which is which stays the Consumer's convention. */
    column(column: string, rule: (row: Row, text: string) => EditVerdict): this {
        if (column === '') throw new TypeError('A column rule names its column (ADR-0034).');
        const given: unknown = rule;
        if (typeof given !== 'function') throw new TypeError(`The rule on column '${column}' is a function of the row and the text (ADR-0034).`);
        this.#columnRules.set(column, rule);
        return this;
    }

    /** A rule spanning the row, judged after the value is applied. Flag-only: there is no row to judge at the
     * commit, so it has nothing to veto. */
    row(rule: (row: Row) => Iterable<RuleViolation>): this {
        const given: unknown = rule;
        if (typeof given !== 'function') throw new TypeError('A row rule is a function of the row (ADR-0034).');
        this.#rowRules.push(rule);
        return this;
    }

    /**
     * A rule over the whole collection (ADR-0381), judged after apply by {@link GridRuleset.evaluate} over every row
     * it is handed. Flag-tier by construction: the way to a valid collection often passes through invalid ones, so
     * it never holds the Cell Editor, and its refusal is the Consumer's own save while {@link GridRuleset.breaches}
     * is not empty. Every cell a Breach names wears `error` and its message. Throws on a ruleset made without a Row
     * Key function: a Breach names rows wherever they stand, which a row instance outside the Window cannot.
     */
    set(rule: (rows: readonly Row[]) => Iterable<Breach>): this {
        const given: unknown = rule;
        if (typeof given !== 'function') throw new TypeError('A set rule is a function of the rows (ADR-0381).');
        if (this.#rowKey === undefined) {
            throw new TypeError('A set rule names its rows by Row Key, so the ruleset is made with the Source\'s Row Key function: '
                + 'new GridRuleset(rowKey) (ADR-0381).');
        }
        this.#setRules.push(rule);
        return this;
    }

    /** The Breaches the last {@link GridRuleset.evaluate} found, in the order the set rules were declared and each
     * answered them: an order that does not depend on the Window (ADR-0381 section 3). */
    get breaches(): readonly Breach[] {
        return this.#breaches;
    }

    /** What a column's `validate` is set to, or `undefined` where no rule was declared, which the grid reads as
     * accept. */
    validatorFor(column: string): ((row: Row, text: string) => EditVerdict) | undefined {
        return this.#columnRules.get(column);
    }

    /**
     * Judges the rows again, after applying: called by the Consumer with the rows it now holds, and it replaces
     * what was known before. Column rules run here too, on the text the editor would open on, which is what makes
     * a paste and a change from program code wear the same marks as a typed edit; a reject arriving this way is a
     * flag, since the value is applied and there is no editor left to hold. A ruleset holding set rules is handed
     * the whole collection: a breach between a row on screen and one far below is still a breach (ADR-0381).
     */
    evaluate(rows: Iterable<Row>, columns: readonly GridColumn<Row>[]): void {
        const all = [...rows];
        const rowKey = this.#rowKey;
        const byKey = new Map<RowKey, Map<string, string>>();
        const byRow = new WeakMap<Row, Map<string, string>>();
        const keyOf = (row: Row): RowKey => checkedKey(defined(rowKey, 'the ruleset\'s Row Key function')(row), 'A row handed to evaluate');
        // Two rows under one key would share one set of marks, and the earlier's would be lost unsaid (ADR-0140:
        // a Row Key names one row).
        const keyed = rowKey === undefined ? undefined : all.map(keyOf);
        const keys = new Set<RowKey>();
        for (const key of keyed === undefined ? [] : keyed) {
            if (keys.has(key)) throw new RangeError(`Two rows handed to evaluate have the Row Key ${quoted(key)}: a Row Key names one row (ADR-0140).`);
            keys.add(key);
        }
        const put = (at: number, row: Row, found: Map<string, string>): void => {
            if (keyed === undefined) byRow.set(row, found);
            else byKey.set(defined(keyed[at], 'the row\'s key'), found);
        };
        for (const [at, row] of all.entries()) {
            const found = new Map<string, string>();
            for (const column of columns) {
                const rule = this.#columnRules.get(column.name);
                if (rule === undefined) continue;
                const verdict = checkedVerdict(rule(row, entryFor(column, column.value(row))), column.name);
                if (verdict.kind !== 'accept') found.set(column.name, verdict.message);
            }
            for (const rule of this.#rowRules) {
                for (const violation of rule(row)) found.set(violation.column, violation.message);
            }
            if (found.size > 0) put(at, row, found);
        }
        const breaches: Breach[] = [];
        if (this.#setRules.length > 0) {
            // A Breach naming a key no row has, or a column the grid does not declare, would mark nothing, and the
            // save would refuse with no cell to show why: refused here instead (spine rule 1).
            const names = new Set(columns.map(column => column.name));
            for (const rule of this.#setRules) {
                for (const breach of rule(all)) {
                    const checked = checkedBreach(breach);
                    for (const { rowKey: key, column } of checked.cells) {
                        if (!keys.has(key)) {
                            throw new RangeError(`Breach '${checked.rule}' names Row Key ${quoted(key)}, which no row handed to evaluate has (ADR-0381).`);
                        }
                        if (!names.has(column)) {
                            throw new RangeError(`Breach '${checked.rule}' names the column '${column}', which none of the columns handed to evaluate is (ADR-0381).`);
                        }
                        let found = byKey.get(key);
                        if (found === undefined) {
                            found = new Map();
                            byKey.set(key, found);
                        }
                        // A cell two rules complain about says both: neither sentence outranks the other.
                        const earlier = found.get(column);
                        found.set(column, earlier === undefined || earlier === checked.message ? checked.message : `${earlier} ${checked.message}`);
                    }
                    breaches.push(checked);
                }
            }
        }
        this.#breaches = Object.freeze(breaches);
        // The rows a grid paints are mostly the instances handed here: looked up by instance, they spare the
        // Consumer's key function a call per cell per paint, and a ruleset with nothing marked asks nothing.
        const handed = new WeakMap<Row, ReadonlyMap<string, string> | undefined>();
        if (keyed !== undefined) for (const [at, row] of all.entries()) handed.set(row, byKey.get(defined(keyed[at], 'the row\'s key')));
        const marked = keyed === undefined
            ? (row: Row) => byRow.get(row)
            : byKey.size === 0
                ? () => undefined
                : (row: Row) => (handed.has(row) ? handed.get(row) : byKey.get(keyOf(row)));
        this.#marked = marked;
        // New functions, deliberately: a grid paints its rows again when the function it was given is another
        // one, and a verdict that changed under the same row instance would otherwise never be painted (ADR-0003).
        this.#cellState = (row, column) => (marked(row)?.has(column.name) === true ? 'error' : 'normal');
        this.#cellMessage = (row, column) => marked(row)?.get(column.name);
    }

    /**
     * The next or the previous error cell (ADR-0381 section 3), for a grid's `findError`: every cell the last
     * {@link GridRuleset.evaluate} marked, walked in Find's order (ADR-0055) through `rowsInOrder`, the result in the
     * order the grid shows it, which a Consumer reads from its Source. A marked row the Filter hides is not stepped
     * to, nor a marked column that is hidden.
     */
    findError(request: GridErrorStepRequest, rowsInOrder: readonly Row[]): GridFindResult {
        const { columns, backward, from } = request;
        const total = rowsInOrder.length * columns.length;
        // A Focus outside the rows handed over means they are not the rows the grid shows, the unfiltered collection
        // or a shorter page, and an answer read in them would name another row: refused, not walked from the start
        // (spine rule 1).
        if (from !== undefined && !(from.row >= 0 && from.row < rowsInOrder.length && from.column >= 0 && from.column < columns.length)) {
            throw new RangeError(`The error step starts at row ${from.row}, column ${from.column}, outside the ${rowsInOrder.length} rows and `
                + `${columns.length} columns it was handed: rowsInOrder is the result in the grid's order, read from the Source (ADR-0381).`);
        }
        if (total === 0) return notFound;
        const start = from !== undefined ? from.row * columns.length + from.column : backward ? 0 : total - 1;
        // The nearest marked cell past the start, counted in the steps the walk would take: one pass over the rows,
        // never one over every cell.
        let best = Number.POSITIVE_INFINITY;
        let found: GridFindResult = notFound;
        rowsInOrder.forEach((row, index) => {
            const marked = this.#marked(row);
            if (marked === undefined) return;
            columns.forEach((column, at) => {
                if (!marked.has(column)) return;
                const ordinal = index * columns.length + at;
                // The start itself is considered last, so a lone error cell finds itself.
                const ahead = (((backward ? start - ordinal : ordinal - start) % total) + total) % total;
                const steps = ahead === 0 ? total : ahead;
                if (steps < best) {
                    best = steps;
                    found = foundAt(index, column);
                }
            });
        });
        return found;
    }

    /** The grid's `cellState`, replaced by every {@link GridRuleset.evaluate}: hand the grid this, read again after
     * each, not a function that reads it. */
    get cellState(): (row: Row, column: GridColumn<Row>) => CellState {
        return this.#cellState;
    }

    /** The grid's `cellMessage`, replaced by every {@link GridRuleset.evaluate} as `cellState` is. */
    get cellMessage(): (row: Row, column: GridColumn<Row>) => string | undefined {
        return this.#cellMessage;
    }
}

// A Breach as an untyped caller may answer one, checked where it arrives: one that says nothing, or names no cell,
// would refuse a save with nothing on screen to say why (ADR-0381, spine rule 1).
function checkedBreach(breach: unknown): Breach {
    if (typeof breach === 'object' && breach !== null) {
        const { rule, message, cells }: { readonly rule?: unknown; readonly message?: unknown; readonly cells?: unknown } = breach;
        if (typeof rule === 'string' && rule.trim() !== '' && typeof message === 'string' && message.trim() !== '' && Array.isArray(cells) && cells.length > 0) {
            const checked = cells.map((cell: unknown): BreachCell => {
                if (typeof cell === 'object' && cell !== null) {
                    const { rowKey, column }: { readonly rowKey?: unknown; readonly column?: unknown } = cell;
                    if (typeof column === 'string' && column !== '') return Object.freeze({ rowKey: checkedKey(rowKey, `A cell of Breach '${rule}'`), column });
                }
                throw new TypeError(`Breach '${rule}' names a cell as { rowKey, column }, not ${JSON.stringify(cell)} (ADR-0381).`);
            });
            return Object.freeze({ rule, message, cells: Object.freeze(checked) });
        }
    }
    throw new TypeError(`A set rule answers Breaches as { rule, message, cells: [{ rowKey, column }, …] }, with a sentence that says why and at `
        + `least one cell, not ${JSON.stringify(breach)} (ADR-0381).`);
}
