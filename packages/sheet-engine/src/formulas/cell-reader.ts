import type { CellAddress } from '../cell-address';
import type { Value } from '../value';
import type { Node } from './nodes';
import type { Operand } from './operand';
import type { Area } from './reference';

/** A calendar day: the Sheet Day `TODAY` answers (ADR-0121). */
export interface SheetDay {
    /** The year, 1900 to 9999. */
    readonly year: number;
    /** 1 to 12. */
    readonly month: number;
    /** 1 to 31. */
    readonly day: number;
}

/** A moment in the Workbook's zone: the date and the time of day `NOW` answers (ADR-0124). */
export interface SheetMoment extends SheetDay {
    /** 0 to 23. */
    readonly hour: number;
    /** 0 to 59. */
    readonly minute: number;
    /** 0 to 59. */
    readonly second: number;
    /** 0 to 999. */
    readonly millisecond: number;
}

/** What the evaluator reads cells through: the Values of one recalculation, blank as `undefined`. */
export interface CellReader {
    read(address: CellAddress): Value | undefined;

    /** The addresses inside `area` that are not blank, in row-major order. */
    nonBlankIn(area: Area): Iterable<CellAddress>;

    /**
     * A Linked Table's column (ADR-0049): the column's Values, or `#NAME?` for a table never declared, `#REF!`
     * for a column it does not have, and `#GETTING_DATA` before its first snapshot.
     */
    tableColumn(table: string, column: string): Operand;

    /** Whether a Linked Table of this name is declared, the name read without regard to case (ADR-0049). */
    isLinkedTable(name: string): boolean;

    /** The Sheet Day `TODAY` answers (ADR-0121), or `undefined` while it is not known. */
    readonly today: SheetDay | undefined;

    /** The moment `NOW` answers, read once for the recalculation (ADR-0124); `undefined` while it is not known. */
    readonly now: SheetMoment | undefined;

    /**
     * The cells of the Sheet a Reference's qualifier names, as this recalculation reads them: this Sheet's for
     * no qualifier, another Sheet's of the Workbook for its name, without regard to case; `undefined` for a
     * name no Sheet of the Workbook has, which is `#REF!` (ADR-0331).
     */
    resolve(sheetName: string | undefined): CellReader | undefined;

    /**
     * The cells of every Sheet a 3D Reference spans (ADR-0331 §7), in tab order; `undefined` when the Workbook
     * has no Sheet of one of the names, which is `#NAME?`, as Excel's.
     */
    resolveSpan(first: string, last: string): readonly CellReader[] | undefined;

    /**
     * The draw of the `RAND` or `RANDBETWEEN` call at `place` in the Formula of `cell` (ADR-0180), from the
     * Sheet Seed and the count of changes; `undefined` while the Sheet has no seed.
     */
    draw(cell: CellAddress, place: number): bigint | undefined;

    /** `A1#` (ADR-0125): the array the Anchor at `anchor` spills, as this recalculation leaves it; `#REF!` when it does not spill. */
    spill(anchor: CellAddress): Operand;

    /**
     * The Formula whose result a cell shows: its own, or for a cell of a Spill Range its Anchor's (ADR-0125);
     * `undefined` for a constant or a blank.
     */
    formulaAt(address: CellAddress): Node | undefined;
}
