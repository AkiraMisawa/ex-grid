using System.Globalization;
using ExGrid.Cells;
using ExGrid.Clipboard;
using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// The sentences ExSheet says to the user, in one place. ExGrid writes no sentences of its own
/// (ADR-0035); ExSheet, as its Consumer, is where they come from.
/// </summary>
internal static class SheetWords
{
    internal static string FormulaUnreadable(FormulaSyntaxException error) =>
        string.Create(CultureInfo.InvariantCulture, $"This Formula cannot be read at character {error.Position + 1}: {error.Reason}");

    internal static string AddressUnreadable(string typed) =>
        $"'{typed}' is not a cell address. Type an address such as D200, a range such as A1:C3, a column such as B:B or a row such as 5:5.";

    internal static string Refused(SheetRefusal refusal) => refusal.Message;

    internal static string EditIsOpen =>
        "Nothing was changed: a cell is being edited. Press Enter to commit the edit or Escape to cancel it, then try again.";

    // ADR-0063: in this state Excel formats the selected characters, and a Cell Format is per cell.
    internal static string FormatKeyWhileEditing =>
        "Nothing was formatted: a cell is being edited, and a format applies to whole cells, not to part of the text. Press Enter to commit the edit or Escape to cancel it, then try again.";

    internal static string EditDiscardedByNewDocument =>
        "What was typed was not entered: another Sheet Document was opened while the cell was being edited.";

    internal static string EditDiscarded(EditDiscardReason reason) => reason switch
    {
        EditDiscardReason.RowLeftTheWindow =>
            "What was typed was not entered: the row being edited was scrolled too far away to be written. Go back to it and type it again.",
        EditDiscardReason.OrderChanged => "What was typed was not entered: the rows moved while the cell was being edited.",
        EditDiscardReason.ColumnsChanged => "What was typed was not entered: the columns changed while the cell was being edited.",
        EditDiscardReason.ColumnNoLongerEditable => "What was typed was not entered: the cell stopped taking entries while it was being edited.",
        _ => "What was typed was not entered.",
    };

    internal static string PasteReadsDifferently(string field, CultureInfo culture) =>
        $"Nothing was pasted: '{field}' came as a value no culture changes, and typed under {culture.Name} it would not read as that value.";

    internal static string PasteTooNarrowToShow(string field, CellAddress at) =>
        $"Nothing was pasted: the field for {at} is '{field}', which is how the source shows a value too wide for its column. The source column was too narrow to show the value, so the value itself was not copied. Widen the column there and copy again.";

    internal static string PasteRefused(PasteRefusalReason reason) => reason switch
    {
        PasteRefusalReason.EmptySelection => "Select a cell to paste into.",
        PasteRefusalReason.SingleCellTarget => "Nothing was pasted: select a range of the copied block's shape.",
        PasteRefusalReason.ShapeMismatch =>
            "Nothing was pasted: the selection is not a whole multiple of the copied block. Select one cell, or a range the block repeats over exactly.",
        PasteRefusalReason.DisjointTarget =>
            "Nothing was pasted: a block of several cells cannot be pasted into a selection of several ranges. Select one range.",
        PasteRefusalReason.TargetNotEditable => "Nothing was pasted: the selection covers cells that cannot be written.",
        PasteRefusalReason.TooLarge => "Nothing was pasted: the clipboard holds more than a paste reads at once.",
        PasteRefusalReason.SpillPastExtent => "Nothing was pasted: the block would run past the Sheet's edge (XFD1048576).",
        _ => "Nothing was pasted.",
    };

    internal static string CopyRefused(CopyRefusalReason reason) => reason switch
    {
        CopyRefusalReason.EmptySelection => "Select the cells to copy.",
        CopyRefusalReason.MisalignedShape =>
            "Nothing was copied: the selected ranges do not line up into one block. Select ranges that share their rows or their columns.",
        CopyRefusalReason.TooLarge => "Nothing was copied: the selection holds more cells than a copy carries.",
        CopyRefusalReason.RowsUnavailable => "Nothing was copied: the rows of the selection could not be read.",
        CopyRefusalReason.ClipboardUnavailable => "Nothing was copied: the browser refused the clipboard, which still holds what it held before.",
        _ => "Nothing was copied.",
    };

    internal static string FormatCellsTitle => "Format Cells";

    internal static string NothingToUndo => "There is nothing to undo.";

    internal static string NothingToRedo => "There is nothing to redo.";
}
