// ExSheet drawn into an element (ADR-0370, section 1): one ExGrid as the Sheet's Consumer (ADR-0046), and the
// Sheet's notice beneath it, its live region. The Sheet holds and computes; the grid paints, selects, navigates and
// reports. Each intent the grid raises becomes one engine step on the one undo stack (ADR-0048), and each change
// the engine reports hands the grid a new instance for the rows it names and nothing else (ADR-0003).
//
// What is deliberately not wired: the grid's sort and filter, since a Sheet's rows are places (ADR-0046); the
// header click selects the column instead (ADR-0050 item 1), and the column menu is hidden, as Excel's Headings
// carry none (ADR-0050 item 12).

import {
    createGrid, formulaBarHeight, gridChromeSlotNames, refusedCells, summaryFigureOrder, replacedCells, type CellPosition, type GridChrome, type GridDirection,
    type GridClearIntent, type GridCopyAnswer, type GridCopyRequest, type GridColumn, type GridColumnWidthChange, type GridDensity, type GridEditIntent, type GridFindPattern, type GridHandle, type GridOptions,
    type GridReplaceRequest, type GridReplaceResult, type GridFindRequest, type GridFindResult, type GridFindPanelSlotContext, type FindOutcome, type FindPanelRefusedCell, type GridDeclaredKeyPress, findOutcomeText,
    type GridFillIntent, type GridOverwriteNotice, type GridPasteIntent, type GridRefusal, type GridSelection, type GridViewRead, type EditDiscardReason, type SelectionRange,
    type ViewportSize, type GridCommand, type GridContextMenuContext, type GridEditorReference, type ReferenceKeyColour, type GridPointArrow, type GridPointState, type EditorCompletion,
    type SelectionSummary, type SummaryFigure,
} from '@ex-grid/core';
import {
    BorderChange, BorderLine, CellAddress, CellFill, CellFormatChange, CellRange, Culture, Entry, NumberFormat, FormulaSyntaxError, RepeatedKeyError, SheetEdit, SheetRefusedError, Value, Workbook,
    Sheet, SheetRefusal, WorkbookDocument, referenceText, type LinkedTable, type CellColour, type SheetDay, type CellFormat, type FillDirection, type HorizontalAlignment, type SheetChange, type SheetDocument, type SheetRefusalReason, type WorkbookChange, type WorkbookStep,
} from '@ex-grid/sheet-engine';
import { elementAt, unreachable } from './must';
import { browserTimeZone, checkTimeZone, dayOf, momentIn, sameDay, untilTheDayIsReadAgainMs, untilTheMinuteTurnsMs } from './sheet-moment';
import { exactNumberFor, isTooNarrowToShow, refusedIn, typedFor } from './sheet-clipboard';
import { rangeOf, sheetCopyAnswer, type SheetOwnCopy } from './sheet-copy';
import { appearanceLookup, charactersOfColumn, SheetColumns, sheetCellPaint } from './sheet-columns';
import { SheetHistory, type SheetOperationStepped } from './sheet-history';
import { SheetRow } from './sheet-row';
import { planSheetReplace, planWorkbookReplace, positionsOf, sheetCellMatches, sheetFindStep, sheetFindStepWorkbook } from './sheet-find';
import { sheetFindPanelSlot, type SheetFindWithin } from './sheet-find-panel';
import { SheetSource } from './sheet-source';
import { readNameBoxPlace } from './name-box-text';
import { SheetEdges } from './sheet-edges';
import { SheetWords, type SheetLabel, type SheetTabCommand, type SheetToolbarBorder, type SheetToolbarCommand } from './sheet-words';
import { SheetTabs } from './sheet-tabs';
import { SheetToolbar, type SheetToolbarIcon, type SheetToolbarItem, type SheetToolbarPaint } from './sheet-toolbar';
import type { SheetToolbarContent, SheetToolbarItemSlot } from './sheet-toolbar-slot';
import { mountedSlot, type SheetFormatCellsSlot, type SheetFormatCellsSlotContext, type SheetTabAddSlot, type SheetTabSlot } from './sheet-chrome-slots';
import { SheetChoicesPanel, type SheetToolbarChoice } from './sheet-choices';
import { automaticColour, colourFromRgb, paintOf } from './cell-colour';
import {
    defaultCodeOf, recogniseNumberFormat,
} from './number-format-codes';
import { sheetTabKeys, sheetTabStepOf } from './sheet-tab-keys';
import { sheetFormatChangeFor, sheetFormatKeyOf, sheetFormatKeysDeclared, type SheetFormatKey } from './sheet-format-keys';
import { sheetContextCommands, sheetCommandWord } from './sheet-context-commands';
import { sheetStructureEditFor, sheetStructureKeyOf, sheetStructureKeysDeclared, type SheetStructureKey } from './sheet-structure-keys';
import { widensAsAHomeTabButtonDoes, widensAsAKeyDoes, widensAsFormatCellsOkDoes } from './sheet-widening';
import { FormatCellsDraft } from './format-cells-draft';
import { formatCellsOffer, isDisabledCategory, type FormatCellsTab, type NumberFormatCategory } from './format-cells-offer';
import { FormatCellsPanel } from './format-cells-panel';
import { linkedColumnsOf, sameLinkedColumns, sheetCompletion, sheetCycleReference, sheetPointAt, sheetReferences, sheetReferenceTextOn } from './sheet-formula-aids';
import { PointingScope, type LinkedColumnColour, type PointingSheet } from './pointing-scope';
import type { PointingRefusal } from './pointing-refusal';

/**
 * A Consumer's own action on the Sheet Toolbar (ADR-0100), a Toolbar Item like ExSheet's: it stands in a row of its own
 * or among the formatting items, and while an edit is open it cannot be pressed. It carries a KeyTip only where one is
 * given.
 */
export interface SheetToolbarButton {
    /** Its name: the text it shows, its accessible name and its tooltip. */
    readonly text: string;
    /** What pressing it does. A press from the keys gives the keyboard back to the Sheet first, so what the press opens
     * may take it. */
    readonly onPress: () => void;
    /** Its KeyTip letters among its row's items. */
    readonly keyTip?: string | undefined;
    /** Why it cannot be pressed now, shown in its tooltip; `undefined` where it can. */
    readonly disabledReason?: string | undefined;
    /** Its own content, such as an icon, drawn in place of its text, which still names it. Hand the same function on
     * every update: a new one draws the content again. */
    readonly content?: SheetToolbarContent | undefined;
}

/** One element of a Toolbar Row: one of ExSheet's own items, named by its command and carrying Excel's KeyTip; a
 * separator between groups; or the Consumer's own button. */
export type SheetToolbarEntry = SheetToolbarCommand | 'separator' | SheetToolbarButton;

/** A Toolbar Row of the Consumer's (ADR-0100): one band of the Sheet Toolbar, as tall as the Formula Bar. */
export interface SheetToolbarRowOptions {
    /** The row's accessible name. */
    readonly label: string;
    /** The row's KeyTip letters, as a ribbon tab's; a row without one is reached by Tab and the arrows only. */
    readonly keyTip?: string | undefined;
    /** What the row holds, in order. */
    readonly items: readonly SheetToolbarEntry[];
}

/**
 * The Sheet's Chrome slots (ADR-0390), the C# `ISheetChrome`'s, each a mount function the Sheet calls with the slot's
 * element and what the slot is handed. A slot not given is drawn by the built-in Chrome. As `ISheetChrome` extends
 * `IGridChrome`, the grid's own slots are among them, and the Sheet hands those to its grid: the Cell Editor, the
 * Formula Bar's fields, the completion list, the find panel, the Context Menu, a cell's message and the Selection
 * Summary. The column menu and the filter panel are taken too, and never shown, since a Sheet's columns have no menu.
 */
export interface SheetChrome extends GridChrome<unknown> {
    /** Each Toolbar Item of the Sheet Toolbar (ADR-0100): a mount that answers `undefined` leaves the item to the
     * built-in Chrome, as the C# Chrome's `ToolbarItem` answering null does. */
    readonly toolbarItem?: SheetToolbarItemSlot;
    /** Format Cells' contents (ADR-0071), in the grid's popover frame. */
    readonly formatCells?: SheetFormatCellsSlot;
    /** Each Sheet Tab's face (ADR-0331 §4). */
    readonly sheetTab?: SheetTabSlot;
    /** The face of the button that adds a Sheet (ADR-0331 §4). */
    readonly sheetTabAdd?: SheetTabAddSlot;
}

/** The Sheet's own slots, beside the grid's. */
const sheetSlotNames: readonly string[] = ['toolbarItem', 'formatCells', 'sheetTab', 'sheetTabAdd'];

/** What a Sheet is drawn with. */
export interface SheetOptions {
    /** The Workbook or Sheet to open; a new Workbook of one empty Sheet unless given. A new document object opens
     * it in place of what is shown, with nothing to undo, and `undefined` given in place of one opens an empty Sheet
     * in `locale`; one `onWorkbookChanged` handed out, handed back as a controlled component's state is, is the
     * Sheet's own and changes nothing. */
    readonly document?: SheetDocument | WorkbookDocument | undefined;
    /** The culture of a Sheet started empty: how typed constants are read and Values shown (ADR-0048). A document
     * carries its own. The browser's language unless given; one the engine holds no data for is refused by name. */
    readonly locale?: string;
    /** Shows the Formula Bar and its Name Box above the columns (ADR-0051); shown unless `false`. */
    readonly showFormulaBar?: boolean;
    /** How many leading columns are pinned — the Sheet's frozen panes, columns only (ADR-0046). */
    readonly pinnedColumnCount?: number;
    /** Shows the Row Headings, the row numbers, beside the rows (ADR-0050); shown unless `false`. The Sheet still
     * addresses `A1` without them. */
    readonly showRowHeadings?: boolean;
    /** Shows the Column Headings, the column letters, above the rows (ADR-0050); shown unless `false`. The Sheet
     * still addresses `A1` without them. */
    readonly showColumnHeadings?: boolean;
    /** Shows the Sheet Tabs at the Sheet's foot, one per Sheet of the Workbook, as Excel does (ADR-0331); shown unless
     * `false`. Their band, as tall as the Formula Bar, comes off `viewportHeight`. */
    readonly showSheetTabs?: boolean;
    /** Shows the Sheet Toolbar above the Formula Bar (ADR-0100): Excel's Home tab formatting commands, acting on this
     * Sheet's Selection. Off unless `true`. Its band, a row as tall as the Formula Bar, comes off `viewportHeight`. */
    readonly showToolbar?: boolean;
    /**
     * The Sheet Toolbar's rows, top to bottom (ADR-0100). `'default'` is ExSheet's own row, Excel's Home tab's commands,
     * whose KeyTip is H; a row of the Consumer's holds ExSheet's items by their commands, separators and the Consumer's
     * own buttons, in the order given. Without it the toolbar is the default row alone. Every row is as tall as the
     * Formula Bar and comes off `viewportHeight`; what a row holds is fitted to it and clipped. KeyTips that would be
     * taken for one another at one level, two rows' or two items' of one row, are refused.
     */
    readonly toolbar?: readonly (SheetToolbarRowOptions | 'default')[] | undefined;
    /**
     * Hears the other value of `showToolbar` when the user asks to hide or show the Sheet Toolbar with Ctrl+F1, Excel's
     * key for the ribbon (ADR-0100). The Sheet holds nothing: whether the toolbar is shown is View State, the
     * Consumer's, so handing the value back as `showToolbar` is what lets the key work. Without it, Ctrl+F1 is not
     * claimed and stays the browser's.
     */
    readonly onShowToolbarChanged?: (shown: boolean) => void;
    /** The parts of the Sheet a Chrome of the Consumer's, or a Wrapper's, draws in place of the built-in ones (ADR-0390). */
    readonly chrome?: SheetChrome;
    /** ExSheet's Japanese words in place of its English (ADR-0370, section 10), chosen in one line, never by the
     * locale. */
    readonly japanese?: boolean;
    /** The Consumer's word for any id, which wins over ExSheet's own (ADR-0370, section 10). */
    readonly label?: SheetLabel;
    /**
     * The Consumer's word for each of the grid's own ids, its commands and its refusals (ADR-0036, ADR-0203), handed to
     * the Sheet's grid as its `commandLabel`: `undefined` keeps the grid's English, and the empty string silences the
     * announcement of a refusal. The Sheet's own ids are worded through `label`.
     */
    readonly commandLabel?: (id: string) => string | undefined;
    /** Shows the Selection Summary under the Sheet, as Excel's status bar does (ADR-0130); shown unless `false`.
     * `onSelectionSummaryChanged` still hears it while hidden. */
    readonly showSelectionSummary?: boolean;
    /** The Selection Summary's figures, Excel's Average, Count and Sum unless given (ADR-0130). The Sheet holds the
     * choice made in the figures menu from then on, and tells it through `onSummaryFiguresChanged`; figures handed here
     * that differ from the ones given before replace it. */
    readonly summaryFigures?: readonly SummaryFigure[];
    /** Hears the figures the user chose in the figures menu, a right-click on the summary (ADR-0130). The Sheet already
     * shows them. */
    readonly onSummaryFiguresChanged?: (figures: readonly SummaryFigure[]) => void;
    /** Hears the Selection Summary as it now stands (ADR-0130), for an application that shows the figures elsewhere. */
    readonly onSelectionSummaryChanged?: (summary: SelectionSummary) => void;
    /**
     * Offers copy with headers in the Context Menu and by Ctrl+Shift+C, which copies the Values with the column letters
     * as the first row (ADR-0048, ADR-0005). Off unless `true`: a Sheet's column letters are addresses, not headers, and
     * Excel has no such command (ADR-0050 item 13), so the key is left to the page.
     */
    readonly allowCopyWithHeaders?: boolean;
    /**
     * Hears that one of Excel's formatting keys was pressed while an edit is open (ADR-0071): the key changed nothing,
     * and the Sheet's notice says why. In that state Excel formats the selected characters, and a Cell Format is per
     * cell. The key is still claimed, so the browser's own meaning does not run either: Ctrl+U opens no page source.
     */
    readonly onFormatKeyRefused?: (refusal: SheetRefusal) => void;
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
    /**
     * Hears a Sheet added, deleted, renamed, moved or copied — by a command, by the user, or by an undo or redo of
     * one — after `onWorkbookChanged`, with the Sheets' names before and after (ADR-0331 §6). Showing another Sheet
     * changes no Sheet, and is not heard; neither is a new document opened.
     */
    readonly onSheetsChanged?: (change: SheetsChange) => void;
    /**
     * Hears whether an edit is open, each time that changes: `true` when a cell or the Formula Bar is typed in, or
     * opened with F2 or a double click, `false` when the edit is committed, cancelled or discarded (ADR-0048). While
     * one is open the commands that change the Sheet are refused, so this is where an application greys out its own
     * buttons for them, as Excel greys out its ribbon.
     */
    readonly onEditingChanged?: (open: boolean) => void;
    /**
     * Hears each Linked Table column the Formula being edited reads, and the colour its References wear in the
     * editor (ADR-0057, ADR-0049): each column once, in order of first appearance, under the table's and the
     * column's names as the Consumer declared them. Told once whenever that list changes and never when it does
     * not, and with an empty list when the edit ends. A table or column not declared names nothing and is not
     * told. The Sheet does not know where, or whether, the table is on screen, and outlines none of it itself: the
     * Consumer hands each entry to the grid that shows the table as one of its `outlinedColumns`, the grid column
     * that shows `column` with the same `colour`, and only it can vouch that that grid shows the rows the Formula
     * reads.
     */
    readonly onLinkedColumnColoursChanged?: (columns: readonly LinkedColumnColour[]) => void;
    /**
     * The Pointing Scope this Sheet belongs to (ADR-0058), or none. While this Sheet holds the keyboard, has an edit
     * open and is in Point, its caret standing where a Reference can go or what Point wrote there still standing, the
     * grids registered in the Scope are pointed at: a press on a cell writes
     * `XLOOKUP("R-4471", Positions[Id], Positions[PV])`, reading the cell by its row's key, where Point writes, and a
     * press on a column header `Positions[PV]`, in the table's column names as this Sheet declares them. A further press, on a registered grid or on this Sheet, replaces what this Point
     * wrote. A press that stands for anything else writes nothing, and is told through `onPointingRefused`. When the
     * keyboard leaves this Sheet, no grid is pointed at, and the edit stands.
     *
     * After such a press F4 changes nothing, and the arrow keys point inside the grid pressed: ↑ and ↓ one row further
     * in its current order, ← and → to the next column its table has, rewriting what this Point wrote, and the grid
     * scrolls the cell into view. After a press on a column header, ↓ points at the column's first row, and ← and → at
     * the next column its table has, as a column; ↑ moves nothing. The Scope also outlines, in the grids registered
     * for their table, the Linked Table columns the Formula edited here reads, and dashes the cell or column a press
     * wrote for until Point over what it wrote ends.
     */
    readonly pointingScope?: PointingScope | undefined;
    /**
     * Hears that a press on a grid of `pointingScope`, or an arrow key after one, wrote nothing into the Formula being
     * edited, and why (ADR-0058): more than one cell or column, a Header Group, a column the table does not have, a
     * table declared without a key, a blank key, a row not yet arrived. A drag that reached another cell took back
     * what its press wrote. The Formula's text is as it was before the press or the key. The Sheet shows nothing of
     * it itself: show the refusal's `message` where the page tells its user things.
     */
    readonly onPointingRefused?: (refusal: PointingRefusal) => void;
    /** Hears the grid's Selection each time it changes, as ExGrid reports it (ADR-0011/0014): what a status line
     * counts. The formatting commands do not wait for it: they act on the Selection the grid holds when they run. */
    readonly onSelectionChanged?: (selection: GridSelection) => void;
    /**
     * A fixed Sheet Seed (ADR-0180): what `RAND()` and `RANDBETWEEN` draw from, with the Sheet, the cell, the call's
     * place in its Formula and the count of changes since the document was opened — for a sheet that must show the
     * same draws whenever it is opened, such as a simulation someone else reviews. The draws still change at each
     * change of the Workbook, and never on the clock. Unless given, the Sheet chooses a seed whenever it opens a
     * document, so a reopened sheet draws again, as Excel's reopened workbook does. An unsigned 64-bit integer; the
     * seed is not recorded in the document.
     */
    readonly seed?: bigint;
    /**
     * A fixed Sheet Day: what `TODAY()` answers whatever the clock says (ADR-0121), for a report that must show the same
     * day whenever it is opened. It goes before `timeZone`. Unless given, the day is the clock's in the zone, read again
     * at midnight there, and at least once an hour.
     */
    readonly today?: SheetDay | undefined;
    /**
     * The IANA time zone whose day `TODAY()` and whose time of day `NOW()` answer (ADR-0121, ADR-0124), such as head
     * office's for a site that keeps one business day for every user. The browser's zone unless given, as Excel takes the
     * day on the user's own device; one the browser does not know is refused by name. A fixed `today` goes before it for
     * `TODAY()`, not for `NOW()`, which moves on as each minute turns.
     */
    readonly timeZone?: string | undefined;
}

