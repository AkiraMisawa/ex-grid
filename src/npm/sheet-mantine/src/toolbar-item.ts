// A Toolbar Item of the Sheet Toolbar in Mantine's components (ADR-0100, ADR-0390): a toggle as an ActionIcon filled
// in the theme's primary while the Focus cell has what it sets, a menu as a subtle Button that opens its list, a split
// control as the face's ActionIcon beside an arrow's, and a Consumer's button as a subtle Button or ActionIcon. What
// an item means is the Sheet's, in its slot's context: a press is `invoke`, a choice is handed back through
// `choicesClosed`, and the toolbar's keys ask for the list through the view's `openRequest`.
//
// Every control takes no Tab and no DOM focus on a pointer press, so the toolbar stays one Tab stop and the keyboard
// stays on the Sheet, as Excel's ribbon leaves it in the cell, and claims its click (data-ex-claims), so a press made
// while an opening is still being answered waits its turn (ADR-0240). The list is Mantine's Popover, kept inside the
// Sheet (no portal) so the Sheet hears its keys, and fixed to the viewport so the toolbar's clipping does not cut it.
// Its keys are the built-in list's, not Mantine's: the arrows, Home and End move among the choices that can be made,
// swatches ten across; Enter or Space makes one; Tab keeps the keyboard where it is; Escape closes the list. A choice
// or Escape is handed back while the keyboard is still in the list, and the Sheet takes it on.

import {
    createElement, useCallback, useContext, useLayoutEffect, useRef, useState, useSyncExternalStore, type KeyboardEvent as ReactKeyboardEvent,
    type MouseEvent as ReactMouseEvent, type ReactElement, type ReactNode,
} from 'react';
import { ActionIcon, Button, Popover, type MantineSize } from '@mantine/core';
import { MantineSizeContext } from '@ex-grid/mantine';
import {
    sheetToolbarPictures, type SheetToolbarIcon, type SheetToolbarItemChoice, type SheetToolbarItemSlotContext, type SheetToolbarItemView,
} from '@ex-grid/sheet';

// Ten swatches to a row, as Excel's palette lays them out and as the built-in list moves among them.
const swatchesAcross = 10;

/** A picture of the built-in Chrome's, in the text's colour. */
function picture(icon: SheetToolbarIcon, className = 'mantine-ex-sheet-toolbar-icon'): ReactElement {
    return createElement('svg', {
        className, viewBox: '0 0 16 16', 'aria-hidden': true, focusable: false, fill: 'none', stroke: 'currentColor',
        strokeLinecap: 'round', strokeLinejoin: 'round', dangerouslySetInnerHTML: { __html: sheetToolbarPictures[icon] },
    });
}

/** The arrow a list opens from. */
function caret(): ReactElement {
    return createElement('svg', {
        className: 'mantine-ex-sheet-toolbar-caret', viewBox: '0 0 16 16', 'aria-hidden': true, focusable: false, fill: 'none',
        stroke: 'currentColor', strokeWidth: 1.5, strokeLinecap: 'round', strokeLinejoin: 'round',
    }, createElement('path', { d: 'M4.5 6.5 8 10l3.5-3.5' }));
}

/** Keeps the keyboard where it is on a pointer press. */
function keepKeyboard(event: ReactMouseEvent): void {
    event.preventDefault();
}

/** A Consumer's own content, mounted into the element its callback ref is handed and taken away with it. */
function OwnContent({ mount }: { readonly mount: (element: HTMLElement) => () => void }): ReactElement {
    const taken = useRef<(() => void) | undefined>(undefined);
    const ref = useCallback((element: HTMLSpanElement | null) => {
        taken.current?.();
        taken.current = element === null ? undefined : mount(element);
    }, [mount]);
    return createElement('span', { ref, className: 'mantine-ex-sheet-toolbar-content' });
}

/** The index the list's keys move to from `at` among `choices`, or `undefined` where they stay. */
function movedTo(key: string, at: number, choices: readonly SheetToolbarItemChoice[]): number | undefined {
    const can = (index: number): boolean => choices[index] !== undefined && choices[index].disabledReason === undefined;
    const enabled = choices.map((_, index) => index).filter(can);
    const first = enabled[0];
    const last = enabled.at(-1);
    if (first === undefined || last === undefined) return undefined;
    const swatch = choices[at]?.swatch !== undefined;
    const step = (by: number): number | undefined => {
        for (let next = at + by; next >= 0 && next < choices.length; next += by) if (can(next)) return next;
        return undefined;
    };
    switch (key) {
        case 'Home': return first;
        case 'End': return last;
        case 'ArrowDown': return step(swatch ? swatchesAcross : 1) ?? step(1);
        case 'ArrowUp': return step(swatch ? -swatchesAcross : -1) ?? step(-1);
        case 'ArrowRight': return swatch ? step(1) : undefined;
        case 'ArrowLeft': return swatch ? step(-1) : undefined;
        default: return undefined;
    }
}

