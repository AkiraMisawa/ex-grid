// ExSheet drawn into an element (ADR-0370, section 1): one ExGrid as the Sheet's Consumer (ADR-0046), and the
// Sheet's notice beneath it, its live region. The Sheet holds and computes; the grid paints, selects, navigates and
// reports. Each intent the grid raises becomes one engine step on the one undo stack (ADR-0048), and each change
// the engine reports hands the grid a new instance for the rows it names and nothing else (ADR-0003).
//
// What is deliberately not wired: the grid's sort and filter, since a Sheet's rows are places (ADR-0046); the
// header click selects the column instead (ADR-0050 item 1), and the column menu is hidden, as Excel's Headings
// carry none (ADR-0050 item 12).

import {
    createGrid, type CellPosition,
    type GridClearIntent, type GridColumnWidthChange, type GridDensity, type GridEditIntent, type GridHandle, type GridOptions,
    type GridOverwriteNotice, type GridPasteIntent, type GridRefusal, type GridSelection, type EditDiscardReason, type SelectionRange,
    type ViewportSize,
} from '@ex-grid/core';
import {
    CellAddress, CellRange, Culture, Entry, FormulaSyntaxError, SheetEdit, SheetRefusedError, Value, Workbook,
    Sheet, SheetRefusal, type FillDirection, type SheetChange, type SheetDocument, type WorkbookChange, type WorkbookDocument, type WorkbookStep,
} from '@ex-grid/sheet-engine';
import { unreachable } from './must';
import { exactNumberFor, isTooNarrowToShow, refusedIn, typedFor } from './sheet-clipboard';
import { appearanceLookup, charactersOfColumn, SheetColumns, sheetCellPaint } from './sheet-columns';
import { SheetHistory, type SheetOperationStepped } from './sheet-history';
import type { SheetRow } from './sheet-row';
import { SheetSource } from './sheet-source';
import { readNameBoxPlace } from './name-box-text';
import { SheetWords, type SheetLabel } from './sheet-words';

/** What a Sheet is drawn with. */
export interface SheetOptions {
    /** The Workbook or Sheet to open; a new Workbook of one empty Sheet unless given. A new document object opens
     * it in place of what is shown, with nothing to undo; one `onWorkbookChanged` handed out, handed back as a
     * controlled component's state is, is the Sheet's own and changes nothing. */
    readonly document?: SheetDocument | WorkbookDocument;
    /** The culture of a Sheet started empty: how typed constants are read and Values shown (ADR-0048). A document
     * carries its own. The browser's language unless given; one the engine holds no data for is refused by name. */
    readonly locale?: string;
    /** Shows the Formula Bar and its Name Box above the columns (ADR-0051); shown unless `false`. */
    readonly showFormulaBar?: boolean;
    /** ExSheet's Japanese words in place of its English (ADR-0370, section 10), chosen in one line, never by the
     * locale. */
    readonly japanese?: boolean;
    /** The Consumer's word for any id, which wins over ExSheet's own (ADR-0370, section 10). */
    readonly label?: SheetLabel;
    /** The grid's density (ADR-0028); `compact` unless given. Column widths in characters follow it. */
    readonly density?: GridDensity;
    /** The height of a row in pixels (ADR-0013). */
    readonly rowHeight?: number;
    /** The grid's width: pixels, or `stretch` to fill the element's box. */
    readonly viewportWidth?: ViewportSize;
    /** The grid's height: pixels, or `stretch` to fill the element's box. */
    readonly viewportHeight?: ViewportSize;
    /** Hears the Workbook as it stands after each operation: an edit, an undo, a redo, a width. */
    readonly onWorkbookChanged?: (document: WorkbookDocument) => void;
}

/** A Sheet drawn into an element, handed back by {@link createSheet}. */
export interface SheetHandle {
    /** The Workbook the Sheet edits. Change it only through the Sheet, or what is shown falls behind it. */
    readonly workbook: Workbook;
    /** Takes the options again, whole. */
    update(options: SheetOptions): void;
    /** Undoes the latest operation; false when there is none, which the notice says. */
    undo(): boolean;
    /** Redoes the latest operation undone; false when there is none, which the notice says. */
    redo(): boolean;
    /** Stops the Sheet and empties its element. */
    destroy(): void;
}

