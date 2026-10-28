// The figures of a Selection Summary over the cells folded in (ADR-0130), the counterpart of the part of
// C#'s AggregateAccumulator a Summary reads. Each figure means the Aggregation of the same name: a number
// is in every figure; text, a boolean, a date and anything else are counted and never a number; a blank
// (`null` or `undefined`) is in none.
//
// A number is summed exactly, as C#'s decimal sums (ADR-0350 §16): a `number` read as C# reads a double
// into a decimal, a `bigint` as itself. Only where no decimal can hold a value, or a sum outgrows one, do
// the figures leave exactness for Excel's double arithmetic, compensated as C#'s are; a value that is not
// finite makes every figure that reads numbers #NUM!, and an error among the cells leaves only the counts.

import { unreachable } from '../must';
import { decimalAdd, decimalCanonical, decimalCompare, decimalDivide, decimalOf, decimalOfCount, decimalToNumber, type ExactDecimal } from './exact-decimal';

/** Why a figure is not a number, as Excel shows it: #DIV/0!, #NUM!, or an error among the cells. */
export type AggregateErrorKind = 'divideByZero' | 'notANumber' | 'errorAmongValues';

/**
 * One figure's answer: nothing to show, an error, or a number. A number summed exactly carries its
 * decimal in `exact`, and `number` is the double C# converts that decimal to; one in Excel's double
 * arithmetic carries `exact` undefined.
 */
export type AggregateResult =
    | { readonly kind: 'empty' }
    | { readonly kind: 'error'; readonly error: AggregateErrorKind }
    | { readonly kind: 'number'; readonly number: number; readonly exact: ExactDecimal | undefined };

/** The figures a Selection Summary can show, as Excel's status bar names them. */
export type SummaryFigure = 'average' | 'count' | 'numericalCount' | 'min' | 'max' | 'sum';

/** The figures in the order Excel's status bar shows them. */
export const summaryFigureOrder: readonly SummaryFigure[] = Object.freeze(['average', 'count', 'numericalCount', 'min', 'max', 'sum']);

/** Excel's default: Average, Count and Sum. */
export const defaultSummaryFigures: readonly SummaryFigure[] = Object.freeze(['average', 'count', 'sum']);

/** Whether a name is one of the figures. */
export function isSummaryFigure(name: string): name is SummaryFigure {
    return summaryFigureOrder.some(figure => figure === name);
}

/** Whether a figure counts cells, Count and Numerical Count, rather than reading their numbers. */
export function isCountFigure(figure: SummaryFigure): boolean {
    return figure === 'count' || figure === 'numericalCount';
}

const empty: AggregateResult = Object.freeze({ kind: 'empty' });

function errorResult(error: AggregateErrorKind): AggregateResult {
    return Object.freeze({ kind: 'error', error });
}

// Read canonical, as C# reads it, and converted to a double from that form.
function exactResult(value: ExactDecimal): AggregateResult {
    const exact = decimalCanonical(value);
    return Object.freeze({ kind: 'number', number: decimalToNumber(exact), exact });
}

// A double that is not finite is #NUM!, as Excel shows an overflowing figure.
function doubleResult(value: number): AggregateResult {
    return Number.isFinite(value) ? Object.freeze({ kind: 'number', number: value, exact: undefined }) : errorResult('notANumber');
}

/** The cells of one Selection Summary, folded in one at a time. */
export class SummaryAccumulator {
    #values = 0;
    #numbers = 0;
    #nonFinite = false;
    #error = false;
    // The sum, exact until a value or a sum no decimal holds; then a double with its Neumaier compensation.
    #exactSum: ExactDecimal | undefined;
    #sumInexact = false;
    #sum = 0;
    #compensation = 0;
    // The extremes, exact until a value no decimal holds; then doubles. Apart from the sum's, as C#'s are:
    // a sum that outgrows a decimal leaves the extremes exact.
    #exactMin: ExactDecimal | undefined;
    #exactMax: ExactDecimal | undefined;
    #extremesInexact = false;
    #min = 0;
    #max = 0;

    /** How many cells are not blank. */
    get values(): number {
        return this.#values;
    }

    /** How many cells hold numbers. */
    get numbers(): number {
        return this.#numbers;
    }

    /** Adds a value by what it is: `null` and `undefined` are blank, a `number` and a `bigint` numbers,
     * anything else counted and never summed. */
    add(value: unknown): void {
        if (value === null || value === undefined) return;
        if (typeof value === 'number' || typeof value === 'bigint') {
            this.#addNumber(value);
            return;
        }
        this.#values += 1;
    }

