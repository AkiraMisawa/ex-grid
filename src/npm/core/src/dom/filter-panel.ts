// The built-in Chrome's filter panel (ADR-0009, ADR-0044): Excel's shape under a column's commands, in the
// same popover. A value list where the column declares one and the source listed its values, and the
// condition form, one or two conditions joined by And or Or, where it declares that or the values were
// TooMany. Everything in it is uncommitted until OK, or Enter in its one text field, and it applies whole,
// through the grid. What its choices mean is FilterPanelChoices' (ADR-0010); what a typed operand reads as
// is the grid's culture's (ADR-0023, ADR-0370 section 10). The panel decides nothing about the Filter
// itself: it hands the grid one FilterSpec, and the grid refuses it or applies it.
//
// The DOM is the C# core's built-in panel's, so ex-grid.css and layer 3 read both alike.

import type { DistinctValues } from '../source/grid-source';
import type { ColumnType, DateType } from '../query/columns';
import { filterOperatorsFor, type FilterCombinator, type FilterOperator, type FilterSpec } from '../query/filter';
import {
    canApply, ChosenValues, fromConditions, fromSearchedValueList, initialOperand, initialOperator, initialSecond, initiallyChosen,
    matches, offersAddToFilter, stateOfAll, takesOperand, toggleAll,
} from '../chrome/filter-panel-choices';
import type { GridCulture } from '../chrome/grid-culture';
import { operandText, readOperand, refusalText, type TemporalLike } from '../chrome/operand-reading';

/** What a column's filter panel lays out (ADR-0009): the condition form alone, the safe default, which never
 * asks the source for its values; a value list of the column's distinct values, degraded to the condition
 * form where the source answers TooMany; or both, which the built-in panel shows as the value list. */
export type FilterUi = 'condition' | 'valueList' | 'both';

/** The built-in Chrome's words for a Blank in the value list (ADR-0023). */
const blankWords = '(Blanks)';

/** The column a panel filters. */
export interface FilterPanelColumn {
    readonly name: string;
    readonly type: ColumnType;
    readonly dateType: DateType;
    readonly filterUi: FilterUi;
    /** The text a value's cell shows (ADR-0006): what the value list shows, and what its search matches. */
    readonly textOf: (value: unknown) => string;
}

/** What the grid does for its panel. */
export interface FilterPanelAnswers {
    /** OK or Enter: the panel's answer, which the grid refuses or applies, closing the popover. */
    readonly apply: (spec: FilterSpec | undefined) => void;
    /** Cancel: the popover closes, and nothing is applied. */
    readonly cancel: () => void;
    /** A key on the value list (ADR-0044): whether it was one of Excel's letters, which the grid has answered. */
    readonly letter: (key: string) => boolean;
    /** Tab off either end of the panel: the keyboard goes back to the first command. */
    readonly returnToCommands: () => void;
    /** An input reached the panel and was answered (ADR-0220). */
    readonly answered: () => void;
    /** The value list's answer arrived, or was given up: the grid may stand settled again (ADR-0220). */
    readonly fetched: () => void;
}

/** The panel's working state, none of it applied until OK. */
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
 * A column's filter panel, standing under its commands: the sentinel before it, its body and the sentinel
 * after it, which the grid places in the popover. Built once per opening; its value list, where the column
 * declares one, is asked for once and not again while it stands, since a list that shifted under every tick
 * would be unusable (ADR-0009).
 */
export class FilterPanel {
    /** The sentinel before the panel: Shift+Tab off its first control lands here, and goes back to the commands. */
    readonly lead: HTMLElement;
    /** The panel itself, `.ex-popover-filter`. */
    readonly body: HTMLElement;
    /** The sentinel after the panel: Tab off its last control lands here, and goes back to the commands. */
    readonly trail: HTMLElement;
    readonly #column: FilterPanelColumn;
    readonly #current: FilterSpec | undefined;
    readonly #culture: GridCulture;
    readonly #temporal: TemporalLike | undefined;
    readonly #answers: FilterPanelAnswers;
    readonly #form: HTMLFormElement;
    #fetch: AbortController | undefined;
    readonly #working: Working;
    readonly #startedOn: FilterOperator;
    #first: HTMLElement | undefined;
    #searchField: HTMLInputElement | undefined;
    #operandField: HTMLInputElement | undefined;
    #last: HTMLButtonElement | undefined;
    #ok: HTMLButtonElement | undefined;

