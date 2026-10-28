// The sentences and names ExSheet shows, in one place (ADR-0035: ExGrid writes no sentences of its own, and
// ExSheet, as its Consumer, is where they come from). Every word has an id and built-in English, and a
// Consumer replaces any of them through a label function that answers a word for an id, or `undefined` to
// keep ExSheet's; ExSheet's Japanese is chosen in one line, `sheetWords({ japanese: true })` (ADR-0370, section
// 10; ADR-0036 and ADR-0203 for ExGrid; ADR-0060 for ExPivot's). The words never follow the locale on their own.
// An id is the C# member's name in camelCase, with the case after a dot where one member says several things.
//
// A sentence with arguments is a function of them here, and a template by its id: {0}, {1} and {2} stand for
// its arguments in the order the function takes them, so a language that orders them otherwise moves them.

import type { CommitRefusalReason, CopyRefusalReason, EditDiscardReason, PasteRefusalReason, PlacementRefusalReason } from '@ex-grid/core';
import type { BorderLineStyle, CellAddress, Culture, FormulaSyntaxError, HorizontalAlignment, SheetRefusal } from '@ex-grid/sheet-engine';
import type {
    BorderEdge, BorderPreset, ColourTarget, DisabledNumberFormatCategory, FontStyle, FormatCellsTab, NumberFormatCategory,
    PaletteSwatch, StandardColourName, ThemeColourName,
} from './format-cells-offer';
import { defined, unreachable } from './must';
import type { PointingRefusalReason } from './pointing-refusal';
import { englishSheetWordTable, japaneseSheetWordTable } from './sheet-word-tables';

/** Where a refused press on a grid of a Pointing Scope is worded otherwise than by its reason alone: a drag
 * that took back what its press wrote, and a column the grid no longer shows (ADR-0058). */
export type PointingRefusalWordCase = PointingRefusalReason | 'severalCellsTakenBack' | 'severalColumnsTakenBack' | 'columnNotHeld';

/** Why Format Cells' OK cannot set what it shows, by name (ADR-0071), worded by {@link SheetWords.formatCellsRefusal}. */
export type FormatCellsRefusal =
    | { readonly kind: 'noNumberFormatCode' }
    | { readonly kind: 'numberFormatNotRead'; readonly code: string; readonly reason: string }
    | { readonly kind: 'notAColour'; readonly target: ColourTarget; readonly text: string };

/**
 * A label of Format Cells' own under the built-in Chrome (ADR-0071): its legends, fields and buttons, worded by
 * {@link SheetWords.formatCellsLabel}.
 */
export type FormatCellsLabel =
    | 'tabs' | 'category' | 'generalHint' | 'textHint' | 'decimalPlaces' | 'thousandsSeparator' | 'negativeNumbers'
    | 'type' | 'codes' | 'horizontal' | 'fontStyle' | 'underline' | 'underlineNone' | 'underlineSingle'
    | 'strikethrough' | 'normalFont' | 'colour' | 'lineStyle' | 'presets' | 'border' | 'preview' | 'backgroundColour'
    | 'automatic' | 'noColour' | 'moreColours' | 'ok' | 'cancel';

/** The words the Sheet's find panel adds to the grid's (ADR-0332 §5): the Within choice and its two answers. */
export type SheetFindWithinWord = 'within' | 'withinSheet' | 'withinWorkbook';

/** One of the commands of a Sheet Tab's menu (ADR-0331 §4), in the order the menu offers them. */
export type SheetTabCommand = 'rename' | 'delete' | 'moveLeft' | 'moveRight' | 'copy';

/** One of ExSheet's own Context Menu commands (ADR-0036). */
export type SheetContextCommand = 'insertRows' | 'deleteRows' | 'insertColumns' | 'deleteColumns' | 'formatCells';

/** A command of the Sheet Toolbar's default row (ADR-0100), in Excel's Home tab order. */
export type SheetToolbarCommand =
    | 'bold' | 'italic' | 'underline' | 'strikethrough' | 'fontColour' | 'fill' | 'borders' | 'alignLeft' | 'alignCenter'
    | 'alignRight' | 'numberFormat' | 'percent' | 'comma' | 'formatCells';

