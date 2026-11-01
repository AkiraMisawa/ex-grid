// The Error Count a grid shows (ADR-0381 section 3's note of 2026-11-01): every cell that wears Error, and how many of
// them the error step cannot reach. The grid asks and whoever holds the errors answers, as for the step; the grid shows
// only the answer to the current question. The question is the judgement painted, the source, the order the result was
// read in and the visible columns: any of these moving clears the count in the pass that shows the move, and asks
// again after it. A late answer is dropped by the question it answers, never by a time, as the Selection Summary's is
// (ADR-0130).

import { errorCountLabelIds } from '../chrome/error-count-words';
import type { GridErrorCount, GridErrorCountRequest } from '../cells/grid-ruleset';

/** Whoever answers the Error Count: the Consumer's `countErrors`. */
export type ErrorCounter = (request: GridErrorCountRequest, signal: AbortSignal) => GridErrorCount | PromiseLike<GridErrorCount>;

/** What a count is asked over: equal parts are the same question, and nothing is asked again. */
export interface ErrorCountQuestion {
    /** The source the result is read from, told apart by identity: two fresh sources share a version (ADR-0142). */
    readonly binding: object;
    readonly rowSequenceVersion: number;
    readonly columns: readonly string[];
    /** The judgement painted, told apart by identity: the cell state and message functions, which a Consumer hands
     * afresh after each judgement (ADR-0003). The counter's own identity is not part of it, so a counter written
     * inline, another function on every render, does not ask again on every render. */
    readonly cellState: unknown;
    readonly cellMessage: unknown;
}

function sameItems(a: readonly unknown[], b: readonly unknown[]): boolean {
    return a.length === b.length && a.every((item, at) => item === b[at]);
}

function sameQuestion(a: ErrorCountQuestion, b: ErrorCountQuestion): boolean {
    return a.binding === b.binding && a.rowSequenceVersion === b.rowSequenceVersion && sameItems(a.columns, b.columns)
        && a.cellState === b.cellState && a.cellMessage === b.cellMessage;
}

/** The question in force and its answer, asked once the pass that showed the question has painted. */
export class ErrorCountQuestions {
    readonly #answered: () => void;
    #question: ErrorCountQuestion | undefined;
    #request: GridErrorCountRequest | undefined;
    #count: GridErrorCount | undefined;
    #failed = false;
    #asking: AbortController | undefined;
    #askOwed = false;

    /** `answered` hears an answer to the question in force, so the grid paints again. */
    constructor(answered: () => void) {
        this.#answered = answered;
    }

    /** The answer to the question in force; undefined while it is out, none stands, or the counter failed. */
    get count(): GridErrorCount | undefined {
        return this.#count;
    }

    /** Whether the counter failed the question in force, or answered it with something that is not a count: the line
     * says no count can be given, rather than go on saying it is counting. */
    get failed(): boolean {
        return this.#failed;
    }

    /** Whether a question is out, unanswered: the grid has not settled while it is (ADR-0220). */
    get asking(): boolean {
        return this.#question !== undefined && this.#count === undefined && !this.#failed;
    }

    /** The question the grid's state asks, or none: one different from the question in force clears the count now
     * and owes the asking to `settle`, which the grid runs once it has painted. */
    review(question: ErrorCountQuestion | undefined): void {
        if (question === undefined) {
            this.#cancel();
            this.#question = undefined;
            this.#request = undefined;
            this.#count = undefined;
            this.#failed = false;
            return;
        }
        if (this.#question !== undefined && sameQuestion(this.#question, question)) return;
        this.#cancel();
        this.#question = question;
        this.#request = Object.freeze({ columns: question.columns, rowSequenceVersion: question.rowSequenceVersion });
        this.#count = undefined;
        this.#failed = false;
        this.#askOwed = true;
    }

    /** Run after the pass that painted the line: asks the question owed. The answer is not awaited; a failure of the
     * counter's, or an answer that is not a count, rejects the asking, which the grid leaves unheard so that it
     * reaches the page as an unhandled rejection rather than a count nobody can trust. */
    settle(counter: ErrorCounter | undefined): Promise<void> | undefined {
        if (!this.#askOwed) return undefined;
        this.#askOwed = false;
        const question = this.#question;
        const request = this.#request;
        if (question === undefined || request === undefined || counter === undefined) return undefined;
        return this.#ask(question, request, counter);
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

    async #ask(question: ErrorCountQuestion, request: GridErrorCountRequest, counter: ErrorCounter): Promise<void> {
        const asking = new AbortController();
        this.#asking = asking;
        let count: GridErrorCount;
        try {
            const answer = await counter(request, asking.signal);
            // Another question has been asked since, or none stands: this answer counts another judgement or another
            // result, and is shown nowhere.
            if (asking.signal.aborted || question !== this.#question) return;
            count = checkedCount(answer);
        // oxlint-disable-next-line ex-grid/no-catch -- a question the grid let go may end in its cancellation, which nobody hears; any other failure is said on the line and rethrown
        } catch (error) {
            if (asking.signal.aborted || question !== this.#question) return;
            this.#asking = undefined;
            this.#failed = true;
            this.#answered();
            throw error;
        }
        this.#asking = undefined;
        this.#count = count;
        this.#answered();
    }
}

/** An answer as the grid takes it: two whole numbers, the hidden no more than the count. One that is not is the
 * counter's defect, named rather than shown (spine rule 1). */
function checkedCount(answer: unknown): GridErrorCount {
    if (typeof answer === 'object' && answer !== null) {
        const { count, hidden }: { readonly count?: unknown; readonly hidden?: unknown } = answer;
        if (typeof count === 'number' && typeof hidden === 'number' && Number.isSafeInteger(count) && Number.isSafeInteger(hidden)
            && hidden >= 0 && hidden <= count) {
            return Object.freeze({ count, hidden });
        }
    }
    throw new TypeError(`countErrors answers { count, hidden }: whole numbers, hidden no more than count, not ${JSON.stringify(answer)} (ADR-0381).`);
}

/** The count's button in the status line, one for the grid's life: pressing it is the step forward, F8's. */
export class ErrorCountBox {
    /** The button, which the grid stands in its status line. */
    readonly element: HTMLButtonElement;
    #painted = '';

    readonly #pressed: () => void;

    constructor(doc: Document, pressed: () => void) {
        this.#pressed = pressed;
        this.element = doc.createElement('button');
        this.element.type = 'button';
        this.element.className = 'ex-error-count';
        this.element.setAttribute('data-ex-claims', 'click');
        this.element.addEventListener('click', pressed);
    }

    /** The button's listener goes: the grid is going. */
    destroy(): void {
        this.element.removeEventListener('click', this.#pressed);
    }

    /** Writes the count, the words that say none could be given, or the pending words while the question is out. */
    paint(count: GridErrorCount | undefined, failed: boolean, words: (id: string) => string): void {
        const text = failed ? words(errorCountLabelIds.failed) : count === undefined ? words(errorCountLabelIds.pending)
            : words(count.hidden > 0 ? errorCountLabelIds.countHidden : errorCountLabelIds.count)
                .replaceAll('{count}', String(count.count)).replaceAll('{hidden}', String(count.hidden));
        const className = failed ? 'ex-error-count ex-error-count-some' : count === undefined ? 'ex-error-count ex-error-count-pending'
            : count.count > 0 ? 'ex-error-count ex-error-count-some' : 'ex-error-count';
        const title = words(errorCountLabelIds.step);
        const key = JSON.stringify([text, className, title]);
        if (key === this.#painted) return;
        this.#painted = key;
        this.element.textContent = text;
        this.element.className = className;
        this.element.title = title;
    }
}
