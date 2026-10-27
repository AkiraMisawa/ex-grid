import type { CellPosition } from '../selection/cell';
import type { SelectionRange } from '../selection/range';
import { browserReports } from './browser-reports';
import type { GridPopoverContext } from './consumer-popover';
import { GridView, type GridOptions, type GridViewRead } from './grid-view';

/** A grid drawn into an element, handed back by {@link createGrid}. */
export interface GridHandle<Row> {
    /** Takes the options again, whole: what did not change costs nothing, and a row that did not change is
     * not touched (ADR-0003). */
    update(options: GridOptions<Row>): void;
    /** Places `range` as the one range with the Focus on `focus`, as a click leaves it, if the grid's source
     * still says `rowSequenceVersion` (ADR-0050 item 4): true once placed, and otherwise refused through
     * `onRefused` (kind `placement`) or held by an open edit's Reject. A range outside the grid throws a
     * RangeError. */
    placeSelection(range: SelectionRange, focus: CellPosition, rowSequenceVersion: number): boolean;
    /** The view as the grid holds it now (ADR-0331): the Selection, with the Row Sequence Version it is written in,
     * and the scroll offsets the grid last knew. A Consumer that shows several sets of rows in one grid keeps each
     * set's view to hand back with `showViewWith`. */
    readView(): GridViewRead;
    /**
     * Shows `view`, read of this grid, once the grid takes rows under `rowSequenceVersion`, the source's (ADR-0331):
     * its Selection in place of the empty one a new version leaves (ADR-0011), and its scroll offsets. Handed as the
     * Consumer brings the rows the view was read of, under the version it brings them with, the two arrive together
     * in the grid's next pass. A view
     * handed later replaces one not yet taken; a Selection that does not fit the rows then shown is dropped.
     */
    showViewWith(view: GridViewRead, rowSequenceVersion: number): void;
    /** Hands the keyboard to the grid: to the field an open edit is typed in, else to where the grid takes keys
     * (ADR-0080). A Consumer's control beside the grid, such as a Sheet's tabs, gives it back with this. */
    focus(): void;
    /**
     * The keyboard back to the grid from something of the Consumer's that held it over the grid and goes away, a
     * dialog, a panel, a tab (DC-61, ADR-0070): to an open edit's field, else where the grid takes keys (ADR-0080).
     * Granted only while the keyboard is still this grid's, DOM focus inside it or on nothing, and not on its Formula
     * Bar or Name Box: a control or a grid the user has moved to keeps it. Handed `from`, the frame that holds the
     * keyboard as it goes, it is granted while DOM focus is on that frame too, so the frame gives the keyboard back
     * before it is removed. It moves neither the Focus nor the Selection and scrolls nothing; once the grid is
     * destroyed it does nothing.
     */
    returnKeyboard(from?: Element): void;
    /**
     * Hands the keyboard the grid holds on to a control of the Consumer's own, in place of the control's own
     * `focus()` (DC-61, ADR-0070): a frame's first control, a dialog's Close. A container names its first enabled
     * text field, its text selected, or else its first enabled control; one holding none throws a RangeError.
     * Granted under `returnKeyboard`'s condition, so a press the user made meanwhile keeps the keyboard where it put
     * it. It moves neither the Focus nor the Selection; the control scrolls into view unless `preventScroll` says
     * not to. Once the grid is destroyed, the control takes the keyboard by its own focus.
     */
    handKeyboardTo(control: HTMLElement, options?: { readonly preventScroll?: boolean }): void;
    /** A key press in the grid's canonical form (ADR-0012), `Control+PageUp`, as this grid reads its own keys: Command
     * stands for Control on an Apple platform. A Consumer that hears keys beside the grid's names them so. */
    keyName(event: KeyboardEvent): string;
    /**
     * Shows the Consumer's own contents in the grid's popover frame (ADR-0050, item 16): `content` makes the element
     * from the frame's context, which closes it. Under the header band, centred across the grid and bounded by its
     * box; its first control takes the keyboard, Tab wraps inside it, and Escape, a press elsewhere in the grid and
     * the box shrinking below a row close it, the keyboard back on the grid. False while an edit is open. `closed`
     * hears it close, however it closes, before its contents leave the page: where a Chrome's contents are taken
     * away (ADR-0390).
     */
    openPopover(content: (context: GridPopoverContext) => HTMLElement, label: string, closed?: () => void): boolean;
    /** Closes the Consumer's popover where one stands; the keyboard returns to the grid. */
    closePopover(): void;
    /** Whether anything can answer a Selection Summary (ADR-0130): `onSummarize`, or a source that summarises.
     * False: no figure ever appears. */
    readonly canSummarize: boolean;
    /** Tells the grid that the values under the selection may have moved where it cannot see them, a change
     * outside the Window or a recalculation (ADR-0130): the figures clear at once and the question is asked
     * again. Nothing happens while no figure stands or is being asked for. */
    refreshSummary(): void;
    /**
     * Writes `text` into the open edit as Point writes a Reference, for a press a Consumer took outside the grid
     * (ADR-0058): a Pointing Scope's `Positions[PV]`, or `XLOOKUP("R-4471", Positions[Id], Positions[PV])`. It goes in
     * place of what this Point wrote, from this grid's cells or from outside, or else at the caret where `pointAt` says
     * a Reference can go, and the caret stands after it. The edit is then in Point, written from outside: no outline
     * stands over this grid's cells, F4 changes nothing and the arrows move nothing. A further press, on this grid or
     * outside it, replaces the text, and anything typed or a caret moved ends Point over it. The grid reads nothing of
     * the text. False, and nothing changed, when no edit is open, pointing is not declared, or the edit is not in Point.
     * An empty text throws a TypeError.
     */
    writePointedText(text: string): boolean;
    /** Takes back the text `writePointedText` wrote last, while it still stands (ADR-0058): the edit returns to what it
     * was before that write, its text, caret and editing state, and what Point had written before it stands again.
     * False, and nothing changed, when no edit is open or that text no longer stands. */
    takeBackPointedText(): boolean;
    /**
     * Runs `handling`, work of the Consumer's that no input of the grid's made — a command from a button outside the
     * grid, a save — in its place in the Input Order (ADR-0240 section 6): after every input made so far, and before any
     * made after it. With nothing pending it runs at once, as part of the input under way where one is; while the
     * promise it answers is out, every input made in the grid waits, in its order, and is taken once it settles, an
     * Escape included. Once the grid is destroyed it runs nothing.
     *
     * @returns A promise that settles once `handling` has, with its failure where it failed.
     */
    inOrder(handling: () => void | PromiseLike<void>): Promise<void>;
    /**
     * Runs `handling`, work of the Consumer's that may take a while — a file still being read — in its place in the
     * Input Order as `inOrder` does, and lets the user cancel it with Escape (ADR-0240 section 12, ADR-0330 section 10).
     * From this call until it ends the Settled Stamp names it (`data-ex-work-placed`) and no longer says an Escape leaves
     * the grid; from its turn until it ends the root is busy (`aria-busy`), names it (`data-ex-work`) and is painted as a
     * paste under way is (`--ex-pasting-bar`, `--ex-pasting-bar-height`, `--ex-pasting-cursor`).
     *
     * A plain Escape whose keydown sees the work placed aborts `handling`'s signal and does nothing else: it closes no
     * editor and no popover. The work decides when it lands. Up to that point it looks at the signal and, once aborted,
     * rejects with the signal's reason, changing nothing; past it, it lands whatever the signal says, synchronously, so
     * no cancel falls between the look and the landing. An Escape that found the work landed is answered in its own
     * turn by `cancelledTooLate(true)`; one that found nothing to cancel — the work failed, or an Escape before it
     * cancelled it already — by `cancelledTooLate(false)`. Saying what the work did is the Consumer's. This is the one
     * outcome that depends on timing, accepted because a cancel exists to race the work it cancels, and both outcomes
     * are said.
     *
     * Destroying the grid aborts the signal of the work under way, and work still waiting never runs.
     *
     * @returns True once the work has landed; false when it rejected with its aborted signal's reason, or never ran. Any
     *   other failure is the promise's.
     * @throws Error Called while cancellable work's `handling` is being called: one Escape could not say which it
     *   cancels. Placed from a later turn, it waits behind the work under way.
     */
    cancellableInOrder(handling: (signal: AbortSignal) => void | PromiseLike<void>, cancelledTooLate: (landed: boolean) => void): Promise<boolean>;
    /** Stops the grid: it hears nothing more and empties its element. A source it was handed stays the
     * Consumer's. */
    destroy(): void;
}

