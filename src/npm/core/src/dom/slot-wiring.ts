// What a Chrome slot's controls do, wired on markup another framework drew (ADR-0410 section 2): the logic React's
// hooks carry out, as plain DOM functions both adapters call, so that the two cannot drift. A Framework Adapter
// whose contents arrive later than their slot, Blazor's on a circuit, wires them in the browser from attributes the
// contents write: nothing a key means waits on a round trip.

import type { FilterPanelView } from '../chrome/filter-panel-model';
import type { FilterOperator } from '../query/filter';
import type {
    GridCellEditorSlotContext, GridEditorCompletionSlotContext, GridFilterPanelSlotContext, GridFindPanelSlotContext, GridFormulaBarTextSlotContext,
    GridMenuSlotContext, GridNameBoxSlotContext,
} from './grid-chrome';

/** The attribute a menu slot's contents write on the control of each command, its index in `commands`: the control
 * {@link wireMenuItems} wires. */
export const menuItemAttribute = 'data-ex-menu-item';

/** A key on a menu's item, as a DOM or a framework's event carries it. */
export interface MenuItemKeyEvent {
    /** `KeyboardEvent.key`. */
    readonly key: string;
    /** Whether Shift was held. */
    readonly shiftKey: boolean;
    /** Whether Control was held. */
    readonly ctrlKey: boolean;
    /** Whether Alt was held. */
    readonly altKey: boolean;
    /** Whether Meta was held. */
    readonly metaKey: boolean;
    /** Prevents the key's default. */
    preventDefault(): void;
}

/**
 * Answers a key on one of a menu slot's items (ADR-0039): the core's table against the place it keeps, the key's
 * default prevented. A move focuses the item it names, through `item`; a run invokes the command; a close closes
 * the menu; a move into the column's filter the core has made itself.
 */
export function answerMenuKey(context: GridMenuSlotContext, event: MenuItemKeyEvent, item: (index: number) => HTMLElement): void {
    event.preventDefault();
    const meaning = context.resolveKey(event.key, event.shiftKey, event.ctrlKey || event.altKey || event.metaKey);
    if (meaning.kind === 'move') {
        context.takeFocus(item(meaning.item));
    } else if (meaning.kind === 'run') {
        const run = context.commands[meaning.item];
        if (run === undefined) throw new Error(`Internal: the core ran item ${meaning.item} of ${context.commands.length}.`);
        run.invoke();
    } else if (meaning.kind === 'close') {
        context.close();
    }
}

/** Answers a menu slot's focus request: the item it asks for takes the keyboard, without scrolling the menu
 * (ADR-0039, ADR-0410 section 5). */
export function takeMenuFocus(context: GridMenuSlotContext, item: (index: number) => HTMLElement): void {
    if (context.focusItem >= 0) context.takeFocus(item(context.focusItem), { preventScroll: true });
}

/**
 * Wires the items a menu slot's contents drew in `contents`, each control marked with {@link menuItemAttribute}
 * naming its command's index, one for every command (ADR-0410 section 2): each takes the menu's role, is focusable
 * by the menu and never a tab stop, is shown disabled where its command is, and is numbered in the Input Order by
 * the click and the keys it claims (ADR-0240); every key on it is {@link answerMenuKey}'s, a press runs its command
 * and a focus moves the menu's place. The first enabled item takes the keyboard as the context asks, unless
 * `answered` says this context's focus request was answered already, by an earlier wiring of the same contents.
 * Answers the function that takes the wiring off again.
 */
