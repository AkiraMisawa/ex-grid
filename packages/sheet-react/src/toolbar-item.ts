// What a Chrome's Toolbar Item needs to stay thin (ADR-0390 section 3, ADR-0100): the item's view as the Sheet holds
// it, its list opened and closed on the toolbar's requests, and the list's keys, which are the built-in list's: the
// arrows, Home and End move among the choices that can be made, swatches ten across; Enter or Space makes one; Tab keeps
// the keyboard where it is; Escape closes the list. A choice or Escape is handed back while the keyboard is still in the
// list, and the Sheet takes it on; a press outside the item closes it. What an item means is the Sheet's; a Wrapper
// draws the controls and the list's frame, and hands every press and key to these handles, so the same keys do the
// same thing under every Chrome.
//
// Every control takes no Tab and no DOM focus on a pointer press, so the toolbar stays one Tab stop and the keyboard
// stays on the Sheet, as Excel's ribbon leaves it in the cell, and claims its click (data-ex-claims), so a press made
// while an opening is still being answered waits its turn (ADR-0240).

import {
    useCallback, useLayoutEffect, useRef, useState, useSyncExternalStore, type KeyboardEvent as ReactKeyboardEvent, type MouseEvent as ReactMouseEvent,
} from 'react';
import {
    toolbarChoiceMovedTo, toolbarChoiceRows, toolbarChoiceStart, type SheetToolbarItemSlotContext, type SheetToolbarItemView, type ToolbarChoiceRow,
} from '@ex-grid/sheet';

export type { ToolbarChoiceRow } from '@ex-grid/sheet';

/** What every control of a Toolbar Item takes: no Tab, no keyboard on a press, its click claimed, and its name and
 * whether it can be pressed as the Sheet says. */
export interface ToolbarControlProps {
    /** Out of the Tab order: the toolbar is one Tab stop, its keys the Sheet's. */
    readonly tabIndex: -1;
    /** Its click waits its turn behind a commit or an opening (ADR-0240). */
    readonly 'data-ex-claims': 'click';
    /** Whether it cannot be pressed now, as the Sheet says. */
    readonly disabled: boolean;
    /** The tooltip, with why it cannot be pressed while it cannot. */
    readonly title: string;
    /** Keeps the keyboard where it is on a pointer press. */
    readonly onMouseDown: (event: ReactMouseEvent) => void;
}

/** What the element that holds a Toolbar Item's choices takes: the keyboard as it is drawn, and the list's keys. */
export interface ToolbarChoiceListProps {
    /** Hands the keyboard to the Focus cell's choice as the list is drawn. */
    readonly ref: (element: HTMLElement | null) => void;
    /** A menu, as the built-in list is. */
    readonly role: 'menu';
    /** The item's name, which names the list. */
    readonly 'aria-label': string;
    /** The list's keys, the built-in list's: none of them reaches the Sheet or the design system's own list. */
    readonly onKeyDown: (event: ReactKeyboardEvent) => void;
}

/** What each choice's control takes: its place among the list's keys, whether it is the Focus cell's, and its press.
 * A choice that cannot be made says so in `aria-disabled` and its title, and stays where the pointer can reach it. */
export interface ToolbarChoiceProps {
    /** Its place among the list's keys. */
    readonly ref: (element: HTMLElement | null) => void;
    /** One of the list's radio choices. */
    readonly role: 'menuitemradio';
    /** Whether the Focus cell has it. */
    readonly 'aria-checked': boolean;
    /** Present where it cannot be made now. */
    readonly 'aria-disabled'?: true;
    /** Out of the Tab order: the list's keys move among the choices. */
    readonly tabIndex: -1;
    /** Its click and keys wait their turn behind a commit or an opening (ADR-0240). */
    readonly 'data-ex-claims': 'click keydown';
    /** Its name, with why it cannot be made while it cannot. */
    readonly title: string;
    /** Makes the choice, where it can be made, and closes the list. */
    readonly onClick: () => void;
}

/** What {@link useToolbarItem} hands a Toolbar Item's contents. */
export interface ToolbarItemHandles {
    /** The ref of the element that holds the item's controls and its list: a press outside it closes the list. */
    readonly item: (element: HTMLElement | null) => void;
    /** What the item shows now, drawn again whenever it changes. */
    readonly view: SheetToolbarItemView;
    /** Whether the item has a list: a menu or a split control. */
    readonly hasList: boolean;
    /** Whether its list is open: the frame shows the choices while it is. */
    readonly open: boolean;
    /** What each of the item's controls takes. */
    readonly control: ToolbarControlProps;
    /** A press on the control the list opens from: opens it, or closes it while it is open. */
    readonly toggleList: () => void;
    /** What the element holding the choices takes. */
    readonly list: ToolbarChoiceListProps;
    /** The list's rows: the choices one to a row, and the swatches ten across. */
    readonly rows: readonly ToolbarChoiceRow[];
    /** What the control of the choice at `index` takes. */
    readonly choice: (index: number) => ToolbarChoiceProps;
}

/** Keeps the keyboard where it is on a pointer press. */
function keepKeyboard(event: ReactMouseEvent): void {
    event.preventDefault();
}

