namespace ExSheet;

/// <summary>
/// The ids of the commands ExSheet adds to the grid's Context Menu (ADR-0036). A command carries
/// no label: the menu asks <c>ExSheet.CommandLabel</c> for the wording of an id, and ExSheet's
/// own English stands where that says nothing. The ids are stable, so a Consumer can translate
/// them.
/// </summary>
public static class SheetCommandIds
{
    /// <summary>Inserts as many rows as the Selection spans, above it (ADR-0046).</summary>
    public const string InsertRows = "exsheet.insert-rows";

    /// <summary>Deletes the rows the Selection spans (ADR-0046).</summary>
    public const string DeleteRows = "exsheet.delete-rows";

    /// <summary>Inserts as many columns as the Selection spans, to its left (ADR-0046).</summary>
    public const string InsertColumns = "exsheet.insert-columns";

    /// <summary>Deletes the columns the Selection spans (ADR-0046).</summary>
    public const string DeleteColumns = "exsheet.delete-columns";

    /// <summary>Opens Format Cells over the Selection (ADR-0071), as <c>ExSheet.OpenFormatCellsAsync</c> does.</summary>
    public const string FormatCells = "exsheet.format-cells";

    /// <summary>
    /// The words the Selection Summary shows in place of its figures when the Selection holds a cell
    /// whose Formula ExSheet refused (a Refused Formula, its cell <c>#VALUE!</c> or <c>#NAME?</c>): no figure is shown
    /// over it, since Excel would answer that Formula. Not a command; asked as a refusal's words are
    /// (ADR-0203) — a substituted Chrome's, else <c>ExSheet.CommandLabel</c> — so a Consumer can
    /// translate it. In the words,
    /// <c>{0}</c> stands for the refused cell's address, the first in reading order, and <c>{1}</c> for
    /// what that cell shows.
    /// </summary>
    public const string SummaryRefused = "exsheet.summary-refused";

    /// <summary>
    /// ExSheet's own English for one of its ids, or null for an id that is not ExSheet's: what the
    /// menu shows where <c>ExSheet.CommandLabel</c> says nothing, and what a substituted Chrome,
    /// which words a command from its id (ADR-0036), falls back to for ExSheet's. It also words the
    /// grid's refusals of a paste or a copy (<c>RefusalLabelIds</c>) in the Sheet's terms, so that the
    /// grid announces a refusal on a Sheet once, in the Sheet's words (ADR-0203).
    /// </summary>
    /// <param name="id">The id of a command, or of a refusal's words.</param>
    public static string? EnglishFor(string id) => id switch
    {
        InsertRows => "Insert rows above",
        DeleteRows => "Delete rows",
        InsertColumns => "Insert columns to the left",
        DeleteColumns => "Delete columns",
        FormatCells => "Format Cells…",
        SummaryRefused => SheetWords.SummaryRefusedEnglish,
        _ => SheetFindLabelIds.EnglishFor(id) ?? SheetWords.RefusalWords(id),
    };
}
