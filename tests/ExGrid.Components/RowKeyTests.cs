using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

// The render batch is what ADR-0140's claim is about, and its types are the ones BL0006 keeps to
// the framework: read here, never shipped.
#pragma warning disable BL0006

namespace ExGrid.Components.Tests;

/// <summary>
/// The Row Key (ADR-0140, LV-1/LV-2): a Consumer's value that names a row across versions. With
/// one declared, the key — not the instance — is the row component's <c>@key</c>, so a row whose
/// instance changes under the same key keeps its component and repaints in place. Row Identity
/// stays the change signal: the key pairs one render's rows with the next one's, and nothing else.
/// A key that repeats in a Window, or a null key, is refused by name before Blazor's own exception
/// for clashing keys. Without the declaration nothing changes (DC-1).
/// </summary>
public class RowKeyTests : GridTestContext
{
    /// <summary>A trade the Consumer replaces with a new instance when it changes, named by its id.</summary>
    private sealed class Trade(int id, string book, decimal amount)
    {
        public int Id { get; } = id;

        public string Book { get; } = book;

        public decimal Amount { get; } = amount;

        public Trade WithAmount(decimal amount) => new(Id, Book, amount);
    }

    private static readonly Func<Trade, object> ById = static trade => trade.Id;

    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static readonly GridColumn<Trade>[] Columns =
    [
        new("Book", ColumnType.Text, t => t.Book, width: Fixed100),
        new("Amount", ColumnType.Number, t => t.Amount, width: Fixed100),
    ];

    private static Trade[] Trades(int count = 3)
    {
        var trades = new Trade[count];
        for (var i = 0; i < count; i++)
            trades[i] = new Trade(1000 + i, $"Book {i:D4}", i);
        return trades;
    }

    private IRenderedComponent<ExGrid<Trade>> RenderGrid(
        IReadOnlyList<Trade> window,
        Func<Trade, object>? rowKey,
        Action<ComponentParameterCollectionBuilder<ExGrid<Trade>>>? more = null,
        IReadOnlyList<GridColumn<Trade>>? columns = null)
        => Render<ExGrid<Trade>>(ps =>
        {
            ps.Add(g => g.Window, window)
              .Add(g => g.Columns, columns ?? Columns)
              .Add(g => g.RowKey, rowKey);
            more?.Invoke(ps);
        });

    private static IReadOnlyList<IRenderedComponent<ExGridRow<Trade>>> Rows(IRenderedComponent<ExGrid<Trade>> cut)
        => cut.FindComponents<ExGridRow<Trade>>();

    private static ExGridRow<Trade> RowComponentOf(IRenderedComponent<ExGrid<Trade>> cut, int id)
        => Rows(cut).Single(row => row.Instance.Row.Id == id).Instance;

    // Not Assert.Same: a failure would print the components, and a row component's CellTextMetrics
    // cannot be printed (its Bold is another CellTextMetrics, so the record's ToString never ends).
    private static void SameComponent(ExGridRow<Trade> expected, ExGridRow<Trade> actual)
        => Assert.True(ReferenceEquals(expected, actual),
            $"the row component of {expected.Row.Id} was replaced by another, now painting {actual.Row.Id}");

    // ---- LV-1: a changed row keeps its component ------------------------------------------------

    [Fact] // ADR-0140 / LV-1: a row whose instance changes under its key keeps its component, renders once, and no other row renders
    public void A_changed_row_keeps_its_component_and_renders_once()
    {
        var trades = Trades();
        var cut = RenderGrid(trades, ById);
        var before = Rows(cut).Select(row => row.Instance).ToArray();

        Trade[] next = [trades[0], trades[1].WithAmount(77m), trades[2]];
        cut.Render(ps => ps.Add(g => g.Window, next));

        var after = Rows(cut);
        Assert.Equal(3, after.Count);
        for (var i = 0; i < 3; i++)
            SameComponent(before[i], after[i].Instance);
        Assert.Same(next[1], after[1].Instance.Row);
        Assert.Equal([1, 2, 1], after.Select(row => row.RenderCount));
        Assert.Contains("77", after[1].Markup);
    }

    [Fact] // ADR-0140 / LV-1 / DC-1: without the declaration a changed row's component is replaced, as before
    public void Without_a_key_a_changed_rows_component_is_replaced()
    {
        var trades = Trades();
        var cut = RenderGrid(trades, rowKey: null);
        var before = Rows(cut).Select(row => row.Instance).ToArray();

        cut.Render(ps => ps.Add(g => g.Window, new[] { trades[0], trades[1].WithAmount(77m), trades[2] }));

        var after = Rows(cut);
        SameComponent(before[0], after[0].Instance);
        Assert.False(ReferenceEquals(before[1], after[1].Instance), "the changed row kept its component");
        SameComponent(before[2], after[2].Instance);
        Assert.All(after, row => Assert.Equal(1, row.RenderCount));
    }

