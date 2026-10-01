using Bunit;
using ExGrid.Chrome;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// A Consumer's popover (ADR-0050 item 16, DC-60): the Consumer's own contents in the grid's
/// popover frame, inside the grid's box and bounded by it (ADR-0040), taking the keyboard and
/// returning it (ADR-0039), closing as a Cancel when the box shrinks below one row, and
/// independent per grid (ADR-0018). Nothing stands until the Consumer opens one (DC-1). 20px rows
/// under a 20px header, 350px across, in a Stretch box whose height the browser reports.
/// </summary>
public class ConsumerPopoverTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    // The contents the tests open: a button, and the context the frame handed them last.
    private sealed class Probe
    {
        public GridPopoverContext? Context { get; private set; }

        public int Renders { get; private set; }

        public RenderFragment<GridPopoverContext> Content => context => builder =>
        {
            Context = context;
            Renders++;
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "class", "probe");
            builder.AddContent(2, "Probe");
            builder.CloseElement();
        };
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(bool formulaBar = false)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(50))
            .Add(g => g.TotalCount, 50)
            .Add(g => g.Columns, (IReadOnlyList<GridColumn<TestRow>>)
            [
                new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
                new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
                new("AsOf", ColumnType.Date, r => r.AsOf, width: Fixed100),
            ])
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, ViewportSize.Stretch)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ShowFormulaBar, formulaBar)
            .Add(g => g.OnSortChanged, (IReadOnlyList<SortSpec> _) => { }));

    private static Task Report(IRenderedComponent<ExGrid<TestRow>> cut, double heightPx)
        => cut.InvokeAsync(() => cut.Instance.OnViewportReportAsync(0, 0, 350, heightPx));

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, bool fromDescendant = false, bool shift = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, false, shift, false, false, false, fromDescendant: fromDescendant));

    private static Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    private static async Task<bool> OpenAsync(IRenderedComponent<ExGrid<TestRow>> cut, Probe probe, string label = "Probe")
    {
        var opened = await cut.Instance.OpenPopoverAsync(probe.Content, label);
        cut.Render();
        return opened;
    }

    [Fact] // ADR-0050 item 16 / DC-60 / DC-1: nothing stands until the Consumer opens one
    public async Task Without_an_opening_no_popover_stands()
    {
        var cut = RenderGrid();
        await Report(cut, 300);

        Assert.Empty(cut.FindAll(".ex-popover"));
        Assert.Empty(cut.FindAll(".ex-popover-consumer"));
    }

    [Fact] // ADR-0050 item 16 / DC-60 / ADR-0040: the contents stand in the grid's frame, inside its box and bounded by it
    public async Task The_contents_stand_inside_the_grids_box_and_are_bounded_by_it()
    {
        var cut = RenderGrid();
        await Report(cut, 300);
        var probe = new Probe();

        Assert.True(await OpenAsync(cut, probe, "Format Cells"));

        var frame = cut.Find(".ex-popover.ex-popover-consumer");
        Assert.Equal("dialog", frame.GetAttribute("role"));
        Assert.Equal("Format Cells", frame.GetAttribute("aria-label"));
        Assert.NotNull(frame.QuerySelector(".ex-popover-body button.probe"));
        var style = frame.GetAttribute("style")!;
        // Under the header band, centred across the 350px Viewport, never wider than it, and
        // bounded below by the Viewport's bottom: 300 less the 20px header.
        Assert.Contains("left: 175px", style);
        Assert.Contains("top: 20px", style);
        Assert.Contains("transform: translateX(-50%)", style);
        Assert.Contains("max-width: 350px", style);
        Assert.Contains("max-height: 280px", style);
    }

    [Fact] // ADR-0050 item 16 / DC-60 / ADR-0040: a box that shrinks with room to spare keeps the popover, only shorter
    public async Task A_shrink_that_leaves_room_keeps_the_popover()
    {
        var cut = RenderGrid();
        await Report(cut, 300);
        await OpenAsync(cut, new Probe());

        await Report(cut, 120);

        Assert.Contains("max-height: 100px", cut.Find(".ex-popover-consumer").GetAttribute("style"));
    }

    [Fact] // ADR-0050 item 16 / DC-60 / ADR-0040: below one row of room it closes as a Cancel, and the root takes the keyboard
    public async Task Below_one_row_of_room_it_closes_and_the_root_takes_the_keyboard()
    {
        var cut = RenderGrid();
        var root = Js.RootReferenceId;
        await Report(cut, 300);
        await OpenAsync(cut, new Probe());
        var before = Js.Focused.Count;

        await Report(cut, 20 + 19); // the header band and 19px

        Assert.Empty(cut.FindAll(".ex-popover-consumer"));
        Assert.Equal(root, Js.Focused.Skip(before).Last());
    }

    [Fact] // ADR-0050 item 16 / DC-60 / ADR-0040: one row of room still stands
    public async Task One_row_of_room_keeps_it()
    {
        var cut = RenderGrid();
        await Report(cut, 300);
        await OpenAsync(cut, new Probe());

        await Report(cut, 20 + 20);

        Assert.Single(cut.FindAll(".ex-popover-consumer"));
    }

    [Fact] // ADR-0050 item 16 / DC-60 / ADR-0039: the opening asks the contents to take the keyboard, and each opening asks anew
    public async Task Each_opening_asks_the_contents_to_take_the_keyboard()
    {
        var cut = RenderGrid();
        await Report(cut, 300);
        var probe = new Probe();

        await OpenAsync(cut, probe);
        var first = probe.Context!.FocusRequest;
        await OpenAsync(cut, probe);

        Assert.True(first > 0);
        Assert.True(probe.Context!.FocusRequest > first);
        Assert.Equal(0, probe.Context.FocusLastRequest);
        Assert.Single(cut.FindAll(".ex-popover"));
    }

    [Fact] // ADR-0050 item 16 / DC-60 / ADR-0039: Tab wraps inside the popover — off the last control to the first, and back
    public async Task Tab_wraps_inside_the_popover()
    {
        var cut = RenderGrid();
        await Report(cut, 300);
        var probe = new Probe();
        await OpenAsync(cut, probe);
        var opening = probe.Context!.FocusRequest;
        // The contents take the keyboard, as they do on their count.
        await cut.Find(".ex-popover-consumer > .ex-popover-body").FocusInAsync(new FocusEventArgs());
        var sentinels = cut.FindAll(".ex-popover-consumer > .ex-focus-wrap");

        await sentinels[1].FocusAsync(new FocusEventArgs());
        Assert.Equal(opening + 1, probe.Context!.FocusRequest);

        await cut.FindAll(".ex-popover-consumer > .ex-focus-wrap")[0].FocusAsync(new FocusEventArgs());
        Assert.Equal(1, probe.Context!.FocusLastRequest);
        Assert.Equal(opening + 1, probe.Context.FocusRequest);
    }

    [Fact] // ADR-0050 item 16 / DC-60 / ADR-0039: a sentinel reached before the contents hold the keyboard was entered from outside, and the keyboard enters at the near end
    public async Task A_sentinel_entered_from_outside_enters_at_the_near_end()
    {
        var cut = RenderGrid();
        await Report(cut, 300);
        var probe = new Probe();
        await OpenAsync(cut, probe);
        var opening = probe.Context!.FocusRequest;

        // Shift+Tab from the page, while the contents' first focus is still on its way: from behind,
        // onto the trailing sentinel — the last control, not a wrap to the first.
        await cut.FindAll(".ex-popover-consumer > .ex-focus-wrap")[1].FocusAsync(new FocusEventArgs());

        Assert.Equal(1, probe.Context!.FocusLastRequest);
        Assert.Equal(opening, probe.Context.FocusRequest);

        // Tab from in front, onto the leading sentinel: the first control.
        await OpenAsync(cut, probe);
        opening = probe.Context!.FocusRequest;
        await cut.FindAll(".ex-popover-consumer > .ex-focus-wrap")[0].FocusAsync(new FocusEventArgs());

        Assert.Equal(opening + 1, probe.Context!.FocusRequest);
        Assert.Equal(0, probe.Context.FocusLastRequest);
    }

    [Fact] // ADR-0050 item 16 / DC-60 / ADR-0039: Escape from inside closes it, and the root takes the keyboard
    public async Task Escape_closes_it_and_the_root_takes_the_keyboard()
    {
        var cut = RenderGrid();
        var root = Js.RootReferenceId;
        await Report(cut, 300);
        await OpenAsync(cut, new Probe());
        var before = Js.Focused.Count;

        await PressAsync(cut, "Escape", fromDescendant: true);

        Assert.Empty(cut.FindAll(".ex-popover-consumer"));
        Assert.Equal(root, Js.Focused.Skip(before).Last());
    }

    [Fact] // ADR-0050 item 16 / DC-60 / ADR-0010: a pointer-down elsewhere in the instance closes it
    public async Task A_press_on_the_rows_closes_it()
    {
        var cut = RenderGrid();
        await Report(cut, 300);
        await OpenAsync(cut, new Probe());

        await ClickCellAsync(cut, 150, 30);

        Assert.Empty(cut.FindAll(".ex-popover-consumer"));
    }

    [Fact] // ADR-0050 item 16 / DC-60 / ADR-0010: a press into the Formula Bar that opens an edit closes it
    public async Task A_press_into_the_formula_bar_closes_it()
    {
        var cut = RenderGrid(formulaBar: true);
        await Report(cut, 300);
        await ClickCellAsync(cut, 50, 10);
        await OpenAsync(cut, new Probe());

        await cut.Find("input.ex-formula-bar-text").FocusAsync(new FocusEventArgs());

        Assert.Empty(cut.FindAll(".ex-popover-consumer"));
    }

    [Fact] // ADR-0050 item 16 / DC-60 / ADR-0039: the contents' own Close closes it, and the root takes the keyboard
    public async Task The_contexts_close_closes_it_and_the_root_takes_the_keyboard()
    {
        var cut = RenderGrid();
        var root = Js.RootReferenceId;
        await Report(cut, 300);
        var probe = new Probe();
        await OpenAsync(cut, probe);
        var before = Js.Focused.Count;

        probe.Context!.Close();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".ex-popover-consumer")));

        Assert.Equal(root, Js.Focused.Skip(before).Last());
    }

    [Fact] // ADR-0050 item 16 / DC-60: ClosePopoverAsync closes the Consumer's popover, and leaves another the grid opened since
    public async Task Close_popover_closes_only_the_consumers()
    {
        var cut = RenderGrid();
        await Report(cut, 300);
        await OpenAsync(cut, new Probe());

        await cut.Instance.ClosePopoverAsync();
        Assert.Empty(cut.FindAll(".ex-popover"));

        await OpenAsync(cut, new Probe());
        await cut.FindAll(".ex-menu-button")[0].ClickAsync(new MouseEventArgs());
        Assert.Empty(cut.FindAll(".ex-popover-consumer"));
        await cut.Instance.ClosePopoverAsync();
        Assert.Single(cut.FindAll(".ex-popover"));
    }

    [Fact] // ADR-0050 item 16 / DC-60: one popover at a time — it takes the Context Menu's place
    public async Task It_replaces_the_context_menu()
    {
        var cut = RenderGrid();
        await Report(cut, 300);
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "F10", shift: true);
        Assert.Single(cut.FindAll(".ex-popover[role=menu]"));

        await OpenAsync(cut, new Probe());

        Assert.Single(cut.FindAll(".ex-popover"));
        Assert.Single(cut.FindAll(".ex-popover-consumer"));
    }

    [Fact] // ADR-0050 item 16 / DC-60: the Context Menu opened from the keyboard takes its place in turn
    public async Task The_context_menu_replaces_it()
    {
        var cut = RenderGrid();
        await Report(cut, 300);
        await ClickCellAsync(cut, 50, 10);
        await OpenAsync(cut, new Probe());

        await PressAsync(cut, "F10", shift: true);

        Assert.Single(cut.FindAll(".ex-popover"));
        Assert.Empty(cut.FindAll(".ex-popover-consumer"));
    }

    [Fact] // ADR-0050 item 16 / DC-60: it does not open over an open edit, which it would take the keyboard from
    public async Task It_does_not_open_while_an_edit_is_open()
    {
        var cut = RenderGrid();
        await Report(cut, 300);
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "5");
        Assert.NotEmpty(cut.FindAll(".ex-editor"));

        Assert.False(await OpenAsync(cut, new Probe()));

        Assert.Empty(cut.FindAll(".ex-popover-consumer"));
        Assert.Equal("5", cut.Find("input.ex-editor").GetAttribute("value"));
    }

    [Fact] // ADR-0050 item 16 / DC-60 / ADR-0039: a popup of the contents' own reaches the key gate, as in every popover
    public async Task A_popup_of_the_contents_reaches_the_key_gate()
    {
        var cut = RenderGrid();
        await Report(cut, 300);
        var probe = new Probe();
        await OpenAsync(cut, probe);

        probe.Context!.InnerPopupChanged(true);

        cut.WaitForAssertion(() => Assert.Equal([true], Js.InnerPopupTold.Invocations.Select(i => (bool)i.Arguments[0]!)));
    }

    [Fact] // ADR-0050 item 16 / ADR-0010's note of 2026-09-30: the core's focus function asks for the keyboard back on the root
    public async Task Return_keyboard_asks_for_the_root()
    {
        var cut = RenderGrid();
        var root = Js.RootReferenceId;
        await Report(cut, 300);
        var before = Js.Focused.Count;

        await cut.Instance.ReturnKeyboardAsync();

        Assert.Equal([root], Js.Focused.Skip(before));
    }

    [Fact] // ADR-0050 item 16 / DC-60 / ADR-0018: two grids' popovers are independent
    public async Task Two_grids_are_independent()
    {
        var left = RenderGrid();
        var right = RenderGrid();
        await Report(left, 300);
        await Report(right, 300);
        var probe = new Probe();

        await OpenAsync(left, probe);
        Assert.Single(left.FindAll(".ex-popover-consumer"));
        Assert.Empty(right.FindAll(".ex-popover"));

        await OpenAsync(right, new Probe());
        await PressAsync(right, "Escape", fromDescendant: true);

        Assert.Empty(right.FindAll(".ex-popover"));
        Assert.Single(left.FindAll(".ex-popover-consumer"));
    }

    [Fact] // ADR-0050 item 16: an accessible name is required — a dialog that names nothing says nothing
    public async Task A_popover_without_a_label_is_refused()
    {
        var cut = RenderGrid();

        await Assert.ThrowsAsync<ArgumentException>(() => cut.Instance.OpenPopoverAsync(new Probe().Content, " "));
    }
}
