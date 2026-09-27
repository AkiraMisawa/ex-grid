namespace ExGrid.Chrome;

/// <summary>
/// The words the built-in find panel shows (ADR-0047), by id — resolved as the built-in
/// menus resolve theirs: through the grid's <c>CommandLabel</c>, falling back to
/// <see cref="BuiltInCommandLabels"/>. A substituted Chrome words its own panel and never
/// consults these.
/// </summary>
public static class FindPanelLabelIds
{
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
}
