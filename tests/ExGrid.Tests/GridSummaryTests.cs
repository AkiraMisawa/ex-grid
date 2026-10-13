using ExGrid.Data;
using ExGrid.Selection;
using ExGrid.Summarizing;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The reference Selection Summary (ADR-0130, SM-4/SM-5/SM-6): what each figure is over which
/// cells. Pinned because a remote Source is held to it, as to ADR-0023 and ADR-0055.
/// </summary>
public class GridSummaryTests
{
    private sealed record Trade(string Book, decimal? Notional, double Rate, DateTime Date, bool Live);

    private static readonly Trade[] Rows =
    [
        new("Alpha", 1_000m, 0.5, new DateTime(2026, 1, 1), true),
        new("Beta", 2_500.25m, 1.5, new DateTime(2026, 1, 2), false),
        new("Gamma", null, 2.0, new DateTime(2026, 1, 3), true),
        new("Delta", 30m, 4.0, new DateTime(2026, 1, 4), false),
    ];

    private static readonly string[] Columns = ["Book", "Notional", "Rate", "Date", "Live"];

    private static Func<Trade, object?>? ValueOf(string column) => column switch
    {
        "Book" => t => t.Book,
        "Notional" => t => t.Notional,
        "Rate" => t => t.Rate,
        "Date" => t => t.Date,
        "Live" => t => t.Live,
        _ => null,
    };

    private static GridSummaryRequest Ask(SummaryFigures figures, params SelectionRange[] ranges) => new()
    {
        Ranges = ranges,
        Columns = Columns,
        RowSequenceVersion = 0,
        Figures = figures,
    };

    private static GridSummaryResult Summarize(GridSummaryRequest request) => GridSummary.Of(Rows, request, ValueOf);

    [Fact] // ADR-0130 / SM-6: money is summed exactly; a blank is in no figure
    public void ADR0130_a_number_column_is_summed_exactly_and_a_blank_is_not_counted()
    {
        var result = Summarize(Ask(SummaryFigures.All, new SelectionRange(0, 1, 4, 1)));

        Assert.Equal(3_530.25m, result[SummaryFigures.Sum]!.Value.Exact);
        Assert.Equal(3m, result[SummaryFigures.Count]!.Value.Exact);
        Assert.Equal(3m, result[SummaryFigures.NumericalCount]!.Value.Exact);
        Assert.Equal(1_176.75m, result[SummaryFigures.Average]!.Value.Exact);
        Assert.Equal(30m, result[SummaryFigures.Min]!.Value.Exact);
        Assert.Equal(2_500.25m, result[SummaryFigures.Max]!.Value.Exact);
    }

    [Fact] // ADR-0130 / SM-6: text, Booleans and dates are counted and never a number
    public void ADR0130_text_booleans_and_dates_are_counted_only()
    {
        var result = Summarize(Ask(SummaryFigures.All, new SelectionRange(0, 0, 2, 1), new SelectionRange(0, 3, 2, 2)));

        Assert.Equal(6m, result[SummaryFigures.Count]!.Value.Exact);
        Assert.Equal(0m, result[SummaryFigures.NumericalCount]!.Value.Exact);
        // No number among the cells: only Count is shown, as in Excel's status bar.
        Assert.True(result[SummaryFigures.Sum]!.Value.IsEmpty);
        Assert.True(result[SummaryFigures.Average]!.Value.IsEmpty);
        Assert.True(result[SummaryFigures.Min]!.Value.IsEmpty);
    }

    [Fact] // ADR-0130 / SM-6: a cell covered by two ranges counts once
    public void ADR0130_a_cell_under_two_ranges_counts_once()
    {
        var result = Summarize(Ask(SummaryFigures.Sum | SummaryFigures.Count,
            new SelectionRange(0, 1, 2, 1), new SelectionRange(1, 1, 3, 1)));

        Assert.Equal(3_530.25m, result[SummaryFigures.Sum]!.Value.Exact);
        Assert.Equal(3m, result[SummaryFigures.Count]!.Value.Exact);
    }

