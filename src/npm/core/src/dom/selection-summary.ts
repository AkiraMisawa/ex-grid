// The Selection Summary a grid shows (ADR-0130): Excel's status-bar figures over the selection. The grid asks and
// whoever holds the data answers, as for Find (ADR-0055); the grid shows only the answer to the current question.
// The question is the selection's ranges and Focus, the source they were read from and the order they were read
// in, the visible columns, the figures shown and how many times the rows have moved under a standing selection:
// any of these moving clears the figures at once, in the pass that shows the move, and asks again after it. The
// previous answer is never left beside a new selection, and a late answer is dropped by the question it answers,
// never by a time.

import { textFor } from '../cells/display-text';
import { summaryLabelIdOf, summaryLabelIds } from '../chrome/summary-words';
import type { QueryColumn } from '../query/columns';
import { sameCell, type CellPosition } from '../selection/cell';
import { sameRange, type SelectionRange } from '../selection/range';
import { answeredSummary, declinedSummary, summaryFigureText, type GridSummaryRequest, type GridSummaryResult } from '../summary/grid-summary';
import { decimalCompare, decimalOf, decimalRound } from '../summary/exact-decimal';
import { isCountFigure, summaryFigureOrder, type AggregateResult, type SummaryFigure } from '../summary/summary-accumulator';

/** Where a Selection Summary stands (ADR-0130): no question, a question out, its answer, or its decline. */
export type SelectionSummaryStatus = 'none' | 'pending' | 'answered' | 'declined';

/**
 * The Selection Summary as it now stands (ADR-0130): the question asked, and its answer once it has one. Raised
 * to a Consumer that shows the figures elsewhere, such as an application-wide status bar.
 */
export interface SelectionSummary {
    /** Where the summary stands. */
    readonly status: SelectionSummaryStatus;
    /** The question out or answered; undefined while none stands. */
    readonly request: GridSummaryRequest | undefined;
    /** The answer, answered or declined; undefined while none stands or the question is out. */
    readonly result: GridSummaryResult | undefined;
}

/** Whoever answers a Selection Summary: the Consumer's `onSummarize`, or a source that summarises. */
export type SummaryAnswerer = (request: GridSummaryRequest, signal: AbortSignal) => Promise<GridSummaryResult>;

/** What a question is asked over: equal parts are the same question, and nothing is asked again. */
export interface SummaryQuestion {
    readonly ranges: readonly SelectionRange[];
    readonly focus: CellPosition | undefined;
    /** The source the ranges were read from, told apart by identity: two fresh sources share a version
     * (ADR-0142). */
    readonly binding: object;
    readonly rowSequenceVersion: number;
    readonly columns: readonly string[];
    readonly figures: readonly SummaryFigure[];
    readonly rowsStamp: number;
}

const none: SelectionSummary = Object.freeze({ status: 'none', request: undefined, result: undefined });

/** Two distinct cells or more: a range of two, or two ranges that are not the same one cell. Ctrl+clicking one
 * cell twice selects one cell, whatever the areas add up to. */
export function selectsTwoCells(ranges: readonly SelectionRange[]): boolean {
    const first = ranges[0];
    if (first === undefined) return false;
    return ranges.some(range => range.rowCount * range.columnCount > 1 || range.topRow !== first.topRow || range.leftColumn !== first.leftColumn);
}

/** The rows a Window holds, where it starts, and how many rows the result has, if known. */
export interface SummaryRows<Row> {
    readonly window: readonly Row[];
    readonly windowStart: number;
    readonly totalCount: number | undefined;
}

/**
 * Whether a row the figures were taken over moved between two Windows (ADR-0130): a different row count, since a
 * row added or removed anywhere may have shifted the selected rows, or a selected position both Windows hold now
 * holding a different row instance, identity being the change signal (ADR-0003). A Window that only scrolled
 * shows the same rows where both hold them, and moves nothing. Walked only over the selected positions both hold,
 * so the cost follows the Selection's rows in the Window, not the Window (ADR-0130, 2026-10-07).
 */
