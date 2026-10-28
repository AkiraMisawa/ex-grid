/**
 * ExSheet's Script Core (ADR-0370, section 1): a general-purpose Excel-like sheet drawn by ExGrid as that grid's
 * Consumer (ADR-0046), over `@ex-grid/sheet-engine`, free of any UI framework — `createSheet`, and what the Sheet
 * decides beside its engine: the words it shows, by id, in English and Japanese; Format Cells' offer and draft; the
 * Name Box; Excel's keys; where Ctrl+arrow stops; the Selection Summary's cells; what a change widens; and Pointing
 * at a Linked Table's grid. A Framework Adapter mounts it, as `@ex-grid/sheet-react` does.
 *
 * @packageDocumentation
 */
export {
    createSheet, type FocusFormatReading, type SheetChrome, type SheetHandle, type SheetOptions, type SheetsChange, type SheetToolbarButton, type SheetToolbarEntry, type SheetToolbarRowOptions,
} from './create-sheet';
export type {
    SheetFormatCellsSlot, SheetFormatCellsSlotContext, SheetTabAddSlot, SheetTabSlot, SheetTabSlotContext,
} from './sheet-chrome-slots';
export { FormatCellsDraft } from './format-cells-draft';
export { formatCellsLineStroke } from './format-cells-panel';
export {
    formatCellsOffer, isDisabledCategory,
    type BorderEdge, type BorderPreset, type ColourTarget, type DisabledNumberFormatCategory, type FontStyle, type FormatCellsTab,
    type NumberFormatCategory, type NumberFormatCategoryOffer, type PaletteSwatch, type StandardColourName, type SwatchStep,
    type ThemeColourName,
} from './format-cells-offer';
export { readNameBoxPlace, readNameBoxRange, type NameBoxPlace } from './name-box-text';
export {
    currencyCode, currencyNegativeStyles, customTypes, customTypesIn, dateTypes, defaultPlaces, mostDecimalPlaces, numberCode,
    numberNegativeStyles, percentageCode, recogniseNumberFormat, scientificCode, timeTypes, type RecognisedNumberFormat,
} from './number-format-codes';
export type { PointingRefusal, PointingRefusalReason } from './pointing-refusal';
export { SheetEdges } from './sheet-edges';
export type {
    SheetToolbarContent, SheetToolbarIcon, SheetToolbarItemChoice, SheetToolbarItemKind, SheetToolbarItemSlot, SheetToolbarItemSlotContext, SheetToolbarItemView,
} from './sheet-toolbar-slot';
export { sheetToolbarPictures } from './sheet-toolbar';
export { toolbarChoiceMovedTo, toolbarChoiceRows, toolbarChoiceStart, toolbarSwatchesAcross, type ToolbarChoiceRow } from './toolbar-choice-keys';
export { sheetFormatChangeFor, sheetFormatKeyOf, sheetFormatKeysDeclared, type SheetFormatKey } from './sheet-format-keys';
export { sheetCommandIds } from './sheet-context-commands';
export { sheetFindLabelIds, type SheetFindWithin } from './sheet-find-panel';
export { sheetStructureEditFor, sheetStructureKeyOf, sheetStructureKeysDeclared, type SheetStructureKey } from './sheet-structure-keys';
export { sheetSummaryCells, sheetSummaryDeclined, type SheetSummaryCells } from './sheet-summary';
export { sheetTabKeys, sheetTabKeysDeclared, sheetTabStepOf } from './sheet-tab-keys';
export { widensAsAHomeTabButtonDoes, widensAsAKeyDoes, widensAsFormatCellsOkDoes } from './sheet-widening';
export {
    sheetWordIdList, sheetWords, SheetWords,
    type FormatCellsLabel, type FormatCellsRefusal, type PointingRefusalWordCase, type SheetContextCommand, type SheetFindWithinWord, type SheetLabel, type SheetTabCommand, type SheetToolbarBorder, type SheetToolbarCommand, type SheetWordId, type SheetWordsChoice,
} from './sheet-words';
export {
    GridPointedAt, PointingScope, type GridPointArrow, type GridPointDashes, type GridPointedPress, type GridPointedPressKind,
    type GridPointedStep, type GridPointedStepKind, type LinkedColumnColour, type OutlinedColumn, type PointedAtGrid, type PointingSheet,
    type PointState, type ReferenceColour, type TableRow,
} from './pointing-scope';