    [Fact] // ADR-0140 / LV-1: a changed row that also moved is paired with the one painted by its key, not by its place
    public void A_changed_row_that_moved_keeps_its_component()
    {
        var trades = Trades();
        var cut = RenderGrid(trades, ById);
        var first = RowComponentOf(cut, 1000);

        // Sorted the other way, and the first trade's amount changed in the same version.
        cut.Render(ps => ps
            .Add(g => g.Window, new[] { trades[2], trades[1], trades[0].WithAmount(5m) })
            .Add(g => g.RowSequenceVersion, 1));

        SameComponent(first, RowComponentOf(cut, 1000));
        Assert.Equal(2, first.RowIndex);
        Assert.Equal(5m, first.Row.Amount);
    }

    [Fact] // ADR-0140 / ADR-0003: Row Identity stays the change signal — a row rewritten in place under its key does not repaint
    public void A_key_never_says_a_row_is_unchanged_and_a_rewrite_in_place_still_does_not_repaint()
    {
        var trades = Trades();
        var cut = RenderGrid(trades, ById);

        // The same instances in a new list: the key pairs them, and nothing renders.
        cut.Render(ps => ps.Add(g => g.Window, trades.ToArray()));
        Assert.All(Rows(cut), row => Assert.Equal(1, row.RenderCount));

        // A new instance with the very same values: a key never says "unchanged", so it renders.
        cut.Render(ps => ps.Add(g => g.Window, new[] { trades[0], trades[1].WithAmount(trades[1].Amount), trades[2] }));
        Assert.Equal([1, 2, 1], Rows(cut).Select(row => row.RenderCount));
    }

    [Fact] // ADR-0140 / LV-1: the Source's own Row Key is the key where the grid's is not set
    public void The_sources_row_key_is_used_where_the_grid_sets_none()
    {
        var trades = Trades();
        var source = new KeyedSource(trades) { RowKey = ById };
        var cut = Render<ExGrid<Trade>>(ps => ps.Add(g => g.Source, source).Add(g => g.Columns, Columns));
        var middle = RowComponentOf(cut, 1001);

        source.Push([trades[0], trades[1].WithAmount(9m), trades[2]]);

        cut.WaitForAssertion(() => Assert.Equal(9m, RowComponentOf(cut, 1001).Row.Amount));
        SameComponent(middle, RowComponentOf(cut, 1001));
    }

    [Fact] // ADR-0140 / LV-1: the grid's own Row Key wins over the Source's
    public void The_grids_row_key_wins_over_the_sources()
    {
        var trades = Trades();
        // The source's key names each version afresh, so under it every changed row would be a
        // new component; the grid's names the trade.
        var source = new KeyedSource(trades) { RowKey = static trade => trade };
        var cut = Render<ExGrid<Trade>>(ps => ps.Add(g => g.Source, source).Add(g => g.Columns, Columns).Add(g => g.RowKey, ById));
        var middle = RowComponentOf(cut, 1001);

        source.Push([trades[0], trades[1].WithAmount(9m), trades[2]]);

        cut.WaitForAssertion(() => Assert.Equal(9m, RowComponentOf(cut, 1001).Row.Amount));
        SameComponent(middle, RowComponentOf(cut, 1001));
    }

    [Fact] // ADR-0140 / LV-1: a Row Key equal to a Placeholder's position does not clash with it — the two keys are of different kinds
    public void A_row_key_equal_to_a_placeholders_position_does_not_clash_with_it()
    {
        // Rows 0-4 are in hand and named 5-9; rows 5-9 are Placeholders. Keyed by the bare
        // position, a Placeholder would carry the very key its neighbour's row does.
        var trades = Enumerable.Range(0, 5).Select(i => new Trade(5 + i, $"Book {i}", i)).ToArray();

        var cut = Render<ExGrid<Trade>>(ps => ps
            .Add(g => g.Window, trades)
            .Add(g => g.TotalCount, 20)
            .Add(g => g.Columns, Columns)
            .Add(g => g.RowKey, ById)
            .Add(g => g.RowHeight, 20)
            .Add(g => g.ViewportHeight, 300));

        Assert.Equal(5, Rows(cut).Count);
        Assert.NotEmpty(cut.FindAll(".ex-placeholder"));
    }

    // ---- LV-1: what the batch carries ------------------------------------------------------------

