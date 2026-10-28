// The React Framework Adapter (ADR-0350 section 3): it renders the element the Script Core draws into,
// hands the core its props, and unmounts it. It holds no state of the grid's, draws nothing inside the
// instance root, and decides nothing. The rows are never React's: a component per row would repeat,
// through `memo`, the fight with re-rendering that ADR-0003 measured.

import { createElement, Fragment, useImperativeHandle, useLayoutEffect, useMemo, useRef, useState, type ReactElement, type Ref } from 'react';
import { createGrid, type GridHandle, type GridOptions } from '@ex-grid/core';
import { chromeMounts, type ChromeMountsMade, type ExGridChrome } from './chrome';
import { holdPortals, useGridPortals } from './portals';

/**
 * An ExGrid's props: the Script Core's options, whole. `locale` is the browser's language where not given,
 * read once when the grid is made (ADR-0370 section 10); the core never reads it on its own. `temporal` is
 * never the browser's: a grid with a Date column whose filter is offered is given one on every browser
 * alike, or refused on every browser alike (ADR-0350 section 16).
 */
export type ExGridProps<Row> = Omit<GridOptions<Row>, 'chrome'> & {
    /** A ref to what the Consumer may ask of the grid it rendered. */
    readonly ref?: Ref<ExGridApi<Row>>;
    /** The Chrome slots a Wrapper renders, each a render function of its context (ADR-0390). */
    readonly chrome?: ExGridChrome<Row>;
};

/**
 * What a ref to an ExGrid reaches: the Script Core's grid handle, less what the adapter itself does with it
 * (taking the props again, and destroying the grid on unmount). Asked of a grid not mounted, it throws, but for
 * the keyboard's hand-offs: `returnKeyboard` then does nothing, and `handKeyboardTo` focuses the control itself
 * (DC-61).
 */
export type ExGridApi<Row> = Omit<GridHandle<Row>, 'update' | 'destroy'>;

/** The props as the core takes them: the browser's language where the Consumer gave no locale. */
function withLocale<Row>(props: GridOptions<Row>, browserLocale: string): GridOptions<Row> {
    return props.locale === undefined ? { ...props, locale: browserLocale } : props;
}

/**
 * An Excel-like grid. The element it renders is the grid's instance root, which the Script Core owns; every
 * render hands the core the props again, and what did not change costs nothing (ADR-0003).
 */
export function ExGrid<Row>(props: ExGridProps<Row>): ReactElement {
    const { ref, chrome, ...options } = props;
    const element = useRef<HTMLDivElement>(null);
    const grid = useRef<GridHandle<Row> | undefined>(undefined);
    const browserLocale = useRef<string | undefined>(undefined);
    // The Template cells' content and the Chrome slots', rendered by this component into the slots the core keeps
    // (ADR-0020, ADR-0390).
    const portals = useGridPortals();
    // Made again only when the Consumer hands another chrome: the ExGrid renders for every portal change. A render
    // function handed again keeps its mount function.
    const [made] = useState<ChromeMountsMade>(() => new Map());
    const mounts = useMemo(() => chromeMounts(chrome, portals, made), [chrome, portals, made]);
    // Drawn before the browser paints, so a mounted grid is never seen empty.
    useLayoutEffect(() => {
        const root = element.current;
        if (root === null) throw new Error('Internal: React mounted no element for the grid to draw into.');
        holdPortals(root, portals);
        browserLocale.current ??= navigator.language;
        const taken = withLocale(mounts === undefined ? options : { ...options, chrome: mounts }, browserLocale.current);
        if (grid.current === undefined) {
            portals.begin();
            grid.current = createGrid(root, taken);
        } else {
            grid.current.update(taken);
        }
    });
    useImperativeHandle(ref, () => ({
        placeSelection: (range, focus, rowSequenceVersion) => mounted(grid.current).placeSelection(range, focus, rowSequenceVersion),
        placeMatch: (cell, rowSequenceVersion) => mounted(grid.current).placeMatch(cell, rowSequenceVersion),
        readView: () => mounted(grid.current).readView(),
        writePointedText: (text) => mounted(grid.current).writePointedText(text),
        takeBackPointedText: () => mounted(grid.current).takeBackPointedText(),
        showViewWith: (view, rowSequenceVersion) => { mounted(grid.current).showViewWith(view, rowSequenceVersion); },
        showNewView: (rowSequenceVersion, place) => { mounted(grid.current).showNewView(rowSequenceVersion, place); },
        // Before the grid is drawn no edit can be open on it.
        readEditText: () => grid.current?.readEditText(),
        focus: () => { mounted(grid.current).focus(); },
        // Before the grid is drawn there is no keyboard of its own to give back, and the control takes it by its
        // own focus, as the C# core's does before it attaches (DC-61).
        returnKeyboard: (from) => { grid.current?.returnKeyboard(from); },
        handKeyboardTo: (control, how) => {
            if (grid.current === undefined) control.focus({ preventScroll: how?.preventScroll === true });
            else grid.current.handKeyboardTo(control, how);
        },
        keyName: (event) => mounted(grid.current).keyName(event),
        openPopover: (content, label, closed) => mounted(grid.current).openPopover(content, label, closed),
        closePopover: () => { mounted(grid.current).closePopover(); },
        get canSummarize() {
            return mounted(grid.current).canSummarize;
        },
        refreshSummary: () => { mounted(grid.current).refreshSummary(); },
        inOrder: (handling) => mounted(grid.current).inOrder(handling),
        cancellableInOrder: (handling, cancelledTooLate) => mounted(grid.current).cancellableInOrder(handling, cancelledTooLate),
    }), []);
    useLayoutEffect(() => () => {
        // The content goes with the grid, and nothing renders for it again.
        portals.end();
        grid.current?.destroy();
        grid.current = undefined;
        browserLocale.current = undefined;
    }, [portals]);
    return createElement(Fragment, null, createElement('div', { ref: element }), ...portals.elements());
}

function mounted<Row>(grid: GridHandle<Row> | undefined): GridHandle<Row> {
    if (grid === undefined) throw new Error('This ExGrid is not mounted: a grid not drawn is asked nothing.');
    return grid;
}
