// The grid in the DOM (ADR-0350 sections 2 and 4): the Script Core builds and updates the elements inside
// the instance root itself, to the DOM contract the C# core writes, so ex-grid.css and layer 3 read both
// alike. A row is plain markup and the only unit that is skipped or painted again (ADR-0003); the cells
// are rebuilt with their row.
//
// One pass resolves and paints, as the C# component's render does (PrepareRender, then the markup): what
// the browser told and what the Consumer handed over become the Viewport's slice and the columns on
// screen, and the DOM is brought to them. Anything that changes them asks for a pass, which runs in a
// microtask, so a burst of changes in one task paints once.

import { AutoWidth } from '../columns/auto-width';
import { assembleCopy, checkedCopyAnswer, type CopyFlavours, type GridCopyAnswer, type GridCopyRequest } from '../clipboard/clipboard-data';
import { defaultCellCap, pasteShape, planCopy, planFill, planPaste, type CopyPlan, type FillPlan } from '../clipboard/clipboard-rules';
import { GridClearIntent } from '../clipboard/clear-intent';
import { columnPlaceholder, copyRefusalWords, pasteRefusalWords, rowCommandRefusalId, shapeMismatchMaySpillWords } from '../chrome/refusal-words';
import { builtInCommandLabel } from '../chrome/command-labels';
import { summaryLabelIds } from '../chrome/summary-words';
import { markLabelIds } from '../chrome/mark-words';
import { findOutcomeText, type FindOutcome, type FindPanelRefusedCell } from '../chrome/find-panel-labels';
import { gridCommandIds, gridLabelIds } from '../chrome/command-ids';
import { letterOf, menuLetter, resolveColumnMenuKey, resolveMenuKey } from '../chrome/menu-keys';
import { canApply } from '../chrome/filter-panel-choices';
import { gridCulture, invariantLocale, type GridCulture } from '../chrome/grid-culture';
import type { TemporalLike } from '../chrome/operand-reading';
import { filterOperatorsFor, type FilterSpec } from '../query/filter';
import { parseClipboardBlock, type ClipboardBlock } from '../clipboard/clipboard-parse';
import { GridPasteIntent } from '../clipboard/paste-intent';
import { ColumnGeometry, type ColumnRange } from '../columns/column-geometry';
import { HeaderGroup, HeaderGroupLayout, type ResolvedHeaderGroup } from '../columns/header-groups';
import { reorderColumns } from '../columns/header-reorder';
import { decideOverflow } from '../columns/overflow-rules';
import { rawText, textFor } from '../cells/display-text';
import { checkedVerdict, GridEditIntent, type EditDiscardReason, type EditVerdict, type GridOverwriteNotice } from '../cells/edit-intent';
import { GridMetrics, type GridDensity } from '../grid-metrics';
import { matcherOf, patternOf, type GridFindPattern, type GridFindRequest, type GridFindResult } from '../finding/find';
import { columnHeaderId, findHeaders, groupHeaderId, matchingHeaders, sameHeaders, type FindHeader } from '../finding/header-find';
import { headerAfterAnswer, headerBeforeAsking } from '../finding/header-find-walk';
import { replacedEntry, type GridReplaceRequest, type GridReplaceResult } from '../finding/replace';
import { defined, itemAt } from '../must';
import { editingKeysByMode } from '../keys/editing-keys';
import { canonicalKey, resolveKey, type GridKeyAction, type KeyPress } from '../keys/grid-keys';
import { cellClasses, type MatchMark } from '../paint/cell-classes';
import { columnMark } from '../paint/heading-mark-styles';
import { rowClasses } from '../paint/row-classes';
import { rowMarkHeaderPress, rowMarkHeaderState, type MarkRefusalReason, type RowMarkIntent, type RowMarks } from '../rows/row-marks';
import { cell as cellPosition, sameCell, type CellPosition, type GridDirection, type GridExtent } from '../selection/cell';
import { markedColumns, wholeColumnRuns } from '../selection/heading-marks';
import { edgeBandRowsPerTick } from '../selection/edge-band';
import { HeldSelection } from '../selection/held';
import { bottomRow, fromCorners, rangeCellCount, sameRange as sameSelectionRange, spansEveryColumn, spansEveryRow, type SelectionRange } from '../selection/range';
import { Selection, type EdgeAnswer, type GridSelection } from '../selection/selection';
import { nextSort } from '../query/sort';
import type { GridSource, GridSourceState } from '../source/grid-source';
import { InMemoryGridSource } from '../source/in-memory-source';
import type { GridSummaryRequest, GridSummaryResult } from '../summary/grid-summary';
import { isSummaryFigure, summaryFigureOrder, defaultSummaryFigures, type SummaryFigure } from '../summary/summary-accumulator';
import { keyOf, type RowKey } from '../source/row-key';
import { rowRange, type RowRange } from '../viewport/row-range';
import { ViewportBox } from '../viewport/viewport-box';
import { ViewportGeometry } from '../viewport/viewport-geometry';
import type { BrowserReports, ViewportReport } from './browser-reports';
import { ClipboardLedger, type LedgerWrite } from './clipboard-ledger';
import { ColumnStyles } from './column-styles';
import { cellBox, editorAway, editorCell, keyFieldBox, opensAnEdit } from './cell-editing';
import {
    boundaryLeftPx, columnsResizedWith, dragThresholdPx, dropBoundaries, grabbedAt, guideLeftPx, nearestBoundary, type ColumnGesture, type GridColumnWidthChange,
} from './column-gestures';
import { CopiedRange } from './copied-range';
import type { GridCommand, GridContextMenuContext } from './grid-command';
import { contextMenuRoomPx, contextMenuStyle, MenuItems, popoverAcross, popoverMinWidthPx, type MenuKeyTable } from './menu-popover';
import { FilterPanel } from './filter-panel';
import { FindPanel, type FindHeld } from './find-panel';
import { GridMarks, rowMarkBox, rowsSelectedIn } from './grid-marks';
import { copyValues, gatherRows, windowCovers, windowRowAt } from './grid-copy';
import { defaultPasteByteCap, exceedsUtf8 } from './grid-paste';
import type { ActionRefusalReason, GridRefusal } from './grid-refusal';
import {
    resolveColumns, type GridAction, type GridActionEvent, type GridColumn, type GridTemplate, type GridTemplateCell, type ResolvedColumn,
} from './grid-column';
import { moveByKey, movingKinds, type KeyReveal } from './key-moves';
import { selectionLayers, type LayerPiece } from './selection-layers';
import { SummaryMenu, toggledFigures } from './summary-menu';
import { rowsMovedUnder, selectsTwoCells, summaryFiguresShown, SummaryBox, SummaryQuestions, type SelectionSummary, type SummaryAnswerer } from './selection-summary';
import { stylesheetName, stylesheetStamp } from './shipped-stylesheet';
import { CellPaint, type CellPaintOptions } from './cell-paint';
import { FormulaBar, formulaBarDeclarations, type FormulaBarDeclarations, type FormulaBarOptions } from './formula-bar';
import type { ColumnType } from '../query/columns';
import { checkedAppearance, RowAppearances, type RowAppearance } from '../paint/row-appearances';
import { hashesWhenOverflowing } from '../columns/overflow-rules';
import { ConsumerPopoverFrame, type GridPopoverContext } from './consumer-popover';

/** A Viewport axis: a number of pixels the Consumer declares, or `'stretch'`, the parent's (ADR-0028). */
export type ViewportSize = number | 'stretch';

/**
 * What a reader of the grid looks at (ADR-0331): the Selection, with the Row Sequence Version its positions are
 * written in, and where the rows and columns are scrolled to. A Consumer that shows several sets of rows in one grid,
 * one at a time, as a Sheet shows its Sheets behind their tabs, keeps each set's view with `readView` and hands it
 * back with `showViewWith` when that set is shown again.
 */
export interface GridViewRead {
    /** The Row Sequence Version the Selection's positions are written in. */
    readonly rowSequenceVersion: number;
    /** The Selection. */
    readonly selection: GridSelection;
    /** The scroller's vertical offset, in pixels. */
    readonly scrollTopPx: number;
    /** The scroller's horizontal offset, in pixels. */
    readonly scrollLeftPx: number;
}

/**
 * A labelled rectangle over adjacent columns, in a tier above the header row (ADR-0032): colspan and rowspan
 * without a column tree. Its members are named, as a set: the painted order is the columns' own.
 */
export interface GridHeaderGroup {
    /** The caption; empty for a spacer rectangle. */
    readonly label: string;
    /** The member columns' names. They stand side by side in the column order, or the grid refuses the group. */
    readonly columns: readonly string[];
    /** The rectangle's top tier, 1 directly above the header row; 1 unless given. */
    readonly tier?: number;
    /** How many tiers the rectangle spans downward from its top tier; 1 unless given. */
    readonly tierSpan?: number;
}

/** What a grid is drawn from. Rows reach it only through a Grid Source; `rows` is `GridSource.from(rows)`. */
export interface GridOptions<Row> extends CellPaintOptions<Row>, FormulaBarOptions<Row> {
    /** The columns, in display order. */
    readonly columns: readonly GridColumn<Row>[];
    /** Where the rows come from. Exactly one of `source` and `rows` is given. */
    readonly source?: GridSource<Row>;
    /** The rows, held by a source the grid makes over them. Exactly one of `source` and `rows` is given. */
    readonly rows?: readonly Row[];
    /** The preset every geometry not given comes from; `compact` unless given (ADR-0028). */
    readonly density?: GridDensity;
    /** The height of every row in pixels, beating the preset's (ADR-0013). */
    readonly rowHeight?: number;
    /** The header's height in pixels; the row height's when only that is given (ADR-0028). */
    readonly headerHeight?: number;
    /** The Viewport's width: 800 pixels unless given, or `'stretch'` for the parent's (ADR-0028). */
    readonly viewportWidth?: ViewportSize;
    /** The Viewport's height, header and scrollbar included: 600 pixels unless given, or `'stretch'`. */
    readonly viewportHeight?: ViewportSize;
    /** How many columns from the left stay in place while the rest scroll (ADR-0004); none unless given. */
    readonly pinnedColumnCount?: number;
    /** Whether the columns off screen are left out of the DOM (ADR-0004); true unless given. */
    readonly virtualiseColumns?: boolean;
    /** The Header Groups over the columns, in tiers above the header row (ADR-0032); none unless given. A
     * group whose members are not side by side, that straddles the Pinned Columns' edge or that overlaps
     * another is refused by name, as is a Viewport too short for the band and one row. */
    readonly headerGroups?: readonly GridHeaderGroup[];
    /** Hears the Selection each time it changes (ADR-0011): after the grid has painted it, once however
     * many gestures changed it in between. A Selection is dropped, and heard empty, when the Row Sequence
     * Version or the columns change. */
    readonly onSelectionChanged?: (selection: GridSelection) => void;
    /** The most cells one copy carries (ADR-0005): past it the grid copies nothing at all and says so,
     * rather than copy a part. A million unless given; at least one, since the cap may be raised but not
     * used to switch copying off. */
    readonly copyCellCap?: number;
    /**
     * Answers each copy the grid's own rules approved, in place of the grid's assembly (ADR-0050, item 9): the two
     * flavours to write, exactly as given, or a refusal in the Consumer's own sentence, which the grid says, raising
     * `onRefused` with `refusedByConsumer`, and writes nothing. A Consumer that holds what it copies, as a Sheet
     * holds Entries, answers from it however far the copy runs past the rows in hand.
     */
    readonly copyAnswer?: (request: GridCopyRequest) => GridCopyAnswer;
    /** Hears every gesture the grid refused, by kind and reason (ADR-0005): nothing it would have done was
     * done, and the grid has said why through its live region. */
    readonly onRefused?: (refusal: GridRefusal<Row>) => void;
    /**
     * Hears an action pressed (ADR-0020): a press on an Action Column's button, or Space on its cell, which
     * fires its one action, or the one chosen inside a cell with several (ADR-0037). On the row it was pressed
     * on, as the Window holds that row now; a press whose row the Window no longer holds, or that only a
     * position under a moved order names, raises nothing and is refused (`onRefused`, kind `action`). The
     * grid moves no keyboard after it: what the action opens is the Consumer's.
     */
    readonly onAction?: (event: GridActionEvent<Row>) => void;
    /**
     * Who holds the Row Marks of the Mark Column (ADR-0043): answers whether a row is marked, counts the marks,
     * and takes each marking gesture. Needed when a Mark Column is declared over `rows`, or over a `source` that
     * keeps no marks of its own; `GridSource.from` keeps them, and the Consumer reads them from it to act. Given
     * beside a `source` that keeps its own, it is refused: one of the two would have to silently win.
     */
    readonly marks?: RowMarks<Row>;
    /**
     * Hears the one intent each approved paste raises (ADR-0007, ADR-0014): every cell in one notification.
     * The grid writes nothing; the screen changes when the source hands over new row instances. A Consumer
     * that will not write it calls `intent.refuse()` before this returns, and the Selection stays where it
     * was (ADR-0050, item 3). Only columns declared `editable` take a paste; one that covers another is
     * refused whole (ADR-0035).
     */
    readonly onPaste?: (intent: GridPasteIntent) => void;
    /** Whether a block pasted onto one cell spills from it (ADR-0050, item 3): written with that cell at its
     * top-left, and once accepted the block becomes the Selection. Off unless given, and a block onto one
     * cell is then refused (ADR-0014). Declared by a Consumer that holds every cell, such as a Sheet. */
    readonly pasteMaySpill?: boolean;
    /** The most a paste reads, in UTF-8 bytes across both flavours (ADR-0005): past it the paste is refused
     * whole, never pasted from the plain-text flavour alone, which is already rounded. 16 MB unless given. */
    readonly pasteByteCap?: number;
    /** A band across the Focus's row, beneath the Selection's tints (ADR-0008), filled with
     * `--ex-focus-row-fill`. Off unless given. */
    readonly highlightFocusRow?: boolean;
    /** A band across the row under the pointer, what every design system's table calls Hover (ADR-0029),
     * filled with `--ex-row-hover-background`. It is the overlay's, so no row learns anything and none is
     * painted again; it goes when the pointer leaves the grid or the rows move under it. Off unless given,
     * and while off the grid does not hear the pointer move at all. */
    readonly highlightHoverRow?: boolean;
    /**
     * Hears the one Clear Intent Delete raises over the Selection (ADR-0054): the positions should hold no
     * value, which the Consumer maps onto its own Blank per column. Judged by the paste's gate first: only
     * columns declared `editable` take it, and one that covers another is refused whole. Without it Delete is
     * the browser's.
     */
    readonly onClear?: (intent: GridClearIntent) => void;
    /**
     * Ctrl+Z, forwarded (ADR-0007): the Consumer owns the history, because only it sees every change, an edit
     * in the grid and a row added elsewhere alike. The grid has nothing to put in the notification and changes
     * nothing on it. The key is taken from the page only while this is given, and never while the Cell Editor
     * is open, where Ctrl+Z undoes the typing.
     */
    readonly onUndo?: () => void;
    /** Ctrl+Y and Ctrl+Shift+Z, forwarded like `onUndo` (ADR-0007), both of Excel's spellings, on every
     * platform. */
    readonly onRedo?: () => void;
    /**
     * Hears the Edit Intent each Cell Editor commit raises (ADR-0007): the row, the column and the text as the
     * user entered it. The grid writes nothing; the screen changes when the source hands over a new row
     * instance. A Consumer that will not write it calls `intent.refuse(message)` before this returns, and the
     * editor stays open with the typing (ADR-0142). Without it no Cell Editor opens: typing onto a cell is
     * heard by nobody, and a grid with no column `editable` is display-only, its keyboard on its root
     * (ADR-0080).
     */
    readonly onEdit?: (intent: GridEditIntent<Row>) => void;
    /** Hears whether an edit is open, each time that changes (ADR-0050 section 6): once as one opens and once
     * as it ends, after what it ended in — the Edit Intent, or the discard — has been heard. */
    readonly onEditingChanged?: (open: boolean) => void;
    /** Hears an edit the grid threw away, with the reason (ADR-0011): the typing is gone when this arrives,
     * and what a Consumer owes the user is the reason, since nobody asked for it to happen. */
    readonly onEditDiscarded?: (reason: EditDiscardReason) => void;
    /**
     * Hears the Overwrite Notice (ADR-0142, LV-11, LV-21): a Cell Editor commit — Enter, Tab, an arrow in
     * Overwrite, a press elsewhere, Ctrl+Enter — landed, accepted, over a cell whose painted text changed while
     * the editor covered it, the one change the user could not see. It carries the Edit Intent's two texts, and
     * is raised exactly when they differ. The user's own earlier write to the cell, not yet settled when the
     * editor opened, is never told as a change (D1). The grid holds no string for it: Chrome words it into the
     * root's live region, as it words a refusal (A11Y-16), and nothing is added to the row. Not raised for a
     * commit whose handler handed over another source or other columns.
     */
    readonly onOverwriteNotice?: (notice: GridOverwriteNotice) => void;
    /** Hears a double click on a cell where no edit opens, after the click has placed the Focus there
     * (ADR-0063). The grid changes nothing: what the gesture means is the Consumer's. While an edit is open a
     * double click is the editor's, and on an Editable cell it opens one. */
    readonly onCellDoubleClick?: (cell: CellPosition) => void;
    /**
     * Hears the Escape that leaves the grid (ADR-0070): pressed on the grid with nothing left to dismiss, raised
     * beside the grid's own way out, which releases Tab first (ADR-0012): the grid keeps DOM focus, and the next
     * Tab or Shift+Tab leaves it. What leaving means, closing the Consumer's dialog for one, is the Consumer's,
     * which may put the keyboard somewhere first. Only the outermost Escape raises it: the one that closes a
     * popover, cancels an edit, leaves an Interactive cell, removes the Copied Range's outline or returns from a
     * control inside a cell peels its own layer and nothing else. Raised once per press: a held Escape's repeats
     * do nothing more (DC-62).
     */
    readonly onLeave?: () => void;
    /**
     * Hears a column resized by the user (ADR-0016), once per column: a drag on the grip at a header's right
     * edge, applied on release and never live, or a double click on it, which fits the column to its header and
     * every value the grid holds. A grip on a column wholly selected acts on every whole column selected, as in
     * Excel (FN-12c). The grid changes nothing: the width moves when the Consumer hands back the column declared
     * at it. Without it no column has a grip.
     */
    readonly onColumnWidthChanged?: (change: GridColumnWidthChange) => void;
    /**
     * Hears the column order after the user dragged a header (ADR-0011): the unit grabbed moves whole, a leaf
     * within its Header Group or a group's rectangle among its peers, and never across the pinned boundary or a
     * group's edge (ADR-0032). Every column's `name`, in the new order; the grid changes nothing until the
     * Consumer hands back the columns in it. While it is given, a press on a header grabs the column to move it,
     * and no longer selects it as it is dragged (ADR-0050, item 1); a click still sorts.
     */
    readonly onColumnOrderChanged?: (order: readonly string[]) => void;
    /**
     * The Consumer's commands on the Context Menu (ADR-0036), which a secondary click on a cell opens, as do the
     * ContextMenu key and Shift+F10 on the Focus. They stand after the core's own, Copy and Copy with headers,
     * and are asked for each time the menu opens, with the cell it opened on and the Selection its commands act
     * on. A secondary click outside the Selection first collapses it onto the cell it lands on, so what a
     * command acts on is what the user can see.
     */
    readonly contextCommands?: (context: GridContextMenuContext<Row>) => readonly GridCommand[];
    /** What the built-in menu calls a command, by its id (ADR-0036): the Consumer's words, in any language, or
     * `undefined` for the built-in English. A command the table does not know either is shown as its id. */
    readonly commandLabel?: (id: string) => string | undefined;
    /** Leaves Copy with headers off the Context Menu (ADR-0050, item 13). Shown unless given. */
    readonly hideCopyWithHeaders?: boolean;
    /** Leaves the ▾ off every header, and Alt+↓ with it (ADR-0050, item 12): the column menu, the column's
     * commands, is shown unless given. */
    readonly hideColumnMenu?: boolean;
    /** Hears "Hide this column" from the column menu, with the column's `name` (ADR-0010). The grid changes
     * nothing: the column goes when the Consumer hands back the columns without it. Without it the command is
     * shown disabled. */
    readonly onColumnHidden?: (column: string) => void;
    /** Hears the Pinned Columns' count the column menu asks for (ADR-0004, ADR-0010): "Pin up to this column"
     * and "Unpin all columns". The grid changes nothing until the Consumer hands back `pinnedColumnCount`.
     * Without it both commands are shown disabled. */
    readonly onPinnedCountChanged?: (count: number) => void;
    /**
     * The culture the grid reads typed text in (ADR-0370 section 10): a BCP 47 tag, or `'invariant'`, which
     * reads as .NET's InvariantCulture. A value typed into a filter's condition is read in it, and refused
     * where it is not the column's type as the culture writes it (ADR-0023). `'invariant'` unless given; the
     * core never reads the browser's language on its own, so one instance never reads two cultures. The React
     * adapter passes the browser's language when given none.
     */
    readonly locale?: string;
    /**
     * The Temporal namespace a date typed into a filter is made with (ADR-0350 section 16): the browser's own,
     * or the polyfill the page loads. Neither the core nor the React adapter reads the global one. A grid with a
     * Date column whose filter is offered is refused without it, naming the column, on every browser alike.
     */
    readonly temporal?: TemporalLike;
    /**
     * The Replace Request (ADR-0332 §3): Replace All, asked of the Consumer, who performs it and answers. Ctrl+H
     * opens the find panel with Replace only while this is given, a column is `editable` and the source finds;
     * otherwise the key is still taken, opens nothing and is refused (`onRefused`, kind `find`, reason
     * `replaceUnavailable`). The request carries Find's text and options, the replacement, the Selection as the
     * scope when more than one cell is selected, the columns in their current order, which of them are
     * Editable, and the source's Row Sequence Version they were read in. What is matched is each cell's entry,
     * the text the Cell Editor opens with. The Consumer writes every match or none, as one step of its undo
     * stack, and answers the count or the cells it could not write, under the version it read its rows in; an
     * answer under another is refused as the order having moved. `GridSource.from`'s `replace` answers it, and
     * `planReplace` is the reference. Replace, the current match alone, is an ordinary `onEdit`.
     */
    readonly onReplace?: (request: GridReplaceRequest) => GridReplaceResult | Promise<GridReplaceResult>;
    /**
     * The Match Mark's rule (ADR-0332 §4): whether a painted cell matches what the find panel holds. While the
     * panel holds a search, every painted cell it says matches wears `ex-match-mark`, and the current match, the
     * Focus a found step moved to, `ex-match-mark-current` as well. Unless given, the reference rule marks the
     * cells where the source is `GridSource.from`'s, which finds by it, and nothing is marked anywhere else: a
     * source that finds by a rule of its own passes it here to have the marks. Hold it in a field: its identity
     * is the change signal, and a new function makes every painted row judge its cells again (ADR-0003).
     */
    readonly findMatch?: (row: Row, column: GridColumn<Row>, pattern: GridFindPattern) => boolean;
    /** Whether Find searches the column headers and Header Group labels too (ADR-0333): true unless given. A
     * header found is the current match, marked and named in the panel, and the Focus stays where it is. Turn
     * it off where the headers are addresses rather than content. */
    readonly findsHeaders?: boolean;
    /** A plain click on a column header selects the whole column, and nothing sorts: Excel's meaning, where
     * ADR-0012 gives the click to sorting (ADR-0050, item 1). Shift+click extends from the Focus's column as
     * always. Off unless given. */
    readonly headerClickSelects?: boolean;
    /** Where Ctrl+arrow stops (ADR-0050, item 2): handed the Focus and the direction, it answers the cell on that
     * line where the data ends, which the grid cannot know since it does not hold the data. Asked on Ctrl+arrow
     * and Ctrl+Shift+arrow only; Home and End keep their meaning. An answer off the Focus's line, behind it or
     * outside the grid is refused by name. The grid's edge unless given (ADR-0012). */
    readonly dataEdge?: (from: CellPosition, direction: GridDirection) => CellPosition;
    /**
     * The answer to a Selection Summary (ADR-0130): the figures over the cells the request names, in the order it
     * was read in, or a decline with a reason, never a partial figure; `summaryOf` is the reference for what each
     * figure is. A `source` that summarises, as `GridSource.from` does, answers through its own `summarize`, and
     * passing this beside one is refused by name; over `rows` this answers in place of the source the grid makes.
     * The signal aborts when the selection the request was asked of is gone: its answer is shown nowhere anyway.
     */
    readonly onSummarize?: (request: GridSummaryRequest, signal: AbortSignal) => GridSummaryResult | Promise<GridSummaryResult>;
    /** Whether the grid's own status line shows the Selection Summary (ADR-0130): on unless given, so a grid that
     * can summarise shows Excel's status-bar figures without being asked. Off: no strip and no figure on the
     * grid; a Consumer hearing `onSelectionSummaryChanged` is still told, and with nobody hearing either, nothing
     * is asked. */
    readonly showSelectionSummary?: boolean;
    /** The figures the status line shows, Excel's Average, Count and Sum unless given (ADR-0130). Shown in Excel's
     * order whatever the order given; none, and nothing is asked. */
    readonly summaryFigures?: readonly SummaryFigure[];
    /** Hears the Selection Summary as it now stands, after the pass that shows each change (ADR-0130): for a
     * Consumer that shows the figures elsewhere, such as an application-wide status bar. The grid never writes
     * them to a live region. */
    readonly onSelectionSummaryChanged?: (summary: SelectionSummary) => void;
    /** Hears a change of figures made on the status line (ADR-0130): given, a right-click there opens Excel's menu
     * of the six figures, each ticked while shown, and a press on one reports the figures with it ticked or
     * unticked, in Excel's order. The grid holds no choice: the figures stay as `summaryFigures` says until the
     * Consumer hands the new ones back. Not given, the right-click is the browser's. */
    readonly onSummaryFiguresChanged?: (figures: readonly SummaryFigure[]) => void;
}

const defaultViewportWidthPx = 800;

/** The widest a cell's message lays out: emitted inline, never a stylesheet literal beside it (ADR-0027). */
const messageMaxWidthPx = 320;

/** One of the core's own commands, frozen as a Consumer's would be handed it. */
const coreCommand = (id: string, enabled: boolean, invoke: () => void): GridCommand => Object.freeze({ id, enabled, invoke });

/** The commands a Consumer's `contextCommands` answered, checked: a command the menu cannot show or run is the
 * Consumer's defect, refused by name rather than shown as a blank or a button that does nothing. */
function checkedCommands(commands: unknown): readonly GridCommand[] {
    if (!Array.isArray(commands)) throw new TypeError('contextCommands answers an array of commands.');
    return (commands as readonly unknown[]).map(command => {
        if (!isCommand(command)) {
            throw new TypeError('A command contextCommands answers is { id: string, enabled: boolean, invoke: function }.');
        }
        return command;
    });
}

function isCommand(value: unknown): value is GridCommand {
    if (typeof value !== 'object' || value === null) return false;
    const { id, enabled, invoke } = value as { readonly id?: unknown; readonly enabled?: unknown; readonly invoke?: unknown };
    return typeof id === 'string' && typeof enabled === 'boolean' && typeof invoke === 'function';
}

function isRowMarks(value: unknown): value is RowMarks<unknown> {
    if (typeof value !== 'object' || value === null) return false;
    const { isMarked, onMarkIntent, setRowKind, subscribe } = value as {
        readonly isMarked?: unknown; readonly onMarkIntent?: unknown; readonly setRowKind?: unknown; readonly subscribe?: unknown;
    };
    // The counts may be undefined while they are not known, but a holder that declares none at all is not one.
    return typeof isMarked === 'function' && typeof onMarkIntent === 'function' && typeof setRowKind === 'function'
        && typeof subscribe === 'function' && 'counts' in value;
}

function isTemporal(value: unknown): value is TemporalLike {
    if (typeof value !== 'object' || value === null) return false;
    const { PlainDate, PlainDateTime, ZonedDateTime } = value as { readonly PlainDate?: unknown; readonly PlainDateTime?: unknown; readonly ZonedDateTime?: unknown };
    return [PlainDate, PlainDateTime, ZonedDateTime].every(member => typeof member === 'function' && 'from' in member
        && typeof member.from === 'function');
}

/** A column as the find panel names it: its header, or its name where the header is blank (ADR-0332 §2). */
function headerOf<Row>(column: ResolvedColumn<Row>): string {
    return column.header.trim() === '' ? column.column.name : column.header;
}

/** Whether a Consumer answered later rather than now. */
function isThenable<T>(value: T | PromiseLike<T>): value is PromiseLike<T> {
    return typeof value === 'object' && value !== null && 'then' in value && typeof value.then === 'function';
}

/** Whether a Consumer's answer to a Replace All is one: a count, or the cells refused, under a version. */
function isReplaceResult(value: unknown): value is GridReplaceResult {
    if (typeof value !== 'object' || value === null) return false;
    const { kind, count, cells, rowSequenceVersion } = value as {
        readonly kind?: unknown; readonly count?: unknown; readonly cells?: unknown; readonly rowSequenceVersion?: unknown;
    };
    if (typeof rowSequenceVersion !== 'number') return false;
    return (kind === 'replaced' && typeof count === 'number' && Number.isSafeInteger(count) && count >= 0)
        || (kind === 'refused' && Array.isArray(cells) && cells.length > 0);
}

/** Sets an attribute on an element the grid made, or takes it away, only when it changed. */
function setAttribute(element: Element, name: string, value: string | undefined): void {
    if (value === undefined) {
        if (element.hasAttribute(name)) element.removeAttribute(name);
    } else if (element.getAttribute(name) !== value) {
        element.setAttribute(name, value);
    }
}
const defaultViewportHeightPx = 600;

/** How long scrolling must have stood still before a fling's Placeholders are painted over (ADR-0004), and
 * a Selection before it is announced (ADR-0033). */
const settleDelayMs = 150;

/** How often a drag held in an edge band scrolls a step (ADR-0008). */
const edgeScrollTickMs = 50;

/** The keys whose own keydown is no key of the grid's: they neither take nor end a release of Tab. */
const modifierKeys: ReadonlySet<string> = new Set(['Control', 'Shift', 'Alt', 'Meta', 'AltGraph', 'CapsLock']);

/** The built-in Chrome's words for a stylesheet of another version (ADR-0123, ADR-0203). */
const stylesheetNoticeWords = 'This page holds another version\'s stylesheet. Reload the page.';

/** What each instance's ids begin with, so two grids on a page never share one (ADR-0018). */
let instances = 0;

/** A row as it was last painted: what decides whether it is painted again (ADR-0003). */
interface PaintedRow<Row> {
    readonly element: HTMLElement;
    readonly row: Row;
    readonly index: number;
    readonly styles: ColumnStyles;
    readonly columns: readonly ResolvedColumn<Row>[];
    readonly placeholder: boolean;
    readonly scrollable: ColumnRange | undefined;
    /** What its cells' Match Marks were judged by, and the column of the current match in it, or -1. */
    readonly marks: MatchMarks<Row> | undefined;
    readonly current: number;
    /** The painted cells its marks fell on, so a new search repaints only the rows whose marks moved. */
    readonly matched: string;
    /** The per-cell declarations it was painted under, and its cells' appearance (ADR-0050, item 15). */
    readonly paint: CellPaint<Row>;
    readonly appearance: RowAppearance | undefined;
    /** The chosen action of an Interactive cell in it, as `column:action`, or empty (ADR-0037). */
    readonly chosen: string;
    /** The Template slots it painted (ADR-0020). */
    readonly slots: readonly MountedTemplate<Row>[];
    /** Whether its Mark Column's checkbox was painted marked (ADR-0043): a changed mark paints this row again
     * and no other (MK-4). */
    readonly marked: boolean;
}

/**
 * What the painted cells are judged by while the find panel holds a search (ADR-0332 §4): the rule and the
 * pattern, made once per search and kept while neither changes, so a row whose marks did not move is not
 * painted again (ADR-0003). The reference rule is Find's own over the displayed text.
 */
class MatchMarks<Row> {
    readonly rule: ((row: Row, column: GridColumn<Row>, pattern: GridFindPattern) => boolean) | undefined;
    readonly pattern: GridFindPattern;
    readonly #judge: (row: Row, column: GridColumn<Row>) => boolean;
    readonly #matcher: (shown: string) => boolean;

    /** `rule` undefined is the reference rule. */
    constructor(rule: ((row: Row, column: GridColumn<Row>, pattern: GridFindPattern) => boolean) | undefined, pattern: GridFindPattern) {
        this.rule = rule;
        this.pattern = pattern;
        const matches = matcherOf(pattern);
        this.#matcher = matches;
        if (rule === undefined) {
            this.#judge = (row, column) => matches(textFor(column, column.value(row)));
        } else {
            this.#judge = (row, column) => {
                const answer: unknown = rule(row, column, pattern);
                if (typeof answer !== 'boolean') {
                    throw new TypeError(`findMatch answered ${typeof answer} for column '${column.name}': it answers whether the cell matches, true or false (ADR-0332).`);
                }
                return answer;
            };
        }
    }

    same(rule: MatchMarks<Row>['rule'], held: FindHeld): boolean {
        return rule === this.rule && held.text === this.pattern.text && held.matchCase === this.pattern.matchCase
            && held.wholeCell === this.pattern.wholeCell;
    }

    /** Whether the cell matches; `shown` is its displayed text where the caller has it already, which the
     * reference rule judges. */
    matches(row: Row, column: GridColumn<Row>, shown?: string): boolean {
        return shown !== undefined && this.rule === undefined ? this.#matcher(shown) : this.#judge(row, column);
    }
}

/** What the header band marks (ADR-0333 §1): each column header's and each Header Group's mark, judged once per
 * search, columns, layout and current header, never per frame. */
interface HeaderMatches<Row> {
    readonly pattern: GridFindPattern;
    readonly columns: readonly ResolvedColumn<Row>[];
    readonly layout: HeaderGroupLayout;
    readonly current: string | undefined;
    readonly leaves: readonly MatchMark[];
    readonly groups: ReadonlyMap<string, MatchMark>;
}

/** The options as `update` takes them: checked and resolved, and not yet the grid's. */
interface Taken<Row> {
    readonly source: GridSource<Row>;
    /** The source when the grid made it over `rows`. */
    readonly own: InMemoryGridSource<Row> | undefined;
    readonly rows: readonly Row[] | undefined;
    readonly metrics: GridMetrics;
    readonly declared: readonly GridColumn<Row>[];
    readonly columns: readonly ResolvedColumn<Row>[];
    readonly autoWidths: readonly AutoWidth[];
    readonly columnsChanged: boolean;
    readonly width: ViewportSize;
    readonly height: ViewportSize;
    readonly pinnedCount: number;
    readonly virtualiseColumns: boolean;
    readonly headerGroups: readonly HeaderGroup[];
    readonly headerLayout: HeaderGroupLayout;
    readonly onSelectionChanged: ((selection: GridSelection) => void) | undefined;
    readonly copyCellCap: number;
    readonly copyAnswer: ((request: GridCopyRequest) => GridCopyAnswer) | undefined;
    readonly onRefused: ((refusal: GridRefusal<Row>) => void) | undefined;
    readonly onAction: ((event: GridActionEvent<Row>) => void) | undefined;
    /** Who holds the marks of the Mark Column, while one is declared. */
    readonly marks: RowMarks<Row> | undefined;
    readonly onPaste: ((intent: GridPasteIntent) => void) | undefined;
    readonly pasteMaySpill: boolean;
    readonly highlightFocusRow: boolean;
    readonly highlightHoverRow: boolean;
    readonly pasteByteCap: number;
    readonly onClear: ((intent: GridClearIntent) => void) | undefined;
    readonly onUndo: (() => void) | undefined;
    readonly onRedo: (() => void) | undefined;
    readonly onEdit: ((intent: GridEditIntent<Row>) => void) | undefined;
    readonly onEditingChanged: ((open: boolean) => void) | undefined;
    readonly onEditDiscarded: ((reason: EditDiscardReason) => void) | undefined;
    readonly onOverwriteNotice: ((notice: GridOverwriteNotice) => void) | undefined;
    readonly onCellDoubleClick: ((cell: CellPosition) => void) | undefined;
    readonly onLeave: (() => void) | undefined;
    readonly onColumnWidthChanged: ((change: GridColumnWidthChange) => void) | undefined;
    readonly onColumnOrderChanged: ((order: readonly string[]) => void) | undefined;
    readonly contextCommands: ((context: GridContextMenuContext<Row>) => readonly GridCommand[]) | undefined;
    readonly commandLabel: ((id: string) => string | undefined) | undefined;
    readonly hideCopyWithHeaders: boolean;
    readonly hideColumnMenu: boolean;
    readonly onColumnHidden: ((column: string) => void) | undefined;
    readonly onPinnedCountChanged: ((count: number) => void) | undefined;
    readonly culture: GridCulture;
    readonly temporal: TemporalLike | undefined;
    readonly onReplace: ((request: GridReplaceRequest) => GridReplaceResult | Promise<GridReplaceResult>) | undefined;
    readonly findMatch: ((row: Row, column: GridColumn<Row>, pattern: GridFindPattern) => boolean) | undefined;
    readonly findsHeaders: boolean;
    readonly cellPaint: CellPaint<Row>;
    readonly headerClickSelects: boolean;
    readonly dataEdge: EdgeAnswer | undefined;
    readonly onSummarize: SummaryAnswerer | undefined;
    readonly showSelectionSummary: boolean;
    readonly summaryFigures: readonly SummaryFigure[];
    readonly onSelectionSummaryChanged: ((summary: SelectionSummary) => void) | undefined;
    readonly onSummaryFiguresChanged: ((figures: readonly SummaryFigure[]) => void) | undefined;
    readonly formulaBar: FormulaBarDeclarations<Row>;
}

// The popover standing (ADR-0039, ADR-0040): its element and the commands it shows, and where it stands. The
// Context Menu is held where it was opened in the Viewport's readable box and against what: an order, columns
// or a source that moved take the Selection its commands act on, and the menu with it (ADR-0011, ADR-0036).
// A column's menu stands under its column's header, followed by the column's name (ADR-0010).
// The find panel stands in the grid's top right under the header, and holds no commands (ADR-0055).
// A Consumer's popover stands under the header band, centred across the grid (ADR-0050, item 16).
type OpenPopover = MenuPopover | { readonly kind: 'find'; readonly element: HTMLElement; readonly panel: FindPanel }
    | {
        readonly kind: 'consumer'; readonly element: HTMLElement; readonly frame: ConsumerPopoverFrame; readonly opening: object;
        // The grid's own contents in the frame, the figures menu, which the Consumer's closePopover leaves alone.
        readonly own: boolean;
    };

type MenuPopover = {
    readonly element: HTMLElement;
    readonly commands: readonly GridCommand[];
    readonly items: MenuItems;
} & ({
    readonly kind: 'context';
    readonly leftPx: number;
    readonly topPx: number;
    readonly version: number;
    readonly source: unknown;
} | {
    readonly kind: 'column';
    readonly column: string;
    /** The column's filter, below its commands, where the column can be filtered (ADR-0044). */
    readonly panel: FilterPanel | undefined;
});

/** A popover about to stand: everything but the items #standMenu makes. */
type StandingPopover = MenuPopover extends infer P ? (P extends unknown ? Omit<P, 'items'> : never) : never;

// A Selection the rows or the source dropped: where the typing aimed at it was going, why it went, and whether
// the Consumer has heard of typing thrown away with it (ADR-0142).
interface DroppedAim {
    readonly focus: CellPosition | undefined;
    readonly reason: EditDiscardReason;
    /** The action chosen in the Focus's cell while it was Interactive (ADR-0037). */
    readonly chosenAction: number | undefined;
    told: boolean;
}

/** A Template cell's content as mounted (ADR-0020): its slot, what it was mounted with, and how to take it
 * away. `cell` is undefined while a pass owes it a mount. */
interface MountedTemplate<Row> {
    readonly slot: HTMLElement;
    template: GridTemplate<Row>;
    row: Row;
    readonly column: string;
    cell: GridTemplateCell<Row> | undefined;
    unmount: (() => void) | undefined;
}

/** A row pressed on, as its element was painted: its row, its position, and the order and source it was
 * painted under (ADR-0142). */
interface PressedRow<Row> {
    readonly row: Row;
    readonly index: number;
    readonly sequence: number;
    readonly source: unknown;
}

/** A press on an action's button, from its mousedown until its click (ADR-0142): what the button stood for
 * when it was pressed. */
interface ActionPress<Row> extends PressedButton<Row> {
    readonly button: Element;
    readonly pressed: PressedRow<Row>;
}

/** What an action's button stands for: its column as painted, and the action. */
interface PressedButton<Row> {
    readonly column: ResolvedColumn<Row>;
    readonly action: GridAction;
}

/** What a row being painted is told and gathers (ADR-0037, ADR-0020): its key, the chosen action of an
 * Interactive cell in it, the Template slots this pass has painted so far, and the ones it paints. */
interface RowPaint<Row> {
    readonly key: unknown;
    readonly chosen: { readonly column: number; readonly action: number } | undefined;
    readonly marked: boolean;
    readonly used: Set<MountedTemplate<Row>>;
    readonly slots: MountedTemplate<Row>[];
    /** Its cells' appearance (ADR-0050, item 15). */
    readonly appearance: RowAppearance | undefined;
}

/** A reveal's repaint under way (ADR-0012, 2026-10-08): where the reveal left the scroller, and what stops
 * the frame it waits for. */
interface Repaint {
    readonly top: number;
    readonly left: number;
    cancel: () => void;
}

// A copy past the Window as gathered (ADR-0005): its flavours, and the copy to outline once they are written,
// where the browser can say the clipboard changed (ADR-0170).
interface GatheredCopy<Row> {
    readonly flavours: CopyFlavours;
    /** Made once the write lands, so the cells' text is read again off the write's path; none where the copy is
     * not outlined, which still replaces the outline before. */
    readonly outline: () => CopiedRange<Row> | undefined;
}

// A write of the grid's own by the asynchronous route (ADR-0170): what was gathered, once in hand; the write as
// counted in the document's ledger of writes the browser has yet to report, once it is; and whether the write
// has already ended, after which it is never counted.
interface OwnWrite<Row> {
    gathered: GatheredCopy<Row> | undefined;
    counted: LedgerWrite | undefined;
    ended: boolean;
}

/**
 * A write the grid raised for one of the user's own gestures (ADR-0142, D1) — an Edit Intent, a paste or fill
 * intent, a Clear Intent — until it settles: the source, the order and the columns its positions are written
 * in, and the cells of it the Window held when it was raised. Positions only, never a row or a string
 * (ADR-0160).
 */
interface UserWrite {
    readonly source: unknown;
    readonly sequence: number;
    /** The columns its positions are written among, by name: other columns name other cells. */
    readonly columnNames: readonly string[];
    /** Its cells, by row and then column. */
    readonly cells: Map<number, Set<number>>;
    /** How many cells it follows. */
    cellCount: number;
    /** Whether its intent's handler has returned, and did not refuse it. */
    answered: boolean;
    /** Whether a new Window has been taken in since it was raised. */
    windowTakenIn: boolean;
}

// The bounds on what D1 follows, for a Consumer that never hands over a new Window: cells per write, the
// painted rows first, and writes kept. Beyond them a cell is not followed, and an editor opened on it tells
// the user's own write arriving under it as a change, erring towards a notice (ADR-0142).
const userWriteCellsFollowed = 1024;
const userWritesKept = 64;

/** An edit open in the Cell Editor (ADR-0010, ADR-0142). Its text is the editor's own field: nothing the grid
 * holds writes it back while the user types (the bind trap the C# core names, SRV-7). */
interface OpenEdit<Row> {
    /** Where the editor stands: the cell it opened on, followed through an order move where a Row Key is in
     * force (ADR-0011's note of 2026-10-07). */
    cell: CellPosition;
    /** Overwrite, Excel's Enter mode, where the arrows commit and move; or Caret, where they are the field's. */
    mode: 'overwrite' | 'caret';
    readonly element: HTMLInputElement;
    /** The source, the columns and the Row Sequence Version it opened under. */
    readonly source: GridSource<Row>;
    readonly columnNames: readonly string[];
    readonly sequence: number;
    /** The Row Key of the row it opened on, where one is in force: the third holding ADR-0160 names. */
    readonly key: RowKey | undefined;
    /** What the user saw of the cell (ADR-0142, D1): what it painted as the editor opened, or, once a write
     * of the user's own to it that had not settled then has settled, what it painted at that moment. The
     * intent's seen text. */
    seenText: string;
    /** The user's own writes over its cell, unsettled, when it opened (D1). */
    readonly awaits: Set<UserWrite>;
    /** One of them settled or went as a Window was taken in: the seen text is read again once the editor has
     * followed its row. */
    rebaseOwed: boolean;
    /** The row instance it opened on: what a fill is judged against once the Window no longer holds its row. */
    readonly openedOn: Row;
    /** The sentence a Reject or a refusal holds it with (ADR-0034), shown at it while it stands. */
    error: string | undefined;
    /** Where the text is typed: the Cell Editor over the cell, or the Formula Bar, its second surface (ADR-0051). */
    surface: 'cell' | 'bar';
}

// What the Formula Bar last read of the Focus, and what it was read through (ADR-0051).
interface BarReading<Row> {
    readonly focus: CellPosition | undefined;
    readonly row: Row | undefined;
    readonly declared: FormulaBarDeclarations<Row>;
    readonly cellPaint: CellPaint<Row>;
    readonly columns: readonly ResolvedColumn<Row>[];
    readonly commandLabel: ((id: string) => string | undefined) | undefined;
    readonly editHeard: unknown;
    readonly placeholder: boolean;
    readonly text: string;
    readonly borrowed: boolean;
    readonly named: string;
    readonly editable: boolean;
    readonly nameBoxLabel: string;
    readonly textLabel: string;
}

/** The refusals the grid words itself (ADR-0203); a commit's is the Consumer's to word (A11Y-16). */
type WordedRefusal = Extract<GridRefusal, { readonly kind: 'copy' | 'paste' }>;

/** A page follow (ADR-0204): armed by a move for the Focus it moved to and the cell its reveal brought into
 * the Viewport, and asked once that cell is painted. */
interface Follow {
    readonly focus: CellPosition;
    readonly target: CellPosition;
}

/** Whether two lists hold the same ranges, in order. */
function sameRanges(a: readonly SelectionRange[], b: readonly SelectionRange[]): boolean {
    return a.length === b.length && a.every((range, i) => sameSelectionRange(range, itemAt(b, i)));
}

/** Whether two arrays hold the same items, in the same order. */
function sameItems<T>(a: readonly T[], b: readonly T[]): boolean {
    return a === b || (a.length === b.length && a.every((item, i) => item === b[i]));
}

/** Whether two declarations of Header Groups say the same. They are plain data, compared by value, so a
 * Consumer that writes them inline does not build the header band again on every update (ADR-0032). */
function sameGroups(a: readonly HeaderGroup[], b: readonly HeaderGroup[]): boolean {
    return a === b || (a.length === b.length && a.every((group, i) => {
        const other = itemAt(b, i);
        return group.label === other.label && group.tier === other.tier && group.tierSpan === other.tierSpan
            && sameItems(group.columns, other.columns);
    }));
}

/** The rows the header stands for in the accessibility tree, ahead of the data rows: the header row and a
 * tier per level of Header Groups above it (ADR-0032/0033). */
function headerRowsOf(layout: HeaderGroupLayout): number {
    return 1 + layout.tierCount;
}

/** The header band's height, the one figure the refusal, the band and the rows' room all read (ADR-0032). */
function bandHeightPxOf(layout: HeaderGroupLayout, metrics: GridMetrics): number {
    return headerRowsOf(layout) * metrics.headerHeightPx;
}

/** A header cell as painted, with what its classes are composed from. */
interface HeaderCell {
    readonly element: HTMLElement;
    readonly column: number;
    readonly align: GridColumn<unknown>['align'];
    readonly pinned: boolean;
}

/** The Heading Mark's runs, made once per Selection and extent (ADR-0008, 2026-10-13). */
interface HeadingMarkRuns {
    readonly selection: Selection;
    readonly extent: GridExtent;
    readonly columns: readonly ColumnRange[];
    readonly whole: readonly ColumnRange[];
}

/** The Heading Mark as painted over the header: the runs and the geometry it was painted from, its
 * rectangles' styles and their elements, so a pass that changes none of them touches nothing (PF-3). */
interface PaintedHeadingMarks {
    readonly marks: HeadingMarkRuns;
    /** The header band's Match Marks it was painted with (ADR-0333), by identity. */
    readonly matches: unknown;
    readonly styles: ColumnStyles;
    readonly topPx: number;
    readonly heightPx: number;
    readonly pinnedStyles: readonly string[];
    readonly scrollableStyles: readonly string[];
    readonly elements: { readonly pinned: HTMLElement | undefined; readonly scrollable: readonly HTMLElement[] };
}

/** Whether two runs of columns are the same run. */
function sameRange(a: ColumnRange | undefined, b: ColumnRange | undefined): boolean {
    return a === b || (a !== undefined && b !== undefined && a.start === b.start && a.count === b.count);
}

function sizeOf(size: ViewportSize | undefined, fallback: number, name: string): ViewportSize {
    if (size === undefined) return fallback;
    if (size === 'stretch') return size;
    if (typeof size !== 'number' || !Number.isFinite(size) || size <= 0) {
        throw new RangeError(`${name} is a finite, positive number of pixels or 'stretch', not ${String(size)} (ADR-0028).`);
    }
    return size;
}

/** The stretch half of the root's style (ADR-0028, "Which element takes the box"). */
function stretchRootStyle(width: ViewportSize, height: ViewportSize): string {
    if (height !== 'stretch') return width === 'stretch' ? '; min-width: 0' : '';
    return width === 'stretch'
        ? '; display: flex; flex-direction: column; height: 100%; flex: 1 1 auto; min-height: 0; min-width: 0'
        : '; display: flex; flex-direction: column; height: 100%; flex: 1 1 auto; min-height: 0';
}

/**
 * One grid in one element. Made by `createGrid`, which hands back its handle; nothing outside the core
 * holds it.
 */
export class GridView<Row> {
    readonly #root: HTMLElement;
    readonly #reports: BrowserReports;
    readonly #ids: string;
    readonly #scroller: HTMLElement;
    readonly #spacer: HTMLElement;
    readonly #header: HTMLElement;
    readonly #viewport: HTMLElement;
    readonly #stamp: HTMLElement;
    readonly #announce: HTMLElement;
    readonly #shadeLayer: HTMLElement;
    readonly #pinnedLayer: HTMLElement;
    readonly #selectionLayer: HTMLElement;
    readonly #metaIsPrimary: boolean;
    readonly #stops: (() => void)[] = [];

    // What the Consumer handed over.
    #declared: readonly GridColumn<Row>[] = [];
    #columns: readonly ResolvedColumn<Row>[] = [];
    #metrics = GridMetrics.resolve({ density: 'compact' });
    #source: GridSource<Row>;
    #ownSource: InMemoryGridSource<Row> | undefined;
    #ownRows: readonly Row[] | undefined;
    // Set once the first source is subscribed to, which is the grid holding a source at all.
    #unsubscribe: (() => void) | undefined;
    #state: GridSourceState<Row>;
    #sequenceOffset = 0;
    #width: ViewportSize = defaultViewportWidthPx;
    #height: ViewportSize = defaultViewportHeightPx;
    #pinnedCount = 0;
    #virtualiseColumns = true;
    // The Header Groups as taken, and resolved against the columns and the pinned count (ADR-0032).
    #headerGroups: readonly HeaderGroup[] = [];
    #headerLayout = HeaderGroupLayout.empty;
    #onSelectionChanged: ((selection: GridSelection) => void) | undefined;
    #copyCellCap = defaultCellCap;
    #copyAnswer: ((request: GridCopyRequest) => GridCopyAnswer) | undefined;
    #onRefused: ((refusal: GridRefusal<Row>) => void) | undefined;
    #onAction: ((event: GridActionEvent<Row>) => void) | undefined;
    // Interactive over the core's own actions (ADR-0037): the cell Space entered and the action chosen in it.
    // The keyboard never leaves the root; this is what the arrows move, what Space fires and what the row
    // paints. Valid only while the Focus is still that cell (#interactiveChoice).
    #interactive: { readonly cell: CellPosition; readonly action: number } | undefined;
    // A Template cell's focus request (ADR-0037): pending from the Space that asked until the pass that paints
    // the cell hands it over, then gone, so a cell mounted again later is never handed it. Dropped if the
    // Focus moves first.
    #focusRequest: { readonly cell: CellPosition; readonly number: number } | undefined;
    #focusRequests = 0;
    // The Template cells' content as mounted (ADR-0020), by row key and column: a cell painted again keeps its
    // slot, and its content is mounted again into it only when its row instance changed or a request is handed
    // over. Mounts wait until the rows are in the DOM.
    readonly #templates = new Map<unknown, Map<string, MountedTemplate<Row>>>();
    // What each painted row element was painted for, and the order and source the last pass painted under, so
    // a press on one of its buttons names its row (ADR-0142). Every row in the DOM is the last pass's.
    readonly #rowsPainted = new WeakMap<Element, { readonly row: Row; readonly index: number }>();
    #paintedUnder: { readonly sequence: number; readonly source: unknown } | undefined;
    // Which action each painted button stands for, by column and position in it.
    readonly #actionButtons = new WeakMap<Element, PressedButton<Row>>();
    #actionPress: ActionPress<Row> | undefined;
    // The Row Marks (ADR-0043): who holds them, the header's checkbox and the count; every painted row's
    // checkbox, with whether it was painted marked; and a press on one, from its mousedown until its click, with
    // the row it was painted in, or on the header's, with the order and source it was made under (MK-9).
    readonly #rowMarks: GridMarks<Row>;
    readonly #markBoxes = new WeakMap<Element, boolean>();
    #markPress: { readonly box: Element; readonly pressed: PressedRow<Row> } | undefined;
    #headerMarkPress: { readonly sequence: number; readonly source: unknown } | undefined;
    // The status line after the rows (ADR-0014): the marks' count and the Selection Summary, standing while either
    // has its place in it.
    readonly #status: HTMLElement;
    #statusShown: readonly HTMLElement[] = [];
    // The Selection Summary (ADR-0130): who answers it, the figures shown, who hears it, the question in force and
    // its box, and how many times the rows under a standing selection may have moved.
    #onSummarize: SummaryAnswerer | undefined;
    #showSelectionSummary = true;
    #summaryFigures: readonly SummaryFigure[] = defaultSummaryFigures;
    #onSelectionSummaryChanged: ((summary: SelectionSummary) => void) | undefined;
    #onSummaryFiguresChanged: ((figures: readonly SummaryFigure[]) => void) | undefined;
    // The figures menu while it stands, or since it last did (ADR-0130), and the figures it last reported, until the
    // Consumer hands others back.
    #summaryMenu: SummaryMenu | undefined;
    #summaryChoice: readonly SummaryFigure[] | undefined;
    readonly #summary: SummaryQuestions;
    readonly #summaryBox: SummaryBox;
    #summaryRowsStamp = 0;
    #pasteHeard: ((intent: GridPasteIntent) => void) | undefined;
    #pasteMaySpill = false;
    #highlightFocusRow = false;
    #highlightHoverRow = false;
    // The row under the pointer, while the hover band is on and the pointer is over a row (ADR-0029).
    #hoverRow: number | undefined;
    #pasteByteCap = defaultPasteByteCap;
    #editHeard: ((intent: GridEditIntent<Row>) => void) | undefined;
    #onEditingChanged: ((open: boolean) => void) | undefined;
    #onEditDiscarded: ((reason: EditDiscardReason) => void) | undefined;
    #onOverwriteNotice: ((notice: GridOverwriteNotice) => void) | undefined;
    #onCellDoubleClick: ((cell: CellPosition) => void) | undefined;
    #leaveHeard: (() => void) | undefined;
    // Whether the grid answered the Escape now held down, whose repeats it then takes and does nothing with (DC-62).
    #escapeAnswered = false;
    // The copies the asynchronous route is still writing, each holding the Settled Mark (ADR-0220), and what
    // stops the latest one's reads of the source when another copy supersedes it or the grid is destroyed.
    #clipboardWrites = 0;
    #copy: AbortController | undefined;
    #clearHeard: ((intent: GridClearIntent) => void) | undefined;
    #onUndo: (() => void) | undefined;
    #onRedo: (() => void) | undefined;
    // A fill key's rows asked of the source, while they are: a newer fill, or the grid destroyed, stops it.
    #fill: AbortController | undefined;
    // The Copied Range (ADR-0170): the copy outlined, and the ledger every grid of the document counts the
    // writes of its copies in, which says whose write a change of the clipboard reports. A browser that cannot
    // report a change outlines nothing.
    #copied: CopiedRange<Row> | undefined;
    readonly #ledger: ClipboardLedger;
    readonly #clipboardWatched: boolean;

    // The Selection (ADR-0011, ADR-0012), held under the Row Sequence Version it was made under, and the
    // columns it was made over, by name: either changing drops it.
    // The Cell Editor (ADR-0010) and what stands for the keyboard while none is open (ADR-0080).
    #edit: OpenEdit<Row> | undefined;
    // Whether an edit is open as the Consumer last heard it (ADR-0050 section 6).
    #heardOpen = false;
    // The editor asks for the keyboard once it is painted: on opening, and after a gesture it held.
    #editorFocusOwed = false;
    // The user's own writes that have not settled, oldest first (ADR-0142, D1).
    readonly #userWrites: UserWrite[] = [];
    readonly #keyFieldLayer: HTMLElement;
    readonly #keyField: HTMLInputElement;
    // The Keyboard Field's composition (ADR-0080): whether an IME composes there now; how much of the field's
    // value has been handed on, so a second composition finished before the keyboard left carries only its
    // own text; and whether a key the IME took there is still being told, from its keydown to the next keyup
    // the root hears. The editor's request for the keyboard waits while either holds: moving DOM focus would
    // end a composition half-typed, or carry a key that starts the next one into the editor without its
    // romaji (the sixteenth Windows run's k6).
    #composing = false;
    #carried = 0;
    #imeKeyTold = false;
    readonly #editorPinned: HTMLElement;
    readonly #message: HTMLElement;
    // Whether the keyboard last came to the grid by a press, which a text field's :focus-visible cannot tell
    // (KB-12, ADR-0080).
    #keyboardByPress = false;
    #held = HeldSelection.empty(0);
    // The source the held Selection was last reconciled with, to tell another source from a moved order.
    #heldSource: GridSource<Row> | undefined;
    // A Selection the rows or the source dropped since the last paint: what a key typed meanwhile was aimed at
    // (ADR-0142).
    #droppedAim: DroppedAim | undefined;
    // Every Selection the rows or the source dropped, counted, and the last: a composition is aimed at what was
    // painted when it began, however many paints come before it ends (ADR-0142).
    #drops = 0;
    #lastDrop: DroppedAim | undefined;
    // What a composition in the Keyboard Field was aimed at as it began: the drops counted by then, and the
    // Selection dropped before it that no paint had shown yet.
    #composedAim: { readonly drops: number; readonly dropped: DroppedAim | undefined } | undefined;
    #columnNames: readonly string[] = [];
    // The Selection the Consumer last heard.
    #raised: Selection = Selection.empty;
    #dragging = false;
    // Escape with nothing to dismiss releases Tab: the next Tab is the browser's (ADR-0012).
    #tabReleased = false;
    // The inputs answered, written on the stamp by the pass that paints the answer (ADR-0220).
    #answered = 0;
    #revealOwed: KeyReveal | undefined;
    #revealToken = 0;
    #repaint: Repaint | undefined;
    #followArmed: Follow | undefined;
    #followToken = 0;
    #cancelFollow: (() => void) | undefined;
    // The live region (ADR-0033): what it says, and the ranges it last described.
    #announcedRanges: readonly SelectionRange[] = [];
    #describesSelection = false;
    #cancelAnnounce: (() => void) | undefined;

    // What the browser told (ADR-0013, ADR-0028, ADR-0053, ADR-0090).
    #viewportReport: ViewportReport | undefined;
    #ceilingPx: number | undefined;
    #devicePixelRatio: number | undefined;
    #scrollTopPx = 0;
    #scrollLeftPx = 0;

    // What a pass resolves.
    #geometry: ViewportGeometry | undefined;
    #visible: RowRange | undefined;
    #styles: ColumnStyles;
    #scrollable: ColumnRange | undefined;
    #autoWidths: readonly AutoWidth[] = [];
    #measuredWindow: readonly Row[] | undefined;
    #measuredWindowStart = -1;
    #measuredVisible: RowRange | undefined;
    #placeholderMode = false;
    #cancelSettle: (() => void) | undefined;
    #lastRaised: { readonly range: RowRange; readonly windowStart: number; readonly windowCount: number } | undefined;

    // What is painted.
    #painted = new Map<unknown, PaintedRow<Row>>();
    #paintedHeader: {
        readonly styles: ColumnStyles; readonly columns: readonly ResolvedColumn<Row>[]; readonly scrollable: ColumnRange | undefined;
        readonly sorts: string; readonly layout: HeaderGroupLayout; readonly tierPx: number; readonly grips: boolean;
        readonly menus: boolean;
    } | undefined;
    // The tiers of Header Groups as painted, and the leaf row beneath them: the rectangles stand on the
    // column geometry, the layout and the tier height alone, so a scroll or a Sort that rebuilds the leaves
    // keeps them (PF-3).
    #paintedTiers: {
        readonly styles: ColumnStyles; readonly columns: readonly ResolvedColumn<Row>[]; readonly layout: HeaderGroupLayout;
        readonly tierPx: number; readonly rows: readonly HTMLElement[]; readonly leaves: HTMLElement;
        readonly groups: readonly { readonly element: HTMLElement; readonly id: string }[];
    } | undefined;
    // The header cells as painted, each with its class before the Heading Mark's, and the mark's runs and
    // rectangles (ADR-0008, 2026-10-13): made once per Selection and extent, and the rectangles once per
    // Selection, column geometry and band, since a pass runs on every scroll frame (PF-3).
    #headerCells: readonly HeaderCell[] = [];
    #marks: HeadingMarkRuns | undefined;
    #paintedMarks: PaintedHeadingMarks | undefined;
    // The placeholder rows on screen, by position: a position still waiting keeps its element (ADR-0003).
    #placeholders = new Map<number, HTMLElement>();
    readonly #written = new Map<string, string>();
    #layoutId = 0;
    #paintId = 0;
    #layoutStyles: ColumnStyles | undefined;
    #paintedLayers: string | undefined;
    #visibleWidthPx = 0;
    #visibleHeightPx = 0;

    // A Heading drag (ADR-0050 item 1, ADR-0012, 2026-09-29), a press on the header that is not yet a click
    // or a drag, where the press was, and a click a gesture has already answered.
    #headingDrag = false;
    #headerPress: { readonly column: number; readonly extends: boolean; readonly toggles: boolean } | undefined;
    #headingPress = { x: 0, y: 0, clientX: 0, clientY: 0 };
    #gestureConsumedClick = false;
    // A drag on the header's grip or of a header (ADR-0011, ADR-0016): what it holds, and the one element it
    // paints, the resize guide or the drop indicator, over the band and the rows.
    #onColumnWidthChanged: ((change: GridColumnWidthChange) => void) | undefined;
    #onColumnOrderChanged: ((order: readonly string[]) => void) | undefined;
    #columnGesture: ColumnGesture | undefined;
    #gestureLine: HTMLElement | undefined;
    // Each painted grip's column, so a press tells this grid's grips from anything else in its header.
    #grips = new Map<Element, number>();
    #reorderDrops: { readonly geometry: ColumnGeometry; readonly layout: HeaderGroupLayout; readonly boundaries: readonly number[] } | undefined;
    // The Context Menu (ADR-0036): what the Consumer adds to it and calls its commands, and the menu standing.
    #contextCommands: ((context: GridContextMenuContext<Row>) => readonly GridCommand[]) | undefined;
    #commandLabel: ((id: string) => string | undefined) | undefined;
    #hideCopyWithHeaders = false;
    #popover: OpenPopover | undefined;
    // The column menu (ADR-0010): whether the headers carry a ▾, each painted ▾'s column, and the commands'
    // sinks the Consumer gave.
    #hideColumnMenu = false;
    #menuButtons = new Map<Element, string>();
    #onColumnHidden: ((column: string) => void) | undefined;
    #onPinnedCountChanged: ((count: number) => void) | undefined;
    // The culture typed text is read in, and the Temporal a typed date is made with (ADR-0370 section 10).
    #culture: GridCulture = gridCulture(invariantLocale);
    #temporal: TemporalLike | undefined;
    // Find and Replace (ADR-0055, ADR-0332, ADR-0333). What the panel holds outlives it, as Excel's dialog's
    // does. A step out with the source is cancelled by the next, or by the panel closing; its answer is
    // somebody else's then. The current match is the cell a found step moved the Focus to, under the version
    // it was found in, forgotten the moment either moves; a header found is current under the Selection it
    // was found under. The marks are judged once per search, never per frame (ADR-0332 §4).
    #onReplace: ((request: GridReplaceRequest) => GridReplaceResult | Promise<GridReplaceResult>) | undefined;
    #findMatch: ((row: Row, column: GridColumn<Row>, pattern: GridFindPattern) => boolean) | undefined;
    #findsHeaders = true;
    // The per-cell declarations (ADR-0050, items 6, 7, 11 and 15), the row appearances resolved from them, and
    // the generated stylesheet their classes name, last in the root (ADR-0071).
    #cellPaint: CellPaint<Row> = CellPaint.empty();
    // The Formula Bar (ADR-0051): its declarations, and its band, made once and in the root while it is shown.
    #formulaBarDeclared: FormulaBarDeclarations<Row> = { shown: false, nameBoxLabel: undefined, onNameBoxEntered: undefined, borrowedText: undefined };
    readonly #bar: FormulaBar;
    #barRead: BarReading<Row> | undefined;
    readonly #appearances = new RowAppearances<Row>();
    #appearanceStyle: HTMLStyleElement | undefined;
    #appearanceVersion = -1;
    #appearanceWritten = 0;
    #headerClickSelects = false;
    // The Consumer's answer to where Ctrl+arrow stops (ADR-0050, item 2), or the grid's edge.
    #dataEdge: EdgeAnswer | undefined;
    readonly #findHeld: FindHeld = { text: '', matchCase: false, wholeCell: false, replaceText: '' };
    #findOutcome: FindOutcome = { kind: 'none' };
    #findStepping: AbortController | undefined;
    #replacing = 0;
    #findCurrent: { readonly cell: CellPosition; readonly version: number } | undefined;
    #findCurrentHeader: {
        readonly id: string; readonly selection: Selection; readonly columns: readonly ResolvedColumn<Row>[]; readonly layout: HeaderGroupLayout;
    } | undefined;
    #revealHeaderOwed: { readonly first: number; readonly last: number } | undefined;
    #matchMarks: MatchMarks<Row> | undefined;
    #headerMatches: HeaderMatches<Row> | undefined;
    // The edge band (ADR-0008): where the pointer stands in the Viewport's readable box and the step each
    // tick takes, and an offset a step moved that the next pass writes.
    #edge: { readonly x: number; readonly y: number; readonly rows: number; readonly stepXPx: number } | undefined;
    #cancelEdgeTick: (() => void) | undefined;
    #scrollOwed = false;
    // The views this grid read (readView), each holding the grid's own Selection; the one handed back to be shown
    // when the grid takes the version it names, in place of the empty Selection a new version leaves (ADR-0331).
    readonly #viewsRead = new WeakMap<GridViewRead, Selection>();
    #viewAhead: { readonly selection: Selection; readonly version: number; readonly topPx: number; readonly leftPx: number } | undefined;
    // Whether the offsets taken from a view are still to be kept inside the rows shown, as the browser keeps them.
    #viewScrollUnclamped = false;
    #layerInputs: Readonly<Record<string, unknown>> | undefined;
    #shadeHolds = false;

    // The Settled Mark (ADR-0220).
    #passOwed = false;
    #settledShown: boolean | undefined;
    #destroyed = false;

    constructor(root: HTMLElement, options: GridOptions<Row>, reports: BrowserReports) {
        this.#root = root;
        this.#reports = reports;
        instances += 1;
        this.#ids = `ex${instances}-`;
        const doc = root.ownerDocument;
        this.#ledger = ClipboardLedger.of(doc);
        const element = (tag: string, attributes: Readonly<Record<string, string>>): HTMLElement => {
            const made = doc.createElement(tag);
            for (const [name, value] of Object.entries(attributes)) made.setAttribute(name, value);
            return made;
        };

        // Everything handed over is checked and resolved, and the source told its columns, before anything
        // of the grid's exists: options a grid cannot be drawn from leave nothing behind.
        const taken = this.#take(options);
        this.#prepare(taken, undefined);
        this.#source = taken.source;
        this.#state = taken.source.snapshot();

        // The root's own attributes (ADR-0033): `dir` so a grid on an RTL page is an LTR island, and the
        // root the tab stop the keys are heard on (ADR-0018).
        root.setAttribute('tabindex', '0');
        root.setAttribute('dir', 'ltr');
        root.setAttribute('id', `${this.#ids}root`);
        root.setAttribute('role', 'grid');
        root.setAttribute('aria-multiselectable', 'true');
        root.setAttribute('data-ex-reveal', '0');
        root.setAttribute('data-ex-follow', '0');

        // The core's half of the Settled Mark, as the C# core writes it on the root's first child: every
        // input answered as it is taken, and no layer an Escape would peel (ADR-0220, ADR-0230).
        const stamp = element('span', { hidden: '', 'data-ex-answered': '0', 'data-ex-escape-leaves': '' });
        this.#stamp = stamp;
        // The stylesheet's notice (ADR-0123): shown by a stylesheet of another version, hidden by its own.
        const notice = element('div', {
            'data-ex-notice': 'stylesheet', 'data-ex-stylesheet': stylesheetName, 'data-ex-stamp': stylesheetStamp, style: 'display: none',
        });
        notice.textContent = stylesheetNoticeWords;
        // One scroll container for both axes, the header and the Pinned Columns held by `position: sticky`
        // inside it; tabindex -1 keeps it out of the tab sequence (A11Y-4).
        this.#scroller = element('div', { class: 'ex-scroller', tabindex: '-1', role: 'presentation' });
        this.#spacer = element('div', { class: 'ex-spacer', role: 'presentation' });
        // Its role and place in the tree are the header pass's: a row, or a group of tiers (ADR-0032/0033).
        this.#header = element('div', { class: 'ex-header', 'data-ex-claims': 'mousedown click' });
        this.#viewport = element('div', { class: 'ex-viewport', role: 'presentation', 'data-ex-claims': 'contextmenu' });
        // The selection layers (ADR-0008, ADR-0206): paint, not content, so hidden from the accessibility
        // tree, and the Viewport's first children, so they ride its translation and pass under the pinned
        // cells. The pinned shade layer stands only while it holds a tint.
        this.#shadeLayer = element('div', { class: 'ex-shade-pinned', 'aria-hidden': 'true' });
        this.#pinnedLayer = element('div', { class: 'ex-selection-pinned', 'aria-hidden': 'true' });
        this.#selectionLayer = element('div', { class: 'ex-selection', 'aria-hidden': 'true' });
        // The Keyboard Field (ADR-0080): a text field of the grid's own, the Viewport's first child while some
        // column edits, so an IME has somewhere to start on a selected cell. Unseen; its value is the core's.
        this.#keyFieldLayer = element('div', { class: 'ex-key-field-layer' });
        const keyField = doc.createElement('input');
        for (const [name, value] of Object.entries({
            class: 'ex-key-field', tabindex: '0', autocomplete: 'off', autocapitalize: 'off', spellcheck: 'false',
        })) {
            keyField.setAttribute(name, value);
        }
        this.#keyField = keyField;
        this.#keyFieldLayer.append(keyField);
        // The sticky anchor a Cell Editor over a Pinned Column rides, as the pinned selection layer does.
        this.#editorPinned = element('div', { class: 'ex-editor-pinned' });
        // The message a Reject or a refusal holds the editor with (ADR-0034): a tooltip outside the scroller,
        // which the editor is described by. Not a live region: the root's says it once.
        this.#message = element('div', { class: 'ex-message', id: `${this.#ids}message`, role: 'tooltip' });
        this.#rowMarks = new GridMarks(doc, () => {
            this.#invalidate();
        });
        // The status line sits where the C# core's holds it (ADR-0014, ADR-0043, ADR-0130): after the rows, outside
        // the scroller.
        this.#status = element('div', { class: 'ex-status', role: 'presentation' });
        this.#summary = new SummaryQuestions(() => {
            this.#invalidate();
        });
        this.#summaryBox = new SummaryBox(doc);
        this.#summaryBox.element.addEventListener('contextmenu', this.#onSummaryContextMenu);
        this.#stops.push(() => { this.#summaryBox.element.removeEventListener('contextmenu', this.#onSummaryContextMenu); });
        this.#bar = new FormulaBar(doc, {
            textTyped: this.#onBarTyped, textFocused: this.#onBarFocused, nameBoxEntered: this.#onNameBoxEntered, nameBoxLeft: this.#onNameBoxLeft,
            nameBoxFocused: this.#onNameBoxFocused,
        });
        this.#spacer.append(this.#header, this.#viewport);
        this.#scroller.append(this.#spacer);
        const announce = element('div', { class: 'ex-announce', role: 'status' });
        this.#announce = announce;
        // The Layout Ceiling's probe (ADR-0053): an element declared 2^25 px tall, laid out at the tallest
        // the browser can.
        const probe = element('div', { class: 'ex-ceiling-probe', 'aria-hidden': 'true' });
        const tallest = element('div', { style: 'width: 1px; height: 33554432px' });
        probe.append(tallest);
        root.replaceChildren(stamp, notice, this.#scroller, announce, probe);
        this.#styles = new ColumnStyles(new ColumnGeometry({ widthsPx: [], pinnedCount: 0, viewportWidthPx: 1 }));
        this.#metaIsPrimary = reports.metaIsPrimary();

        // A first pass that throws leaves nothing listening either: there is no handle to stop it with.
        let drawn = false;
        try {
            this.#commit(taken);
            const onScroll = (): void => {
                this.#onScroll();
            };
            this.#scroller.addEventListener('scroll', onScroll, { passive: true });
            // The keys, heard on the root in the capture phase, ahead of anything inside it (ADR-0018).
            root.addEventListener('keydown', this.#onKeyDown, true);
            this.#viewport.addEventListener('mousedown', this.#onPress);
            this.#viewport.addEventListener('mouseup', this.#onRelease);
            this.#viewport.addEventListener('mousedown', this.#onActionPress);
            this.#viewport.addEventListener('click', this.#onActionClick);
            this.#viewport.addEventListener('mousedown', this.#onMarkPress);
            this.#viewport.addEventListener('click', this.#onMarkClick);
            this.#viewport.addEventListener('dblclick', this.#onDoubleClick);
            this.#viewport.addEventListener('contextmenu', this.#onContextMenu);
            root.addEventListener('mousedown', this.#onRootPress, true);
            root.addEventListener('mouseup', this.#onRootRelease, true);
            root.addEventListener('focus', this.#onFocused, true);
            keyField.addEventListener('input', this.#onKeyFieldInput);
            keyField.addEventListener('compositionstart', this.#onKeyFieldComposing);
            keyField.addEventListener('compositionend', this.#onKeyFieldComposed);
            root.addEventListener('keyup', this.#onKeyUp, true);
            this.#header.addEventListener('mousedown', this.#onHeaderPress);
            this.#header.addEventListener('click', this.#onHeaderClick);
            this.#header.addEventListener('dblclick', this.#onHeaderDoubleClick);
            root.addEventListener('focusout', this.#onFocusLeft);
            // The browser's own copy command fires the copy event on the root that holds the keyboard; Ctrl+C is
            // never a key of the grid's, which would prevent the very command that fires it (ADR-0005).
            root.addEventListener('copy', this.#onCopy);
            root.addEventListener('paste', this.#onPaste);
            this.#stops.push(
                () => { this.#scroller.removeEventListener('scroll', onScroll); },
                () => { root.removeEventListener('keydown', this.#onKeyDown, true); },
                () => { this.#viewport.removeEventListener('mousedown', this.#onPress); },
                () => { this.#viewport.removeEventListener('mouseup', this.#onRelease); },
                () => { this.#viewport.removeEventListener('mousedown', this.#onActionPress); },
                () => { this.#viewport.removeEventListener('click', this.#onActionClick); },
                () => { this.#viewport.removeEventListener('mousedown', this.#onMarkPress); },
                () => { this.#viewport.removeEventListener('click', this.#onMarkClick); },
                () => { this.#viewport.removeEventListener('dblclick', this.#onDoubleClick); },
                () => { this.#viewport.removeEventListener('contextmenu', this.#onContextMenu); },
                () => { root.removeEventListener('mousedown', this.#onRootPress, true); },
                () => { root.removeEventListener('mouseup', this.#onRootRelease, true); },
                () => { root.removeEventListener('focus', this.#onFocused, true); },
                () => { keyField.removeEventListener('input', this.#onKeyFieldInput); },
                () => { keyField.removeEventListener('compositionstart', this.#onKeyFieldComposing); },
                () => { keyField.removeEventListener('compositionend', this.#onKeyFieldComposed); },
                () => { root.removeEventListener('keyup', this.#onKeyUp, true); },
                () => { root.removeEventListener('focusout', this.#onFocusLeft); },
                () => { root.removeEventListener('mouseup', this.#onDragEnd); },
                () => { this.#viewport.removeEventListener('mousemove', this.#onDrag); },
                () => { this.#header.removeEventListener('mousedown', this.#onHeaderPress); },
                () => { this.#header.removeEventListener('click', this.#onHeaderClick); },
                () => { this.#header.removeEventListener('dblclick', this.#onHeaderDoubleClick); },
                () => { this.#endColumnGesture(); },
                () => { this.#header.removeEventListener('mousemove', this.#onDrag); },
                () => { root.removeEventListener('mouseleave', this.#onLeave); },
                () => { root.removeEventListener('mousemove', this.#onHover); },
                () => { root.removeEventListener('mouseleave', this.#dropHover); },
                () => { root.removeEventListener('copy', this.#onCopy); },
                () => { root.removeEventListener('paste', this.#onPaste); },
                reports.observeViewport(this.#scroller, report => {
                    this.#viewportReport = report;
                    this.#invalidate();
                }),
                reports.observeCeiling(tallest, ceilingPx => {
                    this.#ceilingPx = ceilingPx;
                    this.#invalidate();
                }),
                reports.observeDevicePixel(ratio => {
                    this.#devicePixelRatio = ratio;
                    this.#invalidate();
                }),
            );
            const stopClipboard = reports.observeClipboard(root, this.#onClipboardChange);
            this.#clipboardWatched = stopClipboard !== undefined;
            if (stopClipboard !== undefined) {
                this.#ledger.join(this);
                this.#stops.push(stopClipboard, () => { this.#ledger.leave(this); });
            }
            this.#pass();
            drawn = true;
        } finally {
            if (!drawn) this.destroy();
        }
    }

    /** Takes the Consumer's options again; what did not change costs nothing (ADR-0003). Options a grid
     * cannot be drawn from are refused whole: the grid stays as it was. */
    update(options: GridOptions<Row>): void {
        if (this.#destroyed) throw new Error('This grid was destroyed: a grid destroyed is not drawn again.');
        const taken = this.#take(options);
        this.#prepare(taken, this.#ownSource);
        this.#commit(taken);
        this.#invalidate();
    }

    /**
     * The Consumer asks the grid to place the Selection and the Focus (ADR-0050, item 4): `range` becomes the
     * one range, with the Focus on `focus` inside it, and the grid then does what it does after a click: an open
     * edit commits first, a menu closes, and the Focus is scrolled into view. The Selection stays the grid's; the
     * Consumer asks and the grid places. Positions mean something only under the order they were written in, so
     * a request under another Row Sequence Version than the one the grid's source says (its `snapshot()`) places
     * nothing and is refused (`onRefused`, kind `placement`, reason `orderMoved`), as is one whose edit commit
     * brought in another source (`sourceChanged`, ADR-0142) or moved the columns (`columnsMoved`, ADR-0011). The
     * version is all a request carries, so one made after the source was replaced is taken as written for the
     * new one, as the C# grid takes it. True once placed; false when refused, or when a Reject or a refusal held
     * the open edit (ADR-0034), which says so itself. A range reaching outside the grid, a Focus outside the
     * range, or a grid with no cells throws a RangeError and changes nothing.
     */
    placeSelection(range: SelectionRange, focus: CellPosition, rowSequenceVersion: number): boolean {
        if (this.#destroyed) throw new Error('This grid was destroyed: a grid destroyed places nothing.');
        // Changes a live source gathered are the order's before the check, as a commit's are (ADR-0142 D5).
        this.#source.publishGathered?.();
        const extent = this.#extent;
        if (extent.rowCount === 0 || extent.columnCount === 0) {
            throw new RangeError(`The grid has no cells to place a Selection on (${String(extent.rowCount)} rows × ${String(extent.columnCount)} columns) (ADR-0050).`);
        }
        // Checked first, and before anything changes: a place outside the grid is the Consumer's mistake, whatever
        // else holds, and commits nothing.
        this.#held.selection.place(range, focus, extent);
        // The version the grid hands, which is the source's own and the one a Consumer reads.
        if (rowSequenceVersion !== this.#handedVersion()) {
            this.#onRefused?.({ kind: 'placement', reason: 'orderMoved' });
            return false;
        }
        // The source and the columns the request was made under: the version alone cannot tell a replaced source
        // (ADR-0142), and column positions name other columns once the columns move (ADR-0011).
        const source = this.#source;
        const names = this.#columns.map(({ column }) => column.name);
        this.#answered += 1;
        this.#invalidate();
        if (!this.#commitEdit()) return false;
        // A menu standing at the old place closes, and a drag ends, so its next move does not reshape the place
        // from the old anchor.
        this.#closePopover(true);
        this.#setDragging(false);
        this.#interactive = undefined;
        // The commit's intent may have moved the order or handed over another source: the positions name other
        // rows now (ADR-0011, ADR-0142).
        if (source !== this.#source) {
            this.#onRefused?.({ kind: 'placement', reason: 'sourceChanged' });
            return false;
        }
        if (!sameItems(names, this.#columns.map(({ column }) => column.name))) {
            this.#onRefused?.({ kind: 'placement', reason: 'columnsMoved' });
            return false;
        }
        if (rowSequenceVersion !== this.#handedVersion()) {
            this.#onRefused?.({ kind: 'placement', reason: 'orderMoved' });
            return false;
        }
        this.#reconcileSelection();
        // The commit may have changed how many rows there are under the same order, as a fetching source's total
        // can: a place that no longer fits names rows that are not there, which the order moving says.
        const now = this.#extent;
        if (range.topRow + range.rowCount > now.rowCount || range.leftColumn + range.columnCount > now.columnCount) {
            this.#onRefused?.({ kind: 'placement', reason: 'orderMoved' });
            return false;
        }
        if (this.#apply(this.#held.selection.place(range, focus, now))) {
            this.#revealOwed = { target: focus, extending: undefined, rowStart: false, stepRows: 0 };
            this.#revealHeaderOwed = undefined;
            this.#followArmed = { focus, target: focus };
        }
        return true;
    }

    /** Stops hearing everything and empties the root. */
    destroy(): void {
        if (this.#destroyed) return;
        this.#destroyed = true;
        this.#cancelSettle?.();
        this.#cancelAnnounce?.();
        this.#cancelFollow?.();
        this.#repaint?.cancel();
        this.#stopEdgeTimer();
        this.#copy?.abort();
        this.#fill?.abort();
        this.#findStepping?.abort();
        this.#findStepping = undefined;
        // An edit open as the grid goes ends with it: the Consumer hears the end of every open (ADR-0050
        // section 6). The typing goes with the grid, which is the Consumer's own doing.
        this.#edit = undefined;
        this.#tellEditing();
        this.#popover = undefined;
        for (const stop of this.#stops) stop();
        this.#unsubscribe?.();
        this.#rowMarks.destroy();
        this.#summary.destroy();
        // Every Template cell's content is taken away with the grid (ADR-0020).
        for (const byColumn of this.#templates.values()) {
            for (const mounted of byColumn.values()) this.#unmount(mounted);
        }
        this.#templates.clear();
        this.#root.replaceChildren();
        for (const name of ['tabindex', 'dir', 'id', 'role', 'aria-multiselectable', 'data-ex-reveal', 'data-ex-follow',
            'data-ex-follow-focus', 'aria-activedescendant', 'data-ex-settled', 'class', 'style', 'aria-rowcount', 'aria-colcount',
            'data-ex-focus-visible']) {
            this.#root.removeAttribute(name);
        }
    }

    // The options checked and resolved, with nothing of the grid's changed. `rows` handed again as the same
    // array is the same source; a new array is a new source, made here and told nothing yet.
    #take(options: GridOptions<Row>): Taken<Row> {
        if ((options.source === undefined) === (options.rows === undefined)) {
            throw new TypeError('A grid takes its rows from exactly one of `source` and `rows` (ADR-0350, section 16).');
        }
        let source: GridSource<Row>;
        let own: InMemoryGridSource<Row> | undefined;
        if (options.source === undefined) {
            const rows = defined(options.rows, 'the rows');
            own = this.#ownSource !== undefined && this.#ownRows === rows ? this.#ownSource : InMemoryGridSource.from(rows);
            source = own;
        } else {
            source = options.source;
        }

        const { density = 'compact', pinnedColumnCount = 0, virtualiseColumns = true } = options;
        const resolved = GridMetrics.resolve({
            density,
            ...(options.rowHeight === undefined ? {} : { rowHeightPx: options.rowHeight }),
            ...(options.headerHeight === undefined ? {} : { headerHeightPx: options.headerHeight }),
        });
        const metricsChanged = !resolved.equals(this.#metrics);
        const metrics = metricsChanged ? resolved : this.#metrics;
        let columns = this.#columns;
        let autoWidths = this.#autoWidths;
        // A new array holding the same column objects is the same columns: a Framework Adapter hands a new
        // array on every render, and nothing about them changed (ADR-0003).
        // A ▾ that comes or goes changes what a header needs, so every Auto width is measured afresh (ADR-0016).
        const menusChanged = (options.hideColumnMenu === true) !== this.#hideColumnMenu;
        const columnsChanged = metricsChanged || menusChanged || !sameItems(options.columns, this.#declared);
        if (columnsChanged) {
            const previous = this.#columns;
            const previousWidths = this.#autoWidths;
            columns = resolveColumns(options.columns);
            // An Auto width is carried by the column's name for the same column object under the same
            // metrics, as the C# core carries it, and measured afresh otherwise: two equal declarations may
            // paint differently (ADR-0016; ADR-0003's note of 2026-10-11).
            const at = new Map(previous.map(({ column }, i) => [column.name, i]));
            autoWidths = columns.map(({ column, spec, header, actions }) => {
                const was = at.get(column.name);
                if (!metricsChanged && !menusChanged && was !== undefined && itemAt(previous, was).column === column) {
                    return itemAt(previousWidths, was);
                }
                // The header is content the column paints too: unobserved, a short Auto column would
                // stand with its label chopped (ADR-0016).
                if (!spec.width.isAuto) return AutoWidth.of(spec);
                const auto = AutoWidth.of(spec).observe(metrics.headerRequiredPx({ label: header, menuButton: options.hideColumnMenu !== true, sortable: column.queryable !== false }));
                // An Action Column paints no value, so the rows never grow it and its header is usually empty:
                // what it declares is its labels, measured once, side by side, each with its button's box, and
                // the cell's own padding once (ADR-0016, ADR-0020). A label is text (ticket 83).
                if (actions.length === 0) return auto;
                const asText = metrics.cellMetrics.for('text');
                return auto.observe(actions.reduce((required, action) => required + asText.textWidthPx(action.label) + metrics.actionButtonChromePx,
                    asText.estimatePx('')));
            });
        }
        const width = sizeOf(options.viewportWidth, defaultViewportWidthPx, 'viewportWidth');
        const height = sizeOf(options.viewportHeight, defaultViewportHeightPx, 'viewportHeight');
        if (!Number.isSafeInteger(pinnedColumnCount) || pinnedColumnCount < 0) {
            throw new RangeError(`pinnedColumnCount is a whole number of at least 0, not ${pinnedColumnCount}.`);
        }
        const {
            onSelectionChanged, onRefused, onAction, onPaste, pasteMaySpill, onClear, onUndo, onRedo, onEdit, onEditingChanged, onEditDiscarded,
            onOverwriteNotice, onCellDoubleClick, onLeave, onColumnWidthChanged, onColumnOrderChanged, contextCommands, commandLabel, hideCopyWithHeaders,
            hideColumnMenu, onColumnHidden, onPinnedCountChanged,
        }: {
            readonly onSelectionChanged?: unknown; readonly onRefused?: unknown; readonly onAction?: unknown; readonly onPaste?: unknown;
            readonly pasteMaySpill?: unknown;
            readonly onClear?: unknown; readonly onUndo?: unknown; readonly onRedo?: unknown;
            readonly onEdit?: unknown; readonly onEditingChanged?: unknown; readonly onEditDiscarded?: unknown;
            readonly onOverwriteNotice?: unknown; readonly onCellDoubleClick?: unknown; readonly onLeave?: unknown;
            readonly onColumnWidthChanged?: unknown; readonly onColumnOrderChanged?: unknown;
            readonly contextCommands?: unknown; readonly commandLabel?: unknown; readonly hideCopyWithHeaders?: unknown;
            readonly hideColumnMenu?: unknown; readonly onColumnHidden?: unknown; readonly onPinnedCountChanged?: unknown;
        } = options;
        if (onSelectionChanged !== undefined && typeof onSelectionChanged !== 'function') {
            throw new TypeError('onSelectionChanged is a function of the Selection.');
        }
        if (onRefused !== undefined && typeof onRefused !== 'function') {
            throw new TypeError('onRefused is a function of the refusal.');
        }
        if (onAction !== undefined && typeof onAction !== 'function') {
            throw new TypeError('onAction is a function of the action pressed.');
        }
        if (onPaste !== undefined && typeof onPaste !== 'function') {
            throw new TypeError('onPaste is a function of the paste\'s intent.');
        }
        if (pasteMaySpill !== undefined && typeof pasteMaySpill !== 'boolean') {
            throw new TypeError('pasteMaySpill is true or false.');
        }
        const { highlightFocusRow, highlightHoverRow }: { readonly highlightFocusRow?: unknown; readonly highlightHoverRow?: unknown } = options;
        if (highlightFocusRow !== undefined && typeof highlightFocusRow !== 'boolean') {
            throw new TypeError('highlightFocusRow is true or false.');
        }
        if (highlightHoverRow !== undefined && typeof highlightHoverRow !== 'boolean') {
            throw new TypeError('highlightHoverRow is true or false.');
        }
        if (onClear !== undefined && typeof onClear !== 'function') {
            throw new TypeError('onClear is a function of the clear\'s intent.');
        }
        if (onUndo !== undefined && typeof onUndo !== 'function') {
            throw new TypeError('onUndo is a function of nothing: the history is the Consumer\'s.');
        }
        if (onRedo !== undefined && typeof onRedo !== 'function') {
            throw new TypeError('onRedo is a function of nothing: the history is the Consumer\'s.');
        }
        if (onEdit !== undefined && typeof onEdit !== 'function') {
            throw new TypeError('onEdit is a function of the edit\'s intent.');
        }
        if (onEditingChanged !== undefined && typeof onEditingChanged !== 'function') {
            throw new TypeError('onEditingChanged is a function of whether an edit is open.');
        }
        if (onEditDiscarded !== undefined && typeof onEditDiscarded !== 'function') {
            throw new TypeError('onEditDiscarded is a function of the reason.');
        }
        if (onOverwriteNotice !== undefined && typeof onOverwriteNotice !== 'function') {
            throw new TypeError('onOverwriteNotice is a function of the notice.');
        }
        if (onCellDoubleClick !== undefined && typeof onCellDoubleClick !== 'function') {
            throw new TypeError('onCellDoubleClick is a function of the cell.');
        }
        if (onLeave !== undefined && typeof onLeave !== 'function') {
            throw new TypeError('onLeave is a function, raised by the Escape that leaves the grid.');
        }
        if (onColumnWidthChanged !== undefined && typeof onColumnWidthChanged !== 'function') {
            throw new TypeError('onColumnWidthChanged is a function of the width change.');
        }
        if (onColumnOrderChanged !== undefined && typeof onColumnOrderChanged !== 'function') {
            throw new TypeError('onColumnOrderChanged is a function of the columns\' new order.');
        }
        if (contextCommands !== undefined && typeof contextCommands !== 'function') {
            throw new TypeError('contextCommands is a function of the Context Menu\'s context, answering the commands to add.');
        }
        if (commandLabel !== undefined && typeof commandLabel !== 'function') {
            throw new TypeError('commandLabel is a function of a command\'s id, answering its words or undefined.');
        }
        if (hideCopyWithHeaders !== undefined && typeof hideCopyWithHeaders !== 'boolean') {
            throw new TypeError('hideCopyWithHeaders is true or false.');
        }
        if (hideColumnMenu !== undefined && typeof hideColumnMenu !== 'boolean') {
            throw new TypeError('hideColumnMenu is true or false.');
        }
        if (onColumnHidden !== undefined && typeof onColumnHidden !== 'function') {
            throw new TypeError('onColumnHidden is a function of the column\'s name.');
        }
        if (onPinnedCountChanged !== undefined && typeof onPinnedCountChanged !== 'function') {
            throw new TypeError('onPinnedCountChanged is a function of the Pinned Columns\' count.');
        }
        const { locale = invariantLocale, temporal }: { readonly locale?: unknown; readonly temporal?: unknown } = options;
        if (typeof locale !== 'string') {
            throw new TypeError(`locale is a BCP 47 tag or '${invariantLocale}', not ${String(locale)} (ADR-0370 section 10).`);
        }
        const culture = gridCulture(locale);
        if (temporal !== undefined && !isTemporal(temporal)) {
            throw new TypeError('temporal is a Temporal namespace, with PlainDate, PlainDateTime and ZonedDateTime (ADR-0350 section 16).');
        }
        // A date typed into a filter is made with Temporal: a grid that offers one without it is refused as it is
        // given, not on the click that opens the filter.
        const dated = hideColumnMenu === true || temporal !== undefined
            ? undefined
            : columns.find(({ column }) => column.type === 'date' && column.queryable !== false);
        if (dated !== undefined) {
            throw new TypeError(`Column '${dated.column.name}' holds dates, and a date typed into its filter is made with Temporal, `
                + 'which the grid was not given: pass `temporal`, the browser\'s Temporal or a polyfill\'s (ADR-0350 section 16).');
        }
        const marks = this.#marksOf(options, columns, source);
        const { headerClickSelects }: { readonly headerClickSelects?: unknown } = options;
        const { dataEdge }: { readonly dataEdge?: unknown } = options;
        if (dataEdge !== undefined && typeof dataEdge !== 'function') throw new TypeError('dataEdge is a function (ADR-0050, item 2).');
        if (headerClickSelects !== undefined && typeof headerClickSelects !== 'boolean') {
            throw new TypeError('headerClickSelects is true or false.');
        }
        // A press on a header either grabs the column to move it or selects it: both declared, neither meaning
        // could be honoured without breaking the other's promise (ADR-0050, item 1; ADR-0011).
        if (headerClickSelects === true && onColumnOrderChanged !== undefined) {
            throw new TypeError('headerClickSelects and onColumnOrderChanged both claim a press on a header: declare one of them.');
        }
        const summary = this.#summaryOptionsOf(options);
        const { copyCellCap = defaultCellCap, pasteByteCap = defaultPasteByteCap } = options;
        const { copyAnswer }: { readonly copyAnswer?: unknown } = options;
        if (copyAnswer !== undefined && typeof copyAnswer !== 'function') throw new TypeError('copyAnswer is a function of the copy asked (ADR-0050, item 9).');
        if (!Number.isSafeInteger(copyCellCap) || copyCellCap < 1) {
            throw new RangeError(`copyCellCap is a whole number of at least one cell, not ${copyCellCap}: the cap may be raised, `
                + 'not used to switch copying off (ADR-0005).');
        }
        if (!Number.isSafeInteger(pasteByteCap) || pasteByteCap < 1) {
            throw new RangeError(`pasteByteCap is a whole number of at least one byte, not ${pasteByteCap}: the cap may be raised, `
                + 'not used to switch pasting off (ADR-0005).');
        }
        const { headerGroups, headerLayout } = this.#layoutOf(options.headerGroups, columns, pinnedColumnCount, columnsChanged);
        // The band comes off the scroll budget, and the refusal names the band (ADR-0032): a Viewport that
        // cannot hold it and one row shows headers and no data, which reads as an empty result.
        const bandPx = bandHeightPxOf(headerLayout, metrics);
        const formulaBar = formulaBarDeclarations(options);
        // The Formula Bar takes its band of the declared height as well, and the rows what both leave (ADR-0051).
        const barPx = formulaBar.shown ? metrics.formulaBarHeightPx : 0;
        if (formulaBar.shown && height !== 'stretch' && height <= barPx + bandPx + metrics.rowHeightPx) {
            throw new RangeError(`viewportHeight (${height}px) must exceed the Formula Bar's ${barPx}px, the header band's `
                + `${bandPx}px and one row of ${metrics.rowHeightPx}px: each takes its band of it (ADR-0051, ADR-0028).`);
        }
        if (headerLayout.tierCount > 0 && height !== 'stretch' && height <= bandPx + metrics.rowHeightPx) {
            throw new RangeError(`viewportHeight (${height}px) must exceed the header band — ${headerRowsOf(headerLayout)} tiers × `
                + `${metrics.headerHeightPx}px = ${bandPx}px — plus one row of ${metrics.rowHeightPx}px (ADR-0032).`);
        }
        return {
            source, own, rows: options.rows, metrics, declared: options.columns, columns, autoWidths, columnsChanged,
            width, height, pinnedCount: pinnedColumnCount, virtualiseColumns, headerGroups, headerLayout,
            onSelectionChanged: options.onSelectionChanged, copyCellCap, copyAnswer: options.copyAnswer, onRefused: options.onRefused, onAction: options.onAction,
            marks,
            onPaste: options.onPaste, pasteMaySpill: options.pasteMaySpill === true, pasteByteCap, onClear: options.onClear,
            highlightFocusRow: options.highlightFocusRow === true, highlightHoverRow: options.highlightHoverRow === true,
            onUndo: options.onUndo, onRedo: options.onRedo,
            onEdit: options.onEdit, onEditingChanged: options.onEditingChanged, onEditDiscarded: options.onEditDiscarded,
            onOverwriteNotice: options.onOverwriteNotice, onCellDoubleClick: options.onCellDoubleClick, onLeave: options.onLeave,
            onColumnWidthChanged: options.onColumnWidthChanged, onColumnOrderChanged: options.onColumnOrderChanged,
            contextCommands: options.contextCommands, commandLabel: options.commandLabel, hideCopyWithHeaders: options.hideCopyWithHeaders === true,
            hideColumnMenu: options.hideColumnMenu === true, onColumnHidden: options.onColumnHidden,
            onPinnedCountChanged: options.onPinnedCountChanged, culture, temporal,
            onReplace: options.onReplace, findMatch: options.findMatch, findsHeaders: options.findsHeaders !== false,
            cellPaint: CellPaint.take(options, this.#cellPaint), headerClickSelects: headerClickSelects === true, formulaBar,
            dataEdge: options.dataEdge, ...summary,
        };
    }

    // The Selection Summary's options checked (ADR-0130): `onSummarize` beside a source that summarises is refused,
    // since one of the two would have to silently win, and the figures are taken in Excel's order.
    #summaryOptionsOf(options: GridOptions<Row>): Pick<Taken<Row>,
        'onSummarize' | 'showSelectionSummary' | 'summaryFigures' | 'onSelectionSummaryChanged' | 'onSummaryFiguresChanged'> {
        const { onSummarize, showSelectionSummary, summaryFigures, onSelectionSummaryChanged, onSummaryFiguresChanged }: {
            readonly onSummarize?: unknown; readonly showSelectionSummary?: unknown; readonly summaryFigures?: unknown;
            readonly onSelectionSummaryChanged?: unknown; readonly onSummaryFiguresChanged?: unknown;
        } = options;
        if (onSummarize !== undefined && typeof onSummarize !== 'function') {
            throw new TypeError('onSummarize is a function of the Selection Summary\'s request and an abort signal (ADR-0130).');
        }
        if (onSummarize !== undefined && options.source?.summarize !== undefined) {
            throw new TypeError('onSummarize was given together with a source that summarises. One of the two would have to silently win; '
                + 'give the source alone, or a source that does not summarise (ADR-0130).');
        }
        if (showSelectionSummary !== undefined && typeof showSelectionSummary !== 'boolean') {
            throw new TypeError('showSelectionSummary is true or false.');
        }
        if (onSelectionSummaryChanged !== undefined && typeof onSelectionSummaryChanged !== 'function') {
            throw new TypeError('onSelectionSummaryChanged is a function of the Selection Summary.');
        }
        if (onSummaryFiguresChanged !== undefined && typeof onSummaryFiguresChanged !== 'function') {
            throw new TypeError('onSummaryFiguresChanged is a function of the figures chosen (ADR-0130).');
        }
        let figures = defaultSummaryFigures;
        if (summaryFigures !== undefined) {
            if (!Array.isArray(summaryFigures)) throw new TypeError('summaryFigures is a list of figures (ADR-0130).');
            const given: readonly unknown[] = summaryFigures;
            const unknown = given.find(figure => typeof figure !== 'string' || !isSummaryFigure(figure));
            if (unknown !== undefined) {
                throw new TypeError(`summaryFigures holds ${JSON.stringify(unknown)}, which is none of the figures: ${summaryFigureOrder.join(', ')} (ADR-0130).`);
            }
            const ordered = summaryFigureOrder.filter(figure => given.includes(figure));
            // The same figures as before are the same array, so a Framework Adapter's new one each render asks nothing.
            figures = sameItems(ordered, this.#summaryFigures) ? this.#summaryFigures : Object.freeze(ordered);
        }
        const answer = options.onSummarize;
        return {
            onSummarize: answer === undefined ? undefined : async (request, signal) => answer(request, signal),
            showSelectionSummary: showSelectionSummary !== false,
            summaryFigures: figures,
            onSelectionSummaryChanged: options.onSelectionSummaryChanged,
            onSummaryFiguresChanged: options.onSummaryFiguresChanged,
        };
    }

    // Who holds the marks of the Mark Column, refusing by name what cannot be painted honestly (ADR-0043): marks
    // given beside a source that keeps its own, and a Mark Column nobody holds marks for.
    #marksOf(options: GridOptions<Row>, columns: readonly ResolvedColumn<Row>[], source: GridSource<Row>): RowMarks<Row> | undefined {
        const { marks: given }: { readonly marks?: unknown } = options;
        if (given !== undefined && !isRowMarks(given)) {
            throw new TypeError('marks holds the Row Marks: isMarked, counts, onMarkIntent, setRowKind and subscribe (ADR-0043).');
        }
        if (given !== undefined && options.source?.marks !== undefined) {
            throw new TypeError('marks was given together with a source that keeps its own Row Marks. One of the two would have to '
                + 'silently win; give the source alone, or a source without marks (ADR-0043).');
        }
        const column = columns.find(({ mark }) => mark);
        if (column === undefined) return undefined;
        // The source the grid makes over `rows` is made again for every new array, and marks it kept would go
        // with it without a word: the marks then belong to someone who outlives the array.
        if (options.source === undefined && options.marks === undefined) {
            throw new TypeError(`Column '${column.column.name}' is a Mark Column over rows the grid holds, which it holds again for every `
                + 'new array, and the marks would go with them. Give marks, or a source that keeps them, as GridSource.from does (ADR-0043).');
        }
        const marks = options.marks ?? source.marks;
        if (marks === undefined) {
            throw new TypeError(`Column '${column.column.name}' is a Mark Column, but nobody holds the marks. Give marks, or a source `
                + 'that keeps them: GridSource.from does, and GridSource.fetch does given a marks adapter (ADR-0043).');
        }
        return marks;
    }

    // The Header Groups taken and resolved against the columns' order and the pinned count (ADR-0032): the
    // layout in force kept while none of the three changed, so the header is not built again.
    #layoutOf(groups: readonly GridHeaderGroup[] | undefined, columns: readonly ResolvedColumn<Row>[], pinnedCount: number,
        columnsChanged: boolean): { readonly headerGroups: readonly HeaderGroup[]; readonly headerLayout: HeaderGroupLayout } {
        if (groups === undefined) return { headerGroups: [], headerLayout: HeaderGroupLayout.empty };
        const given: unknown = groups;
        if (!Array.isArray(given)) throw new TypeError(`headerGroups is a list of Header Groups, not ${String(given)} (ADR-0032).`);
        const headerGroups = groups.map((group, i) => {
            const item: unknown = group;
            if (typeof item !== 'object' || item === null) {
                throw new TypeError(`headerGroups[${i}] is a Header Group, not ${String(item)} (ADR-0032).`);
            }
            return new HeaderGroup(group);
        });
        if (headerGroups.length === 0) return { headerGroups, headerLayout: HeaderGroupLayout.empty };
        if (!columnsChanged && pinnedCount === this.#pinnedCount && sameGroups(headerGroups, this.#headerGroups)) {
            return { headerGroups: this.#headerGroups, headerLayout: this.#headerLayout };
        }
        const headerLayout = HeaderGroupLayout.resolve({
            groups: headerGroups,
            columnNames: columns.map(({ column }) => column.name),
            pinnedCount,
        });
        return { headerGroups, headerLayout };
    }

    // What the taken options ask of the sources, the last step that may refuse them: the source told its
    // columns, and a source the grid made over new rows given the Sort and the Filter in force over the
    // last ones. A source the grid made and then refused is dropped, never held.
    #prepare(taken: Taken<Row>, previousOwn: InMemoryGridSource<Row> | undefined): void {
        const { source, own } = taken;
        const newSource = source !== this.#sourceIfAny();
        if (newSource || taken.columnsChanged) source.setColumns(taken.columns.map(({ column }) => column));
        if (own !== undefined && own !== previousOwn && previousOwn !== undefined) {
            const { sorts, filter } = previousOwn.snapshot();
            own.setSorts(sorts);
            own.setFilter(filter);
        }
    }

    // The source in force, or none while the constructor has not taken one.
    #sourceIfAny(): GridSource<Row> | undefined {
        return this.#unsubscribe === undefined ? undefined : this.#source;
    }

    // What was taken becomes the grid's. Nothing here refuses.
    #commit(taken: Taken<Row>): void {
        const { source } = taken;
        if (source !== this.#sourceIfAny()) {
            // The order the grid judges its gestures under moves on whenever its source is another one: every
            // position may now name another row (ADR-0011), whatever version the new source counts from.
            const shown = this.#unsubscribe === undefined ? undefined : this.#sequence();
            this.#unsubscribe?.();
            this.#source = source;
            this.#unsubscribe = source.subscribe(this.#onSource);
            const { window, windowStart } = this.#state;
            this.#state = source.snapshot();
            this.#sequenceOffset = shown === undefined ? 0 : shown + 1 - this.#state.rowSequenceVersion;
            this.#lastRaised = undefined;
            this.#userWritesTakenIn(window, windowStart);
        }
        this.#ownSource = taken.own;
        this.#ownRows = taken.own === undefined ? undefined : taken.rows;
        if (taken.columnsChanged) {
            this.#columns = taken.columns;
            this.#autoWidths = taken.autoWidths;
            this.#measuredWindow = undefined;
        }
        this.#declared = taken.declared;
        this.#metrics = taken.metrics;
        this.#width = taken.width;
        this.#height = taken.height;
        this.#pinnedCount = taken.pinnedCount;
        this.#virtualiseColumns = taken.virtualiseColumns;
        this.#headerGroups = taken.headerGroups;
        // Every row's place in the accessibility tree counts the header's rows, so a change in the tiers
        // paints every row again (ADR-0033).
        if (taken.headerLayout.tierCount !== this.#headerLayout.tierCount) {
            this.#painted = new Map();
            this.#placeholders = new Map();
        }
        this.#headerLayout = taken.headerLayout;
        this.#onSelectionChanged = taken.onSelectionChanged;
        this.#copyCellCap = taken.copyCellCap;
        this.#copyAnswer = taken.copyAnswer;
        this.#onRefused = taken.onRefused;
        this.#onAction = taken.onAction;
        this.#rowMarks.bind(taken.marks);
        this.#pasteHeard = taken.onPaste;
        this.#pasteMaySpill = taken.pasteMaySpill;
        this.#highlightFocusRow = taken.highlightFocusRow;
        if (taken.highlightHoverRow !== this.#highlightHoverRow) {
            this.#highlightHoverRow = taken.highlightHoverRow;
            // Heard only while the band is on: off, a pointer crossing the grid costs nothing (ADR-0021's fifth
            // entry, which the Script Core keeps as its own rule).
            if (taken.highlightHoverRow) {
                this.#root.addEventListener('mousemove', this.#onHover);
                this.#root.addEventListener('mouseleave', this.#dropHover);
            } else {
                this.#root.removeEventListener('mousemove', this.#onHover);
                this.#root.removeEventListener('mouseleave', this.#dropHover);
                this.#hoverRow = undefined;
            }
        }
        this.#pasteByteCap = taken.pasteByteCap;
        this.#clearHeard = taken.onClear;
        this.#onUndo = taken.onUndo;
        this.#onRedo = taken.onRedo;
        this.#editHeard = taken.onEdit;
        this.#onEditingChanged = taken.onEditingChanged;
        this.#onEditDiscarded = taken.onEditDiscarded;
        this.#onOverwriteNotice = taken.onOverwriteNotice;
        this.#onCellDoubleClick = taken.onCellDoubleClick;
        this.#leaveHeard = taken.onLeave;
        this.#onColumnWidthChanged = taken.onColumnWidthChanged;
        this.#onColumnOrderChanged = taken.onColumnOrderChanged;
        this.#contextCommands = taken.contextCommands;
        this.#commandLabel = taken.commandLabel;
        this.#hideCopyWithHeaders = taken.hideCopyWithHeaders;
        this.#hideColumnMenu = taken.hideColumnMenu;
        this.#onColumnHidden = taken.onColumnHidden;
        this.#onPinnedCountChanged = taken.onPinnedCountChanged;
        this.#culture = taken.culture;
        this.#temporal = taken.temporal;
        this.#onReplace = taken.onReplace;
        this.#findMatch = taken.findMatch;
        this.#findsHeaders = taken.findsHeaders;
        this.#cellPaint = taken.cellPaint;
        this.#headerClickSelects = taken.headerClickSelects;
        this.#dataEdge = taken.dataEdge;
        this.#onSummarize = taken.onSummarize;
        this.#showSelectionSummary = taken.showSelectionSummary;
        if (taken.summaryFigures !== this.#summaryFigures) this.#summaryChoice = undefined;
        this.#summaryFigures = taken.summaryFigures;
        this.#onSelectionSummaryChanged = taken.onSelectionSummaryChanged;
        this.#onSummaryFiguresChanged = taken.onSummaryFiguresChanged;
        this.#formulaBarDeclared = taken.formulaBar;
        // A panel over a source that no longer finds closes, as a Cancel: nothing could answer its steps
        // (ADR-0055). One offering Replace where none can land any more offers Find alone (ADR-0332 §2).
        const popover = this.#popover;
        if (popover?.kind === 'find' && !this.#canFind) {
            this.#closePopover(true);
        } else if (popover?.kind === 'find' && popover.panel.replaceShown && !this.#canReplace) {
            const held = popover.element.contains(this.#root.ownerDocument.activeElement);
            popover.panel.showReplace(false);
            if (held) popover.panel.focusField();
        }
    }

    // The order the grid's own gestures are judged under: the source's Row Sequence Version, carried past every
    // change of source, so a gesture taken under one source is never taken as under the next. Never handed to a
    // Consumer, who is handed the source's own (`#state.rowSequenceVersion`), the one its `snapshot()` says and
    // `placeSelection` and `showViewWith` take (ADR-0050, ADR-0142, ADR-0331), as the C# grid hands it. Rows handed
    // as a new array are a source of their own, which counts from 0 as any new source does.
    #sequence(): number {
        return this.#state.rowSequenceVersion + this.#sequenceOffset;
    }

    // The Row Sequence Version every value handed to a Consumer carries: an intent, the Context Menu's context, a
    // view read, the copy request and the viewport's `data-ex-sequence` (ADR-0011, ADR-0050). Never `#sequence()`.
    #handedVersion(): number {
        return this.#state.rowSequenceVersion;
    }

    readonly #onSource = (): void => {
        const previous = this.#state;
        const { window, windowStart } = previous;
        this.#state = this.#source.snapshot();
        this.#noteRowsForSummary(previous);
        this.#userWritesTakenIn(window, windowStart);
        this.#invalidate();
    };

    // Notes whether the rows moved under the figures (ADR-0130). Walked only while a question stands, of this source
    // and under this order; with none, nothing on screen can be wrong, and the stamp moves so that the next question
    // asks afresh. A question asked under another order or of another source goes with the Selection (ADR-0011).
    #noteRowsForSummary(previous: GridSourceState<Row>): void {
        const question = this.#summary.standing;
        if (question === undefined || question.binding !== this.#source || question.rowSequenceVersion !== this.#state.rowSequenceVersion
            || rowsMovedUnder(question.ranges, previous, this.#state)) {
            this.#summaryRowsStamp += 1;
        }
    }

    #onScroll(): void {
        // A reveal's repaint is the browser's business: meanwhile the grid stands where the reveal left the
        // scroller, and a scroll the user makes is read when the repaint yields to it (ADR-0012).
        if (this.#repaint !== undefined) return;
        // The offsets the browser holds, read on the scroll it reports: the one read this grid makes.
        const top = this.#scroller.scrollTop;
        const left = this.#scroller.scrollLeft;
        if (top === this.#scrollTopPx && left === this.#scrollLeftPx) return;
        // The user scrolled away from a move whose cell is not painted yet: the page no longer follows it
        // (ADR-0204).
        this.#followArmed = undefined;
        // The rows moved under a still pointer: the band would stand on a row the pointer is no longer over. It
        // goes, and the next movement brings it back. Panning sideways moves no row under it.
        if (top !== this.#scrollTopPx) this.#dropHover();
        const painted = this.#visible;
        const previousLeftPx = this.#scrollLeftPx;
        this.#scrollTopPx = top;
        this.#scrollLeftPx = left;
        const landed = this.#geometry?.sliceAt(top);
        // Either axis on its own makes a fling, and the horizontal one only while the columns are
        // virtualised: off, panning changes no cell (ADR-0004).
        const flung = (painted !== undefined && landed !== undefined && landed.start !== painted.start
            && defined(this.#geometry, 'the geometry a slice came from').isFling(painted.start, landed.start))
            || (this.#virtualiseColumns && this.#styles.geometry.isFling(previousLeftPx, left));
        if (flung) this.#placeholderMode = true;
        if (this.#placeholderMode) {
            // Any scrolling keeps it unsettled: the delay measures stillness.
            this.#cancelSettle?.();
            this.#cancelSettle = this.#reports.clock.setTimer(this.#onSettle, settleDelayMs);
        }
        this.#invalidate();
    }

    readonly #onSettle = (): void => {
        this.#cancelSettle = undefined;
        this.#placeholderMode = false;
        this.#invalidate();
    };

    #invalidate(): void {
        if (this.#destroyed) return;
        this.#showSettled(false);
        if (this.#passOwed) return;
        this.#passOwed = true;
        queueMicrotask(() => {
            this.#passOwed = false;
            if (!this.#destroyed) this.#pass();
        });
    }

    #pass(): void {
        this.#reconcileSelection();
        this.#resolve();
        const revealed = this.#stageReveal();
        this.#stageFollow();
        this.#paint();
        const stepped = this.#scrollOwed;
        this.#scrollOwed = false;
        if (revealed || stepped) {
            // Written in the task that painted the slice, so the offset and the rows reach the screen
            // together (ADR-0012, 2026-09-29); its scroll event then finds nothing moved. An edge band's
            // step is written so too (ADR-0008).
            this.#scroller.scrollTop = this.#scrollTopPx;
            this.#scroller.scrollLeft = this.#scrollLeftPx;
        }
        if (revealed) this.#repaintAfterReveal(this.#scrollTopPx, this.#scrollLeftPx);
        this.#focusEditor();
        this.#raiseRangeIfUncovered();
        this.#raiseSelection();
        void this.#summary.settle(() => this.#summaryAnswerer, this.#onSelectionSummaryChanged);
        this.#lookSettled();
    }

    // Settled while nothing is owed: no pass, no fling's settle, no announcement, no reveal's repaint and
    // no page follow, and the browser has told the box and the ceiling (ADR-0220).
    #lookSettled(): void {
        this.#showSettled(!this.#passOwed && this.#cancelSettle === undefined && this.#cancelAnnounce === undefined
            && this.#repaint === undefined && this.#followArmed === undefined && this.#cancelFollow === undefined
            && this.#clipboardWrites === 0 && this.#viewportReport !== undefined && this.#ceilingPx !== undefined
            && !(this.#popover?.kind === 'column' && this.#popover.panel?.fetching === true)
            && this.#findStepping === undefined && this.#replacing === 0);
    }

    #showSettled(settled: boolean): void {
        if (settled === this.#settledShown) return;
        this.#settledShown = settled;
        this.#root.toggleAttribute('data-ex-settled', settled);
    }

    // ---- Resolving (the C# core's PrepareRender) ------------------------------------------------------

    // The header band: the header row and a tier per level of Header Groups above it (ADR-0032).
    get #bandHeightPx(): number {
        return bandHeightPxOf(this.#headerLayout, this.#metrics);
    }

    // The Formula Bar's band, or nothing while it is off (ADR-0051).
    get #barPx(): number {
        return this.#formulaBarDeclared.shown ? this.#metrics.formulaBarHeightPx : 0;
    }

    // A popover's place below the Formula Bar: its `top` is the root's padding box's, which the bar's band starts
    // (ADR-0051, ADR-0040).
    #underBar(style: string): string {
        const barPx = this.#barPx;
        return barPx === 0 ? style : `${style}; margin-top: ${barPx}px`;
    }

    // The rows the header stands for in the accessibility tree, ahead of the data rows (ADR-0033).
    get #headerRowCount(): number {
        return headerRowsOf(this.#headerLayout);
    }

    #resolve(): void {
        const report = this.#viewportReport;
        const visibleWidthPx = this.#width === 'stretch'
            ? Math.max(0, report?.contentWidthPx ?? 0) // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0028: a stretched axis is 0 until the browser reports it, and paints nothing
            : ViewportBox.visibleWidthOf(this.#width, report?.gutterWidthPx ?? 0); // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0013: the gutter is 0 until the browser reports otherwise
        this.#visibleWidthPx = visibleWidthPx;
        const visibleHeightPx = this.#height === 'stretch'
            ? Math.max(0, report?.contentHeightPx ?? 0) // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0028: as the width
            : ViewportBox.visibleHeightOf(this.#height - this.#barPx, report?.gutterHeightPx ?? 0); // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0013: as the width
        // A box that shrinks until the popover has less than a row of room would leave it showing no item,
        // and still holding the keyboard: it closes as a Cancel. Only a shrink does: one opened in a box that
        // small is the Consumer's box, not a change (ADR-0040).
        const popover = this.#popover;
        if (popover !== undefined && visibleHeightPx < this.#visibleHeightPx
            && this.#popoverRoomPx(popover, visibleHeightPx) < this.#metrics.rowHeightPx) {
            this.#closePopover(true);
        }
        this.#visibleHeightPx = visibleHeightPx;
        const band = this.#bandHeightPx;
        const total = this.#totalRows;
        if (visibleHeightPx <= band || visibleWidthPx <= 0) {
            // A declared height this small is the Consumer's defect, said by name; a reported zero is a
            // hidden tab, and paints nothing until the next report (ADR-0013, ADR-0028).
            if (visibleHeightPx <= band && this.#height !== 'stretch') {
                throw new Error(`A horizontal scrollbar takes ${report?.gutterHeightPx ?? 0}px out of the ${this.#height - this.#barPx}px Viewport${this.#barPx === 0 ? '' : ` left under the ${this.#barPx}px Formula Bar`}, ` // oxlint-disable-line ex-grid/no-stand-in-default -- the message names the gutter, 0 until reported
                    + `leaving ${visibleHeightPx}px — not enough for the header band's own ${band}px, let alone a row. `
                    + 'Declare a Viewport tall enough to hold its own scrollbar (ADR-0013).');
            }
            this.#visible = undefined;
            this.#scrollable = undefined;
            this.#geometry = undefined;
            this.#resolveColumnGeometry(visibleWidthPx);
            return;
        }
        // The rows share the scroll content with the header band, so they get what it leaves, under the
        // ceiling less the band (ADR-0013, ADR-0053).
        this.#geometry = new ViewportGeometry({
            rowHeightPx: this.#metrics.rowHeightPx,
            viewportHeightPx: visibleHeightPx - band,
            totalRowCount: total,
            scrollRoomPx: Math.max(1, (this.#ceilingPx ?? ViewportGeometry.maxScrollHeightPx) - band),
        });
        // Offsets from a view of other rows may lie past these rows' end. They are kept inside here, as the browser
        // keeps the ones written, from the arithmetic and never read back (ADR-0331, ADR-0021).
        const clampView = this.#viewScrollUnclamped;
        this.#viewScrollUnclamped = false;
        if (clampView) this.#scrollTopPx = Math.min(Math.max(this.#scrollTopPx, 0), this.#geometry.maxScrollTopPx);
        this.#visible = this.#geometry.sliceAt(this.#scrollTopPx);
        this.#observeAutoWidths();
        this.#resolveColumnGeometry(visibleWidthPx);
        if (clampView) this.#scrollLeftPx = Math.min(Math.max(this.#scrollLeftPx, 0), this.#styles.geometry.maxScrollLeftPx);
        this.#scrollable = this.#styles.geometry.scrollableSliceAt(this.#scrollLeftPx, this.#virtualiseColumns);
    }

    get #totalRows(): number {
        return this.#state.totalCount ?? this.#state.windowStart + this.#state.window.length;
    }

    // Auto widths grow from what has been painted, every column observed so the horizontal axis stays
    // still, and only the rows newly on screen read (ADR-0016). Nothing is painted mid-fling, so nothing is
    // measured then.
    #observeAutoWidths(): void {
        const { window, windowStart } = this.#state;
        const sameWindow = this.#measuredWindow === window && this.#measuredWindowStart === windowStart;
        if (this.#placeholderMode && this.#measuredWindow !== undefined) return;
        const visible = this.#visible;
        const seen = sameWindow ? this.#measuredVisible : undefined;
        if (sameWindow && seen?.start === visible?.start && seen?.count === visible?.count) return;
        this.#measuredWindow = window;
        this.#measuredWindowStart = windowStart;
        this.#measuredVisible = visible;
        if (visible === undefined) return;
        const widths = [...this.#autoWidths];
        for (let index = visible.start; index < visible.start + visible.count; index++) {
            if (seen !== undefined && index >= seen.start && index < seen.start + seen.count) continue;
            const slice = index - windowStart;
            if (slice < 0 || slice >= window.length) continue;
            const row = itemAt(window, slice);
            this.#columns.forEach(({ column, spec, paintsValue }, c) => {
                // A Template or Action cell has no text to measure: observed, the empty string would hold the
                // column at MinWidth, below the estimate it already carries (ADR-0016, ADR-0020).
                if (!spec.width.isAuto || !paintsValue) return;
                const text = textFor(column, column.value(row));
                widths[c] = itemAt(widths, c).observe(this.#metrics.cellMetrics.for(column.type).estimatePx(text));
            });
        }
        this.#autoWidths = widths;
    }

    // A new geometry only when something it is made of moved, so an unchanged layout keeps its instance and
    // the rows keep skipping (ADR-0003).
    #resolveColumnGeometry(viewportWidthPx: number): void {
        const widths = this.#columns.map(({ spec }, c) => spec.resolveWidthPx(itemAt(this.#autoWidths, c)));
        const geometry = this.#styles.geometry;
        const width = Math.max(1, viewportWidthPx);
        if (geometry.count === widths.length && geometry.requestedPinnedCount === this.#pinnedCount
            && geometry.viewportWidthPx === width && geometry.devicePixelRatio === this.#devicePixelRatio
            && widths.every((px, c) => geometry.declaredWidthPxOf(c) === px)) {
            return;
        }
        this.#styles = new ColumnStyles(new ColumnGeometry({
            widthsPx: widths,
            pinnedCount: this.#pinnedCount,
            viewportWidthPx: width,
            ...(this.#devicePixelRatio === undefined ? {} : { devicePixelRatio: this.#devicePixelRatio }),
        }));
    }

    #raiseRangeIfUncovered(): void {
        // Mid-fling the range is passing through; settling raises the one landed on (ADR-0004).
        const range = this.#visible;
        if (this.#placeholderMode) return;
        const { window, windowStart } = this.#state;
        if (range === undefined || (range.start >= windowStart && range.start + range.count <= windowStart + window.length)) {
            this.#lastRaised = undefined;
            return;
        }
        // Asked once, and not again until the Window moved: where it sits counts, never which instance it
        // is, or a source answering with rows that still miss would be asked forever (ADR-0001).
        const last = this.#lastRaised;
        if (last !== undefined && last.range.start === range.start && last.range.count === range.count
            && last.windowStart === windowStart && last.windowCount === window.length) {
            return;
        }
        this.#lastRaised = { range, windowStart, windowCount: window.length };
        // A source whose fetch fails says so through its own rejection, which reaches the page unheard as
        // any unhandled error does: nothing here makes it go away (ADR-0025).
        void this.#source.needRange(rowRange(range.start, range.count));
    }

    // ---- The Selection, the keys and the press (ADR-0011, ADR-0012, ADR-0052) ----------------------------

    get #extent(): GridExtent {
        return { rowCount: this.#totalRows, columnCount: this.#columns.length };
    }

    // Drops the Selection when the positions it is written in stop meaning what they meant (ADR-0011): the
    // Row Sequence Version for the rows, which moves on with another source too, and the columns by name,
    // in order, never the array's identity, which a Framework Adapter hands afresh on every render.
    #reconcileSelection(): void {
        const version = this.#sequence();
        const names = this.#columns.map(({ column }) => column.name);
        const columnsMoved = !sameItems(names, this.#columnNames);
        const was = this.#held.selection;
        this.#held = columnsMoved ? HeldSelection.empty(version) : this.#held.under(version);
        // A view handed ahead is taken with the version it names, so the Selection and the version arrive together
        // and nothing between them meets an empty one (ADR-0331). One that does not fit the rows shown is not.
        const ahead = this.#viewAhead;
        if (ahead !== undefined && ahead.version === this.#handedVersion()) {
            this.#viewAhead = undefined;
            const extent = this.#extent;
            if (!columnsMoved && ahead.selection.ranges.every(range => bottomRow(range) < extent.rowCount && range.leftColumn + range.columnCount <= extent.columnCount)) {
                this.#held = HeldSelection.of(version, ahead.selection);
            }
            // The offsets come back either way: the browser keeps them inside the rows shown.
            this.#scrollTopPx = ahead.topPx;
            this.#scrollLeftPx = ahead.leftPx;
            this.#scrollOwed = true;
            this.#viewScrollUnclamped = true;
        }
        if (!was.isEmpty && this.#held.selection.isEmpty) {
            const interactive = this.#interactive;
            this.#droppedAim = {
                focus: columnsMoved ? undefined : was.focus,
                reason: this.#source === this.#heldSource ? 'orderMoved' : 'sourceChanged',
                chosenAction: !columnsMoved && interactive !== undefined && was.focus !== undefined && sameCell(interactive.cell, was.focus)
                    ? interactive.action : undefined,
                told: false,
            };
            this.#drops += 1;
            this.#lastDrop = this.#droppedAim;
        }
        this.#heldSource = this.#source;
        this.#columnNames = names;
        // The Context Menu's commands act on the Selection it was opened over; an order, columns or a source
        // that moved took that Selection, and the menu goes with it, as a Cancel (ADR-0011, ADR-0036). A
        // column's menu follows its column wherever it moved, and goes with it, or with the ▾ when the column
        // menu is hidden.
        // The find panel stands through both: a step's answer is judged against the order it was asked under.
        const popover = this.#popover;
        // So does a Consumer's popover: what it acts on is the Consumer's to judge (ADR-0050, item 16).
        if (popover !== undefined && popover.kind !== 'find' && popover.kind !== 'consumer' && (popover.kind === 'context'
            ? columnsMoved || popover.version !== version || popover.source !== this.#source
            : !names.includes(popover.column) || this.#hideColumnMenu)) {
            this.#closePopover(true);
        }
        this.#reconcileEdit();
        // The Copied Range is written in the same coordinates and dropped on the same triggers; it also goes
        // when a copied cell the Window holds comes to read otherwise (ADR-0170).
        const copied = this.#copied;
        if (copied !== undefined && (copied.version !== version || !sameItems(copied.columnNames, names)
            || !copied.holds(this.#columns, this.#state.window, this.#state.windowStart))) {
            this.#copied = undefined;
        }
    }

    // Takes a gesture's Selection; whether it changed anything.
    #apply(next: Selection): boolean {
        // A gesture's own Selection is what the next key is aimed at.
        this.#droppedAim = undefined;
        if (next.equals(this.#held.selection)) return false;
        this.#held = this.#held.with(next);
        return true;
    }

    // The Consumer hears the Selection the pass painted, once however many gestures made it, and the live
    // region is armed for it (ADR-0033).
    #raiseSelection(): void {
        const selection = this.#held.selection;
        if (selection === this.#raised) return;
        this.#raised = selection;
        this.#scheduleAnnouncement(selection);
        this.#onSelectionChanged?.(selection);
    }

    // Only when the ranges moved: a Focus move announces nothing, aria-activedescendant moved with it. A
    // 1×1 or empty Selection is not announced, and a sentence describing the one it replaced is emptied,
    // silently (A11Y-10). The rest is said once the Selection has stood still for the settle delay.
    #scheduleAnnouncement(selection: Selection): void {
        const { ranges } = selection;
        if (sameRanges(ranges, this.#announcedRanges)) return;
        if (ranges.length === 0 || (ranges.length === 1 && rangeCellCount(itemAt(ranges, 0)) === 1)) {
            this.#announcedRanges = ranges;
            this.#cancelAnnounce?.();
            this.#cancelAnnounce = undefined;
            if (this.#describesSelection) this.#say('');
            return;
        }
        this.#cancelAnnounce?.();
        this.#cancelAnnounce = this.#reports.clock.setTimer(this.#onAnnounceSettled, settleDelayMs);
    }

    readonly #onAnnounceSettled = (): void => {
        this.#cancelAnnounce = undefined;
        if (this.#destroyed) return;
        const selection = this.#held.selection;
        if (!sameRanges(selection.ranges, this.#announcedRanges)) {
            this.#announcedRanges = selection.ranges;
            this.#say(this.#describe(selection));
            this.#describesSelection = true;
        }
        this.#lookSettled();
    };

    #say(sentence: string): void {
        this.#describesSelection = false;
        this.#announce.textContent = sentence;
    }

    // The extent in one sentence, naming the corners by index (ADR-0033), as the C# core words it.
    #describe(selection: Selection): string {
        const { ranges } = selection;
        if (ranges.length === 0) return '';
        if (ranges.length > 1) return `${selection.cellCount} cells in ${ranges.length} ranges selected`;
        const range = itemAt(ranges, 0);
        const left = itemAt(this.#columns, range.leftColumn).header;
        const right = itemAt(this.#columns, range.leftColumn + range.columnCount - 1).header;
        return `${range.rowCount} rows by ${range.columnCount} columns selected, `
            + `${left} ${range.topRow + 1} to ${right} ${range.topRow + range.rowCount}`;
    }

    readonly #onKeyDown = (event: KeyboardEvent): void => {
        // The keyboard came by a key, which the root's ring says from here on (KB-12, ADR-0080).
        this.#keyboardByPress = false;
        // Mid-composition an IME owns Enter, Escape and the arrows; one it takes in the Keyboard Field is told
        // until its keyup (ADR-0080). A modifier's own keydown is no key.
        if (event.isComposing || event.keyCode === 229) {
            if (event.target === this.#keyField) this.#imeKeyTold = true;
            return;
        }
        if (modifierKeys.has(event.key)) return;
        const press: KeyPress = {
            key: event.key, ctrl: event.ctrlKey, shift: event.shiftKey, alt: event.altKey, meta: event.metaKey,
            metaIsPrimary: this.#metaIsPrimary,
        };
        const popover = this.#popover;
        const onGrid = event.target === this.#root || event.target === this.#keyField;
        const inPopover = popover !== undefined && event.target instanceof Node && popover.element.contains(event.target);
        const escape = event.key === 'Escape' && !event.ctrlKey && !event.altKey && !event.metaKey;
        // A held Escape is one press (ADR-0012, refined with ADR-0070): the press peels its one layer, and the
        // repeats dismiss nothing more, nor end the release of Tab the press made. Otherwise a held key closed a
        // popover and then cancelled the edit under it, or released Tab and raised onLeave again and again (DC-62).
        // Only a press the grid answered: one it left to the page, a control or a chord keeps its repeats too, and a
        // control of the Consumer's in a cell keeps its own whatever brought the keyboard back to it.
        const inCell = event.target instanceof Element && event.target !== this.#keyField && this.#viewport.contains(event.target);
        if (!event.repeat) this.#escapeAnswered = false;
        else if (event.key === 'Escape' && this.#escapeAnswered && !inCell) {
            this.#taken(event);
            return;
        }
        // Escape peels a popover before anything else, from inside it or from the grid (ADR-0012's layering,
        // ADR-0039); the keyboard comes back to the grid.
        if (popover !== undefined && escape && (onGrid || inPopover)
            && !(popover.kind === 'consumer' && popover.frame.innerPopupOpen)) {
            this.#taken(event);
            this.#closePopover(true);
            return;
        }
        const edit = this.#edit;
        // The Formula Bar's text is the editor's second surface, and its keys are the editor's (ADR-0051).
        if (edit !== undefined && (event.target === edit.element || event.target === this.#bar.text)) {
            this.#onEditorKey(event, press, edit);
            return;
        }
        // The bar's text with no edit open reads a cell that does not edit: Escape, Enter and Tab hand the keyboard
        // back to the grid, since the field is no tab stop to leave by (ADR-0051, ADR-0020).
        if (edit === undefined && event.target === this.#bar.text
            && (event.key === 'Escape' || event.key === 'Enter' || event.key === 'Tab')) {
            this.#taken(event);
            this.#takeKeyboard();
            return;
        }
        // A key the IME did not take, typed while the editor a composition opened still waits for the keyboard
        // in the Keyboard Field: the key it took before has been told whole, so the editor takes the keyboard
        // now, and the key is the editor's, its character typed where DOM focus now is (ADR-0080).
        if (edit !== undefined && event.target === this.#keyField) {
            this.#imeKeyTold = false;
            this.#editorFocusOwed = true;
            this.#focusEditor();
            this.#onEditorKey(event, press, edit);
            return;
        }
        // Ctrl+F and Ctrl+H on the grid, or anywhere in a popover of its own, its fields included: the find panel
        // takes the popover's place, and the browser's own find, which sees only the painted rows, stays shut. Taken
        // on every grid, and refused where nothing can search (ADR-0055, ADR-0332 §2).
        if (onGrid || inPopover) {
            const { kind } = resolveKey(press);
            if (kind === 'find' || kind === 'replace') {
                this.#taken(event);
                // A Consumer's popover holds work of the Consumer's, as Format Cells holds a draft: the find panel does
                // not take its place, and the browser's find stays shut too.
                if (!(inPopover && popover.kind === 'consumer')) this.#openFind(kind === 'replace');
                return;
            }
        }
        // Escape from a control inside a cell, a Template Column's own, is the way out of that cell and not of
        // the grid (ADR-0020): the keyboard comes back to the grid, and an Interactive cell the pointer walked
        // away from ends too. Not from a control of a grid nested in a cell, whose own Escape it is. A held
        // Escape's repeats are the control's: the press brought the keyboard back, and they bring nothing more.
        const target = event.target;
        if (!event.repeat && target instanceof Element && target !== this.#keyField && this.#viewport.contains(target)
            && target.closest('.ex-grid') === this.#root && resolveKey(press).kind === 'leave') {
            this.#taken(event);
            this.#interactive = undefined;
            this.#takeKeyboard();
            return;
        }
        // A key typed in a control inside the root is that control's: only the root and this grid's own
        // Keyboard Field are the grid's (ADR-0010, ADR-0080).
        if (target !== this.#root && target !== this.#keyField) return;
        // A released Tab is the browser's, and any other key ends the release (ADR-0012).
        if (this.#tabReleased) {
            this.#tabReleased = false;
            const canonical = canonicalKey(press);
            if (canonical === 'Tab' || canonical === 'Shift+Tab') return;
        }
        // A held Space engages once (ADR-0037): it fires a single action, and its repeats would fire it again on
        // the same row for as long as it is held. They are taken, so the page does not scroll by them either,
        // and dropped. Shift+Space and Ctrl+Space name whole regions, and repeat harmlessly.
        if (event.repeat && canonicalKey(press) === ' ') {
            this.#taken(event);
            return;
        }
        const action = resolveKey(press);
        // Inside a cell with several actions the keyboard never left the root (ADR-0037): the arrows, Home and
        // End choose within it, clamped at both ends, never leaving it by overshoot; Space and Escape are
        // answered below; every other key leaves the cell first and keeps the meaning it has everywhere else,
        // Enter and Tab included, which is why nothing ever fires on them (ADR-0020).
        this.#reconcileSelection();
        const choice = this.#interactiveChoice();
        if (choice !== undefined) {
            const chosen = this.#choiceAfter(choice, action);
            if (chosen !== undefined) {
                this.#taken(event);
                this.#interactive = { cell: choice.cell, action: chosen };
                return;
            }
            if (action.kind !== 'engage' && action.kind !== 'leave') {
                this.#interactive = undefined;
                this.#invalidate();
            }
        }
        // Space engages with the Focus cell's content (ADR-0020, ADR-0037). Aimed with a Selection an order move
        // or a replaced source has dropped since, it engages nothing, never the Focus in force the user did not
        // aim at, and an action it was aimed at is refused (ADR-0142). With nothing selected it is a first key
        // like any other, and only places the Focus (ADR-0012): it never fires or enters a cell the user has not
        // seen focused.
        if (action.kind === 'engage') {
            const dropped = this.#droppedAim;
            if (dropped !== undefined) {
                this.#taken(event);
                this.#refuseDroppedSpace(dropped);
                return;
            }
            if (!this.#held.selection.isEmpty) {
                this.#taken(event);
                this.#engage();
                return;
            }
        }
        // F2 and a character the key table does not name open the Cell Editor on a cell that edits (ADR-0010):
        // the character typed onto it is not lost, since the editor opens holding it; Caret keeps the cell's
        // text. Shift+Space and Ctrl+Space are the key table's, and select.
        const typed = action.kind === 'none' && opensAnEdit(event.key, event.ctrlKey, event.altKey, event.metaKey);
        if (this.#canEdit && typed) {
            const caret = event.key === 'F2';
            if (this.#tryOpenEdit(caret ? 'caret' : 'overwrite', caret ? undefined : event.key)) {
                this.#taken(event);
                return;
            }
        }
        // Backspace opens an empty Overwrite on a grid that edits (ADR-0035).
        if (action.kind === 'clearAndEdit' && this.#canEdit) {
            this.#taken(event);
            this.#tryOpenEdit('overwrite', '');
            return;
        }
        // Ctrl+Z and Ctrl+Y are forwarded and nothing else, while the Consumer listens (ADR-0007): the history,
        // and what the screen shows afterwards, are the Consumer's, and arrive as new row instances.
        // An edit left open while the keyboard went elsewhere (ED-26) is answered first: none of these keys
        // writes past it, or undoes under it.
        const history = this.#edit !== undefined ? undefined : action.kind === 'undo' ? this.#onUndo : action.kind === 'redo' ? this.#onRedo : undefined;
        if (history !== undefined) {
            this.#taken(event);
            history();
            return;
        }
        // Delete clears, and Ctrl+D and Ctrl+R fill, on a grid that hears the write (ADR-0054, ADR-0035).
        const fills = action.kind === 'fillDown' || action.kind === 'fillRight';
        if (this.#edit === undefined
            && ((action.kind === 'clear' && this.#writes(this.#clearHeard)) || (fills && this.#writes(this.#pasteHeard)))) {
            this.#taken(event);
            if (action.kind === 'clear') this.#clear();
            else this.#fillFromKey(action.kind === 'fillDown' ? 'down' : 'right');
            return;
        }
        // The ContextMenu key and Shift+F10 open the Context Menu on the Focus, and the browser's own never
        // appears (ADR-0036, CTX-4).
        if (action.kind === 'openContextMenu') {
            this.#taken(event);
            this.#openContextMenuAtFocus();
            return;
        }
        // Alt+↓ opens the Focus's column menu, on a grid whose headers carry one (ADR-0039).
        if (action.kind === 'openColumnMenu' && !this.#hideColumnMenu) {
            this.#taken(event);
            this.#openColumnMenuAtFocus();
            return;
        }
        if (action.kind !== 'leave' && !movingKinds.has(action.kind)) return;
        this.#taken(event);
        if (action.kind === 'leave') {
            this.#reconcileSelection();
            // The Interactive layer comes first (ADR-0037, ADR-0012's layering): Escape leaves the cell, and the
            // Focus and the keyboard stay. The Copied Range is the last layer before the way out (ADR-0170): with
            // a copy outlined, Escape removes the outline and does nothing else. With nothing left to dismiss it
            // is the way out of Enter and Tab cycling: the keyboard stays and the next Tab leaves the grid.
            // An edit still open, reached by an Escape on the root rather than in the editor, is a layer too, and
            // the one the Escape cancels (ADR-0230): leaving over it would let the Consumer close a dialog on
            // text nobody committed.
            if (this.#edit !== undefined) this.#endEdit(undefined);
            else if (this.#interactiveChoice() !== undefined) this.#interactive = undefined;
            else if (this.#copied === undefined) this.#leave();
            else this.#copied = undefined;
        } else {
            this.#moveByKey(action, event.key);
        }
    };

    // The Escape with nothing left to dismiss (ADR-0012, ADR-0070): Tab is released first, so a Consumer's handler
    // that puts the keyboard elsewhere finds the release made, and then the Consumer that listens hears it.
    #leave(): void {
        this.#tabReleased = true;
        this.#leaveHeard?.();
    }

    // A key the grid answers: the browser's own meaning is prevented, nothing inside hears it, and the pass
    // that paints the answer is asked for.
    #taken(event: KeyboardEvent): void {
        event.preventDefault();
        event.stopPropagation();
        if (event.key === 'Escape' && !event.repeat) this.#escapeAnswered = true;
        this.#answered += 1;
        this.#invalidate();
    }

    #moveByKey(action: GridKeyAction, key: string): void {
        this.#reconcileSelection();
        const geometry = this.#geometry;
        const visible = this.#visible;
        // Nothing painted is nothing the keys can be aimed at.
        if (geometry === undefined || visible === undefined) return;
        const start = defined(this.#firstVisibleCell(), 'the first cell of painted rows');
        // PageUp and PageDown move by the rows fully visible, at least one (ADR-0012).
        const viewportRows = Math.max(1, Math.floor(geometry.viewportHeightPx / this.#metrics.rowHeightPx));
        // The Consumer's edge is asked on the arrows only: Home and End resolve to the same moves and keep ADR-0012's
        // meaning (ADR-0050, item 2).
        const edge = this.#dataEdge !== undefined && key.startsWith('Arrow') ? this.#dataEdge : undefined;
        const move = moveByKey(action, this.#held.selection, this.#extent, start, viewportRows, edge);
        if (move === undefined) return;
        this.#apply(move.selection);
        this.#revealOwed = move.reveal;
        const focus = move.selection.focus;
        // And the page follows what the reveal brings into view (ADR-0204).
        if (focus !== undefined) this.#followArmed = { focus, target: move.reveal.target };
    }

    // ---- Actions and Template cells (ADR-0020, ADR-0037, ADR-0142) --------------------------------------

    // Space on the Focus cell (ADR-0020, completed by ADR-0037): fires a single action; enters a cell with
    // several, the first one chosen; asks a Template cell's content to take the keyboard; and opens Overwrite
    // on a cell that edits, the space already in it. Inside an Interactive cell it fires the chosen action, and
    // leaves: an action changes the row under it, and staying would leave the chosen action over whatever row
    // slid into its place, one Space from firing on that. A Focus over a row not in hand engages nothing.
    #engage(): void {
        const focus = defined(this.#held.selection.focus, 'the Focus Space engages');
        const resolved = itemAt(this.#columns, focus.column);
        const choice = this.#interactiveChoice();
        this.#interactive = undefined;
        // The Mark Column acts on the Selection's rows, not on the one under the Focus, and needs none of them in
        // hand: they are named by position (ADR-0043).
        if (resolved.mark) {
            this.#markSelectedRows(focus.column);
            return;
        }
        const held = this.#rowAt(focus.row);
        if (held === undefined) return;
        const pressed: PressedRow<Row> = { row: held.row, index: focus.row, sequence: this.#sequence(), source: this.#source };
        const { actions, template } = resolved;
        if (choice !== undefined) {
            this.#pressAction(pressed, resolved, itemAt(actions, choice.action));
        } else if (actions.length === 1) {
            this.#pressAction(pressed, resolved, itemAt(actions, 0));
        } else if (actions.length > 1) {
            // The keyboard stays on the root: what changes is which action the row paints as chosen, and what
            // aria-activedescendant names. A chosen button nobody can see is no choice, so the Focus is revealed.
            this.#interactive = { cell: focus, action: 0 };
            this.#revealOwed = { target: focus, extending: undefined, rowStart: false, stepRows: 0 };
        } else if (template !== undefined) {
            // Asked, never reached into: the pass that paints the cell hands the request to its content, which
            // focuses its own control (ADR-0037), so the cell has to be painted for it.
            this.#focusRequests += 1;
            this.#focusRequest = { cell: focus, number: this.#focusRequests };
            this.#revealOwed = { target: focus, extending: undefined, rowStart: false, stepRows: 0 };
        } else if (this.#canEdit) {
            this.#tryOpenEdit('overwrite', ' ');
        }
    }

    // Space with the Focus in the Mark Column (ADR-0043, MK-1): every row with a selected cell in that column,
    // once each, as positions under the order the Selection was made in, since past the Window the grid has no
    // identity to name them by. The marks bring them into line: all marked if any was not.
    #markSelectedRows(column: number): void {
        const marks = this.#rowMarks.marks;
        const ranges = rowsSelectedIn(this.#held.selection.ranges, column);
        if (marks === undefined || ranges.length === 0) return;
        this.#raiseMark(marks, { kind: 'positions', ranges, rowSequenceVersion: this.#state.rowSequenceVersion });
    }

    // One marking gesture, one intent, never one per row (ADR-0043, MK-3). The marks apply it in their own time:
    // the grid paints what they answer when they say they changed. A failure of theirs is theirs, and reaches
    // the page as their unhandled rejection rather than a mark the grid pretends was made.
    #raiseMark(marks: RowMarks<Row>, intent: RowMarkIntent<Row>): void {
        void marks.onMarkIntent(intent);
    }

    // A row's checkbox pressed (ADR-0043): its row by identity, wherever an order move has taken it, asked for the
    // state the checkbox did not show when it was pressed, which is what the gesture was taken against: a mark
    // the Consumer applied since, by another gesture, does not turn the press around (principle 6). Made on what
    // a source since replaced painted, or on a row the Window no longer holds, or that only a position under a
    // moved order names, it marks nothing and is refused once, and said as an action's refusal is (ADR-0043's
    // note of 2026-10-09, MK-9; ADR-0142, ADR-0160).
    #pressRowMark(pressed: PressedRow<Row>, marked: boolean): void {
        const marks = this.#rowMarks.marks;
        if (marks === undefined) return;
        if (pressed.source !== this.#source) {
            this.#refuseRowMark('sourceChanged');
            return;
        }
        this.#source.publishGathered?.();
        this.#reconcileSelection();
        const now = this.#pressedRowNow(pressed);
        if ('refused' in now) {
            this.#refuseRowMark(now.refused);
            return;
        }
        this.#raiseMark(marks, { kind: 'oneRow', row: now.row, marked });
    }

    #refuseRowMark(reason: MarkRefusalReason): void {
        this.#sayRefusal(this.#labelOf(rowCommandRefusalId));
        this.#onRefused?.({ kind: 'mark', reason });
    }

    // The header's checkbox pressed (ADR-0043): every Detail row of the current result, as it stands now, marked,
    // unless all are marked already, which unmarks them; counts not yet known mark. It names the result as it
    // stood at the press: made on what a source since replaced painted, or under an order that has moved since,
    // it marks nothing and is refused once, for the Consumer to word (MK-9). A click no press came before, one a
    // script made, names what the header names now.
    #pressHeaderMark(press: { readonly sequence: number; readonly source: unknown } | undefined): void {
        const marks = this.#rowMarks.marks;
        if (marks === undefined) return;
        if (press !== undefined && (press.source !== this.#source || press.sequence !== this.#sequence())) {
            this.#onRefused?.({ kind: 'mark', reason: press.source === this.#source ? 'orderMoved' : 'sourceChanged' });
            return;
        }
        const { counts } = marks;
        this.#raiseMark(marks, {
            kind: 'allRows', marked: rowMarkHeaderPress(counts === undefined ? 'some' : rowMarkHeaderState(counts)),
            rowSequenceVersion: this.#state.rowSequenceVersion,
        });
    }

    // Space aimed with a Selection an order move or a replaced source has dropped since (ADR-0142, LV-12,
    // LV-20): an action it would have fired is refused, naming no row, since the grid kept no row and no Row
    // Key of that Selection (ADR-0160). One action fires on Space; of several, the one chosen in the cell, and
    // none before one was.
    #refuseDroppedSpace(dropped: DroppedAim): void {
        const focus = dropped.focus;
        if (focus === undefined || focus.column >= this.#columns.length) return;
        const { column, actions } = itemAt(this.#columns, focus.column);
        const fired = actions.length === 1 ? 0 : dropped.chosenAction;
        if (fired === undefined || fired >= actions.length) return;
        this.#refuseAction({
            kind: 'action', reason: dropped.reason === 'sourceChanged' ? 'sourceChanged' : 'orderMoved', row: undefined,
            column: column.name, action: itemAt(actions, fired).name,
        });
    }

    // An action refused is said in the live region, in the words a row command whose row is gone says
    // (A11Y-16), and raised by name (ADR-0142): nothing the press would have done was done.
    #refuseAction(refusal: GridRefusal<Row> & { readonly kind: 'action' }): void {
        this.#sayRefusal(this.#labelOf(rowCommandRefusalId));
        this.#onRefused?.(refusal);
    }

    // An action pressed, by pointer or by Space, raised on the row it was pressed on as that row is now
    // (ADR-0142, LV-12), or refused. Acted on the newest rows the source holds: one that gathers live changes
    // puts them out first (D5). Nothing else is done, not even to the keyboard (ADR-0037, amended): a press
    // never focused the button, and what the handler opens is the Consumer's.
    #pressAction(pressed: PressedRow<Row>, resolved: ResolvedColumn<Row>, action: GridAction): void {
        const column = resolved.column.name;
        // Made on what a source since replaced painted: its row is another source's, whatever the new one holds
        // there, so the refusal names no row.
        if (pressed.source !== this.#source) {
            this.#refuseAction({ kind: 'action', reason: 'sourceChanged', row: undefined, column, action: action.name });
            return;
        }
        this.#source.publishGathered?.();
        this.#reconcileSelection();
        const now = this.#pressedRowNow(pressed);
        if ('refused' in now) {
            // The refusal names the row only while the Window holds it (ADR-0160).
            this.#refuseAction({
                kind: 'action', reason: now.refused, row: this.#windowHolds(pressed.row) ? pressed.row : undefined, column, action: action.name,
            });
            return;
        }
        this.#onAction?.({ row: now.row, column, action: action.name });
    }

    // The row a press acts on now (ADR-0142, LV-12): with a Row Key, the row under the pressed row's key,
    // wherever the Window holds it (ADR-0140); without one, the pressed instance while the Window holds it,
    // else the row at the position it was pressed at, while the order it was pressed under holds.
    #pressedRowNow(pressed: PressedRow<Row>): { readonly row: Row } | { readonly refused: ActionRefusalReason } {
        const rowKey = this.#source.rowKey;
        if (rowKey !== undefined) {
            // A key met twice in one Window names its row only where it was pressed (ADR-0140).
            const at = this.#positionOfKey(keyOf(rowKey, pressed.row, 'The row an action was pressed on'), pressed.index);
            const found = at === undefined ? undefined : this.#rowAt(at);
            return found === undefined ? { refused: 'rowLeftTheWindow' } : found;
        }
        if (this.#state.window.includes(pressed.row)) return { row: pressed.row };
        if (this.#sequence() !== pressed.sequence) return { refused: 'orderMoved' };
        const at = this.#rowAt(pressed.index);
        return at === undefined ? { refused: 'rowLeftTheWindow' } : at;
    }

    // Whether the Window holds a row: by its key where the source keys its rows, else as the instance.
    #windowHolds(row: Row): boolean {
        const rowKey = this.#source.rowKey;
        if (rowKey === undefined) return this.#state.window.includes(row);
        const key = keyOf(rowKey, row, 'The row an action was pressed on');
        return this.#state.window.some(held => keyOf(rowKey, held, 'A row of the Window') === key);
    }

    // The chosen action of the Interactive cell, or none, including when the field is stale: Interactive
    // belongs to the Focus, so a Focus moved or dropped since, by a press, a sort or new columns, has ended it,
    // whatever the field still says (ADR-0037).
    #interactiveChoice(): { readonly cell: CellPosition; readonly action: number } | undefined {
        const interactive = this.#interactive;
        const focus = this.#held.selection.focus;
        if (interactive === undefined || focus === undefined || !sameCell(focus, interactive.cell)
            || interactive.cell.column >= this.#columns.length) {
            return undefined;
        }
        const { actions } = itemAt(this.#columns, interactive.cell.column);
        return actions.length > 1 && interactive.action < actions.length ? interactive : undefined;
    }

    // The action a key chooses inside an Interactive cell, or none for a key that does not choose (ADR-0037):
    // ← and → step, and Home and End, with the Ctrl+arrows that resolve to the same edge move, go to either
    // end. Clamped at both, never wrapping.
    #choiceAfter(choice: { readonly cell: CellPosition; readonly action: number }, action: GridKeyAction): number | undefined {
        const last = itemAt(this.#columns, choice.cell.column).actions.length - 1;
        const left = action.direction === 'left';
        if (!left && action.direction !== 'right') return undefined;
        if (action.kind === 'move') return left ? Math.max(0, choice.action - 1) : Math.min(last, choice.action + 1);
        if (action.kind === 'moveToEdge') return left ? 0 : last;
        return undefined;
    }

    // A press on an action's button (ADR-0020). Its default is prevented, so the button never takes the
    // keyboard: a press dragged off it, the platform's way to cancel a click, would leave it focused, and Enter
    // would then fire it natively, the key that never fires (ADR-0037). What it stood for is noted for its
    // click, as the row was painted when it was pressed.
    readonly #onActionPress = (event: MouseEvent): void => {
        const button = event.target;
        const stands = button instanceof Element ? this.#actionButtons.get(button) : undefined;
        if (stands === undefined || !(button instanceof Element)) return;
        event.preventDefault();
        if (event.button !== 0) return;
        const pressed = this.#pressedRowOf(button);
        this.#actionPress = pressed === undefined ? undefined : { ...stands, button, pressed };
    };

    // The click on an action's button: acted on the row it was pressed on (ADR-0142). A click no press of
    // the pointer came before, one a script made, is taken on the row as painted now.
    readonly #onActionClick = (event: MouseEvent): void => {
        const press = this.#actionPress;
        this.#actionPress = undefined;
        const button = event.target;
        const stands = button instanceof Element ? this.#actionButtons.get(button) : undefined;
        if (stands === undefined || !(button instanceof Element)) return;
        const pressed = press !== undefined && press.button === button ? press.pressed : this.#pressedRowOf(button);
        if (pressed === undefined) return;
        this.#answered += 1;
        this.#invalidate();
        // A press past an open editor commits it first, as any click away does; a Reject or a refusal holds it,
        // and the press keeps no meaning of its own (ADR-0034).
        if (!this.#commitEdit()) return;
        this.#pressAction(pressed, stands.column, stands.action);
    };

    // A press whose button a pass took away before its release, as a row the source handed over again is
    // painted anew: the browser clicks no button then, so the press is answered at its release, over the
    // button that stands in its place, for the same action of the same row. A release anywhere else is the
    // platform's cancel, and ends the press. No press is lost, and none fires twice (ADR-0142).
    #releaseActionPress(event: MouseEvent): void {
        const press = this.#actionPress;
        const target = event.target;
        if (press === undefined || target === press.button) return;
        this.#actionPress = undefined;
        if (press.button.isConnected || !(target instanceof Element)) return;
        const over = this.#actionButtons.get(target);
        const overRow = this.#pressedRowOf(target);
        if (over === undefined || overRow === undefined || over.column.column.name !== press.column.column.name
            || over.action.name !== press.action.name || !this.#sameRowPainted(press.pressed, overRow)) {
            return;
        }
        if (!this.#commitEdit()) return;
        this.#pressAction(press.pressed, press.column, press.action);
    }

    // Whether two painted rows are one row: by its key where the source keys its rows, else by its position
    // under the same order, a new instance there being its new version (ADR-0140).
    #sameRowPainted(was: PressedRow<Row>, now: PressedRow<Row>): boolean {
        const rowKey = this.#source.rowKey;
        if (rowKey === undefined) return was.sequence === now.sequence && was.index === now.index;
        return keyOf(rowKey, was.row, 'The row an action was pressed on') === keyOf(rowKey, now.row, 'A row of the Window');
    }

    // The row a button was painted in, under the order and source of the pass that painted it.
    #pressedRowOf(button: Element): PressedRow<Row> | undefined {
        const element = button.closest('[role="row"]');
        const painted = element === null ? undefined : this.#rowsPainted.get(element);
        const under = this.#paintedUnder;
        return painted === undefined || under === undefined ? undefined : { ...painted, ...under };
    }

    // A press on a row's checkbox (ADR-0043): its default prevented, so it never takes the keyboard, as an action's
    // button does not, and the row it was painted in noted for its click (MK-9).
    readonly #onMarkPress = (event: MouseEvent): void => {
        const box = event.target;
        if (!(box instanceof Element) || !this.#markBoxes.has(box)) return;
        event.preventDefault();
        if (event.button !== 0) return;
        const pressed = this.#pressedRowOf(box);
        this.#markPress = pressed === undefined ? undefined : { box, pressed };
    };

    // The click on a row's checkbox: on the row it was pressed on. A click no press of the pointer came before,
    // one a script made, is taken on the row as painted now. An open edit is committed first, as for an action.
    readonly #onMarkClick = (event: MouseEvent): void => {
        const press = this.#markPress;
        this.#markPress = undefined;
        const box = event.target;
        if (!(box instanceof Element) || !this.#markBoxes.has(box)) return;
        const pressed = press !== undefined && press.box === box ? press.pressed : this.#pressedRowOf(box);
        if (pressed === undefined) return;
        this.#answered += 1;
        this.#invalidate();
        if (!this.#commitEdit()) return;
        this.#pressRowMark(pressed, this.#markBoxes.get(box) !== true);
    };

    // A press whose checkbox a pass took away before its release, as an action's (#releaseActionPress): answered
    // at its release over a checkbox, as its click would have been, on the row it was pressed on, by identity
    // wherever an order move has taken it, or refused (MK-9); ended by a release anywhere else, the platform's
    // cancel. Unlike an action's, it is not held to the checkbox on the same row: a row's checkbox names its row
    // by identity, so a press released where it was made, over whatever row an order move put there, is still
    // the press the user made, whenever the move arrived (principle 6).
    #releaseMarkPress(event: MouseEvent): void {
        const press = this.#markPress;
        const target = event.target;
        if (press === undefined || target === press.box) return;
        this.#markPress = undefined;
        if (press.box.isConnected || !(target instanceof Element) || !this.#markBoxes.has(target)) return;
        if (!this.#commitEdit()) return;
        this.#pressRowMark(press.pressed, this.#markBoxes.get(press.box) !== true);
    }

    readonly #onPress = (event: MouseEvent): void => {
        // Only a press on the rows' own box, whose cells take no pointer events: its offsets are the
        // Viewport's, which the arithmetic reads (ADR-0012).
        if (event.button !== 0 || event.target !== this.#viewport) return;
        this.#reconcileSelection();
        const at = this.#cellUnder(event);
        if (at === undefined) return;
        this.#answered += 1;
        // A press past an open editor commits it, as Excel's click-away does; a Reject or a refusal holds it,
        // and the press keeps no meaning of its own: the keyboard stays in the editor (ADR-0034).
        if (!this.#commitEdit()) {
            event.preventDefault();
            this.#invalidate();
            return;
        }
        // A press is a new gesture: a Heading gesture whose release was not heard is over.
        this.#forgetHeadingDrag();
        // A press on the grid ends a release of Tab: the user has come back to it (ADR-0012).
        this.#tabReleased = false;
        const extent = this.#extent;
        const current = this.#held.selection;
        const toggling = this.#toggles(event);
        const next = event.shiftKey ? current.extendTo(at, extent)
            : toggling ? current.toggleRange(at, extent)
                : current.click(at, extent);
        this.#apply(next);
        // A Ctrl+press that took a cell out of the Selection starts no drag.
        this.#setDragging(!(toggling && current.contains(at)));
        this.#invalidate();
    };

    readonly #onDrag = (event: MouseEvent): void => {
        if ((event.buttons & 1) === 0) {
            this.#setDragging(false);
            return;
        }
        // A Heading drag over the header or the cells: only the pointer's column counts (ADR-0050).
        if (this.#headingDrag) {
            this.#moveHeadingDrag(event);
            return;
        }
        if (event.target !== this.#viewport) return;
        const at = this.#cellUnder(event);
        if (at === undefined) return;
        if (this.#apply(this.#held.selection.extendTo(at, this.#extent))) this.#invalidate();
        const y = this.#viewportYOf(event);
        if (y !== undefined) this.#updateEdgeBand(event.offsetX - this.#scrollLeftPx, y);
    };

    // A release on the rows is an input answered too, wherever its press was, as the C# core counts it
    // (ADR-0220): it ends a drag begun there or on the header.
    readonly #onRelease = (event: MouseEvent): void => {
        if (event.button !== 0) return;
        this.#answered += 1;
        this.#invalidate();
        this.#releaseActionPress(event);
        this.#releaseMarkPress(event);
    };

    // A drag ends at its release anywhere on the grid, over the header too; released outside it, the next
    // move without the button ends it (#onDrag). Nothing is heard on the document: every grid on the page
    // would hear every drag (ADR-0018).
    readonly #onDragEnd = (event: MouseEvent): void => {
        // Where a Heading drag is released counts as a move: a fast release reaches another column without
        // a move between (ADR-0050, item 1; ADR-0012).
        if (this.#headingDrag) this.#moveHeadingDrag(event);
        this.#setDragging(false);
    };

    // The pointer left the grid: the edge band stops, since outside it no event would contradict a running
    // timer. The drag stands, and the next move without the button ends it (ADR-0008).
    readonly #onLeave = (): void => {
        this.#stopEdgeTimer();
    };

    // The hover band (ADR-0029): the pointer moving onto another row moves it, by the same arithmetic a press
    // goes through; a move within the row it stands on paints nothing. Cells take no pointer events, so a move
    // over the rows lands on the Viewport. Inside it but on an element of its own, the editor or a Consumer's
    // control, the band stands; on the header or a scrollbar no row is under the pointer, and it goes.
    readonly #onHover = (event: MouseEvent): void => {
        if (event.target !== this.#viewport) {
            if (!(event.target instanceof Node && this.#viewport.contains(event.target))) this.#dropHover();
            return;
        }
        const row = this.#cellUnder(event)?.row;
        if (row === this.#hoverRow) return;
        this.#hoverRow = row;
        this.#invalidate();
    };

    // The pointer leaving the rows, or the rows moving under it, takes the band with it.
    readonly #dropHover = (): void => {
        if (this.#hoverRow === undefined) return;
        this.#hoverRow = undefined;
        this.#invalidate();
    };

    // ---- The Cell Editor (ADR-0010, ADR-0034, ADR-0142) ------------------------------------------------------

    // Whether typing can open an edit here: some column edits and somebody hears the intent (ADR-0080). A grid
    // where neither holds is display-only, and keeps the keyboard on its root.
    get #canEdit(): boolean {
        return this.#editHeard !== undefined && this.#columns.some(({ editable }) => editable);
    }

    // A cell an edit may open on: its column Editable and its row in hand, never mid-fling (ADR-0035).
    #isEditableCell(at: CellPosition): boolean {
        if (this.#editHeard === undefined || at.column < 0 || at.column >= this.#columns.length || !itemAt(this.#columns, at.column).editable) {
            return false;
        }
        const slice = at.row - this.#state.windowStart;
        return slice >= 0 && slice < this.#state.window.length && !this.#placeholderMode;
    }

    // The row the Window holds at a position, or none.
    #rowAt(row: number): { readonly row: Row } | undefined {
        const slice = row - this.#state.windowStart;
        return slice < 0 || slice >= this.#state.window.length ? undefined : { row: itemAt(this.#state.window, slice) };
    }

    // The text an edit opens on: the Consumer's where it declares one, as a Sheet's Entry (ADR-0051), and the
    // value's own otherwise.
    #openingTextAt(at: CellPosition): string {
        const declared = this.#cellPaint.editorText;
        const held = declared === undefined ? undefined : this.#rowAt(at.row);
        const text = declared === undefined || held === undefined ? undefined : declared(held.row, itemAt(this.#columns, at.column).column);
        return text ?? this.#paintedTextAt(at);
    }

    // The text a cell paints: what an edit opens on in Caret, and what a commit is told it saw and replaced.
    #paintedTextAt(at: CellPosition): string {
        const held = this.#rowAt(at.row);
        if (held === undefined) return '';
        const { column } = itemAt(this.#columns, at.column);
        return textFor(column, column.value(held.row));
    }

    // The cell a key pressed with nothing selected starts at: the first on screen (ADR-0012), in the first
    // Pinned Column, or the first scrollable column painted; with neither, there is no column at all.
    #firstVisibleCell(): CellPosition | undefined {
        const visible = this.#visible;
        if (visible === undefined) return undefined;
        const scrollable = this.#scrollable;
        return cellPosition(visible.start, this.#styles.geometry.pinnedCount > 0 || scrollable === undefined ? 0 : scrollable.start);
    }

    // Opens the Cell Editor on the Focus, placing one by the first key's rule where there is none (ADR-0012),
    // holding `typed`, or the cell's text where none was typed. A cell that does not edit opens nothing.
    // Whether the key is answered: opened, or thrown away with the Selection it was aimed at.
    #tryOpenEdit(mode: OpenEdit<Row>['mode'], typed: string | undefined): boolean {
        this.#reconcileSelection();
        if (this.#discardedAim()) return true;
        const extent = this.#extent;
        if (extent.rowCount <= 0 || extent.columnCount <= 0) return false;
        let selection = this.#held.selection;
        if (selection.isEmpty) {
            const start = this.#firstVisibleCell();
            if (start === undefined) return false;
            selection = selection.click(start, extent);
        }
        const focus = defined(selection.focus, 'the Focus of a Selection that is not empty');
        if (!this.#isEditableCell(focus)) return false;
        this.#apply(selection);
        this.#startEdit(focus, mode, typed ?? this.#openingTextAt(focus));
        return true;
    }

    // Typing aimed at a Selection the rows or the source took away since it was painted is thrown away, never
    // opened on the first cell of an order the user has not seen; the Consumer hears it once (ADR-0142, decided
    // 2026-10-08). Whether there was such an aim.
    #discardedAim(): boolean {
        const dropped = this.#droppedAim;
        if (dropped === undefined) return false;
        if (!dropped.told && dropped.focus !== undefined && this.#isEditableCell(dropped.focus)) {
            dropped.told = true;
            this.#onEditDiscarded?.(dropped.reason);
        }
        return true;
    }

    #startEdit(at: CellPosition, mode: OpenEdit<Row>['mode'], text: string, surface: OpenEdit<Row>['surface'] = 'cell'): void {
        // An edit opening ends the Copied Range, as an edit ends Excel's copy mode (ADR-0170).
        this.#copied = undefined;
        const element = this.#root.ownerDocument.createElement('input');
        element.className = 'ex-editor';
        element.value = text;
        // One uncommitted text in two places: what is typed in the cell is shown in the bar (ADR-0051), and a
        // press back into the cell types there again.
        element.addEventListener('input', this.#onEditorTyped);
        element.addEventListener('focus', this.#onEditorFocused);
        const rowKey = this.#source.rowKey;
        const held = defined(this.#rowAt(at.row), 'the row an edit opens on');
        this.#edit = {
            cell: at, mode, element, source: this.#source, columnNames: this.#columnNames, sequence: this.#sequence(),
            key: rowKey === undefined ? undefined : keyOf(rowKey, held.row, 'The row an edit opens on'),
            seenText: this.#paintedTextAt(at), openedOn: held.row, error: undefined,
            awaits: this.#userWritesOver(at), rebaseOwed: false, surface,
        };
        // The editor takes the keyboard in the task that opened it, with the caret placed by the core at the end
        // of the opening text: a key typed next lands in it whenever the pass paints, never in the Keyboard
        // Field behind it (the spine's sixth rule). The pass then sets it over its cell.
        this.#editorFocusOwed = true;
        const firstRow = [...this.#viewport.children].find(child => child.getAttribute('role') === 'row');
        for (const node of this.#paintEditor()) {
            if (!node.isConnected) this.#viewport.insertBefore(node, firstRow === undefined ? null : firstRow);
        }
        this.#focusEditor();
        this.#tellEditing();
        this.#invalidate();
    }

    // A key typed in the Cell Editor: the mode's own keys are the grid's, and every other is the field's
    // (ADR-0010). Escape cancels, F2 switches the mode, Ctrl+Enter fills, and the rest commit and then do what
    // they do with no edit open: Enter and Tab cycle, and Overwrite's arrows move or extend (ED-2).
    #onEditorKey(event: KeyboardEvent, press: KeyPress, edit: OpenEdit<Row>): void {
        const canonical = canonicalKey(press);
        if (!editingKeysByMode[edit.mode === 'caret' ? 'editing' : 'overwrite'].includes(canonical)) return;
        this.#taken(event);
        switch (canonical) {
            case 'Escape':
                // The uncommitted text was the grid's, and goes with the editor (ADR-0007).
                this.#endEdit(undefined);
                return;
            case 'F2':
                edit.mode = edit.mode === 'caret' ? 'overwrite' : 'caret';
                return;
            case 'Control+Enter':
                this.#commitFill(edit);
                return;
            case 'Control+f':
            case 'Control+F':
            case 'Control+h':
            case 'Control+H':
                // Find and Replace are disabled while a cell is being edited (ADR-0055, ADR-0332 §2), and the
                // browser's own would search only the painted rows.
                return;
            default:
                this.#commitAndKey(press);
        }
    }

    // Commits, and then does what the key does with no edit open (ADR-0010, ED-2). A held commit keeps the
    // key no meaning of its own (ADR-0034); a commit that left no Selection, its handler having moved the
    // order, moves nothing and places nothing from the first cell on screen (ADR-0011).
    #commitAndKey(press: KeyPress): void {
        if (!this.#commitEdit()) return;
        this.#reconcileSelection();
        if (this.#held.selection.isEmpty) return;
        const action = resolveKey(press);
        if (movingKinds.has(action.kind)) this.#moveByKey(action, press.key);
    }

    /**
     * A commit leaves the grid as an Edit Intent and nothing else (ADR-0007): the painted value does not change
     * until the source hands over a new row instance. True once no edit stands; false when a Reject or a
     * refusal held the editor, whose gesture then keeps no meaning of its own (ADR-0034).
     */
    #commitEdit(): boolean {
        const edit = this.#edit;
        if (edit === undefined) return true;
        // Made on the newest rows the source holds: one that gathers live changes puts them out now
        // (ADR-0142 D5). What they did to the edit is answered here, by the row the commit goes to.
        this.#source.publishGathered?.();
        this.#reconcileSelection();
        if (this.#edit !== edit) return true;
        const column = itemAt(this.#columns, edit.cell.column);
        const name = column.column.name;
        const landing = this.#landingOf(edit);
        if (landing.kind === 'discard') {
            this.#endEdit(landing.reason);
            return true;
        }
        if (landing.kind === 'refused') {
            this.#holdEdit(edit, undefined);
            this.#onRefused?.({ kind: 'commit', reason: landing.reason, cell: edit.cell, column: name });
            return false;
        }
        // Asked again, not assumed from when the editor opened: a Consumer may revoke the declaration
        // mid-edit, and `editable` means a write may land here (ADR-0035).
        const heard = this.#editHeard;
        if (!column.editable || heard === undefined) {
            this.#endEdit('columnNoLongerEditable');
            return true;
        }
        const text = edit.element.value;
        const { row } = landing;
        // The Consumer's judgement, at the one moment a veto is still possible (ADR-0034).
        const verdict: EditVerdict = column.column.validate === undefined
            ? { kind: 'accept' }
            : checkedVerdict(column.column.validate(row, text), name);
        if (verdict.kind === 'reject') {
            this.#holdEdit(edit, verdict.message);
            return false;
        }
        // The commit lands over whatever the cell paints now, and the intent says what the user saw of it
        // (ADR-0142). A flag raises the intent unchanged: the mark comes back through what the cell shows.
        this.#supersedeFill();
        const intent = new GridEditIntent(row, name, text, edit.seenText, this.#paintedTextAt(landing.cell));
        // The user's own write (D1), noted before the Consumer hears it, and taken back if refused.
        const written = this.#noteUserWrite([fromCorners(landing.cell, landing.cell)]);
        let returned = false;
        try {
            heard(intent);
            returned = true;
        } finally {
            intent.seal();
            // What the source gathered while the Consumer wrote is put out as its handler returns (D5).
            this.#source.publishGathered?.();
            this.#userWriteAnswered(written, !returned || intent.refused);
        }
        // A source or columns the Consumer handed over while it heard the intent are the commit's own doing:
        // accepted, the edit ends as committed; refused, the editor cannot stand over rows or columns the grid
        // no longer shows, and the edit is discarded with the reason (ADR-0142). Neither raises a notice: its
        // cell would name a position among rows or columns the grid no longer shows.
        const outlived = this.#outlivedBy(edit);
        const refusal = intent.refusalMessage;
        if (refusal === undefined || outlived !== undefined) {
            this.#endEdit(refusal === undefined ? undefined : outlived);
            if (refusal === undefined && outlived === undefined) {
                this.#raiseOverwriteNotice(landing.cell, name, intent.seenText, intent.replacedText);
            }
            return true;
        }
        this.#holdEdit(edit, refusal);
        return false;
    }

    // ---- D1: the user's own writes count as seen, for the Overwrite Notice (ADR-0142) ----------------------
    //
    // A write the grid raises for the user's own gesture is followed from the moment it is raised until it
    // settles, by the positions it covers. An editor opened over one of its cells while it has not settled
    // awaits it, and when it settles the editor's seen text becomes what the cell paints then: the change it
    // brings is the user's own. After that, any change under the editor is told. A write settles at a point
    // in the grid's order of events, never at a time and never by comparing text: once its handler has
    // returned unrefused and a new Window — another list or another start — has been taken in since it was
    // raised. A bound source is asked to put out what it gathered as the handler returns (D5), so a source
    // that publishes the Consumer's write at once settles it at once. An order move or another source lets it
    // go, as does a Window that holds none of its rows; an editor that awaited it reads the cell then too.

    // Notes a write as it is raised, before the Consumer hears it: the cells of it the Window holds, the
    // painted rows first, as far as the bound.
    #noteUserWrite(targets: readonly SelectionRange[]): UserWrite {
        const write: UserWrite = {
            source: this.#source, sequence: this.#sequence(), columnNames: this.#columnNames, cells: new Map(), cellCount: 0,
            answered: false, windowTakenIn: false,
        };
        const visible = this.#visible;
        if (visible !== undefined) this.#followUserWriteCells(targets, visible.start, visible.start + visible.count - 1, write);
        const { window, windowStart } = this.#state;
        this.#followUserWriteCells(targets, windowStart, windowStart + window.length - 1, write);
        // The values summed may move with it (ADR-0130).
        this.#askSummaryAgain();
        if (write.cellCount > 0) {
            this.#userWrites.push(write);
            if (this.#userWrites.length > userWritesKept) {
                // Past the bound: no longer followed, and an editor that awaited it keeps what it saw.
                const dropped = defined(this.#userWrites.shift(), 'the oldest write followed');
                this.#edit?.awaits.delete(dropped);
            }
        }
        return write;
    }

    #followUserWriteCells(targets: readonly SelectionRange[], fromRow: number, toRow: number, write: UserWrite): void {
        const columnCount = this.#columns.length;
        for (const range of targets) {
            for (let row = Math.max(range.topRow, fromRow); row <= Math.min(bottomRow(range), toRow); row++) {
                if (this.#rowAt(row) === undefined) continue;
                let columns = write.cells.get(row);
                for (let column = Math.max(range.leftColumn, 0); column < Math.min(range.leftColumn + range.columnCount, columnCount); column++) {
                    if (write.cellCount >= userWriteCellsFollowed) return;
                    if (columns === undefined) {
                        columns = new Set();
                        write.cells.set(row, columns);
                    }
                    if (columns.has(column)) continue;
                    columns.add(column);
                    write.cellCount += 1;
                }
            }
        }
    }

    // The unsettled writes of the user's own over a cell an editor opens on, under the source, the order and
    // the columns now.
    #userWritesOver(at: CellPosition): Set<UserWrite> {
        const sequence = this.#sequence();
        return new Set(this.#userWrites.filter(write => write.source === this.#source && write.sequence === sequence
            && sameItems(write.columnNames, this.#columnNames) && write.cells.get(at.row)?.has(at.column) === true));
    }

    // A write's handler is done (D1, D5), after the source put out what it gathered. One refused, or whose
    // handler threw, is taken back: nothing says it wrote. An accepted one settles now if a new Window was
    // taken in since it was raised, and otherwise with the first one taken in after this.
    #userWriteAnswered(write: UserWrite, refused: boolean): void {
        const kept = this.#userWrites.indexOf(write);
        if (refused) {
            if (kept >= 0) this.#userWrites.splice(kept, 1);
            this.#edit?.awaits.delete(write);
            return;
        }
        write.answered = true;
        if (!write.windowTakenIn || kept < 0) return;
        this.#userWrites.splice(kept, 1);
        this.#settled(write);
    }

    // The user's own writes against the Window just taken in (D1). Under another source or another order a
    // write's positions name other rows, and it goes. A new Window lets go of the cells whose rows it no
    // longer holds, and is the one a write waits for: one whose handler has returned settles. The same
    // Window again settles nothing.
    #userWritesTakenIn(previous: readonly Row[], previousStart: number): void {
        if (this.#userWrites.length === 0) return;
        const { window, windowStart } = this.#state;
        const newWindow = previous !== window || previousStart !== windowStart;
        const sequence = this.#sequence();
        for (let i = this.#userWrites.length - 1; i >= 0; i--) {
            const write = itemAt(this.#userWrites, i);
            let goes: boolean;
            if (write.source !== this.#source || write.sequence !== sequence) {
                goes = true;
            } else if (!newWindow) {
                goes = false;
            } else {
                for (const [row, columns] of write.cells) {
                    if (this.#rowAt(row) !== undefined) continue;
                    write.cells.delete(row);
                    write.cellCount -= columns.size;
                }
                write.windowTakenIn = true;
                goes = write.answered || write.cellCount === 0;
            }
            if (!goes) continue;
            this.#userWrites.splice(i, 1);
            this.#settled(write);
        }
    }

    // A write settled or went: an editor that awaited it reads its cell again, once it has followed its row.
    #settled(write: UserWrite): void {
        const edit = this.#edit;
        if (edit === undefined || !edit.awaits.delete(write)) return;
        edit.rebaseOwed = true;
        this.#invalidate();
    }

    // The editor's seen text moves to what its cell paints now, read from the edited row only: by its Row
    // Key, or by its position while the order it opened under holds. While the Window does not hold that
    // row, the editor keeps what it saw before, never another row's text; the commit may then tell the user's
    // own write as a change, which errs towards a notice (ADR-0142's accepted limits).
    #rebaseEdit(edit: OpenEdit<Row>): void {
        edit.rebaseOwed = false;
        const held = this.#rowAt(edit.cell.row);
        if (held === undefined) return;
        const rowKey = this.#source.rowKey;
        const own = edit.key === undefined
            ? this.#sequence() === edit.sequence
            : rowKey !== undefined && keyOf(rowKey, held.row, 'A row of the Window') === edit.key;
        if (own) edit.seenText = this.#paintedTextAt(edit.cell);
    }

    // The Overwrite Notice a landed commit owes (ADR-0142, LV-11): exactly when what the user saw and what
    // the commit replaced differ.
    #raiseOverwriteNotice(cell: CellPosition, column: string, seenText: string, replacedText: string): void {
        if (seenText === replacedText || this.#destroyed) return;
        this.#onOverwriteNotice?.({ cell, column, seenText, replacedText });
    }

    // Where a commit lands (ADR-0142, LV-20): on the row the editor opened on, by its Row Key wherever the
    // Window holds it now, or, without one, at the editor's position while the order it opened under holds.
    // Without a Row Key, a row that left the Window under that order takes the typing with it (ED-21).
    #landingOf(edit: OpenEdit<Row>):
        | { readonly kind: 'row'; readonly row: Row; readonly cell: CellPosition }
        | { readonly kind: 'refused'; readonly reason: 'rowLeftTheWindow' | 'orderMoved' }
        | { readonly kind: 'discard'; readonly reason: EditDiscardReason } {
        if (edit.key !== undefined) {
            const at = this.#positionOfKey(edit.key, edit.cell.row);
            if (at === undefined) return { kind: 'refused', reason: 'rowLeftTheWindow' };
            const cell = cellPosition(at, edit.cell.column);
            return { kind: 'row', row: defined(this.#rowAt(at), 'the row a key was found at').row, cell };
        }
        if (this.#sequence() !== edit.sequence) return { kind: 'refused', reason: 'orderMoved' };
        const held = this.#rowAt(edit.cell.row);
        return held === undefined ? { kind: 'discard', reason: 'rowLeftTheWindow' } : { kind: 'row', row: held.row, cell: edit.cell };
    }

    // Where the Window holds the row with `key` now, looked for first where it stood: a row met under its key
    // more than once cannot be told apart from another, and is not followed (ADR-0140).
    #positionOfKey(key: RowKey, near: number): number | undefined {
        const rowKey = this.#source.rowKey;
        if (rowKey === undefined) return undefined;
        const { window, windowStart } = this.#state;
        const slice = near - windowStart;
        if (slice >= 0 && slice < window.length && keyOf(rowKey, itemAt(window, slice), 'A row of the Window') === key) return near;
        let found: number | undefined;
        for (let i = 0; i < window.length; i++) {
            if (keyOf(rowKey, itemAt(window, i), 'A row of the Window') !== key) continue;
            if (found !== undefined) return undefined;
            found = windowStart + i;
        }
        return found;
    }

    // What took the open edit's rows or columns away: another source, or other columns (ADR-0011, ADR-0142).
    #outlivedBy(edit: OpenEdit<Row>): EditDiscardReason | undefined {
        if (edit.source !== this.#source) return 'sourceChanged';
        // The columns in force, not as last reconciled: a handler may have handed over others just now.
        if (!sameItems(edit.columnNames, this.#columns.map(({ column: { name } }) => name))) return 'columnsChanged';
        return undefined;
    }

    // Answers what the rows and columns did to an open edit, as the Selection is answered (ADR-0011): taken
    // away with another source or other columns, and, with a Row Key, followed to where its row stands now,
    // the Focus with it, while the Selection the order move dropped stays dropped. The grid does not scroll
    // to follow it; a row the Window no longer holds leaves the editor where it is.
    #reconcileEdit(): void {
        const edit = this.#edit;
        if (edit === undefined) return;
        const outlived = this.#outlivedBy(edit);
        if (outlived !== undefined) {
            this.#endEdit(outlived);
            return;
        }
        if (edit.key !== undefined) {
            const at = this.#positionOfKey(edit.key, edit.cell.row);
            if (at !== undefined) {
                edit.cell = cellPosition(at, edit.cell.column);
                const focus = this.#held.selection.focus;
                if (focus === undefined || !sameCell(focus, edit.cell)) this.#apply(Selection.empty.click(edit.cell, this.#extent));
            }
        }
        if (edit.rebaseOwed) this.#rebaseEdit(edit);
    }

    // Holds the editor (ADR-0034, ADR-0142): the typing stays, the keyboard stays in it, and the Consumer's
    // sentence, where there is one, is shown at it and said in the live region. Escape is the way out.
    #holdEdit(edit: OpenEdit<Row>, message: string | undefined): void {
        edit.error = message;
        this.#editorFocusOwed = true;
        if (message !== undefined) this.#sayRefusal(message);
        this.#invalidate();
    }

    // Ctrl+Enter fills the Selection with the editor's text (ADR-0007, ADR-0035): one paste intent carrying
    // every cell, judged by the paste's gate over the whole Selection, not the one cell the editor opened on.
    // A refusal judges the operation and never the text, so the editor stays with the typing in it.
    #commitFill(edit: OpenEdit<Row>): void {
        this.#source.publishGathered?.();
        this.#reconcileSelection();
        if (this.#edit !== edit) return;
        // A fill is positional: once the order has moved since the editor opened, the Selection it was aimed
        // with no longer names those rows (ADR-0142; ADR-0011's note of 2026-10-07).
        if (this.#sequence() !== edit.sequence) {
            this.#refuse({ kind: 'paste', reason: 'orderMoved' });
            return;
        }
        const heard = this.#pasteHeard;
        const columns = this.#columns;
        const selection = this.#held.selection.isEmpty ? Selection.empty.click(edit.cell, this.#extent) : this.#held.selection;
        const decision = planPaste(selection, pasteShape(1, 1),
            column => heard !== undefined && column >= 0 && column < columns.length && itemAt(columns, column).editable);
        if (decision.refused) {
            this.#refuse({ kind: 'paste', reason: decision.reason }, decision.reason === 'targetNotEditable' ? decision.column : undefined);
            return;
        }
        const text = edit.element.value;
        // The verdict judges the value, once, against the row the editor opened on, as the Window holds it now
        // or, gone from it, as it opened: a fill's targets may lie outside the Window, with no row to pass for
        // most of them (ADR-0034's fill carve-out), and a fill is never left unjudged.
        const column = itemAt(columns, edit.cell.column);
        const held = this.#rowAt(edit.cell.row);
        const judged = held === undefined ? edit.openedOn : held.row;
        const verdict: EditVerdict = column.column.validate === undefined
            ? { kind: 'accept' }
            : checkedVerdict(column.column.validate(judged, text), column.column.name);
        if (verdict.kind === 'reject') {
            this.#holdEdit(edit, verdict.message);
            return;
        }
        this.#supersedeFill();
        const intent = new GridPasteIntent(decision.plan, [[text]], [['shownText']], this.#handedVersion(), undefined, edit.cell);
        // The fill lands over its target as entered (ADR-0142, LV-13). The edited cell lay under the editor: a
        // change there is told once the fill lands (LV-11).
        const seen = edit.seenText;
        const replaced = this.#paintedTextAt(edit.cell);
        const written = this.#noteUserWrite(decision.plan.targets);
        // The editor is down before the Consumer hears the fill, so whatever it hands over meanwhile discards
        // nothing: it is the fill's own doing (ADR-0142), and no notice follows another source or other columns.
        this.#endEdit(undefined, () => {
            let returned = false;
            try {
                defined(heard, 'the paste\'s listener')(intent);
                returned = true;
            } finally {
                intent.seal();
                this.#source.publishGathered?.();
                this.#userWriteAnswered(written, !returned || intent.refused);
            }
        });
        if (this.#outlivedBy(edit) === undefined && !intent.refused) this.#raiseOverwriteNotice(edit.cell, column.column.name, seen, replaced);
    }

    /**
     * Ends the edit (ADR-0010). What it ended in — a discard's reason, a fill's intent — reaches the Consumer
     * before the end itself, so a command the Consumer runs on hearing the end comes after the value (ADR-0050
     * section 6). The keyboard comes back to the grid where the editor held it.
     */
    #endEdit(discarded: EditDiscardReason | undefined, endedIn?: () => void): void {
        const edit = this.#edit;
        if (edit === undefined) return;
        this.#edit = undefined;
        this.#editorFocusOwed = false;
        const element = edit.element;
        const doc = this.#root.ownerDocument;
        // Handed back before the editor goes, so DOM focus never falls to the page between the two; from the
        // Formula Bar too, whose text then reads the Focus cell's again (ADR-0051).
        if (doc.activeElement === element || doc.activeElement === this.#bar.text) this.#takeKeyboard();
        element.remove();
        this.#invalidate();
        if (endedIn !== undefined) endedIn();
        if (discarded !== undefined) this.#onEditDiscarded?.(discarded);
        this.#tellEditing();
    }

    // ---- The Formula Bar (ADR-0051) ---------------------------------------------------------------------------

    /** A key press in the grid's canonical form (ADR-0012), as this grid reads its own keys. */
    keyName(event: KeyboardEvent): string {
        return canonicalKey({
            key: event.key, ctrl: event.ctrlKey, shift: event.shiftKey, alt: event.altKey, meta: event.metaKey, metaIsPrimary: this.#metaIsPrimary,
        });
    }

    /** The view as the grid holds it now (ADR-0331): the Selection with the version it is written in, and the
     * offsets the grid last knew. */
    readView(): GridViewRead {
        if (this.#destroyed) throw new Error('This grid was destroyed.');
        this.#reconcileSelection();
        const selection = this.#held.selection;
        const view: GridViewRead = Object.freeze({
            rowSequenceVersion: this.#handedVersion(), selection, scrollTopPx: this.#scrollTopPx, scrollLeftPx: this.#scrollLeftPx,
        });
        this.#viewsRead.set(view, selection);
        return view;
    }

    /**
     * Shows `view`, which this grid read, once the grid takes rows under `rowSequenceVersion`, the source's own, as
     * a find request names it, since a Consumer knows it before the grid takes the rows (ADR-0331): its Selection in place of the empty one a new version leaves (ADR-0011),
     * and its offsets. A Consumer that swaps the rows the grid shows hands the view of the rows it brings, under the
     * version it brings them with, as it brings them: the grid takes both in its next pass. A view
     * handed later replaces one not yet taken; one whose version the grid never takes is never shown, and a Selection
     * that does not fit the rows then shown is dropped, as a new version drops any, while the offsets still come back.
     */
    showViewWith(view: GridViewRead, rowSequenceVersion: number): void {
        if (this.#destroyed) throw new Error('This grid was destroyed.');
        const selection = this.#viewsRead.get(view);
        if (selection === undefined) throw new TypeError('A view handed back is one this grid read with readView (ADR-0331).');
        if (!Number.isInteger(rowSequenceVersion)) throw new TypeError('rowSequenceVersion is a whole number.');
        this.#viewAhead = { selection, version: rowSequenceVersion, topPx: view.scrollTopPx, leftPx: view.scrollLeftPx };
        this.#invalidate();
    }

    /** Hands the keyboard to the grid: to the field an open edit is typed in, else to where the grid takes keys
     * (ADR-0080). For a Consumer's own control beside the grid, such as a Sheet's tabs, that gives it back. */
    focus(): void {
        if (this.#destroyed) throw new Error('This grid was destroyed.');
        const edit = this.#edit;
        if (edit === undefined) {
            this.#takeKeyboard();
            return;
        }
        const surface = edit.surface === 'bar' ? this.#bar.text : edit.element;
        if (surface.isConnected) surface.focus({ preventScroll: true });
    }

    // A press or a key into the bar's text opens an edit on the Focus there, in Caret, its caret where the press
    // put it; an edit standing in the cell moves to the bar, in Caret (ADR-0051).
    readonly #onBarFocused = (): void => {
        const edit = this.#edit;
        if (edit !== undefined) {
            // Only a move from the cell is a new surface; the grid giving the bar its keyboard back keeps the mode
            // the user chose there with F2.
            if (edit.surface !== 'bar') {
                edit.surface = 'bar';
                edit.mode = 'caret';
            }
            return;
        }
        // The bar holds the keyboard now: a menu standing closes without taking it back (ADR-0050, item 16).
        this.#closePopover(false);
        this.#reconcileSelection();
        if (this.#discardedAim()) return;
        const focus = this.#held.selection.focus;
        if (focus === undefined || !this.#isEditableCell(focus) || this.#bar.text.readOnly) return;
        this.#startEdit(focus, 'caret', this.#openingTextAt(focus), 'bar');
    };

    // Typed in the bar: the one uncommitted text, shown over the cell as well (ADR-0051).
    readonly #onBarTyped = (text: string): void => {
        if (this.#edit === undefined) this.#onBarFocused();
        const edit = this.#edit;
        // No edit could open, though the last pass left the field writable (the column, the Selection or the rows
        // moved since): what was typed goes, and the next pass shows the Focus's text again, as the C# core does.
        if (edit === undefined) {
            this.#invalidate();
            return;
        }
        edit.element.value = text;
    };

    // Typed in the cell: shown in the bar as well (ADR-0051).
    readonly #onEditorTyped = (): void => {
        const edit = this.#edit;
        if (edit !== undefined && this.#formulaBarDeclared.shown) this.#bar.text.value = edit.element.value;
    };

    // A press back into the cell's editor: the edit is typed there again.
    readonly #onEditorFocused = (): void => {
        const edit = this.#edit;
        if (edit !== undefined) edit.surface = 'cell';
    };

    // A press into the Name Box commits an edit standing, in place, as Excel's does; one a Reject or a refusal holds
    // takes the keyboard back to where it was typed (ADR-0051, ADR-0034).
    readonly #onNameBoxFocused = (): void => {
        const edit = this.#edit;
        if (edit === undefined || this.#commitEdit()) return;
        const surface = edit.surface === 'bar' ? this.#bar.text : edit.element;
        if (surface.isConnected) surface.focus({ preventScroll: true });
    };

    // Enter in the Name Box: the keyboard is the grid's again, and the Consumer resolves what was typed and places
    // the Selection (ADR-0051; ADR-0050, item 4).
    readonly #onNameBoxEntered = (typed: string): void => {
        this.#takeKeyboard();
        this.#answered += 1;
        this.#invalidate();
        this.#formulaBarDeclared.onNameBoxEntered?.(typed);
    };

    // Escape in the Name Box: what was typed goes, and the keyboard is the grid's again.
    readonly #onNameBoxLeft = (): void => {
        this.#takeKeyboard();
        this.#answered += 1;
        this.#invalidate();
    };

    // The bar shows the Focus: the Name Box names it in the Consumer's words, and the text field holds the text an
    // edit would open on, the Entry on a Sheet, or, while an edit stands, its uncommitted text (ADR-0051). A cell
    // whose own text is empty may show the Consumer's borrowed text, dimmed and read only (ADR-0125).
    #paintFormulaBar(): void {
        const declared = this.#formulaBarDeclared;
        const bar = this.#bar;
        if (!declared.shown) {
            // A band taken away while it holds the keyboard hands it back first: to the editor, where an edit was
            // typed in the bar, or to the grid, so the next key is not the page's.
            if (bar.band.contains(this.#root.ownerDocument.activeElement)) {
                const edit = this.#edit;
                if (edit !== undefined) {
                    edit.surface = 'cell';
                    edit.element.focus({ preventScroll: true });
                } else {
                    this.#takeKeyboard();
                }
            }
            bar.band.remove();
            return;
        }
        if (bar.band.parentNode !== this.#root) this.#scroller.after(bar.band);
        const edit = this.#edit;
        const read = this.#barReading();
        bar.paint({
            heightPx: this.#barPx,
            widthPx: this.#width === 'stretch' ? undefined : this.#width,
            nameBoxWidthPx: this.#metrics.nameBoxWidthPx,
            nameBox: read.named,
            text: edit === undefined ? read.text : edit.element.value,
            borrowed: edit === undefined && read.borrowed,
            editable: edit !== undefined || read.editable,
            nameBoxLabel: read.nameBoxLabel,
            textLabel: read.textLabel,
        });
    }

    // What the bar reads of the Focus, asked of the Consumer again only when the Focus, its row's instance or a
    // declaration it is read through changed: a scroll's passes ask nothing (ADR-0003).
    #barReading(): BarReading<Row> {
        const declared = this.#formulaBarDeclared;
        const focus = this.#held.selection.focus;
        const held = focus === undefined ? undefined : this.#rowAt(focus.row);
        const last = this.#barRead;
        if (last !== undefined && last.declared === declared && last.cellPaint === this.#cellPaint && last.columns === this.#columns
            && last.commandLabel === this.#commandLabel && last.editHeard === this.#editHeard
            && last.placeholder === this.#placeholderMode && last.row === held?.row
            && (focus === undefined ? last.focus === undefined : last.focus !== undefined && sameCell(last.focus, focus))) {
            return last;
        }
        let text = '';
        let borrowed = false;
        if (focus !== undefined && held !== undefined) {
            // Borrowed text, where the Consumer lends any, wins over the cell's own, as the C# core's does: a spilled
            // cell paints its Value, and the bar shows the Anchor's Formula in its place (ADR-0125).
            const borrow = declared.borrowedText;
            const lent: unknown = borrow?.(held.row, itemAt(this.#columns, focus.column).column);
            if (lent !== undefined && typeof lent !== 'string') throw new TypeError(`formulaBarBorrowedText answers text or undefined, not ${typeof lent}.`);
            if (lent === undefined) {
                text = this.#openingTextAt(focus);
            } else {
                text = lent;
                borrowed = true;
            }
        }
        const label = declared.nameBoxLabel;
        const named: unknown = focus === undefined || label === undefined ? '' : label(focus);
        if (typeof named !== 'string') throw new TypeError(`nameBoxLabel answers the Name Box's text, not ${typeof named} (ADR-0051).`);
        const read: BarReading<Row> = {
            focus, row: held?.row, declared, cellPaint: this.#cellPaint, columns: this.#columns, commandLabel: this.#commandLabel,
            editHeard: this.#editHeard, placeholder: this.#placeholderMode, text, borrowed, named,
            editable: focus !== undefined && held !== undefined && !borrowed && this.#isEditableCell(focus),
            nameBoxLabel: this.#labelOf(gridLabelIds.nameBox),
            textLabel: this.#labelOf(gridLabelIds.formulaBar),
        };
        this.#barRead = read;
        return read;
    }

    // The Consumer hears whether an edit is open when that is not what it last heard (ADR-0050 section 6).
    #tellEditing(): void {
        const open = this.#edit !== undefined;
        if (open === this.#heardOpen) return;
        this.#heardOpen = open;
        this.#onEditingChanged?.(open);
    }

    // ---- Delete and the fill keys (ADR-0054, ADR-0035) -------------------------------------------------------

    // A write raised while a fill's rows are still asked for comes after the fill in the user's order, so the
    // fill, which would land later over it, is given up and said to be (the spine's sixth rule): the newer
    // write stands whatever the source's round trip took.
    #supersedeFill(): void {
        const fill = this.#fill;
        if (fill === undefined) return;
        fill.abort();
        this.#fill = undefined;
        this.#refuse({ kind: 'paste', reason: 'cancelled' });
    }

    // Whether a write key is the grid's: someone hears the write, and some column takes one.
    #writes(heard: ((intent: never) => void) | undefined): boolean {
        return heard !== undefined && this.#columns.some(({ editable }) => editable);
    }

    // Whether a write heard by `heard` may land in a column: declared Editable, and heard (ADR-0035).
    #columnTakes(column: number, heard: unknown): boolean {
        const columns = this.#columns;
        return heard !== undefined && column >= 0 && column < columns.length && itemAt(columns, column).editable;
    }

    // The Selection a write key is aimed at, judged on the newest rows the source holds (ADR-0142 D5). One the
    // rows or the source took away since it was painted refuses the write aloud, never writing over rows the
    // user did not select (ADR-0011); with nothing selected the key only places the Focus, as any first key
    // does (ADR-0012). `undefined` once answered.
    #writeTarget(): Selection | undefined {
        this.#source.publishGathered?.();
        this.#reconcileSelection();
        const dropped = this.#droppedAim;
        if (dropped !== undefined) {
            this.#refuse({ kind: 'paste', reason: dropped.reason === 'sourceChanged' ? 'sourceChanged' : 'orderMoved' });
            return undefined;
        }
        const selection = this.#held.selection;
        if (!selection.isEmpty) return selection;
        const start = this.#firstVisibleCell();
        if (start !== undefined) this.#apply(selection.click(start, this.#extent));
        return undefined;
    }

    // Delete (ADR-0054): one Clear Intent over the whole Selection, judged by the paste's gate as a 1×1 block,
    // so only the empty Selection and the Editable declaration can refuse it, in ADR-0035's order. No verdict
    // runs: there is no value to judge.
    #clear(): void {
        const target = this.#writeTarget();
        const heard = this.#clearHeard;
        if (target === undefined || heard === undefined) return;
        const decision = planPaste(target, pasteShape(1, 1), column => this.#columnTakes(column, heard));
        if (decision.refused) {
            this.#refuse({ kind: 'paste', reason: decision.reason }, decision.reason === 'targetNotEditable' ? decision.column : undefined);
            return;
        }
        this.#supersedeFill();
        const written = this.#noteUserWrite(decision.plan.targets);
        let returned = false;
        try {
            heard(new GridClearIntent(decision.plan.targets, this.#handedVersion()));
            returned = true;
        } finally {
            // What the source gathered while the Consumer wrote is put out as its handler returns (D5).
            this.#source.publishGathered?.();
            this.#userWriteAnswered(written, !returned);
        }
    }

    // Ctrl+D and Ctrl+R (ADR-0035): the source row or column read in its raw, locale-free form, never the
    // displayed text, which may be rounded or ####, and raised as one paste intent. Rows the Window holds are
    // read from it at once; the rest are asked of the source, and anything short of the whole source refuses
    // the fill rather than filling from a guess.
    #fillFromKey(direction: 'down' | 'right'): void {
        const target = this.#writeTarget();
        const heard = this.#pasteHeard;
        if (target === undefined || heard === undefined) return;
        const decision = planFill(target, direction, column => this.#columnTakes(column, heard), { cellCap: this.#copyCellCap });
        if (decision.refused) {
            this.#refuse({ kind: 'paste', reason: decision.reason }, decision.reason === 'targetNotEditable' ? decision.column : undefined);
            return;
        }
        const { plan } = decision;
        const { topRow, rowCount } = plan.source;
        const { window, windowStart } = this.#state;
        this.#supersedeFill();
        if (topRow >= windowStart && topRow + rowCount <= windowStart + window.length) {
            this.#raiseFill(plan, window.slice(topRow - windowStart, topRow - windowStart + rowCount), heard);
            return;
        }
        const fill = new AbortController();
        this.#fill = fill;
        const source = this.#source;
        const columns = this.#columns;
        const version = this.#sequence();
        // oxlint-disable-next-line ex-grid/no-catch -- an aborted fill's own rejection is expected and refuses nothing; any other failure is said and rethrown
        void source.rows({ start: topRow, count: rowCount }, fill.signal).then(rows => {
            if (fill.signal.aborted) return;
            this.#fill = undefined;
            // A source replaced while the rows were asked for: they were read from the source the key was aimed
            // at, and its positions are another source's rows now (ADR-0142).
            if (source !== this.#source) {
                this.#refuse({ kind: 'paste', reason: 'sourceChanged' });
                return;
            }
            // An order or columns that moved while the rows were asked for make both the rows and the positions
            // someone else's (ADR-0011); a run that came back short is not the whole source.
            if (rows.length < rowCount || version !== this.#sequence() || columns !== this.#columns) {
                this.#refuse({ kind: 'paste', reason: 'sourceUnavailable' });
                return;
            }
            // Nobody hears a paste any more: the fill has nowhere to go, and says so.
            const now = this.#pasteHeard;
            if (now === undefined) this.#refuse({ kind: 'paste', reason: 'cancelled' });
            else this.#raiseFill(plan, rows, now);
        }, (error: unknown) => {
            if (fill.signal.aborted) return;
            this.#fill = undefined;
            // Said, and the failure itself left to surface: the refusal is the user's, the error the Consumer's.
            this.#refuse({ kind: 'paste', reason: 'sourceUnavailable' });
            throw error;
        });
    }

    // The fill's one paste intent, its block read from the source's rows as each cell's raw value.
    #raiseFill(plan: FillPlan, rows: readonly Row[], heard: (intent: GridPasteIntent) => void): void {
        const { source } = plan;
        const columns = this.#columns;
        const values = rows.map(row => Array.from({ length: source.columnCount },
            (_, offset) => rawText(itemAt(columns, source.leftColumn + offset).column.value(row))));
        // Raw values are written in the invariant notation, and read so (ADR-0050, item 10): never shown text,
        // which a Consumer reads under its own culture, `1234.5` becoming 12345 under de-DE.
        const intent = new GridPasteIntent(plan.paste, values, values.map(row => row.map(() => 'invariant' as const)),
            this.#handedVersion(), undefined, undefined, source);
        const written = this.#noteUserWrite(plan.paste.targets);
        let returned = false;
        try {
            heard(intent);
            returned = true;
        } finally {
            intent.seal();
            this.#source.publishGathered?.();
            this.#userWriteAnswered(written, !returned || intent.refused);
        }
        this.#invalidate();
    }

    // ---- The Context Menu (ADR-0036, ADR-0039, ADR-0040) -------------------------------------------------

    // A secondary click on the rows. Outside the Selection it collapses it onto the cell it lands on, so what a
    // command will act on is what the user can see; inside it, the Selection stands. The browser's own menu
    // never appears over the rows. A press past an open editor commits it first; a Reject or a refusal holds
    // it, and the click opens nothing (ADR-0010, ADR-0034).
    readonly #onContextMenu = (event: MouseEvent): void => {
        if (event.target !== this.#viewport) return;
        event.preventDefault();
        this.#answered += 1;
        this.#invalidate();
        if (!this.#commitEdit()) return;
        this.#reconcileSelection();
        const at = this.#cellUnder(event);
        const topPx = this.#viewportYOf(event);
        if (at === undefined || topPx === undefined) {
            this.#closePopover(true);
            return;
        }
        this.#tabReleased = false;
        const selection = this.#held.selection;
        if (!selection.contains(at)) this.#apply(selection.click(at, this.#extent));
        this.#openContextMenu(at, event.offsetX - this.#scrollLeftPx, topPx);
    };

    // The Context Menu from the keyboard, on the Focus, which is inside the Selection by construction, so
    // nothing about the Selection changes. It opens at the Focus cell's corner, where the user's attention
    // already is (ADR-0036).
    #openContextMenuAtFocus(): void {
        this.#reconcileSelection();
        const focus = this.#held.selection.focus;
        const geometry = this.#geometry;
        if (focus === undefined || geometry === undefined) return;
        this.#openContextMenu(focus, this.#cellLeftPx(focus.column), geometry.viewportTopPxOf(focus.row, this.#scrollTopPx));
    }

    // A cell's left edge in the Viewport's readable box, as the C# core's CellCorner reads it: a Pinned
    // Column's stands still, any other's moves with the scroll.
    #cellLeftPx(column: number): number {
        const columns = this.#styles.geometry;
        return columns.offsetPxOf(column) - (column < columns.pinnedCount ? 0 : this.#scrollLeftPx);
    }

    // Opens the Context Menu on `cell`, at (leftPx, topPx) in the Viewport's readable box: the core's commands,
    // then the Consumer's, asked for now. Its first enabled item takes the keyboard (ADR-0039). A cell whose
    // row the Window does not hold opens nothing: there is no row to hand the commands.
    #openContextMenu(cell: CellPosition, leftPx: number, topPx: number): void {
        const { window, windowStart } = this.#state;
        const slice = cell.row - windowStart;
        if (slice < 0 || slice >= window.length || cell.column >= this.#columns.length) return;
        const canCopy = !this.#held.selection.isEmpty;
        const core: GridCommand[] = [
            coreCommand(gridCommandIds.copy, canCopy, () => { this.#menuCopy(false); }),
        ];
        if (!this.#hideCopyWithHeaders) {
            core.push(coreCommand(gridCommandIds.copyWithHeaders, canCopy, () => { this.#menuCopy(true); }));
        }
        const { column } = itemAt(this.#columns, cell.column);
        const version = this.#sequence();
        const context: GridContextMenuContext<Row> = Object.freeze({
            row: itemAt(window, slice), column: column.name, columnType: column.type,
            selection: this.#held.selection.ranges, rowSequenceVersion: this.#handedVersion(), commands: Object.freeze(core),
        });
        const commands = this.#contextCommands === undefined ? core : [...core, ...checkedCommands(this.#contextCommands(context))];
        this.#closePopover(false);
        const element = this.#root.ownerDocument.createElement('div');
        element.className = 'ex-popover';
        element.setAttribute('role', 'menu');
        this.#standMenu({ kind: 'context', element, commands, leftPx, topPx, version, source: this.#source }, element, resolveMenuKey, false);
    }

    // Stands a menu's popover: its items in `container`, inside the popover's element, placed under the root
    // after the scroller, and its first enabled item taking the keyboard (ADR-0039). `lettered` marks Excel's
    // letters, the column menu's (ADR-0044).
    #standMenu(popover: StandingPopover, container: HTMLElement, table: MenuKeyTable, lettered: boolean): void {
        const items = new MenuItems(container, popover.commands, id => this.#labelOf(id), table, {
            run: index => { this.#runCommand(index); },
            cancel: () => { this.#closePopover(true); },
            answered: this.#answer,
            toFilter: where => { this.#toFilter(where); },
        }, lettered);
        this.#popover = { ...popover, items };
        this.#placePopover();
        this.#scroller.after(popover.element);
        items.focus();
        this.#invalidate();
    }

    // An input reached a popover and was answered (ADR-0220).
    readonly #answer = (): void => {
        this.#answered += 1;
        this.#invalidate();
    };

    // What the built-in menu calls a command: the Consumer's words where it has some, the built-in English
    // otherwise, and the id for a command neither knows (ADR-0036).
    #labelOf(id: string): string {
        const words = this.#commandLabel?.(id);
        if (words !== undefined && typeof words !== 'string') {
            throw new TypeError(`commandLabel answered ${typeof words} for '${id}': it answers a command's words, or undefined for the built-in ones.`);
        }
        return words ?? builtInCommandLabel(id);
    }

    // A command chosen: the menu closes and the keyboard is back on the grid before it runs, so a command may
    // move the keyboard on itself, and the keys typed after it are the grid's (ADR-0039, ADR-0044's note of
    // 2026-10-12).
    #runCommand(index: number): void {
        const popover = this.#popover;
        if (popover === undefined || popover.kind === 'find' || popover.kind === 'consumer') return;
        const shown = itemAt(popover.commands, index);
        if (!shown.enabled) return;
        // A column's commands are asked again as the grid stands now: the column may have moved, or the pinned
        // count changed, since the menu opened, and a command runs against what is, never what was (principle
        // 6). One no longer enabled runs nothing, and the menu is drawn again as the commands now stand, so the
        // user sees why.
        let command = shown;
        if (popover.kind === 'column') {
            const column = this.#columns.findIndex(({ column: declared }) => declared.name === popover.column);
            const now = column < 0 ? undefined : this.#columnCommands(column).find(found => found.id === shown.id);
            if (now?.enabled !== true) {
                if (column < 0) this.#closePopover(true);
                else this.#openColumnMenu(column);
                return;
            }
            command = now;
        }
        this.#closePopover(true);
        command.invoke();
        this.#invalidate();
    }

    // Closes the popover standing. However the core closes it, the keyboard comes back to the grid
    // (`giveBack`, ADR-0039); a press that dismisses it keeps its own meaning, and where it lands decides.
    // Taken back only while it is still this grid's, or on nothing, as the C# core's reclaimFocus takes it:
    // a menu an order or a shrink closes after the user has put the keyboard on another grid or a field of
    // the page's must not take it from there mid-typing (ADR-0018).
    #closePopover(giveBack: boolean): void {
        const popover = this.#popover;
        if (popover === undefined) return;
        this.#popover = undefined;
        if (popover.kind === 'column') popover.panel?.close();
        // A step still out is nobody's once the panel is gone (ADR-0055).
        if (popover.kind === 'find') {
            this.#findStepping?.abort();
            this.#findStepping = undefined;
        }
        const doc = this.#root.ownerDocument;
        const active = doc.activeElement;
        const held = popover.element.contains(active);
        const ours = active === null || active === doc.body || active === doc.documentElement || this.#root.contains(active);
        popover.element.remove();
        if (held || (giveBack && ours)) this.#takeKeyboard();
        this.#invalidate();
    }

    /**
     * Shows a Consumer's own contents in the grid's popover frame (ADR-0050, item 16), in place of any popover that
     * stands. The frame stands under the header band, centred across the grid and inside its box, and contents taller
     * or wider than the room scroll inside it (ADR-0040). Its first control takes the keyboard, Tab and Shift+Tab wrap
     * inside it, and Escape, a press elsewhere in
     * the instance and the box shrinking below a row close it as a Cancel; however it closes, the keyboard returns to
     * the grid (ADR-0039). Refused (false) while an edit is open, which the popover would take the keyboard from with
     * its text uncommitted. A label that names nothing is refused by name: a dialog that names nothing says nothing
     * to assistive technology (ADR-0033). The opening, a wrap and a close by the handle are each an input answered
     * (ADR-0220); a popover that stood before is closed only once the new contents are made.
     */
    openPopover(content: (context: GridPopoverContext) => HTMLElement, label: string): boolean {
        if (this.#destroyed) throw new Error('This grid was destroyed.');
        if (typeof content !== 'function') throw new TypeError("A Consumer's popover's content is a function that makes its element (ADR-0050, item 16).");
        if (typeof label !== 'string' || label.trim() === '') throw new TypeError("A Consumer's popover is named in the Consumer's words: its label is not empty (ADR-0033).");
        if (this.#edit !== undefined) return false;
        this.#standFrame(content, label, false);
        return true;
    }

    // Stands contents in the popover frame, the Consumer's or the grid's own, in place of any popover that stands.
    #standFrame(content: (context: GridPopoverContext) => HTMLElement, label: string, own: boolean): void {
        // The context speaks for this opening only: once another popover stands, its close closes nothing.
        const opening = {};
        const standing = (): ConsumerPopoverFrame | undefined => {
            const popover = this.#popover;
            return popover?.kind === 'consumer' && popover.opening === opening ? popover.frame : undefined;
        };
        const context: GridPopoverContext = Object.freeze({
            close: () => { if (standing() !== undefined) this.#closePopover(true); },
            innerPopupChanged: (open: boolean) => {
                const frame = standing();
                if (frame !== undefined) frame.innerPopupOpen = open;
            },
        });
        const contents: unknown = content(context);
        if (!(contents instanceof HTMLElement)) throw new TypeError("A Consumer's popover's content function answers the element to show (ADR-0050, item 16).");
        // The contents may have opened another popover as they were made: this one takes its place.
        this.#closePopover(false);
        const frame = new ConsumerPopoverFrame(this.#root.ownerDocument, label, contents, this.#answer);
        this.#popover = { kind: 'consumer', element: frame.element, frame, opening, own };
        this.#placePopover();
        this.#scroller.after(frame.element);
        frame.focusFirst();
        this.#answer();
    }

    /** Closes a Consumer's popover where one stands (ADR-0050, item 16); the keyboard returns to the grid. */
    closePopover(): void {
        if (this.#destroyed) throw new Error('This grid was destroyed.');
        if (this.#popover?.kind !== 'consumer' || this.#popover.own) return;
        this.#closePopover(true);
        this.#answer();
    }

    // The popover's place and bounds, from the geometry the grid already holds (ADR-0040): written again as the
    // box changes, never measured.
    #placePopover(): void {
        const popover = this.#popover;
        if (popover === undefined) return;
        if (popover.kind === 'context') {
            setAttribute(popover.element, 'style',
                this.#underBar(contextMenuStyle(popover.leftPx, this.#bandHeightPx + popover.topPx, this.#visibleWidthPx, this.#visibleHeightPx)));
            return;
        }
        // A Consumer's popover under the header, centred across the grid, as wide as its contents within the grid's
        // width (ADR-0050, item 16).
        if (popover.kind === 'consumer') {
            const widest = Math.max(0, this.#visibleWidthPx);
            setAttribute(popover.element, 'style', this.#underBar(`left: ${widest / 2}px; top: ${this.#bandHeightPx}px; transform: translateX(-50%); `
                + `width: max-content; min-width: ${Math.min(popoverMinWidthPx, widest)}px; max-width: ${widest}px; `
                + `max-height: ${this.#popoverRoomPx(popover, this.#visibleHeightPx)}px`));
            return;
        }
        // The find panel in the grid's top right, under the header, anchored by its right edge at the Viewport's
        // with the width bounds every popover is clamped with, so it never reaches past the grid's left edge
        // either (ADR-0040).
        if (popover.kind === 'find') {
            setAttribute(popover.element, 'style', this.#underBar(`left: ${Math.max(0, this.#visibleWidthPx)}px; top: ${this.#bandHeightPx}px; `
                + `transform: translateX(-100%); ${popoverAcross(0, this.#visibleWidthPx).widths}; `
                + `max-height: ${this.#popoverRoomPx(popover, this.#visibleHeightPx)}px`));
            return;
        }
        // Under its column's header, from the band's foot to the box's (ADR-0040).
        const column = this.#columns.findIndex(({ column: { name } }) => name === popover.column);
        if (column < 0) return;
        const { leftPx, widths } = popoverAcross(this.#cellLeftPx(column), this.#visibleWidthPx);
        setAttribute(popover.element, 'style',
            this.#underBar(`left: ${leftPx}px; top: ${this.#bandHeightPx}px; ${widths}; max-height: ${this.#popoverRoomPx(popover, this.#visibleHeightPx)}px`));
    }

    // How tall the popover may be in a box `visibleHeightPx` tall: the Context Menu's side of the pointer, a
    // column menu's room below the header band (ADR-0040).
    #popoverRoomPx(popover: OpenPopover, visibleHeightPx: number): number {
        return popover.kind === 'context'
            ? contextMenuRoomPx(this.#bandHeightPx + popover.topPx, visibleHeightPx)
            : Math.max(0, visibleHeightPx - this.#bandHeightPx);
    }

    // ---- The column menu (ADR-0010, ADR-0039, ADR-0044) ---------------------------------------------------

    // The ▾ is a toggle: pressed on the column whose menu stands, it closes it, as a Cancel; on another column,
    // that column's menu takes the place of whatever popover stood. A press past an open editor commits it
    // first, and one the edit holds opens nothing (ADR-0010, ADR-0034).
    #toggleColumnMenu(name: string): void {
        this.#answered += 1;
        this.#invalidate();
        if (!this.#commitEdit()) return;
        const popover = this.#popover;
        if (popover?.kind === 'column' && popover.column === name) {
            this.#closePopover(true);
            return;
        }
        const column = this.#columns.findIndex(({ column: declared }) => declared.name === name);
        if (column >= 0) this.#openColumnMenu(column);
    }

    // Alt+↓ (ADR-0039): the Focus's column's menu, opened, never toggled shut. With no Focus it does nothing.
    #openColumnMenuAtFocus(): void {
        this.#reconcileSelection();
        const focus = this.#held.selection.focus;
        if (focus === undefined || focus.column >= this.#columns.length) return;
        const popover = this.#popover;
        if (popover?.kind === 'column' && popover.column === itemAt(this.#columns, focus.column).column.name) return;
        this.#openColumnMenu(focus.column);
    }

    // The column's popover (ADR-0044), as Excel's drop-down: its commands, which the core decides (ADR-0010),
    // and below them, where the column can be filtered, its filter (ADR-0009), Tab wrapping through both by a
    // sentinel either side of the filter. Named by the column's own header, the Consumer's words, so the core
    // holds no sentence (ADR-0039): a dialog holding the menu and the filter, the menu alone where there is no
    // filter. Its first enabled command takes the keyboard.
    #openColumnMenu(column: number): void {
        const { column: declared, header, filterUi } = itemAt(this.#columns, column);
        const commands = this.#columnCommands(column);
        this.#closePopover(false);
        const doc = this.#root.ownerDocument;
        const filterable = declared.queryable !== false;
        const element = doc.createElement('div');
        element.className = 'ex-popover';
        element.setAttribute('role', filterable ? 'dialog' : 'none');
        if (filterable) element.setAttribute('aria-label', header);
        const menu = doc.createElement('div');
        menu.className = 'ex-popover-commands';
        menu.setAttribute('role', 'menu');
        menu.setAttribute('aria-label', header);
        // The letters that run a command here, enabled ones only, as the C# core writes them for its key
        // listener (ADR-0010, ADR-0044).
        menu.setAttribute('data-ex-letters', commands.filter(command => command.enabled).map(command => letterOf(command.id) ?? '').join('')); // oxlint-disable-line ex-grid/no-stand-in-default -- a command Excel gives no letter adds none
        element.append(menu);
        const name = declared.name;
        const panel = filterable ? new FilterPanel(doc, {
            name, type: declared.type, dateType: declared.dateType ?? 'dateTime', filterUi, // oxlint-disable-line ex-grid/no-stand-in-default -- a Date column's dates are dateTime unless declared
            textOf: value => textFor(declared, value),
        }, this.#filterOf(name), this.#culture, this.#temporal, signal => this.#source.distinctValues(name, signal), {
            apply: spec => { this.#applyFilter(name, spec); },
            cancel: () => { this.#closePopover(true); },
            letter: key => this.#valueListLetter(key),
            returnToCommands: () => {
                const standing = this.#popover;
                if (standing?.kind === 'column') standing.items.focusFirst();
            },
            answered: this.#answer,
            fetched: () => { this.#invalidate(); },
        }) : undefined;
        if (panel !== undefined) element.append(panel.lead, panel.body, panel.trail);
        this.#standMenu({ kind: 'column', element, commands, column: name, panel }, menu,
            (pressed, shift, chord, held, current) => resolveColumnMenuKey(pressed, shift, chord, held, current, filterable), true);
    }

    // Tab, Shift+Tab and E on a command (ADR-0044): the keyboard goes into the filter below.
    #toFilter(where: 'filterFirst' | 'filterLast' | 'filterSearch'): void {
        const popover = this.#popover;
        const panel = popover?.kind === 'column' ? popover.panel : undefined;
        if (panel === undefined) return;
        if (where === 'filterFirst') panel.focusFirst();
        else if (where === 'filterLast') panel.focusLast();
        else panel.focusSearch();
    }

    // A key on the value list (ADR-0044): Excel's letters act there as on the commands, S, O and C running their
    // command and E going back to the search box. Whether it was one.
    #valueListLetter(key: string): boolean {
        const popover = this.#popover;
        if (popover?.kind !== 'column') return false;
        const meaning = menuLetter(key, false, popover.commands, true);
        if (meaning.kind === 'run') {
            this.#runCommand(meaning.item);
            return true;
        }
        if (meaning.kind === 'filterSearch') {
            popover.panel?.focusSearch();
            return true;
        }
        return false;
    }

    // The column's spec in force, which its filter panel opens on.
    #filterOf(name: string): FilterSpec | undefined {
        const filter = this.#state.filter;
        return filter !== undefined && Object.hasOwn(filter.columns, name) ? filter.columns[name] : undefined;
    }

    // The one way a panel's answer reaches the Filter (ADR-0009): the column's spec replaced, or removed, in the
    // whole Filter, every other column's and the Opaque Filter kept; the popover closes and the keyboard comes
    // back to the grid, and the source applies it. An in with no values is refused, and the panel stands: the
    // engine would refuse it (ADR-0023, ticket 76). So is a clause whose operator the column was not offered: a
    // Chrome lays out the operators, it cannot add one (ADR-0010, FN-18).
    #applyFilter(name: string, spec: FilterSpec | undefined): void {
        const column = this.#columns.find(({ column: declared }) => declared.name === name);
        if (column === undefined || !canApply(spec)) return;
        const allowed = filterOperatorsFor(column.column.type);
        if (spec !== undefined && !spec.clauses.every(clause => allowed.includes(clause.operator))) return;
        const filter = this.#state.filter;
        // Built from entries, never by assigning a key: a column named `__proto__` is a column like any other.
        const others = filter === undefined ? [] : Object.entries(filter.columns).filter(([named]) => named !== name);
        const columns: Record<string, FilterSpec> = Object.fromEntries(spec === undefined ? others : [...others, [name, spec]]);
        const opaque = filter?.opaque;
        const next = Object.keys(columns).length === 0 && opaque === undefined
            ? undefined
            : Object.freeze({ columns: Object.freeze(columns), ...(opaque === undefined ? {} : { opaque }) });
        this.#closePopover(true);
        this.#source.setFilter(next);
        this.#invalidate();
    }

    // The column's commands (ADR-0010, ADR-0044): every one moves View State, so each ends in the source or in
    // a Consumer's callback. A command nobody hears is shown disabled rather than left out, so every Chrome lays
    // out the same list. The Sort and the Filter are the source's to apply.
    #columnCommands(column: number): readonly GridCommand[] {
        const { column: declared } = itemAt(this.#columns, column);
        const name = declared.name;
        const queryable = declared.queryable !== false;
        const geometry = this.#styles.geometry;
        const hidden = this.#onColumnHidden;
        const pinned = this.#onPinnedCountChanged;
        const widths = this.#onColumnWidthChanged;
        const filter = this.#state.filter;
        const filtered = filter !== undefined && Object.hasOwn(filter.columns, name);
        return Object.freeze([
            coreCommand(gridCommandIds.sortAscending, queryable, () => { this.#source.setSorts([{ column: name, direction: 'ascending' }]); }),
            coreCommand(gridCommandIds.sortDescending, queryable, () => { this.#source.setSorts([{ column: name, direction: 'descending' }]); }),
            coreCommand(gridCommandIds.clearFilter, queryable && filtered, () => { this.#clearFilterOf(name); }),
            coreCommand(gridCommandIds.hide, hidden !== undefined, () => { hidden?.(name); }),
            // Never a pin the grid would refuse: a boundary through a Header Group cannot be drawn honestly
            // (ADR-0032).
            coreCommand(gridCommandIds.pin, pinned !== undefined && geometry.requestedPinnedCount !== column + 1
                && this.#headerLayout.allowsPinnedCount(column + 1), () => { pinned?.(column + 1); }),
            coreCommand(gridCommandIds.unpin, pinned !== undefined && geometry.requestedPinnedCount > 0, () => { pinned?.(0); }),
            coreCommand(gridCommandIds.sizeToFit, widths !== undefined && itemAt(this.#columns, column).paintsValue, () => {
                widths?.({ column: name, widthPx: this.#fittedPx(column) });
            }),
        ]);
    }

    // Clear filter (ADR-0044): the column's own spec goes, every other column's and the Opaque Filter stay.
    #clearFilterOf(name: string): void {
        const filter = this.#state.filter;
        if (filter === undefined) return;
        const columns = Object.fromEntries(Object.entries(filter.columns).filter(([column]) => column !== name));
        const next = Object.keys(columns).length === 0 && filter.opaque === undefined
            ? undefined
            : Object.freeze({ columns: Object.freeze(columns), ...(filter.opaque === undefined ? {} : { opaque: filter.opaque }) });
        this.#source.setFilter(next);
    }

    // ---- Find and Replace (ADR-0055, ADR-0332, ADR-0333) ---------------------------------------------------

    // Whether anything can answer a Find: a source that has `find` (ADR-0055, ADR-0350 section 16).
    get #canFind(): boolean {
        return this.#source.find !== undefined;
    }

    // Whether a Replace can land (ADR-0332 §2): someone handles the Replace Request, a column is Editable, and a
    // Find is wired, which Replace's next match is.
    get #canReplace(): boolean {
        return this.#onReplace !== undefined && this.#columns.some(({ editable }) => editable) && this.#canFind;
    }

    // Ctrl+F, and Ctrl+H with `replace` (ADR-0055, ADR-0332 §2): the find panel in the place of whatever popover
    // stood, taking the keyboard as every popover does (ADR-0039); or, where nothing can search or no Replace
    // can land, a refusal and nothing opened, because the browser's own find would look complete and see only
    // the painted rows. The panel already open stays, only what it shows changing: Ctrl+F turns it back into Find
    // alone, and Ctrl+H shows Replace. Either way its field takes the keyboard with its text selected.
    #openFind(replace: boolean): void {
        if (!this.#canFind || (replace && !this.#canReplace)) {
            this.#onRefused?.({ kind: 'find', reason: replace ? 'replaceUnavailable' : 'unavailable' });
            return;
        }
        this.#findCurrent = undefined;
        this.#findCurrentHeader = undefined;
        let popover = this.#popover;
        if (popover?.kind !== 'find') {
            this.#closePopover(false);
            const panel = new FindPanel(this.#root.ownerDocument, this.#findHeld, id => this.#labelOf(id), {
                step: backward => { this.#findStep(backward); },
                replaceOne: () => { this.#replaceOne(); },
                replaceAll: () => { this.#replaceAll(); },
                heldChanged: () => { this.#invalidate(); },
                answered: this.#answer,
            });
            popover = { kind: 'find', element: panel.element, panel };
            this.#popover = popover;
            this.#placePopover();
            this.#scroller.after(panel.element);
        }
        popover.panel.showReplace(replace);
        this.#setFindOutcome({ kind: 'none' });
        popover.panel.focusField();
    }

    // What the panel holds, as a step and a mark judge by it.
    #findPattern(): GridFindPattern {
        const { text, matchCase, wholeCell } = this.#findHeld;
        return Object.freeze({ text, matchCase, wholeCell });
    }

    // What the last step or Replace came to, said in the panel's outcome line (ADR-0033): a step that moves
    // nothing is otherwise a key that did nothing and said nothing.
    #setFindOutcome(outcome: FindOutcome): void {
        this.#findOutcome = outcome;
        const popover = this.#popover;
        if (popover?.kind === 'find') popover.panel.say(findOutcomeText(outcome, id => this.#labelOf(id)));
        this.#invalidate();
    }

    // A step that moved nothing (ADR-0055): the panel says why, and the Consumer is told.
    #refuseFind(reason: 'notFound' | 'orderChanged' | 'sourceChanged'): void {
        this.#setFindOutcome({ kind: reason });
        this.#onRefused?.({ kind: 'find', reason });
    }

    /**
     * One Find step (ADR-0055): asked of the source with the Focus to start from, the Selection as the scope
     * when it is more than one cell, the columns in their current order and the source's version they were read
     * in. The headers come first in the walk where they are searched (ADR-0333 §3), and the grid holds them, so
     * a header the step reaches before any row is landed on without asking. An answer under another version, or
     * from a source since replaced, moves nothing (ADR-0011, ADR-0142); a found cell becomes the Focus and is
     * revealed. A new step, or the panel closing, makes the answer of the one still out nobody's.
     */
    #findStep(backward: boolean): void {
        const source = this.#source;
        if (this.#popover?.kind !== 'find' || this.#findHeld.text === '' || source.find === undefined) return;
        this.#findStepping?.abort();
        this.#findStepping = undefined;
        this.#reconcileSelection();
        const selection = this.#held.selection;
        const scope = !selection.isEmpty && selection.cellCount > 1 ? selection.ranges : undefined;
        // The headers this step may stop on, read now and carried with it; a Selection as the scope searches
        // none. The header it starts from, when the current match is one.
        const headers = scope === undefined ? this.#findHeaders() : [];
        const fromHeader = this.#currentHeaderIn(headers);
        const pattern = this.#findPattern();
        // No match is current until this step finds one (ADR-0332 §4).
        this.#findCurrent = undefined;
        this.#findCurrentHeader = undefined;
        const before = headerBeforeAsking(matchingHeaders(headers, pattern), fromHeader, !selection.isEmpty, backward);
        if (before !== undefined) {
            this.#landOnHeader(itemAt(headers, before));
            return;
        }
        // The version is the source's own, which its answer is read against: the grid's goes on past a change
        // of source, which the source's identity tells apart.
        const request: GridFindRequest = Object.freeze({
            ...pattern, backward,
            // From a header the rows are searched from their start (their end, backward): the header rows lie
            // before the first data row (ADR-0333 §3).
            from: selection.isEmpty || fromHeader !== undefined ? undefined : selection.focus,
            scope, columns: Object.freeze(this.#columns.map(({ column }) => column.name)),
            rowSequenceVersion: this.#state.rowSequenceVersion,
        });
        const stepping = new AbortController();
        this.#findStepping = stepping;
        this.#invalidate();
        // oxlint-disable-next-line ex-grid/no-catch -- a step given up by whoever answers moves nothing and says so; any other failure is rethrown
        source.find(request, stepping.signal).then(answer => {
            if (this.#destroyed || this.#findStepping !== stepping) return;
            this.#findStepping = undefined;
            // The grid stands settled again whatever the answer comes to, a defect named included.
            this.#invalidate();
            this.#findAnswered(request, answer, source, selection, headers);
        }, (error: unknown) => {
            if (this.#destroyed || this.#findStepping !== stepping) return;
            this.#findStepping = undefined;
            this.#setFindOutcome({ kind: 'none' });
            // Given up by whoever answers, who says why itself: nothing moves, and no "No match" is claimed for
            // cells nobody read (ADR-0055, ADR-0025).
            if (error instanceof DOMException && error.name === 'AbortError') return;
            throw error;
        });
    }

    // A step's answer, judged against what it was asked under (ADR-0055, ADR-0333 §3).
    #findAnswered(request: GridFindRequest, answer: GridFindResult, source: GridSource<Row>, selection: Selection,
        headers: readonly FindHeader[]): void {
        // Another source was handed over while the question was out: the answer, found or not, is about the
        // rows of a source the grid no longer shows (ADR-0011, ADR-0142).
        if (source !== this.#source) {
            this.#refuseFind('sourceChanged');
            return;
        }
        // The order moved while the question was out: the answer is about positions that no longer stand.
        if (request.rowSequenceVersion !== this.#state.rowSequenceVersion) {
            this.#refuseFind('orderChanged');
            return;
        }
        // An answer outside the question is the source's defect, named rather than reworded as a reorder: a
        // column it was not asked about, or a row the result does not have.
        const given: unknown = answer;
        if (typeof given !== 'object' || given === null || typeof (given as { readonly found?: unknown }).found !== 'boolean') {
            throw new TypeError('The source\'s find answered something other than a Find result: { found: true, row, column } or '
                + '{ found: false } (ADR-0055).');
        }
        const total = this.#totalRows;
        if (answer.found && (!request.columns.includes(answer.column) || !Number.isInteger(answer.row) || answer.row < 0 || answer.row >= total)) {
            throw new RangeError(`The Find answer (row ${answer.row}, column '${answer.column}') is outside the request: it asked about `
                + `the columns [${request.columns.join(', ')}] of a result of ${total} rows (ADR-0055).`);
        }
        // Whether the walk passes the header rows before it reaches the answer: it wrapped, or there was none.
        // The headers are read again as they stand now, and must stand where they stood when the step was
        // taken; headers that moved meanwhile are the columns having moved, the order changed's other half.
        const answered = answer.found ? cellPosition(answer.row, request.columns.indexOf(answer.column)) : undefined;
        const headersNow = headers.length === 0 ? headers : this.#findHeaders();
        const after = headerAfterAnswer(matchingHeaders(headersNow, patternOf(request)), request.from, answered, request.backward);
        if (after !== undefined) {
            if (!sameHeaders(headers, headersNow)) {
                this.#refuseFind('orderChanged');
                return;
            }
            this.#landOnHeader(itemAt(headersNow, after));
            return;
        }
        if (!answer.found) {
            this.#refuseFind('notFound');
            return;
        }
        // A column the request named that has since left the grid: the columns moved while the step was out.
        const column = this.#columns.findIndex(({ column: declared }) => declared.name === answer.column);
        if (column < 0) {
            this.#refuseFind('orderChanged');
            return;
        }
        this.#reconcileSelection();
        const found = cellPosition(answer.row, column);
        this.#findCurrent = { cell: found, version: this.#sequence() };
        this.#setFindOutcome({ kind: 'found' });
        // With a scope the Selection stands and only the Focus moves inside it, so the next step searches the
        // same ranges; without one it collapses onto the match. A scope is kept only while it is the Selection
        // the step was asked under: one the columns moved has been dropped (ADR-0011), and one the user replaced
        // is theirs, and either way the match is clicked onto.
        const held = this.#held.selection;
        this.#apply(request.scope !== undefined && held === selection && held.contains(found) ? held.focusOn(found) : held.click(found, this.#extent));
        this.#revealOwed = { target: found, extending: undefined, rowStart: false, stepRows: 0 };
        this.#revealHeaderOwed = undefined;
        this.#followArmed = { focus: found, target: found };
    }

    // The headers a step may stop on, in walk order (ADR-0333 §3); none while they are not searched.
    #findHeaders(): readonly FindHeader[] {
        if (!this.#findsHeaders) return [];
        return findHeaders(this.#columns.map(({ column, header }) => ({ name: column.name, header })), this.#headerLayout);
    }

    // The current header's index among `headers`, wherever the columns have moved it, or none: none is current,
    // the Selection it was found under has been replaced, or it is not among them, and then it is forgotten.
    #currentHeaderIn(headers: readonly FindHeader[]): number | undefined {
        this.#forgetCurrentHeaderOffSelection();
        const current = this.#findCurrentHeader;
        if (current === undefined) return undefined;
        const index = headers.findIndex(header => header.id === current.id);
        if (index >= 0) return index;
        this.#dropCurrentHeader();
        return undefined;
    }

    // A header found is the current match only under the Selection it was found under: the user has moved,
    // and the next step starts from where they moved to (ADR-0333).
    #forgetCurrentHeaderOffSelection(): void {
        const current = this.#findCurrentHeader;
        if (current !== undefined && current.selection !== this.#held.selection) this.#dropCurrentHeader();
    }

    // No header is current any more, and the panel no longer says one was found: it would name a header that
    // is not the current match.
    #dropCurrentHeader(): void {
        if (this.#findCurrentHeader === undefined) return;
        this.#findCurrentHeader = undefined;
        if (this.#findOutcome.kind === 'foundInHeader') this.#setFindOutcome({ kind: 'none' });
    }

    // A step's header becomes the current match (ADR-0333 §1): the current Match Mark on it, the columns under
    // it scrolled into view across, the panel naming it. The Focus and the Selection stay where they are.
    #landOnHeader(header: FindHeader): void {
        this.#findCurrent = undefined;
        this.#findCurrentHeader = { id: header.id, selection: this.#held.selection, columns: this.#columns, layout: this.#headerLayout };
        this.#setFindOutcome({ kind: 'foundInHeader', header: header.text });
        this.#revealOwed = undefined;
        this.#revealHeaderOwed = { first: header.firstColumn, last: header.lastColumn };
    }

    // The current match is the Focus a found step moved to only while the Focus stays there, in the order it
    // was found in: once either moves it is forgotten, not merely hidden, so coming back to that position, or
    // another row arriving there, is not taken for it (ADR-0332 §4). A header found goes with its Selection,
    // and with its header no longer shown.
    #forgetCurrentMatchOffFocus(): void {
        this.#forgetCurrentHeaderOffSelection();
        // Looked for again only once the columns or the Header Groups have changed, not on every pass.
        const header = this.#findCurrentHeader;
        if (header !== undefined && (header.columns !== this.#columns || header.layout !== this.#headerLayout)) {
            if (this.#findHeaders().some(({ id }) => id === header.id)) {
                this.#findCurrentHeader = { ...header, columns: this.#columns, layout: this.#headerLayout };
            } else {
                this.#dropCurrentHeader();
            }
        }
        const current = this.#findCurrent;
        if (current === undefined) return;
        const focus = this.#held.selection.focus;
        if (focus === undefined || !sameCell(focus, current.cell) || current.version !== this.#sequence()) this.#findCurrent = undefined;
    }

    // What the painted cells are judged by in this pass (ADR-0332 §4), or none while the panel is closed, holds
    // no text, or nobody can judge: the Consumer's rule, else the reference rule where `GridSource.from`'s
    // source answers Find by it. The same instance while nothing it holds changed.
    #matchMarksNow(): MatchMarks<Row> | undefined {
        const held = this.#findHeld;
        const rule = this.#findMatch;
        if (this.#popover?.kind !== 'find' || held.text === '' || (rule === undefined && !(this.#source instanceof InMemoryGridSource))) {
            this.#matchMarks = undefined;
            return undefined;
        }
        const marks = this.#matchMarks;
        if (marks?.same(rule, held) === true) return marks;
        this.#matchMarks = new MatchMarks(rule, this.#findPattern());
        return this.#matchMarks;
    }

    // What the header band marks in this pass (ADR-0333 §1), or none while the panel is closed, holds no text,
    // or the headers are not searched. Judged by the reference rule over the text the header shows, whoever
    // answers Find: the grid holds that text. The same instance while nothing it holds changed.
    #headerMatchesNow(): HeaderMatches<Row> | undefined {
        const held = this.#findHeld;
        if (this.#popover?.kind !== 'find' || held.text === '' || !this.#findsHeaders) {
            this.#headerMatches = undefined;
            return undefined;
        }
        const current = this.#findCurrentHeader?.id;
        const was = this.#headerMatches;
        if (was !== undefined && was.columns === this.#columns && was.layout === this.#headerLayout && was.current === current
            && was.pattern.text === held.text && was.pattern.matchCase === held.matchCase && was.pattern.wholeCell === held.wholeCell) {
            return was;
        }
        const pattern = this.#findPattern();
        const matches = matcherOf(pattern);
        const mark = (text: string, id: string): MatchMark => (!matches(text) ? 'none' : id === current ? 'current' : 'match');
        const groups = new Map(this.#headerLayout.groups.map(group => [groupHeaderId(group), mark(group.label, groupHeaderId(group))] as const));
        this.#headerMatches = {
            pattern, columns: this.#columns, layout: this.#headerLayout, current, groups,
            leaves: this.#columns.map(({ column, header }) => mark(header, columnHeaderId(column.name))),
        };
        return this.#headerMatches;
    }

    /**
     * Replace (ADR-0332 §2): the current match written with the replacement, as one Edit Intent with its Edit
     * Verdict, and then the next match found. With no current match, this finds the next one, so the user sees
     * what the next press writes; a current match whose entry no longer matches is passed over to the next. One
     * that cannot be written, its column not Editable, a Reject or the Consumer's refusal, writes nothing and is
     * said, and stays the current match.
     */
    #replaceOne(): void {
        const popover = this.#popover;
        const held = this.#findHeld;
        // While a Replace All is out, its buttons are disabled: a Replace taken now would be judged on rows the
        // Consumer is still writing.
        if (popover?.kind !== 'find' || !popover.panel.replaceShown || !this.#canReplace || held.text === '' || this.#replacing > 0) return;
        // Made on the newest rows the source holds, as every write is (ADR-0142 D5).
        this.#source.publishGathered?.();
        this.#reconcileSelection();
        this.#forgetCurrentMatchOffFocus();
        const current = this.#findCurrent;
        const holding = current === undefined ? undefined : this.#rowAt(current.cell.row);
        if (current === undefined || holding === undefined) {
            this.#findStep(false);
            return;
        }
        const { cell } = current;
        // The replacement is written into what the editor would open with, a Sheet's Entry where it declares one, so
        // a Formula stays a Formula (ADR-0332 §2); what the cell paints is what the user sees as they press, and what
        // the write is told it replaces (ADR-0142).
        const painted = this.#paintedTextAt(cell);
        const text = replacedEntry(this.#openingTextAt(cell), this.#findPattern(), held.replaceText);
        if (text === undefined) {
            this.#findStep(false);
            return;
        }
        const resolved = itemAt(this.#columns, cell.column);
        const name = resolved.column.name;
        const refused = (reason: string | undefined): void => {
            this.#refuseReplace([{ rowNumber: cell.row + 1, column: headerOf(resolved), reason }]);
        };
        // With nobody hearing the write, it is refused and said, never a step that looks like it wrote.
        const heard = this.#editHeard;
        if (!resolved.editable || heard === undefined) {
            refused(undefined);
            return;
        }
        // The Consumer's judgement, as at a commit (ADR-0034): a Reject writes nothing.
        const verdict: EditVerdict = resolved.column.validate === undefined
            ? { kind: 'accept' }
            : checkedVerdict(resolved.column.validate(holding.row, text), name);
        if (verdict.kind === 'reject') {
            refused(verdict.message);
            return;
        }
        this.#supersedeFill();
        const intent = new GridEditIntent(holding.row, name, text, painted, painted);
        // The user's own write (D1), noted before the Consumer hears it, and taken back if refused.
        const written = this.#noteUserWrite([fromCorners(cell, cell)]);
        let returned = false;
        try {
            heard(intent);
            returned = true;
        } finally {
            intent.seal();
            // What the source gathered while the Consumer wrote is put out as its handler returns (D5).
            this.#source.publishGathered?.();
            this.#userWriteAnswered(written, !returned || intent.refused);
        }
        const refusal = intent.refusalMessage;
        if (refusal !== undefined) {
            refused(refusal);
            return;
        }
        this.#findStep(false);
    }

    /**
     * Replace All (ADR-0332 §3): one Replace Request, raised under the source's version the positions in it were
     * read in, which the Consumer performs and answers, now or later. The grid writes nothing: it says the
     * count, or names the cells the Consumer could not write, and refuses an answer under another version than
     * the request's, or from a source since replaced, as the order having moved (ADR-0011).
     */
    #replaceAll(): void {
        const popover = this.#popover;
        const held = this.#findHeld;
        const replace = this.#onReplace;
        if (popover?.kind !== 'find' || !popover.panel.replaceShown || !this.#canReplace || held.text === '' || replace === undefined
            || this.#replacing > 0) {
            return;
        }
        this.#source.publishGathered?.();
        this.#reconcileSelection();
        // The write moves the Focus nowhere, and no match is current after it (ADR-0332 §4).
        this.#findCurrent = undefined;
        this.#dropCurrentHeader();
        const selection = this.#held.selection;
        const request: GridReplaceRequest = Object.freeze({
            ...this.#findPattern(), replacement: held.replaceText,
            scope: !selection.isEmpty && selection.cellCount > 1 ? selection.ranges : undefined,
            columns: Object.freeze(this.#columns.map(({ column }) => column.name)),
            editableColumns: Object.freeze(this.#columns.filter(({ editable }) => editable).map(({ column }) => column.name)),
            rowSequenceVersion: this.#state.rowSequenceVersion,
        });
        const source = this.#source;
        const answer = replace(request);
        if (!isThenable(answer)) {
            this.#replaceAnswered(request, answer, source);
            return;
        }
        // Answered later: until then a second Replace All, raised under the same version over rows the first is
        // still writing, would be performed or not as the answer's timing fell, so the buttons wait for it
        // (the spine's sixth rule).
        this.#replacing += 1;
        popover.panel.waitForReplace(true);
        this.#invalidate();
        const answered = (): void => {
            this.#replacing -= 1;
            const standing = this.#popover;
            if (standing?.kind === 'find') standing.panel.waitForReplace(false);
            this.#invalidate();
        };
        // oxlint-disable-next-line ex-grid/no-catch -- the grid stops waiting on a Replace All the Consumer failed, and rethrows the failure
        answer.then(result => {
            answered();
            if (!this.#destroyed) this.#replaceAnswered(request, result, source);
        }, (error: unknown) => {
            answered();
            throw error;
        });
    }

    // A Replace All's answer (ADR-0332 §3).
    #replaceAnswered(request: GridReplaceRequest, result: GridReplaceResult, source: GridSource<Row>): void {
        // The source puts out what the write gathered before the next gesture (D5).
        this.#source.publishGathered?.();
        if (!isReplaceResult(result)) {
            throw new TypeError('onReplace answered something other than a Replace result: it answers replacedCells(count, version) '
                + 'or refusedCells(cells, version), as GridSource.from\'s replace does (ADR-0332).');
        }
        if (result.rowSequenceVersion !== request.rowSequenceVersion || source !== this.#source) {
            this.#refuseFind('orderChanged');
            return;
        }
        if (result.kind === 'refused') {
            this.#refuseReplace(result.cells.map(cell => {
                const index = request.columns.indexOf(cell.column);
                if (index < 0 || !Number.isInteger(cell.row) || cell.row < 0 || cell.row >= this.#totalRows) {
                    throw new RangeError(`The Replace answer names row ${cell.row}, column '${cell.column}', outside the request: it asked `
                        + `about the columns [${request.columns.join(', ')}] of a result of ${this.#totalRows} rows (ADR-0332).`);
                }
                const now = index < this.#columns.length ? itemAt(this.#columns, index) : undefined;
                return { rowNumber: cell.row + 1, column: now?.column.name === cell.column ? headerOf(now) : cell.column, reason: cell.message };
            }));
            return;
        }
        if (result.count === 0) {
            this.#refuseFind('notFound');
            return;
        }
        this.#askSummaryAgain();
        this.#setFindOutcome({ kind: 'replaced', count: result.count });
    }

    // A Replace that wrote nothing (ADR-0332 §2): the panel names the cells and why, and the Consumer is told.
    #refuseReplace(cells: readonly FindPanelRefusedCell[]): void {
        this.#setFindOutcome({ kind: 'replaceRefused', cells });
        this.#onRefused?.({ kind: 'find', reason: 'replaceRefused' });
    }

    // ---- The clipboard (ADR-0005) ------------------------------------------------------------------------

    // A copy the browser's command asked for. Refused, the clipboard stays as it was: the default copy of a
    // user-select: none root carries nothing, so the event is left alone. Approved, a Selection the Window
    // holds is written on the event itself, which needs no permission; one that runs past it starts the
    // asynchronous write now, inside the gesture, and gathers the rows while the browser waits. Each copy
    // supersedes the one before it, so a slow copy still gathering never lands over a newer one.
    readonly #onCopy = (event: ClipboardEvent): void => {
        // Only when the grid itself holds the keyboard, on its root or its Keyboard Field: a control inside the
        // grid, the Cell Editor among them, keeps its own clipboard (ADR-0080).
        if (event.target !== this.#root && event.target !== this.#keyField) return;
        this.#copy?.abort();
        this.#copy = undefined;
        // The Selection as the source and the columns stand now, not as the last pass left it (ADR-0011).
        this.#reconcileSelection();
        const decision = planCopy(this.#held.selection, { cellCap: this.#copyCellCap });
        if (decision.refused) {
            this.#refuse({ kind: 'copy', reason: decision.reason });
            return;
        }
        const { plan } = decision;
        const state = this.#state;
        const columns = this.#columns;
        const data = event.clipboardData;
        // The Consumer's flavours where it answers copies (ADR-0050, item 9), or nothing where it refused, which it
        // has said.
        const answered = this.#copyAnswer === undefined ? undefined : this.#answeredCopy(plan, false);
        if (this.#copyAnswer !== undefined && answered === undefined) return;
        const covered = windowCovers(plan, state);
        if (data !== null && (answered !== undefined || covered)) {
            // Both flavours in one operation: what was on screen in text/plain, and the raw value in
            // text/html, which Excel prefers and reads with its precision and type (ADR-0005).
            const { text, html } = answered === undefined ? assembleCopy(plan, copyValues(columns, windowRowAt(state))) : answered;
            event.preventDefault();
            data.setData('text/plain', text);
            data.setData('text/html', html);
            // Set here, the write cannot fail: it is the grid's own, and it has landed (ADR-0170). Outlined where the
            // Window holds every row it copied, and not when made while a write by the asynchronous route is in
            // flight, which it may land on either side of.
            if (this.#clipboardWatched) {
                const counted = this.#ledger.count(this);
                this.#land(covered && !counted.uncertain ? CopiedRange.of(plan, this.#sequence(), columns, windowRowAt(state)) : undefined);
            }
            return;
        }
        // Started before the event is taken from the browser: a write that cannot start leaves the clipboard
        // as the default copy leaves it, untouched.
        if (answered === undefined) this.#writeCopy(plan, false);
        else this.#writeAnswered(plan, answered);
        event.preventDefault();
    };

    // The Context Menu's copy (ADR-0005, ADR-0036): a menu item's click fires no copy event, so it always takes
    // the asynchronous route, whatever the Selection's size, started inside the gesture that chose it. With
    // headers, the header row counts against the cap and goes into both flavours.
    #menuCopy(withHeaders: boolean): void {
        this.#copy?.abort();
        this.#copy = undefined;
        this.#reconcileSelection();
        const decision = planCopy(this.#held.selection, { cellCap: this.#copyCellCap, withHeaders });
        if (decision.refused) {
            this.#refuse({ kind: 'copy', reason: decision.reason });
            return;
        }
        if (this.#copyAnswer !== undefined) {
            const answer = this.#answeredCopy(decision.plan, withHeaders);
            if (answer !== undefined) this.#writeAnswered(decision.plan, answer);
            return;
        }
        this.#writeCopy(decision.plan, withHeaders);
    }

    // The Consumer's answer to an approved copy (ADR-0050, item 9), or nothing where it refused, which is said in its
    // own sentence.
    #answeredCopy(plan: CopyPlan, withHeaders: boolean): { readonly text: string; readonly html: string } | undefined {
        const answer = checkedCopyAnswer(defined(this.#copyAnswer, 'the copy answer')({ plan, withHeaders, rowSequenceVersion: this.#handedVersion() }));
        if (answer.kind === 'write') return answer;
        this.#sayRefusal(answer.sentence);
        this.#onRefused?.({ kind: 'copy', reason: 'refusedByConsumer' });
        return undefined;
    }

    // The Consumer's flavours written where no copy event carries them: the menu's copy, or a copy event without
    // its data. Held as a gathered copy is: handed over a microtask later, when a newer copy has superseded it,
    // nothing is written, and its outline, where the Window holds every row it copied, lands with its write
    // (ADR-0170).
    #writeAnswered(plan: CopyPlan, answered: CopyFlavours): void {
        const state = this.#state;
        const columns = this.#columns;
        const version = this.#sequence();
        const outlined = windowCovers(plan, state);
        this.#writeGathered(signal => Promise.resolve().then(() => (signal.aborted ? undefined : {
            flavours: answered, outline: () => (outlined ? CopiedRange.of(plan, version, columns, windowRowAt(state)) : undefined),
        })));
    }

    // The asynchronous write of a copy (ADR-0005): the rows gathered while the browser waits.
    #writeCopy(plan: CopyPlan, withHeaders: boolean): void {
        this.#writeGathered(signal => this.#gatherCopy(plan, this.#state, this.#columns, this.#sequence(), withHeaders, signal));
    }

    // A copy written as `gather` hands it over, held as the one a newer copy supersedes.
    #writeGathered(gather: (signal: AbortSignal) => Promise<GatheredCopy<Row> | undefined>): void {
        const copy = new AbortController();
        // Counted as the grid's own write the moment it is in hand, before the browser writes it, so the change
        // it causes finds it counted (ADR-0170).
        const own: OwnWrite<Row> = { gathered: undefined, counted: undefined, ended: false };
        const flavours = gather(copy.signal).then(gathered => {
            if (gathered === undefined) return undefined;
            own.gathered = gathered;
            if (this.#clipboardWatched && !own.ended) own.counted = this.#ledger.count(this, true);
            return gathered.flavours;
        });
        let written: Promise<boolean>;
        try {
            written = this.#reports.writeClipboard(this.#root, flavours);
        } catch (error) {
            copy.abort();
            throw error;
        }
        this.#copy = copy;
        this.#holdWhileWriting(written, copy, own);
    }

    // A copy's write landed (ADR-0170): its outline replaces the one before, or none does. The pass checks it
    // as it checks every outline: the order or the columns moved since it was made drop it, as does a copied
    // cell the Window holds that reads otherwise.
    #land(copied: CopiedRange<Row> | undefined): void {
        this.#copied = copied;
        this.#invalidate();
    }

    // The clipboard changed. A change this grid's own copy made keeps the outline, and so does one made before
    // a write of this grid's that the browser has yet to report: what the clipboard holds is about to be this
    // grid's. Any other — another application's, another grid's later copy, a text field's, or this grid's own
    // that may have landed on either side of another — means the clipboard may no longer hold what the outline
    // marks (ADR-0170).
    readonly #onClipboardChange = (change: object): void => {
        const by = this.#ledger.hear(change);
        if ((by?.owner === this && !by.uncertain) || this.#ledger.awaits(this)) return;
        if (this.#copied === undefined) return;
        this.#copied = undefined;
        this.#invalidate();
    };

    // The flavours of a copy past the Window, or nothing. Rows that could not all be had, or that the order
    // or the columns moved under while they were gathered, refuse the copy by name: neither the fraction in
    // hand nor rows of two orders is ever written (ADR-0005, ADR-0011). A copy superseded or a grid
    // destroyed writes nothing and says nothing.
    async #gatherCopy(plan: CopyPlan, state: GridSourceState<Row>, columns: readonly ResolvedColumn<Row>[], version: number,
        withHeaders: boolean, signal: AbortSignal): Promise<GatheredCopy<Row> | undefined> {
        const moved = (): boolean => this.#sequence() !== version || this.#columns !== columns;
        let rows: ReadonlyMap<number, Row> | undefined;
        try {
            rows = await gatherRows(plan, state, this.#source, signal, moved);
        // oxlint-disable-next-line ex-grid/no-catch -- a superseded copy's own abort is expected and writes nothing; any other failure is rethrown
        } catch (error) {
            if (signal.aborted) return undefined;
            // Said, and the failure itself left to surface: the refusal is the user's, the error the Consumer's.
            this.#refuse({ kind: 'copy', reason: 'rowsUnavailable' });
            throw error;
        }
        if (signal.aborted) return undefined;
        if (rows === undefined || moved()) {
            this.#refuse({ kind: 'copy', reason: 'rowsUnavailable' });
            return undefined;
        }
        const rowAt = (row: number): Row => defined(rows.get(row), 'a gathered row');
        const values = copyValues(columns, rowAt);
        const flavours = assembleCopy(plan, withHeaders ? { ...values, headerAt: column => itemAt(columns, column).header } : values);
        return { flavours, outline: () => CopiedRange.of(plan, version, columns, rowAt) };
    }

    // The asynchronous write (ADR-0005) holds the Settled Mark until it has written or refused (ADR-0220). A
    // write the browser would not make is a refusal of its own: a user told nothing would paste the old
    // clipboard believing it the copy. A write that lands is outlined (ADR-0170), and one that fails gives
    // back its count of the grid's own writes.
    #holdWhileWriting(written: Promise<boolean>, copy: AbortController, own: OwnWrite<Row>): void {
        this.#clipboardWrites += 1;
        this.#showSettled(false);
        const done = (): void => {
            this.#clipboardWrites -= 1;
            if (this.#copy === copy) this.#copy = undefined;
            if (!this.#destroyed) this.#lookSettled();
        };
        const landed = (wrote: boolean): void => {
            own.ended = true;
            const { gathered, counted } = own;
            if (counted !== undefined) {
                if (wrote) this.#ledger.landed(counted);
                else this.#ledger.withdraw(counted);
            }
            // Landed before the Settled Mark is looked at, so the mark never shows the write without its outline.
            // A newer copy superseded this one, and yet its write landed after it: the clipboard holds this
            // one, which the newer outline would misname, so none stands (ADR-0170). Nor does one for a write that
            // may have landed on either side of another.
            if (wrote && gathered !== undefined && this.#clipboardWatched && !this.#destroyed) {
                this.#land(copy.signal.aborted || counted?.uncertain === true ? undefined : gathered.outline());
            }
            done();
        };
        // oxlint-disable-next-line ex-grid/no-catch -- every rejection is said: a refused write as a refusal, anything else rethrown
        written.then(landed, (error: unknown) => {
            own.ended = true;
            // A failed write causes no change; a change from elsewhere may have taken its count already.
            if (own.counted !== undefined) this.#ledger.withdraw(own.counted);
            done();
            if (this.#destroyed || copy.signal.aborted) return;
            // By name, not by class: the error is the grid's window's, which need not be this realm's.
            if (typeof error === 'object' && error !== null && Reflect.get(error, 'name') === 'NotAllowedError') {
                this.#refuse({ kind: 'copy', reason: 'clipboardUnavailable' });
                return;
            }
            throw error;
        });
    }

    // A refused gesture is said in the live region and raised by name (ADR-0005, ADR-0203). It stands down
    // a description of the Selection still owed, which would otherwise say over it, and a refusal said
    // again is emptied first and said in the next frame: the same text written again is no change, and the
    // second refusal is the one the user needs (A11Y-16).
    // `column`, for a paste refused over a column that is not Editable, is the column its words name.
    #refuse(refusal: WordedRefusal, column?: number): void {
        this.#sayRefusal(this.#wordsFor(refusal, column));
        this.#onRefused?.(refusal);
    }

    // A sentence a refusal or a held edit says in the live region (A11Y-15, A11Y-16).
    #sayRefusal(words: string): void {
        this.#announcedRanges = this.#held.selection.ranges;
        this.#cancelAnnounce?.();
        this.#cancelAnnounce = undefined;
        if (this.#announce.textContent === words) {
            this.#say('');
            this.#showSettled(false);
            this.#cancelAnnounce = this.#reports.nextFrame(this.#root, () => {
                this.#cancelAnnounce = undefined;
                if (this.#destroyed) return;
                this.#say(words);
                this.#lookSettled();
            });
        } else {
            this.#say(words);
        }
    }

    // The built-in words for a refusal (ADR-0203): a shape mismatch where a paste may spill offers a single
    // cell too, and a column that is not Editable is named by its header.
    #wordsFor(refusal: WordedRefusal, column: number | undefined): string {
        if (refusal.kind === 'copy') return copyRefusalWords[refusal.reason];
        if (refusal.reason === 'shapeMismatch' && this.#pasteMaySpill) return shapeMismatchMaySpillWords;
        const words = pasteRefusalWords[refusal.reason];
        // As text, never as a replacement pattern: a header may hold `$&`.
        return column === undefined ? words : words.replace(columnPlaceholder, () => itemAt(this.#columns, column).header);
    }

    // A paste the browser's command fired on the root (ADR-0014). The flavours are readable only inside the
    // event, so both are read here; past the byte cap the paste is refused whole, and a clipboard holding
    // nothing tabular is no paste at all. The default is prevented either way: pasting into an element that
    // is not editable does nothing, and must not start doing something later.
    readonly #onPaste = (event: ClipboardEvent): void => {
        // Only when the grid itself holds the keyboard, on its root or its Keyboard Field: a control inside the
        // grid, the Cell Editor among them, keeps its own clipboard (ADR-0080).
        if (event.target !== this.#root && event.target !== this.#keyField) return;
        event.preventDefault();
        const data = event.clipboardData;
        if (data === null) {
            this.#refuse({ kind: 'paste', reason: 'unreadable' });
            return;
        }
        const text = data.getData('text/plain');
        const html = data.getData('text/html');
        if (exceedsUtf8([text, html], this.#pasteByteCap)) {
            this.#refuse({ kind: 'paste', reason: 'tooLarge' });
            return;
        }
        const block = parseClipboardBlock(html, text);
        if (block !== undefined) this.#paste(block);
    };

    // Plans a parsed block onto the Selection and raises its one intent (ADR-0014, ADR-0035, ADR-0050 item 3).
    #paste(block: ClipboardBlock): void {
        // The Selection the user aimed with, as last painted. Judged on the newest rows the source holds: one
        // that gathers live changes puts them out now (ADR-0142 D5, LV-16). An order that moved since takes
        // that Selection, and the paste is refused aloud rather than written over other rows (ADR-0011).
        const aimed = this.#held;
        this.#source.publishGathered?.();
        this.#reconcileSelection();
        if (!aimed.selection.isEmpty && aimed.rowSequenceVersion !== this.#sequence()) {
            this.#refuse({ kind: 'paste', reason: 'orderMoved' });
            return;
        }
        const target = this.#held.selection;
        const columns = this.#columns;
        // A grid nobody hears a paste from writes no cell: every column is then one that cannot be edited.
        const heard = this.#pasteHeard !== undefined;
        const decision = planPaste(
            target, pasteShape(block.values.length, itemAt(block.values, 0).length),
            column => heard && column >= 0 && column < columns.length && itemAt(columns, column).editable,
            { ...(this.#pasteMaySpill ? { spillWithin: this.#extent } : {}), plainTextOnly: !block.isFromTable },
        );
        if (decision.refused) {
            this.#refuse({ kind: 'paste', reason: decision.reason }, decision.reason === 'targetNotEditable' ? decision.column : undefined);
            return;
        }
        const { plan } = decision;
        const version = this.#sequence();
        this.#supersedeFill();
        const intent = new GridPasteIntent(plan, block.values, block.origins, this.#handedVersion(), block.marks);
        const noted = this.#noteUserWrite(plan.targets);
        let returned = false;
        try {
            defined(this.#pasteHeard, 'the paste\'s listener')(intent);
            returned = true;
        } finally {
            // Answered once it returns: a refusal after that would be one the grid can no longer honour.
            intent.seal();
            // What the source gathered while the Consumer wrote is put out as its handler returns, or throws
            // (ADR-0142 D5).
            this.#source.publishGathered?.();
            this.#userWriteAnswered(noted, !returned || intent.refused);
        }
        // A refused paste wrote nothing: the Selection stays where it was (ADR-0050, item 3).
        if (intent.refused) return;
        this.#reconcileSelection();
        // Positions mean something only under the order they were written in: a handler that moved it, or
        // took the cells away, places nothing (ADR-0011).
        const extent = this.#extent;
        const written = itemAt(plan.targets, 0);
        if (this.#sequence() !== version || bottomRow(written) >= extent.rowCount
            || written.leftColumn + written.columnCount > extent.columnCount) {
            return;
        }
        // One value of plain text went into one cell of a larger Selection, which collapses to it (ADR-0014,
        // amended 2026-09-29); a spilled block becomes the Selection, with nothing scrolled (ADR-0050, item 3).
        const spilled = this.#pasteMaySpill && plan.targets.length === 1 && target.ranges.length === 1
            && !sameSelectionRange(written, itemAt(target.ranges, 0));
        if (!plan.collapsesSelection && !spilled) return;
        if (this.#apply(this.#held.selection.place(written, cellPosition(written.topRow, written.leftColumn), extent))) this.#invalidate();
    }

    // DOM focus leaving the grid ends a release of Tab, as the C# core's key gate does (ADR-0080). The
    // Keyboard Field left takes the root's ring with it, and the keyboard back in from outside is the
    // keyboard's, whatever pressed last. An edit left for another control stands (ED-26).
    readonly #onFocusLeft = (event: FocusEvent): void => {
        if (event.target === this.#keyField) {
            this.#settleKeyField();
            this.#markFocusVisible(false);
        }
        const next = event.relatedTarget;
        // A key the IME took that is still being told ends here: its keyup, if it comes, is told wherever the
        // keyboard went, and none of it can start a composition here (ADR-0080). The editor's request that
        // waited for it is judged where the keyboard is going, since DOM focus is on nothing meanwhile.
        if (this.#imeKeyTold) {
            this.#imeKeyTold = false;
            this.#focusEditor(next instanceof Element ? next : undefined);
        }
        if (!(next instanceof Node) || !this.#root.contains(next)) {
            this.#tabReleased = false;
            this.#keyboardByPress = false;
        }
    };

    // Focus that lands on the root itself, or on the scroller by a press on the rows, is passed on at once to
    // what holds the keyboard here: an open edit's editor, or the Keyboard Field where the grid has one
    // (ADR-0080). The keys and aria-activedescendant are never the scroller's (ADR-0033).
    readonly #onFocused = (event: FocusEvent): void => {
        const target = event.target;
        const edit = this.#edit;
        if (edit !== undefined && (target === this.#root || target === this.#scroller || target === this.#keyField)) {
            // Back into a grid whose edit stands, the keyboard is the edit's (ED-26), on the surface it was typed on.
            const surface = edit.surface === 'bar' && this.#bar.text.isConnected ? this.#bar.text : edit.element;
            if (surface.isConnected) surface.focus({ preventScroll: true });
            return;
        }
        if (target === this.#root || target === this.#scroller) {
            this.#takeKeyboard();
        } else if (target === this.#keyField) {
            this.#markFocusVisible(!this.#keyboardByPress);
        }
    };

    // A press anywhere in the root says the keyboard comes by the pointer now (KB-12). A press while the
    // Keyboard Field composes ends the composition first, so its text is the grid's ahead of the press: by
    // its default the press would end it after moving the Focus, and the text would open an edit there
    // (ADR-0080).
    readonly #onRootPress = (event: MouseEvent): void => {
        // A new press, whose own button, if it is one, is noted after this: one before it whose release went
        // unheard is over.
        this.#actionPress = undefined;
        this.#markPress = undefined;
        this.#headerMarkPress = undefined;
        // Ended before the press is marked: the field left says the keyboard has gone (#onFocusLeft).
        if (this.#composing && event.button === 0) this.#keyField.blur();
        this.#keyboardByPress = true;
        // A press anywhere else in the instance dismisses a popover, and keeps its own meaning: where it lands
        // decides where the keyboard goes (ADR-0010, ADR-0039).
        // The ▾ of the column menu standing is not elsewhere: its click closes that menu, as a toggle.
        const popover = this.#popover;
        const toggle = popover?.kind === 'column' && event.target instanceof Element && this.#menuButtons.get(event.target) === popover.column;
        if (popover !== undefined && !toggle && !(event.target instanceof Node && popover.element.contains(event.target))) {
            this.#closePopover(false);
        }
        // A press anywhere on the rows or the header leaves an Interactive cell, and keeps its own meaning
        // (ADR-0037): the chosen button's paint goes even where the press moves nothing.
        if (this.#interactive !== undefined && event.target instanceof Node && this.#scroller.contains(event.target)) {
            this.#interactive = undefined;
            this.#invalidate();
        }
    };

    // A release that will raise no click on the mark pressed ends that press, the platform's cancel: a click that
    // comes later with no press, a script's or assistive technology's, is then taken as made on the paint it
    // lands on, never on a press it was not part of (MK-9). A release on the rows is the rows' own to judge
    // (#releaseMarkPress), since it may stand over the checkbox a pass put in the pressed one's place.
    readonly #onRootRelease = (event: MouseEvent): void => {
        if (event.target !== this.#rowMarks.headerBox) this.#headerMarkPress = undefined;
        if (!(event.target instanceof Node && this.#viewport.contains(event.target))) this.#markPress = undefined;
    };

    // The root's keyup: a key the IME took in the Keyboard Field has been told whole, since everything it owed
    // the page came before it, so the editor's request that waited for it is granted (ADR-0080).
    readonly #onKeyUp = (): void => {
        if (!this.#imeKeyTold) return;
        this.#imeKeyTold = false;
        this.#focusEditor();
    };

    // The root's ring while its Keyboard Field holds a keyboard that did not come of a press: a text field
    // matches :focus-visible on every focus, so it cannot say how the keyboard arrived (KB-12, ADR-0080).
    #markFocusVisible(on: boolean): void {
        this.#root.toggleAttribute('data-ex-focus-visible', on);
    }

    // Puts the keyboard where the grid holds it with no edit open: its Keyboard Field where it has one, its
    // root otherwise (ADR-0080).
    #takeKeyboard(): void {
        const field = this.#keyField;
        if (field.isConnected) field.focus({ preventScroll: true });
        else this.#root.focus({ preventScroll: true });
    }

    // Text put into the Keyboard Field outside a composition (ADR-0080). What an IME inserts without composing
    // it — a full-width space, a character some IMEs commit at once — is carried as a composition's text is;
    // anything else — the browser's own undo, a drop — is not typing the grid takes, and goes.
    readonly #onKeyFieldInput = (event: Event): void => {
        if (this.#composing || (event instanceof InputEvent && event.isComposing)) return;
        if (event instanceof InputEvent && (event.inputType === 'insertText' || event.inputType === 'insertReplacementText')) {
            this.#keyField.classList.add('ex-key-field-composing');
            this.#carryKeyFieldText();
        } else {
            this.#keyField.value = this.#keyField.value.slice(0, this.#carried);
        }
    };

    // A composition starts in the Keyboard Field, which the stylesheet then draws over the Focus as the
    // editor looks (ADR-0080). The Focus is revealed, as Excel scrolls to the active cell when typing begins;
    // nothing else changes until it ends. DOM focus never moves while it lasts.
    readonly #onKeyFieldComposing = (): void => {
        this.#composing = true;
        this.#keyField.classList.add('ex-key-field-composing');
        if (this.#edit !== undefined) return;
        this.#reconcileSelection();
        this.#composedAim = { drops: this.#drops, dropped: this.#droppedAim };
        const focus = this.#held.selection.focus;
        if (focus !== undefined && !this.#cellIsPainted(focus)) {
            this.#revealOwed = { target: focus, extending: undefined, rowStart: false, stepRows: 0 };
        }
        // A Selection the reconcile just dropped is painted gone while the composition lasts.
        this.#invalidate();
    };

    // A composition ends in the Keyboard Field, with its text, or with none when it was cancelled.
    readonly #onKeyFieldComposed = (): void => {
        this.#composing = false;
        this.#carryKeyFieldText();
    };

    // The field's text past what has already gone (ADR-0080). With no edit open it opens Overwrite holding it,
    // judged as a typed character is, and aimed at what was painted when the composition began: a Selection
    // dropped since throws it away (ADR-0142). An empty text opens an empty edit, as Excel's does when Escape
    // ends a composition. With an edit open — a second composition, finished before the editor took the
    // keyboard — it is typed at the editor's caret. The field keeps showing it until the keyboard leaves.
    #carryKeyFieldText(): void {
        const field = this.#keyField;
        const text = field.value.slice(this.#carried);
        this.#carried = field.value.length;
        const edit = this.#edit;
        if (edit !== undefined) {
            const { element } = edit;
            const start = element.selectionStart ?? element.value.length;
            if (text !== '') element.setRangeText(text, start, element.selectionEnd ?? start, 'end');
        } else if (!this.#destroyed && this.#canEdit) {
            this.#answered += 1;
            // Aimed at what was painted as it began: a Selection dropped since, or dropped before it and not
            // yet painted then, throws the text away (ADR-0142).
            const aim = this.#composedAim;
            this.#reconcileSelection();
            if (aim !== undefined) this.#droppedAim = this.#drops !== aim.drops ? this.#lastDrop : aim.dropped;
            this.#tryOpenEdit('overwrite', text);
            this.#invalidate();
        }
        this.#composedAim = undefined;
        if (this.#edit === undefined || this.#root.ownerDocument.activeElement !== field) this.#settleKeyField();
        this.#focusEditor();
    }

    // The Keyboard Field unseen and empty again: once the keyboard has left it, or when its text opened
    // nothing. Never while it composes, which would end the composition.
    #settleKeyField(): void {
        if (this.#composing) return;
        this.#keyField.value = '';
        this.#carried = 0;
        this.#keyField.classList.remove('ex-key-field-composing');
    }

    // A double click on the rows (ADR-0063): on a cell that edits it opens the edit on its text, in Caret;
    // elsewhere the Consumer hears it, with the Focus its presses placed. While an edit is open it is the
    // editor's, and the editor's own takes no part here.
    readonly #onDoubleClick = (event: MouseEvent): void => {
        if (event.button !== 0 || event.target !== this.#viewport || this.#edit !== undefined) return;
        this.#reconcileSelection();
        const at = this.#cellUnder(event);
        if (at === undefined) return;
        this.#answered += 1;
        this.#invalidate();
        if (this.#isEditableCell(at)) {
            this.#apply(this.#held.selection.click(at, this.#extent));
            this.#startEdit(at, 'caret', this.#openingTextAt(at));
            return;
        }
        this.#onCellDoubleClick?.(at);
    };

    // The move listeners stand only while a drag runs, so a pointer passing over costs nothing. Whatever
    // ends a drag ends a Heading drag, a press on the header and the edge band with it.
    #setDragging(dragging: boolean): void {
        if (!dragging) this.#forgetHeadingDrag();
        if (dragging === this.#dragging) return;
        this.#dragging = dragging;
        if (dragging) {
            this.#viewport.addEventListener('mousemove', this.#onDrag);
            this.#header.addEventListener('mousemove', this.#onDrag);
            this.#root.addEventListener('mouseup', this.#onDragEnd);
            this.#root.addEventListener('mouseleave', this.#onLeave);
        } else {
            this.#viewport.removeEventListener('mousemove', this.#onDrag);
            this.#header.removeEventListener('mousemove', this.#onDrag);
            this.#root.removeEventListener('mouseup', this.#onDragEnd);
            this.#root.removeEventListener('mouseleave', this.#onLeave);
        }
    }

    #forgetHeadingDrag(): void {
        this.#headingDrag = false;
        this.#headerPress = undefined;
        this.#stopEdgeTimer();
    }

    // Whether a press adds or takes out (ADR-0012): Ctrl, or Meta where Meta is Command; Shift outranks it.
    #toggles(event: MouseEvent): boolean {
        return !event.shiftKey && (event.ctrlKey || (event.metaKey && this.#metaIsPrimary));
    }

    // ---- The header: its press, its click and the Heading drag (ADR-0012, ADR-0050 item 1) -------------

    // A press on the header. The click sorts on a plain grid, so the press selects nothing until the
    // pointer reaches another column; released where it was pressed, it is a click (ADR-0012, 2026-09-29).
    readonly #onHeaderPress = (event: MouseEvent): void => {
        this.#answered += 1;
        this.#invalidate();
        // A new press is a new story: a release outside the header raises no click, and a flag left
        // standing would eat the next genuine sort.
        this.#gestureConsumedClick = false;
        this.#tabReleased = false;
        // A press on the header's checkbox is the checkbox's: it neither sorts, selects nor drags, never takes the
        // keyboard, and notes the order and source it was made under for its click (ADR-0043, MK-9).
        if (event.target === this.#rowMarks.headerBox) {
            event.preventDefault();
            if (event.button === 0) this.#headerMarkPress = { sequence: this.#sequence(), source: this.#source };
            return;
        }
        if (event.button !== 0) return;
        // A press on a ▾ is the button's: its click opens the menu, and it neither sorts nor drags. It never takes
        // the keyboard itself: an edit the click cannot commit keeps it in the editor (ADR-0034), and a menu the
        // click opens takes it from where it is.
        if (event.target instanceof Element && this.#menuButtons.has(event.target)) {
            event.preventDefault();
            return;
        }
        // A press on a grip begins a resize (ADR-0016); its click is the gesture's, never the header's.
        const grip = event.target instanceof Element ? this.#grips.get(event.target) : undefined;
        if (grip !== undefined) {
            this.#beginResize(grip, event);
            return;
        }
        if (event.target !== this.#header) return;
        // A press on a heading commits an open edit first (ED-26); one the edit holds sorts and selects
        // nothing, and the keyboard stays in the editor (ADR-0034).
        if (!this.#commitEdit()) {
            event.preventDefault();
            this.#gestureConsumedClick = true;
            return;
        }
        // Where a reorder is heard, dragging a header moves it (ADR-0011); elsewhere a press on a heading
        // begins a Heading drag (ADR-0050, item 1).
        if (this.#onColumnOrderChanged !== undefined) {
            this.#beginReorder(event);
            return;
        }
        this.#reconcileSelection();
        const column = this.#headerColumnAt(event);
        if (column === undefined) return;
        this.#headingPress = {
            x: event.offsetX - this.#scrollLeftPx, y: event.offsetY - this.#bandHeightPx, clientX: event.clientX, clientY: event.clientY,
        };
        this.#setDragging(true);
        // Where a click selects (ADR-0050, item 1), the press selects at once and its click is the gesture's tail,
        // never a sort.
        if (this.#headerClickSelects) {
            this.#gestureConsumedClick = true;
            this.#beginHeadingDrag(column, event.shiftKey, this.#toggles(event));
            return;
        }
        this.#headerPress = { column, extends: event.shiftKey, toggles: this.#toggles(event) };
        this.#headingDrag = true;
    };

    // Clicking a column header sorts, and never selects the column; Shift+click selects whole columns and
    // Ctrl+click adds or takes one out, and neither sorts (ADR-0012). What the Sort does to the rows is the
    // source's (ADR-0001, ADR-0011).
    readonly #onHeaderClick = (event: MouseEvent): void => {
        this.#answered += 1;
        this.#invalidate();
        if (event.target === this.#rowMarks.headerBox) {
            const press = this.#headerMarkPress;
            this.#headerMarkPress = undefined;
            if (this.#commitEdit()) this.#pressHeaderMark(press);
            return;
        }
        // A Heading drag's click is the gesture's tail, not a sort.
        if (this.#gestureConsumedClick) {
            this.#gestureConsumedClick = false;
            if (this.#headingDrag) this.#setDragging(false);
            return;
        }
        // A press whose release was not heard ends with its click, and a click on another column than
        // the one pressed is a drag's tail (ADR-0012, 2026-09-29).
        if (this.#headerPress !== undefined) {
            this.#moveHeadingDrag(event);
            // The move made it a drag when it reached another column, and the press is gone.
            const dragged = !this.#pressPending;
            this.#gestureConsumedClick = false;
            this.#setDragging(false);
            if (dragged) return;
        }
        // A ▾ carries its column's name, so a click that lands after the columns moved and before the header is
        // painted again acts on the column it showed, or on nothing once that column has gone (principle 6).
        const menu = event.target instanceof Element ? this.#menuButtons.get(event.target) : undefined;
        if (menu !== undefined && event.button === 0) {
            this.#toggleColumnMenu(menu);
            return;
        }
        if (event.button !== 0 || event.target !== this.#header) return;
        this.#reconcileSelection();
        const column = this.#headerColumnAt(event);
        if (column === undefined) return;
        const extent = this.#extent;
        const current = this.#held.selection;
        if (event.shiftKey || this.#toggles(event)) {
            // Selection is positional: a column that cannot be sorted is selected like any other. From
            // nothing selected, the Focus is on the first row on screen (KB-9).
            if (extent.rowCount <= 0) return;
            this.#apply(event.shiftKey
                ? current.extendToColumn(column, extent, this.#firstVisibleRow)
                : current.toggleColumn(column, extent, this.#firstVisibleRow));
            return;
        }
        const { column: declared } = itemAt(this.#columns, column);
        if (declared.queryable === false) return;
        this.#source.setSorts(nextSort(this.#state.sorts, declared.name));
    };

    // A double click on a grip is Size to fit (ADR-0016, FN-12a): each column it acts on is fixed at its
    // header's need and every value the grid holds, not only the painted rows, or a column fitted a moment ago
    // shows #### a few rows down; bounded by the declaration.
    readonly #onHeaderDoubleClick = (event: MouseEvent): void => {
        const grip = event.target instanceof Element ? this.#grips.get(event.target) : undefined;
        const heard = this.#onColumnWidthChanged;
        if (grip === undefined || heard === undefined) return;
        this.#answered += 1;
        this.#invalidate();
        this.#reconcileSelection();
        const changes = columnsResizedWith(this.#held.selection.wholeColumns(this.#extent), grip)
            // Only a column that paints its value is fitted to it (ADR-0020).
            .filter(column => itemAt(this.#columns, column).paintsValue)
            .map(column => ({ column: itemAt(this.#columns, column).column.name, widthPx: this.#fittedPx(column) }));
        // Decided before the first is heard: a Consumer answering it with new columns does not move the rest.
        for (const change of changes) heard(change);
    };

    #fittedPx(index: number): number {
        const { column, header, spec } = itemAt(this.#columns, index);
        const metrics = this.#metrics.cellMetrics.for(column.type);
        let required = this.#metrics.headerRequiredPx({ label: header, menuButton: !this.#hideColumnMenu, sortable: column.queryable !== false });
        for (const row of this.#state.window) required = Math.max(required, metrics.estimatePx(textFor(column, column.value(row))));
        return spec.sizeToFit(required).fixedPx;
    }

    // ---- The header's drags: a resize from a grip and a reorder (ADR-0011, ADR-0016, ADR-0032) ----------

    // The guide starts at the width the column declares, not the one painted on Device Pixels (ADR-0090): a
    // resize reports the declared width moved by the drag, never a rounding the Consumer did not ask for.
    #beginResize(column: number, event: MouseEvent): void {
        const geometry = this.#styles.geometry;
        const startWidthPx = geometry.declaredWidthPxOf(column);
        this.#beginColumnGesture({ kind: 'resize', name: itemAt(this.#columns, column).column.name, startClientX: event.clientX, startWidthPx });
        this.#showGestureLine('ex-resize-guide', guideLeftPx(geometry, column, startWidthPx, this.#scrollLeftPx));
    }

    // What a press grabs is the unit that moves (ADR-0032): the leaf under it, or the rectangle standing over it
    // in a tier above. Nothing moves until the pointer passes the threshold, so a plain click stays a sort.
    #beginReorder(event: MouseEvent): void {
        const column = this.#columnUnderHeader(event);
        if (column === undefined) return;
        const grabbed = grabbedAt(this.#headerLayout, column, event.offsetY, this.#bandHeightPx, this.#metrics.headerHeightPx);
        if (grabbed === undefined) return;
        const leaf = grabbed === 'leaf';
        this.#beginColumnGesture({
            kind: 'reorder', names: this.#columns.map(({ column: { name } }) => name),
            unitFirst: leaf ? column : grabbed.firstColumn, unitCount: leaf ? 1 : grabbed.memberCount,
            startClientX: event.clientX, startContentX: event.offsetX, reordering: false, boundary: undefined,
        });
    }

    // The drag is heard on the root, over the header and the rows alike, and only while it runs.
    #beginColumnGesture(gesture: ColumnGesture): void {
        this.#endColumnGesture();
        this.#columnGesture = gesture;
        this.#root.addEventListener('mousemove', this.#onColumnGestureMove);
        this.#root.addEventListener('mouseup', this.#onColumnGestureEnd);
    }

    #endColumnGesture(): void {
        this.#columnGesture = undefined;
        this.#reorderDrops = undefined;
        this.#gestureLine?.remove();
        this.#gestureLine = undefined;
        this.#root.removeEventListener('mousemove', this.#onColumnGestureMove);
        this.#root.removeEventListener('mouseup', this.#onColumnGestureEnd);
    }

    // The guide or the indicator: one element over the band and the rows, its left edge on a Device Pixel as a
    // column's is (ADR-0090), the selection overlay's mechanism for the overlay's reason (ADR-0016).
    #showGestureLine(className: string, leftPx: number): void {
        let line = this.#gestureLine;
        if (line === undefined) {
            line = this.#root.ownerDocument.createElement('div');
            line.className = className;
            line.setAttribute('aria-hidden', 'true');
            this.#spacer.append(line);
            this.#gestureLine = line;
        }
        line.setAttribute('style', `left: ${ColumnGeometry.onDevicePixel(leftPx, this.#devicePixelRatio)}px`);
    }

    #columnNamed(name: string): number | undefined {
        const index = this.#columns.findIndex(({ column }) => column.name === name);
        return index < 0 ? undefined : index;
    }

    // Whether the columns still stand in the order a reorder lifted its unit from: a drop against another order
    // would report a move of other columns, so the drag ends instead (a gesture carries what it was taken
    // against).
    #inOrder(names: readonly string[]): boolean {
        return sameItems(names, this.#columns.map(({ column }) => column.name));
    }

    readonly #onColumnGestureMove = (event: MouseEvent): void => {
        const gesture = this.#columnGesture;
        if (gesture === undefined) return;
        // Released outside the grid: nothing is applied, and a vanished drag drops nowhere.
        if ((event.buttons & 1) === 0) {
            this.#endColumnGesture();
            return;
        }
        const geometry = this.#styles.geometry;
        if (gesture.kind === 'resize') {
            // A column handed over meanwhile is followed by its name; one gone takes the drag with it.
            const column = this.#columnNamed(gesture.name);
            if (column === undefined) {
                this.#endColumnGesture();
                return;
            }
            const widthPx = Math.max(itemAt(this.#columns, column).spec.minWidthPx, gesture.startWidthPx + (event.clientX - gesture.startClientX));
            this.#showGestureLine('ex-resize-guide', guideLeftPx(geometry, column, widthPx, this.#scrollLeftPx));
            return;
        }
        if (!this.#inOrder(gesture.names)) {
            this.#endColumnGesture();
            return;
        }
        if (!gesture.reordering && Math.abs(event.clientX - gesture.startClientX) <= dragThresholdPx) return;
        const contentX = gesture.startContentX + (event.clientX - gesture.startClientX);
        // Worked out once per geometry and layout, not on every move.
        let drops = this.#reorderDrops;
        if (drops === undefined || drops.geometry !== geometry || drops.layout !== this.#headerLayout) {
            drops = { geometry, layout: this.#headerLayout, boundaries: dropBoundaries(geometry, this.#headerLayout, gesture.unitFirst, gesture.unitCount) };
            this.#reorderDrops = drops;
        }
        const boundary = nearestBoundary(geometry, drops.boundaries, gesture.unitFirst, contentX, this.#scrollLeftPx);
        this.#columnGesture = { ...gesture, reordering: true, boundary };
        // Where the unit will land: it stops at the pinned boundary and at a group's edge rather than promising a
        // drop that would be refused (ADR-0011).
        this.#showGestureLine('ex-drop-indicator', boundaryLeftPx(geometry, boundary, gesture.unitFirst, this.#scrollLeftPx));
    };

    readonly #onColumnGestureEnd = (event: MouseEvent): void => {
        const gesture = this.#columnGesture;
        // Another button let go mid-drag ends nothing: the drag is the left button's.
        if (gesture === undefined || event.button !== 0) return;
        this.#endColumnGesture();
        this.#answered += 1;
        this.#invalidate();
        if (gesture.kind === 'resize') {
            // The click that ends it is the gesture's tail, not a sort.
            this.#gestureConsumedClick = true;
            const deltaPx = event.clientX - gesture.startClientX;
            const grabbed = this.#columnNamed(gesture.name);
            const heard = this.#onColumnWidthChanged;
            // A press that barely moved is a click, or half of a double click: it reports nothing, or it would turn
            // an Auto column Fixed behind the user's back (ADR-0016).
            if (Math.abs(deltaPx) <= dragThresholdPx || grabbed === undefined || heard === undefined) return;
            this.#reconcileSelection();
            // Every whole column takes the grabbed one's new width, the guide's, as in Excel, each bounded below
            // by its own MinWidth (FN-12c), and decided before the first is heard.
            const widthPx = Math.max(itemAt(this.#columns, grabbed).spec.minWidthPx, gesture.startWidthPx + deltaPx);
            const changes = columnsResizedWith(this.#held.selection.wholeColumns(this.#extent), grabbed).map(column => {
                const { column: { name }, spec } = itemAt(this.#columns, column);
                return { column: name, widthPx: Math.max(spec.minWidthPx, widthPx) };
            });
            for (const change of changes) heard(change);
            return;
        }
        // A candidate that never crossed the threshold: the click that follows stays a sort.
        if (!gesture.reordering) return;
        this.#gestureConsumedClick = true;
        const { boundary, unitFirst, unitCount, names } = gesture;
        const heard = this.#onColumnOrderChanged;
        // Dropped on its own edges, the unit stays where it was, and nothing is heard.
        if (boundary === undefined || heard === undefined || !this.#inOrder(names)
            || (boundary >= unitFirst && boundary <= unitFirst + unitCount)) return;
        heard(reorderColumns({ names, unitFirst, unitCount, insertAt: boundary }));
    };

    // Read through a getter: after #moveHeadingDrag, which may clear the press, the narrowing at the call
    // would still hold it set.
    get #pressPending(): boolean {
        return this.#headerPress !== undefined;
    }

    // The column a press or a click on the header names, or none: the corner where the Headings meet names
    // none, nor does the dead space past the last column, where columnAt would clamp for a drag, nor a Header
    // Group's rectangle, which is not the leaf under it (ADR-0032).
    #headerColumnAt(event: MouseEvent): number | undefined {
        const column = this.#columnUnderHeader(event);
        if (column === undefined) return undefined;
        const layout = this.#headerLayout;
        if (layout.tierCount > 0 && event.offsetY < this.#bandHeightPx - (layout.leafTierSpanOf(column) * this.#metrics.headerHeightPx)) {
            return undefined;
        }
        return column;
    }

    // The column under a press on the header, whatever tier it is in: none in the corner where the Headings
    // meet, or past the last column, where columnAt would clamp for a drag.
    #columnUnderHeader(event: MouseEvent): number | undefined {
        const geometry = this.#styles.geometry;
        const x = event.offsetX;
        if (!Number.isFinite(x) || !Number.isFinite(event.offsetY) || geometry.isInLead(x, this.#scrollLeftPx)
            || x < 0 || x >= geometry.totalWidthPx) return undefined;
        return geometry.columnAt(x, this.#scrollLeftPx);
    }

    // The row the Focus lands on when a Column Heading is pressed: the first on screen, so the Viewport
    // does not move for a press on the header (KB-9).
    get #firstVisibleRow(): number {
        return this.#visible?.start ?? 0; // oxlint-disable-line ex-grid/no-stand-in-default -- KB-9: with no row painted the Focus goes on the first row, as the C# core puts it
    }

    // What a press on a Column Heading selects (ADR-0050, item 1; ADR-0052): the whole column, or with
    // Shift whole columns from the Focus's to this one, and then the drag. With Ctrl the column is added as
    // a new range; or, wholly selected, taken out, and no drag begins.
    #beginHeadingDrag(column: number, extendsSelection: boolean, toggles: boolean): void {
        const extent = this.#extent;
        if (extent.rowCount <= 0 || extent.columnCount <= 0) {
            this.#setDragging(false);
            return;
        }
        const current = this.#held.selection;
        const first = this.#firstVisibleRow;
        if (toggles && current.coversColumn(column, extent)) {
            this.#apply(current.toggleColumn(column, extent, first));
            this.#setDragging(false);
            return;
        }
        this.#apply(extendsSelection ? current.extendToColumn(column, extent, first)
            : toggles ? current.toggleColumn(column, extent, first)
                : current.selectColumn(column, extent, first));
        this.#headerPress = undefined;
        this.#headingDrag = true;
    }

    // The pointer moved during a Heading gesture, over the header or the cells: the Extent goes to its
    // column, whatever row it is over. A press becomes a drag when the pointer reaches another column, and
    // from then on never sorts, even released back over the column it started on (ADR-0012). Every event is
    // placed by its client delta from the press, whatever element it fired on: nothing is measured
    // (ADR-0021).
    #moveHeadingDrag(event: MouseEvent): void {
        const press = this.#headingPress;
        const x = press.x + (event.clientX - press.clientX);
        const y = press.y + (event.clientY - press.clientY);
        if (!Number.isFinite(x) || !Number.isFinite(y)) return;
        const column = this.#styles.geometry.columnAt(x + this.#scrollLeftPx, this.#scrollLeftPx);
        if (column === undefined) return;
        const pending = this.#headerPress;
        if (pending !== undefined) {
            if (column === pending.column) return;
            this.#gestureConsumedClick = true;
            this.#beginHeadingDrag(pending.column, pending.extends, pending.toggles);
            this.#invalidate();
            if (!this.#headingDrag) return;
        }
        if (this.#apply(this.#held.selection.extendToColumn(column, this.#extent, this.#firstVisibleRow))) this.#invalidate();
        this.#updateEdgeBand(x, y);
    }

    // ---- The edge band (ADR-0008) -------------------------------------------------------------------------

    // Where a pointer on the Viewport stands in its readable box, down from the top of the rows, or none
    // with no rows painted to read it against.
    #viewportYOf(event: MouseEvent): number | undefined {
        const geometry = this.#geometry;
        const visible = this.#visible;
        return geometry === undefined || visible === undefined
            ? undefined
            : event.offsetY + geometry.offsetPxOf(visible.start) - geometry.contentOffsetAt(this.#scrollTopPx);
    }

    // The edge bands for a pointer at (x, y) in the Viewport's readable box. Sideways the unit is a row
    // height a rate step: columns are of unequal width, and the band has no column unit. A Heading drag
    // scrolls along its Heading's axis only (ADR-0050, item 1).
    #updateEdgeBand(viewportX: number, viewportY: number): void {
        const geometry = this.#geometry;
        if (geometry === undefined || this.#visible === undefined) return;
        const rows = this.#headingDrag ? 0 : edgeBandRowsPerTick(viewportY, geometry.viewportHeightPx, geometry.rowsPerViewport);
        const stepXPx = edgeBandRowsPerTick(viewportX, this.#visibleWidthPx, Number.MAX_SAFE_INTEGER) * this.#metrics.rowHeightPx;
        if (rows === 0 && stepXPx === 0) {
            this.#stopEdgeTimer();
            return;
        }
        this.#edge = { x: viewportX, y: viewportY, rows, stepXPx };
        this.#cancelEdgeTick ??= this.#reports.clock.setTimer(this.#onEdgeTick, edgeScrollTickMs);
    }

    #stopEdgeTimer(): void {
        this.#cancelEdgeTick?.();
        this.#cancelEdgeTick = undefined;
        this.#edge = undefined;
    }

    // A tick: the model moves a step first, so the ticks compound deterministically, and the pass writes
    // the offset with the rows it paints there. The pointer has not moved; the cell under it has, and the
    // Extent follows it. The rate stays under the fling threshold, so the rows never turn to Placeholders.
    readonly #onEdgeTick = (): void => {
        this.#cancelEdgeTick = undefined;
        const edge = this.#edge;
        if (this.#destroyed || !this.#dragging || edge === undefined || this.#geometry === undefined) return;
        this.#cancelEdgeTick = this.#reports.clock.setTimer(this.#onEdgeTick, edgeScrollTickMs);
        // No rows to step is no vertical move at all: through a compressed mapping a step of nothing could
        // come back an ulp away, and the rows would drift under a Heading drag (ADR-0053).
        const top = edge.rows === 0 ? this.#scrollTopPx
            : this.#geometry.scrollTopStepped(this.#scrollTopPx, edge.rows * this.#metrics.rowHeightPx);
        const left = Math.min(Math.max(this.#scrollLeftPx + edge.stepXPx, 0), this.#styles.geometry.maxScrollLeftPx);
        const moved = top !== this.#scrollTopPx || left !== this.#scrollLeftPx;
        if (moved) {
            this.#scrollTopPx = top;
            this.#scrollLeftPx = left;
            this.#scrollOwed = true;
            this.#resolve();
        }
        const geometry = this.#geometry;
        const row = geometry.rowAt(geometry.contentOffsetAt(top) + edge.y);
        const column = this.#styles.geometry.columnAt(edge.x + left, left);
        let changed = false;
        if (row !== undefined && column !== undefined) {
            const extent = this.#extent;
            const current = this.#held.selection;
            changed = this.#apply(this.#headingDrag
                ? current.extendToColumn(column, extent, this.#firstVisibleRow)
                : current.extendTo(cellPosition(row, column), extent));
        }
        if (moved || changed) this.#invalidate();
    };

    // The cell under a pointer on the Viewport, by arithmetic on its offsets (ADR-0012): rows counted from
    // the row painted first, columns from the content's left edge.
    #cellUnder(event: MouseEvent): CellPosition | undefined {
        const geometry = this.#geometry;
        const visible = this.#visible;
        if (geometry === undefined || visible === undefined) return undefined;
        const row = geometry.rowAt(event.offsetY + geometry.offsetPxOf(visible.start));
        const column = this.#styles.geometry.columnAt(event.offsetX, this.#scrollLeftPx);
        return row === undefined || column === undefined ? undefined : cellPosition(row, column);
    }

    #cellId(position: CellPosition): string {
        return `${this.#ids}r${position.row}c${position.column}`;
    }

    // Whether a cell is in the DOM now: its row painted with its data, and its column pinned or in the
    // horizontal slice, never mid-fling, when the scrollable cells are skipped (ADR-0004, ADR-0033).
    #cellIsPainted(position: CellPosition): boolean {
        const visible = this.#visible;
        if (visible === undefined || position.row < visible.start || position.row >= visible.start + visible.count) return false;
        const slice = position.row - this.#state.windowStart;
        if (slice < 0 || slice >= this.#state.window.length) return false;
        if (position.column < this.#styles.geometry.pinnedCount) return true;
        const columns = this.#scrollable;
        return !this.#placeholderMode && columns !== undefined
            && position.column >= columns.start && position.column < columns.start + columns.count;
    }

    // Moves the model to where a key's reveal is going, and resolves the slice there, so the pass paints
    // the rows at the offset it then writes (ADR-0012, 2026-09-29). Whether there is an offset to write.
    #stageReveal(): boolean {
        const reveal = this.#revealOwed;
        this.#revealOwed = undefined;
        const across = this.#revealHeaderOwed;
        this.#revealHeaderOwed = undefined;
        const geometry = this.#geometry;
        const columns = this.#styles.geometry;
        // A header Find found is scrolled into view across only (ADR-0333 §1): its last column, then its first,
        // so a header wider than the Viewport shows its start. A pinned header is in view already.
        if (reveal === undefined && across !== undefined && geometry !== undefined) {
            let left = this.#scrollLeftPx;
            if (across.last < columns.count) left = columns.scrollLeftToReveal(across.last, left);
            if (across.first < columns.count) left = columns.scrollLeftToReveal(across.first, left);
            return this.#revealAt(this.#scrollTopPx, left);
        }
        if (reveal === undefined || geometry === undefined) return false;
        const { target } = reveal;
        if (target.row >= this.#totalRows || target.column >= columns.count) return false;
        // PageUp and PageDown scroll by the rows the Focus moved, through the mapping, so a compressed grid
        // moves by rows (ADR-0053); the reveal still runs over the stepped offset.
        const stepped = reveal.stepRows === 0
            ? this.#scrollTopPx
            : geometry.scrollTopStepped(this.#scrollTopPx, reveal.stepRows * this.#metrics.rowHeightPx);
        let top = geometry.scrollTopToReveal(target.row, stepped);
        // "Go to the beginning" is not "make this cell visible": with Pinned Columns the two come apart.
        let left = reveal.rowStart ? 0 : columns.scrollLeftToReveal(target.column, this.#scrollLeftPx);
        // While extending, an axis the range spans end to end is not scrolled for (ADR-0052, 2026-09-29).
        const range = reveal.extending;
        if (range !== undefined) {
            const extent = this.#extent;
            if (spansEveryRow(range, extent)) top = this.#scrollTopPx;
            if (spansEveryColumn(range, extent) && !reveal.rowStart) left = this.#scrollLeftPx;
        }
        return this.#revealAt(top, left);
    }

    // Moves the offsets a reveal asked for, whether it moved anything.
    #revealAt(top: number, left: number): boolean {
        if (top === this.#scrollTopPx && left === this.#scrollLeftPx) return false;
        // A reveal that moves the rows under a still pointer takes the band, as a scroll does; this pass paints
        // it gone.
        if (top !== this.#scrollTopPx) this.#hoverRow = undefined;
        this.#scrollTopPx = top;
        this.#scrollLeftPx = left;
        this.#revealToken += 1;
        this.#repaint?.cancel();
        this.#repaint = undefined;
        this.#resolve();
        return true;
    }

    // A reveal's repaint (ADR-0012, 2026-10-08): under Citrix with hardware acceleration off, a far reveal
    // left the Viewport white until the offset moved once more. So the offset moves a pixel in the frame
    // after the reveal's and back in the frame after that. The grid hears nothing of it (#onScroll), and a
    // scroll the user makes in between stands: the move back is not made, and the offset is read again. The
    // move back writes the value the reveal wrote, on the moved axis only.
    #repaintAfterReveal(writtenTop: number, writtenLeft: number): void {
        const scroller = this.#scroller;
        const current: Repaint = { top: scroller.scrollTop, left: scroller.scrollLeft, cancel: () => { /* no frame asked yet */ } };
        this.#repaint = current;
        current.cancel = this.#reports.nextFrame(this.#root, () => {
            if (this.#repaint !== current) return;
            if (scroller.scrollTop !== current.top || scroller.scrollLeft !== current.left) {
                this.#yieldRepaint();
                return;
            }
            // Away from the edge the offset stands at, so the move is never clamped to nothing; across, where
            // the rows cannot scroll at all.
            scroller.scrollTop = current.top + (current.top > 0 ? -1 : 1);
            const across = scroller.scrollTop === current.top;
            if (across) scroller.scrollLeft = current.left + (current.left > 0 ? -1 : 1);
            const moved = { top: scroller.scrollTop, left: scroller.scrollLeft };
            if (moved.top === current.top && moved.left === current.left) {
                this.#endRepaint();
                return;
            }
            current.cancel = this.#reports.nextFrame(this.#root, () => {
                if (this.#repaint !== current) return;
                if (scroller.scrollTop !== moved.top || scroller.scrollLeft !== moved.left) {
                    this.#yieldRepaint();
                    return;
                }
                if (across) scroller.scrollLeft = writtenLeft;
                else scroller.scrollTop = writtenTop;
                this.#endRepaint();
            });
        });
    }

    // The offset is read once more: a write the browser clamped stands where it landed, not where it was
    // asked to.
    #endRepaint(): void {
        this.#repaint = undefined;
        this.#onScroll();
        this.#lookSettled();
    }

    // The user scrolled during the repaint: their offset stands, and is read.
    #yieldRepaint(): void {
        this.#repaint = undefined;
        this.#onScroll();
        this.#lookSettled();
    }

    // Numbers a page follow once the cell it brings in is painted (ADR-0204), and asks the browser after
    // the frame that paints it, and after a reveal's repaint. One whose Focus has moved on is dropped.
    #stageFollow(): void {
        const follow = this.#followArmed;
        if (follow === undefined) return;
        const focus = this.#held.selection.focus;
        if (focus === undefined || !sameCell(focus, follow.focus)) {
            this.#followArmed = undefined;
            return;
        }
        if (!this.#cellIsPainted(follow.target)) return;
        this.#followArmed = undefined;
        this.#followToken += 1;
        this.#cancelFollow?.();
        const afterPaint = (): void => {
            let cancelTimer: (() => void) | undefined;
            const cancelFrame = this.#reports.nextFrame(this.#root, () => {
                cancelTimer = this.#reports.clock.setTimer(() => {
                    if (this.#repaint !== undefined) {
                        afterPaint();
                        return;
                    }
                    this.#cancelFollow = undefined;
                    this.#follow(follow);
                    this.#lookSettled();
                }, 0);
            });
            this.#cancelFollow = () => {
                cancelFrame();
                cancelTimer?.();
            };
        };
        afterPaint();
    }

    // The browser places, the grid does not measure (ADR-0204): the page moves the cell into the window at
    // the nearest edge, only while the keyboard is still this grid's and the Focus is the one the follow
    // was taken for. The grid's own scroller is the grid's, and is put back if the browser moved it.
    #follow(follow: Follow): void {
        const focus = this.#held.selection.focus;
        if (this.#destroyed || focus === undefined || !sameCell(focus, follow.focus) || !this.#keyboardHere()) return;
        const element = this.#root.querySelector(`#${CSS.escape(this.#cellId(follow.target))}`);
        if (element === null) return;
        const scroller = this.#scroller;
        const top = scroller.scrollTop;
        const left = scroller.scrollLeft;
        element.scrollIntoView({ block: 'nearest', inline: 'nearest' });
        if (scroller.scrollTop !== top) scroller.scrollTop = top;
        if (scroller.scrollLeft !== left) scroller.scrollLeft = left;
    }

    // DOM focus inside this root and not in a grid nested in it, or on nothing; or, while the keyboard is
    // moving, where it is going.
    #keyboardHere(toward?: Element): boolean {
        const active = toward ?? this.#root.ownerDocument.activeElement;
        const doc = this.#root.ownerDocument;
        return active === null || active === doc.body || active === doc.documentElement
            || (this.#root.contains(active) && active.closest('.ex-grid') === this.#root);
    }

    // The selection layers' rectangles over the painted rows, written only when they changed. The layers
    // the Viewport holds first, in order.
    #paintLayers(): readonly HTMLElement[] {
        const visible = this.#visible;
        const geometry = this.#geometry;
        // What lies above the layer at its edges (ADR-0008, 2026-10-01): the first row whose top edge is below
        // the readable area's top, and the offset that takes a column under the pinned block.
        const firstOpenRow = visible === undefined || geometry === undefined ? -1
            : geometry.viewportTopPxOf(visible.start, this.#scrollTopPx) > 0.01 ? visible.start : visible.start + 1;
        const inputs = {
            selection: this.#held.selection, copied: this.#copied, start: visible?.start, count: visible?.count, styles: this.#styles,
            rowHeightPx: this.#metrics.rowHeightPx, scrollLeftPx: this.#scrollLeftPx, firstOpenRow,
            // A row the source has since taken away has no band; the next movement finds the row there now.
            hoverRow: this.#hoverRow !== undefined && this.#hoverRow < this.#totalRows ? this.#hoverRow : undefined,
            focusRow: this.#highlightFocusRow,
        };
        const was = this.#layerInputs;
        // A pass that moved none of them, every scroll frame along an axis the layers ignore, writes nothing.
        if (was === undefined || Object.entries(inputs).some(([name, value]) => was[name] !== value)) {
            this.#layerInputs = inputs;
            const pieces = visible === undefined || geometry === undefined
                ? { shade: undefined, pinned: [], scrollable: [] }
                : selectionLayers(inputs.selection, inputs.copied === undefined ? [] : inputs.copied.rectangles, this.#styles.geometry, inputs.rowHeightPx, visible,
                    { firstOpenRow, scrollLeftPx: this.#scrollLeftPx, hasHeader: true }, { hoverRow: inputs.hoverRow, focusRow: inputs.focusRow });
            const signature = JSON.stringify(pieces);
            if (signature !== this.#paintedLayers) {
                this.#paintedLayers = signature;
                const fill = (layer: HTMLElement, held: readonly LayerPiece[]): void => {
                    layer.replaceChildren(...held.map(({ className, style }) => {
                        const made = this.#root.ownerDocument.createElement('div');
                        made.className = className;
                        made.setAttribute('style', style);
                        return made;
                    }));
                };
                if (pieces.shade !== undefined) fill(this.#shadeLayer, pieces.shade);
                fill(this.#pinnedLayer, pieces.pinned);
                fill(this.#selectionLayer, pieces.scrollable);
                this.#shadeHolds = pieces.shade !== undefined;
            }
        }
        return this.#shadeHolds ? [this.#shadeLayer, this.#pinnedLayer, this.#selectionLayer] : [this.#pinnedLayer, this.#selectionLayer];
    }

    // ---- Painting -------------------------------------------------------------------------------------

    /** Writes an attribute only when it changed, so an unchanged pass touches nothing. */
    #write(element: HTMLElement, key: string, name: string, value: string | undefined): void {
        const slot = `${key}:${name}`;
        if (this.#written.get(slot) === value) return;
        if (value === undefined) {
            this.#written.delete(slot);
            element.removeAttribute(name);
        } else {
            this.#written.set(slot, value);
            element.setAttribute(name, value);
        }
    }

    #paint(): void {
        // What is painted now is what the next key is aimed at.
        this.#droppedAim = undefined;
        const metrics = this.#metrics;
        const geometry = this.#styles.geometry;
        const root = this.#root;
        this.#write(root, 'root', 'class', this.#state.loading ? 'ex-grid ex-loading' : 'ex-grid');
        // The Geometry Tokens, inline on the root from the same resolved metrics the arithmetic reads
        // (ADR-0027, ADR-0028), and the Device Pixel once told (ADR-0090).
        this.#write(root, 'root', 'style', `--ex-row-height: ${metrics.rowHeightPx}px; --ex-header-height: ${metrics.headerHeightPx}px; `
            + `--ex-font-size: ${metrics.fontSizePx}px; --ex-cell-padding-x: ${metrics.cellPaddingXPx}px; `
            + `--ex-action-padding-x: ${metrics.actionPaddingXPx}px; --ex-action-border-width: ${metrics.actionBorderPx}px; `
            + `--ex-action-gap: ${metrics.actionGapPx}px; --ex-menu-button-width: ${metrics.menuButtonWidthPx}px; `
            + `--ex-menu-button-inset: ${metrics.menuButtonInsetPx}px; --ex-sort-mark-width: ${metrics.sortMarkWidthPx}px; `
            + `--ex-resize-grip-width: ${metrics.resizeGripWidthPx}px; --ex-column-guide-width: ${metrics.columnGuideWidthPx}px`
            + stretchRootStyle(this.#width, this.#height)
            // The room at the root's top the Formula Bar stands in: the scroller starts below it (ADR-0051).
            + (this.#barPx === 0 ? '' : `; padding-top: ${this.#barPx}px; box-sizing: border-box`)
            + (this.#devicePixelRatio === undefined ? '' : `; --ex-dp: ${1 / this.#devicePixelRatio}px`));
        this.#write(root, 'root', 'aria-rowcount', String(this.#totalRows + this.#headerRowCount));
        this.#write(root, 'root', 'aria-colcount', String(this.#columns.length));
        // The numbers a reveal and a page follow are known by (ADR-0012, ADR-0204), the Focus a follow
        // was taken for, painted or not, and the Focus cell for assistive technology only while it is in
        // the DOM, so the attribute never names a missing id (ADR-0033).
        this.#write(root, 'root', 'data-ex-reveal', String(this.#revealToken));
        this.#write(root, 'root', 'data-ex-follow', String(this.#followToken));
        const focus = this.#held.selection.focus;
        this.#write(root, 'root', 'data-ex-follow-focus', focus === undefined ? undefined : this.#cellId(focus));
        // The element holding the keyboard carries it: the Keyboard Field on a grid that edits, which is then
        // the one tab stop, and the root otherwise (ADR-0080).
        const keyFieldShown = this.#canEdit;
        // Interactive over several actions, it names the chosen button, which assistive technology reads by the
        // label every action carries, as it would under real focus (ADR-0037).
        const choice = this.#interactiveChoice();
        const active = focus === undefined || !this.#cellIsPainted(focus) ? undefined
            : choice === undefined ? this.#cellId(focus) : `${this.#cellId(focus)}a${choice.action}`;
        this.#write(root, 'root', 'tabindex', keyFieldShown ? '-1' : '0');
        this.#write(root, 'root', 'aria-activedescendant', keyFieldShown ? undefined : active);
        this.#write(this.#keyField, 'keyField', 'aria-activedescendant', keyFieldShown ? active : undefined);
        this.#write(this.#stamp, 'stamp', 'data-ex-answered', String(this.#answered));
        // Whether an Escape on the root would leave the grid now: not while it would leave an Interactive cell
        // or peel the Copied Range (ADR-0230 section 1, ADR-0037, ADR-0170).
        this.#write(this.#stamp, 'stamp', 'data-ex-escape-leaves',
            choice === undefined && this.#copied === undefined && this.#edit === undefined && this.#popover === undefined ? '' : undefined);
        this.#write(this.#scroller, 'scroller', 'style', this.#scrollerStyle());
        const scrollHeightPx = this.#geometry?.scrollHeightPx ?? 0; // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0013: a Viewport with no room for a row scrolls nothing
        this.#write(this.#spacer, 'spacer', 'style', `width: ${geometry.totalWidthPx}px; height: ${this.#bandHeightPx + scrollHeightPx}px`);
        // A current match the user has moved off is no longer marked as one (ADR-0332 §4).
        this.#forgetCurrentMatchOffFocus();
        this.#paintHeader();
        this.#paintViewport();
        this.#paintStatus();
        this.#paintMessage();
        this.#paintFormulaBar();
        this.#placePopover();
    }

    // A right-click on the status line opens the figures menu where a Consumer hears the choice (ADR-0130), and is
    // the browser's otherwise. The grid's popover frame holds it, so it closes as any popover does.
    // An open edit commits first, as for the Context Menu (ADR-0036), and one held by a Reject keeps the keyboard.
    readonly #onSummaryContextMenu = (event: MouseEvent): void => {
        if (this.#destroyed || this.#onSummaryFiguresChanged === undefined) return;
        event.preventDefault();
        event.stopPropagation();
        this.#answer();
        if (!this.#commitEdit()) return;
        const menu = new SummaryMenu(this.#root.ownerDocument, figure => { this.#toggleSummaryFigure(menu, figure); });
        menu.paint(this.#summaryFigures, id => this.#labelOf(id), this.#commandLabel);
        this.#standFrame(() => menu.element, this.#labelOf(summaryLabelIds.menu), true);
        this.#summaryMenu = menu;
        this.#summaryChoice = undefined;
    };

    // A press in the figures menu reports the figures with that one ticked or unticked. Presses come faster than a
    // Consumer may hand the figures back, so each builds on the choice last reported until the Consumer hands figures
    // that differ from the ones shown: two presses report the same choices however the Consumer is timed (the spine's
    // sixth rule).
    #toggleSummaryFigure(menu: SummaryMenu, figure: SummaryFigure): void {
        this.#answer();
        const heard = this.#onSummaryFiguresChanged;
        if (!menu.standing || heard === undefined) return;
        const choice = toggledFigures(this.#summaryChoice ?? this.#summaryFigures, figure);
        this.#summaryChoice = choice;
        heard(choice);
    }

    // The status line after the rows, outside the scroller (ADR-0014): the header's checkbox and the marks' count
    // from the counts the Consumer answers, standing while it says something (ADR-0043), and the Selection
    // Summary, standing whenever the grid can summarise, figures or none, so the strip does not come and go with
    // every selection (ADR-0130).
    #paintStatus(): void {
        const count = this.#rowMarks.marks === undefined ? undefined : this.#rowMarks.paint(id => this.#labelOf(id));
        this.#reviewSummary();
        const shown: HTMLElement[] = [];
        if (count !== undefined) shown.push(count);
        if (this.#showSelectionSummary && this.canSummarize) {
            const summary = this.#summary.summary;
            const focus = this.#held.selection.focus;
            const column = focus === undefined ? undefined : this.#columns[focus.column]?.column;
            this.#summaryBox.paint(summary, summaryFiguresShown(summary.result, column), id => this.#labelOf(id));
            this.#summaryBox.claimMenu(this.#onSummaryFiguresChanged !== undefined);
            shown.push(this.#summaryBox.element);
        }
        // The menu's ticks are the figures handed back, which a press on it does not change by itself. It goes with
        // the line it was opened on, or with the Consumer that heard it, rather than stand with presses that do nothing.
        const menu = this.#summaryMenu;
        if (menu?.standing !== true) {
            this.#summaryMenu = undefined;
        } else if (this.#onSummaryFiguresChanged === undefined || !shown.includes(this.#summaryBox.element)) {
            this.#summaryMenu = undefined;
            const popover = this.#popover;
            if (popover?.kind === 'consumer' && popover.element.contains(menu.element)) this.#closePopover(true);
        } else {
            menu.paint(this.#summaryFigures, id => this.#labelOf(id), this.#commandLabel);
        }
        if (!sameItems(shown, this.#statusShown)) {
            this.#status.replaceChildren(...shown);
            this.#statusShown = shown;
        }
        if (shown.length === 0) {
            this.#status.remove();
        } else if (!this.#status.isConnected) {
            this.#root.insertBefore(this.#status, this.#announce);
        }
    }

    /** Whether anything can answer a Selection Summary (ADR-0130): `onSummarize`, or a source that summarises.
     * False: no figure ever appears. */
    get canSummarize(): boolean {
        return this.#onSummarize !== undefined || this.#source.summarize !== undefined;
    }

    get #summaryAnswerer(): SummaryAnswerer | undefined {
        if (this.#onSummarize !== undefined) return this.#onSummarize;
        const source = this.#source;
        if (source.summarize === undefined) return undefined;
        return async (request, signal) => {
            if (source.summarize === undefined) throw new Error('Internal: a source that summarised no longer does.');
            return source.summarize(request, signal);
        };
    }

    // The question the grid's state asks (ADR-0130), run as the status line paints: a different one from the question
    // in force clears the figures in this very pass and owes the asking to the pass's end.
    #reviewSummary(): void {
        const selection = this.#held.selection;
        const asks = this.canSummarize && (this.#showSelectionSummary || this.#onSelectionSummaryChanged !== undefined)
            && this.#summaryFigures.length > 0 && selectsTwoCells(selection.ranges);
        this.#summary.review(asks ? {
            ranges: selection.ranges, focus: selection.focus, binding: this.#source, rowSequenceVersion: this.#state.rowSequenceVersion,
            columns: this.#columnNames, figures: this.#summaryFigures, rowsStamp: this.#summaryRowsStamp,
        } : undefined);
    }

    /**
     * Tells the grid that the values under the selection may have moved where it cannot see them, a change outside
     * the Window or a recalculation (ADR-0130): the figures clear at once and the question is asked again. Nothing
     * happens while no figure stands or is being asked for.
     */
    refreshSummary(): void {
        if (this.#destroyed || this.#summary.standing === undefined) return;
        this.#askSummaryAgain();
    }

    // The values summed may have moved, an edit the grid handed over or a change it was told of: the next pass clears
    // the figures and asks again.
    #askSummaryAgain(): void {
        this.#summaryRowsStamp += 1;
        this.#invalidate();
    }

    #scrollerStyle(): string {
        const width = this.#width;
        const height = this.#height === 'stretch' ? 'stretch' : this.#height - this.#barPx;
        if (width === 'stretch' && height === 'stretch') return 'flex: 1 1 auto; min-height: 0';
        if (width === 'stretch') return `height: ${height}px`;
        if (height === 'stretch') return `width: ${width}px; flex: 1 1 auto; min-height: 0`;
        return `width: ${width}px; height: ${height}px`;
    }

    #paintHeader(): void {
        const painted = this.#paintedHeader;
        const layout = this.#headerLayout;
        const tierPx = this.#metrics.headerHeightPx;
        // The Sort in force is painted too, as each header's aria-sort (ADR-0033).
        const sorts = this.#state.sorts.map(({ column, direction }) => `${JSON.stringify(column)}:${direction}`).join(',');
        // A column has a grip at its header's right edge while a resize is heard (ADR-0016).
        const grips = this.#onColumnWidthChanged !== undefined;
        // And a ▾ at its right, unless the Consumer hides the column menu (ADR-0044).
        const menus = !this.#hideColumnMenu;
        if (painted !== undefined && painted.styles === this.#styles && painted.columns === this.#columns
            && sameRange(painted.scrollable, this.#scrollable) && painted.sorts === sorts && painted.layout === layout
            && painted.tierPx === tierPx && painted.grips === grips && painted.menus === menus) {
            this.#paintHeadingMarks(false);
            return;
        }
        this.#paintedHeader = { styles: this.#styles, columns: this.#columns, scrollable: this.#scrollable, sorts, layout, tierPx, grips, menus };
        this.#grips = new Map();
        this.#menuButtons = new Map();
        const doc = this.#root.ownerDocument;
        const tiered = layout.tierCount > 0;
        const header = this.#header;
        // Untiered, the header is the one row it always was. Tiered, it holds a row per tier of Header Groups,
        // top first, and the leaf row below them; each is display: contents, so the band lays out, paints and
        // stacks as one element of cells and rectangles would, and only the tree says rows (ADR-0032/0033).
        this.#write(header, 'header', 'role', tiered ? 'rowgroup' : 'row');
        this.#write(header, 'header', 'aria-rowindex', tiered ? undefined : '1');
        this.#write(header, 'header', 'style', tiered ? `height: ${this.#bandHeightPx}px; line-height: ${tierPx}px` : undefined);
        const cells: HTMLElement[] = [];
        const headerCells: HeaderCell[] = [];
        // While tiers stand, a leaf is anchored to the band's bottom edge, as tall as the tiers no
        // rectangle covers above it (ADR-0032).
        const tieredStyle = (c: number, style: string): string => {
            if (!tiered) return style;
            const heightPx = layout.leafTierSpanOf(c) * tierPx;
            return `${style}; align-self: flex-end; height: ${heightPx}px; line-height: ${heightPx}px`;
        };
        const headerCell = (c: number, pinned: boolean): void => {
            const { column, header: label, mark } = itemAt(this.#columns, c);
            const cell = doc.createElement('div');
            cell.className = headerClass(column.headerAlign ?? column.align, pinned, 'none');
            cell.setAttribute('style', tieredStyle(c, pinned ? this.#styles.pinnedCell(c) : this.#styles.cell(c)));
            cell.setAttribute('role', 'columnheader');
            cell.setAttribute('aria-colindex', String(c + 1));
            // Sorted or not, a column that can be sorted says so (ADR-0033).
            if (column.queryable !== false) cell.setAttribute('aria-sort', this.#ariaSort(column.name));
            cell.textContent = label;
            // The header's checkbox is one element for the grid's life, moved into the cell built anew (ADR-0043).
            if (mark) cell.append(this.#rowMarks.headerBox);
            if (menus) {
                // Pressed, never reached by Tab (ADR-0080): a Tab into the grid would otherwise go through every
                // column's ▾ before the first cell. Alt+↓ opens it by key (ADR-0044).
                const button = doc.createElement('button');
                button.type = 'button';
                button.className = 'ex-menu-button';
                button.tabIndex = -1;
                button.setAttribute('aria-label', 'Column menu');
                button.setAttribute('data-ex-claims', 'click');
                cell.append(button);
                this.#menuButtons.set(button, column.name);
            }
            if (grips) {
                const grip = doc.createElement('div');
                grip.className = 'ex-resize-grip';
                // A press and a double click on it are the grip's own, as the C# core marks them (ADR-0240).
                grip.setAttribute('data-ex-claims', 'mousedown dblclick');
                cell.append(grip);
                this.#grips.set(grip, c);
            }
            cells.push(cell);
            headerCells.push({ element: cell, column: c, align: column.headerAlign ?? column.align, pinned });
        };
        for (let c = 0; c < this.#styles.geometry.pinnedCount; c++) headerCell(c, true);
        const scrollable = this.#scrollable;
        if (scrollable !== undefined) {
            if (this.#styles.hasGap(scrollable.start)) cells.push(this.#gap(scrollable.start));
            for (let c = scrollable.start; c < scrollable.start + scrollable.count; c++) headerCell(c, false);
        }
        if (tiered) {
            let tiers = this.#paintedTiers;
            if (tiers === undefined || tiers.styles !== this.#styles || tiers.columns !== this.#columns || tiers.layout !== layout
                || tiers.tierPx !== tierPx) {
                const groups: { readonly element: HTMLElement; readonly id: string }[] = [];
                const rows = Array.from({ length: layout.tierCount }, (_, headerRow) => this.#tierRow(headerRow, groups));
                const leaves = doc.createElement('div');
                leaves.setAttribute('role', 'row');
                leaves.setAttribute('aria-rowindex', String(layout.tierCount + 1));
                leaves.setAttribute('style', 'display: contents');
                tiers = { styles: this.#styles, columns: this.#columns, layout, tierPx, rows, leaves, groups };
                this.#paintedTiers = tiers;
                header.replaceChildren(...rows, leaves);
            }
            tiers.leaves.replaceChildren(...cells);
        } else {
            this.#paintedTiers = undefined;
            header.replaceChildren(...cells);
        }
        this.#headerCells = headerCells;
        this.#paintHeadingMarks(true);
    }

    // One tier's row of Header Groups, the top tier first (ADR-0032). A rectangle stands from the geometry,
    // absolutely, so it is correct whether or not its leaves are in the DOM; one inside the pinned block
    // rides a zero-size sticky anchor held at the band's left edge, as the pinned header cells are.
    #tierRow(headerRow: number, painted: { readonly element: HTMLElement; readonly id: string }[]): HTMLElement {
        const doc = this.#root.ownerDocument;
        const layout = this.#headerLayout;
        const tier = layout.tierCount - headerRow;
        const row = doc.createElement('div');
        row.setAttribute('role', 'row');
        row.setAttribute('aria-rowindex', String(headerRow + 1));
        row.setAttribute('style', 'display: contents');
        const geometry = this.#styles.geometry;
        // Until the column geometry stands for these columns, as under a stretched axis not yet reported,
        // the band paints no rectangles, as the rows paint nothing (ADR-0028).
        if (geometry.count !== this.#columns.length) return row;
        const held = (group: ResolvedHeaderGroup): boolean => group.pinned && !geometry.isPinningSuspended;
        const groupElement = (group: ResolvedHeaderGroup): HTMLElement => {
            const element = this.#groupElement(group);
            painted.push({ element, id: groupHeaderId(group) });
            return element;
        };
        const inTier = layout.groups.filter(group => group.topTier === tier);
        const pinned = inTier.filter(held);
        if (pinned.length > 0) {
            const anchor = doc.createElement('div');
            anchor.className = 'ex-header-groups-pinned';
            anchor.append(...pinned.map(groupElement));
            row.append(anchor);
        }
        row.append(...inTier.filter(group => !held(group)).map(groupElement));
        return row;
    }

    #groupElement(group: ResolvedHeaderGroup): HTMLElement {
        const geometry = this.#styles.geometry;
        const tierPx = this.#metrics.headerHeightPx;
        const last = group.firstColumn + group.memberCount - 1;
        const leftPx = geometry.offsetPxOf(group.firstColumn);
        const widthPx = geometry.offsetPxOf(last) + geometry.widthPxOf(last) - leftPx;
        const topPx = (this.#headerLayout.tierCount - group.topTier) * tierPx;
        const heightPx = group.tierSpan * tierPx;
        const element = this.#root.ownerDocument.createElement('div');
        element.className = 'ex-header-group';
        element.setAttribute('style', `left: ${leftPx}px; top: ${topPx}px; width: ${widthPx}px; height: ${heightPx}px; line-height: ${heightPx}px`);
        element.setAttribute('role', 'columnheader');
        element.setAttribute('aria-colindex', String(group.firstColumn + 1));
        element.setAttribute('aria-colspan', String(group.memberCount));
        // A cell spans one row unless it says otherwise (ADR-0033).
        if (group.tierSpan > 1) element.setAttribute('aria-rowspan', String(group.tierSpan));
        element.textContent = group.label;
        return element;
    }

    // The Heading Mark over the header (ADR-0008, 2026-10-13): every column holding part of the Selection
    // is marked, and one covered whole is marked so (D1), on the header cell's class; and a rectangle per
    // run of marked columns, over the band's last tier, on whose bottom edge the stylesheet draws the line.
    // Its Pinned Columns' part rides a layer first in the band, emitted only while it holds a mark; the rest
    // follows the header cells and passes beneath the pinned ones, as the columns do (ADR-0004). It is
    // painted over the header, so no row learns the Selection (ADR-0003).
    #paintHeadingMarks(rebuilt: boolean): void {
        const selection = this.#held.selection;
        const extent = this.#extent;
        let marks = this.#marks;
        if (marks === undefined || marks.selection !== selection
            || marks.extent.rowCount !== extent.rowCount || marks.extent.columnCount !== extent.columnCount) {
            marks = { selection, extent, columns: markedColumns(selection), whole: wholeColumnRuns(selection, extent) };
            this.#marks = marks;
        }
        const heightPx = this.#metrics.headerHeightPx;
        const topPx = this.#bandHeightPx - heightPx;
        const matches = this.#headerMatchesNow();
        const painted = this.#paintedMarks;
        if (!rebuilt && painted !== undefined && painted.marks === marks && painted.matches === matches && painted.styles === this.#styles
            && painted.topPx === topPx && painted.heightPx === heightPx) {
            return;
        }

        // The Match Mark rides the same class, and the current header says so to assistive technology as its
        // mark says it to the eye (ADR-0333 §1).
        for (const { element, column, align, pinned } of this.#headerCells) {
            const mark = runsHold(marks.whole, column) ? 'whole' : runsHold(marks.columns, column) ? 'marked' : 'none';
            const match = matches === undefined ? 'none' : itemAt(matches.leaves, column);
            const className = headerClass(align, pinned, mark) + matchMarkClasses[match];
            if (element.className !== className) element.className = className;
            setAttribute(element, 'aria-current', match === 'current' ? 'true' : undefined);
        }
        for (const { element, id } of this.#paintedTiers?.groups ?? []) { // oxlint-disable-line ex-grid/no-stand-in-default -- an untiered header has no group to mark
            const match = matches?.groups.get(id) ?? 'none'; // oxlint-disable-line ex-grid/no-stand-in-default -- a group the marks were not judged for wears none
            const className = `ex-header-group${matchMarkClasses[match]}`;
            if (element.className !== className) element.className = className;
            setAttribute(element, 'aria-current', match === 'current' ? 'true' : undefined);
        }

        const geometry = this.#styles.geometry;
        const stylesIn = (pinnedLayer: boolean): readonly string[] =>
            marks.columns.flatMap(run => present(columnMark(run, geometry, pinnedLayer, topPx, heightPx)));
        const pinnedStyles = stylesIn(true);
        const scrollableStyles = stylesIn(false);
        let elements: { readonly pinned: HTMLElement | undefined; readonly scrollable: readonly HTMLElement[] };
        if (painted !== undefined && sameItems(painted.pinnedStyles, pinnedStyles) && sameItems(painted.scrollableStyles, scrollableStyles)) {
            // The same rectangles: a header rebuilt by a scroll or a Sort takes the marks it had back,
            // rather than making them again (PF-3).
            if (!rebuilt) {
                this.#paintedMarks = { ...painted, marks, matches, styles: this.#styles, topPx, heightPx };
                return;
            }
            elements = painted.elements;
        } else {
            if (painted !== undefined) {
                painted.elements.pinned?.remove();
                for (const element of painted.elements.scrollable) element.remove();
            }
            elements = this.#headingMarkElements(pinnedStyles, scrollableStyles);
        }
        if (elements.pinned !== undefined) this.#header.prepend(elements.pinned);
        this.#header.append(...elements.scrollable);
        this.#paintedMarks = { marks, matches, styles: this.#styles, topPx, heightPx, pinnedStyles, scrollableStyles, elements };
    }

    #headingMarkElements(pinnedStyles: readonly string[], scrollableStyles: readonly string[]): PaintedHeadingMarks['elements'] {
        const doc = this.#root.ownerDocument;
        const mark = (style: string): HTMLElement => {
            const element = doc.createElement('div');
            element.className = 'ex-heading-mark ex-heading-mark-column';
            element.setAttribute('style', style);
            return element;
        };
        let pinned: HTMLElement | undefined;
        if (pinnedStyles.length > 0) {
            pinned = doc.createElement('div');
            pinned.className = 'ex-heading-marks-pinned';
            pinned.setAttribute('aria-hidden', 'true');
            pinned.append(...pinnedStyles.map(mark));
        }
        const scrollable = scrollableStyles.map(style => {
            const element = mark(style);
            element.setAttribute('aria-hidden', 'true');
            return element;
        });
        return { pinned, scrollable };
    }

    #ariaSort(name: string): string {
        const sort = this.#state.sorts.find(s => s.column === name);
        if (sort === undefined) return 'none';
        return sort.direction === 'ascending' ? 'ascending' : 'descending';
    }

    #gap(firstScrollable: number): HTMLElement {
        const gap = this.#root.ownerDocument.createElement('div');
        gap.className = 'ex-gap';
        gap.setAttribute('style', this.#styles.gap(firstScrollable));
        gap.setAttribute('aria-hidden', 'true');
        return gap;
    }

    #paintViewport(): void {
        const viewport = this.#viewport;
        const visible = this.#visible;
        const geometry = this.#geometry;
        if (this.#layoutStyles !== this.#styles) {
            this.#layoutStyles = this.#styles;
            this.#layoutId += 1;
        }
        // Placed from the offset the slice is painted at, on the nearest Device Pixel (ADR-0053, DC-59).
        const topPx = geometry === undefined || visible === undefined ? 0 : geometry.paintedTopPxOf(visible.start, this.#scrollTopPx);
        // The stylesheet's default is one header tier; tiers move where the rows begin, so the band's height
        // travels inline (ADR-0032).
        const transform = `transform: translateY(round(nearest, ${topPx}px, var(--ex-dp, 1px)))`;
        this.#write(viewport, 'viewport', 'style', this.#headerLayout.tierCount === 0 ? transform : `top: ${this.#bandHeightPx}px; ${transform}`);
        this.#write(viewport, 'viewport', 'data-ex-first-row', visible === undefined ? undefined : String(visible.start));
        this.#write(viewport, 'viewport', 'data-ex-sequence', String(this.#handedVersion()));
        this.#write(viewport, 'viewport', 'data-ex-layout', String(this.#layoutId));

        const layers = this.#paintLayers();
        // The Keyboard Field first, then the selection layers, then the Cell Editor, then the rows, as the C#
        // core's markup stands them (ADR-0080, ADR-0010).
        const field = this.#canEdit ? [this.#paintKeyField()] : [];
        const editor = this.#paintEditor();
        const rows: HTMLElement[] = [...field, ...layers, ...editor];
        const next = new Map<unknown, PaintedRow<Row>>();
        const nextPlaceholders = new Map<number, HTMLElement>();
        let repainted = false;
        const doc = viewport.ownerDocument;
        // A control of a Template cell's content holding the keyboard: a row painted again moves its slot,
        // which takes DOM focus off it, so it is given back once the rows stand (ADR-0020).
        const active = doc.activeElement;
        const slotHeld = active instanceof HTMLElement && active !== this.#keyField && active !== this.#edit?.element
            && viewport.contains(active) ? active : undefined;
        // The Template slots this pass paints; every other is taken away after it (ADR-0020).
        const used = new Set<MountedTemplate<Row>>();
        const choice = this.#interactiveChoice();
        if (visible !== undefined) {
            const { window, windowStart } = this.#state;
            const rowKey = this.#source.rowKey;
            const marks = this.#matchMarksNow();
            const found = this.#findCurrent;
            const paint = this.#cellPaint;
            const lookup = paint.cellAppearance;
            const scrollableNow = this.#placeholderMode ? undefined : this.#scrollable;
            if (lookup !== undefined) {
                const columns = this.#columns;
                this.#appearances.begin((row, c) => lookup(row, itemAt(columns, c).column), lookup, paint.edgeBorder, columns,
                    columns.length, this.#styles.geometry.pinnedCount, scrollableNow);
            }
            const rowIn = (at: number): Row | undefined => {
                const slice = at - windowStart;
                return slice < 0 || slice >= window.length ? undefined : itemAt(window, slice);
            };
            for (let index = visible.start; index < visible.start + visible.count; index++) {
                const slice = index - windowStart;
                if (slice < 0 || slice >= window.length) {
                    let placeholder = this.#placeholders.get(index);
                    if (placeholder === undefined) {
                        placeholder = this.#placeholderRow(index);
                        repainted = true;
                    }
                    nextPlaceholders.set(index, placeholder);
                    rows.push(placeholder);
                    continue;
                }
                const row = itemAt(window, slice);
                const key: unknown = rowKey === undefined ? row : rowKey(row);
                const was = this.#painted.get(key);
                const scrollable = this.#placeholderMode ? undefined : this.#scrollable;
                const current = marks === undefined || found?.cell.row !== index ? -1 : found.cell.column;
                const appearance = lookup === undefined ? undefined : this.#appearances.at(index, row, rowIn(index - 1), rowIn(index + 1));
                // Only the row of an Interactive cell is told its chosen action, so that no other row paints
                // again for the choice (ADR-0037, ADR-0003).
                const chosenHere = choice !== undefined && choice.cell.row === index ? { column: choice.cell.column, action: choice.action } : undefined;
                const chosen = chosenHere === undefined ? '' : `${chosenHere.column}:${chosenHere.action}`;
                // Asked for every row painted while a Mark Column stands, and part of what decides whether the row
                // paints again, so a changed mark paints its row and no other (ADR-0043, MK-4).
                const marked = this.#rowMarks.isMarked(row);
                if (was !== undefined && !next.has(key) && was.row === row && was.index === index && was.styles === this.#styles
                    && was.columns === this.#columns && was.placeholder === this.#placeholderMode && sameRange(was.scrollable, scrollable)
                    && paint.paintsRowsAs(was.paint) && was.appearance === appearance && was.chosen === chosen && was.marked === marked) {
                    if (was.marks === marks && was.current === current) {
                        next.set(key, was);
                        for (const slot of was.slots) used.add(slot);
                        rows.push(was.element);
                        continue;
                    }
                    // A new search judges the row's cells again, and paints the row again only where its marks
                    // moved (ADR-0332 §4, ADR-0003).
                    if (this.#matchedIn(row, scrollable, marks, current) === was.matched) {
                        next.set(key, { ...was, marks, current });
                        for (const slot of was.slots) used.add(slot);
                        rows.push(was.element);
                        continue;
                    }
                }
                const rowPaint: RowPaint<Row> = { key, chosen: chosenHere, marked, used, slots: [], appearance };
                const { element, matched } = this.#rowElement(row, index, scrollable, marks, current, rowPaint);
                repainted = true;
                // A key met twice in one Window is painted, never paired: which of the two a later row
                // continues cannot be told (ADR-0140).
                if (!next.has(key)) {
                    next.set(key, {
                        element, row, index, styles: this.#styles, columns: this.#columns, placeholder: this.#placeholderMode, scrollable,
                        marks, current, matched, paint, appearance, chosen, slots: rowPaint.slots, marked,
                    });
                }
                rows.push(element);
            }
        }
        this.#painted = next;
        this.#placeholders = nextPlaceholders;
        if (this.#cellPaint.cellAppearance !== undefined) this.#appearances.end();
        this.#paintAppearanceStyle();
        // In order, moving only what is out of place, so a row that stays is not touched (ADR-0003). A
        // selection layer coming or going paints no cell.
        const isLayer = (node: ChildNode): boolean =>
            node === this.#shadeLayer || node === this.#pinnedLayer || node === this.#selectionLayer || node === this.#keyFieldLayer
            || node === this.#editorPinned || node === this.#edit?.element;
        // The shade layer gone is taken out first, or every row after it would be moved past it.
        if (!layers.includes(this.#shadeLayer)) this.#shadeLayer.remove();
        if (field.length === 0) {
            // A grid that stopped editing hands the keyboard to its root as the field goes, or DOM focus would
            // fall to the page (ADR-0080). Taken once the field is gone, which the root's focus would hand on.
            const held = doc.activeElement === this.#keyField;
            this.#keyFieldLayer.remove();
            // A composition the field took with it is over, whether or not the browser says so.
            this.#composing = false;
            this.#imeKeyTold = false;
            this.#settleKeyField();
            if (held) this.#root.focus({ preventScroll: true });
        }
        // Moving a focused field blurs it: the editor taken into or out of the pinned anchor keeps the keyboard.
        const editorHeld = this.#edit !== undefined && doc.activeElement === this.#edit.element ? this.#edit.element : undefined;
        if (!editor.includes(this.#editorPinned)) this.#editorPinned.remove();
        let at: ChildNode | null = viewport.firstChild;
        for (const row of rows) {
            if (at === row) {
                at = at.nextSibling;
            } else {
                viewport.insertBefore(row, at);
                if (!isLayer(row)) repainted = true;
            }
        }
        while (at !== null) {
            const after: ChildNode | null = at.nextSibling;
            at.remove();
            if (!isLayer(at)) repainted = true;
            at = after;
        }
        if (editorHeld !== undefined && doc.activeElement !== editorHeld) editorHeld.focus({ preventScroll: true });
        if (repainted) this.#paintId += 1;
        this.#write(viewport, 'viewport', 'data-ex-paint', String(this.#paintId));
        this.#paintedUnder = { sequence: this.#sequence(), source: this.#source };
        this.#mountTemplates(used);
        // Given back once the content stands: a control mounted again in its place is a new one, and the keyboard
        // it held comes back to the grid rather than falling to the page.
        if (slotHeld !== undefined && doc.activeElement !== slotHeld) {
            if (slotHeld.isConnected) slotHeld.focus({ preventScroll: true });
            else if (!this.#root.contains(doc.activeElement)) this.#takeKeyboard();
        }
    }

    // The Template cells' content, once the rows stand in the DOM (ADR-0020): a slot no longer painted has its
    // content taken away, a slot owed a mount is mounted, and a focus request is handed to the cell it was
    // made for on the pass that paints it, then gone (ADR-0037).
    #mountTemplates(used: ReadonlySet<MountedTemplate<Row>>): void {
        for (const [key, byColumn] of this.#templates) {
            for (const [name, mounted] of byColumn) {
                if (used.has(mounted)) continue;
                byColumn.delete(name);
                this.#unmount(mounted);
            }
            if (byColumn.size === 0) this.#templates.delete(key);
        }
        const handed = this.#focusRequestHanded(used);
        for (const mounted of used) {
            if (mounted.cell === undefined && mounted !== handed?.mounted) this.#mount(mounted, 0);
        }
        if (handed !== undefined) this.#mount(handed.mounted, handed.number);
    }

    // The slot this pass hands the focus request to, and the request, which is then gone: the Focus's cell,
    // once it is painted. A Focus moved first takes the request with it: nothing may take the keyboard for a
    // cell the user left.
    #focusRequestHanded(used: ReadonlySet<MountedTemplate<Row>>): { readonly mounted: MountedTemplate<Row>; readonly number: number } | undefined {
        const request = this.#focusRequest;
        if (request === undefined) return undefined;
        const focus = this.#held.selection.focus;
        if (focus === undefined || !sameCell(focus, request.cell)) {
            this.#focusRequest = undefined;
            return undefined;
        }
        const held = this.#rowAt(focus.row);
        if (held === undefined || !this.#cellIsPainted(focus)) return undefined;
        this.#focusRequest = undefined;
        const rowKey = this.#source.rowKey;
        const mounted = this.#templates.get(rowKey === undefined ? held.row : rowKey(held.row))?.get(itemAt(this.#columns, focus.column).column.name);
        return mounted !== undefined && used.has(mounted) ? { mounted, number: request.number } : undefined;
    }

    // Mounts a Template cell's content into its slot, taking away what was mounted there first (ADR-0020).
    #mount(mounted: MountedTemplate<Row>, focusRequest: number): void {
        this.#unmount(mounted);
        const cell: GridTemplateCell<Row> = Object.freeze({
            row: mounted.row, column: mounted.column, focusRequest, takeKeyboard: this.#takeKeyboardFor,
        });
        mounted.cell = cell;
        const unmount: unknown = mounted.template(mounted.slot, cell);
        if (!isUnmount(unmount)) {
            throw new TypeError(`Column '${mounted.column}': a template answers the function that takes its content away, not ${typeof unmount} (ADR-0020).`);
        }
        mounted.unmount = unmount;
    }

    #unmount(mounted: MountedTemplate<Row>): void {
        const unmount = mounted.unmount;
        mounted.unmount = undefined;
        mounted.cell = undefined;
        unmount?.();
    }

    // A Template cell's content asking for the keyboard (ADR-0037): granted only while it is still this grid's,
    // DOM focus inside its root or on nothing (ADR-0204). A container hands it to its first enabled text field,
    // with its text selected, or else to its first enabled control.
    readonly #takeKeyboardFor = (control: HTMLElement): void => {
        if (!(control instanceof HTMLElement)) {
            throw new TypeError('takeKeyboard is handed the element that takes the keyboard.');
        }
        // Content taken away since, or a grid gone, has nothing to hand the keyboard to.
        if (this.#destroyed || !control.isConnected) return;
        if (!this.#viewport.contains(control)) {
            throw new RangeError('takeKeyboard is handed a control inside its own cell: this grid hands the keyboard only within its rows.');
        }
        const field = control.matches(focusableControls) ? undefined : control.querySelector<HTMLInputElement | HTMLTextAreaElement>(textFields);
        const target = control.matches(focusableControls) ? control : field ?? control.querySelector<HTMLElement>(focusableControls);
        if (target === null) {
            throw new RangeError('takeKeyboard is handed an element that takes no keyboard and holds no enabled control that does.');
        }
        // Only while the keyboard is still the grid's (ADR-0204).
        if (!this.#keyboardHere()) return;
        target.focus({ preventScroll: true });
        field?.select();
    };

    // The Keyboard Field's box and state (ADR-0080): over the Focus, or the first painted cell with none, and
    // read-only over a cell that typing would not open, so an IME stays off there.
    #paintKeyField(): HTMLElement {
        const focus = this.#held.selection.focus;
        const box = keyFieldBox(focus ?? this.#firstVisibleCell(), this.#styles.geometry, this.#metrics.rowHeightPx, this.#visible);
        this.#write(this.#keyFieldLayer, 'keyFieldLayer', 'class', box.pinned ? 'ex-key-field-layer ex-key-field-held' : 'ex-key-field-layer');
        this.#write(this.#keyField, 'keyField', 'style', box.style);
        this.#write(this.#keyField, 'keyField', 'readonly', focus !== undefined && !this.#isEditableCell(focus) ? '' : undefined);
        return this.#keyFieldLayer;
    }

    // The Cell Editor's Font and Fill (ADR-0050, item 15): the Consumer's editor appearance where it declares one,
    // the cell's own otherwise, so the editor reads like the cell it covers. The Borders stay the cell's.
    #editorLook(cell: CellPosition, openedOn: Row): string | undefined {
        const declared = this.#cellPaint.editorAppearance ?? this.#cellPaint.cellAppearance;
        if (declared === undefined) return undefined;
        const row = this.#rowAt(cell.row)?.row ?? openedOn;
        return this.#appearances.styles.editorClassFor(checkedAppearance(declared(row, itemAt(this.#columns, cell.column).column)));
    }

    // The Cell Editor (ADR-0010): one input floated over its cell, never inside a row, which would break the
    // rows' skipping (ADR-0003). Over a Pinned Column it rides a sticky anchor as the pinned layer does. A
    // row it followed out of view leaves it open and unseen on the first painted row (ADR-0011).
    #paintEditor(): readonly HTMLElement[] {
        const edit = this.#edit;
        if (edit === undefined) return [];
        const { element } = edit;
        const visible = this.#visible;
        if (visible !== undefined) {
            const at = editorCell(edit.cell, visible);
            const box = defined(cellBox(at, this.#styles.geometry, this.#metrics.rowHeightPx, visible), 'the box of a cell among the painted rows');
            const look = this.#editorLook(edit.cell, edit.openedOn);
            const base = editorAway(edit.cell, visible) ? 'ex-editor ex-editor-away' : 'ex-editor';
            setAttribute(element, 'class', look === undefined ? base : `${base} ${look}`);
            setAttribute(element, 'style', box.style);
            setAttribute(element, 'data-ex-cell', `${at.row} ${at.column}`);
        }
        // A Reject or a refusal is said at the editor, which the message describes (ADR-0034).
        setAttribute(element, 'aria-invalid', edit.error === undefined ? undefined : 'true');
        setAttribute(element, 'aria-describedby', edit.error === undefined ? undefined : this.#message.id);
        if (edit.cell.column < this.#styles.geometry.pinnedCount) {
            if (element.parentNode !== this.#editorPinned) this.#editorPinned.append(element);
            return [this.#editorPinned];
        }
        return [element];
    }

    // The message an editor is held with (ADR-0034): below its cell, never over the value it complains about,
    // and only while that cell is painted.
    #paintMessage(): void {
        const edit = this.#edit;
        const geometry = this.#geometry;
        const visible = this.#visible;
        const error = edit?.error;
        if (edit === undefined || error === undefined || geometry === undefined || visible === undefined || editorAway(edit.cell, visible)) {
            this.#message.remove();
            return;
        }
        const width = Math.min(messageMaxWidthPx, this.#visibleWidthPx);
        const left = Math.min(Math.max(0, this.#cellLeftPx(edit.cell.column)), Math.max(0, this.#visibleWidthPx - width));
        const top = this.#bandHeightPx + geometry.viewportTopPxOf(edit.cell.row, this.#scrollTopPx) + this.#metrics.rowHeightPx;
        setAttribute(this.#message, 'style', this.#underBar(`left: ${left}px; top: ${top}px; max-width: ${width}px`));
        if (this.#message.textContent !== error) this.#message.textContent = error;
        if (this.#message.parentNode !== this.#root) this.#scroller.after(this.#message);
    }

    // The editor takes the keyboard once it is painted, while the keyboard is this grid's: on opening, with
    // the caret placed at the end of its text by the core, never left where the browser puts it (ADR-0010).
    // The request waits while the Keyboard Field composes, or a key the IME took there is still being told
    // (ADR-0080); `toward` is where the keyboard is going when the field is being left.
    #focusEditor(toward?: Element): void {
        if (!this.#editorFocusOwed || this.#composing || this.#imeKeyTold) return;
        this.#editorFocusOwed = false;
        const edit = this.#edit;
        if (edit === undefined || !edit.element.isConnected || !this.#keyboardHere(toward)) return;
        // An edit typed in the Formula Bar keeps the keyboard there, its caret where the user put it (ADR-0051).
        if (edit.surface === 'bar' && this.#bar.text.isConnected) {
            if (this.#root.ownerDocument.activeElement !== this.#bar.text) this.#bar.text.focus({ preventScroll: true });
            return;
        }
        const { element } = edit;
        if (this.#root.ownerDocument.activeElement === element) return;
        element.focus({ preventScroll: true });
        element.setSelectionRange(element.value.length, element.value.length);
    }

    // A position with no row behind it at all: not even a Pinned Column has anything to paint, and
    // aria-busy, because a value that has not arrived must not read as one (ADR-0004, ADR-0033).
    #placeholderRow(index: number): HTMLElement {
        const element = this.#root.ownerDocument.createElement('div');
        element.className = rowClasses({ kind: 'detail', placeholder: true });
        element.setAttribute('role', 'row');
        element.setAttribute('aria-rowindex', String(this.#headerRowCount + index + 1));
        element.setAttribute('aria-busy', 'true');
        return element;
    }

    // The generated stylesheet of a per-cell appearance (ADR-0050, item 15; ADR-0071): one rule per Font, per Fill
    // colour and per line on each side, added the first time a cell asks for one, last in the root. Written only
    // when a rule was added.
    #paintAppearanceStyle(): void {
        if (!this.#cellPaint.paintsAppearance) {
            this.#appearanceStyle?.remove();
            return;
        }
        let style = this.#appearanceStyle;
        if (style === undefined) {
            style = this.#root.ownerDocument.createElement('style');
            this.#appearanceStyle = style;
        }
        if (style.parentNode !== this.#root || style !== this.#root.lastChild) this.#root.append(style);
        // Rules are only ever added, so only what was added since is appended: the stylesheet is not parsed whole
        // again for each new rule.
        const { version, css } = this.#appearances.styles;
        if (version !== this.#appearanceVersion) {
            this.#appearanceVersion = version;
            style.append(css.slice(this.#appearanceWritten));
            this.#appearanceWritten = css.length;
        }
    }

    // A row: the Pinned Columns, then one gap for what is virtualised away to the left, then the scrollable
    // columns on screen (ADR-0004). Mid-fling only the pinned cells are painted, and the row says it is busy.
    #rowElement(row: Row, index: number, scrollable: ColumnRange | undefined, marks: MatchMarks<Row> | undefined,
        current: number, paint: RowPaint<Row>): { readonly element: HTMLElement; readonly matched: string } {
        const element = this.#root.ownerDocument.createElement('div');
        element.className = rowClasses({ kind: 'detail', placeholder: this.#placeholderMode });
        element.setAttribute('role', 'row');
        element.setAttribute('aria-rowindex', String(this.#headerRowCount + index + 1));
        if (this.#placeholderMode) element.setAttribute('aria-busy', 'true');
        this.#rowsPainted.set(element, { row, index });
        let matched = '';
        const cell = (c: number, pinned: boolean): HTMLElement => {
            const painted = this.#cell(row, index, c, pinned, marks, current, paint);
            matched += markKey(c, painted.mark);
            return painted.element;
        };
        for (let c = 0; c < this.#styles.geometry.pinnedCount; c++) element.append(cell(c, true));
        if (scrollable !== undefined) {
            if (this.#styles.hasGap(scrollable.start)) element.append(this.#gap(scrollable.start));
            for (let c = scrollable.start; c < scrollable.start + scrollable.count; c++) element.append(cell(c, false));
        }
        return { element, matched };
    }

    // The Match Mark a painted cell wears (ADR-0332 §4): only on a cell Find can land on, which a column with
    // no value of its own to search is not, judged by its displayed text where the caller has it.
    #markOf(row: Row, c: number, marks: MatchMarks<Row> | undefined, current: number, shown?: string): MatchMark {
        const { column } = itemAt(this.#columns, c);
        if (marks === undefined || column.queryable === false || !marks.matches(row, column, shown)) return 'none';
        return c === current ? 'current' : 'match';
    }

    // The marks a row's painted cells wear, written as #rowElement writes them while it paints.
    #matchedIn(row: Row, scrollable: ColumnRange | undefined, marks: MatchMarks<Row> | undefined, current: number): string {
        let matched = '';
        for (let c = 0; c < this.#styles.geometry.pinnedCount; c++) matched += markKey(c, this.#markOf(row, c, marks, current));
        if (scrollable !== undefined) {
            for (let c = scrollable.start; c < scrollable.start + scrollable.count; c++) matched += markKey(c, this.#markOf(row, c, marks, current));
        }
        return matched;
    }

    #cell(row: Row, index: number, c: number, pinned: boolean, marks: MatchMarks<Row> | undefined,
        current: number, paint: RowPaint<Row>): { readonly element: HTMLElement; readonly mark: MatchMark } {
        const { column, numeric: columnNumeric, paintsValue } = itemAt(this.#columns, c);
        if (!paintsValue) {
            const mark = this.#markOf(row, c, marks, current);
            return { element: this.#contentCell(row, index, c, pinned, mark, paint), mark };
        }
        const cellPaint = this.#cellPaint;
        const { appearance } = paint;
        const text = textFor(column, column.value(row));
        const mark = this.#markOf(row, c, marks, current, text);
        // A cell's own kind and alignment, where the Consumer declares them, over its column's (ADR-0050, items 6
        // and 7); a bold cell is judged by the bold widths (ADR-0016).
        const type = cellPaint.cellType === undefined ? column.type : checkedType(cellPaint.cellType(row, column));
        const numeric = cellPaint.cellType === undefined ? columnNumeric : hashesWhenOverflowing(type);
        const metrics = appearance?.isBold(c) === true ? this.#metrics.cellMetrics.bold : this.#metrics.cellMetrics;
        // The declared width, not the one put on Device Pixels: an Auto column is exactly as wide as its
        // widest estimate, and rounding it must not hash the value it grew for (ADR-0090).
        const widthPx = this.#styles.geometry.declaredWidthPxOf(c);
        const painted = cellPaint.paintedText?.(row, column, metrics.contentWidthPx(widthPx), metrics) ?? text;
        const decision = decideOverflow(type, painted, widthPx, metrics);
        const cell = this.#root.ownerDocument.createElement('div');
        const align = cellPaint.cellAlign === undefined ? column.align ?? 'auto' : cellPaint.cellAlign(row, column); // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0006: a column that declares no alignment aligns by its type
        const base = cellClasses({ numeric, pinned, state: 'normal', align, mark });
        const own = appearance?.classOf(c);
        cell.className = own === undefined ? base : `${base} ${own}`;
        cell.setAttribute('style', pinned ? this.#styles.pinnedCell(c) : this.#styles.cell(c));
        cell.setAttribute('role', 'gridcell');
        cell.setAttribute('id', `${this.#ids}r${index}c${c}`);
        cell.setAttribute('aria-colindex', String(c + 1));
        // The accessible name is the value wherever what is painted is not (ADR-0016, ADR-0033).
        if (decision.isHashed || decision.displayText !== text) cell.setAttribute('aria-label', text);
        if (decision.isHashed) {
            const hashes = this.#root.ownerDocument.createElement('span');
            hashes.className = 'ex-hashes';
            hashes.textContent = decision.displayText;
            cell.append(hashes);
        } else {
            cell.textContent = decision.displayText;
        }
        return { element: cell, mark };
    }

    // An Action or Template cell (ADR-0020): the grid's own buttons, or the Consumer's content in a slot kept
    // across the row's rebuilds. It paints no text, so nothing of it is hashed or named by a value.
    #contentCell(row: Row, index: number, c: number, pinned: boolean, mark: MatchMark, paint: RowPaint<Row>): HTMLElement {
        const resolved = itemAt(this.#columns, c);
        const { column, numeric, actions, template } = resolved;
        const mounted = template === undefined ? undefined : this.#templateSlot(row, resolved, template, paint);
        const cell = mounted === undefined ? this.#root.ownerDocument.createElement('div') : mounted.slot;
        cell.className = cellClasses({ numeric, pinned, state: 'normal', align: column.align ?? 'auto', mark }); // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0006: a column that declares no alignment aligns by its type
        cell.setAttribute('style', pinned ? this.#styles.pinnedCell(c) : this.#styles.cell(c));
        cell.setAttribute('role', 'gridcell');
        cell.setAttribute('id', `${this.#ids}r${index}c${c}`);
        cell.setAttribute('aria-colindex', String(c + 1));
        actions.forEach((action, a) => {
            cell.append(this.#actionButton(resolved, action, paint.chosen?.column === c && paint.chosen.action === a ? `${this.#ids}r${index}c${c}a${a}` : undefined));
        });
        if (resolved.mark) {
            const box = rowMarkBox(this.#root.ownerDocument, paint.marked, this.#labelOf(markLabelIds.markRow));
            this.#markBoxes.set(box, paint.marked);
            cell.append(box);
        }
        return cell;
    }

    // One action's button (ADR-0020): the label its text, or its name where a class paints an icon. Never
    // holding the keyboard (ADR-0033, ADR-0037): out of the tab sequence, since the root is the grid's one tab
    // stop, and its press prevented. The chosen one of an Interactive cell is painted as the Focus is, and
    // carries the only id the root ever names a button by.
    #actionButton(column: ResolvedColumn<Row>, action: GridAction, chosenId: string | undefined): HTMLElement {
        const button = this.#root.ownerDocument.createElement('button');
        button.type = 'button';
        button.setAttribute('data-ex-claims', 'click');
        button.className = (chosenId === undefined ? 'ex-action' : 'ex-action ex-action-chosen') + (action.className === undefined ? '' : ` ${action.className}`);
        if (chosenId !== undefined) button.id = chosenId;
        button.tabIndex = -1;
        button.title = action.label;
        // An icon-only button has no text to name it, and title alone is the last resort of the accessible
        // name, ignored by several readers' settings and out of reach of touch.
        if (action.className === undefined) button.textContent = action.label;
        else button.setAttribute('aria-label', action.label);
        this.#actionButtons.set(button, { column, action });
        return button;
    }

    // The slot of a Template cell (ADR-0020): kept by its row's key and its column while it is painted, so a
    // row painted again keeps its content. Owed a mount when it is new, or its row instance or template
    // changed. A key met twice in one Window paints its content once, in the first of its rows (ADR-0140):
    // one slot cannot stand in two places.
    #templateSlot(row: Row, resolved: ResolvedColumn<Row>, template: GridTemplate<Row>, paint: RowPaint<Row>): MountedTemplate<Row> | undefined {
        let byColumn = this.#templates.get(paint.key);
        if (byColumn === undefined) {
            byColumn = new Map();
            this.#templates.set(paint.key, byColumn);
        }
        const name = resolved.column.name;
        let mounted = byColumn.get(name);
        if (mounted !== undefined && paint.used.has(mounted)) return undefined;
        if (mounted === undefined) {
            mounted = { slot: this.#root.ownerDocument.createElement('div'), template, row, column: name, cell: undefined, unmount: undefined };
            byColumn.set(name, mounted);
        } else if (mounted.template !== template || mounted.row !== row) {
            mounted.template = template;
            mounted.row = row;
            mounted.cell = undefined;
        }
        paint.used.add(mounted);
        paint.slots.push(mounted);
        return mounted;
    }
}

const columnTypes: ReadonlySet<string> = new Set(['text', 'number', 'date', 'boolean']);

/** A cell's kind as `cellType` answered it, checked where it arrives: one that is not a kind is refused by name
 * rather than painted as some other (ADR-0050, item 6). */
function checkedType(type: ColumnType): ColumnType {
    const given: unknown = type;
    if (typeof given !== 'string' || !columnTypes.has(given)) {
        throw new TypeError(`cellType answers text, number, date or boolean, not ${String(given)} (ADR-0050, item 6).`);
    }
    return type;
}

/** A Template's answer, checked as an untyped Consumer may give it. */
function isUnmount(value: unknown): value is () => void {
    return typeof value === 'function';
}

/** What takes the keyboard of itself, and the text fields a container hands it to first (ADR-0037). */
const focusableControls = 'input:not([disabled]):not([type="hidden"]), select:not([disabled]), textarea:not([disabled]), '
    + 'button:not([disabled]), a[href], [tabindex]';
const textFields = 'input:not([disabled]):is(:not([type]), [type="text"], [type="search"], [type="email"], [type="tel"], [type="url"], '
    + '[type="password"], [type="number"]), textarea:not([disabled])';

/** A painted cell's Match Mark as a row's record of its marks writes it: nothing for none. */
function markKey(column: number, mark: MatchMark): string {
    return mark === 'none' ? '' : mark === 'current' ? `${column}*,` : `${column},`;
}

/** Whether one of `runs`, disjoint and in order, holds `index`: a binary search, so a header cell's mark
 * costs nothing per heading in the runs (ADR-0008's economy). */
function runsHold(runs: readonly ColumnRange[], index: number): boolean {
    let low = 0;
    let high = runs.length - 1;
    while (low <= high) {
        const middle = low + Math.floor((high - low) / 2);
        const run = itemAt(runs, middle);
        if (index < run.start) high = middle - 1;
        else if (index >= run.start + run.count) low = middle + 1;
        else return true;
    }
    return false;
}

const present = (style: string | undefined): readonly string[] => (style === undefined ? [] : [style]);

/** How the Heading Mark marks a column's header (ADR-0008): not, as holding part of the Selection, or as
 * covered whole by it (D1). */
type HeadingMark = 'none' | 'marked' | 'whole';

/** A header's Match Mark (ADR-0333 §1), after its other classes, as the C# core composes them. */
const matchMarkClasses: Readonly<Record<MatchMark, string>> = {
    none: '', match: ' ex-match-mark', current: ' ex-match-mark ex-match-mark-current',
};

const headingMarkClasses: Readonly<Record<HeadingMark, string>> = {
    none: '', marked: ' ex-heading-marked', whole: ' ex-heading-marked ex-heading-whole',
};

/** A header cell's classes: pinned or not, its alignment and its Heading Mark, as the C# core composes them. */
function headerClass(align: GridColumn<unknown>['align'], pinned: boolean, mark: HeadingMark): string {
    const base = pinned ? 'ex-header-cell ex-pinned' : 'ex-header-cell';
    return (align === undefined || align === 'auto' ? base : `${base} ex-align-${align}`) + headingMarkClasses[mark];
}
