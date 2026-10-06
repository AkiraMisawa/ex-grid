using ExPivot.Engine;

namespace ExPivot;

/// <summary>
/// The records behind one cell of the report — Show Details (ADR-0059/0063) — as ExPivot hands
/// them to a Consumer that listens to <c>OnShowDetails</c>, and as its own tab or dialog shows
/// them. It names the cell, and pages its records from the Pivot Source under the Source Version
/// the report was computed from, so they add up to the cell; a source that can no longer answer
/// under it refuses, and the refusal says the data has changed (ADR-0066). Each record carries its
/// values in the order of the source's fields and, from a source in the process, the Consumer's own
/// object.
/// </summary>
public sealed class PivotDetails
{
    internal PivotDetails(
        PivotReportSource source,
        PivotReportDetailsQuery query,
        string sourceVersion,
        IReadOnlyList<PivotDetailItem> rowItems,
        IReadOnlyList<PivotDetailItem> columnItems,
        string? valueField,
        string title)
    {
        Source = source;
        Query = query;
        SourceVersion = sourceVersion;
        RowItems = rowItems;
        ColumnItems = columnItems;
        ValueField = valueField;
        Title = title;
    }

    /// <summary>The row's Items, outermost first: each field's caption and the Item's label.</summary>
    public IReadOnlyList<PivotDetailItem> RowItems { get; }

    /// <summary>The column's Items, outermost first.</summary>
    public IReadOnlyList<PivotDetailItem> ColumnItems { get; }

    /// <summary>The Value Field's caption, or null where the cell shows none.</summary>
    public string? ValueField { get; }

    /// <summary>What the cell is called, in the pivot's words: "Details: Americas / Credit / Bond".</summary>
    public string Title { get; }

    /// <summary>The question for every record behind the cell, from the first — serialisable
    /// (<see cref="PivotJson"/>), so a Consumer can carry it elsewhere.</summary>
    public PivotReportDetailsQuery Query { get; }

    /// <summary>The Source Version the report was computed from, which the records are asked
    /// under.</summary>
    public string SourceVersion { get; }

    /// <summary>The fields each record's values are in, in order: the source's.</summary>
    public IReadOnlyList<PivotField> Fields => Source.Fields;

    /// <summary>The source the report was computed from — the one its records are asked of.</summary>
    internal PivotReportSource Source { get; }

    /// <summary>
    /// One page of the records behind the cell, in the data's order, with how many there are — or
    /// the source's refusal, which a Consumer shows rather than records that would not add up
    /// (ADR-0066). Asked under <see cref="SourceVersion"/>.
    /// </summary>
    /// <param name="start">The first record wanted, counted among the records behind the cell.</param>
    /// <param name="count">How many records are wanted.</param>
    /// <param name="cancellationToken">Cancels the question.</param>
    public async ValueTask<PivotDetailPage> DetailsAsync(int start, int count, CancellationToken cancellationToken = default)
    {
        var result = await Source.DetailsAsync(Query with { Start = start, Count = count }, cancellationToken);
        if (result.Version != Query.Version)
            throw new InvalidOperationException("Show Details answered another Report Version (ADR-0152).");
        if (result.Refusal is { } refusal)
            return refusal.SourceRefusal is { } sourceRefusal ? PivotDetailPage.Refused(sourceRefusal)
                : refusal.Kind == PivotReportRefusalKind.ReportVersionNotHeld
                    ? PivotDetailPage.Refused(PivotSourceRefusal.SourceVersionNotHeld(SourceVersion))
                    : throw new InvalidOperationException(refusal.Message);
        return result.Page ?? throw new InvalidOperationException("Show Details returned neither a page nor a refusal.");
    }
}
