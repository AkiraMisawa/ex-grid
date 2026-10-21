using ExGrid.Finding;
using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The reference Replace (ADR-0332 §2 and §3): what one cell's replacement is, and what a Replace All
/// over the rows writes or refuses — all or nothing, uncapped, under the version it was read in.
/// Pinned exhaustively, as Find's is, because a remote Source is held to it.
/// </summary>
public class GridReplaceTests
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

    private static Func<Trade, string>? EntryOf(string column) => column switch
    {
        "Book" => t => t.Book,
        "Trader" => t => t.Trader,
        "Notional" => t => t.Notional.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => null,
    };

    // The Consumer's write: a new instance with the field changed, refusing a Notional that does not parse.
    private static GridReplaceWrite<Trade> Write(Trade row, string column, string text) => column switch
    {
        "Book" => GridReplaceWrite<Trade>.To(row with { Book = text }),
        "Trader" => GridReplaceWrite<Trade>.To(row with { Trader = text }),
        "Notional" => decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var n)
            ? GridReplaceWrite<Trade>.To(row with { Notional = n })
            : GridReplaceWrite<Trade>.Refuse("not a number"),
        _ => GridReplaceWrite<Trade>.Refuse("no such column"),
    };

    private static GridReplaceRequest Ask(
        string text, string replacement, bool matchCase = false, bool wholeCell = false,
        IReadOnlyList<SelectionRange>? scope = null, string[]? columns = null, string[]? editable = null, int version = 0) => new()
        {
            Text = text,
            Replacement = replacement,
            MatchCase = matchCase,
            WholeCell = wholeCell,
            Scope = scope,
            Columns = columns ?? Columns,
            EditableColumns = editable ?? Columns,
            RowSequenceVersion = version,
        };

    private static GridReplaceAnswer<Trade> Plan(GridReplaceRequest request, IReadOnlyList<Trade>? rows = null, int version = 0)
        => GridReplace.Plan(rows ?? Rows, version, request, EntryOf, Write);

    // ---- One cell ----

    [Fact] // ADR-0332 §2: every occurrence in the entry is replaced, compared OrdinalIgnoreCase
    public void Every_occurrence_is_replaced_ignoring_case()
        => Assert.Equal("x-x-c", GridReplace.Replaced("a-A-c", new GridFindPattern("a", false, false), "x"));

    [Fact] // ADR-0332 §2 / ADR-0023: with Match case, only the occurrences of that case
    public void Match_case_replaces_only_that_case()
        => Assert.Equal("x-A-c", GridReplace.Replaced("a-A-c", new GridFindPattern("a", true, false), "x"));

    [Fact] // ADR-0332 §2: with Whole cell, an equal entry is replaced whole and any other is no match
    public void Whole_cell_replaces_the_entry_whole()
    {
        Assert.Equal("Rossi", GridReplace.Replaced("NOVAK", new GridFindPattern("novak", false, true), "Rossi"));
        Assert.Null(GridReplace.Replaced("Novak & co", new GridFindPattern("novak", false, true), "Rossi"));
        Assert.Null(GridReplace.Replaced("NOVAK", new GridFindPattern("novak", true, true), "Rossi"));
    }

    [Fact] // ADR-0332 §2: an entry that does not match has no replacement
    public void No_match_is_null()
        => Assert.Null(GridReplace.Replaced("Osei", new GridFindPattern("novak", false, false), "x"));

    [Fact] // ADR-0332 §2 / ADR-0055: an empty pattern matches nothing
    public void An_empty_pattern_replaces_nothing()
        => Assert.Null(GridReplace.Replaced("Osei", new GridFindPattern("", false, false), "x"));

    [Fact] // ADR-0332 §2: an empty replacement removes the text
    public void An_empty_replacement_removes_the_text()
        => Assert.Equal("Osi", GridReplace.Replaced("Osei", new GridFindPattern("e", false, false), ""));

    [Fact] // ADR-0332 §2: the rest of the entry keeps its case
    public void The_rest_of_the_entry_keeps_its_case()
        => Assert.Equal("NoVAK", GridReplace.Replaced("NOVAK", new GridFindPattern("o", false, false), "o"));

    // ---- Replace All ----

    [Fact] // ADR-0332 §3: every match in every row and column, counted by cells, each row rewritten once
    public void Every_match_is_replaced_and_counted()
    {
        var answer = Plan(Ask("a", "4"));

        Assert.True(answer.Result.IsReplaced);
        // Book: Alpha, Beta, Gamma, Alpha (4 cells); Trader: Ishikawa, Novak, novak (3); Notional: none.
        Assert.Equal(7, answer.Result.Count);
        Assert.Equal(4, answer.Changes.Count);
        Assert.Equal(new Trade("4lph4", "Ishik4w4", 1_000m), answer.Changes[0].After);
        Assert.Same(Rows[0], answer.Changes[0].Before);
        Assert.Equal(new Trade("Bet4", "Nov4k", 2_500.5m), answer.Changes[1].After);
        Assert.Equal(new Trade("4lph4", "Osei", 1_000m), answer.Changes[3].After);
    }

    [Fact] // ADR-0332 §3: the result's version is the one the rows were read in
    public void The_answer_carries_the_version_read_in()
        => Assert.Equal(5, Plan(Ask("a", "b", version: 5), version: 5).Result.RowSequenceVersion);

    [Fact] // ADR-0332 §3 / ADR-0011: read under another version, nothing is planned, and the grid refuses it
    public void Under_another_version_nothing_is_planned()
    {
        var answer = Plan(Ask("a", "b", version: 3), version: 4);

        Assert.Empty(answer.Changes);
        Assert.Equal(4, answer.Result.RowSequenceVersion);
        Assert.Equal(0, answer.Result.Count);
    }

    [Fact] // ADR-0332 §2: Match case and Whole cell are Find's own options, carried over
    public void Match_case_and_whole_cell_carry_over()
    {
        var cased = Plan(Ask("novak", "Rossi", matchCase: true));
        Assert.Equal(1, cased.Result.Count);
        Assert.Equal("Rossi", cased.Changes.Single().After.Trader);
        Assert.Same(Rows[2], cased.Changes.Single().Before);

        var whole = Plan(Ask("Alpha", "Omega", wholeCell: true));
        Assert.Equal(2, whole.Result.Count);
        Assert.Equal(0, Plan(Ask("Alph", "Omega", wholeCell: true)).Result.Count);
    }

    [Fact] // ADR-0332 §2: the selection as scope; a cell covered by two ranges counts once
    public void A_scope_confines_the_replace_and_counts_a_cell_once()
    {
        SelectionRange[] scope = [new(1, 1, 2, 1), new(2, 1, 1, 1)];
        var answer = Plan(Ask("novak", "Rossi", scope: scope));

        Assert.Equal(2, answer.Result.Count);
        Assert.Equal(["Rossi", "Rossi"], answer.Changes.Select(c => c.After.Trader));
        Assert.Equal(0, Plan(Ask("novak", "Rossi", scope: [new(0, 0, 1, 3)])).Result.Count);
    }

    [Fact] // ADR-0332 §2: what is matched is the entry, not the displayed text
    public void The_entry_is_what_matches()
    {
        // The entry of Notional is its plain number; "2,500.50" is how a format would show it.
        Assert.Equal(0, Plan(Ask("2,500.50", "1")).Result.Count);
        var answer = Plan(Ask("2500.5", "2600"));
        Assert.Equal(1, answer.Result.Count);
        Assert.Equal(2_600m, answer.Changes.Single().After.Notional);
    }

    [Fact] // ADR-0332 §2: a match in a column that is not Editable refuses the whole Replace All, naming it
    public void A_match_in_a_column_not_editable_refuses_everything()
    {
        var answer = Plan(Ask("a", "4", editable: ["Trader", "Notional"]));

        Assert.False(answer.Result.IsReplaced);
        Assert.Empty(answer.Changes);
        Assert.Equal(0, answer.Result.Count);
        Assert.Equal(
            [new(0, "Book", null), new(1, "Book", null), new(2, "Book", null), new(3, "Book", null)],
            answer.Result.RefusedCells);
        Assert.All(answer.Result.RefusedCells, cell => Assert.True(cell.NotEditable));
    }

    [Fact] // ADR-0332 §2: a value the write refuses refuses everything, and every such cell is named with why
    public void A_write_that_refuses_refuses_everything_and_names_every_cell()
    {
        var answer = Plan(Ask("0", "x"));

        Assert.False(answer.Result.IsReplaced);
        Assert.Empty(answer.Changes);
        // Every Notional holds a 0; Book and Trader none.
        Assert.Equal(
            [new(0, "Notional", "not a number"), new(1, "Notional", "not a number"), new(2, "Notional", "not a number"), new(3, "Notional", "not a number")],
            answer.Result.RefusedCells);
    }

    [Fact] // ADR-0332 §3: two matches on one row are written in column order on one new instance
    public void Two_matches_on_a_row_are_one_rewritten_row()
    {
        var rows = new[] { new Trade("Novak books", "Novak", 1m) };
        var answer = Plan(Ask("novak", "Rossi"), rows);

        Assert.Equal(2, answer.Result.Count);
        var change = Assert.Single(answer.Changes);
        Assert.Equal(new Trade("Rossi books", "Rossi", 1m), change.After);
    }

    [Fact] // ADR-0332 §3: a column the Consumer does not know, or one with no entry, is skipped
    public void Unknown_columns_are_skipped()
        => Assert.Equal(2, Plan(Ask("alpha", "x", columns: ["Actions", "Book"], editable: ["Book"])).Result.Count);

    [Fact] // ADR-0332 §3: nothing matched is a count of 0, nothing rewritten
    public void Nothing_matched_is_zero()
    {
        var answer = Plan(Ask("zzz", "x"));
        Assert.True(answer.Result.IsReplaced);
        Assert.Equal(0, answer.Result.Count);
        Assert.Empty(answer.Changes);
    }

    [Fact] // ADR-0332 §2 / ADR-0055: an empty text asks nothing
    public void An_empty_text_replaces_nothing()
        => Assert.Equal(0, Plan(Ask("", "x")).Result.Count);

    [Fact] // ADR-0332 §2 / ADR-0003: a write that returns the instance it was handed is refused by name
    public void A_write_in_place_is_refused_by_name()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            GridReplace.Plan(Rows, 0, Ask("alpha", "x"), EntryOf, (row, _, _) => GridReplaceWrite<Trade>.To(row)));
        Assert.Contains("ADR-0003", error.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0332 §2 / spine 5: no cap — every match is replaced, however many
    public void There_is_no_cap()
    {
        var rows = Enumerable.Range(0, 200_000).Select(i => new Trade("Alpha", "Novak", i)).ToArray();
        var answer = Plan(Ask("novak", "Rossi"), rows);

        Assert.Equal(200_000, answer.Result.Count);
        Assert.Equal(200_000, answer.Changes.Count);
    }

    [Fact] // ADR-0332 §3: a refusal names cells; one that names none is refused by name
    public void A_refusal_names_at_least_one_cell()
        => Assert.Throws<ArgumentException>(() => GridReplaceResult.Refused([], 0));

    // ---- GridSource.From ----

    private static InMemoryGridSource<Trade> SourceOf(IReadOnlyList<Trade> rows, Func<Trade, object>? key = null)
    {
        var source = key is null ? GridSource.From(rows) : GridSource.From(rows, key);
        source.OnColumnsChanged(
        [
            new GridColumn<Trade>("Book", ColumnType.Text, t => t.Book).Info,
            new GridColumn<Trade>("Trader", ColumnType.Text, t => t.Trader).Info,
            new GridColumn<Trade>("Notional", ColumnType.Number, t => t.Notional).Info,
        ]);
        return source;
    }

    [Fact] // ADR-0332 §3: GridSource.From applies every rewritten row as one change, and keeps the order version when nothing moved
    public void The_in_memory_source_applies_the_whole_replace_at_once()
    {
        var source = SourceOf(Rows);
        var changes = 0;
        source.StateChanged += () => changes++;

        var answer = source.Replace(Ask("novak", "Rossi", version: source.RowSequenceVersion), Write);

        Assert.Equal(2, answer.Result.Count);
        Assert.Equal(1, changes);
        Assert.Equal(["Ishikawa", "Rossi", "Rossi", "Osei"], source.Window.Select(t => t.Trader));
        Assert.Equal(0, source.RowSequenceVersion);
        Assert.Same(answer.Changes[0].After, source.Window[1]);
    }

    [Fact] // ADR-0332 §3 / ADR-0011: a replace that reorders a sorted column moves the version once
    public void A_replace_that_reorders_moves_the_version()
    {
        var source = SourceOf(Rows);
        source.OnSortChanged([new SortSpec("Trader", SortDirection.Ascending)]);
        var before = source.RowSequenceVersion;

        source.Replace(Ask("novak", "Aaron", version: before), Write);

        Assert.Equal(before + 1, source.RowSequenceVersion);
        Assert.Equal(["Aaron", "Aaron", "Ishikawa", "Osei"], source.Window.Select(t => t.Trader));
    }

    [Fact] // ADR-0332 §2: a refused Replace All applies nothing to GridSource.From
    public void A_refused_replace_applies_nothing_to_the_source()
    {
        var source = SourceOf(Rows);
        var window = source.Window;

        var answer = source.Replace(Ask("a", "4", editable: ["Trader"]), Write);

        Assert.False(answer.Result.IsReplaced);
        Assert.Same(window, source.Window);
    }

    [Fact] // ADR-0332 §3 / ADR-0011: a request read under another version than the source's applies nothing
    public void A_request_under_another_version_applies_nothing()
    {
        var source = SourceOf(Rows);
        var window = source.Window;

        var answer = source.Replace(Ask("novak", "Rossi", version: 9), Write);

        Assert.Equal(0, answer.Result.Count);
        Assert.NotEqual(9, answer.Result.RowSequenceVersion);
        Assert.Same(window, source.Window);
    }

    [Fact] // ADR-0332 §3 / ADR-0141: under a Row Key, the rewritten rows reach the grid as one user's write
    public void A_keyed_source_takes_the_replace_as_one_write()
    {
        var keyed = SourceOf(
            [new("A", "Novak", 1m), new("B", "novak", 2m), new("C", "Osei", 3m)],
            t => t.Book);
        var changes = 0;
        keyed.StateChanged += () => changes++;

        var answer = keyed.Replace(Ask("novak", "Rossi", version: keyed.RowSequenceVersion), Write);

        Assert.Equal(2, answer.Result.Count);
        Assert.Equal(1, changes);
        Assert.Equal(["Rossi", "Rossi", "Osei"], keyed.Window.Select(t => t.Trader));
    }

    [Fact] // ADR-0332 §2: the entry is the editor's, where the Consumer passes one
    public void The_source_matches_the_entry_it_is_given()
    {
        var source = SourceOf(Rows);
        var answer = source.Replace(Ask("=1000", "=2000", columns: ["Notional"], editable: ["Notional"]),
            (row, _, text) => GridReplaceWrite<Trade>.To(row with { Notional = decimal.Parse(text[1..], System.Globalization.CultureInfo.InvariantCulture) }),
            (row, column) => column == "Notional" ? "=" + row.Notional.ToString(System.Globalization.CultureInfo.InvariantCulture) : null);

        Assert.Equal(2, answer.Result.Count);
        Assert.Equal([2_000m, 2_500.5m, 30m, 2_000m], source.Window.Select(t => t.Notional));
    }
}
