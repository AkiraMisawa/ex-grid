// The reference Selection Summary (ADR-0130), the counterpart of C#'s GridSummary: which cells a request
// names, and the figures over them. `GridSource.from` answers with it and a remote source is held to it, as
// to Find (ADR-0055).

import { itemAt } from '../must';
import type { CellPosition } from '../selection/cell';
import type { SelectionRange } from '../selection/range';
import { decimalCanonical, decimalOf, decimalRound, decimalText, roundedQuotient, type ExactDecimal } from './exact-decimal';
import { isCountFigure, isSummaryFigure, summaryFigureOrder, SummaryAccumulator, type AggregateResult, type SummaryFigure } from './summary-accumulator';

/**
 * One question of a Selection Summary (ADR-0130): which cells, read in which order, and which figures. The
 * grid asks and whoever holds the data answers, as for Find.
 */
export interface GridSummaryRequest {
    /** The selected ranges, positions in the order `rowSequenceVersion` names. A cell covered by two
     * ranges counts once. */
    readonly ranges: readonly SelectionRange[];
    /** The visible columns' names in the grid's current order: position `i` here is column `i` of every
     * range. Hidden columns are not summarised. */
    readonly columns: readonly string[];
    /** The order the ranges were read in (ADR-0011). An answer the grid receives under another version is
     * shown nowhere. */
    readonly rowSequenceVersion: number;
    /** The figures shown, the only ones asked for: an answerer computes these alone. */
    readonly figures: readonly SummaryFigure[];
    /** The Focus, Excel's active cell, whose format the figures are shown in, as Excel shows its status
     * bar's. An answerer that formats cells itself writes each figure's text by it. Absent when the grid has
     * none. */
    readonly focus?: CellPosition | undefined;
}

/**
 * A Selection Summary's answer (ADR-0130): each figure asked for, with the text an answerer that formats
 * cells itself gives it, or a decline with its reason, shown in place of the figures; never a partial
 * figure.
 */
export type GridSummaryResult =
    | {
        readonly kind: 'answered';
        readonly figures: Readonly<Partial<Record<SummaryFigure, AggregateResult>>>;
        readonly texts: Readonly<Partial<Record<SummaryFigure, string>>>;
    }
    | { readonly kind: 'declined'; readonly reason: string };

/** The answerer will not summarise this selection, a million rows on a server, say, and says why. */
export function declinedSummary(reason: string): GridSummaryResult {
    if (typeof reason !== 'string' || reason.trim() === '') throw new TypeError('A declined Selection Summary says why (ADR-0130).');
    return Object.freeze({ kind: 'declined', reason });
}

/** The figures answered and, where the answerer formats cells itself, the text each is shown with. A figure
 * with no text is formatted by the grid; a text with no figure is refused, and so is an answer C#'s
 * `AggregateResult` could not hold: a number that is not finite, or an exact value no decimal holds. */
export function answeredSummary(
    figures: Readonly<Partial<Record<SummaryFigure, AggregateResult>>>,
    texts: Readonly<Partial<Record<SummaryFigure, string>>> = {},
): GridSummaryResult {
    for (const [figure, answer] of Object.entries(figures)) {
        if (!isSummaryFigure(figure)) throw new TypeError(`'${figure}' is not a figure of a Selection Summary (ADR-0130).`);
        checkAnswer(figure, answer);
    }
    for (const figure of Object.keys(texts)) {
        if (!Object.hasOwn(figures, figure)) throw new TypeError(`'${figure}' has a text and no answer (ADR-0130).`);
    }
    return Object.freeze({ kind: 'answered', figures: Object.freeze({ ...figures }), texts: Object.freeze({ ...texts }) });
}

/**
 * The figures `request` asks for over `rows`, the result in its current order. `valueOf` answers a column's
 * value by name, or `undefined` for a column with no value (an Action Column) or one the source does not
 * know: its cells are blank.
 */
export function summaryOf<Row>(
    rows: readonly Row[], request: GridSummaryRequest, valueOf: (column: string) => ((row: Row) => unknown) | undefined,
): GridSummaryResult {
    const values = request.columns.map(valueOf);
    const accumulator = new SummaryAccumulator();
    for (const { top, bottom, columns } of summaryBands(request.ranges, rows.length, request.columns.length)) {
        const read = columns.flatMap(column => {
            const value = values[column];
            return value === undefined ? [] : [value];
        });
        for (let row = top; row < bottom; row++) {
            const item = itemAt(rows, row);
            for (const value of read) accumulator.add(value(item));
        }
    }
    return summaryAnswer(accumulator, request.figures);
}

