// What a Consumer declares about each cell: its Cell State (ADR-0006), and, for one that holds every cell (ADR-0050,
// items 6, 7, 11 and 15; ADR-0051), a cell's own kind, alignment, painted text and appearance, and the text an edit opens on. A Sheet is the
// Consumer this is for: its column `A` holds numbers and text side by side, each formatted as its cell says.
// Each declaration is asked per painted cell, so it is cheap; its identity is the change signal (ADR-0003/0006),
// so a Consumer holds each in a field and hands over a new one when its answers change.

import type { CellAppearance, CellTextMeasure, EdgeBorderOf } from '../cells/cell-appearance';
import { defaultChangeHighlightMs } from '../cells/change-highlight';
import type { CellChangedAt } from '../source/cell-change-times';
import type { CellAlign, CellState } from '../paint/cell-classes';
import { GridMetrics, type GridDensity } from '../grid-metrics';
import type { ColumnType } from '../query/columns';
import type { GridColumn } from './grid-column';

/** The per-cell declarations of a grid's options. */
export interface CellPaintOptions<Row> {
    /**
     * Cell Metadata (ADR-0006): asked for by (row, column) and answered as one of the five states the grid
     * understands, which it paints as a class and does not interpret further. The Consumer maps its own vocabulary
     * onto these five on the way in; a state nobody declared is refused rather than painted as an ordinary cell.
     * Its identity is the change signal, as a row's is (ADR-0003): hand a new function when the metadata changes,
     * because the same one answering otherwise leaves the states on screen where they are.
     */
    readonly cellState?: (row: Row, column: GridColumn<Row>) => CellState;
    /** A cell's kind (ADR-0050, item 6), in place of its column's: a Number or Date is right-aligned and becomes
     * `####` when it does not fit (ADR-0016); Text and Boolean show whole. */
    readonly cellType?: (row: Row, column: GridColumn<Row>) => ColumnType;
    /** A cell's alignment (ADR-0050, item 7), in place of its column's. */
    readonly cellAlign?: (row: Row, column: GridColumn<Row>) => CellAlign;
    /**
     * A cell's painted text (ADR-0050, item 11), or `undefined` for the value's own: asked with the column's
     * content width and the metrics the grid judges `####` by, so the Consumer can fit its text as Excel fits
     * General. A Number or Date still becomes `####` when what it answers does not fit. Only the painted cell
     * changes: copy, the editor and an Auto width read the value's own text, and where the two differ the
     * cell's accessible name is the value's text (ADR-0016, ADR-0033).
     */
    readonly paintedText?: (row: Row, column: GridColumn<Row>, contentWidthPx: number, metrics: CellTextMeasure) => string | undefined;
    /** A cell's Font, Fill and Borders (ADR-0050, item 15; ADR-0071), painted through generated classes. */
    readonly cellAppearance?: (row: Row, column: GridColumn<Row>) => CellAppearance;
    /** Which line is drawn where two cells record different lines on their shared edge; the upper (left)
     * cell's unless given. */
    readonly edgeBorder?: EdgeBorderOf;
    /** The Cell Editor's Font and Fill, where the editor looks unlike the cell (ADR-0050, item 15): a Sheet's
     * Number Format colour paints the formatted Value, not the Entry the editor shows (ADR-0071). The cell's
     * own `cellAppearance` unless given. */
    readonly editorAppearance?: (row: Row, column: GridColumn<Row>) => CellAppearance;
    /** The text an edit opens on (ADR-0051), or `undefined` for the value's: on a Sheet the Entry, so F2 on a
     * Formula's cell shows `=A1*2` rather than its Value. Typing still replaces the cell's text. */
    readonly editorText?: (row: Row, column: GridColumn<Row>) => string | undefined;
    /**
     * The Change Highlight (ADR-0068): when a value cell's shown value last changed, in milliseconds since the epoch
     * on the clock the grid reads, the page's `performance.timeOrigin + performance.now()` (the `GridClock` an
     * `InMemoryGridSource` stamps its changes with unless given another), asked by (row, column name) as `cellState` is, or `undefined` for a cell that did not
     * change. A value cell is painted with the class `ex-changed` while the time is before the answer plus
     * `changeHighlightDuration`, and the class is taken away in one step when that time comes: the grid keeps one
     * timer, for the earliest end among the marks it painted, and paints again only the rows whose marks end.
     * Nothing animates, and no live region announces a mark. An Action, Template or Mark cell is not asked. The grid
     * never compares values itself; a source with a Row Key offers one as its `cellChangedAt`. Its identity is the
     * change signal, as `cellState`'s is: hand a new one when the change times do.
     */
    readonly cellChangedAt?: CellChangedAt<Row>;
    /** How long a Change Highlight lasts after the change time `cellChangedAt` answers, in milliseconds: one second
     * unless given (ADR-0068). Zero marks only a change time still to come; a negative or endless one is refused. The
     * mark's colour is the Visual Token `--ex-change-highlight-background`, never an option (ADR-0027). */
    readonly changeHighlightDuration?: number;
}

