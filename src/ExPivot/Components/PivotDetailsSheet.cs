using System.Globalization;
using ExGrid;
using ExPivot.Engine;

namespace ExPivot.Components;

/// <summary>Why something asked of a Pivot Source cannot be shown: the source refused — the data
/// changed under the report's Source Version, for one — or it failed (ADR-0065/0025).</summary>
internal sealed record SourceProblem(PivotSourceRefusal? Refusal, Exception? Error);

/// <summary>
/// One Show Details opened by ExPivot itself — a tab at the report's foot, or the dialog
/// (ADR-0058): the cell's records as an ExGrid of the source's fields, fetched in pages through
/// <c>GridSource.Fetch</c> over the source's <c>DetailsAsync</c>, under the Source Version the
/// report was computed from (ADR-0025/0065). It keeps that version for as long as it stands, so a
/// bundled source's records always add up; a server that can no longer answer under it refuses, and
/// the sheet says the data has changed rather than show records that do not add up. It owns its
/// fetching source and releases it when it closes.
/// </summary>
internal sealed class PivotDetailsSheet : IDisposable
{
    private readonly Action _changed;

    public PivotDetailsSheet(string id, PivotDetails details, CultureInfo culture, Action changed)
    {
        Id = id;
        Details = details;
        _changed = changed;
        Columns = details.Fields.Select((field, index) => ColumnOf(field, index, culture)).ToArray();
        Source = GridSource.Fetch<PivotDetailRecord>(FetchAsync);
        Source.FetchFailed += OnFetchFailed;
    }

    /// <summary>Unique in its ExPivot: the tab's id, and its panel's with <c>-panel</c>.</summary>
    public string Id { get; }

    /// <summary>The cell and its records.</summary>
    public PivotDetails Details { get; }

    /// <summary>The pages of records, as the details grid's Grid Source.</summary>
    public FetchingGridSource<PivotDetailRecord> Source { get; }

    /// <summary>One column per field of the source, in its order, headed by its caption.</summary>
    public IReadOnlyList<GridColumn<PivotDetailRecord>> Columns { get; }

    /// <summary>Why the records cannot be shown, or null while they can.</summary>
    public SourceProblem? Problem { get; private set; }

    /// <summary>Bumped when <see cref="Problem"/> changes: what the details grid re-renders for.</summary>
    public int Version { get; private set; }

    private async ValueTask<GridPage<PivotDetailRecord>> FetchAsync(GridQuery query, CancellationToken cancellationToken)
    {
        // The records behind a cell are in the data's order, and none is left out: the details
        // grid offers no sort and no filter, and a question carrying one is not answered quietly
        // in another order (ADR-0058).
        if (query.Sorts.Count > 0 || query.Filter is not null)
            throw new NotSupportedException("The records behind a cell are shown in the data's order, unfiltered (ADR-0058).");
        var page = await Details.DetailsAsync(query.Range.Start, query.Range.Count, cancellationToken);
        if (page.IsRefused)
            throw new RefusedException(page.Refusal!);
        if (page.Total > int.MaxValue)
            throw new InvalidOperationException($"{page.Total:N0} records are behind the cell, more than a grid can hold.");
        return new GridPage<PivotDetailRecord>(page.Records, page.Start, (int)page.Total);
    }

    private void OnFetchFailed(Exception error)
    {
        Problem = error is RefusedException refused ? new SourceProblem(refused.Refusal, null) : new SourceProblem(null, error);
        Version++;
        _changed();
    }

    public void Dispose()
    {
        Source.FetchFailed -= OnFetchFailed;
        Source.Dispose();
    }

    /// <summary>A record's value in its field's column: text as it is, a number or a date in the
    /// field's format under the report's culture — at most 15 significant digits, or the culture's
    /// short date, as an Item is labelled (ADR-0059) — a Boolean as <c>TRUE</c> or <c>FALSE</c>.</summary>
    private static GridColumn<PivotDetailRecord> ColumnOf(PivotField field, int index, CultureInfo culture)
    {
        var type = field.Type switch
        {
            PivotFieldType.Number => ColumnType.Number,
            PivotFieldType.Date => ColumnType.Date,
            PivotFieldType.Boolean => ColumnType.Boolean,
            _ => ColumnType.Text,
        };
        var format = field.Format;
        return new GridColumn<PivotDetailRecord>(
            field.Name, type, record => record.Values[index], field.Caption, format: value => TextOf(value, format, culture));
    }

    internal static string TextOf(object value, string? format, CultureInfo culture) => value switch
    {
        string text => text,
        bool flag => flag ? "TRUE" : "FALSE",
        double number when !double.IsFinite(number) => "#NUM!",
        decimal or double => Formatted((IFormattable)value, format ?? "G15", culture),
        DateTime date => Formatted(date, format ?? (date.TimeOfDay == TimeSpan.Zero ? "d" : "G"), culture),
        _ => Convert.ToString(value, culture) ?? "",
    };

    private static string Formatted(IFormattable value, string format, CultureInfo culture)
    {
        try
        {
            return value.ToString(format, culture);
        }
        catch (FormatException)
        {
            return value.ToString(null, culture);
        }
    }

    /// <summary>What a record's value is, cell by cell: a value not of its field's declared type
    /// keeps its own (ADR-0059), and is painted and aligned as what it is.</summary>
    internal static readonly Func<PivotDetailRecord, GridColumn<PivotDetailRecord>, ColumnType> CellTypeOf =
        static (record, column) => column.Value(record) switch
        {
            null => column.Type,
            string => ColumnType.Text,
            decimal or double => ColumnType.Number,
            DateTime => ColumnType.Date,
            bool => ColumnType.Boolean,
            _ => ColumnType.Text,
        };

    /// <summary>A source's refusal, carried through the grid's fetching source to the sheet.</summary>
    private sealed class RefusedException(PivotSourceRefusal refusal) : Exception(refusal.Message)
    {
        public PivotSourceRefusal Refusal { get; } = refusal;
    }
}
