// What a Cell Editor commit says to the Consumer (ADR-0007, ADR-0034, ADR-0142): the Edit Intent it raises,
// the verdict a column may give first, and the reasons an edit ends without one. The grid writes nothing: a
// commit leaves as an intent, and the screen changes when the source hands over a new row instance.

import type { CellPosition } from '../selection/cell';

/**
 * The Consumer's judgement on one commit, asked at the moment of committing (ADR-0034). The grid enforces the
 * verb and never learns the reason.
 *
 * - `accept` raises the Edit Intent unchanged.
 * - `flag` raises it unchanged too: the value is applied, and the Consumer marks it through what the cell
 *   shows. Loudly wrong, never quietly.
 * - `reject` holds the editor: every commit gesture stops, the message is shown at the editor and said in the
 *   live region, and Escape is the only way out without applying. Excel's Stop style.
 *
 * The intended split is that an unparseable text rejects and a parseable value a rule dislikes flags. A Consumer
 * may also reject an object its domain says cannot exist, which it composes from the row and the text (ADR-0380
 * section 4).
 *
 * A column's `validate` answers one at once or as a promise (ADR-0380 section 1). While a promise is out, the
 * commit is pending: the editor stands with the text, every input made meanwhile waits for the answer, and Escape
 * cancels it, aborting the signal the verdict was handed.
 */
export type EditVerdict =
    | { readonly kind: 'accept' }
    | { readonly kind: 'flag'; readonly message: string }
    | { readonly kind: 'reject'; readonly message: string };

/** Why the grid threw away text a user had typed and not committed (ADR-0011, ADR-0035, ADR-0142). None is
 * the user's own doing, which is why it is told rather than assumed. */
export type EditDiscardReason =
    /** The columns changed under the open editor, so its position stopped naming the column it was opened on
     * (ADR-0011). */
    | 'columnsChanged'
    /** No Row Key is in force, and the row left the Window under the order the editor was opened under before
     * the commit landed: there is no row left to carry the intent, and a guess by position would land the
     * value on another row (ADR-0011). With a Row Key the commit is refused instead (ADR-0142). */
    | 'rowLeftTheWindow'
    /** The column stopped being Editable while its editor was open: `editable` is asked again at the commit,
     * not only when the editor opened (ADR-0035). A grid no longer given `onEdit` edits no column. */
    | 'columnNoLongerEditable'
    /** Typing reached the grid aimed at a Selection the rows' order took away since it was painted: it opens
     * nothing, since the cell it would open on is one the user has not seen focused (ADR-0142, 2026-10-08). Said
     * once for the keys typed at it. */
    | 'orderMoved'
    /** The grid's source was replaced by another, so the row the editor was opened on, or the Selection typing
     * was aimed at, belongs to rows the grid no longer shows (ADR-0011, ADR-0142). */
    | 'sourceChanged';

/** Why a Cell Editor commit was refused: the grid cannot find, in the rows it holds now, the row the editor
 * was opened on (ADR-0142), or the user cancelled it while its answer was out (ADR-0380). The editor stays open
 * with what was typed, and Escape leaves without writing. */
export type CommitRefusalReason =
    /** A Row Key is in force and no row in the Window answers the key of the row the editor was opened on. A
     * commit made once the row is back in the Window lands. */
    | 'rowLeftTheWindow'
    /** No Row Key is in force and the Row Sequence Version moved while the editor was open: its position
     * names another row now. */
    | 'orderMoved'
    /** Escape was typed while the commit was pending, its verdict or its `onEdit` not yet answered (ADR-0380
     * section 3): the signal they were handed is aborted, and nothing is written. */
    | 'cancelled';

/**
 * The Edit Intent (ADR-0007): the notification a commit raises, with the row it lands on, the column and the
 * text as the user entered it. The Consumer parses the text by its own column type.
 *
 * It carries the text the user saw of the cell when the editor opened, and the text the cell painted as the
 * commit landed (ADR-0142): when the two differ, the value changed under the editor. A Consumer that will not
 * write it calls {@link GridEditIntent.refuse} before its `onEdit` returns, or before the promise `onEdit` returns
 * settles (ADR-0380 section 2), and the editor stays open with the typing, the message shown at it and said in the
 * live region, as a verdict's reject is (ADR-0034).
 */
