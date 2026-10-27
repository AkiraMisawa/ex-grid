// Point, the fourth editing state (ADR-0051): while the Consumer says the caret stands where a Reference can go,
// the arrows and the mouse move a pointing outline over the grid, painted in the selection overlay (ADR-0008), and
// the Consumer's Reference text for it is written at the caret. The Selection and the Focus do not move. What a
// key means here, and the options checked; the state is the grid's. As the C# core's ExGrid.Pointing.

import type { GridDirection } from '../selection/cell';
import type { SelectionRange } from '../selection/range';

/** Where the open edit stands with respect to Point (ADR-0058): in Point while `pointAt` says a Reference can go
 * at the caret or what Point wrote stands over unchanged text, and whether that was written from outside the grid
 * (`writePointedText`); `'none'` otherwise, and when no edit is open. */
export type GridPointState = 'none' | 'inPoint' | 'writtenFromOutside';

/** What F4 makes of the editor's text (ADR-0051): the whole new text, and the selection to place in it. */
export interface GridEditorRewrite {
    /** The whole new text. */
    readonly text: string;
    /** Where the selection placed in it starts. */
    readonly selectionStart: number;
    /** Where it ends; the caret's place when it is one. */
    readonly selectionEnd: number;
}

/** The Point options, checked. */
export interface PointOptions {
    readonly pointAt: ((text: string, caret: number) => boolean) | undefined;
    readonly referenceText: ((range: SelectionRange) => string) | undefined;
    readonly cycleReference: ((text: string, start: number, end: number) => GridEditorRewrite | undefined) | undefined;
    readonly onPointStateChanged: ((state: GridPointState) => void) | undefined;
}

/** The options checked, refused by name where they are not what they say, or where `pointAt` is given without the
 * Reference text it would write. */
export function pointOptions(given: {
    readonly pointAt?: PointOptions['pointAt'];
    readonly referenceText?: PointOptions['referenceText'];
    readonly cycleReference?: PointOptions['cycleReference'];
    readonly onPointStateChanged?: PointOptions['onPointStateChanged'];
}): PointOptions {
    // Read as unknown first: a Consumer in plain JavaScript hands whatever it hands.
    const loose: { readonly pointAt?: unknown; readonly referenceText?: unknown; readonly cycleReference?: unknown; readonly onPointStateChanged?: unknown } = given;
    for (const name of pointOptionNames) {
        const value = loose[name];
        if (value !== undefined && typeof value !== 'function') throw new TypeError(`${name} is a function (ADR-0051).`);
    }
    if (given.pointAt !== undefined && given.referenceText === undefined) {
        throw new TypeError('pointAt needs referenceText: pointing writes the Consumer\'s Reference text for the pointed range, and the grid has '
            + 'no name of its own for a position (ADR-0051).');
    }
    return { pointAt: given.pointAt, referenceText: given.referenceText, cycleReference: given.cycleReference, onPointStateChanged: given.onPointStateChanged };
}

/** A rewrite F4's Consumer answered, checked: its text a string, and its selection inside it. */
export function checkedRewrite(answer: unknown): GridEditorRewrite {
    if (typeof answer !== 'object' || answer === null) throw new TypeError('cycleReference answers a rewrite or nothing (ADR-0051).');
    const { text, selectionStart, selectionEnd }: { readonly text?: unknown; readonly selectionStart?: unknown; readonly selectionEnd?: unknown } = answer;
    if (typeof text !== 'string') throw new TypeError('A rewrite answers the whole new text (ADR-0051).');
    if (typeof selectionStart !== 'number' || typeof selectionEnd !== 'number' || !Number.isSafeInteger(selectionStart) || !Number.isSafeInteger(selectionEnd)
        || selectionStart < 0 || selectionEnd < selectionStart || selectionEnd > text.length) {
        throw new RangeError(`The rewrite's selection ${String(selectionStart)}..${String(selectionEnd)} does not lie inside its text of ${String(text.length)} characters (ADR-0051).`);
    }
    return { text, selectionStart, selectionEnd };
}

const pointOptionNames = ['pointAt', 'referenceText', 'cycleReference', 'onPointStateChanged'] as const;

/** How a key moves the outline: a step, a Shift step, to the grid's edge, Shift to the grid's edge, to the data
 * edge, or Shift to the data edge. */
export type PointMove = 'move' | 'extend' | 'edge' | 'extendToEdge' | 'dataEdge' | 'extendToDataEdge';

/**
 * What a key does to the outline while editing (ADR-0051): the arrows step it, Shift extends it, Home and End take it
 * to the row's ends, Shift+Home extends to the row's first column, and the Primary Modifier's arrows go to the data
 * edge the Consumer answers, with Shift or without (observed in Excel, run 18a and R1 (viii)). `undefined` for a key
 * Point does not take.
 */
export function pointStep(canonical: string): Step | undefined {
    return pointSteps.get(canonical);
}

type Step = { readonly direction: GridDirection; readonly move: PointMove };

const arrows: readonly (readonly [string, GridDirection])[] = [['ArrowUp', 'up'], ['ArrowDown', 'down'], ['ArrowLeft', 'left'], ['ArrowRight', 'right']];

const stepsOf = (prefix: string, move: PointMove): (readonly [string, Step])[] => arrows.map(([key, direction]) => [`${prefix}${key}`, { direction, move }]);

const pointSteps: ReadonlyMap<string, Step> = new Map<string, Step>([
    ...stepsOf('', 'move'),
    ...stepsOf('Shift+', 'extend'),
    ...stepsOf('Control+', 'dataEdge'),
    ...stepsOf('Control+Shift+', 'extendToDataEdge'),
    ['Home', { direction: 'left', move: 'edge' }],
    ['End', { direction: 'right', move: 'edge' }],
    ['Shift+Home', { direction: 'left', move: 'extendToEdge' }],
]);
