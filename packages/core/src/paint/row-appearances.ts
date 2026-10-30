// Each painted row's appearance, resolved once per pass outside the row's paint (ADR-0050, item 15; ADR-0071),
// as the C# core's CellAppearances does. A cell's lines come from both cells of each edge (its top line is the
// edge it shares with the row above, and a thick line there reaches into it), so a row is resolved from its own
// answers and those of the rows either side.
//
// The Consumer is asked once per row instance for the painted columns and one more on each side, and asked
// again only when the row instance, the lookup, the columns or the painted columns change: the identities
// ADR-0003/0006 make the change signal. A row resolved again keeps the instance it had when it would paint the
// same, so a new instance repaints its own row, and its neighbours only where a line they share has moved.

import type { CellAppearance, CellBorder, EdgeBorderOf } from '../cells/cell-appearance';
import { itemAt } from '../must';
import type { ColumnRange } from '../columns/column-geometry';
import { AppearanceStyles, lineShare, noShare, reachesPast, type Share } from './appearance-styles';

const none: CellAppearance = Object.freeze({});
const noLine: CellBorder = Object.freeze({ style: 'none' });

/** What one row paints of its cells' appearance: the appearance class of each painted cell, and which are bold. */
export class RowAppearance {
    readonly #pinned: number;
    readonly #scrollStart: number;
    readonly #classes: readonly (string | undefined)[];
    readonly #bold: readonly boolean[];

    constructor(pinned: number, scrollStart: number, classes: readonly (string | undefined)[], bold: readonly boolean[]) {
        this.#pinned = pinned;
        this.#scrollStart = scrollStart;
        this.#classes = classes;
        this.#bold = bold;
    }

    /** The cell's appearance classes, or undefined for none. */
    classOf(column: number): string | undefined {
        const slot = this.#slot(column);
        return slot < 0 ? undefined : this.#classes[slot];
    }

    /** Whether the cell is bold, so judged by the bold widths (ADR-0016). */
    isBold(column: number): boolean {
        const slot = this.#slot(column);
        return slot >= 0 && this.#bold[slot] === true;
    }

    /** Whether `other` paints exactly what this does, for the same painted columns. The classes are interned, so
     * comparing them is comparing what they paint. */
    sameAs(other: RowAppearance): boolean {
        if (this.#pinned !== other.#pinned || this.#scrollStart !== other.#scrollStart || this.#classes.length !== other.#classes.length) {
            return false;
        }
        return this.#classes.every((name, i) => name === other.#classes[i] && this.#bold[i] === other.#bold[i]);
    }

    // The painted cells in order: the Pinned Columns, then the scrollable ones on screen.
    #slot(column: number): number {
        const slot = column < this.#pinned ? column : this.#pinned + column - this.#scrollStart;
        return slot >= 0 && slot < this.#classes.length && (column < this.#pinned || column >= this.#scrollStart) ? slot : -1;
    }
}

interface Entry {
    readonly context: number;
    readonly row: unknown;
    readonly above: unknown;
    readonly below: unknown;
    readonly appearance: RowAppearance | undefined;
}

/** One grid's resolver of row appearances, with the classes and stylesheet they name. */
export class RowAppearances<Row> {
    /** The grid's classes and stylesheet. */
    readonly styles = new AppearanceStyles();

    #byPosition = new Map<number, Entry>();
    #asked = new Map<Row, readonly CellAppearance[]>();
    #lookup: ((row: Row, column: number) => CellAppearance) | undefined;
    #edge: EdgeBorderOf | undefined;
    #columnCount = 0;
    #pinned = 0;
    #scrollable: ColumnRange | undefined;
    #context = 0;
    // The columns asked about: the Pinned Columns and the one after them, then the scrollable columns on screen
    // and one either side, every painted cell's neighbours.
    #pinnedEnd = 0;
    #scrollFrom = 0;
    #scrollTo = 0;

