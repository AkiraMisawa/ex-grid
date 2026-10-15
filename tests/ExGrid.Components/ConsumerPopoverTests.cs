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

    [Fact] // ADR-0039 / ADR-0050 item 16, 2026-10-01: opening it tells the key gate that the keyboard is going to it
    public async Task Opening_it_tells_the_key_gate_the_keyboard_is_going_to_it()
    {
        var cut = RenderGrid();
        await Report(cut, 300);

        await OpenAsync(cut, new Probe());

        Assert.Equal(["popover"], Js.HandedOff.Invocations.Select(i => (string)i.Arguments[0]!));
    }

    // A grid whose Context Menu carries one command of the Consumer's, which opens a frame of its
    // own when asked to: the keyboard is handed to it as a Consumer's dialog would take it.
    private IRenderedComponent<ExGrid<TestRow>> RenderGridWithFrameCommand(bool handsToFrame, Action<bool>? sawMenu = null)
    {
        IRenderedComponent<ExGrid<TestRow>>? cut = null;
        cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(50))
            .Add(g => g.TotalCount, 50)
            .Add(g => g.Columns, (IReadOnlyList<GridColumn<TestRow>>)
            [
                new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
                new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
            ])
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, ViewportSize.Stretch)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ContextCommands, _ =>
            [
                new GridCommand("consumer.frame", true, async () =>
                {
                    if (handsToFrame)
                        await cut!.Instance.HandKeyboardToFrameAsync();
                    sawMenu?.Invoke(cut!.FindAll("[role=menu]").Count == 1);
                }),
            ]));
        return cut;
    }

    [Fact] // ADR-0050 item 16 / ADR-0039, 2026-10-01: a command that hands the keyboard to a frame of the Consumer's own tells the key gate while its menu still stands
    public async Task A_command_that_hands_the_keyboard_to_a_frame_tells_the_key_gate_first()
    {
        var menuStoodWhenTold = false;
        var cut = RenderGridWithFrameCommand(handsToFrame: true, stood => menuStoodWhenTold = stood);
        await Report(cut, 300);
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "F10", shift: true);
        var reclaims = Js.FocusReclaimed.Invocations.Count;

        await cut.FindAll("[role=menu] button[role=menuitem]").Single(b => b.TextContent == "consumer.frame").ClickAsync(new MouseEventArgs());

        Assert.Empty(cut.FindAll(".ex-popover"));
        // The key gate was told while the menu still stood: on a circuit the call reaches the
        // browser ahead of the render that removes it, so the keys held behind the command wait
        // for the frame rather than for the root.
        Assert.True(menuStoodWhenTold);
        Assert.Equal(["frame"], Js.HandedOff.Invocations.Select(i => (string)i.Arguments[0]!));
        // The menu's hand-back to the root still follows: the keys typed until the frame takes the
        // keyboard land there, where the gate holds them for the frame.
        Assert.True(Js.FocusReclaimed.Invocations.Count > reclaims);
    }

    // A grid whose Context Menu carries one command of the Consumer's, which opens a popover of the
    // Consumer's in the grid's frame — in the menu's place, as ExSheet's Format Cells… does under the
    // built-in Chrome.
    private IRenderedComponent<ExGrid<TestRow>> RenderGridWithPopoverCommand(Probe probe)
    {
        IRenderedComponent<ExGrid<TestRow>>? cut = null;
        cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(50))
            .Add(g => g.TotalCount, 50)
            .Add(g => g.Columns, (IReadOnlyList<GridColumn<TestRow>>)
            [
                new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
                new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
            ])
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, ViewportSize.Stretch)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ContextCommands, _ =>
            [
                new GridCommand("consumer.popover", true, () => cut!.Instance.OpenPopoverAsync(probe.Content, "Probe")),
            ]));
        return cut;
    }

    [Fact] // ADR-0039 (2026-10-13) / ADR-0050 item 16 / ADR-0010: a popover opened in a menu's place asks for the root's focus while the menu still stands, behind the hand-off, and its contents have the last word
    public async Task A_popover_opened_in_a_menus_place_keeps_the_keyboard_on_the_root_until_its_contents_take_it()
    {
        // On a circuit the render that swaps the menu for the popover removed the item holding DOM
        // focus, and the contents took it a round trip later: a key typed in between fell on body,
        // which the key gate never hears. The request for the root reaches the browser ahead of that
        // render, and after the gate has been told the keyboard is going to the popover, so the key
        // lands where the gate holds it for the contents.
        var probe = new Probe();
        var cut = RenderGridWithPopoverCommand(probe);
        await Report(cut, 300);
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "F10", shift: true);
        // What stood at each moment the grid asked for the root's focus: (menus, Consumer's popovers,
        // hand-offs told).
        var standing = new List<(int, int, int)>();
        Js.OnFocusReclaimed(() => standing.Add((
            cut.FindAll(".ex-popover[role=menu]").Count, cut.FindAll(".ex-popover-consumer").Count, Js.HandedOff.Invocations.Count)));
        var asked = Js.FocusCalls;

        await cut.FindAll("[role=menu] button[role=menuitem]").Single(b => b.TextContent == "consumer.popover").ClickAsync(new MouseEventArgs());

        Assert.Single(cut.FindAll(".ex-popover"));
        Assert.Single(cut.FindAll(".ex-popover-consumer"));
        Assert.Equal([(1, 0, 1)], standing);
        Assert.Equal(["popover"], Js.HandedOff.Invocations.Select(i => (string)i.Arguments[0]!));
        // Nothing is asked after the render: the contents take the keyboard on their count.
        cut.WaitForAssertion(() => Assert.Equal([Js.RootReferenceId], Js.Focused.Skip(asked)));
        Assert.True(probe.Context!.FocusRequest > 0);
    }

    [Fact] // ADR-0039 (2026-10-13): a popover opened in another's place — the status line's figures menu again, a toolbar's choices — asks for the root's focus while that one stands
    public async Task A_popover_opened_in_another_popovers_place_asks_for_the_root_while_that_one_stands()
    {
        var cut = RenderGrid();
        await Report(cut, 300);
        await ClickCellAsync(cut, 50, 10);
        await cut.FindAll(".ex-menu-button")[0].ClickAsync(new MouseEventArgs());
        var standing = new List<(int, int)>();
        Js.OnFocusReclaimed(() => standing.Add((cut.FindAll(".ex-popover").Count, cut.FindAll(".ex-popover-consumer").Count)));

        await OpenAsync(cut, new Probe());
        await OpenAsync(cut, new Probe());

        // The column's menu, then the first Consumer's popover, each standing when the root was asked.
        Assert.Equal([(1, 0), (1, 1)], standing);
        Assert.Single(cut.FindAll(".ex-popover-consumer"));
    }

    [Fact] // ADR-0039 (2026-10-13) / ADR-0050 item 16: a popover opened over none replaces nothing, and asks nothing of the root — the keyboard is where the opening found it
    public async Task A_popover_opened_over_none_asks_nothing_of_the_root()
    {
        var cut = RenderGrid();
        await Report(cut, 300);
        await ClickCellAsync(cut, 50, 10);
        var reclaims = Js.FocusReclaimed.Invocations.Count;

        await OpenAsync(cut, new Probe());

        Assert.Single(cut.FindAll(".ex-popover-consumer"));
        Assert.Equal(reclaims, Js.FocusReclaimed.Invocations.Count);
    }

    [Fact] // ADR-0039 / ADR-0044: a command that opens nothing still hands the keyboard back to the root
    public async Task A_command_that_opens_nothing_hands_the_keyboard_back_to_the_root()
    {
        var cut = RenderGridWithFrameCommand(handsToFrame: false);
        await Report(cut, 300);
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "F10", shift: true);
        var reclaims = Js.FocusReclaimed.Invocations.Count;

        await cut.FindAll("[role=menu] button[role=menuitem]").Single(b => b.TextContent == "consumer.frame").ClickAsync(new MouseEventArgs());

        Assert.Empty(cut.FindAll(".ex-popover"));
        Assert.Empty(Js.HandedOff.Invocations);
        Assert.True(Js.FocusReclaimed.Invocations.Count > reclaims);
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

    [Fact] // ADR-0050 item 16's note of 2026-10-12 (decided with the user: keys typed while the keyboard is in flight back from a closing popover go where it lands): a Consumer that asked hears Escape close its popover before the grid takes the keyboard back, so its hand-off reaches the key gate first
    public async Task Escape_closing_it_tells_the_consumer_who_asked_before_the_root_takes_the_keyboard()
    {
        var cut = RenderGrid();
        await Report(cut, 300);
        var escaped = 0;
        var handOffsAtReclaim = -1;
        Js.OnFocusReclaimed(() => handOffsAtReclaim = Js.HandedOff.Invocations.Count);
        await cut.Instance.OpenPopoverAsync(new Probe().Content, "Probe", async () =>
        {
            escaped++;
            await cut.Instance.HandKeyboardToFrameAsync();
        });
        cut.Render();
        // The contents hold the keyboard, as the Escape typed in them says.
        await cut.Find(".ex-popover-consumer > .ex-popover-body").FocusInAsync(new FocusEventArgs());

        await PressAsync(cut, "Escape", fromDescendant: true);

        Assert.Empty(cut.FindAll(".ex-popover-consumer"));
        Assert.Equal(1, escaped);
        Assert.Equal(["popover", "frame", "none"], Js.HandedOff.Invocations.Select(i => (string)i.Arguments[0]!));
        // Told before the hand-back; "none" withdraws only the popover destination, leaving the frame.
        Assert.Equal(3, handOffsAtReclaim);
    }

    [Fact] // ADR-0050 item 16's note of 2026-10-12: a popover closed any other way than by Escape tells the Consumer nothing
    public async Task Closed_otherwise_it_tells_the_consumer_nothing()
    {
        var cut = RenderGrid();
        await Report(cut, 300);
        var escaped = 0;
        Task Escaped()
        {
            escaped++;
            return Task.CompletedTask;
        }
        var probe = new Probe();

        await cut.Instance.OpenPopoverAsync(probe.Content, "Probe", Escaped);
        cut.Render();
        await cut.Instance.ClosePopoverAsync();
        await cut.Instance.OpenPopoverAsync(probe.Content, "Probe", Escaped);
        cut.Render();
        await cut.InvokeAsync(() => probe.Context!.Close());
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".ex-popover-consumer")));
        await cut.Instance.OpenPopoverAsync(probe.Content, "Probe", Escaped);
        cut.Render();
        await ClickCellAsync(cut, 50, 30);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".ex-popover-consumer")));
        // Opened without asking, Escape tells nobody.
        await OpenAsync(cut, probe);
        await PressAsync(cut, "Escape", fromDescendant: true);

        Assert.Empty(cut.FindAll(".ex-popover-consumer"));
        Assert.Equal(0, escaped);
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

    [Fact] // ADR-0050 item 16's note of 2026-10-12: a frame of the Consumer's own gives the keyboard back from where it stands, and the keys typed on it on the way are the grid's, asked for once
    public async Task Return_keyboard_from_a_frame_hands_the_keys_typed_there_to_the_grid()
    {
        var cut = RenderGrid();
        await Report(cut, 300);
        var frame = new ElementReference("frame-of-the-consumers");
        var asked = 0;
        IReadOnlyList<KeyboardEventArgs> typed = [new() { Key = "Enter" }, new() { Key = "x", ShiftKey = true }];
        var answer = Js.UnansweredKeyboardHandBack();

        var returning = cut.Instance.ReturnKeyboardAsync(frame, () =>
        {
            asked++;
            return typed;
        });

        var call = Assert.Single(answer.Invocations);
        Assert.Equal(frame.Id, ((ElementReference)call.Arguments[0]!).Id);
        var ticket = (int)call.Arguments[1]!;
        Assert.Equal(0, asked);
        Assert.False(returning.IsCompleted);
        // The key gate has the keyboard, and asks for what the frame heard on the way.
        IReadOnlyList<KeyboardEventArgs> handed = [];
        await cut.InvokeAsync(() => { handed = cut.Instance.KeysTypedOnTheWay(ticket); });
        Assert.Equal(["Enter", "x"], handed.Select(k => k.Key));
        Assert.True(handed[1].ShiftKey);
        Assert.Equal(1, asked);
        // The browser completes the hand-back only after collecting the keys, so the Consumer
        // can now remove its frame and queue. The shipped module's ordering is pinned in layer 3.
        answer.SetVoidResult();
        await returning;
        // Asked once: a second question for the same hand-back finds nothing.
        await cut.InvokeAsync(() => { handed = cut.Instance.KeysTypedOnTheWay(ticket); });
        Assert.Empty(handed);
        Assert.Equal(1, asked);
    }

    [Fact] // ADR-0050 item 16 / ADR-0044: closing withdraws the popover destination even after its contents report focus; the key gate may still hold behind a choice
    public async Task Closing_a_consumer_popover_withdraws_its_destination_before_and_after_contents_report_focus()
    {
        var cut = RenderGrid();
        await Report(cut, 300);

        await OpenAsync(cut, new Probe());
        await cut.Instance.ClosePopoverAsync();
        Assert.Equal(["popover", "none"], Js.HandedOff.Invocations.Select(i => (string)i.Arguments[0]!));

        await OpenAsync(cut, new Probe());
        await cut.Find(".ex-popover-consumer > .ex-popover-body").FocusInAsync(new FocusEventArgs());
        await cut.Instance.ClosePopoverAsync();
        Assert.Equal(["popover", "none", "popover", "none"], Js.HandedOff.Invocations.Select(i => (string)i.Arguments[0]!));
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