    [Fact] // ADR-0140 / LV-1: under its key, the batch for a changed row inserts no element and disposes no component — it edits text and attributes only
    public async Task Under_its_key_a_changed_row_is_edited_in_place_in_the_batch()
    {
        var (batches, middle, rows) = await ReplaceTheMiddleRowAsync(ById);

        Assert.All(batches, batch => Assert.Empty(batch.DisposedComponents));
        var diffs = batches.SelectMany(batch => batch.Diffs).ToArray();
        Assert.DoesNotContain(diffs.SelectMany(diff => diff.Edits), edit => edit.Type is RenderTreeEditType.PrependFrame or RenderTreeEditType.RemoveFrame);
        // Of the rows, only the changed one rendered, and what it sent is its changed text.
        var rendered = diffs.Where(diff => rows.Contains(diff.ComponentId)).ToArray();
        var rowDiff = Assert.Single(rendered);
        Assert.Equal(middle, rowDiff.ComponentId);
        // Stepping in and out of an element only moves the diff's place; it writes nothing.
        Assert.All(rowDiff.Edits, edit => Assert.Contains(edit.Type,
            new[]
            {
                RenderTreeEditType.UpdateText, RenderTreeEditType.SetAttribute, RenderTreeEditType.RemoveAttribute,
                RenderTreeEditType.StepIn, RenderTreeEditType.StepOut,
            }));
        Assert.Contains(rowDiff.Edits, edit => edit.Type == RenderTreeEditType.UpdateText);
    }

    [Fact] // ADR-0140 / LV-1 / DC-1: without a key, the batch removes the changed row's component and inserts a new one, as before
    public async Task Without_a_key_a_changed_row_is_inserted_again_in_the_batch()
    {
        var (batches, middle, _) = await ReplaceTheMiddleRowAsync(rowKey: null);

        Assert.Contains(middle, batches.SelectMany(batch => batch.DisposedComponents));
        var edits = batches.SelectMany(batch => batch.Diffs).SelectMany(diff => diff.Edits).ToArray();
        Assert.Contains(edits, edit => edit.Type == RenderTreeEditType.PrependFrame && edit.ComponentType == typeof(ExGridRow<Trade>));
        Assert.Contains(edits, edit => edit.Type == RenderTreeEditType.PrependFrame && edit.ElementName == "div");
    }

    /// <summary>Renders three trades through a renderer that keeps its batches, then replaces the
    /// middle one with a changed instance under the same id: the batches of that push, and the
    /// component ids the middle row and every row had before it.</summary>
    private async Task<(CapturedBatch[] Batches, int MiddleRowId, HashSet<int> RowIds)> ReplaceTheMiddleRowAsync(Func<Trade, object>? rowKey)
    {
        await using var renderer = new CapturingRenderer(Clock);
        var grid = renderer.Create<ExGrid<Trade>>();
        var gridId = renderer.Attach(grid);
        var trades = Trades();

        Dictionary<string, object?> Parameters(IReadOnlyList<Trade> window) => new()
        {
            [nameof(ExGrid<Trade>.Window)] = window,
            [nameof(ExGrid<Trade>.Columns)] = Columns,
            [nameof(ExGrid<Trade>.RowKey)] = rowKey,
            [nameof(ExGrid<Trade>.ViewportHeight)] = (ViewportSize)400,
            [nameof(ExGrid<Trade>.ViewportWidth)] = (ViewportSize)400,
        };

        await renderer.RenderRootAsync(gridId, Parameters(trades));
        var rows = renderer.ChildComponents(gridId).Where(child => child.Component is ExGridRow<Trade>).ToArray();
        Assert.Equal(3, rows.Length);
        var middle = rows.Single(child => child.Component is ExGridRow<Trade> { Row.Id: 1001 });
        renderer.Take();

        await renderer.RenderRootAsync(gridId, Parameters([trades[0], trades[1].WithAmount(77m), trades[2]]));
        return (renderer.Take(), middle.Id, [.. rows.Select(row => row.Id)]);
    }

    // ---- LV-2: a repeated or null key is refused by name ----------------------------------------

    [Fact] // ADR-0140 / LV-2: a Row Key that repeats within a Window is refused by name, naming the key and its positions — never Blazor's exception
    public void A_repeated_row_key_is_refused_by_name()
    {
        var trades = Trades(4);
        Trade[] window = [trades[0], trades[1], trades[2], new Trade(1001, "Another book", 5m)];

        var refusal = Assert.Throws<InvalidOperationException>(() => RenderGrid(window, ById));

        Assert.Contains("Row Key", refusal.Message);
        Assert.Contains("1001", refusal.Message);
        Assert.Contains("Window[1]", refusal.Message);
        Assert.Contains("Window[3]", refusal.Message);
        Assert.Contains("ADR-0140", refusal.Message);
    }

    [Fact] // ADR-0140 / LV-2: a null Row Key is refused by name, naming its position
    public void A_null_row_key_is_refused_by_name()
    {
        var trades = Trades();

        var refusal = Assert.Throws<InvalidOperationException>(
            () => RenderGrid(trades, trade => trade.Id == 1002 ? null! : (object)trade.Id));

        Assert.Contains("Row Key", refusal.Message);
        Assert.Contains("Window[2]", refusal.Message);
        Assert.Contains("null", refusal.Message);
    }

