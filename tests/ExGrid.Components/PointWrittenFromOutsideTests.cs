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
/// Point written from outside the grid (ADR-0058; SH-32, SH-35's grid half): a Consumer that took a
/// press on another instance hands the grid text to write as Point writes a Reference — where Point
/// writes, replacing what this Point wrote — and may take it back; while it stands the Name Box is
/// empty, F4 changes nothing and the arrows move nothing. The grid tells its Consumer where the open
/// edit stands with respect to Point. The grid reads nothing of the text. 50 rows of 20px in a
/// 350 × 200 Viewport under a 20px header; Book (A) and Note (B) edit, Amount (C) does not.
/// </summary>
public class PointWrittenFromOutsideTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private const string Lookup = "XLOOKUP(\"R-4\", T[Id], T[PV])";

    private readonly List<PointState> _told = [];
    private readonly List<(string Text, int Start, int End)> _cycled = [];

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Note", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    /// <summary>A Reference can go after <c>=</c>, an operator, <c>(</c> or <c>,</c> in a Formula —
    /// the shape of ExSheet's predicate.</summary>
    private static bool PointAt(string text, int caret)
        => text.StartsWith('=') && caret > 0 && caret <= text.Length && "=+-*/(,".Contains(text[caret - 1]);

    private static string ReferenceText(SelectionRange range)
        => FormattableString.Invariant($"{(char)('A' + range.LeftColumn)}{range.TopRow + 1}");

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(bool point = true, List<GridEditIntent<TestRow>>? intents = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(50))
              .Add(g => g.TotalCount, 50)
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 200)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.ShowFormulaBar, true)
              .Add(g => g.NameBoxLabel, (Func<CellPosition, string?>)(c => FormattableString.Invariant($"{(char)('A' + c.Column)}{c.Row + 1}")))
              .Add(g => g.OnEdit, (GridEditIntent<TestRow> intent) => intents?.Add(intent))
              .Add(g => g.OnPointStateChanged, (PointState state) => _told.Add(state))
              .Add(g => g.CycleReference, (Func<string, int, int, EditorRewrite?>)((text, start, end) =>
              {
                  _cycled.Add((text, start, end));
                  return new EditorRewrite(text.Replace("A", "$A$", StringComparison.Ordinal), start + 2, start + 2);
              }));
            if (point)
            {
                ps.Add(g => g.PointAt, PointAt)
                  .Add(g => g.ReferenceText, ReferenceText);
            }
        });

    private static Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y, bool shift = false)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y, ShiftKey = shift });

    private static Task ReleaseAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0, Buttons = 0, OffsetX = x, OffsetY = y });

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, string? text = null, int caret = -1, bool shift = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(
            key, false, shift, false, false, false, editorText: text, editorCaret: caret, editorSelectionEnd: caret));

    private static async Task TypeAsync(IRenderedComponent<ExGrid<TestRow>> cut, string text)
    {
        await cut.Find(".ex-viewport .ex-editor").InputAsync(new ChangeEventArgs { Value = text });
        await cut.InvokeAsync(() => cut.Instance.OnEditorCaretAsync(text, text.Length));
    }

    /// <summary>Selects B3 and types <c>=</c> onto it: Overwrite, with the caret after it.</summary>
    private static async Task StartFormulaAsync(IRenderedComponent<ExGrid<TestRow>> cut)
    {
        await ClickAsync(cut, 150, 50);
        await ReleaseAsync(cut, 150, 50);
        await PressAsync(cut, "=");
    }

    private static string EditorText(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find(".ex-viewport .ex-editor").GetAttribute("value") ?? "";

    private static string NameBox(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find("input.ex-name-box").GetAttribute("value") ?? "";

    private static Task<bool> WriteAsync(IRenderedComponent<ExGrid<TestRow>> cut, string text)
        => cut.InvokeAsync(() => cut.Instance.WritePointedTextAsync(text));

    private static Task<bool> TakeBackAsync(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.InvokeAsync(() => cut.Instance.TakeBackPointedTextAsync());

    private (string Text, int Caret, int End)? CaretPlaced()
        => JSInterop.Invocations.LastOrDefault(i => i.Identifier == "setCaret") is { } call
            ? ((string)call.Arguments[0]!, (int)call.Arguments[1]!, (int)call.Arguments[2]!)
            : null;

    private List<string> EditingModesTold()
        => [.. JSInterop.Invocations.Where(i => i.Identifier == "setEditing").Select(i => (string)i.Arguments[0]!)];

    [Fact] // ADR-0058 / SH-32: text handed in is written where Point writes, after =, with the caret after it
    public async Task Text_handed_in_is_written_at_the_caret_where_a_reference_can_go()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(intents: intents);
        await StartFormulaAsync(cut);

        Assert.True(await WriteAsync(cut, Lookup));

        Assert.Equal("=" + Lookup, EditorText(cut));
        Assert.Equal("=" + Lookup, cut.Find("input.ex-formula-bar-text").GetAttribute("value"));
        Assert.Equal(("=" + Lookup, 1 + Lookup.Length, 1 + Lookup.Length), CaretPlaced());
        // In Point, and the gate keeps the arrows; no outline is drawn over this grid's cells.
        Assert.Equal("point", EditingModesTold()[^1]);
        Assert.Empty(cut.FindAll(".ex-point"));
        Assert.Empty(intents);
    }

    [Fact] // ADR-0058 / SH-32: a further write replaces what this Point wrote, and so does a press on the grid's own cell
    public async Task A_further_write_or_a_press_on_the_grid_replaces_what_was_written()
    {
        var cut = RenderGrid();
        await StartFormulaAsync(cut);
        await TypeAsync(cut, "=SUM(");
        Assert.True(await WriteAsync(cut, Lookup));

        Assert.True(await WriteAsync(cut, "T[PV]"));
        Assert.Equal("=SUM(T[PV]", EditorText(cut));

        // A press on this grid's cell replaces it with that cell's Reference, and its outline stands.
        await ClickAsync(cut, 250, 90);
        Assert.Equal("=SUM(C5", EditorText(cut));
        Assert.Single(cut.FindAll(".ex-selection .ex-point"));
        Assert.Equal("C5", NameBox(cut));

        // And a write from outside replaces the grid's own Reference in its turn.
        await ReleaseAsync(cut, 250, 90);
        Assert.True(await WriteAsync(cut, Lookup));
        Assert.Equal("=SUM(" + Lookup, EditorText(cut));
        Assert.Empty(cut.FindAll(".ex-point"));
    }

    [Fact] // ADR-0058 / SH-35: after a write from outside, the Name Box is empty, and it names the Focus again once the Point ends
    public async Task The_name_box_is_empty_while_what_was_written_from_outside_stands()
    {
        var cut = RenderGrid();
        await StartFormulaAsync(cut);
        Assert.Equal("B3", NameBox(cut));

        await WriteAsync(cut, Lookup);
        Assert.Equal("", NameBox(cut));

        await TypeAsync(cut, "=" + Lookup + "*");
        Assert.Equal("B3", NameBox(cut));
    }

    [Fact] // ADR-0058 / SH-35: after a write from outside, F4 changes nothing and the Consumer's rewrite is not asked
    public async Task F4_changes_nothing_after_a_write_from_outside()
    {
        var cut = RenderGrid();
        await StartFormulaAsync(cut);
        await WriteAsync(cut, "T[A]");
        var placed = CaretPlaced();

        await PressAsync(cut, "F4", "=T[A]", 5);

        Assert.Empty(_cycled);
        Assert.Equal("=T[A]", EditorText(cut));
        Assert.Equal(placed, CaretPlaced());

        // Pointed on this grid's own cell, F4 cycles again.
        await ClickAsync(cut, 50, 30);
        await ReleaseAsync(cut, 50, 30);
        await PressAsync(cut, "F4", "=A2", 3);
        Assert.Single(_cycled);
    }

    [Fact] // ADR-0058 / SH-35: after a write from outside the arrows move and write nothing, commit nothing, and move no Focus
    public async Task The_arrows_move_nothing_after_a_write_from_outside()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(intents: intents);
        await StartFormulaAsync(cut);
        await WriteAsync(cut, Lookup);

        foreach (var key in new[] { "ArrowDown", "ArrowRight", "Home", "End" })
            await PressAsync(cut, key, "=" + Lookup, 1 + Lookup.Length);
        await PressAsync(cut, "ArrowUp", "=" + Lookup, 1 + Lookup.Length, shift: true);

        Assert.Equal("=" + Lookup, EditorText(cut));
        Assert.Empty(intents);
        Assert.Empty(cut.FindAll(".ex-point"));
        Assert.Equal("", NameBox(cut));
    }

    [Fact] // ADR-0058 / SH-32: nothing is written where no Reference can go, and nothing without an open edit
    public async Task Nothing_is_written_where_no_reference_can_go()
    {
        var cut = RenderGrid();
        Assert.False(await WriteAsync(cut, Lookup));

        await StartFormulaAsync(cut);
        await TypeAsync(cut, "=1");
        Assert.False(await WriteAsync(cut, Lookup));
        Assert.Equal("=1", EditorText(cut));

        await Assert.ThrowsAsync<ArgumentException>(() => WriteAsync(cut, ""));
    }

    [Fact] // ADR-0058 / SH-32: without PointAt nothing is written from outside, and nothing is told
    public async Task Without_point_nothing_is_written_and_nothing_is_told()
    {
        var cut = RenderGrid(point: false);
        await StartFormulaAsync(cut);

        Assert.False(await WriteAsync(cut, Lookup));
        Assert.Equal("=", EditorText(cut));
        Assert.Empty(_told);
    }

    [Fact] // ADR-0058 / SH-32: a write taken back returns the edit to what it was before it: text, caret and state
    public async Task A_write_taken_back_returns_the_edit_to_what_it_was()
    {
        var cut = RenderGrid();
        await StartFormulaAsync(cut);
        await TypeAsync(cut, "=SUM(");
        await WriteAsync(cut, Lookup);

        Assert.True(await TakeBackAsync(cut));

        Assert.Equal("=SUM(", EditorText(cut));
        Assert.Equal(("=SUM(", 5, 5), CaretPlaced());
        Assert.Equal("B3", NameBox(cut));
        Assert.Equal(PointState.InPoint, _told[^1]);
        // Nothing more to take back.
        Assert.False(await TakeBackAsync(cut));
        // And a write goes in at the caret again.
        Assert.True(await WriteAsync(cut, "T[PV]"));
        Assert.Equal("=SUM(T[PV]", EditorText(cut));
    }

    [Fact] // ADR-0058 / SH-32: taken back over a Reference this grid pointed at, that Reference and its outline stand again
    public async Task A_write_taken_back_over_the_grids_own_reference_restores_it()
    {
        var cut = RenderGrid();
        await StartFormulaAsync(cut);
        await PressAsync(cut, "ArrowDown", "=", 1);
        Assert.Equal("=B4", EditorText(cut));
        await WriteAsync(cut, Lookup);
        Assert.Equal("=" + Lookup, EditorText(cut));

        Assert.True(await TakeBackAsync(cut));

        Assert.Equal("=B4", EditorText(cut));
        Assert.Single(cut.FindAll(".ex-selection .ex-point"));
        Assert.Equal("B4", NameBox(cut));
        // Pointing goes on from there: ↓ moves the outline and rewrites the same place.
        await PressAsync(cut, "ArrowDown", "=B4", 3);
        Assert.Equal("=B5", EditorText(cut));
    }

    [Fact] // ADR-0058 / SH-32: once typed on, what was written from outside is the user's, and is not taken back
    public async Task Nothing_is_taken_back_once_typed_on()
    {
        var cut = RenderGrid();
        await StartFormulaAsync(cut);
        await WriteAsync(cut, Lookup);
        await TypeAsync(cut, "=" + Lookup + "*");

        Assert.False(await TakeBackAsync(cut));
        Assert.Equal("=" + Lookup + "*", EditorText(cut));
    }

    [Fact] // ADR-0058 / SH-35: the Consumer is told where the edit stands with respect to Point, at each change and only then
    public async Task The_point_state_is_told_at_each_change()
    {
        var cut = RenderGrid();
        await StartFormulaAsync(cut);
        Assert.Equal([PointState.InPoint], _told);

        await TypeAsync(cut, "=SUM(1");
        await TypeAsync(cut, "=SUM(1,");
        await WriteAsync(cut, Lookup);
        await TypeAsync(cut, "=SUM(1," + Lookup + ")");
        await PressAsync(cut, "Escape");

        Assert.Equal(
            [PointState.InPoint, PointState.None, PointState.InPoint, PointState.WrittenFromOutside, PointState.None],
            _told);
    }

    [Fact] // ADR-0058 / SH-35: a caret not known yet is waited for, not told as out of Point
    public async Task A_caret_not_known_yet_is_waited_for()
    {
        var cut = RenderGrid();
        await StartFormulaAsync(cut);

        // The input arrives before the listener's report of its caret.
        await cut.Find(".ex-viewport .ex-editor").InputAsync(new ChangeEventArgs { Value = "=1+" });
        Assert.Equal([PointState.InPoint], _told);
        await cut.InvokeAsync(() => cut.Instance.OnEditorCaretAsync("=1+", 3));

        Assert.Equal([PointState.InPoint], _told);
    }
}
