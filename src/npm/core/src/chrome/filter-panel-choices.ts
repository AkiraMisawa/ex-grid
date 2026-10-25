// What a filter panel's choices mean (ADR-0009), as the C# core's FilterPanelChoices decides it: once, so
// that the built-in panel and a substituted one turn the same choices into the same FilterSpec, and swapping
// Chrome changes nothing about behaviour (ADR-0010, FN-17). A panel lays out a value list or a condition,
// keeps its working state, and asks here what that state means; it decides nothing itself.
//
// Values are held in a value list as the source answered them; two are the same value as C#'s default
// equality holds them: text and booleans by value, a number by the number it is (`1` and `1n` alike), a date
// by the date it names, a Blank as `null`, and any other object only as itself.

import type { ColumnType } from '../query/columns';
import { dateIdentity } from '../query/dates';
import { filterOperatorsFor, type FilterClause, type FilterCombinator, type FilterOperator, type FilterSpec } from '../query/filter';
import { foldCase } from '../query/text-casing';
import type { DistinctValues } from '../source/grid-source';

/** What "(Select All)" shows over the values a list shows (ADR-0009, 2026-09-26). */
export type SelectAllState = 'checked' | 'unchecked' | 'mixed';

const objects = new WeakMap<object, number>();
let objectCount = 0;

/** A text that is the same for two values exactly when the panel holds them the same value. */
export function valueIdentity(value: unknown): string {
    switch (typeof value) {
        case 'string': return `text:${value}`;
        case 'boolean': return `boolean:${String(value)}`;
        case 'number':
        case 'bigint': return `number:${canonicalNumber(value)}`;
        case 'object': {
            if (value === null) return 'blank';
            const date = dateIdentity(value);
            if (date !== undefined) return `date:${date}`;
            let id = objects.get(value);
            if (id === undefined) {
                objectCount += 1;
                id = objectCount;
                objects.set(value, id);
            }
            return `object:${id}`;
        }
        case 'undefined': return 'blank';
        case 'function':
        case 'symbol':
        default: throw new TypeError(`A ${typeof value} is not a value a column holds.`);
    }
}

function canonicalNumber(value: number | bigint): string {
    if (typeof value === 'bigint' || Number.isInteger(value)) return BigInt(value).toString();
    return String(value);
}

/** The values chosen in a value list, held by {@link valueIdentity}. */
export class ChosenValues {
    readonly #held = new Map<string, unknown>();

    constructor(values: Iterable<unknown> = []) {
        for (const value of values) this.add(value);
    }

    has(value: unknown): boolean {
        return this.#held.has(valueIdentity(value));
    }

    add(value: unknown): void {
        this.#held.set(valueIdentity(value), value);
    }

    delete(value: unknown): void {
        this.#held.delete(valueIdentity(value));
    }

    get size(): number {
        return this.#held.size;
    }

