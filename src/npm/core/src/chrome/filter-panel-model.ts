// A filter panel's working state and what each choice in it means (ADR-0009, ADR-0044), free of any markup: the
// built-in panel draws from it, and so does a Chrome slot's panel (ADR-0390), so swapping Chrome changes nothing
// about what a choice does (ADR-0010). Everything in it is uncommitted until it is answered, and it answers whole:
// one FilterSpec, which the grid refuses or applies. What the choices mean is FilterPanelChoices'; what a typed
// operand reads as is the grid's culture's (ADR-0023, ADR-0370 section 10).

import type { DistinctValues } from '../source/grid-source';
import type { ColumnType, DateType } from '../query/columns';
import { filterOperatorsFor, type FilterCombinator, type FilterOperator, type FilterSpec } from '../query/filter';
import {
    canApply, ChosenValues, fromConditions, fromSearchedValueList, initialOperand, initialOperator, initialSecond, initiallyChosen,
    matches, offersAddToFilter, stateOfAll, takesOperand, toggleAll, type SelectAllState,
} from './filter-panel-choices';
import type { GridCulture } from './grid-culture';
import { operandText, readOperand, refusalText, type OperandReading, type TemporalLike } from './operand-reading';

/** What a column's filter panel lays out (ADR-0009): the condition form alone, the safe default, which never
 * asks the source for its values; a value list of the column's distinct values, degraded to the condition
 * form where the source answers TooMany; or both, which the built-in panel shows as the value list. */
export type FilterUi = 'condition' | 'valueList' | 'both';

/** The column a panel filters. */
export interface FilterPanelColumn {
    readonly name: string;
    readonly type: ColumnType;
    readonly dateType: DateType;
    readonly filterUi: FilterUi;
    /** The text a value's cell shows (ADR-0006): what the value list shows, and what its search matches. */
    readonly textOf: (value: unknown) => string;
}

/** The built-in Chrome's words for a Blank in the value list (ADR-0023). */
export const blankWords = '(Blanks)';

/** A value the list shows: the value, the text the panel shows for it and its search matches, and whether it
 * is ticked. A Blank's text is the built-in words for it, which the search matches, and `blank` says it is one. */
export interface FilterPanelEntry {
    /** The value, as the source listed it: what the panel hands back when it is ticked. */
    readonly value: unknown;
    /** The text its cells show, which the search matches. */
    readonly text: string;
    /** Whether it is the Blank. */
    readonly blank: boolean;
    /** Whether it is ticked. */
    readonly checked: boolean;
}

/** The value list as it stands (ADR-0009). */
export interface FilterPanelValueList {
    /** The value list stands. */
    readonly kind: 'valueList';
    /** The text in the search box. */
    readonly search: string;
    /** Whether Excel's "Add current selection to filter" is offered: while a search stands over a column filtered
     * by its values. */
    readonly addOffered: boolean;
    /** Whether it is ticked. */
    readonly addToCurrent: boolean;
    /** "(Select All)" over the values shown: checked, unchecked or mixed. Over a search, it is Excel's "(Select
     * All Search Results)". */
    readonly selectAll: SelectAllState;
    /** The values the search shows, in the domain's order. */
    readonly entries: readonly FilterPanelEntry[];
}

/** One condition of the condition form: its operator, the text typed for its value, and why that text does not
 * read as the column's type, where it does not (ADR-0023). */
export interface FilterPanelCondition {
    /** The operator chosen. */
    readonly operator: FilterOperator;
    /** The text typed for its value. */
    readonly operand: string;
    /** Whether the operator takes a value: `isBlank` and `isNotBlank` take none, and their field is not drawn. */
    readonly takesOperand: boolean;
    /** Why the typed text does not read as the column's type, in the core's words, or `undefined` where it reads or
     * nothing is typed (ADR-0023). */
    readonly refusal: string | undefined;
}

/** The condition form as it stands, Excel's Custom AutoFilter (ADR-0009). */
export interface FilterPanelConditions {
    /** The condition form stands. */
    readonly kind: 'conditions';
    /** The operators the core offers the column, and no other (FN-18). */
    readonly allowed: readonly FilterOperator[];
    /** The first condition. */
    readonly first: FilterPanelCondition;
    /** How the two conditions are joined, And or Or. */
    readonly combinator: FilterCombinator;
    /** The second condition, `undefined` until one is chosen ("(none)"). */
    readonly second: FilterPanelCondition | undefined;
}

