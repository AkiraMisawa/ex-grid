// A Chrome of plain controls for the Sheet's own slots, as a Wrapper draws its design system's (ADR-0390 section 7):
// the contract every Wrapper keeps runs against it here, and against each Wrapper in its own package.
import {
    createElement, useLayoutEffect, useRef, useState, useSyncExternalStore, type KeyboardEvent as ReactKeyboardEvent, type ReactElement,
} from 'react';
import {
    formatCellsOffer, type FormatCellsDraft, type SheetFormatCellsSlotContext, type SheetTabSlotContext, type SheetToolbarItemChoice,
    type SheetToolbarItemSlotContext,
} from '@ex-grid/sheet';
import { useFormatCells } from '../src/format-cells';
import type { ExSheetChrome } from '../src/ex-sheet';

const across = 10;

/** The next choice that can be made from `at`, `by` at a time. */
function step(choices: readonly SheetToolbarItemChoice[], at: number, by: number): number {
    for (let next = at + by; next >= 0 && next < choices.length; next += by) if (choices[next]?.disabledReason === undefined) return next;
    return at;
}

function PlainList({ context, close }: { readonly context: SheetToolbarItemSlotContext; readonly close: () => void }): ReactElement {
    const { choices, name } = context.view();
    const options = useRef<(HTMLButtonElement | null)[]>([]);
    useLayoutEffect(() => {
        const start = choices.findIndex(choice => choice.selected);
        options.current[start < 0 ? 0 : start]?.focus();
    }, []);
    const onKeyDown = (event: ReactKeyboardEvent): void => {
        event.preventDefault();
        event.stopPropagation();
        const at = options.current.findIndex(option => option === document.activeElement);
        const by = choices[at]?.swatch === undefined ? 1 : across;
        if (event.key === 'Escape') {
            context.choicesClosed(undefined, true);
            close();
        } else if (event.key === 'Enter') {
            context.choicesClosed(at, false);
            close();
        } else if (event.key === 'ArrowDown') options.current[step(choices, at, by)]?.focus();
        else if (event.key === 'ArrowUp') options.current[step(choices, at, -by)]?.focus();
    };
    return createElement('div', { role: 'menu', 'aria-label': name, onKeyDown },
        ...choices.map((choice, index) => createElement('button', {
            key: index, type: 'button', role: 'menuitemradio', 'aria-checked': choice.selected, tabIndex: -1, 'data-ex-claims': 'click keydown',
            ...(choice.swatch === undefined ? {} : { 'aria-label': choice.label }),
            ref: (element: HTMLButtonElement | null) => { options.current[index] = element; },
            onClick: () => { context.choicesClosed(index, false); close(); },
        }, choice.swatch === undefined ? choice.label : '')));
}

function PlainToolbarItem({ context }: { readonly context: SheetToolbarItemSlotContext }): ReactElement {
    const view = useSyncExternalStore(context.subscribe, context.view);
    const [open, setOpen] = useState(false);
    const answered = useRef({ open: 0, close: 0 });
    useLayoutEffect(() => {
        if (view.closeRequest !== answered.current.close) setOpen(false);
        else if (view.openRequest !== answered.current.open) setOpen(true);
        answered.current = { open: view.openRequest, close: view.closeRequest };
    }, [view]);
    const control = (name: string, press: () => void, pressed?: boolean): ReactElement => createElement('button', {
        key: name, type: 'button', 'aria-label': name, tabIndex: -1, 'data-ex-claims': 'click', disabled: view.disabledReason !== undefined,
        ...(pressed === undefined ? {} : { 'aria-pressed': pressed }),
        onMouseDown: (event: MouseEvent | { preventDefault: () => void }) => { event.preventDefault(); }, onClick: press,
    }, view.text ?? view.name);
    const toggleList = (): void => { setOpen(shown => !shown); };
    const close = (): void => { setOpen(false); };
    switch (context.kind) {
        case 'toggle': return control(view.name, () => { context.invoke(); }, view.pressed);
        case 'button': return control(view.name, () => { context.invoke(); });
        case 'menu': return createElement('span', null, control(view.name, toggleList), open ? createElement(PlainList, { context, close }) : null);
        case 'split': return createElement('span', null, control(view.name, () => { context.invoke(); }),
            control(view.moreName ?? view.name, toggleList), open ? createElement(PlainList, { context, close }) : null);
        default: throw new RangeError(`Unknown kind ${String(context.kind satisfies never)}.`);
    }
}

function PlainFormatCells({ context }: { readonly context: SheetFormatCellsSlotContext }): ReactElement {
    const { tab, showTab, tabControl, set, refusal, ok, cancel } = useFormatCells(context);
    const { draft, words } = context;
    const radio = (label: string, checked: boolean, choose: (each: FormatCellsDraft) => void): ReactElement => createElement('label', { key: label },
        createElement('input', { type: 'radio', checked, onChange: () => { set(choose); } }), label);
    const panel = tab === 'number'
        ? [
            ...formatCellsOffer.categories.map(({ category }) => radio(words.categoryName(category), draft.category === category, each => { each.selectCategory(category); })),
            draft.category === 'custom' ? createElement('label', { key: 'code' }, words.formatCellsLabel('type'),
                createElement('input', { value: draft.typeCode, onChange: event => { const { value } = event.target; set(each => { each.setType(value); }); } })) : null,
        ]
        : tab === 'font'
            ? formatCellsOffer.fontStyles.map(style => radio(words.fontStyleName(style), draft.fontStyle === style, each => { each.setFontStyle(style); }))
            : [];
    return createElement('div', null,
        createElement('div', { role: 'tablist' }, ...formatCellsOffer.tabs.map(each => createElement('button', {
            key: each, type: 'button', role: 'tab', 'aria-selected': each === tab, ...(each === tab ? { ref: tabControl } : {}),
            onClick: () => { showTab(each); },
        }, words.tabName(each)))),
        createElement('div', { role: 'tabpanel' }, ...panel),
        refusal === undefined ? null : createElement('p', { role: 'alert' }, refusal),
        createElement('button', { type: 'button', onClick: ok }, words.formatCellsLabel('ok')),
        createElement('button', { type: 'button', onClick: cancel }, words.formatCellsLabel('cancel')));
}

/** The Sheet's own slots in plain controls; the grid's are left to the built-in Chrome. */
export const plainSheetChrome: ExSheetChrome = {
    toolbarItem: context => createElement(PlainToolbarItem, { context }),
    formatCells: context => createElement(PlainFormatCells, { context }),
    sheetTab: (context: SheetTabSlotContext) => createElement('b', null, context.name),
    sheetTabAdd: () => createElement('b', null, '+'),
};