// An answer arrives from whoever holds the data, so it is checked here, at the boundary (CONVENTIONS.md).
function checkAnswer(figure: SummaryFigure, answer: AggregateResult | undefined): void {
    if (answer === undefined || answer.kind !== 'number') return;
    if (!Number.isFinite(answer.number)) throw new RangeError(`The ${figure} is ${answer.number}, which is not a number a figure shows; say #NUM! with an error (ADR-0130).`);
    if (answer.exact !== undefined) checkExact(figure, answer.exact);
}

function checkExact(figure: SummaryFigure, exact: ExactDecimal): void {
    const fits = typeof exact.mantissa === 'bigint' && Number.isInteger(exact.scale) && exact.scale >= 0 && exact.scale <= 28
        && exact.mantissa < 1n << 96n && exact.mantissa > -(1n << 96n);
    if (!fits) throw new RangeError(`The ${figure}'s exact value is no decimal: a mantissa within 96 bits and a scale of 0 to 28 (ADR-0130).`);
}

/** The figures asked for, read from an accumulator over the selected cells, in Excel's order. */
export function summaryAnswer(accumulator: SummaryAccumulator, figures: readonly SummaryFigure[]): GridSummaryResult {
    return answeredSummary(summaryFiguresOf(accumulator, figures));
}

// Each figure asked for, read from the accumulator, in Excel's order.
function summaryFiguresOf(accumulator: SummaryAccumulator, figures: readonly SummaryFigure[]): Partial<Record<SummaryFigure, AggregateResult>> {
    const answers: Partial<Record<SummaryFigure, AggregateResult>> = {};
    for (const figure of summaryFigureOrder) {
        if (figures.includes(figure)) answers[figure] = accumulator.read(figure);
    }
    return answers;
}

/**
 * The selected cells of an answerer that reads them itself, a Sheet's among them, folded one at a time into the
 * figures' one definition (ADR-0130), which `summaryOf` reads too: the counterpart of C#'s `GridSummaryCells`. A
 * number is in every figure; text and a Boolean are counted; an error is counted, is never a number, and leaves only
 * Count and Numerical Count, as Excel's status bar does; a blank is not added at all.
 */
export class GridSummaryCells {
    readonly #accumulator = new SummaryAccumulator();

    /** Adds a value by what it is, as `summaryOf` reads a row's value: `null` and `undefined` are blank, a `number`
     * and a `bigint` exact numbers, anything else counted and never summed. */
    add(value: unknown): void {
        this.#accumulator.add(value);
    }

    /** Adds a number in Excel's double arithmetic, never read as a decimal. */
    addNumber(value: number): void {
        this.#accumulator.addDouble(value);
    }

    /** Adds a cell that holds something other than a number: counted, never summed. */
    addOther(): void {
        this.#accumulator.addOther();
    }

    /** Adds an error value: counted and never a number. Average, Min, Max and Sum are then shown nowhere; Count and
     * Numerical Count still are. */
    addError(): void {
        this.#accumulator.addError();
    }

