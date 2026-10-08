using System.Globalization;
using Bunit;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests;

/// <summary>
/// The work after a question's pass, sliced (ADR-0066, settled 2026-10-01; PV-40): ExPivot makes a
/// large answer's cube and lays out its report in slices, yielding the thread between them. A
/// gesture made meanwhile supersedes the question, the report on screen stays — under the loading
/// indication — until the new one is complete, and nothing half made is ever shown. A layout of the
/// answer held that grows long is sliced and superseded the same way, and a quick one yields
/// nothing. The yields are the test's: counted, and held until it releases them.
/// </summary>
public class SlicedBuildTests : PivotTestContext
{
    private static readonly string[] Products = ["Apples", "Pears", "Plums"];

    /// <summary>Three thousand sales, each in a Region of its own: three thousand leaves by Region.</summary>
    private static readonly Sale[] Many = [.. Enumerable.Range(0, 3_000).Select(i =>
        new Sale("R" + i.ToString("0000", CultureInfo.InvariantCulture), Products[i % 3], i, 1, i % 2 == 0))];

    private static readonly PivotLayout ByProduct = new() { Rows = [P("Product")], Values = [Sum("Amount")] };

    // The source answers at once, its own work never yielding: the yields counted are ExPivot's.
    private static readonly PivotSlicing Never = new() { Budget = TimeSpan.FromDays(1) };

    private static OnDemandSource AtOnce() => new(PivotSource.From(Many, Fields, Never)) { AnswersAtOnce = true };

    /// <summary>
    /// ExPivot's yields: with no budget, every look at the clock ends a slice. Each yield is counted,
    /// and, while <see cref="Holding"/>, waits until the test releases it — or until the work's
    /// token is cancelled, as a browser's timer does when the work is superseded. The work runs on
    /// the renderer's thread; the test reads the count from its own.
    /// </summary>
    private sealed class HeldYields
    {
        private readonly Lock _gate = new();
        private readonly Queue<TaskCompletionSource> _held = new();
        private int _count;

        public int Count
        {
            get
            {
                lock (_gate)
                    return _count;
            }
        }

        public bool Holding { get; set; }

        /// <summary>The yields waiting for the test.</summary>
        public int Waiting
        {
            get
            {
                lock (_gate)
                    return _held.Count(gate => !gate.Task.IsCompleted);
            }
        }

        public void Reset()
        {
            lock (_gate)
                _count = 0;
        }

        public PivotSlicing Slicing => new()
        {
            Budget = TimeSpan.Zero,
            Yield = token =>
            {
                lock (_gate)
                {
                    _count++;
                    if (!Holding)
                        return ValueTask.CompletedTask;
                }
                var gate = new TaskCompletionSource();
                token.Register(() => gate.TrySetCanceled(token));
                lock (_gate)
                    _held.Enqueue(gate);
                return new ValueTask(gate.Task);
            },
        };

        /// <summary>Lets the oldest yield still waiting go on.</summary>
        public void ReleaseOne()
        {
            TaskCompletionSource? next;
            do
            {
                lock (_gate)
                {
                    if (!_held.TryDequeue(out next))
                        return;
                }
            }
            while (!next.TrySetResult());
        }
    }

