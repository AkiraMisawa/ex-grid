import { answerBoardSpec, boardDeclarationOf, boardInput, readBoard, type BoardSpecAnswer } from '@ex-grid/board';
import type { Snapshot } from '@ex-grid/data';
import { deskBoardData, deskBoardJson } from './DeskBoard';

/** A draw_board call as a model makes it: the Board, and the Data Handle a capability returned for its input this conversation. */
export const toolCall = `{"board": ${deskBoardJson}, "data": {"trades": "h-7f3a"}}`;

// The table behind the handle; what the model was told of it is its declaration alone, never its rows.
const declared = boardInput(readBoard(deskBoardJson), 'trades');
if (declared === undefined) throw new Error('The desk board declares no input \'trades\'.');
const trades: Snapshot = declared.fromJson(deskBoardData);

/** What the model was told of the table: its shape, as a Board declares an input. */
export const told = boardDeclarationOf(trades);

/** What the tool answers the model, with the Board bound to draw when it is accepted. */
export function answer(call: string): BoardSpecAnswer {
    return answerBoardSpec(call, handle => handle === 'h-7f3a' ? trades : undefined);
}