    /** The values chosen, in the order they were first chosen. */
    values(): readonly unknown[] {
        return [...this.#held.values()];
    }
}

/**
 * The values a value list starts with chosen: the applied `in` list where one is in force, and every value
 * otherwise. Intersected with the domain just fetched: another column's filter can have shifted it since the
 * list was applied, and a value the panel cannot show must not ride along chosen unseen.
 */
export function initiallyChosen(current: FilterSpec | undefined, domain: DistinctValues): ChosenValues {
    const values = listed(domain);
    const applied = inForce(current);
    if (applied === undefined) return new ChosenValues(values);
    const wanted = new ChosenValues(applied);
    return new ChosenValues(values.filter(value => wanted.has(value)));
}

/**
 * A value list's filter: the chosen values as one `in` clause, in the domain's order, or `undefined`, no
 * filter at all, when every value is chosen. Set membership, never a count. Nothing chosen is an `in` with no
 * values, never no filter, which would show every row; it is no filter a panel may apply ({@link canApply}).
 */
export function fromValueList(domain: DistinctValues, chosen: ChosenValues): FilterSpec | undefined {
    const values = listed(domain);
    if (values.every(value => chosen.has(value))) return undefined;
    return inList(values.filter(value => chosen.has(value)));
}

/**
 * A value list's filter with a search in the box (ADR-0009, 2026-09-26): the checked values among those whose
 * text matches, as Excel applies them. With `addToCurrent`, Excel's "Add current selection to filter", the
 * values of the `in` list in force join them, including any the domain no longer shows. With no search it is
 * {@link fromValueList}. `textOf` is the text the panel shows for a value, which the search is matched against.
 */
export function fromSearchedValueList(domain: DistinctValues, chosen: ChosenValues, textOf: (value: unknown) => string,
    search: string, addToCurrent: boolean, current: FilterSpec | undefined): FilterSpec | undefined {
    if (search.length === 0) return fromValueList(domain, chosen);
    const values = listed(domain);
    let applied = values.filter(value => chosen.has(value) && matches(search, textOf(value)));
    const held = inForce(current);
    if (addToCurrent && held !== undefined) {
        const taken = new ChosenValues(applied);
        const extra = held.filter(value => !taken.has(value));
        const joined = new ChosenValues([...applied, ...extra]);
        const shown = new ChosenValues(values);
        applied = [...values.filter(value => joined.has(value)), ...extra.filter(value => !shown.has(value))];
    }
    const kept = new ChosenValues(applied);
    if (values.every(value => kept.has(value))) return undefined;
    return inList(applied);
}

/**
 * Whether a panel's answer can be applied: every answer but one holding an `in` with no values. Such a clause
 * keeps no row, and the engine refuses it (ADR-0023); no filter in its place would show every row, which
 * nobody chose. So, as Excel's OK is with nothing ticked (ADR-0009), Apply shows it is unavailable while the
 * answer is that one, and refuses it when it comes anyway.
 */
export function canApply(spec: FilterSpec | undefined): boolean {
    return spec === undefined || spec.clauses.every(clause => clause.operator !== 'in' || (clause.values !== undefined && clause.values.length > 0));
}

/** Whether a value's text matches the search: contained, ignoring case, as the panels list it (ADR-0009).
 * An empty search matches everything. */
export function matches(search: string, text: string): boolean {
    return search.length === 0 || foldCase(text).includes(foldCase(search));
}

/** Whether "Add current selection to filter" is offered: while a search is active on a column whose filter in
 * force is a value list (ADR-0009, 2026-09-26). */
export function offersAddToFilter(current: FilterSpec | undefined, search: string): boolean {
    return search.length > 0 && inForce(current) !== undefined;
}

/** What "(Select All)" shows over the values the list shows now: checked when every one is chosen, clear when
 * none is, mixed otherwise (ADR-0009, 2026-09-26). */
export function stateOfAll(shown: readonly unknown[], chosen: ChosenValues): SelectAllState {
    const any = shown.some(value => chosen.has(value));
    const all = shown.every(value => chosen.has(value));
    return all ? 'checked' : any ? 'mixed' : 'unchecked';
}

/** "(Select All)" toggled: every value shown becomes chosen, or, when every one already was, none of them is.
 * Values the search hides are left as they are. */
export function toggleAll(shown: readonly unknown[], chosen: ChosenValues): void {
    if (stateOfAll(shown, chosen) === 'checked') for (const value of shown) chosen.delete(value);
    else for (const value of shown) chosen.add(value);
}

/**
 * A condition form's filter with an optional second condition, as Excel's Custom AutoFilter (ADR-0009,
 * 2026-09-26): each finished condition is a clause, joined by `combinator`. A condition whose operator takes
 * an operand and has none is unfinished and left out; one finished condition is a filter of its one clause,
 * and none is no filter.
 */
export function fromConditions(first: FilterOperator, firstOperand: unknown, combinator: FilterCombinator,
    second: FilterOperator | undefined, secondOperand: unknown): FilterSpec | undefined {
    const clauses: FilterClause[] = [];
    for (const [operator, operand] of [[first, firstOperand], [second, secondOperand]] as const) {
        if (operator === undefined) continue;
        const spec = fromCondition(operator, operand);
        if (spec !== undefined) clauses.push(...spec.clauses);
    }
    if (clauses.length === 0) return undefined;
    return clauses.length === 1 ? Object.freeze({ clauses: Object.freeze(clauses) }) : Object.freeze({ clauses: Object.freeze(clauses), combinator });
}

/** The second condition a condition form starts with, and the combinator between the two: those in force
 * where the column's filter is two conditions; none, joined by And, otherwise. */
export function initialSecond(current: FilterSpec | undefined): {
    readonly operator: FilterOperator | undefined; readonly operand: unknown; readonly combinator: FilterCombinator;
} {
    const clauses = current?.clauses;
    if (current !== undefined && clauses?.length === 2 && clauses.every(clause => clause.operator !== 'in')) {
        const [, second] = clauses;
        // oxlint-disable-next-line ex-grid/no-stand-in-default -- ADR-0009: a FilterSpec's clauses are joined by And unless it says Or
        return { operator: second?.operator, operand: second?.value, combinator: current.combinator ?? 'and' };
    }
    return { operator: undefined, operand: undefined, combinator: 'and' };
}

/**
 * The operator a condition form starts on: the one in force, where the column's filter is one or two
 * conditions (the first of them); where a value list's answer was TooMany, `contains`, the degraded form being
 * Excel's own search box, where the type allows it; the first the column allows otherwise.
 */
export function initialOperator(type: ColumnType, current: FilterSpec | undefined, tooMany: boolean): FilterOperator {
    const clause = firstCondition(current);
    if (clause !== undefined) return clause.operator;
    const allowed = filterOperatorsFor(type);
    const [first] = allowed;
    if (first === undefined) throw new Error(`Internal: a ${type} column offers no operator.`);
    return tooMany && allowed.includes('contains') ? 'contains' : first;
}

/** The operand a condition form starts with: the one in force, where the column's filter is one or two
 * conditions (the first's); none otherwise. */
export function initialOperand(current: FilterSpec | undefined): unknown {
    return firstCondition(current)?.value;
}

/** Whether the operator takes an operand: every one but `isBlank` and `isNotBlank`. */
export function takesOperand(operator: FilterOperator): boolean {
    return operator !== 'isBlank' && operator !== 'isNotBlank';
}

/**
 * A condition's filter, from its operator and its operand: one clause. An operator that takes an operand and
 * has none is no filter at all. `in` offered in a condition form is membership of the one value given, so its
 * operand becomes a one-member list: a clause the engine reads from `values`, and would refuse with `value`.
 */
export function fromCondition(operator: FilterOperator, operand: unknown): FilterSpec | undefined {
    if (!takesOperand(operator)) return Object.freeze({ clauses: Object.freeze([Object.freeze({ operator })]) });
    if (operand === null || operand === undefined) return undefined;
    const clause: FilterClause = operator === 'in' ? { operator, values: Object.freeze([operand]) } : { operator, value: operand };
    return Object.freeze({ clauses: Object.freeze([Object.freeze(clause)]) });
}

function listed(domain: DistinctValues): readonly unknown[] {
    if (domain.kind === 'tooMany') throw new TypeError('A TooMany answer has no values to choose from; the panel shows its condition form.');
    return domain.values.map(blankAsNull);
}

/** A Blank, held as `null`: a source may answer it as `undefined` or `null`, and the two are one value. */
function blankAsNull(value: unknown): unknown {
    return value === undefined ? null : value;
}

function inList(values: readonly unknown[]): FilterSpec {
    return Object.freeze({ clauses: Object.freeze([Object.freeze({ operator: 'in', values: Object.freeze([...values]) } as const)]) });
}

function inForce(current: FilterSpec | undefined): readonly unknown[] | undefined {
    const clauses = current?.clauses;
    if (clauses?.length !== 1) return undefined;
    const [only] = clauses;
    return only?.operator === 'in' ? only.values?.map(blankAsNull) : undefined;
}

function firstCondition(current: FilterSpec | undefined): FilterClause | undefined {
    const clauses = current?.clauses;
    if (clauses === undefined || clauses.length < 1 || clauses.length > 2 || clauses.some(clause => clause.operator === 'in')) return undefined;
    return clauses[0];
}
