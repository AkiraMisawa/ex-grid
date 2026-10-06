using System.Globalization;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// <c>GridSource.Fetch</c> hears that the data moved on (ADR-0141, LV-8): told so, it reads its
/// Window again — gathered on its clock by the rules <c>GridSource.From</c> gathers by (LV-6) —
/// pairs the answer's rows with the painted ones by Row Key, marks the cells whose painted text
/// changed (LV-9), refuses an answer that repeats a key (LV-10), and moves the Row Sequence
/// Version when the server's order token differs, or with every change when it sends none. The
/// server is a fake that answers from a list it holds; the clock is a fake the tests drive.
/// </summary>
public class LiveFetchTests
{
    private sealed record Deal(string Id, string Book, decimal Amount);

    private static readonly GridColumn<Deal> IdColumn = new("Id", ColumnType.Text, d => d.Id);
    private static readonly GridColumn<Deal> BookColumn = new("Book", ColumnType.Text, d => d.Book);
    private static readonly GridColumn<Deal> AmountColumn = new("Amount", ColumnType.Number, d => d.Amount,
        format: value => ((decimal)value).ToString("0", CultureInfo.InvariantCulture));

    private static readonly IReadOnlyList<ColumnInfo<Deal>> Columns = [IdColumn.Info, BookColumn.Info, AmountColumn.Info];

    /// <summary>A server holding its rows in order, answering a range of them with its order token.
    /// Held, it keeps each question until the test answers it.</summary>
    private sealed class Server
    {
        public Server(int rows)
        {
            for (var i = 0; i < rows; i++)
                Rows.Add(new Deal($"T{i:D4}", "Rates", i));
        }

        public List<Deal> Rows { get; } = [];

        public string? Token { get; set; } = "order-0";

        public bool SendsToken { get; set; } = true;

        /// <summary>Every answer's rows as new instances, as rows read from JSON are.</summary>
        public bool Fresh { get; set; }

        public bool Holds { get; set; }

        public List<GridQuery> Asked { get; } = [];

        public List<(GridQuery Query, TaskCompletionSource<GridPage<Deal>> Answer)> Held { get; } = [];

        public ValueTask<GridPage<Deal>> Fetch(GridQuery query, CancellationToken cancellation)
        {
            Asked.Add(query);
            if (!Holds)
                return ValueTask.FromResult(Answer(query));
            var answer = new TaskCompletionSource<GridPage<Deal>>();
            Held.Add((query, answer));
            return new ValueTask<GridPage<Deal>>(answer.Task);
        }

        public GridPage<Deal> Answer(GridQuery query)
        {
            var start = Math.Min(query.Range.Start, Rows.Count);
            var count = Math.Min(query.Range.Count, Rows.Count - start);
            var rows = Rows.Skip(start).Take(count).Select(r => Fresh ? r with { } : r).ToArray();
            return new GridPage<Deal>(rows, start, Rows.Count) { OrderToken = SendsToken ? Token : null };
        }

        public void AnswerHeld()
        {
            var held = Held.ToArray();
            Held.Clear();
            foreach (var (query, answer) in held)
                answer.SetResult(Answer(query));
        }

        public void Change(int at, Func<Deal, Deal> change) => Rows[at] = change(Rows[at]);

        /// <summary>One row removed and one added at the end: the total stays, the order moves.</summary>
        public void CancelAndBook(int at, string booked)
        {
            Rows.RemoveAt(at);
            Rows.Add(new Deal(booked, "FX", 1));
            Token = $"order-{booked}";
        }
    }

    private static FetchingGridSource<Deal> Live(Server server, FakeTimeProvider clock)
    {
        var source = GridSource.Fetch<Deal>(server.Fetch, readAheadRows: 0, rowKey: d => d.Id, clock: clock);
        source.OnColumnsChanged(Columns);
        return source;
    }

    private static string Ids(IGridSource<Deal> source, int take = 5) => string.Join(",", source.Window.Take(take).Select(d => d.Id));

