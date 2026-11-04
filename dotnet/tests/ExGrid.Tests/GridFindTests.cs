using ExGrid.Finding;
using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// Find (ADR-0055, FD-7/FD-9) where only C# reaches: the fetching Source's delegate, a Source written
/// before Find, the Focus a step moves, the column info a Source matches by, and the property that the
/// step and the mark judge by one rule. What a match is and the order a step walks, the reference
/// Replace All and the header walk are shared cases (<c>tests/cases/finding</c>, and
/// <c>tests/cases/source/finding.json</c> through <c>GridSource.From</c>), which the Script Core runs too.
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

    [Fact] // ADR-0055 / FD-9: a fetching Source searches only with a find delegate
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

    private static GridFindAllRequest AskAll(string text, int start = 0, int count = 100, int version = 0) => new()
    {
        Text = text,
        Columns = Columns,
        RowSequenceVersion = version,
        Start = start,
        Count = count,
    };

    [Fact] // ADR-0470 section 2: a fetching Source lists through its findAll delegate, which it refuses without find
    public async Task The_fetching_source_lists_through_its_find_all_delegate()
    {
        static ValueTask<GridPage<Trade>> Fetch(GridQuery query, CancellationToken token)
            => ValueTask.FromResult(new GridPage<Trade>([], query.Range.Start, 0));
        static Task<GridFindResult> Find(GridFindRequest request, GridFilter? filter, IReadOnlyList<SortSpec> sorts, CancellationToken token)
            => Task.FromResult(GridFindResult.NotFound);

        using var finding = GridSource.Fetch<Trade>(Fetch, find: Find);
        Assert.False(finding.CanFindAll);
        Assert.False(((IGridSource<Trade>)finding).CanFindAll);

        GridFindAllRequest? asked = null;
        using var listing = GridSource.Fetch<Trade>(Fetch, find: Find, findAll: (request, filter, sorts, token) =>
        {
            asked = request;
            return Task.FromResult(GridFindAllPage.Of(12, [new GridFindAllMatch(3, "Book", "Alpha")], request.RowSequenceVersion));
        });
        Assert.True(listing.CanFindAll);
        var page = await listing.FindAllAsync(AskAll("al", start: 4, count: 1), CancellationToken.None);
        Assert.Equal((12, 3, "Book"), (page.Total, page.Matches[0].Row, page.Matches[0].Column));
        Assert.Equal((4, 1), (asked!.Start, asked.Count));

        var refused = Assert.Throws<ArgumentException>(() => GridSource.Fetch<Trade>(Fetch,
            findAll: (request, filter, sorts, token) => Task.FromResult(GridFindAllPage.Of(0, [], 0))));
        Assert.Contains("find", refused.Message);
    }

    [Fact] // ADR-0470 sections 2 and 3: the in-memory Source lists its displayed texts, a page at a time, naming rows as told
    public async Task The_in_memory_source_lists_every_match_a_page_at_a_time()
    {
        var source = GridSource.From<Trade>(Rows);
        source.OnColumnsChanged([.. Columns.Select(name => new ColumnInfo<Trade>(name, ColumnType.Text, row => TextOf(name)!(row)))]);
        Assert.True(source.CanFindAll);

        var first = await source.FindAllAsync(AskAll("a", count: 3), CancellationToken.None);
        Assert.Equal(7, first.Total);
        Assert.Equal(["0 Book Alpha", "0 Trader Ishikawa", "1 Book Beta"], first.Matches.Select(m => $"{m.Row} {m.Column} {m.Text}"));
        Assert.All(first.Matches, match => Assert.Null(match.RowLabel));
        Assert.Equal(source.RowSequenceVersion, first.RowSequenceVersion);

        source.FindAllRowLabel = trade => $"{trade.Book}/{trade.Trader}";
        var last = await source.FindAllAsync(AskAll("a", start: 6, count: 3), CancellationToken.None);
        Assert.Equal(["Alpha/Osei"], last.Matches.Select(m => m.RowLabel));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => source.FindAllAsync(AskAll("a", start: -1), CancellationToken.None));
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

    [Fact] // ADR-0055 / FD-9: a Source written before Find compiles and says it cannot search
    public async Task A_source_without_find_says_it_cannot()
    {
        IGridSource<Trade> source = new OldSource();
        Assert.False(source.CanFind);
        await Assert.ThrowsAsync<NotSupportedException>(() => source.FindAsync(Ask("x"), CancellationToken.None));
        Assert.False(source.CanFindAll);
        await Assert.ThrowsAsync<NotSupportedException>(() => source.FindAllAsync(AskAll("x"), CancellationToken.None));
    }

    [Fact] // ADR-0055: a step searching the selection moves only the Focus, inside it
    public void Focus_on_moves_the_focus_inside_the_selection()
    {
        var extent = new GridExtent(10, 5);
        var selection = GridSelection.Empty.Click(new(1, 1), extent).ExtendTo(new(4, 3), extent);

        var moved = selection.FocusOn(new(3, 2));

        Assert.Equal(new CellPosition(3, 2), moved.Focus);
        Assert.Equal(selection.Ranges, moved.Ranges);
        // The range holding it becomes the Focus's, and the Extent follows the Focus (ADR-0052):
        // on neither edge of either axis, it is the Focus's own row and column.
        Assert.Equal(selection.FocusRange, moved.FocusRange);
        Assert.Equal(new CellPosition(3, 2), moved.Extent);
        Assert.Equal(new CellPosition(1, 1), selection.FocusOn(new(4, 3)).Extent);
        Assert.Throws<ArgumentOutOfRangeException>(() => selection.FocusOn(new(9, 4)));
    }

    [Fact] // ADR-0055 (2026-09-27): a rebuilt column is the same column to a Source — no requery on repush
    public void A_rebuilt_column_carries_an_equal_info()
    {
        static GridColumn<Trade> Build() => new("Notional", ColumnType.Number, t => t.Notional,
            format: v => ((decimal)v).ToString("N0", System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(Build().Info, Build().Info);
    }

    [Fact] // ADR-0055: the displayed text is one rule — the row paints by it and a Source matches by it
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

    [Fact] // ADR-0332 §4: the step and the mark judge by one rule — the cells a step can land on are exactly the ones the rule marks
    public void The_step_lands_only_on_cells_the_reference_rule_marks()
    {
        string[] texts = ["alpha", "ALPHA", "nova", "Novak", "a", "1,000.00", "et", "beta", "0"];
        foreach (var text in texts)
        {
            foreach (var matchCase in new[] { false, true })
            {
                foreach (var wholeCell in new[] { false, true })
                {
                    var pattern = new GridFindPattern(text, matchCase, wholeCell);
                    var marked = new HashSet<(int, string)>();
                    for (var row = 0; row < Rows.Length; row++)
                    {
                        foreach (var column in Columns)
                        {
                            if (GridFind.Matches(TextOf(column)!(Rows[row]), pattern))
                                marked.Add((row, column));
                        }
                    }
                    // Every step from the start, until one comes back to a cell already reached.
                    var reached = new HashSet<(int, string)>();
                    CellPosition? from = null;
                    for (var i = 0; i < Rows.Length * Columns.Length; i++)
                    {
                        var result = GridFind.Step(Rows, Ask(text, from: from, matchCase: matchCase, wholeCell: wholeCell), TextOf);
                        if (!result.IsFound)
                            break;
                        reached.Add((result.Row, result.Column!));
                        from = new CellPosition(result.Row, Array.IndexOf(Columns, result.Column));
                    }
                    Assert.Equal(marked, reached);
                }
            }
        }
    }

    [Fact] // ADR-0332 §4: a request's pattern is its text and its two options
    public void A_request_carries_its_pattern()
    {
        var request = Ask("nova", from: new(1, 1), backward: true, matchCase: true, wholeCell: true);
        Assert.Equal(new GridFindPattern("nova", true, true), request.Pattern);
    }
}
