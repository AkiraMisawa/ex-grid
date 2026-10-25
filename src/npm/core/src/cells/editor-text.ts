// Formula entry's own pieces of the editor's text (ADR-0051): the Consumer's candidates and hint, and what
// accepting a candidate does to the text. The grid does not know what a Formula is, so it neither chooses a
// candidate's span nor spells its replacement. Where the caret stands after the user's own typing is not
// answered here: the browser reports it, because working it out from the change is ambiguous where letters
// repeat (ADR-0051's second round).

/**
 * One candidate the Consumer offers for the text being typed (ADR-0051): what the list shows, and what
 * accepting it does to the editor's text, the span it replaces and what it writes there. `=SU` offers `SUM`,
 * replacing the two letters with `SUM(`. The caret lands after what was written.
 */
export interface CompletionCandidate {
    /** What the list shows for the candidate. */
    readonly label: string;
    /** Where the replaced span starts in the text the answer was for. */
    readonly start: number;
    /** How long the replaced span is; zero inserts at `start`. */
    readonly length: number;
    /** What accepting the candidate writes in the span's place. */
    readonly insert: string;
}

/**
 * The argument hint beneath the candidates (ADR-0051): a line of text, such as `SUM(number1, [number2], …)`,
 * and the span of it that names the argument the caret is in, for Chrome to set off. An empty span sets off
 * nothing.
 */
export class EditorHint {
    /** The hint. */
    readonly text: string;
    /** Where the argument the caret is in starts, in {@link EditorHint.text}. */
    readonly emphasisStart: number;
    /** How long that argument's name is; zero for none. */
    readonly emphasisLength: number;

    /** A hint, with the span it sets off; none by default. */
    constructor(options: { readonly text: string; readonly emphasisStart?: number; readonly emphasisLength?: number }) {
        const { text, emphasisStart = 0, emphasisLength = 0 } = options;
        this.text = text;
        this.emphasisStart = emphasisStart;
        this.emphasisLength = emphasisLength;
    }

    /** The hint split around its emphasis, for a Chrome to paint the middle part set off. A span that does
     * not lie inside the text sets off nothing, rather than a guess. */
    parts(): readonly [before: string, emphasis: string, after: string] {
        const { text, emphasisStart: start, emphasisLength: length } = this;
        if (length <= 0 || start < 0 || start > text.length || length > text.length - start) {
            return Object.freeze([text, '', ''] as const);
        }
        return Object.freeze([text.slice(0, start), text.slice(start, start + length), text.slice(start + length)] as const);
    }
}

/**
 * The Consumer's answer to the editor's text and caret (ADR-0051): the candidates to list, in the order to
 * list them, and an optional hint. Either may be empty; an answer with neither shows nothing. An answer for
 * text that has since changed is dropped, never shown.
 */
export class EditorCompletion {
    /** Nothing to offer: no list and no hint. */
    static readonly none: EditorCompletion = new EditorCompletion({ candidates: [] });

    /** The candidates, first shown first and chosen first. */
    readonly candidates: readonly CompletionCandidate[];
    /** The argument hint, or `undefined`. */
    readonly hint: EditorHint | undefined;

    /** An answer: candidates, and a hint where there is one. */
    constructor(options: { readonly candidates: readonly CompletionCandidate[]; readonly hint?: EditorHint }) {
        this.candidates = Object.freeze([...options.candidates]);
        this.hint = options.hint;
    }

    /** Whether there is anything to show. */
    get isEmpty(): boolean {
        return this.candidates.length === 0 && this.hint === undefined;
    }
}

/**
 * `text` with the span from `start` of `length` characters replaced by `insert`, and the caret after what
 * was written. A span outside the text is refused by name rather than clamped into a plausible edit of the
 * wrong characters.
 */
export function replaceSpan(text: string, start: number, length: number, insert: string): readonly [text: string, caret: number] {
    if (!Number.isSafeInteger(start) || !Number.isSafeInteger(length) || start < 0 || length < 0
        || start > text.length || length > text.length - start) {
        throw new RangeError(`The span ${start}+${length} does not lie inside the editor's text of ${text.length} characters (ADR-0051).`);
    }
    return Object.freeze([text.slice(0, start) + insert + text.slice(start + length), start + insert.length] as const);
}

/** The text and caret after accepting `candidate` in `text` (ADR-0051). */
export function acceptCandidate(text: string, candidate: CompletionCandidate): readonly [text: string, caret: number] {
    return replaceSpan(text, candidate.start, candidate.length, candidate.insert);
}