    [Fact] // ADR-0141 / LV-8: told the data moved on with no Row Key, the source refuses by name
    public void Without_a_key_a_notice_is_refused_by_name()
    {
        var server = new Server(10);
        var source = GridSource.Fetch<Deal>(server.Fetch);

        var refusal = Assert.Throws<InvalidOperationException>(source.NotifyChanged);

        Assert.Contains("Row Key", refusal.Message);
        Assert.Null(source.CellChangedAt);
        Assert.False(((IGridSource<Deal>)source).VouchesDistinctRows);
    }

    [Fact] // ADR-0141 / LV-8: told the data moved on, it reads the Window again and pairs its rows by key
    public void A_notice_reads_the_Window_again()
    {
        var server = new Server(300);
        var source = Live(server, new FakeTimeProvider());
        Assert.Equal(100, source.Window.Count);
        server.Change(3, d => d with { Amount = 77 });

        source.NotifyChanged();

        Assert.Equal(2, server.Asked.Count);
        Assert.Equal(new RowRange(0, 100), server.Asked[^1].Range);
        Assert.Equal(77m, source.Window[3].Amount);
    }

    [Fact] // ADR-0141 / LV-8/LV-9: the cells whose painted text changed are marked; the rest are not
    public void A_notice_marks_the_cells_that_changed()
    {
        var clock = new FakeTimeProvider();
        var server = new Server(300) { Fresh = true };
        var source = Live(server, clock);
        var changedAt = source.CellChangedAt!;
        clock.Advance(TimeSpan.FromSeconds(5));
        var now = clock.GetUtcNow();
        server.Change(3, d => d with { Amount = 77 });
        // A change the format hides: 4 becomes 4.2, painted "4" either way.
        server.Change(4, d => d with { Amount = 4.2m });

        source.NotifyChanged();

        Assert.Equal(now, changedAt(source.Window[3], AmountColumn));
        Assert.Null(changedAt(source.Window[3], BookColumn));
        Assert.Null(changedAt(source.Window[4], AmountColumn));
        // Every row is a new instance, as from JSON: only the painted text decides.
        Assert.Null(changedAt(source.Window[5], AmountColumn));
    }

    [Fact] // ADR-0141 / LV-8 / ADR-0011: an answer whose order token differs moves the version
    public void A_differing_order_token_moves_the_version()
    {
        var server = new Server(300);
        var source = Live(server, new FakeTimeProvider());
        var version = source.RowSequenceVersion;

        // A trade cancelled after the Window, and another booked at the end: the Window's rows are the
        // same, and only the server knows the order moved.
        server.CancelAndBook(250, "T9999");
        source.NotifyChanged();

        Assert.Equal("T0000,T0001,T0002,T0003,T0004", Ids(source));
        Assert.Equal(version + 1, source.RowSequenceVersion);
    }

    [Fact] // ADR-0141 / LV-8: the same token keeps the version, so a change of values keeps the Selection
    public void The_same_order_token_keeps_the_version()
    {
        var server = new Server(300);
        var source = Live(server, new FakeTimeProvider());
        var version = source.RowSequenceVersion;

        server.Change(3, d => d with { Amount = 77 });
        source.NotifyChanged();

        Assert.Equal(version, source.RowSequenceVersion);
    }

    [Fact] // ADR-0141 / LV-8: a server that sends no token moves the order with every change
    public void Without_a_token_every_change_moves_the_version()
    {
        var clock = new FakeTimeProvider();
        var server = new Server(300) { SendsToken = false };
        var source = Live(server, clock);
        var version = source.RowSequenceVersion;

        server.Change(3, d => d with { Amount = 77 });
        source.NotifyChanged();
        Assert.Equal(version + 1, source.RowSequenceVersion);

        // A scroll is not a change of data, and moves nothing (ADR-0025).
        _ = source.OnRangeNeededAsync(new RowRange(150, 20));
        Assert.Equal(version + 1, source.RowSequenceVersion);
    }

    [Fact] // ADR-0141 / LV-8: a scroll answer whose token differs moves the version too
    public void A_scroll_answer_with_another_token_moves_the_version()
    {
        var server = new Server(300);
        var source = Live(server, new FakeTimeProvider());
        var version = source.RowSequenceVersion;
        server.CancelAndBook(250, "T9999");

        _ = source.OnRangeNeededAsync(new RowRange(150, 20));

        Assert.Equal(version + 1, source.RowSequenceVersion);
    }