/** The Sheets' names before and after a change to the Sheets (ADR-0331 §6), in the Workbook's order. */
export interface SheetsChange {
    /** The names as they were. */
    readonly before: readonly string[];
    /** The names as they are now. */
    readonly after: readonly string[];
    /** The name of the Sheet shown after the change. */
    readonly shown: string;
}

/**
 * The Focus cell's Cell Format, as `SheetHandle.readFocusFormat` read it: a change is made from it away from the Sheet,
 * and set with `SheetHandle.setCellFormatFrom`, which knows what it was read against (ADR-0100).
 */
export interface FocusFormatReading {
    /** The Focus cell's Cell Format as it showed. */
    readonly format: CellFormat;
}

/** A Sheet drawn into an element, handed back by {@link createSheet}. */
export interface SheetHandle {
    /** The Workbook the Sheet edits. Change it only through the Sheet, or what is shown falls behind it. */
    readonly workbook: Workbook;
    /** Takes the options again, whole. */
    update(options: SheetOptions): void;
    /** Whether there is a step to undo. */
    readonly canUndo: boolean;
    /** Whether there is an undone step to redo. */
    readonly canRedo: boolean;
    /**
     * Whether an edit is open in the grid, from the moment a cell or the Formula Bar is typed in until the edit is
     * committed, cancelled or discarded (ADR-0048). While it is, `do`, `undo`, `redo`, `setCellFormat` and
     * `openFormatCells` are refused with a `SheetRefusedError`; `cellFormatAt` is a read, and answers.
     */
    readonly isEditing: boolean;
    /**
     * A Consumer command: does `edit` as one operation on the user's undo stack (ADR-0048), and raises
     * `onWorkbookChanged`. It names no Sheet, so it is taken only while the Workbook holds one; with several it is
     * refused, and the Sheet is named with the other form (ADR-0331 §6). It does the edit exactly as it is written, and
     * widens no column.
     *
     * @throws SheetRefusedError The Sheet refuses the edit, an edit is open, or the Workbook holds several Sheets;
     * nothing changed.
     * @throws FormulaSyntaxError A typed Formula cannot be read; nothing changed.
     */
    do(edit: SheetEdit): void;
    /**
     * A Consumer command: does `edit` on the Sheet named `sheet`, shown or not, as one operation on the user's undo
     * stack (ADR-0048, ADR-0331 §6). A Sheet operation — `insertSheet`, `rename`, `moveSheet`, `copySheet`,
     * `deleteSheet` — acts on that Sheet and raises `onSheetsChanged`; the Sheet shown stays shown unless it is the one
     * deleted, and then the Sheet that takes its place is.
     *
     * @throws RangeError No Sheet of the Workbook is named `sheet`; nothing changed.
     * @throws SheetRefusedError The Sheet refuses the edit, or an edit is open; nothing changed.
     * @throws FormulaSyntaxError A typed Formula cannot be read; nothing changed.
     */
    do(sheet: string, edit: SheetEdit): void;
    /**
     * Sets the parts of a Cell Format that `change` names on the Selection the grid holds now, as one operation on the
     * undo stack (ADR-0071, ADR-0048): every part it does not name stays as each cell has it. A Selection of whole
     * columns or rows sets it on the columns or rows, and the change's Borders are relative to each range. It widens
     * as the formatting keys do: a Number Format, underline or strikethrough widens each column whose numbers it no
     * longer fits and the user has not sized, in the same operation. Answers false when nothing is selected.
     *
     * @throws RangeError The change sets no part of a Cell Format.
     * @throws SheetRefusedError An edit is open; nothing changed.
     */
    setCellFormat(change: CellFormatChange): boolean;
    /**
     * Sets on the Selection a change made from the Focus cell's Cell Format as it shows now, as a toggle key reads it
     * (ADR-0071, ADR-0100): Bold on over a Focus cell that is not bold, off over one that is. It widens as Excel's Home
     * tab buttons do: as the formatting keys do, and for a Font colour too. Otherwise it acts, refuses and answers as
     * `setCellFormat`; false when nothing is selected, and then `changeOf` is not called.
     *
     * @throws RangeError The change it makes sets no part of a Cell Format.
     * @throws SheetRefusedError An edit is open; nothing changed.
     */
    setCellFormatFromFocus(changeOf: (focus: CellFormat) => CellFormatChange): boolean;
    /**
     * The Focus cell's Cell Format as it shows now, read for a change made from it away from the Sheet, over a round
     * trip, and set with `setCellFormatFrom` (ADR-0100); undefined when nothing is selected. It is read for a change,
     * so it is refused while an edit is open, before the change is made.
     *
     * @throws SheetRefusedError An edit is open.
     */
    readFocusFormat(): FocusFormatReading | undefined;
    /**
     * Sets `change`, made from `reading`, on the Selection, as `setCellFormatFromFocus` sets the change it makes: only
     * while the Selection, the Sheet shown and the Focus cell's Cell Format are what `reading` was read from.
     * Otherwise the change would be set on cells it was not made for, or undo what it was made to do, so it is refused,
     * and the notice says so (ADR-0100, decided 2026-10-28). A reading is taken by its set, whatever comes of it.
     *
     * @throws TypeError `reading` was not read from this Sheet, or was set already.
     * @throws RangeError The change sets no part of a Cell Format.
     * @throws SheetRefusedError An edit is open; the Selection or the Sheet shown moved (`selectionMoved`); the Focus
     * cell's Cell Format changed (`focusFormatChanged`). Nothing changed.
     */
    setCellFormatFrom(reading: FocusFormatReading, change: CellFormatChange): boolean;
    /**
     * Opens Format Cells over the Selection the grid holds now, from the Consumer's own button (ADR-0071): on the Focus
     * cell's Cell Format, and on the tab shown last. OK sets the parts the user touched as one undo step; Cancel or
     * Escape sets nothing. Opening it again while it stands opens it afresh. Answers false when nothing is selected.
     *
     * @throws SheetRefusedError An edit is open; nothing opened.
     */
    openFormatCells(): boolean;
    /**
     * The Cell Format of the cell at `address` as it shows (ADR-0071): each part the cell's own, else its row's, else
     * its column's, else the default. It names no Sheet, so it is read only while the Workbook holds one; with several,
     * name the Sheet with the other form (ADR-0331 §6).
     *
     * @throws SheetRefusedError The Workbook holds several Sheets.
     */
    cellFormatAt(address: CellAddress): CellFormat;
    /**
     * The Cell Format of the cell at `address` on the Sheet named `sheet`, shown or not, as it shows.
     *
     * @throws RangeError No Sheet of the Workbook is named `sheet`.
     */
    cellFormatAt(sheet: string, address: CellAddress): CellFormat;
    /** Undoes the latest operation; false when there is none, which the notice says. */
    undo(): boolean;
    /** Redoes the latest operation undone; false when there is none, which the notice says. */
    redo(): boolean;
    /**
     * Declares a Linked Table Formulas read by name, as `ExSheet.DeclareLinkedTableAsync` does (ADR-0049): its readers
     * show `#GETTING_DATA` until a snapshot is pushed. The declaration is recorded in the document, so
     * `onWorkbookChanged` hears it when it changes what was declared; declaring the same table again changes nothing.
     * It is the Consumer's data, not a user's operation, so it is no step on the undo stack. `key` names the column, or
     * the columns, a row is read by.
     */
    declareLinkedTable(name: string, columns: readonly string[], key?: string | readonly string[]): void;
    /**
     * Replaces a Linked Table's rows with a whole snapshot, in one step, as `ExSheet.PushLinkedTableAsync` does
     * (ADR-0049): only the Formulas that read it, and what depends on them, recalculate, and only the rows whose
     * Values changed repaint. The document never holds a table's rows, so `onWorkbookChanged` hears nothing, and
     * nothing goes on the undo stack. A snapshot of a keyed table in which a key repeats is refused with the engine's
     * `RepeatedKeyError`, after the table's readers show `#GETTING_DATA` again.
     */
    pushLinkedTable(name: string, rows: Iterable<readonly (Value | undefined)[]>): void;
    /**
     * Replaces the Workbook with the Workbook Document `read` answers, taken at this call's place in the Input Order
     * (ADR-0330 §10), as `ExSheet.OpenAsync` does: the inputs made before it are taken against the Workbook they were
     * made on, and those made after it wait until the document arrives and are then taken against the new one, or
     * refused aloud when what they were taken against no longer holds (ADR-0142). Call it from the handler that hears
     * the user's choice of a file, with the read still to do, such as
     * `signal => openXlsxWorkbook(file, 'en-US', { signal }).then(read => read.document)` from `@ex-grid/sheet-xlsx`.
     *
     * In its turn, an edit still open refuses it by name, as a save is refused, and nothing is read: what is being typed
     * would be thrown away. From this call until the opening ends the Sheet's grid names it in its Settled Stamp, and a
     * plain Escape typed in the grid while it is placed aborts `read`'s signal: the Workbook stays as it was, and the
     * notice says so; an opening cancelled before its turn ends cancelled. While the read is under way the notice says
     * so, and the grid is busy and painted as a paste under way is. A read that fails leaves the Workbook as it was too,
     * says so with the failure's message, and the failure is this method's. When the document arrives it replaces the
     * whole Workbook, as a new `document` does (ADR-0048): the undo stack is cleared, the Linked Tables declared are gone
     * with the Workbook that held them, and `onWorkbookChanged` is raised with the Workbook opened. An Escape that comes
     * once it has landed changes nothing, and the Sheet says it came too late; one that finds the opening over otherwise
     * says there was nothing to cancel.
     *
     * Only the grid's inputs wait behind it. A method the Consumer calls meanwhile is not an input and is not held:
     * what it changes is replaced when the file lands. Once the Sheet is destroyed the signal is aborted, and nothing
     * lands.
     *
     * @param read - Reads the document, looking at the signal as it goes; a read that sees it aborted rejects with the
     *   signal's reason.
     * @returns True once the document opened; false when Escape cancelled the read, or the Sheet was destroyed first.
     * @throws SheetRefusedError An edit is open in the opening's turn; nothing was read.
     * @throws Error Whatever `read` failed with, such as `XlsxOpenError`, or the engine's refusal of the document;
     *   nothing opened. Or whatever `onWorkbookChanged` or `onSheetsChanged` failed with, once the file opened.
     */
    open(read: (signal: AbortSignal) => PromiseLike<WorkbookDocument>): Promise<boolean>;
    /**
     * Saves the Workbook at this call's place in the Input Order (ADR-0330 §10), as `ExSheet.SaveAsync` does: `write` is
     * handed the Workbook as the inputs made before the call left it — every Sheet with its Values, its Cell Formats and
     * widths, and the rows each Linked Table holds — such as `workbook => saveXlsxWorkbook(workbook)` from
     * `@ex-grid/sheet-xlsx`. The Workbook is lent for that call only: read it before the call's first `await`, as
     * `saveXlsxWorkbook` does, and never change it. The inputs made after the save are taken once `write` has returned,
     * not once its promise settles, so a slow write holds nobody up.
     *
     * @returns A promise that settles once `write`'s has.
     * @throws SheetRefusedError An edit is open: the file would quietly lack what is being typed. The Sheet says so, and
     *   nothing was written.
     * @throws Error Whatever `write` failed with, such as `XlsxSaveError`.
     */
    save(write: (workbook: Workbook) => void | PromiseLike<void>): Promise<void>;
    /** Hands the keyboard to the Sheet's grid: to the field an open edit is typed in, else to where the grid takes keys
     * (ADR-0080), as the grid's own `focus` does. A Consumer's control beside the Sheet gives the keyboard back with
     * this. Throws once the Sheet is destroyed, as `holdSettled` and `inOrder` do. */
    focus(): void;
    /**
     * Holds the Settled Mark of the Sheet's grid (ADR-0220) until the function it answers is called, as the grid's own
     * `holdSettled` does: for a Framework Adapter whose Chrome's contents arrive after their slot, a round trip later on
     * a circuit (ADR-0410 section 5), so that nothing reads the part before it stands. Each hold is let go once.
     */
    holdSettled(): () => void;
    /**
     * Runs `handling` in its place in the Input Order of the Sheet's grid (ADR-0240 section 6), as the grid's own
     * `inOrder` does: after every input made so far, and before any made after it, which wait until the promise it
     * answers settles. A Sheet destroyed before its turn runs nothing; one already destroyed throws.
     */
    inOrder(handling: () => void | PromiseLike<void>): Promise<void>;
    /**
     * Says `sentence` in the Sheet's notice, where the Sheet says its own refusals (ADR-0100): for a Framework Adapter
     * whose Consumer's Toolbar button ran a command a round trip later that was refused, as the C# toolbar says one.
     * Throws once the Sheet is destroyed, as `inOrder` does.
     */
    tell(sentence: string): void;
    /** Stops the Sheet and empties its element. */
    destroy(): void;
}

// Whether `day` is a calendar day the engine takes as a Sheet Day (ADR-0121).
function isSheetDay(day: unknown): day is SheetDay {
    if (typeof day !== 'object' || day === null) return false;
    const { year, month, day: date }: { readonly year?: unknown; readonly month?: unknown; readonly day?: unknown } = day;
    if (typeof year !== 'number' || typeof month !== 'number' || typeof date !== 'number') return false;
    if (!Number.isInteger(year) || !Number.isInteger(month) || !Number.isInteger(date) || year < 1900 || year > 9999) return false;
    const read = new Date(Date.UTC(year, month - 1, date));
    return read.getUTCFullYear() === year && read.getUTCMonth() === month - 1 && read.getUTCDate() === date;
}

// Whether two lists name the same figures, in any order: the summary shows them in Excel's order whatever the order
// given (ADR-0130).
function sameFigures(a: readonly SummaryFigure[] | undefined, b: readonly SummaryFigure[] | undefined): boolean {
    if (a === b) return true;
    if (a === undefined || b === undefined) return false;
    const held = new Set(a);
    return held.size === new Set(b).size && b.every(figure => held.has(figure));
}

// Excel's key for the ribbon, which hides and shows the Sheet Toolbar (ADR-0100).
const toolbarToggleKey = 'Control+F1';

// The largest seed: a seed is an unsigned 64-bit integer, as the engine and C#'s ulong hold it (ADR-0180).
const maxSeed = (1n << 64n) - 1n;

// Whether `error` is the reason an aborted signal rejects with: the work ended cancelled (ADR-0240 section 12).
function cancelledBy(signal: AbortSignal, error: unknown): boolean {
    return signal.aborted && error === signal.reason;
}

// What a failure says, in the notice that says nothing opened.
function messageOf(error: unknown): string {
    return error instanceof Error ? error.message : String(error);
}

// A seed drawn from the browser's generator, as the C# Sheet draws one from Random.Shared (ADR-0180).
function chooseSeed(): bigint {
    return elementAt(crypto.getRandomValues(new BigUint64Array(1)), 0);
}

