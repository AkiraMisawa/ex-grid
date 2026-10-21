namespace ExGrid.Chrome;

/// <summary>
/// The words of the find panel (ADR-0055) and its Replace (ADR-0332 §2), by id. The built-in panel resolves them as the
/// built-in menus resolve theirs — through the grid's <c>CommandLabel</c>, falling back to
/// <see cref="BuiltInCommandLabels"/> — and a substituted Chrome may use the same ids for its
/// own wording, as <c>ExGrid.MudBlazor</c> does. The grid holds no sentence: which one a step's
/// outcome calls for is <see cref="For"/>.
/// </summary>
public static class FindPanelLabelIds
{
    /// <summary>The id of what the panel says about a step's outcome, or null for an outcome
    /// that says nothing — no step yet, or a match, which the moved Focus shows.</summary>
    public static string? For(Finding.FindOutcome outcome) => outcome switch
    {
        Finding.FindOutcome.NotFound => NotFound,
        Finding.FindOutcome.OrderChanged => OrderChanged,
        Finding.FindOutcome.SourceChanged => SourceChanged,
        Finding.FindOutcome.Replaced => Replaced,
        Finding.FindOutcome.ReplaceRefused => ReplaceRefused,
        _ => null,
    };

    /// <summary>How many refused cells <see cref="OutcomeText"/> names before it counts the rest.</summary>
    public const int RefusedCellsNamed = 5;

    /// <summary>
    /// The whole sentence the panel shows for <paramref name="context"/>'s outcome, in
    /// <paramref name="words"/> — the Chrome's wording of these ids — or the empty string for an
    /// outcome that says nothing. A Replace All's count fills <see cref="CountPlaceholder"/>; a
    /// refused Replace names its first <see cref="RefusedCellsNamed"/> cells, each with
    /// <see cref="ReplaceRefusedCell"/>, and counts the rest with <see cref="ReplaceRefusedMore"/>
    /// (ADR-0332 §2). The built-in panel and <c>ExGrid.MudBlazor</c>'s both say it through this.
    /// </summary>
    /// <param name="context">The panel's context.</param>
    /// <param name="words">The Chrome's words for an id.</param>
    public static string OutcomeText(FindContext context, Func<string, string> words)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(words);
        if (For(context.Outcome) is not { } id)
            return "";
        if (context.Outcome == Finding.FindOutcome.Replaced)
        {
            var count = context.ReplacedCount.ToString(System.Globalization.CultureInfo.CurrentCulture);
            return words(context.ReplacedCount == 1 ? ReplacedOne : Replaced).Replace(CountPlaceholder, count, StringComparison.Ordinal);
        }
        if (context.Outcome != Finding.FindOutcome.ReplaceRefused)
            return words(id);
        var said = new System.Text.StringBuilder(words(ReplaceRefused));
        var cells = context.ReplaceRefusedCells;
        for (var i = 0; i < cells.Count && i < RefusedCellsNamed; i++)
        {
            var cell = cells[i];
            said.Append(' ').Append(words(ReplaceRefusedCell)
                .Replace(RowPlaceholder, cell.RowNumber.ToString(System.Globalization.CultureInfo.CurrentCulture), StringComparison.Ordinal)
                .Replace(ColumnPlaceholder, cell.Column, StringComparison.Ordinal)
                .Replace(ReasonPlaceholder, (cell.Reason ?? words(NotEditable)).TrimEnd().TrimEnd('.'), StringComparison.Ordinal));
        }
        if (cells.Count > RefusedCellsNamed)
        {
            said.Append(' ').Append(words(ReplaceRefusedMore)
                .Replace(CountPlaceholder, (cells.Count - RefusedCellsNamed).ToString(System.Globalization.CultureInfo.CurrentCulture), StringComparison.Ordinal));
        }
        return said.ToString();
    }

    /// <summary>Where words hold a number: a Replace All's count, or the refused cells not named.</summary>
    public const string CountPlaceholder = "{count}";

    /// <summary>Where <see cref="ReplaceRefusedCell"/> holds the refused cell's row number.</summary>
    public const string RowPlaceholder = "{row}";

    /// <summary>Where <see cref="ReplaceRefusedCell"/> holds the refused cell's column header.</summary>
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
