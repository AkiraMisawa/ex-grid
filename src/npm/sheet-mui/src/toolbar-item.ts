// A Toolbar Item of the Sheet Toolbar in MUI's components (ADR-0100, ADR-0390): a toggle as MUI's ToggleButton,
// selected in the theme's primary while the Focus cell has what it sets, a menu as a text Button that opens its list, a
// split control as the face's IconButton beside an arrow's, and a Consumer's button as a text Button or IconButton. What
// an item means, its list's opening and closing and the list's keys are ExSheet for React's `useToolbarItem`'s; this
// draws them.
//
// The list is MUI's Popper on a Paper, kept inside the Sheet (no portal) so the Sheet hears its keys, and fixed to the
// viewport so the toolbar's clipping does not cut it. MUI's Popover and Menu are not used: both are modal, trapping the
// keyboard and putting a backdrop over the page, and Menu's MenuList answers the arrows itself. Each choice is MUI's
// ListItemButton, as the grid's menus draw an item. MUI's Popper has no dismissal of its own; a press outside the item
// is useToolbarItem's to hear.

import { createElement, useCallback, useContext, useRef, type CSSProperties, type ReactElement } from 'react';
import { Button, IconButton, ListItemButton, Paper, Popper, ToggleButton } from '@mui/material';
import { MuiSizeContext, type MuiExGridSize } from '@ex-grid/mui';
import { sheetToolbarPictures, type SheetToolbarIcon, type SheetToolbarItemSlotContext } from '@ex-grid/sheet';
import { useToolbarItem, type ToolbarItemHandles } from '@ex-grid/sheet-react';

/** A picture of the built-in Chrome's, in the text's colour. */
function picture(icon: SheetToolbarIcon): ReactElement {
    return createElement('svg', {
        className: 'mui-ex-sheet-toolbar-icon', viewBox: '0 0 16 16', 'aria-hidden': true, focusable: false, fill: 'none', stroke: 'currentColor',
        strokeLinecap: 'round', strokeLinejoin: 'round', dangerouslySetInnerHTML: { __html: sheetToolbarPictures[icon] },
    });
}