    [Fact] // ADR-0140 / LV-2: positions are named in the whole result too, where the Window is a slice of it
    public void A_repeated_key_names_its_rows_in_the_whole_result()
    {
        Trade[] window = [new(1, "A", 1m), new(2, "B", 2m), new(1, "C", 3m)];

        var refusal = Assert.Throws<InvalidOperationException>(() => Render<ExGrid<Trade>>(ps => ps
            .Add(g => g.Window, window)
            .Add(g => g.WindowStart, 500)
            .Add(g => g.TotalCount, 1000)
            .Add(g => g.Columns, Columns)
            .Add(g => g.RowKey, ById)));

        Assert.Contains("rows 500 and 502", refusal.Message);
    }

    [Fact] // ADR-0140 / LV-2: a Window pushed later with a repeated key is refused the same way
    public void A_later_window_with_a_repeated_key_is_refused()
    {
        var trades = Trades();
        var cut = RenderGrid(trades, ById);

        var refusal = Assert.Throws<InvalidOperationException>(
            () => cut.Render(ps => ps.Add(g => g.Window, new[] { trades[0], trades[1], trades[1].WithAmount(1m) })));

        Assert.Contains("Window[1]", refusal.Message);
        Assert.Contains("Window[2]", refusal.Message);
    }

    [Fact] // ADR-0140 / LV-2: a key that becomes declared over a Window already in hand checks that Window
    public void Declaring_a_key_checks_the_window_in_hand()
    {
        Trade[] window = [new(1, "A", 1m), new(1, "B", 2m)];
        var cut = RenderGrid(window, rowKey: null);

        var refusal = Assert.Throws<InvalidOperationException>(() => cut.Render(ps => ps.Add(g => g.RowKey, ById)));

        Assert.Contains("Row Key", refusal.Message);
    }

    [Fact] // ADR-0140 / LV-2: the Source's Row Key is checked as the grid's is, when the source does not vouch
    public async Task A_repeated_key_from_a_source_that_does_not_vouch_is_refused()
    {
        var trades = Trades();
        var source = new KeyedSource(trades) { RowKey = ById };
        Render<ExGrid<Trade>>(ps => ps.Add(g => g.Source, source).Add(g => g.Columns, Columns));

        source.Push([trades[0], trades[0].WithAmount(2m)]);

        var raised = await Renderer.UnhandledException.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        Assert.IsType<InvalidOperationException>(raised);
        Assert.Contains("Row Key", raised.Message);
    }

    [Fact] // ADR-0003 / DC-1: without a key, a repeated instance is still refused as before
    public void Without_a_key_a_repeated_instance_is_still_refused()
    {
        var trades = Trades();

        var refusal = Assert.Throws<InvalidOperationException>(
            () => RenderGrid([trades[0], trades[1], trades[0]], rowKey: null));

        Assert.Contains("same row instance", refusal.Message);
    }

    // ---- LV-10, the grid's side: a Window the source vouches for is not passed through the check --

    [Fact] // ADR-0150 / LV-10: a pushed Window can give the same guarantee as a keyed source
    public void ADR0150_a_vouched_pushed_window_only_reads_keys_of_painted_rows()
    {
        var counting = new CountingKey();
        var trades = Trades(5_000);
        var cut = RenderGrid(trades, counting.Key, ps => ps
            .Add(g => g.VouchesDistinctRows, true).Add(g => g.ViewportHeight, 300));
        var painted = Rows(cut).Count;
        counting.Calls = 0;

        cut.Render(ps => ps.Add(g => g.Window,
            trades.Select(trade => trade.Id == 1001 ? trade.WithAmount(3m) : trade).ToArray()));

        Assert.Equal(3m, RowComponentOf(cut, 1001).Row.Amount);
        Assert.InRange(counting.Calls, 1, painted * 4);
    }

    [Fact] // ADR-0150 / LV-10: the default keeps the full validation pass
    public void ADR0150_a_pushed_window_is_checked_by_default()
    {
        var counting = new CountingKey();
        RenderGrid(Trades(5_000), counting.Key);
        Assert.True(counting.Calls >= 5_000, $"the key was asked {counting.Calls} times");
    }

    [Fact] // ADR-0150 / LV-10: withdrawal checks a Window even if it has the same instance
    public void ADR0150_withdrawing_a_pushed_vouch_checks_the_same_window()
    {
        var trades = Trades(5_000);
        trades[4_001] = new Trade(trades[4_000].Id, "Twin", 0m);
        var cut = RenderGrid(trades, ById, ps => ps.Add(g => g.VouchesDistinctRows, true));

        var refusal = Assert.Throws<InvalidOperationException>(() => cut.Render(ps => ps.Add(g => g.VouchesDistinctRows, false)));

        Assert.Contains("Window[4000] and Window[4001]", refusal.Message);
        Assert.Contains("same Row Key", refusal.Message);
    }

