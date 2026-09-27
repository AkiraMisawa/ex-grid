using System.Globalization;
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

    internal static string TooManyCells(long cells, long cap) =>
        string.Create(CultureInfo.InvariantCulture,
            $"The selection holds {cells:N0} cells, and formatting is applied to at most {cap:N0} at once. Select fewer cells.");

    internal static string NothingToUndo => "There is nothing to undo.";

    internal static string NothingToRedo => "There is nothing to redo.";
}
