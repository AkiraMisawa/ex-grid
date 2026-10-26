// The React Framework Adapter of ExPivot's Script Core (ADR-0370 section 1): it renders the element the pivot
// draws into, hands the pivot its props, and unmounts it. It holds no state of the pivot's, draws nothing
// inside its root, and decides nothing.

import { createElement, useLayoutEffect, useRef, type ReactElement } from 'react';
import { browserPivotLocale, createPivot, type PivotHandle, type PivotViewOptions } from '@ex-grid/pivot';

/**
 * An ExPivot's props: the Script Core's options, whole. `locale` is the browser's language where not given,
 * the first of its languages the pivot writes in, read once, the first time the pivot is given none (ADR-0370
 * section 10); the core never reads it on its own.
 */
export type ExPivotProps = PivotViewOptions;

/** The props as the core takes them: the browser's language where the Consumer gave no locale. */
function withLocale(props: ExPivotProps, browserLocale: string | undefined): PivotViewOptions {
    if (props.locale !== undefined) return props;
    if (browserLocale === undefined) throw new Error('Internal: the browser\'s locale was not read for a pivot given none.');
    return { ...props, locale: browserLocale };
}

/**
 * Excel's PivotTable, drawn by ExGrid, beside its Field List. The element it renders is the pivot's root,
 * which the Script Core owns; every render hands the core the props again, and what did not change asks
 * nothing again.
 */
export function ExPivot(props: ExPivotProps): ReactElement {
    const element = useRef<HTMLDivElement>(null);
    const pivot = useRef<PivotHandle | undefined>(undefined);
    const browserLocale = useRef<string | undefined>(undefined);
    // Drawn before the browser paints, so a mounted pivot is never seen empty.
    useLayoutEffect(() => {
        const root = element.current;
        if (root === null) throw new Error('Internal: React mounted no element for the pivot to draw into.');
        // The browser is asked only where the Consumer gave no locale: one that did needs nothing of it.
        if (props.locale === undefined) browserLocale.current ??= browserPivotLocale(navigator.languages.length > 0 ? navigator.languages : [navigator.language]);
        const options = withLocale(props, browserLocale.current);
        if (pivot.current === undefined) {
            pivot.current = createPivot(root, options);
        } else {
            pivot.current.update(options);
        }
    });
    useLayoutEffect(() => () => {
        pivot.current?.destroy();
        pivot.current = undefined;
        browserLocale.current = undefined;
    }, []);
    return createElement('div', { ref: element });
}
