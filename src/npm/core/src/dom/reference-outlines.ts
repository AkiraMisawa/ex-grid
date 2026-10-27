// Reference Outlines (ADR-0057), the fifth formula entry aid: while an edit is open, the Consumer answers the
// References in the editor's text, the core gives each a colour, and every range answered is outlined in the
// selection overlay (ADR-0008) in its colour. Beside them, the columns a Consumer asks for are outlined over all
// their rows, with or without an edit: a Linked Table's columns, in the grid that shows the table. The grid does
// not know what a Formula is. As the C# core's ExGrid.ReferenceOutlines.

import {
    ReferenceColouring, referenceColour, type GridEditorReference, type ReferenceColour, type ReferenceKeyColour,
} from '../cells/reference-colouring';
import type { GridExtent } from '../selection/cell';
import { sameRange, selectionRange, type SelectionRange } from '../selection/range';

/** A column a Consumer asks the grid to outline, and the colour to outline it in (ADR-0057): the colour is one
 * the core handed out, which `onReferenceKeyColoursChanged` told, never one the Consumer picks. */
export interface GridOutlinedColumn {
    /** The column's `name`, compared as written. */
    readonly column: string;
    /** The colour the References that read it wear. */
    readonly colour: ReferenceColour;
}

/** A rectangle to outline, and its colour. */
export type OutlinedRange = readonly [range: SelectionRange, colour: ReferenceColour];

/** What the Reference Outline options are, checked. */
export interface ReferenceOutlineOptions {
    readonly referencesIn: ((text: string) => readonly GridEditorReference[] | undefined) | undefined;
    readonly onReferenceKeyColoursChanged: ((keys: readonly ReferenceKeyColour[]) => void) | undefined;
    readonly outlinedColumns: readonly GridOutlinedColumn[] | undefined;
}

/** The options checked, refused by name where they are not what they say. */
export function referenceOutlineOptions(given: {
    readonly referencesIn?: ReferenceOutlineOptions['referencesIn'];
    readonly onReferenceKeyColoursChanged?: ReferenceOutlineOptions['onReferenceKeyColoursChanged'];
    readonly outlinedColumns?: readonly GridOutlinedColumn[] | undefined;
}): ReferenceOutlineOptions {
    // Read as unknown first: a Consumer in plain JavaScript hands whatever it hands.
    const { referencesIn, onReferenceKeyColoursChanged, outlinedColumns }: {
        readonly referencesIn?: unknown; readonly onReferenceKeyColoursChanged?: unknown; readonly outlinedColumns?: unknown;
    } = given;
    if (referencesIn !== undefined && typeof referencesIn !== 'function') {
        throw new TypeError('referencesIn is a function of the editor\'s text, answering its References (ADR-0057).');
    }
    if (onReferenceKeyColoursChanged !== undefined && typeof onReferenceKeyColoursChanged !== 'function') {
        throw new TypeError('onReferenceKeyColoursChanged is a function of the keys and their colours (ADR-0057).');
    }
    if (outlinedColumns !== undefined && !Array.isArray(outlinedColumns)) {
        throw new TypeError('outlinedColumns is a list of columns, each named with its colour (ADR-0057).');
    }
    const asked: readonly unknown[] | undefined = given.outlinedColumns;
    return {
        referencesIn: given.referencesIn,
        onReferenceKeyColoursChanged: given.onReferenceKeyColoursChanged,
        outlinedColumns: asked === undefined ? undefined : Object.freeze(asked.map(outlinedColumn)),
    };
}

// One column asked for, checked: a non-empty name and a place in the palette.
function outlinedColumn(asked: unknown, i: number): GridOutlinedColumn {
    if (typeof asked !== 'object' || asked === null) throw new TypeError(`outlinedColumns[${String(i)}] is not a column with a colour (ADR-0057).`);
    const { column, colour }: { readonly column?: unknown; readonly colour?: unknown } = asked;
    if (typeof column !== 'string' || column === '') {
        throw new TypeError(`outlinedColumns[${String(i)}] names no column: a name is a non-empty string (ADR-0057).`);
    }
    if (typeof colour !== 'number') throw new TypeError(`outlinedColumns[${String(i)}] has no colour: a place in the palette, 1 to 7 (ADR-0057).`);
    return Object.freeze({ column, colour: referenceColour(colour) });
}

/**
 * The Reference Outlines of one grid: the colouring of the open edit's text, asked of the Consumer only when the
 * text or the function changed since it was last asked, the columns asked for resolved to their places, and the
 * key colours the Consumer was last told, so a change is told once (DC-49).
 */
export class ReferenceOutlines {
    #colouring = ReferenceColouring.none;
    #colouredText: string | undefined;
    #colouredBy: ReferenceOutlineOptions['referencesIn'];
    #resolved: readonly OutlinedRange[] = [];
    #resolvedFrom: readonly GridOutlinedColumn[] | undefined;
    #resolvedOver: readonly string[] | undefined;
    #resolvedRows = -1;
    #told: readonly ReferenceKeyColour[] = [];
    #toldTo: ReferenceOutlineOptions['onReferenceKeyColoursChanged'];