/** Draws a Sheet into `element`, which it owns from now on. */
export function createSheet(element: HTMLElement, options: SheetOptions = {}): SheetHandle {
    const view = new SheetView(element, options);
    return Object.freeze({
        get workbook() { return view.workbook; },
        update: (next: SheetOptions) => { view.update(next); },
        undo: () => view.undo(),
        redo: () => view.redo(),
        destroy: () => { view.destroy(); },
    });
}

class SheetView {
    readonly #element: HTMLElement;
    readonly #gridElement: HTMLElement;
    readonly #notice: HTMLElement;
    readonly #history = new SheetHistory();
    #options: SheetOptions;
    #words: SheetWords;
    #workbook: Workbook;
    #document: SheetDocument | WorkbookDocument | undefined;
    readonly #source: SheetSource;
    #columns: SheetColumns;
    #reading = 0;
    #appearance: ReturnType<typeof appearanceLookup>;
    #grid: GridHandle<SheetRow> | undefined;
    // Whether the notice stands through the move that completes the gesture that told it (a commit's Enter).
    #noticeOutlivesMove = false;
    // The Selection the grid last reported, and whether an edit is open there.
    #selection: GridSelection | undefined;
    #editing = false;
    // A resize of several whole columns, reported a column at a time: its first step, and the columns still to
    // come, which join that step on the undo stack (FN-12c, ADR-0048).
    #resizing: { readonly first: WorkbookStep; readonly pending: Set<number> } | undefined;
    #destroyed = false;
    // The documents this Sheet handed out: one handed back as `document`, as a controlled component's state comes
    // back, is the Sheet's own state, and is not opened again over its undo stack.
    readonly #raised = new WeakSet<object>();