/** The per-cell declarations taken and checked, one instance while none of them changed. */
export class CellPaint<Row> {
    /** No declaration: every cell is painted by its column. */
    static empty<Row>(): CellPaint<Row> {
        return new CellPaint<Row>({}, defaultChangeHighlightMs);
    }

    readonly cellState: CellPaintOptions<Row>['cellState'];
    readonly cellType: CellPaintOptions<Row>['cellType'];
    readonly cellAlign: CellPaintOptions<Row>['cellAlign'];
    readonly paintedText: CellPaintOptions<Row>['paintedText'];
    readonly cellAppearance: CellPaintOptions<Row>['cellAppearance'];
    readonly edgeBorder: EdgeBorderOf | undefined;
    readonly editorAppearance: CellPaintOptions<Row>['editorAppearance'];
    readonly editorText: CellPaintOptions<Row>['editorText'];
    readonly cellChangedAt: CellPaintOptions<Row>['cellChangedAt'];
    readonly changeHighlightDuration: number;

    private constructor(options: CellPaintOptions<Row>, changeHighlightDuration: number) {
        this.cellState = options.cellState;
        this.cellType = options.cellType;
        this.cellAlign = options.cellAlign;
        this.paintedText = options.paintedText;
        this.cellAppearance = options.cellAppearance;
        this.edgeBorder = options.edgeBorder;
        this.editorAppearance = options.editorAppearance;
        this.editorText = options.editorText;
        this.cellChangedAt = options.cellChangedAt;
        this.changeHighlightDuration = changeHighlightDuration;
    }

    /** The declarations in `options`, checked as an untyped caller may hand them; `previous` again while every
     * one is the same function, so nothing is painted again for a render that changed none of them. */
    static take<Row>(options: CellPaintOptions<Row>, previous: CellPaint<Row>): CellPaint<Row> {
        // Checked as an untyped caller may hand them, where they arrive (CONVENTIONS.md).
        const { cellState, cellType, cellAlign, paintedText, cellAppearance, edgeBorder, editorAppearance, editorText, cellChangedAt, changeHighlightDuration }: {
            readonly cellState?: unknown; readonly cellType?: unknown; readonly cellAlign?: unknown; readonly paintedText?: unknown; readonly cellAppearance?: unknown;
            readonly edgeBorder?: unknown; readonly editorAppearance?: unknown; readonly editorText?: unknown; readonly cellChangedAt?: unknown;
            readonly changeHighlightDuration?: unknown;
        } = options;
        const given = { cellState, cellType, cellAlign, paintedText, cellAppearance, edgeBorder, editorAppearance, editorText, cellChangedAt };
        for (const [name, value] of Object.entries(given)) {
            if (value !== undefined && typeof value !== 'function') {
                throw new TypeError(`${name} is a function (${name === 'cellState' ? 'ADR-0006' : name === 'cellChangedAt' ? 'ADR-0068' : 'ADR-0050'}).`);
            }
        }
        if (changeHighlightDuration !== undefined && (typeof changeHighlightDuration !== 'number' || !Number.isFinite(changeHighlightDuration) || changeHighlightDuration < 0)) {
            throw new RangeError('changeHighlightDuration is a finite number of zero or more milliseconds: a mark cannot end before the change it '
                + `marks, and one that never ends would hide every later change (ADR-0068), not ${typeof changeHighlightDuration === 'number' ? String(changeHighlightDuration) : typeof changeHighlightDuration}.`);
        }
        const duration = changeHighlightDuration ?? defaultChangeHighlightMs;
        return previous.cellState === cellState && previous.cellType === cellType && previous.cellAlign === cellAlign && previous.paintedText === paintedText
            && previous.cellAppearance === cellAppearance && previous.edgeBorder === edgeBorder
            && previous.editorAppearance === editorAppearance && previous.editorText === editorText
            && previous.cellChangedAt === cellChangedAt && previous.changeHighlightDuration === duration
            ? previous
            : new CellPaint(options, duration);
    }

    /** Whether rows painted under `other` paint as under these: only the declarations a row reads are compared, so
     * a new `editorText` or `editorAppearance` paints no row again (ADR-0003). */
    paintsRowsAs(other: CellPaint<Row>): boolean {
        return other === this || (other.cellState === this.cellState && other.cellType === this.cellType && other.cellAlign === this.cellAlign
            && other.paintedText === this.paintedText && other.cellAppearance === this.cellAppearance && other.edgeBorder === this.edgeBorder
            && other.cellChangedAt === this.cellChangedAt
            && (this.cellChangedAt === undefined || other.changeHighlightDuration === this.changeHighlightDuration));
    }

    /** Whether any cell is asked for an appearance. */
    get paintsAppearance(): boolean {
        return this.cellAppearance !== undefined || this.editorAppearance !== undefined;
    }
}

/** The Cell Metrics a grid of `density` judges its cells by (ADR-0016, ADR-0028), for a Consumer that sizes its
 * columns in the grid's digits before the grid is drawn, as a Sheet sizes Excel's 8.43 characters. */
export function cellTextMeasure(density: GridDensity): CellTextMeasure {
    return GridMetrics.resolve({ density }).cellMetrics;
}