    [Fact] // ADR-0141 / LV-6: the first notice after a quiet interval reads at once; the ones within it, once at its end
    public void Notices_are_gathered()
    {
        var clock = new FakeTimeProvider();
        var server = new Server(300);
        var source = Live(server, clock);
        var asked = server.Asked.Count;

        source.NotifyChanged();
        Assert.Equal(asked + 1, server.Asked.Count);

        clock.Advance(TimeSpan.FromMilliseconds(100));
        source.NotifyChanged();
        source.NotifyChanged();
        clock.Advance(TimeSpan.FromMilliseconds(149));
        Assert.Equal(asked + 1, server.Asked.Count);

        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(asked + 2, server.Asked.Count);

        // Quiet past the interval: at once again.
        clock.Advance(TimeSpan.FromSeconds(1));
        source.NotifyChanged();
        Assert.Equal(asked + 3, server.Asked.Count);
    }

    [Fact] // ADR-0141 / LV-6: the interval is settable, and 0 reads for every notice
    public void The_interval_is_settable()
    {
        var clock = new FakeTimeProvider();
        var server = new Server(300);
        var source = Live(server, clock);
        source.GatherInterval = TimeSpan.Zero;
        var asked = server.Asked.Count;

        source.NotifyChanged();
        source.NotifyChanged();
        source.NotifyChanged();

        Assert.Equal(asked + 3, server.Asked.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => source.GatherInterval = TimeSpan.FromSeconds(-1));
    }

    [Fact] // ADR-0141 / ADR-0067: a question out is never cancelled for a notice; the change is read when it lands
    public void A_question_out_is_not_cancelled_for_a_notice()
    {
        var clock = new FakeTimeProvider();
        var server = new Server(300);
        var source = Live(server, clock);
        server.Holds = true;
        _ = source.OnRangeNeededAsync(new RowRange(150, 20));
        var scroll = Assert.Single(server.Held);

        source.NotifyChanged();
        Assert.Single(server.Held);

        clock.Advance(TimeSpan.FromSeconds(1));
        server.AnswerHeld();
        // The scroll landed and the notice is asked for now, the interval having passed.
        var reread = Assert.Single(server.Held);
        Assert.Equal(new RowRange(150, 20), reread.Query.Range);
        _ = scroll;
    }

    [Fact] // ADR-0141 / ADR-0067: a read for newer data does not raise IsLoading; a scroll still does
    public void A_read_for_newer_data_does_not_flicker_the_loading_indication()
    {
        var server = new Server(300) { Holds = true };
        var source = Live(server, new FakeTimeProvider());
        server.AnswerHeld();
        var loading = new List<bool>();
        source.StateChanged += () => loading.Add(source.IsLoading);

        source.NotifyChanged();
        Assert.False(source.IsLoading);
        server.AnswerHeld();

        Assert.DoesNotContain(true, loading);
    }

    [Fact] // ADR-0141 / LV-10: an answer that repeats a key is refused by name, and the Window stays
    public void An_answer_with_a_repeated_key_is_refused_by_name()
    {
        var server = new Server(10);
        var source = Live(server, new FakeTimeProvider());
        Exception? failure = null;
        source.FetchFailed += error => failure = error;
        server.Rows[5] = server.Rows[2] with { Amount = 9 };

        source.NotifyChanged();

        Assert.NotNull(failure);
        Assert.Contains("'T0002'", failure!.Message);
        Assert.Contains("2 and 5", failure.Message);
        Assert.Equal(10, source.Window.Count);
        Assert.Equal(5m, source.Window[5].Amount);
        Assert.True(((IGridSource<Deal>)source).VouchesDistinctRows);
    }

    [Fact] // ADR-0141 / ADR-0140: the Row Mark adapter's key is a Row Key; a second, different key is refused
    public void The_mark_adapters_key_is_the_row_key()
    {
        var server = new Server(10);
        Func<Deal, object> key = d => d.Id;
        var marks = new Rows.RowMarkAdapter<Deal>(key,
            (_, _) => ValueTask.FromResult<object>(0),
            (_, _) => true,
            (_, _, _) => ValueTask.FromResult(new Rows.RowMarkCounts(0, 0, 0)));

        var source = GridSource.Fetch<Deal>(server.Fetch, marks: marks);
        Assert.Same(key, ((IGridSource<Deal>)source).RowKey);
        Assert.Same(key, GridSource.Fetch<Deal>(server.Fetch, marks: marks, rowKey: key).RowKey);

        Assert.Throws<ArgumentException>(() => GridSource.Fetch<Deal>(server.Fetch, marks: marks, rowKey: d => d.Book));
    }