    constructor(element: HTMLElement, options: SheetOptions) {
        this.#element = element;
        this.#options = SheetView.#checked(options);
        this.#words = new SheetWords({ japanese: this.#options.japanese, label: this.#options.label });
        this.#document = this.#options.document;
        this.#workbook = this.#open(this.#options);
        this.#source = new SheetSource(this.#workbook.shown);
        this.#columns = new SheetColumns(this.#options.density ?? 'compact', error => this.#words.formulaUnreadable(error)); // oxlint-disable-line ex-grid/no-stand-in-default -- the grid's own default density (ADR-0028)
        this.#columns.read(this.#workbook.shown);
        this.#appearance = appearanceLookup(this.#reading);

        element.classList.add('ex-sheet');
        element.setAttribute('data-ex-input-root', '');
        this.#gridElement = document.createElement('div');
        this.#notice = document.createElement('div');
        this.#notice.className = 'ex-sheet-notice';
        this.#notice.setAttribute('role', 'status');
        element.replaceChildren(this.#gridElement, this.#notice);
        this.#grid = createGrid(this.#gridElement, this.#gridOptions());
    }

    get workbook(): Workbook { return this.#workbook; }

    update(options: SheetOptions): void {
        this.#alive();
        const next = SheetView.#checked(options);
        // Judged by the culture the Sheet holds, so a locale naming that culture again changes nothing; with a document,
        // the document's culture is the one, as the option says.
        if (next.document === undefined && this.#document === undefined && Culture.of(next.locale ?? navigator.language) !== this.#source.sheet.culture) {
            throw new Error("A Sheet's culture is recorded in its document, so it is not changed on a live Sheet: Entries typed would not be read again (ADR-0048).");
        }
        if ((next.density ?? 'compact') !== (this.#options.density ?? 'compact')) { // oxlint-disable-line ex-grid/no-stand-in-default -- the grid's own default density (ADR-0028)
            this.#columns = new SheetColumns(next.density ?? 'compact', error => this.#words.formulaUnreadable(error)); // oxlint-disable-line ex-grid/no-stand-in-default -- the grid's own default density (ADR-0028)
            this.#columns.read(this.#source.sheet);
        }
        if (next.label !== this.#options.label || next.japanese !== this.#options.japanese) this.#words = new SheetWords({ japanese: next.japanese, label: next.label });
        this.#options = next;
        // A document this Sheet raised, handed back, is its own state: nothing is opened again.
        if (next.document !== undefined && this.#raised.has(next.document)) this.#document = next.document;
        if (next.document !== this.#document) {
            this.#document = next.document;
            this.#workbook = this.#open(next);
            this.#history.clear();
            this.#showSheet(this.#workbook.shown);
            return;
        }
        this.#grid?.update(this.#gridOptions());
    }

    undo(): boolean {
        this.#alive();
        this.#refuseWhileEditing();
        const stepped = this.#history.undo();
        if (stepped === undefined) {
            this.#tell(this.#words.nothingToUndo);
            return false;
        }
        this.#stepped(stepped);
        return true;
    }

    redo(): boolean {
        this.#alive();
        this.#refuseWhileEditing();
        const stepped = this.#history.redo();
        if (stepped === undefined) {
            this.#tell(this.#words.nothingToRedo);
            return false;
        }
        this.#stepped(stepped);
        return true;
    }

    // A step taken under an open editor would change the cells, or the Sheet, it commits to: refused, as the C#
    // Sheet's is (ADR-0048).
    #refuseWhileEditing(): void {
        if (this.#editing) throw new SheetRefusedError(new SheetRefusal('editIsOpen', this.#words.editIsOpen));
    }

    destroy(): void {
        if (this.#destroyed) return;
        this.#destroyed = true;
        this.#grid?.destroy();
        this.#grid = undefined;
        this.#element.replaceChildren();
        this.#element.classList.remove('ex-sheet');
        this.#element.removeAttribute('data-ex-input-root');
    }

    // ---- The grid ---------------------------------------------------------------------------------------------

    #gridOptions(): GridOptions<SheetRow> {
        const { rowHeight, viewportWidth, viewportHeight, density } = this.#options;
        return {
            columns: this.#columns.all,
            source: this.#source,
            ...(density === undefined ? {} : { density }),
            ...(rowHeight === undefined ? {} : { rowHeight }),
            ...(viewportWidth === undefined ? {} : { viewportWidth }),
            ...(viewportHeight === undefined ? {} : { viewportHeight }),
            ...sheetCellPaint,
            cellAppearance: this.#appearance,
            headerClickSelects: true,
            showFormulaBar: this.#options.showFormulaBar !== false,
            nameBoxLabel,
            onNameBoxEntered: this.#onNameBoxEntered,
            hideColumnMenu: true,
            hideCopyWithHeaders: true,
            pasteMaySpill: true,
            onEdit: this.#onEdit,
            onPaste: this.#onPaste,
            onClear: this.#onClear,
            onUndo: this.#onUndo,
            onRedo: this.#onRedo,
            onColumnWidthChanged: this.#onColumnWidthChanged,
            onSelectionChanged: this.#onSelectionChanged,
            onEditingChanged: this.#onEditingChanged,
            onRefused: this.#onRefused,
            onEditDiscarded: this.#onEditDiscarded,
            onOverwriteNotice: this.#onOverwriteNotice,
        };
    }

    readonly #onEdit = (intent: GridEditIntent<SheetRow>): void => {
        const column = CellAddress.tryParseColumn(intent.column);
        if (column === undefined) throw new Error(`Internal: '${intent.column}' is not one of the Sheet's columns.`);
        // A Formula committed while another Sheet is shown lands on the Sheet it was typed on (ADR-0331 §5).
        if (intent.row.sheet !== this.#source.sheet) {
            if (!this.#workbook.sheets.includes(intent.row.sheet)) {
                intent.refuse(this.#words.editSheetGone);
                return;
            }
            // Shown, and told, before the commit, which may yet be refused.
            this.#showSheet(intent.row.sheet);
            this.#raiseDocument();
        }
        const address = new CellAddress(intent.row.index, column);
        try {
            this.#perform(SheetEdit.enter(address, intent.value), undefined, [new CellRange(address)]);
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the engine's refusal and an unreadable Formula are answers told to the user; anything else is rethrown
            // The column's verdict rejects an unreadable Formula before the intent is raised (ADR-0034); this is
            // the net under it, and it changes nothing either.
            if (error instanceof FormulaSyntaxError) {
                this.#tell(this.#words.formulaUnreadable(error), { outlivesMove: true });
                return;
            }
            if (error instanceof SheetRefusedError) {
                this.#tell(this.#words.refused(error.refusal), { outlivesMove: true });
                return;
            }
            throw error;
        }
    };

    readonly #onPaste = (intent: GridPasteIntent): void => {
        // Rows are places, so the order moves only when another Sheet is shown (ADR-0046); an intent written
        // under any other version names positions that mean something else, and is dropped (ADR-0011).
        if (intent.rowSequenceVersion !== this.#source.snapshot().rowSequenceVersion) {
            intent.refuse();
            return;
        }
        const sheet = this.#source.sheet;
        // Ctrl+D and Ctrl+R (ADR-0035): the range the grid read, its Entries copied with relative References
        // shifted, never a series (ADR-0050 item 5).
        if (intent.fillSource !== undefined) {
            const target = rangeOf(firstTarget(intent));
            const direction: FillDirection = firstTarget(intent).topRow > intent.fillSource.topRow + intent.fillSource.rowCount - 1 ? 'down' : 'right';
            this.#performChecked(intent, SheetEdit.fillCopy(rangeOf(intent.fillSource), target, direction));
            return;
        }
        // Ctrl+Enter (ADR-0050 item 5): text typed into every cell of the Selection, read as entered in the cell
        // the grid names, a Formula's relative References shifted by each cell's offset.
        if (intent.enteredAt !== undefined) {
            const at = intent.enteredAt;
            this.#performRefusing(intent, () => SheetEdit.enterInto(intent.plan.targets.map(rangeOf), new CellAddress(at.row, at.column), intent.valueFor(at)));
            return;
        }
        // A Sheet's copy holding a refused cell, marked in its table: refused whole, rather than turn the refusal into
        // an Error Value a Formula can catch (ADR-0047, 2026-10-12).
        const refusedFrom = firstRefusedIn(intent);
        if (refusedFrom !== undefined) {
            intent.refuse();
            this.#tell(this.#words.pasteRefusedFormula(refusedFrom));
            return;
        }
        const typed: [CellAddress, string][] = [];
        const exact: [CellAddress, Entry | undefined][] = [];
        for (const range of intent.plan.targets) {
            for (let row = range.topRow; row < range.topRow + range.rowCount; row++) {
                for (let column = range.leftColumn; column < range.leftColumn + range.columnCount; column++) {
                    const at = { row, column };
                    const field = intent.valueFor(at);
                    const origin = intent.originFor(at);
                    const address = new CellAddress(row, column);
                    const number = exactNumberFor(field, origin);
                    if (number !== undefined) {
                        exact.push([address, Entry.fromValue(Value.fromNumber(number))]);
                        continue;
                    }
                    if (isTooNarrowToShow(field, origin)) {
                        intent.refuse();
                        this.#tell(this.#words.pasteTooNarrowToShow(field, address));
                        return;
                    }
                    const text = typedFor(field, origin, sheet.culture);
                    if (text === undefined) {
                        intent.refuse();
                        this.#tell(this.#words.pasteReadsDifferently(field, sheet.culture));
                        return;
                    }
                    typed.push([address, text]);
                }
            }
        }
        // The typed fields and the exact numbers are one engine edit: one step on the undo stack (ADR-0048). Its
        // undo selects the ranges pasted over (ADR-0331 §3).
        this.#performRefusing(intent, () => SheetEdit.pasteFields(typed, exact), intent.plan.targets.map(rangeOf));
    };

    readonly #onClear = (intent: GridClearIntent): void => {
        if (intent.rowSequenceVersion !== this.#source.snapshot().rowSequenceVersion) return;
        // Only the cells that hold an Entry are named, so a Delete over whole columns costs what they hold, not a
        // million cells each (ADR-0054); formats stay, as Excel's Delete leaves them.
        const cleared: [CellAddress, undefined][] = [];
        for (const address of this.#source.sheet.entryAddresses) {
            if (intent.targets.some(target => contains(target, address))) cleared.push([address, undefined]);
        }
        if (cleared.length === 0) return;
        try {
            this.#perform(SheetEdit.setEntries(cleared), intent.targets.map(rangeOf));
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the engine's refusal and an unreadable Formula are answers told to the user; anything else is rethrown
            if (!(error instanceof SheetRefusedError)) throw error;
            this.#tell(this.#words.refused(error.refusal));
        }
    };

    readonly #onUndo = (): void => { this.undo(); };

    readonly #onRedo = (): void => { this.redo(); };

    readonly #onColumnWidthChanged = (change: GridColumnWidthChange): void => {
        const column = CellAddress.tryParseColumn(change.column);
        if (column === undefined) throw new Error(`Internal: '${change.column}' is not one of the Sheet's columns.`);
        const characters = charactersOfColumn(change.widthPx, this.#columns.metrics);
        if (characters === undefined) return;
        // The width is recorded as the user's, so an entry never widens the column again (ADR-0046).
        const sheet = this.#source.sheet;
        const step = this.#workbook.do(sheet.name, SheetEdit.setColumnWidth(CellRange.wholeColumns(column, column), characters));
        const resizing = this.#resizing;
        if (resizing !== undefined && resizing.pending.delete(column) && this.#history.tryExtend(resizing.first, step)) {
            if (resizing.pending.size === 0) this.#resizing = undefined;
        } else {
            this.#history.record([step], sheet);
            // The whole columns the grid resizes with this one, from the Selection it chose them by (FN-12c).
            const resized = this.#selection === undefined ? [] : this.#selection.wholeColumns({ rowCount: Sheet.rowCount, columnCount: Sheet.columnCount });
            this.#resizing = resized.includes(column) && resized.length > 1 ? { first: step, pending: new Set(resized.filter(c => c !== column)) } : undefined;
        }
        this.#changed([step.change]);
    };

    // What was typed into the Name Box, read as a place: a range of the Sheet shown, or of the Sheet named before it,
    // which is shown with the place selected (ADR-0051; ADR-0331 §5).
    readonly #onNameBoxEntered = (typed: string): void => {
        const place = readNameBoxPlace(typed);
        const sheet = place === undefined ? undefined : place.sheet === undefined ? this.#source.sheet : this.#workbook.find(place.sheet);
        if (place === undefined || sheet === undefined) {
            this.#tell(this.#words.addressUnreadable(typed));
            return;
        }
        this.#clearNotice();
        if (sheet !== this.#source.sheet) {
            this.#showSheet(sheet);
            this.#raiseDocument();
        }
        this.#select(place.range);
    };

    // Places the Selection on `range`, its Focus on the first cell, against the rows shown now (ADR-0050, item 4).
    #select(range: CellRange): void {
        const first = range.first;
        this.#grid?.placeSelection(
            { topRow: first.row, leftColumn: first.column, rowCount: range.rowCount, columnCount: range.columnCount },
            { row: first.row, column: first.column }, this.#source.snapshot().rowSequenceVersion);
    }

    readonly #onSelectionChanged = (selection: GridSelection): void => {
        this.#selection = selection;
        // A Selection the grid dropped, as another Sheet is shown, is no move of the user's: the notice stands.
        if (selection.isEmpty) return;
        // The user moved on: what the notice said is behind them, unless it was told by the gesture that moved.
        if (this.#noticeOutlivesMove) {
            this.#noticeOutlivesMove = false;
            return;
        }
        this.#clearNotice();
    };

    readonly #onRefused = (refusal: GridRefusal): void => {
        switch (refusal.kind) {
            case 'paste': this.#tell(this.#words.pasteRefused(refusal.reason), { saidByGrid: true }); return;
            case 'copy': this.#tell(this.#words.copyRefused(refusal.reason), { saidByGrid: true }); return;
            case 'commit':
                this.#tell(this.#words.commitRefused(new CellAddress(refusal.cell.row, refusal.cell.column).toString(), refusal.reason), { saidByGrid: true });
                return;
            // A Sheet offers no Find yet, so Ctrl+F opens nothing (ADR-0055); it has no Action Column (ADR-0037) and no
            // Mark Column (ADR-0043) to refuse.
            case 'find':
            case 'action':
            case 'mark': return;
            default: unreachable(refusal);
        }
    };