    /**
     * Starts one pass over the rows: what is asked, of which columns. A change of any of it forgets every answer,
     * since the answers or the cells they are for have changed; what each row painted is kept, so a row whose
     * cells resolve the same keeps its instance.
     */
    begin(lookup: (row: Row, column: number) => CellAppearance, lookupIdentity: unknown, edge: EdgeBorderOf | undefined,
        columnsIdentity: unknown, columnCount: number, pinned: number, scrollable: ColumnRange | undefined): void {
        const key = [lookupIdentity, edge, columnsIdentity, pinned, scrollable?.start, scrollable?.count];
        if (!sameKey(key, this.#key)) {
            this.#key = key;
            this.#lookup = lookup;
            this.#edge = edge;
            this.#columnCount = columnCount;
            this.#pinned = Math.min(pinned, columnCount);
            this.#scrollable = scrollable;
            this.#context += 1;
            this.#pinnedEnd = Math.min(this.#pinned + 1, columnCount);
            this.#scrollFrom = scrollable === undefined ? 0 : Math.max(scrollable.start - 1, 0);
            this.#scrollTo = scrollable === undefined ? 0 : Math.min(scrollable.start + scrollable.count + 1, columnCount);
            this.#asked = new Map();
        }
        this.#passAsked = new Map();
        this.#passPositions = new Map();
    }

    #key: readonly unknown[] = [];
    // What this pass asked and resolved: only rows painted by the last pass are kept, so a fling replaces them
    // all, and a slow scroll keeps all but one.
    #passAsked = new Map<Row, readonly CellAppearance[]>();
    #passPositions = new Map<number, Entry>();

    /** Ends the pass: what it did not paint is forgotten. */
    end(): void {
        this.#asked = this.#passAsked;
        this.#byPosition = this.#passPositions;
    }

    /** The appearance of the row at `position`, from the rows above and below it (undefined where none is held),
     * or undefined when none of its painted cells has any. */
    at(position: number, row: Row, above: Row | undefined, below: Row | undefined): RowAppearance | undefined {
        const entry = this.#byPosition.get(position);
        if (entry !== undefined && entry.context === this.#context && entry.row === row && entry.above === above && entry.below === below) {
            this.#passPositions.set(position, entry);
            this.#keepAsked(row);
            if (above !== undefined) this.#keepAsked(above);
            if (below !== undefined) this.#keepAsked(below);
            return entry.appearance;
        }
        let resolved = this.#resolve(row, above, below);
        const previous = entry?.appearance;
        if (previous !== undefined && resolved !== undefined && previous.sameAs(resolved)) resolved = previous;
        this.#passPositions.set(position, { context: this.#context, row, above, below, appearance: resolved });
        return resolved;
    }

    #keepAsked(row: Row): void {
        const cells = this.#asked.get(row);
        if (cells !== undefined) this.#passAsked.set(row, cells);
    }

    #resolve(row: Row, above: Row | undefined, below: Row | undefined): RowAppearance | undefined {
        const own = this.#ask(row);
        const up = above === undefined ? undefined : this.#ask(above);
        const down = below === undefined ? undefined : this.#ask(below);
        const scrollStart = this.#scrollable === undefined ? 0 : this.#scrollable.start;
        const painted = this.#pinned + (this.#scrollable === undefined ? 0 : this.#scrollable.count);
        const classes: (string | undefined)[] = [];
        const bold: boolean[] = [];
        let any = false;
        for (let slot = 0; slot < painted; slot++) {
            const c = slot < this.#pinned ? slot : scrollStart + slot - this.#pinned;
            if (c >= this.#columnCount) {
                classes.push(undefined);
                bold.push(false);
                continue;
            }
            const cell = this.#at(own, c);
            const right = this.#at(own, c + 1);
            const left = this.#at(own, c - 1);
            const cellAbove = this.#at(up, c);
            const cellBelow = this.#at(down, c);
            const bottomLine = this.#choose(cell.bottom ?? noLine, cellBelow.top ?? noLine);
            const rightLine = this.#choose(cell.right ?? noLine, right.left ?? noLine);
            const topLine = this.#choose(cellAbove.bottom ?? noLine, cell.top ?? noLine);
            const leftLine = this.#choose(left.right ?? noLine, cell.left ?? noLine);
            // This cell holds the gridline of its bottom and right edges: a line there is its share. Beneath any
            // line a Fill covers that gridline: the lower (right) cell's where it has one, which Excel paints over
            // the upper (left) cell's (the fourteenth Windows run, case 16), else this cell's own.
            const top: Share = reachesPast(topLine.style) ? share(topLine) : noShare;
            const leftShare: Share = reachesPast(leftLine.style) ? share(leftLine) : noShare;
            const bottomCover = cellBelow.fill !== cell.fill ? cellBelow.fill : undefined;
            const rightCover = right.fill !== cell.fill ? right.fill : undefined;
            const name = this.styles.classFor(cell, top, share(rightLine), share(bottomLine), leftShare, rightCover, bottomCover);
            classes.push(name);
            bold.push(cell.bold === true);
            any ||= name !== undefined || cell.bold === true;
        }
        return any ? new RowAppearance(this.#pinned, scrollStart, classes, bold) : undefined;
    }

