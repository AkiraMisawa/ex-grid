// What a Chrome slot's controls do, wired on markup another framework drew (ADR-0410 section 2): the logic React's
// hooks carry out, as plain DOM functions both adapters call, so that the two cannot drift. A Framework Adapter
// whose contents arrive later than their slot, Blazor's on a circuit, wires them in the browser from attributes the
// contents write: nothing a key means waits on a round trip.

import type { GridMenuSlotContext } from './grid-chrome';

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