    [Fact] // ADR-0150 / LV-10: granting a guarantee over an already checked Window still records its later withdrawal
    public void ADR0150_a_vouch_granted_and_withdrawn_on_the_same_window_rechecks_it()
    {
        var counting = new CountingKey();
        var cut = RenderGrid(Trades(5_000), counting.Key);
        cut.Render(ps => ps.Add(g => g.VouchesDistinctRows, true));
        counting.Calls = 0;

        cut.Render(ps => ps.Add(g => g.VouchesDistinctRows, false));

        Assert.True(counting.Calls >= 5_000, $"the key was asked {counting.Calls} times");
    }

    [Fact] // ADR-0150 / LV-10: the pushed guarantee says nothing without a Row Key
    public void ADR0150_a_pushed_vouch_without_a_key_still_checks_instances()
    {
        var trades = Trades(5_000);
        trades[4_001] = trades[4_000];

        var refusal = Assert.Throws<InvalidOperationException>(() =>
            RenderGrid(trades, null, ps => ps.Add(g => g.VouchesDistinctRows, true)));

        Assert.Contains("same row instance", refusal.Message);
    }

    [Theory] // ADR-0150 / LV-10: a bound Source supplies the only vouch; a pushed promise cannot cover an overridden key
    [InlineData(false)]
    [InlineData(true)]
    public void ADR0150_a_pushed_vouch_does_not_override_the_sources_guarantee(bool sourceVouches)
    {
        var counting = new CountingKey();
        var source = new KeyedSource(Trades(5_000)) { RowKey = ById, Vouches = sourceVouches };

        Render<ExGrid<Trade>>(ps => ps.Add(g => g.Source, source).Add(g => g.Columns, Columns)
            .Add(g => g.RowKey, counting.Key).Add(g => g.VouchesDistinctRows, true));

        Assert.True(counting.Calls >= 5_000, $"the key was asked {counting.Calls} times");
    }

    [Fact] // ADR-0150 / LV-10: only the Source itself can promise its own Window is valid
    public void ADR0150_a_pushed_vouch_cannot_vouch_for_an_unvouched_source()
    {
        var counting = new CountingKey();
        var source = new KeyedSource(Trades(5_000)) { RowKey = counting.Key, Vouches = false };

        Render<ExGrid<Trade>>(ps => ps.Add(g => g.Source, source).Add(g => g.Columns, Columns)
            .Add(g => g.VouchesDistinctRows, true));

        Assert.True(counting.Calls >= 5_000, $"the key was asked {counting.Calls} times");
    }

    [Fact] // ADR-0153 / LV-24: an unchanged row keeps its captured text when another row changes
    public void ADR0153_unchanged_rows_do_not_revisit_their_value_accessors_for_history()
    {
        var reads = new List<int>();
        GridColumn<Trade>[] columns =
        [
            new("Book", ColumnType.Text, row => { reads.Add(row.Id); return row.Book; }, width: Fixed100),
        ];
        var trades = Trades();
        var cut = RenderGrid(trades, ById, columns: columns);
        reads.Clear();

        cut.Render(ps => ps.Add(g => g.Window, new[] { trades[0], trades[1].WithAmount(3m), trades[2] }));

        Assert.Contains(trades[1].Id, reads);
        Assert.DoesNotContain(trades[2].Id, reads);
    }

    [Fact] // ADR-0141 / LV-10: a Window the source vouches for is not walked — the key is asked only of the rows painted
    public void A_vouched_window_is_not_walked()
    {
        var counting = new CountingKey();
        var trades = Trades(5_000);
        var source = new KeyedSource(trades) { RowKey = counting.Key, Vouches = true };
        var cut = Render<ExGrid<Trade>>(ps => ps
            .Add(g => g.Source, source).Add(g => g.Columns, Columns).Add(g => g.ViewportHeight, 300));
        var painted = Rows(cut).Count;
        counting.Calls = 0;

        source.Push([.. trades.Select(trade => trade.Id == 1001 ? trade.WithAmount(3m) : trade)]);

        cut.WaitForAssertion(() => Assert.Equal(3m, RowComponentOf(cut, 1001).Row.Amount));
        Assert.InRange(counting.Calls, 1, painted * 4);
    }

    [Fact] // ADR-0141 / LV-10: a Window the source does not vouch for is walked whole, as the check needs
    public void A_window_not_vouched_for_is_walked_whole()
    {
        var counting = new CountingKey();
        var trades = Trades(5_000);
        var source = new KeyedSource(trades) { RowKey = counting.Key, Vouches = false };
        var cut = Render<ExGrid<Trade>>(ps => ps
            .Add(g => g.Source, source).Add(g => g.Columns, Columns).Add(g => g.ViewportHeight, 300));
        counting.Calls = 0;

        source.Push([.. trades.Select(trade => trade.Id == 1001 ? trade.WithAmount(3m) : trade)]);

        cut.WaitForAssertion(() => Assert.Equal(3m, RowComponentOf(cut, 1001).Row.Amount));
        Assert.True(counting.Calls >= 5_000, $"the key was asked {counting.Calls} times");
    }

