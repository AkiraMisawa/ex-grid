// What a Chrome slot's controls do, wired on markup another framework drew (ADR-0410 section 2): the logic React's
// hooks carry out, as plain DOM functions both adapters call, so that the two cannot drift. A Framework Adapter
// whose contents arrive later than their slot, Blazor's on a circuit, wires them in the browser from attributes the
// contents write: nothing a key means waits on a round trip.

import type { FilterPanelView } from '../chrome/filter-panel-model';
import type { FilterOperator } from '../query/filter';
import type { GridFilterPanelSlotContext, GridMenuSlotContext } from './grid-chrome';

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
    const marked = (target: EventTarget | null, attribute: string): { readonly element: Element; readonly value: string } | undefined => {
        if (!(target instanceof Element)) return undefined;
        const element = target.closest(`[${attribute}]`);
        if (element === null || !contents.contains(element)) return undefined;
        // oxlint-disable-next-line ex-grid/no-stand-in-default -- the element was found by the attribute, so it carries one
        return { element, value: element.getAttribute(attribute) ?? '' };
    };
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
