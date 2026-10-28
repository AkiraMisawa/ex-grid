// The grid in the DOM (ADR-0350 sections 2 and 4): the Script Core builds and updates the elements inside
// the instance root itself, to the DOM contract the C# core writes, so ex-grid.css and layer 3 read both
// alike. A row is plain markup and the only unit that is skipped or painted again (ADR-0003); the cells
// are rebuilt with their row.
//
// One pass resolves and paints, as the C# component's render does (PrepareRender, then the markup): what
// the browser told and what the Consumer handed over become the Viewport's slice and the columns on
// screen, and the DOM is brought to them. Anything that changes them asks for a pass, which runs in a
// microtask, so a burst of changes in one task paints once.

import { showCaret } from './caret-shown';
import { AutoWidth } from '../columns/auto-width';
import { assembleCopy, checkedCopyAnswer, fieldCopyFlavours, type CopyFlavours, type GridCopyAnswer, type GridCopyRequest } from '../clipboard/clipboard-data';
import { defaultCellCap, pasteShape, planCopy, planFill, planPaste, type CopyPlan, type FillPlan } from '../clipboard/clipboard-rules';
import { GridClearIntent } from '../clipboard/clear-intent';
import {
    columnPlaceholder, commitCancelledId, copyRefusalLabelIds, errorStepMovedId, errorStepNoneId, heldInputLostId, pasteRefusalLabelIds, replaceCellChangedId, rowCommandRefusalId,
    shapeMismatchMaySpillLabelId,
} from '../chrome/refusal-words';
import { builtInCommandLabel } from '../chrome/command-labels';
import { summaryLabelIds } from '../chrome/summary-words';
import { markLabelIds } from '../chrome/mark-words';
import { selectionLabelIds } from '../chrome/selection-words';
import { findOutcomeText, type FindOutcome, type FindPanelRefusedCell } from '../chrome/find-panel-labels';
import { gridCommandIds, gridLabelIds, reloadNoticeLabelIds } from '../chrome/command-ids';
import { filledWords } from '../chrome/filled-words';
import { letterOf, menuLetter, resolveColumnMenuKey, resolveMenuKey } from '../chrome/menu-keys';
import { canApply } from '../chrome/filter-panel-choices';
import { FilterPanelModel } from '../chrome/filter-panel-model';
import { gridCulture, invariantLocale, type GridCulture } from '../chrome/grid-culture';
import type { TemporalLike } from '../chrome/operand-reading';
import { filterOperatorsFor, type FilterSpec } from '../query/filter';
import { parseClipboardBlock, type ClipboardBlock } from '../clipboard/clipboard-parse';
import { GridPasteIntent } from '../clipboard/paste-intent';
import { ColumnGeometry, type ColumnRange } from '../columns/column-geometry';
import { HeaderGroup, HeaderGroupLayout, type HeaderGroupToggle, type HiddenMark, type ResolvedHeaderGroup } from '../columns/header-groups';
import { reorderColumns } from '../columns/header-reorder';
import { decideOverflow } from '../columns/overflow-rules';
import { changeHighlightShowingUntil } from '../cells/change-highlight';
import { entryFor, rawText, textFor } from '../cells/display-text';
import { checkedVerdict, GridEditIntent, type EditDiscardReason, type EditVerdict, type GridOverwriteNotice } from '../cells/edit-intent';
import {
    cancelsPending, elementDefault, eventAgain, heldEventTypes, heldInputOf, isClipboardKey, keyDefault, selectionOf, typedField, typeText,
    type HeldInput,
} from './pending-commit';
import { GridMetrics, type GridDensity } from '../grid-metrics';
import { GridPresentationDefaults } from '../grid-presentation-defaults';
import { CellTextMetrics } from '../columns/cell-text-metrics';
import { matcherOf, patternOf, type GridFindPattern, type GridFindRequest, type GridFindResult } from '../finding/find';
import { columnHeaderId, findHeaders, groupHeaderId, matchingHeaders, sameHeaders, type FindHeader } from '../finding/header-find';
import { headerAfterAnswer, headerBeforeAsking } from '../finding/header-find-walk';
import { replacedEntry, type GridReplaceRequest, type GridReplaceResult } from '../finding/replace';
import { defined, elementAt, itemAt, unreachable } from '../must';
import { editingKeysByMode } from '../keys/editing-keys';
import {
    canonicalKey, copiesWithHeaders, copyWithHeadersKeys, declareKeys, errorStepOf, headerGroupKeyOf, headerGroupKeys, resolveKey, type GridKeyAction,
    type KeyPress,
} from '../keys/grid-keys';
import type { GridErrorStepRequest } from '../cells/grid-ruleset';
import { cellClasses, type CellTone, type MatchMark } from '../paint/cell-classes';
import { columnMark, rowMark } from '../paint/heading-mark-styles';
import { rowClasses, type RowKind } from '../paint/row-classes';
import { rowMarkHeaderPress, rowMarkHeaderState, type MarkRefusalReason, type RowMarkIntent, type RowMarks } from '../rows/row-marks';
import { cell as cellPosition, sameCell, type CellPosition, type GridDirection, type GridExtent } from '../selection/cell';
import { markedColumns, markedRows, wholeColumnRuns, wholeRowRuns } from '../selection/heading-marks';
import { edgeBandRowsPerTick } from '../selection/edge-band';
import { HeldSelection } from '../selection/held';
import { bottomRow, boundingRange, fromCorners, rangeCellCount, rangeContains, rightColumn, sameRange as sameSelectionRange, selectionRange, spansEveryColumn, spansEveryRow, type SelectionRange } from '../selection/range';
import { GridFillIntent } from '../selection/fill-intent';
import { fillExtensionFor, fillHandleCorner, fillHandleRange, planHandleFill, type FillHandleCorner } from '../selection/fill-rules';
import { Selection, type EdgeAnswer, type GridSelection } from '../selection/selection';
import { nextSort } from '../query/sort';
import type { GridSource, GridSourceState } from '../source/grid-source';
import { InMemoryGridSource } from '../source/in-memory-source';
import type { GridSummaryRequest, GridSummaryResult } from '../summary/grid-summary';
import { isSummaryFigure, summaryFigureOrder, defaultSummaryFigures, type SummaryFigure } from '../summary/summary-accumulator';
import { keyOf, type RowKey } from '../source/row-key';
import { rowRange, type RowRange } from '../viewport/row-range';
import { ViewportBox } from '../viewport/viewport-box';
import { PageGeometry } from '../viewport/page-geometry';
import { ViewportGeometry } from '../viewport/viewport-geometry';
import type { BrowserReports, ViewportReport } from './browser-reports';
import { ClipboardLedger, type LedgerWrite } from './clipboard-ledger';
import { ColumnStyles } from './column-styles';
import { cellBox, editorAway, keyFieldBox, opensAnEdit } from './cell-editing';
import {
    boundaryLeftPx, columnsResizedWith, dragThresholdPx, dropBoundaries, grabbedAt, guideLeftPx, nearestBoundary, type ColumnGesture, type GridColumnWidthChange,
} from './column-gestures';
import { CopiedRange } from './copied-range';
import type { GridCommand, GridContextMenuContext } from './grid-command';
import { contextMenuRoomPx, contextMenuStyle, MenuItems, popoverAcross, popoverMinWidthPx, type MenuKeyTable, type MenuPlace } from './menu-popover';
import {
    checkedChrome, columnMenuExtra, ShownSlot, SlottedEditor, SlottedFilterPanel, SlottedFindPanel, SlottedMenu, type EditorField,
    type CellEditorView, type GridCellMessageSlotContext, type GridChrome,
    type GridChromeSlot, type GridLoadingIndicatorSlotContext, type GridMenuSlotContext,
} from './grid-chrome';
import { FilterPanel, type FilterPlace, type FilterUi } from './filter-panel';
import { FindPanel, type FindHeld, type FindPlace } from './find-panel';
import { GridMarks, rowMarkBox, rowsSelectedIn } from './grid-marks';
import { copyValues, gatherRows, windowCovers, windowRowAt } from './grid-copy';
import { defaultPasteByteCap, exceedsUtf8 } from './grid-paste';
import type { ActionRefusalReason, GridRefusal } from './grid-refusal';
import {
    resolveColumns, type GridAction, type GridActionEvent, type GridColumn, type GridTemplate, type GridTemplateCell, type ResolvedColumn,
} from './grid-column';
import { moveByKey, movingKinds, type KeyReveal } from './key-moves';
import { selectionLayers, type FillPaint, type LayerPiece, type PointPaint } from './selection-layers';
import { handle as paintedHandle, handleCell, handleCentrePx, type ReadableEdge } from '../paint/selection-styles';
import { SummaryMenu, toggledFigures } from './summary-menu';
import { rowsMovedUnder, selectsTwoCells, summaryFiguresShown, SummaryBox, SummaryQuestions, type SelectionSummary, type SummaryAnswerer } from './selection-summary';
import { stylesheetName, stylesheetStamp } from './shipped-stylesheet';
import { CellPaint, type CellPaintOptions } from './cell-paint';
import { FormulaBar, formulaBarDeclarations, type FormulaBarDeclarations, type FormulaBarHooks, type FormulaBarOptions } from './formula-bar';
import type { ColumnType } from '../query/columns';
import { checkedAppearance, RowAppearances, type RowAppearance } from '../paint/row-appearances';
import { hashesWhenOverflowing } from '../columns/overflow-rules';
import { ConsumerPopoverFrame, type GridPopoverContext } from './consumer-popover';
import { checkedCompleteEditorText, checkedCompletion, CompletionBox, completionKey, completionPlace, type CompleteEditorText, type CompletionPlace } from './completion';
import { checkedRewrite, pointArrowOf, pointOptions, pointStep, type GridEditorRewrite, type GridPointState, type PointOptions } from './point-mode';
import { GridPointedAt, type GridPointArrow, type GridPointedPress, type GridPointedStep, type PointedAtGrid } from './pointed-at';
import { acceptCandidate, EditorCompletion, replaceSpan } from '../cells/editor-text';
import { ReferenceOutlines, referenceOutlineOptions, withinExtent, type GridOutlinedColumn, type ReferenceOutlineOptions } from './reference-outlines';
import { ReferenceHighlights, ReferenceTextLayer, type ColouredStretch, type PointedSpan } from './reference-text';
import type { GridEditorReference, ReferenceColouring, ReferenceKeyColour } from '../cells/reference-colouring';

/** A Viewport axis: a number of pixels the Consumer declares, or `'stretch'`, the parent's (ADR-0028). */
export type ViewportSize = number | 'stretch';

/**
 * A declared key, pressed (ADR-0050, item 14): the key as it was declared, whether an edit was open, and the
 * Selection as the grid holds it at the key, with the Row Sequence Version it is written in. Raised and not
 * interpreted: the grid moves nothing, opens nothing, and leaves an open edit as it was, its text and caret
 * included.
 */
export interface GridDeclaredKeyPress {
    /** The key, in the canonical form it was declared in. */
    readonly key: string;
    /** Whether an edit was open: while one is, the Consumer decides what the key does. */
    readonly editOpen: boolean;
    /** The Selection at the key, which a key that acts on the Selection acts on. */
    readonly selection: GridSelection;
    /** The Row Sequence Version the Selection's positions are written in. */
    readonly rowSequenceVersion: number;
}

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
    /** What names the group in `onHeaderGroupToggled` (ADR-0400). Required with `expanded`, and unique. */
    readonly id?: string;
    /** Whether the group shows open (ADR-0400): given, it draws a toggle, ▾ when true and ▸ when false, whose press
     * notifies `onHeaderGroupToggled`; the grid adds and removes no column, and shows the `expanded` it was last given. */
    readonly expanded?: boolean;
    /** The Hidden Mark a collapsed group wears (ADR-0400 section 6): the Cell State a column it hides holds, and the
     * reason, its accessible description. Only on a group declared `expanded: false`. */
    readonly hiddenMark?: HiddenMark;
}

/** What a grid is drawn from. Rows reach it only through a Grid Source; `rows` is `GridSource.from(rows)`. */
export interface GridOptions<Row> extends CellPaintOptions<Row>, FormulaBarOptions<Row> {
    /** The columns, in display order. */
    readonly columns: readonly GridColumn<Row>[];
    /** Where the rows come from. Exactly one of `source` and `rows` is given. */
    readonly source?: GridSource<Row>;
    /** The rows, held by a source the grid makes over them. Exactly one of `source` and `rows` is given. */
    readonly rows?: readonly Row[];
    /** The preset every geometry not given comes from: the Wrapper's, then `compact`, unless given (ADR-0028). */
    readonly density?: GridDensity;
    /** The widths the width and overflow estimates read (ADR-0016), beating a Wrapper's and the preset's. Whoever
     * sets `--ex-font-family` owes them (ADR-0027). */
    readonly cellMetrics?: CellTextMetrics;
    /** What a design-system Wrapper hands every grid inside it (ADR-0030): the widths of the font its stylesheet
     * sets, and the Density, hover band and Row Stripes its own words map onto. A value given on the grid beats
     * it, and it beats the preset (ADR-0028). */
    readonly presentationDefaults?: GridPresentationDefaults;
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
    /**
     * Answers each approved copy later (ADR-0152), as `copyAnswer` does now, for a Consumer that asks elsewhere for
     * what it copies: ExPivot asks its report source. A copy event then takes the asynchronous write, even when the
     * Window holds every cell. The Consumer captures what the copy reads, such as the report on screen, before it
     * awaits, and the grid never puts the rows in hand in place of an answer. The signal aborts when a newer copy
     * supersedes this one or the grid is destroyed; the answer is then written nowhere. `copyAnswer` takes
     * precedence where both are given.
     */
    readonly copyAnswerAsync?: (request: GridCopyRequest, signal: AbortSignal) => Promise<GridCopyAnswer>;
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
     * refused whole (ADR-0035). It may answer later with a promise: until it settles the paste is pending, every
     * input made meanwhile waits for it, `refuse` may still be called, and Escape cancels it, aborting
     * `intent.signal` (ADR-0380).
     */
    readonly onPaste?: (intent: GridPasteIntent) => void | PromiseLike<void>;
    /** Whether a block pasted onto one cell spills from it (ADR-0050, item 3): written with that cell at its
     * top-left, and once accepted the block becomes the Selection. Off unless given, and a block onto one
     * cell is then refused (ADR-0014). Declared by a Consumer that holds every cell, such as a Sheet. */
    readonly pasteMaySpill?: boolean;
    /** The fill handle (ADR-0050, item 5): painted at the corner of the Selection's one range while no edit is
     * open, and dragged along one axis to raise one Fill Intent through `onFill`. Off unless given. */
    readonly showFillHandle?: boolean;
    /**
     * Hears the one intent a fill-handle drag raises (ADR-0050, item 5): the source, the target beside it and the
     * direction. The grid writes nothing; what a fill means is the Consumer's. A Consumer that does not fill the
     * pattern calls `intent.refuse()` before this returns, and the Selection stays on the source; otherwise it
     * becomes the source and the target together. A target over a column not declared `editable` is refused
     * whole before anything is raised (`onRefused`, kind `paste`, reason `targetNotEditable`; ADR-0035). It may
     * answer later with a promise, as `onPaste` may: until it settles the fill is pending, every input made
     * meanwhile waits for it, `refuse` may still be called, and Escape cancels it, aborting `intent.signal`
     * (ADR-0380).
     */
    readonly onFill?: (intent: GridFillIntent) => void | PromiseLike<void>;
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
     * Why a cell wears its Cell State, asked of the Consumer for one cell at a time (ADR-0034): once the Focus has
     * stood on it for 300 ms, or the pointer has come to rest over it as long, never while painting. The answer
     * is shown below the cell until the Focus moves or the pointer leaves the grid; `undefined` or an empty
     * sentence shows nothing. While an edit is open its own message outranks it. While this is not given the
     * grid does not hear the pointer move for it. It may answer later, as Blazor's ExGrid does across the circuit
     * (ADR-0380 section 5): the message opens once answered, if its cell is still the one settled on, and the grid
     * is not settled meanwhile.
     */
    readonly cellMessage?: (row: Row, column: GridColumn<Row>) => string | undefined | PromiseLike<string | undefined>;
    /**
     * Keys the Consumer names, which the grid takes from the browser and raises through `onDeclaredKey`, whether or
     * not an edit is open (ADR-0050, item 14): ExSheet's formatting keys, or a key of the page's own. Each is in
     * the canonical form, the event's `key` after `Control+`, `Meta+`, `Shift+` and `Alt+` in that order for the
     * modifiers held, Command folding into Control where it is the Primary Modifier (ADR-0012), and is matched
     * exactly, so a letter wanted under CapsLock too is declared in both cases. A key the core answers itself is
     * refused by name, as is one not in the canonical form. Taken only while the grid holds the keyboard on its
     * root, its Keyboard Field or an editor surface: a control inside the grid keeps its own keys.
     */
    readonly declaredKeys?: readonly string[];
    /** Hears a declared key pressed (ADR-0050, item 14). Required with `declaredKeys`: a key taken from the page
     * for nobody would be taken for nothing. It may answer later, as Blazor's ExGrid awaits its `OnDeclaredKey`
     * (ADR-0240's note of 2026-10-19): while the promise is out, every input made in the grid after the key, a row
     * Action or Mark click among them, is held in its order and taken once it settles, as behind `inOrder`. */
    readonly onDeclaredKey?: (press: GridDeclaredKeyPress) => void | PromiseLike<void>;
    /** Paints a Row Stripe on every second row of the result (ADR-0038), appearance only and off unless given.
     * Counted by the row's position in the whole result, so a stripe stays with its row while scrolling and on a
     * Placeholder; Row Kind and Cell State paint over it, and the overlays above it. The colour is
     * `--ex-row-stripe-background`. */
    readonly stripeRows?: boolean;
    /**
     * Rows per page: a pager exists when this is given, and its absence is continuous scrolling (ADR-0015). The
     * page is the context the grid shows: the scrollbar spans the page, a drag stops at its edge, and Ctrl+A and the
     * corner select it, while the keyboard crosses pages and the status line offers the whole result. A page turned
     * raises the same Range Request a scroll does. At least one.
     */
    readonly pageSize?: number;
    /**
     * The Range Request Margin (ADR-0183): how many rows short of the Window's edge the grid asks its source for rows.
     * At M, the source's `needRange` is called as soon as the rows on screen plus M rows either side, clamped into the
     * total, are not all in the Window, while the rows on screen may still be held, so a source that reads ahead is
     * asked before the Viewport reaches rows it does not hold. The request still names the rows on screen, and the
     * same rows are not asked for again until the Window moves: a scroll within the margin asks for the rows it shows
     * then. 0, the default, asks once the rows on screen are not all held. Set it only
     * where the source's answer reads ahead at least this far: one that answers exactly the rows asked for would be
     * asked again at every move near the edge. `GridSource.fetch` does not act on an early request, since its Window
     * still covers the rows on screen. A whole number of at least 0.
     */
    readonly rangeRequestMargin?: number;
    /** What role each row plays, detail, group or total (ADR-0024): none, and every row is a detail row. Asked once
     * per row painted, and by the Row Marks of every row of the result when they count or mark all, so it is a cheap
     * read of the row; its identity is the change signal: a new function paints the rows again, and the same one
     * answering otherwise paints nothing. The Mark Column paints no checkbox on a group or total row, and the marks
     * leave them out of "all" and of the counts. */
    readonly rowKind?: (row: Row) => RowKind;
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
     * editor stays open with the typing (ADR-0142). It may answer later with a promise, against the newest
     * version it holds: until it settles the commit is pending, the editor stands read-only with the text, every
     * input made meanwhile waits for it, `refuse` may still be called, and Escape cancels it, aborting
     * `intent.signal` (ADR-0380). Without it no Cell Editor opens: typing onto a cell is
     * heard by nobody, and a grid with no column `editable` is display-only, its keyboard on its root
     * (ADR-0080).
     */
    readonly onEdit?: (intent: GridEditIntent<Row>) => void | PromiseLike<void>;
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
     * command acts on is what the user can see. The answer may be a promise: the menu then stands once it settles,
     * and every input made meanwhile is held, in its order, and taken once it stands (ADR-0421). A menu whose
     * Selection an order, a source, columns or `placeSelection` moved meanwhile does not open, as an open one closes;
     * nor does one whose promise rejects, with an `AbortError` silently, as given up by whoever answers.
     */
    readonly contextCommands?: (context: GridContextMenuContext<Row>) => readonly GridCommand[] | PromiseLike<readonly GridCommand[]>;
    /** What the built-in menu calls a command, by its id (ADR-0036): the Consumer's words, in any language, or
     * `undefined` for the built-in English. A command the table does not know either is shown as its id. A refused
     * copy, paste, fill or Delete is said in the words it answers for the reason's id (ADR-0203:
     * `copyRefusalLabelIds`, `pasteRefusalLabelIds`, `shapeMismatchMaySpillLabelId`), where
     * `refusalColumnPlaceholder` stands for the column's header; `undefined` keeps the built-in English and `''`
     * says nothing, the refusal still reaching `onRefused`. */
    readonly commandLabel?: (id: string) => string | undefined;
    /** Leaves Copy with headers off the Context Menu, and leaves its key, Ctrl+Shift+C, to the page (ADR-0050, item 13;
     * ADR-0005). Shown and taken unless given. */
    readonly hideCopyWithHeaders?: boolean;
    /** The Chrome slots a Wrapper fills (ADR-0390): each part named here is drawn by its mount function in place
     * of the built-in Chrome's, and what it means stays the core's. Read at each opening of the part: a part
     * standing keeps the contents it opened with. */
    readonly chrome?: GridChrome<Row>;
    /** Leaves the ▾ off every header, and Alt+↓ with it (ADR-0050, item 12): the column menu, the column's
     * commands, is shown unless given. */
    readonly hideColumnMenu?: boolean;
    /** Hears "Hide this column" from the column menu, with the column's `name` (ADR-0010). The grid changes
     * nothing: the column goes when the Consumer hands back the columns without it. Without it the command is
     * shown disabled. */
    readonly onColumnHidden?: (column: string) => void;
    /**
     * Hears a Header Group's toggle (ADR-0400): a press on it, or Alt+Shift+↓ or ↑ from a cell under the group, with
     * the group's `id`, the state asked for, which is the opposite of the `expanded` shown when the input was taken,
     * and the Focus's column. The grid adds and removes no column: the Consumer answers by handing back the columns
     * and the group's new `expanded`. Required for any group declared `expanded`; given, Alt+Shift+↓ and ↑ cannot be
     * among `declaredKeys`.
     */
    readonly onHeaderGroupToggled?: (toggle: HeaderGroupToggle) => void;
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
     * source that finds by a rule of its own passes it here to have the marks. It is asked about every painted cell
     * that carries a value, a column that is not queryable included, since the Consumer's own Find says where a step
     * lands; never about an Action or Mark cell. Hold it in a field: its identity is the change signal, and a new
     * function makes every painted row judge its cells again (ADR-0003).
     */
    readonly findMatch?: (row: Row, column: GridColumn<Row>, pattern: GridFindPattern) => boolean;
    /**
     * The next or the previous error cell (ADR-0381 section 3), asked when F8 or Shift+F8 is typed on the grid:
     * answered as a Find step is, with a row in the order the request was read in and a column name, or nothing
     * found. The grid knows only the cells it paints, so the step is the Consumer's; `GridRuleset.findError` answers
     * it from what the ruleset marked, given the rows in the grid's order. Without it the grid offers no step and
     * leaves F8 to the page; with it, F8 and Shift+F8 cannot be among `declaredKeys`. An answer about rows or columns
     * that moved while it was out moves nothing, and is said; none found is said too.
     */
    readonly findError?: (request: GridErrorStepRequest, signal: AbortSignal) => GridFindResult | PromiseLike<GridFindResult>;
    /** Whether Find searches the column headers and Header Group labels too (ADR-0333): true unless given. A
     * header found is the current match, marked and named in the panel, and the Focus stays where it is. Turn
     * it off where the headers are addresses rather than content. */
    readonly findsHeaders?: boolean;
    /** A plain click on a column header selects the whole column, and nothing sorts: Excel's meaning, where
     * ADR-0012 gives the click to sorting (ADR-0050, item 1). Shift+click extends from the Focus's column as
     * always. Off unless given. */
    readonly headerClickSelects?: boolean;
    /**
     * The Row Headings (ADR-0050, item 1): a band held at the left edge beside the rows, each labelled by this
     * function, handed the row's position in the whole result, Placeholders included (the row number, on a
     * Sheet). The band stands outside the column index space: it is never in the Selection, a copy, Ctrl+A or the
     * Enter/Tab cycle. A press on a heading selects the whole row, Shift extends from the Focus's row, Ctrl adds
     * or takes the row out, and a drag across the headings selects whole rows; where the header stands too, a
     * click on the corner where the two meet selects all. None, and there is no band. Asked once per painted row,
     * so it is as cheap as `rowKind` has to be; a label that changes paints its row again.
     */
    readonly rowHeadings?: (row: number) => string;
    /** The Row Headings' width in pixels (ADR-0050, ADR-0028): geometry, emitted inline. None, and it is estimated
     * from the Cell Metrics as the wider of the first and the last row's labels, which is the widest for row
     * numbers. */
    readonly rowHeadingWidth?: number;
    /** Takes the whole header band away, so the first row starts at the Viewport's top (ADR-0050, item 1): what a
     * headerless list asks for. Whole columns stay reachable with Ctrl+Space. Off unless given. */
    readonly hideHeader?: boolean;
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
    /**
     * The References in the editor's text (ADR-0057), the fifth formula entry aid. Asked synchronously while an
     * edit is open, from either editor surface, with the text as it stands after each change; it answers each
     * Reference's span and either the cells it names on this grid or a key naming something the grid does not
     * hold. It must answer for text that is not finished, `=SUM(A1,`, since a Formula is unfinished for as long as
     * it is typed, and with nothing for text that is not a Formula. References naming the same cells, or carrying
     * the same key, share a colour, handed out in order of first appearance round a palette of seven; a Consumer
     * never picks one. Each range answered is outlined in the selection overlay, cut to the painted rows, and the
     * grid never scrolls to show one. A commit or a cancel takes every outline away. A span that does not lie
     * inside the text, or overlaps another, is refused by name. None, and nothing is outlined.
     */
    readonly referencesIn?: (text: string) => readonly GridEditorReference[] | undefined;
    /** Hears the colour each key `referencesIn` answered was given (ADR-0057): each key once, in order of first
     * appearance, whenever that set changes and never when it does not, and an empty list when the edit ends. A
     * Reference naming cells on this grid is not told: the grid outlines it itself. The Consumer outlines what a
     * key names wherever it shows it, a Linked Table's column in the grid that shows the table. */
    readonly onReferenceKeyColoursChanged?: (keys: readonly ReferenceKeyColour[]) => void;
    /**
     * Columns to outline, each in a colour (ADR-0057): a Reference Outline over the column's body, across all its
     * rows, cut to the painted rows, with or without an edit or a Selection. It is how the grid that shows a Linked
     * Table outlines the columns a Formula edited elsewhere reads, in the colours that Formula's grid told its
     * Consumer. A column is named by its `name` and outlined wherever the order puts it; a name the grid does not
     * show is outlined nowhere, and a column listed twice is outlined twice in list order, the later over the
     * earlier. None, and nothing is outlined.
     */
    readonly outlinedColumns?: readonly GridOutlinedColumn[];
    /**
     * Whether, in this text with the caret here, a Reference can go at the caret (ADR-0051): after `=`, an operator,
     * `(` or `,` in a Formula. Asked synchronously on the editor's text and caret as each key or press finds them,
     * never on an earlier answer, so a fast typist is never pointed where the text no longer allows it. While it
     * answers true in Overwrite, an arrow points instead of committing (`=` ↓ ↓ writes the Reference two rows down),
     * Home points at the row's first column, End commits nothing and writes nothing (ADR-0058), and a press on a cell
     * points in any editing state. Where it answers false, Overwrite and Caret keep the meanings ADR-0012 gives the
     * arrows. Needs `referenceText`. None, and nothing is pointed.
     */
    readonly pointAt?: (text: string, caret: number) => boolean;
    /** The Reference text for a pointed range, written at the caret while pointing (ADR-0051): `A3`, `B7:C9` on a
     * Sheet. The grid knows positions; what they are called is the Consumer's. Required with `pointAt`. */
    readonly referenceText?: (range: SelectionRange) => string;
    /**
     * What F4 makes of the editor's text (ADR-0051): on a Sheet, the Reference at the caret cycled through its forms,
     * `=B2` → `=$B$2` → `=B$2` → `=$B2` → `=B2`. Asked with the text and the selection in the surface typed in, it
     * answers the whole new text and the selection in it, or nothing to change. The answer is written to both editor
     * surfaces, and its selection placed as a selection where the key found one or a caret before the text's end, and
     * as a caret at the selection's end where the caret stood at the text's end. While pointing it is asked over the
     * whole Reference the outline wrote, so both ends of a pointed range cycle together, and pointing goes on over the
     * rewritten Reference. Given, F4 is taken while an edit is open, and only then.
     */
    readonly cycleReference?: (text: string, start: number, end: number) => GridEditorRewrite | undefined;
    /**
     * Completion and the argument hint (ADR-0051): while an edit is open, the editor's text and caret are handed to
     * this as the user types or moves the caret, in the Cell Editor and in the Formula Bar alike, and the answer is
     * painted beneath the surface typed in, a list of candidates and a hint. ↑ and ↓ choose, Tab accepts the chosen
     * candidate and closes the list, the text it wrote asked about again for its hint alone, and Escape closes the list
     * and leaves the edit open. A list takes no other key: where `pointAt` says a Reference can go at the caret, ←, →,
     * Home, End and the Shift+arrows point past it and close it, as they point without it (ADR-0058). A press on a
     * candidate accepts it. The answer may come now or later: one for text the user has typed on from is dropped,
     * never shown. Nothing, or an answer with nothing in it, shows nothing. None, and nothing is asked or painted.
     */
    readonly completeEditorText?: CompleteEditorText;
    /** Hears where the open edit stands with respect to Point (ADR-0058), when that changes and never when it does not,
     * as the key, the typing or the press that changed it is answered, and `'none'` when the edit ends. A Consumer that
     * points from other instances on the page declares them pointed at while it is not `'none'`. Never heard where
     * `pointAt` is not given. */
    readonly onPointStateChanged?: (state: GridPointState) => void;
    /**
     * Hears the arrow keys pressed while what Point wrote in the open edit was written from outside the grid
     * (`writtenFromOutside`; ADR-0058, "The keyboard"): ↑, ↓, ← and →, with Shift (`extends`) or the Primary
     * Modifier (`toEdge`) or both. They move nothing here, since the text stands for a cell of another instance: the
     * Consumer that wrote it moves it there, writing again through the handle's `writePointedText`, or refuses. Not
     * given, the arrows are taken and move nothing.
     */
    readonly onPointArrowFromOutside?: (arrow: GridPointArrow) => void;
    /**
     * A Consumer declaration (ADR-0058): that this grid is pointed at from outside, a Formula edited elsewhere on the
     * page pointing at its cells, and what it draws meanwhile. While `isPointedAt` holds and the grid has no edit of
     * its own open, a primary press on the rows or the column headers moves neither DOM focus nor the Selection and
     * the Focus, runs no sort, column menu, reorder, resize or Heading drag, and is handed to the declaration's
     * `onPress` as the pressed cell, the pressed column header, or a shape the Consumer will refuse; a drag onto
     * another cell or column is handed over once more. The root wears `ex-pointed-at`. Whether or not it holds, the
     * declaration's dashes and outlined columns are drawn in the selection overlay, and the grid answers the
     * declaration's steps and reveals for arrow keys pressed elsewhere (DC-55). The grid follows the declaration's
     * changes as it is told of them. None changes nothing.
     */
    readonly pointedAt?: GridPointedAt<Row>;
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
// A commit's gesture that will not be taken again, as its commit did not land: a held row command it was stops being
// followed (ADR-0240's note of 2026-10-19).
function dropTrigger(trigger: HeldInput | undefined): void {
    if (trigger?.kind === 'turn') trigger.dropped?.();
}

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

/** How long the Focus must stand on a cell, or the pointer rest over it, before its message opens (ADR-0034): the
 * intent to read, which is not the stillness the settle measures, so a number of its own. */
const popoverDelayMs = 300;

/** How often a drag held in an edge band scrolls a step (ADR-0008). */
const edgeScrollTickMs = 50;

/** The keys whose own keydown is no key of the grid's: they neither take nor end a release of Tab. */
const modifierKeys: ReadonlySet<string> = new Set(['Control', 'Shift', 'Alt', 'Meta', 'AltGraph', 'CapsLock']);

/** Home and End where Command is the primary key, which the browser binds to a scroll in a text field (ticket 32). */
const appleCaretKeys: ReadonlySet<string> = new Set(['Home', 'End', 'Shift+Home', 'Shift+End']);

/** The shortest and longest waits the Change Highlight timer is armed for (ADR-0068). */
const shortestHighlightWaitMs = 1;
const longestHighlightWaitMs = 24 * 60 * 60 * 1000;

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
    /** The Row Kind it was asked under, and whether it wore a stripe (ADR-0024, ADR-0038). */
    readonly rowKind: ((row: Row) => RowKind) | undefined;
    readonly stripe: boolean;
    /** Its Row Heading's label and style, or none without the band (ADR-0050, item 1). */
    readonly heading: string | undefined;
    readonly headingStyle: string | undefined;
    /** The earliest end among the Change Highlights it painted, or none (ADR-0068): the time that paints it again. */
    readonly highlightEnd: number | undefined;
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

// The Focus a Header Group's toggle remembers (ADR-0011's section "A Header Group's toggle keeps the Focus"): what was
// notified, and the row by its key, or by its position under the order it was taken in. Let go when anything else
// placed or dropped the Focus first, which the Selection and the source it was taken under tell.
interface ToggleFocus {
    readonly toggle: HeaderGroupToggle;
    readonly selection: Selection;
    readonly source: object;
    readonly sequence: number;
    readonly row: number;
    readonly key: RowKey | undefined;
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
    readonly copyAnswerAsync: ((request: GridCopyRequest, signal: AbortSignal) => Promise<GridCopyAnswer>) | undefined;
    readonly onRefused: ((refusal: GridRefusal<Row>) => void) | undefined;
    readonly onAction: ((event: GridActionEvent<Row>) => void) | undefined;
    /** Who holds the marks of the Mark Column, while one is declared. */
    readonly marks: RowMarks<Row> | undefined;
    readonly onPaste: ((intent: GridPasteIntent) => void | PromiseLike<void>) | undefined;
    readonly pasteMaySpill: boolean;
    readonly showFillHandle: boolean;
    readonly onFill: ((intent: GridFillIntent) => void | PromiseLike<void>) | undefined;
    readonly highlightFocusRow: boolean;
    readonly highlightHoverRow: boolean;
    readonly cellMessage: ((row: Row, column: GridColumn<Row>) => string | undefined | PromiseLike<string | undefined>) | undefined;
    readonly declaredKeys: ReadonlySet<string>;
    readonly onDeclaredKey: ((press: GridDeclaredKeyPress) => void | PromiseLike<void>) | undefined;
    readonly stripeRows: boolean;
    readonly pageSize: number | undefined;
    readonly rangeRequestMargin: number;
    readonly rowKind: ((row: Row) => RowKind) | undefined;
    readonly pasteByteCap: number;
    readonly onClear: ((intent: GridClearIntent) => void) | undefined;
    readonly onUndo: (() => void) | undefined;
    readonly onRedo: (() => void) | undefined;
    readonly onEdit: ((intent: GridEditIntent<Row>) => void | PromiseLike<void>) | undefined;
    readonly onEditingChanged: ((open: boolean) => void) | undefined;
    readonly onEditDiscarded: ((reason: EditDiscardReason) => void) | undefined;
    readonly onOverwriteNotice: ((notice: GridOverwriteNotice) => void) | undefined;
    readonly onCellDoubleClick: ((cell: CellPosition) => void) | undefined;
    readonly onLeave: (() => void) | undefined;
    readonly onColumnWidthChanged: ((change: GridColumnWidthChange) => void) | undefined;
    readonly onColumnOrderChanged: ((order: readonly string[]) => void) | undefined;
    readonly contextCommands: ((context: GridContextMenuContext<Row>) => readonly GridCommand[] | PromiseLike<readonly GridCommand[]>) | undefined;
    readonly commandLabel: ((id: string) => string | undefined) | undefined;
    readonly hideCopyWithHeaders: boolean;
    readonly chrome: GridChrome<Row> | undefined;
    readonly hideColumnMenu: boolean;
    readonly onColumnHidden: ((column: string) => void) | undefined;
    readonly onPinnedCountChanged: ((count: number) => void) | undefined;
    readonly onHeaderGroupToggled: ((toggle: HeaderGroupToggle) => void) | undefined;
    readonly culture: GridCulture;
    readonly temporal: TemporalLike | undefined;
    readonly onReplace: ((request: GridReplaceRequest) => GridReplaceResult | Promise<GridReplaceResult>) | undefined;
    readonly findMatch: ((row: Row, column: GridColumn<Row>, pattern: GridFindPattern) => boolean) | undefined;
    readonly findError: ((request: GridErrorStepRequest, signal: AbortSignal) => GridFindResult | PromiseLike<GridFindResult>) | undefined;
    readonly findsHeaders: boolean;
    readonly cellPaint: CellPaint<Row>;
    readonly headerClickSelects: boolean;
    readonly rowHeadings: ((row: number) => string) | undefined;
    readonly rowHeadingWidth: number | undefined;
    readonly hideHeader: boolean;
    readonly dataEdge: EdgeAnswer | undefined;
    readonly onSummarize: SummaryAnswerer | undefined;
    readonly showSelectionSummary: boolean;
    readonly summaryFigures: readonly SummaryFigure[];
    readonly onSelectionSummaryChanged: ((summary: SelectionSummary) => void) | undefined;
    readonly onSummaryFiguresChanged: ((figures: readonly SummaryFigure[]) => void) | undefined;
    readonly formulaBar: FormulaBarDeclarations<Row>;
    readonly referenceOutlines: ReferenceOutlineOptions;
    readonly point: PointOptions;
    readonly completeEditorText: CompleteEditorText | undefined;
    readonly pointedAt: GridPointedAt<Row> | undefined;
}

// The popover standing (ADR-0039, ADR-0040): its element and the commands it shows, and where it stands. The
// Context Menu is held where it was opened in the Viewport's readable box and against what: an order, columns
// or a source that moved take the Selection its commands act on, and the menu with it (ADR-0011, ADR-0036).
// A column's menu stands under its column's header, followed by the column's name (ADR-0010).
// The find panel stands in the grid's top right under the header, and holds no commands (ADR-0055).
// A Consumer's popover stands under the header band, centred across the grid (ADR-0050, item 16).
type OpenPopover = MenuPopover | { readonly kind: 'find'; readonly element: HTMLElement; readonly panel: FindPlace }
    | {
        readonly kind: 'consumer'; readonly element: HTMLElement; readonly frame: ConsumerPopoverFrame; readonly opening: object;
        // The grid's own contents in the frame, the figures menu, which the Consumer's closePopover leaves alone.
        readonly own: boolean;
        // What the Consumer hears as its contents go, however the frame closes, before the frame leaves the page.
        readonly closed: (() => void) | undefined;
    };

type MenuPopover = {
    readonly element: HTMLElement;
    readonly commands: readonly GridCommand[];
    readonly items: MenuPlace;
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
    readonly panel: FilterPlace | undefined;
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

/** A row Action or row Mark click held behind earlier input (ADR-0240's note of 2026-10-19): the row it was made
 * on, whether that row has left the Window while it waited, and what it does at its turn. */
interface HeldRowCommand<Row> {
    readonly pressed: PressedRow<Row>;
    left: boolean;
    readonly act: (pressed: PressedRow<Row>, left: boolean) => void;
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
    /** The role it plays, asked once for the row (ADR-0024). */
    readonly kind: RowKind;
    /** Its Row Heading's label, or none without the band (ADR-0050, item 1). */
    readonly heading: string | undefined;
    /** The time its Change Highlights are painted by, read once for the pass while they are declared (ADR-0068),
     * and the earliest end among the marks its cells painted, gathered as they are. */
    readonly now: number | undefined;
    highlightEnd: number | undefined;
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
    /** Overwrite, Excel's Enter mode, where the arrows commit and move; Caret, where they are the field's; or Point,
     * where they move the pointing outline (ADR-0051). */
    mode: 'overwrite' | 'caret' | 'point';
    /** The editor's box over the cell: the core's own field, or the box a Chrome's cellEditor slot draws its field in
     * (ADR-0390). */
    readonly element: HTMLElement;
    /** The Chrome's contents, where its cellEditor slot draws the field. */
    readonly slot: SlottedEditor | undefined;
    /** The source, the columns and the Row Sequence Version it opened under. */
    readonly source: GridSource<Row>;
    readonly columnNames: readonly string[];
    /** Taken again as it comes back among its own rows from another set's (ADR-0331 §5). */
    sequence: number;
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
    /** The committed text it opened on, where it opened on the cell's own text, by F2, a double click or a press into
     * the Formula Bar; none where a typed character opened it, which is never untyped (ADR-0007's note of 2026-10-11). */
    readonly untypedText: string | undefined;
    /** The set of rows it opened among (ADR-0331 §5): while another is shown it is away, open with its editor holding
     * the keyboard unseen, pointing at the rows shown, and its commit landing on `awayRow`. */
    readonly set: number;
    /** The row it opened on as the grid last held it, read from the Window as another set replaced it: what its
     * commit lands on while it is away; none where that Window no longer held the row (ED-21). */
    awayRow: Row | undefined;
    /** Whether it left its own rows under the order it was opened under: without a Row Key, only then does its
     * position name its row again when they come back (ADR-0142, LV-20). */
    leftInOrder: boolean;
}

// A press handed over whose button is still down (ADR-0058): where it was, on a header or not, and where the pointer
// stood, to place a header drag by its client delta.
interface PointedDrag {
    readonly from: CellPosition;
    readonly fromHeader: boolean;
    readonly x: number;
    readonly clientX: number;
}

// The pointedAt option checked: a declaration, or none.
function checkedPointedAt<Row>(given: GridPointedAt<Row> | undefined): GridPointedAt<Row> | undefined {
    // Read as unknown first: a Consumer in plain JavaScript hands whatever it hands.
    const loose: unknown = given;
    if (loose !== undefined && !(loose instanceof GridPointedAt)) {
        throw new TypeError('pointedAt is a GridPointedAt, made by the Consumer that points (ADR-0058).');
    }
    return given;
}

// The coloured text's layers of one grid (ADR-0057).
interface ReferenceTextLayers {
    readonly highlights: ReferenceHighlights;
    readonly style: HTMLStyleElement;
    readonly cell: ReferenceTextLayer;
    readonly bar: ReferenceTextLayer;
}

// What the pointed dashes were last found from: the declaration, the columns, the Window and the painted rows.
interface PointDashesAsked {
    readonly dashes: object | undefined;
    readonly columns: readonly unknown[];
    readonly window: readonly unknown[];
    readonly windowStart: number;
    readonly start: number | undefined;
    readonly count: number | undefined;
    readonly totalRows: number;
}

function sameDashesAsked(was: PointDashesAsked, now: PointDashesAsked): boolean {
    return was.dashes === now.dashes && was.columns === now.columns && was.window === now.window && was.windowStart === now.windowStart
        && was.start === now.start && was.count === now.count && was.totalRows === now.totalRows;
}

/** An open edit's Point as it stood before a write from outside: what taking the write back returns it to. */
interface PointBefore {
    readonly text: string;
    readonly caret: number;
    readonly mode: 'overwrite' | 'caret' | 'point';
    readonly pointer: Selection | undefined;
    readonly start: number;
    readonly length: number;
    readonly written: string | undefined;
    readonly fromOutside: boolean;
}

/** A commit whose answer is out (ADR-0380 section 3): a verdict, or the Consumer's to an intent, answered as a
 * promise. */
interface PendingCommit<Row> {
    /** What the root's `data-ex-pending-commit` says while it stands. */
    readonly stamp: number;
    /** Handed to the answer as its signal, and aborted when the user cancels. */
    readonly controller: AbortController;
    /** The editor standing read-only with the text, or none: a paste's, or a fill's once its editor is down. */
    readonly edit: OpenEdit<Row> | undefined;
    /** The verdict's answer, before any intent was raised, or the Consumer's to one. The rows or columns handed over
     * under the latter are the commit's own doing, as they are while a handler runs (ADR-0142), so they do not take
     * its editor away. */
    readonly phase: 'verdict' | 'intent' | 'work';
    /** The input whose handler made the commit, taken again once it lands: its gesture keeps its meaning. */
    readonly trigger: HeldInput | undefined;
    /** The inputs made meanwhile, in the order they were made. */
    readonly held: HeldInput[];
    /** What a cancel says, and what it takes back. */
    readonly cancelled: () => void;
    /** The text field that held the keyboard, read-only while the answer is out, where it is not the editor. */
    readonly stilled: HTMLInputElement | HTMLTextAreaElement | undefined;
}

// Work of the Consumer's in the Input Order (ADR-0240 sections 6 and 12, ADR-0330 section 10), from the place it took
// until it ends.
interface PlacedWork {
    /** The number the stamp names cancellable work by (`data-ex-work-placed`, `data-ex-work`). */
    readonly id: number;
    /** Handed to the work as its signal, and aborted by the Escape that cancels it. */
    readonly controller: AbortController;
    /** Says that an Escape came after the work had landed (true) or found nothing to cancel (false); none for work
     * an Escape does not cancel. */
    readonly tooLate: ((landed: boolean) => void) | undefined;
    /** Answers the caller with nothing run, for work still waiting as the grid goes. */
    readonly neverRan: () => void;
    started: boolean;
    ended: 'landed' | 'cancelled' | 'failed' | undefined;
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

/** A column's tone rule's answer about a value, refused by the column's name where it is no tone (ADR-0006). */
function toneOf<Row>(column: GridColumn<Row>, value: unknown): CellTone {
    const tone: unknown = defined(column.tone, 'a tone rule')(value);
    if (tone !== 'none' && tone !== 'positive' && tone !== 'negative') {
        throw new RangeError(`Column '${column.name}': its tone rule answered ${JSON.stringify(tone)} for ${JSON.stringify(value)}; a tone is none, positive or negative (ADR-0006).`);
    }
    return tone;
}

/** A page follow (ADR-0204): armed by a move for the Focus it moved to and the cell its reveal brought into
 * the Viewport, and asked once that cell is painted. `rowsKept` says the reveal left the rows where the user had
 * them, as it does for a range of whole columns (ADR-0052). */
interface Follow {
    readonly focus: CellPosition;
    readonly target: CellPosition;
    readonly rowsKept?: boolean;
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
            && sameItems(group.columns, other.columns) && group.id === other.id && group.expanded === other.expanded
            && group.hiddenMark?.state === other.hiddenMark?.state && group.hiddenMark?.reason === other.hiddenMark?.reason;
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

/** The rows the Heading Mark marks over the Row Headings, and the Selection and extent they were made for. */
interface RowHeadingMarks {
    readonly selection: Selection;
    readonly extent: GridExtent;
    readonly rows: readonly RowRange[];
    readonly whole: readonly RowRange[];
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
/** A press on a mark of the grid's own, held to the order and source it was taken against (MK-9). */
interface MarkPress {
    readonly sequence: number;
    readonly source: unknown;
}

/** A press on the header's checkbox: under a pager, the page it was pressed on too (ADR-0015). */
interface HeaderMarkPress extends MarkPress {
    readonly paged: boolean;
    readonly page: RowRange | undefined;
}

export class GridView<Row> {
    readonly #root: HTMLElement;
    // The Input Root (ADR-0240 ticket 04): the product's box around the grid that marks itself `data-ex-input-root`,
    // whose surfaces outside the grid — the Sheet's toolbar and tabs — wait behind what the grid holds for; the grid's
    // own root where none stands around it.
    readonly #inputRoot: HTMLElement;
    readonly #reports: BrowserReports;
    readonly #ids: string;
    readonly #scroller: HTMLElement;
    readonly #spacer: HTMLElement;
    readonly #header: HTMLElement;
    readonly #viewport: HTMLElement;
    readonly #stamp: HTMLElement;
    // The stylesheet's notice (ADR-0123), worded through `commandLabel`.
    readonly #stylesheetNotice: HTMLElement;
    readonly #announce: HTMLElement;
    readonly #shadeLayer: HTMLElement;
    readonly #pinnedLayer: HTMLElement;
    readonly #selectionLayer: HTMLElement;
    readonly #rowMarkLayer: HTMLElement;
    readonly #cornerBox: HTMLElement;
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
    #copyAnswerAsync: ((request: GridCopyRequest, signal: AbortSignal) => Promise<GridCopyAnswer>) | undefined;
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
    // Row Action and row Mark clicks held behind earlier input, each with the row it was made on, noted at the click,
    // and whether that row has left the Window since: one that left is refused at its turn, though it came back
    // (ADR-0240's note of 2026-10-19).
    readonly #heldRowCommands = new Set<HeldRowCommand<Row>>();
    // The order and source a press on the header's checkbox was made under, and, while a pager stood, the page it
    // named: `paged` with no `page` is a page that held no row (ADR-0043, ADR-0015).
    #headerMarkPress: HeaderMarkPress | undefined;
    // The status line after the rows (ADR-0014): the marks' count and the Selection Summary, standing while either
    // has its place in it.
    readonly #status: HTMLElement;
    // The off-screen indicator's sentence and its button (ADR-0014), one each for the grid's life.
    readonly #offScreen: HTMLElement;
    readonly #goToSelection: HTMLElement;
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
    #pasteHeard: ((intent: GridPasteIntent) => void | PromiseLike<void>) | undefined;
    #pasteMaySpill = false;
    #showFillHandle = false;
    #fillHeard: ((intent: GridFillIntent) => void | PromiseLike<void>) | undefined;
    // A fill-handle drag (ADR-0050, item 5): the range it started from, and the order and the source it was taken
    // under; the cell the pointer last reached; and the outline that paints, the source and what the drag has
    // reached as one rectangle, kept so the layers see the same object until the drag reaches elsewhere.
    #handleDrag: { readonly source: SelectionRange; readonly sequence: number; readonly binding: GridSource<Row> } | undefined;
    #handlePointer: CellPosition | undefined;
    #handleOutline: SelectionRange | undefined;
    #highlightFocusRow = false;
    #highlightHoverRow = false;
    #stripeRows = false;
    // The pager (ADR-0015): the rows a page holds while one stands, and the page shown, transient view state as the
    // scroll offset is. The geometry runs over that page alone. The pager's buttons and count, the status line's offer
    // of the whole result, and the Mark Column's, one each for the grid's life.
    #pageSize: number | undefined;
    // The Range Request Margin (ADR-0183): rows either side of those on screen the Window must hold for no request.
    #rangeRequestMargin = 0;
    #pageIndex = 0;
    readonly #pager: HTMLElement;
    readonly #pageBack: HTMLButtonElement;
    readonly #pageOn: HTMLButtonElement;
    readonly #pageShown: HTMLElement;
    readonly #selectWholeResult: HTMLElement;
    readonly #markResult: HTMLElement;
    #markResultPress: MarkPress | undefined;
    // How many times the marks have said they changed, and the page's checkbox state with what it was asked over, so a
    // pass that changed none of it asks no row again (ADR-0027).
    #marksStamp = 0;
    #pageMarkedMemo: {
        readonly window: readonly Row[]; readonly windowStart: number; readonly start: number; readonly count: number;
        readonly rowKind: ((row: Row) => RowKind) | undefined; readonly marks: RowMarks<Row> | undefined; readonly stamp: number;
        readonly checked: 'false' | 'true' | 'mixed';
    } | undefined;
    #rowKind: ((row: Row) => RowKind) | undefined;
    // The row under the pointer, while the hover band is on and the pointer is over a row (ADR-0029).
    #hoverRow: number | undefined;
    // A flagged cell's message (ADR-0034): the Consumer's question, the cell the Focus waits on and the timer that
    // opens it, the pointer's rest timer, and the message shown, which is for one cell at a time.
    #cellMessage: ((row: Row, column: GridColumn<Row>) => string | undefined | PromiseLike<string | undefined>) | undefined;
    // The cell a message is asked about while its answer is out, until it comes or the question goes with the Focus.
    #messageAsked: { readonly cell: CellPosition } | undefined;
    #declaredKeys: ReadonlySet<string> = declareKeys();
    #onDeclaredKey: ((press: GridDeclaredKeyPress) => void | PromiseLike<void>) | undefined;
    #cancelMessage: (() => void) | undefined;
    #cancelRest: (() => void) | undefined;
    #restAt: { readonly x: number; readonly y: number; readonly movedAt: number } | undefined;
    #shownMessage: { readonly cell: CellPosition; readonly text: string; readonly row: Row; readonly column: string } | undefined;
    #pasteByteCap = defaultPasteByteCap;
    #editHeard: ((intent: GridEditIntent<Row>) => void | PromiseLike<void>) | undefined;
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
    // The Size Tip (ADR-0052, 2026-09-29), painted while a drag across Headings covers more than one column or row.
    #sizeTip: HTMLElement | undefined;
    // The Chrome's cellMessage slot, standing in the message while it draws it (ADR-0390).
    readonly #messageSlot: ShownSlot<GridCellMessageSlotContext>;
    // The Chrome's loadingIndicator slot, at the root's foot while the Chrome has one (ADR-0390).
    readonly #loadingSlot: ShownSlot<GridLoadingIndicatorSlotContext>;
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
    // Owed while the browser may yet tell a box the last written scroll content changed (ADR-0220).
    #cancelBoxTold: (() => void) | undefined;
    // The root's, the scroller's and the scroll content's boxes as last written, and the root's parts, whose change
    // is what may change the box the browser tells.
    #laidOut: string | undefined;

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
    // The one Change Highlight timer and the end it is armed for, while a mark is painted (ADR-0068).
    #highlightTimer: { readonly cancel: () => void; readonly end: number } | undefined;
    #paintedHeader: {
        readonly styles: ColumnStyles; readonly columns: readonly ResolvedColumn<Row>[]; readonly scrollable: ColumnRange | undefined;
        readonly sorts: string; readonly layout: HeaderGroupLayout; readonly tierPx: number; readonly grips: boolean;
        readonly menus: boolean; readonly headingStyle: string | undefined;
        /** The commandLabel the header's controls were named through: a ▾ and a group's toggle (ADR-0370 section 10). */
        readonly labels: ((id: string) => string | undefined) | undefined;
    } | undefined;
    // The tiers of Header Groups as painted, and the leaf row beneath them: the rectangles stand on the
    // column geometry, the layout and the tier height alone, so a scroll or a Sort that rebuilds the leaves
    // keeps them (PF-3).
    #paintedTiers: {
        readonly styles: ColumnStyles; readonly columns: readonly ResolvedColumn<Row>[]; readonly layout: HeaderGroupLayout;
        readonly tierPx: number; readonly rows: readonly HTMLElement[]; readonly leaves: HTMLElement;
        readonly groups: readonly { readonly element: HTMLElement; readonly id: string }[];
        /** The commandLabel the toggles' names were worded through (ADR-0400 section 8). */
        readonly labels: ((id: string) => string | undefined) | undefined;
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
    #paintedRowMarks: readonly string[] = [];
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
    #contextCommands: ((context: GridContextMenuContext<Row>) => readonly GridCommand[] | PromiseLike<readonly GridCommand[]>) | undefined;
    #commandLabel: ((id: string) => string | undefined) | undefined;
    #hideCopyWithHeaders = false;
    #chrome: GridChrome<Row> | undefined;
    #popover: OpenPopover | undefined;
    // The column menu (ADR-0010): whether the headers carry a ▾, each painted ▾'s column, and the commands'
    // sinks the Consumer gave.
    #hideColumnMenu = false;
    #menuButtons = new Map<Element, string>();
    #onColumnHidden: ((column: string) => void) | undefined;
    // A Header Group's toggle (ADR-0400): who hears it, the toggles painted by the group each names, and the Focus a
    // toggle remembers for the columns push that answers it (ADR-0011's section on the toggle).
    #onHeaderGroupToggled: ((toggle: HeaderGroupToggle) => void) | undefined;
    #groupToggles = new Map<Element, string>();
    #toggleFocus: ToggleFocus | undefined;
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
    // The error step (ADR-0381 section 3): who answers it, and the question still out.
    #findError: ((request: GridErrorStepRequest, signal: AbortSignal) => GridFindResult | PromiseLike<GridFindResult>) | undefined;
    #errorStepping: AbortController | undefined;
    #findsHeaders = true;
    // The per-cell declarations (ADR-0050, items 6, 7, 11 and 15), the row appearances resolved from them, and
    // the generated stylesheet their classes name, last in the root (ADR-0071).
    #cellPaint: CellPaint<Row> = CellPaint.empty();
    // The Formula Bar (ADR-0051): its declarations, and its band, made once and in the root while it is shown.
    #formulaBarDeclared: FormulaBarDeclarations<Row> = { shown: false, nameBoxLabel: undefined, nameBoxSizeLabel: undefined, onNameBoxEntered: undefined, borrowedText: undefined };
    readonly #bar: FormulaBar;
    #barRead: BarReading<Row> | undefined;
    readonly #appearances = new RowAppearances<Row>();
    #appearanceStyle: HTMLStyleElement | undefined;
    #appearanceVersion = -1;
    #appearanceWritten = 0;
    #headerClickSelects = false;
    // The Row Headings' labels and declared width, and whether the header band stands (ADR-0050, item 1).
    #rowHeadings: ((row: number) => string) | undefined;
    #rowHeadingWidth: number | undefined;
    #hideHeader = false;
    // The Row Headings' width the column geometry was made with, and the one style string every heading and the
    // corner are painted with: a new string only when the width moves, so a row compares it without painting.
    #leadPx = 0;
    #rowHeadingStyle: string | undefined;
    // The hidden markers after the painted rows, by position, and every marker ever made, which the pass tells from
    // a row (ADR-0008); the marked rows' runs, made once per Selection.
    #headingStates = new Map<number, HTMLElement>();
    readonly #headingStateElements = new WeakSet<ChildNode>();
    #rowMarkRuns: RowHeadingMarks | undefined;
    // The corner where the two Headings meet, while both stand (ADR-0050).
    #corner: HTMLElement | undefined;
    // Which Headings a Heading drag runs along (ADR-0050, item 1).
    #headingAxis: 'columns' | 'rows' = 'columns';
    // The Consumer's answer to where Ctrl+arrow stops (ADR-0050, item 2), or the grid's edge.
    #dataEdge: EdgeAnswer | undefined;
    readonly #findHeld: FindHeld = { text: '', matchCase: false, wholeCell: false, replaceText: '' };
    #findOutcome: FindOutcome = { kind: 'none' };
    #findStepping: AbortController | undefined;
    #replacing = 0;
    // A Pending Commit (ADR-0380 section 3), and the stamps given so far. `#input` is the last input the root heard
    // ahead of everything in it: while its dispatch is under way, it is the gesture a commit made in its handler
    // waits with. `#retaking` carries that gesture from a verdict answered to the intent it raises.
    #pending: PendingCommit<Row> | undefined;
    #pendingStamps = 0;
    // Work of the Consumer's placed and not ended, in the order it was placed; the cancellable one whose turn is under
    // way; the count that numbers each; and whether cancellable work's handling is being called (ADR-0240 section 12).
    #works: PlacedWork[] = [];
    #workUnderWay: PlacedWork | undefined;
    #worksPlaced = 0;
    #workStarting = false;
    #input: Event | undefined;
    // The input a Chrome slot's callback last counted (ADR-0220).
    #inputAnswered: Event | undefined;
    #retaking: HeldInput | undefined;
    // An Escape cancelled a commit, and its repeats are its own.
    #escapeCancelled = false;
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
    // The views this grid read (readView), each holding the grid's own Selection and the set of rows it was read of; the
    // one handed back to be shown when the grid takes the version it names, in place of the empty Selection a new
    // version leaves (ADR-0331). A view of rows never shown (showNewView) brings a set of its own.
    readonly #viewsRead = new WeakMap<GridViewRead, { readonly selection: Selection; readonly set: number }>();
    #viewAhead: {
        readonly selection: Selection | { readonly range: SelectionRange; readonly focus: CellPosition } | undefined; readonly set: number | undefined;
        readonly version: number; readonly topPx: number; readonly leftPx: number;
    } | undefined;
    // The set of rows shown, by a number the grid gives each set a view brings in, and the numbers given so far. Rows
    // the source hands under a new version with no view are the same set, in another order (ADR-0331 §5).
    #rowSet = 1;
    #rowSetsGiven = 1;
    // The state the Selection was last reconciled under: the Window an edit leaving its set is read from.
    #reconciledState: GridSourceState<Row> | undefined;
    // Whether the offsets taken from a view are still to be kept inside the rows shown, as the browser keeps them.
    #viewScrollUnclamped = false;
    // The row a new page size brings to the top of the view, once the geometry over it is made (ADR-0015).
    #topRowOwed: number | undefined;
    #layerInputs: Readonly<Record<string, unknown>> | undefined;
    // The Reference Outline options (ADR-0057), and the colouring, columns and key colours they make.
    #referenceOptions: ReferenceOutlineOptions = { referencesIn: undefined, onReferenceKeyColoursChanged: undefined, outlinedColumns: undefined };
    readonly #referenceOutlines = new ReferenceOutlines();
    // Point (ADR-0051): the options, and the pointing outline as a Focus and an Extent of its own, a one-range
    // selection moved by the transitions the Selection's own use; the span of the text its Reference occupies, and
    // the text as the core last wrote it. Pointing stands only while the editor's text is still that text and its
    // caret stands after it: anything typed ends it, as a caret moved does.
    #point: PointOptions = {
        pointAt: undefined, referenceText: undefined, cycleReference: undefined, onPointStateChanged: undefined, onPointArrowFromOutside: undefined,
    };
    #pointer: Selection | undefined;
    #pointStart = 0;
    #pointLength = 0;
    #pointWritten: string | undefined;
    #pointDragging = false;
    // Whether what Point wrote was handed in from outside (ADR-0058): no outline stands over this grid's cells for
    // it, and F4 and the arrows change nothing while it stands. With it, the edit as it was before that write.
    #pointedFromOutside = false;
    #pointBefore: PointBefore | undefined;
    // Whether what Point wrote stands with no outline, which stood over rows another set has replaced (#takeRowSet):
    // the next arrow or press points afresh, from where pointing starts among the rows shown, and writes in its place.
    #pointAside = false;
    #pointStateTold: GridPointState = 'none';
    // Completion (ADR-0051): the declaration, the answer shown and the candidate chosen in it, the number of the last
    // question asked (an answer to any other is stale and dropped, DC-18), and the text and caret the core last saw,
    // so a caret the field reports moved asks again and one that stayed does not. The caret is -1 while not known.
    #completeEditorText: CompleteEditorText | undefined;
    #completion: { readonly answer: EditorCompletion; selected: number } | undefined;
    #completionAsked = 0;
    #completionSeen: { readonly text: string; readonly caret: number } | undefined;
    readonly #completionBox: CompletionBox;
    // The declaration this grid is pointed at through (ADR-0058), and what stops hearing it.
    #pointedAt: GridPointedAt<Row> | undefined;
    #pointedAtStop: (() => void) | undefined;
    // Whether the last press on the rows or the header was handed over: the click, the double click and the context
    // menu that follow it are the same gesture, and keep no meaning of their own.
    #pressHandedOver = false;
    // A press handed over whose button is still down: a move onto another cell or column hands the drag over once.
    #pointedDrag: PointedDrag | undefined;
    // The last press handed over: a drag from it is handed over behind it.
    #pointedPressHandOver: Promise<void> = Promise.resolve();
    // The dashes and the outlined columns as last worked out, held so a pass that changed neither finds them the same.
    // The coloured text (ADR-0057): the grid's highlights and their stylesheet, and the layers beneath the Cell Editor
    // and the Formula Bar's field, made the first time a References function is declared.
    #referenceText: ReferenceTextLayers | undefined;
    // The editor surface an IME composes in, which its layer never shows over.
    #composingIn: HTMLInputElement | undefined;
    #pointDashesAsked: { readonly asked: PointDashesAsked; readonly range: SelectionRange | undefined } | undefined;
    #outlinedColumnsAsked: { readonly own: readonly GridOutlinedColumn[] | undefined; readonly declared: readonly GridOutlinedColumn[] | undefined;
        readonly both: readonly GridOutlinedColumn[] | undefined; } | undefined;
    // Options handed over while an edit is open: the next pass tells the Point state as they left it, when it is not
    // what the Consumer last heard, through whichever handler it now hands.
    #pointStateOwed = false;
    #shadeHolds = false;

    // The Settled Mark (ADR-0220).
    #passOwed = false;
    #settledShown: boolean | undefined;
    // What a Framework Adapter still owes the page, each held until it stands: a slot's contents a round trip away
    // (ADR-0410 section 5).
    #settledHolds = 0;
    #destroyed = false;

    constructor(root: HTMLElement, options: GridOptions<Row>, reports: BrowserReports) {
        this.#root = root;
        // Never one beyond a grid this root is nested in, as the C# script finds it.
        const around = root.parentElement?.closest<HTMLElement>('[data-ex-input-root], .ex-grid');
        this.#inputRoot = around?.hasAttribute('data-ex-input-root') === true ? around : root;
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
        this.#stylesheetNotice = notice;
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
        // The Heading Mark's layer over the Row Headings (ADR-0008, 2026-10-13): a zero-size sticky layer at the
        // Viewport's left edge, as the Row Headings are, and above them. It stands only while it holds a mark.
        this.#rowMarkLayer = element('div', { class: 'ex-heading-marks-rows', 'aria-hidden': 'true' });
        // Where the two Headings meet (ADR-0050, item 1): one element for the grid's life, in the band while the
        // Row Headings stand. Paint only; the header's own press reads the offsets to know it was pressed.
        this.#cornerBox = element('div', { class: 'ex-headings-corner', 'aria-hidden': 'true' });
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
        this.#messageSlot = new ShownSlot('cellMessage', element('span', {}));
        this.#loadingSlot = new ShownSlot('loadingIndicator', element('div', { class: 'ex-loading-indicator' }));
        // The completion list and hint, beneath the surface typed in, outside the scroller as the message is (ADR-0051).
        this.#completionBox = new CompletionBox(doc, this.#ids, (index, answer) => { this.#acceptCandidate(index, answer); });
        this.#rowMarks = new GridMarks(doc, () => {
            this.#marksStamp += 1;
            this.#invalidate();
        });
        // The status line sits where the C# core's holds it (ADR-0014, ADR-0043, ADR-0130): after the rows, outside
        // the scroller.
        this.#status = element('div', { class: 'ex-status', role: 'presentation' });
        this.#offScreen = element('span', {});
        this.#goToSelection = element('button', { type: 'button', 'data-ex-claims': 'click' });
        this.#goToSelection.addEventListener('click', this.#onGoToSelection);
        this.#selectWholeResult = element('button', { type: 'button', 'data-ex-claims': 'click' });
        this.#selectWholeResult.addEventListener('click', this.#onSelectWholeResult);
        this.#markResult = element('button', { type: 'button', class: 'ex-mark-result', 'data-ex-claims': 'click' });
        this.#markResult.addEventListener('mousedown', this.#onMarkResultPress);
        this.#markResult.addEventListener('click', this.#onMarkResult);
        // The pager stands after the rows, ahead of the status line, as the C# core's does (ADR-0015).
        this.#pager = element('div', { class: 'ex-pager', role: 'presentation' });
        const pagerButton = (text: string, turned: () => void): HTMLButtonElement => {
            const button = doc.createElement('button');
            Object.assign(button, { type: 'button', textContent: text });
            button.setAttribute('data-ex-claims', 'click');
            button.addEventListener('click', turned);
            return button;
        };
        this.#pageBack = pagerButton('‹', this.#onPageBack);
        this.#pageShown = element('span', {});
        this.#pageOn = pagerButton('›', this.#onPageOn);
        this.#pager.append(this.#pageBack, this.#pageShown, this.#pageOn);
        this.#summary = new SummaryQuestions(() => {
            this.#invalidate();
        });
        this.#summaryBox = new SummaryBox(doc);
        this.#summaryBox.element.addEventListener('contextmenu', this.#onSummaryContextMenu);
        this.#stops.push(() => { this.#summaryBox.element.removeEventListener('contextmenu', this.#onSummaryContextMenu); });
        this.#bar = new FormulaBar(doc, this.#barHooks, { text: undefined, nameBox: undefined });
        // A caret moved in the bar's text, as in the editor's (ADR-0051), and the events the coloured text follows,
        // heard on the band whichever field stands in it.
        this.#bar.band.addEventListener('keyup', this.#onBarCaret);
        this.#bar.band.addEventListener('mouseup', this.#onBarCaret);
        this.#hearSurface(this.#bar.band);
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
            this.#wordNotice();
            const onScroll = (): void => {
                this.#onScroll();
            };
            this.#scroller.addEventListener('scroll', onScroll, { passive: true });
            // Every input, heard on the Input Root ahead of the grid's own listeners, so a Pending Commit holds it first
            // (ADR-0380 section 3), and holds what is made on the product's surfaces around the grid too (ADR-0240).
            const inputRoot = this.#inputRoot;
            for (const type of heldEventTypes) inputRoot.addEventListener(type, this.#onInput, true);
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
                () => { for (const type of heldEventTypes) inputRoot.removeEventListener(type, this.#onInput, true); },
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
                () => { root.removeEventListener('mousemove', this.#onRestMove); },
                () => { root.removeEventListener('mouseleave', this.#onRestLeave); },
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
        this.#wordNotice();
        this.#invalidate();
    }

    // The stylesheet's notice says the Consumer's words where `commandLabel` has some, as the C# core's does. Called
    // after `#commit`, which takes the `commandLabel` it asks.
    #wordNotice(): void {
        const words = this.#labelOf(reloadNoticeLabelIds.stylesheet);
        if (this.#stylesheetNotice.textContent !== words) this.#stylesheetNotice.textContent = words;
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
     * the open edit (ADR-0034), which says so itself, or while its commit is pending (ADR-0380). A range reaching outside the grid, a Focus outside the
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
        // The Consumer's call is answered here: a commit pending makes it false, and nothing takes it again.
        if (!this.#commitEdit(false)) return false;
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
        // Revealed in the Viewport; the page is not moved for it, as a Consumer's placement is no move the grid made
        // (ADR-0204): a Name Box's Go To scrolls the grid, never the page under it.
        if (this.#apply(this.#held.selection.place(range, focus, now))) {
            this.#revealOwed = { target: focus, extending: undefined, rowStart: false, stepRows: 0 };
            this.#revealHeaderOwed = undefined;
        }
        return true;
    }

    /**
     * Places the Selection on `cell` alone and takes it as the current match a Find step found there (ADR-0332 §5),
     * as the step's own answer would land: a Consumer that answers a step by showing other rows, a Sheet showing the
     * Sheet the match is on, places the match itself under the version it shows them with, and gives the step up. The
     * find panel stays open with the keyboard. The cell wears `ex-match-mark-current` while the panel holds a search,
     * and the next step goes on from it. Answers false, and places nothing, under a version the grid has moved past;
     * with an edit open it throws, since a step's answer never lands over one.
     */
    placeMatch(cell: CellPosition, rowSequenceVersion: number): boolean {
        if (this.#destroyed) throw new Error('This grid was destroyed: a grid destroyed places nothing.');
        if (this.#edit !== undefined) throw new Error('A match is placed with no edit open: a Find step never lands over one (ADR-0332).');
        this.#source.publishGathered?.();
        const extent = this.#extent;
        if (!Number.isInteger(cell.row) || !Number.isInteger(cell.column) || cell.row < 0 || cell.column < 0
            || cell.row >= extent.rowCount || cell.column >= extent.columnCount) {
            throw new RangeError(`The match (row ${String(cell.row)}, column ${String(cell.column)}) is outside the grid's `
                + `${String(extent.rowCount)} rows × ${String(extent.columnCount)} columns (ADR-0332).`);
        }
        if (rowSequenceVersion !== this.#handedVersion()) {
            this.#onRefused?.({ kind: 'placement', reason: 'orderMoved' });
            return false;
        }
        // Landed as a step's own answer lands: the find panel stays open with the keyboard where it is. A step still
        // out is answered by it, so what that step's source says afterwards, a give-up included, changes nothing.
        this.#findStepping?.abort();
        this.#findStepping = undefined;
        this.#reconcileSelection();
        const found = cellPosition(cell.row, cell.column);
        this.#findCurrent = { cell: found, version: this.#sequence() };
        this.#findCurrentHeader = undefined;
        this.#setFindOutcome({ kind: 'found' });
        this.#apply(this.#held.selection.click(found, this.#extent));
        this.#revealOwed = { target: found, extending: undefined, rowStart: false, stepRows: 0 };
        this.#revealHeaderOwed = undefined;
        this.#followArmed = { focus: found, target: found };
        this.#answered += 1;
        this.#invalidate();
        return true;
    }

    /** Stops hearing everything and empties the root. */
    destroy(): void {
        if (this.#destroyed) return;
        this.#destroyed = true;
        this.#cancelSettle?.();
        this.#cancelAnnounce?.();
        this.#cancelBoxTold?.();
        this.#cancelFollow?.();
        this.#cancelMessage?.();
        this.#cancelRest?.();
        this.#repaint?.cancel();
        this.#stopEdgeTimer();
        this.#stopHighlightTimer();
        this.#copy?.abort();
        this.#fill?.abort();
        this.#errorStepping?.abort();
        this.#errorStepping = undefined;
        this.#findStepping?.abort();
        this.#findStepping = undefined;
        // A commit pending as the grid goes is cancelled: nothing is to be written, and its held inputs go with it.
        this.#pending?.controller.abort();
        this.#pending = undefined;
        this.#heldRowCommands.clear();
        // Work of the Consumer's is cancelled, and work still waiting never runs (ADR-0240 section 12).
        const works = this.#works;
        this.#works = [];
        this.#workUnderWay = undefined;
        for (const work of works) {
            work.controller.abort();
            if (!work.started) work.neverRan();
        }
        // An edit open as the grid goes ends with it: the Consumer hears the end of every open (ADR-0050
        // section 6). The typing goes with the grid, which is the Consumer's own doing.
        this.#edit?.slot?.dispose();
        this.#edit = undefined;
        this.#tellEditing();
        this.#endPointing();
        this.#tellPointState();
        // A declaration this grid answered for is answered no more: its steps find no grid.
        this.#bindPointedAt(undefined);
        // The keys go with the edit: a Consumer outlining a table's column for them hears that none is read (ADR-0057).
        this.#referenceOutlines.tellKeys(this.#colouring(), this.#referenceOptions.onReferenceKeyColoursChanged);
        // The coloured text's highlights are this grid's own, and go with it.
        this.#referenceText?.highlights.unregister();
        // A Chrome slot's contents are taken away with the grid, as a Template cell's are.
        const popover = this.#popover;
        this.#popover = undefined;
        // What the opener's closed throws is thrown once the rest of the grid has gone.
        let failed: { readonly error: unknown } | undefined;
        if (popover?.kind === 'find') popover.panel.dispose();
        else if (popover?.kind === 'consumer') {
            try {
                popover.closed?.();
            } catch (error: unknown) { // oxlint-disable-line ex-grid/no-catch -- rethrown below, once destroy has run the rest
                failed = { error };
            }
        } else if (popover !== undefined) popover.items.dispose();
        for (const stop of this.#stops) stop();
        this.#unsubscribe?.();
        this.#rowMarks.destroy();
        this.#summary.destroy();
        this.#summaryBox.hide();
        this.#completionBox.hide();
        this.#messageSlot.hide();
        this.#loadingSlot.hide();
        this.#bar.dispose();
        // Every Template cell's content is taken away with the grid (ADR-0020).
        for (const byColumn of this.#templates.values()) {
            for (const mounted of byColumn.values()) this.#unmount(mounted);
        }
        this.#templates.clear();
        this.#root.replaceChildren();
        for (const name of ['tabindex', 'dir', 'id', 'role', 'aria-multiselectable', 'data-ex-reveal', 'data-ex-follow',
            'data-ex-follow-focus', 'aria-activedescendant', 'data-ex-settled', 'class', 'style', 'aria-rowcount', 'aria-colcount',
            'data-ex-focus-visible', 'data-ex-pending-commit']) {
            this.#root.removeAttribute(name);
        }
        if (failed !== undefined) throw failed.error;
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

        const { pinnedColumnCount = 0, virtualiseColumns = true } = options;
        const { cellMetrics, presentationDefaults: defaults }: { readonly cellMetrics?: unknown; readonly presentationDefaults?: unknown } = options;
        if (cellMetrics !== undefined && !(cellMetrics instanceof CellTextMetrics)) {
            throw new TypeError('cellMetrics is a CellTextMetrics: the widths the grid charges each glyph (ADR-0016).');
        }
        if (defaults !== undefined && !(defaults instanceof GridPresentationDefaults)) {
            throw new TypeError('presentationDefaults is a GridPresentationDefaults, as a Wrapper makes one (ADR-0030).');
        }
        // ADR-0028's precedence, one rung for the Wrapper: the grid's own value, then the Wrapper's, then the preset.
        // oxlint-disable-next-line ex-grid/no-stand-in-default -- ADR-0028: an untouched grid is compact
        const density = options.density ?? defaults?.density ?? 'compact';
        const resolved = GridMetrics.resolve({
            density,
            ...(cellMetrics === undefined ? {} : { cellMetrics }),
            ...(defaults === undefined ? {} : { defaults }),
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
        const { showFillHandle, onFill }: { readonly showFillHandle?: unknown; readonly onFill?: unknown } = options;
        if (showFillHandle !== undefined && typeof showFillHandle !== 'boolean') throw new TypeError('showFillHandle is true or false.');
        if (onFill !== undefined && typeof onFill !== 'function') throw new TypeError('onFill is a function of the fill\'s intent.');
        const { highlightFocusRow, highlightHoverRow }: { readonly highlightFocusRow?: unknown; readonly highlightHoverRow?: unknown } = options;
        if (highlightFocusRow !== undefined && typeof highlightFocusRow !== 'boolean') {
            throw new TypeError('highlightFocusRow is true or false.');
        }
        if (highlightHoverRow !== undefined && typeof highlightHoverRow !== 'boolean') {
            throw new TypeError('highlightHoverRow is true or false.');
        }
        const { cellMessage }: { readonly cellMessage?: unknown } = options;
        if (cellMessage !== undefined && typeof cellMessage !== 'function') {
            throw new TypeError('cellMessage is a function of the row and the column (ADR-0034).');
        }
        const { declaredKeys: declaring, onDeclaredKey }: { readonly declaredKeys?: unknown; readonly onDeclaredKey?: unknown } = options;
        if (declaring !== undefined && !Array.isArray(declaring)) throw new TypeError('declaredKeys is a list of keys in the canonical form (ADR-0050, item 14).');
        if (onDeclaredKey !== undefined && typeof onDeclaredKey !== 'function') {
            throw new TypeError('onDeclaredKey is a function of the key pressed (ADR-0050, item 14).');
        }
        const declaredKeys = declareKeys(options.declaredKeys);
        const { findError }: { readonly findError?: unknown } = options;
        if (findError !== undefined && typeof findError !== 'function') {
            throw new TypeError('findError is a function of the error step\'s request and an abort signal (ADR-0381).');
        }
        const stepKey = findError === undefined ? undefined : ['F8', 'Shift+F8'].find(key => declaredKeys.has(key));
        if (stepKey !== undefined) {
            throw new TypeError(`declaredKeys names ${stepKey}, which is the error step on a grid given findError: the key would mean two things `
                + '(ADR-0381, ADR-0050 item 14).');
        }
        const { onHeaderGroupToggled }: { readonly onHeaderGroupToggled?: unknown } = options;
        if (onHeaderGroupToggled !== undefined && typeof onHeaderGroupToggled !== 'function') {
            throw new TypeError('onHeaderGroupToggled is a function of the toggle (ADR-0400).');
        }
        const groupKey = onHeaderGroupToggled === undefined ? undefined : headerGroupKeys.find(key => declaredKeys.has(key));
        if (groupKey !== undefined) {
            throw new TypeError(`declaredKeys names ${groupKey}, which opens or closes a Header Group on a grid given onHeaderGroupToggled: the `
                + 'key would mean two things (ADR-0400 section 4, ADR-0050 item 14).');
        }
        const copyKey = options.hideCopyWithHeaders === true ? undefined : copyWithHeadersKeys.find(key => declaredKeys.has(key));
        if (copyKey !== undefined) {
            throw new TypeError(`declaredKeys names ${copyKey}, which is Copy with headers on a grid that offers it: the key would mean two `
                + 'things (ADR-0005, ADR-0050 item 14). A grid given hideCopyWithHeaders leaves it to be declared.');
        }
        if (declaredKeys.size > 0 && onDeclaredKey === undefined) {
            throw new TypeError('declaredKeys names keys, but no onDeclaredKey hears them: the keys would be taken from the page and nobody told '
                + '(ADR-0050, item 14).');
        }
        const { stripeRows, rowKind }: { readonly stripeRows?: unknown; readonly rowKind?: unknown } = options;
        if (stripeRows !== undefined && typeof stripeRows !== 'boolean') throw new TypeError('stripeRows is true or false.');
        const { pageSize }: { readonly pageSize?: unknown } = options;
        if (pageSize !== undefined && (typeof pageSize !== 'number' || !Number.isInteger(pageSize) || pageSize < 1)) {
            throw new RangeError('A page holds at least one row; continuous scrolling is pageSize left unset (ADR-0015).');
        }
        const { rangeRequestMargin }: { readonly rangeRequestMargin?: unknown } = options;
        if (rangeRequestMargin !== undefined && (typeof rangeRequestMargin !== 'number' || !Number.isSafeInteger(rangeRequestMargin) || rangeRequestMargin < 0)) {
            throw new RangeError('rangeRequestMargin is a whole number of rows, at least 0 (ADR-0183).');
        }
        if (rowKind !== undefined && typeof rowKind !== 'function') throw new TypeError('rowKind is a function of the row (ADR-0024).');
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
        const { rowHeadings, rowHeadingWidth, hideHeader }: {
            readonly rowHeadings?: unknown; readonly rowHeadingWidth?: unknown; readonly hideHeader?: unknown;
        } = options;
        if (rowHeadings !== undefined && typeof rowHeadings !== 'function') {
            throw new TypeError('rowHeadings is a function of the row\'s position (ADR-0050, item 1).');
        }
        if (rowHeadingWidth !== undefined && (typeof rowHeadingWidth !== 'number' || !Number.isFinite(rowHeadingWidth) || rowHeadingWidth <= 0)) {
            throw new RangeError(`rowHeadingWidth is a finite, positive number of pixels (ADR-0050, ADR-0028), not ${typeof rowHeadingWidth === 'number' ? String(rowHeadingWidth) : typeof rowHeadingWidth}.`);
        }
        if (hideHeader !== undefined && typeof hideHeader !== 'boolean') throw new TypeError('hideHeader is true or false.');
        const summary = this.#summaryOptionsOf(options);
        const { copyCellCap = defaultCellCap, pasteByteCap = defaultPasteByteCap } = options;
        const { copyAnswer }: { readonly copyAnswer?: unknown } = options;
        if (copyAnswer !== undefined && typeof copyAnswer !== 'function') throw new TypeError('copyAnswer is a function of the copy asked (ADR-0050, item 9).');
        const { copyAnswerAsync }: { readonly copyAnswerAsync?: unknown } = options;
        if (copyAnswerAsync !== undefined && typeof copyAnswerAsync !== 'function') throw new TypeError('copyAnswerAsync is a function of the copy asked (ADR-0152).');
        if (!Number.isSafeInteger(copyCellCap) || copyCellCap < 1) {
            throw new RangeError(`copyCellCap is a whole number of at least one cell, not ${copyCellCap}: the cap may be raised, `
                + 'not used to switch copying off (ADR-0005).');
        }
        if (!Number.isSafeInteger(pasteByteCap) || pasteByteCap < 1) {
            throw new RangeError(`pasteByteCap is a whole number of at least one byte, not ${pasteByteCap}: the cap may be raised, `
                + 'not used to switch pasting off (ADR-0005).');
        }
        const { headerGroups, headerLayout } = this.#layoutOf(options.headerGroups, columns, pinnedColumnCount, columnsChanged,
            options.onHeaderGroupToggled !== undefined);
        // The band comes off the scroll budget, and the refusal names the band (ADR-0032): a Viewport that
        // cannot hold it and one row shows headers and no data, which reads as an empty result.
        // A hidden header takes no band (ADR-0050, item 1).
        const bandPx = hideHeader === true ? 0 : bandHeightPxOf(headerLayout, metrics);
        const formulaBar = formulaBarDeclarations(options);
        // The Formula Bar takes its band of the declared height as well, and the rows what both leave (ADR-0051).
        const barPx = formulaBar.shown ? metrics.formulaBarHeightPx : 0;
        if (formulaBar.shown && height !== 'stretch' && height <= barPx + bandPx + metrics.rowHeightPx) {
            throw new RangeError(`viewportHeight (${height}px) must exceed the Formula Bar's ${barPx}px, the header band's `
                + `${bandPx}px and one row of ${metrics.rowHeightPx}px: each takes its band of it (ADR-0051, ADR-0028).`);
        }
        if (headerLayout.tierCount > 0 && hideHeader !== true && height !== 'stretch' && height <= bandPx + metrics.rowHeightPx) {
            throw new RangeError(`viewportHeight (${height}px) must exceed the header band — ${headerRowsOf(headerLayout)} tiers × `
                + `${metrics.headerHeightPx}px = ${bandPx}px — plus one row of ${metrics.rowHeightPx}px (ADR-0032).`);
        }
        return {
            source, own, rows: options.rows, metrics, declared: options.columns, columns, autoWidths, columnsChanged,
            width, height, pinnedCount: pinnedColumnCount, virtualiseColumns, headerGroups, headerLayout,
            onSelectionChanged: options.onSelectionChanged, copyCellCap, copyAnswer: options.copyAnswer, copyAnswerAsync: options.copyAnswerAsync, onRefused: options.onRefused, onAction: options.onAction,
            marks,
            onPaste: options.onPaste, pasteMaySpill: options.pasteMaySpill === true, pasteByteCap, onClear: options.onClear,
            showFillHandle: options.showFillHandle === true, onFill: options.onFill,
            highlightFocusRow: options.highlightFocusRow === true,
            highlightHoverRow: (options.highlightHoverRow ?? options.presentationDefaults?.highlightHoverRow) === true,
            cellMessage: options.cellMessage, declaredKeys, onDeclaredKey: options.onDeclaredKey,
            stripeRows: (options.stripeRows ?? options.presentationDefaults?.stripeRows) === true, pageSize: options.pageSize, rowKind: options.rowKind,
            // oxlint-disable-next-line ex-grid/no-stand-in-default -- ADR-0183: the margin is 0 until whoever reads ahead sets it
            rangeRequestMargin: options.rangeRequestMargin ?? 0,
            onUndo: options.onUndo, onRedo: options.onRedo,
            onEdit: options.onEdit, onEditingChanged: options.onEditingChanged, onEditDiscarded: options.onEditDiscarded,
            onOverwriteNotice: options.onOverwriteNotice, onCellDoubleClick: options.onCellDoubleClick, onLeave: options.onLeave,
            onColumnWidthChanged: options.onColumnWidthChanged, onColumnOrderChanged: options.onColumnOrderChanged,
            contextCommands: options.contextCommands, commandLabel: options.commandLabel, hideCopyWithHeaders: options.hideCopyWithHeaders === true,
            chrome: checkedChrome<Row>(options.chrome), hideColumnMenu: options.hideColumnMenu === true, onColumnHidden: options.onColumnHidden,
            onPinnedCountChanged: options.onPinnedCountChanged, onHeaderGroupToggled: options.onHeaderGroupToggled, culture, temporal,
            onReplace: options.onReplace, findMatch: options.findMatch, findError: options.findError, findsHeaders: options.findsHeaders !== false,
            cellPaint: CellPaint.take(options, this.#cellPaint), headerClickSelects: headerClickSelects === true, formulaBar,
            rowHeadings: options.rowHeadings, rowHeadingWidth: options.rowHeadingWidth, hideHeader: hideHeader === true,
            dataEdge: options.dataEdge, ...summary, referenceOutlines: referenceOutlineOptions(options),
            point: pointOptions(options), completeEditorText: checkedCompleteEditorText(options), pointedAt: checkedPointedAt(options.pointedAt),
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
        columnsChanged: boolean, toggleHeard: boolean): { readonly headerGroups: readonly HeaderGroup[]; readonly headerLayout: HeaderGroupLayout } {
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
            // A toggle stops being heard with no change to the groups: its refusal is asked whatever is kept (ADR-0400).
            // A layout resolved afresh asks it in resolve.
            HeaderGroupLayout.refuseToggles(headerGroups, toggleHeard);
            return { headerGroups: this.#headerGroups, headerLayout: this.#headerLayout };
        }
        const headerLayout = HeaderGroupLayout.resolve({
            groups: headerGroups,
            columnNames: columns.map(({ column }) => column.name),
            pinnedCount,
            toggleHeard,
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
        if (taken.headerLayout.tierCount !== this.#headerLayout.tierCount || taken.hideHeader !== this.#hideHeader) {
            this.#painted = new Map();
            this.#placeholders = new Map();
        }
        this.#headerLayout = taken.headerLayout;
        this.#onSelectionChanged = taken.onSelectionChanged;
        this.#copyCellCap = taken.copyCellCap;
        this.#copyAnswer = taken.copyAnswer;
        this.#copyAnswerAsync = taken.copyAnswerAsync;
        this.#onRefused = taken.onRefused;
        this.#onAction = taken.onAction;
        this.#rowMarks.bind(taken.marks, taken.rowKind);
        this.#rangeRequestMargin = taken.rangeRequestMargin;
        if (taken.pageSize !== this.#pageSize) {
            // A new size shows the first row shown before — the page's first, or the first painted without a pager —
            // at the top of the page that holds it, kept inside the pages there are as the geometry is made.
            // Nothing painted yet shows no row, and the first page is shown from its top.
            const first = this.#pageSize === undefined ? this.#visible?.start : this.#pageIndex * this.#pageSize;
            this.#pageIndex = taken.pageSize === undefined || first === undefined ? 0 : Math.floor(first / taken.pageSize);
            this.#topRowOwed = first;
            this.#pageSize = taken.pageSize;
            this.#viewScrollUnclamped = true;
            if (taken.pageSize === undefined) {
                this.#pager.remove();
            } else if (!this.#pager.isConnected) {
                this.#root.insertBefore(this.#pager, this.#status.isConnected ? this.#status : this.#announce);
            }
        }
        if (taken.stripeRows !== this.#stripeRows) {
            this.#stripeRows = taken.stripeRows;
            // A Placeholder wears its stripe too, and is painted again with it.
            this.#placeholders = new Map();
        }
        this.#rowKind = taken.rowKind;
        this.#pasteHeard = taken.onPaste;
        this.#pasteMaySpill = taken.pasteMaySpill;
        this.#showFillHandle = taken.showFillHandle;
        this.#fillHeard = taken.onFill;
        // A handle taken away takes its drag with it: nothing it reached is raised, and the rest of the gesture is
        // no selection drag either.
        if (!taken.showFillHandle && this.#handleDrag !== undefined) this.#setDragging(false);
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
        this.#declaredKeys = taken.declaredKeys;
        this.#onDeclaredKey = taken.onDeclaredKey;
        if (taken.cellMessage !== this.#cellMessage) {
            const heard = this.#cellMessage !== undefined;
            this.#cellMessage = taken.cellMessage;
            // The pointer is heard for a rest only while somebody asks why (ADR-0021's fifth entry); a message
            // opened before goes with the question.
            if (taken.cellMessage === undefined) {
                this.#root.removeEventListener('mousemove', this.#onRestMove);
                this.#root.removeEventListener('mouseleave', this.#onRestLeave);
                this.#closeMessage();
                this.#cancelMessage?.();
                this.#cancelMessage = undefined;
                this.#dropRest();
            } else if (!heard) {
                this.#root.addEventListener('mousemove', this.#onRestMove);
                this.#root.addEventListener('mouseleave', this.#onRestLeave);
                // The Focus may already stand on a cell with something to say: it is asked as a Focus arriving is.
                this.#scheduleMessage(this.#held.selection.focus);
            } else {
                // Another function is another judgement (a ruleset evaluated again): what stands is asked again, and
                // goes if the cell has nothing more to say (ADR-0034).
                const shown = this.#shownMessage;
                if (shown !== undefined) {
                    this.#closeMessage();
                    this.#showMessageAt(shown.cell);
                    this.#invalidate();
                }
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
        this.#chrome = taken.chrome;
        this.#hideColumnMenu = taken.hideColumnMenu;
        this.#onColumnHidden = taken.onColumnHidden;
        this.#onPinnedCountChanged = taken.onPinnedCountChanged;
        this.#onHeaderGroupToggled = taken.onHeaderGroupToggled;
        // A toggle no longer heard remembers no Focus for an answer that will not come.
        if (taken.onHeaderGroupToggled === undefined) this.#toggleFocus = undefined;
        this.#culture = taken.culture;
        this.#temporal = taken.temporal;
        this.#onReplace = taken.onReplace;
        this.#findMatch = taken.findMatch;
        this.#findError = taken.findError;
        this.#findsHeaders = taken.findsHeaders;
        this.#cellPaint = taken.cellPaint;
        this.#headerClickSelects = taken.headerClickSelects;
        if (taken.rowHeadings !== this.#rowHeadings) {
            this.#rowHeadings = taken.rowHeadings;
            // A Placeholder wears its position's heading, and is painted again with new labels.
            this.#placeholders = new Map();
        }
        this.#rowHeadingWidth = taken.rowHeadingWidth;
        this.#hideHeader = taken.hideHeader;
        this.#dataEdge = taken.dataEdge;
        this.#onSummarize = taken.onSummarize;
        this.#showSelectionSummary = taken.showSelectionSummary;
        if (taken.summaryFigures !== this.#summaryFigures) this.#summaryChoice = undefined;
        this.#summaryFigures = taken.summaryFigures;
        this.#onSelectionSummaryChanged = taken.onSelectionSummaryChanged;
        this.#onSummaryFiguresChanged = taken.onSummaryFiguresChanged;
        this.#referenceOptions = taken.referenceOutlines;
        const pointWithdrawn = taken.point.pointAt === undefined || taken.point.referenceText === undefined;
        this.#point = taken.point;
        const pointed = this.#edit;
        if (pointed !== undefined && pointWithdrawn && pointed.mode === 'point') {
            // Pointing taken away mid-edit: what it wrote stays, and the edit goes on as typing leaves it.
            this.#endPointing();
            this.#setDragging(false);
            pointed.mode = this.#modeAfterPointing(pointed);
        }
        this.#pointStateOwed = pointed !== undefined;
        if (taken.completeEditorText === undefined && this.#completeEditorText !== undefined) this.#closeCompletion();
        this.#completeEditorText = taken.completeEditorText;
        this.#bindPointedAt(taken.pointedAt);
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
        this.#noteRowsLeft();
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
        // A pointer at rest is over another cell now: the rest is waited for again from the next movement (ADR-0034).
        this.#dropRest();
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
        this.#lookForNewLayout();
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
        this.#gateReferenceTexts();
        this.#raiseRangeIfUncovered();
        this.#raiseSelection();
        this.#referenceOutlines.tellKeys(this.#colouring(), this.#referenceOptions.onReferenceKeyColoursChanged);
        if (this.#pointStateOwed) {
            this.#pointStateOwed = false;
            this.#tellPointState();
        }
        void this.#summary.settle(() => this.#summaryAnswerer, this.#onSelectionSummaryChanged);
        this.#lookSettled();
    }

    // Settled while nothing is owed: no pass, no fling's settle, no announcement, no reveal's repaint and
    // no page follow, and the browser has told the box and the ceiling (ADR-0220). A follow armed while the box
    // holds no row waits on the browser to grow the box, which nothing the grid does brings, so it is not owed.
    #lookSettled(): void {
        this.#showSettled(!this.#passOwed && this.#cancelSettle === undefined && this.#cancelAnnounce === undefined
            && this.#cancelBoxTold === undefined
            && this.#repaint === undefined && this.#cancelMessage === undefined
            && (this.#followArmed === undefined || this.#visible === undefined)
            && this.#cancelFollow === undefined
            && this.#clipboardWrites === 0 && this.#viewportReport !== undefined && this.#ceilingPx !== undefined
            && !(this.#popover?.kind === 'column' && this.#popover.panel?.fetching === true)
            && this.#findStepping === undefined && this.#errorStepping === undefined && this.#replacing === 0 && this.#pending === undefined
            && this.#messageAsked === undefined && this.#settledHolds === 0);
    }

    #showSettled(settled: boolean): void {
        if (settled === this.#settledShown) return;
        this.#settledShown = settled;
        this.#root.toggleAttribute('data-ex-settled', settled);
    }

    // ---- Resolving (the C# core's PrepareRender) ------------------------------------------------------

    // The header band: the header row and a tier per level of Header Groups above it (ADR-0032).
    get #bandHeightPx(): number {
        return this.#hideHeader ? 0 : bandHeightPxOf(this.#headerLayout, this.#metrics);
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
        return this.#hideHeader ? 0 : headerRowsOf(this.#headerLayout);
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
        // ceiling less the band (ADR-0013, ADR-0053). Under a pager the arithmetic runs over the page alone: the
        // scrollbar spans the page, and the page's edge is where a drag stops (ADR-0015).
        const size = this.#pageSize;
        if (size !== undefined && this.#pageIndex > this.#pageCount - 1) {
            // The result shrank under the page shown: its new last page is shown from its top, as a turn shows it.
            this.#pageIndex = this.#pageCount - 1;
            this.#scrollTopPx = 0;
            this.#scrollOwed = true;
            this.#hoverRow = undefined;
            this.#dropRest();
        }
        const firstRow = size === undefined ? 0 : this.#pageIndex * size;
        const rows = {
            rowHeightPx: this.#metrics.rowHeightPx,
            viewportHeightPx: visibleHeightPx - band,
            totalRowCount: size === undefined ? total : Math.min(Math.max(total - firstRow, 0), size),
            scrollRoomPx: Math.max(1, (this.#ceilingPx ?? ViewportGeometry.maxScrollHeightPx) - band),
        };
        const geometry = size === undefined ? new ViewportGeometry(rows) : new PageGeometry(firstRow, rows);
        this.#geometry = geometry;
        const top = this.#topRowOwed;
        this.#topRowOwed = undefined;
        if (top !== undefined && top >= firstRow && top < firstRow + rows.totalRowCount) {
            this.#scrollTopPx = geometry.scrollTopAt(geometry.offsetPxOf(top));
            this.#scrollOwed = true;
        }
        // Offsets from a view of other rows may lie past these rows' end. They are kept inside here, as the browser
        // keeps the ones written, from the arithmetic and never read back (ADR-0331, ADR-0021).
        const clampView = this.#viewScrollUnclamped;
        this.#viewScrollUnclamped = false;
        if (clampView) this.#scrollTopPx = Math.min(Math.max(this.#scrollTopPx, 0), geometry.maxScrollTopPx);
        this.#visible = geometry.sliceAt(this.#scrollTopPx);
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
        const leadPx = this.#resolveRowHeadingWidth();
        if (geometry.count === widths.length && geometry.requestedPinnedCount === this.#pinnedCount
            && geometry.viewportWidthPx === width && geometry.devicePixelRatio === this.#devicePixelRatio && this.#leadPx === leadPx
            && widths.every((px, c) => geometry.declaredWidthPxOf(c) === px)) {
            return;
        }
        this.#leadPx = leadPx;
        this.#styles = new ColumnStyles(new ColumnGeometry({
            widthsPx: widths,
            pinnedCount: this.#pinnedCount,
            viewportWidthPx: width,
            leadWidthPx: leadPx,
            ...(this.#devicePixelRatio === undefined ? {} : { devicePixelRatio: this.#devicePixelRatio }),
        }));
        const style = leadPx === 0 ? undefined : `width: ${this.#styles.geometry.leadWidthPx}px`;
        if (style !== this.#rowHeadingStyle) {
            this.#rowHeadingStyle = style;
            this.#placeholders = new Map();
        }
    }

    // The Row Headings' width (ADR-0050, ADR-0028): the Consumer's number where it gave one, otherwise the wider of
    // the first and the last row's labels as the Cell Metrics estimate a cell's text and padding; none without the
    // band. Resolved with the column geometry, so a result that grows a digit widens the band. On a Device Pixel, so
    // every column edge is counted from where the band is painted, as the C# grid counts it (ADR-0090).
    #resolveRowHeadingWidth(): number {
        if (this.#rowHeadings === undefined) return 0;
        if (this.#rowHeadingWidth !== undefined) return ColumnGeometry.onDevicePixel(this.#rowHeadingWidth, this.#devicePixelRatio);
        const asText = this.#metrics.cellMetrics.for('text');
        const width = Math.max(asText.estimatePx(this.#headingAt(0)), asText.estimatePx(this.#headingAt(Math.max(0, this.#totalRows - 1))));
        return ColumnGeometry.onDevicePixel(width, this.#devicePixelRatio);
    }

    #raiseRangeIfUncovered(): void {
        // Mid-fling the range is passing through; settling raises the one landed on (ADR-0004).
        const range = this.#visible;
        if (this.#placeholderMode) return;
        const { window, windowStart } = this.#state;
        // With a margin, the Window must hold the rows either side of those on screen too, or they are asked for now,
        // while those on screen may still be held. The request names the rows on screen all the same (ADR-0183).
        const needed = range === undefined ? undefined : this.#withMargin(range);
        if (range === undefined || needed === undefined || (needed.start >= windowStart && needed.start + needed.count <= windowStart + window.length)) {
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

    // The rows on screen and the Range Request Margin either side, clamped into the total (ADR-0183): what the Window
    // must hold for no request to be made. Never short of the rows on screen, whatever the total says: a Window that
    // does not hold them is uncovered at any margin.
    #withMargin(onScreen: RowRange): RowRange {
        const margin = this.#rangeRequestMargin;
        if (margin === 0) return onScreen;
        const end = onScreen.start + onScreen.count;
        const start = Math.max(0, onScreen.start - margin);
        // A total not known yet bounds nothing: the margin reaches past the Window's end however far it ends.
        const total = this.#state.totalCount;
        const last = total === undefined ? end + margin : Math.max(end, Math.min(total, end + margin));
        return rowRange(start, last - start);
    }

    // ---- The Selection, the keys and the press (ADR-0011, ADR-0012, ADR-0052) ----------------------------

    get #extent(): GridExtent {
        return { rowCount: this.#totalRows, columnCount: this.#columns.length };
    }

    // Drops the Selection when the positions it is written in stop meaning what they meant (ADR-0011): the
    // Row Sequence Version for the rows, which moves on with another source too, and the columns by name,
    // in order, never the array's identity, which a Framework Adapter hands afresh on every render.
    #reconcileSelection(): void {
        const left = this.#reconciledState;
        this.#reconciledState = this.#state;
        const version = this.#sequence();
        const names = this.#columns.map(({ column }) => column.name);
        const columnsMoved = !sameItems(names, this.#columnNames);
        const was = this.#held.selection;
        // A toggle's Focus is let go once anything else has placed or dropped the Focus, or the source is another.
        let toggled = this.#toggleFocus;
        if (toggled !== undefined && (toggled.selection !== was || toggled.source !== this.#source)) {
            toggled = undefined;
            this.#toggleFocus = undefined;
        }
        this.#held = columnsMoved ? HeldSelection.empty(version) : this.#held.under(version);
        if (columnsMoved && toggled !== undefined) {
            // The first columns push after a toggle answers it, whatever it shows: only the Focus it remembered comes
            // back, by name, never the Selection (ADR-0011, ADR-0400 section 9).
            this.#toggleFocus = undefined;
            const kept = this.#focusAfterToggle(toggled, names);
            if (kept !== undefined) {
                this.#held = HeldSelection.of(version, Selection.empty.click(kept, this.#extent));
                this.#revealOwed = { target: kept, extending: undefined, rowStart: false, stepRows: 0 };
                this.#revealHeaderOwed = undefined;
            }
        }
        // A view handed ahead is taken with the version it names, so the Selection and the version arrive together
        // and nothing between them meets an empty one (ADR-0331). One that does not fit the rows shown is not.
        const ahead = this.#viewAhead;
        if (ahead !== undefined && ahead.version === this.#handedVersion()) {
            this.#viewAhead = undefined;
            this.#takeRowSet(ahead.set ?? ++this.#rowSetsGiven, left);
            const extent = this.#extent;
            const fits = (range: SelectionRange): boolean => bottomRow(range) < extent.rowCount && range.leftColumn + range.columnCount <= extent.columnCount;
            // Rows never shown with no place given are left with nothing selected, as a new version leaves them.
            const shown = ahead.selection;
            if (!columnsMoved && shown !== undefined) {
                if (shown instanceof Selection) {
                    if (shown.ranges.every(fits)) this.#held = HeldSelection.of(version, shown);
                } else if (fits(shown.range)) {
                    this.#held = HeldSelection.of(version, Selection.empty.place(shown.range, shown.focus, extent));
                }
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

    // Where a toggle's Focus stands in the columns that answered it (ADR-0011, ADR-0400 section 9): in its column by
    // name, or the group's first member, on its row by Row Key, or by position while the order it was taken in holds.
    #focusAfterToggle(toggled: ToggleFocus, names: readonly string[]): CellPosition | undefined {
        const column = this.#headerLayout.focusColumnAfter(toggled.toggle, names);
        if (column === undefined) return undefined;
        // A row the Window did not hold when the toggle was taken has no key, and one whose key the Window no longer
        // holds is not found by it: either is still its position while the order it was taken in holds.
        const byKey = toggled.key === undefined ? undefined : this.#positionOfKey(toggled.key, toggled.row);
        const row = byKey !== undefined ? byKey : this.#sequence() === toggled.sequence ? toggled.row : undefined;
        return row === undefined || row >= this.#totalRows ? undefined : cellPosition(row, column);
    }

    // A Header Group's toggle pressed, or its key typed, in its turn (ADR-0400 sections 1 and 5): the opposite of what
    // the group shows now is notified, and the Focus remembered for the push that answers it. Nothing on screen changes.
    #toggleGroup(group: HeaderGroup): void {
        const heard = defined(this.#onHeaderGroupToggled, 'the toggle\'s onHeaderGroupToggled');
        this.#reconcileSelection();
        const selection = this.#held.selection;
        const { focus } = selection;
        const toggle = group.toggled(focus === undefined ? undefined : itemAt(this.#columns, focus.column).column.name);
        if (focus === undefined) {
            this.#toggleFocus = undefined;
        } else {
            const rowKey = this.#source.rowKey;
            const held = this.#rowAt(focus.row);
            this.#toggleFocus = {
                toggle, selection, source: this.#source, sequence: this.#sequence(), row: focus.row,
                key: rowKey === undefined || held === undefined ? undefined : keyOf(rowKey, held.row, 'The row a toggle kept the Focus on'),
            };
        }
        heard(toggle);
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
        const before = this.#raised.focus;
        this.#raised = selection;
        const { focus } = selection;
        if (before === undefined ? focus !== undefined : focus === undefined || !sameCell(before, focus)) this.#scheduleMessage(focus);
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
        const press: KeyPress = {
            key: event.key, ctrl: event.ctrlKey, shift: event.shiftKey, alt: event.altKey, meta: event.metaKey,
            metaIsPrimary: this.#metaIsPrimary,
        };
        if (modifierKeys.has(event.key)) {
            // The Alt key alone is the one modifier a Consumer may declare (ADR-0100), heard where the grid holds the
            // keyboard: its root, its Keyboard Field, or an editor surface.
            const edit = this.#edit;
            const held = event.target === this.#root || event.target === this.#keyField
                || (edit !== undefined && (this.#inEditor(edit, event.target) || event.target === this.#bar.text));
            if (event.key === 'Alt' && held) this.#raiseDeclaredKey(event, press);
            return;
        }
        // The page's text selection is never the grid's to copy (CP-10): a key on the root drops it before the
        // browser runs the command it is the default of, so Ctrl+C fires its copy event on the root. Not from the
        // Keyboard Field (ADR-0080), whose caret is the document's selection then, and which an IME needs.
        if (event.target === this.#root) {
            const selection = this.#root.ownerDocument.getSelection();
            if (selection !== null && selection.rangeCount > 0) selection.removeAllRanges();
        }
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
        if (popover !== undefined && escape && (onGrid || inPopover) && !innerPopupOpen(popover)) {
            this.#taken(event);
            this.#closePopover(true);
            return;
        }
        const edit = this.#edit;
        // The Formula Bar's text is the editor's second surface, and its keys are the editor's (ADR-0051).
        if (edit !== undefined && (this.#inEditor(edit, event.target) || event.target === this.#bar.text)) {
            if (this.#raiseDeclaredKey(event, press)) return;
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
            // A declared key is the Consumer's to answer while the edit stays as it is, waiting for the keyboard too.
            if (this.#raiseDeclaredKey(event, press)) return;
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
        // A key on the grid while a menu's Chrome contents still owe the opening focus, a round trip away on a
        // circuit, is the menu's, answered against the place it keeps as a key on an item is (ADR-0410 section 5).
        if (popover !== undefined && (popover.kind === 'column' || popover.kind === 'context')
            && popover.items.keyOnGrid(event.key, event.shiftKey, event.ctrlKey || event.altKey || event.metaKey)) {
            this.#taken(event);
            return;
        }
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
        // A declared key is the Consumer's, and resolves to nothing of the core's (ADR-0050, item 14): raised, and
        // it moves nothing.
        if (action.kind === 'none' && this.#raiseDeclaredKey(event, press)) return;
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
        // Ctrl+Shift+C copies with headers, on a grid that offers it (ADR-0005), as the Context Menu's item does: the key
        // fires no copy event, so the write takes the asynchronous route, inside the press's activation. Not past an edit
        // left open (ED-26): it would copy the value the edit has not yet replaced.
        if (!this.#hideCopyWithHeaders && this.#edit === undefined && copiesWithHeaders(press)) {
            this.#taken(event);
            this.#commandCopy(true);
            return;
        }
        // F8 and Shift+F8 step to the next and the previous error cell, on a grid given findError (ADR-0381). Not
        // past an edit left open (ED-26): the step would move the Focus out from under it.
        const errorStep = this.#findError === undefined || this.#edit !== undefined ? undefined : errorStepOf(press);
        if (errorStep !== undefined) {
            this.#taken(event);
            this.#stepToError(errorStep === 'previous');
            return;
        }
        // Alt+Shift+↓ and ↑ open and close the lowest Header Group over the Focus's column that answers, on a grid
        // given onHeaderGroupToggled (ADR-0400 section 4); where none answers, the key is the page's. Not past an
        // edit left open (ED-26), whose keys they are.
        const groupKey = this.#onHeaderGroupToggled === undefined || this.#edit !== undefined ? undefined : headerGroupKeyOf(press);
        if (groupKey !== undefined) {
            const focus = this.#held.selection.focus;
            const group = focus === undefined ? undefined : this.#headerLayout.toggleOver(focus.column, groupKey === 'expand');
            if (group === undefined) return;
            this.#taken(event);
            this.#toggleGroup(group.group);
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
        // A key with nothing selected starts at the first cell on screen, and with nothing painted it has nothing to
        // start at; a key with a Focus moves from it whatever the box shows meanwhile, a box too small for a row
        // included. A key typed as the box grows back moves, whether or not the browser has reported the new size
        // yet (ADR-0040; spine rule 6), as the C# core's does.
        const start = this.#firstVisibleCell();
        // PageUp and PageDown move by the rows fully visible, at least one (ADR-0012): one in a box that holds none.
        const viewportRows = geometry === undefined ? 1 : Math.max(1, Math.floor(geometry.viewportHeightPx / this.#metrics.rowHeightPx));
        // The Consumer's edge is asked on the arrows only: Home and End resolve to the same moves and keep ADR-0012's
        // meaning (ADR-0050, item 2).
        const edge = this.#dataEdge !== undefined && key.startsWith('Arrow') ? this.#dataEdge : undefined;
        const extent = this.#extent;
        const move = moveByKey(action, this.#held.selection, extent, start, viewportRows, edge, this.#selectAllContext(extent));
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
    #pressRowMark(pressed: PressedRow<Row>, marked: boolean, left = false): void {
        // Pointed at from outside, a press on the rows or the header is handed over instead (ADR-0058): the
        // stylesheet lets it through to them, and this is the guard behind it, for a click no press came before.
        if (this.#pointedAtNow()) return;
        const marks = this.#rowMarks.marks;
        if (marks === undefined) return;
        if (pressed.source !== this.#source) {
            this.#refuseRowMark('sourceChanged');
            return;
        }
        this.#source.publishGathered?.();
        this.#reconcileSelection();
        // Held behind earlier input, as an action's (#pressAction).
        const now = left ? { refused: 'rowLeftTheWindow' as const } : this.#pressedRowNow(pressed);
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
    // Under a pager it lines up the page's rows instead, as positions, since a single gesture does not vault past the
    // page, and the whole result is offered beside the count (ADR-0015). A press names what was in force at it: the
    // page it was made on, though turned, resized or the pager removed since, or the whole result, though a pager
    // was added since.
    #pressHeaderMark(press: HeaderMarkPress | undefined): void {
        // Pointed at from outside, as for a row's checkbox (#pressRowMark).
        if (this.#pointedAtNow()) return;
        const marks = this.#rowMarks.marks;
        if (marks === undefined || !this.#markPressHolds(press)) return;
        if (press === undefined ? this.#pageSize !== undefined : press.paged) {
            const page = press === undefined ? this.#pageRows() : press.page;
            if (page !== undefined) this.#raiseMark(marks, { kind: 'positions', ranges: [page], rowSequenceVersion: this.#state.rowSequenceVersion });
            return;
        }
        const { counts } = marks;
        this.#raiseMark(marks, {
            kind: 'allRows', marked: rowMarkHeaderPress(counts === undefined ? 'some' : rowMarkHeaderState(counts)),
            rowSequenceVersion: this.#state.rowSequenceVersion,
        });
    }

    // Whether a press on a mark that names rows by position still names them: made on what a source since replaced
    // painted, or under an order that has moved since, it marks nothing and is refused once, for the Consumer to word
    // (MK-9). A click no press came before names what is there now.
    #markPressHolds(press: MarkPress | undefined): boolean {
        if (press === undefined || (press.source === this.#source && press.sequence === this.#sequence())) return true;
        this.#onRefused?.({ kind: 'mark', reason: press.source === this.#source ? 'orderMoved' : 'sourceChanged' });
        return false;
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
    #pressAction(pressed: PressedRow<Row>, resolved: ResolvedColumn<Row>, action: GridAction, left = false): void {
        // Pointed at from outside, a press on the rows or the header is handed over instead (ADR-0058): the
        // stylesheet lets it through to them, and this is the guard behind it, for a click no press came before.
        if (this.#pointedAtNow()) return;
        const column = resolved.column.name;
        // Made on what a source since replaced painted: its row is another source's, whatever the new one holds
        // there, so the refusal names no row.
        if (pressed.source !== this.#source) {
            this.#refuseAction({ kind: 'action', reason: 'sourceChanged', row: undefined, column, action: action.name });
            return;
        }
        this.#source.publishGathered?.();
        this.#reconcileSelection();
        // A press held behind earlier input whose row left the Window meanwhile has lost it, though it came back
        // (ADR-0240's note of 2026-10-19).
        const now = left ? { refused: 'rowLeftTheWindow' as const } : this.#pressedRowNow(pressed);
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
        // A press past an open editor commits it first, as any click away does; a Reject or a refusal holds it,
        // and the press keeps no meaning of its own (ADR-0034).
        if (!this.#answerRowCommand()) return;
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
        if (!this.#answerRowCommand()) return;
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
        // Pointed at from outside, the press is handed over and means nothing here (ADR-0058).
        if (this.#pointedAtNow()) {
            this.#handOverRowsPress(event);
            return;
        }
        this.#pressHandedOver = false;
        // Only a press on the rows' own box, whose cells take no pointer events: its offsets are the
        // Viewport's, which the arithmetic reads (ADR-0012).
        if (event.button !== 0 || event.target !== this.#viewport) return;
        // Read before anything below changes what is painted: the handle is grabbed where the user saw it
        // (ADR-0050, item 5).
        const grabbed = event.shiftKey || event.ctrlKey || event.metaKey ? undefined : this.#fillHandleUnder(event);
        this.#reconcileSelection();
        const at = this.#cellUnder(event);
        if (at === undefined) return;
        if (this.#inRowHeadings(event)) {
            this.#pressRowHeading(event, at.row);
            return;
        }
        this.#answered += 1;
        // A press on a cell while editing may point, and then commits nothing: the keyboard stays in the editor
        // (ADR-0051).
        if (this.#pointPress(event, at)) {
            event.preventDefault();
            // The press is the user's own way back to the edit: a keyboard that had gone to a control on the page
            // comes back with it, to the surface the edit is typed in, its selection what Point wrote (ADR-0058, SH-35).
            const surface = this.#edit === undefined ? undefined : this.#surfaceOf(this.#edit);
            if (surface !== undefined && this.#root.ownerDocument.activeElement !== surface) surface.focus({ preventScroll: true });
            return;
        }
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
        if (grabbed !== undefined && this.#startHandleDrag(grabbed)) return;
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
            if (!this.#headingHears(event)) return;
            this.#moveHeadingDrag(event);
            return;
        }
        if (this.#pointedDrag !== undefined) {
            this.#movePointedDrag(event);
            return;
        }
        if (event.target !== this.#viewport) return;
        const at = this.#cellUnder(event);
        if (at === undefined) return;
        if (this.#pointDragging) {
            this.#pointDrag(at);
            return;
        }
        if (this.#handleDrag !== undefined ? this.#moveHandlePointer(at) : this.#apply(this.#held.selection.extendTo(at, this.#extent))) this.#invalidate();
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
        if (this.#headingDrag && this.#headingHears(event)) this.#moveHeadingDrag(event);
        // A fill-handle drag is raised where it is released, and over no cell where it was last reached.
        if (this.#handleDrag !== undefined) {
            this.#finishHandleDrag(event.target === this.#viewport ? this.#cellUnder(event) : undefined);
            return;
        }
        this.#setDragging(false);
    };

    // Whether a Heading drag takes this move or release: the Column Headings are what one over the header band is
    // over, so a Row Heading drag learns nothing there, as the C# core refuses it (ADR-0050, item 1).
    #headingHears(event: MouseEvent): boolean {
        return this.#headingAxis !== 'rows' || !(event.target instanceof Node && this.#header.contains(event.target));
    }

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

    // ---- A flagged cell's message (ADR-0034) -----------------------------------------------------------------

    // The Focus moved: a message open belongs to the cell left and goes at once, and the cell reached is asked
    // about only once the Focus has stood on it for the delay, so a run of arrows across flagged cells asks
    // nothing. Until the delay has run the grid is not settled: whether a message opens is known only then.
    #scheduleMessage(target: CellPosition | undefined): void {
        if (this.#closeMessage()) this.#invalidate();
        this.#cancelMessage?.();
        this.#cancelMessage = undefined;
        if (this.#cellMessage === undefined || target === undefined) return;
        this.#cancelMessage = this.#reports.clock.setTimer(() => { this.#onMessageSettled(target); }, popoverDelayMs);
    }

    #onMessageSettled(target: CellPosition): void {
        this.#cancelMessage = undefined;
        if (this.#destroyed) return;
        this.#showMessageAt(target);
        this.#lookSettled();
    }

    // The pointer is heard only for its rest (ADR-0021's fifth entry): each move puts the rest off, and only a
    // pointer stopped over the rows asks about the cell under it. Over the header, a control of its own or the
    // message itself nothing is asked, and what is open stands. A move records where and when; the one timer
    // waits out what is left of the delay, so a pointer crossing the grid arms nothing per event.
    readonly #onRestMove = (event: MouseEvent): void => {
        if (event.target !== this.#viewport) {
            this.#dropRest();
            return;
        }
        this.#restAt = { x: event.offsetX, y: event.offsetY, movedAt: this.#reports.clock.now() };
        this.#cancelRest ??= this.#reports.clock.setTimer(this.#onRestWaited, popoverDelayMs);
    };

    readonly #onRestWaited = (): void => {
        this.#cancelRest = undefined;
        const at = this.#restAt;
        if (at === undefined || this.#destroyed) return;
        const still = this.#reports.clock.now() - at.movedAt;
        if (still < popoverDelayMs) {
            this.#cancelRest = this.#reports.clock.setTimer(this.#onRestWaited, popoverDelayMs - still);
            return;
        }
        this.#restAt = undefined;
        this.#onRest(at.x, at.y);
    };

    #dropRest(): void {
        this.#cancelRest?.();
        this.#cancelRest = undefined;
        this.#restAt = undefined;
    }

    #onRest(x: number, y: number): void {
        if (this.#cellMessage === undefined || this.#edit !== undefined || this.#dragging) return;
        // Over a Row Heading there is no cell (ADR-0050, item 1).
        if (this.#rowHeadingStyle !== undefined && this.#styles.geometry.isInLead(x, this.#scrollLeftPx)) return;
        const cell = this.#cellAt(x, y);
        const shown = this.#shownMessage ?? this.#messageAsked;
        // Shown, or asked about and not yet answered: a rest on the same cell asks nothing more.
        if (cell === undefined || (shown !== undefined && sameCell(shown.cell, cell))) return;
        // What was open belonged to the cell the pointer left, whether or not this one has anything to say.
        if (this.#closeMessage()) this.#invalidate();
        this.#showMessageAt(cell);
    }

    // Leaving the grid takes the message with it, whichever way it opened.
    readonly #onRestLeave = (): void => {
        this.#dropRest();
        if (this.#closeMessage()) this.#invalidate();
    };

    // The one place the Consumer is asked why (ADR-0034), never on the painting path, and only for the cell settled
    // on. While an edit is open its own message outranks it, and mid-drag a message would sit under the release.
    #showMessageAt(target: CellPosition): void {
        const ask = this.#cellMessage;
        if (ask === undefined || this.#edit !== undefined || this.#dragging || target.column >= this.#columns.length) return;
        const held = this.#rowAt(target.row);
        if (held === undefined) return;
        const { column } = itemAt(this.#columns, target.column);
        const said: unknown = ask(held.row, column);
        if (!isThenable(said)) {
            this.#openMessage(target, held.row, column, said);
            return;
        }
        // Answered later: opened once answered, where nothing has taken the question away meanwhile (the Focus moved,
        // the pointer left, an edit opened) and the cell still holds the row it was asked about. Until then whether a
        // message opens is not known, and the grid is not settled.
        const asked = { cell: target };
        this.#messageAsked = asked;
        // oxlint-disable-next-line ex-grid/no-catch -- a failed answer leaves no question out, and the failure is rethrown
        void Promise.resolve(said).then(answer => {
            if (this.#messageAsked !== asked || this.#destroyed) return;
            this.#messageAsked = undefined;
            this.#invalidate();
            if (this.#edit === undefined && !this.#dragging && this.#rowAt(target.row)?.row === held.row && this.#columns[target.column]?.column === column) {
                this.#openMessage(target, held.row, column, answer);
            }
        }, (error: unknown) => {
            if (this.#messageAsked === asked && !this.#destroyed) {
                this.#messageAsked = undefined;
                this.#invalidate();
            }
            throw error;
        });
    }

    #openMessage(target: CellPosition, row: Row, column: GridColumn<Row>, said: unknown): void {
        // From an untyped caller anything; a message that is not text would be painted as something it is not.
        if (said === undefined || said === '') return;
        if (typeof said !== 'string') {
            throw new TypeError(`Column '${column.name}': cellMessage answers a sentence or undefined, not ${typeof said} (ADR-0034).`);
        }
        this.#shownMessage = { cell: target, text: said, row, column: column.name };
        this.#invalidate();
    }

    // Whether a message was open, or asked about, which it no longer is.
    #closeMessage(): boolean {
        const asked = this.#messageAsked !== undefined;
        this.#messageAsked = undefined;
        if (this.#shownMessage === undefined) return asked;
        this.#shownMessage = undefined;
        return true;
    }

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
        return text ?? this.#entryAt(at);
    }

    // The entry a cell holds where the Consumer declares none: the painted text, or an unformatted number whole
    // (ADR-0006, note of 2026-10-26).
    #entryAt(at: CellPosition): string {
        const held = this.#rowAt(at.row);
        if (held === undefined) return '';
        const { column } = itemAt(this.#columns, at.column);
        return entryFor(column, column.value(held.row));
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
        if (typed === undefined) this.#startEdit(focus, mode, this.#openingTextAt(focus), 'cell', true);
        else this.#startEdit(focus, mode, typed);
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

    #startEdit(at: CellPosition, mode: OpenEdit<Row>['mode'], text: string, surface: OpenEdit<Row>['surface'] = 'cell', onOwnText = false): void {
        // An edit opening ends the Copied Range, as an edit ends Excel's copy mode (ADR-0170), and a flagged cell's
        // message, which the editor's own outranks from here on (ADR-0034).
        this.#copied = undefined;
        this.#closeMessage();
        // Opening the editor is followed as reaching its cell is (ADR-0204): the page brings it into the window at
        // the nearest edge once it is painted, while the keyboard is still this grid's.
        // Only the cell's own editor, painted: a Formula Bar edit is typed where the user already is, and an editor
        // opened away from a row scrolled out of the Viewport has no cell in the page to bring in.
        const focus = this.#held.selection.focus;
        if (surface === 'cell' && focus !== undefined && sameCell(focus, at) && this.#cellIsPainted(at)) this.#followArmed = { focus, target: at };
        const doc = this.#root.ownerDocument;
        const declared = this.#chrome?.cellEditor;
        const column = itemAt(this.#columns, at.column).column;
        // A Chrome's cellEditor draws the field in a box of the core's (ADR-0390); it hands the field over once
        // drawn, and the keyboard owed is given then, as to the core's own.
        const slot: SlottedEditor | undefined = declared === undefined ? undefined : new SlottedEditor(doc, declared,
            { column: column.name, columnType: column.type, initialText: text },
            (): CellEditorView => {
                const edit = this.#edit;
                if (edit === undefined || edit.slot !== slot) throw new Error('Internal: the cell editor\'s view is read while its edit is not open.');
                return { mode: edit.mode, error: edit.error, pending: this.#pending !== undefined && this.#pending.edit === edit };
            },
            (control, preventScroll, owed) => {
                if (this.#edit?.slot !== slot) return;
                if (owed) this.#focusEditor();
                else this.#takeChromeFocus(control, preventScroll, false);
            },
            () => { if (this.#edit?.slot === slot) this.#endEdit(undefined); },
            // A field drawn anew while the commit is pending takes no input either (ADR-0380 section 3); the pass marks
            // it as the editor stands.
            field => {
                const edit = this.#edit;
                if (edit?.slot !== slot) return;
                field.readOnly = this.#pending !== undefined && this.#pending.edit === edit;
                this.#invalidate();
            });
        const element = slot?.box ?? doc.createElement('input');
        element.className = 'ex-editor';
        if (element instanceof HTMLInputElement) element.value = text;
        // One uncommitted text in two places: what is typed in the cell is shown in the bar (ADR-0051), and a
        // press back into the cell types there again. Heard on the box a Chrome draws in, as its field's events rise.
        element.addEventListener('input', this.#onEditorTyped);
        element.addEventListener('focusin', this.#onEditorFocused);
        // A caret moved by the field's own keys or a press inside its text (ADR-0051), heard as they rise from a
        // Chrome's field too.
        element.addEventListener('keyup', this.#onEditorCaret);
        element.addEventListener('mouseup', this.#onEditorCaret);
        // The coloured text is laid over the core's own field only, whose text it is measured to match (ADR-0057).
        if (slot === undefined) this.#hearSurface(builtInEditor(element));
        const rowKey = this.#source.rowKey;
        const held = defined(this.#rowAt(at.row), 'the row an edit opens on');
        const opened: OpenEdit<Row> = {
            cell: at, mode, element, slot, source: this.#source, columnNames: this.#columnNames, sequence: this.#sequence(),
            key: rowKey === undefined ? undefined : keyOf(rowKey, held.row, 'The row an edit opens on'),
            seenText: this.#paintedTextAt(at), openedOn: held.row, error: undefined,
            awaits: this.#userWritesOver(at), rebaseOwed: false, surface, untypedText: onOwnText ? text : undefined,
            set: this.#rowSet, awayRow: undefined, leftInOrder: true,
        };
        this.#edit = opened;
        // The editor takes the keyboard in the task that opened it, with the caret placed by the core at the end
        // of the opening text: a key typed next lands in it whenever the pass paints, never in the Keyboard
        // Field behind it (the spine's sixth rule). The pass then sets it over its cell.
        this.#editorFocusOwed = true;
        const firstRow = [...this.#viewport.children].find(child => child.getAttribute('role') === 'row');
        for (const node of this.#paintEditor()) {
            if (!node.isConnected) this.#viewport.insertBefore(node, firstRow === undefined ? null : firstRow);
        }
        slot?.open();
        this.#focusEditor();
        this.#tellEditing();
        this.#invalidate();
        // Its References are asked of the opening text now, where an answer refused is refused (ADR-0057).
        this.#colouring();
        // A character typed onto the cell is typing, and is asked about; an edit opened on the cell's text is not,
        // until the user types or moves the caret in it (ADR-0051). Opened by a press into the Formula Bar, the caret
        // is where the press put it, not known until the field reports it.
        if (mode === 'overwrite') {
            this.#requestCompletion(this.#edit, true, text.length);
        } else {
            this.#closeCompletion();
            this.#completionSeen = { text, caret: surface === 'bar' ? -1 : text.length };
        }
        this.#tellPointState();
    }

    // Raises the key when it is declared (ADR-0050, item 14), and answers whether it was: the key then means nothing
    // more to the core, in either state, and the browser does nothing with it either. Nothing is reconciled first:
    // an open edit is left exactly as it was, and the Selection is the one the grid holds at the key.
    #raiseDeclaredKey(event: KeyboardEvent, press: KeyPress): boolean {
        const heard = this.#onDeclaredKey;
        const key = canonicalKey(press);
        if (heard === undefined || !this.#declaredKeys.has(key)) return false;
        this.#taken(event);
        const pressed: GridDeclaredKeyPress = Object.freeze({
            key, editOpen: this.#edit !== undefined, selection: this.#held.selection, rowSequenceVersion: this.#handedVersion(),
        });
        // An answer still out takes the key's place in the Input Order: every input made after the key is held until it
        // settles, as behind Blazor's awaited OnDeclaredKey (ADR-0240's note of 2026-10-19). A failure the handler throws
        // is thrown here, and one its promise rejects with reaches the page as that rejection, unheard.
        const answer = heard(pressed);
        if (isThenable(answer)) void this.#placeWork(() => answer, undefined);
        return true;
    }

    // A key typed in the Cell Editor: the mode's own keys are the grid's, and every other is the field's
    // (ADR-0010). Escape cancels, F2 switches the mode, Ctrl+Enter fills, and the rest commit and then do what
    // they do with no edit open: Enter and Tab cycle, and Overwrite's arrows move or extend (ED-2).
    #onEditorKey(event: KeyboardEvent, press: KeyPress, edit: OpenEdit<Row>): void {
        const canonical = canonicalKey(press);
        // A caret the field moved since Point wrote ends pointing before the key is decided (ADR-0051).
        this.#settlePointCaret(edit);
        // F4 is taken while an edit is open where the Consumer rewrites References, and only then (ADR-0051).
        if (canonical === 'F4' && this.#point.cycleReference !== undefined) {
            this.#taken(event);
            this.#cycleReferenceKey(edit, this.#point.cycleReference);
            return;
        }
        // A list for another text or caret than the field's is asked again before the key is decided against it.
        if (this.#completionListOpen() && this.#completionMoved(edit)) this.#requestCompletion(edit);
        const listed = this.#completionListOpen();
        const overPoint = listed && this.#completionOverPoint(edit);
        const keys = listed ? overPoint ? 'completionOverPoint' : 'completion'
            : edit.mode === 'caret' ? 'editing' : edit.mode === 'overwrite' ? 'overwrite' : this.#pointedFromOutside ? 'pointed' : 'point';
        if (!editingKeysByMode[keys].includes(canonical)) {
            this.#scrollingKeyInField(event, canonical);
            return;
        }
        if (listed && this.#completionKey(canonical, overPoint)) {
            this.#taken(event);
            this.#invalidate();
            return;
        }
        if (this.#pointKey(canonical, edit)) {
            this.#taken(event);
            this.#invalidate();
            return;
        }
        this.#taken(event);
        switch (canonical) {
            case 'Escape':
                // The uncommitted text was the grid's, and goes with the editor (ADR-0007).
                this.#endEdit(undefined);
                return;
            case 'F2':
                this.#f2(edit);
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

    // A key left to an editor surface that the browser would scroll the grid with is answered here (ADR-0010's note
    // of 2026-09-30, ticket 32): scrolled away, the edited cell left the painted rows. PageUp and PageDown, which move
    // no caret in a one-line field, do nothing on every platform, and Home and End, bound to a scroll where Command is
    // the primary key, place the caret or, with Shift, extend the selection as they do elsewhere. The keys the core
    // claims, Home and End in Overwrite and Point, stay the core's.
    #scrollingKeyInField(event: KeyboardEvent, canonical: string): void {
        if (canonical === 'PageUp' || canonical === 'PageDown') {
            this.#taken(event);
            return;
        }
        const field = event.target;
        if (this.#metaIsPrimary && appleCaretKeys.has(canonical) && (field instanceof HTMLInputElement || field instanceof HTMLTextAreaElement)) {
            this.#taken(event);
            keyDefault(field, event, true);
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
     * refusal held the editor, whose gesture then keeps no meaning of its own (ADR-0034), and false while the
     * commit is pending (ADR-0380): its gesture is taken again once it lands. `retake` is false for a commit made
     * by the Consumer's own call, whose caller is told and has no gesture to take again. `judged` is a verdict
     * answered later, with the signal it was handed: the commit is taken again against the rows as they stand
     * then, and the verdict is not asked again.
     */
    #commitEdit(retake = true, judged?: { readonly verdict: EditVerdict; readonly controller: AbortController }): boolean {
        // One commit at a time (ADR-0142): nothing is committed, nor placed after it, while another is pending. Work of
        // the Consumer's out is no commit: with no edit open there is nothing to commit, and a command it calls as it
        // lands is taken (ADR-0240 section 6).
        if (this.#pending !== undefined && (this.#pending.phase !== 'work' || this.#edit !== undefined)) return false;
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
        const text = this.#committedText(edit);
        const { row } = landing;
        const controller = judged?.controller ?? new AbortController();
        // The Consumer's judgement, at the one moment a veto is still possible (ADR-0034), now or later (ADR-0380).
        const verdict = judged?.verdict ?? this.#verdictOf(column, row, text, controller,
            { edit, cancelled: () => { this.#commitCancelled(edit, name); } }, retake,
            answered => this.#edit === edit && this.#commitEdit(retake, { verdict: answered, controller }));
        if (verdict === undefined) return false;
        if (verdict.kind === 'reject') {
            this.#holdEdit(edit, verdict.message);
            return false;
        }
        // The commit lands over whatever the cell paints now, and the intent says what the user saw of it
        // (ADR-0142). A flag raises the intent unchanged: the mark comes back through what the cell shows.
        this.#supersedeFill();
        // Away, the cell is not painted: what it holds is read from the edit's own row as the grid last held it, so a
        // change made to that row is told as any other is (LV-11), and no cell shown is the user's write (ADR-0331 §5).
        // A row the Consumer replaced with another instance while away is not seen: the grid holds no Window of it.
        const away = this.#away(edit);
        const replaced = away ? textFor(column.column, column.column.value(row)) : this.#paintedTextAt(landing.cell);
        const intent = new GridEditIntent(row, name, text, edit.seenText, replaced, controller.signal);
        // The user's own write (D1), noted before the Consumer hears it, and taken back if refused.
        const written = this.#noteUserWrite(away ? [] : [fromCorners(landing.cell, landing.cell)]);
        const answer = this.#heardNow(() => heard(intent), failed => { this.#intentAnswered(intent, written, failed); });
        if (answer !== undefined) {
            this.#pend(answer, {
                controller, edit, phase: 'intent',
                cancelled: () => {
                    this.#intentAnswered(intent, written, true);
                    this.#commitCancelled(edit, name);
                },
            }, retake, () => {
                this.#intentAnswered(intent, written, false);
                return this.#editLanded(edit, intent, landing.cell);
            }, () => { this.#intentAnswered(intent, written, true); });
            return false;
        }
        return this.#editLanded(edit, intent, landing.cell);
    }

    // A column's verdict on `text` for `row` (ADR-0034), or none yet: answered with a promise, the commit is pending,
    // and `again` takes it up with the verdict once it comes, against the rows as they stand then (ADR-0380).
    #verdictOf(column: ResolvedColumn<Row>, row: Row, text: string, controller: AbortController,
        pending: Pick<PendingCommit<Row>, 'edit' | 'cancelled'>, retake: boolean, again: (verdict: EditVerdict) => boolean): EditVerdict | undefined {
        const { validate, name } = column.column;
        const given: unknown = validate === undefined ? { kind: 'accept' } : validate(row, text, controller.signal);
        if (!isThenable(given)) return checkedVerdict(given, name);
        this.#pend(given, { ...pending, controller, phase: 'verdict' }, retake, answer => again(checkedVerdict(answer, name)));
        return undefined;
    }

    // A Consumer's handler heard (ADR-0142, ADR-0380 section 2): answered now, `answered` ends the time to refuse in
    // at once, told whether the handler threw; answered with a promise, that is handed back, and the caller ends it
    // once the promise settles.
    #heardNow(hear: () => unknown, answered: (failed: boolean) => void): PromiseLike<unknown> | undefined {
        let answer: unknown;
        let returned = false;
        try {
            answer = hear();
            returned = true;
        } finally {
            if (!returned || !isThenable(answer)) answered(!returned);
        }
        return isThenable(answer) ? answer : undefined;
    }

    // The Consumer has answered an Edit Intent, a paste intent or a fill intent: the time to refuse in ends, what the source gathered
    // while it wrote is put out (D5), and the user's write is taken back where it was refused, cancelled or its handler
    // failed.
    #intentAnswered(intent: { seal(): void; readonly refused: boolean }, written: UserWrite, failed: boolean): void {
        intent.seal();
        this.#source.publishGathered?.();
        this.#userWriteAnswered(written, failed || intent.refused);
    }

    // An intent answered and not refused ends the edit as committed; refused, it holds the editor. A source or
    // columns the Consumer handed over while it answered are the commit's own doing: accepted, the edit ends as
    // committed; refused, the editor cannot stand over rows or columns the grid no longer shows, and the edit is
    // discarded with the reason (ADR-0142). Neither raises a notice: its cell would name a position among rows or
    // columns the grid no longer shows. Nor does a commit away whose answer did not hand its own rows back, for the
    // same reason (ADR-0331 §5).
    #editLanded(edit: OpenEdit<Row>, intent: GridEditIntent<Row>, cell: CellPosition): boolean {
        const outlived = this.#outlivedBy(edit);
        const refusal = intent.refusalMessage;
        const amongItsRows = !this.#away(edit) || this.#viewAhead?.set === edit.set;
        if (refusal === undefined || outlived !== undefined) {
            this.#endEdit(refusal === undefined ? undefined : outlived);
            if (refusal === undefined && outlived === undefined && amongItsRows) {
                this.#raiseOverwriteNotice(cell, intent.column, intent.seenText, intent.replacedText);
            }
            return true;
        }
        this.#holdEdit(edit, refusal);
        return false;
    }

    // A commit the user cancelled while its answer was out (ADR-0380 section 3): the editor stays with the text, and
    // the cancel is said aloud and raised, as a refused commit is.
    #commitCancelled(edit: OpenEdit<Row>, column: string): void {
        if (this.#edit === edit) this.#holdEdit(edit, undefined);
        this.#sayRefusal(this.#labelOf(commitCancelledId));
        this.#onRefused?.({ kind: 'commit', reason: 'cancelled', cell: edit.cell, column });
    }

    // ---- A Pending Commit (ADR-0380 section 3) ------------------------------------------------------------
    //
    // A verdict, or the Consumer's answer to an intent, may be a promise. While it is out the commit is pending:
    // the root carries a stamp, set in the handler of the gesture that committed, so the next input's handler sees
    // it however fast it follows; the editor stands read-only with its text and the keyboard; and every input made
    // meanwhile is held, in the order it was made, and taken once the answer has come, against what it left. Typed
    // fast or slow, the same inputs end with the same cells (spine rule 6). An Escape whose keydown sees the stamp
    // cancels instead: an input that acts ahead of its turn (ADR-0240 section 12).

    // A commit's answer is out. `answered` takes it, against the rows as they stand then, and says whether the
    // commit landed: the gesture that made it is then taken again, where `retake` allows, and the held inputs after
    // it. `failed` takes back what an answer that failed leaves, and the failure is rethrown once they are taken.
    #pend<T>(answer: PromiseLike<T>, pending: Pick<PendingCommit<Row>, 'controller' | 'edit' | 'phase' | 'cancelled'>, retake: boolean,
        answered: (value: T) => boolean, failed?: () => void): void {
        const input = this.#input;
        const trigger: HeldInput | undefined = !retake ? undefined
            : this.#retaking ?? (input !== undefined && input.eventPhase !== Event.NONE ? { kind: 'event', event: input } : undefined);
        // The field that holds the keyboard takes no input of its own either, so no IME starts in it meanwhile.
        const holder = typedField(this.#keyboardHolder());
        const stilled = holder === undefined || (pending.edit !== undefined && this.#inEditor(pending.edit, holder)) ? undefined : holder;
        this.#pendingStamps += 1;
        const out: PendingCommit<Row> = { ...pending, stamp: this.#pendingStamps, trigger, held: [], stilled };
        this.#pending = out;
        this.#root.setAttribute('data-ex-pending-commit', String(out.stamp));
        this.#markPending(out, true);
        this.#showSettled(false);
        this.#invalidate();
        // oxlint-disable-next-line ex-grid/no-catch -- a failed answer holds nothing: the inputs are taken, and the failure rethrown
        void Promise.resolve(answer).then(value => {
            if (!this.#unpend(out)) return;
            this.#retaking = out.trigger;
            let landed = false;
            try {
                landed = answered(value);
            } finally {
                this.#retaking = undefined;
                this.#afterAnswer(out, landed);
            }
        }, (error: unknown) => {
            if (!this.#unpend(out)) return;
            try {
                failed?.();
            } finally {
                this.#afterAnswer(out, false);
            }
            throw error;
        });
    }

    // The answer is in, or the commit cancelled: the stamp goes, and the editor takes input again. False where the
    // commit was no longer pending: cancelled already, its editor taken away, or the grid destroyed.
    #unpend(out: PendingCommit<Row>): boolean {
        if (this.#pending !== out || this.#destroyed) return false;
        this.#pending = undefined;
        this.#root.removeAttribute('data-ex-pending-commit');
        this.#markPending(out, false);
        this.#invalidate();
        return true;
    }

    // The editor of a Pending Commit, and the field that holds the keyboard, take no input of their own, and the
    // editor says it is busy (ADR-0380 section 3).
    #markPending(out: PendingCommit<Row>, pending: boolean): void {
        if (out.stilled !== undefined) out.stilled.readOnly = pending;
        const edit = out.edit;
        if (edit === undefined) return;
        const field = this.#editorField(edit);
        if (field !== undefined) field.readOnly = pending;
        if (pending) edit.element.setAttribute('aria-busy', 'true');
        else edit.element.removeAttribute('aria-busy');
        edit.slot?.review();
    }

    // What an answer leaves to take: the gesture, where the commit landed, and the inputs held behind it. A commit
    // that goes on pending, its intent now out, keeps them waiting.
    #afterAnswer(out: PendingCommit<Row>, landed: boolean): void {
        const next = this.#pending;
        if (next !== undefined) {
            next.held.push(...out.held);
            if (next.trigger !== out.trigger) dropTrigger(out.trigger);
            return;
        }
        if (!landed) dropTrigger(out.trigger);
        this.#takeAgain(landed && out.trigger !== undefined ? [out.trigger, ...out.held] : out.held);
    }

    // The held inputs taken in their order. One that makes another commit pending holds the rest behind it. Those that
    // could not be taken are said, once.
    #takeAgain(inputs: readonly HeldInput[]): void {
        let lost = false;
        for (let i = 0; i < inputs.length && !this.#destroyed; i++) {
            const next = this.#pending;
            if (next !== undefined) {
                next.held.push(...inputs.slice(i));
                break;
            }
            if (!this.#takeOne(itemAt(inputs, i))) lost = true;
        }
        if (this.#destroyed) return;
        if (lost) this.#sayRefusal(this.#labelOf(heldInputLostId));
        this.#lookSettled();
    }

    // One held input, taken at its turn where it lands now. A key, typed text and the clipboard's commands go to
    // what holds the keyboard; a press to where it was made. An event made again has no default of its own, so
    // what the browser would have done with one the page left alone is done here: a field types or moves its caret,
    // a control is pressed, Tab moves on, a press focuses. False for one that cannot be taken again.
    #takeOne(input: HeldInput): boolean {
        const holder = this.#keyboardHolder();
        const field = typedField(holder);
        const grids = holder === this.#root || holder === this.#keyField;
        switch (input.kind) {
            case 'paste': {
                const { flavours } = input;
                if (!grids) {
                    if (field !== undefined && flavours !== undefined) typeText(field, flavours.text, 'insertFromPaste');
                } else if (flavours === undefined) {
                    this.#refuse({ kind: 'paste', reason: 'unreadable' });
                } else {
                    this.#pasteFlavours(flavours.text, flavours.html);
                }
                return true;
            }
            case 'copy':
            case 'cut':
                if (field !== undefined && !grids) this.#fieldCopy(field, input.kind === 'cut');
                // The grid's copy, by the asynchronous route a menu's takes: the event that carried data is gone.
                // The grid has no cut of its own (ADR-0005).
                else if (grids && input.kind === 'copy') this.#commandCopy(false);
                return true;
            case 'turn':
                input.take();
                return true;
            case 'event':
                break;
        }
        const { event } = input;
        if (event instanceof CompositionEvent && event.type === 'compositionend') {
            // A composition held whole is taken as its text (ADR-0380 section 3).
            if (field !== undefined && event.data !== '') typeText(field, event.data, 'insertText');
            return true;
        }
        // What follows a key or a composition is its consequence, which taking them again brings about.
        if (event.type === 'compositionstart' || event.type === 'compositionupdate' || event.type === 'beforeinput' || event.type === 'input') {
            return true;
        }
        const again = eventAgain(event);
        if (event instanceof KeyboardEvent) {
            // A key an IME was composing carries none of its text: it cannot be typed again.
            if (event.type === 'keydown' && (event.isComposing || event.keyCode === 229)) return false;
            holder.dispatchEvent(again);
            if (event.type !== 'keydown' || again.defaultPrevented) return true;
            const done = field === undefined ? 'none' : keyDefault(field, event, this.#metaIsPrimary);
            return (done === 'none' ? elementDefault(holder, event) : done) !== 'lost';
        }
        // A press lands where it was made. A pass may have painted that control anew since: it lands then on what stands
        // at the same point, and on nothing where nothing of the grid's does.
        let target = event.target;
        if (!(target instanceof Element) || !target.isConnected) {
            const doc = this.#root.ownerDocument;
            const under = event instanceof MouseEvent && typeof doc.elementFromPoint === 'function' ? doc.elementFromPoint(event.clientX, event.clientY) : null;
            target = under !== null && this.#inputRoot.contains(under) ? under : null;
        }
        if (!(target instanceof Element)) return false;
        target.dispatchEvent(again);
        if (event.type === 'mousedown' && !again.defaultPrevented) {
            const focusable = target.closest<HTMLElement>('input, textarea, select, button, [tabindex]');
            focusable?.focus({ preventScroll: true });
        }
        return true;
    }

    // What holds the keyboard in the grid now: what has DOM focus inside the Input Root, the grid or a surface of the
    // product's around it, or, where the keyboard has left it, what the grid would hand it to (ADR-0080).
    #keyboardHolder(): HTMLElement {
        const active = this.#root.ownerDocument.activeElement;
        if (active instanceof HTMLElement && (this.#root.contains(active) || this.#inProductSurface(active))) return active;
        const edit = this.#edit;
        if (edit !== undefined && edit.element.isConnected) return this.#editorField(edit) ?? edit.element;
        return this.#keyField.isConnected ? this.#keyField : this.#root;
    }

    // A copy or a cut in a text field, taken at its turn: its selected text goes to the clipboard as the field's own
    // copy would put it, and a cut deletes it.
    #fieldCopy(field: HTMLInputElement | HTMLTextAreaElement, cut: boolean): void {
        const start = selectionOf(field, 'start');
        const end = selectionOf(field, 'end');
        if (start === end) return;
        const text = field.value.slice(start, end);
        // oxlint-disable-next-line ex-grid/no-catch -- a field's copy the browser refused is said, as the grid's own is; anything else is rethrown
        void this.#reports.writeClipboard(this.#root, Promise.resolve(fieldCopyFlavours(text))).then(undefined, (error: unknown) => {
            if (this.#destroyed) return;
            if (typeof error === 'object' && error !== null && Reflect.get(error, 'name') === 'NotAllowedError') {
                this.#refuse({ kind: 'copy', reason: 'clipboardUnavailable' });
                return;
            }
            throw error;
        });
        if (cut) typeText(field, '', 'deleteByCut');
    }

    // Every input on the Input Root, heard ahead of everything in it; one not in this grid's order passes. With no
    // commit pending an input in the grid is noted as the input under way, and one on a surface around it is that
    // surface's; while one is, it is held in its turn, nothing inside hearing it and the browser's own meaning
    // prevented, and an Escape cancels the commit instead. A move with no button down is the hover's, and passes. A clipboard
    // key keeps its default, which is the copy, cut or paste event held in its turn after it.
    readonly #onInput = (event: Event): void => {
        const pending = this.#pending;
        if (!this.#holds(event)) return;
        if (pending === undefined) {
            if (event.target instanceof Node && this.#root.contains(event.target)) this.#input = event;
            // The repeats of an Escape that cancelled are that Escape's, and close nothing (ADR-0012).
            if (event instanceof KeyboardEvent && event.type === 'keydown') {
                if (event.repeat && event.key === 'Escape' && this.#escapeCancelled) {
                    event.stopImmediatePropagation();
                    event.preventDefault();
                    return;
                }
                if (!event.repeat) this.#escapeCancelled = false;
            }
            return;
        }
        if (event instanceof MouseEvent && event.type === 'mousemove' && event.buttons === 0) return;
        event.stopImmediatePropagation();
        // Cancellable work placed is what the stamp names, so an Escape cancels it first, behind a commit too (ADR-0240
        // section 12); with none, a commit's Escape cancels the commit, and work no Escape cancels holds it as a key.
        const workPlaced = this.#works.some(work => work.tooLate !== undefined);
        if (cancelsPending(event) && (workPlaced || pending.phase !== 'work')) {
            event.preventDefault();
            if (workPlaced) this.#cancelWork(pending, event instanceof KeyboardEvent && event.repeat);
            else this.#cancelPending(pending);
            return;
        }
        if (event.cancelable && !isClipboardKey(event)) event.preventDefault();
        if (event instanceof MouseEvent && this.#holdRowCommand(event, pending)) return;
        pending.held.push(heldInputOf(event));
    };

    // A row Action or row Mark click held behind earlier input (ADR-0240's note of 2026-10-19): its row is noted now,
    // at the click, with a Mark's painted state, as is the row of its press, and followed while it waits; at its turn it
    // acts on that row, or is refused where the row left the Window meanwhile. Never looked for again in the DOM,
    // where another row's button may stand by then. Its press and release are held as any are, and mean nothing more
    // at their turn. True where the click was one of these, and is taken care of.
    #holdRowCommand(event: MouseEvent, pending: PendingCommit<Row>): boolean {
        const target = event.target;
        if (!(target instanceof Element)) return false;
        const action = this.#actionButtons.get(target);
        const box = this.#markBoxes.has(target);
        if (action === undefined && !box) return false;
        if (event.type === 'mousedown') {
            if (action !== undefined) this.#onActionPress(event);
            else this.#onMarkPress(event);
            return false;
        }
        if (event.type !== 'click') return false;
        let press: PressedRow<Row> | undefined;
        let act: (pressed: PressedRow<Row>, left: boolean) => void;
        if (action !== undefined) {
            press = this.#actionPress?.button === target ? this.#actionPress.pressed : undefined;
            act = (pressed, left) => { this.#pressAction(pressed, action.column, action.action, left); };
        } else {
            press = this.#markPress?.box === target ? this.#markPress.pressed : undefined;
            const marked = this.#markBoxes.get(target) !== true;
            act = (pressed, left) => { this.#pressRowMark(pressed, marked, left); };
        }
        this.#actionPress = undefined;
        this.#markPress = undefined;
        const pressed = press ?? this.#pressedRowOf(target);
        if (pressed === undefined) return true;
        // A row the Window no longer holds at the click is lost already, though its button is still painted.
        const command: HeldRowCommand<Row> = { pressed, left: this.#leftTheWindow(pressed), act };
        this.#heldRowCommands.add(command);
        pending.held.push(this.#rowCommandTurn(command));
        return true;
    }

    // A held row command's turn. An open edit is committed first, as for a click made now: a commit whose answer is out
    // keeps the command, followed still, as the gesture it takes again once it lands.
    #rowCommandTurn(command: HeldRowCommand<Row>): HeldInput {
        const turn: HeldInput = {
            kind: 'turn',
            take: () => {
                const retaking = this.#retaking;
                this.#retaking = turn;
                let committed: boolean;
                try {
                    committed = this.#answerRowCommand();
                } finally {
                    this.#retaking = retaking;
                }
                // A commit whose answer is out takes this turn again once it lands; one held by a Reject or a refusal
                // ends the command, which keeps no meaning of its own.
                if (!committed && this.#pending?.trigger === turn) return;
                this.#heldRowCommands.delete(command);
                if (!committed) return;
                command.act(command.pressed, command.left);
            },
            dropped: () => { this.#heldRowCommands.delete(command); },
        };
        return turn;
    }

    // A click on a row command, answered: the stamp counts it, and an open edit is committed first, as any click away
    // does. False where a Reject, a refusal or an answer still out holds the edit, and the click keeps no meaning of its
    // own, or waits for that answer.
    #answerRowCommand(): boolean {
        this.#answered += 1;
        this.#invalidate();
        return this.#commitEdit();
    }

    // Whether a row command's row is gone from the Window, where the source it was made on is still bound: one a
    // replaced source painted is refused as that at its turn.
    #leftTheWindow(pressed: PressedRow<Row>): boolean {
        return pressed.source === this.#source && !this.#windowHolds(pressed.row);
    }

    // A new Window: a held row command whose row it no longer holds has lost it for good (ADR-0240's note of
    // 2026-10-19). One made on what a replaced source painted is refused as that at its turn.
    #noteRowsLeft(): void {
        for (const command of this.#heldRowCommands) {
            if (!command.left && this.#leftTheWindow(command.pressed)) command.left = true;
        }
    }

    // Whether an input is in this grid's order (ADR-0240 ticket 04): any made in the grid; on the product's surfaces
    // around it, in its Input Root and not inside another grid's or another Input Root, a key, typed text or a
    // clipboard command, and a press only on a control that claims it (`data-ex-claims`), as the C# script holds
    // them. A press on any other control there keeps its own default, which a press taken again could not bring about.
    #holds(event: Event): boolean {
        const { target } = event;
        if (!(target instanceof Node)) return false;
        if (this.#root.contains(target)) return true;
        if (!this.#inProductSurface(target)) return false;
        if (!(event instanceof MouseEvent)) return true;
        const element = target instanceof Element ? target : target.parentElement;
        const claimant = element?.closest('[data-ex-claims]');
        return claimant !== null && claimant !== undefined && this.#inputRoot.contains(claimant);
    }

    // Whether a node stands on a surface of the product's own around the grid: in its Input Root, and in no other
    // grid or Input Root inside it.
    #inProductSurface(node: Node): boolean {
        if (this.#inputRoot === this.#root || !this.#inputRoot.contains(node)) return false;
        const element = node instanceof Element ? node : node.parentElement;
        return element?.closest('[data-ex-input-root], .ex-grid') === this.#inputRoot;
    }

    // Escape on a Pending Commit (ADR-0380 section 3). Its keydown saw the stamp, so the answer has not been taken:
    // the signal is aborted, nothing is written, and the cancel is said. The editor stays with the text, the inputs
    // held before the Escape are taken in it, and the Escape's own turn closes nothing, its repeats included.
    #cancelPending(pending: PendingCommit<Row>): void {
        this.#escapeAnswered = true;
        this.#escapeCancelled = true;
        this.#answered += 1;
        if (!this.#unpend(pending)) return;
        pending.controller.abort();
        pending.cancelled();
        dropTrigger(pending.trigger);
        this.#takeAgain(pending.held);
    }

    // ---- Work of the Consumer's in the Input Order (ADR-0240 sections 6 and 12, ADR-0330 section 10) ----------
    //
    // A command of the Consumer's that no input of the grid's made — a file opened from a button outside it, a save —
    // takes one place in the order: after every input made so far, before any made after it, which are held while its
    // promise is out as they are behind a Pending Commit and taken once it ends, against what it left. Cancellable
    // work is named by the stamp from the moment it is placed until it ends, and an Escape whose keydown sees it
    // cancels it: the input that acts ahead of its turn, decided by what the keydown sees and never by when the work
    // ends. The work decides when it lands; an Escape that came too late is answered in its own turn.

    /**
     * Runs `handling`, work of the Consumer's that no input of the grid's made, in its place in the Input Order
     * (ADR-0240 section 6): at once when nothing is pending, else after the inputs held so far. While the promise it
     * answers is out, every input made in the grid is held, in its order, and taken once it settles; an Escape is held
     * too, and does what it was typed to do. Once the grid is destroyed it runs nothing.
     */
    inOrder(handling: () => void | PromiseLike<void>): Promise<void> {
        if (typeof handling !== 'function') throw new TypeError('inOrder takes the work as a function (ADR-0240).');
        return this.#placeWork(() => handling(), undefined).then(() => undefined);
    }

    /**
     * Runs `handling` in its place in the Input Order as `inOrder` does, and lets the user cancel it with Escape
     * (ADR-0240 section 12, ADR-0330 section 10). From this call until it ends the stamp names it
     * (`data-ex-work-placed`) and no longer says an Escape leaves the grid; from its turn until it ends the root is busy
     * (`aria-busy`), names it (`data-ex-work`) and is painted as a paste under way is. A plain Escape whose keydown sees
     * it placed aborts `handling`'s signal, the first such work's not aborted yet, and does nothing else; one that finds
     * every placed work aborted already is answered as that work ends. The work decides when it lands: up to that point
     * it looks at the signal and rejects with the signal's reason once aborted; past it, it lands whatever the signal
     * says. An Escape that found the work landed is answered in its own turn by `cancelledTooLate(true)`, one that found
     * nothing to cancel by `cancelledTooLate(false)`. Destroying the grid aborts the signal, and work still waiting
     * never runs.
     *
     * @returns True once the work has landed; false when it rejected with its aborted signal's reason, or never ran.
     *   Any other failure is the promise's.
     * @throws Error Called while cancellable work's handling is being called: one Escape could not say which it cancels.
     */
    cancellableInOrder(handling: (signal: AbortSignal) => void | PromiseLike<void>, cancelledTooLate: (landed: boolean) => void): Promise<boolean> {
        if (typeof handling !== 'function' || typeof cancelledTooLate !== 'function') {
            throw new TypeError('cancellableInOrder takes the work and what says a cancel came too late, as functions (ADR-0240).');
        }
        if (this.#workStarting) {
            throw new Error('Cancellable work cannot be placed while cancellable work starts: one Escape could not say which it cancels. Place it once the work under way has ended (ADR-0240).');
        }
        return this.#placeWork(handling, cancelledTooLate);
    }

    #placeWork(handling: (signal: AbortSignal) => void | PromiseLike<void>, tooLate: ((landed: boolean) => void) | undefined): Promise<boolean> {
        if (this.#destroyed) return Promise.resolve(false);
        return new Promise<boolean>((resolve, reject) => {
            const work: PlacedWork = {
                id: ++this.#worksPlaced, controller: new AbortController(), tooLate, neverRan: () => { resolve(false); }, started: false, ended: undefined,
            };
            this.#works.push(work);
            // The stamp names it, and stops saying an Escape leaves, from here on.
            if (tooLate !== undefined) this.#invalidate();
            const start = (): void => { this.#startWork(work, handling, resolve, reject); };
            const pending = this.#pending;
            if (pending === undefined) start();
            else pending.held.push({ kind: 'turn', take: start });
        });
    }

    // The work's turn: called with its signal, and pending while what it answers is out.
    #startWork(work: PlacedWork, handling: (signal: AbortSignal) => void | PromiseLike<void>, resolve: (ran: boolean) => void, reject: (error: unknown) => void): void {
        if (this.#destroyed) {
            resolve(false);
            return;
        }
        work.started = true;
        const cancellable = work.tooLate !== undefined;
        if (cancellable) this.#workUnderWay = work;
        const end = (error: unknown, failed: boolean): void => {
            const { signal } = work.controller;
            const ended = !failed ? 'landed' : signal.aborted && error === signal.reason ? 'cancelled' : 'failed';
            this.#endWork(work, ended);
            if (ended === 'failed') reject(error);
            else resolve(ended === 'landed');
        };
        let answer: void | PromiseLike<void>;
        const starting = this.#workStarting;
        this.#workStarting = starting || cancellable;
        try {
            answer = handling(work.controller.signal);
        // oxlint-disable-next-line ex-grid/no-catch -- the work's own failure is its caller's, answered through the promise
        } catch (error) {
            end(error, true);
            return;
        } finally {
            this.#workStarting = starting;
        }
        if (!isThenable(answer)) {
            end(undefined, false);
            return;
        }
        // Pending from here, the keyboard's field taking no input of its own meanwhile, so no IME starts in it. What the
        // handling itself left pending — work it placed, a commit it made — is taken first, and this work is pending
        // after it, if it has not ended by then.
        let out: PendingCommit<Row> | undefined;
        const pend = (): void => {
            if (work.ended !== undefined || this.#destroyed) return;
            this.#pendingStamps += 1;
            const pending: PendingCommit<Row> = {
                stamp: this.#pendingStamps, controller: work.controller, edit: undefined, phase: 'work', trigger: undefined, held: [],
                cancelled: () => undefined, stilled: typedField(this.#keyboardHolder()),
            };
            out = pending;
            this.#pending = pending;
            this.#markPending(pending, true);
            this.#showSettled(false);
            this.#invalidate();
        };
        const before = this.#pending;
        if (before === undefined) pend();
        else before.held.push({ kind: 'turn', take: pend });
        const settled = (error: unknown, failed: boolean): void => {
            const pending = out;
            const unpended = pending !== undefined && this.#unpend(pending);
            end(error, failed);
            if (unpended) this.#afterAnswer(pending, false);
        };
        // oxlint-disable-next-line ex-grid/no-catch -- the work's failure is its caller's, rejected through the promise placeWork answers
        answer.then(() => { settled(undefined, false); }, (error: unknown) => { settled(error, true); });
    }

    // The work has ended: it leaves the stamp, and the root is no longer busy.
    #endWork(work: PlacedWork, ended: 'landed' | 'cancelled' | 'failed'): void {
        work.ended = ended;
        this.#works = this.#works.filter(placed => placed !== work);
        if (this.#workUnderWay === work) this.#workUnderWay = undefined;
        if (!this.#destroyed) this.#invalidate();
    }

    // A plain Escape whose keydown saw cancellable work placed (ADR-0240 section 12): the first such work not cancelled
    // yet is cancelled; where every one is, the Escape is answered as the first ends. Either way it is answered in its
    // own turn, after the work: too late where the work landed, nothing to cancel where it ended otherwise than by this
    // cancel. It does nothing else, and its repeats nothing at all.
    #cancelWork(pending: PendingCommit<Row>, repeat: boolean): void {
        if (repeat) return;
        this.#escapeAnswered = true;
        this.#escapeCancelled = true;
        this.#answered += 1;
        const placed = this.#works.filter(work => work.tooLate !== undefined);
        const target = placed.find(work => !work.controller.signal.aborted);
        const named = target ?? itemAt(placed, 0);
        target?.controller.abort();
        const tooLate = named.tooLate;
        if (tooLate === undefined) throw new Error('Internal: work an Escape names is cancellable.');
        pending.held.push({
            kind: 'turn',
            take: () => {
                const { ended } = named;
                if (ended === undefined) throw new Error('Internal: an Escape is answered after the work it named.');
                if (named === target && ended === 'cancelled') return;
                try {
                    tooLate(ended === 'landed');
                // oxlint-disable-next-line ex-grid/no-catch -- rethrown outside the order: the inputs held behind the Escape are still taken in it
                } catch (error) {
                    queueMicrotask(() => { throw error; });
                }
            },
        });
        this.#invalidate();
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
        // Away, the cell is not painted: the reading stays owed until its own rows come back (ADR-0331 §5).
        if (this.#away(edit)) return;
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
        // Away among rows another set replaced (ADR-0331 §5): the commit lands on the row the edit was opened on, as the
        // grid last held it, at the edit's own cell among those rows. Gone before the set was replaced, the grid never
        // held it away, and the typing goes with it (ED-21).
        if (this.#away(edit)) {
            return edit.awayRow === undefined ? { kind: 'discard', reason: 'rowLeftTheWindow' } : { kind: 'row', row: edit.awayRow, cell: edit.cell };
        }
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

    // Whether the open edit is away: opened among rows another set has replaced (ADR-0331 §5).
    #away(edit: OpenEdit<Row>): boolean {
        return edit.set !== this.#rowSet;
    }

    // The rows of `set` are shown now (ADR-0331 §5). An open edit leaving its own set is set aside with them: its row
    // as last held, and Point's outline, which stood over the rows left, goes while what it wrote stays, for the next
    // point to replace. Back among its own set, the edit stands over its cell under the version they are shown under
    // now, as when it was opened: the Consumer vouches that a view handed back is of the same rows in the same order.
    //
    // `left` is the state the grid last reconciled under, the edit's own rows: the edited row is read from its Window,
    // the one row the grid holds beyond its Window while the edit is away (ADR-0160), by its key, or without one at
    // its position while the order it opened under holds. A Window that no longer held it leaves none, and a commit
    // away then has no row to land on (ED-21); an order that had moved leaves its position naming no row of its own,
    // back among those rows too, so the commit there is refused as before (LV-20).
    #takeRowSet(set: number, left: GridSourceState<Row> | undefined): void {
        if (set === this.#rowSet) return;
        const edit = this.#edit;
        const wasAway = edit !== undefined && this.#away(edit);
        this.#rowSet = set;
        if (edit === undefined) return;
        if (set === edit.set) {
            if (edit.leftInOrder) edit.sequence = this.#sequence();
            edit.awayRow = undefined;
        } else if (!wasAway) {
            const inOrder = left !== undefined && left.rowSequenceVersion + this.#sequenceOffset === edit.sequence;
            edit.leftInOrder = edit.key !== undefined || inOrder;
            edit.awayRow = left === undefined ? undefined : this.#editedRowAmong(edit, left, inOrder);
        }
        // An outline over the rows left goes, whether or not it still stood: what it wrote stays for the next point.
        if (this.#pointer !== undefined) {
            this.#pointAside = this.#pointingContinues(edit);
            this.#pointer = undefined;
        }
    }

    // The row the open edit was opened on among the Window of `state`: under its key wherever it stands, met once; or,
    // without one, at the edit's position while `inOrder`, the order it was opened under holding there.
    #editedRowAmong(edit: OpenEdit<Row>, state: GridSourceState<Row>, inOrder: boolean): Row | undefined {
        const { window, windowStart } = state;
        const rowKey = this.#source.rowKey;
        if (edit.key !== undefined) {
            if (rowKey === undefined) return undefined;
            const found = window.filter(row => keyOf(rowKey, row, 'A row of the Window') === edit.key);
            return found.length === 1 ? found[0] : undefined;
        }
        const slice = edit.cell.row - windowStart;
        return inOrder && slice >= 0 && slice < window.length ? itemAt(window, slice) : undefined;
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
        // Under an intent's answer, what the Consumer hands over is the commit's own doing, answered when it lands.
        const answering = this.#pending?.edit === edit && this.#pending.phase === 'intent';
        if (outlived !== undefined && !answering) {
            this.#endEdit(outlived);
            return;
        }
        // Away, the rows shown are another set's, and no key among them names the edit's row.
        if (edit.key !== undefined && !this.#away(edit)) {
            const at = this.#positionOfKey(edit.key, edit.cell.row);
            if (at !== undefined) {
                edit.cell = cellPosition(at, edit.cell.column);
                const focus = this.#held.selection.focus;
                if (focus === undefined || !sameCell(focus, edit.cell)) this.#apply(Selection.empty.click(edit.cell, this.#extent));
            }
        }
        // An editor whose text is still the committed value it opened on holds a copy of committed data, not typing:
        // once the Window no longer holds its row it closes as Escape closes it, its field saying it still holds that
        // text and is not composing (ST-2, ADR-0007's note of 2026-10-11). One with typed text stays (ADR-0142).
        if (!answering && !this.#editedRowInWindow(edit) && this.#untouched(edit)) {
            this.#endEdit(undefined);
            return;
        }
        if (edit.rebaseOwed) this.#rebaseEdit(edit);
    }

    // Whether the Window holds the row the editor opened on: with a Row Key, the row under its key, wherever it stands
    // now; without one, the row at the editor's position while the order it opened under holds (ADR-0011, ADR-0142).
    #editedRowInWindow(edit: OpenEdit<Row>): boolean {
        // Away, the rows shown are another set's: the edit's row is set aside, not gone (ADR-0331 §5).
        if (this.#away(edit)) return true;
        if (edit.key !== undefined) return this.#positionOfKey(edit.key, edit.cell.row) !== undefined;
        return this.#sequence() === edit.sequence && this.#rowAt(edit.cell.row) !== undefined;
    }

    // Whether the field the edit is typed in still holds the committed text it opened on and is not composing.
    #untouched(edit: OpenEdit<Row>): boolean {
        if (edit.untypedText === undefined) return false;
        const field = this.#surfaceOf(edit);
        return field !== undefined && this.#composingIn !== field && field.value === edit.untypedText;
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
    // A refusal judges the operation and never the text, so the editor stays with the typing in it. `judged` is a
    // verdict answered later (ADR-0380): the gate is passed again, and the verdict not asked again.
    #commitFill(edit: OpenEdit<Row>, judged?: { readonly verdict: EditVerdict; readonly controller: AbortController }): void {
        if (this.#pending !== undefined) return;
        this.#source.publishGathered?.();
        this.#reconcileSelection();
        if (this.#edit !== edit) return;
        // A fill is positional: once the order has moved since the editor opened, the Selection it was aimed
        // with no longer names those rows (ADR-0142; ADR-0011's note of 2026-10-07). Away among another set's rows,
        // the Selection names that set's rows, never the edit's, whatever version they are shown under (ADR-0331 §5).
        if (this.#away(edit) || this.#sequence() !== edit.sequence) {
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
        const text = this.#committedText(edit);
        // The verdict judges the value, once, against the row the editor opened on, as the Window holds it now
        // or, gone from it, as it opened: a fill's targets may lie outside the Window, with no row to pass for
        // most of them (ADR-0034's fill carve-out), and a fill is never left unjudged. The other rows are the
        // Consumer's to judge in onPaste (ADR-0380 section 4).
        const column = itemAt(columns, edit.cell.column);
        const name = column.column.name;
        const controller = judged?.controller ?? new AbortController();
        const held = this.#rowAt(edit.cell.row);
        const verdict = judged?.verdict ?? this.#verdictOf(column, held === undefined ? edit.openedOn : held.row, text, controller,
            { edit, cancelled: () => { this.#commitCancelled(edit, name); } }, false, answered => {
                if (this.#edit === edit) this.#commitFill(edit, { verdict: answered, controller });
                return false;
            });
        if (verdict === undefined) return;
        if (verdict.kind === 'reject') {
            this.#holdEdit(edit, verdict.message);
            return;
        }
        this.#supersedeFill();
        const intent = new GridPasteIntent(decision.plan, [[text]], [['shownText']], this.#handedVersion(), undefined, controller.signal, edit.cell);
        // The fill lands over its target as entered (ADR-0142, LV-13). The edited cell lay under the editor: a
        // change there is told once the fill lands (LV-11).
        const seen = edit.seenText;
        const replaced = this.#paintedTextAt(edit.cell);
        const written = this.#noteUserWrite(decision.plan.targets);
        // The editor is down before the Consumer hears the fill, so whatever it hands over meanwhile discards
        // nothing: it is the fill's own doing (ADR-0142), and no notice follows another source or other columns.
        let answer: PromiseLike<unknown> | undefined;
        this.#endEdit(undefined, () => {
            answer = this.#heardNow(() => defined(heard, 'the paste\'s listener')(intent), failed => { this.#intentAnswered(intent, written, failed); });
        });
        const landed = (): void => {
            if (this.#outlivedBy(edit) === undefined && !intent.refused) this.#raiseOverwriteNotice(edit.cell, name, seen, replaced);
        };
        if (answer === undefined) landed();
        else this.#intentPending(answer, intent, controller, written, landed);
    }

    // A paste's or a fill's intent answered later (ADR-0380 section 2): it is pending with no editor, and an Escape
    // cancels it aloud. `landed` runs once the Consumer has answered, refused or not.
    #intentPending(
        answer: PromiseLike<unknown>, intent: GridPasteIntent | GridFillIntent, controller: AbortController, written: UserWrite, landed: () => void,
    ): void {
        this.#pend(answer, {
            controller, edit: undefined, phase: 'intent',
            cancelled: () => {
                this.#intentAnswered(intent, written, true);
                this.#refuse({ kind: 'paste', reason: 'cancelled' });
            },
        }, false, () => {
            this.#intentAnswered(intent, written, false);
            landed();
            return true;
        }, () => { this.#intentAnswered(intent, written, true); });
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
        if (this.#endPointing()) this.#setDragging(false);
        // The list and the hint answer the edit, and go with it, as the outline does (ADR-0051).
        this.#closeCompletion();
        this.#completionSeen = undefined;
        this.#tellPointState();
        // An editor taken away under its verdict's answer, its rows or columns gone (ADR-0011): nothing is to be
        // written, the answer is not taken, and the inputs held behind it are taken once the end has been told, the
        // gesture with them, as a discard at once lets it keep its meaning.
        const pending = this.#pending;
        if (pending?.edit === edit && this.#unpend(pending)) {
            pending.controller.abort();
            queueMicrotask(() => {
                if (!this.#destroyed) this.#afterAnswer(pending, true);
            });
        }
        this.#editorFocusOwed = false;
        const element = edit.element;
        const doc = this.#root.ownerDocument;
        // Handed back before the editor goes, so DOM focus never falls to the page between the two; from the
        // Formula Bar too, whose text then reads the Focus cell's again (ADR-0051).
        if (this.#inEditor(edit, doc.activeElement) || doc.activeElement === this.#bar.text) this.#takeKeyboard();
        element.remove();
        edit.slot?.dispose();
        this.#referenceText?.cell.element.remove();
        // A composition the editor took with it is over, whether or not the browser says so.
        this.#composingIn = undefined;
        this.#gateReferenceTexts();
        this.#invalidate();
        if (endedIn !== undefined) endedIn();
        if (discarded !== undefined) this.#onEditDiscarded?.(discarded);
        this.#tellEditing();
    }


    // ---- Point (ADR-0051) -------------------------------------------------------------------------------------

    // The field the edit is typed in: the cell's editor, or the Formula Bar's text. None while a Chrome's cellEditor
    // slot has drawn no field yet (ADR-0390).
    #surfaceOf(edit: OpenEdit<Row>): EditorField | undefined {
        return edit.surface === 'bar' ? this.#bar.text : this.#editorField(edit);
    }

    // The caret in the surface the edit is typed in, or -1 where the field holds none, or none stands yet.
    #caretOf(edit: OpenEdit<Row>): number {
        return this.#surfaceOf(edit)?.selectionStart ?? -1; // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0051: a caret not known is never guessed at, and -1 points nowhere
    }

    // Writes the edit's text into both surfaces and places the selection in the one typed in (ADR-0051): no input
    // event is fired, so it is not typing.
    #writeEditText(edit: OpenEdit<Row>, text: string, start: number, end: number): void {
        this.#writeEditorText(edit, text);
        if (this.#formulaBarDeclared.shown) this.#bar.showText(text);
        const surface = this.#surfaceOf(edit);
        if (surface === undefined) return;
        surface.setSelectionRange(start, end);
        showCaret(surface, end);
    }

    // Whether what Point wrote stands, its outline over this grid's cells or text written from outside, the
    // editor's text is still the text it was written into, and the caret still stands after it.
    #pointingContinues(edit: OpenEdit<Row>): boolean {
        return (this.#pointer !== undefined || this.#pointedFromOutside || this.#pointAside) && this.#editorText(edit) === this.#pointWritten
            && this.#caretOf(edit) === this.#pointStart + this.#pointLength;
    }

    // The outline goes; the text written stays. Whether anything stood.
    #endPointing(): boolean {
        const stood = this.#pointer !== undefined || this.#pointedFromOutside || this.#pointWritten !== undefined;
        this.#pointer = undefined;
        this.#pointAside = false;
        this.#pointWritten = undefined;
        this.#pointDragging = false;
        this.#pointedFromOutside = false;
        this.#pointBefore = undefined;
        return stood;
    }

    // Typing ends pointing: the outline goes, and Point gives way to Overwrite, whose arrows point again wherever
    // the Consumer says a Reference can go, or to Caret in the Formula Bar, which is never in Overwrite (ADR-0051,
    // 2026-09-30; ED-29).
    #typed(edit: OpenEdit<Row>): void {
        const stood = this.#endPointing();
        if (edit.mode === 'point') edit.mode = this.#modeAfterPointing(edit);
        if (stood) this.#invalidate();
        this.#tellPointState();
    }

    // What Point gives way to when typing or a rewrite ends it: Overwrite, or Caret in the Formula Bar.
    #modeAfterPointing(edit: OpenEdit<Row>): 'caret' | 'overwrite' {
        return edit.surface === 'bar' ? 'caret' : 'overwrite';
    }

    // A caret moved away from what Point wrote ends pointing, as typing ends it, and the edit is in Caret: the user
    // has taken the caret into the text (ADR-0051's third round).
    // In Point that has written nothing yet, as F2 leaves it, the caret moved from where it stood ends Point so too.
    #settlePointCaret(edit: OpenEdit<Row>): void {
        if (this.#pointWritten === undefined) {
            if (edit.mode === 'point' && this.#caretOf(edit) !== this.#pointStart) {
                edit.mode = 'caret';
                this.#invalidate();
            }
            return;
        }
        if (this.#pointingContinues(edit)) return;
        if (this.#editorText(edit) !== this.#pointWritten) {
            this.#typed(edit);
            return;
        }
        this.#endPointing();
        if (edit.mode === 'point') edit.mode = 'caret';
        this.#invalidate();
    }

    // The field's own keys or a press inside its text may have moved its caret.
    // A caret moved in the bar's text field, heard on its band: the Name Box beside it is no surface of the edit.
    readonly #onBarCaret = (event: Event): void => {
        if (event.target === this.#bar.text) this.#onEditorCaret();
    };

    readonly #onEditorCaret = (): void => {
        const edit = this.#edit;
        if (edit === undefined) return;
        this.#settlePointCaret(edit);
        // Completion acts at the caret where it now stands (ADR-0051's second round).
        if (this.#completeEditorText !== undefined && this.#completionMoved(edit)) this.#requestCompletion(edit);
        this.#tellPointState();
    };

    // Where the open edit stands with respect to Point (ADR-0058).
    #currentPointState(): GridPointState {
        const edit = this.#edit;
        const { pointAt } = this.#point;
        if (edit === undefined || pointAt === undefined) return 'none';
        if (this.#pointingContinues(edit)) return this.#pointedFromOutside ? 'writtenFromOutside' : 'inPoint';
        const text = this.#editorText(edit);
        const caret = this.#caretOf(edit);
        return caret >= 0 && caret <= text.length && pointAt(text, caret) ? 'inPoint' : 'none';
    }

    // Tells the Consumer the Point state when it is not what the Consumer last heard.
    #tellPointState(): void {
        // A Chrome's cell editor hears the mode where it changed, Point's steps among them (ADR-0390).
        this.#edit?.slot?.review();
        const told = this.#point.onPointStateChanged;
        if (told === undefined) return;
        const state = this.#currentPointState();
        if (state === this.#pointStateTold) return;
        this.#pointStateTold = state;
        told(state);
    }

    // Where pointing starts: the edited cell, or, while the edit is away, the Focus of the rows shown, the first cell
    // where they have none, as Excel points from the active cell of the Sheet shown (ADR-0331 §5).
    #pointOrigin(edit: OpenEdit<Row>): CellPosition {
        if (!this.#away(edit)) return edit.cell;
        return this.#held.selection.focus ?? cellPosition(0, 0);
    }

    /**
     * A key Point may take while editing (ADR-0051): while an outline stands over unchanged text it moves or extends
     * it, the Primary Modifier's to the data edge the Consumer answers, and Shift+Home extends it to the row's first
     * column. Otherwise, in Overwrite or Point, it starts pointing from the edited cell where the Consumer says a
     * Reference can go at the caret, and End there is taken and does nothing (ADR-0058, Q53). What a press outside the
     * grid wrote has no cell here to move from: its keys are taken and move nothing. Whether the key was Point's.
     */
    #pointKey(canonical: string, edit: OpenEdit<Row>): boolean {
        const { pointAt, referenceText } = this.#point;
        const step = pointStep(canonical);
        if (pointAt === undefined || referenceText === undefined || step === undefined) return false;
        const continues = this.#pointingContinues(edit);
        if (continues && this.#pointedFromOutside) {
            // The text stands for a cell of another instance: the Consumer that wrote it moves it there (ADR-0058).
            // Home, End, and the arrows where no Consumer hears them, are taken and move nothing, as the C# core's.
            const arrow = pointArrowOf(canonical);
            const heard = this.#point.onPointArrowFromOutside;
            if (arrow !== undefined && heard !== undefined) heard(arrow);
            return true;
        }
        const extent = this.#extent;
        if (extent.rowCount <= 0 || extent.columnCount <= 0) return false;
        let pointer = continues ? this.#pointer : undefined;
        if (!continues) {
            this.#endPointing();
            const caret = this.#caretOf(edit);
            // Caret's arrows are the field's.
            if (edit.mode === 'caret' || caret < 0 || !pointAt(this.#editorText(edit), caret)) return false;
            // End turns on an End Mode in Excel, which the grid does not have: it writes nothing and commits nothing.
            if (canonical === 'End') return true;
            pointer = Selection.empty.click(this.#pointOrigin(edit), extent);
            this.#pointStart = caret;
            this.#pointLength = 0;
        } else if (pointer === undefined) {
            // Set aside with the rows it stood over: the arrow points afresh from among the rows shown, over what was
            // written (ADR-0331 §5).
            pointer = Selection.empty.click(this.#pointOrigin(edit), extent);
            this.#pointAside = false;
        }
        const { direction, move } = step;
        const edge = this.#dataEdge;
        switch (move) {
            case 'move': this.#pointer = pointer.move(direction, extent); break;
            case 'extend': this.#pointer = pointer.extend(direction, extent); break;
            case 'edge': this.#pointer = pointer.moveToEdge(direction, extent); break;
            case 'extendToEdge': this.#pointer = pointer.extendToEdge(direction, extent); break;
            case 'dataEdge': this.#pointer = pointer.moveToEdge(direction, extent, edge); break;
            case 'extendToDataEdge': this.#pointer = pointer.extendToEdge(direction, extent, edge); break;
            default: return unreachable(move);
        }
        this.#writePointedReference(edit, true);
        return true;
    }

    /**
     * A press on a cell while editing (ADR-0051): it points, at that cell, or with Shift from the outline's Focus or
     * the edited cell, where an outline stands over unchanged text or the Consumer says a Reference can go at the
     * caret. The press does not commit, and the drag it starts extends the outline. Whether it pointed.
     */
    #pointPress(event: MouseEvent, at: CellPosition): boolean {
        const edit = this.#edit;
        const { pointAt, referenceText } = this.#point;
        if (edit === undefined || pointAt === undefined || referenceText === undefined) return false;
        const extent = this.#extent;
        const continues = this.#pointingContinues(edit);
        // What a press outside the grid wrote is replaced by this cell's Reference, in the same place (ADR-0058):
        // there is no outline here to move, so Shift reaches from the edited cell.
        const from = continues ? this.#pointer : undefined;
        if (!continues) {
            this.#endPointing();
            const caret = this.#caretOf(edit);
            // A caret not known is not guessed at: the Reference would land somewhere else.
            if (caret < 0 || !pointAt(this.#editorText(edit), caret)) return false;
            this.#pointStart = caret;
            this.#pointLength = 0;
        }
        // What was pointed among rows since replaced is replaced in the same place, and Shift reaches from where
        // pointing starts, as it does before anything is pointed (ADR-0331 §5).
        this.#pointAside = false;
        this.#pointer = event.shiftKey ? (from ?? Selection.empty.click(this.#pointOrigin(edit), extent)).extendTo(at, extent) : Selection.empty.click(at, extent);
        this.#writePointedReference(edit, false);
        this.#setDragging(true);
        this.#pointDragging = true;
        return true;
    }

    // The pointing drag moved onto `at`: the outline extends to it.
    #pointDrag(at: CellPosition): void {
        const edit = this.#edit;
        const pointer = this.#pointer;
        if (edit === undefined || pointer === undefined || !this.#pointingContinues(edit)) return;
        const reached = pointer.extent;
        if (reached !== undefined && sameCell(reached, at)) return;
        this.#pointer = pointer.extendTo(at, this.#extent);
        this.#writePointedReference(edit, false);
    }

    // F2 moves between the states (ADR-0010), and where pointing is declared between Caret and Point: from Caret it
    // points where the Consumer says a Reference can go at the caret, and from Point it moves the caret again, the
    // outline gone and its Reference left written (ADR-0051).
    #f2(edit: OpenEdit<Row>): void {
        const { pointAt, referenceText } = this.#point;
        const caret = this.#caretOf(edit);
        const points = pointAt !== undefined && referenceText !== undefined && caret >= 0 && pointAt(this.#editorText(edit), caret);
        edit.mode = edit.mode === 'point' ? 'caret' : edit.mode === 'caret' ? (points ? 'point' : 'overwrite') : 'caret';
        if (this.#endPointing()) this.#invalidate();
        // Point here has written nothing: it stands at this caret until the caret moves.
        if (edit.mode === 'point') {
            this.#pointStart = caret;
            this.#pointLength = 0;
        }
        this.#tellPointState();
    }

    /**
     * Writes the Consumer's Reference text for the outline in place of the one written for it before, or the first
     * time at the caret, and leaves the caret after it (ADR-0051). The edit is now in Point. Its moving end is kept on
     * screen as the Extent is (ADR-0052): the pointed cell when the outline moves, the far end when Shift extends it.
     */
    #writePointedReference(edit: OpenEdit<Row>, reveal: boolean): void {
        const pointer = defined(this.#pointer, 'the outline a Reference is written for');
        const referenceText = defined(this.#point.referenceText, 'the Reference text pointing writes');
        const range = elementAt(pointer.ranges, 0);
        const reference: unknown = referenceText(range);
        if (typeof reference !== 'string' || reference === '') {
            throw new TypeError('referenceText answered no text for a pointed range: a Reference is a non-empty string (ADR-0051).');
        }
        const [text, caret] = replaceSpan(this.#editorText(edit), this.#pointStart, this.#pointLength, reference);
        this.#pointLength = reference.length;
        this.#writeEditText(edit, text, caret, caret);
        this.#pointWritten = text;
        edit.mode = 'point';
        this.#pointAside = false;
        // This grid's own cell replaces whatever a press outside it wrote.
        this.#pointedFromOutside = false;
        this.#pointBefore = undefined;
        if (reveal) {
            const target = defined(pointer.extent, 'the outline\'s moving end');
            this.#revealOwed = { target, extending: pointer.focusRange, rowStart: false, stepRows: 0 };
        }
        this.#closeCompletion();
        this.#tellPointState();
        this.#askReferences();
        this.#invalidate();
    }

    /**
     * F4 while an edit is open (ADR-0051, 2026-09-29): the Consumer's rewrite of the text at the selection in the
     * surface typed in, written back and its selection placed: as a selection where the key found one or a caret
     * before the text's end, and as a caret at the selection's end where the caret stood at the text's end (the
     * seventeenth Windows run, Part C; R1 (iv) and (v); run 18a). While pointing it is the whole Reference the outline
     * wrote, so both ends of a pointed range cycle together (R1 (vi)), and pointing goes on over what it became, the
     * caret at its end. After a press outside the grid, F4 changes nothing (ADR-0058).
     */
    #cycleReferenceKey(edit: OpenEdit<Row>, cycle: NonNullable<PointOptions['cycleReference']>): void {
        const pointing = this.#pointingContinues(edit);
        if (pointing && this.#pointedFromOutside) return;
        const text = this.#editorText(edit);
        const start = pointing ? this.#pointStart : this.#caretOf(edit);
        let end = pointing ? this.#pointStart + this.#pointLength : this.#surfaceOf(edit)?.selectionEnd ?? start;
        if (start < 0 || start > text.length) return;
        if (end < start || end > text.length) end = start;
        const answer: unknown = cycle(text, start, end);
        if (answer === undefined) return;
        const { text: rewritten, selectionStart, selectionEnd } = checkedRewrite(answer);
        // The outline's Reference is what lies between the text before it and the text after it, both unchanged; a
        // rewrite that touched either has rewritten more than the outline's Reference, and pointing ends as typing ends it.
        const before = this.#pointStart;
        const after = text.length - this.#pointStart - this.#pointLength;
        const keeps = pointing && rewritten.length >= before + after && rewritten.startsWith(text.slice(0, before))
            && rewritten.endsWith(text.slice(text.length - after));
        const selects = !keeps && (start !== end || start < text.length);
        if (keeps) {
            this.#pointLength = rewritten.length - before - after;
            this.#pointWritten = rewritten;
        } else {
            this.#endPointing();
            if (edit.mode === 'point') edit.mode = this.#modeAfterPointing(edit);
        }
        this.#writeEditText(edit, rewritten, selects ? selectionStart : selectionEnd, selectionEnd);
        // What pointing goes on over takes no list; text rewritten otherwise is asked about at its new caret.
        if (keeps) this.#closeCompletion();
        else this.#requestCompletion(edit);
        this.#tellPointState();
        this.#askReferences();
        this.#invalidate();
    }

    /**
     * Writes `text` into the open edit as Point writes a Reference, for a press a Consumer took outside the grid
     * (ADR-0058). See the handle's `writePointedText`.
     */
    writePointedText(text: string): boolean {
        const given: unknown = text;
        if (typeof given !== 'string' || given === '') throw new TypeError('A text written from outside is a non-empty string (ADR-0058).');
        const edit = this.#edit;
        // A commit waiting on its verdict keeps the text that verdict judges (ADR-0380).
        if (this.#destroyed || edit === undefined || this.#pending !== undefined || this.#currentPointState() === 'none') return false;
        if (!this.#pointingContinues(edit)) {
            // Nothing this Point wrote stands: the text goes in at the caret.
            this.#endPointing();
            this.#pointStart = this.#caretOf(edit);
            this.#pointLength = 0;
        }
        const before: PointBefore = {
            text: this.#editorText(edit), caret: this.#caretOf(edit), mode: edit.mode, pointer: this.#pointer, start: this.#pointStart,
            length: this.#pointLength, written: this.#pointWritten, fromOutside: this.#pointedFromOutside,
        };
        const [written, caret] = replaceSpan(this.#editorText(edit), this.#pointStart, this.#pointLength, text);
        this.#pointer = undefined;
        this.#setDragging(false);
        this.#pointLength = text.length;
        this.#writeEditText(edit, written, caret, caret);
        this.#pointWritten = written;
        this.#pointedFromOutside = true;
        this.#pointBefore = before;
        edit.mode = 'point';
        this.#closeCompletion();
        this.#tellPointState();
        this.#askReferences();
        this.#invalidate();
        return true;
    }

    /** Takes back the text `writePointedText` wrote last, while it still stands (ADR-0058). See the handle's. */
    takeBackPointedText(): boolean {
        const edit = this.#edit;
        const before = this.#pointBefore;
        if (this.#destroyed || edit === undefined || this.#pending !== undefined || before === undefined || !this.#pointedFromOutside
            || !this.#pointingContinues(edit)) return false;
        this.#pointBefore = undefined;
        this.#setDragging(false);
        // A write is taken only where a caret stood (#currentPointState), so the one before it is known.
        if (before.caret < 0) throw new Error('A text written from outside was taken where no caret stood (ADR-0058).');
        this.#writeEditText(edit, before.text, before.caret, before.caret);
        edit.mode = before.mode;
        this.#pointer = before.pointer;
        this.#pointStart = before.start;
        this.#pointLength = before.length;
        this.#pointWritten = before.written;
        this.#pointedFromOutside = before.fromOutside;
        // What Point wrote before stands again, and takes no list; text that was only waiting for a Reference is
        // asked about again, as it was before the press.
        if (this.#pointingContinues(edit)) this.#closeCompletion();
        else this.#requestCompletion(edit);
        this.#tellPointState();
        this.#askReferences();
        this.#invalidate();
        return true;
    }

    // ---- Completion (ADR-0051) --------------------------------------------------------------------------------

    // Whether a list of candidates is showing, the state in which ↑, ↓, Tab and Escape are the list's. A hint alone
    // takes no key.
    #completionListOpen(): boolean {
        const shown = this.#completion;
        return this.#edit !== undefined && shown !== undefined && shown.answer.candidates.length > 0;
    }

    // Whether the list is open over Point (ADR-0058, "What the tenth Windows run settled"): the caret stands where the
    // Consumer says a Reference can go, outside Caret, whose arrows are the editor's. ←, →, Home, End and the
    // Shift+arrows then point, as they would without the list, where in a list of names they are the editor's.
    #completionOverPoint(edit: OpenEdit<Row>): boolean {
        const { pointAt, referenceText } = this.#point;
        if (edit.mode === 'caret' || pointAt === undefined || referenceText === undefined) return false;
        const text = this.#editorText(edit);
        const caret = this.#caretOf(edit);
        return caret >= 0 && caret <= text.length && pointAt(text, caret);
    }

    // Whether the field's text or caret is not the one the core last saw.
    #completionMoved(edit: OpenEdit<Row>): boolean {
        const seen = this.#completionSeen;
        return seen === undefined || seen.text !== this.#editorText(edit) || seen.caret !== this.#caretOf(edit);
    }

    /**
     * Hands the edit's text and caret to the Consumer (ADR-0051). What is showing is taken down first, and every
     * earlier question is made stale: nothing answers the new text until its own answer comes. An answer given now is
     * shown in the pass already on its way. `listed` false shows the answer's hint alone: for the text Tab wrote, the
     * name just accepted as a rule, whose list would come back on it and keep Tab from moving on (ADR-0058, the tenth
     * Windows run).
     */
    #requestCompletion(edit: OpenEdit<Row>, listed = true, caret = this.#caretOf(edit)): void {
        if (this.#completion !== undefined) {
            this.#completion = undefined;
            this.#invalidate();
        }
        this.#completionAsked += 1;
        const asked = this.#completionAsked;
        const text = this.#editorText(edit);
        this.#completionSeen = { text, caret };
        const complete = this.#completeEditorText;
        // A caret not known is waited for, never guessed: a list for the wrong caret would replace the wrong span.
        if (complete === undefined || caret < 0 || caret > text.length) return;
        const answer: unknown = complete(text, caret);
        if (isThenable(answer)) {
            // A failure of the Consumer's reaches the page as its unhandled rejection, as a source's does (ADR-0025).
            void Promise.resolve(answer).then(later => { this.#showCompletion(asked, text, later, listed); });
            return;
        }
        this.#showCompletion(asked, text, answer, listed);
    }

    // Shows an answer, unless it is stale: asked before a later question, for text that is no longer the editor's,
    // or after the edit closed (ADR-0051, DC-18). Its hint alone where it was asked for without a list.
    #showCompletion(asked: number, text: string, given: unknown, listed: boolean): void {
        const checked = checkedCompletion(given);
        const edit = this.#edit;
        if (this.#destroyed || asked !== this.#completionAsked || edit === undefined || this.#editorText(edit) !== text || checked === undefined) return;
        const answer = listed || checked.candidates.length === 0 ? checked
            : checked.hint === undefined ? EditorCompletion.none : new EditorCompletion({ candidates: [], hint: checked.hint });
        if (answer.isEmpty) return;
        this.#completion = { answer, selected: 0 };
        this.#invalidate();
    }

    // Takes the list and the hint down, and makes any answer still on its way stale. The text and caret it leaves
    // are seen: a caret that stays where it is asks nothing again.
    #closeCompletion(): void {
        if (this.#completion !== undefined) this.#invalidate();
        this.#completion = undefined;
        this.#completionAsked += 1;
        const edit = this.#edit;
        if (edit !== undefined) this.#completionSeen = { text: this.#editorText(edit), caret: this.#caretOf(edit) };
    }

    // A key typed while a list is open (ADR-0051): whether it was the list's. Closed over Point by ←, →, Home, End
    // or a Shift+arrow, the list lets the key point.
    #completionKey(canonical: string, overPoint: boolean): boolean {
        const shown = defined(this.#completion, 'the list a key is typed in');
        const last = shown.answer.candidates.length - 1;
        const action = completionKey(canonical, overPoint);
        switch (action) {
            case 'down': shown.selected = Math.min(last, shown.selected + 1); return true;
            case 'up': shown.selected = Math.max(0, shown.selected - 1); return true;
            case 'accept': this.#acceptCandidate(shown.selected, shown.answer); return true;
            case 'close': this.#closeCompletion(); return true;
            case 'closeAndPoint': this.#closeCompletion(); return false;
            case undefined: return false;
            default: return unreachable(action);
        }
    }

    /**
     * Accepts a candidate (ADR-0051), by Tab or a press on it: its span of the text is replaced by what it writes, in
     * both surfaces, and the caret stands after it. The list closes (ADR-0058, the tenth Windows run), and the new
     * text is asked about for its hint alone, so the hint for what was just accepted can follow.
     */
    #acceptCandidate(index: number, shown: EditorCompletion): void {
        const edit = this.#edit;
        // An accept carries the answer it was made on, and is refused once another has come (the spine's sixth rule).
        const candidate = this.#completionListOpen() && this.#completion?.answer === shown ? shown.candidates[index] : undefined;
        if (edit === undefined || candidate === undefined) return;
        const [text, caret] = acceptCandidate(this.#editorText(edit), candidate);
        this.#writeEditText(edit, text, caret, caret);
        this.#askReferences();
        this.#gateReferenceTexts();
        this.#tellPointState();
        this.#invalidate();
        // Last: a Consumer's completion that throws leaves the References asked and the Point state told.
        this.#requestCompletion(edit, false, caret);
    }

    // Where the box stands: beneath the surface typed in (ADR-0040). None before the grid has its geometry.
    #completionPlaceOf(edit: OpenEdit<Row>): CompletionPlace | undefined {
        const metrics = this.#metrics;
        const inBar = edit.surface === 'bar' && this.#formulaBarDeclared.shown;
        const geometry = this.#geometry;
        if (!inBar && geometry === undefined) return undefined;
        return completionPlace({
            inBar, barPx: this.#barPx, nameBoxWidthPx: metrics.nameBoxWidthPx, bandHeightPx: this.#bandHeightPx,
            rowHeightPx: metrics.rowHeightPx, visibleHeightPx: this.#visibleHeightPx,
            cellLeftPx: inBar ? 0 : this.#cellLeftPx(edit.cell.column),
            cellTopPx: inBar || geometry === undefined ? 0 : geometry.viewportTopPxOf(edit.cell.row, this.#scrollTopPx),
        });
    }

    // The list and the hint, and what the editor surfaces say of them to assistive technology: `aria-autocomplete`
    // where completion is declared, and while a list is shown, the list it controls and the chosen candidate on the
    // surface typed in.
    #paintCompletion(): void {
        const edit = this.#edit;
        const shown = this.#completion;
        const box = this.#completionBox;
        const place = edit === undefined || shown === undefined ? undefined : this.#completionPlaceOf(edit);
        if (place === undefined || shown === undefined) {
            box.hide();
            box.element.remove();
        } else {
            const { leftPx, widths } = popoverAcross(place.leftPx, this.#visibleWidthPx);
            setAttribute(box.element, 'style', `left: ${leftPx}px; top: ${place.topPx}px; ${place.above ? 'transform: translateY(-100%); ' : ''}`
                + `${widths}; max-height: ${place.roomPx}px`);
            box.paint(shown.answer, shown.selected, this.#chrome?.editorCompletion);
            if (box.element.parentNode !== this.#root) this.#scroller.after(box.element);
        }
        const listed = place !== undefined && this.#completionListOpen() ? shown : undefined;
        const typedIn = edit === undefined ? undefined : this.#surfaceOf(edit);
        const autocomplete = this.#completeEditorText === undefined ? undefined : 'list';
        const cellField = edit === undefined ? undefined : this.#editorField(edit);
        for (const surface of [cellField, this.#bar.text]) {
            if (surface === undefined) continue;
            const controls = surface === typedIn ? listed : undefined;
            setAttribute(surface, 'aria-autocomplete', autocomplete);
            setAttribute(surface, 'aria-controls', controls === undefined ? undefined : box.listId);
            setAttribute(surface, 'aria-activedescendant', controls === undefined ? undefined : box.optionId(controls.selected));
        }
    }

    // ---- The coloured text (ADR-0057) -----------------------------------------------------------------------

    // The layers, made the first time a References function is declared: the bar's stands in the band before its
    // field from then on, empty while no edit is open; the cell's is placed with the editor. None while no function is
    // declared, and those made before are taken away.
    #referenceTextLayers(): ReferenceTextLayers | undefined {
        const made = this.#referenceText;
        if (this.#referenceOptions.referencesIn === undefined) {
            if (made !== undefined) {
                this.#referenceText = undefined;
                this.#bar.text?.classList.remove('ex-reference-text-shown');
                this.#edit?.element.classList.remove('ex-reference-text-shown');
                made.cell.uncolour();
                made.bar.uncolour();
                made.highlights.unregister();
                made.cell.element.remove();
                made.bar.element.remove();
                made.style.remove();
            }
            return undefined;
        }
        const doc = this.#root.ownerDocument;
        const layers = made ?? this.#makeReferenceTextLayers(doc);
        if (layers.style.parentNode !== this.#root) this.#root.prepend(layers.style);
        // Before the bar's text box, which a Chrome's field may be drawn in: the gate shows the colours over the core's own only.
        const textBox = this.#bar.textBox;
        if (layers.bar.element.nextSibling !== textBox && textBox.parentNode === this.#bar.band) this.#bar.band.insertBefore(layers.bar.element, textBox);
        return layers;
    }

    #makeReferenceTextLayers(doc: Document): ReferenceTextLayers {
        const highlights = new ReferenceHighlights(this.#ids);
        const style = doc.createElement('style');
        style.textContent = highlights.css;
        const layers: ReferenceTextLayers = {
            highlights, style,
            cell: new ReferenceTextLayer(doc, 'ex-reference-text ex-reference-text-cell'),
            bar: new ReferenceTextLayer(doc, 'ex-reference-text ex-reference-text-bar'),
        };
        this.#referenceText = layers;
        return layers;
    }

    // The coloured text shown in the surface the edit is in, the field holding DOM focus, as Excel colours only that
    // one, while the layer's text is the field's value and no IME composes there; the other surface keeps its plain
    // text. Both layers carry the text, so the colours follow a press from one surface into the other.
    // The References are not asked here: they were asked of the text in the input or the pass that changed it, where a
    // refused answer is refused. A text not yet asked about is not coloured.
    #gateReferenceTexts(): void {
        if (this.#destroyed) return;
        const layers = this.#referenceTextLayers();
        if (layers === undefined) return;
        const edit = this.#edit;
        const outlines = this.#referenceOutlines;
        const text = edit === undefined ? '' : this.#editorText(edit);
        const stretches = edit === undefined || outlines.askedFor !== text ? [] : layers.highlights.coloursOf(outlines.asked, this.#pointedSpan(edit));
        // The core's own field only: a Chrome's field is drawn in a look the layer is not measured against (ADR-0057).
        const cellField = edit === undefined || edit.slot !== undefined ? undefined : builtInEditor(edit.element);
        this.#gateReferenceText(layers, layers.cell, cellField, text, stretches);
        const barShown = edit !== undefined && this.#formulaBarDeclared.shown;
        setAttribute(layers.bar.element, 'style', `left: ${this.#metrics.nameBoxWidthPx}px`);
        this.#gateReferenceText(layers, layers.bar, this.#bar.builtInText, barShown ? text : '', barShown ? stretches : []);
    }

    #gateReferenceText(layers: ReferenceTextLayers, layer: ReferenceTextLayer, field: HTMLInputElement | undefined, text: string,
        stretches: readonly ColouredStretch[]): void {
        const written = layer.write(text, stretches);
        const shown = field !== undefined && this.#edit !== undefined && field.ownerDocument.activeElement === field
            && this.#composingIn !== field && field.value === text;
        if (field !== undefined) {
            const was = field.classList.contains('ex-reference-text-shown');
            if (shown !== was) field.classList.toggle('ex-reference-text-shown', shown);
            // Read only when the text or the look changed: the field's own scroll is heard as it happens.
            if (shown && (written || !was)) layer.scrollTo(field.scrollLeft);
        }
        if (shown) layer.colour(layers.highlights);
        else layer.uncolour();
    }

    // Where what Point wrote stands, shown selected (ADR-0051, ADR-0058): the Reference Point is writing, or the text a
    // press outside the grid wrote, while pointing; none otherwise. A look only: the field's text is never selected.
    #pointedSpan(edit: OpenEdit<Row>): PointedSpan | undefined {
        return this.#pointingContinues(edit) ? { start: this.#pointStart, length: this.#pointLength } : undefined;
    }

    // An editor surface's own events the coloured text follows: the keyboard leaving it, a composition, and its text
    // scrolled sideways.
    // Heard on the field, or on the band that holds whichever field stands: the keyboard's moves and the
    // compositions rise to it, and its scroll is heard in the capture phase, as a scroll does not rise.
    #hearSurface(holder: HTMLElement): void {
        holder.addEventListener('focusout', this.#onSurfaceChanged);
        holder.addEventListener('focusin', this.#onSurfaceChanged);
        holder.addEventListener('compositionstart', this.#onCompositionStarted);
        holder.addEventListener('compositionend', this.#onCompositionEnded);
        holder.addEventListener('scroll', this.#onSurfaceScrolled, true);
    }

    readonly #onSurfaceChanged = (): void => {
        this.#gateReferenceTexts();
    };

    readonly #onCompositionStarted = (event: Event): void => {
        if (event.target instanceof HTMLInputElement) this.#composingIn = event.target;
        this.#gateReferenceTexts();
    };

    readonly #onCompositionEnded = (): void => {
        this.#composingIn = undefined;
        this.#gateReferenceTexts();
    };

    readonly #onSurfaceScrolled = (event: Event): void => {
        const field = event.target;
        const layers = this.#referenceText;
        if (layers === undefined || !(field instanceof HTMLInputElement)) return;
        if (field === this.#bar.builtInText) layers.bar.scrollTo(field.scrollLeft);
        else if (field === this.#edit?.element) layers.cell.scrollTo(field.scrollLeft);
    };

    // ---- Pointed at from outside (ADR-0058) ---------------------------------------------------------------------

    // Whether the grid is pointed at now: declared so, and holding no open edit of its own, which a press goes to.
    #pointedAtNow(): boolean {
        return this.#pointedAt?.isPointedAt === true && this.#edit === undefined;
    }

    // Hears the declaration passed, and answers its steps; stops hearing one no longer passed.
    #bindPointedAt(declared: GridPointedAt<Row> | undefined): void {
        if (declared === this.#pointedAt) return;
        this.#pointedAtStop?.();
        this.#pointedAtStop = undefined;
        this.#pointedAt = declared;
        if (declared === undefined) return;
        const stopHearing = declared.onChanged(() => { if (!this.#destroyed) this.#invalidate(); });
        const detach = declared.attach(this.#pointedAtAnswers);
        this.#pointedAtStop = () => {
            stopHearing();
            detach();
        };
    }

    // What the grid answers a declaration for arrow keys pressed elsewhere (DC-55): the Selection, the Focus and the
    // scroll do not move, except as a reveal asks.
    readonly #pointedAtAnswers: PointedAtGrid<Row> = {
        step: async (isRow, column, direction, isColumn) => this.#pointedStep(isRow, column, direction, isColumn),
        stepFromColumn: async (column, direction, isColumn) => this.#pointedStepFromColumn(column, direction, isColumn),
        reveal: async (isRow, column) => this.#revealPointedCell(isRow, column),
        revealColumn: async column => this.#revealPointedColumn(column),
    };

    /**
     * A press on the rows while the grid is pointed at (ADR-0058): its default prevented, so DOM focus stays where it
     * is, and handed over as the cell it landed on, or as more than one cell with Shift or on a Row Heading. The
     * Selection and the Focus do not move, and no edit opens or commits. A press on a cell is followed while its
     * button is down, so that a drag onto another cell is handed over too.
     */
    #handOverRowsPress(event: MouseEvent): void {
        event.preventDefault();
        this.#answered += 1;
        this.#invalidate();
        this.#pressHandedOver = true;
        this.#setDragging(false);
        const pointedAt = this.#pointedAt;
        if (event.button !== 0 || pointedAt === undefined || event.target !== this.#viewport) return;
        const at = this.#cellUnder(event);
        let press: GridPointedPress<Row>;
        if (this.#inRowHeadings(event)) {
            if (at === undefined) return;
            press = { kind: 'severalCells' };
        } else if (event.offsetX < 0 || event.offsetX >= this.#styles.geometry.totalWidthPx || at === undefined) {
            // Past the last column the rows are dead space, not the last column again.
            return;
        } else if (event.shiftKey) {
            press = { kind: 'severalCells' };
        } else {
            press = { kind: 'cell', row: this.#rowAt(at.row)?.row, column: itemAt(this.#columns, at.column).column.name };
            this.#handOver(pointedAt, press);
            this.#setDragging(true);
            this.#pointedDrag = { from: at, fromHeader: false, x: event.offsetX, clientX: event.clientX };
            return;
        }
        this.#handOver(pointedAt, press);
    }

    /**
     * A press on the header while the grid is pointed at (ADR-0058): handed over as the column whose header it landed
     * on, as several columns with Shift, as a Header Group's rectangle, or as more than one cell on the corner where
     * the Headings meet. Nothing is sorted, selected, grabbed or opened. A press on a column's header is followed
     * while its button is down, so that a drag onto another column is handed over too.
     */
    #handOverHeaderPress(event: MouseEvent): void {
        event.preventDefault();
        this.#pressHandedOver = true;
        this.#setDragging(false);
        const pointedAt = this.#pointedAt;
        if (event.button !== 0 || pointedAt === undefined || event.target !== this.#header) return;
        let press: GridPointedPress<Row>;
        if (this.#rowHeadingStyle !== undefined && this.#styles.geometry.isInLead(event.offsetX, this.#scrollLeftPx)) {
            press = { kind: 'severalCells' };
        } else {
            const column = this.#columnUnderHeader(event);
            if (column === undefined) return;
            const layout = this.#headerLayout;
            const tierPx = this.#metrics.headerHeightPx;
            if (layout.tierCount > 0 && event.offsetY < this.#bandHeightPx - (layout.leafTierSpanOf(column) * tierPx)) {
                const tier = layout.tierCount - Math.floor(event.offsetY / tierPx);
                const group = layout.groups.find(candidate => column >= candidate.firstColumn && column < candidate.firstColumn + candidate.memberCount
                    && tier >= candidate.bottomTier && tier <= candidate.topTier);
                // An empty tier above a leaf that does not stretch there is no header at all.
                if (group === undefined) return;
                press = { kind: 'headerGroup' };
            } else if (event.shiftKey) {
                press = { kind: 'severalColumns' };
            } else {
                press = { kind: 'columnHeader', column: itemAt(this.#columns, column).column.name };
                this.#handOver(pointedAt, press);
                this.#setDragging(true);
                this.#pointedDrag = { from: cellPosition(0, column), fromHeader: true, x: event.offsetX, clientX: event.clientX };
                return;
            }
        }
        this.#handOver(pointedAt, press);
    }

    // Whether a click, a double click or a context menu on the rows or the header means nothing here: its press was
    // handed over, or, a click no press came before, the grid is pointed at now.
    #handedOver(): boolean {
        return this.#pressHandedOver || this.#pointedAtNow();
    }

    // A press handed over: the Consumer's answer is its own, and a failure of it reaches the page as its unhandled
    // rejection, as a source's does (ADR-0025).
    #handOver(pointedAt: GridPointedAt<Row>, press: GridPointedPress<Row>): void {
        this.#pointedPressHandOver = pointedAt.onPress(press);
    }

    /**
     * A move while a press handed over is held. From a cell, reaching another cell makes it a drag across cells;
     * from a header, reaching another column makes it a drag across columns. Either is handed over once, behind the
     * press it began with, and the drag ends there: nothing it passes over is selected.
     */
    #movePointedDrag(event: MouseEvent): void {
        const drag = defined(this.#pointedDrag, 'the press a pointed drag began with');
        const pointedAt = this.#pointedAt;
        let moved: boolean;
        if (drag.fromHeader) {
            // Placed by its client delta from the press, whatever element the event fired on, as a Heading drag is.
            const x = drag.x + (event.clientX - drag.clientX);
            const column = Number.isFinite(x) ? this.#styles.geometry.columnAt(x, this.#scrollLeftPx) : undefined;
            moved = column !== undefined && column !== drag.from.column;
        } else {
            const at = event.target === this.#viewport ? this.#cellUnder(event) : undefined;
            moved = at !== undefined && !sameCell(at, drag.from);
        }
        if (!moved) return;
        this.#setDragging(false);
        if (pointedAt === undefined) return;
        const dragged: GridPointedPress<Row> = { kind: drag.fromHeader ? 'severalColumns' : 'severalCells', dragged: true };
        const press = this.#pointedPressHandOver;
        // Behind the press, whatever its answer: its own failure has reached the page as its own rejection.
        void (async () => {
            await Promise.allSettled([press]);
            if (!this.#destroyed) await pointedAt.onPress(dragged);
        })();
    }

    // The first painted row `isRow` names, or none.
    #paintedRowOf(isRow: (row: Row) => boolean): number | undefined {
        const visible = this.#visible;
        if (visible === undefined) return undefined;
        for (let row = visible.start; row < visible.start + visible.count; row += 1) {
            const held = this.#rowAt(row);
            if (held !== undefined && isRow(held.row)) return row;
        }
        return undefined;
    }

    // The position, in the whole result, of the row `isRow` names: the painted rows are asked first, since the cell
    // pointed at was pressed or revealed there, and then the rest of the Window. None when the grid holds no such row.
    #pointedRowOf(isRow: (row: Row) => boolean): number | undefined {
        const painted = this.#paintedRowOf(isRow);
        if (painted !== undefined) return painted;
        const { window, windowStart } = this.#state;
        const found = window.findIndex(row => isRow(row));
        return found < 0 ? undefined : windowStart + found;
    }

    // The name of the nearest column left or right of `from` that `isColumn` answers true for, passing over the rest.
    #nearestColumn(from: number, direction: GridDirection, isColumn: (column: string) => boolean): string | undefined {
        const step = direction === 'left' ? -1 : 1;
        for (let c = from + step; c >= 0 && c < this.#columns.length; c += step) {
            const { name } = itemAt(this.#columns, c).column;
            if (isColumn(name)) return name;
        }
        return undefined;
    }

    // The cell one step from the named one (ADR-0058, "The keyboard"; DC-55): a row up or down in the current order,
    // in the same column, or the nearest column the Consumer names left or right, in the same row.
    #pointedStep(isRow: (row: Row) => boolean, column: string, direction: GridDirection, isColumn: (column: string) => boolean): GridPointedStep<Row> {
        const from = this.#columnNamed(column);
        const row = this.#destroyed || !this.#pointedAtNow() || from === undefined ? undefined : this.#pointedRowOf(isRow);
        if (from === undefined || row === undefined) return { kind: 'notHeld' };
        if (direction === 'up' || direction === 'down') {
            const next = direction === 'up' ? row - 1 : row + 1;
            if (next < 0 || next >= this.#totalRows) return { kind: 'edge' };
            const held = this.#rowAt(next);
            return held === undefined ? { kind: 'rowNotArrived', column } : { kind: 'cell', row: held.row, column };
        }
        const reached = this.#nearestColumn(from, direction, isColumn);
        return reached === undefined ? { kind: 'edge' } : { kind: 'cell', row: this.#rowAt(row)?.row, column: reached };
    }

    // What one step from a whole column reaches (ADR-0058, "The keyboard"; DC-55): down, the column's first row in
    // the current order; left or right, the nearest column the Consumer names, as a column; up, nothing.
    #pointedStepFromColumn(column: string, direction: GridDirection, isColumn: (column: string) => boolean): GridPointedStep<Row> {
        const from = this.#columnNamed(column);
        if (this.#destroyed || !this.#pointedAtNow() || from === undefined) return { kind: 'notHeld' };
        switch (direction) {
            case 'up':
                return { kind: 'edge' };
            case 'down': {
                if (this.#totalRows === 0) return { kind: 'edge' };
                const first = this.#rowAt(0);
                return first === undefined ? { kind: 'rowNotArrived', column } : { kind: 'cell', row: first.row, column };
            }
            case 'left':
            case 'right': {
                const reached = this.#nearestColumn(from, direction, isColumn);
                return reached === undefined ? { kind: 'edge' } : { kind: 'column', column: reached };
            }
            default:
                return unreachable(direction);
        }
    }

    // Scrolls the named cell into view, as Point's outline is kept in view (DC-55): the page turns where one is
    // paged. The Selection and the Focus do not move. Whether the grid holds the cell.
    #revealPointedCell(isRow: (row: Row) => boolean, column: string): boolean {
        const c = this.#columnNamed(column);
        const row = this.#destroyed || c === undefined ? undefined : this.#pointedRowOf(isRow);
        if (c === undefined || row === undefined) return false;
        this.#revealOwed = { target: cellPosition(row, c), extending: undefined, rowStart: false, stepRows: 0 };
        this.#revealHeaderOwed = undefined;
        this.#invalidate();
        return true;
    }

    // Scrolls the named column into view across only (ADR-0058, 2026-10-01; DC-55): its body is what is dashed, and
    // no row of it is the one pointed at, so the rows stay where they are. Whether the grid shows the column.
    #revealPointedColumn(column: string): boolean {
        const c = this.#destroyed ? undefined : this.#columnNamed(column);
        if (c === undefined) return false;
        this.#revealOwed = undefined;
        this.#revealHeaderOwed = { first: c, last: c };
        this.#invalidate();
        return true;
    }

    // The dashes the declaration asks for, as the range they are drawn over: the named column's body across all its
    // rows, or its cell in the first painted row the declaration names. None for no dashes asked, a column the grid
    // does not show, or a row that is not painted: the grid never scrolls to show one. The same range while it is.
    // Asked again only when what it reads changed, since a row's dashes ask the Consumer about every painted row.
    #pointDashesRange(): SelectionRange | undefined {
        const dashes = this.#pointedAt?.dashes;
        const visible = this.#visible;
        const asked: PointDashesAsked = {
            dashes, columns: this.#columns, window: this.#state.window, windowStart: this.#state.windowStart,
            start: visible?.start, count: visible?.count, totalRows: this.#totalRows,
        };
        const was = this.#pointDashesAsked;
        if (was !== undefined && sameDashesAsked(was.asked, asked)) return was.range;
        const column = dashes === undefined || this.#totalRows <= 0 ? undefined : this.#columnNamed(dashes.column);
        let range: SelectionRange | undefined;
        if (dashes !== undefined && column !== undefined) {
            const isRow = dashes.row;
            if (isRow === undefined) {
                range = selectionRange(0, column, this.#totalRows, 1);
            } else {
                const found = this.#paintedRowOf(isRow);
                if (found !== undefined) range = selectionRange(found, column, 1, 1);
            }
        }
        const painted = range === undefined || was?.range === undefined || !sameSelectionRange(range, was.range) ? range : was.range;
        this.#pointDashesAsked = { asked, range: painted };
        return painted;
    }

    // The columns outlined: the Consumer's own, then the declaration's, the same list while neither changed.
    #outlinedColumnsBoth(): readonly GridOutlinedColumn[] | undefined {
        const own = this.#referenceOptions.outlinedColumns;
        const declared = this.#pointedAt?.outlinedColumns;
        const was = this.#outlinedColumnsAsked;
        if (was !== undefined && was.own === own && was.declared === declared) return was.both;
        const both = own === undefined ? declared : declared === undefined ? own : [...own, ...declared];
        this.#outlinedColumnsAsked = { own, declared, both };
        return both;
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
        this.#viewsRead.set(view, { selection, set: this.#rowSet });
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
        const read = this.#viewsRead.get(view);
        if (read === undefined) throw new TypeError('A view handed back is one this grid read with readView (ADR-0331).');
        if (!Number.isInteger(rowSequenceVersion)) throw new TypeError('rowSequenceVersion is a whole number.');
        this.#viewAhead = { selection: read.selection, set: read.set, version: rowSequenceVersion, topPx: view.scrollTopPx, leftPx: view.scrollLeftPx };
        this.#invalidate();
    }

    /**
     * Shows rows this grid holds no view of, a Sheet shown for the first time, once the grid takes rows under
     * `rowSequenceVersion`, as {@link GridView.showViewWith} shows a view it read (ADR-0331): scrolled to the start,
     * with `place` selected where it is given and fits those rows, and nothing selected otherwise. They are rows of
     * their own: an edit open as they are taken stays open with the rows it was opened among, away (ADR-0331 §5).
     */
    showNewView(rowSequenceVersion: number, place?: { readonly range: SelectionRange; readonly focus: CellPosition }): void {
        if (this.#destroyed) throw new Error('This grid was destroyed.');
        if (!Number.isInteger(rowSequenceVersion)) throw new TypeError('rowSequenceVersion is a whole number.');
        if (place !== undefined) {
            const { range, focus } = place;
            if (!Number.isInteger(range.topRow) || !Number.isInteger(range.leftColumn) || range.topRow < 0 || range.leftColumn < 0
                || !Number.isInteger(range.rowCount) || !Number.isInteger(range.columnCount) || range.rowCount < 1 || range.columnCount < 1) {
                throw new RangeError('A range starts at row and column 0 or after and holds a cell at least (ADR-0050).');
            }
            if (!Number.isInteger(focus.row) || !Number.isInteger(focus.column)
                || focus.row < range.topRow || focus.row > bottomRow(range) || focus.column < range.leftColumn || focus.column >= range.leftColumn + range.columnCount) {
                throw new RangeError('The Focus is placed inside the range it belongs to (ADR-0050, ADR-0052).');
            }
        }
        this.#viewAhead = { selection: place, set: undefined, version: rowSequenceVersion, topPx: 0, leftPx: 0 };
        this.#invalidate();
    }

    /** The open edit's text as the grid holds it, or undefined while no edit is open: a Consumer deciding what a key of
     * its own means with an edit open reads the text it was pressed over (a Sheet shows another Sheet while a Formula
     * is typed, and not while a constant is, ADR-0331 §5). */
    readEditText(): string | undefined {
        if (this.#destroyed) throw new Error('This grid was destroyed.');
        const edit = this.#edit;
        return edit === undefined ? undefined : this.#editorText(edit);
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
        const surface = this.#editSurface(edit);
        if (surface.isConnected) surface.focus({ preventScroll: true });
    }

    /**
     * The keyboard back to the grid from something of the Consumer's that held it over the grid and goes away, a
     * dialog, a panel, a tab (DC-61, ADR-0070), or from a Chrome's frame outside it (ADR-0071): to the field an
     * open edit is typed in, else to the Keyboard Field, else the root (ADR-0080). Granted only while the keyboard
     * is still this grid's, DOM focus inside its root or on nothing, and not on the Formula Bar or the Name Box,
     * where the user is typing (ADR-0021): a control or a grid the user has moved to keeps it (ADR-0018). `from`,
     * the frame that holds the keyboard as it goes, widens the condition to DOM focus on it or inside it, so the
     * frame gives the keyboard back before it is removed (ADR-0050 item 16, note of 2026-10-12). It moves neither
     * the Focus nor the Selection and scrolls nothing. Once the grid is gone, it does nothing.
     */
    returnKeyboard(from?: Element): void {
        if (from !== undefined && !(from instanceof Element)) throw new TypeError('returnKeyboard is handed the frame that holds the keyboard.');
        if (this.#destroyed) return;
        const active = this.#root.ownerDocument.activeElement;
        const fromFrame = from !== undefined && active !== null && from.contains(active);
        if (!fromFrame && (!this.#keyboardHere() || this.#onOwnField())) return;
        this.focus();
    }

    /**
     * Hands the keyboard the grid holds on to a control of the Consumer's own (DC-61, ADR-0070): the first control
     * of a frame or a tab opened from the grid, a dialog's Close, in place of the control's own `focus()`. A
     * container names its first enabled text field, its text selected, or else its first enabled control, and one
     * holding neither is refused with a RangeError. Granted under `returnKeyboard`'s condition, so a press the user
     * made meanwhile keeps the keyboard where it put it. It moves neither the Focus nor the Selection and paints
     * nothing; the control scrolls into view unless `preventScroll` says not to. Once the grid is gone, the control
     * takes the keyboard by its own focus.
     */
    handKeyboardTo(control: HTMLElement, options?: { readonly preventScroll?: boolean }): void {
        if (!(control instanceof HTMLElement)) throw new TypeError('handKeyboardTo is handed the element that takes the keyboard.');
        // A control taken away since has nothing to hand the keyboard to.
        if (!control.isConnected) return;
        const { target, field } = keyboardTargetIn(control, 'handKeyboardTo');
        if (!this.#destroyed && (!this.#keyboardHere() || this.#onOwnField())) return;
        target.focus({ preventScroll: options?.preventScroll === true });
        field?.select();
    }

    // What the bar hears, handed to each bar made.
    readonly #barHooks: FormulaBarHooks = {
        textTyped: text => { this.#onBarTyped(text); },
        textFocused: () => { this.#onBarFocused(); },
        nameBoxEntered: typed => { this.#onNameBoxEntered(typed); },
        nameBoxLeft: () => { this.#onNameBoxLeft(); },
        nameBoxFocused: () => { this.#onNameBoxFocused(); },
    };

    // The field an open edit is typed in: the bar's text where it was typed there and the field stands, else the
    // cell's editor.
    #editSurface(edit: OpenEdit<Row>): HTMLElement {
        const bar = edit.surface === 'bar' ? this.#bar.text : undefined;
        return bar?.isConnected === true ? bar : this.#editorField(edit) ?? edit.element;
    }

    // DOM focus on the Formula Bar's text or the Name Box, the fields of the grid's own beside the rows that hold
    // the keyboard while the user types in them: a hand-back leaves it there (ADR-0021, widened 2026-09-28).
    #onOwnField(): boolean {
        const active = this.#root.ownerDocument.activeElement;
        return active !== null && (active === this.#bar.text || active === this.#bar.nameBox);
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
                edit.slot?.review();
                // The outline goes with the move, and its Reference stays written (ADR-0051).
                if (this.#endPointing()) this.#invalidate();
                this.#tellPointState();
            }
            return;
        }
        // The bar holds the keyboard now: a menu standing closes without taking it back (ADR-0050, item 16).
        this.#closePopover(false);
        this.#reconcileSelection();
        if (this.#discardedAim()) return;
        const focus = this.#held.selection.focus;
        if (focus === undefined || !this.#isEditableCell(focus) || !this.#bar.editable) return;
        this.#startEdit(focus, 'caret', this.#openingTextAt(focus), 'bar', true);
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
        this.#writeEditorText(edit, text);
        this.#typed(edit);
        this.#askReferences();
        this.#gateReferenceTexts();
        // Last: a Consumer's completion that throws leaves the References asked and the gate told.
        this.#requestCompletion(edit);
    };

    // Typed in the cell: shown in the bar as well (ADR-0051).
    readonly #onEditorTyped = (): void => {
        const edit = this.#edit;
        if (edit !== undefined && this.#formulaBarDeclared.shown) this.#bar.showText(this.#editorText(edit));
        if (edit === undefined) return;
        this.#typed(edit);
        this.#askReferences();
        this.#gateReferenceTexts();
        // Last: a Consumer's completion that throws leaves the References asked and the gate told.
        this.#requestCompletion(edit);
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
        // A commit pending lands in place too, the keyboard left where the user put it (ADR-0380).
        if (edit === undefined || this.#commitEdit(false) || this.#pending?.edit === edit) return;
        const surface = this.#editSurface(edit);
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
                    (this.#editorField(edit) ?? edit.element).focus({ preventScroll: true });
                } else {
                    this.#takeKeyboard();
                }
            }
            bar.band.remove();
            // A Chrome's fields go with the band, and are drawn again when it shows.
            bar.hide();
            return;
        }
        // A field the Consumer's Chrome now draws by another mount function is made again, though not while it is in
        // use, holding the keyboard or an edit typed there: its contents stand until a pass where it is not (ADR-0390).
        const doc = this.#root.ownerDocument;
        bar.adopt({ text: this.#chrome?.formulaBarText, nameBox: this.#chrome?.nameBox },
            box => box.contains(doc.activeElement) || (box.classList.contains('ex-formula-bar-text') && this.#edit?.surface === 'bar'));
        if (bar.band.parentNode !== this.#root) this.#scroller.after(bar.band);
        const edit = this.#edit;
        const read = this.#barReading();
        bar.paint({
            heightPx: this.#barPx,
            widthPx: this.#width === 'stretch' ? undefined : this.#width,
            nameBoxWidthPx: this.#metrics.nameBoxWidthPx,
            nameBox: this.#nameBoxText(read.named),
            text: edit === undefined ? read.text : this.#editorText(edit),
            borrowed: edit === undefined && read.borrowed,
            editable: edit !== undefined || read.editable,
            nameBoxLabel: read.nameBoxLabel,
            textLabel: read.textLabel,
        });
    }

    // The Name Box's text, as the C# core's NameBoxText: the Consumer's words for the Focus (`named`). While a pointing
    // outline stands over an open edit it names the outline's Focus instead, as Excel's does in Point (the Focus itself
    // has not moved), and the edited cell again the moment the outline goes; while what Point wrote came from outside
    // the grid, nothing, since the cell pressed there has no name here and naming the edited cell would read as
    // pointing at it (ADR-0058). While a selecting drag holds the button over more than one cell, the Consumer's words
    // for the range's size, as Excel's shows `4R x 3C` (ADR-0052). Asked on each pass: these change between passes
    // that leave the Focus where it was.
    #nameBoxText(named: string): string {
        const declared = this.#formulaBarDeclared;
        if (this.#edit !== undefined) {
            if (this.#pointedFromOutside) return '';
            const pointer = this.#pointer?.focus;
            if (pointer !== undefined) return this.#nameBoxWords(declared.nameBoxLabel?.(pointer), 'nameBoxLabel');
        }
        // A drag across Headings shows its size in the Size Tip instead, and the Name Box is empty while the tip shows;
        // over one column or row it names the Focus (ADR-0052, 2026-09-29).
        if (this.#headingDrag) return this.#sizeTipText() === undefined ? named : '';
        const range = this.#held.selection.focusRange;
        const sizeLabel = declared.nameBoxSizeLabel;
        if (sizeLabel === undefined || range === undefined || !this.#dragging || this.#handleDrag !== undefined
            || this.#pointDragging || rangeCellCount(range) <= 1) {
            return named;
        }
        const size: unknown = sizeLabel(range);
        if (size === undefined) return named;
        return this.#nameBoxWords(size, 'nameBoxSizeLabel');
    }

    #nameBoxWords(words: unknown, from: string): string {
        if (words === undefined) return '';
        if (typeof words !== 'string') throw new TypeError(`${from} answers the Name Box's text, not ${typeof words} (ADR-0051).`);
        return words;
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
    #writes(heard: ((intent: never) => unknown) | undefined): boolean {
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
    #raiseFill(plan: FillPlan, rows: readonly Row[], heard: (intent: GridPasteIntent) => void | PromiseLike<void>): void {
        const { source } = plan;
        const columns = this.#columns;
        const values = rows.map(row => Array.from({ length: source.columnCount },
            (_, offset) => rawText(itemAt(columns, source.leftColumn + offset).column.value(row))));
        // Raw values are written in the invariant notation, and read so (ADR-0050, item 10): never shown text,
        // which a Consumer reads under its own culture, `1234.5` becoming 12345 under de-DE.
        const controller = new AbortController();
        const intent = new GridPasteIntent(plan.paste, values, values.map(row => row.map(() => 'invariant' as const)),
            this.#handedVersion(), undefined, controller.signal, undefined, source);
        const written = this.#noteUserWrite(plan.paste.targets);
        const answer = this.#heardNow(() => heard(intent), failed => { this.#intentAnswered(intent, written, failed); });
        if (answer !== undefined) this.#intentPending(answer, intent, controller, written, () => undefined);
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
        if (this.#handedOver()) return;
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
        if (focus === undefined || this.#geometry === undefined) return;
        // Under a pager the Focus may stand on another page, which the menu turns to and reveals the Focus on, as a
        // key that reveals it does (ADR-0015): the menu opens over the page that holds its cell.
        const size = this.#pageSize;
        if (size !== undefined && this.#turnPageTo(Math.floor(focus.row / size))) {
            this.#scrollTopPx = defined(this.#geometry, 'the geometry a page turn resolves').scrollTopToReveal(focus.row, 0);
            this.#resolve();
            this.#invalidate();
        }
        const geometry = defined(this.#geometry, 'the geometry a page turn resolves');
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
    // row the Window does not hold opens nothing: there is no row to hand the commands. Commands answered later
    // take the menu's place in the Input Order (ADR-0421): it stands once they come, and what was typed meanwhile
    // reaches it then.
    #openContextMenu(cell: CellPosition, leftPx: number, topPx: number): void {
        const { window, windowStart } = this.#state;
        const slice = cell.row - windowStart;
        if (slice < 0 || slice >= window.length || cell.column >= this.#columns.length) return;
        const canCopy = !this.#held.selection.isEmpty;
        const core: GridCommand[] = [
            coreCommand(gridCommandIds.copy, canCopy, () => { this.#commandCopy(false); }),
        ];
        if (!this.#hideCopyWithHeaders) {
            core.push(coreCommand(gridCommandIds.copyWithHeaders, canCopy, () => { this.#commandCopy(true); }));
        }
        const { column } = itemAt(this.#columns, cell.column);
        const version = this.#sequence();
        const context: GridContextMenuContext<Row> = Object.freeze({
            row: itemAt(window, slice), column: column.name, columnType: column.type,
            selection: this.#held.selection.ranges, rowSequenceVersion: this.#handedVersion(), commands: Object.freeze(core),
        });
        const answer = this.#contextCommands === undefined ? Object.freeze([]) : this.#contextCommands(context);
        if (!isThenable(answer)) {
            this.#standContextMenu(context, [...core, ...checkedCommands(answer)], version, leftPx, topPx);
            return;
        }
        // A menu standing goes now, the keyboard back on the grid, so no press reaches its commands while the next
        // menu's are out, and what is typed meanwhile is held for the menu that stands.
        this.#closePopover(true);
        const source = this.#source;
        const names = this.#columnNames;
        // oxlint-disable-next-line ex-grid/no-catch -- an answer given up as an AbortError stands no menu; anything else is rethrown
        void this.#placeWork(() => Promise.resolve(answer).then(extra => {
            // The Selection the commands act on went with an order, a source or columns that moved meanwhile, as an open
            // menu goes (ADR-0011, ADR-0036), or was placed elsewhere by the Consumer: no menu stands over another
            // Selection.
            if (this.#destroyed || this.#sequence() !== version || this.#source !== source || !sameItems(this.#columnNames, names)
                || !sameRanges(this.#held.selection.ranges, context.selection)) return;
            this.#standContextMenu(context, [...core, ...checkedCommands(extra)], version, leftPx, topPx);
        }, (error: unknown) => {
            // Given up by whoever answers, who says why itself, as a Find step is: no menu stands, and nothing more is
            // said (ADR-0055). Any other failure reaches the page.
            if (error instanceof DOMException && error.name === 'AbortError') return;
            throw error;
        }), undefined);
    }

    // Stands the Context Menu of `context` with `commands`, the core's and the Consumer's.
    #standContextMenu(context: GridContextMenuContext<Row>, commands: readonly GridCommand[], version: number, leftPx: number, topPx: number): void {
        this.#closePopover(false);
        const element = this.#root.ownerDocument.createElement('div');
        element.className = 'ex-popover';
        element.setAttribute('role', 'menu');
        const slot = this.#chrome?.contextMenu;
        this.#standMenu({ kind: 'context', element, commands, leftPx, topPx, version, source: this.#source }, element, resolveMenuKey, false,
            slot === undefined ? undefined : { name: 'contextMenu', mount: slot, extra: {
                row: context.row, column: context.column, columnType: context.columnType, selection: context.selection,
                rowSequenceVersion: context.rowSequenceVersion,
            } });
    }

    // Stands a menu's popover: its items in `container`, inside the popover's element, placed under the root
    // after the scroller, and its first enabled item taking the keyboard (ADR-0039). `lettered` marks Excel's
    // letters, the column menu's (ADR-0044). Where a Chrome slot is filled, its contents draw the items in
    // `container` instead, mounted once the popover stands, and answer through the same table (ADR-0390).
    #standMenu<Extra extends object>(popover: StandingPopover, container: HTMLElement, table: MenuKeyTable, lettered: boolean,
        slot?: { readonly name: string; readonly mount: GridChromeSlot<GridMenuSlotContext & Extra>; readonly extra: Extra }): void {
        const label = (id: string): string => this.#labelOf(id);
        const answers = {
            run: (index: number) => { this.#runCommand(index); },
            cancel: () => { this.#closePopover(true); },
            answered: this.#answer,
            toFilter: (where: 'filterFirst' | 'filterLast' | 'filterSearch') => { this.#toFilter(where); },
        };
        const items: MenuPlace = slot === undefined
            ? new MenuItems(container, popover.commands, label, table, answers, lettered)
            : new SlottedMenu(container, slot.name, slot.mount, slot.extra, popover.commands, label, table,
                { ...answers, answered: this.#answerInput },
                (control, preventScroll, owed) => { this.#takeChromeFocus(control, preventScroll, owed); });
        this.#popover = { ...popover, items };
        this.#placePopover();
        this.#scroller.after(popover.element);
        try {
            items.focus();
        } catch (error) {
            // A slot whose contents could not be drawn leaves no empty menu standing.
            this.#closePopover(true);
            throw error;
        }
        this.#invalidate();
    }

    // An input reached a popover and was answered (ADR-0220).
    readonly #answer = (): void => {
        this.#answered += 1;
        this.#invalidate();
    };

    // The same, for a Chrome slot's contents, which may call back twice for one input: a key resolved, then the
    // command it runs invoked. The input the root heard is counted once.
    readonly #answerInput = (): void => {
        const input = this.#input;
        if (input !== undefined && input === this.#inputAnswered) return;
        this.#inputAnswered = input;
        this.#answer();
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
        // A command still running takes its place in the Input Order: every input made after the press is held until
        // it settles (ADR-0421), as behind a declared key's answer. A failure its promise rejects with reaches the page
        // as that rejection, unheard.
        const ran = command.invoke();
        if (isThenable(ran)) void this.#placeWork(() => ran, undefined);
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
        const doc = this.#root.ownerDocument;
        // Read before the contents go: a slot's cleanup that empties it moves DOM focus off what it held.
        const held = popover.element.contains(doc.activeElement);
        // A Chrome slot's contents are taken away before their slot goes (ADR-0390). A cleanup of the Consumer's that
        // throws still leaves no part of the popover standing, and the failure is said once it is gone.
        try {
            if (popover.kind === 'column') popover.panel?.close();
        } finally {
            try {
                if (popover.kind === 'column' || popover.kind === 'context') popover.items.dispose();
                else if (popover.kind === 'find') popover.panel.dispose();
                else popover.closed?.();
            } finally {
                this.#popoverGone(popover, held, giveBack);
            }
        }
    }

    // The rest of a popover's closing, once its contents are gone.
    #popoverGone(popover: OpenPopover, held: boolean, giveBack: boolean): void {
        const doc = this.#root.ownerDocument;
        // A step still out is nobody's once the panel is gone (ADR-0055).
        if (popover.kind === 'find') {
            this.#findStepping?.abort();
            this.#findStepping = undefined;
        }
        const active = doc.activeElement;
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
    openPopover(content: (context: GridPopoverContext) => HTMLElement, label: string, closed?: () => void): boolean {
        if (this.#destroyed) throw new Error('This grid was destroyed.');
        if (typeof content !== 'function') throw new TypeError("A Consumer's popover's content is a function that makes its element (ADR-0050, item 16).");
        if (typeof label !== 'string' || label.trim() === '') throw new TypeError("A Consumer's popover is named in the Consumer's words: its label is not empty (ADR-0033).");
        // Checked as a script may hand it, whatever its type says.
        const hears: unknown = closed;
        if (hears !== undefined && typeof hears !== 'function') throw new TypeError("A Consumer's popover's closed is the function that hears it close.");
        if (this.#edit !== undefined) return false;
        this.#standFrame(content, label, false, closed);
        return true;
    }

    // Stands contents in the popover frame, the Consumer's or the grid's own, in place of any popover that stands.
    #standFrame(content: (context: GridPopoverContext) => HTMLElement, label: string, own: boolean, closed?: () => void): void {
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
        this.#popover = { kind: 'consumer', element: frame.element, frame, opening, own, closed };
        this.#placePopover();
        this.#scroller.after(frame.element);
        frame.focusFirst();
        this.#answer();
    }

    /** Holds the Settled Mark until the answer is called, once (ADR-0410 section 5). */
    holdSettled(): () => void {
        if (this.#destroyed) throw new Error('This grid was destroyed.');
        this.#settledHolds += 1;
        this.#showSettled(false);
        let held = true;
        return () => {
            if (!held) return;
            held = false;
            this.#settledHolds -= 1;
            if (!this.#destroyed) this.#lookSettled();
        };
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
        // Pointed at from outside, no column menu opens (ADR-0058), as for a row's checkbox (#pressRowMark).
        if (this.#pointedAtNow()) return;
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
        const panel = filterable ? this.#filterPanel(doc, declared, filterUi) : undefined;
        if (panel !== undefined) element.append(panel.lead, panel.body, panel.trail);
        const slot = this.#chrome?.columnMenu;
        this.#standMenu({ kind: 'column', element, commands, column: name, panel }, menu,
            (pressed, shift, chord, held, current) => resolveColumnMenuKey(pressed, shift, chord, held, current, filterable), true,
            slot === undefined ? undefined : { name: 'columnMenu', mount: slot, extra: columnMenuExtra(name, declared.type) });
        if (panel === undefined || this.#popover?.kind !== 'column' || this.#popover.panel !== panel) return;
        try {
            panel.open();
        } catch (error) {
            // A slot whose contents could not be drawn leaves no empty panel standing.
            this.#closePopover(true);
            throw error;
        }
    }

    // A column's filter for one opening (ADR-0009): its state and what each choice means, drawn by the built-in panel
    // or by the Chrome's filterPanel slot (ADR-0390).
    #filterPanel(doc: Document, declared: GridColumn<Row>, filterUi: FilterUi): FilterPlace {
        const name = declared.name;
        const model = new FilterPanelModel({
            name, type: declared.type, dateType: declared.dateType ?? 'dateTime', filterUi, // oxlint-disable-line ex-grid/no-stand-in-default -- a Date column's dates are dateTime unless declared
            textOf: value => textFor(declared, value),
        }, this.#filterOf(name), this.#culture, this.#temporal, signal => this.#source.distinctValues(name, signal), () => { this.#invalidate(); },
        id => this.#labelOf(id));
        const answers = {
            apply: (spec: FilterSpec | undefined) => { this.#applyFilter(name, spec); },
            cancel: () => { this.#closePopover(true); },
            letter: (key: string) => this.#valueListLetter(key),
            returnToCommands: () => {
                const standing = this.#popover;
                if (standing?.kind === 'column') standing.items.focusFirst();
            },
            answered: this.#answer,
        };
        const slot = this.#chrome?.filterPanel;
        if (slot === undefined) return new FilterPanel(doc, model, answers);
        return new SlottedFilterPanel(doc, slot, model, { ...answers, answered: this.#answerInput },
            (control, preventScroll, owed) => { this.#takeChromeFocus(control, preventScroll, owed); });
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
            const panel = this.#findPanel();
            // A Replace All still out with the Consumer holds a panel opened again too (ADR-0332 §3).
            panel.waitForReplace(this.#replacing > 0);
            popover = { kind: 'find', element: panel.element, panel };
            this.#popover = popover;
            this.#placePopover();
            this.#scroller.after(panel.element);
        }
        popover.panel.showReplace(replace);
        this.#setFindOutcome({ kind: 'none' });
        popover.panel.focusField();
    }

    // The find panel for one opening: the built-in one, or the Chrome's findPanel slot (ADR-0390).
    #findPanel(): FindPlace {
        const doc = this.#root.ownerDocument;
        const label = (id: string): string => this.#labelOf(id);
        const answers = {
            step: (backward: boolean) => { this.#findStep(backward); },
            replaceOne: () => { this.#replaceOne(); },
            replaceAll: () => { this.#replaceAll(); },
            heldChanged: () => { this.#invalidate(); },
            answered: this.#answer,
        };
        const slot = this.#chrome?.findPanel;
        if (slot === undefined) return new FindPanel(doc, this.#findHeld, label, answers);
        return new SlottedFindPanel(doc, slot, this.#findHeld, label,
            { ...answers, answered: this.#answerInput, close: () => { this.#closePopover(true); } },
            (control, preventScroll, owed) => { this.#takeChromeFocus(control, preventScroll, owed); });
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
            // Given up by a source since replaced: whatever stopped it, it was about rows the grid no longer shows,
            // which the panel says as any answer from such a source (ADR-0142).
            if (source !== this.#source && error instanceof DOMException && error.name === 'AbortError') {
                this.#refuseFind('sourceChanged');
                return;
            }
            this.#setFindOutcome({ kind: 'none' });
            // Given up by whoever answers, who says why itself: nothing moves, and no "No match" is claimed for
            // cells nobody read (ADR-0055, ADR-0025).
            if (error instanceof DOMException && error.name === 'AbortError') return;
            throw error;
        });
    }

    /**
     * The error step (ADR-0381 section 3): asked of the Consumer from the Focus, in the columns' current order and
     * the source's version, as a Find step is. A found cell becomes the Focus and is revealed, wherever it stands in
     * the result. An answer under another version or source, or naming a column that has left, moves nothing and is
     * said, and so is none found. A new step makes the answer of the one still out nobody's.
     */
    #stepToError(backward: boolean): void {
        const ask = defined(this.#findError, 'the error step\'s findError');
        this.#errorStepping?.abort();
        this.#errorStepping = undefined;
        this.#reconcileSelection();
        const source = this.#source;
        const selection = this.#held.selection;
        const request: GridErrorStepRequest = Object.freeze({
            backward, from: selection.focus, columns: Object.freeze(this.#columns.map(({ column }) => column.name)),
            rowSequenceVersion: this.#state.rowSequenceVersion,
        });
        const stepping = new AbortController();
        this.#errorStepping = stepping;
        this.#invalidate();
        // oxlint-disable-next-line ex-grid/no-catch -- a step given up by whoever answers moves nothing; any other failure is rethrown
        Promise.resolve().then(() => ask(request, stepping.signal)).then(answer => {
            if (this.#destroyed || this.#errorStepping !== stepping) return;
            this.#errorStepping = undefined;
            this.#invalidate();
            this.#errorAnswered(request, answer, source);
        }, (error: unknown) => {
            if (this.#destroyed || this.#errorStepping !== stepping) return;
            this.#errorStepping = undefined;
            this.#invalidate();
            // Given up by whoever answers, who says why itself: nothing moves, and no "no error" is claimed for cells
            // nobody read (ADR-0055's rule for a Find step).
            if (error instanceof DOMException && error.name === 'AbortError') return;
            throw error;
        });
    }

    // An error step's answer, judged against what it was asked under (ADR-0381, ADR-0011, ADR-0142).
    #errorAnswered(request: GridErrorStepRequest, answer: GridFindResult, source: GridSource<Row>): void {
        // The shape first: a Consumer's defect is the same defect whether or not the rows moved meanwhile.
        const given: unknown = answer;
        if (typeof given !== 'object' || given === null || typeof (given as { readonly found?: unknown }).found !== 'boolean') {
            throw new TypeError('findError answered something other than a Find result: { found: true, row, column } or { found: false } (ADR-0381).');
        }
        if (source !== this.#source || request.rowSequenceVersion !== this.#state.rowSequenceVersion) {
            this.#sayRefusal(this.#labelOf(errorStepMovedId));
            return;
        }
        const total = this.#totalRows;
        if (answer.found && (!request.columns.includes(answer.column) || !Number.isInteger(answer.row) || answer.row < 0 || answer.row >= total)) {
            throw new RangeError(`The error step's answer (row ${answer.row}, column '${answer.column}') is outside the request: it asked about `
                + `the columns [${request.columns.join(', ')}] of a result of ${total} rows (ADR-0381).`);
        }
        if (!answer.found) {
            this.#sayRefusal(this.#labelOf(errorStepNoneId));
            return;
        }
        const column = this.#columns.findIndex(({ column: declared }) => declared.name === answer.column);
        if (column < 0) {
            this.#sayRefusal(this.#labelOf(errorStepMovedId));
            return;
        }
        this.#reconcileSelection();
        const found = cellPosition(answer.row, column);
        this.#apply(this.#held.selection.click(found, this.#extent));
        this.#revealOwed = { target: found, extending: undefined, rowStart: false, stepRows: 0 };
        this.#revealHeaderOwed = undefined;
        this.#followArmed = { focus: found, target: found };
        this.#invalidate();
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
     * said, and stays the current match. A verdict or an `onEdit` answered later makes the Replace a Pending
     * Commit with no editor (ADR-0380): an Escape cancels it, and it is said as a write refused.
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
        const controller = new AbortController();
        const cancelled = (): void => {
            refused(this.#labelOf(commitCancelledId));
        };
        const judged = (verdict: EditVerdict): void => {
            if (verdict.kind === 'reject') {
                refused(verdict.message);
                return;
            }
            this.#supersedeFill();
            const intent = new GridEditIntent(holding.row, name, text, painted, painted, controller.signal);
            // The user's own write (D1), noted before the Consumer hears it, and taken back if refused.
            const written = this.#noteUserWrite([fromCorners(cell, cell)]);
            const replaced = (): void => {
                const refusal = intent.refusalMessage;
                if (refusal !== undefined) refused(refusal);
                else this.#findStep(false);
            };
            const answer = this.#heardNow(() => heard(intent), failed => { this.#intentAnswered(intent, written, failed); });
            if (answer === undefined) {
                replaced();
                return;
            }
            this.#pend(answer, {
                controller, edit: undefined, phase: 'intent',
                cancelled: () => {
                    this.#intentAnswered(intent, written, true);
                    cancelled();
                },
            }, false, () => {
                this.#intentAnswered(intent, written, false);
                replaced();
                return true;
            }, () => { this.#intentAnswered(intent, written, true); });
        };
        // The Consumer's judgement, as at a commit (ADR-0034): a Reject writes nothing. Answered later, it is taken
        // against the rows as they stand then: a source replaced or an order moved meanwhile makes the match another
        // row's, and the Replace is refused as a Replace All's answer is (ADR-0011); a cell whose row or entry changed
        // would be written with text made from what it held before, and is refused aloud.
        const source = this.#source;
        const sequence = this.#sequence();
        const opening = this.#openingTextAt(cell);
        const verdict = this.#verdictOf(resolved, holding.row, text, controller, { edit: undefined, cancelled }, false, answered => {
            this.#source.publishGathered?.();
            if (this.#source !== source) this.#refuseFind('sourceChanged');
            else if (this.#sequence() !== sequence) this.#refuseFind('orderChanged');
            else if (this.#rowAt(cell.row)?.row !== holding.row || this.#openingTextAt(cell) !== opening) refused(this.#labelOf(replaceCellChangedId));
            else judged(answered);
            return true;
        });
        if (verdict !== undefined) judged(verdict);
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
        // A Consumer that answers later answers on the asynchronous write, whatever the Window holds (ADR-0152).
        if (this.#copyAnswer === undefined && this.#copyAnswerAsync !== undefined) {
            this.#writeAnsweredLater(plan, false);
            event.preventDefault();
            return;
        }
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

    // The Context Menu's copy and Ctrl+Shift+C (ADR-0005, ADR-0036): neither a menu item's click nor a taken key fires
    // a copy event, so it always takes the asynchronous route, whatever the Selection's size, started inside the
    // gesture that chose it. With headers, the header row counts against the cap and goes into both flavours.
    #commandCopy(withHeaders: boolean): void {
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
        if (this.#copyAnswerAsync !== undefined) {
            this.#writeAnsweredLater(decision.plan, withHeaders);
            return;
        }
        this.#writeCopy(decision.plan, withHeaders);
    }

    // The Consumer's answer to an approved copy (ADR-0050, item 9), or nothing where it refused, which is said in its
    // own sentence.
    #answeredCopy(plan: CopyPlan, withHeaders: boolean): { readonly text: string; readonly html: string } | undefined {
        return this.#takenAnswer(defined(this.#copyAnswer, 'the copy answer')({ plan, withHeaders, rowSequenceVersion: this.#handedVersion() }));
    }

    // A Consumer's answer, checked: its flavours, or nothing where it refused, which is said in its own sentence.
    #takenAnswer(given: unknown): CopyFlavours | undefined {
        const answer = checkedCopyAnswer(given);
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

    // The Consumer's later answer to an approved copy (ADR-0152), written as it comes, held as a gathered copy is: one a
    // newer copy superseded, or one answered after the grid was destroyed, writes nothing and says nothing. Its outline is
    // taken as the copy is asked, as the rows in hand stand then, and lands with its write (ADR-0170).
    #writeAnsweredLater(plan: CopyPlan, withHeaders: boolean): void {
        const answerAsync = defined(this.#copyAnswerAsync, 'the asynchronous copy answer');
        const request: GridCopyRequest = { plan, withHeaders, rowSequenceVersion: this.#handedVersion() };
        const state = this.#state;
        const columns = this.#columns;
        const version = this.#sequence();
        const outlined = windowCovers(plan, state);
        this.#writeGathered(async signal => {
            let given: unknown;
            try {
                given = await answerAsync(request, signal);
            // oxlint-disable-next-line ex-grid/no-catch -- a superseded copy's own abort writes nothing; any other failure is said and rethrown
            } catch (error) {
                if (signal.aborted || this.#destroyed) return undefined;
                // Said, and the failure itself left to surface, as a gather's is: the refusal is the user's, the error
                // the Consumer's.
                this.#refuse({ kind: 'copy', reason: 'rowsUnavailable' });
                throw error;
            }
            if (signal.aborted || this.#destroyed) return undefined;
            const flavours = this.#takenAnswer(given);
            return flavours === undefined ? undefined : {
                flavours, outline: () => (outlined ? CopiedRange.of(plan, version, columns, windowRowAt(state)) : undefined),
            };
        });
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

    // A sentence a refusal or a held edit says in the live region (A11Y-15, A11Y-16). The Consumer's empty words
    // silence it (ADR-0203): the Consumer owns saying it, so what the grid still owed saying stands down too, and the
    // refusal is still raised by name.
    #sayRefusal(words: string): void {
        this.#announcedRanges = this.#held.selection.ranges;
        this.#cancelAnnounce?.();
        this.#cancelAnnounce = undefined;
        if (words === '') return;
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

    // The words a refusal is said in (ADR-0203): the Consumer's through `commandLabel` by the reason's id, else the
    // built-in English. A shape mismatch where a paste may spill offers a single cell too, and a column that is not
    // Editable is named by its header, or by its name where the header is blank.
    #wordsFor(refusal: WordedRefusal, column: number | undefined): string {
        const spills = refusal.kind === 'paste' && refusal.reason === 'shapeMismatch' && this.#pasteMaySpill;
        const id = refusal.kind === 'copy' ? copyRefusalLabelIds[refusal.reason] : spills ? shapeMismatchMaySpillLabelId : pasteRefusalLabelIds[refusal.reason];
        const words = this.#labelOf(id);
        if (column === undefined || !words.includes(columnPlaceholder)) return words;
        const refused = headerOf(itemAt(this.#columns, column));
        // As text, never as a replacement pattern: a header may hold `$&`.
        return words.replaceAll(columnPlaceholder, () => refused);
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
        this.#pasteFlavours(data.getData('text/plain'), data.getData('text/html'));
    };

    // A paste's two flavours, read in its event, or kept from it while a commit was pending (ADR-0380).
    #pasteFlavours(text: string, html: string): void {
        if (exceedsUtf8([text, html], this.#pasteByteCap)) {
            this.#refuse({ kind: 'paste', reason: 'tooLarge' });
            return;
        }
        const block = parseClipboardBlock(html, text);
        if (block !== undefined) this.#paste(block);
    }

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
        const controller = new AbortController();
        const intent = new GridPasteIntent(plan, block.values, block.origins, this.#handedVersion(), block.marks, controller.signal);
        const noted = this.#noteUserWrite(plan.targets);
        // Answered once it returns, or once its promise settles: a refusal after that would be one the grid can no
        // longer honour. What the source gathered while the Consumer wrote is put out then (ADR-0142 D5).
        const hear = defined(this.#pasteHeard, 'the paste\'s listener');
        const answer = this.#heardNow(() => hear(intent), failed => { this.#intentAnswered(intent, noted, failed); });
        if (answer === undefined) this.#pasted(intent, target, version);
        else this.#intentPending(answer, intent, controller, noted, () => { this.#pasted(intent, target, version); });
    }

    // A paste the Consumer answered: written, the Selection collapses to it or takes the block it spilled.
    #pasted(intent: GridPasteIntent, target: Selection, version: number): void {
        const { plan } = intent;
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
            const surface = this.#editSurface(edit);
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
        this.#markResultPress = undefined;
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
        if (event.target !== this.#markResult) this.#markResultPress = undefined;
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
            const editor = this.#editorField(edit);
            if (editor === undefined) {
                // Typed before a Chrome drew the field: the field is handed it once drawn.
                this.#writeEditorText(edit, this.#editorText(edit) + text);
            } else {
                const start = editor.selectionStart ?? editor.value.length;
                if (text !== '') editor.setRangeText(text, start, editor.selectionEnd ?? start, 'end');
                // Typed by the core, with no input event: a field drawn anew is handed it too.
                if (edit.slot !== undefined) edit.slot.text = editor.value;
            }
            // Typed as the field types, so the bar shows it and its References are asked again (ADR-0051, ADR-0057).
            if (text !== '') this.#onEditorTyped();
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
        // A double click on a Row Heading is two presses on it, and opens nothing (ADR-0050, item 1).
        if (event.button !== 0 || event.target !== this.#viewport || this.#edit !== undefined || this.#inRowHeadings(event) || this.#handedOver()) return;
        this.#reconcileSelection();
        const at = this.#cellUnder(event);
        if (at === undefined) return;
        this.#answered += 1;
        this.#invalidate();
        if (this.#isEditableCell(at)) {
            this.#apply(this.#held.selection.click(at, this.#extent));
            this.#startEdit(at, 'caret', this.#openingTextAt(at), 'cell', true);
            return;
        }
        this.#onCellDoubleClick?.(at);
    };

    // The move listeners stand only while a drag runs, so a pointer passing over costs nothing. Whatever
    // ends a drag ends a Heading drag, a press on the header and the edge band with it.
    #setDragging(dragging: boolean): void {
        if (!dragging) {
            this.#forgetHeadingDrag();
            this.#endHandleDrag();
            this.#pointDragging = false;
            this.#pointedDrag = undefined;
        }
        if (dragging === this.#dragging) return;
        this.#dragging = dragging;
        if (dragging) {
            this.#viewport.addEventListener('mousemove', this.#onDrag);
            this.#header.addEventListener('mousemove', this.#onDrag);
            this.#root.addEventListener('mouseup', this.#onDragEnd);
            this.#root.addEventListener('mouseleave', this.#onLeave);
        } else {
            // The Name Box names the Focus again once the button is up, where a drag showed its size (ADR-0052).
            if (this.#formulaBarDeclared.nameBoxSizeLabel !== undefined) this.#invalidate();
            this.#viewport.removeEventListener('mousemove', this.#onDrag);
            this.#header.removeEventListener('mousemove', this.#onDrag);
            this.#root.removeEventListener('mouseup', this.#onDragEnd);
            this.#root.removeEventListener('mouseleave', this.#onLeave);
        }
    }

    #forgetHeadingDrag(): void {
        this.#headingDrag = false;
        this.#headerPress = undefined;
        // Back to the columns' axis, which a press on the header takes without saying so: a drag left on the
        // rows' would extend the next column's gesture by row (ADR-0050, item 1).
        this.#headingAxis = 'columns';
        this.#stopEdgeTimer();
    }

    // Whether a press adds or takes out (ADR-0012): Ctrl, or Meta where Meta is Command; Shift outranks it.
    #toggles(event: MouseEvent): boolean {
        return !event.shiftKey && (event.ctrlKey || (event.metaKey && this.#metaIsPrimary));
    }

    // ---- The fill handle (ADR-0050, item 5) --------------------------------------------------------------

    // The handle where it is painted: declared, with no edit open, on a Selection of one range that is not every
    // cell, at the corner that range's shape names.
    #fillHandle(): { readonly range: SelectionRange; readonly corner: FillHandleCorner } | undefined {
        if (!this.#showFillHandle || this.#edit !== undefined) return undefined;
        const range = fillHandleRange(this.#held.selection);
        const corner = range === undefined ? undefined : fillHandleCorner(range, this.#extent);
        return range === undefined || corner === undefined ? undefined : { range, corner };
    }

    // What the layers paint of the fill: the handle where it stands, and a running drag's outline.
    #fillPaint(handle: { readonly range: SelectionRange; readonly corner: FillHandleCorner } | undefined, readable: ReadableEdge | undefined): FillPaint {
        return {
            outline: this.#handleOutline,
            handle: handle === undefined || readable === undefined ? undefined : { ...handle, sizePx: this.#metrics.fillHandleSizePx, readable },
        };
    }

    // Where the readable area begins over the painted rows, for a handle held at its edge: its top below the first
    // painted row's top, and its left in the content's coordinates.
    #readableEdge(visible: RowRange, geometry: ViewportGeometry): ReadableEdge {
        const columns = this.#styles.geometry;
        return {
            topPx: Math.max(0, -geometry.viewportTopPxOf(visible.start, this.#scrollTopPx)),
            leftPx: Math.min(Math.max(this.#scrollLeftPx, 0), columns.maxScrollLeftPx) + columns.pinnedWidthPx,
        };
    }

    // The range whose fill handle a press lands on, or `undefined`: arithmetic, as every other press on the rows is
    // (ADR-0008), at the place the handle is painted, with a grab box twice its side about its centre. A handle
    // not painted, or scrolled under the Pinned Columns or the Row Headings, cannot be grabbed, as it cannot be
    // seen; and a press on a Row Heading is the Heading's, though a whole row's handle stands beside it.
    #fillHandleUnder(event: MouseEvent): SelectionRange | undefined {
        const handle = this.#fillHandle();
        const visible = this.#visible;
        const geometry = this.#geometry;
        if (handle === undefined || visible === undefined || geometry === undefined) return undefined;
        const columns = this.#styles.geometry;
        if (columns.isInLead(event.offsetX, this.#scrollLeftPx)) return undefined;
        const size = this.#metrics.fillHandleSizePx;
        const rowHeight = this.#metrics.rowHeightPx;
        const readable = this.#readableEdge(visible, geometry);
        const pinnedCorner = handleCell(handle.range, handle.corner).column < columns.pinnedCount;
        // Grabbed only where it is painted: the same judgement the layer makes.
        if (paintedHandle(handle.range, handle.corner, columns, rowHeight, visible, size, pinnedCorner, readable) === undefined) return undefined;
        const [x, y] = handleCentrePx(handle.range, handle.corner, columns, rowHeight, visible.start, size, readable);
        const scrollLeft = readable.leftPx - columns.pinnedWidthPx;
        // The pinned layer stays put as the content pans, and the press is read in the content's coordinates.
        if (!pinnedCorner && x - scrollLeft < columns.pinnedWidthPx) return undefined;
        const centreX = pinnedCorner ? x + scrollLeft : x;
        return Math.abs(event.offsetX - centreX) <= size && Math.abs(event.offsetY - y) <= size ? handle.range : undefined;
    }

    // A press on the handle of `grabbed` starts a fill drag, not a selection: the Selection stays as it is, and the
    // moves reach out from it along one axis. Whether one started: a Selection the press's reconcile changed is
    // not the one whose handle the user grabbed.
    #startHandleDrag(grabbed: SelectionRange): boolean {
        const source = fillHandleRange(this.#held.selection);
        if (source === undefined || !sameSelectionRange(source, grabbed)) return false;
        this.#handleDrag = { source, sequence: this.#sequence(), binding: this.#source };
        this.#handlePointer = undefined;
        this.#handleOutline = undefined;
        this.#setDragging(true);
        this.#invalidate();
        return true;
    }

    // The drag's pointer moved onto `cell`. Whether what is painted changes: the outline moves only when what the
    // drag reaches does.
    #moveHandlePointer(cell: CellPosition): boolean {
        const drag = this.#handleDrag;
        if (drag === undefined || (this.#handlePointer !== undefined && sameCell(cell, this.#handlePointer))) return false;
        this.#handlePointer = cell;
        const reached = fillExtensionFor(drag.source, cell);
        const outline = reached === undefined ? undefined : boundingRange(drag.source, reached.target);
        const was = this.#handleOutline;
        if (outline === undefined ? was === undefined : was !== undefined && sameSelectionRange(outline, was)) return false;
        this.#handleOutline = outline;
        return true;
    }

    // A fill drag over, raised or not: its outline goes.
    #endHandleDrag(): void {
        if (this.#handleDrag === undefined) return;
        this.#handleDrag = undefined;
        this.#handlePointer = undefined;
        this.#handleOutline = undefined;
        this.#invalidate();
    }

    // The drag's release (ADR-0050, item 5): exactly one Fill Intent, or one refusal, or nothing when the pointer
    // came back inside the source. Editable is judged on the target before anything is raised (ADR-0035). Judged
    // on the newest rows the source holds (ADR-0142 D5): positions taken under an order that has moved since, or
    // on what a source since replaced painted, name other rows, so such a drag is refused, as a paste aimed under
    // another order is (ADR-0011).
    #finishHandleDrag(releasedOn: CellPosition | undefined): void {
        const drag = this.#handleDrag;
        const pointer = releasedOn ?? this.#handlePointer;
        this.#setDragging(false);
        // An edit opened while the button was held takes the handle away, and with it the drag: nothing is raised
        // under an open editor (ADR-0034).
        if (this.#destroyed || drag === undefined || pointer === undefined || this.#edit !== undefined) return;
        const columns = this.#columns;
        const decision = planHandleFill(drag.source, pointer, column => column >= 0 && column < columns.length && itemAt(columns, column).editable);
        // A release back inside the source raises nothing, under any order.
        if (decision === undefined) return;
        this.#source.publishGathered?.();
        this.#reconcileSelection();
        if (drag.binding !== this.#source) {
            this.#refuse({ kind: 'paste', reason: 'sourceChanged' });
            return;
        }
        if (drag.sequence !== this.#sequence()) {
            this.#refuse({ kind: 'paste', reason: 'orderMoved' });
            return;
        }
        if (decision.refused) {
            this.#refuse({ kind: 'paste', reason: decision.reason }, decision.column);
            return;
        }
        // Nobody taking the intent is nobody accepting it: the Selection stays on the source.
        const listener = this.#fillHeard;
        if (listener === undefined) return;
        this.#supersedeFill();
        const controller = new AbortController();
        const intent = new GridFillIntent(drag.source, decision.extension.target, decision.extension.direction, this.#handedVersion(), controller.signal);
        // The user's own write (ADR-0142 D1), taken back if refused; what the source gathered while the Consumer
        // filled is put out as it answers (D5).
        const written = this.#noteUserWrite([intent.target]);
        const landed = (): void => {
            if (!intent.refused) this.#selectFilled(drag.source, intent.target, drag.sequence, drag.binding);
        };
        const answer = this.#heardNow(() => listener(intent), failed => { this.#intentAnswered(intent, written, failed); });
        // Answered later, the fill is pending as a paste is, and the Selection is placed once it lands (ADR-0380).
        if (answer === undefined) landed();
        else this.#intentPending(answer, intent, controller, written, landed);
    }

    // After a fill the Consumer accepted, the Selection is the source and the target together, as in Excel
    // (ADR-0050, item 5 refined), the Focus on the cell it was on, inside the source, so nothing scrolls. Positions
    // mean something only under the order they were written in, and the Selection is the user's: if the order or
    // the source moved while the Consumer filled, or the Selection is no longer the source, nothing is placed
    // (ADR-0011).
    #selectFilled(source: SelectionRange, target: SelectionRange, sequence: number, binding: GridSource<Row>): void {
        this.#reconcileSelection();
        if (this.#destroyed || sequence !== this.#sequence() || binding !== this.#source) return;
        const current = this.#held.selection;
        const focus = current.focus;
        if (current.ranges.length !== 1 || !sameSelectionRange(itemAt(current.ranges, 0), source) || focus === undefined || !rangeContains(source, focus)) {
            return;
        }
        const extent = this.#extent;
        const filled = boundingRange(source, target);
        if (bottomRow(filled) >= extent.rowCount || rightColumn(filled) >= extent.columnCount) return;
        if (this.#apply(current.place(filled, focus, extent))) this.#invalidate();
    }

    // ---- The header: its press, its click and the Heading drag (ADR-0012, ADR-0050 item 1) -------------

    // A press on the header. The click sorts on a plain grid, so the press selects nothing until the
    // pointer reaches another column; released where it was pressed, it is a click (ADR-0012, 2026-09-29).
    readonly #onHeaderPress = (event: MouseEvent): void => {
        this.#answered += 1;
        this.#invalidate();
        if (this.#pointedAtNow()) {
            this.#handOverHeaderPress(event);
            return;
        }
        this.#pressHandedOver = false;
        // A new press is a new story: a release outside the header raises no click, and a flag left
        // standing would eat the next genuine sort.
        this.#gestureConsumedClick = false;
        this.#tabReleased = false;
        // A press on the header's checkbox is the checkbox's: it neither sorts, selects nor drags, never takes the
        // keyboard, and notes the order and source it was made under for its click (ADR-0043, MK-9).
        if (event.target === this.#rowMarks.headerBox) {
            event.preventDefault();
            if (event.button === 0) {
                this.#headerMarkPress = { sequence: this.#sequence(), source: this.#source, paged: this.#pageSize !== undefined, page: this.#pageRows() };
            }
            return;
        }
        if (event.button !== 0) return;
        // A press on a ▾ is the button's: its click opens the menu, and it neither sorts nor drags. It never takes
        // the keyboard itself: an edit the click cannot commit keeps it in the editor (ADR-0034), and a menu the
        // click opens takes it from where it is.
        if (event.target instanceof Element && (this.#menuButtons.has(event.target) || this.#groupToggles.has(event.target))) {
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
        // A new gesture on its own axis, as a press on a Row Heading is: nothing of the last Heading drag stands.
        this.#forgetHeadingDrag();
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
        if (this.#handedOver()) return;
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
        // A toggle carries its group's id, so a click that lands after the groups changed acts on the group as it
        // shows now, or on nothing once the group draws no toggle (ADR-0400 section 5). An open edit is settled first,
        // as by a press on a header.
        const toggled = event.target instanceof Element ? this.#groupToggles.get(event.target) : undefined;
        if (toggled !== undefined) {
            if (event.button !== 0 || !this.#commitEdit()) return;
            const group = this.#headerGroups.find(candidate => candidate.id === toggled);
            if (group?.expanded !== undefined) this.#toggleGroup(group);
            return;
        }
        if (event.button !== 0 || event.target !== this.#header) return;
        this.#reconcileSelection();
        // The corner where the two Headings meet selects all (ADR-0050), as Ctrl+A does, and puts the Focus on the
        // top-left cell on screen, as a Heading click puts it on the first row or column on screen (ADR-0052).
        if (this.#rowHeadingStyle !== undefined && this.#styles.geometry.isInLead(event.offsetX, this.#scrollLeftPx)) {
            this.#selectAllFromCorner();
            return;
        }
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
        if (grip === undefined || heard === undefined || this.#handedOver()) return;
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

    #selectAllFromCorner(): void {
        const extent = this.#extent;
        if (extent.rowCount <= 0 || extent.columnCount <= 0) return;
        let all = this.#held.selection.selectAll(extent, this.#selectAllContext(extent));
        const [whole] = all.ranges;
        const topLeft = this.#firstVisibleCell();
        if (whole !== undefined && topLeft !== undefined && rangeContains(whole, topLeft)) all = all.place(whole, topLeft, extent);
        this.#apply(all);
    }

    // Whether a pointer on the rows is over the Row Headings, which name no column (ADR-0050, item 1).
    #inRowHeadings(event: MouseEvent): boolean {
        return this.#rowHeadingStyle !== undefined && this.#styles.geometry.isInLead(event.offsetX, this.#scrollLeftPx);
    }

    // A press on a Row Heading (ADR-0050, item 1): the whole row, with Shift whole rows from the Focus's, with Ctrl
    // the row added or taken out, and then a Heading drag whose moves extend by row; a cell drag's would extend by
    // cell and collapse the whole rows. The band stands outside the column index space, so the Focus lands in the
    // first column on screen, where the Viewport already is. An open edit commits first (ED-26), and one the edit
    // holds selects nothing (ADR-0034).
    #pressRowHeading(event: MouseEvent, row: number): void {
        this.#answered += 1;
        this.#invalidate();
        if (!this.#commitEdit()) {
            event.preventDefault();
            return;
        }
        this.#forgetHeadingDrag();
        this.#tabReleased = false;
        const y = this.#viewportYOf(event);
        if (y === undefined) return;
        this.#headingPress = { x: event.offsetX - this.#scrollLeftPx, y, clientX: event.clientX, clientY: event.clientY };
        this.#setDragging(true);
        this.#beginRowHeadingDrag(row, event.shiftKey, this.#toggles(event));
    }

    // The column the Focus lands in when a Row Heading is pressed: the first on screen, so the Viewport does not
    // move for a press on the band (KB-9).
    get #firstVisibleColumn(): number {
        return this.#firstVisibleCell()?.column ?? 0; // oxlint-disable-line ex-grid/no-stand-in-default -- KB-9: with no row painted the Focus goes on the first column, as the C# core puts it
    }

    // What a press on a Row Heading selects, as #beginHeadingDrag does for a column (ADR-0050, item 1; ADR-0052).
    #beginRowHeadingDrag(row: number, extendsSelection: boolean, toggles: boolean): void {
        const extent = this.#extent;
        if (extent.rowCount <= 0 || extent.columnCount <= 0) {
            this.#setDragging(false);
            return;
        }
        const current = this.#held.selection;
        const first = this.#firstVisibleColumn;
        if (toggles && current.coversRow(row, extent)) {
            this.#apply(current.toggleRow(row, extent, first));
            this.#setDragging(false);
            return;
        }
        this.#apply(extendsSelection ? current.extendToRow(row, extent, first)
            : toggles ? current.toggleRow(row, extent, first)
                : current.selectRow(row, extent, first));
        this.#headerPress = undefined;
        this.#headingAxis = 'rows';
        this.#headingDrag = true;
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
        this.#headingAxis = 'columns';
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
        // Along the Row Headings only the pointer's row counts, whatever column it is over (ADR-0050, item 1).
        if (this.#headingAxis === 'rows') {
            const geometry = this.#geometry;
            const row = geometry?.rowAt(geometry.contentOffsetAt(this.#scrollTopPx) + y);
            if (row === undefined) return;
            if (this.#apply(this.#held.selection.extendToRow(row, this.#extent, this.#firstVisibleColumn))) this.#invalidate();
            this.#updateEdgeBand(x, y);
            return;
        }
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
        const columnsOnly = this.#headingDrag && this.#headingAxis === 'columns';
        const rowsOnly = this.#headingDrag && this.#headingAxis === 'rows';
        const rows = columnsOnly ? 0 : edgeBandRowsPerTick(viewportY, geometry.viewportHeightPx, geometry.rowsPerViewport);
        const stepXPx = rowsOnly ? 0 : edgeBandRowsPerTick(viewportX, this.#visibleWidthPx, Number.MAX_SAFE_INTEGER) * this.#metrics.rowHeightPx;
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
            changed = this.#handleDrag !== undefined ? this.#moveHandlePointer(cellPosition(row, column))
                : this.#apply(!this.#headingDrag ? current.extendTo(cellPosition(row, column), extent)
                    : this.#headingAxis === 'rows' ? current.extendToRow(row, extent, this.#firstVisibleColumn)
                        : current.extendToColumn(column, extent, this.#firstVisibleRow));
        }
        if (moved || changed) this.#invalidate();
    };

    // The cell under a pointer on the Viewport, by arithmetic on its offsets (ADR-0012): rows counted from
    // the row painted first, columns from the content's left edge.
    #cellUnder(event: MouseEvent): CellPosition | undefined {
        return this.#cellAt(event.offsetX, event.offsetY);
    }

    // The cell at a pair of offsets into the Viewport, whose top edge is the first painted row's.
    #cellAt(x: number, y: number): CellPosition | undefined {
        const geometry = this.#geometry;
        const visible = this.#visible;
        if (geometry === undefined || visible === undefined) return undefined;
        const row = geometry.rowAt(y + geometry.offsetPxOf(visible.start));
        const column = this.#styles.geometry.columnAt(x, this.#scrollLeftPx);
        return row === undefined || column === undefined ? undefined : cellPosition(row, column);
    }

    #cellId(position: CellPosition): string {
        return `${this.#ids}r${position.row}c${position.column}`;
    }

    // Whether a cell is in the DOM now: its row painted with its data, and its column pinned or in the
    // horizontal slice, never mid-fling, when the scrollable cells are skipped (ADR-0004, ADR-0033).
    // Whether a row is among those painted now, its data in hand: the first half of #cellIsPainted.
    #rowIsPainted(row: number): boolean {
        const visible = this.#visible;
        if (visible === undefined || row < visible.start || row >= visible.start + visible.count) return false;
        const slice = row - this.#state.windowStart;
        return slice >= 0 && slice < this.#state.window.length;
    }

    #cellIsPainted(position: CellPosition): boolean {
        if (!this.#rowIsPainted(position.row)) return false;
        if (position.column < this.#styles.geometry.pinnedCount) return true;
        const columns = this.#scrollable;
        return !this.#placeholderMode && columns !== undefined
            && position.column >= columns.start && position.column < columns.start + columns.count;
    }

    // Moves the model to where a key's reveal is going, and resolves the slice there, so the pass paints
    // the rows at the offset it then writes (ADR-0012, 2026-09-29). Whether there is an offset to write.
    #stageReveal(): boolean {
        // A box too small for a row has no offset to reveal at: the reveal stays owed for the pass that has one,
        // so a key that moved the Focus meanwhile brings it into view once the box grows back (ADR-0040).
        if (this.#geometry === undefined) return false;
        const reveal = this.#revealOwed;
        this.#revealOwed = undefined;
        const across = this.#revealHeaderOwed;
        this.#revealHeaderOwed = undefined;
        const columns = this.#styles.geometry;
        // A header Find found is scrolled into view across only (ADR-0333 §1): its last column, then its first,
        // so a header wider than the Viewport shows its start. A pinned header is in view already.
        if (reveal === undefined && across !== undefined) {
            let left = this.#scrollLeftPx;
            if (across.last < columns.count) left = columns.scrollLeftToReveal(across.last, left);
            if (across.first < columns.count) left = columns.scrollLeftToReveal(across.first, left);
            return this.#revealAt(this.#scrollTopPx, left);
        }
        if (reveal === undefined) return false;
        const { target } = reveal;
        if (target.row >= this.#totalRows || target.column >= columns.count) return false;
        // While extending, an axis the range spans end to end is not scrolled for (ADR-0052, 2026-09-29).
        const range = reveal.extending;
        const extent = this.#extent;
        const keepsRows = range !== undefined && spansEveryRow(range, extent);
        // The keyboard crosses pages; dragging does not (ADR-0015): a cell carried past the page's edge turns the
        // page, and the reveal then places it in the new one. A page turn is the pager's scroll, so an extension the
        // view does not scroll down for turns none.
        const size = this.#pageSize;
        if (size !== undefined && !keepsRows) this.#turnPageTo(Math.floor(target.row / size));
        const geometry = defined(this.#geometry, 'the geometry a page turn resolves');
        let top = this.#scrollTopPx;
        if (keepsRows) {
            const follow = this.#followArmed;
            if (follow !== undefined) this.#followArmed = { ...follow, rowsKept: true };
        } else {
            // PageUp and PageDown scroll by the rows the Focus moved, through the mapping, so a compressed grid
            // moves by rows (ADR-0053); the reveal still runs over the stepped offset.
            const stepped = reveal.stepRows === 0
                ? this.#scrollTopPx
                : geometry.scrollTopStepped(this.#scrollTopPx, reveal.stepRows * this.#metrics.rowHeightPx);
            top = geometry.scrollTopToReveal(target.row, stepped);
        }
        // "Go to the beginning" is not "make this cell visible": with Pinned Columns the two come apart.
        let left = reveal.rowStart ? 0 : columns.scrollLeftToReveal(target.column, this.#scrollLeftPx);
        if (range !== undefined && spansEveryColumn(range, extent) && !reveal.rowStart) left = this.#scrollLeftPx;
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
        if (!this.#cellIsPainted(follow.target)) {
            // A target whose row is painted and whose column is not is one no later pass will paint on its own: the
            // reveal moved the axis the Selection grew on and left the other where the user had it, so the Extent of
            // whole rows is the last column, off to the right for ever (ADR-0052). Rows still on their way are the
            // other case, and that follow stays armed for the pass that paints them. Whole columns are the same on the
            // other axis: the reveal left the rows where the user had them, so the Extent, the last row, is one no
            // later pass reaches either. Dropped rather than kept, or the Settled Mark would never come back
            // (ADR-0220).
            if (follow.rowsKept === true || this.#rowIsPainted(follow.target.row)) this.#followArmed = undefined;
            return;
        }
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

    // A Chrome slot's control takes the keyboard (ADR-0204, ADR-0390). A focus the grid owes, the one a menu's
    // opening asked for, is taken wherever DOM focus stands, as the built-in menu takes it: a press on a ▾ never moves
    // the keyboard itself (ADR-0039). Any other only while the keyboard is still this grid's, so a grid or a field
    // of the page's the user has moved to keeps it.
    #takeChromeFocus(control: HTMLElement, preventScroll: boolean, owed: boolean): void {
        if (!control.isConnected || (!owed && !this.#keyboardHere())) return;
        keyboardTargetIn(control, 'takeFocus').target.focus({ preventScroll });
    }

    // DOM focus inside this root and not in a grid nested in it, or on nothing; or, while the keyboard is
    // moving, where it is going.
    #keyboardHere(toward?: Element): boolean {
        const active = toward ?? this.#root.ownerDocument.activeElement;
        const doc = this.#root.ownerDocument;
        return active === null || active === doc.body || active === doc.documentElement
            || (this.#root.contains(active) && active.closest('.ex-grid') === this.#root);
    }

    // The colours of the References in the open edit's text (ADR-0057), asked of the Consumer only when the text or
    // the function changed since it was last asked; none while no edit is open.
    #colouring(): ReferenceColouring {
        return this.#referenceOutlines.colouring(this.#edit === undefined ? undefined : this.#editorText(this.#edit), this.#referenceOptions.referencesIn);
    }

    // The References asked again of the editor's text as it now stands, where it changed (ADR-0057): an answer refused
    // is refused here, in the input that changed it, and never in a pass. A pass is owed only when what the layers
    // paint or the keys told changed, so typing text that is no Formula paints nothing.
    #askReferences(): void {
        if (this.#referenceOptions.referencesIn === undefined) return;
        const was = this.#referenceOutlines.asked;
        try {
            this.#colouring();
        } finally {
            // A refused answer is held as none: the outlines of the text before it go too.
            if (!ReferenceOutlines.same(was, this.#referenceOutlines.asked)) this.#invalidate();
        }
    }

    // Point's outline (ADR-0051, ADR-0057): dashes in the Focus outline's colour. Where the text names the range it
    // points at, that range has its Reference Outline, and the dashes lie just inside its line; where it does not, or
    // where no References function is given, the dashes are Point's own look.
    #pointPaint(pointer: Selection | undefined, colouring: ReferenceColouring): PointPaint | undefined {
        const range = pointer?.ranges[0];
        if (range === undefined) return undefined;
        return { range, className: colouring.colourOf(range) === undefined ? 'ex-point' : 'ex-point ex-point-on-reference' };
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
        // The fill handle, and where the readable area it is held at the edge of begins (ADR-0050, item 5).
        const handle = this.#fillHandle();
        const readable = handle === undefined || visible === undefined || geometry === undefined ? undefined : this.#readableEdge(visible, geometry);
        const inputs = {
            selection: this.#held.selection, copied: this.#copied, start: visible?.start, count: visible?.count, styles: this.#styles,
            rowHeightPx: this.#metrics.rowHeightPx, scrollLeftPx: this.#scrollLeftPx, firstOpenRow,
            handleRange: handle?.range, handleCorner: handle?.corner, handleSizePx: this.#metrics.fillHandleSizePx,
            readableTopPx: readable?.topPx, readableLeftPx: readable?.leftPx, fillOutline: this.#handleOutline,
            // A row the source has since taken away has no band; the next movement finds the row there now.
            hoverRow: this.#hoverRow !== undefined && this.#hoverRow < this.#totalRows ? this.#hoverRow : undefined,
            focusRow: this.#highlightFocusRow,
            // Each held, so a pass that changed neither text nor columns finds them the same.
            colouring: this.#colouring(), extentRows: this.#totalRows, extentColumns: this.#columns.length,
            pointer: this.#edit === undefined ? undefined : this.#pointer,
            outlinedColumns: this.#referenceOutlines.columns(this.#outlinedColumnsBoth(), this.#columnNames, this.#totalRows),
            pointDashes: this.#pointDashesRange(),
        };
        const was = this.#layerInputs;
        // A pass that moved none of them, every scroll frame along an axis the layers ignore, writes nothing.
        if (was === undefined || Object.entries(inputs).some(([name, value]) => was[name] !== value)) {
            this.#layerInputs = inputs;
            const pieces = visible === undefined || geometry === undefined
                ? { shade: undefined, pinned: [], scrollable: [] }
                : selectionLayers(inputs.selection, inputs.copied === undefined ? [] : inputs.copied.rectangles, this.#styles.geometry, inputs.rowHeightPx, visible,
                    { firstOpenRow, scrollLeftPx: this.#scrollLeftPx, hasHeader: true }, { hoverRow: inputs.hoverRow, focusRow: inputs.focusRow },
                    this.#fillPaint(handle, readable), {
                        references: inputs.colouring.outlines.flatMap(([range, colour]) => {
                            const drawn = withinExtent(range, { rowCount: inputs.extentRows, columnCount: inputs.extentColumns });
                            return drawn === undefined ? [] : [[drawn, colour] as const];
                        }),
                        columns: inputs.outlinedColumns,
                        point: this.#pointPaint(inputs.pointer, inputs.colouring),
                        pointDashes: inputs.pointDashes,
                    });
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

    // The Heading Mark over the Row Headings (ADR-0008, 2026-10-13): one rectangle per run of marked rows, cut to
    // the painted rows, on whose right edge the stylesheet draws the line. Over the Headings, as the header's part
    // is over the header cells, so no row learns the Selection and every row still skips its render (ADR-0003).
    // The layer is in the DOM only while it holds a rectangle.
    #paintRowMarks(): readonly HTMLElement[] {
        const runs = this.#rowHeadingMarks();
        const visible = this.#visible;
        const styles = runs === undefined || visible === undefined ? []
            : runs.rows.flatMap(run => present(rowMark(run, visible, this.#metrics.rowHeightPx, this.#styles.geometry.leadWidthPx)));
        if (!sameItems(this.#paintedRowMarks, styles)) {
            this.#paintedRowMarks = styles;
            const doc = this.#root.ownerDocument;
            this.#rowMarkLayer.replaceChildren(...styles.map(style => {
                const made = doc.createElement('div');
                made.className = 'ex-heading-mark ex-heading-mark-row';
                made.setAttribute('style', style);
                return made;
            }));
        }
        return styles.length === 0 ? [] : [this.#rowMarkLayer];
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

    // A box or scroll content written may change the box the browser tells: content that comes to overflow shows a
    // scrollbar, which takes its strip out of the Viewport (ADR-0013). Until the browser has laid it out and told
    // any such change the grid is not settled: a reading taken before would see a band built again (ADR-0220).
    #lookForNewLayout(): void {
        const layout = `${this.#root.getAttribute('style')}|${this.#scroller.getAttribute('style')}|${this.#spacer.getAttribute('style')}|`
            + [...this.#root.children].map(child => child.className).join(' ');
        if (layout === this.#laidOut) return;
        this.#laidOut = layout;
        this.#awaitBoxTold();
    }

    #awaitBoxTold(): void {
        this.#cancelBoxTold?.();
        this.#cancelBoxTold = undefined;
        const ask = { open: true };
        const cancel = this.#reports.afterLayout(this.#root, () => {
            const owed = ask.open && this.#cancelBoxTold !== undefined;
            ask.open = false;
            if (!owed) return;
            this.#cancelBoxTold = undefined;
            this.#lookSettled();
        });
        // Told as it was asked, where nothing is laid out: nothing is owed.
        if (ask.open) {
            this.#cancelBoxTold = () => {
                ask.open = false;
                cancel();
            };
        }
    }

    #paint(): void {
        // What is painted now is what the next key is aimed at.
        this.#droppedAim = undefined;
        const metrics = this.#metrics;
        const geometry = this.#styles.geometry;
        const root = this.#root;
        // ex-loading while rows are on their way, and ex-editing while the Cell Editor stands (ADR-0029).
        this.#write(root, 'root', 'class',
            `ex-grid${this.#state.loading ? ' ex-loading' : ''}${this.#edit === undefined ? '' : ' ex-editing'}${this.#pointedAtNow() ? ' ex-pointed-at' : ''}`);
        // The Geometry Tokens, inline on the root from the same resolved metrics the arithmetic reads
        // (ADR-0027, ADR-0028), and the Device Pixel once told (ADR-0090).
        this.#write(root, 'root', 'style', `--ex-row-height: ${metrics.rowHeightPx}px; --ex-header-height: ${metrics.headerHeightPx}px; `
            + `--ex-font-size: ${metrics.fontSizePx}px; --ex-cell-padding-x: ${metrics.cellPaddingXPx}px; `
            + `--ex-action-padding-x: ${metrics.actionPaddingXPx}px; --ex-action-border-width: ${metrics.actionBorderPx}px; `
            + `--ex-action-gap: ${metrics.actionGapPx}px; --ex-menu-button-width: ${metrics.menuButtonWidthPx}px; `
            + `--ex-menu-button-inset: ${metrics.menuButtonInsetPx}px; --ex-sort-mark-width: ${metrics.sortMarkWidthPx}px; `
            + `--ex-resize-grip-width: ${metrics.resizeGripWidthPx}px; --ex-column-guide-width: ${metrics.columnGuideWidthPx}px; `
            + `--ex-header-group-toggle-width: ${metrics.headerGroupToggleWidthPx}px`
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
        // Cancellable work placed is what an Escape cancels, named by the latest placed (ADR-0240 section 12); under
        // way, the root is busy and painted as a paste under way is.
        const placedWork = this.#works.findLast(work => work.tooLate !== undefined);
        this.#write(this.#stamp, 'stamp', 'data-ex-work-placed', placedWork === undefined ? undefined : String(placedWork.id));
        this.#write(root, 'root', 'data-ex-work', this.#workUnderWay === undefined ? undefined : String(this.#workUnderWay.id));
        this.#write(root, 'root', 'aria-busy', this.#workUnderWay === undefined ? undefined : 'true');
        this.#write(this.#stamp, 'stamp', 'data-ex-escape-leaves',
            choice === undefined && this.#copied === undefined && this.#edit === undefined && this.#popover === undefined && placedWork === undefined ? '' : undefined);
        const scrollHeightPx = this.#geometry?.scrollHeightPx ?? 0; // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0013: a Viewport with no room for a row scrolls nothing
        this.#write(this.#scroller, 'scroller', 'style', this.#scrollerStyle());
        this.#write(this.#spacer, 'spacer', 'style', `width: ${geometry.totalWidthPx}px; height: ${this.#bandHeightPx + scrollHeightPx}px`);
        // A current match the user has moved off is no longer marked as one (ADR-0332 §4).
        this.#forgetCurrentMatchOffFocus();
        this.#paintHeader();
        this.#paintViewport();
        this.#paintStatus();
        this.#paintMessage();
        this.#paintSizeTip();
        this.#paintLoading();
        this.#paintFormulaBar();
        this.#paintCompletion();
        this.#placePopover();
    }

    // The Size Tip's text (ADR-0052, 2026-09-29): the Consumer's size label for the range, while a Heading drag covers
    // more than one column or row. None otherwise, over one, without a label or with no answer: then nothing is
    // painted and the Name Box names the Focus.
    #sizeTipText(): string | undefined {
        const label = this.#formulaBarDeclared.nameBoxSizeLabel;
        const range = this.#held.selection.focusRange;
        if (!this.#dragging || !this.#headingDrag || this.#headerPress !== undefined || label === undefined || range === undefined) return undefined;
        const covered = this.#headingAxis === 'columns' ? range.columnCount : range.rowCount;
        if (covered <= 1) return undefined;
        const size: unknown = label(range);
        return size === undefined ? undefined : this.#nameBoxWords(size, 'nameBoxSizeLabel');
    }

    // The Size Tip (ADR-0052, 2026-09-29), at the Heading the Extent is on: beneath its Column Heading, or beside its
    // Row Heading, inside the grid's box. It follows the Extent's Heading, not the pointer, so it is written only when
    // the Extent moves to another; its width is the Cell Metrics' estimate of its text, so nothing is measured
    // (ADR-0021). Under the root and outside the scroll container, as a popover is (ADR-0040); paint only, taking no
    // pointer and saying nothing to assistive technology, to which the live region describes the range (ADR-0033).
    #paintSizeTip(): void {
        const text = this.#sizeTipText();
        const extent = this.#held.selection.extent;
        const geometry = this.#geometry;
        if (text === undefined || extent === undefined || geometry === undefined) {
            this.#sizeTip?.remove();
            this.#sizeTip = undefined;
            return;
        }
        let tip = this.#sizeTip;
        if (tip === undefined) {
            tip = this.#root.ownerDocument.createElement('div');
            tip.className = 'ex-size-tip';
            tip.setAttribute('aria-hidden', 'true');
            this.#sizeTip = tip;
        }
        const band = this.#bandHeightPx;
        const width = Math.min(this.#metrics.cellMetrics.for('text').estimatePx(text), Math.max(0, this.#visibleWidthPx));
        let left: number;
        let top: number;
        if (this.#headingAxis === 'columns') {
            left = this.#cellLeftPx(extent.column);
            top = band;
        } else {
            left = this.#styles.geometry.leadWidthPx;
            top = Math.min(Math.max(band + geometry.viewportTopPxOf(extent.row, this.#scrollTopPx), band),
                Math.max(band, this.#visibleHeightPx - this.#metrics.rowHeightPx));
        }
        left = Math.min(Math.max(left, 0), Math.max(0, this.#visibleWidthPx - width));
        setAttribute(tip, 'style', `left: ${left}px; top: ${this.#barPx + top}px; width: ${width}px`);
        if (tip.textContent !== text) tip.textContent = text;
        if (tip.parentNode !== this.#root) this.#scroller.after(tip);
    }

    // The Chrome's loading indicator (ADR-0010), at the root's foot before the pager and the status line, as the C#
    // core renders it: handed whether rows are on their way and how many painted rows are Placeholders, which stay
    // the core's (ADR-0004).
    #paintLoading(): void {
        const mount = this.#chrome?.loadingIndicator;
        const box = this.#loadingSlot.element;
        if (mount === undefined) {
            this.#loadingSlot.hide();
            box.remove();
            return;
        }
        const loading = this.#state.loading;
        const placeholderRows = this.#placeholderCount();
        this.#loadingSlot.show(mount, `${loading} ${placeholderRows}`, () => Object.freeze({ loading, placeholderRows }));
        const before = this.#pager.parentNode === this.#root ? this.#pager : this.#status.parentNode === this.#root ? this.#status : this.#announce;
        if (box.nextSibling !== before) this.#root.insertBefore(box, before);
    }

    // How many of the painted rows are Placeholders: every one while a fling paints them, and otherwise those the
    // Window does not hold.
    #placeholderCount(): number {
        const visible = this.#visible;
        if (visible === undefined) return 0;
        if (this.#placeholderMode) return visible.count;
        let count = 0;
        for (let row = visible.start; row < visible.start + visible.count; row++) {
            if (this.#rowAt(row) === undefined) count += 1;
        }
        return count;
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
        this.#paintPager();
        const paged = this.#pageSize !== undefined;
        const count = this.#rowMarks.marks === undefined ? undefined
            : this.#rowMarks.paint(id => this.#labelOf(id), paged ? this.#pageMarked() : undefined);
        this.#reviewSummary();
        const shown: HTMLElement[] = [...this.#selectionStatus()];
        if (count !== undefined) shown.push(count);
        // Under a pager the header's checkbox lines up the page, and the whole result is the explicit step past it
        // (ADR-0015, ADR-0043).
        if (paged && this.#rowMarks.marks !== undefined) {
            const counts = this.#rowMarks.marks.counts;
            // The rows the marks count in the result, and until they have counted, the result's rows, as the C# core
            // says it (MarkableRows): the button is offered before the Consumer's first answer.
            const rows = counts === undefined ? this.#totalRows : counts.rowsInResult;
            const said = this.#labelOf(markLabelIds.markAllResultRows).replaceAll('{count}', String(rows));
            if (this.#markResult.textContent !== said) this.#markResult.textContent = said;
            shown.push(this.#markResult);
        }
        if (this.#showSelectionSummary && this.canSummarize) {
            const summary = this.#summary.summary;
            const focus = this.#held.selection.focus;
            const column = focus === undefined ? undefined : this.#columns[focus.column]?.column;
            this.#summaryBox.paint(summary, summaryFiguresShown(summary.result, column), id => this.#labelOf(id), this.#chrome?.selectionSummary);
            this.#summaryBox.claimMenu(this.#onSummaryFiguresChanged !== undefined);
            shown.push(this.#summaryBox.element);
        } else {
            this.#summaryBox.hide();
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

    // The off-screen indicator (ADR-0014, ADR-0015): a Selection none of whose rectangles meets what is painted is
    // said, with its size, so a paste into it is never made unseen and unannounced, and a button brings it back.
    // Judged against the offset the scroller is going to (ADR-0012), and with no row data. Under a pager the count
    // stands whenever anything is selected, with the offer of the whole result, the explicit way past the page's
    // context (ADR-0015).
    #selectionStatus(): readonly HTMLElement[] {
        const { ranges } = this.#held.selection;
        const paged = this.#pageSize !== undefined;
        if (ranges.length === 0) return [];
        const inView = this.#selectionInView(ranges);
        if (inView && !paged) return [];
        const said = this.#labelOf(inView ? selectionLabelIds.selectedCells : selectionLabelIds.selectedOutsideView)
            .replaceAll('{count}', String(this.#held.selection.cellCount));
        if (this.#offScreen.textContent !== said) this.#offScreen.textContent = said;
        const shown = [this.#offScreen];
        if (!inView) {
            const go = this.#labelOf(selectionLabelIds.goToSelection);
            if (this.#goToSelection.textContent !== go) this.#goToSelection.textContent = go;
            shown.push(this.#goToSelection);
        }
        if (paged) {
            const all = this.#labelOf(selectionLabelIds.selectAllRows).replaceAll('{count}', String(this.#totalRows));
            if (this.#selectWholeResult.textContent !== all) this.#selectWholeResult.textContent = all;
            shown.push(this.#selectWholeResult);
        }
        return shown;
    }

    // The pager's count and its buttons, each disabled at its end (ADR-0015).
    #paintPager(): void {
        if (this.#pageSize === undefined) return;
        const count = this.#pageCount;
        const said = `${this.#pageIndex + 1} / ${count}`;
        if (this.#pageShown.textContent !== said) this.#pageShown.textContent = said;
        if (this.#pageBack.disabled !== (this.#pageIndex === 0)) this.#pageBack.disabled = this.#pageIndex === 0;
        if (this.#pageOn.disabled !== (this.#pageIndex >= count - 1)) this.#pageOn.disabled = this.#pageIndex >= count - 1;
    }

    // What the header's checkbox shows under a pager (ADR-0043, ADR-0015): the page's rows, asked one by one, so
    // nothing is guessed, as the C# core asks them. A page row not in hand claims neither none nor all; Group and Total
    // rows take no mark. Asked again only when the page, the Window, the Row Kind or the marks moved.
    #pageMarked(): 'false' | 'true' | 'mixed' {
        const page = this.#pageRows();
        if (page === undefined) return 'false';
        const { window, windowStart } = this.#state;
        const memo = this.#pageMarkedMemo;
        const marks = this.#rowMarks.marks;
        if (memo !== undefined && memo.window === window && memo.windowStart === windowStart && memo.start === page.start
            && memo.count === page.count && memo.rowKind === this.#rowKind && memo.marks === marks && memo.stamp === this.#marksStamp) {
            return memo.checked;
        }
        const checked = this.#askPageMarked(page, window, windowStart);
        this.#pageMarkedMemo = { window, windowStart, start: page.start, count: page.count, rowKind: this.#rowKind, marks, stamp: this.#marksStamp, checked };
        return checked;
    }

    #askPageMarked(page: RowRange, window: readonly Row[], windowStart: number): 'false' | 'true' | 'mixed' {
        let marked = 0;
        let rows = 0;
        for (let row = page.start; row < page.start + page.count; row++) {
            const slice = row - windowStart;
            if (slice < 0 || slice >= window.length) return 'mixed';
            const data = itemAt(window, slice);
            if (this.#rowKind !== undefined && this.#rowKind(data) !== 'detail') continue;
            rows += 1;
            if (this.#rowMarks.isMarked(data)) marked += 1;
        }
        return marked === 0 ? 'false' : marked === rows ? 'true' : 'mixed';
    }

    // Whether any rectangle meets the painted rows and, on them, a pinned column or the painted slice of the others.
    // Before the first pass nothing is known to be out of view, and nothing is said.
    #selectionInView(ranges: readonly SelectionRange[]): boolean {
        const visible = this.#visible;
        if (visible === undefined) return true;
        const pinned = this.#styles.geometry.pinnedCount;
        const scrollable = this.#scrollable;
        return ranges.some(range => range.topRow < visible.start + visible.count && bottomRow(range) >= visible.start
            && (range.leftColumn < pinned
                || (scrollable !== undefined && range.leftColumn < scrollable.start + scrollable.count && rightColumn(range) >= scrollable.start)));
    }

    // "Mark all N rows" under a pager (ADR-0015, ADR-0043): the explicit step past the page, every row of the result
    // marked, judged against the order and source of its press as the header's is (MK-9).
    readonly #onMarkResultPress = (event: MouseEvent): void => {
        if (event.button === 0) this.#markResultPress = { sequence: this.#sequence(), source: this.#source };
    };

    readonly #onMarkResult = (): void => {
        const press = this.#markResultPress;
        this.#markResultPress = undefined;
        const marks = this.#rowMarks.marks;
        if (this.#destroyed || marks === undefined || !this.#commitEdit()) return;
        this.#takeKeyboard();
        if (!this.#markPressHolds(press)) return;
        this.#raiseMark(marks, { kind: 'allRows', marked: true, rowSequenceVersion: this.#state.rowSequenceVersion });
    };

    // "Select all N rows" under a pager (ADR-0015): what Ctrl+A does without one, the explicit way past the page.
    // An open edit is committed first, as a press on the corner commits it, and a Reject keeps the Selection.
    readonly #onSelectWholeResult = (): void => {
        if (this.#destroyed || !this.#commitEdit()) return;
        this.#reconcileSelection();
        const extent = this.#extent;
        if (extent.rowCount <= 0 || extent.columnCount <= 0) return;
        this.#apply(this.#held.selection.selectAll(extent));
        this.#takeKeyboard();
        this.#invalidate();
    };

    readonly #onPageBack = (): void => {
        this.#turnPageBy(-1);
    };

    readonly #onPageOn = (): void => {
        this.#turnPageBy(1);
    };

    // A page turned by the pager (ADR-0015): the same machinery as a scroll, the new page's slice resolved at its top
    // and the Range Request raised if the Window does not cover it. The keyboard comes back to the grid, so an open
    // edit is committed first: left standing, it would hold no keyboard on a page that does not show it, and a Reject
    // keeps the page.
    #turnPageBy(delta: number): void {
        if (this.#destroyed || this.#pageSize === undefined || !this.#commitEdit()) return;
        this.#turnPageTo(this.#pageIndex + delta);
        this.#takeKeyboard();
        this.#invalidate();
    }

    // Shows another page from its top, whether it turned. The offset is written with the rows the pass paints, and
    // the pointer's band and rest go with the rows that moved from under it.
    #turnPageTo(index: number): boolean {
        const size = this.#pageSize;
        if (size === undefined) return false;
        const target = Math.min(Math.max(index, 0), this.#pageCount - 1);
        if (target === this.#pageIndex) return false;
        this.#pageIndex = target;
        this.#scrollTopPx = 0;
        this.#scrollOwed = true;
        this.#hoverRow = undefined;
        this.#dropRest();
        this.#resolve();
        return true;
    }

    get #pageCount(): number {
        const size = this.#pageSize;
        return size === undefined ? 1 : Math.max(1, Math.ceil(this.#totalRows / size));
    }

    // The rows of the page shown, as positions in the whole result: none while no pager stands or the page is empty.
    #pageRows(): RowRange | undefined {
        const size = this.#pageSize;
        if (size === undefined) return undefined;
        const first = this.#pageIndex * size;
        const count = Math.min(size, this.#totalRows - first);
        return count > 0 ? rowRange(first, count) : undefined;
    }

    // What Ctrl+A and the corner select under a pager (ADR-0015): the page, which reads as the world; with none, every
    // row.
    #selectAllContext(extent: GridExtent): { readonly firstRow: number; readonly rowCount: number } | undefined {
        const size = this.#pageSize;
        if (size === undefined) return undefined;
        // The page the next pass shows: a result that shrank since the last keeps its last page.
        const index = Math.min(this.#pageIndex, Math.max(0, Math.ceil(extent.rowCount / size) - 1));
        const firstRow = Math.min(index * size, extent.rowCount - 1);
        return { firstRow, rowCount: Math.min(extent.rowCount - firstRow, size) };
    }

    // Go to selection: the Focus is revealed, on its page under a pager, and the keyboard is the grid's again
    // (ADR-0014, ADR-0015).
    readonly #onGoToSelection = (): void => {
        const focus = this.#held.selection.focus;
        if (this.#destroyed || focus === undefined) return;
        this.#revealOwed = { target: focus, extending: undefined, rowStart: false, stepRows: 0 };
        this.#revealHeaderOwed = undefined;
        this.#takeKeyboard();
        this.#invalidate();
    };

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
        // A hidden header is no band at all (ADR-0050, item 1): taken out of the flow, and built again when it
        // comes back.
        if (this.#hideHeader) {
            this.#header.remove();
            this.#paintedHeader = undefined;
            return;
        }
        if (this.#header.parentNode !== this.#spacer) this.#spacer.prepend(this.#header);
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
            && painted.tierPx === tierPx && painted.grips === grips && painted.menus === menus && painted.headingStyle === this.#rowHeadingStyle
            && painted.labels === this.#commandLabel) {
            this.#paintHeadingMarks(false);
            return;
        }
        this.#paintedHeader = {
            styles: this.#styles, columns: this.#columns, scrollable: this.#scrollable, sorts, layout, tierPx, grips, menus,
            headingStyle: this.#rowHeadingStyle, labels: this.#commandLabel,
        };
        // Where the two Headings meet (ADR-0050): held at the left edge as the Row Headings are, and a click there
        // selects all. Paint only: the header's own click reads the offsets.
        const headingStyle = this.#rowHeadingStyle;
        // One element for the grid's life, as the header's checkbox is (ADR-0043): a band rebuilt by a scroll takes
        // the corner it had back, and the Heading Mark's class on it is written to the corner that stands.
        this.#corner = headingStyle === undefined ? undefined : this.#cornerBox;
        const corner = headingStyle === undefined ? [] : [this.#cornerBox];
        if (headingStyle !== undefined) this.#cornerBox.setAttribute('style', headingStyle);
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
                button.setAttribute('aria-label', this.#labelOf(gridLabelIds.columnMenu));
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
                this.#groupToggles = new Map();
                const rows = Array.from({ length: layout.tierCount }, (_, headerRow) => this.#tierRow(headerRow, groups));
                const leaves = doc.createElement('div');
                leaves.setAttribute('role', 'row');
                leaves.setAttribute('aria-rowindex', String(layout.tierCount + 1));
                leaves.setAttribute('style', 'display: contents');
                tiers = { styles: this.#styles, columns: this.#columns, layout, tierPx, rows, leaves, groups, labels: this.#commandLabel };
                this.#paintedTiers = tiers;
                // Only where the tiers themselves are new: a horizontal scroll paints the leaves alone, and
                // detaching the tier rows every step would move the band's whole structure (PF-3). The corner comes
                // and goes with the lead width, which is a new ColumnStyles, so the tiers are new with it.
                header.replaceChildren(...corner, ...rows, leaves);
            } else if (tiers.labels !== this.#commandLabel) {
                // The toggles' names are worded through commandLabel, which can change while the tiers stand: they are
                // worded again in place, and the tiers stay (ADR-0400 section 8).
                for (const [toggle, id] of this.#groupToggles) {
                    const shown = defined(layout.groups.find(candidate => candidate.group.id === id), `the Header Group '${id}' a toggle names`);
                    toggle.setAttribute('aria-label', this.#toggleLabel(shown.group));
                }
                tiers = { ...tiers, labels: this.#commandLabel };
                this.#paintedTiers = tiers;
            }
            tiers.leaves.replaceChildren(...cells);
        } else {
            this.#paintedTiers = undefined;
            this.#groupToggles = new Map();
            header.replaceChildren(...corner, ...cells);
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

    // A Header Group's toggle's name, "Expand {label}" or "Collapse {label}" as the word tables say it (ADR-0400 section 8).
    #toggleLabel(group: HeaderGroup): string {
        const id = group.expanded === true ? gridLabelIds.headerGroupCollapse : gridLabelIds.headerGroupExpand;
        return filledWords(this.#labelOf(id), { label: group.label });
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
        const { id, expanded, hiddenMark } = group.group;
        if (id !== undefined && expanded !== undefined) {
            // The toggle (ADR-0400 sections 3, 7 and 8): a button of the grid's own at the rectangle's left, pressed and
            // never reached by Tab, named from the word tables; its look is the stylesheet's, by aria-expanded.
            const doc = this.#root.ownerDocument;
            const toggle = doc.createElement('button');
            toggle.type = 'button';
            toggle.className = 'ex-header-group-toggle';
            toggle.tabIndex = -1;
            toggle.setAttribute('aria-expanded', String(expanded));
            toggle.setAttribute('aria-label', this.#toggleLabel(group.group));
            toggle.setAttribute('data-ex-claims', 'mousedown click');
            element.prepend(toggle);
            this.#groupToggles.set(toggle, id);
            // The group's own name is its label: named from its contents it would take the toggle's words in too,
            // "Collapse Leg 1 Leg 1" (ADR-0400 section 8).
            element.setAttribute('aria-label', group.label);
        }
        if (hiddenMark !== undefined) {
            // The Hidden Mark (ADR-0400 section 6): its state's look, and its reason as the group's description.
            const mark = this.#root.ownerDocument.createElement('span');
            mark.className = `ex-hidden-mark ex-hidden-mark-${hiddenMark.state}`;
            mark.title = hiddenMark.reason;
            mark.setAttribute('aria-hidden', 'true');
            element.append(mark);
            element.setAttribute('aria-description', hiddenMark.reason);
        }
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
        // The corner is marked while every cell is selected, as Excel marks it (ADR-0008, 2026-10-13).
        if (this.#corner !== undefined) {
            const [run] = marks.whole;
            const everyCell = marks.whole.length === 1 && run?.start === 0 && run.count === extent.columnCount;
            const className = everyCell ? 'ex-headings-corner ex-headings-corner-marked' : 'ex-headings-corner';
            if (this.#corner.className !== className) this.#corner.className = className;
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
        // A hidden header moves where the rows begin too (ADR-0050, item 1).
        this.#write(viewport, 'viewport', 'style', this.#headerLayout.tierCount === 0 && !this.#hideHeader
            ? transform : `top: ${this.#bandHeightPx}px; ${transform}`);
        this.#write(viewport, 'viewport', 'data-ex-first-row', visible === undefined ? undefined : String(visible.start));
        this.#write(viewport, 'viewport', 'data-ex-sequence', String(this.#handedVersion()));
        this.#write(viewport, 'viewport', 'data-ex-layout', String(this.#layoutId));

        const layers = this.#paintLayers();
        // The Keyboard Field first, then the selection layers, then the Cell Editor, then the rows, as the C#
        // core's markup stands them (ADR-0080, ADR-0010).
        const field = this.#canEdit ? [this.#paintKeyField()] : [];
        const editor = this.#paintEditor();
        // The Row Headings' Heading Mark stands above them and below the editor, as the C# core stands it.
        const rowMarks = this.#paintRowMarks();
        const rows: HTMLElement[] = [...field, ...layers, ...rowMarks, ...editor];
        const next = new Map<unknown, PaintedRow<Row>>();
        const nextPlaceholders = new Map<number, HTMLElement>();
        const nextStates = new Map<number, HTMLElement>();
        let repainted = false;
        // The earliest end among the Change Highlights this pass leaves painted, a row painted under a key met twice
        // included (ADR-0068).
        let earliestHighlight: number | undefined;
        const doc = viewport.ownerDocument;
        // A control of a Template cell's content holding the keyboard: a row painted again moves its slot,
        // which takes DOM focus off it, so it is given back once the rows stand (ADR-0020).
        const active = doc.activeElement;
        const edit = this.#edit;
        const slotHeld = active instanceof HTMLElement && active !== this.#keyField && (edit === undefined || !this.#inEditor(edit, active))
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
            // Every row of one pass paints its Change Highlights by one time, read only while they are declared
            // (ADR-0068): a row whose earliest mark has ended by it paints again, and no other row does.
            const now = paint.cellChangedAt === undefined ? undefined : this.#reports.clock.now();
            const ended = (end: number | undefined): void => {
                if (end !== undefined && (earliestHighlight === undefined || end < earliestHighlight)) earliestHighlight = end;
            };
            if (lookup !== undefined) {
                const columns = this.#columns;
                this.#appearances.begin((row, c) => lookup(row, itemAt(columns, c).column), lookup, paint.edgeBorder, columns,
                    columns.length, this.#styles.geometry.pinnedCount, scrollableNow);
            }
            // A hidden marker after every painted row while the Row Headings stand, which the stylesheet paints that
            // row's heading from (ADR-0008, 2026-10-13): no row learns the Selection, and the markers' count never
            // moves with its size (ADR-0003).
            const runs = this.#rowHeadingMarks();
            const headingState = (index: number): void => {
                if (runs === undefined) return;
                let marker = this.#headingStates.get(index);
                if (marker === undefined) marker = this.#headingStateElement();
                const className = runsHold(runs.whole, index) ? 'ex-heading-state ex-heading-marked ex-heading-whole'
                    : runsHold(runs.rows, index) ? 'ex-heading-state ex-heading-marked' : 'ex-heading-state';
                if (marker.className !== className) marker.className = className;
                nextStates.set(index, marker);
                rows.push(marker);
            };
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
                    headingState(index);
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
                const stripe = this.#stripeAt(index);
                const headingStyle = this.#rowHeadingStyle;
                const heading = headingStyle === undefined ? undefined : this.#headingAt(index);
                if (was !== undefined && !next.has(key) && was.row === row && was.index === index && was.styles === this.#styles
                    && was.rowKind === this.#rowKind && was.stripe === stripe && was.heading === heading && was.headingStyle === headingStyle
                    && was.columns === this.#columns && was.placeholder === this.#placeholderMode && sameRange(was.scrollable, scrollable)
                    && paint.paintsRowsAs(was.paint) && was.appearance === appearance && was.chosen === chosen && was.marked === marked
                    && (was.highlightEnd === undefined || (now !== undefined && now < was.highlightEnd))) {
                    if (was.marks === marks && was.current === current) {
                        ended(was.highlightEnd);
                        next.set(key, was);
                        for (const slot of was.slots) used.add(slot);
                        rows.push(was.element);
                        headingState(index);
                        continue;
                    }
                    // A new search judges the row's cells again, and paints the row again only where its marks
                    // moved (ADR-0332 §4, ADR-0003).
                    if (this.#matchedIn(row, scrollable, marks, current) === was.matched) {
                        ended(was.highlightEnd);
                        next.set(key, { ...was, marks, current });
                        for (const slot of was.slots) used.add(slot);
                        rows.push(was.element);
                        headingState(index);
                        continue;
                    }
                }
                const kind = this.#rowKind === undefined ? 'detail' : this.#rowKind(row);
                const rowPaint: RowPaint<Row> = { key, chosen: chosenHere, marked, used, slots: [], appearance, kind, heading, now, highlightEnd: undefined };
                const { element, matched } = this.#rowElement(row, index, scrollable, marks, current, rowPaint, stripe);
                ended(rowPaint.highlightEnd);
                repainted = true;
                // A key met twice in one Window is painted, never paired: which of the two a later row
                // continues cannot be told (ADR-0140).
                if (!next.has(key)) {
                    next.set(key, {
                        element, row, index, styles: this.#styles, columns: this.#columns, placeholder: this.#placeholderMode, scrollable,
                        marks, current, matched, paint, appearance, chosen, slots: rowPaint.slots, marked, rowKind: this.#rowKind, stripe,
                        heading, headingStyle, highlightEnd: rowPaint.highlightEnd,
                    });
                }
                rows.push(element);
                headingState(index);
            }
        }
        this.#painted = next;
        this.#armHighlightTimer(earliestHighlight);
        this.#placeholders = nextPlaceholders;
        this.#headingStates = nextStates;
        if (this.#cellPaint.cellAppearance !== undefined) this.#appearances.end();
        this.#paintAppearanceStyle();
        // In order, moving only what is out of place, so a row that stays is not touched (ADR-0003). A
        // selection layer coming or going paints no cell.
        const isLayer = (node: ChildNode): boolean =>
            node === this.#shadeLayer || node === this.#pinnedLayer || node === this.#selectionLayer || node === this.#keyFieldLayer
            || node === this.#rowMarkLayer || node === this.#editorPinned || node === this.#edit?.element || node === this.#referenceText?.cell.element
            || this.#headingStateElements.has(node);
        // The shade layer gone is taken out first, or every row after it would be moved past it.
        if (!layers.includes(this.#shadeLayer)) this.#shadeLayer.remove();
        // The Heading Mark's layer likewise: a Selection cleared would otherwise move every row past it.
        if (rowMarks.length === 0) this.#rowMarkLayer.remove();
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
        const editing = this.#edit;
        const editorHeld = editing !== undefined && doc.activeElement instanceof HTMLElement && this.#inEditor(editing, doc.activeElement)
            ? doc.activeElement : undefined;
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
        const { target, field } = keyboardTargetIn(control, 'takeKeyboard');
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
    #editorLook(edit: OpenEdit<Row>): string | undefined {
        const declared = this.#cellPaint.editorAppearance ?? this.#cellPaint.cellAppearance;
        if (declared === undefined) return undefined;
        const { cell } = edit;
        // Away, the rows shown are another set's: the look is the edit's own row's (ADR-0331 §5).
        const row = this.#away(edit) ? edit.awayRow ?? edit.openedOn : this.#rowAt(cell.row)?.row ?? edit.openedOn;
        return this.#appearances.styles.editorClassFor(checkedAppearance(declared(row, itemAt(this.#columns, cell.column).column)));
    }

    // The Cell Editor (ADR-0010): one input floated over its cell, never inside a row, which would break the
    // rows' skipping (ADR-0003). Over a Pinned Column it rides a sticky anchor as the pinned layer does. A
    // row it followed out of view leaves it open and unseen on the first painted row (ADR-0011).
    #paintEditor(): readonly HTMLElement[] {
        const edit = this.#edit;
        if (edit === undefined) return [];
        const { element } = edit;
        // The coloured text stands under the core's own field only: a Chrome's field is drawn in a look the layer is not
        // measured against (ADR-0057, ADR-0390).
        const layers = this.#referenceTextLayers();
        const layer = edit.slot === undefined ? layers?.cell.element : undefined;
        const visible = this.#visible;
        if (visible !== undefined) {
            // Away, it takes its column's box on the first painted row, where it holds the keyboard but is neither
            // seen nor pressed (ADR-0011's note of 2026-10-07).
            const away = this.#editorAway(edit.cell, visible);
            const at = away ? cellPosition(visible.start, edit.cell.column) : edit.cell;
            const box = defined(cellBox(at, this.#styles.geometry, this.#metrics.rowHeightPx, visible), 'the box of a cell among the painted rows');
            const look = this.#editorLook(edit);
            // The coloured text's mark is the gate's (#gateReferenceTexts), and is kept here.
            const shown = element.classList.contains('ex-reference-text-shown') ? ' ex-reference-text-shown' : '';
            const base = away ? 'ex-editor ex-editor-away' : 'ex-editor';
            setAttribute(element, 'class', `${look === undefined ? base : `${base} ${look}`}${shown}`);
            setAttribute(element, 'style', box.style);
            setAttribute(element, 'data-ex-cell', `${at.row} ${at.column}`);
            if (layer !== undefined) {
                // The coloured text takes the editor's box and look, so its characters stand where the field's do.
                const under = away ? 'ex-reference-text ex-reference-text-cell ex-editor-away' : 'ex-reference-text ex-reference-text-cell';
                setAttribute(layer, 'class', look === undefined ? under : `${under} ${look}`);
                setAttribute(layer, 'style', box.style);
            }
        }
        // A Reject or a refusal is said at the editor's field, which the message describes (ADR-0034).
        const field = this.#editorField(edit);
        if (field !== undefined) {
            setAttribute(field, 'aria-invalid', edit.error === undefined ? undefined : 'true');
            setAttribute(field, 'aria-describedby', edit.error === undefined ? undefined : this.#message.id);
        }
        edit.slot?.review();
        if (edit.cell.column < this.#styles.geometry.pinnedCount) {
            if (element.parentNode !== this.#editorPinned) this.#editorPinned.append(element);
            if (layer !== undefined && layer.nextSibling !== element) this.#editorPinned.insertBefore(layer, element);
            return [this.#editorPinned];
        }
        return layer === undefined ? [element] : [layer, element];
    }

    // Whether the editor, or a message, is away from the rows painted (ADR-0011), or under a pager from the page: the
    // row just before a page is on the page before, and never scrolls into this one (ADR-0015).
    #editorAway(at: CellPosition, painted: RowRange): boolean {
        // Away among another set's rows, its cell is none of the rows painted (ADR-0331 §5).
        const edit = this.#edit;
        if (edit !== undefined && this.#away(edit)) return true;
        const page = this.#pageRows();
        return editorAway(at, painted) || (page !== undefined && (at.row < page.start || at.row >= page.start + page.count));
    }

    // The message an editor is held with, or the one a flagged cell says while no edit is open (ADR-0034): below
    // its cell, never over the value it complains about, and only while that cell is painted.
    #paintMessage(): void {
        const edit = this.#edit;
        const geometry = this.#geometry;
        const visible = this.#visible;
        // A message names the row and the column it was asked about: rows or columns moved under it take it away, as
        // the Focus moving does, rather than leave it under a cell it does not describe.
        const shown = this.#shownMessage;
        if (shown !== undefined && (this.#rowAt(shown.cell.row)?.row !== shown.row || this.#columns[shown.cell.column]?.column.name !== shown.column)) {
            this.#shownMessage = undefined;
        }
        const said = edit === undefined ? this.#shownMessage : edit.error === undefined ? undefined : { cell: edit.cell, text: edit.error };
        if (said === undefined || geometry === undefined || visible === undefined || this.#editorAway(said.cell, visible)) {
            this.#messageSlot.hide();
            this.#message.remove();
            return;
        }
        const width = Math.min(messageMaxWidthPx, this.#visibleWidthPx);
        const left = Math.min(Math.max(0, this.#cellLeftPx(said.cell.column)), Math.max(0, this.#visibleWidthPx - width));
        const top = this.#bandHeightPx + geometry.viewportTopPxOf(said.cell.row, this.#scrollTopPx) + this.#metrics.rowHeightPx;
        setAttribute(this.#message, 'style', this.#underBar(`left: ${left}px; top: ${top}px; max-width: ${width}px`));
        this.#sayMessage(said.cell, said.text, edit !== undefined);
        if (this.#message.parentNode !== this.#root) this.#scroller.after(this.#message);
    }

    // Writes the message's text, or has the Chrome's cellMessage slot draw it (ADR-0390). The slot's close takes away
    // the flagged cell's message it was mounted for, and nothing after another has taken its place.
    #sayMessage(cell: CellPosition, text: string, editorError: boolean): void {
        const mount = this.#chrome?.cellMessage;
        if (mount === undefined) {
            // The slot's element goes with its contents, whatever text it was left showing.
            const slotted = this.#message.firstChild === this.#messageSlot.element;
            this.#messageSlot.hide();
            if (slotted || this.#message.textContent !== text) this.#message.textContent = text;
            return;
        }
        const column = itemAt(this.#columns, cell.column).column.name;
        const key = JSON.stringify([cell.row, column, text, editorError]);
        this.#messageSlot.show(mount, key, standing => Object.freeze({
            column, message: text, editorError,
            close: () => {
                if (this.#destroyed || !standing() || editorError) return;
                if (this.#closeMessage()) this.#invalidate();
            },
        }));
        if (this.#message.firstChild !== this.#messageSlot.element || this.#message.childNodes.length !== 1) {
            this.#message.replaceChildren(this.#messageSlot.element);
        }
    }

    // The editor takes the keyboard once it is painted, while the keyboard is this grid's: on opening, with
    // the caret placed at the end of its text by the core, never left where the browser puts it (ADR-0010).
    // The request waits while the Keyboard Field composes, or a key the IME took there is still being told
    // (ADR-0080); `toward` is where the keyboard is going when the field is being left.
    #focusEditor(toward?: Element): void {
        if (!this.#editorFocusOwed || this.#composing || this.#imeKeyTold) return;
        const edit = this.#edit;
        // A Chrome's field not yet drawn: its contents hand it over once drawn, and the request waits for them.
        if (edit !== undefined && edit.surface === 'cell' && edit.slot !== undefined && edit.slot.field() === undefined) return;
        this.#editorFocusOwed = false;
        if (edit === undefined || !edit.element.isConnected || !this.#keyboardHere(toward)) return;
        // An edit typed in the Formula Bar keeps the keyboard there, its caret where the user put it (ADR-0051).
        const bar = edit.surface === 'bar' ? this.#bar.text : undefined;
        if (bar?.isConnected === true) {
            if (this.#root.ownerDocument.activeElement !== bar) bar.focus({ preventScroll: true });
            return;
        }
        const element = this.#editorField(edit);
        if (element === undefined || this.#root.ownerDocument.activeElement === element) return;
        element.focus({ preventScroll: true });
        element.setSelectionRange(element.value.length, element.value.length);
        showCaret(element, element.value.length);
    }

    // Whether `target` is the editor's: its own field, or anything in the box a Chrome draws it in.
    #inEditor(edit: OpenEdit<Row>, target: EventTarget | null): boolean {
        return target instanceof Node && edit.element.contains(target);
    }

    // The field an edit's text is typed in: the core's own, or the one a Chrome drew, once it has.
    #editorField(edit: OpenEdit<Row>): EditorField | undefined {
        return edit.slot === undefined ? builtInEditor(edit.element) : edit.slot.field();
    }

    // The edit's uncommitted text (ADR-0010).
    #editorText(edit: OpenEdit<Row>): string {
        return edit.slot === undefined ? builtInEditor(edit.element).value : edit.slot.text;
    }

    // The text a commit writes: the field's, which a Chrome must have drawn (ADR-0390).
    #committedText(edit: OpenEdit<Row>): string {
        return edit.slot === undefined ? builtInEditor(edit.element).value : edit.slot.committed();
    }

    // Writes the edit's uncommitted text, as typing in the Formula Bar does (ADR-0051).
    #writeEditorText(edit: OpenEdit<Row>, text: string): void {
        if (edit.slot === undefined) builtInEditor(edit.element).value = text;
        else edit.slot.text = text;
    }

    // Whether the row at this position in the whole result wears a stripe: the second, the fourth, …, counted in the
    // result and never on the screen, so a stripe stays with its row however far the Viewport scrolls (ADR-0038).
    #stripeAt(index: number): boolean {
        return this.#stripeRows && (index & 1) === 1;
    }

    // A position with no row behind it at all: not even a Pinned Column has anything to paint, and
    // aria-busy, because a value that has not arrived must not read as one (ADR-0004, ADR-0033).
    #placeholderRow(index: number): HTMLElement {
        const element = this.#root.ownerDocument.createElement('div');
        element.className = rowClasses({ kind: 'detail', placeholder: true, stripe: this.#stripeAt(index) });
        element.setAttribute('role', 'row');
        element.setAttribute('aria-rowindex', String(this.#headerRowCount + index + 1));
        element.setAttribute('aria-busy', 'true');
        // A Row Heading is a position's label, so a row that has not arrived still has one (ADR-0050).
        const style = this.#rowHeadingStyle;
        if (style !== undefined) element.append(this.#rowHeadingElement(this.#headingAt(index), style));
        return element;
    }

    // A painted position's Row Heading label (ADR-0050, item 1), refused where the Consumer answers no text: a
    // heading that read "undefined" would be the band lying about its row.
    #headingAt(index: number): string {
        const label = this.#rowHeadings;
        if (label === undefined) throw new Error('Internal: a Row Heading was asked for with no band.');
        const text: unknown = label(index);
        if (typeof text !== 'string') throw new TypeError(`rowHeadings answers a row's label as text, not ${String(text)} (ADR-0050).`);
        return text;
    }

    // The Row Heading (ADR-0050): first in its row's flow and held at the left edge by the stylesheet. Paint only,
    // and hidden from assistive technology: the row's aria-rowindex already says where it is.
    #rowHeadingElement(label: string, style: string): HTMLElement {
        const heading = this.#root.ownerDocument.createElement('div');
        heading.className = 'ex-row-heading';
        heading.setAttribute('style', style);
        heading.setAttribute('aria-hidden', 'true');
        heading.textContent = label;
        return heading;
    }

    #headingStateElement(): HTMLElement {
        const marker = this.#root.ownerDocument.createElement('div');
        marker.setAttribute('aria-hidden', 'true');
        this.#headingStateElements.add(marker);
        return marker;
    }

    // The rows the Heading Mark marks, made once per Selection and extent, or none while no Row Headings stand
    // (ADR-0008, 2026-10-13).
    #rowHeadingMarks(): RowHeadingMarks | undefined {
        if (this.#rowHeadingStyle === undefined) return undefined;
        const selection = this.#held.selection;
        const extent = this.#extent;
        let marks = this.#rowMarkRuns;
        if (marks === undefined || marks.selection !== selection
            || marks.extent.rowCount !== extent.rowCount || marks.extent.columnCount !== extent.columnCount) {
            marks = { selection, extent, rows: markedRows(selection), whole: wholeRowRuns(selection, extent) };
            this.#rowMarkRuns = marks;
        }
        return marks;
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
        current: number, paint: RowPaint<Row>, stripe: boolean): { readonly element: HTMLElement; readonly matched: string } {
        const element = this.#root.ownerDocument.createElement('div');
        // A Placeholder keeps its Kind: a group row that blinked into a detail row mid-fling would be the landmark
        // lying about itself (ADR-0024, ADR-0004).
        element.className = rowClasses({ kind: paint.kind, placeholder: this.#placeholderMode, stripe });
        element.setAttribute('role', 'row');
        element.setAttribute('aria-rowindex', String(this.#headerRowCount + index + 1));
        if (this.#placeholderMode) element.setAttribute('aria-busy', 'true');
        if (paint.heading !== undefined && this.#rowHeadingStyle !== undefined) {
            element.append(this.#rowHeadingElement(paint.heading, this.#rowHeadingStyle));
        }
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
    // An Action or Mark cell carries no value and is never marked. The reference rule skips a column that is not
    // queryable, as `GridSource.from` finds in none; a Consumer's rule is asked about every other cell, since the
    // Consumer's own Find says where a step lands (ExPivot's report columns are not queryable, and are searched).
    #markOf(row: Row, c: number, marks: MatchMarks<Row> | undefined, current: number, shown?: string): MatchMark {
        const { column, actions, mark } = itemAt(this.#columns, c);
        if (marks === undefined || actions.length > 0 || mark || (marks.rule === undefined && column.queryable === false)
            || !marks.matches(row, column, shown)) return 'none';
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
        const value = column.value(row);
        const text = textFor(column, value);
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
        const state = cellPaint.cellState === undefined ? 'normal' : cellPaint.cellState(row, column);
        // A null value means nothing, and its rule is not asked (ADR-0006).
        const tone = column.tone === undefined || value === null || value === undefined ? 'none' : toneOf(column, value);
        const changed = this.#changed(row, column, paint);
        const base = cellClasses({ numeric, pinned, state, align, tone, changed, mark });
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

    // Whether a value cell wears the Change Highlight at the pass's time (ADR-0068): one question to the Consumer per
    // painted value cell, and none without the declaration. A mark it shows joins the row's earliest end. Keyed by the
    // row and the column painted, never by the element, so a row painted afresh asks again for what it now paints.
    #changed(row: Row, column: GridColumn<Row>, paint: RowPaint<Row>): boolean {
        const { cellChangedAt, changeHighlightDuration } = this.#cellPaint;
        if (cellChangedAt === undefined) return false;
        const end = changeHighlightShowingUntil(cellChangedAt(row, column.name), changeHighlightDuration, defined(paint.now, 'the pass\'s time'));
        if (end === undefined) return false;
        if (paint.highlightEnd === undefined || end < paint.highlightEnd) paint.highlightEnd = end;
        return true;
    }

    // Arms the one timer for the earliest end among the Change Highlights painted now (ADR-0068), after every pass
    // that can paint or drop a mark. With no mark painted, no timer stands. When it fires, the rows are painted
    // again, and only those whose marks ended by then are rebuilt.
    #armHighlightTimer(earliest: number | undefined): void {
        if (earliest === undefined || this.#destroyed) {
            this.#stopHighlightTimer();
            return;
        }
        if (this.#highlightTimer !== undefined && this.#highlightTimer.end === earliest) return;
        this.#stopHighlightTimer();
        const end = earliest;
        const { clock } = this.#reports;
        // An end already passed is waited for by a millisecond rather than none, so the timer never fires inside the
        // pass that armed it; a far one is waited for a day at a time, as a timer's delay is bounded.
        const wait = Math.min(Math.max(end - clock.now(), shortestHighlightWaitMs), longestHighlightWaitMs);
        const cancel = clock.setTimer(() => {
            this.#highlightTimer = undefined;
            if (this.#destroyed) return;
            // A timer can fire a moment before the clock reaches its end, and a wait cut short fires long before:
            // neither ends a mark, and the timer is armed again for what remains.
            if (clock.now() < end) {
                this.#armHighlightTimer(end);
                return;
            }
            this.#invalidate();
        }, wait);
        this.#highlightTimer = { cancel, end };
    }

    #stopHighlightTimer(): void {
        if (this.#highlightTimer === undefined) return;
        this.#highlightTimer.cancel();
        this.#highlightTimer = undefined;
    }

    // An Action or Template cell (ADR-0020): the grid's own buttons, or the Consumer's content in a slot kept
    // across the row's rebuilds. It paints no text, so nothing of it is hashed or named by a value.
    #contentCell(row: Row, index: number, c: number, pinned: boolean, mark: MatchMark, paint: RowPaint<Row>): HTMLElement {
        const resolved = itemAt(this.#columns, c);
        const { column, numeric, actions, template } = resolved;
        const mounted = template === undefined ? undefined : this.#templateSlot(row, resolved, template, paint);
        const cell = mounted === undefined ? this.#root.ownerDocument.createElement('div') : mounted.slot;
        const state = this.#cellPaint.cellState === undefined ? 'normal' : this.#cellPaint.cellState(row, column);
        cell.className = cellClasses({ numeric, pinned, state, align: column.align ?? 'auto', mark }); // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0006: a column that declares no alignment aligns by its type
        cell.setAttribute('style', pinned ? this.#styles.pinnedCell(c) : this.#styles.cell(c));
        cell.setAttribute('role', 'gridcell');
        cell.setAttribute('id', `${this.#ids}r${index}c${c}`);
        cell.setAttribute('aria-colindex', String(c + 1));
        actions.forEach((action, a) => {
            cell.append(this.#actionButton(resolved, action, paint.chosen?.column === c && paint.chosen.action === a ? `${this.#ids}r${index}c${c}a${a}` : undefined));
        });
        // A group or total row is no target of a mark, so it has no checkbox to press (ADR-0043, ADR-0024).
        if (resolved.mark && paint.kind === 'detail') {
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
// What takes the keyboard for an element a Consumer names, as the C# core's script finds it (boxTarget): the
// element itself when it is a control; else its first enabled text field, one with a caret, not read-only and
// not a design system's combobox; else its first enabled control, a read-only input such as that combobox among
// them, and no other input that is not a text field, nor an element taken out of the tab order.
const ownControl = 'input, textarea, select, button, a[href], [tabindex]';
const textFields = 'input:not([disabled]):not([readonly]):not([role="combobox"]):is(:not([type]), [type="text"], [type="search"], '
    + '[type="url"], [type="tel"], [type="password"]), textarea:not([disabled]):not([readonly]):not([role="combobox"])';
const otherControls = 'input[readonly]:not([disabled]):not([type="hidden"]), button:not([disabled]), select:not([disabled]), a[href], '
    + '[tabindex]:not([tabindex="-1"])';

/** The element inside `control` that takes the keyboard for it, by the rule above: a text field found inside it
 * has its text selected. One that holds none is refused, naming `asker`. */
function keyboardTargetIn(control: HTMLElement, asker: string): { readonly target: HTMLElement; readonly field: HTMLInputElement | HTMLTextAreaElement | undefined } {
    if (control.matches(ownControl)) return { target: control, field: undefined };
    const field = control.querySelector<HTMLInputElement | HTMLTextAreaElement>(textFields);
    if (field !== null) return { target: field, field };
    const target = control.querySelector<HTMLElement>(otherControls);
    if (target === null) throw new RangeError(`${asker} is handed an element that takes no keyboard and holds no enabled control that does.`);
    return { target, field: undefined };
}

/** Whether a popup the popover's contents opened of their own is open: Escape is then the popup's (ADR-0039). */
function innerPopupOpen(popover: OpenPopover): boolean {
    if (popover.kind === 'consumer') return popover.frame.innerPopupOpen;
    if (popover.kind === 'find') return popover.panel.innerPopupOpen;
    return popover.items.innerPopupOpen || (popover.kind === 'column' && popover.panel?.innerPopupOpen === true);
}

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

// The core's own editor, the field its edit's box is (ADR-0010).
function builtInEditor(element: HTMLElement): HTMLInputElement {
    if (!(element instanceof HTMLInputElement)) throw new Error('Internal: the built-in Cell Editor is an input.');
    return element;
}
