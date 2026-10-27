// The built-in Chrome's filter panel (ADR-0009, ADR-0044): Excel's shape under a column's commands, in the
// same popover. A value list where the column declares one and the source listed its values, and the
// condition form, one or two conditions joined by And or Or, where it declares that or the values were
// TooMany. Everything in it is uncommitted until OK, or Enter in its one text field, and it applies whole,
// through the grid. What its choices mean is FilterPanelChoices' (ADR-0010); what a typed operand reads as
// is the grid's culture's (ADR-0023, ADR-0370 section 10). The panel decides nothing about the Filter
// itself: it hands the grid one FilterSpec, and the grid refuses it or applies it.
//
// The DOM is the C# core's built-in panel's, so ex-grid.css and layer 3 read both alike.

import type { FilterOperator, FilterSpec } from '../query/filter';
import type { FilterPanelCondition, FilterPanelModel, FilterPanelValueList } from '../chrome/filter-panel-model';
import { filterPanelLabelIds, operatorLabelId } from '../chrome/filter-panel-words';

export type { FilterPanelColumn, FilterUi } from '../chrome/filter-panel-model';

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
}

/** A column's filter as the grid holds it while it stands under the commands: the built-in panel, or a Chrome
 * slot's (ADR-0390). The grid places the sentinel before it, its body and the sentinel after it. */
export interface FilterPlace {
    /** The sentinel before the panel: Shift+Tab off its first control lands here, and goes back to the commands. */
    readonly lead: HTMLElement;
    /** The panel itself, `.ex-popover-filter`. */
    readonly body: HTMLElement;
    /** The sentinel after the panel: Tab off its last control lands here, and goes back to the commands. */
    readonly trail: HTMLElement;
    /** Whether the value list is still on its way: the grid stands unsettled meanwhile (ADR-0220). */
    readonly fetching: boolean;
    /** Whether a popup of the contents' own is open: Escape is then the popup's (ADR-0039). */
    readonly innerPopupOpen: boolean;
    /** The popover stands: the panel's contents are drawn. */
    open(): void;
    /** The keyboard to the panel's first control: Tab on a command (ADR-0044). */
    focusFirst(): void;
    /** The keyboard to the panel's last control: Shift+Tab on a command, wrapping (ADR-0044). */
    focusLast(): void;
    /** The keyboard to the field a search is typed into, E on a command (ADR-0044). */
    focusSearch(): void;
    /** The popover closed: the contents go, and an answer still on its way is given up. */
    close(): void;
}

/**
 * A column's filter panel, standing under its commands: the sentinel before it, its body and the sentinel
 * after it, which the grid places in the popover. Built once per opening, drawn from its `FilterPanelModel`,
 * which holds what each choice means.
 */
export class FilterPanel implements FilterPlace {
    readonly lead: HTMLElement;
    readonly body: HTMLElement;
    readonly trail: HTMLElement;
    /** The built-in panel's selects open the browser's own lists, whose Escape never reaches the grid. */
    readonly innerPopupOpen = false;
    /** What the panel holds, uncommitted until OK. */
    readonly model: FilterPanelModel;
    readonly #answers: FilterPanelAnswers;
    readonly #form: HTMLFormElement;
    #built: boolean;
    #first: HTMLElement | undefined;
    #searchField: HTMLInputElement | undefined;
    #operandField: HTMLInputElement | undefined;
    #last: HTMLButtonElement | undefined;
    #ok: HTMLButtonElement | undefined;

