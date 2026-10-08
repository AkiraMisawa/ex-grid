using System.Globalization;
using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Data;
using ExGrid.Finding;
using ExGrid.Rows;
using ExGrid.Selection;
using ExGrid.Summarizing;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// An answer or a placement asked of a Source that has since been replaced by another instance never
/// moves the Focus, the figures or the marks of the new one (ADR-0142, ADR-0011; decided with the user
/// 2026-10-08). Find, the placement a Consumer asks for, the Selection Summary and the Row Marks tell
/// the two sources apart by the grid's binding and the Row Sequence Version together — never by the
/// version alone, which two fresh sources share: every source here stands at version 0.
///
/// 20px rows in a 120px Viewport, 350px wide: Book 0–100 and Amount 100–200 (both editable), or the
/// Mark Column 0–100 before them where a test asks for one.
/// </summary>
public class AnswersAcrossAReplacedSourceTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100, editable: true),
    ];

    private static GridColumn<TestRow>[] WithMarks() =>
    [
        GridColumn<TestRow>.MarkColumn("Mark", width: Fixed100),
        .. Columns(),
    ];

    private sealed class Heard
    {
        public List<FindRefusalReason> FindRefusals { get; } = [];
        public List<SelectionSummary> Summaries { get; } = [];
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        IGridSource<TestRow> source, Heard heard, GridColumn<TestRow>[]? columns = null)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, columns ?? Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.OnFindRefused, (FindRefusalReason r) => heard.FindRefusals.Add(r))
            .Add(g => g.OnSelectionSummaryChanged, (SelectionSummary s) => heard.Summaries.Add(s)));

    /// <summary>The Consumer hands the grid another Source instance.</summary>
    private static void Replace(IRenderedComponent<ExGrid<TestRow>> cut, IGridSource<TestRow> source)
        => cut.Render(ps => ps.Add(g => g.Source, source));

    private static int Paint(IRenderedComponent<ExGrid<TestRow>> cut)
        => int.Parse(cut.Find(".ex-viewport").GetAttribute("data-ex-paint")!, CultureInfo.InvariantCulture);

    private static Task KeyAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false, bool shift = false, int paint = -1)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, shift, false, false, false, paint: paint));

    private static Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    private static GridSelection SelectionOf(IRenderedComponent<ExGrid<TestRow>> cut) => cut.Instance.ReadSelection().Selection;

    /// <summary>Another source's rows: new instances with the same values.</summary>
    private static TestRow[] Copies(TestRow[] rows)
        => [.. rows.Select(r => new TestRow { Book = r.Book, Amount = r.Amount, AsOf = r.AsOf, Active = r.Active })];

    // ---- Find ----

    /// <summary>Opens the find panel, types <paramref name="text"/>, and asks for the next match; the
    /// step is returned unawaited, as its answer is the test's to give.</summary>
    private static async Task<Task> AskFindAsync(IRenderedComponent<ExGrid<TestRow>> cut, string text)
    {
        await KeyAsync(cut, "f", ctrl: true);
        await cut.Find("input.ex-find-field").InputAsync(new ChangeEventArgs { Value = text });
        return cut.Find("button.ex-find-next").ClickAsync(new MouseEventArgs());
    }

    [Fact] // ADR-0142 / ADR-0055, ADR-0011: a Find answer from a Source replaced while the step was out moves nothing in the new one — both sources at version 0 — and is refused as SourceChanged, which the panel says
    public async Task A_find_answer_from_the_replaced_source_moves_nothing_and_is_refused()
    {
        var rows = TestRows.Many(50);
        var old = new AnsweringSource(rows);
        var replacement = new AnsweringSource(Copies(rows));
        var heard = new Heard();
        var cut = RenderGrid(old, heard);
        await ClickCellAsync(cut, 50, 10);
        var step = await AskFindAsync(cut, "Row 000003");
        Assert.Single(old.FindRequests);

        Replace(cut, replacement);
        Assert.Equal(old.RowSequenceVersion, replacement.RowSequenceVersion);
        await cut.InvokeAsync(() => old.FindAnswer.SetResult(GridFindResult.Found(3, "Book")));
        await step;

        Assert.Equal([FindRefusalReason.SourceChanged], heard.FindRefusals);
        Assert.True(SelectionOf(cut).IsEmpty);
        Assert.Equal("The data was replaced; find again", cut.Find(".ex-find-outcome").TextContent);
        Assert.Empty(replacement.FindRequests);
    }

    [Fact] // ADR-0142 / ADR-0055: the next step, asked of the new Source, answers there and moves the Focus
    public async Task A_find_step_asked_of_the_new_source_moves_the_focus()
    {
        var rows = TestRows.Many(50);
        var old = new AnsweringSource(rows);
        var replacement = new AnsweringSource(Copies(rows));
        var heard = new Heard();
        var cut = RenderGrid(old, heard);
        var step = await AskFindAsync(cut, "Row 000003");
        Replace(cut, replacement);
        await cut.InvokeAsync(() => old.FindAnswer.SetResult(GridFindResult.Found(3, "Book")));
        await step;

        replacement.FindAnswer.SetResult(GridFindResult.Found(2, "Amount"));
        await cut.Find("button.ex-find-next").ClickAsync(new MouseEventArgs());

        Assert.Single(replacement.FindRequests);
        Assert.Equal([FindRefusalReason.SourceChanged], heard.FindRefusals);
        Assert.Equal(new CellPosition(2, 1), SelectionOf(cut).Focus);
    }

    // ---- The placement a Consumer asks for ----

    [Fact] // ADR-0142 / ADR-0050 item 4, ADR-0011: a placement whose commit of the open edit brought in another Source — both at version 0 — places nothing in the new one
    public async Task A_placement_whose_commit_brought_in_another_source_places_nothing()
    {
        var rows = TestRows.Many(50);
        var host = Render<ReplacingHost>(ps => ps
            .Add(h => h.First, new AnsweringSource(rows))
            .Add(h => h.Second, new AnsweringSource(Copies(rows))));
        var cut = host.FindComponent<ExGrid<TestRow>>();
        await ClickCellAsync(cut, 50, 10);
        await KeyAsync(cut, "5", paint: Paint(cut));
        Assert.Single(cut.FindAll("input.ex-editor"));
        var bound = cut.Instance.Source;

        var placed = await cut.InvokeAsync(() => cut.Instance.PlaceSelectionAsync(
            SelectionRange.FromCorners(new CellPosition(3, 1), new CellPosition(4, 1)), new CellPosition(3, 1), rowSequenceVersion: 0));

        // The commit landed, on the old source, and its end brought in the new one.
        Assert.Equal("5", Assert.Single(host.Instance.Edits).Value);
        Assert.NotSame(bound, cut.Instance.Source);
        Assert.Equal(0, cut.Instance.Source!.RowSequenceVersion);
        Assert.False(placed);
        Assert.True(SelectionOf(cut).IsEmpty);
    }

    [Fact] // ADR-0142 / ADR-0050 item 4: a placement asked after the new Source is in, under its version, places there — the version is all a request carries
    public async Task A_placement_asked_under_the_new_source_places()
    {
        var rows = TestRows.Many(50);
        var cut = RenderGrid(new AnsweringSource(rows), new Heard());
        await ClickCellAsync(cut, 50, 10);
        Replace(cut, new AnsweringSource(Copies(rows)));

        var placed = await cut.InvokeAsync(() => cut.Instance.PlaceSelectionAsync(
            SelectionRange.FromCorners(new CellPosition(3, 1), new CellPosition(3, 1)), new CellPosition(3, 1), rowSequenceVersion: 0));

        Assert.True(placed);
        Assert.Equal(new CellPosition(3, 1), SelectionOf(cut).Focus);
    }

    // ---- The Selection Summary ----

    private static GridSummaryResult Figures(decimal sum) => GridSummaryResult.Answered(
        new Dictionary<SummaryFigures, AggregateResult>
        {
            [SummaryFigures.Average] = AggregateResult.Of(sum / 2),
            [SummaryFigures.Count] = AggregateResult.Of(2m),
            [SummaryFigures.Sum] = AggregateResult.Of(sum),
        });

    [Fact] // ADR-0142 / ADR-0130, ADR-0011: the figures asked of a Source replaced before they came are never shown or raised over the new one — both sources at version 0 — and the question is withdrawn
    public async Task Summary_figures_from_the_replaced_source_are_never_shown()
    {
        var rows = TestRows.Many(50);
        var old = new AnsweringSource(rows);
        var replacement = new AnsweringSource(Copies(rows));
        var heard = new Heard();
        var cut = RenderGrid(old, heard);
        await ClickCellAsync(cut, 150, 10);
        await KeyAsync(cut, "ArrowDown", shift: true, paint: Paint(cut));
        Assert.Single(old.SummaryRequests);
        Assert.Equal("Calculating…", cut.Find(".ex-summary").TextContent);

        Replace(cut, replacement);
        await cut.InvokeAsync(() => old.SummaryAnswer.SetResult(Figures(999m)));

        Assert.True(old.SummaryTokens[0].IsCancellationRequested);
        Assert.DoesNotContain("999", cut.Find(".ex-summary").TextContent);
        Assert.DoesNotContain(heard.Summaries, s => s.Status == SelectionSummaryStatus.Answered);
        Assert.Equal(SelectionSummaryStatus.None, heard.Summaries[^1].Status);
    }

    [Fact] // ADR-0142 / ADR-0130: a range selected on the new Source is asked of it, and its figures stand
    public async Task Summary_figures_asked_of_the_new_source_are_shown()
    {
        var rows = TestRows.Many(50);
        var old = new AnsweringSource(rows);
        var replacement = new AnsweringSource(Copies(rows));
        var heard = new Heard();
        var cut = RenderGrid(old, heard);
        await ClickCellAsync(cut, 150, 10);
        await KeyAsync(cut, "ArrowDown", shift: true, paint: Paint(cut));
        Replace(cut, replacement);
        await cut.InvokeAsync(() => old.SummaryAnswer.SetResult(Figures(999m)));

        await ClickCellAsync(cut, 150, 10);
        await KeyAsync(cut, "ArrowDown", shift: true, paint: Paint(cut));
        await cut.InvokeAsync(() => replacement.SummaryAnswer.SetResult(Figures(1m)));

        Assert.Single(replacement.SummaryRequests);
        Assert.Contains("Sum: 1", cut.Find(".ex-summary").TextContent);
        Assert.DoesNotContain("999", cut.Find(".ex-summary").TextContent);
    }

    // ---- Row Marks ----

    [Fact] // ADR-0142 / ADR-0043, ADR-0011: Space in the Mark Column taken on the replaced Source's paint — both at version 0 — raises no Mark intent, to either source's marks
    public async Task Space_in_the_mark_column_aimed_at_the_replaced_source_marks_nothing()
    {
        var rows = TestRows.Many(50);
        var old = new AnsweringSource(rows);
        var replacement = new AnsweringSource(Copies(rows));
        var cut = RenderGrid(old, new Heard(), WithMarks());
        await ClickCellAsync(cut, 50, 30);
        await KeyAsync(cut, "ArrowDown", shift: true, paint: Paint(cut));
        var takenOn = Paint(cut);

        Replace(cut, replacement);
        await KeyAsync(cut, " ", paint: takenOn);

        Assert.Empty(old.MarksHeard.Intents);
        Assert.Empty(replacement.MarksHeard.Intents);
        Assert.True(SelectionOf(cut).IsEmpty);
    }

    [Fact] // ADR-0142 / ADR-0043: Space in the Mark Column taken on the new Source's paint marks its rows, through its own marks
    public async Task Space_in_the_mark_column_on_the_new_source_marks_there()
    {
        var rows = TestRows.Many(50);
        var old = new AnsweringSource(rows);
        var replacement = new AnsweringSource(Copies(rows));
        var cut = RenderGrid(old, new Heard(), WithMarks());
        Replace(cut, replacement);
        await ClickCellAsync(cut, 50, 30);
        await KeyAsync(cut, "ArrowDown", shift: true, paint: Paint(cut));

        await KeyAsync(cut, " ", paint: Paint(cut));

        Assert.Empty(old.MarksHeard.Intents);
        var intent = Assert.IsType<RowMarkIntent<TestRow>.Positions>(Assert.Single(replacement.MarksHeard.Intents));
        Assert.Equal([new RowRange(1, 2)], intent.Ranges);
        Assert.Equal(0, intent.RowSequenceVersion);
    }

    /// <summary>A Consumer whose handler for an edit's end hands the grid another Source, at the same
    /// version 0: the replacement lands inside the grid's commit of that edit.</summary>
    private sealed class ReplacingHost : ComponentBase
    {
        [Parameter, EditorRequired] public IGridSource<TestRow> First { get; set; } = default!;

        [Parameter, EditorRequired] public IGridSource<TestRow> Second { get; set; } = default!;

        public List<GridEditIntent<TestRow>> Edits { get; } = [];

        private IGridSource<TestRow>? _source;

        protected override void OnInitialized() => _source = First;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ExGrid<TestRow>>(0);
            builder.AddComponentParameter(1, nameof(ExGrid<TestRow>.Source), _source);
            builder.AddComponentParameter(2, nameof(ExGrid<TestRow>.Columns), (IReadOnlyList<GridColumn<TestRow>>)Columns());
            builder.AddComponentParameter(3, nameof(ExGrid<TestRow>.RowHeight), 20d);
            builder.AddComponentParameter(4, nameof(ExGrid<TestRow>.ViewportHeight), (ViewportSize)120);
            builder.AddComponentParameter(5, nameof(ExGrid<TestRow>.ViewportWidth), (ViewportSize)350);
            builder.AddComponentParameter(6, nameof(ExGrid<TestRow>.OnEdit),
                EventCallback.Factory.Create<GridEditIntent<TestRow>>(this, Edits.Add));
            builder.AddComponentParameter(7, nameof(ExGrid<TestRow>.OnEditingChanged),
                EventCallback.Factory.Create<bool>(this, open =>
                {
                    if (!open)
                        _source = Second;
                }));
            builder.CloseComponent();
        }
    }

    /// <summary>The Consumer's marks, recorded: every intent the grid reports is kept.</summary>
    private sealed class RecordingMarks : IRowMarks<TestRow>
    {
        public List<RowMarkIntent<TestRow>> Intents { get; } = [];

        public RowMarkCounts? Counts => new RowMarkCounts(0, 50, 0);

        public event Action? Changed { add { } remove { } }

        public bool IsMarked(TestRow row) => false;

        public Task OnMarkIntentAsync(RowMarkIntent<TestRow> intent)
        {
            Intents.Add(intent);
            return Task.CompletedTask;
        }

        public void OnRowKindChanged(Func<TestRow, RowKind>? rowKind)
        {
        }
    }

    /// <summary>A fresh source at Row Sequence Version 0 that searches, summarises and keeps marks; each
    /// answer is held until the test gives it.</summary>
    private sealed class AnsweringSource(IReadOnlyList<TestRow> rows) : IGridSource<TestRow>
    {
        public IReadOnlyList<TestRow> Window { get; } = rows;

        public int WindowStart => 0;

        public int? TotalCount => Window.Count;

        public bool IsLoading => false;

        public int RowSequenceVersion => 0;

        public IReadOnlyList<SortSpec> Sorts => [];

        public GridFilter? Filter => null;

        public event Action? StateChanged { add { } remove { } }

        public List<GridFindRequest> FindRequests { get; } = [];

        public TaskCompletionSource<GridFindResult> FindAnswer { get; } = new();

        public List<GridSummaryRequest> SummaryRequests { get; } = [];

        public List<CancellationToken> SummaryTokens { get; } = [];

        public TaskCompletionSource<GridSummaryResult> SummaryAnswer { get; } = new();

        public RecordingMarks MarksHeard { get; } = new();

        public IRowMarks<TestRow>? Marks => MarksHeard;

        public bool CanFind => true;

        public bool CanSummarize => true;

        public Task<GridFindResult> FindAsync(GridFindRequest request, CancellationToken cancellationToken)
        {
            FindRequests.Add(request);
            return FindAnswer.Task;
        }

        public Task<GridSummaryResult> SummarizeAsync(GridSummaryRequest request, CancellationToken cancellationToken)
        {
            SummaryRequests.Add(request);
            SummaryTokens.Add(cancellationToken);
            return SummaryAnswer.Task;
        }

        public void OnSortChanged(IReadOnlyList<SortSpec> sorts)
        {
        }

        public void OnFilterChanged(GridFilter? filter)
        {
        }

        public void OnColumnsChanged(IReadOnlyList<ColumnInfo<TestRow>> columns)
        {
        }

        public Task OnRangeNeededAsync(RowRange range) => Task.CompletedTask;

        public Task<IReadOnlyList<TestRow>> GetRowsAsync(RowRange range, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<TestRow>>([.. Window.Skip(range.Start).Take(range.Count)]);

        public Task<Chrome.DistinctValues> GetDistinctValuesAsync(string column, CancellationToken cancellationToken)
            => Task.FromResult(Chrome.DistinctValues.Of([]));
    }
}
