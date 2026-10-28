// ExSheet's Context Menu commands (ADR-0036), C#'s SheetCommandIds and ContextCommandsFor: inserting and deleting the
// rows or columns the Selection spans, and Format Cells (ADR-0071). A command carries no label: the menu asks the
// grid's `commandLabel` for the wording of an id, which the Sheet answers from its words for its own ids.

import type { GridCommand, SelectionRange } from '@ex-grid/core';
import { SheetEdit } from '@ex-grid/sheet-engine';
import { sheetFindLabelIds } from './sheet-find-panel';
import type { SheetContextCommand, SheetFindWithinWord, SheetWords } from './sheet-words';

/** The ids of the commands ExSheet adds to the grid's Context Menu, as C#'s SheetCommandIds spells them. */
export const sheetCommandIds: Readonly<Record<SheetContextCommand, string>> = Object.freeze({
    insertRows: 'exsheet.insert-rows',
    deleteRows: 'exsheet.delete-rows',
    insertColumns: 'exsheet.insert-columns',
    deleteColumns: 'exsheet.delete-columns',
    formatCells: 'exsheet.format-cells',
});

const commands: readonly SheetContextCommand[] = ['insertRows', 'deleteRows', 'insertColumns', 'deleteColumns', 'formatCells'];
const commandOfId: ReadonlyMap<string, SheetContextCommand> = new Map(commands.map(command => [sheetCommandIds[command], command]));

const findWords: readonly SheetFindWithinWord[] = ['within', 'withinSheet', 'withinWorkbook'];
const findWordOfId: ReadonlyMap<string, SheetFindWithinWord> = new Map(findWords.map(word => [sheetFindLabelIds[word], word]));

/** The Sheet's word for one of its own ids the grid shows, a Context Menu command's or the find panel's Within, or
 * `undefined` for an id that is not ExSheet's. */
export function sheetCommandWord(words: SheetWords, id: string): string | undefined {
    const command = commandOfId.get(id);
    if (command !== undefined) return words.command(command);
    const find = findWordOfId.get(id);
    return find === undefined ? undefined : words.findWithin(find);
}

/** What the commands do, as the Sheet does them: a structural edit, each one step on the undo stack, and Format
 * Cells opened by the user. */
export interface SheetContextActions {
    readonly structure: (edit: SheetEdit) => void;
    readonly formatCells: () => void;
}

/**
 * The Sheet's commands for a Context Menu opened over `selection`. Rows and columns are places, so the Row Sequence
 * Version does not move and the Selection stays where it was (ADR-0046). A Selection of several ranges has no one
 * span to act on, so the structural commands are unavailable for it rather than guessing one; Format Cells acts on
 * every range of the Selection, so it stands for several as for one.
 */
export function sheetContextCommands(selection: readonly SelectionRange[], actions: SheetContextActions): readonly GridCommand[] {
    const span = selection.length === 1 ? selection[0] : undefined;
    const structure = (id: string, edit: (range: SelectionRange) => SheetEdit): GridCommand => ({
        id,
        enabled: span !== undefined,
        invoke: () => {
            if (span === undefined) throw new Error('Internal: a disabled command is never run.');
            actions.structure(edit(span));
        },
    });
    return [
        structure(sheetCommandIds.insertRows, r => SheetEdit.insertRows(r.topRow, r.rowCount)),
        structure(sheetCommandIds.deleteRows, r => SheetEdit.deleteRows(r.topRow, r.rowCount)),
        structure(sheetCommandIds.insertColumns, r => SheetEdit.insertColumns(r.leftColumn, r.columnCount)),
        structure(sheetCommandIds.deleteColumns, r => SheetEdit.deleteColumns(r.leftColumn, r.columnCount)),
        { id: sheetCommandIds.formatCells, enabled: selection.length > 0, invoke: actions.formatCells },
    ];
}
