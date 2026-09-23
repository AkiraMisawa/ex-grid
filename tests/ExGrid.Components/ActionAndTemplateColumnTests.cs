using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Action and Template columns as they reach the DOM (ADR-0020): buttons are plain
/// markup, a template rides inside the row's own boundary, and neither lets a press
/// through to the Viewport's hit test.
/// </summary>
public class ActionAndTemplateColumnTests : GridTestContext
{
    private static readonly RenderFragment<TemplateCellContext<TestRow>> Bar =
        cell => builder => builder.AddMarkupContent(0, $"<span class='bar'>{cell.Row.Book}</span>");

    private static GridColumn<TestRow>[] WithActions(params GridAction[] actions) =>
    [
        new("Book", ColumnType.Text, r => r.Book),
        GridColumn<TestRow>.ActionColumn("Actions", actions, width: new ColumnWidthSpec(ColumnWidth.Fixed(120))),
    ];

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        GridColumn<TestRow>[] columns, Action<GridActionEventArgs<TestRow>>? onAction = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Window()).Add(g => g.Columns, columns);
            if (onAction is not null)
                ps.Add(g => g.OnAction, onAction);
        });

    [Fact] // PF-3 / ADR-0003: a button's handler is held, so re-rendering its row re-registers no event
    public void An_action_keeps_its_click_handler_across_renders()
    {
        var cut = RenderGrid(WithActions(new GridAction("open", "Open"), new GridAction("close", "Close")));
        var handlers = cut.FindAll(".ex-action").Select(b => b.GetAttribute("blazor:onclick")).ToArray();
        Assert.NotEmpty(handlers);
        Assert.All(handlers, Assert.NotNull); // or an all-null pair would compare equal
        var renders = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToArray();

        // A new state lookup re-renders every row while changing nothing they paint.
        cut.Render(ps => ps.Add(g => g.CellState, (_, _) => CellState.Normal));

        Assert.All(
            cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).Zip(renders),
            pair => Assert.Equal(pair.Second + 1, pair.First));
        // The same handler, not an equal one: a delegate built afresh on each render is a
        // changed attribute, which the diff answers by registering a new event and sending
        // it to the browser — for every button, on every render of its row.
        Assert.Equal(handlers, cut.FindAll(".ex-action").Select(b => b.GetAttribute("blazor:onclick")));
    }

    [Fact] // ADR-0020: a press is the row's event, so a Consumer's failure reaches the nearest ErrorBoundary
    public void A_failing_action_is_caught_by_the_error_boundary_around_the_grid()
    {
        // Held so its handler is not rebuilt per render, the button's click must still name
        // the row as its receiver: the renderer routes a handler's exception to an error
        // boundary only through the receiver, and without one it is unhandled — on a
        // Server circuit, fatal.
        var cut = Render<ErrorBoundary>(ps => ps
            .AddChildContent<ExGrid<TestRow>>(grid => grid
                .Add(g => g.Window, TestRows.Window())
                .Add(g => g.Columns, WithActions(new GridAction("open", "Open")))
                .Add(g => g.OnAction, (GridActionEventArgs<TestRow> _) => throw new InvalidOperationException("boom")))
            .Add(b => b.ErrorContent, (Exception ex) => $"<p class='caught'>{ex.Message}</p>"));

        cut.FindAll(".ex-action")[0].Click();

        Assert.Equal("boom", cut.Find(".caught").TextContent);
    }

    [Fact] // ADR-0020: one button per declared action, painted as plain markup in the row
    public void An_action_column_paints_one_button_per_declared_action()
    {
        var cut = RenderGrid(WithActions(new GridAction("open", "Open"), new GridAction("close", "Close")));

        var buttons = cut.FindAll(".ex-action");
        Assert.Equal(6, buttons.Count); // three rows × two actions
        // A labelled button names itself; no aria-label is added over its own text.
        Assert.Null(buttons[0].GetAttribute("aria-label"));
        Assert.Equal("Open", buttons[0].TextContent);
        Assert.Equal("Close", buttons[1].TextContent);
    }

    [Fact] // ADR-0020: an icon action paints its class and keeps the label as its accessible name
    public void An_icon_action_keeps_its_label_as_the_accessible_name()
    {
        var cut = RenderGrid(WithActions(new GridAction("open", "Open", cssClass: "icon-open")));

        var button = cut.Find(".ex-action");
        Assert.Contains("icon-open", button.ClassName);
        Assert.Equal("Open", button.GetAttribute("title"));
        // aria-label, not title alone: title is the last-resort source of an accessible
        // name and several screen readers ignore it (ADR-0020's mandatory label is the
        // promise this keeps).
        Assert.Equal("Open", button.GetAttribute("aria-label"));
        Assert.Equal("", button.TextContent);
    }

    [Fact] // ADR-0020: the grid reports which row, which column, which action — and does nothing else
    public async Task Pressing_an_action_reports_it_once()
    {
        var raised = new List<GridActionEventArgs<TestRow>>();
        var cut = RenderGrid(WithActions(new GridAction("open", "Open")), raised.Add);

        await cut.FindAll(".ex-action")[1].ClickAsync(new MouseEventArgs());

        var args = Assert.Single(raised);
        Assert.Equal("Beta", args.Row.Book);
        Assert.Equal("Actions", args.ColumnName);
        Assert.Equal("open", args.ActionName);
    }

    [Fact] // ADR-0020: a press on an action is not a click on a cell — the selection must not move
    public async Task Pressing_an_action_does_not_reach_the_viewports_hit_test()
    {
        GridSelection? selection = null;
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, WithActions(new GridAction("open", "Open")))
            .Add(g => g.SelectionChanged, s => selection = s));
        var press = new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 4, OffsetY = 4 };

        // A press on an ordinary cell bubbles up to the Viewport, which is the only
        // mouse handler in the body — that is what makes the hit test a piece of
        // arithmetic with no per-cell handlers (ADR-0008).
        await cut.FindAll(".ex-cell")[0].MouseDownAsync(press);
        Assert.NotNull(selection);

        selection = null;
        var painted = cut.FindAll(".ex-range").Count;
        // A press inside an Action cell does not: the cell stops it. bUnit walks the
        // ancestors looking for a handler and honours that stop, so it reports finding
        // none — which is the contract itself. Left to bubble, the Viewport would read
        // offsets measured from the button and move the selection to whichever cell that
        // arithmetic named (ADR-0020).
        await Assert.ThrowsAsync<MissingEventHandlerException>(
            () => cut.FindAll(".ex-action")[1].MouseDownAsync(press));

        Assert.Null(selection);
        Assert.Equal(painted, cut.FindAll(".ex-range").Count);
    }

    [Fact] // ADR-0020: the template's own content is what the cell paints
    public void A_template_column_paints_the_consumers_markup()
    {
        var cut = RenderGrid([
            new("Book", ColumnType.Text, r => r.Book),
            GridColumn<TestRow>.TemplateColumn("Bar", ColumnType.Text, r => r.Book, Bar),
        ]);

        var bars = cut.FindAll(".ex-cell .bar");
        Assert.Equal(3, bars.Count);
        Assert.Equal("Alpha", bars[0].TextContent);
    }

    [Fact] // ADR-0016 / ADR-0020: #### is about a value that does not fit; a template paints no value
    public void A_template_column_is_never_hashed()
    {
        var cut = RenderGrid([
            GridColumn<TestRow>.TemplateColumn("Amount", ColumnType.Number, r => r.Amount, Bar,
                width: new ColumnWidthSpec(ColumnWidth.Fixed(40))),
        ]);

        Assert.DoesNotContain('#', cut.Markup);
        Assert.Equal(3, cut.FindAll(".bar").Count);
    }

    [Theory] // ADR-0020 / ADR-0016: an Action Column's Auto width comes from its labels
    [InlineData("Open", 68)]                 // the cell's 2 × 5, then 4 × 10 of text + the button's 18
    [InlineData("Open the detail view", 228)] // the cell's 2 × 5, then 20 × 10 of text + the button's 18
    public void An_auto_action_column_is_sized_from_its_labels(string label, double expectedPx)
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.CellMetrics, new CellTextMetrics(digitWidthPx: 10, cellHorizontalPaddingPx: 5))
            .Add(g => g.Columns, [
                new GridColumn<TestRow>("Book", ColumnType.Text, r => r.Book),
                GridColumn<TestRow>.ActionColumn("Actions", [new GridAction("open", label)],
                    width: new ColumnWidthSpec(ColumnWidth.Auto, minWidthPx: 40, maxWidthPx: 400)),
            ]));

        // The cell's own padding is counted on top of the labels: without it a short
        // label resolves to a column narrower than the single button it holds, and
        // `overflow: hidden` clips it — the outcome the estimate exists to prevent.
        var header = cut.FindAll(".ex-header-cell")[1].GetAttribute("style")!;
        Assert.Contains(FormattableString.Invariant($"width: {expectedPx}px"), header);
    }

    [Fact] // ADR-0016: the numeric classification is the core's for every column, template or not
    public void A_number_template_column_is_painted_as_numeric()
    {
        var cut = RenderGrid([
            GridColumn<TestRow>.TemplateColumn("Amount", ColumnType.Number, r => r.Amount, Bar),
        ]);

        Assert.All(cut.FindAll(".ex-cell"), cell => Assert.Contains("ex-cell-numeric", cell.ClassName));
    }

    [Fact] // ADR-0020: a drag released over an action must not leave the grid dragging
    public async Task Releasing_a_drag_over_an_action_ends_it()
    {
        GridSelection? selection = null;
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, WithActions(new GridAction("open", "Open")))
            .Add(g => g.RowHeight, 20)
            .Add(g => g.SelectionChanged, s => selection = s));
        var viewport = cut.Find(".ex-viewport");

        await viewport.MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 10, OffsetY = 10 });
        await viewport.MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = 10, OffsetY = 30 });
        var extended = selection!.CellCount;

        // Released over a button. mouseup carries no position, so it is let through and
        // ends the drag; swallowed, the next move with a button held would go on
        // extending the selection nobody is dragging any more.
        await cut.FindAll(".ex-action")[1].MouseUpAsync(new MouseEventArgs { Button = 0, Buttons = 0 });

        // The Viewport carries its move handler only while a drag is running, so the
        // handler being gone is the drag having ended. Swallowed, the mouseup would have
        // left it attached, and the next move with a button held — a press on that very
        // button, dragged off it — would extend a selection nobody is dragging.
        await Assert.ThrowsAsync<MissingEventHandlerException>(
            () => viewport.MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = 10, OffsetY = 50 }));
        Assert.Equal(extended, selection!.CellCount);
    }

    [Fact] // ADR-0003: neither kind of column costs a row a second render
    public void Neither_kind_of_column_defeats_row_memoisation()
    {
        var rows = TestRows.Window();
        GridColumn<TestRow>[] columns =
        [
            new("Book", ColumnType.Text, r => r.Book),
            GridColumn<TestRow>.TemplateColumn("Bar", ColumnType.Text, r => r.Book, Bar),
            GridColumn<TestRow>.ActionColumn("Actions", [new GridAction("open", "Open")]),
        ];
        var cut = Render<ExGrid<TestRow>>(ps => ps.Add(g => g.Window, rows).Add(g => g.Columns, columns));

        cut.Render(ps => ps.Add(g => g.Window, rows).Add(g => g.Columns, columns));

        foreach (var row in cut.FindComponents<ExGridRow<TestRow>>())
        {
            Assert.Equal(1, row.RenderCount);
        }
    }
}
