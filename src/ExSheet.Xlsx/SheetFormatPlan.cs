using ExGrid.Data.Xlsx;
using ExSheet.Engine;

namespace ExSheet.Xlsx;

/// <summary>
/// What a worksheet writes for a Sheet's Cell Formats and column widths (ADR-0330 §4, ADR-0046, ADR-0047, ADR-0071): its
/// columns' widths and formats, its rows' formats, and the format each cell element is written with.
/// </summary>
/// <remarks>
/// <para>
/// A Sheet records a format part by part at three levels, cell over row over column; a file records one whole format at
/// each, and a cell element's format stands alone. So a column writes its own level over the default, a row its own level
/// over the default (in the file a row's format stands in place of its columns'), and every cell the Sheet records writes
/// the format the Sheet shows there (<see cref="Style"/>). Where a formatted row crosses a formatted column that records a
/// part the row leaves to it, what the Sheet shows there is not the row's format, so a cell of no value is written there with
/// the format it shows: a <see cref="Crossing"/>. A width is written in the file's unit (<see cref="ColumnWidthUnit"/>),
/// marked fitted (<c>bestFit</c>) where an entry widened it; a column that records a format and no width is written at the
/// default width, not marked as set.
/// </para>
/// <para>
/// The crossings are what a Sheet formatted on many rows and many columns costs: rows × columns cells. They are counted
/// first, from the runs alone (<see cref="Crossings"/>), in time that grows with the runs and not with the cells, so that the
/// writer refuses a Sheet that would write more cells than a file is read with (<see cref="XlsxSaveOptions.MaxCells"/>)
/// before a cell is written; only then are their formats found (<see cref="Build"/>).
/// </para>
/// </remarks>
internal sealed class SheetFormatPlan
{
    /// <summary>A run of formatted rows, as the file writes them: each row's format, and the column ranges it crosses.</summary>
    /// <param name="First">The run's first row.</param>
    /// <param name="Last">The run's last row.</param>
    /// <param name="Style">The cell format index each row is written with.</param>
    /// <param name="Crossings">The column ranges, in order, where a cell of no value is written in each row of the run.</param>
    public sealed record RowRun(int First, int Last, int Style, IReadOnlyList<Crossing> Crossings);

    /// <summary>A column range where a formatted row crosses a formatted column and shows another format than the row's.</summary>
    /// <param name="First">The range's first column.</param>
    /// <param name="Last">The range's last column.</param>
    /// <param name="Style">The cell format index its cells are written with.</param>
    public sealed record Crossing(int First, int Last, int Style);

    private readonly SheetDocument sheet;
    private readonly Dictionary<int, FormatParts> columnLevels = [];

    /// <summary>Each formatted column's parts other than the default's, as a mask.</summary>
    private readonly Dictionary<int, int> columnParts = [];

    /// <summary>The formatted column runs, in order, each with the parts it records other than the default's, as a mask.</summary>
    private readonly List<(int First, int Last, FormatParts Level, int Parts)> columnRuns = [];

    /// <summary>The formatted row runs, in order, each with the parts it leaves to its columns, as a mask.</summary>
    private readonly List<(int First, int Last, FormatParts Level, int Open)> rowRuns = [];

    /// <summary>For each mask of parts a row leaves open, the column runs it crosses and how many columns they hold.</summary>
    private readonly Dictionary<int, (List<int> Runs, long Columns)> crossed = [];

    private SheetFormatPlan(SheetDocument sheet)
    {
        this.sheet = sheet;
        foreach (var run in sheet.Columns.OrderBy(r => r.First))
        {
            var level = Parts(run);
            var parts = Mask(FormatParts.Differing(level.Over(CellFormat.Default), CellFormat.Default));
            for (var c = run.First; c <= run.Last; c++)
            {
                columnLevels[c] = level;
                columnParts[c] = parts;
            }
            columnRuns.Add((run.First, run.Last, level, parts));
        }
        foreach (var run in sheet.Rows.OrderBy(r => r.First))
        {
            var level = Parts(run);
            rowRuns.Add((run.First, run.Last, level, ~Mask(level) & AllParts));
        }
    }

    /// <summary>The plan for <paramref name="sheet"/>, its levels read; nothing is written to a styles part yet.</summary>
    public static SheetFormatPlan For(SheetDocument sheet) => new(sheet);

    /// <summary>The column ranges, in order, each with its width and its format's index; set by <see cref="Build"/>.</summary>
    public IReadOnlyList<XlsxColumn> Columns { get; private set; } = [];

    /// <summary>The runs of formatted rows, in order; set by <see cref="Build"/>.</summary>
    public IReadOnlyList<RowRun> Rows { get; private set; } = [];

    /// <summary>The rows the row formats write, each a <c>&lt;row&gt;</c> element whether or not it holds a cell.</summary>
    public long FormattedRows => rowRuns.Sum(r => (long)(r.Last - r.First + 1));

