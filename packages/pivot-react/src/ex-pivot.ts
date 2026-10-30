// The React Framework Adapter of ExPivot's Script Core (ADR-0370 section 1): it renders the element the pivot
// draws into, hands the pivot its props, and unmounts it. It holds no state of the pivot's, draws nothing
// inside its root but the Chrome slots it is handed, and decides nothing.

import { createElement, Fragment, useLayoutEffect, useMemo, useRef, useState, type ReactElement } from 'react';
import { browserPivotLocale, createPivot, type PivotHandle, type PivotViewOptions } from '@ex-grid/pivot';
import { useGridPortals } from '@ex-grid/react';
import { pivotChromeMounts, type ExPivotChrome, type PivotMountsMade } from './chrome';

/**
 * An ExPivot's props: the Script Core's options, whole, its `chrome` as render functions. `locale` is the browser's
 * language where not given, the first of its languages the pivot writes in, read once, the first time the pivot is
 * given none (ADR-0370 section 10); the core never reads it on its own.
 */
export type ExPivotProps = Omit<PivotViewOptions, 'chrome'> & {
    /** The parts a Wrapper's Chrome, or the Consumer's own, draws in place of the built-in markup (ADR-0390). */
    readonly chrome?: ExPivotChrome;
};

/** The props as the core takes them: the browser's language where the Consumer gave no locale. */
function withLocale(props: PivotViewOptions, browserLocale: string | undefined): PivotViewOptions {
    if (props.locale !== undefined) return props;
    if (browserLocale === undefined) throw new Error('Internal: the browser\'s locale was not read for a pivot given none.');
    return { ...props, locale: browserLocale };
}

// Whether `b` hands the pivot what `a` did, option by option: a render of the ExPivot's own, for a portal of its
// Chrome slots, asks the pivot nothing.
function sameOptions(a: PivotViewOptions, b: PivotViewOptions): boolean {
    const keys = Object.keys(a);
    return keys.length === Object.keys(b).length && keys.every(key => Object.is(Reflect.get(a, key), Reflect.get(b, key)));
}

/**
 * Excel's PivotTable, drawn by ExGrid, beside its Field List. The element it renders is the pivot's root,
 * which the Script Core owns; every render hands the core the props again, and what did not change asks
 * nothing again.
 */
export function ExPivot(props: ExPivotProps): ReactElement {
    const { chrome, ...given } = props;
    const element = useRef<HTMLDivElement>(null);
    const pivot = useRef<PivotHandle | undefined>(undefined);
    const browserLocale = useRef<string | undefined>(undefined);
    // The options the pivot was handed last.
    const handed = useRef<PivotViewOptions | undefined>(undefined);
    // The Chrome slots' content, rendered by this component into the slots the pivot and its grids keep (ADR-0390).
    const portals = useGridPortals();
    // A render function handed again keeps its mount function, so the pivot does not draw its part again.
    const [made] = useState<PivotMountsMade>(() => ({ pivot: new Map(), grid: new Map() }));
    const mounts = useMemo(() => pivotChromeMounts(chrome, portals, made), [chrome, portals, made]);
    // Drawn before the browser paints, so a mounted pivot is never seen empty.
    useLayoutEffect(() => {
        const root = element.current;
        if (root === null) throw new Error('Internal: React mounted no element for the pivot to draw into.');
        // The browser is asked only where the Consumer gave no locale: one that did needs nothing of it.
        if (given.locale === undefined) browserLocale.current ??= browserPivotLocale(navigator.languages.length > 0 ? navigator.languages : [navigator.language]);
        const options = withLocale(mounts === undefined ? given : { ...given, chrome: mounts }, browserLocale.current);
        if (pivot.current === undefined) {
            portals.begin();
            pivot.current = createPivot(root, options);
        } else if (handed.current === undefined || !sameOptions(handed.current, options)) {
            pivot.current.update(options);
        }
        handed.current = options;
    });
    useLayoutEffect(() => () => {
        // The portals go first: the contents the pivot takes away as it goes ask for no render of a component gone.
        portals.end();
        pivot.current?.destroy();
        pivot.current = undefined;
        handed.current = undefined;
        browserLocale.current = undefined;
    }, [portals]);
    return createElement(Fragment, null, createElement('div', { ref: element }), ...portals.elements());
}