export function rowsMovedUnder<Row>(ranges: readonly SelectionRange[], before: SummaryRows<Row>, after: SummaryRows<Row>): boolean {
    if (before.totalCount !== after.totalCount) return true;
    if (before.window === after.window && before.windowStart === after.windowStart) return false;
    const from = Math.max(before.windowStart, after.windowStart);
    const to = Math.min(before.windowStart + before.window.length, after.windowStart + after.window.length);
    for (const range of ranges) {
        const end = Math.min(to, range.topRow + range.rowCount);
        for (let position = Math.max(from, range.topRow); position < end; position++) {
            if (after.window[position - after.windowStart] !== before.window[position - before.windowStart]) return true;
        }
    }
    return false;
}

function sameQuestion(a: SummaryQuestion, b: SummaryQuestion): boolean {
    return a.binding === b.binding && a.rowSequenceVersion === b.rowSequenceVersion && a.rowsStamp === b.rowsStamp
        && sameItems(a.figures, b.figures) && sameItems(a.columns, b.columns)
        && (a.focus === undefined ? b.focus === undefined : b.focus !== undefined && sameCell(a.focus, b.focus))
        && (a.ranges === b.ranges || (a.ranges.length === b.ranges.length && a.ranges.every((range, i) => {
            const other = b.ranges[i];
            return other !== undefined && sameRange(range, other);
        })));
}

function sameItems<T>(a: readonly T[], b: readonly T[]): boolean {
    return a === b || (a.length === b.length && a.every((item, i) => item === b[i]));
}

/** The question in force and where its answer stands; asks it, and keeps only the answer to it. */
export class SummaryQuestions {
    readonly #answered: () => void;
    #question: SummaryQuestion | undefined;
    #summary: SelectionSummary = none;
    #asking: AbortController | undefined;
    #askOwed = false;
    #raiseOwed = false;

    /** `answered` hears an answer to the question in force, so the grid paints again. */
    constructor(answered: () => void) {
        this.#answered = answered;
    }

    /** The summary as it now stands. */
    get summary(): SelectionSummary {
        return this.#summary;
    }

    /** The question standing, figures shown or being asked for; undefined while none does. */
    get standing(): SummaryQuestion | undefined {
        return this.#question;
    }

