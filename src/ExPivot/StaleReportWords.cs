namespace ExPivot;

/// <summary>
/// The ids of the words the Stale Report's notice paints (ADR-0067). Their English and their
/// Japanese are <c>PivotWords</c>'s, as every other word ExPivot paints. A word with <c>{0}</c>
/// (and <c>{1}</c>) is a template.
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
}