/** The choice the list's keyboard starts on: the one the Focus cell has, else the first that can be made. */
function startOf(choices: readonly SheetToolbarItemChoice[]): number | undefined {
    const selected = choices.findIndex(choice => choice.selected && choice.disabledReason === undefined);
    if (selected >= 0) return selected;
    const first = choices.findIndex(choice => choice.disabledReason === undefined);
    return first >= 0 ? first : undefined;
}

/** An item's list: its choices, words and swatches as the Sheet hands them, in the theme's look. */
function ChoiceList({ view, size, choose, escape }: {
    readonly view: SheetToolbarItemView; readonly size: MantineSize; readonly choose: (index: number) => void; readonly escape: () => void;
}): ReactElement {
    const options = useRef<(HTMLElement | null)[]>([]);
    const focusAt = (index: number | undefined): void => { if (index !== undefined) options.current[index]?.focus({ preventScroll: true }); };
    // The list takes the keyboard as it is drawn.
    const list = useCallback((element: HTMLDivElement | null) => {
        if (element !== null) focusAt(startOf(view.choices));
    }, []);
    const onKeyDown = (event: ReactKeyboardEvent): void => {
        const at = options.current.findIndex(option => option === document.activeElement);
        event.preventDefault();
        event.stopPropagation();
        if (event.key === 'Escape') escape();
        else if (event.key === 'Enter' || event.key === ' ') {
            if (at >= 0 && view.choices[at]?.disabledReason === undefined) choose(at);
        } else focusAt(movedTo(event.key, at < 0 ? -1 : at, view.choices));
    };
    const option = (choice: SheetToolbarItemChoice, index: number): ReactElement => {
        const disabled = choice.disabledReason !== undefined;
        const common = {
            key: index, ref: (element: HTMLElement | null) => { options.current[index] = element; }, role: 'menuitemradio',
            'aria-checked': choice.selected, ...(disabled ? { 'aria-disabled': true } : {}), tabIndex: -1, 'data-ex-claims': 'click keydown',
            title: disabled ? `${choice.label}: ${choice.disabledReason}` : choice.label,
            onClick: () => { if (!disabled) choose(index); },
        };
        if (choice.swatch !== undefined) {
            return createElement('button', {
                ...common, type: 'button', 'aria-label': choice.label, className: 'mantine-ex-sheet-swatch', style: { backgroundColor: choice.swatch },
            });
        }
        return createElement(Button<'button'>, {
            ...common, type: 'button', variant: choice.selected ? 'light' : 'subtle', ...(choice.selected ? {} : { color: 'gray' }), size, fullWidth: true,
            justify: 'flex-start', className: 'mantine-ex-sheet-choice', ...(disabled ? { 'data-disabled': true } : {}),
        }, choice.label);
    };
    // The choices with a swatch stand ten across, the rest one to a row, in the Sheet's order.
    const rows: ReactNode[] = [];
    let swatches: ReactElement[] = [];
    view.choices.forEach((choice, index) => {
        if (choice.swatch !== undefined) {
            swatches.push(option(choice, index));
            return;
        }
        if (swatches.length > 0) rows.push(createElement('div', { key: `s${String(index)}`, className: 'mantine-ex-sheet-swatches', role: 'none' }, ...swatches));
        swatches = [];
        rows.push(option(choice, index));
    });
    if (swatches.length > 0) rows.push(createElement('div', { key: 'swatches', className: 'mantine-ex-sheet-swatches', role: 'none' }, ...swatches));
    return createElement('div', { ref: list, role: 'menu', 'aria-label': view.name, className: 'mantine-ex-sheet-choices', onKeyDown }, ...rows);
}