    [Fact] // ADR-0141 / LV-10: a vouched Window is taken on the source's word — a repeat it should have refused is not the grid's to find
    public void A_vouched_window_is_taken_on_the_sources_word()
    {
        var trades = Trades(5_000);
        // Rows 4,000 and 4,001 answer one key, far below the rows painted: walked, the Window
        // would be refused; vouched for, it is painted.
        var lying = trades.ToArray();
        lying[4_001] = new Trade(lying[4_000].Id, "Twin", 0m);
        var source = new KeyedSource(lying) { RowKey = ById, Vouches = true };

        var cut = Render<ExGrid<Trade>>(ps => ps
            .Add(g => g.Source, source).Add(g => g.Columns, Columns).Add(g => g.ViewportHeight, 300));

        Assert.NotEmpty(Rows(cut));
    }

    [Fact] // ADR-0141 / LV-10: a Row Key the Consumer sets other than the source's is the grid's to check, vouched or not
    public void A_grid_key_other_than_the_sources_is_checked_by_the_grid()
    {
        var counting = new CountingKey();
        var trades = Trades(5_000);
        var source = new KeyedSource(trades) { RowKey = ById, Vouches = true };
        var cut = Render<ExGrid<Trade>>(ps => ps
            .Add(g => g.Source, source).Add(g => g.Columns, Columns).Add(g => g.ViewportHeight, 300)
            .Add(g => g.RowKey, counting.Key));
        counting.Calls = 0;

        source.Push([.. trades.Select(trade => trade.Id == 1001 ? trade.WithAmount(3m) : trade)]);

        cut.WaitForAssertion(() => Assert.Equal(3m, RowComponentOf(cut, 1001).Row.Amount));
        Assert.True(counting.Calls >= 5_000, $"the key was asked {counting.Calls} times");
    }

    [Fact] // ADR-0141 / LV-10: the grid's Row Key set to the source's own is still the source's — the vouch stands
    public void The_sources_own_key_set_on_the_grid_keeps_the_vouch()
    {
        var counting = new CountingKey();
        var trades = Trades(5_000);
        var source = new KeyedSource(trades) { RowKey = counting.Key, Vouches = true };
        var cut = Render<ExGrid<Trade>>(ps => ps
            .Add(g => g.Source, source).Add(g => g.Columns, Columns).Add(g => g.ViewportHeight, 300)
            .Add(g => g.RowKey, source.RowKey));
        var painted = Rows(cut).Count;
        counting.Calls = 0;

        source.Push([.. trades.Select(trade => trade.Id == 1001 ? trade.WithAmount(3m) : trade)]);

        cut.WaitForAssertion(() => Assert.Equal(3m, RowComponentOf(cut, 1001).Row.Amount));
        Assert.InRange(counting.Calls, 1, painted * 4);
    }

    [Fact] // ADR-0141 / LV-10: a vouch with no Row Key behind it is not taken — the grid checks for a repeated row itself
    public async Task A_vouch_without_a_row_key_is_not_taken()
    {
        var trades = Trades();
        var source = new KeyedSource(trades) { Vouches = true };
        Render<ExGrid<Trade>>(ps => ps.Add(g => g.Source, source).Add(g => g.Columns, Columns));

        source.Push([trades[0], trades[1], trades[0]]);

        var raised = await Renderer.UnhandledException.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        Assert.Contains("same row instance", raised.Message);
    }

    // ---- A component that now outlives its instance (ADR-0140, Consequences) ---------------------

    [Fact] // ADR-0140 / ADR-0068: a kept row whose new version is unmarked takes its mark away, and the grid's timer goes with the last one
    public void A_kept_row_whose_new_version_is_unmarked_drops_its_mark_and_the_timer()
    {
        var clock = new CountingTimeProvider(Clock);
        var trades = Trades();
        // Marked by instance: the first version of the middle trade changed just now, its next
        // version did not.
        var marked = new HashSet<Trade>(ReferenceEqualityComparer.Instance) { trades[1] };
        var now = Clock.GetUtcNow();
        CellChangeOf<Trade> changedAt = (row, column) => marked.Contains(row) && column.Name == "Amount" ? now : null;
        var cut = RenderGrid(trades, ById, ps => ps.Add(g => g.CellChangedAt, changedAt).Add(g => g.Clock, clock));
        Assert.Single(cut.FindAll(".ex-changed"));
        var timer = Assert.Single(clock.Timers);
        var middle = RowComponentOf(cut, 1001);

        cut.Render(ps => ps.Add(g => g.Window, new[] { trades[0], trades[1].WithAmount(8m), trades[2] }));

        SameComponent(middle, RowComponentOf(cut, 1001));
        Assert.Empty(cut.FindAll(".ex-changed"));
        Assert.Equal(1, timer.Disposals);
    }

