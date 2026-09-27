using ExGrid.Finding;
using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The reference Find (ADR-0047, FD-7/FD-9): what a match is, and the order a step walks.
/// Pinned exhaustively because a remote Source is held to it, as to ADR-0023.
/// </summary>
public class GridFindTests
{
    private sealed record Trade(string Book, string Trader, decimal Notional);

    private static readonly Trade[] Rows =
    [
        new("Alpha", "Ishikawa", 1_000m),
        new("Beta", "Novak", 2_500.5m),
        new("Gamma", "novak", 30m),
        new("Alpha", "Osei", 1_000m),
    ];

    private static readonly string[] Columns = ["Book", "Trader", "Notional"];

    private static Func<Trade, string>? TextOf(string column) => column switch
    {
        "Book" => t => t.Book,
        "Trader" => t => t.Trader,
        // Displayed with a thousands separator and two places — the text a user reads.
        "Notional" => t => t.Notional.ToString("N2", System.Globalization.CultureInfo.InvariantCulture),
        _ => null,
    };

    private static GridFindRequest Ask(
        string text, CellPosition? from = null, bool backward = false, bool matchCase = false,
        bool wholeCell = false, IReadOnlyList<SelectionRange>? scope = null, string[]? columns = null) => new()
        {
            Text = text,
            From = from,
            Backward = backward,
            MatchCase = matchCase,
            WholeCell = wholeCell,
            Scope = scope,
            Columns = columns ?? Columns,
            RowSequenceVersion = 0,
        };

    private static (int Row, string? Column) Find(GridFindRequest request)
    {
        var result = GridFind.Step(Rows, request, TextOf);
        return (result.Row, result.Column);
    }

    [Fact] // ADR-0047 / FD-7: by rows — across a row in column order, then down
    public void A_step_walks_by_rows_from_the_cell_after_from()
    {
        Assert.Equal((1, "Trader"), Find(Ask("nova", from: new(0, 2))));
        Assert.Equal((2, "Trader"), Find(Ask("nova", from: new(1, 1))));
    }

    [Fact] // ADR-0047 / FD-7: with no Focus, from the first cell
    public void With_no_from_the_walk_starts_at_the_first_cell()
        => Assert.Equal((0, "Book"), Find(Ask("alpha")));

    [Fact] // ADR-0047 / FD-7: past the last cell the walk wraps to the first
    public void The_walk_wraps()
        => Assert.Equal((0, "Book"), Find(Ask("alpha", from: new(3, 0))));

    [Fact] // ADR-0047 / FD-7: the cell From names is considered last, so a lone match finds itself
    public void A_lone_match_finds_itself()
        => Assert.Equal((1, "Book"), Find(Ask("beta", from: new(1, 0))));

    [Fact] // ADR-0047 / FD-7: backward walks the other way and wraps too
    public void Backward_walks_the_other_way()
    {
        Assert.Equal((0, "Book"), Find(Ask("alpha", from: new(3, 0), backward: true)));
        Assert.Equal((3, "Book"), Find(Ask("alpha", from: new(0, 0), backward: true)));
        Assert.Equal((3, "Book"), Find(Ask("alpha", backward: true)));
    }

    [Fact] // ADR-0047 / ADR-0023: OrdinalIgnoreCase unless MatchCase
    public void Case_is_ignored_unless_asked_for()
    {
        Assert.Equal((1, "Trader"), Find(Ask("NOVAK")));
        Assert.Equal((2, "Trader"), Find(Ask("novak", matchCase: true)));
        Assert.Equal((-1, null), Find(Ask("NOVAK", matchCase: true)));
    }

    [Fact] // ADR-0047 / FD-7: containment unless WholeCell
    public void Containment_unless_whole_cell()
    {
        Assert.Equal((1, "Book"), Find(Ask("et")));
        Assert.Equal((-1, null), Find(Ask("et", wholeCell: true)));
        Assert.Equal((1, "Book"), Find(Ask("BETA", wholeCell: true)));
    }

    [Fact] // ADR-0047: what is matched is the displayed text, never the raw value
    public void The_displayed_text_is_what_matches()
    {
        Assert.Equal((1, "Notional"), Find(Ask("2,500.50")));
        Assert.Equal((-1, null), Find(Ask("2500.5")));
    }

    [Fact] // ADR-0047 / FD-7: within the scope when there is one; a cell covered twice counts once
    public void A_scope_confines_the_walk()
    {
        SelectionRange[] scope = [new(2, 0, 2, 2), new(3, 0, 1, 1)];
        Assert.Equal((3, "Book"), Find(Ask("alpha", scope: scope)));
        Assert.Equal((-1, null), Find(Ask("ishikawa", scope: scope)));
    }

    [Fact] // ADR-0047: a column with no text, or one the Source does not know, is skipped
    public void Unknown_columns_are_skipped()
        => Assert.Equal((1, "Book"), Find(Ask("beta", columns: ["Actions", "Book"])));

    [Fact] // ADR-0047: an empty text asks nothing
    public void An_empty_text_finds_nothing()
        => Assert.False(GridFind.Step(Rows, Ask(""), TextOf).IsFound);

    [Fact] // ADR-0047 / FD-7: the in-memory Source matches the columns' displayed text as the grid handed it over
    public async Task The_in_memory_source_searches_the_result_in_its_order()
    {
        var source = GridSource.From(Rows);
        source.OnColumnsChanged(
        [
            new GridColumn<Trade>("Book", ColumnType.Text, t => t.Book).Info,
            new GridColumn<Trade>("Notional", ColumnType.Number, t => t.Notional,
                format: v => ((decimal)v).ToString("N0", System.Globalization.CultureInfo.InvariantCulture)).Info,
        ]);
        source.OnSortChanged([new SortSpec("Book", SortDirection.Descending)]);

        Assert.True(source.CanFind);
        var result = await source.FindAsync(Ask("1,000", columns: ["Book", "Notional"]), CancellationToken.None);

        // Sorted descending: Gamma, Beta, Alpha, Alpha — the first 1,000 is row 2.
        Assert.Equal(2, result.Row);
        Assert.Equal("Notional", result.Column);
    }