export function wireMenuItems(contents: HTMLElement, context: GridMenuSlotContext, answered = false): () => void {
    const items = new Map<number, HTMLElement>();
    for (const control of contents.querySelectorAll<HTMLElement>(`[${menuItemAttribute}]`)) {
        const index = Number(control.getAttribute(menuItemAttribute));
        if (!Number.isSafeInteger(index) || index < 0 || index >= context.commands.length || items.has(index)) {
            // oxlint-disable-next-line ex-grid/no-stand-in-default -- the control was found by the attribute, so it carries one
            throw new RangeError(`A menu's item is marked ${menuItemAttribute}="${control.getAttribute(menuItemAttribute) ?? ''}": each of the menu's ${context.commands.length} commands is marked once, by its index.`);
        }
        items.set(index, control);
    }
    const item = (index: number): HTMLElement => {
        const control = items.get(index);
        if (control === undefined) throw new Error(`The menu's item ${index} is not drawn: mark the control of every command with ${menuItemAttribute}.`);
        return control;
    };
    const unwire: (() => void)[] = [];
    for (const [index, control] of items) {
        const command = context.commands[index];
        if (command === undefined) throw new Error(`Internal: the menu's item ${index} has no command.`);
        control.setAttribute('role', 'menuitem');
        control.tabIndex = -1;
        control.setAttribute('aria-disabled', String(!command.enabled));
        if (command.enabled) control.setAttribute('data-ex-claims', 'click keydown');
        else control.removeAttribute('data-ex-claims');
        const keydown = (event: KeyboardEvent): void => { answerMenuKey(context, event, item); };
        const click = (): void => { if (command.enabled) command.invoke(); };
        const focus = (): void => { context.itemFocused(index); };
        control.addEventListener('keydown', keydown);
        control.addEventListener('click', click);
        control.addEventListener('focus', focus);
        unwire.push(() => {
            control.removeEventListener('keydown', keydown);
            control.removeEventListener('click', click);
            control.removeEventListener('focus', focus);
        });
    }
    if (!answered) takeMenuFocus(context, item);
    return () => { for (const each of unwire) each(); };
}

/**
 * The attributes a filter panel slot's contents write on their controls, which {@link wireFilterPanel} wires
 * (ADR-0410 section 2). Each is written on the control itself or on an element holding it, as a component library
 * places its attributes.
 */
export const filterPanelAttributes = Object.freeze({
    /** The search box: its text is the search. */
    search: 'data-ex-filter-search',
    /** "Add current selection to filter": a checkbox. */
    addToCurrent: 'data-ex-filter-add',
    /** The value list, whose keys are Excel's letters (ADR-0044). */
    values: 'data-ex-filter-values',
    /** "(Select All)": a checkbox. */
    selectAll: 'data-ex-filter-all',
    /** A value's checkbox, naming its index among the entries of the view the contents drew. */
    value: 'data-ex-filter-value',
    /** A condition's operator, `first` or `second`: a `select` whose options' values are the view's operators, and
     * the second's empty option "(none)". */
    operator: 'data-ex-filter-operator',
    /** A condition's value, `first` or `second`: a text field whose text is the operand. */
    operand: 'data-ex-filter-operand',
    /** How the conditions are joined, `and` or `or`: a radio button. */
    join: 'data-ex-filter-join',
    /** Cancel: a button. OK is a submit button, which submits the panel's form. */
    cancel: 'data-ex-filter-cancel',
    /** The controls a focus request may name, separated by spaces: `first`, `last` and `search`. */
    focus: 'data-ex-filter-focus',
} as const);

/** A key on a filter panel's value list, as a DOM or a framework's event carries it. */
export interface ValueListKeyEvent extends MenuItemKeyEvent {
    /** Whether an IME is composing: such a key is the IME's. */
    readonly isComposing: boolean;
}

/** Answers a key on a filter panel's value list (ADR-0044): one of Excel's letters is the core's, its default
 * prevented. A key composing is the IME's. */
export function answerValueListKey(context: GridFilterPanelSlotContext, event: ValueListKeyEvent): void {
    if (event.isComposing) return;
    if (context.valueListKey(event.key, event.ctrlKey || event.altKey || event.metaKey)) event.preventDefault();
}

/**
 * Answers a filter panel slot's focus request: the control it names takes the keyboard (ADR-0044, ADR-0204). `control`
 * answers the control drawn for a target, or `undefined` where none is: the search target has none in a condition form
 * whose first operator takes no value, and asks nothing then, as the built-in panel does; a first or last control not
 * drawn is refused by name.
 */
export function takeFilterFocus(context: GridFilterPanelSlotContext, control: (target: 'first' | 'last' | 'search') => HTMLElement | undefined): void {
    const target = context.focusTarget;
    if (target === undefined) return;
    const found = control(target);
    if (found !== undefined) context.takeFocus(found);
    else if (target !== 'search') throw new Error(`The filter panel's ${target} control is not drawn: the core asks the keyboard onto it.`);
}