    /**
     * The question the grid's state asks, or none: one different from the question in force clears the figures
     * now and owes the asking to `settle`, which the grid runs once it has painted.
     */
    review(question: SummaryQuestion | undefined): void {
        if (question === undefined) {
            if (this.#question === undefined && this.#summary.status === 'none') return;
            this.#cancel();
            this.#question = undefined;
            this.#summary = none;
            this.#raiseOwed = true;
            return;
        }
        if (this.#question !== undefined && sameQuestion(this.#question, question)) return;
        this.#cancel();
        this.#question = question;
        const request: GridSummaryRequest = Object.freeze({
            ranges: question.ranges, columns: question.columns, rowSequenceVersion: question.rowSequenceVersion, figures: question.figures,
            ...(question.focus === undefined ? {} : { focus: question.focus }),
        });
        this.#summary = Object.freeze({ status: 'pending', request, result: undefined });
        this.#askOwed = true;
        this.#raiseOwed = true;
    }

    /**
     * Run after the pass that painted the summary: tells `raise` where the summary stands, if that changed, and
     * asks the question owed. The answer is not awaited: a slow answerer holds back nothing, and its answer is
     * matched to its question whenever it lands. A failure of the answerer's, or an answer that is not one to
     * the question, rejects the asking, which the grid leaves unheard so that it reaches the page as an unhandled
     * rejection rather than a figure shown in part.
     */
    settle(answererOf: () => SummaryAnswerer | undefined, raise: ((summary: SelectionSummary) => void) | undefined): Promise<void> | undefined {
        if (this.#raiseOwed) {
            this.#raiseOwed = false;
            raise?.(this.#summary);
        }
        if (!this.#askOwed) return undefined;
        this.#askOwed = false;
        const question = this.#question;
        const request = this.#summary.request;
        const answerer = answererOf();
        if (question === undefined || request === undefined || answerer === undefined) return undefined;
        return this.#ask(question, request, answerer);
    }

    /** Stops asking: the grid is going. */
    destroy(): void {
        this.#cancel();
        this.#question = undefined;
    }

    #cancel(): void {
        this.#asking?.abort();
        this.#asking = undefined;
        this.#askOwed = false;
    }

    async #ask(question: SummaryQuestion, request: GridSummaryRequest, answerer: SummaryAnswerer): Promise<void> {
        const asking = new AbortController();
        this.#asking = asking;
        let answer: GridSummaryResult;
        try {
            answer = await answerer(request, asking.signal);
        // oxlint-disable-next-line ex-grid/no-catch -- a question the grid let go may end in its cancellation, which nobody hears; any other failure is rethrown
        } catch (error) {
            if (asking.signal.aborted) return;
            throw error;
        }
        // Another question has been asked since, or none stands: this answer is about cells that are no longer the
        // selection, and is shown nowhere (ADR-0025's rule).
        if (asking.signal.aborted || question !== this.#question) return;
        this.#asking = undefined;
        const result = checkedAnswer(request, answer);
        this.#summary = Object.freeze({ status: result.kind, request, result });
        this.#raiseOwed = true;
        this.#answered();
    }
}

/** An answer as the grid takes it: every figure asked and no other, or a decline with its reason. One that is
 * neither is the answerer's defect, named rather than shown in part (ADR-0130, as ADR-0055 names one for Find). */
function checkedAnswer(request: GridSummaryRequest, answer: unknown): GridSummaryResult {
    if (typeof answer !== 'object' || answer === null) {
        throw new TypeError('A Selection Summary is answered with answeredSummary(figures) or declinedSummary(reason) (ADR-0130).');
    }
    const { kind, reason, figures, texts } = answer as { readonly kind?: unknown; readonly reason?: unknown; readonly figures?: unknown; readonly texts?: unknown };
    if (kind === 'declined') {
        if (typeof reason !== 'string') throw new TypeError('A declined Selection Summary says why, in words (ADR-0130).');
        return declinedSummary(reason);
    }
    if (kind !== 'answered' || typeof figures !== 'object' || figures === null) {
        throw new TypeError('A Selection Summary is answered with answeredSummary(figures) or declinedSummary(reason) (ADR-0130).');
    }
    const checked = answeredSummary(figures, typeof texts === 'object' && texts !== null ? texts : {});
    const answered = Object.keys(figures);
    if (answered.length !== request.figures.length || !request.figures.every(figure => answered.includes(figure))) {
        throw new TypeError(`The Selection Summary answered the figures [${answered.join(', ')}], and was asked for `
            + `[${request.figures.join(', ')}]. An answer gives every figure asked and no other, or declines (ADR-0130).`);
    }
    return checked;
}

/** A figure as the line shows it: its name's id and its text. */
export interface SummaryFigureShown {
    readonly figure: SummaryFigure;
    readonly labelId: string;
    readonly text: string;
}

/**
 * The figures an answer shows, in Excel's order: those that are numbers, each with the answerer's text or the
 * grid's, and #NUM! for one that is not a number, which is said rather than left out: a Sum missing from the line
 * reads as nothing to sum (ADR-0060, the spine's first rule). Counts are whole numbers in invariant text; the
 * others take the format of the Focus's column where it is a number column with a format of its own, Excel's
 * rule of the active cell's format whatever the other cells', and the number's own text otherwise.
 */
export function summaryFiguresShown<Row>(
    result: GridSummaryResult | undefined, focusColumn: Pick<QueryColumn<Row>, 'name' | 'type' | 'format'> | undefined,
): readonly SummaryFigureShown[] {
    if (result?.kind !== 'answered') return [];
    const shown: SummaryFigureShown[] = [];
    for (const figure of summaryFigureOrder) {
        const answer = result.figures[figure];
        if (answer === undefined) continue;
        const labelId = summaryLabelIdOf(figure);
        if (answer.kind === 'number') {
            const text = result.texts[figure] ?? formattedText(figure, answer, focusColumn) ?? summaryFigureText(figure, answer);
            if (text === undefined) throw new Error(`Internal: the ${figure} is a number and has no text.`);
            shown.push({ figure, labelId, text });
        } else if (answer.kind === 'error' && answer.error === 'notANumber') {
            shown.push({ figure, labelId, text: '#NUM!' });
        }
    }
    return shown;
}

/**
 * A figure in the Focus column's format, or undefined where the figure's own text is shown: a count, a column with
 * no format or that does not hold numbers, and a figure summed exactly that its `number` does not carry to the ten
 * decimal places its own text keeps, which a format handed that `number` would show rounded (the spine's first
 * rule). A format written for the column's own values, a `bigint` say, that cannot take the figure's `number` is
 * not the figure's: its own text is shown rather than nothing, as the C# core shows it past an InvalidCastException.
 */
function formattedText<Row>(
    figure: SummaryFigure, answer: AggregateResult & { readonly kind: 'number' }, focusColumn: Pick<QueryColumn<Row>, 'name' | 'type' | 'format'> | undefined,
): string | undefined {
    if (isCountFigure(figure) || focusColumn?.type !== 'number' || focusColumn.format === undefined) return undefined;
    if (answer.exact !== undefined) {
        const read = decimalOf(answer.number);
        if (read === undefined || decimalCompare(decimalRound(read, 10), decimalRound(answer.exact, 10)) !== 0) return undefined;
    }
    try {
        return textFor(focusColumn, answer.number);
    // oxlint-disable-next-line ex-grid/no-catch -- a format that cannot take a number is the column's, not the figure's: the figure's own exact text is shown, never a made-up one
    } catch (error) {
        if (error instanceof TypeError) return undefined;
        throw error;
    }
}

/**
 * The status line's summary box (ADR-0130): the pending words while the question is out, the reason in place of
 * the figures when it is declined, and each figure as "Name: text". One element for the grid's life, so the strip
 * the line stands in does not come and go with each selection; written only where it changed.
 */
export class SummaryBox {
    /** The box, which the grid stands in its status line. */
    readonly element: HTMLElement;
    #painted = '';

    constructor(doc: Document) {
        this.element = doc.createElement('span');
        this.element.className = 'ex-summary';
    }

    /** Marks the box as a control of the grid's own whose right-click the grid takes, while it does (ADR-0240). */
    claimMenu(claimed: boolean): void {
        if (claimed === this.element.hasAttribute('data-ex-claims')) return;
        if (claimed) this.element.setAttribute('data-ex-claims', 'contextmenu');
        else this.element.removeAttribute('data-ex-claims');
    }

    /** Writes the summary into the box. */
    paint(summary: SelectionSummary, shown: readonly SummaryFigureShown[], words: (id: string) => string): void {
        const status = summary.status;
        const reason = summary.result?.kind === 'declined' ? summary.result.reason : '';
        const figures = shown.map(({ labelId, text }) => `${words(labelId)}: ${text}`);
        const key = JSON.stringify([status, reason, figures, status === 'pending' ? words(summaryLabelIds.pending) : '']);
        if (key === this.#painted) return;
        this.#painted = key;
        this.element.className = status === 'pending' ? 'ex-summary ex-summary-pending'
            : status === 'declined' ? 'ex-summary ex-summary-declined' : 'ex-summary';
        const doc = this.element.ownerDocument;
        if (status === 'pending') {
            this.element.textContent = words(summaryLabelIds.pending);
        } else if (status === 'declined') {
            this.element.textContent = reason;
        } else {
            this.element.replaceChildren(...figures.map(text => {
                const figure = doc.createElement('span');
                figure.className = 'ex-summary-figure';
                figure.textContent = text;
                return figure;
            }));
        }
    }
}
