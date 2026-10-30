// A Toolbar Item of the Sheet Toolbar in Mantine's components (ADR-0100, ADR-0390): a toggle as an ActionIcon filled
// in the theme's primary while the Focus cell has what it sets, a menu as a subtle Button that opens its list, a split
// control as the face's ActionIcon beside an arrow's, and a Consumer's button as a subtle Button or ActionIcon. What
// an item means, its list's opening and closing and the list's keys are ExSheet for React's `useToolbarItem`'s; this
// draws them. The list is Mantine's Popover, kept inside the Sheet (no portal) so the Sheet hears its keys, and fixed
// to the viewport so the toolbar's clipping does not cut it.

import { createElement, useCallback, useContext, useRef, type ReactElement } from 'react';
import { ActionIcon, Button, Popover, type MantineSize } from '@mantine/core';
import { MantineSizeContext } from '@ex-grid/mantine';
import { sheetToolbarPictures, type SheetToolbarIcon, type SheetToolbarItemSlotContext } from '@ex-grid/sheet';
import { useToolbarItem, type ToolbarItemHandles } from '@ex-grid/sheet-react';

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

/** A Consumer's own content, mounted into the element its callback ref is handed and taken away with it. */
function OwnContent({ mount }: { readonly mount: (element: HTMLElement) => () => void }): ReactElement {
    const taken = useRef<(() => void) | undefined>(undefined);
    const ref = useCallback((element: HTMLSpanElement | null) => {
        taken.current?.();
        taken.current = element === null ? undefined : mount(element);
    }, [mount]);
    return createElement('span', { ref, className: 'mantine-ex-sheet-toolbar-content' });
}

/** An item's list: its choices, words and swatches as the Sheet hands them, in the theme's look. */
function ChoiceList({ item, size }: { readonly item: ToolbarItemHandles; readonly size: MantineSize }): ReactElement {
    const { view, list, choice: props } = item;
    const option = (index: number): ReactElement => {
        const choice = view.choices[index];
        if (choice === undefined) throw new RangeError(`The list of ${view.name} has no choice ${String(index)} (ADR-0100).`);
        const common = { key: index, ...props(index) };
        if (choice.swatch !== undefined) {
            return createElement('button', {
                ...common, type: 'button', 'aria-label': choice.label, className: 'mantine-ex-sheet-swatch', style: { backgroundColor: choice.swatch },
            });
        }
        return createElement(Button<'button'>, {
            ...common, type: 'button', variant: choice.selected ? 'light' : 'subtle', ...(choice.selected ? {} : { color: 'gray' }), size, fullWidth: true,
            justify: 'flex-start', className: 'mantine-ex-sheet-choice', ...(choice.disabledReason === undefined ? {} : { 'data-disabled': true }),
        }, choice.label);
    };
    // The choices one to a row and the swatches ten across, in the Sheet's order, as the list's keys move among them.
    const rows = item.rows.map((row, at) => (typeof row === 'number'
        ? option(row)
        : createElement('div', { key: `s${String(at)}`, className: 'mantine-ex-sheet-swatches', role: 'none' }, ...row.map(option))));
    return createElement('div', { ...list, className: 'mantine-ex-sheet-choices' }, ...rows);
}

/** A Toolbar Item in Mantine's components, its list included. */
export function MantineToolbarItem({ context }: { readonly context: SheetToolbarItemSlotContext }): ReactElement {
    const item = useToolbarItem(context);
    const { view, open, control, toggleList } = item;
    const size = useContext(MantineSizeContext);
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
    const body = !item.hasList ? drawn : createElement(Popover, {
        // A press outside is useToolbarItem's to hear, so Mantine's own is off.
        opened: open, closeOnClickOutside: false, withinPortal: false, floatingStrategy: 'fixed', position: 'bottom-start', offset: 2,
        trapFocus: false, returnFocus: false, closeOnEscape: false, withRoles: false, shadow: 'md', transitionProps: { duration: 0 },
    }, createElement(Popover.Target, null, drawn), createElement(Popover.Dropdown, { className: 'mantine-ex-sheet-toolbar-list' },
        createElement(ChoiceList, { item, size })));
    return createElement('span', { ref: item.item, className: classes.join(' ') }, body);
}