/**
 * Wires the filter panel a slot's contents drew in `contents` from {@link filterPanelAttributes} (ADR-0410 section
 * 2): each change made in a control goes to the core at once, in the browser, a value's checkbox naming the value of
 * the entry it was drawn for in `drawn`, the view the contents show; Cancel cancels, and the value list's letters are
 * {@link answerValueListKey}'s. The control the context's focus request names takes the keyboard, unless `answered`
 * says an earlier wiring of the same contents answered it. Answers the function that takes the wiring off again.
 */
export function wireFilterPanel(contents: HTMLElement, context: GridFilterPanelSlotContext, drawn: () => FilterPanelView, answered = false): () => void {
    const marked = (target: EventTarget | null, attribute: string): Marked | undefined => markedIn(contents, target, attribute);
    const input = (event: Event): void => {
        const field = event.target;
        if (!(field instanceof HTMLInputElement || field instanceof HTMLTextAreaElement)) return;
        if (marked(field, filterPanelAttributes.search) !== undefined) {
            context.setSearch(field.value);
            return;
        }
        const operand = marked(field, filterPanelAttributes.operand);
        if (operand === undefined) return;
        if (which(operand.value, filterPanelAttributes.operand) === 'first') context.setOperand(field.value);
        else context.setSecondOperand(field.value);
    };
    const change = (event: Event): void => {
        const control = event.target;
        if (control instanceof HTMLSelectElement) {
            const operator = marked(control, filterPanelAttributes.operator);
            if (operator === undefined) return;
            const view = drawn();
            if (view.kind !== 'conditions') throw new Error('A filter panel\'s operator changed while the value list stands.');
            const chosen = control.value;
            if (which(operator.value, filterPanelAttributes.operator) === 'first') context.setOperator(allowedOperator(chosen, view.allowed));
            else context.setSecond(chosen === '' ? undefined : allowedOperator(chosen, view.allowed));
            return;
        }
        if (!(control instanceof HTMLInputElement)) return;
        if (marked(control, filterPanelAttributes.selectAll) !== undefined) {
            context.toggleAll();
            return;
        }
        if (marked(control, filterPanelAttributes.addToCurrent) !== undefined) {
            context.setAddToCurrent(control.checked);
            return;
        }
        const join = marked(control, filterPanelAttributes.join);
        if (join !== undefined) {
            if (!control.checked) return;
            if (join.value !== 'and' && join.value !== 'or') throw new RangeError(`A filter panel's radio button is marked ${filterPanelAttributes.join}="${join.value}": it joins with and or or.`);
            context.setCombinator(join.value);
            return;
        }
        const value = marked(control, filterPanelAttributes.value);
        if (value === undefined) return;
        // The entry the checkbox was drawn for: the value the user saw, though the core's list may have moved since.
        const view = drawn();
        const index = Number(value.value);
        const entry = view.kind === 'valueList' && Number.isSafeInteger(index) ? view.entries[index] : undefined;
        if (entry === undefined) throw new RangeError(`A filter panel's checkbox is marked ${filterPanelAttributes.value}="${value.value}", which names no entry of the value list it was drawn with.`);
        context.setChecked(entry.value, control.checked);
    };
    const click = (event: MouseEvent): void => {
        if (marked(event.target, filterPanelAttributes.cancel) !== undefined) context.cancel();
    };
    const keydown = (event: KeyboardEvent): void => {
        const target = event.target;
        if (target instanceof HTMLInputElement && target.type !== 'checkbox' && target.type !== 'radio') return;
        if (target instanceof HTMLTextAreaElement || target instanceof HTMLSelectElement) return;
        if (marked(target, filterPanelAttributes.values) !== undefined) answerValueListKey(context, event);
    };
    contents.addEventListener('input', input);
    contents.addEventListener('change', change);
    contents.addEventListener('click', click);
    contents.addEventListener('keydown', keydown);
    if (!answered) {
        takeFilterFocus(context, target => [...contents.querySelectorAll<HTMLElement>(`[${filterPanelAttributes.focus}]`)]
            // oxlint-disable-next-line ex-grid/no-stand-in-default -- the element was found by the attribute, so it carries one
            .find(each => (each.getAttribute(filterPanelAttributes.focus) ?? '').split(' ').includes(target)));
    }
    return () => {
        contents.removeEventListener('input', input);
        contents.removeEventListener('change', change);
        contents.removeEventListener('click', click);
        contents.removeEventListener('keydown', keydown);
    };
}