    [Fact] // ADR-0141 / LV-9: a row that appears between rows painted before is marked whole
    public void A_row_that_appears_between_painted_rows_is_marked_whole()
    {
        var clock = new FakeTimeProvider();
        var server = new Server(300);
        var source = Live(server, clock);
        var changedAt = source.CellChangedAt!;
        var now = clock.GetUtcNow();

        // Two rows cancelled near the top and one booked between T0009 and T0010: every row after
        // moves up one, and T0100 slides in at the Window's foot.
        server.Rows.RemoveAt(2);
        server.Rows.RemoveAt(2);
        server.Rows.Insert(8, new Deal("T0009b", "FX", 5));
        server.Token = "order-1";
        source.NotifyChanged();

        var appeared = source.Window[8];
        Assert.Equal("T0009b", appeared.Id);
        Assert.Equal(now, changedAt(appeared, IdColumn));
        Assert.Equal(now, changedAt(appeared, AmountColumn));
        // The row that slid in at the Window's foot is not marked: it may only have moved.
        Assert.Equal("T0100", source.Window[^1].Id);
        Assert.Null(changedAt(source.Window[^1], IdColumn));
    }

    [Fact] // ADR-0141 / LV-9: past the end of the result both Windows reached, a row added is marked whole
    public void A_row_added_at_a_reached_end_is_marked_whole()
    {
        var clock = new FakeTimeProvider();
        var server = new Server(20);
        var source = Live(server, clock);
        var changedAt = source.CellChangedAt!;

        server.Rows.Add(new Deal("T0100", "FX", 5));
        server.Token = "order-1";
        source.NotifyChanged();

        Assert.Equal(clock.GetUtcNow(), changedAt(source.Window[^1], BookColumn));
    }

    [Fact] // ADR-0141 / LV-9: a mark is kept by key, so it comes back with its row after a scroll away and back
    public void A_mark_comes_back_with_its_row()
    {
        var clock = new FakeTimeProvider();
        var server = new Server(300) { Fresh = true };
        var source = Live(server, clock);
        var changedAt = source.CellChangedAt!;
        server.Change(3, d => d with { Amount = 77 });
        source.NotifyChanged();
        var then = clock.GetUtcNow();

        _ = source.OnRangeNeededAsync(new RowRange(200, 20));
        _ = source.OnRangeNeededAsync(new RowRange(0, 20));

        Assert.Equal(then, changedAt(source.Window[3], AmountColumn));
        Assert.Null(changedAt(source.Window[2], AmountColumn));
    }

    [Fact] // ADR-0141 / LV-9: a sort after a notice brings the change, and marks nothing — the Window was dropped
    public void A_sort_marks_nothing()
    {
        var clock = new FakeTimeProvider();
        var server = new Server(300);
        var source = Live(server, clock);
        var changedAt = source.CellChangedAt!;
        source.NotifyChanged();
        server.Change(3, d => d with { Amount = 77 });
        source.NotifyChanged(); // gathered: within the interval

        source.OnSortChanged([new SortSpec("Amount", SortDirection.Ascending)]);

        Assert.All(source.Window, row => Assert.Null(changedAt(row, AmountColumn)));
        // The gathered notice was carried by the sort's question: nothing more is read at the interval's end.
        var asked = server.Asked.Count;
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(asked, server.Asked.Count);
    }

    // ---- LV-16: nothing to put out without waiting ------------------------------------------------