    [Fact] // ADR-0130 / SM-6: a double leaves exactness for Excel's arithmetic
    public void ADR0130_doubles_are_summed_in_double()
    {
        var result = Summarize(Ask(SummaryFigures.Sum | SummaryFigures.Average, new SelectionRange(0, 2, 4, 1)));

        Assert.Null(result[SummaryFigures.Sum]!.Value.Exact);
        Assert.Equal(8d, result[SummaryFigures.Sum]!.Value.Number);
        Assert.Equal(2d, result[SummaryFigures.Average]!.Value.Number);
    }

    [Fact] // ADR-0130 / SM-6: a column with no value — an Action Column — is blank
    public void ADR0130_a_column_with_no_value_is_blank()
    {
        var request = Ask(SummaryFigures.Count, new SelectionRange(0, 0, 4, 2)) with { Columns = ["Actions", "Notional"] };

        var result = GridSummary.Of(Rows, request, ValueOf);

        Assert.Equal(3m, result[SummaryFigures.Count]!.Value.Exact);
    }

    [Fact] // ADR-0130 / SM-1: only the figures asked are answered
    public void ADR0130_only_the_figures_asked_are_answered()
    {
        var result = Summarize(Ask(SummaryFigures.Sum, new SelectionRange(0, 1, 4, 1)));

        Assert.Equal(SummaryFigures.Sum, result.Figures);
        Assert.Null(result[SummaryFigures.Count]);
    }

    [Fact] // ADR-0130 / SM-4: a whole column of a million rows is summed, never declined
    public void ADR0130_a_million_rows_are_summed()
    {
        var rows = new decimal[1_000_000];
        Array.Fill(rows, 0.01m);

        var result = GridSummary.Of(rows,
            new GridSummaryRequest { Ranges = [new SelectionRange(0, 0, rows.Length, 1)], Columns = ["V"], RowSequenceVersion = 0, Figures = SummaryFigures.Sum },
            _ => v => v);

        Assert.False(result.IsDeclined);
        Assert.Equal(10_000m, result[SummaryFigures.Sum]!.Value.Exact);
    }

    [Fact] // ADR-0130 / SM-4: the in-memory Source summarises a whole column of a million rows, never declining
    public async Task ADR0130_the_in_memory_source_never_declines()
    {
        var rows = Enumerable.Range(0, 1_000_000).Select(i => new Trade("B", 0.01m, 0, default, false)).ToArray();
        var source = GridSource.From(rows);
        source.OnColumnsChanged([new GridColumn<Trade>("Notional", ColumnType.Number, t => t.Notional).Info]);

        var result = await source.SummarizeAsync(
            new GridSummaryRequest { Ranges = [new SelectionRange(0, 0, rows.Length, 1)], Columns = ["Notional"], RowSequenceVersion = 0, Figures = SummaryFigures.Sum },
            CancellationToken.None);

        Assert.False(result.IsDeclined);
        Assert.Equal(10_000m, result[SummaryFigures.Sum]!.Value.Exact);
    }

    [Fact] // ADR-0130 / SM-6: a Template Column contributes its value accessor's answer, never its markup
    public async Task ADR0130_a_template_column_is_summed_by_its_value()
    {
        var source = GridSource.From(Rows);
        source.OnColumnsChanged(
        [
            GridColumn<Trade>.TemplateColumn("Shown", ColumnType.Number, t => t.Notional, _ => _ => { }).Info,
        ]);

        var result = await source.SummarizeAsync(
            new GridSummaryRequest { Ranges = [new SelectionRange(0, 0, 4, 1)], Columns = ["Shown"], RowSequenceVersion = 0, Figures = SummaryFigures.Sum },
            CancellationToken.None);

        Assert.Equal(3_530.25m, result[SummaryFigures.Sum]!.Value.Exact);
    }

