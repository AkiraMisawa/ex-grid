// The React Framework Adapter of ExSheet (ADR-0370, section 1): it renders the element the Sheet draws into,
// hands the Sheet its props, and unmounts it. It holds no state of the Sheet's, draws nothing inside the element,
// and decides nothing: the Workbook, the undo stack and every word are the Sheet's.

import { createElement, Fragment, useImperativeHandle, useLayoutEffect, useRef, type ReactElement, type ReactNode, type Ref } from 'react';
import { GridPortals, useGridPortals } from '@ex-grid/react';
import { createSheet, type SheetChrome, type SheetHandle, type SheetOptions, type SheetToolbarItemSlot, type SheetToolbarItemSlotContext } from '@ex-grid/sheet';
import type { CellAddress, SheetEdit } from '@ex-grid/sheet-engine';

/**
 * The Sheet's own Chrome slots for React (ADR-0390 section 3), each a render function of what the Sheet hands that
 * slot, rendered through a portal into the slot the Sheet made. A slot not given is drawn by the built-in Chrome. A
 * render function is read when its part is drawn, and its answer is rendered as a component of its own: a hook
 * belongs in a component it renders. Hand the same function on every render, declared outside the component or
 * memoised: a new one draws its parts again.
 */
export interface ExSheetChrome {
    /** Each Toolbar Item of the Sheet Toolbar (ADR-0100): `null` leaves the item to the built-in Chrome, as the C#
     * Chrome's `ToolbarItem` answering null does. The view changes through the context's `subscribe`. */
    readonly toolbarItem?: (context: SheetToolbarItemSlotContext) => ReactNode;
}

/**
 * An ExSheet's props: the Sheet's options, whole, its `chrome` as render functions. `locale` is the browser's
 * language where not given, read once when the Sheet is made (ADR-0370 section 10): a Sheet's culture is recorded in
 * its document, so it is never changed on a live Sheet.
 */
export type ExSheetProps = Omit<SheetOptions, 'chrome'> & {
    /** The parts of the Sheet a Chrome of the Consumer's, or a Wrapper's, draws in place of the built-in ones. */
    readonly chrome?: ExSheetChrome;
    /** A ref to what the Consumer may ask of the Sheet it rendered: its Workbook, its commands and reads, undo and
     * redo, and its Linked Tables. */
    readonly ref?: Ref<ExSheetApi>;
};

/**
 * What a ref to an ExSheet reaches: the Sheet's handle, less what the adapter itself does with it (taking the props
 * again, and destroying the Sheet on unmount). Asked of a Sheet not mounted, it throws.
 */
export type ExSheetApi = Omit<SheetHandle, 'update' | 'destroy'>;

/**
 * An Excel-like sheet with formulas. The element it renders is the Sheet's, which `@ex-grid/sheet` owns; every
 * render hands the Sheet the props again, and what did not change costs nothing (ADR-0003).
 */
