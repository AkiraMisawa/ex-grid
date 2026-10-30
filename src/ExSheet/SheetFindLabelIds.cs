namespace ExSheet;

/// <summary>
/// The words ExSheet's find panel adds to the grid's (<see cref="ExGrid.Chrome.FindPanelLabelIds"/>), by id: Within and its
/// two choices (ADR-0332 §5). A Consumer translates them through <c>ExSheet.CommandLabel</c>, or a Chrome
/// through its own words, as it translates the grid's.
/// </summary>
public static class SheetFindLabelIds
{
    /// <summary>The label of the Within choice.</summary>
    public const string Within = "exsheet.find-within";

    /// <summary>Within: the Sheet shown.</summary>
    public const string WithinSheet = "exsheet.find-within-sheet";

    /// <summary>Within: the Workbook.</summary>
    public const string WithinWorkbook = "exsheet.find-within-workbook";

    /// <summary>ExSheet's English for one of these ids, as Excel's dialog words them; null for any other id.</summary>
    /// <param name="id">The id.</param>
    public static string? EnglishFor(string id) => id switch
    {
        Within => "Within",
        WithinSheet => "Sheet",
        WithinWorkbook => "Workbook",
        _ => null,
    };
}