    /// <summary>
    /// How many cells of no value the crossings write: every place a formatted row crosses a formatted column that records a
    /// part the row leaves open, less those of <paramref name="recorded"/>, which are written as the Sheet's own cells. Counted
    /// from the runs, never cell by cell.
    /// </summary>
    /// <param name="recorded">The cells the Sheet writes of its own, each once.</param>
    public long Crossings(IEnumerable<CellAddress> recorded)
    {
        long count = 0;
        foreach (var run in rowRuns)
            count += (long)(run.Last - run.First + 1) * Crossed(run.Open).Columns;
        if (count == 0) return 0;
        foreach (var at in recorded)
        {
            if (RowRunAt(at.Row) is { } r && columnParts.TryGetValue(at.Column, out var parts) && (parts & rowRuns[r].Open) != 0)
            {
                count--;
            }
        }
        return count;
    }

    /// <summary>Finds every format the columns, the rows and the crossings write, adding each to <paramref name="styles"/>.</summary>
    public void Build(StyleTable styles)
    {
        var rows = new List<RowRun>(rowRuns.Count);
        foreach (var run in rowRuns)
        {
            var crossings = new List<Crossing>();
            foreach (var index in Crossed(run.Open).Runs)
            {
                var column = columnRuns[index];
                var style = styles.Index(run.Level.Over(column.Level.Over(CellFormat.Default)));
                if (crossings.Count > 0 && crossings[^1] is var last && last.Last == column.First - 1 && last.Style == style)
                    crossings[^1] = last with { Last = column.Last };
                else
                    crossings.Add(new Crossing(column.First, column.Last, style));
            }
            rows.Add(new RowRun(run.First, run.Last, styles.Index(run.Level.Over(CellFormat.Default)), crossings));
        }
        Rows = rows;
        Columns = ColumnRanges(styles);
    }

    /// <summary>The index of the format a cell element at <paramref name="at"/> is written with: what the Sheet shows there, its own level over its row's and its column's.</summary>
    public int Style(StyleTable styles, FormatParts own, CellAddress at)
    {
        var under = columnLevels.TryGetValue(at.Column, out var c) ? c.Over(CellFormat.Default) : CellFormat.Default;
        if (RowRunAt(at.Row) is { } r) under = rowRuns[r].Level.Over(under);
        return styles.Index(own.Over(under));
    }

    private const int AllParts = 0b11111;

    /// <summary>The parts a level records, as a mask: Number Format, alignment, Font, Fill, borders.</summary>
    private static int Mask(FormatParts level) =>
        (level.NumberFormat is null ? 0 : 1) | (level.Alignment is null ? 0 : 2) | (level.Font is null ? 0 : 4) | (level.Fill is null ? 0 : 8) | (level.Borders is null ? 0 : 16);

    /// <summary>The column runs a row that leaves <paramref name="open"/> to its columns crosses: those that record one of those parts other than the default's.</summary>
    private (List<int> Runs, long Columns) Crossed(int open)
    {
        if (crossed.TryGetValue(open, out var known)) return known;
        var runs = new List<int>();
        long columns = 0;
        for (var i = 0; i < columnRuns.Count; i++)
        {
            if ((columnRuns[i].Parts & open) == 0) continue;
            runs.Add(i);
            columns += columnRuns[i].Last - columnRuns[i].First + 1;
        }
        return crossed[open] = (runs, columns);
    }

    /// <summary>The index of the formatted row run that holds <paramref name="row"/>, or null.</summary>
    private int? RowRunAt(int row)
    {
        int low = 0, high = rowRuns.Count - 1;
        while (low <= high)
        {
            var mid = (low + high) / 2;
            if (row < rowRuns[mid].First) high = mid - 1;
            else if (row > rowRuns[mid].Last) low = mid + 1;
            else return mid;
        }
        return null;
    }

    private static FormatParts Parts(SheetDocumentAxisFormat run) => new(run.NumberFormat, run.Alignment, run.Font, run.Fill, run.Borders);

    /// <summary>The columns' widths and formats as ranges: a range wherever both stay the same.</summary>
    private List<XlsxColumn> ColumnRanges(StyleTable styles)
    {
        var widths = new Dictionary<int, SheetDocumentColumnWidth>();
        foreach (var run in sheet.ColumnWidths)
        {
            for (var c = run.First; c <= run.Last; c++) widths[c] = run;
        }
        var ranges = new List<XlsxColumn>();
        foreach (var column in widths.Keys.Union(columnLevels.Keys).Order())
        {
            // A column that records a format and no width of its own is written at the default width, not marked as set: a <col>
            // with no width is believed to be read by Excel as a width of 0, which hides the column (ticket 07 asks Excel).
            var custom = widths.TryGetValue(column, out var w);
            var width = ColumnWidthUnit.ToFile(custom ? w!.Width : Sheet.DefaultColumnWidth);
            var bestFit = w is { Kind: SheetColumnWidthKind.WidenedByEntry };
            var style = columnLevels.TryGetValue(column, out var level) ? styles.Index(level.Over(CellFormat.Default)) : 0;
            var next = new XlsxColumn(column, column, width, StyleIndex: style, BestFit: bestFit, CustomWidth: custom);
            if (ranges.Count > 0 && ranges[^1] is var last && last.Last == column - 1 && last with { Last = column } == next with { First = last.First })
                ranges[^1] = last with { Last = column };
            else
                ranges.Add(next);
        }
        return ranges;
    }
}