/** A preset of the Sheet Toolbar's Borders list (ADR-0100), in Excel's order. */
export type SheetToolbarBorder = 'bottom' | 'top' | 'left' | 'right' | 'none' | 'all' | 'outside' | 'thickOutside';

/**
 * The id of every word ExSheet shows: stable, so a Consumer's label function can answer it. An id with a
 * reason or a name in it takes the reason or the name as the union beside it spells it.
 */
export type SheetWordId =
    | 'formulaUnreadable' | 'addressUnreadable' | 'sheetNotNamed' | 'editIsOpen'
    | 'saveWhileEditing' | 'opening' | 'openWhileEditing' | 'opened'
    | 'openingCancelled' | 'openingRefused' | 'openingAlreadyLanded'
    | 'openingNothingToCancel' | 'editDiscardedByOpening' | 'formatKeyWhileEditing'
    | 'structureKeyWhileEditing.insert' | 'structureKeyWhileEditing.delete'
    | 'structureKeyNeedsWholeRowsOrColumns.insert' | 'structureKeyNeedsWholeRowsOrColumns.delete'
    | 'toolbarName' | 'toolbarWhileEditing' | 'toolbar.row.home' | 'toolbar.withShortcut' | `toolbar.item.${SheetToolbarCommand}` | 'toolbar.more' | 'toolbar.automatic' | 'toolbar.noFill'
    | `toolbar.border.${SheetToolbarBorder}` | 'toolbar.moreNumberFormats' | 'sheetTabsName' | 'editSheetGone'
    | 'sheetSwitchWhileEditing' | 'sheetOperationWhileEditing' | 'sheetAdded'
    | 'sheetRenamed' | 'sheetDeleted' | 'sheetMoved' | 'sheetCopied'
    | 'sheetNotRenamed' | 'newSheetName' | 'sheetNameField' | 'sheetMenuName' | `tabCommand.${SheetTabCommand}`
    | 'onlySheet' | 'sheetGone' | 'sheetNotChanged' | 'firstSheet'
    | 'lastSheet' | 'commaStyleUnread' | 'editDiscardedByNewDocument'
    | `editDiscarded.${EditDiscardReason}`
    | 'pasteReadsDifferently' | 'pasteRefusedFormula' | 'summaryRefused'
    | 'pasteTooNarrowToShow'
    | `pasteRefused.${PasteRefusalReason}`
    | `commitRefused.${CommitRefusalReason}`
    | `placementRefused.${PlacementRefusalReason}`
    | 'overwritten'
    | `copyRefused.${CopyRefusalReason}`
    | `pointingRefused.${PointingRefusalWordCase}`
    | 'pointingExtendedByKey' | 'nothingToUndo' | 'nothingToRedo'
    | `command.${SheetContextCommand}` | `find.${SheetFindWithinWord}` | 'formatCellsTitle'
    | `formatCells.tab.${FormatCellsTab}`
    | `formatCells.category.${NumberFormatCategory}`
    | `formatCells.disabled.${DisabledNumberFormatCategory}`
    | 'formatCells.noNumberFormatCode' | 'formatCells.numberFormatNotRead'
    | 'formatCells.notAColour'
    | `formatCells.alignment.${HorizontalAlignment}`
    | `formatCells.fontStyle.${FontStyle}`
    | `formatCells.edge.${BorderEdge}`
    | `formatCells.preset.${BorderPreset}`
    | `formatCells.lineStyle.${BorderLineStyle}`
    | `formatCells.colour.${ThemeColourName | StandardColourName}`
    | 'formatCells.colourDarker' | 'formatCells.colourLighter' | 'formatCells.colourStep'
    | `formatCells.label.${FormatCellsLabel}` | 'formatCells.symbol';

/**
 * A Consumer's word for an id, or `undefined` to keep ExSheet's own (its Japanese where that was chosen, else
 * its English). `english` is the id's English as a template: where it has `{0}` (and `{1}`, `{2}`), the word
 * answered is a template too, its arguments put in where it names them, so a language that orders them
 * otherwise moves them.
 */