/** Draws a Sheet into `element`, which it owns from now on. */
export function createSheet(element: HTMLElement, options: SheetOptions = {}): SheetHandle {
    const view = new SheetView(element, options);
    return Object.freeze({
        get workbook() { return view.workbook; },
        get canUndo() { return view.canUndo; },
        get canRedo() { return view.canRedo; },
        get isEditing() { return view.isEditing; },
        update: (next: SheetOptions) => { view.update(next); },
        do: (sheetOrEdit: string | SheetEdit, edit?: SheetEdit) => { view.do(sheetOrEdit, edit); },
        setCellFormat: (change: CellFormatChange) => view.setCellFormat(change),
        setCellFormatFromFocus: (changeOf: (focus: CellFormat) => CellFormatChange) => view.setCellFormatFromFocus(changeOf),
        readFocusFormat: () => view.readFocusFormat(),
        setCellFormatFrom: (reading: FocusFormatReading, change: CellFormatChange) => view.setCellFormatFrom(reading, change),
        openFormatCells: () => view.openFormatCells(),
        cellFormatAt: (sheetOrAddress: string | CellAddress, address?: CellAddress) => view.cellFormatAt(sheetOrAddress, address),
        undo: () => view.undo(),
        redo: () => view.redo(),
        declareLinkedTable: (name: string, columns: readonly string[], key?: string | readonly string[]) => { view.declareLinkedTable(name, columns, key); },
        pushLinkedTable: (name: string, rows: Iterable<readonly (Value | undefined)[]>) => { view.pushLinkedTable(name, rows); },
        open: (read: (signal: AbortSignal) => PromiseLike<WorkbookDocument>) => view.open(read),
        save: (write: (workbook: Workbook) => void | PromiseLike<void>) => view.save(write),
        focus: () => { view.focus(); },
        holdSettled: () => view.holdSettled(),
        inOrder: (handling: () => void | PromiseLike<void>) => view.inOrder(handling),
        tell: (sentence: string) => { view.tellFromAdapter(sentence); },
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
    #linkedColumnsTold: readonly LinkedColumnColour[] = [];
    // The figures chosen in the summary's menu, which stand until the Consumer hands other figures (ADR-0130).
    #summaryChoice: readonly SummaryFigure[] | undefined;
    // The Pointing Scope this Sheet joined (ADR-0058), where the grid last said its edit stands with respect to Point,
    // whether DOM focus is inside the Sheet as the browser last said, and whether the Scope last heard it points.
    #joinedScope: PointingScope | undefined;
    #pointState: GridPointState = 'none';
    #holdsKeyboard = false;
    #scopeHeardPoints = false;
    // The copy last put on the clipboard, whose Entries a paste of exactly it carries (ADR-0048), and the sentence a
    // copy of the Sheet's own was last refused with (ADR-0049).
    #ownCopy: SheetOwnCopy | undefined;
    #copyRefusal: string | undefined;
    // Format Cells while it stands in the grid's popover, and the Sheet, the Selection and the ranges it was opened
    // over; and the tab it last showed, which the next opening shows again (ADR-0071). The grid closing the popover
    // itself, by Escape or a press elsewhere, tells the Sheet nothing: the record stands, its frame gone, until the
    // next opening or move drops it, and nothing reads it as open meanwhile, as only the frame's OK sets anything.
    #formatCells: {
        readonly panel: FormatCellsPanel | SlottedFormatCells; readonly draft: FormatCellsDraft; readonly sheet: Sheet; readonly selection: GridSelection;
        readonly ranges: readonly CellRange[];
    } | undefined;
    #formatCellsTab: FormatCellsTab = 'number';
    // What each colour control's face sets: the colour chosen last from its list, red for the Font and yellow for the
    // Fill until one is, as Excel's start (ADR-0100). Taking the colour away is not kept.
    #faceFontColour: CellColour = colourFromRgb(0xFF0000);
    #faceFill: CellColour = colourFromRgb(0xFFFF00);
    // The Borders preset the face sets: the one chosen last, Bottom Border until one is.
    #faceBorder: SheetToolbarBorder = 'bottom';
    // The Selection a toolbar list a Chrome draws stands over: one moved from it ends the list.
    #chromeListsOver: GridSelection | undefined;
    // What each Focus format reading was read against: the Sheet shown and the Selection (ADR-0100).
    readonly #readings = new WeakMap<FocusFormatReading, { readonly sheet: Sheet; readonly selection: GridSelection }>();
    // The grid's slots made from the chrome last given.
    #gridChromeOf: { readonly given: SheetChrome | undefined; readonly made: GridChrome<SheetRow> } | undefined;
    // A Toolbar Item's list while it stands in the grid's popover frame, and the Selection it was opened over.
    #choices: { readonly panel: SheetChoicesPanel; readonly selection: GridSelection; readonly keyTipRow: number | undefined } | undefined;
    // The Alt key went down alone where the toolbar's keys are heard, and its release has not been heard: the release
    // starts the KeyTips, unless another key went down first, a chord, which is not a KeyTip.
    #altAlone = false;
    // The category the Number Format menu shows, read once for each Number Format and culture the Focus cell has.
    #shownCategory: { readonly format: NumberFormat; readonly culture: Culture; readonly category: NumberFormatCategory } | undefined;
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
    // The seed chosen when the document was opened, which draws while no `seed` is given (ADR-0180).
    #chosenSeed: bigint;
    // Whether the Workbook shown was opened from a file since the Consumer last handed a document (ADR-0330 §10).
    #fileOpened = false;
    // The Sheets' names as the Consumer last heard them, so a change says what it was taken from (ADR-0331 §6): at
    // open, the document's own.
    #sheetNames: readonly string[] = [];
    // The day read again at midnight in the zone, and NOW moved on as the minute turns (ADR-0121, ADR-0124).
    #dayTimer: ReturnType<typeof setTimeout> | undefined;
    #minuteTimer: ReturnType<typeof setTimeout> | undefined;
    // The browser's zone, read once, as the C# Sheet is told it once at attach (ADR-0122).
    readonly #browserZone = browserTimeZone();

    constructor(element: HTMLElement, options: SheetOptions) {
        this.#element = element;
        this.#options = SheetView.#checked(options);
        this.#words = new SheetWords({ japanese: this.#options.japanese, label: this.#options.label });
        this.#commandLabel = SheetView.#commandLabelOf(this.#words, this.#options.commandLabel);
        this.#document = this.#options.document;
        this.#chosenSeed = chooseSeed();
        this.#workbook = this.#open(this.#options);
        this.#source = new SheetSource(this.#workbook.shown, () => this.#words, this.#findStepOf);
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
        this.#tabs = new SheetTabs(document, {
            pressed: this.#onTabPressed, added: this.#onTabAdded, listKey: this.#onTabListKey,
            renameAsked: sheet => { this.#beginRename(sheet, false); },
            renameInput: text => {
                if (text === this.#renameText) return;
                this.#renameText = text;
                this.#tabs.markRenameRefused(false);
            },
            renameKey: key => {
                if (key === 'Enter') this.#commitRename(this.#renameFromTab ? 'tab' : 'grid');
                else this.#endRename(this.#renameFromTab ? 'tab' : 'grid');
            },
            renameLeft: session => {
                if (session !== this.#renameSession || this.#tabs.renaming === undefined) return;
                if (!this.#commitRename('nowhere')) this.#endRename('nowhere');
            },
            menuAsked: sheet => { this.#openTabMenu(sheet, false); },
            menuKey: this.#onTabMenuKey,
            menuCommand: command => { this.#tabCommand(command); },
            menuLeft: session => {
                if (session !== this.#menuSession || this.#menuSheet === undefined) return;
                this.#menuSheet = undefined;
                this.#tabs.closeMenu();
            },
            dropped: (sheet, target) => { this.#moveByDrag(sheet, target); },
        });
        this.#toolbar = new SheetToolbar(document, {
            giveBack: () => { this.#grid?.focus(); },
            // A choice of a list the Chrome draws, made as the built-in list's is.
            chosen: choice => {
                if (this.#editing) this.#tell(this.#words.toolbarWhileEditing);
                else choice.choose();
                this.#paintToolbar();
            },
            opening: () => { this.#chromeListsOver = this.#grid?.readView().selection; },
        });
        element.replaceChildren(this.#gridElement, this.#notice);
        element.addEventListener('focusout', this.#onSheetFocusOut);
        element.addEventListener('pointerdown', this.#forgetAlt, { capture: true });
        element.addEventListener('wheel', this.#forgetAlt, { capture: true, passive: true });
        element.addEventListener('focusin', this.#onKeyboardIn);
        element.addEventListener('focusout', this.#onKeyboardOut);
        this.#holdsKeyboard = element.contains(element.ownerDocument.activeElement);
        this.#grid = createGrid(this.#gridElement, gridOptions);
        // Excel's keys for the Sheet Tabs, heard on the Sheet before its grid hears them, wherever in it the keyboard
        // is, so the browser does not move among its own tabs (ADR-0331 §4). Listened to once the grid is, so a key the
        // grid holds behind a commit or an opening is heard here in its turn, not before it (ADR-0240).
        element.addEventListener('keydown', this.#onSheetKey, { capture: true });
        element.addEventListener('keyup', this.#onSheetKeyUp, { capture: true });
        this.#bindPointingScope();
        this.#paintFrame();
        this.#armTheDay();
        this.#armTheMinute();
    }

    get workbook(): Workbook { return this.#workbook; }

    get canUndo(): boolean { return this.#history.canUndo; }

    get canRedo(): boolean { return this.#history.canRedo; }

    get isEditing(): boolean { return this.#editing; }

    update(options: SheetOptions): void {
        this.#alive();
        const next = SheetView.#checked(options);
        // A height too short for the bands is refused before anything changes, so the Sheet keeps the options it had.
        this.#gridHeight(next);
        // Judged by the culture the Sheet holds, so a locale naming that culture again changes nothing; with a document,
        // the document's culture is the one, as the option says.
        // A file opened since is a document of its own, whose culture the file's opening declared (ADR-0330 §10).
        if (next.document === undefined && this.#document === undefined && !this.#fileOpened
            && Culture.of(next.locale ?? navigator.language) !== this.#source.sheet.culture) {
            throw new Error("A Sheet's culture is recorded in its document, so it is not changed on a live Sheet: Entries typed would not be read again (ADR-0048).");
        }
        if ((next.density ?? 'compact') !== (this.#options.density ?? 'compact')) { // oxlint-disable-line ex-grid/no-stand-in-default -- the grid's own default density (ADR-0028)
            this.#columns = new SheetColumns(next.density ?? 'compact', error => this.#words.formulaUnreadable(error)); // oxlint-disable-line ex-grid/no-stand-in-default -- the grid's own default density (ADR-0028)
            this.#columns.read(this.#source.sheet);
        }
        const wordsMoved = next.label !== this.#options.label || next.japanese !== this.#options.japanese;
        if (wordsMoved) this.#words = new SheetWords({ japanese: next.japanese, label: next.label });
        if (wordsMoved || next.commandLabel !== this.#options.commandLabel) this.#commandLabel = SheetView.#commandLabelOf(this.#words, next.commandLabel);
        const seedBefore = this.#options.seed;
        const previous = this.#options;
        this.#options = next;
        // Other figures handed over replace the choice made in the menu; the same figures again leave it standing.
        if (!sameFigures(next.summaryFigures, previous.summaryFigures)) this.#summaryChoice = undefined;
        // A document this Sheet raised, handed back, is its own state: nothing is opened again.
        if (next.document !== undefined && this.#raised.has(next.document)) this.#document = next.document;
        if (next.document !== this.#document) {
            this.#document = next.document;
            this.#chosenSeed = chooseSeed();
            this.#replaceWorkbook(this.#open(next), false);
            return;
        }
        // A new seed, or none, which goes back to the one chosen at opening, draws again at the same count of changes:
        // no change of the Sheet's, so neither the document nor the notice hears it.
        if (next.seed !== seedBefore) this.#changed([this.#workbook.setSeed(next.seed ?? this.#chosenSeed)], false, false);
        // A new fixed day or zone sets the day at once, and NOW reads the new zone.
        if (!sameDay(next.today, previous.today) || next.timeZone !== previous.timeZone) {
            this.#changed([this.#workbook.setToday(this.#resolveDay(next)), this.#workbook.recalculateVolatile()], false, false);
            this.#armTheDay();
            this.#armTheMinute();
        }
        this.#grid?.update(this.#gridOptions());
        // A summary declined over a refused cell says why in the words that stand.
        if (wordsMoved) this.#grid?.refreshSummary();
        this.#bindPointingScope();
        this.#paintFrame();
    }

    do(sheetOrEdit: string | SheetEdit, edit?: SheetEdit): void {
        this.#alive();
        if (typeof sheetOrEdit !== 'string') {
            this.#refuseUnnamedSheet();
            this.#refuseWhileEditing();
            this.#perform(sheetOrEdit);
            return;
        }
        if (edit === undefined) throw new TypeError('A command that names its Sheet names its edit too.');
        const target = this.#named(sheetOrEdit);
        this.#refuseWhileEditing();
        this.#perform(edit, undefined, undefined, target);
    }

    setCellFormat(change: CellFormatChange): boolean {
        this.#alive();
        if (change.isEmpty) throw new RangeError('The change sets no part of a Cell Format.');
        this.#refuseWhileEditing();
        // The Selection as the grid holds it now, which a Selection notice still on its way may not have said yet.
        const selection = this.#grid?.readView().selection;
        if (selection === undefined || selection.isEmpty) return false;
        const cells = selection.ranges.map(rangeOf);
        this.#perform(SheetEdit.setCellFormat(cells, change), undefined, widensAsAKeyDoes(change) ? cells : undefined);
        return true;
    }

    setCellFormatFromFocus(changeOf: (focus: CellFormat) => CellFormatChange): boolean {
        if (typeof changeOf !== 'function') throw new TypeError('setCellFormatFromFocus takes the change as a function of the Focus cell\'s Cell Format.');
        this.#alive();
        this.#refuseWhileEditing();
        return this.#formatFromFocus(changeOf, widensAsAHomeTabButtonDoes);
    }

    readFocusFormat(): FocusFormatReading | undefined {
        this.#alive();
        // Read for a change, which an open edit refuses: refused here, before the change is made from it.
        this.#refuseWhileEditing();
        // The Selection as the grid holds it now, which a Selection notice still on its way may not have said yet.
        const selection = this.#grid?.readView().selection;
        const focus = selection?.focus;
        if (selection === undefined || focus === undefined) return undefined;
        const sheet = this.#source.sheet;
        const reading: FocusFormatReading = Object.freeze({ format: sheet.getCellFormat(new CellAddress(focus.row, focus.column)) });
        this.#readings.set(reading, { sheet, selection });
        return reading;
    }

    setCellFormatFrom(reading: FocusFormatReading, change: CellFormatChange): boolean {
        this.#alive();
        const against = this.#readings.get(reading);
        if (against === undefined) throw new TypeError('setCellFormatFrom takes a reading this Sheet\'s readFocusFormat made, once.');
        // A reading is taken by its set, whatever comes of it: set again, it would be a change made from a state the
        // user may have left and come back to.
        this.#readings.delete(reading);
        if (change.isEmpty) throw new RangeError('The change sets no part of a Cell Format.');
        this.#refuseWhileEditing();
        // What the change was made from, which must still stand: set on another Selection, or over a format that has
        // changed since, it would be a plausible change to cells, or in a direction, it was not made for (ADR-0100).
        // Cells moved under a Selection that stands, their Focus format unchanged, take the change a call made now would
        // make.
        const selection = this.#grid?.readView().selection;
        const focus = selection?.focus;
        if (selection === undefined || focus === undefined || this.#source.sheet !== against.sheet || !selection.equals(against.selection)) {
            this.#refuseAndTell('selectionMoved', this.#words.selectionMoved);
        }
        if (!against.sheet.getCellFormat(new CellAddress(focus.row, focus.column)).equals(reading.format)) {
            this.#refuseAndTell('focusFormatChanged', this.#words.focusFormatChanged);
        }
        const cells = selection.ranges.map(rangeOf);
        this.#perform(SheetEdit.setCellFormat(cells, change), undefined, widensAsAHomeTabButtonDoes(change) ? cells : undefined);
        return true;
    }

    // A refusal of a change made away from the Sheet, said in the notice too: the user moved what it was made from, and
    // sees it come to nothing.
    #refuseAndTell(reason: SheetRefusalReason, sentence: string): never {
        // The move it names may be heard after it, and that Selection notice must not take the sentence away.
        this.#tell(sentence, { outlivesMove: true });
        throw new SheetRefusedError(new SheetRefusal(reason, sentence));
    }

    openFormatCells(): boolean {
        this.#alive();
        this.#refuseWhileEditing();
        this.#openFormatCells();
        return this.#formatCells !== undefined;
    }

    cellFormatAt(sheetOrAddress: string | CellAddress, address?: CellAddress): CellFormat {
        this.#alive();
        if (typeof sheetOrAddress !== 'string') {
            this.#refuseUnnamedSheet();
            return this.#source.sheet.getCellFormat(sheetOrAddress);
        }
        if (address === undefined) throw new TypeError('A read that names its Sheet names its cell too.');
        return this.#named(sheetOrAddress).getCellFormat(address);
    }

    #named(sheet: string): Sheet {
        const found = this.#workbook.find(sheet);
        if (found === undefined) throw new RangeError(`No Sheet of the Workbook is named '${sheet}'.`);
        return found;
    }

    // A command or an address that names no Sheet, refused while the Workbook holds several: taken against the Sheet
    // shown, its outcome would depend on whether a press on a tab came before it (ADR-0331 §6).
    #refuseUnnamedSheet(): void {
        const count = this.#workbook.sheets.length;
        if (count > 1) throw new SheetRefusedError(new SheetRefusal('sheetNotNamed', this.#words.sheetNotNamed(count)));
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

    declareLinkedTable(name: string, columns: readonly string[], key: string | readonly string[] = []): void {
        this.#alive();
        // The engine counts a declaration that declares or replaces a table, and no other: only such a one is
        // recorded in the document, so only it raises one.
        const before = this.#workbook.changes;
        const change = this.#workbook.declareLinkedTable(name, columns, key);
        const declared = before !== this.#workbook.changes;
        this.#changed([change], declared, false);
        if (declared) {
            this.#referencesIn = this.#referencesOnSheetShown();
            this.#grid?.update(this.#gridOptions());
        }
    }

    pushLinkedTable(name: string, rows: Iterable<readonly (Value | undefined)[]>): void {
        this.#alive();
        let change: WorkbookChange;
        try {
            change = this.#workbook.pushLinkedTable(name, rows);
        } catch (error) {
            if (!(error instanceof RepeatedKeyError)) throw error;
            // The table went back to waiting: its readers show #GETTING_DATA, on every Sheet, and then the Consumer
            // hears why (ADR-0049). The refusal names what it changed on the Workbook's first Sheet; another Sheet shown
            // has every row of its Window read again, as the C# Sheet's are.
            const first = this.#source.sheet === this.#workbook.sheets[0];
            this.#sheetChanged(first ? [error.change] : [], false);
            if (!first) {
                this.#source.renewWindow();
                this.#grid?.refreshSummary();
            }
            this.#paintToolbar();
            throw error;
        }
        this.#changed([change], false, false);
    }

    // A step taken under an open editor would change the cells, or the Sheet, it commits to: refused, as the C#
    // Sheet's is (ADR-0048).
    #refuseWhileEditing(): void {
        if (this.#editing) throw new SheetRefusedError(new SheetRefusal('editIsOpen', this.#words.editIsOpen));
    }

    // ---- Opening and saving a file in the Input Order (ADR-0330 §10) -----------------------------------------
    //
    // The Consumer chooses the file and delivers the written one (ADR-0330 §2): the Sheet references no file format, and
    // takes a Workbook Document still being read, or lends the Workbook to a writer, each at its own place in the order.

    async open(read: (signal: AbortSignal) => PromiseLike<WorkbookDocument>): Promise<boolean> {
        this.#alive();
        if (typeof read !== 'function') throw new TypeError('open takes the read as a function of an AbortSignal (ADR-0330 §10).');
        // What a handler of the Consumer's throws once the file has landed is that handler's, held until the opening has
        // ended as landed: it never reads as a cancel, nor as a failure of the opening.
        let afterLanding: { readonly error: unknown } | undefined;
        const opening = async (signal: AbortSignal): Promise<void> => {
            await this.#opening(read, signal);
            try {
                this.#raiseDocument();
            // oxlint-disable-next-line ex-grid/no-catch -- held, and rethrown once the opening has ended as landed
            } catch (error) {
                afterLanding = { error };
            }
        };
        const grid = this.#grid;
        if (grid === undefined) throw new Error('This Sheet was destroyed.');
        const landed = await grid.cancellableInOrder(opening, this.#openingCameTooLate);
        if (afterLanding !== undefined) throw afterLanding.error;
        return landed;
    }

    // An Escape that came too late, said in its own turn.
    readonly #openingCameTooLate = (landed: boolean): void => {
        if (this.#destroyed) return;
        this.#tell(landed ? this.#words.openingAlreadyLanded : this.#words.openingNothingToCancel, { outlivesMove: true });
    };

    // The opening, in its turn: refused while an edit is open; else said, read, and landed, or left as it was and thrown.
    async #opening(read: (signal: AbortSignal) => PromiseLike<WorkbookDocument>, signal: AbortSignal): Promise<void> {
        // The grid runs it, so the Sheet still stands here. An Escape that came while the opening waited for its turn has cancelled it already: it ends cancelled, whatever
        // its turn finds.
        if (signal.aborted) {
            this.#tell(this.#words.openingCancelled, { outlivesMove: true });
            throw signal.reason;
        }
        this.#refuseFileWhileEditing(this.#words.openWhileEditing);
        this.#tell(this.#words.opening, { outlivesMove: true });
        let document: unknown;
        try {
            document = await read(signal);
            if (!(document instanceof WorkbookDocument)) throw new TypeError('The read answered no Workbook Document (ADR-0330 §10).');
            // The point past which an Escape finds the file opened.
            signal.throwIfAborted();
        } catch (error) {
            if (!this.#destroyed) {
                this.#tell(cancelledBy(signal, error) ? this.#words.openingCancelled : this.#words.openingRefused(messageOf(error)), { outlivesMove: true });
            }
            throw error;
        }
        // The Sheet still stands here: destroying it destroys the grid, which aborts the signal looked at above.
        let opened: Workbook;
        // A seed of its own, as a new document gets one, and the names the Consumer has heard, which a change to the
        // Sheets is said against once it lands (ADR-0180, ADR-0331 §6).
        const seed = chooseSeed();
        const heard = this.#sheetNames;
        try {
            // Read before anything is replaced: a document the engine refuses leaves the Workbook as it was.
            opened = this.#open(this.#options, document, seed);
        } catch (error) {
            this.#sheetNames = heard;
            this.#tell(this.#words.openingRefused(messageOf(error)), { outlivesMove: true });
            throw error;
        }
        this.#sheetNames = heard;
        this.#chosenSeed = seed;
        // The inputs held behind the opening are taken after it, on the Workbook opened.
        this.#replaceWorkbook(opened, true);
        this.#tell(this.#words.opened, { outlivesMove: true });
    }

    // A Workbook opened replaces the one shown, from a new document or a file (ADR-0048, ADR-0330 §10): its undo stack
    // and its views go with it, under a new Row Sequence Version, which drops the Selection and refuses what was aimed
    // at the old cells (ADR-0011, ADR-0142).
    #replaceWorkbook(opened: Workbook, fromFile: boolean): void {
        this.#fileOpened = fromFile;
        this.#workbook = opened;
        this.#history.clear();
        this.#showSheet(opened.shown);
        this.#armTheDay();
        this.#armTheMinute();
    }

    async save(write: (workbook: Workbook) => void | PromiseLike<void>): Promise<void> {
        this.#alive();
        if (typeof write !== 'function') throw new TypeError('save takes the write as a function of the Workbook (ADR-0330 §10).');
        const grid = this.#grid;
        if (grid === undefined) throw new Error('This Sheet was destroyed.');
        const written: { writing?: PromiseLike<void> } = {};
        await grid.inOrder(() => {
            this.#refuseFileWhileEditing(this.#words.saveWhileEditing);
            written.writing = Promise.resolve(write(this.#workbook));
        });
        // A save whose turn never came — the Sheet destroyed while inputs before it were held — wrote nothing, and says so.
        if (written.writing === undefined) throw new Error('This Sheet was destroyed before the save\'s turn: nothing was written.');
        await written.writing;
    }

    focus(): void {
        this.#liveGrid().focus();
    }

    holdSettled(): () => void {
        return this.#liveGrid().holdSettled();
    }

    inOrder(handling: () => void | PromiseLike<void>): Promise<void> {
        if (typeof handling !== 'function') throw new TypeError('inOrder takes the work as a function (ADR-0240 section 6).');
        return this.#liveGrid().inOrder(handling);
    }

    tellFromAdapter(sentence: string): void {
        if (typeof sentence !== 'string') throw new TypeError('tell takes the sentence to say.');
        this.#alive();
        this.#tell(sentence);
    }

    // The Sheet's grid, while the Sheet stands.
    #liveGrid(): GridHandle<SheetRow> {
        this.#alive();
        const grid = this.#grid;
        if (grid === undefined) throw new Error('Internal: a Sheet that stands has no grid.');
        return grid;
    }

    // A file opened or saved while an edit is open is refused by name, and said (ADR-0330 §10, ADR-0048).
    #refuseFileWhileEditing(sentence: string): void {
        if (!this.#editing) return;
        this.#tell(sentence);
        throw new SheetRefusedError(new SheetRefusal('editIsOpen', sentence));
    }

    destroy(): void {
        if (this.#destroyed) return;
        this.#destroyed = true;
        clearTimeout(this.#dayTimer);
        clearTimeout(this.#minuteTimer);
        // Let go of first, so nothing the grid tells as it goes reads it again.
        const grid = this.#grid;
        this.#grid = undefined;
        grid?.destroy();
        // A Chrome's toolbar contents and tab faces go with the Sheet.
        this.#toolbar.dispose();
        this.#tabs.dispose();
        // A destroyed Sheet points at nothing.
        this.#joinedScope?.leave(this.#pointingSheet);
        this.#joinedScope = undefined;
        this.#element.removeEventListener('focusin', this.#onKeyboardIn);
        this.#element.removeEventListener('focusout', this.#onKeyboardOut);
        this.#element.removeEventListener('keydown', this.#onSheetKey, { capture: true });
        this.#element.removeEventListener('keyup', this.#onSheetKeyUp, { capture: true });
        this.#element.removeEventListener('focusout', this.#onSheetFocusOut);
        this.#element.removeEventListener('pointerdown', this.#forgetAlt, { capture: true });
        this.#element.removeEventListener('wheel', this.#forgetAlt, { capture: true });
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
            ...(this.#options.pinnedColumnCount === undefined ? {} : { pinnedColumnCount: this.#options.pinnedColumnCount }),
            ...(this.#options.showRowHeadings === false ? {} : { rowHeadings: rowLabel }),
            hideHeader: this.#options.showColumnHeadings === false,
            showFormulaBar: this.#options.showFormulaBar !== false,
            nameBoxLabel,
            nameBoxSizeLabel,
            onNameBoxEntered: this.#onNameBoxEntered,
            dataEdge: this.#dataEdge,
            hideColumnMenu: true,
            hideCopyWithHeaders: this.#options.allowCopyWithHeaders !== true,
            declaredKeys: sheetDeclaredKeys,
            onDeclaredKey: this.#onDeclaredKey,
            contextCommands: this.#contextCommands,
            commandLabel: this.#commandLabel,
            // The Selection Summary is the Sheet Source's (ADR-0130); the Sheet holds the figures chosen in its menu.
            ...(this.#options.showSelectionSummary === undefined ? {} : { showSelectionSummary: this.#options.showSelectionSummary }),
            ...this.#summaryFiguresShown(),
            ...(this.#options.onSelectionSummaryChanged === undefined ? {} : { onSelectionSummaryChanged: this.#options.onSelectionSummaryChanged }),
            onSummaryFiguresChanged: this.#onSummaryFiguresChanged,
            // Find matches the text a cell is shown with, by the Sheet's rule; the column letters are addresses,
            // not content, and Excel's Find never stops on them (ADR-0332 §4, ADR-0333 §2).
            findMatch: sheetFindMatch,
            findsHeaders: false,
            onReplace: this.#onReplace,
            pasteMaySpill: true,
            onEdit: this.#onEdit,
            copyAnswer: this.#copyAnswer,
            onPaste: this.#onPaste,
            showFillHandle: true,
            onFill: this.#onFill,
            onClear: this.#onClear,
            onUndo: this.#onUndo,
            onRedo: this.#onRedo,
            onColumnWidthChanged: this.#onColumnWidthChanged,
            onSelectionChanged: this.#onSelectionChanged,
            onEditingChanged: this.#onEditingChanged,
            // The References in the Formula being edited are coloured and outlined (ADR-0057): on the Sheet the
            // Formula is typed on, all of them; on another shown to point at its cells, those naming it (ADR-0331 §5).
            referencesIn: this.#referencesIn,
            onReferenceKeyColoursChanged: this.#onKeyColoursChanged,
            // Point: the arrows and a press write a Reference where one can go, and F4 cycles its forms (ADR-0051).
            pointAt: sheetPointAt,
            referenceText: this.#referenceText,
            cycleReference: sheetCycleReference,
            onPointStateChanged: this.#onPointStateChanged,
            onPointArrowFromOutside: this.#onPointArrow,
            // Completion and the argument hint, beneath the surface typed in (ADR-0051, ADR-0058).
            completeEditorText: this.#completeEditorText,
            onRefused: this.#onRefused,
            onEditDiscarded: this.#onEditDiscarded,
            onOverwriteNotice: this.#onOverwriteNotice,
            ...this.#gridChrome(),
        };
    }

    // The figures the summary shows: those chosen in its menu, else the Consumer's, else the grid's own.
    #summaryFiguresShown(): { readonly summaryFigures?: readonly SummaryFigure[] } {
        const figures = this.#summaryChoice ?? this.#options.summaryFigures;
        return figures === undefined ? {} : { summaryFigures: figures };
    }

    // A choice made in the summary's menu: the Sheet holds it and shows it, then tells the Consumer (ADR-0130).
    readonly #onSummaryFiguresChanged = (figures: readonly SummaryFigure[]): void => {
        this.#summaryChoice = Object.freeze([...figures]);
        this.#grid?.update(this.#gridOptions());
        this.#options.onSummaryFiguresChanged?.(this.#summaryChoice);
    };

    // The grid's slots of the Sheet's chrome, handed to its grid as they were given: the same object while the chrome
    // is, so the grid keeps the parts it has drawn (ADR-0390).
    // The find panel is the Sheet's own, with Within (ADR-0332 §5), where the chrome draws none: a Chrome's panel is
    // the grid's, and searches the Sheet shown.
    #gridChrome(): { readonly chrome: GridChrome<SheetRow> } {
        const chrome = this.#options.chrome;
        if (this.#gridChromeOf === undefined || this.#gridChromeOf.given !== chrome) {
            const slots: Record<string, unknown> = { findPanel: this.#findPanel };
            for (const name of gridChromeSlotNames) {
                const slot = chrome?.[name];
                if (slot !== undefined) slots[name] = slot;
            }
            // The grid's slots, each checked a function by #checked.
            this.#gridChromeOf = { given: chrome, made: slots };
        }
        return { chrome: this.#gridChromeOf.made };
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

    // A Fill Intent (ADR-0050, item 5), filled as Excel fills by the engine: copies with relative References shifted,
    // a linear series from two or more numbers, dates by day, as one step on the undo stack. Any other pattern is
    // refused before anything is written: the grid is told, so the Selection stays on the source, and the user is
    // told why.
    readonly #onFill = (intent: GridFillIntent): void => {
        if (intent.rowSequenceVersion !== this.#source.snapshot().rowSequenceVersion) {
            intent.refuse();
            return;
        }
        this.#performChecked(intent, SheetEdit.fill(rangeOf(intent.source), rangeOf(intent.target), intent.direction));
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
        this.#findMoved();
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
        if (this.#destroyed) return;
        this.#followSelection(selection);
        // Heard last, so a Consumer that reads or formats from here finds the Sheet already following the move.
        this.#options.onSelectionChanged?.(selection);
    };

    #followSelection(selection: GridSelection): void {
        // A Chrome's toolbar list reads the Selection it opened over, as the built-in one does: a Selection moved
        // since ends it. The notice of the Selection it opened over, given after the grid's paint, is no move.
        if (this.#chromeListsOver?.equals(selection) === false) {
            this.#chromeListsOver = selection;
            this.#toolbar.endLists();
        }
        const before = this.#selection?.focus;
        this.#selection = selection;
        // The Toolbar shows the Focus cell's state: a Selection grown or shrunk around the same Focus changes none of it.
        const focus = selection.focus;
        if (before === undefined || focus === undefined || before.row !== focus.row || before.column !== focus.column) this.#paintToolbar();
        // Format Cells shows the Selection it opened over and sets what OK names on it: a Selection moved under it,
        // by the Name Box or a Consumer, ends it. The notice of the Selection it opened over, which the grid gives after
        // its paint, is no move.
        if (this.#formatCells?.selection.equals(selection) === false) this.#endFormatCells();
        if (this.#choices?.selection.equals(selection) === false) this.#endChoices();
        // A Selection the grid dropped, as another Sheet is shown, is no move of the user's: the notice stands.
        if (selection.isEmpty) return;
        // The user moved on: what the notice said is behind them, unless it was told by the gesture that moved.
        if (this.#noticeOutlivesMove) {
            this.#noticeOutlivesMove = false;
            return;
        }
        this.#clearNotice();
    }

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

    // The grid tells only a change, so each call is one the Consumer hears. An edit the destroyed grid discards is not
    // told: the Sheet is gone, and a handler that asked it anything would be refused.
    readonly #onEditingChanged = (open: boolean): void => {
        this.#editing = open;
        if (this.#destroyed) return;
        // An edit ended while another Sheet than its own is shown, cancelled or discarded, shows its own again, where
        // its cell is (ADR-0331 §5). A commit has shown it already (#onEdit).
        if (!open) this.#returnToEditHome();
        this.#paintToolbar();
        this.#options.onEditingChanged?.(open);
    };

    // Made again when the declared tables change, a Reference to a column being coloured only once its table is
    // declared, and with each Sheet shown: the grid asks again of a new function (ADR-0057, ADR-0331 §5).
    #referencesIn = this.#referencesOnSheetShown();

    #referencesOnSheetShown(): (text: string) => readonly GridEditorReference[] {
        return text => sheetReferences(this.#source.sheet, text, this.#editHome);
    }

    // The Reference Point writes: bare on the Sheet the Formula is typed on, named with its Sheet on another (ADR-0331 §5).
    readonly #referenceText = (range: SelectionRange): string => sheetReferenceTextOn(range, this.#source.sheet, this.#editHome);

    // The Sheet the open edit was opened on, once another Sheet has been shown while it is typed (ADR-0331 §5): its
    // Formula is committed there, and the edit's end shows it again.
    #editHome: Sheet | undefined;
    // The Linked Tables offered are the Workbook's, whichever of its Sheets is shown while the Formula is typed.
    readonly #completeEditorText = (text: string, caret: number): EditorCompletion | undefined => sheetCompletion(this.#source.sheet, text, caret);

    // The colours the grid gave the keys of the Formula being edited, as the Linked Table columns they name
    // (ADR-0057): told to the Consumer when they differ from what it was last told.
    readonly #onKeyColoursChanged = (keys: readonly ReferenceKeyColour[]): void => {
        const columns = linkedColumnsOf(this.#source.sheet, keys);
        if (sameLinkedColumns(columns, this.#linkedColumnsTold)) return;
        this.#linkedColumnsTold = columns;
        this.#joinedScope?.outlineLinkedColumns(this.#pointingSheet, columns);
        this.#options.onLinkedColumnColoursChanged?.(columns);
    };

    // ---- The Pointing Scope (ADR-0058) -------------------------------------------------------------------------

    // What the Scope asks of this Sheet: its declared tables, and its edit to write into. The TS grid keeps a press
    // on another grid in its place among this Sheet's keys by the browser's own event order, so no root is named.
    readonly #pointingSheet: PointingSheet = pointingSheet(() => this.#source.sheet.linkedTables, {
        writePointedText: async text => this.#grid !== undefined && this.#grid.writePointedText(text),
        takeBackPointedText: async () => this.#grid !== undefined && this.#grid.takeBackPointedText(),
        tellPointingRefused: async refusal => { this.#options.onPointingRefused?.(refusal); },
    });

    // Joins the Scope given, leaving one no longer given.
    #bindPointingScope(): void {
        const scope = this.#options.pointingScope;
        if (scope === this.#joinedScope) return;
        this.#joinedScope?.leave(this.#pointingSheet);
        this.#scopeHeardPoints = false;
        this.#joinedScope = scope;
        if (scope === undefined) return;
        scope.join(this.#pointingSheet);
        // The columns an edit already open reads are outlined in the new Scope's grids.
        scope.outlineLinkedColumns(this.#pointingSheet, this.#linkedColumnsTold);
        this.#tellScopeWhetherPointing();
    }

    // Tells the Scope whether this Sheet points, when that changed: it holds the keyboard and its edit is in Point.
    #tellScopeWhetherPointing(): void {
        const scope = this.#joinedScope;
        if (scope === undefined) return;
        const points = this.#holdsKeyboard && this.#pointState !== 'none';
        if (points === this.#scopeHeardPoints) return;
        this.#scopeHeardPoints = points;
        scope.points(this.#pointingSheet, points);
    }

    readonly #onPointStateChanged = (state: GridPointState): void => {
        this.#pointState = state;
        if (this.#destroyed) return;
        this.#tellScopeWhetherPointing();
        // Whether or not this Sheet holds the keyboard: the dashes go once Point over what a press wrote has ended.
        this.#joinedScope?.pointStateChanged(this.#pointingSheet, state);
    };

    // The arrows point inside the grid the press landed on (ADR-0058, "The keyboard").
    readonly #onPointArrow = (arrow: GridPointArrow): void => {
        void this.#joinedScope?.pointArrow(this.#pointingSheet, arrow);
    };

    // The keyboard coming into the Sheet, or leaving it: a move inside it, the grid's root to the editor as `=`
    // opens it, leaves it inside.
    readonly #onKeyboardIn = (): void => {
        this.#holdsKeyboard = true;
        this.#tellScopeWhetherPointing();
    };

    readonly #onKeyboardOut = (event: FocusEvent): void => {
        const to = event.relatedTarget;
        this.#holdsKeyboard = to instanceof Node && this.#element.contains(to);
        this.#tellScopeWhetherPointing();
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
        if (!tabs && this.#tabs.area.isConnected) {
            // The faces a Chrome drew go with the band, and are drawn again when it is shown.
            this.#tabs.dispose();
            this.#tabs.area.remove();
        }
        if (!toolbar && this.#toolbar.band.isConnected) {
            // A band taken away takes its KeyTips and an item's list with it, and while it holds the keyboard hands it
            // back first, so the next key is not the page's.
            this.#toolbar.dropKeyTips();
            this.#endChoices();
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
            this.#forgetTabGesturesGone();
            this.#tabs.paint({
                sheets: this.#workbook.sheets, shown: this.#source.sheet, heightPx: this.#tabsHeightPx,
                listName: this.#words.sheetTabsName, addName: this.#words.newSheetName,
                tabFace: this.#options.chrome?.sheetTab, addFace: this.#options.chrome?.sheetTabAdd,
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
        const toolbar = options.showToolbar === true ? SheetToolbar.heightPx(toolbarRowsOf(options).length, SheetView.#bandRowPxOf(options)) : 0;
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
        // What a list a Chrome draws stands over from here, whether the band's keys or a press opens it.
        this.#chromeListsOver = this.#grid?.readView().selection;
        const rows = this.#toolbarRows();
        this.#toolbar.paint({ name: this.#words.toolbarName, rowHeightPx: this.#bandRowPx, rows, itemSlot: this.#options.chrome?.toolbarItem });
    }

    // The rows the options give, the default one Excel's Home tab's commands in its order (ADR-0100), each item's state
    // the Focus cell's as the grid holds it now. While an edit is open every item is unavailable, the Consumer's
    // included, and says why.
    #toolbarRows(): SheetToolbarPaint['rows'] {
        const focus = this.#grid?.readView().selection.focus;
        const format = focus === undefined ? undefined : this.#source.sheet.getCellFormat(new CellAddress(focus.row, focus.column));
        const words = this.#words;
        const item = (key: SheetToolbarCommand, icon: SheetToolbarIcon | undefined, act: ((focused: CellFormat) => CellFormatChange) | 'formatCells' | undefined,
            { pressed, shortcut, disabledReason, list, text, swatch }: {
                readonly pressed?: boolean; readonly shortcut?: string; readonly disabledReason?: string;
                // A split control's list, or a menu's, which has no `act`.
                readonly list?: () => readonly SheetToolbarChoice[]; readonly text?: string; readonly swatch?: CellColour;
            } = {}): SheetToolbarItem => {
            const name = words.toolbarItem(key);
            const kind = list === undefined ? (pressed === undefined ? 'button' : 'toggle') : (act === undefined ? 'menu' : 'split');
            return {
                key, kind, name, icon, pressed: pressed === true, text, content: undefined,
                tooltip: shortcut === undefined ? name : words.toolbarItemWithShortcut(name, shortcut),
                swatch: swatch === undefined ? undefined : paintOf(swatch),
                moreName: kind === 'split' ? words.toolbarMore(name) : undefined,
                // An open edit is the reason over any other, as the C# Sheet's.
                disabledReason: this.#editing ? words.toolbarWhileEditing : disabledReason,
                press: act === undefined ? undefined : () => { this.#toolbarPressed(act); },
                open: list === undefined ? undefined : (keyTipRow: number | undefined) => { this.#openChoices(name, list(), keyTipRow); },
                list,
                keyTip: toolbarKeyTips[key],
            };
        };
        const key = (kind: SheetFormatKey) => (focused: CellFormat): CellFormatChange => sheetFormatChangeFor(kind, focused, this.#source.sheet.culture);
        const culture = this.#source.sheet.culture;
        const shown = format === undefined ? 'general' : this.#categoryOf(format.numberFormat, culture);
        const fontColour = this.#faceFontColour;
        const fill = this.#faceFill;
        const border = this.#faceBorder;
        const commands: Readonly<Record<SheetToolbarCommand, SheetToolbarItem>> = {
            bold: item('bold', 'bold', key('bold'), { pressed: format?.font.bold === true, shortcut: 'Ctrl+B' }),
            italic: item('italic', 'italic', key('italic'), { pressed: format?.font.italic === true, shortcut: 'Ctrl+I' }),
            underline: item('underline', 'underline', key('underline'), { pressed: format?.font.underline === true, shortcut: 'Ctrl+U' }),
            strikethrough: item('strikethrough', 'strikethrough', key('strikethrough'), { pressed: format?.font.strikethrough === true, shortcut: 'Ctrl+5' }),
            fontColour: item('fontColour', 'fontColour', () => new CellFormatChange({ fontColour }), {
                swatch: fontColour,
                list: () => this.#colourChoices(words.toolbarAutomatic,
                    format === undefined || format.font.colour.isAutomatic ? undefined : format.font.colour,
                    chosen => {
                        // Taking the colour away is not kept: the face goes on setting the last colour chosen.
                        if (!chosen.isAutomatic) this.#faceFontColour = chosen;
                        return new CellFormatChange({ fontColour: chosen });
                    }),
            }),
            fill: item('fill', 'fill', () => new CellFormatChange({ fill: CellFill.solid(fill) }), {
                swatch: fill,
                list: () => this.#colourChoices(words.toolbarNoFill, format?.fill.colour, chosen => {
                    if (chosen.isAutomatic) return new CellFormatChange({ fill: CellFill.none });
                    this.#faceFill = chosen;
                    return new CellFormatChange({ fill: CellFill.solid(chosen) });
                }),
            }),
            borders: item('borders', 'borders', () => new CellFormatChange({ borders: borderPresets[border] }), {
                list: () => toolbarBorders.map(preset => this.#choice(words.toolbarBorder(preset), () => {
                    this.#faceBorder = preset;
                    this.#formatFromFocus(() => new CellFormatChange({ borders: borderPresets[preset] }), widensAsAHomeTabButtonDoes);
                })),
            }),
            alignLeft: item('alignLeft', 'alignLeft', align('left'), { pressed: format?.alignment === 'left' }),
            alignCenter: item('alignCenter', 'alignCenter', align('center'), { pressed: format?.alignment === 'center' }),
            alignRight: item('alignRight', 'alignRight', align('right'), { pressed: format?.alignment === 'right' }),
            numberFormat: item('numberFormat', undefined, undefined, {
                text: words.categoryName(shown),
                list: () => this.#numberFormatChoices(shown),
            }),
            percent: item('percent', 'percent', key('percent'), { shortcut: 'Ctrl+Shift+%' }),
            // Excel's Comma Style pads with `*`, as Accounting does, which ExSheet does not read: shown, and disabled
            // with the reason, rather than set as something else under Excel's name.
            comma: item('comma', 'comma', undefined, { disabledReason: words.commaStyleUnread }),
            formatCells: item('formatCells', 'formatCells', 'formatCells', { shortcut: 'Ctrl+1' }),
        };
        return toolbarRowsOf(this.#options).map((row, r) => {
            const { label, keyTip, items }: SheetToolbarRowOptions = row === 'default' ? { label: words.toolbarHomeRow, keyTip: 'H', items: defaultToolbarRow } : row;
            return {
                label,
                keyTip: keyTip?.toUpperCase(),
                // Each item's key is its place, so the item the keyboard is on stays named while the rows stay as they are.
                items: items.map((entry, i): SheetToolbarItem | 'separator' => {
                    if (entry === 'separator') return entry;
                    if (typeof entry === 'string') return { ...commands[entry], key: `${String(r)}-${entry}` };
                    return this.#consumerItem(entry, `${String(r)}-${String(i)}`);
                }),
            };
        });
    }

    // A Consumer's own button (ADR-0100): pressed as ExSheet's items are, and unavailable while an edit is open.
    #consumerItem(button: SheetToolbarButton, key: string): SheetToolbarItem {
        return {
            key, kind: 'button', name: button.text, tooltip: button.text, icon: undefined,
            text: button.content === undefined ? button.text : undefined, content: button.content, swatch: undefined,
            moreName: undefined, pressed: false,
            disabledReason: this.#editing ? this.#words.toolbarWhileEditing : button.disabledReason,
            // An edit opened since the last pass refuses the press, as ExSheet's own items do, whatever that pass showed.
            press: () => {
                if (this.#editing) this.#tell(this.#words.toolbarWhileEditing);
                else button.onPress();
            },
            open: undefined,
            list: undefined,
            keyTip: button.keyTip?.toUpperCase(),
        };
    }

    #categoryOf(format: NumberFormat, culture: Culture): NumberFormatCategory {
        const known = this.#shownCategory;
        if (known?.format === format && known.culture === culture) return known.category;
        const category = recogniseNumberFormat(format, culture).category;
        this.#shownCategory = { format, culture, category };
        return category;
    }

    // A choice of a Toolbar Item's list that sets a format, as the item's face does.
    #choice(label: string, choose: () => void, { swatch, selected = false, disabledReason, opensPopover = false }: {
        readonly swatch?: CellColour; readonly selected?: boolean; readonly disabledReason?: string; readonly opensPopover?: boolean;
    } = {}): SheetToolbarChoice {
        return { label, swatch: swatch === undefined ? undefined : paintOf(swatch), selected, disabledReason, opensPopover, choose };
    }

    // A colour control's list: the choice that takes the colour away, then the Office theme's colours and the ten
    // standard ones, as Format Cells offers them, the Focus cell's own marked (`undefined` where it has none). Each
    // sets through the Home tab buttons' widening.
    #colourChoices(takenAway: string, focus: CellColour | undefined, changeOf: (chosen: CellColour) => CellFormatChange): SheetToolbarChoice[] {
        const set = (chosen: CellColour) => (): void => { this.#formatFromFocus(() => changeOf(chosen), widensAsAHomeTabButtonDoes); };
        const swatches = [...formatCellsOffer.themeColours.flat(), ...formatCellsOffer.standardColours];
        return [
            // Automatic, which the Fill's change reads as No Fill.
            this.#choice(takenAway, set(automaticColour()), { selected: focus === undefined }),
            ...swatches.map(swatch => this.#choice(this.#words.swatchName(swatch), set(swatch.colour), {
                swatch: swatch.colour, selected: focus?.equals(swatch.colour) === true,
            })),
        ];
    }

    // The Number Format list: Excel's categories in Excel's order, each setting what Format Cells' Number tab sets when
    // the category is chosen with its defaults. Accounting and Fraction are shown disabled with Format Cells' reasons,
    // Special is not on Excel's ribbon list, and More Number Formats… opens Format Cells.
    #numberFormatChoices(shown: NumberFormatCategory): SheetToolbarChoice[] {
        const culture = this.#source.sheet.culture;
        const choices: SheetToolbarChoice[] = [];
        for (const { category } of formatCellsOffer.categories) {
            if (category === 'special') continue;
            if (category === 'custom') {
                choices.push(this.#choice(this.#words.toolbarMoreNumberFormats, () => { this.#openFormatCells(); }, { opensPopover: true }));
                continue;
            }
            const label = this.#words.categoryName(category);
            if (isDisabledCategory(category)) {
                choices.push(this.#choice(label, () => { throw new Error(`Internal: ${category} cannot be chosen, and the list makes no choice it shows disabled.`); },
                    { disabledReason: this.#words.categoryDisabled(category) }));
                continue;
            }
            const numberFormat = NumberFormat.parse(defaultCodeOf(category, culture));
            choices.push(this.#choice(label, () => {
                this.#formatFromFocus(() => new CellFormatChange({ numberFormat }), widensAsAHomeTabButtonDoes);
            }, { selected: category === shown }));
        }
        return choices;
    }

    // A Toolbar Item's list, in the grid's popover frame (ADR-0100, ADR-0050 item 16), the keyboard on the choice the
    // Focus cell has. A choice closes the frame, which gives the keyboard back to the grid, and is then made; one that
    // opens a popover in the list's place is made first, so what it opens takes the frame, and the list is closed
    // after it only where nothing took its place. The list reads the Selection and the Sheet it was opened over: a
    // Selection moved, another Sheet shown or an operation run ends it, as each ends Format Cells. An edit open at the
    // opening shows nothing, and the notice says why.
    #openChoices(title: string, choices: readonly SheetToolbarChoice[], keyTipRow: number | undefined): void {
        const grid = this.#grid;
        const selection = grid?.readView().selection;
        if (grid === undefined || selection === undefined) return;
        if (this.#editing) {
            this.#tell(this.#words.toolbarWhileEditing);
            return;
        }
        const panel: SheetChoicesPanel = new SheetChoicesPanel(this.#element.ownerDocument, title, choices, {
            chosen: choice => {
                if (this.#choices?.panel !== panel) return;
                if (!choice.opensPopover) this.#endChoices();
                else this.#choices = undefined;
                if (this.#editing) this.#tell(this.#words.toolbarWhileEditing);
                else choice.choose();
                if (this.#gridElement.contains(panel.element)) grid.closePopover();
                // The face sets what was chosen last, though the choice changed nothing.
                this.#paintToolbar();
            },
        });
        this.#endFormatCells();
        this.#endChoices();
        this.#clearNotice();
        if (!grid.openPopover(() => panel.element, title)) return;
        this.#choices = { panel, selection, keyTipRow };
        panel.takeKeyboard();
    }

    // Ends a Toolbar Item's list where it stands: the grid's popover closes and gives the keyboard back. One the grid
    // already closed, by Escape or a press elsewhere, has left its frame, and only the record goes.
    #endChoices(): void {
        // A list a Chrome draws ends with it.
        this.#toolbar.endLists();
        const standing = this.#choices;
        if (standing === undefined) return;
        this.#choices = undefined;
        if (this.#gridElement.contains(standing.panel.element)) this.#grid?.closePopover();
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

    // A press on a tab shows its Sheet; the keyboard stays in the grid, where the next key acts on it. A name being
    // edited on another tab is committed first, and a menu closes.
    readonly #onTabPressed = (sheet: Sheet): void => {
        if (!this.#workbook.sheets.includes(sheet) || this.#refusedSwitch(true)) return;
        if (this.#tabs.renaming === sheet) return;
        this.#leaveTabGestures('grid');
        this.#sayLeftOrClear();
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
        // Shift+F10 and the menu key open the tab's menu, the keyboard coming back to the tab when it closes; taken
        // on the key, so the browser raises no context menu of its own after it.
        if ((event.key === 'F10' && event.shiftKey && !event.ctrlKey && !event.altKey && !event.metaKey) || (event.key === 'ContextMenu' && !event.shiftKey)) {
            this.#openTabMenu(this.#source.sheet, true);
            return true;
        }
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
        if (this.#toolbarKey(event)) return;
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
        if (this.#refusedSwitch(true)) return;
        const sheets = this.#workbook.sheets;
        const sheet = sheets[sheets.indexOf(this.#source.sheet) + step];
        if (sheet === undefined) return;
        this.#clearNotice();
        const onTabs = this.#tabs.holdsKeyboard;
        this.#showSheet(sheet);
        this.#raiseDocument();
        if (onTabs) this.#tabs.focus(sheet);
    };

    // The Sheet Toolbar's keys (ADR-0100), while it is shown: the Alt key alone and F10 start the KeyTips where the grid
    // takes keys, unless an edit is open; Escape in a list a KeyTip opened goes back to its row's KeyTips, as Excel's
    // does (R2 (b)). Answers whether the key was one of them.
    #toolbarKey(event: KeyboardEvent): boolean {
        if (this.#options.showToolbar !== true) return false;
        const target = event.target;
        // While the KeyTips stand every key typed on the band is theirs, a key the Sheet would claim among them.
        if (this.#toolbar.showsKeyTips && target === this.#toolbar.band) return true;
        const choices = this.#choices;
        const keyTipRow = choices?.keyTipRow;
        if (event.key === 'Escape' && keyTipRow !== undefined && target instanceof Node && choices?.panel.element.contains(target) === true) {
            event.preventDefault();
            event.stopPropagation();
            this.#endChoices();
            this.#toolbar.backToKeyTips(keyTipRow);
            return true;
        }
        const plain = !event.ctrlKey && !event.metaKey && !event.shiftKey;
        if (event.key === 'Alt') {
            this.#altAlone = plain && !this.#editing && this.#takesGridKeys(target);
            return this.#altAlone;
        }
        // Any other key between an Alt's press and its release: that Alt is not alone.
        this.#altAlone = false;
        if (event.key !== 'F10' || !plain || event.altKey || this.#editing || !this.#takesGridKeys(target)) return false;
        if (!this.#toolbar.startKeyTips()) return false;
        event.preventDefault();
        event.stopPropagation();
        return true;
    }

    // The Alt key's release, when it went down alone, starts the KeyTips; its default, which would take the keyboard to
    // the browser's menu, is the KeyTips' too.
    readonly #onSheetKeyUp = (event: KeyboardEvent): void => {
        if (event.key !== 'Alt' || !this.#altAlone) return;
        this.#altAlone = false;
        if (this.#options.showToolbar !== true || this.#editing || !this.#takesGridKeys(event.target)) return;
        if (this.#toolbar.startKeyTips()) event.preventDefault();
    };

    // The keyboard leaving where it was between an Alt's press and its release, as Alt+Tab takes it from the page: no
    // release comes, and a later one is not that Alt's.
    readonly #onSheetFocusOut = (): void => { this.#altAlone = false; };

    // A press or a wheel between an Alt's press and its release, as Alt+click and Alt+wheel are: that Alt is not alone.
    readonly #forgetAlt = (): void => { this.#altAlone = false; };

    // Where the Sheet's own keys are heard: in the grid, outside a panel it opened and the Name Box, whose fields keep
    // their own keys, as the C# Sheet's declared keys are.
    #takesGridKeys(target: EventTarget | null): boolean {
        return target instanceof Element && this.#gridElement.contains(target) && target.closest('.ex-popover, .ex-name-box') === null;
    }

    // Excel's formatting keys and its insert and delete keys on the Sheet Toolbar's band (ADR-0071, "Keys"; ADR-0100),
    // whose tooltips name them. Where the grid takes keys they are its declared keys, taken by the grid in their place
    // in the Input Order and heard by #onDeclaredKey (ADR-0050 item 14); the Name Box and a panel the grid opened keep
    // their own. Ctrl+1 waits on Format Cells.
    #onCellKey(event: KeyboardEvent, key: string): void {
        if (key === toolbarToggleKey) {
            this.#toolbarToggleKey(event);
            return;
        }
        const format = sheetFormatKeyOf(key);
        const structure = format === undefined ? sheetStructureKeyOf(key) : undefined;
        if (format === undefined && structure === undefined) return;
        const target = event.target;
        // Ctrl+1 inside Format Cells opens nothing more, and is not the browser's, whose Ctrl+1 leaves the page.
        if (format === 'formatCells' && target instanceof Element && target.closest('.ex-format-cells, .ex-format-cells-slot') !== null) {
            event.preventDefault();
            return;
        }
        // The band leaves Ctrl's keys to the Sheet.
        if (target !== this.#toolbar.band) return;
        event.preventDefault();
        event.stopPropagation();
        if (format !== undefined) this.#formatKey(format);
        else if (structure !== undefined) this.#structureKey(structure);
    }

    // A formatting key or an insert or delete key, taken by the grid where it holds the keyboard, on the Selection it
    // held at the key (ADR-0050 item 14).
    readonly #onDeclaredKey = (press: GridDeclaredKeyPress): void => {
        const format = sheetFormatKeyOf(press.key);
        if (format !== undefined) {
            this.#formatKey(format);
            return;
        }
        const structure = sheetStructureKeyOf(press.key);
        if (structure === undefined) throw new Error(`Internal: '${press.key}' is not one of the keys the Sheet declares.`);
        this.#structureKey(structure);
    };

    // Ctrl+F1, claimed only where the Consumer hears it: the toolbar's other state is told, and the Consumer shows it.
    #toolbarToggleKey(event: KeyboardEvent): void {
        const changed = this.#options.onShowToolbarChanged;
        const target = event.target;
        if (changed === undefined || (target !== this.#toolbar.band && !this.#takesGridKeys(target))) return;
        event.preventDefault();
        event.stopPropagation();
        changed(this.#options.showToolbar !== true);
    }

    // A formatting key sets its change on every range of the Selection as one step, its toggle's direction the Focus
    // cell's (case 17), widening the columns a number no longer fits where the key widens (ADR-0071). While an edit
    // is open it changes nothing, and the notice says why.
    #formatKey(kind: SheetFormatKey): void {
        if (this.#editing) {
            const refusal = new SheetRefusal('editIsOpen', this.#words.formatKeyWhileEditing);
            this.#tell(refusal.message);
            this.#options.onFormatKeyRefused?.(refusal);
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
    // the columns a number no longer fits where `widens` says the gesture does (ADR-0071, ADR-0100); false over no
    // Selection, where `changeOf` is not asked.
    #formatFromFocus(changeOf: (focus: CellFormat) => CellFormatChange, widens: (change: CellFormatChange) => boolean): boolean {
        const selection = this.#grid?.readView().selection;
        const focus = selection?.focus;
        if (selection === undefined || focus === undefined) return false;
        const change = changeOf(this.#source.sheet.getCellFormat(new CellAddress(focus.row, focus.column)));
        if (change.isEmpty) throw new RangeError('The change sets no part of a Cell Format.');
        const cells = selection.ranges.map(rangeOf);
        this.#perform(SheetEdit.setCellFormat(cells, change), undefined, widens(change) ? cells : undefined);
        return true;
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
        const callbacks = {
            ok: () => this.#formatCellsOk(panel),
            cancel: () => { if (this.#formatCells?.panel === panel) this.#endFormatCells(); },
            tabShown: (tab: FormatCellsTab) => { this.#formatCellsTab = tab; },
        };
        const mount = this.#options.chrome?.formatCells;
        const panel: FormatCellsPanel | SlottedFormatCells = mount === undefined
            ? new FormatCellsPanel(this.#element.ownerDocument, draft, this.#words, this.#formatCellsTab, callbacks)
            : new SlottedFormatCells(this.#element.ownerDocument, mount);
        this.#endFormatCells();
        this.#clearNotice();
        const sheet = this.#source.sheet;
        const tab = this.#formatCellsTab;
        const words = this.#words;
        const opened = grid.openPopover(context => {
            if (panel instanceof SlottedFormatCells) {
                panel.mount(Object.freeze({
                    draft, tab, tabShown: callbacks.tabShown, ok: callbacks.ok, cancel: callbacks.cancel, words,
                    takeKeyboard: (control: HTMLElement) => { if (panel.live) this.#grid?.handKeyboardTo(control, { preventScroll: true }); },
                    innerPopupChanged: (open: boolean) => { if (panel.live) context.innerPopupChanged(open); },
                }));
            }
            return panel.element;
        }, words.formatCellsTitle, panel instanceof SlottedFormatCells ? () => { panel.dispose(); } : undefined);
        if (opened) this.#formatCells = { panel, draft, sheet, selection, ranges };
    }

    // Format Cells' OK (ADR-0071, SH-45): the parts the user touched, set on the ranges it was opened over as one undo
    // step, widening the columns whose numbers no longer fit where a Number Format or any part of the Font is among
    // them; with nothing touched nothing is set and no step recorded. A draft holding a refusal sets nothing and
    // stays open, for the panel to show why. It is taken against the Sheet it was opened over: shown another, which
    // ends it, an OK that still arrives sets nothing (the spine's sixth rule).
    #formatCellsOk(panel: FormatCellsPanel | SlottedFormatCells): boolean {
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

    // ---- Find Within (ADR-0332 §5, ADR-0331 §8) -------------------------------------------------------------------

    // Where the panel searches, as it last chose: kept across a close and a reopen, as the grid keeps the text.
    #findWithin: SheetFindWithin = 'sheet';
    // Every answer that changes what the panel says moves this on: a step asked, the grid's Replace Request, a choice
    // of Within. The Sheet's own Replace All's outcome is said while nothing has moved it since, and the panel has not
    // been mounted again.
    #findEvents = 0;
    #ownFind: { readonly outcome: FindOutcome; readonly events: number } | undefined;
    readonly #findListeners = new Set<() => void>();

    #findMoved(): void {
        this.#findEvents += 1;
        if (this.#ownFind === undefined) return;
        this.#ownFind = undefined;
        this.#tellFind();
    }

    #tellFind(): void {
        // A listener may stop hearing as it hears.
        for (const listener of Array.from(this.#findListeners)) listener();
    }

    #sayOwnFind(outcome: FindOutcome): void {
        this.#ownFind = { outcome, events: this.#findEvents };
        this.#tellFind();
    }

    // The find panel's contents, made once with the Sheet, so the grid sees no change of Chrome.
    readonly #findPanel = sheetFindPanelSlot({
        within: () => this.#findWithin,
        // A choice of Within is a click taken in its place in the Input Order (ADR-0240): a step numbered before it
        // searches where the panel searched then, one numbered after it where it searches now.
        chooseWithin: within => {
            this.#findWithin = within;
            this.#findMoved();
        },
        // Over the Sheet, the grid's Replace Request; over the Workbook, the Sheet's own.
        replaceAll: context => {
            if (this.#findWithin === 'workbook') this.#replaceAllInWorkbook(context);
            else context.replaceAll();
        },
        outcome: context => {
            const own = this.#ownFind;
            return own !== undefined && own.events === this.#findEvents ? findOutcomeText(own.outcome, context.label) : context.view().outcome;
        },
        subscribe: listener => {
            this.#findListeners.add(listener);
            return () => { this.#findListeners.delete(listener); };
        },
        mounted: () => { this.#ownFind = undefined; },
    });

    /**
     * One Find step (ADR-0055, ADR-0332 §5), over the Sheet shown or over the Workbook. A match on the Sheet the step
     * was asked of is the answer. A match on another Sheet shows that Sheet with the cell selected and taken as the
     * current match, handed to the grid under the Sheet's rows, and the step is given up: its answer could name only a
     * place among the rows it was asked about. While an edit is open no other Sheet is shown, and the notice says why.
     */
    readonly #findStepOf = (sheet: Sheet, request: GridFindRequest): Promise<GridFindResult> => {
        this.#findMoved();
        // A step asked of the Sheet shown before the last one searches that Sheet alone: the Workbook is walked from
        // the Sheet shown.
        if (this.#findWithin === 'sheet' || sheet !== this.#source.sheet) return Promise.resolve(sheetFindStep(sheet, request));
        const found = sheetFindStepWorkbook(this.#workbook.sheets, sheet, request);
        if (found === undefined) return Promise.resolve({ found: false });
        const column = request.columns[found.position];
        if (column === undefined) throw new Error(`Internal: no column stands at ${String(found.position)} of the request.`);
        if (found.sheet === sheet) return Promise.resolve({ found: true, row: found.row, column });
        if (this.#refusedSwitch()) return Promise.reject(new DOMException('The match is on another Sheet, which is not shown while an edit is open.', 'AbortError'));
        // Shown in the step's own turn, after the grid has taken the step as out, so the answer it gives up is this one.
        return Promise.resolve().then(() => {
            if (this.#destroyed) throw new DOMException('The Sheet was destroyed.', 'AbortError');
            this.#clearNotice();
            this.#showSheet(found.sheet, { row: found.row, column: found.position });
            this.#raiseDocument();
            throw new DOMException('The match is on another Sheet, now shown with it selected.', 'AbortError');
        });
    };

    /**
     * Replace All over the Workbook (ADR-0332 §5), from the panel's button in its place in the Input Order: every
     * cell of every Sheet whose Entry matches, all or nothing, as one step on the undo stack, which shows the Sheet it
     * was done from — refused whole when any replaced Formula cannot be read, naming each such cell by its Sheet —
     * and the count said. With nothing matched, the panel says no match.
     */
    #replaceAllInWorkbook(context: GridFindPanelSlotContext): void {
        this.#findMoved();
        const view = context.view();
        if (!view.replaceShown || view.replaceWaiting || view.text === '') return;
        if (this.#editing) {
            this.#tell(this.#words.editIsOpen);
            return;
        }
        const pattern: GridFindPattern = { text: view.text, matchCase: view.matchCase, wholeCell: view.wholeCell };
        const plan = planWorkbookReplace(this.#workbook.sheets, pattern, view.replaceText, (sheet, address, text) => {
            const verdict = new SheetRow(sheet, address.row).judge(text, error => this.#words.formulaUnreadable(error));
            return verdict.kind === 'reject' ? verdict.message : undefined;
        });
        if (plan.refused.length > 0) {
            this.#sayOwnFind({ kind: 'replaceRefused', cells: plan.refused.map(cell => refusedCellOf(cell, cell.reason)) });
            return;
        }
        if (plan.writes.length === 0) {
            this.#sayOwnFind({ kind: 'notFound' });
            return;
        }
        this.#clearNotice();
        const from = this.#source.sheet;
        const bySheet = new Map<Sheet, CellAddress[]>();
        for (const write of plan.writes) {
            const cells = bySheet.get(write.sheet);
            if (cells === undefined) bySheet.set(write.sheet, [write.address]);
            else cells.push(write.address);
        }
        const steps: WorkbookStep[] = [];
        try {
            // One engine step across the Sheets (one change, ADR-0180).
            steps.push(this.#workbook.doAll([...bySheet.keys()].map(sheet => [sheet.name,
                SheetEdit.enterMany(plan.writes.filter(write => write.sheet === sheet).map(write => [write.address, write.text] as const))] as const)));
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the engine's refusal and an unreadable Formula are answers told to the user; anything else is rethrown
            const message = error instanceof FormulaSyntaxError ? this.#words.formulaUnreadable(error)
                : error instanceof SheetRefusedError ? this.#words.refused(error.refusal) : undefined;
            if (message === undefined) throw error;
            this.#tell(message);
            this.#sayOwnFind({ kind: 'replaceRefused', cells: plan.writes.map(write => refusedCellOf(write, message)) });
            return;
        }
        // The widths a typed number widens its column to, as typing it would (ADR-0047), in the same undo step.
        this.#resizing = undefined;
        for (const [sheet, addresses] of bySheet) {
            for (const [column, characters] of this.#columns.widenedBy(sheet, addresses.map(address => new CellRange(address)))) {
                steps.push(this.#workbook.do(sheet.name, SheetEdit.setAutomaticColumnWidth(CellRange.wholeColumns(column, column), characters)));
            }
        }
        this.#history.record(steps, from);
        this.#changed(steps.map(step => step.change));
        this.#sayOwnFind({ kind: 'replaced', count: plan.writes.length });
    }

    // ---- The Context Menu (ADR-0036) ----------------------------------------------------------------------------

    // The grid's commandLabel: the Consumer's word for an id first, the empty string that silences a refusal among
    // them, then the Sheet's word for one of its own ids, and otherwise none, so the grid words the rest. A new
    // function only when the words or the Consumer's commandLabel change, so the grid words its menu again then and
    // only then.
    #commandLabel: (id: string) => string | undefined;

    static #commandLabelOf(words: SheetWords, consumer: ((id: string) => string | undefined) | undefined): (id: string) => string | undefined {
        if (consumer === undefined) return id => sheetCommandWord(words, id);
        return id => {
            const given: unknown = consumer(id);
            if (given !== undefined && typeof given !== 'string') throw new TypeError(`commandLabel answers a word or undefined, not ${typeof given} (ADR-0036).`);
            return given ?? sheetCommandWord(words, id);
        };
    }

    // Inserting and deleting the rows or columns the Selection spans, and Format Cells (ADR-0071), after the grid's
    // clipboard commands, as C#'s ContextCommandsFor adds them.
    readonly #contextCommands = (context: GridContextMenuContext<SheetRow>): readonly GridCommand[] => sheetContextCommands(context.selection, {
        structure: edit => { this.#structureCommand(edit); },
        formatCells: () => { this.#formatCellsByUser(); },
    });

    // A structural command from the menu: one step on the undo stack; refused while an edit is open, or by the
    // engine, it changes nothing and the notice says why (ADR-0048).
    #structureCommand(edit: SheetEdit): void {
        if (this.#editing) {
            this.#tell(this.#words.editIsOpen);
            return;
        }
        try {
            this.#perform(edit);
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the engine's refusal is an answer told to the user; anything else is rethrown
            if (!(error instanceof SheetRefusedError)) throw error;
            this.#tell(this.#words.refused(error.refusal));
        }
    }

    // Format Cells chosen by the user, from the menu: refused while an edit is open, and said (ADR-0071).
    #formatCellsByUser(): void {
        if (this.#editing) {
            this.#tell(this.#words.editIsOpen);
            return;
        }
        this.#openFormatCells();
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

    // While an edit is open another Sheet is shown only to point at its cells: with `pointing`, while the edit's text is
    // a Formula, as Excel shows one (ADR-0331 §5), noting the Sheet the edit is typed on. Otherwise it is not, and the
    // notice says why. The text is read from the grid, as the key or press being answered left it. Whether it refused.
    #refusedSwitch(pointing = false): boolean {
        const text = this.#grid?.readEditText();
        if (text === undefined && !this.#editing) return false;
        if (pointing && text?.startsWith('=') === true) {
            this.#editHome ??= this.#source.sheet;
            return false;
        }
        this.#tell(this.#words.sheetSwitchWhileEditing);
        return true;
    }

    // The edit has ended: where another Sheet than the one it was typed on is shown, that one is shown again, where its
    // cell is (ADR-0331 §5).
    #returnToEditHome(): void {
        const home = this.#editHome;
        if (home === undefined) return;
        this.#editHome = undefined;
        if (home !== this.#source.sheet && this.#workbook.sheets.includes(home)) {
            this.#showSheet(home);
            this.#raiseDocument();
        } else {
            // The References on the Sheet shown are all its own again.
            this.#referencesIn = this.#referencesOnSheetShown();
            this.#grid?.update(this.#gridOptions());
        }
    }

    // A new, empty Sheet beside the one shown, named as Excel names it, and shown, as Excel shows the Sheet it adds:
    // one step on the undo stack, whose undo shows the Sheet it was added from (ADR-0331 §3).
    #addSheet(before: boolean): void {
        if (this.#refusedSheetOperation()) return;
        // A name being edited is committed first, the keyboard going back to the grid as the field goes; the keyboard
        // on a tab itself stays on the tab list.
        const onTabs = this.#tabs.holdsKeyboard && this.#tabs.renaming === undefined && !this.#tabs.menuOpen;
        this.#leaveTabGestures('grid');
        const from = this.#source.sheet;
        this.#resizing = undefined;
        const step = this.#workbook.do(from.name, SheetEdit.insertSheet(undefined, before));
        this.#history.record([step], from);
        this.#showSheet(step.sheet);
        this.#raiseDocument();
        // The tabs moved to make room lose the keyboard: one on the list goes to the new Sheet's tab, selected now.
        if (onTabs) this.#tabs.focus(step.sheet);
        this.#say(this.#words.sheetAdded(step.sheet.name), { outlivesMove: true });
    }

    // ---- What the tabs do besides showing a Sheet (ADR-0331 §4; exsheet-workbook ticket 06) ---------------------
    //
    // A double click edits a tab's name in place, the tab's menu — a secondary press, Shift+F10 or the menu key —
    // deletes, moves left or right, copies and renames, and a drag moves a tab. Each is one step on the one undo stack
    // (ADR-0331 §3), said in the notice. While an edit is open none is done, and the notice says why. Each is answered
    // within the input that made it, so a key typed straight after it acts where it left the Sheet and the keyboard.

    // The name typed on its tab, the edit's session, whether it was begun from the keyboard on the tab list (Enter and
    // Escape give the keyboard back to the tab, where it came from), and whether it is ending.
    #renameText = '';
    #renameSession = 0;
    #renameFromTab = false;
    #renameEnding = false;
    // The tab's menu: the Sheet it acts on, the command the keyboard is on, whether the keyboard came from that
    // Sheet's tab, and the menu's session.
    #menuSheet: Sheet | undefined;
    #menuActive: SheetTabCommand = 'rename';
    #menuFromTab = false;
    #menuSession = 0;
    // What ending a gesture on the tabs said — a name committed, or refused and kept — when the operation that ended
    // it says something of its own: both are said, the first not lost under the second.
    #saidOnLeaving: string | undefined;
    // What committing or refusing the name last said, so leaving the edit keeps only what the edit itself said.
    #renameSaid: string | undefined;

    // Any open edit refuses a Sheet operation, and the notice says why.
    #refusedSheetOperation(): boolean {
        if (!this.#editing) return false;
        this.#tell(this.#words.sheetOperationWhileEditing);
        return true;
    }

    // Where the keyboard goes as a gesture on the tabs ends: nowhere, where the input that ends it hands it on itself;
    // the grid; or the tab it came from.
    #keyboardTo(to: 'nowhere' | 'grid' | 'tab', sheet: Sheet): void {
        if (to === 'grid') this.#grid?.focus();
        else if (to === 'tab' && this.#workbook.sheets.includes(sheet)) this.#tabs.focus(sheet);
        else if (to === 'tab') this.#grid?.focus();
    }

    // A name being edited is committed — or, refused, kept as it was — and a menu closes, before another gesture on the
    // tabs. What the edit's end said is kept, and said with what the operation says next.
    #leaveTabGestures(keyboard: 'nowhere' | 'grid'): void {
        this.#saidOnLeaving = undefined;
        if (this.#tabs.renaming !== undefined) {
            this.#renameSaid = undefined;
            if (!this.#commitRename(keyboard)) this.#endRename(keyboard);
            this.#saidOnLeaving = this.#renameSaid;
        }
        if (this.#menuSheet !== undefined) this.#closeTabMenu(keyboard);
    }

    // Says `sentence` after what ending a gesture on the tabs said, if it said anything.
    #say(sentence: string, options?: { readonly outlivesMove?: boolean }): void {
        const said = this.#saidOnLeaving;
        this.#saidOnLeaving = undefined;
        this.#tell(said === undefined ? sentence : `${said} ${sentence}`, options);
    }

    // For an operation that says nothing of its own: what ending a gesture on the tabs said stands, else the notice is
    // cleared.
    #sayLeftOrClear(): void {
        const said = this.#saidOnLeaving;
        this.#saidOnLeaving = undefined;
        if (said === undefined) this.#clearNotice();
        else this.#tell(said);
    }

    // Edits `sheet`'s name on its tab, its whole name selected and the keyboard on the field, so the keys typed straight
    // after it are the field's.
    #beginRename(sheet: Sheet, fromTab: boolean): void {
        if (this.#refusedSheetOperation() || !this.#workbook.sheets.includes(sheet) || this.#tabs.renaming === sheet) return;
        this.#leaveTabGestures('nowhere');
        if (sheet !== this.#source.sheet) {
            this.#sayLeftOrClear();
            this.#showSheet(sheet);
            this.#raiseDocument();
        }
        this.#renameText = sheet.name;
        this.#renameFromTab = fromTab;
        this.#renameSession = this.#tabs.beginRename(sheet, sheet.name, this.#words.sheetNameField);
    }

    // Commits the name typed: one step, every Formula naming the Sheet rewritten (ADR-0331 §4). A name Excel's rules
    // refuse is refused by name, and the field stays, holding the keyboard. Answers whether the edit ended.
    #commitRename(keyboard: 'nowhere' | 'grid' | 'tab'): boolean {
        const sheet = this.#tabs.renaming;
        if (sheet === undefined || this.#renameEnding) return true;
        const name = this.#renameText;
        if (!this.#workbook.sheets.includes(sheet) || name === sheet.name) {
            this.#endRename(keyboard);
            return true;
        }
        const reason = Sheet.whyNotAName(name);
        if (reason !== undefined) {
            this.#refuseRename(reason);
            return false;
        }
        const from = sheet.name;
        let step: WorkbookStep;
        try {
            step = this.#workbook.do(from, SheetEdit.rename(name));
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the engine's refusal is an answer told to the user; anything else is rethrown
            if (!(error instanceof SheetRefusedError)) throw error;
            this.#refuseRename(error.refusal.message);
            return false;
        }
        this.#history.record([step], sheet);
        this.#endRename(keyboard);
        // A rename changes the Sheets: #changed draws the tabs again and raises the document, once.
        this.#changed([step.change]);
        this.#renameSaid = this.#words.sheetRenamed(from, sheet.name);
        this.#tell(this.#renameSaid);
        return true;
    }

    #refuseRename(reason: string): void {
        this.#tabs.markRenameRefused(true);
        this.#renameSaid = this.#words.sheetNotRenamed(reason);
        this.#tell(this.#renameSaid);
    }

    // Ends the name's edit, the keyboard going `keyboard` while the field still holds it, and the tab drawn again.
    #endRename(keyboard: 'nowhere' | 'grid' | 'tab'): void {
        const sheet = this.#tabs.renaming;
        if (sheet === undefined || this.#renameEnding) return;
        this.#renameEnding = true;
        // The edit is over before the keyboard moves, so the field's losing it ends nothing again.
        this.#renameSession = -1;
        // To the grid while the field still holds it; to the tab once it is drawn again in the field's place.
        if (keyboard === 'grid') this.#keyboardTo('grid', sheet);
        this.#tabs.endRename();
        this.#renameEnding = false;
        this.#renameFromTab = false;
        if (keyboard === 'tab') this.#keyboardTo('tab', sheet);
    }

    // Opens `sheet`'s menu, showing the Sheet, with the keyboard on its first command that can be done. A menu already
    // open for the Sheet stays as it is.
    #openTabMenu(sheet: Sheet, fromTab: boolean): void {
        if (this.#menuSheet === sheet || this.#refusedSheetOperation() || !this.#workbook.sheets.includes(sheet)) return;
        this.#leaveTabGestures('nowhere');
        if (sheet !== this.#source.sheet) {
            this.#sayLeftOrClear();
            this.#showSheet(sheet);
            this.#raiseDocument();
        }
        this.#menuSheet = sheet;
        this.#menuFromTab = fromTab;
        const first = tabCommandOrder.find(command => this.#tabCommandRefusal(command, sheet) === undefined);
        if (first === undefined) throw new Error('Internal: Rename can always be done on a Sheet of the Workbook.');
        this.#menuActive = first;
        this.#menuSession = this.#paintTabMenu(sheet);
    }

    // The menu's commands as they stand: which can be done on its Sheet, and which the keyboard is on.
    #paintTabMenu(sheet: Sheet): number {
        return this.#tabs.showMenu({
            name: this.#words.sheetMenuName(sheet.name),
            active: this.#menuActive,
            items: tabCommandOrder.map(command => ({
                command, label: this.#words.tabCommand(command), disabledReason: this.#tabCommandRefusal(command, sheet),
            })),
        });
    }

    // Why `command` cannot be done on `sheet` in the Workbook as it stands now, or `undefined` where it can.
    #tabCommandRefusal(command: SheetTabCommand, sheet: Sheet): string | undefined {
        const sheets = this.#workbook.sheets;
        const at = sheets.indexOf(sheet);
        if (at < 0) return this.#words.sheetGone;
        if (command === 'delete' && sheets.length === 1) return this.#words.onlySheet;
        if (command === 'moveLeft' && at === 0) return this.#words.firstSheet;
        if (command === 'moveRight' && at === sheets.length - 1) return this.#words.lastSheet;
        return undefined;
    }

    // The menu's keys: ↑ and ↓ move among the commands that can be done, round from the last to the first, Home and End
    // to the first and the last, Enter or Space does the command, and Escape, Tab, the menu key or Shift+F10 closes the
    // menu, the keyboard going back to the tab it came from, or to the grid.
    readonly #onTabMenuKey = (event: KeyboardEvent): boolean => {
        const sheet = this.#menuSheet;
        if (sheet === undefined) return false;
        const enabled = tabCommandOrder.filter(command => this.#tabCommandRefusal(command, sheet) === undefined);
        const back = this.#menuFromTab ? 'tab' : 'grid';
        switch (event.key) {
            case 'ArrowDown':
            case 'ArrowUp': {
                const at = enabled.indexOf(this.#menuActive);
                const step = event.key === 'ArrowDown' ? 1 : -1;
                this.#menuActive = elementAt(enabled, ((((at < 0 ? 0 : at + step) % enabled.length) + enabled.length) % enabled.length));
                this.#paintTabMenu(sheet);
                return true;
            }
            case 'Home':
            case 'End':
                this.#menuActive = elementAt(enabled, event.key === 'Home' ? 0 : enabled.length - 1);
                this.#paintTabMenu(sheet);
                return true;
            case 'Enter':
            case ' ':
                this.#tabCommand(this.#menuActive);
                return true;
            case 'Escape':
            case 'Tab':
            case 'ContextMenu':
                this.#closeTabMenu(back);
                return true;
            case 'F10':
                if (!event.shiftKey) return false;
                this.#closeTabMenu(back);
                return true;
            default:
                return false;
        }
    };

    // Closes the menu, handing the keyboard on first, while the menu still holds it.
    #closeTabMenu(keyboard: 'nowhere' | 'grid' | 'tab'): void {
        const sheet = this.#menuSheet;
        if (sheet === undefined) return;
        this.#menuSheet = undefined;
        this.#menuSession = -1;
        if (keyboard !== 'nowhere') this.#keyboardTo(keyboard, sheet);
        this.#tabs.closeMenu();
    }

    // The command on the menu's Sheet: each one step, said in the notice, the keyboard going back to the grid — but for
    // Rename, which hands it to the name's field. A command that cannot be done does nothing, and the menu stays.
    #tabCommand(command: SheetTabCommand): void {
        const sheet = this.#menuSheet;
        if (sheet === undefined || this.#tabCommandRefusal(command, sheet) !== undefined) return;
        if (command === 'rename') {
            this.#beginRename(sheet, this.#menuFromTab);
            return;
        }
        this.#closeTabMenu('grid');
        if (this.#refusedSheetOperation()) return;
        const name = sheet.name;
        switch (command) {
            case 'delete':
                if (this.#sheetOperation(sheet, SheetEdit.deleteSheet()) === undefined) return;
                this.#tell(this.#words.sheetDeleted(name));
                return;
            case 'moveLeft':
            case 'moveRight': {
                const to = this.#workbook.sheets.indexOf(sheet) + (command === 'moveLeft' ? -1 : 1);
                if (this.#sheetOperation(sheet, SheetEdit.moveSheet(to)) === undefined) return;
                this.#tell(this.#words.sheetMoved(name, to + 1, this.#workbook.sheets.length));
                return;
            }
            case 'copy': {
                const copied = this.#sheetOperation(sheet, SheetEdit.copySheet(), step => step.sheet);
                if (copied === undefined) return;
                this.#tell(this.#words.sheetCopied(name, copied.sheet.name));
                return;
            }
        }
    }

    // `edit` done on `sheet` as one step on the undo stack, showing `show`'s Sheet when asked, else the Sheet shown
    // where it stands — the next, where the shown one was deleted. A refusal is said. Answers the step, or none.
    #sheetOperation(sheet: Sheet, edit: SheetEdit, show?: (step: WorkbookStep) => Sheet): WorkbookStep | undefined {
        if (!this.#workbook.sheets.includes(sheet)) return undefined;
        this.#resizing = undefined;
        let step: WorkbookStep;
        try {
            step = this.#workbook.do(sheet.name, edit);
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the engine's refusal is an answer told to the user; anything else is rethrown
            if (!(error instanceof SheetRefusedError)) throw error;
            this.#tell(this.#words.refused(error.refusal));
            return undefined;
        }
        this.#history.record([step], sheet);
        const shown = show?.(step);
        const target = shown !== undefined && this.#workbook.sheets.includes(shown) ? shown : this.#workbook.shown;
        if (target === this.#source.sheet) {
            // A change to the Sheets: #changed draws the tabs again and raises the document, once.
            this.#changed([step.change]);
            return step;
        }
        this.#showSheet(target);
        this.#raiseDocument();
        this.#paintFrame();
        return step;
    }

    // A tab dragged onto another, or past the last: it moves to that place, and its Sheet is shown.
    #moveByDrag(sheet: Sheet, target: Sheet | undefined): void {
        const sheets = this.#workbook.sheets;
        if (this.#refusedSheetOperation() || !sheets.includes(sheet) || (target !== undefined && !sheets.includes(target))) return;
        this.#leaveTabGestures('grid');
        const to = target === undefined ? sheets.length - 1 : sheets.indexOf(target);
        if (to === sheets.indexOf(sheet)) {
            this.#sayLeftOrClear();
            if (sheet !== this.#source.sheet) {
                this.#showSheet(sheet);
                this.#raiseDocument();
            }
            return;
        }
        const name = sheet.name;
        // A move refused says why, after what ending a name's edit said, if it said anything.
        if (this.#sheetOperation(sheet, SheetEdit.moveSheet(to), () => sheet) === undefined) {
            const refused = this.#notice.textContent;
            this.#say(refused);
            return;
        }
        this.#say(this.#words.sheetMoved(name, to + 1, this.#workbook.sheets.length));
    }

    // The Sheets have changed — by a gesture, a Consumer's command, an undo or a redo: a name being edited on a tab
    // whose Sheet is gone ends, and so does its menu, the keyboard they held going back to the grid; a menu still standing is
    // drawn again, so what it offers follows the Sheets as they stand.
    #forgetTabGesturesGone(): void {
        const sheets = this.#workbook.sheets;
        const renaming = this.#tabs.renaming;
        // The keyboard goes to the grid only from the field or the menu that held it: one elsewhere on the page stays.
        const keyboard = this.#tabs.gestureHoldsKeyboard ? 'grid' : 'nowhere';
        if (renaming !== undefined && !sheets.includes(renaming)) this.#endRename(keyboard);
        const menuSheet = this.#menuSheet;
        if (menuSheet === undefined) return;
        if (!sheets.includes(menuSheet)) {
            this.#closeTabMenu(keyboard);
            return;
        }
        if (this.#tabCommandRefusal(this.#menuActive, menuSheet) !== undefined) {
            const first = tabCommandOrder.find(command => this.#tabCommandRefusal(command, menuSheet) === undefined);
            if (first !== undefined) this.#menuActive = first;
        }
        this.#paintTabMenu(menuSheet);
    }

    // ---- Operations ------------------------------------------------------------------------------------------

    /** `edit` on `sheet`, the Sheet shown unless named, as one operation on the undo stack; its undo selects `selects`
     * where given. The columns a number entered over `widenOver` no longer fits are widened in the same operation, so
     * one undo puts back both (ADR-0048; ADR-0071, case 17). */
    #perform(edit: SheetEdit, selects?: readonly CellRange[], widenOver?: readonly CellRange[], sheet: Sheet = this.#source.sheet): void {
        this.#resizing = undefined;
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
    #performChecked(intent: GridPasteIntent | GridFillIntent, edit: SheetEdit): void {
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

    /**
     * What one operation changed across the Workbook: the Sheet shown paints what it changed there, and the document is
     * raised unless `raiseDocument` is false. A user's own operation ends Format Cells and clears the notice; a
     * Consumer's programmatic change (`byUser` false: a Linked Table's declaration or snapshot) leaves both standing, as
     * the C# Sheet's does, since it changes Values and no format Format Cells read.
     */
    #changed(changes: readonly WorkbookChange[], raiseDocument = true, byUser = true): void {
        // Format Cells read the cells it was opened over: an operation under it, an undo or a Toolbar press, ends it,
        // so its OK never sets a draft read from formats or places that no longer stand (the spine's sixth rule).
        if (byUser) {
            this.#endFormatCells();
            this.#endChoices();
        }
        const sheet = this.#source.sheet;
        if (changes.some(change => change.changesSheets) && !this.#workbook.sheets.includes(sheet)) {
            this.#showSheet(this.#workbook.shown);
            if (raiseDocument) this.#raiseDocument();
            return;
        }
        this.#sheetChanged(changes.map(change => change.for(sheet)), byUser);
        if (changes.some(change => change.changesSheets)) this.#paintFrame();
        else this.#paintToolbar();
        if (raiseDocument) this.#raiseDocument();
    }

    #sheetChanged(changes: readonly SheetChange[], byUser = true): void {
        if (byUser) this.#clearNotice();
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
        // A change may reach cells under the Selection that the Window does not hold, a recalculation or a whole
        // column's format among them: the Selection Summary is asked again (ADR-0130).
        this.#grid?.refreshSummary();
    }

    // `match`, where given, is a Find step's match on `sheet` (ADR-0332 §5): selected and taken as the current match in
    // place of the view the Sheet was left with.
    #showSheet(sheet: Sheet, match?: CellPosition): void {
        // Format Cells read the Sheet it was opened over: another shown, or this one opened again, ends it, and so does
        // a Toolbar Item's list.
        this.#endFormatCells();
        this.#endChoices();
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
        this.#referencesIn = this.#referencesOnSheetShown();
        // The view it was left with comes with its rows, under the version they are shown with; a Sheet shown for the
        // first time opens on A1, scrolled to it, as Excel's does. Either is rows of their own, so an edit open stays
        // with the Sheet it was typed on (ADR-0331 §5).
        const view = this.#views.get(sheet);
        const version = this.#source.snapshot().rowSequenceVersion;
        if (grid !== undefined) {
            if (view !== undefined) grid.showViewWith(view, version);
            else grid.showNewView(version, { range: { topRow: 0, leftColumn: 0, rowCount: 1, columnCount: 1 }, focus: { row: 0, column: 0 } });
        }
        grid?.update(this.#gridOptions());
        this.#paintFrame();
        if (match !== undefined) grid?.placeMatch(match, version);
    }

    // The document, then a change to the Sheets where the names moved. The names are taken as heard before either
    // handler runs, so one that throws or acts on the Sheet again leaves the next change said against these.
    #raiseDocument(): void {
        const before = this.#sheetNames;
        const after = this.#workbook.sheets.map(sheet => sheet.name);
        const sheetsChanged = after.length !== before.length || after.some((name, at) => name !== before[at]);
        this.#sheetNames = after;
        const change: SheetsChange = { before, after, shown: this.#workbook.shown.name };
        const { onWorkbookChanged } = this.#options;
        if (onWorkbookChanged !== undefined) {
            const raised = this.#workbook.toDocument();
            this.#raised.add(raised);
            onWorkbookChanged(raised);
        }
        if (sheetsChanged) this.#options.onSheetsChanged?.(change);
    }

    // A document opened, with its seed: the Consumer's, or the one just chosen, at a count of 0 changes (ADR-0180). Its
    // Sheets' names are the ones the Consumer has heard.
    #open(options: SheetOptions, document = options.document, chosenSeed = this.#chosenSeed): Workbook {
        const workbook = document === undefined ? new Workbook(Culture.of(options.locale ?? navigator.language)) : Workbook.open(document);
        workbook.setSeed(options.seed ?? chosenSeed, 0);
        // The day and the clock, before any row is read: every Sheet reads them, one added later too. NOW reads the zone
        // the options name when the engine asks, once per recalculation.
        workbook.nowSource = () => {
            const zone = this.#zoneOf(this.#options);
            return zone === undefined ? undefined : momentIn(zone, new Date());
        };
        workbook.setToday(this.#resolveDay(options));
        // The document's NOW was computed before the clock was given, and waits until it is read again.
        workbook.recalculateVolatile();
        this.#sheetNames = workbook.sheets.map(sheet => sheet.name);
        return workbook;
    }

    // ---- The Sheet Day and NOW (ADR-0121, ADR-0124) ---------------------------------------------------------

    // The zone the options name, else the browser's; none where the browser does not say it (ADR-0121).
    #zoneOf(options: SheetOptions): string | undefined {
        return options.timeZone ?? this.#browserZone;
    }

    // The fixed day, else the day in the zone now; none while no zone is known, when TODAY() waits.
    #resolveDay(options: SheetOptions): SheetDay | undefined {
        if (options.today !== undefined) return options.today;
        const zone = this.#zoneOf(options);
        return zone === undefined ? undefined : dayOf(momentIn(zone, new Date()));
    }

    // Reads the day again when it can have changed: at the next midnight in the zone, and at most an hour after this
    // reading. With a fixed day, nothing is armed.
    #armTheDay(): void {
        clearTimeout(this.#dayTimer);
        this.#dayTimer = undefined;
        const zone = this.#zoneOf(this.#options);
        if (this.#options.today !== undefined || zone === undefined) return;
        const due = untilTheDayIsReadAgainMs(momentIn(zone, new Date()));
        this.#dayTimer = setTimeout(() => {
            this.#dayTimer = undefined;
            if (this.#destroyed) return;
            const change = this.#workbook.setToday(this.#resolveDay(this.#options));
            this.#armTheDay();
            this.#changedByTheClock(change);
        }, due);
    }

    // Moves NOW on as the minute turns: the volatile Formulas are recalculated, and nothing at all where there are none.
    #armTheMinute(): void {
        clearTimeout(this.#minuteTimer);
        this.#minuteTimer = setTimeout(() => {
            this.#minuteTimer = undefined;
            if (this.#destroyed) return;
            this.#armTheMinute();
            if (this.#workbook.hasVolatileFormulas) this.#changedByTheClock(this.#workbook.recalculateVolatile());
        }, untilTheMinuteTurnsMs(Date.now()));
    }

    // What the clock moved, painted only where it reached a row of the Sheet shown: a RAND or an OFFSET recalculated by
    // the minute changes nothing there, and the Sheet is left as it stands.
    #changedByTheClock(change: WorkbookChange): void {
        const shown = change.for(this.#source.sheet);
        if (shown.rows.length > 0 || shown.recalculated.length > 0) this.#changed([change], false, false);
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
        const { locale, seed }: { readonly locale?: unknown; readonly seed?: unknown } = options;
        if (locale !== undefined && typeof locale !== 'string') throw new TypeError("A Sheet's locale is a language tag, such as 'en-US'.");
        // Refused here, before anything changes, where the engine would refuse it only once the Sheet had taken it.
        if (seed !== undefined && (typeof seed !== 'bigint' || seed < 0n || seed > maxSeed)) {
            throw new RangeError(`${typeof seed === 'bigint' ? seed.toString() : typeof seed} is not a seed: a seed is a bigint from 0 to 2^64 - 1.`);
        }
        const { timeZone, today }: { readonly timeZone?: unknown; readonly today?: unknown } = options;
        if (timeZone !== undefined) {
            if (typeof timeZone !== 'string') throw new TypeError("A Sheet's time zone is an IANA name, such as 'Asia/Tokyo'.");
            checkTimeZone(timeZone);
        }
        if (today !== undefined && !isSheetDay(today)) throw new RangeError("A Sheet Day is a calendar day from 1900 to 9999: { year, month, day }.");
        // The day is taken as it is now: one the Consumer changes in place afterwards is a new day only when handed over again.
        const taken = today === undefined ? options : { ...options, today: { year: today.year, month: today.month, day: today.day } };
        checkToolbar(options.toolbar);
        const figures: unknown = options.summaryFigures;
        if (figures !== undefined && (!Array.isArray(figures) || figures.some((figure: unknown) => !summaryFigureOrder.some(known => known === figure)))) {
            throw new TypeError(`summaryFigures is a list of the figures ${summaryFigureOrder.join(', ')} (ADR-0130).`);
        }
        const chrome: unknown = options.chrome;
        if (chrome !== undefined) {
            if (typeof chrome !== 'object' || chrome === null) throw new TypeError("A Sheet's chrome is an object of Chrome slots, each a mount function (ADR-0390).");
            const names: readonly string[] = [...sheetSlotNames, ...gridChromeSlotNames];
            for (const [name, slot] of Object.entries(chrome)) {
                if (!names.includes(name)) throw new TypeError(`A Sheet's chrome has no slot '${name}': its slots are ${names.join(', ')} (ADR-0390).`);
                if (slot !== undefined && typeof slot !== 'function') throw new TypeError(`chrome.${name} is a mount function (ADR-0390).`);
            }
        }
        const handlers: readonly (readonly [string, unknown])[] = [
            ['onWorkbookChanged', options.onWorkbookChanged], ['onSheetsChanged', options.onSheetsChanged],
            ['onEditingChanged', options.onEditingChanged], ['onSelectionChanged', options.onSelectionChanged],
            ['onShowToolbarChanged', options.onShowToolbarChanged], ['onLinkedColumnColoursChanged', options.onLinkedColumnColoursChanged],
            ['onPointingRefused', options.onPointingRefused], ['onSummaryFiguresChanged', options.onSummaryFiguresChanged],
            ['onSelectionSummaryChanged', options.onSelectionSummaryChanged], ['onFormatKeyRefused', options.onFormatKeyRefused],
            ['commandLabel', options.commandLabel],
        ];
        const scope: unknown = options.pointingScope;
        if (scope !== undefined && !(scope instanceof PointingScope)) throw new TypeError('pointingScope is a PointingScope (ADR-0058).');
        for (const [name, handler] of handlers) {
            if (handler !== undefined && typeof handler !== 'function') throw new TypeError(`${name} is a function.`);
        }
        return taken;
    }
}

// A Sheet as its Pointing Scope reads it (ADR-0058): its declared tables, read as they stand when asked.
function pointingSheet(
    linkedTables: () => readonly LinkedTable[], edit: Pick<PointingSheet, 'writePointedText' | 'takeBackPointedText' | 'tellPointingRefused'>,
): PointingSheet {
    return {
        get linkedTables() { return linkedTables(); },
        rootId: undefined,
        writePointedText: edit.writePointedText,
        takeBackPointedText: edit.takeBackPointedText,
        tellPointingRefused: edit.tellPointingRefused,
    };
}

// The Match Mark's rule (ADR-0332 §4): the one a step finds by, so a mark is never a match no step reaches.
const sheetFindMatch = (row: SheetRow, column: GridColumn<SheetRow>, pattern: GridFindPattern): boolean => {
    const at = CellAddress.tryParseColumn(column.name);
    if (at === undefined) throw new Error(`Internal: '${column.name}' is not one of the Sheet's columns.`);
    return sheetCellMatches(row.sheet, new CellAddress(row.index, at), pattern, row.at(at)?.text);
};

/** A cell a Replace All over the Workbook could not write, as the find panel names it: by its Sheet and column. */
function refusedCellOf(cell: { readonly sheet: Sheet; readonly address: CellAddress }, reason: string): FindPanelRefusedCell {
    return { rowNumber: cell.address.row + 1, column: sheetQualifier(cell.sheet) + CellAddress.columnName(cell.address.column), reason };
}

/** The commands of a Sheet Tab's menu, in its order (ADR-0331 §4). */
const tabCommandOrder: readonly SheetTabCommand[] = ['rename', 'delete', 'moveLeft', 'moveRight', 'copy'];

/** The keys the Sheet declares to its grid: Excel's formatting keys and its insert and delete keys (ADR-0050 item 14). */
const sheetDeclaredKeys: readonly string[] = Object.freeze([...sheetFormatKeysDeclared(), ...sheetStructureKeysDeclared()]);

/** How a Reference names `sheet` before its cell, quoted where its name needs it: `Sheet2!`, `'Q1 Sales'!`. */
function sheetQualifier(sheet: Sheet): string {
    const a1 = referenceText(new CellRange(new CellAddress(0, 0)), sheet.name);
    return a1.slice(0, a1.length - 'A1'.length);
}

function unreachableColumn(position: number): never {
    throw new Error(`Internal: no column of the request stands at ${String(position)}.`);
}

// Each Toolbar Item's KeyTip, Excel's Home tab's where Excel has one (ADR-0100; a reading until a Windows run reads them
// all). Strikethrough has none in Excel, and ExSheet's own is 4. Excel reaches Format Cells… through the Cells group's
// Format menu, O, then E: ExSheet has no such menu, so its letters are both, O narrowing to it and E running it.
const toolbarKeyTips: Readonly<Record<SheetToolbarCommand, string>> = {
    bold: '1', italic: '2', underline: '3', strikethrough: '4', fontColour: 'FC', fill: 'H', borders: 'B',
    alignLeft: 'AL', alignCenter: 'AC', alignRight: 'AR', numberFormat: 'N', percent: 'P', comma: 'K', formatCells: 'OE',
};

// Excel's Borders presets, in its list's order, each relative to every selected range as Format Cells' are (ADR-0100).
const toolbarBorders: readonly SheetToolbarBorder[] = ['bottom', 'top', 'left', 'right', 'none', 'all', 'outside', 'thickOutside'];
const thinLine = new BorderLine('thin');
const borderPresets: Readonly<Record<SheetToolbarBorder, BorderChange>> = {
    bottom: new BorderChange({ bottom: thinLine }),
    top: new BorderChange({ top: thinLine }),
    left: new BorderChange({ left: thinLine }),
    right: new BorderChange({ right: thinLine }),
    none: BorderChange.none,
    all: new BorderChange({ top: thinLine, bottom: thinLine, left: thinLine, right: thinLine, insideHorizontal: thinLine, insideVertical: thinLine }),
    outside: BorderChange.outline(thinLine),
    thickOutside: BorderChange.outline(new BorderLine('thick')),
};

// An alignment item's change: the alignment, or General where the Focus cell has it, as Excel's buttons do (ADR-0100).
const align = (alignment: HorizontalAlignment) => (focused: CellFormat): CellFormatChange =>
    new CellFormatChange({ alignment: focused.alignment === alignment ? 'general' : alignment });

// The default row: Excel's Home tab's commands in its order (ADR-0100).
const defaultToolbarRow: readonly (SheetToolbarCommand | 'separator')[] = [
    'bold', 'italic', 'underline', 'strikethrough', 'separator', 'fontColour', 'fill', 'separator', 'borders', 'separator',
    'alignLeft', 'alignCenter', 'alignRight', 'separator', 'numberFormat', 'percent', 'comma', 'separator', 'formatCells',
];

// The Toolbar's rows as the options give them. The grid's height is laid out from their count without drawing them.
const toolbarRowsOf = (options: SheetOptions): readonly (SheetToolbarRowOptions | 'default')[] => options.toolbar ?? ['default'];

// The Toolbar's rows as an untyped caller may hand them (CONVENTIONS.md): refused by what is wrong, before anything changes.
function checkToolbar(given: unknown): void {
    if (given === undefined) return;
    if (!Array.isArray(given) || given.length === 0) throw new TypeError("A Sheet Toolbar's rows are a list of one row or more; a toolbar of none is not shown: leave showToolbar off.");
    const rows: readonly unknown[] = given;
    if (rows.filter(row => row === 'default').length > 1) throw new RangeError("ExSheet's default row stands once on a toolbar (ADR-0100).");
    for (const row of rows) {
        if (row === 'default') continue;
        if (typeof row !== 'object' || row === null) throw new TypeError("A Toolbar Row is 'default' or { label, keyTip?, items }.");
        const { label, keyTip, items }: { readonly label?: unknown; readonly keyTip?: unknown; readonly items?: unknown } = row;
        if (typeof label !== 'string' || label === '') throw new TypeError("A Toolbar Row's label, its accessible name, is a string that is not empty.");
        checkKeyTip(keyTip, `the Toolbar Row '${label}'`);
        // A row of nothing would take a band off the Sheet's height and show nothing in it.
        if (!Array.isArray(items) || items.length === 0) throw new TypeError(`The items of the Toolbar Row '${label}' are a list of one item or more.`);
        const entries: readonly unknown[] = items;
        for (const entry of entries) {
            if (entry === 'separator') continue;
            if (typeof entry === 'string') {
                if (!Object.hasOwn(toolbarKeyTips, entry)) throw new RangeError(`'${entry}' is not one of ExSheet's toolbar commands: ${Object.keys(toolbarKeyTips).join(', ')}.`);
                continue;
            }
            if (typeof entry !== 'object' || entry === null) throw new TypeError("A Toolbar Row's item is one of ExSheet's commands, 'separator', or a button { text, onPress, keyTip?, disabledReason?, content? }.");
            const { text, onPress, keyTip: letters, disabledReason, content }: {
                readonly text?: unknown; readonly onPress?: unknown; readonly keyTip?: unknown; readonly disabledReason?: unknown; readonly content?: unknown;
            } = entry;
            if (typeof text !== 'string' || text === '') throw new TypeError("A toolbar button's text, its name, is a string that is not empty.");
            if (typeof onPress !== 'function') throw new TypeError(`The toolbar button '${text}' has an onPress, which is a function.`);
            checkKeyTip(letters, `the button '${text}'`);
            if (disabledReason !== undefined && typeof disabledReason !== 'string') throw new TypeError(`The disabledReason of the toolbar button '${text}' is a string, or undefined where it can be pressed.`);
            if (content !== undefined && typeof content !== 'function') throw new TypeError(`The content of the toolbar button '${text}' is a function that mounts it into the element it is handed.`);
        }
    }
    refuseCollidingKeyTips(given);
}

// The KeyTips of one level, refused where one begins another.
function refuseCollidingAtOneLevel(tips: readonly { readonly letters: string; readonly name: string }[]): void {
    tips.forEach((tip, i) => {
        for (const other of tips.slice(0, i)) {
            if (tip.letters.startsWith(other.letters) || other.letters.startsWith(tip.letters)) {
                throw new RangeError(`The KeyTips '${other.letters}' of ${other.name} and '${tip.letters}' of ${tip.name} collide at one level: typed, one would be taken for the other. Give one of them other letters (ADR-0100).`);
            }
        }
    });
}

// KeyTip letters are typed: letters or digits, one or more, read without case.
function checkKeyTip(letters: unknown, name: string): void {
    if (letters !== undefined && (typeof letters !== 'string' || !/^[\p{L}\p{N}]+$/u.test(letters))) {
        throw new TypeError(`The KeyTip of ${name} is letters or digits that can be typed, such as 'R'.`);
    }
}

// The KeyTip letters of a toolbar's rows, and of each row's items, which may not be taken for one another at one level
// (ADR-0100): one that begins another would be typed as it. Refused by name.
function refuseCollidingKeyTips(rows: readonly (SheetToolbarRowOptions | 'default')[]): void {
    refuseCollidingAtOneLevel(rows.flatMap(row => {
        if (row === 'default') return [{ letters: 'H', name: "ExSheet's default row" }];
        return row.keyTip === undefined ? [] : [{ letters: row.keyTip.toUpperCase(), name: `the Toolbar Row '${row.label}'` }];
    }));
    for (const row of rows) {
        if (row === 'default') continue;
        refuseCollidingAtOneLevel(row.items.flatMap(item => {
            if (item === 'separator') return [];
            if (typeof item === 'string') return [{ letters: toolbarKeyTips[item], name: `the item '${item}'` }];
            return item.keyTip === undefined ? [] : [{ letters: item.keyTip.toUpperCase(), name: `the button '${item.text}'` }];
        }));
    }
}

// The Name Box names the Focus as A1 does (ADR-0051).
const nameBoxLabel = (focus: CellPosition): string => new CellAddress(focus.row, focus.column).toString();
// The size of a range a drag selects, in the Name Box, as Excel's shows it while the button is down (ADR-0052).
const nameBoxSizeLabel = (range: SelectionRange): string =>
    `${String(range.rowCount)}R x ${String(range.columnCount)}C`;

// A Row Heading is Excel's row number, which is the position plus one in every culture (ADR-0050, ADR-0048).
const rowLabel = (row: number): string => String(row + 1);

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

// Format Cells drawn by a Chrome (ADR-0390): the slot it stands in, in the grid's popover frame, and its contents,
// taken away however the frame closes. Its calls reach nothing once they are.
class SlottedFormatCells {
    readonly element: HTMLElement;
    readonly #mount: SheetFormatCellsSlot;
    #cleanup: (() => void) | undefined;
    #live = false;

    constructor(doc: Document, mount: SheetFormatCellsSlot) {
        this.#mount = mount;
        this.element = doc.createElement('div');
        // A class of its own, by which Ctrl+1 inside it opens nothing more: not the built-in Format Cells', whose layout
        // is the built-in contents'.
        this.element.className = 'ex-format-cells-slot';
    }

    get live(): boolean {
        return this.#live;
    }

    mount(context: SheetFormatCellsSlotContext): void {
        this.#live = true;
        this.#cleanup = mountedSlot('formatCells', () => this.#mount(this.element, context));
    }

    dispose(): void {
        if (!this.#live) return;
        this.#live = false;
        const cleanup = this.#cleanup;
        this.#cleanup = undefined;
        cleanup?.();
    }
}