    [Fact] // ADR-0047 / FD-9: a fetching Source searches only with a find delegate
    public async Task The_fetching_source_finds_through_its_delegate()
    {
        static ValueTask<GridPage<Trade>> Fetch(GridQuery query, CancellationToken token)
            => ValueTask.FromResult(new GridPage<Trade>([], query.Range.Start, 0));

        using var without = GridSource.Fetch<Trade>(Fetch);
        Assert.False(without.CanFind);
        Assert.False(((IGridSource<Trade>)without).CanFind);

        GridFindRequest? asked = null;
        using var with = GridSource.Fetch<Trade>(Fetch, find: (request, filter, sorts, token) =>
        {
            asked = request;
            return Task.FromResult(GridFindResult.Found(7, "Book"));
        });
        Assert.True(with.CanFind);
        var result = await with.FindAsync(Ask("x"), CancellationToken.None);
        Assert.Equal(7, result.Row);
        Assert.Equal("x", asked!.Text);
    }

    private sealed class OldSource : IGridSource<Trade>
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
        public Task<IReadOnlyList<Trade>> GetRowsAsync(RowRange range, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<Trade>>([]);
        public Task<Chrome.DistinctValues> GetDistinctValuesAsync(string column, CancellationToken cancellationToken)
            => Task.FromResult(Chrome.DistinctValues.TooMany);
    }

    [Fact] // ADR-0047 / FD-9: a Source written before Find compiles and says it cannot search
    public async Task A_source_without_find_says_it_cannot()
    {
        IGridSource<Trade> source = new OldSource();
        Assert.False(source.CanFind);
        await Assert.ThrowsAsync<NotSupportedException>(() => source.FindAsync(Ask("x"), CancellationToken.None));
    }

    [Fact] // ADR-0047: a step searching the selection moves only the Focus, inside it
    public void Focus_on_moves_the_focus_inside_the_selection()
    {
        var extent = new GridExtent(10, 5);
        var selection = GridSelection.Empty.Click(new(1, 1), extent).ExtendTo(new(4, 3), extent);

        var moved = selection.FocusOn(new(3, 2));

        Assert.Equal(new CellPosition(3, 2), moved.Focus);
        Assert.Equal(selection.Ranges, moved.Ranges);
        Assert.Equal(selection.Anchor, moved.Anchor);
        Assert.Throws<ArgumentOutOfRangeException>(() => selection.FocusOn(new(9, 4)));
    }

    [Fact] // ADR-0047 (2026-09-27): a rebuilt column is the same column to a Source — no requery on repush
    public void A_rebuilt_column_carries_an_equal_info()
    {
        static GridColumn<Trade> Build() => new("Notional", ColumnType.Number, t => t.Notional,
            format: v => ((decimal)v).ToString("N0", System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(Build().Info, Build().Info);
    }

    [Fact] // ADR-0047: the displayed text is one rule — the row paints by it and a Source matches by it
    public void The_info_answers_the_displayed_text()
    {
        var formatted = new GridColumn<Trade>("Notional", ColumnType.Number, t => t.Notional,
            format: v => ((decimal)v).ToString("N0", System.Globalization.CultureInfo.InvariantCulture)).Info;
        var plain = new GridColumn<Trade>("Book", ColumnType.Text, t => t.Book).Info;
        var blank = new GridColumn<Trade>("Nothing", ColumnType.Text, _ => null).Info;

        Assert.Equal("1,000", formatted.TextOf(Rows[0]));
        Assert.Equal("Beta", plain.TextOf(Rows[1]));
        Assert.Equal("", blank.TextOf(Rows[1]));
    }

    [Fact] // ADR-0047 / FD-7: GridSource.From answers every clause exactly as the reference step does
    public async Task The_in_memory_source_agrees_with_the_reference_on_every_clause()
    {
        var source = GridSource.From(Rows);
        source.OnColumnsChanged(
        [
            new GridColumn<Trade>("Book", ColumnType.Text, t => t.Book).Info,
            new GridColumn<Trade>("Trader", ColumnType.Text, t => t.Trader).Info,
            new GridColumn<Trade>("Notional", ColumnType.Number, t => t.Notional,
                format: v => ((decimal)v).ToString("N2", System.Globalization.CultureInfo.InvariantCulture)).Info,
        ]);
        GridFindRequest[] clauses =
        [
            Ask("nova", from: new(0, 2)),                       // by rows, from the cell after
            Ask("alpha"),                                       // no From: the first cell
            Ask("alpha", from: new(3, 0)),                      // wrapping
            Ask("beta", from: new(1, 0)),                       // a lone match finds itself
            Ask("alpha", from: new(0, 0), backward: true),      // backward, wrapping
            Ask("NOVAK", matchCase: true),                      // case
            Ask("et", wholeCell: true),                         // whole cell
            Ask("2,500.50"),                                    // displayed text
            Ask("alpha", scope: [new(2, 0, 2, 2), new(3, 0, 1, 1)]), // scope
        ];

        foreach (var request in clauses)
        {
            var expected = GridFind.Step(Rows, request, TextOf);
            var actual = await source.FindAsync(request, CancellationToken.None);
            Assert.Equal((expected.Row, expected.Column), (actual.Row, actual.Column));
        }
    }
}
