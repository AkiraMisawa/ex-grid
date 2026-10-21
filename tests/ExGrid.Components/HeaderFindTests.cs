using AngleSharp.Dom;
using Bunit;
using ExGrid.Cells;
using ExGrid.Chrome;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Finding;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Find reaches the column headers (ADR-0333): the grid searches its visible column headers and Header
/// Group labels itself, before the data rows, and asks the Consumer only about rows, with ADR-0055's
/// request unchanged. A header found is the current match — its Match Mark the current one, scrolled
/// into view across, the panel saying so — and the Focus stays. 20px rows in a 120px Viewport 350px
/// wide, six 100px columns: Book ("Row 000000" …), Label ("Total 0" …) under the header "Total", Amount,
/// AsOf under "As of", and Desk A and Desk B ("-"). The Consumer answers by the reference rule.
/// </summary>
public class HeaderFindTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns(bool bookEditable = false) =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: bookEditable),
        new("Label", ColumnType.Text, r => "Total " + r.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture), header: "Total", width: Fixed100),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
        new("AsOf", ColumnType.Date, r => r.AsOf, header: "As of", width: Fixed100),
        new("DeskA", ColumnType.Text, _ => "-", header: "Desk A", width: Fixed100),
        new("DeskB", ColumnType.Text, _ => "-", header: "Desk B", width: Fixed100),
    ];

    private sealed class Heard
    {
        public List<GridFindRequest> Requests { get; } = [];
        public List<FindRefusalReason> Refusals { get; } = [];
        public List<GridEditIntent<TestRow>> Edits { get; } = [];
        public GridSelection? Selection;
    }

    private const int RowCount = 50;

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        Heard heard,
        Func<GridFindRequest, Task<GridFindResult>>? answer = null,
        Action<ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? more = null,
        bool bookEditable = false)
    {
        var window = TestRows.Many(RowCount);
        var columns = Columns(bookEditable);
        var reference = (Func<GridFindRequest, Task<GridFindResult>>)(request =>
            Task.FromResult(GridFind.Step(window, request, name => columns.FirstOrDefault(c => c.Name == name) is { } column ? column.Info.TextOf : null)));
        return Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, window)
              .Add(g => g.TotalCount, RowCount)
              .Add(g => g.Columns, columns)
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.OnFindRefused, r => heard.Refusals.Add(r))
              .Add(g => g.SelectionChanged, s => heard.Selection = s)
              .Add(g => g.OnFind, (request, _) =>
              {
                  heard.Requests.Add(request);
                  return (answer ?? reference)(request);
              });
            more?.Invoke(ps);
        });
    }

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, false, false, false, false));

    private static Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y, bool shift = false)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y, ShiftKey = shift });

    private static async Task OpenAsync(IRenderedComponent<ExGrid<TestRow>> cut, string text, bool replace = false)
    {
        await PressAsync(cut, replace ? "h" : "f", ctrl: true);
        await cut.Find("input.ex-find-field").InputAsync(new ChangeEventArgs { Value = text });
    }

    private static Task NextAsync(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find("button.ex-find-next").ClickAsync(new MouseEventArgs());

    private static Task PreviousAsync(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find("button.ex-find-previous").ClickAsync(new MouseEventArgs());

    private static IElement Header(IRenderedComponent<ExGrid<TestRow>> cut, string text)
        => cut.FindAll(".ex-header-cell, .ex-header-group").Single(e => e.TextContent.Trim() == text);

    private static string Outcome(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find(".ex-find-outcome").TextContent.Trim();

    private static string[] Current(IRenderedComponent<ExGrid<TestRow>> cut)
        => [.. cut.FindAll(".ex-match-mark-current").Select(e => e.ClassList.Contains("ex-cell") ? "cell" : e.TextContent.Trim())];

    [Fact] // ADR-0333 §1/§3: a header the walk reaches is the current match, the panel says so, and the Focus stays where it was
    public async Task ADR0333_a_header_found_is_the_current_match_and_the_focus_stays()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard);
        await ClickCellAsync(cut, 50, 30); // (1, 0)
        await OpenAsync(cut, "amount");

        await NextAsync(cut);

        // The Consumer was asked, as ADR-0055 asks, and found nothing among the rows.
        var request = Assert.Single(heard.Requests);
        Assert.Equal(new CellPosition(1, 0), request.From);
        Assert.Equal(new CellPosition(1, 0), heard.Selection!.Focus);
        Assert.Equal(["Amount"], Current(cut));
        Assert.Contains("ex-match-mark", Header(cut, "Amount").ClassList);
        // The panel names the header, as the Focus does not show where the match is; the header says it
        // is the current one to assistive technology, and no other header does.
        Assert.Equal("Found in the header Amount", Outcome(cut));
        Assert.Equal("status", cut.Find(".ex-find-outcome").GetAttribute("role"));
        Assert.Equal("true", Header(cut, "Amount").GetAttribute("aria-current"));
        Assert.Single(cut.FindAll("[aria-current]"));
        Assert.Empty(heard.Refusals);
    }

    [Fact] // ADR-0333 §3: forward, an answer that wraps past the end gives way to the headers, and the next step goes on from the header to the first row
    public async Task ADR0333_forward_the_headers_come_between_the_last_row_and_the_first()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard);
        await cut.InvokeAsync(() => cut.Instance.PlaceSelectionAsync(new SelectionRange(RowCount - 1, 1, 1, 1), new CellPosition(RowCount - 1, 1), 0));
        await OpenAsync(cut, "Total");

        await NextAsync(cut);

        Assert.Equal(["Total"], Current(cut));
        Assert.Equal(new CellPosition(RowCount - 1, 1), heard.Selection!.Focus);

        await NextAsync(cut);

        // From a header, the rows are searched from their start.
        Assert.Null(heard.Requests[^1].From);
        Assert.Equal(new CellPosition(0, 1), heard.Selection!.Focus);
        // The header is current no longer; the cell is (marked only where a match function is passed).
        Assert.DoesNotContain("ex-match-mark-current", Header(cut, "Total").ClassList);
        Assert.Equal("", Outcome(cut));
    }

    [Fact] // ADR-0333 §3: an answer after the Focus stands, the headers lying behind it
    public async Task ADR0333_an_answer_ahead_of_the_focus_stands()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard);
        await ClickCellAsync(cut, 50, 50); // (2, 0)
        await OpenAsync(cut, "Total");

        await NextAsync(cut);

        Assert.Equal(new CellPosition(2, 1), heard.Selection!.Focus);
        Assert.DoesNotContain("ex-match-mark-current", Header(cut, "Total").ClassList);
    }

    [Fact] // ADR-0333 §3: backward, the headers come between the first row and the last
    public async Task ADR0333_backward_the_headers_come_between_the_first_row_and_the_last()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard);
        await ClickCellAsync(cut, 50, 10); // (0, 0)
        await OpenAsync(cut, "Total");

        await PreviousAsync(cut);

        Assert.Equal(["Total"], Current(cut));
        Assert.Equal(new CellPosition(0, 0), heard.Selection!.Focus);

        await PreviousAsync(cut);

        Assert.True(heard.Requests[^1].Backward);
        Assert.Null(heard.Requests[^1].From);
        Assert.Equal(new CellPosition(RowCount - 1, 1), heard.Selection!.Focus);
    }

    [Fact] // ADR-0333 §3: from no Focus the walk starts at the top; header to header nobody is asked; past the last header the rows are asked, and a walk with no row matching wraps to the first header
    public async Task ADR0333_header_to_header_asks_nothing_and_wraps()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard);
        await OpenAsync(cut, "Desk");

        await NextAsync(cut);
        Assert.Equal(["Desk A"], Current(cut));
        Assert.Empty(heard.Requests);

        await NextAsync(cut);
        Assert.Equal(["Desk B"], Current(cut));
        Assert.Empty(heard.Requests);

        await NextAsync(cut);
        Assert.Equal(["Desk A"], Current(cut));
        var asked = Assert.Single(heard.Requests);
        Assert.Null(asked.From);
        Assert.Empty(heard.Refusals);

        // Backward from the first header the rows come next, from their end, and none matches.
        await PreviousAsync(cut);
        Assert.Equal(["Desk B"], Current(cut));
        Assert.Equal(2, heard.Requests.Count);
        Assert.True(heard.Requests[^1].Backward);
        Assert.Null(heard.Requests[^1].From);
    }

    [Fact] // ADR-0333 §3: the Header Group row comes before the column header row
    public async Task ADR0333_a_header_group_label_is_found_before_the_column_headers()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, more: ps => ps.Add(g => g.HeaderGroups, (HeaderGroup[])[new("Desks", ["DeskA", "DeskB"])]));
        await OpenAsync(cut, "Desk");

        await NextAsync(cut);
        Assert.Equal(["Desks"], Current(cut));
        Assert.Contains("ex-match-mark-current", Header(cut, "Desks").ClassList);
        Assert.Equal("true", Header(cut, "Desks").GetAttribute("aria-current"));
        Assert.Equal("Found in the header Desks", Outcome(cut));

        await NextAsync(cut);
        Assert.Equal(["Desk A"], Current(cut));
        Assert.Null(Header(cut, "Desks").GetAttribute("aria-current"));
        Assert.DoesNotContain("ex-match-mark-current", Header(cut, "Desks").ClassList);
        Assert.Contains("ex-match-mark", Header(cut, "Desks").ClassList);
    }

    [Fact] // ADR-0333 §1: every header in view that matches is marked while the panel holds a search, by the reference rule, though the Consumer answers Find with no match function of its own
    public async Task ADR0333_every_matching_header_in_view_is_marked()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, more: ps => ps.Add(g => g.HeaderGroups, (HeaderGroup[])[new("Desks", ["DeskA", "DeskB"])]));

        await OpenAsync(cut, "desk");

        Assert.Contains("ex-match-mark", Header(cut, "Desks").ClassList);
        Assert.DoesNotContain("ex-match-mark", Header(cut, "Total").ClassList);
        Assert.Empty(cut.FindAll(".ex-match-mark-current"));

        await cut.Find("input.ex-find-field").InputAsync(new ChangeEventArgs { Value = "TOTAL" });
        Assert.Contains("ex-match-mark", Header(cut, "Total").ClassList);
        Assert.DoesNotContain("ex-match-mark", Header(cut, "Book").ClassList);
        Assert.DoesNotContain("ex-match-mark", Header(cut, "Desks").ClassList);
        // No cell is marked: the Consumer passed no match function (ADR-0332 §4).
        Assert.Empty(cut.FindAll(".ex-cell.ex-match-mark"));

        await cut.Find("input.ex-find-match-case").ChangeAsync(new ChangeEventArgs { Value = true });
        Assert.DoesNotContain("ex-match-mark", Header(cut, "Total").ClassList);

        await PressAsync(cut, "Escape");
        Assert.Empty(cut.FindAll(".ex-header-cell.ex-match-mark, .ex-header-group.ex-match-mark"));
    }

    [Fact] // ADR-0333 §1: a header found is scrolled into view across, and the rows stay where they are
    public async Task ADR0333_a_header_found_is_revealed_across()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard);
        await ClickCellAsync(cut, 50, 10);
        await OpenAsync(cut, "Desk B");

        await NextAsync(cut);

        Assert.Equal(["Desk B"], Current(cut));
        Assert.Contains(Js.ScrolledTo, offset => offset.Left > 0 && offset.Top == 0);
    }

    [Fact] // ADR-0333 §3: a selection of more than one cell as the scope searches no header
    public async Task ADR0333_a_selection_as_the_scope_searches_no_header()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard);
        await ClickCellAsync(cut, 50, 10);
        await ClickCellAsync(cut, 150, 50, shift: true); // (0,0)–(2,1)
        await OpenAsync(cut, "Amount");

        await NextAsync(cut);

        Assert.NotNull(Assert.Single(heard.Requests).Scope);
        Assert.Equal([FindRefusalReason.NotFound], heard.Refusals);
        Assert.Empty(cut.FindAll(".ex-match-mark-current"));
    }

    [Fact] // ADR-0333 §2: FindsHeaders off searches no header and marks none
    public async Task ADR0333_finds_headers_off_searches_no_header()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, more: ps => ps.Add(g => g.FindsHeaders, false));
        await OpenAsync(cut, "Amount");

        Assert.DoesNotContain("ex-match-mark", Header(cut, "Amount").ClassList);
        await NextAsync(cut);

        Assert.Equal([FindRefusalReason.NotFound], heard.Refusals);
        Assert.Equal("No match", Outcome(cut));
    }

    [Fact] // ADR-0333 §2: a hidden header is not searched
    public async Task ADR0333_a_hidden_header_is_not_searched()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, more: ps => ps.Add(g => g.HideHeader, true));
        await OpenAsync(cut, "Amount");

        await NextAsync(cut);

        Assert.Equal([FindRefusalReason.NotFound], heard.Refusals);
    }

    [Fact] // ADR-0333 §1: the user moving the Selection leaves the header, and the next step starts from the Focus they moved to
    public async Task ADR0333_moving_the_selection_leaves_the_header()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard);
        await ClickCellAsync(cut, 50, 30);
        await OpenAsync(cut, "Total");
        await cut.InvokeAsync(() => cut.Instance.PlaceSelectionAsync(new SelectionRange(RowCount - 1, 1, 1, 1), new CellPosition(RowCount - 1, 1), 0));
        await NextAsync(cut);
        Assert.Equal(["Total"], Current(cut));

        await cut.InvokeAsync(() => cut.Instance.PlaceSelectionAsync(new SelectionRange(4, 0, 1, 1), new CellPosition(4, 0), 0));

        Assert.Empty(cut.FindAll(".ex-match-mark-current"));
        // The panel no longer says a header was found: none is the current match.
        Assert.Equal("", Outcome(cut));
        await NextAsync(cut);
        Assert.Equal(new CellPosition(4, 0), heard.Requests[^1].From);
        Assert.Equal(new CellPosition(4, 1), heard.Selection!.Focus);
    }

    [Fact] // ADR-0333 §3 / ADR-0055 / spine 6: headers that moved while the step was out are the visible columns having moved — the step lands on nothing
    public async Task ADR0333_headers_moved_while_the_step_was_out_is_order_changed()
    {
        var heard = new Heard();
        var answer = new TaskCompletionSource<GridFindResult>();
        var cut = RenderGrid(heard, _ => answer.Task);
        await ClickCellAsync(cut, 50, 30);
        await OpenAsync(cut, "Amount");
        // Not awaited: the step waits on the answer given below.
        var step = NextAsync(cut);

        cut.Render(ps => ps.Add(g => g.Columns, Columns().Reverse().ToArray()));
        await cut.InvokeAsync(() => answer.SetResult(GridFindResult.NotFound));
        await step;

        Assert.Equal([FindRefusalReason.OrderChanged], heard.Refusals);
        Assert.Empty(cut.FindAll(".ex-match-mark-current"));
    }

    [Fact] // ADR-0333 §4: Replace with a header as the current match writes nothing and finds the next match
    public async Task ADR0333_replace_on_a_header_writes_nothing_and_finds_the_next()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, bookEditable: true, more: ps => ps
            .Add(g => g.OnEdit, intent => heard.Edits.Add(intent))
            .Add(g => g.OnReplace, request => Task.FromResult(GridReplaceResult.Replaced(0, request.RowSequenceVersion))));
        await cut.InvokeAsync(() => cut.Instance.PlaceSelectionAsync(new SelectionRange(RowCount - 1, 0, 1, 1), new CellPosition(RowCount - 1, 0), 0));
        await OpenAsync(cut, "Book", replace: true);
        await cut.Find("input.ex-find-replace-field").InputAsync(new ChangeEventArgs { Value = "Ledger" });
        await NextAsync(cut);
        Assert.Equal(["Book"], Current(cut));

        await cut.Find("button.ex-find-replace").ClickAsync(new MouseEventArgs());

        Assert.Empty(heard.Edits);
        // No row says "Book", so the walk comes round to the header again, the one match there is.
        Assert.Equal(["Book"], Current(cut));
        Assert.Null(heard.Requests[^1].From);
        Assert.Equal(new CellPosition(RowCount - 1, 0), heard.Selection!.Focus);
    }

    /// <summary>A Chrome whose find panel records what it was handed and renders a marker.</summary>
    private sealed class FindChrome : IGridChrome
    {
        public FindContext? Find { get; private set; }

        public RenderFragment? FindPanel(FindContext context)
        {
            Find = context;
            return builder => builder.AddMarkupContent(0, "<div class='ex-stub-find'>stub find</div>");
        }

        public RenderFragment? FilterPanel(FilterPanelContext context) => null;

        public RenderFragment? ColumnMenu(ColumnMenuContext context) => null;

        public RenderFragment? ContextMenu<TRow>(ContextMenuContext<TRow> context) => null;

        public RenderFragment? CellEditor(CellEditorContext context) => null;

        public RenderFragment? LoadingIndicator(LoadingContext context) => null;
    }

    [Fact] // ADR-0333 §1 / ADR-0055: a substituted panel's text marks the headers as it is typed, though OnFind answers with no match function, and the context names the header a step found
    public async Task ADR0333_a_substituted_panel_marks_the_headers_as_it_is_typed()
    {
        var heard = new Heard();
        var chrome = new FindChrome();
        var cut = RenderGrid(heard, more: ps => ps.Add(g => g.Chrome, chrome));
        await PressAsync(cut, "f", ctrl: true);

        // From the Chrome's own event, not the grid's: the grid renders itself where a mark moves.
        await cut.InvokeAsync(() => chrome.Find!.TextChanged("amount"));

        Assert.Contains("ex-match-mark", Header(cut, "Amount").ClassList);

        await cut.InvokeAsync(chrome.Find!.Next);

        Assert.Equal(FindOutcome.FoundInHeader, chrome.Find!.Outcome);
        Assert.Equal("Amount", chrome.Find.FoundHeader);
        Assert.Equal("Found in the header Amount", FindPanelLabelIds.OutcomeText(chrome.Find, BuiltInCommandLabels.For));
    }

    [Fact] // ADR-0333 §3: with no Focus, the header found is still the one the next step goes on from after the columns are reordered, and the one marked current
    public async Task ADR0333_a_reorder_with_no_focus_keeps_the_header_found()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard);
        await OpenAsync(cut, "Desk");
        await NextAsync(cut);
        Assert.Equal(["Desk A"], Current(cut));

        var columns = Columns();
        cut.Render(ps => ps.Add(g => g.Columns, [columns[0], columns[1], columns[2], columns[4], columns[3], columns[5]]));

        Assert.Equal(["Desk A"], Current(cut));
        Assert.Equal("Found in the header Desk A", Outcome(cut));
        await NextAsync(cut);
        Assert.Equal(["Desk B"], Current(cut));
        Assert.Empty(heard.Requests);
    }

    [Fact] // ADR-0333 §1: a Header Group found stays the current match while it is declared again as it was, and is forgotten, with what the panel said, once it is no longer declared
    public async Task ADR0333_a_header_group_found_follows_the_header_groups()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, more: ps => ps.Add(g => g.HeaderGroups, (HeaderGroup[])[new("Desks", ["DeskA", "DeskB"])]));
        await OpenAsync(cut, "Desk");
        await NextAsync(cut);
        Assert.Equal(["Desks"], Current(cut));

        cut.Render(ps => ps.Add(g => g.HeaderGroups, (HeaderGroup[])[new("Desks", ["DeskA", "DeskB"])]));
        Assert.Equal(["Desks"], Current(cut));
        Assert.Equal("Found in the header Desks", Outcome(cut));

        cut.Render(ps => ps.Add(g => g.HeaderGroups, (HeaderGroup[])[]));
        Assert.Empty(cut.FindAll(".ex-match-mark-current"));
        Assert.Equal("", Outcome(cut));

        // The next step starts from the top again: the first header that matches.
        await NextAsync(cut);
        Assert.Equal(["Desk A"], Current(cut));
    }

    [Fact] // ADR-0333 §2: a column left out of Columns is not searched, nor marked
    public async Task ADR0333_a_column_left_out_is_not_searched()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard);
        cut.Render(ps => ps.Add(g => g.Columns, Columns().Where(c => c.Name != "Amount").ToArray()));
        await OpenAsync(cut, "Amount");

        await NextAsync(cut);

        Assert.Empty(cut.FindAll(".ex-header-cell.ex-match-mark"));
        Assert.Equal([FindRefusalReason.NotFound], heard.Refusals);
        Assert.Equal("No match", Outcome(cut));
    }

    [Fact] // ADR-0333 §2 / ADR-0055: the headers turned off while a step was out are not searched, and the answer stands alone — no match, not a reorder
    public async Task ADR0333_finds_headers_turned_off_while_the_step_was_out_falls_to_the_answer()
    {
        var heard = new Heard();
        var answer = new TaskCompletionSource<GridFindResult>();
        var cut = RenderGrid(heard, _ => answer.Task);
        await ClickCellAsync(cut, 50, 30);
        await OpenAsync(cut, "Amount");
        // Not awaited: the step waits on the answer given below.
        var step = NextAsync(cut);

        cut.Render(ps => ps.Add(g => g.FindsHeaders, false));
        await cut.InvokeAsync(() => answer.SetResult(GridFindResult.NotFound));
        await step;

        Assert.Equal([FindRefusalReason.NotFound], heard.Refusals);
        Assert.Empty(cut.FindAll(".ex-match-mark-current"));
    }

    [Fact] // ADR-0333 §3: a header whose text changed while the step was out is judged as it now reads, and stands where it stood
    public async Task ADR0333_a_header_retitled_while_the_step_was_out_is_judged_as_it_reads()
    {
        var heard = new Heard();
        var answer = new TaskCompletionSource<GridFindResult>();
        var cut = RenderGrid(heard, _ => answer.Task);
        await ClickCellAsync(cut, 50, 30);
        await OpenAsync(cut, "Total");
        // Not awaited: the step waits on the answer given below.
        var step = NextAsync(cut);

        var columns = Columns();
        columns[2] = new("Amount", ColumnType.Number, r => r.Amount, header: "Total amount", width: Fixed100);
        cut.Render(ps => ps.Add(g => g.Columns, columns));
        await cut.InvokeAsync(() => answer.SetResult(GridFindResult.NotFound));
        await step;

        Assert.Empty(heard.Refusals);
        Assert.Equal(["Total"], Current(cut));
        Assert.Equal("Found in the header Total", Outcome(cut));
    }
}
