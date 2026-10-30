namespace ExGrid.Chrome;

/// <summary>
/// The words of the find panel (ADR-0055) and its Replace (ADR-0332 §2), by id: the ids a Chrome's
/// <see cref="IGridChrome.Words"/> is asked for, as <c>ExGrid.MudBlazor</c> answers them, with
/// <see cref="BuiltInCommandLabels"/> the English beneath.
/// </summary>
public static class FindPanelLabelIds
{
    /// <summary>Where words hold a number: a Replace All's count, or the refused cells not named.</summary>
    public const string CountPlaceholder = "{count}";

    /// <summary>Where <see cref="ReplaceRefusedCell"/> holds the refused cell's row number.</summary>
    public const string RowPlaceholder = "{row}";

    /// <summary>Where <see cref="ReplaceRefusedCell"/> holds the refused cell's column header, and
    /// <see cref="FoundInHeader"/> the header the match was found in.</summary>
    public const string ColumnPlaceholder = "{column}";

    /// <summary>Where <see cref="ReplaceRefusedCell"/> holds why the cell cannot be written.</summary>
    public const string ReasonPlaceholder = "{reason}";

    /// <summary>The panel's accessible name.</summary>
    public const string Title = "find";

    /// <summary>The text field's label.</summary>
    public const string Field = "find-field";

    /// <summary>The Match case option.</summary>
    public const string MatchCase = "find-match-case";

    /// <summary>The Match entire cell contents option.</summary>
    public const string WholeCell = "find-whole-cell";

    /// <summary>The step forward.</summary>
    public const string Next = "find-next";

    /// <summary>The step backward.</summary>
    public const string Previous = "find-previous";

    /// <summary>What a step that matched nothing says.</summary>
    public const string NotFound = "find-not-found";

    /// <summary>What a step that found its match in a column header or a Header Group label says
    /// (ADR-0333), the header at <see cref="ColumnPlaceholder"/>: the Focus stays where it was, so the
    /// words are what tells the user where the match is.</summary>
    public const string FoundInHeader = "find-found-in-header";

    /// <summary>What a step whose answer arrived under another order says.</summary>
    public const string OrderChanged = "find-order-changed";

    /// <summary>What a step whose answer came from a <c>Source</c> since replaced by another
    /// instance says.</summary>
    public const string SourceChanged = "find-source-changed";

    /// <summary>The panel's accessible name while it offers Replace (ADR-0332 §2).</summary>
    public const string ReplaceTitle = "find-replace-title";

    /// <summary>The Replace field's label.</summary>
    public const string ReplaceField = "find-replace-field";

    /// <summary>The Replace button: the current match, then the next.</summary>
    public const string Replace = "find-replace";

    /// <summary>The Replace All button.</summary>
    public const string ReplaceAll = "find-replace-all";

    /// <summary>What a Replace All that replaced several cells says, their count at
    /// <see cref="CountPlaceholder"/>; and one that replaced none says <see cref="NotFound"/>.</summary>
    public const string Replaced = "find-replaced";

    /// <summary>What a Replace All that replaced one cell says.</summary>
    public const string ReplacedOne = "find-replaced-one";

    /// <summary>What a Replace that wrote nothing says first, before the cells it names.</summary>
    public const string ReplaceRefused = "find-replace-refused";

    /// <summary>One cell a Replace could not write: <see cref="RowPlaceholder"/>,
    /// <see cref="ColumnPlaceholder"/> and <see cref="ReasonPlaceholder"/>.</summary>
    public const string ReplaceRefusedCell = "find-replace-refused-cell";

    /// <summary>The refused cells past those named, their count at <see cref="CountPlaceholder"/>.</summary>
    public const string ReplaceRefusedMore = "find-replace-refused-more";

    /// <summary>Why a match in a column that is not Editable cannot be written.</summary>
    public const string NotEditable = "find-not-editable";
}