export function ExSheet(props: ExSheetProps): ReactElement {
    const { ref, chrome, ...given } = props;
    const element = useRef<HTMLDivElement>(null);
    const portals = useGridPortals();
    const made = useRef<MadeMounts>({});
    const sheetChrome = sheetChromeOf(chrome, portals, made.current);
    const sheetOptions: SheetOptions = sheetChrome === undefined ? given : { ...given, chrome: sheetChrome };
    const sheet = useRef<SheetHandle | undefined>(undefined);
    const browserLocale = useRef<string | undefined>(undefined);
    // Drawn before the browser paints, so a mounted Sheet is never seen empty.
    useLayoutEffect(() => {
        const root = element.current;
        if (root === null) throw new Error('Internal: React mounted no element for the Sheet to draw into.');
        browserLocale.current ??= navigator.language;
        const options = sheetOptions.locale === undefined ? { ...sheetOptions, locale: browserLocale.current } : sheetOptions;
        if (sheet.current === undefined) {
            portals.begin();
            sheet.current = createSheet(root, options);
        } else {
            sheet.current.update(options);
        }
    });
    useImperativeHandle(ref, () => ({
        get workbook() {
            return mounted(sheet.current).workbook;
        },
        get canUndo() {
            return mounted(sheet.current).canUndo;
        },
        get canRedo() {
            return mounted(sheet.current).canRedo;
        },
        get isEditing() {
            return mounted(sheet.current).isEditing;
        },
        do: (sheetOrEdit: string | SheetEdit, edit?: SheetEdit) => {
            const handle = mounted(sheet.current);
            if (typeof sheetOrEdit === 'string') handle.do(sheetOrEdit, defined(edit));
            else handle.do(sheetOrEdit);
        },
        setCellFormat: change => mounted(sheet.current).setCellFormat(change),
        openFormatCells: () => mounted(sheet.current).openFormatCells(),
        cellFormatAt: (sheetOrAddress: string | CellAddress, address?: CellAddress) => {
            const handle = mounted(sheet.current);
            return typeof sheetOrAddress === 'string' ? handle.cellFormatAt(sheetOrAddress, defined(address)) : handle.cellFormatAt(sheetOrAddress);
        },
        undo: () => mounted(sheet.current).undo(),
        redo: () => mounted(sheet.current).redo(),
        declareLinkedTable: (name, columns, key) => { mounted(sheet.current).declareLinkedTable(name, columns, key); },
        pushLinkedTable: (name, rows) => { mounted(sheet.current).pushLinkedTable(name, rows); },
    }), []);
    useLayoutEffect(() => () => {
        // The portals go first: the contents the Sheet takes away as it goes ask for no render of a component gone.
        portals.end();
        sheet.current?.destroy();
        sheet.current = undefined;
        browserLocale.current = undefined;
    }, []);
    return createElement(Fragment, null, createElement('div', { ref: element }), ...portals.elements());
}

// The mount functions made for the render functions handed, by slot: one handed again keeps its mount, so the
// Sheet does not draw its parts again on every render.
interface MadeMounts {
    toolbarItem?: { readonly render: (context: SheetToolbarItemSlotContext) => ReactNode; readonly mount: SheetToolbarItemSlot };
}

// The Sheet's Chrome as the Sheet takes it: each render function as a mount function rendering through the portals.
function sheetChromeOf(given: ExSheetChrome | undefined, portals: GridPortals, made: MadeMounts): SheetChrome | undefined {
    // Checked as a script may hand it, whatever its type says.
    const chrome: unknown = given;
    if (chrome === undefined) return undefined;
    if (typeof chrome !== 'object' || chrome === null) throw new TypeError('chrome is an object of Chrome slots, each a render function of its context (ADR-0390).');
    const { toolbarItem }: { readonly toolbarItem?: unknown } = chrome;
    if (toolbarItem === undefined) return {};
    if (typeof toolbarItem !== 'function') throw new TypeError('chrome.toolbarItem is a render function of the slot\'s context (ADR-0390).');
    if (made.toolbarItem?.render !== toolbarItem) {
        // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- checked a function just above, the render the slot's type takes
        const render = toolbarItem as (context: SheetToolbarItemSlotContext) => ReactNode;
        made.toolbarItem = {
            render,
            mount: (slot, context) => {
                // Read as the part is drawn, as the C# Chrome's ToolbarItem is: nothing drawn leaves it to the built-in.
                const node = render(context);
                if (node === null || node === undefined || node === false) return undefined;
                return portals.mount(slot, () => node);
            },
        };
    }
    return { toolbarItem: made.toolbarItem.mount };
}

function mounted(sheet: SheetHandle | undefined): SheetHandle {
    if (sheet === undefined) throw new Error('This ExSheet is not mounted: a Sheet not drawn is asked nothing.');
    return sheet;
}

// The second argument a form of an overload naming its Sheet carries, which the handle's own overload refuses to miss.
function defined<T>(value: T | undefined): T {
    if (value === undefined) throw new TypeError('A command or a read that names its Sheet names what it acts on too.');
    return value;
}