    /**
     * Opens the panel over `current`, the column's spec in force. `distinct` asks the source for the column's
     * values; it is asked only where the column declares a value list, and the condition form stands while
     * the answer is on its way, as the C# core's does.
     */
    constructor(doc: Document, column: FilterPanelColumn, current: FilterSpec | undefined, culture: GridCulture,
        temporal: TemporalLike | undefined, distinct: (signal: AbortSignal) => Promise<DistinctValues>, answers: FilterPanelAnswers) {
        if (column.type === 'date' && temporal === undefined) {
            throw new TypeError(`Column '${column.name}' holds dates, and a date typed into its filter is made with Temporal, `
                + 'which the grid was not given: pass `temporal`, the browser\'s Temporal or a polyfill\'s (ADR-0350 section 16).');
        }
        this.#column = column;
        this.#current = current;
        this.#culture = culture;
        this.#temporal = temporal;
        this.#answers = answers;
        const operator = initialOperator(column.type, current, false);
        const second = initialSecond(current);
        this.#startedOn = operator;
        // An operand reopens in a text that reads back as itself (ADR-0006, note of 2026-10-01; tickets 94, 96).
        this.#working = {
            values: undefined, chosen: undefined, search: '', addToCurrent: false,
            operator, operand: operandText(initialOperand(current), culture),
            combinator: second.combinator, second: second.operator, secondOperand: operandText(second.operand, culture),
        };
        this.lead = sentinel(doc, answers);
        this.trail = sentinel(doc, answers);
        this.body = doc.createElement('div');
        this.body.className = 'ex-popover-filter';
        this.#form = doc.createElement('form');
        this.#form.className = 'ex-popover-form';
        this.#form.setAttribute('data-ex-claims', 'submit');
        // Enter is the browser's implicit submission, not a key handler: a form of one text field and no
        // submit button submits on Enter there, and not on the Enter that confirms an IME's candidate, which a
        // composing IME is owed (ADR-0010). Enter is not gated on OK's state: what it submits is refused on the
        // same rule (ticket 76).
        this.#form.addEventListener('submit', event => {
            event.preventDefault();
            this.#answers.answered();
            this.#apply();
        });
        this.body.append(this.#form);
        if (column.filterUi === 'condition') {
            this.#fetch = undefined;
        } else {
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
                this.#answers.fetched();
                throw error;
            });
        }
        this.#build();
    }

    /** Whether the value list is still on its way: the grid stands unsettled meanwhile (ADR-0220). */
    get fetching(): boolean {
        return this.#fetch !== undefined && !this.#fetch.signal.aborted && this.#working.values === undefined;
    }

    /** The keyboard to the panel's first control: Tab on a command (ADR-0044). */
    focusFirst(): void {
        this.#first?.focus();
    }

    /** The keyboard to the panel's last control: Shift+Tab on a command, wrapping (ADR-0044). */
    focusLast(): void {
        this.#last?.focus();
    }

    /** The keyboard to the field a search is typed into, E on a command (ADR-0044): the value list's search
     * box, or the condition's value. */
    focusSearch(): void {
        if (this.#searchField === undefined) this.#operandField?.focus();
        else this.#searchField.focus();
    }

    /** The popover closed: an answer still on its way is given up. */
    close(): void {
        const fetching = this.fetching;
        this.#fetch?.abort();
        if (fetching) this.#answers.fetched();
    }

    // The value list's answer. The condition form stood while it was on its way, and whatever the user chose
    // there meanwhile stays: only an untouched form starts over on TooMany's operator.
    #arrived(answer: DistinctValues): void {
        const working = this.#working;
        working.values = answer;
        if (answer.kind === 'tooMany') {
            if (working.operator === this.#startedOn && working.operand.length === 0) {
                working.operator = initialOperator(this.#column.type, this.#current, true);
            }
        } else {
            working.chosen = initiallyChosen(this.#current, answer);
        }
        // The form is built again; a keyboard that was in it lands on its new first control, not on nothing.
        const doc = this.#form.ownerDocument;
        const held = this.#form.contains(doc.activeElement);
        this.#build();
        if (held) this.focusFirst();
        this.#answers.fetched();
    }

    // The value list as it stands, or none while the condition form stands.
    #list(): { readonly domain: DistinctValues; readonly chosen: ChosenValues } | undefined {
        const { values, chosen } = this.#working;
        return values?.kind === 'values' && chosen !== undefined ? { domain: values, chosen } : undefined;
    }

    // A value as the panel lists it and its search matches it: the text its cell shows, a Blank in words.
    readonly #textOf = (value: unknown): string => (value === null || value === undefined ? blankWords : this.#column.textOf(value));

    #build(): void {
        const doc = this.#form.ownerDocument;
        this.#form.replaceChildren();
        this.#searchField = undefined;
        this.#operandField = undefined;
        const list = this.#list();
        if (list === undefined) this.#buildConditions(doc);
        else this.#buildValueList(doc, list.chosen);
        // Each closes the popover: marked so, as the C# core marks them (ADR-0010, ADR-0044's note of 2026-10-12).
        const actions = doc.createElement('div');
        actions.className = 'ex-popover-actions';
        const ok = button(doc, 'OK', () => { this.#apply(); }, this.#answers);
        const cancel = button(doc, 'Cancel', () => { this.#answers.cancel(); }, this.#answers);
        actions.append(ok, cancel);
        this.#form.append(actions);
        this.#ok = ok;
        this.#last = cancel;
        this.#showCanApply();
    }

    // The value list (ADR-0009): a search, Excel's "Add current selection to filter" while a search stands
    // over a column already filtered by its values, "(Select All)" over the values shown, and a checkbox per
    // value, the Blank among them.
    #buildValueList(doc: Document, chosen: ChosenValues): void {
        const working = this.#working;
        const search = doc.createElement('input');
        search.type = 'search';
        search.placeholder = 'Search';
        search.value = working.search;
        const add = doc.createElement('label');
        add.className = 'ex-popover-add';
        const addBox = doc.createElement('input');
        addBox.type = 'checkbox';
        addBox.checked = working.addToCurrent;
        addBox.addEventListener('change', () => {
            this.#answers.answered();
            working.addToCurrent = addBox.checked;
            this.#showCanApply();
        });
        add.append(addBox, 'Add current selection to filter');
        const entries = doc.createElement('div');
        entries.className = 'ex-popover-list ex-value-list';
        entries.setAttribute('data-ex-claims', 'keydown');
        // Excel's letters act on the list as on the commands (ADR-0044): S, O and C run their command, E goes
        // back to the search box. Every other key is the list's own.
        entries.addEventListener('keydown', event => {
            if (event.key.length !== 1 || event.ctrlKey || event.altKey || event.metaKey || event.isComposing) return;
            this.#answers.answered();
            if (this.#answers.letter(event.key)) event.preventDefault();
        });
        // "(Select All)" over the values shown, with Excel's mixed state: a checkbox role on a button, since a
        // native checkbox's mixed state is a property, not markup (ADR-0009, FL-11). It stays one element as
        // the search narrows the list, so the keyboard on it stays.
        let shown: readonly unknown[] = [];
        const all = doc.createElement('button');
        all.type = 'button';
        all.setAttribute('role', 'checkbox');
        all.className = 'ex-select-all';
        all.addEventListener('click', () => {
            this.#answers.answered();
            toggleAll(shown, chosen);
            fill();
        });
        // A checkbox per value, made once: a search shows those whose text matches, and narrowing it again
        // makes nothing new.
        const listed = this.#listed().map(value => {
            const label = doc.createElement('label');
            const box = doc.createElement('input');
            box.type = 'checkbox';
            box.addEventListener('change', () => {
                this.#answers.answered();
                if (box.checked) chosen.add(value);
                else chosen.delete(value);
                all.setAttribute('aria-checked', ariaChecked(stateOfAll(shown, chosen)));
                this.#showCanApply();
            });
            const text = this.#textOf(value);
            label.append(box, text);
            return { value, label, box, text };
        });
        const fill = (): void => {
            const matching = listed.filter(({ text }) => matches(working.search, text));
            shown = matching.map(({ value }) => value);
            all.setAttribute('aria-checked', ariaChecked(stateOfAll(shown, chosen)));
            all.textContent = working.search.length > 0 ? '(Select All Search Results)' : '(Select All)';
            for (const { value, box } of matching) box.checked = chosen.has(value);
            entries.replaceChildren(all, ...matching.map(({ label }) => label));
            this.#showCanApply();
        };
        search.addEventListener('input', () => {
            this.#answers.answered();
            working.search = search.value;
            showAdd();
            fill();
        });
        const showAdd = (): void => {
            const offered = offersAddToFilter(this.#current, working.search);
            if (offered && !add.isConnected) search.after(add);
            if (!offered) add.remove();
        };
        this.#form.append(search, entries);
        showAdd();
        fill();
        this.#first = search;
        this.#searchField = search;
    }

    // The values the list holds, the domain's, in its order.
    #listed(): readonly unknown[] {
        const list = this.#list();
        return list?.domain.kind === 'values' ? list.domain.values : [];
    }

    // The condition form, as Excel's Custom AutoFilter (ADR-0009): an operator and its value, then a second
    // condition, "(none)" until one is chosen, joined by And or Or. A value that does not read as the column's
    // type is said so under it, by name (ADR-0023).
    #buildConditions(doc: Document): void {
        const working = this.#working;
        const allowed = filterOperatorsFor(this.#column.type);
        const first = operatorSelect(doc, allowed, false, working.operator);
        first.addEventListener('change', () => {
            this.#answers.answered();
            working.operator = operatorOf(first.value, allowed);
        });
        const operand = doc.createElement('input');
        operand.value = working.operand;
        const refusal = refusalLine(doc);
        operand.addEventListener('input', () => {
            this.#answers.answered();
            working.operand = operand.value;
            this.#showRefusal(refusal, operand, working.operand);
        });
        const join = doc.createElement('div');
        join.className = 'ex-popover-join';
        join.setAttribute('role', 'radiogroup');
        // One group per opening and per instance: the root's own id prefix is the grid's, and a name unique to
        // this form is all the radios need.
        const group = `ex-join-${String(++joins)}`;
        for (const combinator of ['and', 'or'] as const) {
            const label = doc.createElement('label');
            const radio = doc.createElement('input');
            radio.type = 'radio';
            radio.name = group;
            radio.checked = working.combinator === combinator;
            radio.addEventListener('change', () => {
                this.#answers.answered();
                if (radio.checked) working.combinator = combinator;
            });
            label.append(radio, combinator === 'and' ? 'And' : 'Or');
            join.append(label);
        }
        const second = operatorSelect(doc, allowed, true, working.second);
        second.classList.add('ex-popover-second');
        const secondOperand = doc.createElement('input');
        secondOperand.className = 'ex-popover-second';
        secondOperand.value = working.secondOperand;
        const secondRefusal = refusalLine(doc);
        const showSecond = (): void => {
            if (working.second !== undefined && takesOperand(working.second)) {
                if (!secondOperand.isConnected) second.after(secondOperand);
                this.#showRefusal(secondRefusal, secondOperand, working.secondOperand);
            } else {
                secondOperand.remove();
                secondRefusal.remove();
            }
        };
        second.addEventListener('change', () => {
            this.#answers.answered();
            working.second = second.value.length === 0 ? undefined : operatorOf(second.value, allowed);
            showSecond();
        });
        secondOperand.addEventListener('input', () => {
            this.#answers.answered();
            working.secondOperand = secondOperand.value;
            this.#showRefusal(secondRefusal, secondOperand, working.secondOperand);
        });
        this.#form.append(first, operand, join, second);
        this.#showRefusal(refusal, operand, working.operand);
        showSecond();
        this.#first = first;
        this.#operandField = operand;
    }

    // Why a typed value is refused, under its field, or nothing where it reads (ADR-0023; tickets 96 to 98).
    #showRefusal(line: HTMLElement, field: HTMLInputElement, text: string): void {
        const words = text.length === 0 ? undefined : refusalText(this.#read(text), text, this.#culture);
        if (words === undefined) {
            line.remove();
            return;
        }
        line.textContent = words;
        if (!line.isConnected) field.after(line);
    }

    #read(text: string): ReturnType<typeof readOperand> {
        return readOperand(this.#column.type, text, this.#culture, this.#column.dateType, this.#temporal);
    }

    // OK shows it is unavailable while the value list's answer keeps nothing (ADR-0009, as Excel's OK). The
    // condition form's OK stays available, and a value that does not read applies nothing when it is pressed,
    // while the form says why.
    #showCanApply(): void {
        const ok = this.#ok;
        if (ok === undefined) return;
        const list = this.#list();
        ok.disabled = list !== undefined && !canApply(this.#valueListAnswer(list.domain, list.chosen));
    }

    #valueListAnswer(domain: DistinctValues, chosen: ChosenValues): FilterSpec | undefined {
        const { search, addToCurrent } = this.#working;
        return fromSearchedValueList(domain, chosen, this.#textOf, search, addToCurrent, this.#current);
    }

    // The working state becomes one FilterSpec, by FilterPanelChoices' rules, and the grid takes it. Each
    // condition's value is read as the column's type: empty is an unfinished condition, left out; a value that
    // does not read applies nothing, and the panel stands.
    #apply(): void {
        const list = this.#list();
        if (list !== undefined) {
            this.#answers.apply(this.#valueListAnswer(list.domain, list.chosen));
            return;
        }
        const { operator, operand, combinator, second, secondOperand } = this.#working;
        const first = this.#operand(operator, operand);
        if (!first.reads) return;
        const other = second === undefined ? { reads: true, value: undefined } : this.#operand(second, secondOperand);
        if (!other.reads) return;
        this.#answers.apply(fromConditions(operator, first.value, combinator, second, other.value));
    }

    #operand(operator: FilterOperator, text: string): { readonly reads: boolean; readonly value: unknown } {
        if (!takesOperand(operator) || text.length === 0) return { reads: true, value: undefined };
        const reading = this.#read(text);
        return { reads: reading.refusal === 'none', value: reading.value };
    }
}

