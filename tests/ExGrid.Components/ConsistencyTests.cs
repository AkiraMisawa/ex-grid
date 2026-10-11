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
/// ST-1: after any sequence of operations the invariants hold. A scripted randomised
/// sequence — clicks, drags, every claimed key, scrolls, sorts, filters, geometry
/// changes — asserting after each step that every selection rectangle lies inside the
/// extent, the Focus and the Extent lie inside the range holding the Focus (ADR-0052),
/// the displayed cell count is the sum of rectangle areas, the painted rows are a
/// contiguous slice, and nothing throws. The seed is in the test name's data so
/// a failure replays exactly.
/// </summary>
public class ConsistencyTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
        new("AsOf", ColumnType.Date, r => r.AsOf, width: Fixed100),
        new("Active", ColumnType.Boolean, r => r.Active, width: Fixed100),
    ];

    // ST-1: ≥500 randomised operations at 10³ and 10⁶ rows (§22 Step 5); the seeds are
    // recorded here. The 10⁶ case takes about a minute, and nearly all of it is the
    // reference source re-sorting and re-filtering a million rows (~0.5 s a sort) — the
    // Consumer's work, not the grid's (ADR-0001). The grid's own steps stay in
    // milliseconds at either scale. So it runs only when asked for, with
    // EXGRID_ST1_MILLION=1 (decided 2026-09-24): an ordinary run skips it by name, and
    // Step 5 is the run that sets it.
    private const string MillionSwitch = "EXGRID_ST1_MILLION";

    [Theory]
    [InlineData(20260901, 200)]
    [InlineData(424242, 200)]
    [InlineData(20260901, 1_000)]
    [InlineData(424242, 1_000)]
    [InlineData(20260901, 1_000_000)]
    public async Task Five_hundred_random_operations_leave_every_invariant_standing(int seed, int totalRows)
    {
        if (totalRows >= 1_000_000)
            Assert.SkipUnless(Environment.GetEnvironmentVariable(MillionSwitch) == "1",
                $"ST-1 at 10⁶ rows runs with {MillionSwitch}=1 (Definition of Done §22 Step 5)");

        var random = new Random(seed);
        var source = GridSource.From(TestRows.Many(totalRows));
        GridSelection selection = GridSelection.Empty;
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 140)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.SelectionChanged, (GridSelection s) => selection = s));

        string[] keys =
        [
            "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "Home", "End",
            "PageUp", "PageDown", "Enter", "Tab", " ", "a", "Escape", "F2", "5",
            ".", "Backspace",
        ];
        var maxScrollTop = (totalRows * 20d) - (140 - 20);

        for (var step = 0; step < 500; step++)
        {
            var operation = random.Next(8);
            switch (operation)
            {
                case 0: // click somewhere on screen
                    await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs
                    {
                        Button = 0,
                        Buttons = 1,
                        OffsetX = random.Next(0, 400),
                        OffsetY = random.Next(0, 120),
                    });
                    break;
                case 1: // drag a step (only lands while a mousedown armed it)
                    if (cut.Find(".ex-viewport").GetAttribute("onmousemove") is not null)
                        break;
                    await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs());
                    break;
                case 2: // a claimed key, sometimes chorded
                    await cut.InvokeAsync(() => cut.Instance.OnKeyAsync(
                        keys[random.Next(keys.Length)],
                        ctrl: random.Next(4) == 0,
                        shift: random.Next(3) == 0,
                        alt: false, meta: false, metaIsPrimary: false));
                    break;
                case 3: // scroll to an arbitrary offset, then let the settle timer run
                    await ScrollToAsync(cut.Find(".ex-scroller"),
                        random.NextDouble() * maxScrollTop, random.Next(0, 100));
                    await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(200)));
                    break;
                case 4: // sort by a random column, sometimes back to none
                    source.OnSortChanged(random.Next(3) == 0
                        ? []
                        : [new SortSpec(Columns()[random.Next(2)].Name, (SortDirection)random.Next(2))]);
                    break;
                case 5: // filter on Active, or clear
                    source.OnFilterChanged(random.Next(2) == 0
                        ? null
                        : new GridFilter(new Dictionary<string, FilterSpec>
                        {
                            ["Active"] = new([new FilterClause(FilterOperator.Equals, random.Next(2) == 0)]),
                        }));
                    break;
                case 6: // a geometry change
                    cut.Render(ps => ps.Add(g => g.RowHeight, 18d + random.Next(4) * 4));
                    break;
                case 7: // the edge-band clock ticks while whatever is armed runs
                    await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(50)));
                    break;
            }

            AssertInvariants(cut, selection, source);
        }
    }

    // ---- ST-2: the grid holds no committed data ---------------------------------------------------
    //
    // An editor opened with F2 and not typed into holds the cell's committed value as its text: a copy
    // of committed data, not half-typed text, so it closes when its row leaves the Window. An editor
    // with typed text stays, as ADR-0142 has it (ADR-0007's note of 2026-10-11).

    [Theory] // ADR-0001 / ADR-0007 / ST-2: after any sequence, an empty Window leaves the grid nothing to paint, and nothing throws
    [InlineData(20261011)]
    [InlineData(424242)]
    public async Task ST2_after_any_sequence_an_empty_window_leaves_nothing_to_paint(int seed)
    {
        var random = new Random(seed);
        var all = TestRows.Many(1_000);
        var asked = new List<RowRange>();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, all[..60])
            .Add(g => g.WindowStart, 0)
            .Add(g => g.TotalCount, all.Length)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 140)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.PinnedColumnCount, 1)
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> _) => { })
            .Add(g => g.OnRangeNeeded, asked.Add));

        string[] keys = ["ArrowDown", "ArrowRight", "End", "PageDown", "Enter", "Tab", " ", "a", "Escape", "F2", "5", "Backspace"];
        for (var step = 0; step < 200; step++)
        {
            switch (random.Next(6))
            {
                case 0: // a press somewhere on screen
                    await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs
                    {
                        Button = 0, Buttons = 1, OffsetX = random.Next(0, 400), OffsetY = random.Next(0, 120),
                    });
                    await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs());
                    break;
                case 1: // a key, sometimes chorded — an edit opened and typed into among them
                    await cut.InvokeAsync(() => cut.Instance.OnKeyAsync(
                        keys[random.Next(keys.Length)], ctrl: random.Next(4) == 0, shift: random.Next(3) == 0,
                        alt: false, meta: false, metaIsPrimary: false));
                    break;
                case 2: // a scroll, and the settle that follows a fling
                    await ScrollToAsync(cut.Find(".ex-scroller"), random.NextDouble() * 18_000, random.Next(0, 100));
                    await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(200)));
                    break;
                case 3: // the Consumer answers the last Range Request with the rows around it
                    if (asked.Count > 0)
                    {
                        var range = asked[^1];
                        var start = Math.Max(0, range.Start - 20);
                        var end = Math.Min(all.Length, range.Start + range.Count + 20);
                        cut.Render(ps => ps.Add(g => g.Window, all[start..end]).Add(g => g.WindowStart, start));
                    }
                    break;
                case 4: // a copy, whatever the selection
                    _ = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayload());
                    break;
                case 5: // a geometry change
                    cut.Render(ps => ps.Add(g => g.RowHeight, 18d + (random.Next(4) * 4)));
                    break;
            }
        }

        // The Consumer's state removed: nothing to show.
        cut.Render(ps => ps
            .Add(g => g.Window, Array.Empty<TestRow>())
            .Add(g => g.WindowStart, 0)
            .Add(g => g.TotalCount, 0));

        AssertNothingPainted(cut);
    }

    [Fact] // ADR-0001 / ADR-0007 / ST-2: the same through a Source — its empty Window leaves nothing behind
    public async Task ST2_a_source_pushing_an_empty_window_leaves_nothing_to_paint()
    {
        var source = new TestSource();
        source.Push(TestRows.Many(50), totalCount: 50);
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 140)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> _) => { }));
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 20, OffsetY = 30 });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs());
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, true, false, false, false));
        // The Formula Bar shows the Focus cell's value: committed data, which must go with the Window.
        Assert.Equal("Row 000001", cut.Find(".ex-formula-bar-text").GetAttribute("value"));

        await cut.InvokeAsync(() => source.Push([], totalCount: 0));

        AssertNothingPainted(cut);
    }

    [Fact] // ADR-0007 / ST-2: half-typed text is the grid's to hold, and is all that stays — no committed value does
    public async Task ST2_an_open_edit_keeps_only_its_own_text_when_the_window_empties()
    {
        var source = new TestSource();
        source.Push(TestRows.Many(50), totalCount: 50);
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 140)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> _) => { }));
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 20, OffsetY = 30 });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs());
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("5", false, false, false, false, false));

        await cut.InvokeAsync(() => source.Push([], totalCount: 0));

        // Uncommitted text is held by the grid, committed data by the Consumer (ADR-0007): the
        // Formula Bar may still show what the user typed, and nothing the rows held.
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".ex-row"));
            Assert.Empty(cut.FindAll(".ex-cell"));
            Assert.Contains(cut.Find(".ex-formula-bar-text").GetAttribute("value") ?? "", new[] { "", "5" });
            Assert.DoesNotContain("Row 0", cut.Markup, StringComparison.Ordinal);
        });
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    [Fact] // ADR-0007 / ST-2: an editor opened with F2 and not typed into holds the committed value as its text — and that must go with the Window
    public async Task ST2_an_edit_opened_with_f2_keeps_no_committed_value_when_the_window_empties()
    {
        var source = new TestSource();
        source.Push(TestRows.Many(50), totalCount: 50);
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 140)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> _) => { }));
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 20, OffsetY = 30 });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs());
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("F2", false, false, false, false, false));
        // F2 seeds the editor with the cell's committed value: the user has typed nothing.
        Assert.Equal("Row 000001", cut.Find(".ex-formula-bar-text").GetAttribute("value"));

        await cut.InvokeAsync(() => source.Push([], totalCount: 0));

        AssertNothingPainted(cut);
    }

    [Fact] // ADR-0007 / ADR-0142 / ST-2: an editor opened with F2 and typed into holds the user's text, not committed data — it stays when its row leaves the Window
    public async Task ST2_an_edit_opened_with_f2_and_typed_into_stays_when_its_row_leaves_the_window()
    {
        var source = new TestSource();
        var rows = TestRows.Many(50);
        source.Push(rows, totalCount: 50);
        var editing = new List<bool>();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 140)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> _) => { })
            .Add(g => g.OnEditingChanged, editing.Add));
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 20, OffsetY = 30 });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs());
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("F2", false, false, false, false, false));
        await cut.Find(".ex-viewport input.ex-editor").InputAsync(new ChangeEventArgs { Value = "Typed" });

        await cut.InvokeAsync(() => source.Push([], totalCount: 0));

        // The rows' data is gone; the typing is the grid's to hold, and the edit has not ended.
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".ex-row"));
            Assert.Empty(cut.FindAll(".ex-cell"));
            Assert.DoesNotContain("Row 0", cut.Markup, StringComparison.Ordinal);
        });
        Assert.Equal([true], editing);

        // The row back in the Window: the editor is still there with what was typed.
        await cut.InvokeAsync(() => source.Push(rows, totalCount: 50));
        cut.WaitForAssertion(() => Assert.Equal("Typed", cut.Find(".ex-formula-bar-text").GetAttribute("value")));
        Assert.Equal([true], editing);
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    [Fact] // ADR-0007 / ST-2: an untyped editor goes only with its row — a Window that still holds the row leaves it open
    public async Task ST2_an_edit_opened_with_f2_stays_while_the_window_holds_its_row()
    {
        var source = new TestSource();
        var rows = TestRows.Many(50);
        source.Push(rows, totalCount: 50);
        var editing = new List<bool>();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 140)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> _) => { })
            .Add(g => g.OnEditingChanged, editing.Add));
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 20, OffsetY = 30 });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs());
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("F2", false, false, false, false, false));

        // Another Window that still holds row 1: a refresh, with the rows past 30 gone.
        await cut.InvokeAsync(() => source.Push(rows[..30], totalCount: 30));

        cut.WaitForAssertion(() => Assert.Equal("Row 000001", cut.Find(".ex-viewport input.ex-editor").GetAttribute("value")));
        Assert.Equal([true], editing);
    }

    [Fact] // ADR-0007 / ST-2: an untyped editor whose row a moved Window no longer holds closes, as Escape would — nothing written, nothing discarded, the edit ended
    public async Task ST2_an_edit_opened_with_f2_closes_when_a_moved_window_no_longer_holds_its_row()
    {
        var source = new TestSource();
        var rows = TestRows.Many(500);
        source.Push(rows[..60], totalCount: 500);
        var editing = new List<bool>();
        var edits = new List<GridEditIntent<TestRow>>();
        var discards = new List<EditDiscardReason>();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 140)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.OnEdit, edits.Add)
            .Add(g => g.OnEditDiscarded, discards.Add)
            .Add(g => g.OnEditingChanged, editing.Add));
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 20, OffsetY = 30 });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs());
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("F2", false, false, false, false, false));
        Assert.Equal("Row 000001", cut.Find(".ex-viewport input.ex-editor").GetAttribute("value"));

        // The Window moves on to rows 200 to 259: row 1 is no longer in it.
        await cut.InvokeAsync(() => source.Push(rows[200..260], windowStart: 200, totalCount: 500));

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".ex-editor:not(.ex-formula-bar-text)"));
            Assert.Equal([true, false], editing);
        });
        Assert.Empty(edits);
        Assert.Empty(discards);
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    // ---- ST-2 as refined on 2026-10-11: the browser says whether nothing was typed -----------------

    /// <summary>A grid over 500 rows holding rows 0 to 59, with row 1's Book open in an editor by F2,
    /// and ST-2's question left for the test to answer.</summary>
    private async Task<(IRenderedComponent<ExGrid<TestRow>> Cut, TestSource Source, TestRow[] Rows,
        JSRuntimeInvocationHandler<bool> Question, List<bool> Editing, List<GridEditIntent<TestRow>> Edits,
        List<EditDiscardReason> Discards, List<GridSelection> Selections)> F2OverRow1Async()
    {
        var source = new TestSource();
        var rows = TestRows.Many(500);
        source.Push(rows[..60], totalCount: 500);
        var question = Js.UnansweredUntouched();
        var editing = new List<bool>();
        var edits = new List<GridEditIntent<TestRow>>();
        var discards = new List<EditDiscardReason>();
        var selections = new List<GridSelection>();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 140)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.OnEdit, edits.Add)
            .Add(g => g.OnEditDiscarded, discards.Add)
            .Add(g => g.OnEditingChanged, editing.Add)
            .Add(g => g.SelectionChanged, selections.Add));
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 20, OffsetY = 30 });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs());
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("F2", false, false, false, false, false));
        Assert.Equal("Row 000001", cut.Find(".ex-viewport input.ex-editor").GetAttribute("value"));
        return (cut, source, rows, question, editing, edits, discards, selections);
    }

    [Theory] // ADR-0007 (ST-2, refined 2026-10-11) / ADR-0142: the ordering that used to lose typing — the Window moves off the row, then the editor's input arrives, then the browser's answer — keeps the edit with what was typed, whatever the answer says
    [InlineData(false)]
    [InlineData(true)]
    public async Task ST2_input_that_reaches_the_grid_before_the_answer_keeps_the_edit_with_its_text(bool answeredUntouched)
    {
        var (cut, source, rows, question, editing, edits, discards, _) = await F2OverRow1Async();

        // The Window moves on to rows 200 to 259: the grid asks the browser, and the editor stands
        // meanwhile — it is not taken down on the core's own copy of the text.
        await cut.InvokeAsync(() => source.Push(rows[200..260], windowStart: 200, totalCount: 500));
        cut.WaitForAssertion(() => Assert.Single(question.Invocations));
        Assert.Equal("Row 000001", (string)question.Invocations.First().Arguments[0]!);
        Assert.Single(cut.FindAll(".ex-viewport input.ex-editor"));

        // Typed in the browser before the question reached it: the input travels ahead of the answer.
        await cut.Find(".ex-viewport input.ex-editor").InputAsync(new ChangeEventArgs { Value = "Row 000001x" });
        // A browser answers "typed" here; answered "untouched" all the same, the grid holds typing
        // and the edit stays.
        await cut.InvokeAsync(() => question.SetResult(answeredUntouched));

        cut.WaitForAssertion(() => Assert.Equal("Row 000001x", cut.Find(".ex-formula-bar-text").GetAttribute("value")));
        Assert.Equal([true], editing);
        Assert.Empty(discards);
        Assert.Equal(answeredUntouched ? 1 : 0, Js.CloseAnswered.Invocations.Count);

        // The row back in the Window, Enter writes what was typed onto it.
        await cut.InvokeAsync(() => source.Push(rows[..60], totalCount: 500));
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("Enter", false, false, false, false, false));
        var edit = Assert.Single(edits);
        Assert.Same(rows[1], edit.Row);
        Assert.Equal("Row 000001x", edit.Value);
        Assert.Equal([true, false], editing);
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    [Fact] // ADR-0007 (ST-2, refined 2026-10-11) / ADR-0010: answered "untouched", the edit closes as Escape does — the gate told it is over and the keyboard handed back before the listener lets its held keys go — and a held key is then the grid's
    public async Task ST2_answered_untouched_the_edit_closes_and_the_held_keys_are_the_grids()
    {
        var (cut, source, rows, question, editing, edits, discards, selections) = await F2OverRow1Async();
        await cut.InvokeAsync(() => source.Push(rows[200..260], windowStart: 200, totalCount: 500));
        cut.WaitForAssertion(() => Assert.Single(question.Invocations));
        var before = Js.GateTold.Count;

        await cut.InvokeAsync(() => question.SetResult(true));

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".ex-editor:not(.ex-formula-bar-text)"));
            Assert.Equal([true, false], editing);
            Assert.Single(Js.CloseAnswered.Invocations);
        });
        Assert.Empty(edits);
        Assert.Empty(discards);
        // In the order the listener needs them: the gate in its new mode and the keyboard back on the
        // root, then the answer, after which the held keys go on against that mode.
        Assert.Equal(["setEditing none", "reclaimFocus", "closeAnswered"], Js.GateTold.Skip(before));

        // A held ↓, replayed against no edit: the grid's key, moving the Focus from row 1 to row 2.
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, false, false, false, false));
        Assert.Equal(new CellPosition(2, 0), selections[^1].Focus);
        Assert.Empty(cut.FindAll(".ex-editor:not(.ex-formula-bar-text)"));
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    [Fact] // ADR-0007 (ST-2, refined 2026-10-11): an "untouched" that comes after the row is back finds nothing to close — the edit stays, and the listener is still told, so what it held goes on into the edit
    public async Task ST2_answered_untouched_after_the_row_came_back_the_edit_stays()
    {
        var (cut, source, rows, question, editing, _, discards, _) = await F2OverRow1Async();
        await cut.InvokeAsync(() => source.Push(rows[200..260], windowStart: 200, totalCount: 500));
        cut.WaitForAssertion(() => Assert.Single(question.Invocations));
        await cut.InvokeAsync(() => source.Push(rows[..60], totalCount: 500));

        await cut.InvokeAsync(() => question.SetResult(true));

        cut.WaitForAssertion(() => Assert.Single(Js.CloseAnswered.Invocations));
        Assert.Equal("Row 000001", cut.Find(".ex-viewport input.ex-editor").GetAttribute("value"));
        Assert.Equal([true], editing);
        Assert.Empty(discards);
    }

    [Fact] // ADR-0007 (ST-2) / ADR-0142: an editor the grid already knows to hold typing is not asked about — it stays, as ADR-0142 has it
    public async Task ST2_an_editor_known_to_hold_typing_is_not_asked_about()
    {
        var (cut, source, rows, question, editing, _, _, _) = await F2OverRow1Async();
        await cut.Find(".ex-viewport input.ex-editor").InputAsync(new ChangeEventArgs { Value = "Typed" });

        await cut.InvokeAsync(() => source.Push(rows[200..260], windowStart: 200, totalCount: 500));

        cut.WaitForAssertion(() => Assert.Equal("Typed", cut.Find(".ex-formula-bar-text").GetAttribute("value")));
        Assert.Empty(question.Invocations);
        Assert.Equal([true], editing);
    }

    [Fact] // ADR-0007 / ADR-0011 / ADR-0142 / ST-2: without a Row Key an untyped editor names its row by position under the order it opened under — an order move, the same rows reordered, closes it: nothing written, nothing discarded
    public async Task ST2_without_a_row_key_an_untyped_editor_closes_when_the_order_moves()
    {
        var source = new TestSource();
        var rows = TestRows.Many(50);
        source.Push(rows, totalCount: 50);
        var editing = new List<bool>();
        var edits = new List<GridEditIntent<TestRow>>();
        var discards = new List<EditDiscardReason>();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 140)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.OnEdit, edits.Add)
            .Add(g => g.OnEditDiscarded, discards.Add)
            .Add(g => g.OnEditingChanged, editing.Add));
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 20, OffsetY = 30 });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs());
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("F2", false, false, false, false, false));
        Assert.Equal("Row 000001", cut.Find(".ex-viewport input.ex-editor").GetAttribute("value"));

        // The same rows, reordered under a new order: position 1 now holds Row 000002, and the
        // Window holds no row the position-bound editor can call its own.
        TestRow[] reordered = [rows[0], rows[2], rows[1], .. rows[3..]];
        await cut.InvokeAsync(() => source.Push(reordered, totalCount: 50, rowSequenceVersion: 1));

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".ex-editor:not(.ex-formula-bar-text)"));
            Assert.Equal([true, false], editing);
        });
        Assert.Empty(edits);
        Assert.Empty(discards);
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    [Fact] // ADR-0007 / ADR-0140 / ST-2: with a Row Key an untyped editor goes with its row by key — a Window holding another row at its position closes it, one holding its row elsewhere does not
    public async Task ST2_with_a_row_key_an_untyped_editor_goes_with_its_row_not_its_position()
    {
        var source = new TestSource();
        var rows = TestRows.Many(50);
        source.Push(rows, totalCount: 50);
        var editing = new List<bool>();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowKey, (Func<TestRow, object>)(row => row.Book))
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 140)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> _) => { })
            .Add(g => g.OnEditingChanged, editing.Add));
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 20, OffsetY = 30 });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs());
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("F2", false, false, false, false, false));

        // Row 000001 moved to position 3, under a new order: the editor follows it and stays.
        TestRow[] moved = [rows[0], rows[2], rows[3], rows[1], .. rows[4..]];
        await cut.InvokeAsync(() => source.Push(moved, totalCount: 50, rowSequenceVersion: 1));
        cut.WaitForAssertion(() => Assert.Equal("Row 000001", cut.Find(".ex-viewport input.ex-editor").GetAttribute("value")));
        Assert.Equal([true], editing);

        // Row 000001 gone, another row at every position it stood at: the editor closes.
        TestRow[] without = [.. moved.Where(row => !ReferenceEquals(row, rows[1]))];
        await cut.InvokeAsync(() => source.Push(without, totalCount: 49, rowSequenceVersion: 2));
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".ex-editor:not(.ex-formula-bar-text)"));
            Assert.Equal([true, false], editing);
        });
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    /// <summary>ST-2's pass condition: no row, cell, Placeholder, Focus or range is painted, the
    /// Formula Bar shows nothing, no value of the data is anywhere in the markup, and nothing was
    /// thrown on the way.</summary>
    private void AssertNothingPainted(IRenderedComponent<ExGrid<TestRow>> cut)
    {
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".ex-row"));
            Assert.Empty(cut.FindAll(".ex-cell"));
            Assert.Empty(cut.FindAll(".ex-placeholder"));
            Assert.Empty(cut.FindAll(".ex-focus"));
            Assert.Empty(cut.FindAll(".ex-range"));
            Assert.Equal("", cut.Find(".ex-formula-bar-text").GetAttribute("value") ?? "");
            Assert.DoesNotContain("Row 0", cut.Markup, StringComparison.Ordinal);
        });
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    private static void AssertInvariants(
        IRenderedComponent<ExGrid<TestRow>> cut, GridSelection selection, InMemoryGridSource<TestRow> source)
    {
        var extentRows = source.TotalCount!.Value;

        // Every rectangle inside the extent, and the count is the sum of the areas.
        long area = 0;
        foreach (var range in selection.Ranges)
        {
            Assert.InRange(range.TopRow, 0, extentRows - 1);
            Assert.InRange(range.BottomRow, range.TopRow, extentRows - 1);
            Assert.InRange(range.LeftColumn, 0, 3);
            Assert.InRange(range.RightColumn, range.LeftColumn, 3);
            area += range.CellCount;
        }
        Assert.Equal(area, selection.CellCount);

        // The Focus is always inside the Selection, in the range it names, and the Extent is a
        // cell of that range — there is no detached state (ADR-0052).
        if (!selection.IsEmpty)
        {
            Assert.True(selection.FocusRange.Contains(selection.Focus));
            Assert.True(selection.FocusRange.Contains(selection.Extent));
            Assert.Contains(selection.FocusRange, selection.Ranges);
        }

        // The painted rows are one contiguous, absolutely-indexed slice.
        var rows = cut.FindAll(".ex-row");
        int? previous = null;
        foreach (var row in rows)
        {
            var index = int.Parse(row.GetAttribute("aria-rowindex")!);
            Assert.InRange(index, 1, Math.Max(1, extentRows));
            if (previous is { } p)
                Assert.Equal(p + 1, index);
            previous = index;
        }
    }
}
