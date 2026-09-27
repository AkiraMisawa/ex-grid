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

    /// <summary>ExSheet's own English for one of its ids, or null for an id that is not ExSheet's.</summary>
    internal static string? EnglishFor(string id) => id switch
    {
        InsertRows => "Insert rows above",
        DeleteRows => "Delete rows",
        InsertColumns => "Insert columns to the left",
        DeleteColumns => "Delete columns",
        _ => null,
    };
}
