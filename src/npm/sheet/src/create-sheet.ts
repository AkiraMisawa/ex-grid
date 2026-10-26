// ExSheet drawn into an element (ADR-0370, section 1): one ExGrid as the Sheet's Consumer (ADR-0046), and the
// Sheet's notice beneath it, its live region. The Sheet holds and computes; the grid paints, selects, navigates and
// reports. Each intent the grid raises becomes one engine step on the one undo stack (ADR-0048), and each change
// the engine reports hands the grid a new instance for the rows it names and nothing else (ADR-0003).
//
// What is deliberately not wired: the grid's sort and filter, since a Sheet's rows are places (ADR-0046); the
// header click selects the column instead (ADR-0050 item 1), and the column menu is hidden, as Excel's Headings
// carry none (ADR-0050 item 12).

import {
    createGrid, formulaBarHeight, refusedCells, replacedCells, type CellPosition, type GridDirection,
    type GridClearIntent, type GridCopyAnswer, type GridCopyRequest, type GridColumn, type GridColumnWidthChange, type GridDensity, type GridEditIntent, type GridFindPattern, type GridHandle, type GridOptions,
    type GridReplaceRequest, type GridReplaceResult,
    type GridOverwriteNotice, type GridPasteIntent, type GridRefusal, type GridSelection, type GridViewRead, type EditDiscardReason, type SelectionRange,
    type ViewportSize,
} from '@ex-grid/core';
import {
    CellAddress, CellFormatChange, CellRange, Culture, Entry, FormulaSyntaxError, SheetEdit, SheetRefusedError, Value, Workbook,
    Sheet, SheetRefusal, type CellFormat, type FillDirection, type HorizontalAlignment, type SheetChange, type SheetDocument, type WorkbookChange, type WorkbookDocument, type WorkbookStep,
} from '@ex-grid/sheet-engine';
import { unreachable } from './must';
import { exactNumberFor, isTooNarrowToShow, refusedIn, typedFor } from './sheet-clipboard';
import { rangeOf, sheetCopyAnswer, type SheetOwnCopy } from './sheet-copy';
import { appearanceLookup, charactersOfColumn, SheetColumns, sheetCellPaint } from './sheet-columns';
import { SheetHistory, type SheetOperationStepped } from './sheet-history';
import { SheetRow } from './sheet-row';
import { planSheetReplace, positionsOf, sheetCellMatches } from './sheet-find';
import { SheetSource } from './sheet-source';
import { readNameBoxPlace } from './name-box-text';
import { SheetEdges } from './sheet-edges';
import { SheetWords, type SheetLabel, type SheetToolbarCommand } from './sheet-words';
import { SheetTabs } from './sheet-tabs';
import { SheetToolbar, type SheetToolbarIcon, type SheetToolbarItem, type SheetToolbarPaint } from './sheet-toolbar';
import { sheetTabKeys, sheetTabStepOf } from './sheet-tab-keys';
import { sheetFormatChangeFor, sheetFormatKeyOf, type SheetFormatKey } from './sheet-format-keys';
import { sheetStructureEditFor, sheetStructureKeyOf, type SheetStructureKey } from './sheet-structure-keys';
import { widensAsAHomeTabButtonDoes, widensAsAKeyDoes, widensAsFormatCellsOkDoes } from './sheet-widening';
import { FormatCellsDraft } from './format-cells-draft';
import type { FormatCellsTab } from './format-cells-offer';
import { FormatCellsPanel } from './format-cells-panel';

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
    /** Shows the Sheet Tabs at the Sheet's foot, one per Sheet of the Workbook, as Excel does (ADR-0331); shown unless
     * `false`. Their band, as tall as the Formula Bar, comes off `viewportHeight`. */
    readonly showSheetTabs?: boolean;
    /** Shows the Sheet Toolbar above the Formula Bar (ADR-0100): Excel's Home tab formatting commands, acting on this
     * Sheet's Selection. Off unless `true`. Its band, a row as tall as the Formula Bar, comes off `viewportHeight`. */
    readonly showToolbar?: boolean;
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
    readonly #toolbar: SheetToolbar;
    readonly #tabs: SheetTabs;
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
    // The copy last put on the clipboard, whose Entries a paste of exactly it carries (ADR-0048), and the sentence a
    // copy of the Sheet's own was last refused with (ADR-0049).
    #ownCopy: SheetOwnCopy | undefined;
    #copyRefusal: string | undefined;
    // Format Cells while it stands in the grid's popover, and the Sheet, the Selection and the ranges it was opened
    // over; and the tab it last showed, which the next opening shows again (ADR-0071). The grid closing the popover
    // itself, by Escape or a press elsewhere, tells the Sheet nothing: the record stands, its frame gone, until the
    // next opening or move drops it, and nothing reads it as open meanwhile, as only the frame's OK sets anything.
    #formatCells: {
        readonly panel: FormatCellsPanel; readonly draft: FormatCellsDraft; readonly sheet: Sheet; readonly selection: GridSelection;
        readonly ranges: readonly CellRange[];
    } | undefined;
    #formatCellsTab: FormatCellsTab = 'number';
    // A resize of several whole columns, reported a column at a time: its first step, and the columns still to
    // come, which join that step on the undo stack (FN-12c, ADR-0048).
    #resizing: { readonly first: WorkbookStep; readonly pending: Set<number> } | undefined;
    // Where Ctrl+arrow stops on the Sheet shown: its filled cells, indexed on the first Ctrl+arrow and kept in step
    // with every change after (ADR-0050, item 2).
    #edges: SheetEdges | undefined;
    #destroyed = false;
    // Each Sheet's view as it was left: its Selection and scroll offsets, given back when its tab is selected
    // again (ADR-0331 §1). A Sheet not yet shown has none, and opens on A1.
    readonly #views = new WeakMap<Sheet, GridViewRead>();
    // The style properties the Sheet set on its element (#frame), each with the value the Consumer had given it.
    readonly #framed = new Map<string, { readonly value: string; readonly priority: string }>();
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
        // Everything a refusal could come from is asked before the element is touched, so a refused Sheet leaves it as
        // it was.
        const gridOptions = this.#gridOptions();

        element.classList.add('ex-sheet');
        element.setAttribute('data-ex-input-root', '');
        this.#gridElement = document.createElement('div');
        this.#notice = document.createElement('div');
        this.#notice.className = 'ex-sheet-notice';
        this.#notice.setAttribute('role', 'status');
        this.#tabs = new SheetTabs(document, { pressed: this.#onTabPressed, added: this.#onTabAdded, listKey: this.#onTabListKey });
        this.#toolbar = new SheetToolbar(document, { giveBack: () => { this.#grid?.focus(); } });
        element.replaceChildren(this.#gridElement, this.#notice);
        // Excel's keys for the Sheet Tabs, heard on the Sheet before its grid hears them, wherever in it the keyboard
        // is, so the browser does not move among its own tabs (ADR-0331 §4).
        element.addEventListener('keydown', this.#onSheetKey, { capture: true });
        this.#grid = createGrid(this.#gridElement, gridOptions);
        this.#paintFrame();
    }

    get workbook(): Workbook { return this.#workbook; }

    update(options: SheetOptions): void {
        this.#alive();
        const next = SheetView.#checked(options);
        // A height too short for the bands is refused before anything changes, so the Sheet keeps the options it had.
        this.#gridHeight(next);
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
        this.#paintFrame();
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
        this.#element.removeEventListener('keydown', this.#onSheetKey, { capture: true });
        this.#element.replaceChildren();
        this.#element.classList.remove('ex-sheet');
        this.#element.removeAttribute('data-ex-input-root');
        this.#frame({});
    }

    // ---- The grid ---------------------------------------------------------------------------------------------

    #gridOptions(): GridOptions<SheetRow> {
        const { rowHeight, viewportWidth, density } = this.#options;
        const viewportHeight = this.#gridHeight(this.#options);
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
            dataEdge: this.#dataEdge,
            hideColumnMenu: true,
            hideCopyWithHeaders: true,
            // Find matches the text a cell is shown with, by the Sheet's rule; the column letters are addresses,
            // not content, and Excel's Find never stops on them (ADR-0332 §4, ADR-0333 §2).
            findMatch: sheetFindMatch,
            findsHeaders: false,
            onReplace: this.#onReplace,
            pasteMaySpill: true,
            onEdit: this.#onEdit,
            copyAnswer: this.#copyAnswer,
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
            // Refused, so the grid holds the edit with the reason, or a Replace names the cell, rather than taking a
            // write that did not happen as made. The column's verdict rejects an unreadable Formula before the intent
            // is raised (ADR-0034); this is the net under it.
            if (error instanceof FormulaSyntaxError) {
                intent.refuse(this.#words.formulaUnreadable(error));
                return;
            }
            if (error instanceof SheetRefusedError) {
                intent.refuse(this.#words.refused(error.refusal));
                return;
            }
            throw error;
        }
    };

    // A copy (ADR-0050 item 9, ADR-0048): the engine copies the range, so the clipboard carries the Values exactly as
    // the engine gives them however far the range runs beyond the Window, and a copy reaching #GETTING_DATA is
    // refused in the engine's words (ADR-0049). The Entries of a one-range copy are kept, so that a paste of exactly
    // this block back into the Sheet carries them.
    readonly #copyAnswer = (request: GridCopyRequest): GridCopyAnswer => {
        const { answer, own } = sheetCopyAnswer(this.#source.sheet, request, refusal => this.#words.refused(refusal));
        if (answer.kind === 'refuse') {
            // The clipboard still holds the last copy, so the Entries kept for it still stand.
            this.#copyRefusal = answer.sentence;
            return answer;
        }
        this.#ownCopy = own;
        return answer;
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
        // This Sheet's own copy pasted back (ADR-0048): its Entries, formats and alignment, relative References
        // shifted by the distance pasted, over each target range the grid approved, as one step on the undo stack.
        const own = this.#ownCopy;
        if (own?.isPastedAs(intent.values, intent.origins) === true) {
            this.#performRefusing(intent, () => SheetEdit.paste(own.block, intent.plan.targets.map(rangeOf)), intent.plan.targets.map(rangeOf));
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

    /**
     * The grid's Replace Request (ADR-0332 §3): Replace All over the Sheet shown, inside the Selection when more than
     * one cell is selected, matched on each cell's Entry. All or nothing: a replaced Formula the engine cannot read
     * refuses it all, naming every such cell. Otherwise one step on the undo stack, as typing the replaced Entries
     * would be, which widens the columns a number no longer fits, and the count.
     */
    readonly #onReplace = (request: GridReplaceRequest): GridReplaceResult => {
        const version = this.#source.snapshot().rowSequenceVersion;
        // A Sheet's rows are places, so a request on another version names nothing here (ADR-0046): answered under the
        // version shown, which the grid refuses as the order having moved, and nothing is written.
        if (request.rowSequenceVersion !== version) return replacedCells(0, version);
        const sheet = this.#source.sheet;
        const positions = positionsOf(request.columns);
        const plan = planSheetReplace(sheet, request, request.replacement, positions, request.scope, (address, text) => {
            const verdict = new SheetRow(sheet, address.row).judge(text, error => this.#words.formulaUnreadable(error));
            return verdict.kind === 'reject' ? verdict.message : undefined;
        });
        const cellAt = (address: CellAddress, position: number, message: string): { readonly row: number; readonly column: string; readonly message: string } =>
            ({ row: address.row, column: request.columns[position] ?? unreachableColumn(position), message });
        if (plan.refused.length > 0) return refusedCells(plan.refused.map(cell => cellAt(cell.address, cell.position, cell.reason)), version);
        if (plan.writes.length === 0) return replacedCells(0, version);
        const written = plan.writes.map(write => new CellRange(write.address));
        try {
            this.#perform(SheetEdit.enterMany(plan.writes.map(write => [write.address, write.text] as const)), undefined, written);
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the engine's refusal and an unreadable Formula are answers told to the user; anything else is rethrown
            const message = error instanceof FormulaSyntaxError ? this.#words.formulaUnreadable(error)
                : error instanceof SheetRefusedError ? this.#words.refused(error.refusal) : undefined;
            if (message === undefined) throw error;
            return refusedCells(plan.writes.map(write => cellAt(write.address, positions.get(write.address.column) ?? unreachableColumn(write.address.column), message)), version);
        }
        return replacedCells(plan.writes.length, version);
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

    readonly #dataEdge = (from: CellPosition, direction: GridDirection): CellPosition => {
        const sheet = this.#source.sheet;
        this.#edges ??= SheetEdges.of(sheet);
        const at = this.#edges.find(new CellAddress(from.row, from.column), direction);
        return { row: at.row, column: at.column };
    };

    // Places the Selection on `range`, its Focus on the first cell, against the rows shown now (ADR-0050, item 4).
    #select(range: CellRange): void {
        const first = range.first;
        this.#grid?.placeSelection(
            { topRow: first.row, leftColumn: first.column, rowCount: range.rowCount, columnCount: range.columnCount },
            { row: first.row, column: first.column }, this.#source.snapshot().rowSequenceVersion);
    }

    readonly #onSelectionChanged = (selection: GridSelection): void => {
        const before = this.#selection?.focus;
        this.#selection = selection;
        // The Toolbar shows the Focus cell's state: a Selection grown or shrunk around the same Focus changes none of it.
        const focus = selection.focus;
        if (before === undefined || focus === undefined || before.row !== focus.row || before.column !== focus.column) this.#paintToolbar();
        // Format Cells shows the Selection it opened over and sets what OK names on it: a Selection moved under it,
        // by the Name Box or a Consumer, ends it. The notice of the Selection it opened over, which the grid gives after
        // its paint, is no move.
        if (this.#formatCells?.selection.equals(selection) === false) this.#endFormatCells();
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
            // A refusal of the Sheet's own is in the engine's words, which the grid relayed from the copy answer.
            case 'copy': {
                const own = this.#copyRefusal;
                this.#copyRefusal = undefined;
                this.#tell(refusal.reason === 'refusedByConsumer' && own !== undefined ? own : this.#words.copyRefused(refusal.reason), { saidByGrid: true });
                return;
            }
            case 'commit':
                this.#tell(this.#words.commitRefused(new CellAddress(refusal.cell.row, refusal.cell.column).toString(), refusal.reason), { saidByGrid: true });
                return;
            case 'placement': this.#tell(this.#words.placementRefused(refusal.reason), { saidByGrid: true }); return;
            // The find panel says its own outcomes (ADR-0332 §5); a Sheet has no Action Column (ADR-0037) and no Mark
            // Column (ADR-0043) to refuse.
            case 'find':
            case 'action':
            case 'mark': return;
            default: unreachable(refusal);
        }
    };

    readonly #onEditingChanged = (open: boolean): void => {
        this.#editing = open;
        this.#paintToolbar();
    };

    readonly #onEditDiscarded = (reason: EditDiscardReason): void => {
        // A discard at the commit is the commit gesture's, and stands through the move that completes it.
        this.#tell(this.#words.editDiscarded(reason), { outlivesMove: reason === 'rowLeftTheWindow' || reason === 'columnNoLongerEditable' });
    };

    readonly #onOverwriteNotice = (notice: GridOverwriteNotice): void => {
        const address = new CellAddress(notice.cell.row, notice.cell.column).toString();
        this.#tell(this.#words.overwritten(address, notice.seenText, notice.replacedText), { outlivesMove: true });
    };

    // ---- The Sheet Tabs (ADR-0331 §4) ------------------------------------------------------------------------

    // A band of the Sheet's own, a Toolbar Row or the tabs: the Formula Bar's height, so they read as bands of one
    // frame, as the C# Sheet draws them.
    get #bandRowPx(): number {
        return SheetView.#bandRowPxOf(this.#options);
    }

    static #bandRowPxOf(options: SheetOptions): number {
        return formulaBarHeight(options.density ?? 'compact', options.rowHeight); // oxlint-disable-line ex-grid/no-stand-in-default -- the grid's own default density (ADR-0028)
    }

    get #tabsHeightPx(): number {
        return SheetView.#tabsHeightOf(this.#options);
    }

    static #tabsHeightOf(options: SheetOptions): number {
        return options.showSheetTabs === false ? 0 : SheetView.#bandRowPxOf(options);
    }

    // The Sheet's box, its toolbar and its tabs. With neither band the element lays out as if they were not there.
    // With either, it holds the grid and its bands: at the declared width, or under a stretched height as a column as
    // tall as its parent, in which the grid takes what the bands leave.
    #paintFrame(): void {
        const tabs = this.#options.showSheetTabs !== false;
        const toolbar = this.#options.showToolbar === true;
        if (!tabs && this.#tabs.area.isConnected) this.#tabs.area.remove();
        if (!toolbar && this.#toolbar.band.isConnected) {
            // A band taken away while it holds the keyboard hands it back first, so the next key is not the page's.
            const holds = this.#toolbar.holdsKeyboard;
            this.#toolbar.band.remove();
            if (holds) this.#grid?.focus();
        }
        if (!tabs && !toolbar) {
            this.#frame({});
            return;
        }
        const { viewportWidth, viewportHeight } = this.#options;
        const width = typeof viewportWidth === 'number' ? { width: `${String(viewportWidth)}px` } : { 'min-width': '0' };
        this.#frame(viewportHeight === 'stretch'
            ? { display: 'flex', 'flex-direction': 'column', height: '100%', flex: '1 1 auto', 'min-height': '0', ...width }
            : { display: 'block', ...width });
        if (toolbar) {
            if (!this.#toolbar.band.isConnected) this.#gridElement.before(this.#toolbar.band);
            this.#paintToolbar();
        }
        if (tabs) {
            if (!this.#tabs.area.isConnected) this.#gridElement.after(this.#tabs.area);
            this.#tabs.paint({
                sheets: this.#workbook.sheets, shown: this.#source.sheet, heightPx: this.#tabsHeightPx,
                listName: this.#words.sheetTabsName, addName: this.#words.newSheetName,
            });
        }
    }

    // The properties of the element's style the Sheet sets, and only those: one it stops setting gets back the value
    // the Consumer had given it, so what the Consumer set there stays theirs.
    #frame(properties: Readonly<Record<string, string>>): void {
        const style = this.#element.style;
        for (const [name, given] of this.#framed) {
            if (name in properties) continue;
            this.#framed.delete(name);
            if (given.value === '') style.removeProperty(name);
            else style.setProperty(name, given.value, given.priority);
        }
        for (const [name, value] of Object.entries(properties)) {
            if (!this.#framed.has(name)) this.#framed.set(name, { value: style.getPropertyValue(name), priority: style.getPropertyPriority(name) });
            if (style.getPropertyValue(name) !== value) style.setProperty(name, value);
        }
    }

    // The grid's height under `options`: the Sheet's, less its bands. A Sheet too short to hold them is refused by name.
    #gridHeight(options: SheetOptions): ViewportSize | undefined {
        const height = options.viewportHeight;
        if (typeof height !== 'number') return height;
        const tabs = SheetView.#tabsHeightOf(options);
        const toolbar = options.showToolbar === true ? SheetToolbar.heightPx(toolbarRowCount, SheetView.#bandRowPxOf(options)) : 0;
        if (height <= tabs + toolbar) {
            const bands = [
                ...(toolbar > 0 ? [`its Sheet Toolbar's ${String(toolbar)}px band (ADR-0100)`] : []),
                ...(tabs > 0 ? [`its Sheet Tabs' ${String(tabs)}px band (ADR-0331)`] : []),
            ];
            throw new RangeError(`A Sheet of ${String(height)}px cannot hold ${bands.join(', ')} and a grid.`);
        }
        return height - tabs - toolbar;
    }

    // ---- The Sheet Toolbar (ADR-0100) ------------------------------------------------------------------------

    #paintToolbar(): void {
        if (this.#options.showToolbar !== true) return;
        const rows = this.#toolbarRows();
        if (rows.length !== toolbarRowCount) throw new Error(`Internal: the Toolbar draws ${String(rows.length)} rows, and its band is laid out for ${String(toolbarRowCount)}.`);
        this.#toolbar.paint({ name: this.#words.toolbarName, rowHeightPx: this.#bandRowPx, rows });
    }

    // The default row, Excel's Home tab's commands in its order (ADR-0100), each item's state the Focus cell's as the
    // grid holds it now. While an edit is open every item is unavailable, and says why. The colour, Borders and Number
    // Format lists follow.
    #toolbarRows(): SheetToolbarPaint['rows'] {
        const focus = this.#grid?.readView().selection.focus;
        const format = focus === undefined ? undefined : this.#source.sheet.getCellFormat(new CellAddress(focus.row, focus.column));
        const words = this.#words;
        const item = (key: SheetToolbarCommand, icon: SheetToolbarIcon, act: ((focused: CellFormat) => CellFormatChange) | 'formatCells' | undefined,
            { pressed, shortcut, disabledReason }: { readonly pressed?: boolean; readonly shortcut?: string; readonly disabledReason?: string } = {}): SheetToolbarItem => {
            const name = words.toolbarItem(key);
            return {
                key, kind: pressed === undefined ? 'button' : 'toggle', name, icon, pressed: pressed === true,
                tooltip: shortcut === undefined ? name : words.toolbarItemWithShortcut(name, shortcut),
                // An open edit is the reason over any other, as the C# Sheet's.
                disabledReason: this.#editing ? words.toolbarWhileEditing : disabledReason,
                press: act === undefined ? undefined : () => { this.#toolbarPressed(act); },
            };
        };
        const key = (kind: SheetFormatKey) => (focused: CellFormat): CellFormatChange => sheetFormatChangeFor(kind, focused, this.#source.sheet.culture);
        return [{
            label: words.toolbarHomeRow,
            items: [
                item('bold', 'bold', key('bold'), { pressed: format?.font.bold === true, shortcut: 'Ctrl+B' }),
                item('italic', 'italic', key('italic'), { pressed: format?.font.italic === true, shortcut: 'Ctrl+I' }),
                item('underline', 'underline', key('underline'), { pressed: format?.font.underline === true, shortcut: 'Ctrl+U' }),
                item('strikethrough', 'strikethrough', key('strikethrough'), { pressed: format?.font.strikethrough === true, shortcut: 'Ctrl+5' }),
                'separator',
                item('alignLeft', 'alignLeft', align('left'), { pressed: format?.alignment === 'left' }),
                item('alignCenter', 'alignCenter', align('center'), { pressed: format?.alignment === 'center' }),
                item('alignRight', 'alignRight', align('right'), { pressed: format?.alignment === 'right' }),
                'separator',
                item('percent', 'percent', key('percent'), { shortcut: 'Ctrl+Shift+%' }),
                // Excel's Comma Style pads with `*`, as Accounting does, which ExSheet does not read: shown, and disabled
                // with the reason, rather than set as something else under Excel's name.
                item('comma', 'comma', undefined, { disabledReason: words.commaStyleUnread }),
                'separator',
                item('formatCells', 'formatCells', 'formatCells', { shortcut: 'Ctrl+1' }),
            ],
        }];
    }

    // A Toolbar Item pressed: its change of the Focus cell's format on the Selection, widening as Excel's Home tab
    // buttons do, or Format Cells… opened. An edit open at the press, whatever the last pass showed, changes nothing,
    // and the notice says why.
    #toolbarPressed(act: ((focused: CellFormat) => CellFormatChange) | 'formatCells'): void {
        if (this.#editing) {
            this.#tell(this.#words.toolbarWhileEditing);
            return;
        }
        if (act === 'formatCells') this.#openFormatCells();
        else this.#formatFromFocus(act, widensAsAHomeTabButtonDoes);
    }

    // A press on a tab shows its Sheet; the keyboard stays in the grid, where the next key acts on it.
    readonly #onTabPressed = (sheet: Sheet): void => {
        if (!this.#workbook.sheets.includes(sheet) || this.#refusedSwitch()) return;
        this.#clearNotice();
        if (sheet !== this.#source.sheet) {
            this.#showSheet(sheet);
            this.#raiseDocument();
        }
        // Pressed while the keyboard is on the tab list, it moves with the selection, as the list's keys move it.
        if (this.#tabs.holdsKeyboard) this.#tabs.focus(sheet);
    };

    // + adds a Sheet after the one shown, as Excel's button does.
    readonly #onTabAdded = (): void => { this.#addSheet(false); };

    // The keys on the tab list, where Tab put the keyboard: ← and → select the tab before or after, Home and End the
    // first and the last, the keyboard staying on the tab selected; Escape or Enter gives it back to the grid.
    readonly #onTabListKey = (event: KeyboardEvent): boolean => {
        if (event.ctrlKey || event.altKey || event.metaKey || event.shiftKey) return false;
        const sheets = this.#workbook.sheets;
        const at = sheets.indexOf(this.#source.sheet);
        switch (event.key) {
            case 'ArrowLeft':
            case 'ArrowRight':
            case 'Home':
            case 'End': {
                if (this.#refusedSwitch()) return true;
                const to = event.key === 'Home' ? 0 : event.key === 'End' ? sheets.length - 1 : at + (event.key === 'ArrowLeft' ? -1 : 1);
                const sheet = sheets[to];
                if (sheet === undefined) return true;
                this.#clearNotice();
                if (sheet !== this.#source.sheet) {
                    this.#showSheet(sheet);
                    this.#raiseDocument();
                }
                this.#tabs.focus(sheet);
                return true;
            }
            case 'Escape':
            case 'Enter':
                this.#grid?.focus();
                return true;
            default:
                return false;
        }
    };

    // Ctrl+PageUp and Ctrl+PageDown show the Sheet before or after the one shown, never past the first or the last;
    // Shift+F11 adds a Sheet before it. Claimed from the browser wherever in the Sheet the keyboard is.
    readonly #onSheetKey = (event: KeyboardEvent): void => {
        const grid = this.#grid;
        if (event.isComposing || grid === undefined) return;
        const key = grid.keyName(event);
        const step = sheetTabStepOf(key);
        if (step === undefined && key !== sheetTabKeys.insert) {
            this.#onCellKey(event, key);
            return;
        }
        event.preventDefault();
        event.stopPropagation();
        if (step === undefined) {
            this.#addSheet(true);
            return;
        }
        if (this.#refusedSwitch()) return;
        const sheets = this.#workbook.sheets;
        const sheet = sheets[sheets.indexOf(this.#source.sheet) + step];
        if (sheet === undefined) return;
        this.#clearNotice();
        const onTabs = this.#tabs.holdsKeyboard;
        this.#showSheet(sheet);
        this.#raiseDocument();
        if (onTabs) this.#tabs.focus(sheet);
    };

    // Excel's formatting keys and its insert and delete keys, on the Selection as the grid holds it at the key (ADR-0071,
    // "Keys"; ADR-0050 item 14), where the grid takes keys: not in the Name Box or a panel the grid opened, whose
    // fields keep their own. Ctrl+1 waits on Format Cells.
    #onCellKey(event: KeyboardEvent, key: string): void {
        const format = sheetFormatKeyOf(key);
        const structure = format === undefined ? sheetStructureKeyOf(key) : undefined;
        if (format === undefined && structure === undefined) return;
        const target = event.target;
        // Ctrl+1 inside Format Cells opens nothing more, and is not the browser's, whose Ctrl+1 leaves the page.
        if (format === 'formatCells' && target instanceof Element && target.closest('.ex-format-cells') !== null) {
            event.preventDefault();
            return;
        }
        // On the toolbar's band too: its tooltips name these keys, and the band leaves Ctrl's keys to the Sheet.
        const onToolbar = target === this.#toolbar.band;
        if (!(target instanceof Element) || (!onToolbar && (!this.#gridElement.contains(target) || target.closest('.ex-popover, .ex-name-box') !== null))) return;
        event.preventDefault();
        event.stopPropagation();
        if (format !== undefined) this.#formatKey(format);
        else if (structure !== undefined) this.#structureKey(structure);
    }

    // A formatting key sets its change on every range of the Selection as one step, its toggle's direction the Focus
    // cell's (case 17), widening the columns a number no longer fits where the key widens (ADR-0071). While an edit
    // is open it changes nothing, and the notice says why.
    #formatKey(kind: SheetFormatKey): void {
        if (this.#editing) {
            this.#tell(this.#words.formatKeyWhileEditing);
            return;
        }
        // Ctrl+1 opens Format Cells and sets nothing itself (ADR-0071, case 22).
        if (kind === 'formatCells') {
            this.#openFormatCells();
            return;
        }
        this.#formatFromFocus(focus => sheetFormatChangeFor(kind, focus, this.#source.sheet.culture), widensAsAKeyDoes);
    }

    // The change `changeOf` makes of the Focus cell's format, set on every range of the Selection as one step, widening
    // the columns a number no longer fits where `widens` says the gesture does (ADR-0071, ADR-0100).
    #formatFromFocus(changeOf: (focus: CellFormat) => CellFormatChange, widens: (change: CellFormatChange) => boolean): void {
        const selection = this.#grid?.readView().selection;
        const focus = selection?.focus;
        if (selection === undefined || focus === undefined) return;
        const change = changeOf(this.#source.sheet.getCellFormat(new CellAddress(focus.row, focus.column)));
        const cells = selection.ranges.map(rangeOf);
        this.#perform(SheetEdit.setCellFormat(cells, change), undefined, widens(change) ? cells : undefined);
    }

    // Format Cells over the Selection the grid holds, in the grid's popover frame (ADR-0071, ADR-0050 item 16), opening
    // on the Focus cell's Cell Format and on the tab it last showed. Opened while it stands, it opens afresh.
    #openFormatCells(): void {
        const grid = this.#grid;
        const selection = grid?.readView().selection;
        const focus = selection?.focus;
        if (grid === undefined || selection === undefined || focus === undefined) return;
        const ranges = selection.ranges.map(rangeOf);
        const draft = FormatCellsDraft.open(this.#source.sheet, new CellAddress(focus.row, focus.column), ranges);
        const panel = new FormatCellsPanel(this.#element.ownerDocument, draft, this.#words, this.#formatCellsTab, {
            ok: () => this.#formatCellsOk(panel),
            cancel: () => { if (this.#formatCells?.panel === panel) this.#endFormatCells(); },
            tabShown: tab => { this.#formatCellsTab = tab; },
        });
        this.#endFormatCells();
        this.#clearNotice();
        const sheet = this.#source.sheet;
        if (grid.openPopover(() => panel.element, this.#words.formatCellsTitle)) this.#formatCells = { panel, draft, sheet, selection, ranges };
    }

    // Format Cells' OK (ADR-0071, SH-45): the parts the user touched, set on the ranges it was opened over as one undo
    // step, widening the columns whose numbers no longer fit where a Number Format or any part of the Font is among
    // them; with nothing touched nothing is set and no step recorded. A draft holding a refusal sets nothing and
    // stays open, for the panel to show why. It is taken against the Sheet it was opened over: shown another, which
    // ends it, an OK that still arrives sets nothing (the spine's sixth rule).
    #formatCellsOk(panel: FormatCellsPanel): boolean {
        const standing = this.#formatCells;
        if (standing?.panel !== panel) return true;
        if (standing.draft.refusal !== undefined) return false;
        const change = standing.draft.change;
        this.#endFormatCells();
        if (change.isEmpty || standing.sheet !== this.#source.sheet) return true;
        if (this.#editing) {
            this.#tell(this.#words.formatKeyWhileEditing);
            return true;
        }
        this.#perform(SheetEdit.setCellFormat(standing.ranges, change), undefined, widensAsFormatCellsOkDoes(change) ? standing.ranges : undefined);
        return true;
    }

    // Ends Format Cells where it stands: the grid's popover closes and gives the keyboard back. One the grid already
    // closed, by Escape or a press elsewhere, has left its frame, and only the record goes.
    #endFormatCells(): void {
        const standing = this.#formatCells;
        if (standing === undefined) return;
        this.#formatCells = undefined;
        // Its frame is in the grid's element, attached to the page or not, until the grid closes it.
        if (this.#gridElement.contains(standing.panel.element)) this.#grid?.closePopover();
    }

    // Ctrl+Plus and Ctrl+Minus: over whole rows they insert or delete those rows, over whole columns those columns,
    // each one step; over any other Selection, and while an edit is open, nothing changes and the notice says why
    // (ADR-0050 item 14).
    #structureKey(kind: SheetStructureKey): void {
        const insert = kind === 'insert';
        if (this.#editing) {
            this.#tell(this.#words.structureKeyWhileEditing(insert));
            return;
        }
        const selection = this.#grid?.readView().selection;
        if (selection === undefined || selection.isEmpty) return;
        const edit = sheetStructureEditFor(kind, selection, { rowCount: Sheet.rowCount, columnCount: Sheet.columnCount });
        if (edit === undefined) {
            this.#tell(this.#words.structureKeyNeedsWholeRowsOrColumns(insert));
            return;
        }
        try {
            this.#perform(edit);
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the engine's refusal is an answer told to the user; anything else is rethrown
            if (!(error instanceof SheetRefusedError)) throw error;
            this.#tell(this.#words.refused(error.refusal));
        }
    }

    // While an edit is open no other Sheet is shown, and the notice says why: the edit is typed on this one. Showing
    // another to point at its cells, as Excel does for a Formula, waits on Pointing across Sheets (ADR-0331 §5).
    #refusedSwitch(): boolean {
        if (!this.#editing) return false;
        this.#tell(this.#words.sheetSwitchWhileEditing);
        return true;
    }

    // A new, empty Sheet beside the one shown, named as Excel names it, and shown, as Excel shows the Sheet it adds:
    // one step on the undo stack, whose undo shows the Sheet it was added from (ADR-0331 §3).
    #addSheet(before: boolean): void {
        if (this.#editing) {
            this.#tell(this.#words.sheetOperationWhileEditing);
            return;
        }
        const from = this.#source.sheet;
        this.#resizing = undefined;
        const onTabs = this.#tabs.holdsKeyboard;
        const step = this.#workbook.do(from.name, SheetEdit.insertSheet(undefined, before));
        this.#history.record([step], from);
        this.#showSheet(step.sheet);
        this.#raiseDocument();
        // The tabs moved to make room lose the keyboard: one on the list goes to the new Sheet's tab, selected now.
        if (onTabs) this.#tabs.focus(step.sheet);
        this.#tell(this.#words.sheetAdded(step.sheet.name), { outlivesMove: true });
    }

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
        // Format Cells read the cells it was opened over: an operation under it, an undo or a Toolbar press, ends it,
        // so its OK never sets a draft read from formats or places that no longer stand (the spine's sixth rule).
        this.#endFormatCells();
        const sheet = this.#source.sheet;
        if (changes.some(change => change.changesSheets) && !this.#workbook.sheets.includes(sheet)) {
            this.#showSheet(this.#workbook.shown);
            this.#raiseDocument();
            return;
        }
        this.#sheetChanged(changes.map(change => change.for(sheet)));
        if (changes.some(change => change.changesSheets)) this.#paintFrame();
        else this.#paintToolbar();
        this.#raiseDocument();
    }

    #sheetChanged(changes: readonly SheetChange[]): void {
        this.#clearNotice();
        const edges = this.#edges;
        if (edges !== undefined) for (const change of changes) edges.update(this.#source.sheet, change);
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
        // Format Cells read the Sheet it was opened over: another shown, or this one opened again, ends it.
        this.#endFormatCells();
        const grid = this.#grid;
        const leaving = this.#source.sheet;
        if (grid !== undefined && leaving !== sheet) this.#views.set(leaving, grid.readView());
        // The Workbook records the Sheet shown: its document reopens on it, and its volatile functions recalculate
        // there (ADR-0331).
        this.#workbook.shown = sheet;
        this.#resizing = undefined;
        this.#edges = undefined;
        this.#columns.read(sheet);
        this.#appearance = appearanceLookup(++this.#reading);
        this.#source.show(sheet);
        // The view it was left with comes with its rows, under the version they are shown with.
        const view = this.#views.get(sheet);
        if (grid !== undefined && view !== undefined) grid.showViewWith(view, this.#source.snapshot().rowSequenceVersion);
        grid?.update(this.#gridOptions());
        this.#paintFrame();
        // A Sheet shown for the first time opens on A1, scrolled to it, as Excel's does.
        if (view === undefined) this.#select(new CellRange(new CellAddress(0, 0)));
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

// The Match Mark's rule (ADR-0332 §4): the one a step finds by, so a mark is never a match no step reaches.
const sheetFindMatch = (row: SheetRow, column: GridColumn<SheetRow>, pattern: GridFindPattern): boolean => {
    const at = CellAddress.tryParseColumn(column.name);
    if (at === undefined) throw new Error(`Internal: '${column.name}' is not one of the Sheet's columns.`);
    return sheetCellMatches(row.sheet, new CellAddress(row.index, at), pattern, row.at(at)?.text);
};

function unreachableColumn(position: number): never {
    throw new Error(`Internal: no column of the request stands at ${String(position)}.`);
}

// An alignment item's change: the alignment, or General where the Focus cell has it, as Excel's buttons do (ADR-0100).
const align = (alignment: HorizontalAlignment) => (focused: CellFormat): CellFormatChange =>
    new CellFormatChange({ alignment: focused.alignment === alignment ? 'general' : alignment });

// The Toolbar's rows: Excel's Home tab, one row. The grid's height is laid out from it without drawing the rows.
const toolbarRowCount = 1;

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

function firstTarget(intent: GridPasteIntent): SelectionRange {
    const [first] = intent.plan.targets;
    if (first === undefined) throw new Error('Internal: a paste names at least one target.');
    return first;
}

function contains(range: SelectionRange, address: CellAddress): boolean {
    return address.row >= range.topRow && address.row < range.topRow + range.rowCount
        && address.column >= range.leftColumn && address.column < range.leftColumn + range.columnCount;
}