/** A Toolbar Item in Mantine's components, its list included. */
export function MantineToolbarItem({ context }: { readonly context: SheetToolbarItemSlotContext }): ReactElement {
    const view = useSyncExternalStore(context.subscribe, context.view);
    const size = useContext(MantineSizeContext);
    const [open, setOpen] = useState(false);
    const answered = useRef({ open: 0, close: 0 });
    const holder = useRef<HTMLSpanElement | null>(null);
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
    const choose = useCallback((index: number) => {
        context.choicesClosed(index, false);
        setOpen(false);
    }, [context]);
    const escape = useCallback(() => {
        context.choicesClosed(undefined, true);
        setOpen(false);
    }, [context]);
    // A press elsewhere closes the list; the Sheet hears it only while the list holds the keyboard, which then goes
    // back to it, and a control pressed outside keeps the keyboard it took.
    const dismissed = useCallback(() => {
        const inList = holder.current?.querySelector('.mantine-ex-sheet-choices')?.contains(document.activeElement) === true;
        if (inList) context.choicesClosed(undefined, false);
        setOpen(false);
    }, [context]);
    const toggleList = (): void => {
        if (open) dismissed();
        else if (enabled && view.choices.length > 0) setOpen(true);
    };
    const control = {
        tabIndex: -1, 'data-ex-claims': 'click', disabled: !enabled, title: enabled ? view.tooltip : `${view.tooltip}: ${view.disabledReason}`,
        onMouseDown: keepKeyboard,
    };
    const face = view.content !== undefined
        ? createElement(OwnContent, { mount: view.content })
        : view.icon !== undefined ? picture(view.icon) : (view.text ?? view.name);
    let drawn: ReactElement;
    switch (context.kind) {
        case 'toggle':
            drawn = createElement(ActionIcon<'button'>, {
                ...control, type: 'button', size, variant: view.pressed ? 'filled' : 'subtle', ...(view.pressed ? {} : { color: 'gray' }),
                'aria-label': view.name, 'aria-pressed': view.pressed, onClick: () => { context.invoke(); },
            }, face);
            break;
        case 'menu':
            drawn = createElement(Button<'button'>, {
                ...control, type: 'button', size, variant: 'subtle', color: 'gray', 'aria-label': view.name, 'aria-haspopup': 'menu',
                'aria-expanded': open, rightSection: caret(), className: 'mantine-ex-sheet-toolbar-menu', onClick: toggleList,
            }, view.text ?? view.name);
            break;
        case 'split':
            drawn = createElement('span', { className: 'mantine-ex-sheet-toolbar-split', role: 'group', 'aria-label': view.name },
                createElement(ActionIcon<'button'>, {
                    ...control, type: 'button', size, variant: 'subtle', color: 'gray', 'aria-label': view.name, className: 'mantine-ex-sheet-toolbar-face',
                    style: view.swatch === undefined ? undefined : { '--mantine-ex-sheet-swatch': view.swatch }, onClick: () => { context.invoke(); },
                }, face),
                createElement(ActionIcon<'button'>, {
                    ...control, type: 'button', size, variant: 'subtle', color: 'gray', 'aria-label': view.moreName ?? view.name,
                    'aria-haspopup': 'menu', 'aria-expanded': open, className: 'mantine-ex-sheet-toolbar-arrow', onClick: toggleList,
                }, caret()));
            break;
        case 'button':
            drawn = view.content === undefined && view.icon !== undefined
                ? createElement(ActionIcon<'button'>, {
                    ...control, type: 'button', size, variant: 'subtle', color: 'gray', 'aria-label': view.name, onClick: () => { context.invoke(); },
                }, face)
                : createElement(Button<'button'>, {
                    ...control, type: 'button', size, variant: 'subtle', color: 'gray', 'aria-label': view.name, onClick: () => { context.invoke(); },
                }, face);
            break;
        default:
            throw new RangeError(`Unknown Toolbar Item kind '${String(context.kind satisfies never)}' (ADR-0100).`);
    }
    const classes = ['mantine-ex-sheet-toolbar-item'];
    if (view.active) classes.push('mantine-ex-sheet-toolbar-active');
    const body = !hasList ? drawn : createElement(Popover, {
        opened: open, onDismiss: dismissed, withinPortal: false, floatingStrategy: 'fixed', position: 'bottom-start', offset: 2,
        trapFocus: false, returnFocus: false, closeOnEscape: false, withRoles: false, shadow: 'md', transitionProps: { duration: 0 },
    }, createElement(Popover.Target, null, drawn), createElement(Popover.Dropdown, { className: 'mantine-ex-sheet-toolbar-list' },
        createElement(ChoiceList, { view, size, choose, escape })));
    return createElement('span', { ref: holder, className: classes.join(' ') }, body);
}