/**
 * Draws a Toolbar Item from its slot's context (ADR-0100, ADR-0390): its view, its list's opening and closing, and the
 * list's keys. Put `item` on the element holding the item and its list, spread `control` on every control the item
 * draws, open the list from `toggleList`, and show the choices in an element taking `list` while `open` holds, laid out
 * by `rows`, one control per choice taking `choice(index)`. Keep the list inside that element and the Sheet's, never
 * in a portal, so the Sheet hears its keys.
 */
export function useToolbarItem(context: SheetToolbarItemSlotContext): ToolbarItemHandles {
    const view = useSyncExternalStore(context.subscribe, context.view);
    const [open, setOpen] = useState(false);
    const answered = useRef({ open: 0, close: 0 });
    const holder = useRef<HTMLElement | null>(null);
    const listElement = useRef<HTMLElement | null>(null);
    const options = useRef<(HTMLElement | null)[]>([]);
    // One ref per choice for the item's life, so a render while the list is open does not take each away and back.
    const optionRefs = useRef<((element: HTMLElement | null) => void)[]>([]);
    // Read by the list as it is drawn, which is once per opening, whatever the view is then.
    const shown = useRef(view);
    shown.current = view;
    const enabled = view.disabledReason === undefined;
    const hasList = context.kind === 'menu' || context.kind === 'split';
    // The toolbar's requests: one to close reaches the list asked for, or any opened by a press; one to open opens it,
    // or, where it cannot open, is closed at once choosing nothing.
    useLayoutEffect(() => {
        const seen = answered.current;
        if (view.closeRequest !== seen.close) {
            answered.current = { open: view.openRequest, close: view.closeRequest };
            setOpen(false);
            return;
        }
        if (view.openRequest === seen.open) return;
        answered.current = { open: view.openRequest, close: seen.close };
        if (hasList && enabled && view.choices.length > 0) setOpen(true);
        else context.choicesClosed(undefined, false);
    }, [view, context, hasList, enabled]);
    const focusAt = (index: number | undefined): void => { if (index !== undefined) options.current[index]?.focus({ preventScroll: true }); };
    // The list takes the keyboard as it is drawn.
    const listRef = useCallback((element: HTMLElement | null) => {
        listElement.current = element;
        if (element !== null) focusAt(toolbarChoiceStart(shown.current.choices));
    }, []);
    const choose = (index: number): void => {
        context.choicesClosed(index, false);
        setOpen(false);
    };
    // A press elsewhere closes the list; the Sheet hears it only while the list holds the keyboard, which then goes
    // back to it, and a control pressed outside keeps the keyboard it took.
    const dismiss = useCallback(() => {
        if (listElement.current?.contains(document.activeElement) === true) context.choicesClosed(undefined, false);
        setOpen(false);
    }, [context]);
    // A press outside the item closes its list, heard from the commit that opens it, so a press made at once after
    // the opening is heard too (the spine's sixth rule).
    useLayoutEffect(() => {
        if (!open) return undefined;
        const outside = (event: Event): void => {
            if (event.target instanceof Node && holder.current?.contains(event.target) !== true) dismiss();
        };
        document.addEventListener('mousedown', outside, true);
        document.addEventListener('touchstart', outside, true);
        return () => {
            document.removeEventListener('mousedown', outside, true);
            document.removeEventListener('touchstart', outside, true);
        };
    }, [open, dismiss]);
    const item = useCallback((element: HTMLElement | null) => { holder.current = element; }, []);
    const optionRef = (index: number): ((element: HTMLElement | null) => void) => {
        const made = optionRefs.current[index];
        if (made !== undefined) return made;
        const ref = (element: HTMLElement | null): void => { options.current[index] = element; };
        optionRefs.current[index] = ref;
        return ref;
    };
    const onKeyDown = (event: ReactKeyboardEvent): void => {
        const at = options.current.findIndex(option => option === document.activeElement);
        event.preventDefault();
        event.stopPropagation();
        if (event.key === 'Escape') {
            context.choicesClosed(undefined, true);
            setOpen(false);
        } else if (event.key === 'Enter' || event.key === ' ') {
            if (at >= 0 && view.choices[at]?.disabledReason === undefined) choose(at);
        } else focusAt(toolbarChoiceMovedTo(event.key, at < 0 ? -1 : at, view.choices));
    };
    return {
        item, view, hasList, open,
        control: {
            tabIndex: -1, 'data-ex-claims': 'click', disabled: !enabled, title: enabled ? view.tooltip : `${view.tooltip}: ${view.disabledReason}`,
            onMouseDown: keepKeyboard,
        },
        toggleList: () => {
            if (open) dismiss();
            else if (enabled && view.choices.length > 0) setOpen(true);
        },
        list: { ref: listRef, role: 'menu', 'aria-label': view.name, onKeyDown },
        rows: toolbarChoiceRows(view.choices),
        choice: index => {
            const choice = view.choices[index];
            if (choice === undefined) throw new RangeError(`The list of ${view.name} has no choice ${String(index)} (ADR-0100).`);
            const disabled = choice.disabledReason !== undefined;
            return {
                ref: optionRef(index), role: 'menuitemradio', 'aria-checked': choice.selected,
                ...(disabled ? { 'aria-disabled': true } : {}), tabIndex: -1, 'data-ex-claims': 'click keydown',
                title: disabled ? `${choice.label}: ${choice.disabledReason}` : choice.label,
                onClick: () => { if (!disabled) choose(index); },
            };
        },
    };
}