/** What a filter panel shows now: its form, whether OK may be pressed, and whether the value list is still on
 * its way (the condition form stands meanwhile). A new object at each change, and the same one otherwise. */
export type FilterPanelView = (FilterPanelValueList | FilterPanelConditions) & {
    /** Whether OK is available: not while a value list keeps nothing, as Excel's OK is (ADR-0009, ticket 76). The
     * condition form's stays available, and a value that does not read applies nothing when it is pressed. */
    readonly canApply: boolean;
    /** Whether the value list is still on its way. */
    readonly fetching: boolean;
};

/** The panel's answer: the FilterSpec to apply, or `reads: false` where a typed value does not read, and nothing
 * is applied while the form says why. */
export interface FilterPanelAnswer {
    readonly reads: boolean;
    readonly spec: FilterSpec | undefined;
}

interface Working {
    values: DistinctValues | undefined;
    chosen: ChosenValues | undefined;
    search: string;
    addToCurrent: boolean;
    operator: FilterOperator;
    operand: string;
    combinator: FilterCombinator;
    second: FilterOperator | undefined;
    secondOperand: string;
}

/**
 * A column's filter panel's state, for one opening. Its value list, where the column declares one, is asked for
 * once and not again while it stands, since a list that shifted under every tick would be unusable (ADR-0009).
 */
export class FilterPanelModel {
    readonly #column: FilterPanelColumn;
    readonly #current: FilterSpec | undefined;
    readonly #culture: GridCulture;
    readonly #temporal: TemporalLike | undefined;
    readonly #fetched: () => void;
    readonly #listeners = new Set<() => void>();
    readonly #working: Working;
    readonly #startedOn: FilterOperator;
    #fetch: AbortController | undefined;
    #view: FilterPanelView | undefined;
    // The listed values with their text, made once as the list arrives: a keystroke in the search reformats nothing.
    #listed: readonly { readonly value: unknown; readonly text: string }[] = [];
    #textOfListed = new Map<unknown, string>();
    #domain = new ChosenValues();
    // Each value's entry, ticked and not, made once: a change makes no new entry for a value it did not change.
    readonly #entries = new Map<unknown, readonly [FilterPanelEntry, FilterPanelEntry]>();

