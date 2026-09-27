using System.Globalization;
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

    internal static string FormatSeveralWholeRanges =>
        "Nothing was formatted: whole columns or rows are formatted one selection range at a time. Select one range.";

    internal static string PasteUnreadable(FormulaSyntaxException error) =>
        string.Create(CultureInfo.InvariantCulture,
            $"Nothing was pasted: a pasted Formula cannot be read at character {error.Position + 1}: {error.Reason}");

    internal static string PasteReadsDifferently(string field, CultureInfo culture) =>
        $"Nothing was pasted: '{field}' came as a value no culture changes, and typed under {culture.Name} it would not read as that value.";

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

    internal static string NothingToUndo => "There is nothing to undo.";

    internal static string NothingToRedo => "There is nothing to redo.";
}