    [Fact] // ADR-0140 / ADR-0068: a kept row whose new version is marked later moves the grid's one timer to the new end
    public void A_kept_row_whose_new_version_is_marked_later_rearms_the_timer()
    {
        var clock = new CountingTimeProvider(Clock);
        var trades = Trades();
        var times = new Dictionary<Trade, DateTimeOffset>(ReferenceEqualityComparer.Instance) { [trades[1]] = Clock.GetUtcNow() };
        CellChangeOf<Trade> changedAt = (row, column) => column.Name == "Amount" && times.TryGetValue(row, out var at) ? at : null;
        var cut = RenderGrid(trades, ById, ps => ps.Add(g => g.CellChangedAt, changedAt).Add(g => g.Clock, clock));
        var timer = Assert.Single(clock.Timers);

        Clock.Advance(TimeSpan.FromMilliseconds(600));
        var next = trades[1].WithAmount(8m);
        times[next] = Clock.GetUtcNow();
        cut.Render(ps => ps.Add(g => g.Window, new[] { trades[0], next, trades[2] }));

        // The first version's end, 400 ms from now, no longer holds the mark up: it lasts until
        // the new version's end, a second from now.
        Clock.Advance(TimeSpan.FromMilliseconds(400));
        Assert.Single(cut.FindAll(".ex-changed"));
        Clock.Advance(TimeSpan.FromMilliseconds(600));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".ex-changed")));
        Assert.Same(timer, Assert.Single(clock.Timers));
    }

    [Fact] // ADR-0140 / ADR-0037: a Template cell's own component survives a change of the row's values, and is handed the new version
    public void A_template_cells_component_survives_and_is_handed_the_new_row()
    {
        RenderFragment<TemplateCellContext<Trade>> probe = cell => builder =>
        {
            builder.OpenComponent<RowProbe>(0);
            builder.AddComponentParameter(1, nameof(RowProbe.Row), cell.Row);
            builder.CloseComponent();
        };
        GridColumn<Trade>[] columns = [.. Columns, GridColumn<Trade>.TemplateColumn("Probe", ColumnType.Text, t => t.Book, probe, width: Fixed100)];
        var trades = Trades();
        var cut = RenderGrid(trades, ById, columns: columns);
        var before = cut.FindComponents<RowProbe>().Single(p => p.Instance.Row.Id == 1001).Instance;

        var next = trades[1].WithAmount(8m);
        cut.Render(ps => ps.Add(g => g.Window, new[] { trades[0], next, trades[2] }));

        var after = cut.FindComponents<RowProbe>().Single(p => p.Instance.Row.Id == 1001).Instance;
        Assert.Same(before, after);
        Assert.Same(next, after.Row);
    }

    [Fact] // ADR-0140 / ADR-0020: a press on a kept row reports the version it now paints, never the one it was built with
    public void A_press_on_a_kept_row_reports_the_row_it_now_paints()
    {
        var raised = new List<GridActionEventArgs<Trade>>();
        GridColumn<Trade>[] columns = [.. Columns, GridColumn<Trade>.ActionColumn("Open", [new GridAction("open", "Open")], width: Fixed100)];
        var trades = Trades();
        var cut = RenderGrid(trades, ById, ps => ps.Add(g => g.OnAction, raised.Add), columns);

        var next = trades[1].WithAmount(8m);
        cut.Render(ps => ps.Add(g => g.Window, new[] { trades[0], next, trades[2] }));
        cut.FindAll(".ex-action")[1].Click();

        Assert.Same(next, Assert.Single(raised).Row);
    }

    [Fact] // ADR-0140 / ADR-0033: a kept row that moves names its cells by its new position
    public void A_kept_row_that_moves_names_its_cells_by_its_new_position()
    {
        var trades = Trades();
        var cut = RenderGrid(trades, ById);
        var first = RowComponentOf(cut, 1000);

        cut.Render(ps => ps
            .Add(g => g.Window, new[] { trades[1], trades[2], trades[0].WithAmount(4m) })
            .Add(g => g.RowSequenceVersion, 1));

        var moved = Rows(cut).Single(row => row.Instance.Row.Id == 1000);
        SameComponent(first, moved.Instance);
        Assert.All(moved.FindAll("[role=gridcell]"), cell => Assert.Contains("r2c", cell.Id));
        Assert.Equal("3", moved.Find("[role=row]").GetAttribute("aria-rowindex"));
    }

    [Fact] // ADR-0140 / ADR-0027 P7 / ED-6: the editor's uncommitted text and the Selection are the core's — a kept row changing under them takes neither
    public async Task The_editors_text_and_the_selection_outlive_a_change_of_the_row_under_them()
    {
        var trades = Trades();
        var cut = RenderGrid(trades, ById, ps => ps.Add(g => g.RowHeight, 20d).Add(g => g.ViewportHeight, 120), EditableColumns);
        var middle = RowComponentOf(cut, 1001);
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 30 });
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("x", false, false, false, false, false));
        await cut.Find(".ex-editor").InputAsync(new ChangeEventArgs { Value = "typed" });

        cut.Render(ps => ps.Add(g => g.Window, new[] { trades[0], trades[1].WithAmount(8m), trades[2] }));

        SameComponent(middle, RowComponentOf(cut, 1001));
        // The edit still open is the Selection kept too: a dropped Selection discards the edit (ADR-0011).
        Assert.Equal("typed", cut.Find(".ex-editor").GetAttribute("value"));
    }

    [Fact] // ADR-0140 / ADR-0037: an Interactive cell's chosen action is held by the core, by position — it stays on the kept row's new version
    public async Task An_engaged_cell_stays_engaged_across_a_change_of_its_row()
    {
        var trades = Trades();
        var cut = RenderGrid(trades, ById, ps => ps.Add(g => g.RowHeight, 20d).Add(g => g.ViewportHeight, 120), EditableColumns);
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 150, OffsetY = 30 });
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync(" ", false, false, false, false, false));
        Assert.Equal("Approve", Assert.Single(cut.FindAll("button.ex-action-chosen")).TextContent);
        var middle = RowComponentOf(cut, 1001);

        cut.Render(ps => ps.Add(g => g.Window, new[] { trades[0], trades[1].WithAmount(8m), trades[2] }));

        SameComponent(middle, RowComponentOf(cut, 1001));
        Assert.Equal("Approve", Assert.Single(cut.FindAll("button.ex-action-chosen")).TextContent);
        Assert.Contains("8", Rows(cut)[1].Markup);
    }

    private static readonly GridColumn<Trade>[] EditableColumns =
    [
        new("Book", ColumnType.Text, t => t.Book, width: Fixed100, editable: true),
        GridColumn<Trade>.ActionColumn("Review",
            [new GridAction("approve", "Approve"), new GridAction("query", "Query"), new GridAction("escalate", "Escalate")],
            width: new ColumnWidthSpec(ColumnWidth.Fixed(200))),
        new("Amount", ColumnType.Number, t => t.Amount, width: Fixed100),
    ];

    /// <summary>A Template cell's own component: records the row it was last handed.</summary>
    private sealed class RowProbe : ComponentBase
    {
        [Parameter] public Trade Row { get; set; } = default!;

        protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, Row.Book);
    }

    /// <summary>A Row Key that counts how often it is asked.</summary>
    private sealed class CountingKey
    {
        public CountingKey() => Key = trade =>
        {
            Calls++;
            return trade.Id;
        };

        public int Calls { get; set; }

        public Func<Trade, object> Key { get; }
    }

    /// <summary>
    /// A Grid Source that holds its Window whole and names its rows by the Row Key it is given; it
    /// vouches for its Windows only when told to, as a bundled source that refuses a repeated key
    /// does (ADR-0141).
    /// </summary>
    private sealed class KeyedSource(IReadOnlyList<Trade> window) : IGridSource<Trade>
    {
        public IReadOnlyList<Trade> Window { get; private set; } = window;

        public int WindowStart => 0;

        public int? TotalCount => null;

        public bool IsLoading => false;

        public int RowSequenceVersion { get; private set; }

        public IReadOnlyList<SortSpec> Sorts => [];

        public GridFilter? Filter => null;

        public Func<Trade, object>? RowKey { get; init; }

        public bool Vouches { get; init; }

        public bool VouchesDistinctRows => Vouches;

        public event Action? StateChanged;

        public void Push(IReadOnlyList<Trade> window)
        {
            Window = window;
            RowSequenceVersion++;
            StateChanged?.Invoke();
        }

        public void OnSortChanged(IReadOnlyList<SortSpec> sorts)
        {
        }

        public void OnFilterChanged(GridFilter? filter)
        {
        }

        public void OnColumnsChanged(IReadOnlyList<ColumnInfo<Trade>> columns)
        {
        }

        public Task OnRangeNeededAsync(RowRange range) => Task.CompletedTask;

        public Task<IReadOnlyList<Trade>> GetRowsAsync(RowRange range, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<Trade>>([.. Window.Skip(range.Start).Take(range.Count)]);

        public Task<Chrome.DistinctValues> GetDistinctValuesAsync(string column, CancellationToken cancellationToken)
            => Task.FromResult(Chrome.DistinctValues.TooMany);
    }
}