    /** The figures asked for, over the cells added, in Excel's order. `textOf`, where given, writes the text of each
     * figure that reads numbers (Average, Min, Max, Sum) in the format of the request's Focus, or answers
     * `undefined` to leave it to the grid. It is handed the figure's double. */
    answer(figures: readonly SummaryFigure[], textOf?: (figure: number) => string | undefined): GridSummaryResult {
        const answers = summaryFiguresOf(this.#accumulator, figures);
        const texts: Partial<Record<SummaryFigure, string>> = {};
        if (textOf !== undefined) {
            for (const figure of summaryFigureOrder) {
                const result = answers[figure];
                if (result?.kind !== 'number' || isCountFigure(figure)) continue;
                const text = textOf(result.number);
                if (text !== undefined) texts[figure] = text;
            }
        }
        return answeredSummary(answers, texts);
    }
}

/** A band of selected rows, `top` to before `bottom`, and the columns some range covers across all of it. */
export interface SummaryBand {
    readonly top: number;
    readonly bottom: number;
    readonly columns: readonly number[];
}

/**
 * The selected cells as bands of rows, each with the columns some range covers across the whole band, so a
 * cell under two ranges is visited once and a whole column of a million rows is one band. Rows past
 * `rowCount` and columns past `columnCount` are not cells.
 */
export function summaryBands(ranges: readonly SelectionRange[], rowCount: number, columnCount: number): readonly SummaryBand[] {
    const clamp = (value: number): number => Math.min(Math.max(value, 0), rowCount);
    const edges = new Set<number>();
    for (const range of ranges) {
        const top = clamp(range.topRow);
        const bottom = clamp(range.topRow + range.rowCount);
        if (top < bottom) {
            edges.add(top);
            edges.add(bottom);
        }
    }
    const sorted = [...edges].toSorted((a, b) => a - b);
    const bands: SummaryBand[] = [];
    for (let i = 1; i < sorted.length; i++) {
        const top = itemAt(sorted, i - 1);
        const columns = new Set<number>();
        for (const range of ranges) {
            if (range.topRow <= top && range.topRow + range.rowCount > top) {
                const right = Math.min(range.leftColumn + range.columnCount, columnCount);
                for (let column = Math.max(range.leftColumn, 0); column < right; column++) columns.add(column);
            }
        }
        if (columns.size > 0) bands.push(Object.freeze({ top, bottom: itemAt(sorted, i), columns: Object.freeze([...columns].toSorted((a, b) => a - b)) }));
    }
    return Object.freeze(bands);
}

/**
 * A figure's own text, where no format applies (ADR-0130): a count as a whole number; a number summed
 * exactly as it is, to ten decimal places; one in double arithmetic to ten significant digits, as Excel's
 * status bar shows one. `undefined` for a figure that is not a number, which is shown nowhere.
 */
export function summaryFigureText(figure: SummaryFigure, result: AggregateResult): string | undefined {
    if (result.kind !== 'number') return undefined;
    if (isCountFigure(figure)) {
        // C#'s FigureText: the count's decimal, or the decimal its double reads as, written with "0", which
        // rounds a tie away from zero.
        const count = result.exact === undefined ? decimalOf(result.number) : result.exact;
        if (count === undefined) throw new RangeError(`A ${figure} of ${result.number} is no decimal, and has no text.`);
        return decimalText(decimalRound(count, 0, 'awayFromZero'));
    }
    if (result.exact !== undefined) return decimalText(decimalCanonical(decimalRound(result.exact, 10)));
    return generalText(result.number, 10);
}

/** A double as .NET's "G" format writes it with `precision` significant digits: the exact value rounded half
 * to even, fixed where the exponent is from -4 to below the precision, scientific with a signed exponent of
 * at least two digits otherwise, and no trailing zeros either way. */
export function generalText(value: number, precision: number): string {
    if (!Number.isFinite(value)) throw new RangeError(`${value} has no general text.`);
    if (value === 0) return '0';
    const { digits: exact, scale } = exactDigitsOf(Math.abs(value));
    // Rounded to `precision` significant digits, half to even on the exact value.
    const dropped = Math.max(exact.length - precision, 0);
    const kept = dropped > 0 ? roundedQuotient(BigInt(exact), 10n ** BigInt(dropped)) : BigInt(exact);
    let digits = kept.toString();
    const exponent = digits.length - 1 + dropped - scale;
    digits = digits.slice(0, precision).replace(/0+$/u, '');
    if (digits === '') digits = '0';
    const sign = value < 0 ? '-' : '';
    if (exponent > -5 && exponent < precision) {
        if (exponent < 0) return `${sign}0.${'0'.repeat(-exponent - 1)}${digits}`;
        const whole = digits.padEnd(exponent + 1, '0').slice(0, exponent + 1);
        const fraction = digits.slice(exponent + 1);
        return sign + whole + (fraction === '' ? '' : `.${fraction}`);
    }
    const shown = digits.length > 1 ? `${digits.slice(0, 1)}.${digits.slice(1)}` : digits;
    return `${sign}${shown}E${exponent < 0 ? '-' : '+'}${Math.abs(exponent).toString().padStart(2, '0')}`;
}

/** A positive finite double's exact value as decimal digits and a scale: a binary fraction always ends in
 * decimal. */
function exactDigitsOf(value: number): { readonly digits: string; readonly scale: number } {
    const view = new DataView(new ArrayBuffer(8));
    view.setFloat64(0, value);
    const bits = view.getBigUint64(0);
    const biased = Number((bits >> 52n) & 0x7ffn);
    const fraction = bits & ((1n << 52n) - 1n);
    const mantissa = biased === 0 ? fraction : fraction | (1n << 52n);
    const exponent = (biased === 0 ? 1 : biased) - 1075;
    if (exponent >= 0) return { digits: (mantissa << BigInt(exponent)).toString(), scale: 0 };
    return { digits: (mantissa * 5n ** BigInt(-exponent)).toString(), scale: -exponent };
}