    /** The line drawn on an edge: the one recorded, where one cell records it; the Consumer's answer where both
     * record different lines; the upper or left one without an answer (`CONTEXT.md`, Border). */
    #choose(upperOrLeft: CellBorder, lowerOrRight: CellBorder): CellBorder {
        if (upperOrLeft.style === 'none') return lowerOrRight;
        if (lowerOrRight.style === 'none' || sameLine(upperOrLeft, lowerOrRight)) return upperOrLeft;
        return this.#edge === undefined ? upperOrLeft : checkedBorder(this.#edge(upperOrLeft, lowerOrRight));
    }

    #ask(row: Row): readonly CellAppearance[] {
        const kept = this.#passAsked.get(row) ?? this.#asked.get(row);
        if (kept !== undefined) {
            this.#passAsked.set(row, kept);
            return kept;
        }
        const lookup = this.#lookup;
        if (lookup === undefined) throw new Error('Internal: a row appearance was asked for before a pass began.');
        const cells: CellAppearance[] = [];
        for (let c = 0; c < this.#pinnedEnd; c++) cells.push(checkedAppearance(lookup(row, c)));
        for (let c = this.#scrollFrom; c < this.#scrollTo; c++) cells.push(c < this.#pinnedEnd ? itemAt(cells, c) : checkedAppearance(lookup(row, c)));
        const frozen = Object.freeze(cells);
        this.#passAsked.set(row, frozen);
        return frozen;
    }

    #at(cells: readonly CellAppearance[] | undefined, column: number): CellAppearance {
        if (cells === undefined || column < 0 || column >= this.#columnCount) return none;
        if (column < this.#pinnedEnd) return itemAt(cells, column);
        if (column >= this.#scrollFrom && column < this.#scrollTo) return itemAt(cells, this.#pinnedEnd + column - this.#scrollFrom);
        return none;
    }
}

const share = (line: CellBorder): Share => lineShare(line.style, line.style === 'none' ? 0 : colourOf(line));

// oxlint-disable-next-line ex-grid/no-stand-in-default -- ADR-0071: a line with no colour chosen is Excel's black
const colourOf = (line: CellBorder): number => line.colour ?? 0;

const sameLine = (a: CellBorder, b: CellBorder): boolean => a.style === b.style && colourOf(a) === colourOf(b);

function sameKey(a: readonly unknown[], b: readonly unknown[]): boolean {
    return a.length === b.length && a.every((item, i) => item === b[i]);
}

const rgbOk = (value: unknown): boolean => value === undefined || (typeof value === 'number' && Number.isInteger(value) && value >= 0 && value <= 0xffffff);

const styles: ReadonlySet<string> = new Set(['none', 'hair', 'thin', 'medium', 'thick', 'double', 'dotted', 'dashed', 'dashDot', 'dashDotDot',
    'mediumDashed', 'mediumDashDot', 'mediumDashDotDot', 'slantedDashDot']);

// A Consumer's answer, checked where it arrives (CONVENTIONS.md): a colour that is not RGB, or a line style that
// is not Excel's, is refused by name rather than painted as something else.
export function checkedAppearance(appearance: CellAppearance): CellAppearance {
    const given: unknown = appearance;
    if (typeof given !== 'object' || given === null) throw new TypeError('cellAppearance answers an appearance object (ADR-0050, item 15).');
    if (!rgbOk(appearance.fontColour) || !rgbOk(appearance.fill)) {
        throw new RangeError('A Font colour and a Fill are RGB numbers, 0x000000 to 0xFFFFFF (ADR-0050, item 15).');
    }
    for (const side of [appearance.top, appearance.right, appearance.bottom, appearance.left]) {
        if (side !== undefined) checkedBorder(side);
    }
    return appearance;
}

function checkedBorder(line: CellBorder): CellBorder {
    const given: unknown = line;
    if (typeof given !== 'object' || given === null) throw new TypeError('A Border is { style, colour } (ADR-0050, item 15).');
    const { style, colour }: { readonly style: unknown; readonly colour?: unknown } = line;
    if (typeof style !== 'string' || !styles.has(style)) throw new RangeError(`Not a line style: ${String(style)} (ADR-0071).`);
    if (!rgbOk(colour)) throw new RangeError('A line\'s colour is an RGB number, 0x000000 to 0xFFFFFF (ADR-0050, item 15).');
    return line;
}

