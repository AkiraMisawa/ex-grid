import { Sheet, type CellAddress, type SheetDocument, type SheetDocumentAxisFormat, type SheetDocumentColumnWidth } from '@ex-grid/sheet-engine';
import type { XlsxColumnInput } from '@ex-grid/data-xlsx';
import { widthToFile } from './column-width-unit';
import { defaultFormat, FormatParts } from './format-parts';
import { elementAt } from './must';
import type { StyleTable } from './style-table';

// What a worksheet writes for a Sheet's Cell Formats and column widths (ADR-0330 §4, ADR-0046, ADR-0047, ADR-0071), as
// ExSheet.Xlsx's SheetFormatPlan plans it: its columns' widths and formats, its rows' formats, and the format each cell
// element is written with.
//
// A Sheet records a format part by part at three levels, cell over row over column; a file records one whole format at each,
// and a cell element's format stands alone. So a column writes its own level over the default, a row its own level over the
// default (in the file a row's format stands in place of its columns'), and every cell the Sheet records writes the format the
// Sheet shows there (`style`). Where a formatted row crosses a formatted column that records a part the row leaves to it, what
// the Sheet shows there is not the row's format, so a cell of no value is written there with the format it shows: a crossing.
// A width is written in the file's unit, marked fitted (`bestFit`) where an entry widened it; a column that records a format
// and no width is written at the default width, not marked as set.
//
// The crossings are what a Sheet formatted on many rows and many columns costs: rows × columns cells. They are counted first,
// from the runs alone (`crossings`), in time that grows with the runs and not with the cells, so that the writer refuses a
// Sheet that would write more cells than a file is read with before a cell is written; only then are their formats found
// (`build`).

/** A column range where a formatted row crosses a formatted column and shows another format than the row's. */
export interface Crossing {
    readonly first: number;
    readonly last: number;
    /** The cell format index its cells are written with. */
    readonly style: number;
}

/** A run of formatted rows, as the file writes them: each row's format, and the column ranges it crosses. */
export interface RowRun {
    readonly first: number;
    readonly last: number;
    /** The cell format index each row is written with. */
    readonly style: number;
    /** The column ranges, in order, where a cell of no value is written in each row of the run. */
    readonly crossings: readonly Crossing[];
}

const allParts = 0b11111;

interface ColumnRun { readonly first: number; readonly last: number; readonly level: FormatParts; readonly parts: number }

interface FormattedRows { readonly first: number; readonly last: number; readonly level: FormatParts; readonly open: number }

export class SheetFormatPlan {
    readonly #sheet: SheetDocument;
    readonly #columnLevels = new Map<number, FormatParts>();
    /** Each formatted column's parts other than the default's, as a mask. */
    readonly #columnParts = new Map<number, number>();
    /** The formatted column runs, in order, each with the parts it records other than the default's, as a mask. */
    readonly #columnRuns: ColumnRun[] = [];
    /** The formatted row runs, in order, each with the parts it leaves to its columns, as a mask. */
    readonly #rowRuns: FormattedRows[] = [];
    /** For each mask of parts a row leaves open, the column runs it crosses and how many columns they hold. */
    readonly #crossed = new Map<number, { readonly runs: readonly number[]; readonly columns: number }>();

    /** The column ranges, in order, each with its width and its format's index; set by `build`. */
    columns: readonly XlsxColumnInput[] = [];
    /** The runs of formatted rows, in order; set by `build`. */
    rows: readonly RowRun[] = [];

    /** The plan for `sheet`, its levels read; nothing is written to a styles part yet. */
    constructor(sheet: SheetDocument) {
        this.#sheet = sheet;
        for (const run of sheet.columns.toSorted((a, b) => a.first - b.first)) {
            const level = partsOf(run);
            const parts = FormatParts.differing(level.over(defaultFormat), defaultFormat).mask;
            for (let c = run.first; c <= run.last; c++) {
                this.#columnLevels.set(c, level);
                this.#columnParts.set(c, parts);
            }
            this.#columnRuns.push({ first: run.first, last: run.last, level, parts });
        }
        for (const run of sheet.rows.toSorted((a, b) => a.first - b.first)) {
            const level = partsOf(run);
            this.#rowRuns.push({ first: run.first, last: run.last, level, open: ~level.mask & allParts });
        }
    }