/** The arrow a list opens from. */
function caret(): ReactElement {
    return createElement('svg', {
        className: 'mui-ex-sheet-toolbar-caret', viewBox: '0 0 16 16', 'aria-hidden': true, focusable: false, fill: 'none',
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
    return createElement('span', { ref, className: 'mui-ex-sheet-toolbar-content' });
}

/** An item's list: its choices, words and swatches as the Sheet hands them, in the theme's look. */
function ChoiceList({ item, size }: { readonly item: ToolbarItemHandles; readonly size: MuiExGridSize }): ReactElement {
    const { view, list, choice: props } = item;
    const option = (index: number): ReactElement => {
        const choice = view.choices[index];
        if (choice === undefined) throw new RangeError(`The list of ${view.name} has no choice ${String(index)} (ADR-0100).`);
        const common = { key: index, ...props(index) };
        if (choice.swatch !== undefined) {
            return createElement('button', {
                ...common, type: 'button', 'aria-label': choice.label, className: 'mui-ex-sheet-swatch', style: { backgroundColor: choice.swatch },
            });
        }
        // Not `disabled`: a choice that cannot be made keeps its title, which says why, where the pointer reaches it.
        return createElement(ListItemButton<'button'>, {
            ...common, component: 'button', type: 'button', dense: size.dense, selected: choice.selected, className: 'mui-ex-sheet-choice',
        }, choice.label);
    };
    // The choices one to a row and the swatches ten across, in the Sheet's order, as the list's keys move among them.
    const rows = item.rows.map((row, at) => (typeof row === 'number'
        ? option(row)
        : createElement('div', { key: `s${String(at)}`, className: 'mui-ex-sheet-swatches', role: 'none' }, ...row.map(option))));
    return createElement('div', { ...list, className: 'mui-ex-sheet-choices' }, ...rows);
}

/** A Toolbar Item in MUI's components, its list included. */
export function MuiToolbarItem({ context }: { readonly context: SheetToolbarItemSlotContext }): ReactElement {
    const item = useToolbarItem(context);
    const { view, open, control, toggleList } = item;
    const size = useContext(MuiSizeContext);
    const anchor = useRef<HTMLElement | null>(null);
    const face = view.content !== undefined
        ? createElement(OwnContent, { mount: view.content })
        : view.icon !== undefined ? picture(view.icon) : (view.text ?? view.name);
    const press = (): void => { context.invoke(); };
    // The element the list opens beneath: the menu's button, or the split control's pair.
    const listAnchor = useCallback((element: HTMLElement | null) => { anchor.current = element; }, []);
    const anchorOf = useCallback((): HTMLElement => {
        if (anchor.current === null) throw new Error(`Internal: ${context.kind} drew no element for its list to open beneath.`);
        return anchor.current;
    }, [context.kind]);
    // A colour control's face shows the colour it sets beneath its picture; the colour is the user's, written inline.
    const swatch: (CSSProperties & Readonly<Record<'--mui-ex-sheet-swatch', string>>) | undefined = view.swatch === undefined ? undefined : { '--mui-ex-sheet-swatch': view.swatch };
    let drawn: ReactElement;
    switch (context.kind) {
        case 'toggle':
            // ToggleButton writes aria-pressed from `selected` itself.
            drawn = createElement(ToggleButton, {
                ...control, value: context.id, size: size.size, selected: view.pressed, color: 'primary', 'aria-label': view.name,
                className: 'mui-ex-sheet-toolbar-toggle', onClick: press,
            }, face);
            break;
        case 'menu':
            drawn = createElement(Button, {
                ...control, ref: listAnchor, type: 'button', size: size.size, variant: 'text', color: 'inherit', 'aria-label': view.name,
                'aria-haspopup': 'menu', 'aria-expanded': open, endIcon: caret(), className: 'mui-ex-sheet-toolbar-menu', onClick: toggleList,
            }, view.text ?? view.name);
            break;
        case 'split':
            drawn = createElement('span', { ref: listAnchor, className: 'mui-ex-sheet-toolbar-split', role: 'group', 'aria-label': view.name },
                createElement(IconButton, {
                    ...control, type: 'button', size: size.size, color: 'inherit', 'aria-label': view.name, className: 'mui-ex-sheet-toolbar-face',
                    ...(swatch === undefined ? {} : { style: swatch }), onClick: press,
                }, face),
                createElement(IconButton, {
                    ...control, type: 'button', size: size.size, color: 'inherit', 'aria-label': view.moreName ?? view.name,
                    'aria-haspopup': 'menu', 'aria-expanded': open, className: 'mui-ex-sheet-toolbar-arrow', onClick: toggleList,
                }, caret()));
            break;
        case 'button':
            drawn = view.content === undefined && view.icon !== undefined
                ? createElement(IconButton, {
                    ...control, type: 'button', size: size.size, color: 'inherit', 'aria-label': view.name, onClick: press,
                }, face)
                : createElement(Button, {
                    ...control, type: 'button', size: size.size, variant: 'text', color: 'inherit', 'aria-label': view.name,
                    className: 'mui-ex-sheet-toolbar-button', onClick: press,
                }, face);
            break;
        default:
            throw new RangeError(`Unknown Toolbar Item kind '${String(context.kind satisfies never)}' (ADR-0100).`);
    }
    const classes = ['mui-ex-sheet-toolbar-item'];
    if (view.active) classes.push('mui-ex-sheet-toolbar-active');
    return createElement('span', { ref: item.item, className: classes.join(' ') }, drawn,
        !item.hasList || !open ? null : createElement(Popper, {
            open, anchorEl: anchorOf, disablePortal: true, placement: 'bottom-start',
            className: 'mui-ex-sheet-toolbar-popper', popperOptions: { strategy: 'fixed' },
            modifiers: [{ name: 'offset', options: { offset: [0, 2] } }],
        }, createElement(Paper, { elevation: 8, className: 'mui-ex-sheet-toolbar-list' }, createElement(ChoiceList, { item, size }))));
}