    /** Adds a number in Excel's double arithmetic, as C#'s `AddDouble` does: never read as a decimal, so a Sheet's
     * figures are the doubles Excel's are (ADR-0047). One that is not finite makes every figure that reads numbers
     * #NUM!. */
    addDouble(value: number): void {
        this.#values += 1;
        const first = this.#numbers === 0;
        this.#numbers += 1;
        if (!Number.isFinite(value)) {
            this.#nonFinite = true;
            return;
        }
        if (this.#nonFinite) return;
        this.#addDouble(value, first);
    }

    /** Adds a cell that holds something other than a number: counted, never summed. */
    addOther(): void {
        this.#values += 1;
    }

    /** Adds a cell holding an error value: counted and never a number. Average, Min, Max and Sum are then
     * shown nowhere; Count and Numerical Count still are, as in Excel's status bar. */
    addError(): void {
        this.#values += 1;
        this.#error = true;
    }

    /** A figure's answer over the cells added, as C#'s `GridSummary` reads it: a figure that reads numbers
     * is empty where no cell holds one, and only the counts are shown. */
    read(figure: SummaryFigure): AggregateResult {
        if (!isCountFigure(figure) && this.#numbers === 0) return empty;
        if (this.#error && !isCountFigure(figure)) return errorResult('errorAmongValues');
        if (this.#values === 0) return empty;
        if (figure === 'count') return exactResult(decimalOfCount(this.#values));
        if (figure === 'numericalCount') return exactResult(decimalOfCount(this.#numbers));
        if (this.#nonFinite) return errorResult('notANumber');
        switch (figure) {
            case 'sum':
                return this.#sumInexact ? doubleResult(this.#sum + this.#compensation) : exactResult(held(this.#exactSum));
            case 'average':
                return this.#sumInexact
                    ? doubleResult((this.#sum + this.#compensation) / this.#numbers)
                    : exactResult(decimalDivide(held(this.#exactSum), this.#numbers));
            case 'min':
                return this.#extremesInexact ? doubleResult(this.#min) : exactResult(held(this.#exactMin));
            case 'max':
                return this.#extremesInexact ? doubleResult(this.#max) : exactResult(held(this.#exactMax));
            default:
                return unreachable(figure);
        }
    }

    #addNumber(value: number | bigint): void {
        this.#values += 1;
        const first = this.#numbers === 0;
        this.#numbers += 1;
        if (typeof value === 'number' && !Number.isFinite(value)) {
            this.#nonFinite = true;
            return;
        }
        if (this.#nonFinite) return;
        const exact = decimalOf(value);
        if (exact === undefined) {
            // A bigint past a double's range converts to an infinity, which C#'s AddDouble takes as #NUM!.
            const double = Number(value);
            if (!Number.isFinite(double)) {
                this.#nonFinite = true;
                return;
            }
            this.#addDouble(double, first);
        } else {
            this.#addExact(exact, first);
        }
    }

    #addExact(value: ExactDecimal, first: boolean): void {
        if (this.#sumInexact) {
            this.#neumaier(decimalToNumber(value));
        } else if (first) {
            this.#exactSum = value;
        } else {
            const sum = decimalAdd(held(this.#exactSum), value);
            if (sum === undefined) {
                // The sum outgrew a decimal, as C#'s overflows: Excel's arithmetic from here on.
                this.#sumToInexact();
                this.#neumaier(decimalToNumber(value));
            } else {
                this.#exactSum = sum;
            }
        }
        if (this.#extremesInexact) {
            this.#extremes(decimalToNumber(value));
        } else if (first) {
            this.#exactMin = value;
            this.#exactMax = value;
        } else {
            if (decimalCompare(value, held(this.#exactMin)) < 0) this.#exactMin = value;
            if (decimalCompare(value, held(this.#exactMax)) > 0) this.#exactMax = value;
        }
    }

    #addDouble(value: number, first: boolean): void {
        if (first) {
            this.#sumInexact = true;
            this.#sum = value;
            this.#extremesInexact = true;
            this.#min = value;
            this.#max = value;
            return;
        }
        if (!this.#sumInexact) this.#sumToInexact();
        this.#neumaier(value);
        if (!this.#extremesInexact) {
            // The conversion is monotonic, so the double extremes of exact numbers are their exact extremes
            // converted.
            this.#extremesInexact = true;
            this.#min = decimalToNumber(held(this.#exactMin));
            this.#max = decimalToNumber(held(this.#exactMax));
            this.#exactMin = undefined;
            this.#exactMax = undefined;
        }
        this.#extremes(value);
    }

    #sumToInexact(): void {
        this.#sumInexact = true;
        this.#sum = decimalToNumber(held(this.#exactSum));
        this.#compensation = 0;
        this.#exactSum = undefined;
    }

    // Neumaier's compensated sum, as C#'s AggregateArithmetic adds in double.
    #neumaier(value: number): void {
        const total = this.#sum + value;
        this.#compensation += Math.abs(this.#sum) >= Math.abs(value) ? (this.#sum - total) + value : (value - total) + this.#sum;
        this.#sum = total;
    }

    #extremes(value: number): void {
        if (value < this.#min) this.#min = value;
        if (value > this.#max) this.#max = value;
    }
}

function held(value: ExactDecimal | undefined): ExactDecimal {
    if (value === undefined) throw new Error('Internal: an exact figure is read before a number was added.');
    return value;
}
