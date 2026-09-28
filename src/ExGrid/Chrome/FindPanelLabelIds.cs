namespace ExGrid.Chrome;

/// <summary>
/// The words of the find panel (ADR-0055), by id. The built-in panel resolves them as the
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
        _ => null,
    };

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
