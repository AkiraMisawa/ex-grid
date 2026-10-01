namespace ExPivot;

/// <summary>
/// The English of the words the Stale Report's notice paints (ADR-0066), by id, until the engine's
/// <c>PivotWords</c> holds them with their Japanese: a word is asked of the Consumer's
/// <c>Label</c> first, then of <c>PivotWords</c>, then of this, and an id none of them knows is
/// painted as itself. A word with <c>{0}</c> (and <c>{1}</c>) is a template.
/// </summary>
internal static class StaleReportWords
{
    /// <summary>The notice's sentence: {0} the time of the version shown, {1} what happened.</summary>
    public const string Notice = "stale-report";

    /// <summary>The newest data would make the report need more leaves than the cap: {0} the cap.</summary>
    public const string TooManyCells = "stale-too-many-cells";

    /// <summary>The newest data would make the report pass the rows' cap: {0} the cap.</summary>
    public const string TooManyRows = "stale-too-many-rows";

    /// <summary>The newest data would make the report pass the columns' cap: {0} the cap.</summary>
    public const string TooManyColumns = "stale-too-many-columns";

    /// <summary>The source failed: {0} its message.</summary>
    public const string SourceFailed = "stale-source-failed";

    /// <summary>The source refused for another reason: {0} its sentence.</summary>
    public const string SourceRefused = "stale-source-refused";

    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        [Notice] = "Showing the data as of {0}: {1}",
        [TooManyCells] = "the newest data needs more than {0} cells.",
        [TooManyRows] = "the newest data needs more than {0} rows.",
        [TooManyColumns] = "the newest data needs more than {0} columns.",
        [SourceFailed] = "the source could not answer: {0}",
        [SourceRefused] = "the source refused to answer: {0}",
        [PivotCommandIds.Retry] = "Retry",
    };

    /// <summary>Every id this class has an English word for.</summary>
    public static IReadOnlyCollection<string> Ids => English.Keys;

    /// <summary>The English for an id, or null for an id this class does not know.</summary>
    public static string? EnglishFor(string id) => English.GetValueOrDefault(id);
}