    [Fact] // SM-6, ADR-0130: a hidden column is never summarised — the request's Columns are the visible ones
    public async Task SM6_a_hidden_column_is_never_summarised()
    {
        // The Source knows all three; the Consumer has hidden Notional, so the grid asks
        // about Book and Rate only, and the range spans both visible columns.
        var source = GridSource.From(Rows);
        source.OnColumnsChanged(
        [
            new GridColumn<Trade>("Book", ColumnType.Text, t => t.Book).Info,
            new GridColumn<Trade>("Notional", ColumnType.Number, t => t.Notional).Info,
            new GridColumn<Trade>("Rate", ColumnType.Number, t => t.Rate).Info,
        ]);

        var result = await source.SummarizeAsync(
            new GridSummaryRequest
            {
                Ranges = [new SelectionRange(0, 0, 4, 2)],
                Columns = ["Book", "Rate"],
                RowSequenceVersion = 0,
                Figures = SummaryFigures.Count | SummaryFigures.NumericalCount | SummaryFigures.Max,
            },
            CancellationToken.None);

        // Four Books and four Rates; Notional's three numbers and its 2,500.25 maximum are absent.
        Assert.Equal(8m, result[SummaryFigures.Count]!.Value.Exact);
        Assert.Equal(4m, result[SummaryFigures.NumericalCount]!.Value.Exact);
        Assert.Equal(4d, result[SummaryFigures.Max]!.Value.Number);
    }

    [Fact] // ADR-0130 / SM-6: the in-memory Source summarises the result in its order
    public async Task ADR0130_the_in_memory_source_summarises_the_result_in_its_order()
    {
        var source = GridSource.From(Rows);
        source.OnColumnsChanged(
        [
            new GridColumn<Trade>("Book", ColumnType.Text, t => t.Book).Info,
            new GridColumn<Trade>("Notional", ColumnType.Number, t => t.Notional).Info,
        ]);
        source.OnSortChanged([new SortSpec("Book", SortDirection.Ascending)]);

        Assert.True(source.CanSummarize);
        // Sorted: Alpha, Beta, Delta, Gamma — the first two rows' Notional.
        var result = await source.SummarizeAsync(
            new GridSummaryRequest { Ranges = [new SelectionRange(0, 1, 2, 1)], Columns = ["Book", "Notional"], RowSequenceVersion = 0, Figures = SummaryFigures.Sum },
            CancellationToken.None);

        Assert.Equal(3_500.25m, result[SummaryFigures.Sum]!.Value.Exact);
    }

    [Fact] // ADR-0130 / SM-5: a fetching Source summarises only with a summarize delegate
    public async Task ADR0130_the_fetching_source_summarises_through_its_delegate()
    {
        static ValueTask<GridPage<Trade>> Fetch(GridQuery query, CancellationToken token)
            => ValueTask.FromResult(new GridPage<Trade>([], query.Range.Start, 0));

        using var without = GridSource.Fetch<Trade>(Fetch);
        Assert.False(without.CanSummarize);
        Assert.False(((IGridSource<Trade>)without).CanSummarize);

        GridSummaryRequest? asked = null;
        using var with = GridSource.Fetch<Trade>(Fetch, summarize: (request, filter, sorts, token) =>
        {
            asked = request;
            return Task.FromResult(GridSummaryResult.Declined("Too many rows to sum on the server."));
        });
        Assert.True(with.CanSummarize);
        var result = await with.SummarizeAsync(Ask(SummaryFigures.Sum, new SelectionRange(0, 0, 1, 1)), CancellationToken.None);
        Assert.True(result.IsDeclined);
        Assert.Equal("Too many rows to sum on the server.", result.DeclineReason);
        Assert.Equal(SummaryFigures.Sum, asked!.Figures);
    }

