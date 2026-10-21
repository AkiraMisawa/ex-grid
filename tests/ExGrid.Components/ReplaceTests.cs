using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Finding;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Replace (ADR-0332 §2 and §3): Ctrl+H opens the find panel with Replace where a Replace can land;
/// Replace is one Edit Intent on the current match and then the next; Replace All is one Replace
/// Request the Consumer answers. 20px rows in a 120px Viewport, three 100px columns: Book (editable),
/// Amount, AsOf.
/// </summary>
public class ReplaceTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns(bool bookEditable = true) =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: bookEditable,
            validate: (_, text) => text.Contains('!', StringComparison.Ordinal) ? EditVerdict.Reject("no exclamations") : EditVerdict.Accept),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
        new("AsOf", ColumnType.Date, r => r.AsOf, width: Fixed100),
    ];

    private sealed class Heard
    {
        public List<GridFindRequest> Finds { get; } = [];
        public List<GridReplaceRequest> Replaces { get; } = [];
        public List<GridEditIntent<TestRow>> Edits { get; } = [];
        public List<FindRefusalReason> Refusals { get; } = [];
        public GridSelection? Selection;
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        Heard heard,
        Func<GridFindRequest, GridFindResult>? find,
        Func<GridReplaceRequest, Task<GridReplaceResult>>? replace,
        bool bookEditable = true,
        Action<GridEditIntent<TestRow>>? onEdit = null,
        bool withEdit = true)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(500))
              .Add(g => g.TotalCount, 500)
              .Add(g => g.Columns, Columns(bookEditable))
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.OnFindRefused, r => heard.Refusals.Add(r))
              .Add(g => g.SelectionChanged, s => heard.Selection = s);
            if (withEdit)
            {
                ps.Add(g => g.OnEdit, intent =>
                {
                    heard.Edits.Add(intent);
                    onEdit?.Invoke(intent);
                });
            }
            if (find is not null)
            {
                ps.Add(g => g.OnFind, (request, _) =>
                {
                    heard.Finds.Add(request);
                    return Task.FromResult(find(request));
                });
            }
            if (replace is not null)
            {
                ps.Add(g => g.OnReplace, request =>
                {
                    heard.Replaces.Add(request);
                    return replace(request);
                });
            }
        });

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, false, false, false, false));

    private static Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y, bool shift = false)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y, ShiftKey = shift });

    private static async Task TypeAsync(IRenderedComponent<ExGrid<TestRow>> cut, string find, string replacement)
    {
        await cut.Find("input.ex-find-field").InputAsync(new ChangeEventArgs { Value = find });
        await cut.Find("input.ex-find-replace-field").InputAsync(new ChangeEventArgs { Value = replacement });
    }

    private static Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, string selector)
        => cut.Find(selector).ClickAsync(new MouseEventArgs());

    private static Func<GridFindRequest, GridFindResult> At(int row, string column)
        => _ => GridFindResult.Found(row, column);

    private static Func<GridReplaceRequest, Task<GridReplaceResult>> Count(int count)
        => request => Task.FromResult(GridReplaceResult.Replaced(count, request.RowSequenceVersion));

    [Fact] // ADR-0332 §2: Ctrl+H is claimed on every grid, as Ctrl+F is
    public void ADR0332_the_gate_takes_ctrl_h_on_every_grid()
    {
        RenderGrid(new Heard(), find: null, replace: null);

        Assert.Contains("Control+h", Js.TakenAtAttach);
        Assert.Contains("Control+H", Js.TakenAtAttach);
        Assert.False(Js.ReplaceableAtAttach);
    }

    [Fact] // ADR-0332 §2: the gate is told a Replace can land only where one can
    public void ADR0332_the_gate_is_told_when_a_replace_can_land()
    {
        RenderGrid(new Heard(), At(0, "Book"), Count(1));

        Assert.True(Js.ReplaceableAtAttach);
    }

    [Fact] // ADR-0332 §2: with nothing handling the Replace Request, Ctrl+H opens nothing and says so
    public async Task ADR0332_ctrl_h_without_a_replace_request_handler_is_refused()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, At(0, "Book"), replace: null);

        await PressAsync(cut, "h", ctrl: true);

        Assert.Equal([FindRefusalReason.ReplaceUnavailable], heard.Refusals);
        Assert.Empty(cut.FindAll(".ex-popover"));
    }

    [Fact] // ADR-0332 §2: with no visible column Editable, Ctrl+H opens nothing and says so
    public async Task ADR0332_ctrl_h_with_no_editable_column_is_refused()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, At(0, "Book"), Count(1), bookEditable: false);

        await PressAsync(cut, "h", ctrl: true);

        Assert.Equal([FindRefusalReason.ReplaceUnavailable], heard.Refusals);
        Assert.Empty(cut.FindAll(".ex-popover"));
    }

    [Fact] // ADR-0332 §2: with no search wired there is no next match, so Ctrl+H is refused
    public async Task ADR0332_ctrl_h_with_no_search_is_refused()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, find: null, Count(1));

        await PressAsync(cut, "h", ctrl: true);

        Assert.Equal([FindRefusalReason.ReplaceUnavailable], heard.Refusals);
        Assert.Empty(cut.FindAll(".ex-popover"));
    }

    [Fact] // ADR-0332 §2: Ctrl+H opens the panel with Replace; Ctrl+F turns it back into Find alone
    public async Task ADR0332_ctrl_h_opens_the_panel_with_replace_and_ctrl_f_without()
    {
        var cut = RenderGrid(new Heard(), At(0, "Book"), Count(1));

        await PressAsync(cut, "h", ctrl: true);

        var panel = cut.Find(".ex-popover-find");
        Assert.Equal("Find and Replace", panel.GetAttribute("aria-label"));
        Assert.Single(cut.FindAll("input.ex-find-field"));
        Assert.Single(cut.FindAll("input.ex-find-replace-field"));
        Assert.Equal("click", cut.Find("button.ex-find-replace").GetAttribute("data-ex-claims"));
        Assert.Equal("click", cut.Find("button.ex-find-replace-all").GetAttribute("data-ex-claims"));

        await PressAsync(cut, "f", ctrl: true);

        Assert.Equal("Find", cut.Find(".ex-popover-find").GetAttribute("aria-label"));
        Assert.Empty(cut.FindAll("input.ex-find-replace-field"));
    }

    [Fact] // ADR-0332 §2 / PF-3: the Replace field keeps its handlers across typing, as the find field does
    public async Task ADR0332_the_replace_field_keeps_its_handlers_across_typing()
    {
        var cut = RenderGrid(new Heard(), At(0, "Book"), Count(1));
        await PressAsync(cut, "h", ctrl: true);
        string? Handler() => cut.Find("input.ex-find-replace-field").GetAttribute("blazor:oninput");
        var before = Handler();
        Assert.NotNull(before);

        await cut.Find("input.ex-find-replace-field").InputAsync(new ChangeEventArgs { Value = "a" });
        await cut.Find("input.ex-find-field").InputAsync(new ChangeEventArgs { Value = "b" });

        Assert.Equal(before, Handler());
    }

    [Fact] // ADR-0332 §3: Replace All is one request with Find's fields, the replacement, the Editable columns and the version
    public async Task ADR0332_replace_all_raises_one_request_with_every_field()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, At(0, "Book"), Count(12));
        cut.Render(ps => ps.Add(g => g.RowSequenceVersion, 7));
        await ClickCellAsync(cut, 50, 10);
        await ClickCellAsync(cut, 150, 70, shift: true); // (0,0)–(3,1)
        await PressAsync(cut, "h", ctrl: true);
        await cut.Find("input.ex-find-whole-cell").ChangeAsync(new ChangeEventArgs { Value = true });
        await TypeAsync(cut, "Row", "Line");

        await ClickAsync(cut, "button.ex-find-replace-all");

        var request = Assert.Single(heard.Replaces);
        Assert.Equal("Row", request.Text);
        Assert.Equal("Line", request.Replacement);
        Assert.False(request.MatchCase);
        Assert.True(request.WholeCell);
        Assert.Equal([new SelectionRange(0, 0, 4, 2)], request.Scope);
        Assert.Equal(["Book", "Amount", "AsOf"], request.Columns);
        Assert.Equal(["Book"], request.EditableColumns);
        Assert.Equal(7, request.RowSequenceVersion);
        // The grid writes nothing itself and asks no step: it says the count.
        Assert.Empty(heard.Edits);
        Assert.Empty(heard.Finds);
        Assert.Equal("Replaced 12 cells.", cut.Find(".ex-find-outcome").TextContent);
        Assert.Empty(heard.Refusals);
    }

    [Fact] // ADR-0332 §2: one cell replaced is said in the singular
    public async Task ADR0332_one_cell_replaced_is_said()
    {
        var cut = RenderGrid(new Heard(), At(0, "Book"), Count(1));
        await PressAsync(cut, "h", ctrl: true);
        await TypeAsync(cut, "Row 000001", "x");

        await ClickAsync(cut, "button.ex-find-replace-all");

        Assert.Equal("Replaced 1 cell.", cut.Find(".ex-find-outcome").TextContent);
    }

    [Fact] // ADR-0332 §3: a Replace All that replaced nothing is said as no match
    public async Task ADR0332_replace_all_that_replaced_nothing_is_not_found()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, At(0, "Book"), Count(0));
        await PressAsync(cut, "h", ctrl: true);
        await TypeAsync(cut, "zzz", "x");

        await ClickAsync(cut, "button.ex-find-replace-all");

        Assert.Equal([FindRefusalReason.NotFound], heard.Refusals);
        Assert.Equal("No match", cut.Find(".ex-find-outcome").TextContent);
    }

    [Fact] // ADR-0332 §2: a refused Replace All names the cells and why, the first five, and counts the rest
    public async Task ADR0332_a_refused_replace_all_names_the_cells()
    {
        var heard = new Heard();
        GridReplaceRefusedCell[] cells =
        [
            new(3, "Amount", null), new(4, "Book", "too long"), new(5, "Book", "too long"),
            new(6, "Book", "too long"), new(7, "Book", "too long"), new(8, "Book", "too long"), new(9, "Book", "too long"),
        ];
        var cut = RenderGrid(heard, At(0, "Book"), request => Task.FromResult(GridReplaceResult.Refused(cells, request.RowSequenceVersion)));
        await PressAsync(cut, "h", ctrl: true);
        await TypeAsync(cut, "0", "x");

        await ClickAsync(cut, "button.ex-find-replace-all");

        Assert.Equal([FindRefusalReason.ReplaceRefused], heard.Refusals);
        Assert.Equal(
            "Nothing was replaced. Row 4, Amount: this column cannot be edited. Row 5, Book: too long. Row 6, Book: too long. "
            + "Row 7, Book: too long. Row 8, Book: too long. And 2 more.",
            cut.Find(".ex-find-outcome").TextContent);
    }

    [Fact] // ADR-0332 §3 / ADR-0011: an answer under another version than the request's is refused as the order having moved
    public async Task ADR0332_an_answer_under_another_version_is_refused()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, At(0, "Book"), request => Task.FromResult(GridReplaceResult.Replaced(5, request.RowSequenceVersion + 1)));
        await PressAsync(cut, "h", ctrl: true);
        await TypeAsync(cut, "Row", "x");

        await ClickAsync(cut, "button.ex-find-replace-all");

        Assert.Equal([FindRefusalReason.OrderChanged], heard.Refusals);
        Assert.Equal("The rows moved; find again", cut.Find(".ex-find-outcome").TextContent);
    }

    [Fact] // ADR-0332 §3: a Replace All whose answer moves the order (its own write) is still its count
    public async Task ADR0332_the_consumers_own_write_moving_the_order_keeps_the_count()
    {
        var heard = new Heard();
        IRenderedComponent<ExGrid<TestRow>>? grid = null;
        var cut = grid = RenderGrid(heard, At(0, "Book"), async request =>
        {
            // The write re-sorts the rows: the grid's version moves while the request is out.
            await grid!.InvokeAsync(() => grid!.Render(ps => ps.Add(g => g.RowSequenceVersion, request.RowSequenceVersion + 1)));
            return GridReplaceResult.Replaced(3, request.RowSequenceVersion);
        });
        await PressAsync(cut, "h", ctrl: true);
        await TypeAsync(cut, "Row", "x");

        await ClickAsync(cut, "button.ex-find-replace-all");

        Assert.Empty(heard.Refusals);
        Assert.Equal("Replaced 3 cells.", cut.Find(".ex-find-outcome").TextContent);
    }

    [Fact] // ADR-0332 §2: Replace with no current match finds the next one first, and writes nothing
    public async Task ADR0332_replace_with_no_current_match_finds_first()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, At(3, "Book"), Count(1));
        await PressAsync(cut, "h", ctrl: true);
        await TypeAsync(cut, "Row", "Line");

        await ClickAsync(cut, "button.ex-find-replace");

        Assert.Single(heard.Finds);
        Assert.Empty(heard.Edits);
        Assert.Equal(new CellPosition(3, 0), heard.Selection!.Focus);
    }

    [Fact] // ADR-0332 §2: Replace writes the current match as one Edit Intent of its replaced entry, then finds the next
    public async Task ADR0332_replace_writes_the_current_match_as_an_edit_intent_then_finds_the_next()
    {
        var heard = new Heard();
        var answers = new Queue<GridFindResult>([GridFindResult.Found(3, "Book"), GridFindResult.Found(4, "Book")]);
        var cut = RenderGrid(heard, _ => answers.Dequeue(), Count(1));
        await PressAsync(cut, "h", ctrl: true);
        await TypeAsync(cut, "row", "Line");
        await ClickAsync(cut, "button.ex-find-next");

        await ClickAsync(cut, "button.ex-find-replace");

        var intent = Assert.Single(heard.Edits);
        Assert.Equal("Book", intent.Column);
        // Every occurrence, compared ignoring case; the entry is the text the editor opens with.
        Assert.Equal("Line 000003", intent.Value);
        Assert.Equal("Row 000003", intent.Row.Book);
        Assert.Equal("Row 000003", intent.SeenText);
        Assert.Equal("Row 000003", intent.ReplacedText);
        Assert.Equal(2, heard.Finds.Count);
        Assert.Equal(new CellPosition(3, 0), heard.Finds[1].From);
        Assert.Equal(new CellPosition(4, 0), heard.Selection!.Focus);
        Assert.Empty(heard.Replaces);
    }

    [Fact] // ADR-0332 §2: the entry is what Replace matches — EditorTextOf's, not the displayed text
    public async Task ADR0332_replace_matches_the_entry()
    {
        var heard = new Heard();
        var answers = new Queue<GridFindResult>([GridFindResult.Found(3, "Book"), GridFindResult.Found(4, "Book")]);
        var cut = RenderGrid(heard, _ => answers.Dequeue(), Count(1));
        cut.Render(ps => ps.Add(g => g.EditorTextOf, (row, column) => column.Name == "Book" ? "=" + row.Book : null));
        await PressAsync(cut, "h", ctrl: true);
        await TypeAsync(cut, "=row", "=Line");
        await ClickAsync(cut, "button.ex-find-next");

        await ClickAsync(cut, "button.ex-find-replace");

        Assert.Equal("=Line 000003", Assert.Single(heard.Edits).Value);
    }

    [Fact] // ADR-0332 §2: a current match whose entry no longer matches is passed over to the next
    public async Task ADR0332_a_current_match_that_does_not_match_its_entry_is_passed_over()
    {
        var heard = new Heard();
        var answers = new Queue<GridFindResult>([GridFindResult.Found(3, "Book"), GridFindResult.Found(4, "Book")]);
        var cut = RenderGrid(heard, _ => answers.Dequeue(), Count(1));
        await PressAsync(cut, "h", ctrl: true);
        await TypeAsync(cut, "zzz", "x");
        await ClickAsync(cut, "button.ex-find-next");

        await ClickAsync(cut, "button.ex-find-replace");

        Assert.Empty(heard.Edits);
        Assert.Equal(new CellPosition(4, 0), heard.Selection!.Focus);
    }

    [Fact] // ADR-0332 §2: a current match in a column that is not Editable writes nothing, and is said
    public async Task ADR0332_replace_in_a_column_not_editable_is_refused()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, At(3, "Amount"), Count(1));
        await PressAsync(cut, "h", ctrl: true);
        await TypeAsync(cut, "3", "4");
        await ClickAsync(cut, "button.ex-find-next");

        await ClickAsync(cut, "button.ex-find-replace");

        Assert.Empty(heard.Edits);
        Assert.Equal([FindRefusalReason.ReplaceRefused], heard.Refusals);
        Assert.Equal("Nothing was replaced. Row 4, Amount: this column cannot be edited.", cut.Find(".ex-find-outcome").TextContent);
        // It stays the current match: no step was asked, and a second press refuses it again.
        Assert.Single(heard.Finds);
        await ClickAsync(cut, "button.ex-find-replace");
        Assert.Single(heard.Finds);
        Assert.Equal([FindRefusalReason.ReplaceRefused, FindRefusalReason.ReplaceRefused], heard.Refusals);
    }

    [Fact] // ADR-0332 §2 / ADR-0034: an Edit Verdict that rejects the replaced text writes nothing, and is said
    public async Task ADR0332_replace_rejected_by_the_verdict_is_refused()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, At(3, "Book"), Count(1));
        await PressAsync(cut, "h", ctrl: true);
        await TypeAsync(cut, "Row", "Row!");
        await ClickAsync(cut, "button.ex-find-next");

        await ClickAsync(cut, "button.ex-find-replace");

        Assert.Empty(heard.Edits);
        Assert.Equal([FindRefusalReason.ReplaceRefused], heard.Refusals);
        Assert.Equal("Nothing was replaced. Row 4, Book: no exclamations.", cut.Find(".ex-find-outcome").TextContent);
    }

    [Fact] // ADR-0332 §2 / ADR-0142: an Edit Intent the Consumer refuses writes nothing, and is said with its words
    public async Task ADR0332_replace_refused_by_the_consumer_is_said()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, At(3, "Book"), Count(1), onEdit: intent => intent.Refuse("locked"));
        await PressAsync(cut, "h", ctrl: true);
        await TypeAsync(cut, "Row", "Line");
        await ClickAsync(cut, "button.ex-find-next");

        await ClickAsync(cut, "button.ex-find-replace");

        Assert.Single(heard.Edits);
        Assert.Equal([FindRefusalReason.ReplaceRefused], heard.Refusals);
        Assert.Equal("Nothing was replaced. Row 4, Book: locked.", cut.Find(".ex-find-outcome").TextContent);
        Assert.Single(heard.Finds);
    }

    [Fact] // ADR-0332 §2: with no OnEdit nothing would hear Replace's write, so it is refused and said, not stepped past
    public async Task ADR0332_replace_with_no_edit_handler_is_refused()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, At(3, "Book"), Count(1), withEdit: false);
        await PressAsync(cut, "h", ctrl: true);
        await TypeAsync(cut, "Row", "Line");
        await ClickAsync(cut, "button.ex-find-next");

        await ClickAsync(cut, "button.ex-find-replace");

        Assert.Equal([FindRefusalReason.ReplaceRefused], heard.Refusals);
        Assert.Equal("Nothing was replaced. Row 4, Book: this column cannot be edited.", cut.Find(".ex-find-outcome").TextContent);
        Assert.Single(heard.Finds);
    }

    [Fact] // ADR-0332 §2: a reason the Consumer ends with a full stop is not given a second one
    public async Task ADR0332_a_reason_ending_in_a_full_stop_is_not_doubled()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, At(3, "Book"), Count(1), onEdit: intent => intent.Refuse("Locked by the desk."));
        await PressAsync(cut, "h", ctrl: true);
        await TypeAsync(cut, "Row", "Line");
        await ClickAsync(cut, "button.ex-find-next");

        await ClickAsync(cut, "button.ex-find-replace");

        Assert.Equal("Nothing was replaced. Row 4, Book: Locked by the desk.", cut.Find(".ex-find-outcome").TextContent);
    }

    [Fact] // ADR-0332 §2 / ADR-0055: in the Cell Editor Ctrl+H is taken and does nothing
    public async Task ADR0332_ctrl_h_in_the_editor_does_nothing()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, At(0, "Book"), Count(1));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "5");

        await PressAsync(cut, "h", ctrl: true);

        Assert.Empty(cut.FindAll(".ex-popover-find"));
        Assert.Equal("5", cut.Find(".ex-editor").GetAttribute("value"));
        Assert.Empty(heard.Refusals);
    }

    [Fact] // ADR-0332 §2: the Replace field's text survives a close and a reopen, as the find text does
    public async Task ADR0332_the_replacement_survives_a_reopen()
    {
        var cut = RenderGrid(new Heard(), At(0, "Book"), Count(1));
        await PressAsync(cut, "h", ctrl: true);
        await TypeAsync(cut, "a", "kept");

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("Escape", false, false, false, false, false, fromDescendant: true));
        Assert.Empty(cut.FindAll(".ex-popover-find"));
        await PressAsync(cut, "h", ctrl: true);

        Assert.Equal("kept", cut.Find("input.ex-find-replace-field").GetAttribute("value"));
    }

    [Fact] // ADR-0332 §3: bound to GridSource.From, Replace All writes every match through the source, as one change
    public async Task ADR0332_gridsource_from_answers_replace_all()
    {
        var heard = new Heard();
        var source = GridSource.From(TestRows.Many(200));
        var columns = Columns();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, columns)
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.OnFindRefused, r => heard.Refusals.Add(r))
            .Add(g => g.OnReplace, request => Task.FromResult(source.Replace(request, (row, column, text) =>
                GridReplaceWrite<TestRow>.To(new TestRow { Book = text, Amount = row.Amount, AsOf = row.AsOf, Active = row.Active })).Result)));
        await PressAsync(cut, "h", ctrl: true);
        await TypeAsync(cut, "Row 0001", "Line 0001");

        await ClickAsync(cut, "button.ex-find-replace-all");

        Assert.Equal("Replaced 100 cells.", cut.Find(".ex-find-outcome").TextContent);
        Assert.Equal(100, source.Window.Count(r => r.Book.StartsWith("Line 0001", StringComparison.Ordinal)));
        Assert.Empty(heard.Refusals);
    }

    [Fact] // ADR-0332 §4: a Replace All forgets the current match, so the next Replace finds before it writes
    public async Task ADR0332_replace_all_forgets_the_current_match()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, At(3, "Book"), Count(4));
        await PressAsync(cut, "h", ctrl: true);
        await TypeAsync(cut, "Row", "x");
        await ClickAsync(cut, "button.ex-find-next");

        await ClickAsync(cut, "button.ex-find-replace-all");
        await ClickAsync(cut, "button.ex-find-replace");

        Assert.Empty(heard.Edits);
        Assert.Equal(2, heard.Finds.Count);
    }
}
