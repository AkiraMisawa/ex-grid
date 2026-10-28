// The React Framework Adapter of ExSheet (ADR-0370, section 1): it renders the element the Sheet draws into,
// hands the Sheet its props, and unmounts it. It holds no state of the Sheet's, draws nothing inside the element,
// and decides nothing: the Workbook, the undo stack and every word are the Sheet's.

import { createElement, Fragment, useImperativeHandle, useLayoutEffect, useRef, useSyncExternalStore, type ReactElement, type ReactNode, type Ref } from 'react';
import { chromeMounts, GridPortals, useGridPortals, type ChromeMountsMade, type ExGridChrome } from '@ex-grid/react';
import {
    createSheet, type SheetChrome, type SheetFormatCellsSlotContext, type SheetHandle, type SheetOptions, type SheetTabSlotContext, type SheetToolbarButton,
    type SheetToolbarCommand, type SheetToolbarContent, type SheetToolbarItemSlot, type SheetToolbarItemSlotContext, type SheetToolbarRowOptions,
} from '@ex-grid/sheet';
import type { CellAddress, SheetEdit } from '@ex-grid/sheet-engine';

/**
 * The Sheet's Chrome slots for React (ADR-0390 section 3), each a render function of what the Sheet hands that slot,
 * rendered through a portal into the slot the Sheet made. A slot not given is drawn by the built-in Chrome. As the
 * Sheet's `chrome` holds its grid's slots, so does this: ExGrid for React's render functions, which the Sheet hands
 * its grid, beside the Sheet's own. A render function is read when its part is drawn, and its answer is rendered as
 * a component of its own: a hook belongs in a component it renders. Hand the same function on every render, declared
 * outside the component or memoised: a new one draws its parts again.
 */
export interface ExSheetChrome extends ExGridChrome<unknown> {
    /** Each Toolbar Item of the Sheet Toolbar (ADR-0100): `null` leaves the item to the built-in Chrome, as the C#
     * Chrome's `ToolbarItem` answering null does. The view changes through the context's `subscribe`. */
    readonly toolbarItem?: (context: SheetToolbarItemSlotContext) => ReactNode;
    /** Format Cells' contents (ADR-0071), in the grid's popover frame, drawn with {@link useFormatCells}. */
    readonly formatCells?: (context: SheetFormatCellsSlotContext) => ReactNode;
    /** Each Sheet Tab's face (ADR-0331 §4): the Sheet's name as its only text, taking no keyboard and hearing
     * nothing. */
    readonly sheetTab?: (context: SheetTabSlotContext) => ReactNode;
    /** The face of the button that adds a Sheet (ADR-0331 §4), taking no keyboard and hearing nothing. */
    readonly sheetTabAdd?: () => ReactNode;
}

/** A Consumer's own action on the Sheet Toolbar (ADR-0100), the Sheet's button with its content as a React node. */
export interface ExSheetToolbarButton extends Omit<SheetToolbarButton, 'content'> {
    /** Its own content, such as an icon, rendered in place of its text, which still names it: the C# `ToolbarButton`'s
     * `ChildContent`. A node that renders nothing (`null`, `false`, `true`, `''`) leaves the text. A function is the
     * Sheet's own `SheetToolbarContent`, handed on as it is. A `chrome.toolbarItem` slot is handed the content as
     * `view().content`, a function that mounts it into an element and answers what takes it away, which a React 19
     * callback ref can be. */
    readonly content?: ReactNode | SheetToolbarContent;
}

/** One element of a Toolbar Row for React: one of ExSheet's own items by its command, a separator, or the
 * Consumer's own button. */
export type ExSheetToolbarEntry = SheetToolbarCommand | 'separator' | ExSheetToolbarButton;

/** A Toolbar Row of the Consumer's for React (ADR-0100): the Sheet's, its buttons' content React nodes. */
export interface ExSheetToolbarRow extends Omit<SheetToolbarRowOptions, 'items'> {
    /** What the row holds, in order. */
    readonly items: readonly ExSheetToolbarEntry[];
}

/**
 * An ExSheet's props: the Sheet's options, whole, its `chrome` as render functions and its toolbar buttons' content
 * as React nodes. `locale` is the browser's
 * language where not given, read once when the Sheet is made (ADR-0370 section 10): a Sheet's culture is recorded in
 * its document, so it is never changed on a live Sheet.
 */
