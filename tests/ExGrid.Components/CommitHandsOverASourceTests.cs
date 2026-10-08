using System.Globalization;
using Bunit;
using ExGrid.Cells;
using ExGrid.Clipboard;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// A Source the Consumer hands over while it hears the open edit's own commit is that commit's doing
/// (ADR-0142, LV-32; decided 2026-10-08). A discard is never said for typing that was handed over
/// (principle 1): accepted, the edit ends as committed and nothing is discarded; refused, the editor
/// cannot be held over a row that went with the old source, so it is discarded as
/// <c>SourceChanged</c>, said once. No Overwrite Notice follows such a commit: it would name a position
/// among the rows of a source the grid no longer shows, and the intent carried both texts. A Ctrl+Enter
/// fill takes its editor down before the Consumer hears it, and so discards nothing either.
///
/// The Consumer here is a page whose handlers hand the grid another Source, as a page that rebuilds
/// its source on a write does: Blazor renders the page as each handler completes, inside the grid's
/// wait for it. 20px rows in a 120px Viewport, 350px wide: Book 0–100 and Amount 100–200, both
/// editable. Every source is fresh, at Row Sequence Version 0.
/// </summary>
public class CommitHandsOverASourceTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static readonly Func<TestRow, object> ByBook = static row => row.Book;

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100, editable: true),
    ];

    /// <summary>Another source's rows: new instances with the same values.</summary>
    private static TestRow[] Copies(TestRow[] rows)
        => [.. rows.Select(r => new TestRow { Book = r.Book, Amount = r.Amount, AsOf = r.AsOf, Active = r.Active })];

    private IRenderedComponent<CommitHost> RenderHost(
        IGridSource<TestRow> first, IGridSource<TestRow> second,
        Action<ComponentParameterCollectionBuilder<CommitHost>>? more = null)
        => Render<CommitHost>(ps =>
        {
            ps.Add(h => h.First, first).Add(h => h.Second, second);
            more?.Invoke(ps);
        });

    private static IRenderedComponent<ExGrid<TestRow>> GridOf(IRenderedComponent<CommitHost> host)
        => host.FindComponent<ExGrid<TestRow>>();

    private static int Paint(IRenderedComponent<ExGrid<TestRow>> cut)
        => int.Parse(cut.Find(".ex-viewport").GetAttribute("data-ex-paint")!, CultureInfo.InvariantCulture);

    private static Task KeyAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false, int paint = -1)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, false, false, false, false, paint: paint));

    private static async Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y, bool shift = false)
    {
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y, ShiftKey = shift });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0, OffsetX = x, OffsetY = y });
    }

    // The Cell Editor: the Formula Bar's own field is an editor surface too, and stands with no edit open.
    private static bool EditorOpen(IRenderedComponent<ExGrid<TestRow>> cut) => cut.FindAll(".ex-viewport .ex-editor").Count > 0;

    private string EditingModeToldLast()
        => (string)JSInterop.Invocations.Last(i => i.Identifier == "setEditing").Arguments[0]!;

    /// <summary>Opens the Cell Editor on Book of the first row by typing <c>5</c>.</summary>
    private static async Task TypeFiveAsync(IRenderedComponent<ExGrid<TestRow>> cut)
    {
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5", paint: Paint(cut));
        Assert.True(EditorOpen(cut));
    }

    [Theory] // ADR-0142 / LV-32, principle 1: Enter or Tab commits `5`, the page hands over another Source as it hears the Edit Intent, and the edit ends as committed — "edit 5" alone, no discard said — with or without a Row Key
    [InlineData("Enter", false)]
    [InlineData("Tab", false)]
    [InlineData("Enter", true)]
    [InlineData("Tab", true)]
    public async Task A_commit_whose_handler_hands_over_another_source_ends_as_committed(string key, bool rowKey)
    {
        var rows = TestRows.Many(50);
        var host = rowKey
            ? RenderHost(GridSource.From(rows, ByBook, Clock), GridSource.From(Copies(rows), ByBook, Clock))
            : RenderHost(GridSource.From(rows), GridSource.From(Copies(rows)));
        var cut = GridOf(host);
        await TypeFiveAsync(cut);
        var reclaimed = Js.FocusReclaimed.Invocations.Count;

        await KeyAsync(cut, key, paint: Paint(cut));

        Assert.Equal(["edit 5"], host.Instance.Heard);
        Assert.Same(host.Instance.Second, cut.Instance.Source);
        Assert.False(EditorOpen(cut));
        // Ended as a commit ends: the keyboard comes back to the root.
        Assert.True(Js.FocusReclaimed.Invocations.Count > reclaimed);
        // The Selection went with the old source, and Enter or Tab moved nothing in the new one.
        Assert.True(cut.Instance.ReadSelection().Selection.IsEmpty);
    }

    [Fact] // ADR-0142 / LV-32: refused while the page handed over another Source, the editor cannot be held over a row that went with the old source: it is discarded, said once as SourceChanged, and no editor stands; keys typed after it against the old paint say nothing more
    public async Task A_refused_commit_whose_handler_hands_over_another_source_is_discarded_once()
    {
        var rows = TestRows.Many(50);
        var host = RenderHost(GridSource.From(rows), GridSource.From(Copies(rows)), ps => ps.Add(h => h.Refuse, true));
        var cut = GridOf(host);
        await TypeFiveAsync(cut);
        var typedOn = Paint(cut);

        await KeyAsync(cut, "Enter", paint: typedOn);
        await KeyAsync(cut, "7", paint: typedOn);

        Assert.Equal(["edit 5", "discard SourceChanged"], host.Instance.Heard);
        Assert.False(EditorOpen(cut));
        Assert.True(cut.Instance.ReadSelection().Selection.IsEmpty);
    }

    [Theory] // ADR-0142 D1 / LV-32, LV-11: a commit over a cell that changed under the editor raises its Overwrite Notice — but not when the page hands over another Source as it hears the commit, which holds other text there too
    [InlineData(false)]
    [InlineData(true)]
    public async Task No_overwrite_notice_follows_a_commit_whose_handler_hands_over_another_source(bool handsOver)
    {
        var rows = TestRows.Many(50);
        var first = GridSource.From(rows);
        var elsewhere = Copies(rows);
        elsewhere[0] = new TestRow { Book = "Another source's", Amount = 7m, AsOf = rows[0].AsOf, Active = rows[0].Active };
        var host = RenderHost(first, GridSource.From(elsewhere), ps => ps.Add(h => h.Replace, handsOver));
        var cut = GridOf(host);
        await TypeFiveAsync(cut);
        await cut.InvokeAsync(() => first.ReplaceRow(rows[0],
            new TestRow { Book = "Moved upstream", Amount = rows[0].Amount, AsOf = rows[0].AsOf, Active = rows[0].Active }));

        await KeyAsync(cut, "Enter", paint: Paint(cut));

        Assert.Equal(handsOver ? ["edit 5"] : ["edit 5", "notice Moved upstream"], host.Instance.Heard);
        Assert.False(EditorOpen(cut));
    }

    [Fact] // ADR-0142 / LV-32, ADR-0051: a Formula Bar commit whose handler hands over another Source ends as committed the same way, and says no discard
    public async Task A_formula_bar_commit_whose_handler_hands_over_another_source_ends_as_committed()
    {
        var rows = TestRows.Many(50);
        var host = RenderHost(GridSource.From(rows), GridSource.From(Copies(rows)), ps => ps.Add(h => h.ShowFormulaBar, true));
        var cut = GridOf(host);
        await ClickAsync(cut, 50, 10);
        await cut.Find(".ex-formula-bar-text").FocusAsync(new FocusEventArgs());
        await cut.Find(".ex-formula-bar-text").InputAsync(new ChangeEventArgs { Value = "Typed in the bar" });

        // Typed in the bar, the key reaches the listener as a descendant's.
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("Enter", false, false, false, false, false, fromDescendant: true, paint: Paint(cut)));

        Assert.Equal(["edit Typed in the bar"], host.Instance.Heard);
        Assert.False(EditorOpen(cut));
        Assert.Equal("none", EditingModeToldLast());
    }

    [Theory] // ADR-0142 / LV-32, ADR-0007: a Ctrl+Enter fill takes its editor down before the page hears it, so a Source handed over then discards nothing; and, being the fill's own doing, no Overwrite Notice follows it
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_ctrl_enter_fill_whose_handler_hands_over_another_source_discards_nothing(bool handsOver)
    {
        var rows = TestRows.Many(50);
        var first = GridSource.From(rows);
        var host = RenderHost(first, GridSource.From(Copies(rows)), ps => ps.Add(h => h.Replace, handsOver));
        var cut = GridOf(host);
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 50, 50, shift: true);
        await KeyAsync(cut, "5", paint: Paint(cut));
        await cut.InvokeAsync(() => first.ReplaceRow(rows[0],
            new TestRow { Book = "Moved upstream", Amount = rows[0].Amount, AsOf = rows[0].AsOf, Active = rows[0].Active }));

        await KeyAsync(cut, "Enter", ctrl: true, paint: Paint(cut));

        Assert.Equal(handsOver ? ["paste"] : ["paste", "notice Moved upstream"], host.Instance.Heard);
        Assert.False(EditorOpen(cut));
    }

    [Fact] // ADR-0142 / LV-32: a placement asked while an edit is open commits it first; its handler handing over another Source is the commit's, said as no discard, and the placement places nothing in the new one
    public async Task A_placement_whose_commit_hands_over_another_source_says_no_discard()
    {
        var rows = TestRows.Many(50);
        var host = RenderHost(GridSource.From(rows), GridSource.From(Copies(rows)));
        var cut = GridOf(host);
        await TypeFiveAsync(cut);

        var placed = await cut.InvokeAsync(() => cut.Instance.PlaceSelectionAsync(
            SelectionRange.FromCorners(new CellPosition(3, 1), new CellPosition(3, 1)), new CellPosition(3, 1), rowSequenceVersion: 0));

        Assert.False(placed);
        Assert.Equal(["edit 5"], host.Instance.Heard);
        Assert.False(EditorOpen(cut));
    }

    [Fact] // ADR-0142 / LV-32: a handler that hands over another Source and then fails leaves no editor standing over the new source's row: the edit goes as under any replacement, and is said once
    public async Task A_failing_handler_that_hands_over_another_source_leaves_no_editor()
    {
        var rows = TestRows.Many(50);
        var host = RenderHost(GridSource.From(rows), GridSource.From(Copies(rows)), ps => ps.Add(h => h.Fail, true));
        var cut = GridOf(host);
        await TypeFiveAsync(cut);

        await Assert.ThrowsAsync<InvalidOperationException>(() => KeyAsync(cut, "Enter", paint: Paint(cut)));

        Assert.Equal(["edit 5", "discard SourceChanged"], host.Instance.Heard);
        Assert.False(EditorOpen(cut));
        // Nothing is left to commit into the new source.
        await KeyAsync(cut, "Enter", paint: Paint(cut));
        Assert.Equal(["edit 5", "discard SourceChanged"], host.Instance.Heard);
    }

    [Theory] // ADR-0142 / LV-32, ED-21: a handler that hands over another Source and other columns at once: accepted, the edit ends as committed and nothing is said; refused, it is discarded once, as SourceChanged — the Source first, as a discard under no commit says it
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_commit_whose_handler_hands_over_another_source_and_other_columns_is_said_at_most_once(bool refused)
    {
        var rows = TestRows.Many(50);
        var host = RenderHost(GridSource.From(rows), GridSource.From(Copies(rows)), ps => ps
            .Add(h => h.ChangeColumns, true)
            .Add(h => h.Refuse, refused));
        var cut = GridOf(host);
        await TypeFiveAsync(cut);
        var reclaimed = Js.FocusReclaimed.Invocations.Count;

        await KeyAsync(cut, "Enter", paint: Paint(cut));

        Assert.Equal(refused ? ["edit 5", "discard SourceChanged"] : ["edit 5"], host.Instance.Heard);
        Assert.False(EditorOpen(cut));
        Assert.True(Js.FocusReclaimed.Invocations.Count > reclaimed);
    }

    /// <summary>A page holding the grid, whose handlers hand it <see cref="Second"/> in place of
    /// <see cref="First"/> as they hear a commit or a fill — Blazor renders the page, and so the grid's new
    /// Source, as each handler completes.</summary>
    private sealed class CommitHost : ComponentBase
    {
        [Parameter, EditorRequired] public IGridSource<TestRow> First { get; set; } = default!;

        [Parameter, EditorRequired] public IGridSource<TestRow> Second { get; set; } = default!;

        /// <summary>Whether a handler hands over <see cref="Second"/>.</summary>
        [Parameter] public bool Replace { get; set; } = true;

        /// <summary>Whether the page refuses the Edit Intent.</summary>
        [Parameter] public bool Refuse { get; set; }

        /// <summary>Whether the edit's handler fails once it has handed over the new Source.</summary>
        [Parameter] public bool Fail { get; set; }

        [Parameter] public bool ShowFormulaBar { get; set; }

        /// <summary>Whether the edit's handler also shows other columns.</summary>
        [Parameter] public bool ChangeColumns { get; set; }

        /// <summary>What the page heard, in order.</summary>
        public List<string> Heard { get; } = [];

        private IGridSource<TestRow>? _source;
        private GridColumn<TestRow>[] _columns = Columns();

        protected override void OnInitialized() => _source = First;

        private void HandOver()
        {
            if (Replace)
                _source = Second;
        }

        private void OnEdit(GridEditIntent<TestRow> intent)
        {
            Heard.Add("edit " + intent.Value);
            if (Refuse)
                intent.Refuse("Not above 150");
            HandOver();
            if (ChangeColumns)
                _columns = [Columns()[0], new("AsOf", ColumnType.Date, r => r.AsOf, width: Fixed100)];
            if (Fail)
            {
                StateHasChanged();
                throw new InvalidOperationException("The page failed after handing over its new source.");
            }
        }

        private void OnPaste(GridPasteIntent intent)
        {
            Heard.Add("paste");
            HandOver();
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ExGrid<TestRow>>(0);
            builder.AddComponentParameter(1, nameof(ExGrid<TestRow>.Source), _source);
            builder.AddComponentParameter(2, nameof(ExGrid<TestRow>.Columns), (IReadOnlyList<GridColumn<TestRow>>)_columns);
            builder.AddComponentParameter(3, nameof(ExGrid<TestRow>.RowHeight), 20d);
            builder.AddComponentParameter(4, nameof(ExGrid<TestRow>.ViewportHeight), (ViewportSize)120);
            builder.AddComponentParameter(5, nameof(ExGrid<TestRow>.ViewportWidth), (ViewportSize)350);
            builder.AddComponentParameter(6, nameof(ExGrid<TestRow>.ShowFormulaBar), ShowFormulaBar);
            builder.AddComponentParameter(7, nameof(ExGrid<TestRow>.OnEdit),
                EventCallback.Factory.Create<GridEditIntent<TestRow>>(this, OnEdit));
            builder.AddComponentParameter(8, nameof(ExGrid<TestRow>.OnPaste),
                EventCallback.Factory.Create<GridPasteIntent>(this, OnPaste));
            builder.AddComponentParameter(9, nameof(ExGrid<TestRow>.OnEditDiscarded),
                EventCallback.Factory.Create<EditDiscardReason>(this, reason => Heard.Add("discard " + reason)));
            builder.AddComponentParameter(10, nameof(ExGrid<TestRow>.OnOverwriteNotice),
                EventCallback.Factory.Create<GridOverwriteNotice>(this, notice => Heard.Add("notice " + notice.ReplacedText)));
            builder.CloseComponent();
        }
    }
}