export class GridEditIntent<Row> {
    /** The row instance the edit lands on: Row Identity, not a position. */
    readonly row: Row;
    /** The column's name. */
    readonly column: string;
    /** The editor's text as committed. */
    readonly value: string;
    /** What the edited cell painted when the editor opened over it. */
    readonly seenText: string;
    /** What the edited cell paints as the commit lands: what the commit replaces. */
    readonly replacedText: string;
    /** Aborted when the user cancels the commit with Escape while `onEdit`'s promise is out (ADR-0380 section 3):
     * nothing is to be written then, and a refusal after it throws. The verdict was handed the same signal. */
    readonly signal: AbortSignal;
    #refusal: string | undefined;
    #sealed = false;

    /** @internal */
    constructor(row: Row, column: string, value: string, seenText: string, replacedText: string, signal: AbortSignal) {
        this.row = row;
        this.column = column;
        this.value = value;
        this.seenText = seenText;
        this.replacedText = replacedText;
        this.signal = signal;
    }

    /** The message the Consumer refused the intent with, or `undefined` while it has not. */
    get refusalMessage(): string | undefined {
        return this.#refusal;
    }

    /** Whether the Consumer refused the intent. */
    get refused(): boolean {
        return this.#refusal !== undefined;
    }

    /**
     * The Consumer's answer that it does not write this edit (ADR-0142, ADR-0034). Called before `onEdit`
     * returns, or before the promise it returned settles (ADR-0380), the editor stays open with the text typed, the
     * gesture that committed keeps no meaning of its own, and `message` is shown at the editor and said in the live
     * region. A later commit raises the intent again. Called after, it throws: the editor has closed, and a refusal
     * it never heard would leave the screen saying the edit was taken. Called once the user has cancelled the
     * commit, it throws too: nothing was to be written.
     */
    refuse(message: string): void {
        if (this.#sealed) {
            throw new Error(this.signal.aborted
                ? 'This edit was cancelled while onEdit was answering it: nothing is to be written (ADR-0380).'
                : 'This edit was answered when onEdit returned, or when its promise settled: refuse it before then (ADR-0142, ADR-0380).');
        }
        // From an untyped caller anything; a refusal that says nothing is the silence ADR-0034 rules out.
        const given: unknown = message;
        if (typeof given !== 'string' || given.trim() === '') {
            throw new TypeError('An edit is refused with a sentence that says why (ADR-0034): the grid holds none of its own.');
        }
        this.#refusal = message;
    }

    /** @internal Ends the time the Consumer may refuse it in. */
    seal(): void {
        this.#sealed = true;
    }
}

/** A verdict a column's `validate` answered, checked where it arrives: an untyped caller may answer anything,
 * and a flag or a reject that says nothing would paint an error the user cannot act on (ADR-0034). */
export function checkedVerdict(verdict: unknown, column: string): EditVerdict {
    if (typeof verdict === 'object' && verdict !== null) {
        const { kind, message }: { readonly kind?: unknown; readonly message?: unknown } = verdict;
        if (kind === 'accept') return { kind };
        if ((kind === 'flag' || kind === 'reject') && typeof message === 'string' && message.trim() !== '') return { kind, message };
    }
    throw new TypeError(`Column '${column}': validate answers { kind: 'accept' }, or { kind: 'flag' | 'reject', message } with a `
        + `sentence that says why, not ${JSON.stringify(verdict)} (ADR-0034).`);
}

/**
 * The Overwrite Notice (ADR-0142, CONTEXT.md): a Cell Editor commit landed over text that changed while the
 * editor was open, the one change the user cannot see, because the editor covers the cell. Nothing was
 * refused: the commit landed with what the user typed, and the Consumer accepted its intent. A Consumer may
 * offer to put the replaced value back. The grid holds no string for it: Chrome words it into the root's live
 * region, as it words a refusal (A11Y-16, LV-21), and nothing is added to the row (ADR-0013).
 */
export interface GridOverwriteNotice {
    /** The edited cell's position, in the order in force when the commit landed. */
    readonly cell: CellPosition;
    /** The edited column's name. */
    readonly column: string;
    /** The text the user saw: what the cell painted when the editor opened, or, where one of the user's own
     * earlier writes to it had not settled then, what it painted once that write settled (D1). The Edit
     * Intent's `seenText`. */
    readonly seenText: string;
    /** The text the cell painted as the commit landed: what it replaced. The Edit Intent's `replacedText`. */
    readonly replacedText: string;
}
