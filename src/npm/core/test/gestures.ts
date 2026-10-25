// The selection gestures a case's steps name (tests/cases/README.md, "selection"), applied to the Script
// Core's Selection: read by the selection cases and by every kind whose cases begin with a selection.
import { expect } from 'vitest';
import type { GridDirection, GridExtent } from '../src/selection/cell';
import type { Selection } from '../src/selection/selection';
import { readCell, readRange, wholeNumber, type CellText, type RangeText } from './cases';

export type Step = Record<string, unknown>;

const cycleKeys = {
    'enter': ['columnMajor', false],
    'shift+enter': ['columnMajor', true],
    'tab': ['rowMajor', false],
    'shift+tab': ['rowMajor', true],
} as const;

/** The gesture a step names, applied to the selection. */
function gesture(selection: Selection, step: Step, extent: GridExtent): Selection {
    const at = (key: string) => readCell(step[key] as CellText);
    const answer = step['answer'] === undefined
        ? undefined
        : (() => readCell(step['answer'] as CellText));
    const int = (key: string) => wholeNumber(step, key);
    if ('click' in step) return selection.click(at('click'), extent);
    if ('extendTo' in step) return selection.extendTo(at('extendTo'), extent);
    if ('toggleRange' in step) return selection.toggleRange(at('toggleRange'), extent);
    if ('toggleColumn' in step) return selection.toggleColumn(step['toggleColumn'] as number, extent, int('focusRow'));
    if ('toggleRow' in step) return selection.toggleRow(step['toggleRow'] as number, extent, int('focusColumn'));
    if ('move' in step) return selection.move(step['move'] as GridDirection, extent);
    if ('extend' in step) return selection.extend(step['extend'] as GridDirection, extent);
    if ('moveToEdge' in step) return selection.moveToEdge(step['moveToEdge'] as GridDirection, extent, answer);
    if ('extendToEdge' in step) return selection.extendToEdge(step['extendToEdge'] as GridDirection, extent, answer);
    if ('moveByViewport' in step) return selection.moveByViewport(step['moveByViewport'] as number, extent);
    if ('extendByViewport' in step) return selection.extendByViewport(step['extendByViewport'] as number, extent);
    if ('selectAll' in step) {
        const page = step['selectAll'];
        return page === true
            ? selection.selectAll(extent)
            : selection.selectAll(extent, page as { firstRow: number; rowCount: number });
    }
    if ('selectWholeColumns' in step) return selection.selectWholeColumns(extent);
    if ('selectWholeRows' in step) return selection.selectWholeRows(extent);
    if ('collapseToFocus' in step) return selection.collapseToFocus(extent);
    if ('focusOn' in step) return selection.focusOn(at('focusOn'));
    if ('extendToColumn' in step) {
        return selection.extendToColumn(step['extendToColumn'] as number, extent, int('focusRowIfEmpty'));
    }
    if ('extendToRow' in step) {
        return selection.extendToRow(step['extendToRow'] as number, extent, int('focusColumnIfEmpty'));
    }
    if ('place' in step) return selection.place(readRange(step['place'] as RangeText), at('focus'), extent);
    if ('selectColumn' in step) return selection.selectColumn(step['selectColumn'] as number, extent, int('focusRow'));
    if ('selectRow' in step) return selection.selectRow(step['selectRow'] as number, extent, int('focusColumn'));
    throw new Error(`The step ${JSON.stringify(step)} names no gesture this runner knows.`);
}

/** A key that is pressed `times` times, or once for each Focus in `focuses`, which it is held to. */
function presses(selection: Selection, step: Step, press: (s: Selection) => Selection): Selection {
    const focuses = step['focuses'] as CellText[] | undefined;
    // oxlint-disable-next-line ex-grid/no-stand-in-default -- tests/cases/README.md: a key named with neither is pressed once
    const times = (step['times'] as number | undefined) ?? focuses?.length ?? 1;
    for (let i = 0; i < times; i++) {
        selection = press(selection);
        const expected = focuses?.[i];
        if (expected !== undefined) {
            expect(selection.focus, `the Focus after press ${i + 1}`).toEqual(readCell(expected));
        }
    }
    return selection;
}

/** The gesture or key a step names, applied; a step naming neither is a defect in the case. */
export function apply(selection: Selection, step: Step, extent: GridExtent): Selection {
    if ('cycle' in step) {
        const [order, backward] = cycleKeys[step['cycle'] as keyof typeof cycleKeys];
        return presses(selection, step, s => s.cycleFocus(order, backward, extent));
    }
    if ('nextCorner' in step) {
        return presses(selection, step, s => s.moveFocusToNextCorner(extent));
    }
    return gesture(selection, step, extent);
}

