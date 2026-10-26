// The React Framework Adapter of ExBoard's Script Core (ADR-0370 section 1): it renders the element the Board draws
// into, hands the Board its props, and unmounts it. It holds no state of the Board's, draws nothing inside its root,
// and decides nothing.

import { createElement, useLayoutEffect, useRef, type ReactElement } from 'react';
import { createBoard, type BoardHandle, type BoardOptions } from '@ex-grid/board';
import { browserPivotLocale } from '@ex-grid/pivot';

/**
 * An ExBoard's props: the Script Core's options, whole. `locale` is the browser's language where not given, the first
 * of its languages a pivot writes in, read once, the first time the Board is given none (ADR-0370 section 10); the core
 * never reads it on its own.
 */
export type ExBoardProps = BoardOptions;

/** The props as the core takes them: the browser's language where the Consumer gave no locale. */
function withLocale(props: ExBoardProps, browserLocale: string | undefined): BoardOptions {
    if (props.locale !== undefined) return props;
    if (browserLocale === undefined) throw new Error('Internal: the browser\'s locale was not read for a Board given none.');
    return { ...props, locale: browserLocale };
}

/**
 * A Board (ADR-0340): its grids, pivots and charts laid out in its rows, drawn by ExGrid, ExPivot and ExChart. The
 * element it renders is the Board's root, which the Script Core owns; every render hands the core the props again, and
 * what did not change costs nothing.
 */
export function ExBoard(props: ExBoardProps): ReactElement {
    const element = useRef<HTMLDivElement>(null);
    const board = useRef<BoardHandle | undefined>(undefined);
    const browserLocale = useRef<string | undefined>(undefined);
    // Drawn before the browser paints, so a mounted Board is never seen empty.
    useLayoutEffect(() => {
        const root = element.current;
        if (root === null) throw new Error('Internal: React mounted no element for the Board to draw into.');
        // The browser is asked only where the Consumer gave no locale: one that did needs nothing of it.
        if (props.locale === undefined) browserLocale.current ??= browserPivotLocale(navigator.languages.length > 0 ? navigator.languages : [navigator.language]);
        const options = withLocale(props, browserLocale.current);
        if (board.current === undefined) {
            board.current = createBoard(root, options);
        } else {
            board.current.update(options);
        }
    });
    useLayoutEffect(() => () => {
        board.current?.destroy();
        board.current = undefined;
        browserLocale.current = undefined;
    }, []);
    return createElement('div', { ref: element });
}
