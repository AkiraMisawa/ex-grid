// The React Framework Adapter of ExSheet (ADR-0370, section 1): it renders the element the Sheet draws into,
// hands the Sheet its props, and unmounts it. It holds no state of the Sheet's, draws nothing inside the element,
// and decides nothing: the Workbook, the undo stack and every word are the Sheet's.

import { createElement, useImperativeHandle, useLayoutEffect, useRef, type ReactElement, type Ref } from 'react';
import { createSheet, type SheetHandle, type SheetOptions } from '@ex-grid/sheet';
import type { CellAddress, SheetEdit } from '@ex-grid/sheet-engine';

/**
 * An ExSheet's props: the Sheet's options, whole. `locale` is the browser's language where not given, read once
 * when the Sheet is made (ADR-0370 section 10): a Sheet's culture is recorded in its document, so it is never
 * changed on a live Sheet.
 */
export type ExSheetProps = SheetOptions & {
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
    const { ref, ...sheetOptions } = props;
    const element = useRef<HTMLDivElement>(null);
    const sheet = useRef<SheetHandle | undefined>(undefined);
    const browserLocale = useRef<string | undefined>(undefined);
    // Drawn before the browser paints, so a mounted Sheet is never seen empty.
    useLayoutEffect(() => {
        const root = element.current;
        if (root === null) throw new Error('Internal: React mounted no element for the Sheet to draw into.');
        browserLocale.current ??= navigator.language;
        const options = sheetOptions.locale === undefined ? { ...sheetOptions, locale: browserLocale.current } : sheetOptions;
        if (sheet.current === undefined) {
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
        sheet.current?.destroy();
        sheet.current = undefined;
        browserLocale.current = undefined;
    }, []);
    return createElement('div', { ref: element });
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
