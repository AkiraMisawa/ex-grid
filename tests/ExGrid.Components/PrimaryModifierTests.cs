using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// KB-4: the keyboard and the mouse read one answer about which modifier is primary (ADR-0012).
/// Control is primary everywhere; Meta is primary only where the browser says it is Command, which
/// is asked once at attach. So on each platform answer, a modifier adds a range on a click exactly
/// when the same modifier with A selects everything — Ctrl+click and Ctrl+A never disagree, and
/// neither do Cmd+click and Cmd+A. A Win+click off an Apple platform is a plain click, as Win+A is
/// no key the grid takes.
/// </summary>
public class PrimaryModifierTests : GridTestContext
{
    private const int Rows = 3;
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(Action<GridSelection> changed)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20)
            .Add(g => g.ViewportHeight, 100)
            .Add(g => g.ViewportWidth, 250)
            .Add(g => g.SelectionChanged, changed));

    private static async Task PressCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y, bool ctrl = false, bool meta = false)
    {
        var viewport = cut.Find(".ex-viewport");
        await viewport.MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, CtrlKey = ctrl, MetaKey = meta, OffsetX = x, OffsetY = y });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0, CtrlKey = ctrl, MetaKey = meta, OffsetX = x, OffsetY = y });
    }

    /// <summary>Whether the modifier adds a range on a click: a press on Book row 0, then the
    /// modified press on Amount row 2.</summary>
    private async Task<bool> ClickAddsARangeAsync(bool ctrl, bool meta)
    {
        GridSelection selection = GridSelection.Empty;
        var cut = RenderGrid(s => selection = s);
        await PressCellAsync(cut, 50, 10);
        await PressCellAsync(cut, 150, 50, ctrl, meta);
        Assert.NotEmpty(selection.Ranges);
        return selection.Ranges.Count == 2;
    }

    /// <summary>Whether the modifier with A selects every cell, on the keyboard's path, told the
    /// same platform answer the mouse was given at attach.</summary>
    private async Task<bool> KeySelectsAllAsync(bool ctrl, bool meta, bool metaIsPrimary)
    {
        GridSelection selection = GridSelection.Empty;
        var cut = RenderGrid(s => selection = s);
        await PressCellAsync(cut, 50, 10);
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("a", ctrl, false, false, meta, metaIsPrimary));
        return selection.CellCount == Rows * Columns().Length;
    }

    [Theory] // ADR-0012 / KB-4: Ctrl+click and Ctrl+A, and Meta+click and Meta+A, agree on every platform answer
    [InlineData(false)]
    [InlineData(true)]
    public async Task KB4_the_mouse_and_the_keyboard_agree_on_the_primary_modifier(bool metaIsPrimary)
    {
        if (metaIsPrimary)
            Js.MetaIsPrimary();

        var ctrlClick = await ClickAddsARangeAsync(ctrl: true, meta: false);
        var ctrlA = await KeySelectsAllAsync(ctrl: true, meta: false, metaIsPrimary);
        var metaClick = await ClickAddsARangeAsync(ctrl: false, meta: true);
        var metaA = await KeySelectsAllAsync(ctrl: false, meta: true, metaIsPrimary);

        // The agreement KB-4 is about…
        Assert.Equal(ctrlClick, ctrlA);
        Assert.Equal(metaClick, metaA);
        // …and the answer it agrees on: Control everywhere, Meta only where it is Command.
        Assert.True(ctrlClick);
        Assert.Equal(metaIsPrimary, metaClick);
    }
}