    /**
     * Opens over `current`, the column's spec in force. `distinct` asks the source for the column's values; it is
     * asked only where the column declares a value list, and the condition form stands while the answer is on its
     * way, as the C# core's does. `fetched` hears that answer arrive, or be given up.
     */
    constructor(column: FilterPanelColumn, current: FilterSpec | undefined, culture: GridCulture, temporal: TemporalLike | undefined,
        distinct: (signal: AbortSignal) => Promise<DistinctValues>, fetched: () => void) {
        if (column.type === 'date' && temporal === undefined) {
            throw new TypeError(`Column '${column.name}' holds dates, and a date typed into its filter is made with Temporal, `
                + 'which the grid was not given: pass `temporal`, the browser\'s Temporal or a polyfill\'s (ADR-0350 section 16).');
        }
        this.#column = column;
        this.#current = current;
        this.#culture = culture;
        this.#temporal = temporal;
        this.#fetched = fetched;
        const operator = initialOperator(column.type, current, false);
        const second = initialSecond(current);
        this.#startedOn = operator;
        // An operand reopens in a text that reads back as itself (ADR-0006, note of 2026-10-01; tickets 94, 96).
        this.#working = {
            values: undefined, chosen: undefined, search: '', addToCurrent: false,
            operator, operand: operandText(initialOperand(current), culture),
            combinator: second.combinator, second: second.operator, secondOperand: operandText(second.operand, culture),
        };
        if (column.filterUi !== 'condition') {
            const fetch = new AbortController();
            this.#fetch = fetch;
            // A source that fails to answer fails where it is heard, and the panel stands on its condition form:
            // no list is better than one made up. The fetch is over then, so the grid settles on that form.
            // oxlint-disable-next-line ex-grid/no-catch -- an aborted fetch's own rejection is expected and refuses nothing; any other failure is said and rethrown
            void distinct(fetch.signal).then(answer => {
                if (!fetch.signal.aborted) this.#arrived(answer);
            }, (error: unknown) => {
                if (fetch.signal.aborted) return;
                this.#fetch = undefined;
                this.#changed();
                this.#fetched();
                throw error;
            });
        }
    }

    /** The column the panel filters. */
    get column(): FilterPanelColumn {
        return this.#column;
    }

    /** The column's spec in force when the panel opened. */
    get current(): FilterSpec | undefined {
        return this.#current;
    }

    /** What the panel shows now. */
    get view(): FilterPanelView {
        this.#view ??= this.#made();
        return this.#view;
    }

    /** Whether the source's answer to the value list has arrived: values, or TooMany. */
    get arrived(): boolean {
        return this.#working.values !== undefined;
    }

    /** Whether the value list is still on its way: the grid stands unsettled meanwhile (ADR-0220). */
    get fetching(): boolean {
        return this.#fetch !== undefined && !this.#fetch.signal.aborted && this.#working.values === undefined;
    }

    /** Hears each change to {@link view}; answers the function that stops it. */
    subscribe(listener: () => void): () => void {
        this.#listeners.add(listener);
        return () => { this.#listeners.delete(listener); };
    }

    /** The panel closed: an answer still on its way is given up. */
    close(): void {
        const fetching = this.fetching;
        this.#fetch?.abort();
        if (fetching) this.#fetched();
    }

    /** The search box's text. */
    setSearch(text: string): void {
        this.#working.search = text;
        this.#changed();
    }

    /** "Add current selection to filter" ticked or not. */
    setAddToCurrent(on: boolean): void {
        this.#working.addToCurrent = on;
        this.#changed();
    }

    /** A value's checkbox ticked or not: the `value` of one of the view's entries, never one the list does not hold. */
    setChecked(value: unknown, checked: boolean): void {
        const chosen = this.#working.chosen;
        if (chosen === undefined) throw new Error('The value list is not standing: there is no value to tick.');
        if (!this.#domain.has(value)) {
            throw new RangeError(`${String(value)} is not one of the values the list holds: tick the \`value\` of one of the view's entries.`);
        }
        if (checked) chosen.add(value);
        else chosen.delete(value);
        this.#changed();
    }

    /** "(Select All)" toggled over the values shown; the values the search hides are left as they are. */
    toggleAll(): void {
        const list = this.#list();
        if (list === undefined) throw new Error('The value list is not standing: there is nothing to select.');
        toggleAll(this.#shown().map(({ value }) => value), list.chosen);
        this.#changed();
    }

    /** The first condition's operator: one the core offers the column (FN-18). */
    setOperator(operator: FilterOperator): void {
        this.#working.operator = this.#offered(operator);
        this.#changed();
    }

    /** The text typed for the first condition's value. */
    setOperand(text: string): void {
        this.#working.operand = text;
        this.#changed();
    }

    /** How the two conditions are joined. */
    setCombinator(combinator: FilterCombinator): void {
        const given: unknown = combinator;
        if (given !== 'and' && given !== 'or') throw new RangeError(`A combinator is 'and' or 'or', not '${String(given)}'.`);
        this.#working.combinator = combinator;
        this.#changed();
    }

    /** The second condition's operator, or `undefined` for none. */
    setSecond(operator: FilterOperator | undefined): void {
        this.#working.second = operator === undefined ? undefined : this.#offered(operator);
        this.#changed();
    }

    /** The text typed for the second condition's value. */
    setSecondOperand(text: string): void {
        this.#working.secondOperand = text;
        this.#changed();
    }

    /** The working state as one FilterSpec, by FilterPanelChoices' rules. Each condition's value is read as the
     * column's type: empty is an unfinished condition, left out; a value that does not read answers `reads: false`. */
    answer(): FilterPanelAnswer {
        const list = this.#list();
        if (list !== undefined) return { reads: true, spec: this.#valueListAnswer(list.domain, list.chosen) };
        const { operator, operand, combinator, second, secondOperand } = this.#working;
        const first = this.#operand(operator, operand);
        const other = second === undefined ? { reads: true, value: undefined } : this.#operand(second, secondOperand);
        if (!first.reads || !other.reads) return { reads: false, spec: undefined };
        return { reads: true, spec: fromConditions(operator, first.value, combinator, second, other.value) };
    }

    // The value list's answer. The condition form stood while it was on its way, and whatever the user chose there
    // meanwhile stays: only an untouched form starts over on TooMany's operator.
    #arrived(answer: DistinctValues): void {
        const working = this.#working;
        working.values = answer;
        if (answer.kind === 'tooMany') {
            if (working.operator === this.#startedOn && working.operand.length === 0) {
                working.operator = initialOperator(this.#column.type, this.#current, true);
            }
        } else {
            working.chosen = initiallyChosen(this.#current, answer);
            this.#listed = answer.values.map(value => ({ value, text: this.#textOf(value) }));
            this.#textOfListed = new Map(this.#listed.map(({ value, text }) => [value, text]));
            this.#domain = new ChosenValues(answer.values);
        }
        this.#changed();
        this.#fetched();
    }

    #changed(): void {
        this.#view = undefined;
        for (const listener of this.#listeners) listener();
    }

    #offered(operator: FilterOperator): FilterOperator {
        if (!filterOperatorsFor(this.#column.type).includes(operator)) {
            throw new RangeError(`'${operator}' is not an operator this column's filter offers.`);
        }
        return operator;
    }

    #list(): { readonly domain: DistinctValues; readonly chosen: ChosenValues } | undefined {
        const { values, chosen } = this.#working;
        return values?.kind === 'values' && chosen !== undefined ? { domain: values, chosen } : undefined;
    }

    // A value as the panel lists it and its search matches it: the text its cell shows, a Blank in words.
    readonly #textOf = (value: unknown): string => (value === null || value === undefined ? blankWords : this.#column.textOf(value));

    // The same, read from the list made as it arrived.
    readonly #listedText = (value: unknown): string => {
        const text = this.#textOfListed.get(value);
        if (text === undefined) throw new Error('Internal: a value the list does not hold was asked for its text.');
        return text;
    };

    #shown(): readonly { readonly value: unknown; readonly text: string }[] {
        const search = this.#working.search;
        return search.length === 0 ? this.#listed : this.#listed.filter(({ text }) => matches(search, text));
    }

    #valueListAnswer(domain: DistinctValues, chosen: ChosenValues): FilterSpec | undefined {
        const { search, addToCurrent } = this.#working;
        return fromSearchedValueList(domain, chosen, this.#listedText, search, addToCurrent, this.#current);
    }

    #entry(value: unknown, text: string, checked: boolean): FilterPanelEntry {
        let made = this.#entries.get(value);
        if (made === undefined) {
            const blank = value === null || value === undefined;
            made = [Object.freeze({ value, text, blank, checked: false }), Object.freeze({ value, text, blank, checked: true })];
            this.#entries.set(value, made);
        }
        return made[checked ? 1 : 0];
    }

    #read(text: string): OperandReading {
        return readOperand(this.#column.type, text, this.#culture, this.#column.dateType, this.#temporal);
    }

    #operand(operator: FilterOperator, text: string): { readonly reads: boolean; readonly value: unknown } {
        if (!takesOperand(operator) || text.length === 0) return { reads: true, value: undefined };
        const reading = this.#read(text);
        return { reads: reading.refusal === 'none', value: reading.value };
    }

    #condition(operator: FilterOperator, operand: string): FilterPanelCondition {
        const takes = takesOperand(operator);
        const refusal = !takes || operand.length === 0 ? undefined : refusalText(this.#read(operand), operand, this.#culture);
        return Object.freeze({ operator, operand, takesOperand: takes, refusal });
    }

    #made(): FilterPanelView {
        const working = this.#working;
        const fetching = this.fetching;
        const list = this.#list();
        if (list !== undefined) {
            const shown = this.#shown();
            const entries = Object.freeze(shown.map(({ value, text }) => this.#entry(value, text, list.chosen.has(value))));
            return Object.freeze({
                kind: 'valueList', search: working.search, addOffered: offersAddToFilter(this.#current, working.search),
                addToCurrent: working.addToCurrent, selectAll: stateOfAll(shown.map(({ value }) => value), list.chosen), entries,
                canApply: canApply(this.#valueListAnswer(list.domain, list.chosen)), fetching,
            });
        }
        return Object.freeze({
            kind: 'conditions', allowed: filterOperatorsFor(this.#column.type),
            first: this.#condition(working.operator, working.operand), combinator: working.combinator,
            second: working.second === undefined ? undefined : this.#condition(working.second, working.secondOperand),
            canApply: true, fetching,
        });
    }
}
