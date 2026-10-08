using System.Globalization;
using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The visible columns the Consumer changes while it hears the open edit's own commit are that
/// commit's doing (ADR-0142, ED-21; decided 2026-10-08), as a Source it hands over then is
/// (CommitHandsOverASourceTests). ED-21: text the grid throws away is announced with the reason that
/// is true of it, and no Edit Intent is raised for a discard. So a commit whose handler changes the
/// columns is never said to be discarded when the Consumer accepted it — the typing was handed over —
/// and is discarded once as <c>ColumnsChanged</c> when it refused it: the editor cannot stand over new
/// columns. No Overwrite Notice follows such a commit. Columns changed by anything else while an editor
/// is open still discard it, as before.
///
/// The Consumer is a page that pushes its Window and changes its columns in its handler; Blazor renders
/// the page, and so the grid's new columns, as the handler completes, inside the grid's wait for it.
/// 20px rows in a 120px Viewport, 350px wide: Book 0–100 and Amount 100–200, both editable; the page's
/// other columns are Book and AsOf.
/// </summary>
public class CommitChangesTheColumnsTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100, editable: true),
    ];

    private static GridColumn<TestRow>[] OtherColumns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("AsOf", ColumnType.Date, r => r.AsOf, width: Fixed100),
    ];

    private IRenderedComponent<ColumnsHost> RenderHost(Action<ComponentParameterCollectionBuilder<ColumnsHost>>? more = null)
        => Render<ColumnsHost>(ps =>
        {
            ps.Add(h => h.Rows, TestRows.Many(50));
            more?.Invoke(ps);
        });

    private static IRenderedComponent<ExGrid<TestRow>> GridOf(IRenderedComponent<ColumnsHost> host)
        => host.FindComponent<ExGrid<TestRow>>();

    private static int Paint(IRenderedComponent<ExGrid<TestRow>> cut)
        => int.Parse(cut.Find(".ex-viewport").GetAttribute("data-ex-paint")!, CultureInfo.InvariantCulture);

    private static Task KeyAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, bool fromBar = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, false, false, false, false, false, fromDescendant: fromBar, paint: Paint(cut)));

    private static async Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
    {
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0, OffsetX = x, OffsetY = y });
    }

    // The Cell Editor: the Formula Bar's own field is an editor surface too, and stands with no edit open.
    private static bool EditorOpen(IRenderedComponent<ExGrid<TestRow>> cut) => cut.FindAll(".ex-viewport .ex-editor").Count > 0;

    /// <summary>Opens the Cell Editor on Book of the first row by typing <c>5</c>.</summary>
    private static async Task TypeFiveAsync(IRenderedComponent<ExGrid<TestRow>> cut)
    {
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        Assert.True(EditorOpen(cut));
    }

    private static string[] ColumnNames(IRenderedComponent<ExGrid<TestRow>> cut) => [.. cut.Instance.Columns.Select(c => c.Name)];

    [Theory] // ADR-0142 / ED-21, principle 1: Enter or Tab commits `5`, the page changes the visible columns as it hears the Edit Intent, and the edit ends as committed — "edit 5" alone, no ColumnsChanged discard said — with the keyboard back on the root
    [InlineData("Enter")]
    [InlineData("Tab")]
    public async Task A_commit_whose_handler_changes_the_columns_ends_as_committed(string key)
    {
        var host = RenderHost();
        var cut = GridOf(host);
        await TypeFiveAsync(cut);
        var reclaimed = Js.FocusReclaimed.Invocations.Count;

        await KeyAsync(cut, key);

        Assert.Equal(["edit 5"], host.Instance.Heard);
        Assert.Equal(["Book", "AsOf"], ColumnNames(cut));
        Assert.False(EditorOpen(cut));
        // The editor went while the page heard the commit: the keyboard was asked back for it.
        Assert.True(Js.FocusReclaimed.Invocations.Count > reclaimed);
        Assert.True(cut.Instance.ReadSelection().Selection.IsEmpty);
    }

    [Fact] // ADR-0142 / ED-21, ADR-0051: a Formula Bar commit whose handler changes the columns ends as committed the same way, and the keyboard is taken out of the bar
    public async Task A_formula_bar_commit_whose_handler_changes_the_columns_ends_as_committed()
    {
        var host = RenderHost(ps => ps.Add(h => h.ShowFormulaBar, true));
        var cut = GridOf(host);
        await ClickAsync(cut, 50, 10);
        await cut.Find(".ex-formula-bar-text").FocusAsync(new FocusEventArgs());
        await cut.Find(".ex-formula-bar-text").InputAsync(new ChangeEventArgs { Value = "Typed in the bar" });
        var reclaimed = Js.FocusReclaimed.Invocations.Count;

        await KeyAsync(cut, "Enter", fromBar: true);

        Assert.Equal(["edit Typed in the bar"], host.Instance.Heard);
        Assert.False(EditorOpen(cut));
        var handBack = Assert.Single(Js.FocusReclaimed.Invocations.Skip(reclaimed));
        Assert.True((bool)handBack.Arguments[0]!);
    }

    [Fact] // ADR-0142 / ED-21: refused while the page changed the columns, the editor cannot stand over the new ones: it is discarded once as ColumnsChanged, and no editor stands
    public async Task A_refused_commit_whose_handler_changes_the_columns_is_discarded_once()
    {
        var host = RenderHost(ps => ps.Add(h => h.Refuse, true));
        var cut = GridOf(host);
        await TypeFiveAsync(cut);

        await KeyAsync(cut, "Enter");

        Assert.Equal(["edit 5", "discard ColumnsChanged"], host.Instance.Heard);
        Assert.False(EditorOpen(cut));
    }

    [Theory] // ADR-0142 D1 / ED-21, LV-11: a commit over a cell that changed under the editor raises its Overwrite Notice — but not when the page changes the columns as it hears the commit
    [InlineData(false)]
    [InlineData(true)]
    public async Task No_overwrite_notice_follows_a_commit_whose_handler_changes_the_columns(bool changes)
    {
        var host = RenderHost(ps => ps.Add(h => h.ChangeColumns, changes));
        var cut = GridOf(host);
        await TypeFiveAsync(cut);
        await host.InvokeAsync(() => host.Instance.ChangeFirstRow("Moved upstream"));

        await KeyAsync(cut, "Enter");

        Assert.Equal(changes ? ["edit 5"] : ["edit 5", "notice Moved upstream"], host.Instance.Heard);
        Assert.False(EditorOpen(cut));
    }

    [Fact] // ADR-0011 / ED-21 (control): columns changed by anything but the commit's own handler while an editor is open still discard it, said as ColumnsChanged, and raise no Edit Intent
    public async Task Columns_changed_while_an_editor_is_open_still_discard_it()
    {
        var host = RenderHost();
        var cut = GridOf(host);
        await TypeFiveAsync(cut);

        await host.InvokeAsync(host.Instance.ShowOtherColumns);

        Assert.Equal(["discard ColumnsChanged"], host.Instance.Heard);
        Assert.False(EditorOpen(cut));
        // Nothing is left to commit under the new columns.
        await KeyAsync(cut, "Enter");
        Assert.Equal(["discard ColumnsChanged"], host.Instance.Heard);
    }

    [Fact] // ADR-0142 / ED-21: a handler that changes the columns and then fails leaves no editor: the edit goes as under any change of columns, and is said once
    public async Task A_failing_handler_that_changes_the_columns_leaves_no_editor()
    {
        var host = RenderHost(ps => ps.Add(h => h.Fail, true));
        var cut = GridOf(host);
        await TypeFiveAsync(cut);

        await Assert.ThrowsAsync<InvalidOperationException>(() => KeyAsync(cut, "Enter"));

        Assert.Equal(["edit 5", "discard ColumnsChanged"], host.Instance.Heard);
        Assert.False(EditorOpen(cut));
    }

    /// <summary>A page that pushes its Window and, as it hears an Edit Intent, shows other columns:
    /// Blazor renders the page, and so the grid's new columns, as the handler completes.</summary>
    private sealed class ColumnsHost : ComponentBase
    {
        [Parameter, EditorRequired] public TestRow[] Rows { get; set; } = [];

        /// <summary>Whether the edit's handler shows other columns.</summary>
        [Parameter] public bool ChangeColumns { get; set; } = true;

        /// <summary>Whether the page refuses the Edit Intent.</summary>
        [Parameter] public bool Refuse { get; set; }

        /// <summary>Whether the edit's handler fails once it has shown other columns.</summary>
        [Parameter] public bool Fail { get; set; }

        [Parameter] public bool ShowFormulaBar { get; set; }

        /// <summary>What the page heard, in order.</summary>
        public List<string> Heard { get; } = [];

        private TestRow[]? _window;
        private GridColumn<TestRow>[] _columns = Columns();

        /// <summary>The page shows other columns, outside any commit.</summary>
        public void ShowOtherColumns()
        {
            _columns = OtherColumns();
            StateHasChanged();
        }

        /// <summary>The first row changes upstream: a new instance with another Book.</summary>
        public void ChangeFirstRow(string book)
        {
            var window = (TestRow[])_window!.Clone();
            var old = window[0];
            window[0] = new TestRow { Book = book, Amount = old.Amount, AsOf = old.AsOf, Active = old.Active };
            _window = window;
            StateHasChanged();
        }

        protected override void OnParametersSet() => _window ??= Rows;

        private void OnEdit(GridEditIntent<TestRow> intent)
        {
            Heard.Add("edit " + intent.Value);
            if (Refuse)
                intent.Refuse("Not above 150");
            if (ChangeColumns)
                _columns = OtherColumns();
            if (Fail)
            {
                StateHasChanged();
                throw new InvalidOperationException("The page failed after showing other columns.");
            }
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ExGrid<TestRow>>(0);
            builder.AddComponentParameter(1, nameof(ExGrid<TestRow>.Window), _window);
            builder.AddComponentParameter(2, nameof(ExGrid<TestRow>.TotalCount), (int?)_window!.Length);
            builder.AddComponentParameter(3, nameof(ExGrid<TestRow>.Columns), (IReadOnlyList<GridColumn<TestRow>>)_columns);
            builder.AddComponentParameter(4, nameof(ExGrid<TestRow>.RowHeight), 20d);
            builder.AddComponentParameter(5, nameof(ExGrid<TestRow>.ViewportHeight), (ViewportSize)120);
            builder.AddComponentParameter(6, nameof(ExGrid<TestRow>.ViewportWidth), (ViewportSize)350);
            builder.AddComponentParameter(7, nameof(ExGrid<TestRow>.ShowFormulaBar), ShowFormulaBar);
            builder.AddComponentParameter(8, nameof(ExGrid<TestRow>.OnEdit),
                EventCallback.Factory.Create<GridEditIntent<TestRow>>(this, OnEdit));
            builder.AddComponentParameter(9, nameof(ExGrid<TestRow>.OnEditDiscarded),
                EventCallback.Factory.Create<EditDiscardReason>(this, reason => Heard.Add("discard " + reason)));
            builder.AddComponentParameter(10, nameof(ExGrid<TestRow>.OnOverwriteNotice),
                EventCallback.Factory.Create<GridOverwriteNotice>(this, notice => Heard.Add("notice " + notice.ReplacedText)));
            builder.CloseComponent();
        }
    }
}
