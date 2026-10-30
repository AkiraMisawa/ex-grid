namespace ExGrid.Chrome;

/// <summary>
/// The words of the Selection Summary (ADR-0130), by id: each figure's name, what the line says
/// while the question is out, and the figures menu's accessible name. The built-in Chrome resolves
/// them as it resolves the find panel's — through the grid's <c>CommandLabel</c>, falling back to
/// <see cref="BuiltInCommandLabels"/> — and a substituted Chrome may use the same ids.
/// </summary>
public static class SummaryLabelIds
{
    /// <summary>The id of a single figure's name.</summary>
    public static string For(Summarizing.SummaryFigures figure) => Summarizing.SummaryFigureOrder.LabelIdOf(figure);

    /// <summary>Average.</summary>
    public const string Average = "summary-average";

    /// <summary>Count.</summary>
    public const string Count = "summary-count";

    /// <summary>Numerical Count.</summary>
    public const string NumericalCount = "summary-numerical-count";

    /// <summary>Min.</summary>
    public const string Min = "summary-min";

    /// <summary>Max.</summary>
    public const string Max = "summary-max";

    /// <summary>Sum.</summary>
    public const string Sum = "summary-sum";

    /// <summary>What the line says while the question is out.</summary>
    public const string Pending = "summary-pending";

    /// <summary>The figures menu's accessible name.</summary>
    public const string Menu = "summary-menu";
}
