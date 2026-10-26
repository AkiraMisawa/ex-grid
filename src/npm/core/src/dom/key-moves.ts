// A key that moves or selects, answered over the Selection as it stands (ADR-0012): the C# core's
// MoveByKey, without the view. What it returns is the Selection the key leaves and what the grid then
// keeps in view; the grid applies both.

import type { GridKeyAction, GridKeyKind } from '../keys/grid-keys';
import { unreachable } from '../must';
import { cell, type CellPosition, type GridExtent } from '../selection/cell';
import type { SelectionRange } from '../selection/range';
import type { EdgeAnswer, Selection } from '../selection/selection';

/** What a move asks the grid to keep in view (ADR-0012, ADR-0052). */
export interface KeyReveal {
    /** The cell to reveal: the Extent while extending, the Focus otherwise. */
    readonly target: CellPosition;
    /** While extending, the range after the move: an axis it spans end to end is not scrolled for. */
    readonly extending: SelectionRange | undefined;
    /** Whether the key means "go to the beginning of the row": the Viewport's left edge goes back to 0. */
    readonly rowStart: boolean;
    /** The rows PageUp and PageDown move the Viewport by, alongside the Focus; 0 for every other key. */
    readonly stepRows: number;
}

/** A key's answer: the Selection it leaves and what to keep in view. */
export interface KeyMove {
    readonly selection: Selection;
    readonly reveal: KeyReveal;
}

/** The kinds a key moves or selects by, which {@link moveByKey} answers. */
export const movingKinds: ReadonlySet<GridKeyKind> = new Set<GridKeyKind>([
    'move', 'extend', 'moveToEdge', 'extendToEdge', 'cycle', 'selectAll', 'selectWholeColumns', 'selectWholeRows',
    'moveToCorner', 'moveByViewport', 'extendByViewport', 'engage', 'moveFocusToNextCorner', 'revealFocus', 'collapseToFocus',
]);

const movesTheFocus: ReadonlySet<GridKeyKind> = new Set<GridKeyKind>([
    'move', 'extend', 'moveToEdge', 'extendToEdge', 'cycle', 'moveByViewport', 'extendByViewport',
]);

const extending: ReadonlySet<GridKeyKind> = new Set<GridKeyKind>(['extend', 'extendToEdge', 'extendByViewport']);

/**
 * Answers a key of {@link movingKinds}, or `undefined` when it changes nothing. `start` is where a key
 * pressed with nothing selected starts: the first cell on screen, which the view answers, not the
 * Selection; `undefined` when nothing is painted. `viewportRows` is how many rows PageUp and PageDown move.
 */
export function moveByKey(
    action: GridKeyAction, current: Selection, extent: GridExtent, start: CellPosition | undefined, viewportRows: number,
    edge?: EdgeAnswer,
): KeyMove | undefined {
    if (extent.rowCount <= 0 || extent.columnCount <= 0) return undefined;
    let from = current;
    const fromEmpty = current.isEmpty;
    if (fromEmpty) {
        // Where a keyboard user starts is a question about the view: the first cell on screen, without
        // moving the Viewport (ADR-0012). After a sort drops the Selection (ADR-0011), every key still works.
        if (start === undefined) return undefined;
        from = current.click(start, extent);
    }

    // Ctrl+Backspace reveals the Focus and changes nothing else (ADR-0052); from no Focus it places one.
    if (action.kind === 'revealFocus') {
        return { selection: from, reveal: { target: focusOf(from), extending: undefined, rowStart: false, stepRows: 0 } };
    }

    const rows = action.direction === 'up' ? -viewportRows : viewportRows;
    // A key that moves the Focus, pressed with no Focus, only places one (ADR-0012): the first Down selects
    // the first cell, not the second. A key that names a whole region needs no start and does what it says.
    const next = fromEmpty && movesTheFocus.has(action.kind) ? from : step(action, from, extent, rows, edge);
    if (next.equals(current)) return undefined;

    const extendsRange = extending.has(action.kind);
    const target = extendsRange ? next.extent : focusOf(next);
    if (target === undefined) throw new Error('A Selection a key leaves holds an Extent (ADR-0052).');
    return {
        selection: next,
        reveal: {
            target,
            extending: extendsRange ? next.focusRange : undefined,
            rowStart: namesTheStartOfTheRow(action),
            // PageUp and PageDown move the Viewport by the rows the Focus moved (ADR-0012); a press that
            // only placed the Focus moves nothing.
            stepRows: !fromEmpty && (action.kind === 'moveByViewport' || action.kind === 'extendByViewport') ? rows : 0,
        },
    };
}

function focusOf(selection: Selection): CellPosition {
    const focus = selection.focus;
    if (focus === undefined) throw new Error('A Selection a key leaves holds a Focus (ADR-0012).');
    return focus;
}

function step(action: GridKeyAction, current: Selection, extent: GridExtent, rows: number, edge: EdgeAnswer | undefined): Selection {
    switch (action.kind) {
        case 'move': return current.move(action.direction, extent);
        case 'extend': return current.extend(action.direction, extent);
        case 'moveToEdge': return current.moveToEdge(action.direction, extent, edge);
        case 'extendToEdge': return current.extendToEdge(action.direction, extent, edge);
        case 'cycle': return current.cycleFocus(action.order, action.backward, extent);
        case 'selectAll': return current.selectAll(extent);
        case 'selectWholeColumns': return current.selectWholeColumns(extent);
        case 'selectWholeRows': return current.selectWholeRows(extent);
        case 'moveToCorner':
            return current.click(action.backward ? cell(0, 0) : cell(extent.rowCount - 1, extent.columnCount - 1), extent);
        case 'moveByViewport': return current.moveByViewport(rows, extent);
        case 'extendByViewport': return current.extendByViewport(rows, extent);
        case 'moveFocusToNextCorner': return current.moveFocusToNextCorner(extent);
        case 'collapseToFocus': return current.collapseToFocus(extent);
        // Space only places a Focus where there is none; the rest are not keys that move.
        case 'engage':
        case 'revealFocus':
        case 'none':
        case 'leave':
        case 'openContextMenu':
        case 'openColumnMenu':
        case 'undo':
        case 'redo':
        case 'clear':
        case 'clearAndEdit':
        case 'fillDown':
        case 'fillRight':
        case 'find':
        case 'replace': return current;
        default: return unreachable(action.kind);
    }
}

/** Whether a key means "go to the beginning" rather than "move one step": Home and Ctrl+Left for the row,
 * Ctrl+Home for the whole result, with their Shift forms. */
function namesTheStartOfTheRow(action: GridKeyAction): boolean {
    switch (action.kind) {
        case 'moveToEdge':
        case 'extendToEdge': return action.direction === 'left';
        case 'moveToCorner': return action.backward;
        case 'none':
        case 'move':
        case 'extend':
        case 'cycle':
        case 'selectAll':
        case 'selectWholeColumns':
        case 'selectWholeRows':
        case 'moveByViewport':
        case 'extendByViewport':
        case 'engage':
        case 'leave':
        case 'openContextMenu':
        case 'openColumnMenu':
        case 'undo':
        case 'redo':
        case 'clear':
        case 'clearAndEdit':
        case 'fillDown':
        case 'fillRight':
        case 'find':
        case 'replace':
        case 'moveFocusToNextCorner':
        case 'revealFocus':
        case 'collapseToFocus': return false;
        default: return unreachable(action.kind);
    }
}