    [Fact] // ADR-0141/0142 / LV-16: GridSource.Fetch has nothing it could put out without waiting: PublishGathered asks nothing and changes nothing
    public void PublishGathered_on_a_fetching_source_does_nothing()
    {
        var clock = new FakeTimeProvider();
        var server = new Server(300);
        var source = Live(server, clock);
        source.NotifyChanged();
        server.Change(3, d => d with { Amount = 77 });
        // Within the interval: gathered.
        source.NotifyChanged();
        var asked = server.Asked.Count;
        var window = source.Window;
        var events = 0;
        source.StateChanged += () => events++;

        ((IGridSource<Deal>)source).PublishGathered();

        Assert.Equal(asked, server.Asked.Count);
        Assert.Same(window, source.Window);
        Assert.Equal(0, events);
        // The notice is still gathered, and read at the interval's end.
        clock.Advance(TimeSpan.FromMilliseconds(250));
        Assert.Equal(asked + 1, server.Asked.Count);
        Assert.Equal(77m, source.Window[3].Amount);
    }

    // ---- LV-9 (D6): the Consumer names the keys it knows were added ---------------------------------

    /// <summary>A row booked at <paramref name="at"/> of the server's rows, moving the order.</summary>
    private static void Book(Server server, int at, string id)
    {
        server.Rows.Insert(at, new Deal(id, "FX", 5));
        server.Token = $"order-{id}";
    }

    private static void AssertWhole(Cells.CellChangeOf<Deal> changedAt, Deal row, DateTimeOffset at)
    {
        foreach (var column in new[] { IdColumn, BookColumn, AmountColumn })
            Assert.Equal(at, changedAt(row, column));
    }

    private static void AssertUnmarked(Cells.CellChangeOf<Deal> changedAt, Deal row)
    {
        foreach (var column in new[] { IdColumn, BookColumn, AmountColumn })
            Assert.Null(changedAt(row, column));
    }

    [Fact] // ADR-0141 / LV-9 (D6): a key the Consumer names as added is marked whole wherever it lands in the Window, an edge included
    public void A_named_key_is_marked_whole_wherever_it_lands()
    {
        var clock = new FakeTimeProvider();
        var server = new Server(300);
        var source = Live(server, clock);
        var changedAt = source.CellChangedAt!;

        // Booked as the Window's last row: where a row may also have slid in from below, which the
        // source cannot tell apart without being told.
        Book(server, 99, "T0098b");
        source.NotifyChanged(["T0098b"]);

        Assert.Equal("T0098b", source.Window[99].Id);
        AssertWhole(changedAt, source.Window[99], clock.GetUtcNow());
    }

    [Fact] // ADR-0141 / LV-9 (D6): with names given, a key not named is not marked whole — it slid in — and a painted row's cells are compared
    public void With_names_given_a_key_not_named_is_not_marked_whole()
    {
        var clock = new FakeTimeProvider();
        var server = new Server(300);
        var source = Live(server, clock);
        var changedAt = source.CellChangedAt!;

        // Two rows cancelled near the top, a row put between painted rows that the notice does not
        // name, and a value changed: the Consumer says that no key was added.
        server.Rows.RemoveAt(2);
        server.Rows.RemoveAt(2);
        Book(server, 8, "T0009b");
        server.Change(3, d => d with { Amount = 77 });
        source.NotifyChanged([]);

        Assert.Equal("T0009b", source.Window[8].Id);
        AssertUnmarked(changedAt, source.Window[8]);
        Assert.Equal("T0100", source.Window[^1].Id);
        AssertUnmarked(changedAt, source.Window[^1]);
        // T0005 was painted before, and moved up: its cells are compared.
        Assert.Equal("T0005", source.Window[3].Id);
        Assert.Equal(clock.GetUtcNow(), changedAt(source.Window[3], AmountColumn));
        Assert.Null(changedAt(source.Window[3], BookColumn));
    }

    [Fact] // ADR-0141 / LV-9 (D6): a nameless notice keeps the old guess for the keys no notice named, beside a named one
    public void A_nameless_notice_keeps_the_guess()
    {
        var clock = new FakeTimeProvider();
        var server = new Server(300);
        var source = Live(server, clock);
        var changedAt = source.CellChangedAt!;
        // A first notice, read at once, so the two below are gathered into one read.
        source.NotifyChanged([]);
        clock.Advance(TimeSpan.FromMilliseconds(100));

        Book(server, 8, "T0007b");
        source.NotifyChanged();
        Book(server, 99, "T0097b");
        source.NotifyChanged(["T0097b"]);
        clock.Advance(TimeSpan.FromMilliseconds(150));

        // Between painted rows, and no notice named it: guessed, since one notice named nothing.
        Assert.Equal("T0007b", source.Window[8].Id);
        AssertWhole(changedAt, source.Window[8], clock.GetUtcNow());
        // At the Window's foot, where the guess marks nothing: named.
        Assert.Equal("T0097b", source.Window[99].Id);
        AssertWhole(changedAt, source.Window[99], clock.GetUtcNow());
    }