    readonly #onEditingChanged = (open: boolean): void => {
        this.#editing = open;
    };

    readonly #onEditDiscarded = (reason: EditDiscardReason): void => {
        // A discard at the commit is the commit gesture's, and stands through the move that completes it.
        this.#tell(this.#words.editDiscarded(reason), { outlivesMove: reason === 'rowLeftTheWindow' || reason === 'columnNoLongerEditable' });
    };

    readonly #onOverwriteNotice = (notice: GridOverwriteNotice): void => {
        const address = new CellAddress(notice.cell.row, notice.cell.column).toString();
        this.#tell(this.#words.overwritten(address, notice.seenText, notice.replacedText), { outlivesMove: true });
    };

    // ---- Operations ------------------------------------------------------------------------------------------

    /** `edit` on the Sheet shown, as one operation on the undo stack; its undo selects `selects` where given. The
     * columns a number entered over `widenOver` no longer fits are widened in the same operation, so one undo puts
     * back both (ADR-0048; ADR-0071, case 17). */
    #perform(edit: SheetEdit, selects?: readonly CellRange[], widenOver?: readonly CellRange[]): void {
        this.#resizing = undefined;
        const sheet = this.#source.sheet;
        const steps: WorkbookStep[] = [this.#workbook.do(sheet.name, edit)];
        if (widenOver !== undefined) {
            for (const [column, characters] of this.#columns.widenedBy(sheet, widenOver)) {
                steps.push(this.#workbook.do(sheet.name, SheetEdit.setAutomaticColumnWidth(CellRange.wholeColumns(column, column), characters)));
            }
        }
        this.#history.record(steps, sheet, selects);
        this.#changed(steps.map(step => step.change));
    }

    /** `edit` checked before anything is written: a refusal is told to the grid, which leaves the Selection where
     * it was, and to the user (ADR-0050 item 5). */
    #performChecked(intent: GridPasteIntent, edit: SheetEdit): void {
        const refusal = this.#workbook.check(this.#source.sheet.name, edit);
        if (refusal !== undefined) {
            intent.refuse();
            this.#tell(this.#words.refused(refusal));
            return;
        }
        this.#perform(edit);
    }

    /** The edit `make` builds, done; one the engine refuses, or a Formula it cannot read, is told to the grid
     * and to the user, and nothing is written. */
    #performRefusing(intent: GridPasteIntent, make: () => SheetEdit, selects?: readonly CellRange[]): void {
        try {
            this.#perform(make(), selects);
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the engine's refusal and an unreadable Formula are answers told to the user; anything else is rethrown
            if (error instanceof FormulaSyntaxError) {
                intent.refuse();
                this.#tell(this.#words.formulaUnreadable(error));
                return;
            }
            if (error instanceof SheetRefusedError) {
                intent.refuse();
                this.#tell(this.#words.refused(error.refusal));
                return;
            }
            throw error;
        }
    }

    /** An undo or a redo: the Sheet it names is shown, and what it changed is painted (ADR-0331 §3). */
    #stepped(stepped: SheetOperationStepped): void {
        const target = stepped.operation.shownAfter(this.#workbook);
        if (target === this.#source.sheet) {
            this.#changed(stepped.changes);
        } else {
            this.#showSheet(target);
            this.#raiseDocument();
        }
        // Excel selects what the step changed (ADR-0048), on the Sheet it changed it on.
        const selects = target === stepped.operation.from ? stepped.operation.selects() : undefined;
        if (selects !== undefined) this.#select(selects);
    }

    /** What one operation changed across the Workbook: the Sheet shown paints what it changed there. */
    #changed(changes: readonly WorkbookChange[]): void {
        const sheet = this.#source.sheet;
        if (changes.some(change => change.changesSheets) && !this.#workbook.sheets.includes(sheet)) {
            this.#showSheet(this.#workbook.shown);
            this.#raiseDocument();
            return;
        }
        this.#sheetChanged(changes.map(change => change.for(sheet)));
        this.#raiseDocument();
    }

    #sheetChanged(changes: readonly SheetChange[]): void {
        this.#clearNotice();
        let options = false;
        // A whole row's or column's Cell Format reaches cells that hold nothing, which the engine names no row of:
        // every painted row is read again, and the grid repaints those whose look changed (ADR-0071).
        if (changes.some(change => change.reformatsUnnamedRows)) {
            this.#appearance = appearanceLookup(++this.#reading);
            options = true;
        }
        // The widths the Sheet records follow the change, its undo and redo included (ADR-0046).
        const widths = [...new Set(changes.flatMap(change => change.columns))];
        if (widths.length > 0 && this.#columns.read(this.#source.sheet, widths)) options = true;
        if (options) this.#grid?.update(this.#gridOptions());
        this.#source.changed(changes);
    }

    #showSheet(sheet: Sheet): void {
        // The Workbook records the Sheet shown: its document reopens on it, and its volatile functions recalculate
        // there (ADR-0331).
        this.#workbook.shown = sheet;
        this.#resizing = undefined;
        this.#columns.read(sheet);
        this.#appearance = appearanceLookup(++this.#reading);
        this.#source.show(sheet);
        this.#grid?.update(this.#gridOptions());
    }

    #raiseDocument(): void {
        const { onWorkbookChanged } = this.#options;
        if (onWorkbookChanged === undefined) return;
        const raised = this.#workbook.toDocument();
        this.#raised.add(raised);
        onWorkbookChanged(raised);
    }

    #open(options: SheetOptions): Workbook {
        if (options.document !== undefined) return Workbook.open(options.document);
        return new Workbook(Culture.of(options.locale ?? navigator.language));
    }

    // ---- The notice ------------------------------------------------------------------------------------------

    /** Says `sentence` in the notice. One the grid has already said in its own live region is shown but not said
     * again (ADR-0203). */
    #tell(sentence: string, { outlivesMove = false, saidByGrid = false }: { readonly outlivesMove?: boolean; readonly saidByGrid?: boolean } = {}): void {
        this.#noticeOutlivesMove = outlivesMove;
        if (saidByGrid) {
            const span = document.createElement('span');
            span.setAttribute('aria-hidden', 'true');
            span.textContent = sentence;
            this.#notice.replaceChildren(span);
            return;
        }
        this.#notice.textContent = sentence;
    }

    #clearNotice(): void {
        this.#noticeOutlivesMove = false;
        this.#notice.replaceChildren();
    }

    #alive(): void {
        if (this.#destroyed) throw new Error('This Sheet was destroyed.');
    }

    static #checked(options: SheetOptions): SheetOptions {
        // Checked as an untyped caller may hand them, where they arrive (CONVENTIONS.md).
        const given: unknown = options;
        if (typeof given !== 'object' || given === null) throw new TypeError("A Sheet's options are an object.");
        const { locale, onWorkbookChanged }: { readonly locale?: unknown; readonly onWorkbookChanged?: unknown } = options;
        if (locale !== undefined && typeof locale !== 'string') throw new TypeError("A Sheet's locale is a language tag, such as 'en-US'.");
        if (onWorkbookChanged !== undefined && typeof onWorkbookChanged !== 'function') throw new TypeError('onWorkbookChanged is a function.');
        return options;
    }
}

