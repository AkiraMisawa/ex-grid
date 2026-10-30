// Which changes widen the columns whose numbers no longer fit, as Excel's do (ADR-0047, second round; SH-20;
// ADR-0071, case 17), the counterpart of the rules in C#'s SheetWidening. A recorded width is one of three kinds
// (ADR-0046, 2026-09-28; SH-26): a column at the default width, or one an earlier entry or Number Format
// widened, is widened again by a number that needs more; a width the user set is never widened (case 18). Only
// these widen: not a paste, a fill or a recalculated Formula. Which columns widen, and to what, is measured
// against the grid's text estimate, which the core does not export yet; that part of SheetWidening is not here.

import type { CellFormatChange } from '@ex-grid/sheet-engine';

/**
 * Whether a change a formatting key sets widens the columns whose numbers no longer fit (ADR-0071; ticket 58's
 * rule): a Number Format key widens (case 17), and so do Ctrl+5 and Ctrl+U, setting strikethrough or underline
 * or taking it off, though they set no Number Format. Excel's Ctrl+5 widened column A for `1234567.50` (the
 * fourteenth Windows run, case 7) and from 8.09 to 11.73 for `123456789.12` in `0.00` (the seventeenth's h8),
 * and Ctrl+U the same column the same way (h7). Ctrl+B and the Fill did not (case 7), nor did Ctrl+I (h7). That
 * Ctrl+4 widens as Ctrl+U does, that the Border keys widen nothing, and that taking strikethrough or underline
 * off widens are readings. `SetCellFormatAsync` widens by this rule too (decided 2026-10-12), as Excel's
 * `Range.Font` set through COM did (the readings of 2026-10-12 (R6), case (5); of 2026-10-14, run 18a, widening
 * cases 2 and 6).
 */
export function widensAsAKeyDoes(change: CellFormatChange): boolean {
    return change.numberFormat !== undefined || change.underline !== undefined || change.strikethrough !== undefined;
}

/**
 * Whether a change a Sheet Toolbar item sets widens the columns whose numbers no longer fit, as Excel's Home tab
 * buttons do (ADR-0071, ADR-0100): as a key does, and for a Font colour too. Excel's Bold and Italic buttons did
 * not widen and its Underline button did (R6, case (3)); its Fill Color button did not (the fourteenth Windows
 * run, case 7); and its Font Color button widened, where `Font.Color` set through COM did not (run 18a, widening
 * case 5; decided with the user, 2026-10-14).
 */
export function widensAsAHomeTabButtonDoes(change: CellFormatChange): boolean {
    return widensAsAKeyDoes(change) || change.fontColour !== undefined;
}

/**
 * Whether a change Format Cells' OK sets widens the columns whose numbers no longer fit, in OK's one undo step
 * (ADR-0071; decided 2026-10-12): one that sets a Number Format (case 17; the fourteenth Windows run, case 8),
 * and one that sets any part of the Font, though Ctrl+B and Ctrl+I widen nothing (the seventeenth run, h7; R6,
 * case (1)). A Fill, Borders or an alignment widens nothing (R6, case (2); case 9); Borders over #### and the
 * horizontal alignment are readings.
 */
export function widensAsFormatCellsOkDoes(change: CellFormatChange): boolean {
    return change.numberFormat !== undefined || change.fontColour !== undefined || change.bold !== undefined
        || change.italic !== undefined || change.underline !== undefined || change.strikethrough !== undefined;
}
