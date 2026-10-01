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

    /// <summary>What a press on a grid of a Pointing Scope, or an arrow key after one, that wrote
    /// nothing tells (ADR-0058).</summary>
    internal static string PointingRefused(PointingRefusalReason reason, string table, string? column, bool tookBack) => reason switch
    {
        PointingRefusalReason.SeveralCells => tookBack
            ? $"What the press wrote was taken back: the drag reached another cell. A Formula reads one row of '{table}' by its key, and a range of cells cannot be written."
            : $"Nothing was written: more than one cell was pressed. A Formula reads one row of '{table}' by its key, and a range of cells cannot be written.",
        PointingRefusalReason.SeveralColumns => tookBack
            ? $"What the press wrote was taken back: the drag reached another column. A Formula names one column of '{table}' at a time."
            : $"Nothing was written: more than one column was pressed. A Formula names one column of '{table}' at a time.",
        PointingRefusalReason.HeaderGroup =>
            $"Nothing was written: a Header Group stands over several columns, and a Formula names one column of '{table}' at a time.",
        PointingRefusalReason.ColumnNotInTable =>
            $"Nothing was written: the column '{column}' is not a column of the Linked Table '{table}'.",
        PointingRefusalReason.NoKey =>
            $"Nothing was written: '{table}' was declared without a key, so a Formula cannot name one of its rows. Press a column's header to read the whole column.",
        PointingRefusalReason.BlankKey =>
            $"Nothing was written: this row's key in '{table}' is blank, so a Formula cannot name the row.",
        PointingRefusalReason.KeyIsAnError =>
            $"Nothing was written: this row's key in '{table}' is an error, which a lookup never finds.",
        PointingRefusalReason.RowNotArrived => "Nothing was written: this row has not arrived yet.",
        PointingRefusalReason.TableNotDeclared =>
            $"Nothing was written: this Sheet has no Linked Table '{table}' declared.",
        PointingRefusalReason.NotPointing =>
            "Nothing was written: the Formula no longer stood where a Reference can go when the press arrived.",
        PointingRefusalReason.DataEdge =>
            $"Nothing was written: Ctrl+arrow goes to the edge of the data, and the grid holds only the rows near those it shows, so where '{table}' ends is not known. Use the arrow alone.",
        PointingRefusalReason.CellNotHeld => column is null
            ? $"Nothing was written: the grid no longer holds the cell of '{table}' pointed at, so the arrow has no cell to move from. Press a cell to point again."
            : $"Nothing was written: the grid no longer shows the column '{column}' of '{table}' pointed at, so the arrow has no column to move from. Press a cell or a column's header to point again.",
        _ => "Nothing was written.",
    };

    /// <summary>What Shift and an arrow key tell after a press on a grid of a Pointing Scope
    /// (ADR-0058): refused as a range is.</summary>
    internal static string PointingExtendedByKey(string table) =>
        $"Nothing was written: Shift+arrow points at more than one cell. A Formula reads one row of '{table}' by its key, and a range of cells cannot be written.";

    internal static string NothingToUndo => "There is nothing to undo.";

    internal static string NothingToRedo => "There is nothing to redo.";
}
