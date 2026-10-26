// The bundled ruleset (ADR-0034): ownership and driving are the Consumer's, the implementation ships with the
// library, as GridSource does (ADR-0001). The grid never references it.

import type { GridColumn } from '../dom/grid-column';
import type { CellState } from '../paint/cell-classes';
import { entryFor } from './display-text';
import { checkedVerdict, type EditVerdict } from './edit-intent';

/** One row rule's complaint about a row: which column wears it, and why. */
export interface RuleViolation {
    /** The column whose cell is marked. */
    readonly column: string;
    /** The sentence the cell says when the Focus or the pointer settles on it. */
    readonly message: string;
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
    #cellState: (row: Row, column: GridColumn<Row>) => CellState = () => 'normal';
    #cellMessage: (row: Row, column: GridColumn<Row>) => string | undefined = () => undefined;

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

    /** What a column's `validate` is set to, or `undefined` where no rule was declared, which the grid reads as
     * accept. */
    validatorFor(column: string): ((row: Row, text: string) => EditVerdict) | undefined {
        return this.#columnRules.get(column);
    }

    /**
     * Judges the rows again, after applying: called by the Consumer with the rows it now holds, and it replaces
     * what was known before. Column rules run here too, on the text the editor would open on, which is what makes
     * a paste and a change from program code wear the same marks as a typed edit; a reject arriving this way is a
     * flag, since the value is applied and there is no editor left to hold.
     */
    evaluate(rows: Iterable<Row>, columns: readonly GridColumn<Row>[]): void {
        const next = new WeakMap<Row, ReadonlyMap<string, string>>();
        for (const row of rows) {
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
            if (found.size > 0) next.set(row, found);
        }
        // New functions, deliberately: a grid paints its rows again when the function it was given is another
        // one, and a verdict that changed under the same row instance would otherwise never be painted (ADR-0003).
        this.#cellState = (row, column) => (next.get(row)?.has(column.name) === true ? 'error' : 'normal');
        this.#cellMessage = (row, column) => next.get(row)?.get(column.name);
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