export type SheetLabel = (id: SheetWordId, english: string) => string | undefined;

/** How a Sheet's words are chosen (ADR-0370, section 10): ExSheet's English, or its Japanese in one line, and
 * the Consumer's label over either. */
export interface SheetWordsChoice {
    /** ExSheet's Japanese in place of its English: Excel Japanese edition's words where Excel shows the same
     * thing (`セルの書式設定`, `表示形式`, `ユーザー定義`), and the family's own elsewhere. Never chosen by the
     * locale or the browser's language. */
    readonly japanese?: boolean | undefined;
    /** The Consumer's word for any id, which wins over ExSheet's. */
    readonly label?: SheetLabel | undefined;
}

/** Every id ExSheet has a word for: what a Consumer replaces to give ExSheet another language. */
export function sheetWordIdList(): readonly SheetWordId[] {
    // The table's keys are the union's members, which the table's type holds it to.
    // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- Object.keys widens a Record's keys to string
    return Object.freeze(Object.keys(englishSheetWordTable) as SheetWordId[]);
}

/** Puts the arguments into a template: `{n}` is the n-th argument. A template that names an argument the
 * sentence does not have is refused, rather than shown with the placeholder standing in it. */
function fill(id: SheetWordId, template: string, args: readonly string[]): string {
    return template.replaceAll(/\{(\d+)\}/gu, (placeholder, index: string) => {
        const arg = args[Number(index)];
        if (arg === undefined) {
            throw new RangeError(`The word for '${id}' names ${placeholder}, and the sentence has ${args.length} argument(s).`);
        }
        return arg;
    });
}

/** A Sheet's words, chosen once: ExSheet's English, or its Japanese, with the Consumer's label over either
 * (ADR-0370, section 10). */
export function sheetWords(choice: SheetWordsChoice = {}): SheetWords {
    return new SheetWords(choice);
}

/**
 * The words ExSheet shows, each through the Consumer's label function, else ExSheet's own (ADR-0370,
 * section 10). Its members are named as the C# package's SheetWords names them: a sentence that says something
 * of the moment is a function of what it names, and its arguments are put into the template its id answers.
 */
export class SheetWords {
    readonly #label: SheetLabel | undefined;
    readonly #own: Readonly<Record<SheetWordId, string>>;

    /** The words as `choice` chooses them; none is ExSheet's English throughout. */
    constructor(choice: SheetWordsChoice = {}) {
        const { japanese, label } = choice;
        if (label !== undefined && typeof label !== 'function') {
            throw new TypeError('A Sheet\'s label is a function of a word\'s id and its English, answering its words or undefined.');
        }
        if (japanese !== undefined && typeof japanese !== 'boolean') throw new TypeError('japanese is true or false.');
        this.#label = label;
        this.#own = japanese === true ? japaneseSheetWordTable : englishSheetWordTable;
    }

    /** The word for an id: the label's, else ExSheet's own. A template is returned as it is. */
    word(id: SheetWordId): string {
        const words: unknown = this.#label?.(id, englishSheetWordTable[id]);
        if (words === undefined) return this.#own[id];
        if (typeof words !== 'string') {
            throw new TypeError(`The label answered ${typeof words} for '${id}': it answers a word, or undefined for ExSheet's own.`);
        }
        return words;
    }