    [Fact] // ADR-0141 / LV-9 (D6): every gathered notice's names are carried by the read; a name it does not show is let go
    public void Names_are_gathered_and_let_go_after_the_read()
    {
        var clock = new FakeTimeProvider();
        var server = new Server(300);
        var source = Live(server, clock);
        var changedAt = source.CellChangedAt!;
        source.NotifyChanged([]);
        clock.Advance(TimeSpan.FromMilliseconds(100));

        Book(server, 98, "X");
        source.NotifyChanged(["X"]);
        Book(server, 99, "Y");
        source.NotifyChanged(["Y"]);
        // Booked just past the Window's foot: the read does not show it.
        Book(server, 100, "Z");
        source.NotifyChanged(["Z"]);
        clock.Advance(TimeSpan.FromMilliseconds(150));

        Assert.Equal(["X", "Y"], source.Window.Skip(98).Select(d => d.Id));
        AssertWhole(changedAt, source.Window[98], clock.GetUtcNow());
        AssertWhole(changedAt, source.Window[99], clock.GetUtcNow());
        Assert.DoesNotContain(source.Window, d => d.Id == "Z");

        // A row cancelled at the top: Z slides into the Window's foot, and its name was let go.
        clock.Advance(TimeSpan.FromSeconds(1));
        server.Rows.RemoveAt(0);
        server.Token = "order-cancelled";
        source.NotifyChanged([]);
        Assert.Equal("Z", source.Window[99].Id);
        AssertUnmarked(changedAt, source.Window[99]);
    }

    [Fact] // ADR-0141 / LV-9 (D6): a name heard while a question is out applies to its answer when that shows it, and otherwise to the next read's
    public void A_name_heard_while_a_question_is_out_waits_for_the_answer_that_shows_it()
    {
        var clock = new FakeTimeProvider();
        var server = new Server(300) { Holds = true };
        var source = Live(server, clock);
        server.AnswerHeld();
        var changedAt = source.CellChangedAt!;

        // Shown by the answer out: the server booked the row before it answered.
        source.NotifyChanged([]);
        Book(server, 50, "P");
        source.NotifyChanged(["P"]);
        server.AnswerHeld();
        AssertWhole(changedAt, Assert.Single(source.Window, d => d.Id == "P"), clock.GetUtcNow());

        // P's notice was heard while that question was out, so a read at the interval's end carries it;
        // P is painted by then, and compared.
        clock.Advance(TimeSpan.FromSeconds(1));
        server.AnswerHeld();
        AssertWhole(changedAt, Assert.Single(source.Window, d => d.Id == "P"), clock.GetUtcNow() - TimeSpan.FromSeconds(1));

        // Not shown by the answer out, which the server made before it booked the row: the read after
        // it carries the name.
        clock.Advance(TimeSpan.FromSeconds(1));
        source.NotifyChanged([]);
        source.NotifyChanged(["Q"]);
        server.AnswerHeld();
        Assert.DoesNotContain(source.Window, d => d.Id == "Q");
        Book(server, 60, "Q");
        clock.Advance(TimeSpan.FromMilliseconds(250));
        server.AnswerHeld();
        AssertWhole(changedAt, Assert.Single(source.Window, d => d.Id == "Q"), clock.GetUtcNow());
    }

    [Fact] // ADR-0141 / LV-9 (D6): a null key among the names is refused by name, and nothing is heard
    public void A_null_name_is_refused()
    {
        var server = new Server(10);
        var source = Live(server, new FakeTimeProvider());
        var asked = server.Asked.Count;

        var refusal = Assert.Throws<ArgumentException>(() => source.NotifyChanged(["T0001", null!]));

        Assert.Contains("null", refusal.Message);
        Assert.Equal(asked, server.Asked.Count);
        Assert.Throws<ArgumentNullException>(() => source.NotifyChanged(null!));
    }
}