/** A control found by the attribute it, or an element holding it, carries, and the attribute's value. */
interface Marked {
    readonly element: Element;
    readonly value: string;
}

/** The element at or around `target`, inside `contents`, that carries `attribute`. */
function markedIn(contents: HTMLElement, target: EventTarget | null, attribute: string): Marked | undefined {
    if (!(target instanceof Element)) return undefined;
    const element = target.closest(`[${attribute}]`);
    if (element === null || !contents.contains(element)) return undefined;
    // oxlint-disable-next-line ex-grid/no-stand-in-default -- the element was found by the attribute, so it carries one
    return { element, value: element.getAttribute(attribute) ?? '' };
}

/** Which condition a control marked `attribute` is for. */
function which(value: string, attribute: string): 'first' | 'second' {
    if (value === 'first' || value === 'second') return value;
    throw new RangeError(`A filter panel's control is marked ${attribute}="${value}": it is the first condition's or the second's.`);
}

/** The operator an option names: one the core offered, never one a changed option claims (ADR-0009, FN-18). */
function allowedOperator(value: string, allowed: readonly FilterOperator[]): FilterOperator {
    const found = allowed.find(operator => operator === value);
    if (found === undefined) throw new RangeError(`'${value}' is not an operator this column's filter offers.`);
    return found;
}

/**
 * The attributes a find panel slot's contents write on their controls, which {@link wireFindPanel} wires (ADR-0410
 * section 2). Each is written on the control itself or on an element holding it, as a component library places its
 * attributes.
 */
export const findPanelAttributes = Object.freeze({
    /** A text field, `find` or `replace`: its text is what to look for, or what Replace writes. The `find` field is
     * the one each focus request puts the keyboard on. */
    field: 'data-ex-find-field',
    /** An option's checkbox, `matchCase` or `wholeCell`. */
    option: 'data-ex-find-option',
    /** A button that steps or writes, `next`, `previous`, `replace` or `replaceAll`. Enter in a field submits the
     * core's form, which steps, so no button need be a submit button. */
    step: 'data-ex-find-step',
} as const);

/** Answers a find panel slot's focus request: the find field takes the keyboard, its text selected where the request
 * selects it (ADR-0055, FD-1). A find field not drawn is refused by name. */
export function takeFindFocus(context: GridFindPanelSlotContext, field: HTMLElement | null | undefined): void {
    if (field === null || field === undefined) throw new Error("The find panel's field is not drawn: each request of the core's puts the keyboard on it.");
    context.takeFocus(field);
}

/**
 * Wires the find panel a slot's contents drew in `contents` from {@link findPanelAttributes} (ADR-0410 section 2):
 * each text typed and option ticked goes to the core at once, in the browser, and each button steps or writes. The
 * find field takes the keyboard for the context's focus request, unless `answered` says an earlier wiring of the
 * same contents answered it. Answers the function that takes the wiring off again.
 */