    /** Draws `model`, the panel's state for this opening, which holds what each choice means. */
    constructor(doc: Document, model: FilterPanelModel, answers: FilterPanelAnswers) {
        this.#answers = answers;
        this.lead = filterSentinel(doc, answers);
        this.trail = filterSentinel(doc, answers);
        this.body = filterBody(doc);
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
        this.#built = false;
        this.model = model;
        // The source's answer is drawn as it arrives: the form is built again, and a keyboard that was in it lands
        // on its new first control, not on nothing.
        this.model.subscribe(() => {
            if (this.#built === this.model.arrived) return;
            this.#built = this.model.arrived;
            const held = this.#form.contains(this.#form.ownerDocument.activeElement);
            this.#build();
            if (held) this.focusFirst();
        });
        this.#build();
    }

    get fetching(): boolean {
        return this.model.fetching;
    }

    /** Drawn as it is made. */
    open(): void {
        // The form stands from the constructor.
    }

    focusFirst(): void {
        this.#first?.focus();
    }

    focusLast(): void {
        this.#last?.focus();
    }

    /** The value list's search box, or the condition's value. */
    focusSearch(): void {
        if (this.#searchField === undefined) this.#operandField?.focus();
        else this.#searchField.focus();
    }

    close(): void {
        this.model.close();
    }

    #build(): void {
        const doc = this.#form.ownerDocument;
        this.#form.replaceChildren();
        this.#searchField = undefined;
        this.#operandField = undefined;
        const view = this.model.view;
        if (view.kind === 'conditions') this.#buildConditions(doc);
        else this.#buildValueList(doc, view);
        // Each closes the popover: marked so, as the C# core marks them (ADR-0010, ADR-0044's note of 2026-10-12).
        const actions = doc.createElement('div');
        actions.className = 'ex-popover-actions';
        const words = this.model.label;
        const ok = button(doc, words(filterPanelLabelIds.ok), () => { this.#apply(); }, this.#answers);
        const cancel = button(doc, words(filterPanelLabelIds.cancel), () => { this.#answers.cancel(); }, this.#answers);
        actions.append(ok, cancel);
        this.#form.append(actions);
        this.#ok = ok;
        this.#last = cancel;
        this.#showCanApply();
    }

    // The value list (ADR-0009): a search, Excel's "Add current selection to filter" while a search stands
    // over a column already filtered by its values, "(Select All)" over the values shown, and a checkbox per
    // value, the Blank among them.
    #buildValueList(doc: Document, initial: FilterPanelValueList): void {
        const model = this.model;
        const search = doc.createElement('input');
        search.type = 'search';
        search.placeholder = model.label(filterPanelLabelIds.search);
        search.setAttribute('aria-label', search.placeholder);
        search.value = initial.search;
        const add = doc.createElement('label');
        add.className = 'ex-popover-add';
        const addBox = doc.createElement('input');
        addBox.type = 'checkbox';
        addBox.checked = initial.addToCurrent;
        addBox.addEventListener('change', () => {
            this.#answers.answered();
            model.setAddToCurrent(addBox.checked);
            this.#showCanApply();
        });
        add.append(addBox, model.label(filterPanelLabelIds.addToCurrent));
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
        const all = doc.createElement('button');
        all.type = 'button';
        all.setAttribute('role', 'checkbox');
        all.className = 'ex-select-all';
        all.addEventListener('click', () => {
            this.#answers.answered();
            model.toggleAll();
            fill();
        });
        // A checkbox per value, made once: a search shows those whose text matches, and narrowing it again
        // makes nothing new.
        const listed = new Map<unknown, { readonly label: HTMLLabelElement; readonly box: HTMLInputElement }>();
        const boxOf = (value: unknown, text: string): { readonly label: HTMLLabelElement; readonly box: HTMLInputElement } => {
            const made = listed.get(value);
            if (made !== undefined) return made;
            const label = doc.createElement('label');
            const box = doc.createElement('input');
            box.type = 'checkbox';
            box.addEventListener('change', () => {
                this.#answers.answered();
                model.setChecked(value, box.checked);
                const view = model.view;
                if (view.kind === 'valueList') all.setAttribute('aria-checked', ariaChecked(view.selectAll));
                this.#showCanApply();
            });
            label.append(box, text);
            const entry = { label, box };
            listed.set(value, entry);
            return entry;
        };
        const fill = (): void => {
            const view = model.view;
            if (view.kind !== 'valueList') throw new Error('Internal: the value list was drawn over the condition form.');
            all.setAttribute('aria-checked', ariaChecked(view.selectAll));
            all.textContent = model.label(view.search.length > 0 ? filterPanelLabelIds.selectAllSearchResults : filterPanelLabelIds.selectAll);
            const labels = view.entries.map(({ value, text, checked }) => {
                const { label, box } = boxOf(value, text);
                box.checked = checked;
                return label;
            });
            entries.replaceChildren(all, ...labels);
            this.#showCanApply();
        };
        search.addEventListener('input', () => {
            this.#answers.answered();
            model.setSearch(search.value);
            showAdd();
            fill();
        });
        const showAdd = (): void => {
            const view = model.view;
            const offered = view.kind === 'valueList' && view.addOffered;
            if (offered && !add.isConnected) search.after(add);
            if (!offered) add.remove();
        };
        this.#form.append(search, entries);
        showAdd();
        fill();
        this.#first = search;
        this.#searchField = search;
    }

    // The condition form, as Excel's Custom AutoFilter (ADR-0009): an operator and its value, then a second
    // condition, "(none)" until one is chosen, joined by And or Or. A value that does not read as the column's
    // type is said so under it, by name (ADR-0023).
    #buildConditions(doc: Document): void {
        const model = this.model;
        const conditions = (): { readonly first: FilterPanelCondition; readonly second: FilterPanelCondition | undefined } => {
            const view = model.view;
            if (view.kind !== 'conditions') throw new Error('Internal: the condition form was drawn over the value list.');
            return view;
        };
        const view = model.view;
        if (view.kind !== 'conditions') throw new Error('Internal: the condition form was drawn over the value list.');
        const { allowed } = view;
        const first = operatorSelect(doc, allowed, false, view.first.operator, model.label);
        first.addEventListener('change', () => {
            this.#answers.answered();
            model.setOperator(operatorOf(first.value, allowed));
        });
        const operand = doc.createElement('input');
        operand.value = view.first.operand;
        const refusal = refusalLine(doc);
        operand.addEventListener('input', () => {
            this.#answers.answered();
            model.setOperand(operand.value);
            showRefusal(refusal, operand, conditions().first);
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
            radio.checked = view.combinator === combinator;
            radio.addEventListener('change', () => {
                this.#answers.answered();
                if (radio.checked) model.setCombinator(combinator);
            });
            label.append(radio, model.label(combinator === 'and' ? filterPanelLabelIds.and : filterPanelLabelIds.or));
            join.append(label);
        }
        const second = operatorSelect(doc, allowed, true, view.second?.operator, model.label);
        second.classList.add('ex-popover-second');
        const secondOperand = doc.createElement('input');
        secondOperand.className = 'ex-popover-second';
        secondOperand.value = view.second?.operand ?? ''; // oxlint-disable-line ex-grid/no-stand-in-default -- "(none)" has no value typed
        const secondRefusal = refusalLine(doc);
        const showSecond = (): void => {
            const condition = conditions().second;
            if (condition?.takesOperand === true) {
                if (!secondOperand.isConnected) second.after(secondOperand);
                showRefusal(secondRefusal, secondOperand, condition);
            } else {
                secondOperand.remove();
                secondRefusal.remove();
            }
        };
        second.addEventListener('change', () => {
            this.#answers.answered();
            model.setSecond(second.value.length === 0 ? undefined : operatorOf(second.value, allowed));
            showSecond();
        });
        secondOperand.addEventListener('input', () => {
            this.#answers.answered();
            model.setSecondOperand(secondOperand.value);
            const condition = conditions().second;
            if (condition !== undefined) showRefusal(secondRefusal, secondOperand, condition);
        });
        this.#form.append(first, operand, join, second);
        showRefusal(refusal, operand, view.first);
        showSecond();
        this.#first = first;
        this.#operandField = operand;
    }

    // OK shows it is unavailable while the value list's answer keeps nothing (ADR-0009, as Excel's OK). The
    // condition form's OK stays available, and a value that does not read applies nothing when it is pressed,
    // while the form says why.
    #showCanApply(): void {
        const ok = this.#ok;
        if (ok !== undefined) ok.disabled = !this.model.view.canApply;
    }

    // The working state becomes one FilterSpec, and the grid takes it; a value that does not read applies nothing,
    // and the panel stands.
    #apply(): void {
        const answer = this.model.answer();
        if (answer.reads) this.#answers.apply(answer.spec);
    }
}

// Why a typed value is refused, under its field, or nothing where it reads (ADR-0023; tickets 96 to 98).
function showRefusal(line: HTMLElement, field: HTMLInputElement, condition: FilterPanelCondition): void {
    const words = condition.refusal;
    if (words === undefined) {
        line.remove();
        return;
    }
    line.textContent = words;
    if (!line.isConnected) field.after(line);
}

let joins = 0;

/** A sentinel either side of a filter: focused, by Tab off the panel's last control or Shift+Tab off its first,
 * it hands the keyboard back to the commands (ADR-0044). */
export function filterSentinel(doc: Document, answers: Pick<FilterPanelAnswers, 'answered' | 'returnToCommands'>): HTMLElement {
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

/** The element the built-in panel stands in, `.ex-popover-filter`. */
function filterBody(doc: Document): HTMLElement {
    const body = doc.createElement('div');
    body.className = 'ex-popover-filter';
    return body;
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
// condition. Each option shows the operator's name in `words`, the C# core's enum's names in the built-in English.
function operatorSelect(doc: Document, allowed: readonly FilterOperator[], none: boolean, chosen: FilterOperator | undefined,
    words: (id: string) => string): HTMLSelectElement {
    const select = doc.createElement('select');
    if (none) select.append(option(doc, '', words(filterPanelLabelIds.none)));
    for (const operator of allowed) select.append(option(doc, operator, words(operatorLabelId(operator))));
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