let joins = 0;

function sentinel(doc: Document, answers: FilterPanelAnswers): HTMLElement {
    const element = doc.createElement('span');
    element.className = 'ex-focus-wrap';
    element.tabIndex = 0;
    element.setAttribute('data-ex-claims', 'focus');
    element.addEventListener('focus', () => {
        answers.answered();
        answers.returnToCommands();
    });
    return element;
}

function button(doc: Document, words: string, press: () => void, answers: FilterPanelAnswers): HTMLButtonElement {
    const element = doc.createElement('button');
    element.type = 'button';
    element.className = 'ex-popover-closes';
    element.setAttribute('data-ex-claims', 'click');
    element.textContent = words;
    element.addEventListener('click', () => {
        answers.answered();
        press();
    });
    return element;
}

function refusalLine(doc: Document): HTMLElement {
    const line = doc.createElement('p');
    line.className = 'ex-popover-refusal';
    line.setAttribute('role', 'alert');
    return line;
}

// An operator's select: the operators the core offers the column, and "(none)" first for the second
// condition. Each option shows the operator's name as the C# core's enum names it.
function operatorSelect(doc: Document, allowed: readonly FilterOperator[], none: boolean, chosen: FilterOperator | undefined): HTMLSelectElement {
    const select = doc.createElement('select');
    if (none) select.append(option(doc, '', '(none)'));
    for (const operator of allowed) select.append(option(doc, operator, operator.charAt(0).toUpperCase() + operator.slice(1)));
    select.value = chosen === undefined ? '' : chosen;
    return select;
}

function option(doc: Document, value: string, words: string): HTMLOptionElement {
    const element = doc.createElement('option');
    element.value = value;
    element.textContent = words;
    return element;
}

// The operator an option names: one the core offered, never one a changed option claims (ADR-0009, FN-18).
function operatorOf(value: string, allowed: readonly FilterOperator[]): FilterOperator {
    const found = allowed.find(operator => operator === value);
    if (found === undefined) throw new RangeError(`'${value}' is not an operator this column's filter offers.`);
    return found;
}

function ariaChecked(state: 'checked' | 'unchecked' | 'mixed'): string {
    return state === 'checked' ? 'true' : state === 'unchecked' ? 'false' : 'mixed';
}