export type ExSheetProps = Omit<SheetOptions, 'chrome' | 'toolbar'> & {
    /** The Sheet Toolbar's rows, top to bottom (ADR-0100), as `SheetOptions.toolbar`; a button's `content` is a React
     * node. */
    readonly toolbar?: readonly (ExSheetToolbarRow | 'default')[] | undefined;
    /** The parts of the Sheet a Chrome of the Consumer's, or a Wrapper's, draws in place of the built-in ones. */
    readonly chrome?: ExSheetChrome;
    /** A ref to what the Consumer may ask of the Sheet it rendered: its Workbook, its commands and reads, undo and
     * redo, its Linked Tables, and opening and saving a file. */
    readonly ref?: Ref<ExSheetApi>;
};

/**
 * What a ref to an ExSheet reaches: the Sheet's handle, less what the adapter itself does with it (taking the props
 * again, and destroying the Sheet on unmount) and the hold of the Settled Mark, which is for a Framework Adapter whose
 * Chrome arrives later than its slot (ADR-0410 section 5), as ExGrid's ref leaves it out. Asked of a Sheet not mounted,
 * it throws.
 */
export type ExSheetApi = Omit<SheetHandle, 'update' | 'destroy' | 'holdSettled'>;

/**
 * An Excel-like sheet with formulas. The element it renders is the Sheet's, which `@ex-grid/sheet` owns; every
 * render hands the Sheet the props again, and what did not change costs nothing (ADR-0003).
 */