export function wireFindPanel(contents: HTMLElement, context: GridFindPanelSlotContext, answered = false): () => void {
    const input = (event: Event): void => {
        const field = event.target;
        if (!(field instanceof HTMLInputElement || field instanceof HTMLTextAreaElement)) return;
        const marked = markedIn(contents, field, findPanelAttributes.field);
        if (marked === undefined) return;
        if (marked.value === 'find') context.setText(field.value);
        else if (marked.value === 'replace') context.setReplaceText(field.value);
        else throw new RangeError(`A find panel's field is marked ${findPanelAttributes.field}="${marked.value}": it is find or replace.`);
    };
    const change = (event: Event): void => {
        const control = event.target;
        if (!(control instanceof HTMLInputElement)) return;
        const marked = markedIn(contents, control, findPanelAttributes.option);
        if (marked === undefined) return;
        if (marked.value === 'matchCase') context.setMatchCase(control.checked);
        else if (marked.value === 'wholeCell') context.setWholeCell(control.checked);
        else throw new RangeError(`A find panel's option is marked ${findPanelAttributes.option}="${marked.value}": it is matchCase or wholeCell.`);
    };
    const click = (event: MouseEvent): void => {
        const marked = markedIn(contents, event.target, findPanelAttributes.step);
        if (marked === undefined) return;
        switch (marked.value) {
            case 'next': context.next(); break;
            case 'previous': context.previous(); break;
            case 'replace': context.replace(); break;
            case 'replaceAll': context.replaceAll(); break;
            default: throw new RangeError(`A find panel's button is marked ${findPanelAttributes.step}="${marked.value}": it is next, previous, replace or replaceAll.`);
        }
    };
    contents.addEventListener('input', input);
    contents.addEventListener('change', change);
    contents.addEventListener('click', click);
    if (!answered) takeFindFocus(context, contents.querySelector<HTMLElement>(`[${findPanelAttributes.field}="find"]`));
    return () => {
        contents.removeEventListener('input', input);
        contents.removeEventListener('change', change);
        contents.removeEventListener('click', click);
    };
}

/** The attribute the Cell Editor slot's contents write on their one text field, an `input` or a `textarea`: the field
 * {@link wireCellEditor} hands the core: the input or the textarea itself, not an element holding it. */
export const editorFieldAttribute = 'data-ex-editor-field';

/** The field each editor's contents were last wired with, so a wiring of the same field again asks nothing. */
const handedEditorFields = new WeakMap<GridCellEditorSlotContext, HTMLElement>();

/**
 * Wires the Cell Editor slot's contents (ADR-0410 section 2), as `useCellEditor` does React's: the one field marked with
 * {@link editorFieldAttribute} in `contents` is handed to the core, which reads and writes it and gives it the
 * keyboard the opening owes, unless `answered` says an earlier wiring answered that already, or that the contents are
 * not yet the ones the request is for. A field drawn anew after one was handed, an `input` swapped for a `textarea`
 * say, is handed over too, so the core reads it and it keeps the keyboard. Draw the
 * field with the text the editor opens with and nothing bound after: the core's text outranks any render's. Answers
 * the function that takes the wiring off again, which has nothing to take: the core hears the field itself.
 */
export function wireCellEditor(contents: HTMLElement, context: GridCellEditorSlotContext, answered = false): () => void {
    const field = markedField(contents, editorFieldAttribute, 'cell editor', true);
    const handed = handedEditorFields.get(context);
    // Contents not yet the ones the request is for take nothing: a later wiring hands the field over.
    if (!answered || (handed !== undefined && handed !== field)) {
        handedEditorFields.set(context, field);
        context.takeFocus(field);
    }
    return () => undefined;
}

/** The attribute the Formula Bar's text slot's contents write on their one text field, an `input` or a `textarea`: the
 * field {@link wireFormulaBarText} hands the core. */
export const formulaBarFieldAttribute = 'data-ex-formula-bar-field';

/** The attribute the Name Box slot's contents write on their one `input`: the field {@link wireNameBox} hands the
 * core. */
export const nameBoxFieldAttribute = 'data-ex-name-box-field';

/** The one field marked with `attribute` in `contents`, an `input`, or where `textarea` allows, a `textarea`: the field
 * itself, so a field drawn anew is one marked anew, never one swapped inside an element kept. Refused by `name` where
 * the contents mark none, several, or another element. */
function markedField(contents: HTMLElement, attribute: string, name: string, textarea: boolean): HTMLElement {
    const fields = contents.querySelectorAll<HTMLElement>(`[${attribute}]`);
    const field = fields[0];
    if (fields.length !== 1 || field === undefined) throw new Error(`The ${name}'s contents drew ${fields.length} fields marked ${attribute}: mark the one text field.`);
    if (field.tagName !== 'INPUT' && !(textarea && field.tagName === 'TEXTAREA')) {
        throw new Error(`The ${name}'s contents marked a ${field.tagName.toLowerCase()} with ${attribute}: mark the ${textarea ? 'input or the textarea' : 'input'} itself.`);
    }
    return field;
}