    /** The rows the row formats write, each a `<row>` element whether or not it holds a cell. */
    get formattedRows(): number {
        let rows = 0;
        for (const run of this.#rowRuns) rows += run.last - run.first + 1;
        return rows;
    }

    /**
     * How many cells of no value the crossings write: every place a formatted row crosses a formatted column that records a
     * part the row leaves open, less those of `recorded`, which are written as the Sheet's own cells. Counted from the runs,
     * never cell by cell.
     * @param recorded - The cells the Sheet writes of its own, each once.
     */
    crossings(recorded: Iterable<CellAddress>): number {
        let count = 0;
        for (const run of this.#rowRuns) count += (run.last - run.first + 1) * this.#crossedBy(run.open).columns;
        if (count === 0) return 0;
        for (const at of recorded) {
            const r = this.#rowRunAt(at.row);
            const parts = this.#columnParts.get(at.column);
            if (r !== undefined && parts !== undefined && (parts & elementAt(this.#rowRuns, r).open) !== 0) count--;
        }
        return count;
    }

    /** Finds every format the columns, the rows and the crossings write, adding each to `styles`. */
    build(styles: StyleTable): void {
        const rows: RowRun[] = [];
        for (const run of this.#rowRuns) {
            const crossings: Crossing[] = [];
            for (const index of this.#crossedBy(run.open).runs) {
                const column = elementAt(this.#columnRuns, index);
                const style = styles.index(run.level.over(column.level.over(defaultFormat)));
                const last = crossings.at(-1);
                if (last !== undefined && last.last === column.first - 1 && last.style === style) crossings[crossings.length - 1] = { ...last, last: column.last };
                else crossings.push({ first: column.first, last: column.last, style });
            }
            rows.push({ first: run.first, last: run.last, style: styles.index(run.level.over(defaultFormat)), crossings });
        }
        this.rows = rows;
        this.columns = this.#columnRanges(styles);
    }

    /** The index of the format a cell element at `at` is written with: what the Sheet shows there, its own level over its row's
     * and its column's. */
    style(styles: StyleTable, own: FormatParts, at: CellAddress): number {
        const column = this.#columnLevels.get(at.column);
        let under = column === undefined ? defaultFormat : column.over(defaultFormat);
        const r = this.#rowRunAt(at.row);
        if (r !== undefined) under = elementAt(this.#rowRuns, r).level.over(under);
        return styles.index(own.over(under));
    }

    /** The column runs a row that leaves `open` to its columns crosses: those that record one of those parts other than the
     * default's. */
    #crossedBy(open: number): { readonly runs: readonly number[]; readonly columns: number } {
        const known = this.#crossed.get(open);
        if (known !== undefined) return known;
        const runs: number[] = [];
        let columns = 0;
        this.#columnRuns.forEach((run, i) => {
            if ((run.parts & open) === 0) return;
            runs.push(i);
            columns += run.last - run.first + 1;
        });
        const crossed = { runs, columns };
        this.#crossed.set(open, crossed);
        return crossed;
    }

    /** The index of the formatted row run that holds `row`, or `undefined`. */
    #rowRunAt(row: number): number | undefined {
        let low = 0;
        let high = this.#rowRuns.length - 1;
        while (low <= high) {
            const mid = Math.floor((low + high) / 2);
            const run = elementAt(this.#rowRuns, mid);
            if (row < run.first) high = mid - 1;
            else if (row > run.last) low = mid + 1;
            else return mid;
        }
        return undefined;
    }

    /** The columns' widths and formats as ranges: a range wherever both stay the same. */
    #columnRanges(styles: StyleTable): XlsxColumnInput[] {
        const widths = new Map<number, SheetDocumentColumnWidth>();
        for (const run of this.#sheet.columnWidths) {
            for (let c = run.first; c <= run.last; c++) widths.set(c, run);
        }
        const ranges: { first: number; last: number; width: number; styleIndex: number; bestFit: boolean; customWidth: boolean }[] = [];
        for (const column of [...new Set([...widths.keys(), ...this.#columnLevels.keys()])].toSorted((a, b) => a - b)) {
            // A column that records a format and no width of its own is written at the default width, not marked as set: a
            // <col> with no width is believed to be read by Excel as a width of 0, which hides the column.
            const w = widths.get(column);
            const width = widthToFile(w === undefined ? Sheet.defaultColumnWidth : w.width);
            const bestFit = w !== undefined && w.kind === 'widenedByEntry';
            const level = this.#columnLevels.get(column);
            const styleIndex = level === undefined ? 0 : styles.index(level.over(defaultFormat));
            const last = ranges.at(-1);
            if (last !== undefined && last.last === column - 1 && last.width === width && last.styleIndex === styleIndex
                && last.bestFit === bestFit && last.customWidth === (w !== undefined)) {
                last.last = column;
            } else {
                ranges.push({ first: column, last: column, width, styleIndex, bestFit, customWidth: w !== undefined });
            }
        }
        return ranges;
    }
}

/** The parts a Sheet Document's run records. */
export function partsOf(run: SheetDocumentAxisFormat): FormatParts {
    return new FormatParts(run.numberFormat, run.alignment, run.font, run.fill, run.borders);
}