    #say(id: SheetWordId, ...args: readonly string[]): string {
        return fill(id, this.word(id), args);
    }

    /** A Formula the engine could not read, with where and why: `This Formula cannot be read at character 4: …`. */
    formulaUnreadable(error: FormulaSyntaxError): string {
        return this.#say('formulaUnreadable', String(error.position + 1), error.reason);
    }

    /** What was typed into the Name Box is not a place on the Sheet (ADR-0051). */
    addressUnreadable(typed: string): string {
        return this.#say('addressUnreadable', typed);
    }

    /** A refusal of the engine's, in its own words: the engine's refusals carry no id yet. */
    refused(refusal: SheetRefusal): string {
        return refusal.message;
    }

    /** A command over a Workbook of several Sheets that names none (ADR-0331 §6): taken against the Sheet
     * shown, its outcome would depend on whether a press on a tab reached the instance first. */
    sheetNotNamed(sheets: number): string {
        return this.#say('sheetNotNamed', String(sheets));
    }

    /** A change refused while a cell is being edited. */
    get editIsOpen(): string {
        return this.word('editIsOpen');
    }

    /** A save refused while a cell is being edited (ADR-0330 §10): it would quietly lack what is being typed. */
    get saveWhileEditing(): string {
        return this.word('saveWhileEditing');
    }

    /** An opening under way, said once as it starts (ADR-0330 §10, ADR-0240 §12). */
    get opening(): string {
        return this.word('opening');
    }

    /** An opening refused while a cell is being edited (ADR-0330 §10, ADR-0048). */
    get openWhileEditing(): string {
        return this.word('openWhileEditing');
    }

    /** A file opened. */
    get opened(): string {
        return this.word('opened');
    }

    /** An opening cancelled. */
    get openingCancelled(): string {
        return this.word('openingCancelled');
    }

    /** An opening refused, with the reason the file was refused for. */
    openingRefused(reason: string): string {
        return this.#say('openingRefused', reason);
    }

    /** The Escape that found the opening landed (ADR-0240 §12). */
    get openingAlreadyLanded(): string {
        return this.word('openingAlreadyLanded');
    }

    /** An Escape that found the opening over, refused or cancelled already: said, never swallowed (ADR-0240 §12). */
    get openingNothingToCancel(): string {
        return this.word('openingNothingToCancel');
    }

    /** What was typed, thrown away by a file opened during the edit. */
    get editDiscardedByOpening(): string {
        return this.word('editDiscardedByOpening');
    }

    /** A formatting key while an edit is open (ADR-0071): Excel formats the selected characters there, and a
     * Cell Format is per cell. */
    get formatKeyWhileEditing(): string {
        return this.word('formatKeyWhileEditing');
    }

    /** An insert or delete key while an edit is open (ADR-0050 item 14, 2026-10-02). */
    structureKeyWhileEditing(insert: boolean): string {
        return this.word(insert ? 'structureKeyWhileEditing.insert' : 'structureKeyWhileEditing.delete');
    }

    /** An insert or delete key over a Selection of neither whole rows nor whole columns (ADR-0050 item 14,
     * 2026-10-02): Excel shifts cells there, and ExSheet shifts none. */
    structureKeyNeedsWholeRowsOrColumns(insert: boolean): string {
        return this.word(insert ? 'structureKeyNeedsWholeRowsOrColumns.insert' : 'structureKeyNeedsWholeRowsOrColumns.delete');
    }

    /** The Sheet Toolbar's accessible name (ADR-0100). */
    get toolbarName(): string {
        return this.word('toolbarName');
    }

    /** The Sheet Toolbar's default row, Excel's Home tab (ADR-0100). */
    get toolbarHomeRow(): string {
        return this.word('toolbar.row.home');
    }

    /** A Toolbar Item's name (ADR-0100): `Bold`. */
    toolbarItem(command: SheetToolbarCommand): string {
        return this.word(`toolbar.item.${command}`);
    }

    /** A Toolbar Item's tooltip, its name with its key (ADR-0100): `Bold (Ctrl+B)`. */
    toolbarItemWithShortcut(name: string, shortcut: string): string {
        return this.#say('toolbar.withShortcut', name, shortcut);
    }

    /** A split control's arrow, which opens its list: `Borders, more`. */
    toolbarMore(name: string): string {
        return this.#say('toolbar.more', name);
    }

    /** The Font Colour list's first choice, which takes the colour away: Excel's Automatic. */
    get toolbarAutomatic(): string {
        return this.word('toolbar.automatic');
    }

    /** The Fill Colour list's first choice, which takes the fill away: Excel's No Fill. */
    get toolbarNoFill(): string {
        return this.word('toolbar.noFill');
    }

    /** A preset of the Borders list: `Bottom Border`. */
    toolbarBorder(preset: SheetToolbarBorder): string {
        return this.word(`toolbar.border.${preset}`);
    }

    /** The Number Format list's last choice, which opens Format Cells: Excel's More Number Formats…. */
    get toolbarMoreNumberFormats(): string {
        return this.word('toolbar.moreNumberFormats');
    }

    /** Why the Sheet Toolbar's items cannot be pressed while an edit is open (ADR-0100). */
    get toolbarWhileEditing(): string {
        return this.word('toolbarWhileEditing');
    }

    /** The Sheet Tabs' accessible name (ADR-0331). */
    get sheetTabsName(): string {
        return this.word('sheetTabsName');
    }

    /** An edit whose Sheet left the Workbook before it was committed. */
    get editSheetGone(): string {
        return this.word('editSheetGone');
    }

    /** Another Sheet not shown while a constant is being edited; a Formula shows one to point at its cells
     * (ADR-0331 §5). */
    get sheetSwitchWhileEditing(): string {
        return this.word('sheetSwitchWhileEditing');
    }

    /** A Sheet Tab operation refused while an edit is open (ADR-0331 §4). */
    get sheetOperationWhileEditing(): string {
        return this.word('sheetOperationWhileEditing');
    }

    /** A Sheet added, said in the Sheet's live region (ADR-0331 §4). */
    sheetAdded(name: string): string {
        return this.#say('sheetAdded', name);
    }

    /** A Sheet renamed. */
    sheetRenamed(from: string, to: string): string {
        return this.#say('sheetRenamed', from, to);
    }

    /** A Sheet deleted, and how to give it back. */
    sheetDeleted(name: string): string {
        return this.#say('sheetDeleted', name);
    }

    /** A Sheet moved to `position`, from 1, of `count` Sheets. */
    sheetMoved(name: string, position: number, count: number): string {
        return this.#say('sheetMoved', name, String(position), String(count));
    }

    /** A Sheet copied, and the copy's name. */
    sheetCopied(name: string, copy: string): string {
        return this.#say('sheetCopied', name, copy);
    }

    /** A rename refused, with the reason. */
    sheetNotRenamed(reason: string): string {
        return this.#say('sheetNotRenamed', reason);
    }

    /** The name a Sheet added is offered under, before it is numbered. */
    get newSheetName(): string {
        return this.word('newSheetName');
    }

    /** The accessible name of the field a Sheet is renamed in. */
    get sheetNameField(): string {
        return this.word('sheetNameField');
    }

    /** The accessible name of a Sheet Tab's menu. */
    sheetMenuName(sheet: string): string {
        return this.#say('sheetMenuName', sheet);
    }

    /** Why the only Sheet cannot be deleted. */
    get onlySheet(): string {
        return this.word('onlySheet');
    }

    /** A Sheet that left the Workbook. */
    get sheetGone(): string {
        return this.word('sheetGone');
    }

    /** A Sheet Tab operation refused, with the reason. */
    sheetNotChanged(reason: string): string {
        return this.#say('sheetNotChanged', reason);
    }

    /** Why the first Sheet does not move before itself. */
    get firstSheet(): string {
        return this.word('firstSheet');
    }

    /** Why the last Sheet does not move after itself. */
    get lastSheet(): string {
        return this.word('lastSheet');
    }

    /** Why the Sheet Toolbar has no Comma Style (ADR-0100): it pads with `*`, as Accounting does. */
    get commaStyleUnread(): string {
        return this.word('commaStyleUnread');
    }

    /** What was typed, thrown away by another Sheet Document opened during the edit. */
    get editDiscardedByNewDocument(): string {
        return this.word('editDiscardedByNewDocument');
    }

    /** What was typed, thrown away by the grid, and why (ADR-0142). */
    editDiscarded(reason: EditDiscardReason): string {
        return this.word(`editDiscarded.${reason}`);
    }

    /** A pasted field that came as an invariant value and would read differently typed under the Sheet's
     * culture, which the sentence names. */
    pasteReadsDifferently(field: string, culture: Culture): string {
        return this.#say('pasteReadsDifferently', field, culture.name);
    }

    /**
     * A paste of a Sheet's copy that holds a cell ExSheet refused, other than this Sheet's own copy of one range
     * (ADR-0047; 2026-10-12, decided with the user: a refusal is never caught, anywhere): pasted as a value, the
     * refusal would become an Error Value a Formula can catch. `refused` is the cell it was copied from.
     */
    pasteRefusedFormula(refused: CellAddress): string {
        return this.#say('pasteRefusedFormula', refused.toString());
    }

    /**
     * Why the Selection Summary shows no figure over a refused cell (2026-10-13, decided with the user): the
     * first refused cell in reading order, and what it shows, `#VALUE!` or `#NAME?`.
     */
    summaryRefused(refused: CellAddress, shown: string): string {
        return this.#say('summaryRefused', refused.toString(), shown);
    }

    /** A pasted field that is how its source shows a value too wide for its column (`####`). */
    pasteTooNarrowToShow(field: string, at: CellAddress): string {
        return this.#say('pasteTooNarrowToShow', field, at.toString());
    }

    /** A paste, a fill or an entry the grid refused, in the Sheet's terms. */
    pasteRefused(reason: PasteRefusalReason): string {
        return this.word(`pasteRefused.${reason}`);
    }

    /** A commit refused because the row it was opened on can no longer be found (ADR-0142, LV-20): the editor
     * stands with the typing, and only Escape leaves it. */
    commitRefused(address: string, reason: CommitRefusalReason): string {
        return this.#say(`commitRefused.${reason}`, address);
    }

    /** A selection the Consumer asked the grid to place, refused because the rows or columns it was taken
     * against have moved since (ADR-0005): the Selection stands where it was. */
    placementRefused(reason: PlacementRefusalReason): string {
        return this.word(`placementRefused.${reason}`);
    }

    /** A commit that landed over a cell changed while it was being edited (ADR-0142, LV-11): what it showed when
     * editing began, and just before. */
    overwritten(address: string, seen: string, replaced: string): string {
        return this.#say('overwritten', address, seen, replaced);
    }

    /** A copy the grid refused, in the Sheet's terms. */
    copyRefused(reason: CopyRefusalReason): string {
        return this.word(`copyRefused.${reason}`);
    }

    /** What the Context Menu calls one of ExSheet's own commands (ADR-0036). */
    command(command: SheetContextCommand): string {
        return this.word(`command.${command}`);
    }

    /** What a Sheet Tab's menu calls one of its commands (ADR-0331 §4). */
    tabCommand(command: SheetTabCommand): string {
        return this.word(`tabCommand.${command}`);
    }

    /** What the find panel calls Within and its two choices (ADR-0332 §5). */
    findWithin(word: SheetFindWithinWord): string {
        return this.word(`find.${word}`);
    }

    /** Format Cells' title. */
    get formatCellsTitle(): string {
        return this.word('formatCellsTitle');
    }

    /**
     * What a press on a grid of a Pointing Scope, or an arrow key after one, that wrote nothing tells (ADR-0058):
     * the table, the column where one is named, and whether a drag took back what its press wrote.
     */
    pointingRefused(reason: PointingRefusalReason, table: string, column: string | undefined, tookBack: boolean): string {
        switch (reason) {
            case 'severalCells':
                return this.#say(tookBack ? 'pointingRefused.severalCellsTakenBack' : 'pointingRefused.severalCells', table);
            case 'severalColumns':
                return this.#say(tookBack ? 'pointingRefused.severalColumnsTakenBack' : 'pointingRefused.severalColumns', table);
            case 'columnNotInTable':
                return this.#say('pointingRefused.columnNotInTable', table, defined(column, 'the column a press on a column the table does not have names'));
            case 'cellNotHeld':
                return column === undefined
                    ? this.#say('pointingRefused.cellNotHeld', table)
                    : this.#say('pointingRefused.columnNotHeld', table, column);
            case 'headerGroup':
            case 'noKey':
            case 'blankKey':
            case 'keyIsAnError':
            case 'rowNotArrived':
            case 'tableNotDeclared':
            case 'notPointing':
            case 'dataEdge':
                return this.#say(`pointingRefused.${reason}`, table);
            default:
                return unreachable(reason);
        }
    }

    /** What Shift and an arrow key tell after a press on a grid of a Pointing Scope (ADR-0058): refused as a
     * range is. */
    pointingExtendedByKey(table: string): string {
        return this.#say('pointingExtendedByKey', table);
    }

    /** Ctrl+Z with nothing to undo. */
    get nothingToUndo(): string {
        return this.word('nothingToUndo');
    }

    /** Ctrl+Y with nothing to redo. */
    get nothingToRedo(): string {
        return this.word('nothingToRedo');
    }

    /** A Format Cells tab's name. */
    tabName(tab: FormatCellsTab): string {
        return this.word(`formatCells.tab.${tab}`);
    }

    /** A Number Format category's name. */
    categoryName(category: NumberFormatCategory): string {
        return this.word(`formatCells.category.${category}`);
    }

    /** Why a category is shown disabled (ADR-0071). */
    categoryDisabled(category: DisabledNumberFormatCategory): string {
        return this.word(`formatCells.disabled.${category}`);
    }

    /** A horizontal alignment's name, as the Alignment tab lists it. */
    alignmentName(alignment: HorizontalAlignment): string {
        return this.word(`formatCells.alignment.${alignment}`);
    }

    /** A font style's name, as the Font tab lists it. */
    fontStyleName(style: FontStyle): string {
        return this.word(`formatCells.fontStyle.${style}`);
    }

    /** An edge's name, as the Border tab's buttons are named. */
    edgeName(edge: BorderEdge): string {
        return this.word(`formatCells.edge.${edge}`);
    }

    /** A preset's name, as the Border tab lists it. */
    presetName(preset: BorderPreset): string {
        return this.word(`formatCells.preset.${preset}`);
    }

    /** A line style's name. */
    lineStyleName(style: BorderLineStyle): string {
        return this.word(`formatCells.lineStyle.${style}`);
    }

    /** A palette colour's name, as Excel's tooltip gives it: `Dark Blue, Text 2, Lighter 90%`. */
    swatchName(swatch: PaletteSwatch): string {
        const name = this.word(`formatCells.colour.${swatch.name}`);
        if (swatch.step === undefined) return name;
        const step = this.#say(
            swatch.step.shade === 'darker' ? 'formatCells.colourDarker' : 'formatCells.colourLighter',
            String(swatch.step.percent));
        return this.#say('formatCells.colourStep', name, step);
    }

    /** A label of Format Cells' own under the built-in Chrome: a legend, a field or a button (ADR-0071). */
    formatCellsLabel(label: FormatCellsLabel): string {
        return this.word(`formatCells.label.${label}`);
    }

    /** The Currency category's symbol, as the Number tab shows it: `Symbol: $`. */
    currencySymbolShown(symbol: string): string {
        return this.#say('formatCells.symbol', symbol);
    }

    /** Why Format Cells' OK cannot set what it shows (ADR-0071). */
    formatCellsRefusal(refusal: FormatCellsRefusal): string {
        switch (refusal.kind) {
            case 'noNumberFormatCode':
                return this.word('formatCells.noNumberFormatCode');
            case 'numberFormatNotRead':
                return this.#say('formatCells.numberFormatNotRead', refusal.code, refusal.reason);
            case 'notAColour':
                return this.#say('formatCells.notAColour', refusal.text.trim());
            default:
                return unreachable(refusal);
        }
    }

    /** ExSheet's English, through no label. */
    static readonly english: SheetWords = new SheetWords();

    /** ExSheet's English for the Selection Summary's refusal, a template: `{0}` the cell, `{1}` what it shows. */
    static readonly summaryRefusedEnglish: string = englishSheetWordTable.summaryRefused;
}