/**
 * Wires the Formula Bar's text slot's contents (ADR-0410 section 2), as `useFormulaBarText` does React's: the one field
 * marked with {@link formulaBarFieldAttribute} in `contents`, an `input` or a `textarea`, is handed to the core, which
 * reads and writes its text, names it, sets its `readOnly` and gives it the keyboard where the user's work asks. A field
 * drawn anew is handed over too. Draw it with no value and no `readonly`, which are the core's, and no key handler.
 * Answers the function that takes the wiring off again, which has nothing to take.
 */
export function wireFormulaBarText(contents: HTMLElement, context: GridFormulaBarTextSlotContext): () => void {
    // The core takes a field it already holds as nothing new.
    context.takeField(markedField(contents, formulaBarFieldAttribute, 'Formula Bar\'s text', true));
    return () => undefined;
}

/**
 * Wires the Name Box slot's contents (ADR-0410 section 2), as `useNameBox` does React's: the one `input` marked with
 * {@link nameBoxFieldAttribute} in `contents` is handed to the core, which writes the Focus's name into it, names it and
 * hears what is typed and entered. A field drawn anew is handed over too. Draw it with no value and no key handler.
 * Answers the function that takes the wiring off again, which has nothing to take.
 */
export function wireNameBox(contents: HTMLElement, context: GridNameBoxSlotContext): () => void {
    context.takeField(markedField(contents, nameBoxFieldAttribute, 'Name Box', false));
    return () => undefined;
}

/** The attributes the completion slot's contents write (ADR-0410 section 2): `list` on the one element holding the
 * candidates, drawn where there are any, and `candidate` on each candidate's element, its index in `candidates`, as the
 * core's own list marks its options. */
export const completionAttributes = Object.freeze({
    list: 'data-ex-completion-list',
    candidate: 'data-ex-candidate',
} as const);

/**
 * Wires the completion slot's contents (ADR-0410 section 2), as `useEditorCompletion` does React's: the list marked
 * {@link completionAttributes}`.list` takes the list's id and role, and each candidate marked
 * {@link completionAttributes}`.candidate` its option's id and role and whether it is the one chosen, in the browser, so
 * ↑ and ↓ move the choice without the contents being drawn again. A click on a candidate accepts it, while `current`
 * says the contents show the answer `context` is for: a click on contents still showing an earlier answer accepts
 * nothing, as the core refuses an answer no longer its own. Draw the candidates and the hint with no handler and no
 * choice of your own. Answers the function that takes the wiring off again.
 */
export function wireEditorCompletion(contents: HTMLElement, context: GridEditorCompletionSlotContext, current: () => boolean): () => void {
    const lists = contents.querySelectorAll<HTMLElement>(`[${completionAttributes.list}]`);
    if (lists.length > 1) throw new Error(`The completion's contents drew ${lists.length} lists marked ${completionAttributes.list}: mark the one holding the candidates.`);
    const list = lists[0];
    if (list !== undefined) {
        list.id = context.listId;
        list.setAttribute('role', 'listbox');
    }
    const shown = current();
    for (const option of contents.querySelectorAll<HTMLElement>(`[${completionAttributes.candidate}]`)) {
        const index = candidateIndex(option, shown ? context.candidates.length : undefined);
        option.id = context.optionId(index);
        option.setAttribute('role', 'option');
        option.setAttribute('aria-selected', index === context.selected ? 'true' : 'false');
    }
    const click = (event: MouseEvent): void => {
        if (event.button !== 0 || !(event.target instanceof Element)) return;
        const option = event.target.closest<HTMLElement>(`[${completionAttributes.candidate}]`);
        if (option === null || !contents.contains(option) || !current()) return;
        context.accept(candidateIndex(option, context.candidates.length));
    };
    contents.addEventListener('click', click);
    return () => { contents.removeEventListener('click', click); };
}

/** The index a candidate's element is marked with, refused where it names no candidate: within `count` where the
 * contents show the answer it counts. */
function candidateIndex(option: HTMLElement, count: number | undefined): number {
    const marked = option.getAttribute(completionAttributes.candidate);
    const index = Number(marked);
    if (marked === null || marked === '' || !Number.isSafeInteger(index) || index < 0 || (count !== undefined && index >= count)) {
        throw new RangeError(`A completion candidate is marked ${completionAttributes.candidate}="${String(marked)}": it is the candidate's index.`);
    }
    return index;
}