    /// <summary>Waits, on the test's thread, until <paramref name="condition"/> holds: between two
    /// yields nothing renders, so a render cannot be waited for.</summary>
    private static void Until(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Waited 20 s for {what}.");
            Thread.Sleep(1);
        }
    }

    /// <summary>Releases one yield after another until nothing waits and <paramref name="done"/>
    /// holds — by default, until nothing is out — checking <paramref name="between"/> at each yield;
    /// answers how many were released.</summary>
    private static async Task<int> ReleaseAllAsync(
        IRenderedComponent<PivotComponent> cut, HeldYields yields, Action? between = null, Func<bool>? done = null)
    {
        done ??= () => !cut.Instance.IsLoading;
        var released = 0;
        while (true)
        {
            Until(() => yields.Waiting > 0 || done(), "a yield or the end of the work");
            if (yields.Waiting == 0)
                return released;
            between?.Invoke();
            await cut.InvokeAsync(yields.ReleaseOne);
            released++;
        }
    }

    // ---- A large answer, made in slices ----------------------------------------------------------

    [Fact] // ADR-0066 (PV-40): a large answer's cube and report are made in slices; the report on screen stays as it was, under IsLoading, until the new one is complete
    public async Task A_large_answer_is_made_in_slices_and_shown_whole()
    {
        var yields = new HeldYields();
        var source = AtOnce();
        var told = new List<PivotLayout>();
        var cut = RenderPivot(ByProduct, ps => ps.Add(p => p.Slicing, yields.Slicing).Add(p => p.LayoutChanged, told.Add), source: source);
        var before = RowTexts(cut);
        var shown = cut.Instance.Report;
        Assert.Equal(["Apples | 1498500", "Pears | 1499500", "Plums | 1500500", "Grand Total | 4498500"], before);
        yields.Holding = true;
        yields.Reset();

        await TickFieldAsync(cut, "Region", true);

        // Answered at once, and three thousand leaves to make: the build has yielded.
        Assert.Equal(2, source.Questions.Count);
        Assert.True(yields.Waiting > 0);
        var released = await ReleaseAllAsync(cut, yields, () =>
        {
            // At every yield the report on screen is the one before, whole, under the indication,
            // and the pane already shows the layout on its way.
            Assert.Same(shown, cut.Instance.Report);
            Assert.Equal(before, RowTexts(cut));
            Assert.True(cut.Instance.IsLoading);
            Assert.True(Grid(cut).Instance.IsLoading);
            Assert.Equal(["Product", "Region"], AreaEntries(cut, "Rows"));
            Assert.Empty(told);
        });

        Assert.True(released > 3, $"{released} yields");
        Assert.Equal(released, yields.Count);
        var report = cut.Instance.Report!;
        Assert.Equal(1 + 3 + 3_000, report.Metadata.RowCount);
        Assert.False(cut.Instance.IsLoading);
        Assert.False(Grid(cut).Instance.IsLoading);
        Assert.Equal("−Apples | 1498500", RowTexts(cut)[0]);
        Assert.Equal("R0000 | 0", RowTexts(cut)[1]);
        Assert.Single(told);
        Assert.Same(report.Metadata.Layout, told[0]);
    }

    [Fact] // ADR-0066/0025 (PV-40): a gesture made while the answer's report is built supersedes the question; the half-built report is never shown
    public async Task A_gesture_during_the_build_supersedes_the_question()
    {
        var yields = new HeldYields();
        var source = AtOnce();
        var told = new List<PivotLayout>();
        var cut = RenderPivot(ByProduct, ps => ps.Add(p => p.Slicing, yields.Slicing).Add(p => p.LayoutChanged, told.Add), source: source);
        var shown = cut.Instance.Report;
        yields.Holding = true;

        await TickFieldAsync(cut, "Region", true);
        var building = source.Questions[1];
        Assert.True(yields.Waiting > 0);
        Assert.False(building.IsCancelled);

        // A further change while the answer's cube is made: a new question.
        await TickFieldAsync(cut, "Online", true);

        Assert.True(building.IsCancelled);
        Until(() => source.Questions.Count == 3, "the superseding question");
        Assert.Equal(["Product", "Region", "Online"], AreaEntries(cut, "Rows"));
        // The superseded build stopped at its yield, which the cancellation released.
        await ReleaseAllAsync(cut, yields, () =>
        {
            Assert.Same(shown, cut.Instance.Report);
            Assert.True(cut.Instance.IsLoading);
        });

        var report = cut.Instance.Report!;
        Assert.Equal(["Product", "Region", "Online"], report.Metadata.Layout.Rows.Select(p => p.Field));
        Assert.Equal(1 + 3 + 3_000 + 3_000, report.Metadata.RowCount);
        // Only the layout shown is raised; the superseded one never is.
        cut.WaitForAssertion(() => Assert.Single(told));
        Assert.Same(report.Metadata.Layout, told[0]);
    }

    [Fact] // ADR-0066 (PV-26/PV-40): a gesture the answer held lays out, made while a question's report is built, supersedes it at once
    public async Task A_gesture_from_the_answer_held_supersedes_the_build()
    {
        var yields = new HeldYields();
        var source = AtOnce();
        var cut = RenderPivot(ByProduct, ps => ps.Add(p => p.Slicing, yields.Slicing), source: source);
        yields.Holding = true;

        await TickFieldAsync(cut, "Region", true);
        Assert.True(yields.Waiting > 0);
        var building = source.Questions[1];

        // Region unticked again: the answer held lays that out, and nothing is asked.
        await TickFieldAsync(cut, "Region", false);
        await ReleaseAllAsync(cut, yields);

        Assert.True(building.IsCancelled);
        Assert.Equal(2, source.Questions.Count);
        Assert.Equal(["Apples | 1498500", "Pears | 1499500", "Plums | 1500500", "Grand Total | 4498500"], RowTexts(cut));
        Assert.Equal(["Product"], cut.Instance.CurrentLayout.Rows.Select(p => p.Field));
        Assert.False(cut.Instance.IsLoading);
    }

    // ---- A layout of the answer held -----------------------------------------------------------

    [Fact] // ADR-0066 (PV-40): a layout of the answer held that grows long is laid out in slices, under the indication, and a further gesture supersedes it
    public async Task A_long_layout_of_the_answer_held_is_sliced_and_superseded()
    {
        var yields = new HeldYields();
        var source = AtOnce();
        var told = new List<PivotLayout>();
        var regions = new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] };
        var cut = RenderPivot(regions, ps => ps.Add(p => p.Slicing, yields.Slicing).Add(p => p.LayoutChanged, told.Add), source: source);
        var shown = cut.Instance.Report!;
        Assert.Equal("R0000 | 0", RowTexts(cut)[0]);
        yields.Holding = true;

        // Region sorted Z to A: laid out from the answer held, three thousand rows.
        await OpenMenuAsync(cut, "Rows", "Region");
        await RunMenuAsync(cut, "Sort Z to A");

        Assert.True(yields.Waiting > 0);
        Assert.True(cut.Instance.IsLoading);
        Assert.Same(shown, cut.Instance.Report);
        Assert.Equal("R0000 | 0", RowTexts(cut)[0]);

        // And A to Z again, before it is done: the first is superseded, and never shown.
        await OpenMenuAsync(cut, "Rows", "Region");
        await RunMenuAsync(cut, "Sort A to Z");
        await ReleaseAllAsync(cut, yields, () =>
        {
            Assert.Same(shown, cut.Instance.Report);
            Assert.Equal("R0000 | 0", RowTexts(cut)[0]);
        });

        Assert.Single(source.Questions);
        Assert.Equal(PivotSort.Ascending, cut.Instance.CurrentLayout.Rows[0].Sort);
        Assert.Equal("R0000 | 0", RowTexts(cut)[0]);
        Assert.NotSame(shown, cut.Instance.Report);
        Assert.Single(told);
        Assert.Equal(PivotSort.Ascending, told[0].Rows[0].Sort);
        Assert.False(cut.Instance.IsLoading);

        // Z to A, let through: shown whole, once it is complete.
        await OpenMenuAsync(cut, "Rows", "Region");
        await RunMenuAsync(cut, "Sort Z to A");
        Assert.True(cut.Instance.IsLoading);
        await ReleaseAllAsync(cut, yields);
        cut.WaitForAssertion(() => Assert.Equal("R2999 | 2999", RowTexts(cut)[0]));
        Assert.Equal(2, told.Count);
    }

    [Fact] // ADR-0066 (PV-26/PV-40): a quick layout of the answer held yields nothing, and is shown in the gesture's own turn
    public async Task A_quick_layout_yields_nothing()
    {
        var yields = 0;
        // The slices' own clock, which stands still: no slice is ever spent, as none is in a quick layout.
        var slicing = new PivotSlicing
        {
            TimeProvider = Clock,
            Yield = _ =>
            {
                yields++;
                return ValueTask.CompletedTask;
            },
        };
        var source = new OnDemandSource(Bundled()) { AnswersAtOnce = true };
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region"), P("Product")], Values = [Sum("Amount")] },
            ps => ps.Add(p => p.Slicing, slicing), source: source);

        await cut.FindAll(".ex-pivot-toggle")[0].ClickAsync(new MouseEventArgs());
        Assert.False(cut.Instance.IsLoading);
        Assert.StartsWith("+East", RowTexts(cut)[0], StringComparison.Ordinal);
        await OpenMenuAsync(cut, "Rows", "Region");
        await RunMenuAsync(cut, "Sort Z to A");
        Assert.StartsWith("−West", RowTexts(cut)[0], StringComparison.Ordinal);
        await cut.Find(".ex-pivot-layout-button").ClickAsync(new MouseEventArgs());
        await RunMenuAsync(cut, "Show in Tabular Form");
        Assert.Equal(PivotReportForm.Tabular, cut.Instance.Report!.Metadata.Layout.Form);

        Assert.Equal(0, yields);
        Assert.Single(source.Questions);
    }

    // ---- What else meets a build ------------------------------------------------------------------

    [Fact] // ADR-0067 (PV-35/PV-40): a batch applied while a report is built waits for it — never half a batch — and is asked for once it is shown
    public async Task A_batch_during_the_build_waits_for_it()
    {
        var fields = PivotFields.Of<Trade>()
            .Key("Id", t => t.Id)
            .Text("Region", t => t.Region)
            .Text("Product", t => t.Product)
            .Number("Amount", t => t.Amount);
        var trades = Enumerable.Range(0, 3_000).Select(i => new Trade(i, "R" + i.ToString("0000", CultureInfo.InvariantCulture), Products[i % 3], 1m)).ToArray();
        var source = PivotSource.From(trades, fields, Never);
        var yields = new HeldYields();
        var cut = RenderPivot(ByProduct, ps => ps.Add(p => p.Slicing, yields.Slicing), source: source);
        Assert.Equal("Grand Total | 3000", RowTexts(cut)[^1]);
        yields.Holding = true;

        await TickFieldAsync(cut, "Region", true);
        Assert.True(yields.Waiting > 0);
        // Every trade worth 2 now, while the report of the answer that arrived is built.
        await cut.InvokeAsync(() => source.Apply(fields.Batch(changed: [.. trades.Select(t => t with { Amount = 2m })])));
        await ReleaseAllAsync(cut, yields);

        // The report built is the answer's, whole: the data before the batch.
        var report = cut.Instance.Report!;
        Assert.Equal(["Product", "Region"], report.Metadata.Layout.Rows.Select(p => p.Field));
        Assert.Equal("1", report.Rows[1].ValueAt(0)!.Text);
        Assert.Equal("1000", report.Rows[0].ValueAt(0)!.Text);
        Assert.EndsWith("Grand Total\t3000\r\n", await CopyAllAsync(cut, yields));

        // The batch was gathered, not lost, and asked for once the report was shown: quietly, as
        // newer data is, and shown whole once its report is complete.
        await cut.InvokeAsync(() => Clock.Advance(PivotComponent.DefaultRedrawInterval));
        await ReleaseAllAsync(cut, yields, done: () => cut.Instance.Report!.Rows[0].ValueAt(0)!.Text == "2000");
        Assert.Equal("2", cut.Instance.Report!.Rows[1].ValueAt(0)!.Text);
        Assert.EndsWith("Grand Total\t6000\r\n", await CopyAllAsync(cut, yields));
        Assert.Equal(["Product", "Region"], cut.Instance.Report.Metadata.Layout.Rows.Select(p => p.Field));
    }

    [Fact] // ADR-0060/0067 (PV-33/PV-40): new words while a report is built: the report shown is in the new words
    public async Task New_words_during_the_build_reach_the_report_shown()
    {
        var yields = new HeldYields();
        var source = AtOnce();
        var cut = RenderPivot(ByProduct, ps => ps.Add(p => p.Slicing, yields.Slicing), source: source);
        yields.Holding = true;

        await TickFieldAsync(cut, "Region", true);
        Assert.True(yields.Waiting > 0);
        cut.Render(ps => ps.Add(p => p.Label, PivotWords.Japanese));
        await ReleaseAllAsync(cut, yields);

        var report = cut.Instance.Report!;
        Assert.Equal(["Product", "Region"], report.Metadata.Layout.Rows.Select(p => p.Field));
        Assert.Equal(PivotWords.Japanese(PivotWords.GrandTotal), report.Metadata.Settings.Words[PivotWords.GrandTotal]);
        Assert.EndsWith(PivotWords.Japanese(PivotWords.GrandTotal) + "\t4498500\r\n", await CopyAllAsync(cut, yields));
        Assert.Equal(PivotWords.Japanese(PivotWords.RowLabels), HeaderTexts(cut)[0]);
    }

    /// <summary>A keyed sale, for Change Batches.</summary>
    public sealed record Trade(long Id, string Region, string Product, decimal Amount);

    // The current component holds a Window. Copy reads the complete selected report at that
    // version, so a grand total outside the Window remains part of these publication checks.
    private static async Task<string> CopyAllAsync(IRenderedComponent<PivotComponent> cut, HeldYields yields)
    {
        var grid = Grid(cut);
        var selecting = cut.InvokeAsync(() => grid.Instance.OnKeyAsync("a", true, false, false, false, false));
        await ReleaseAllAsync(cut, yields, done: () => selecting.IsCompleted);
        await selecting;
        var copying = cut.InvokeAsync(() => grid.Instance.BuildCopyPayloadAsync());
        await ReleaseAllAsync(cut, yields, done: () => copying.IsCompleted);
        var payload = await copying;
        Assert.NotNull(payload);
        Assert.Equal("data", payload.Kind);
        return payload.Text!;
    }
}