    /** The colouring last asked for, without asking again. */
    get asked(): ReferenceColouring {
        return this.#colouring;
    }

    /** The text the colouring was last asked for, or none since an edit ended. */
    get askedFor(): string | undefined {
        return this.#colouredText;
    }

    /**
     * The colours of the References in `text`, the open edit's, or none while no edit is open or no function is
     * declared. An edit that ends forgets it: the next one asks afresh, whatever its text. An answer whose spans
     * do not lie inside the text, or overlap, is refused by name.
     */
    colouring(text: string | undefined, referencesIn: ReferenceOutlineOptions['referencesIn']): ReferenceColouring {
        if (text === undefined || referencesIn === undefined) {
            this.#colouring = ReferenceColouring.none;
            this.#colouredText = undefined;
            this.#colouredBy = undefined;
            return this.#colouring;
        }
        if (referencesIn !== this.#colouredBy || text !== this.#colouredText) {
            // Held before it is asked: an answer refused is refused once, where it was asked, and the text outlines
            // nothing until it changes, rather than every later pass throwing again.
            this.#colouredText = text;
            this.#colouredBy = referencesIn;
            this.#colouring = ReferenceColouring.none;
            this.#colouring = ReferenceColouring.of(text, referencesIn(text));
        }
        return this.#colouring;
    }

    /**
     * The columns asked for (ADR-0057), as the rectangles over their bodies, every row the grid has, in list order:
     * resolved again only when the list, the columns or the row count changed. A name the grid does not show is
     * outlined nowhere, and a column listed twice is outlined twice, the later over the earlier.
     */
    columns(asked: readonly GridOutlinedColumn[] | undefined, names: readonly string[], rowCount: number): readonly OutlinedRange[] {
        // The names are compared item by item: the grid reads them afresh each pass.
        if (asked === this.#resolvedFrom && rowCount === this.#resolvedRows && sameNames(names, this.#resolvedOver)) return this.#resolved;
        this.#resolvedFrom = asked;
        this.#resolvedOver = names;
        this.#resolvedRows = rowCount;
        this.#resolved = asked === undefined || rowCount === 0 ? [] : Object.freeze(asked.flatMap(({ column, colour }): OutlinedRange[] => {
            const at = names.indexOf(column);
            return at < 0 ? [] : [Object.freeze([selectionRange(0, at, rowCount, 1), colour] as const)];
        }));
        return this.#resolved;
    }

    /** Tells the Consumer the colour each key was given, when that changed since it was last told (ADR-0057,
     * DC-49): each key once, in order of first appearance, and none once the edit has ended. */
    tellKeys(colouring: ReferenceColouring, told: ReferenceOutlineOptions['onReferenceKeyColoursChanged']): void {
        // A handler handed over in place of another has been told nothing.
        if (told !== this.#toldTo) {
            this.#toldTo = told;
            this.#told = [];
        }
        if (told === undefined) return;
        const keys = colouring.keys;
        if (sameKeyColours(keys, this.#told)) return;
        this.#told = keys;
        told(keys);
    }

    /** Whether two colourings paint and tell the same: the same outlines and the same keys, in the same colours. */
    static same(a: ReferenceColouring, b: ReferenceColouring): boolean {
        return a.outlines.length === b.outlines.length && sameKeyColours(a.keys, b.keys)
            && a.outlines.every(([range, colour], i) => {
                const other = b.outlines[i];
                return other !== undefined && other[1] === colour && sameRange(other[0], range);
            });
    }
}

/** The part of `range` that lies on the grid, or `undefined` when none does: an outline is drawn over cells the
 * grid has, never over columns it would have to invent. */
export function withinExtent(range: SelectionRange, extent: GridExtent): SelectionRange | undefined {
    const rows = Math.min(range.topRow + range.rowCount, extent.rowCount) - range.topRow;
    const columns = Math.min(range.leftColumn + range.columnCount, extent.columnCount) - range.leftColumn;
    if (rows <= 0 || columns <= 0) return undefined;
    return rows === range.rowCount && columns === range.columnCount ? range : selectionRange(range.topRow, range.leftColumn, rows, columns);
}

function sameNames(a: readonly string[], b: readonly string[] | undefined): boolean {
    return b !== undefined && a.length === b.length && a.every((name, i) => name === b[i]);
}

// Whether two tellings hold the same keys in the same colours, in whatever order: each key is listed once.
function sameKeyColours(a: readonly ReferenceKeyColour[], b: readonly ReferenceKeyColour[]): boolean {
    return a.length === b.length && a.every(told => b.some(other => other.key === told.key && other.colour === told.colour));
}