export function ExSheet(props: ExSheetProps): ReactElement {
    const { ref, chrome, toolbar, ...given } = props;
    const element = useRef<HTMLDivElement>(null);
    const portals = useGridPortals();
    const made = useRef<MadeMounts>({ slots: new Map() });
    const contents = useRef<MadeToolbar>({ given: undefined, made: undefined, held: new Map() });
    const sheetChrome = sheetChromeOf(chrome, portals, made.current);
    const sheetToolbar = sheetToolbarOf(toolbar, portals, contents.current);
    const sheetOptions: SheetOptions = {
        ...given,
        ...(sheetChrome === undefined ? {} : { chrome: sheetChrome }),
        ...(sheetToolbar === undefined ? {} : { toolbar: sheetToolbar }),
    };
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
        // The content this render handed is what each button's place shows from now on.
        for (const content of contents.current.held.values()) content.show();
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
        open: read => mounted(sheet.current).open(read),
        save: write => mounted(sheet.current).save(write),
        focus: () => { mounted(sheet.current).focus(); },
        inOrder: handling => mounted(sheet.current).inOrder(handling),
        tell: sentence => { mounted(sheet.current).tell(sentence); },
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
// Sheet does not draw its parts again on every render. Every slot but the Toolbar Item's is made as ExGrid for React
// makes its own, the grid's and the Sheet's alike, and the Sheet refuses a name it has not.
interface MadeMounts {
    readonly slots: ChromeMountsMade;
    toolbarItem?: { readonly render: unknown; readonly portals: GridPortals; readonly mount: SheetToolbarItemSlot };
    // The Sheet's chrome made last, and what it was made from: the same chrome handed again is the same to the Sheet.
    given?: ExSheetChrome | undefined;
    portals?: GridPortals;
    chrome?: SheetChrome;
}

// The Sheet's Chrome as the Sheet takes it: each render function as a mount function rendering through the portals.
// Which slots there are is the Sheet's to check; a render function that is not a function is named here.
function sheetChromeOf(given: ExSheetChrome | undefined, portals: GridPortals, made: MadeMounts): SheetChrome | undefined {
    // Checked as a script may hand it, whatever its type says.
    const chrome: unknown = given;
    if (chrome === undefined) return undefined;
    if (typeof chrome !== 'object' || chrome === null) throw new TypeError('chrome is an object of Chrome slots, each a render function of its context (ADR-0390).');
    if (made.chrome !== undefined && made.given === given && made.portals === portals) return made.chrome;
    const { toolbarItem, ...slots }: Record<string, unknown> = { ...chrome };
    const sheetChrome: Record<string, unknown> = { ...chromeMounts<unknown>(slots, portals, made.slots) };
    if (toolbarItem !== undefined) {
        if (typeof toolbarItem !== 'function') throw new TypeError('chrome.toolbarItem is a render function of the slot\'s context (ADR-0390).');
        sheetChrome['toolbarItem'] = toolbarItemMount(toolbarItem, portals, made);
    }
    made.given = given;
    made.portals = portals;
    // Each slot a mount function made above; the Sheet checks their names.
    made.chrome = sheetChrome;
    return made.chrome;
}

// A Toolbar Item's mount: the render read as the part is drawn, as the C# Chrome's ToolbarItem is, so nothing drawn
// leaves it to the built-in.
function toolbarItemMount(render: unknown, portals: GridPortals, made: MadeMounts): SheetToolbarItemSlot {
    const before = made.toolbarItem;
    if (before !== undefined && before.render === render && before.portals === portals) return before.mount;
    const mount: SheetToolbarItemSlot = (slot, context) => {
        // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- checked a function by the caller, the render the slot's type takes
        const node: ReactNode = Reflect.apply(render as (context: SheetToolbarItemSlotContext) => ReactNode, undefined, [context]);
        if (node === null || node === undefined || node === false) return undefined;
        return portals.mount(slot, () => node);
    };
    made.toolbarItem = { render, portals, mount };
    return mount;
}

// A button's content as the Sheet takes it: one mount function for the button's place, kept across renders, so the
// Sheet does not draw the face again on each. What a render hands waits in `next` until that render is committed,
// and the places mounted hear it then, without the portals rendering again.
interface HeldContent {
    next: ReactNode;
    readonly mount: SheetToolbarContent;
    readonly show: () => void;
}

// The toolbar last handed and what the Sheet was handed for it; the content held, by the button's place.
interface MadeToolbar {
    given: ExSheetProps['toolbar'];
    made: SheetOptions['toolbar'];
    held: Map<string, HeldContent>;
}

// Whether a node renders nothing, which leaves the button its text.
function rendersNothing(node: ReactNode): boolean {
    return node === undefined || node === null || typeof node === 'boolean' || node === '';
}

// The Sheet's toolbar: each button's React content as a mount function rendering through the portals. The same
// toolbar handed again is the same toolbar to the Sheet, which then draws nothing again; a row or a button with no
// React content is handed on as it was given.
function sheetToolbarOf(given: ExSheetProps['toolbar'], portals: GridPortals, made: MadeToolbar): SheetOptions['toolbar'] {
    if (given === made.given) return made.made;
    // Checked by the Sheet as a script may hand it: what is not a list of rows is handed on for it to refuse.
    const rows: unknown = given;
    const held = new Map<string, HeldContent>();
    const sheetRows = !Array.isArray(rows) ? rows : rows.map((row: unknown, r): unknown => {
        if (typeof row !== 'object' || row === null) return row;
        // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- an object row is a Toolbar Row; the Sheet checks its fields by name
        const { label, keyTip, items } = row as ExSheetToolbarRow;
        const entries: unknown = items;
        if (!Array.isArray(entries)) return row;
        const sheetItems = entries.map((entry: unknown, i): unknown => {
            if (typeof entry !== 'object' || entry === null) return entry;
            // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- an object entry is a button; the Sheet checks its fields by name
            const button = entry as ExSheetToolbarButton;
            const { content } = button;
            if (typeof content === 'function') return entry;
            // Read through the button, as the Sheet reads it, so a button of a class of the Consumer's keeps its fields.
            const { text, onPress, keyTip: letters, disabledReason } = button;
            if (rendersNothing(content)) return { text, onPress, keyTip: letters, disabledReason };
            const place = `${String(r)}:${String(i)}`;
            const holding = made.held.get(place) ?? heldContent(portals, content);
            holding.next = content;
            held.set(place, holding);
            return { text, onPress, keyTip: letters, disabledReason, content: holding.mount };
        });
        return sheetItems.some((item, i) => item !== entries[i]) ? { label, keyTip, items: sheetItems } : row;
    });
    made.given = given;
    made.held = held;
    // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- the rows as given, each button's React content a mount function; the Sheet checks them by name
    made.made = sheetRows as SheetOptions['toolbar'];
    return made.made;
}

function heldContent(portals: GridPortals, first: ReactNode): HeldContent {
    let shown = first;
    const listeners = new Set<() => void>();
    const subscribe = (listener: () => void): (() => void) => {
        listeners.add(listener);
        return () => { listeners.delete(listener); };
    };
    const read = (): ReactNode => shown;
    const content: HeldContent = {
        next: first,
        mount: element => portals.mount(element, () => createElement(HeldNode, { subscribe, read })),
        show: () => {
            if (content.next === shown) return;
            shown = content.next;
            for (const listener of listeners) listener();
        },
    };
    return content;
}

// A button's content where it is mounted: the node committed last.
function HeldNode({ subscribe, read }: { readonly subscribe: (listener: () => void) => () => void; readonly read: () => ReactNode }): ReactNode {
    return useSyncExternalStore(subscribe, read);
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
