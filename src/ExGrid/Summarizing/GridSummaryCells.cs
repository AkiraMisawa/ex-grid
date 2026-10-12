using ExGrid.Data;

namespace ExGrid.Summarizing;

/// <summary>
/// The selected cells of an answerer that reads them itself — a Sheet's, a pivot report's — folded
/// one at a time into the figures' one definition (ADR-0130), which <see cref="GridSummary.Of{TRow}"/>
/// reads too: a number is in every figure; text, a Boolean and a date are counted; an error is
/// counted, is never a number, and leaves only Count and Numerical Count, as Excel's status bar
/// does; a blank is not added at all.
/// </summary>
public sealed class GridSummaryCells
{
    private readonly AggregateAccumulator _accumulator = new();

    /// <summary>Adds a value by what it is, as <see cref="GridSummary.Of{TRow}"/> reads a row's
    /// value: null is blank, an integral or <c>decimal</c> value an exact number, a <c>double</c> a
    /// number in Excel's arithmetic, anything else text.</summary>
    public void Add(object? value) => _accumulator.Add(value);

    /// <summary>Adds a number in Excel's <c>double</c>.</summary>
    public void AddNumber(double value) => _accumulator.AddDouble(value);

    /// <summary>Adds an exact number.</summary>
    public void AddNumber(decimal value) => _accumulator.AddExact(value);

    /// <summary>Adds a cell that holds something other than a number: counted, never summed.</summary>
    public void AddOther() => _accumulator.AddOther();

    /// <summary>Adds an error value: counted and never a number. Average, Min, Max and Sum are then
    /// shown nowhere; Count and Numerical Count still are.</summary>
    public void AddError() => _accumulator.AddError();

    /// <summary>Adds the parts of cells summarised elsewhere — a server's <c>COUNT</c>, <c>SUM</c>,
    /// <c>MIN</c> and <c>MAX</c> over a band — by the arithmetic that merges two pivot leaves
    /// (<see cref="AggregateAccumulator.Merge"/>).</summary>
    public void Merge(in AggregateCounts counts, in AggregateSum sum, in AggregateExtremes extremes)
        => _accumulator.Merge(counts, sum, extremes);

    /// <summary>The figures asked for, over the cells added; <paramref name="textOf"/>, where given,
    /// writes the text of each figure that reads numbers — Average, Min, Max, Sum — in the format of
    /// the request's Focus, or null to leave it to the grid. It is handed the figure as a column's
    /// format is handed a value: a <c>decimal</c> where the figure is exact, a <c>double</c>
    /// otherwise.</summary>
    public GridSummaryResult Answer(SummaryFigures figures, Func<object, string?>? textOf = null)
        => GridSummary.Answer(_accumulator, figures,
            textOf is null ? null : (_, figure) => textOf(figure.Exact is { } exact ? exact : figure.Number));
}