    [Fact] // ADR-0130 / SM-5: a Source written before the Selection Summary compiles and cannot summarise
    public async Task ADR0130_an_existing_source_reports_that_it_cannot_summarise()
    {
        IGridSource<Trade> source = new BareSource();

        Assert.False(source.CanSummarize);
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            source.SummarizeAsync(Ask(SummaryFigures.Sum, new SelectionRange(0, 0, 1, 1)), CancellationToken.None));
    }

    [Fact] // ADR-0130 / SM-7: an error among the cells leaves Count and Numerical Count, as Excel's status bar (run 17, b5)
    public void ADR0130_an_error_among_the_cells_leaves_count_and_numerical_count()
    {
        // Excel's b5: 1, 2, #DIV/0!, x and a blank show "Count: 4    Numerical Count: 2" and nothing else.
        var cells = new GridSummaryCells();
        cells.AddNumber(1m);
        cells.AddNumber(2d);
        cells.AddError();
        cells.AddOther();
        cells.Add(null);

        var result = cells.Answer(SummaryFigures.All, figure => "text " + figure);

        Assert.Equal(SummaryFigures.All, result.Figures);
        Assert.Equal(4m, result[SummaryFigures.Count]!.Value.Exact);
        Assert.Equal(2m, result[SummaryFigures.NumericalCount]!.Value.Exact);
        foreach (var figure in new[] { SummaryFigures.Average, SummaryFigures.Min, SummaryFigures.Max, SummaryFigures.Sum })
        {
            Assert.False(result[figure]!.Value.IsNumber);
            Assert.Null(result.TextOf(figure));
        }
        // The counts are the grid's whole numbers, never an answerer's formatted text.
        Assert.Null(result.TextOf(SummaryFigures.NumericalCount));
    }

    [Fact] // ADR-0130 / SM-7: once the error is gone, every figure is back (run 17, b6, SM-7's control)
    public void ADR0130_without_the_error_every_figure_is_shown()
    {
        var cells = new GridSummaryCells();
        cells.AddNumber(1d);
        cells.AddNumber(2d);
        cells.AddOther();

        var result = cells.Answer(SummaryFigures.All);

        // Excel's b6: "Average: 1.5    Count: 3    Numerical Count: 2    Min.: 1    Max.: 2    Sum: 3".
        Assert.Equal(1.5d, result[SummaryFigures.Average]!.Value.Number);
        Assert.Equal(3m, result[SummaryFigures.Count]!.Value.Exact);
        Assert.Equal(2m, result[SummaryFigures.NumericalCount]!.Value.Exact);
        Assert.Equal(1d, result[SummaryFigures.Min]!.Value.Number);
        Assert.Equal(2d, result[SummaryFigures.Max]!.Value.Number);
        Assert.Equal(3d, result[SummaryFigures.Sum]!.Value.Number);
    }

    [Fact] // ADR-0130: a text for a figure that was not answered is refused by name
    public void ADR0130_a_text_without_an_answer_is_refused()
        => Assert.Throws<ArgumentException>(() => GridSummaryResult.Answered(
            new Dictionary<SummaryFigures, AggregateResult> { [SummaryFigures.Sum] = AggregateResult.Of(1m) },
            new Dictionary<SummaryFigures, string> { [SummaryFigures.Average] = "1" }));

    [Fact] // ADR-0130: an answer names single figures, never a combination
    public void ADR0130_an_answer_names_single_figures()
        => Assert.Throws<ArgumentException>(() => GridSummaryResult.Answered(
            new Dictionary<SummaryFigures, AggregateResult> { [SummaryFigures.Default] = AggregateResult.Empty }));

    private sealed class BareSource : IGridSource<Trade>
    {
        public IReadOnlyList<Trade> Window => [];
        public int WindowStart => 0;
        public int? TotalCount => 0;
        public bool IsLoading => false;
        public int RowSequenceVersion => 0;
        public IReadOnlyList<SortSpec> Sorts => [];
        public GridFilter? Filter => null;
        public event Action? StateChanged { add { } remove { } }
        public void OnSortChanged(IReadOnlyList<SortSpec> sorts) { }
        public void OnFilterChanged(GridFilter? filter) { }
        public void OnColumnsChanged(IReadOnlyList<ColumnInfo<Trade>> columns) { }
        public Task OnRangeNeededAsync(RowRange range) => Task.CompletedTask;
        public Task<Chrome.DistinctValues> GetDistinctValuesAsync(string column, CancellationToken cancellationToken)
            => Task.FromResult(Chrome.DistinctValues.TooMany);
        public Task<IReadOnlyList<Trade>> GetRowsAsync(RowRange range, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<Trade>>([]);
    }
}