// The Name Box names the Focus as A1 does (ADR-0051).
const nameBoxLabel = (focus: CellPosition): string => new CellAddress(focus.row, focus.column).toString();

/** The cell the first pasted field ExSheet marked refused was copied from, in the order of the copied block, or
 * `undefined` for a paste that holds none. */
function firstRefusedIn(intent: GridPasteIntent): CellAddress | undefined {
    const source = intent.plan.source;
    for (const range of intent.plan.targets) {
        // Every field of the block lands in each target range, from its top-left (ADR-0014).
        for (let row = 0; row < Math.min(source.rows, range.rowCount); row++) {
            for (let column = 0; column < Math.min(source.columns, range.columnCount); column++) {
                const from = refusedIn(intent.markFor({ row: range.topRow + row, column: range.leftColumn + column }));
                if (from !== undefined) return from;
            }
        }
    }
    return undefined;
}

function rangeOf(range: SelectionRange): CellRange {
    return new CellRange(
        new CellAddress(range.topRow, range.leftColumn),
        new CellAddress(range.topRow + range.rowCount - 1, range.leftColumn + range.columnCount - 1));
}

function firstTarget(intent: GridPasteIntent): SelectionRange {
    const [first] = intent.plan.targets;
    if (first === undefined) throw new Error('Internal: a paste names at least one target.');
    return first;
}

function contains(range: SelectionRange, address: CellAddress): boolean {
    return address.row >= range.topRow && address.row < range.topRow + range.rowCount
        && address.column >= range.leftColumn && address.column < range.leftColumn + range.columnCount;
}
