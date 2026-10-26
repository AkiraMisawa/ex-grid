// The React Framework Adapter of ExSheet (ADR-0370, section 1): it renders the element the Sheet draws into,
// hands the Sheet its props, and unmounts it. It holds no state of the Sheet's, draws nothing inside the element,
// and decides nothing: the Workbook, the undo stack and every word are the Sheet's.

import { createElement, useLayoutEffect, useRef, type ReactElement } from 'react';
import { createSheet, type SheetHandle, type SheetOptions } from '@ex-grid/sheet';

/**
 * An ExSheet's props: the Sheet's options, whole. `locale` is the browser's language where not given, read once
 * when the Sheet is made (ADR-0370 section 10): a Sheet's culture is recorded in its document, so it is never
 * changed on a live Sheet.
 */
export type ExSheetProps = SheetOptions;

/**
 * An Excel-like sheet with formulas. The element it renders is the Sheet's, which `@ex-grid/sheet` owns; every
 * render hands the Sheet the props again, and what did not change costs nothing (ADR-0003).
 */
export function ExSheet(props: ExSheetProps): ReactElement {
    const element = useRef<HTMLDivElement>(null);
    const sheet = useRef<SheetHandle | undefined>(undefined);
    const browserLocale = useRef<string | undefined>(undefined);
    // Drawn before the browser paints, so a mounted Sheet is never seen empty.
    useLayoutEffect(() => {
        const root = element.current;
        if (root === null) throw new Error('Internal: React mounted no element for the Sheet to draw into.');
        browserLocale.current ??= navigator.language;
        const options = props.locale === undefined ? { ...props, locale: browserLocale.current } : props;
        if (sheet.current === undefined) {
            sheet.current = createSheet(root, options);
        } else {
            sheet.current.update(options);
        }
    });
    useLayoutEffect(() => () => {
        sheet.current?.destroy();
        sheet.current = undefined;
        browserLocale.current = undefined;
    }, []);
    return createElement('div', { ref: element });
}