/**
 * Draws a grid into `element`, which becomes its instance root (ADR-0350 section 16): the grid owns
 * everything inside it, and the element's own attributes it writes. A Framework Adapter renders the element
 * and hands the grid its props; nothing here assumes React's lifecycle or Blazor's.
 */
export function createGrid<Row>(element: HTMLElement, options: GridOptions<Row>): GridHandle<Row> {
    const view = new GridView(element, options, browserReports);
    return Object.freeze({
        update: (next: GridOptions<Row>) => { view.update(next); },
        placeSelection: (range: SelectionRange, focus: CellPosition, rowSequenceVersion: number) => view.placeSelection(range, focus, rowSequenceVersion),
        readView: () => view.readView(),
        writePointedText: (text: string) => view.writePointedText(text),
        takeBackPointedText: () => view.takeBackPointedText(),
        showViewWith: (state: GridViewRead, rowSequenceVersion: number) => { view.showViewWith(state, rowSequenceVersion); },
        focus: () => { view.focus(); },
        returnKeyboard: (from?: Element) => { view.returnKeyboard(from); },
        handKeyboardTo: (control: HTMLElement, how?: { readonly preventScroll?: boolean }) => { view.handKeyboardTo(control, how); },
        keyName: (event: KeyboardEvent) => view.keyName(event),
        openPopover: (content: (context: GridPopoverContext) => HTMLElement, label: string, closed?: () => void) => view.openPopover(content, label, closed),
        closePopover: () => { view.closePopover(); },
        get canSummarize() {
            return view.canSummarize;
        },
        refreshSummary: () => { view.refreshSummary(); },
        inOrder: (handling: () => void | PromiseLike<void>) => view.inOrder(handling),
        cancellableInOrder: (handling: (signal: AbortSignal) => void | PromiseLike<void>, cancelledTooLate: (landed: boolean) => void) =>
            view.cancellableInOrder(handling, cancelledTooLate),
        destroy: () => { view.destroy(); },
    });
}
