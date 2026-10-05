using ExGrid.Data;

namespace ExGrid.Summarizing;

/// <summary>
/// The selected cells of an answerer that reads them itself — a Sheet's, a pivot report's — folded
/// one at a time into the figures' one definition (ADR-0130), which <see cref="GridSummary.Of{TRow}"/>
/// reads too: a number is in every figure; text, a Boolean and a date are counted; an error is
/// counted and leaves Count alone; a blank is not added at all.
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

    /// <summary>Adds an error value: counted, and every figure but Count is shown nowhere.</summary>
    public void AddError() => _accumulator.AddError();

    /// <summary>The figures asked for, over the cells added.</summary>
    public GridSummaryResult Answer(SummaryFigures figures) => GridSummary.Answer(_accumulator, figures);
}
