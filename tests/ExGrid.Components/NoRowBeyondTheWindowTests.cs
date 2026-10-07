using System.Runtime.CompilerServices;
using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The grid holds a Consumer's row instance, and its Row Key, only while it is in the Window the
/// grid was last given (ADR-0160, LV-22). Checked by reachability, never by megabytes: weak
/// references are taken to the rows of each earlier Window, a run of new Windows is handed over, a
/// full collection is made, and none of them may be alive. Each Window is made, handed over and let
/// go in a method of its own, so the JIT keeps no reference to it in the test's frame.
///
/// The holdings ADR-0160 names are kept out of the run, or shown to hold no row: no press is in
/// flight, the Viewport has a size and is never flung, so nothing defers the Auto widths' measure, and
/// an open editor holds its row's Row Key, not the row.
///
/// <para>One more set of rows ADR-0160 names as the renderer's: Blazor keeps a component's previous
/// render tree as its next buffer, and those frames still name the rows the render before the newest
/// painted until the grid renders again (<c>ComponentState._nextRenderTree</c>). The checks name
/// exactly those rows, read from the positions that render painted, and no others.</para>
/// </summary>
public class NoRowBeyondTheWindowTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, editable: true),
        GridColumn<TestRow>.ActionColumn("Do", [new GridAction("approve", "Approve")], width: Fixed100),
    ];

    // A bold Book, a border under Amount: what the appearance cache keeps per row (ADR-0050, item 15).
    private static readonly CellAppearanceOf<TestRow> Appearance = static (row, column) => column.Name switch
    {
        "Book" => new CellAppearance { Bold = row.Amount % 2 == 0 },
        "Amount" => new CellAppearance { Bottom = new Border(BorderStyle.Thin, default) },
        _ => default,
    };

    private static readonly Func<TestRow, object> ByBook = static row => row.Book;

    private IRenderedComponent<ExGrid<TestRow>> RenderEmpty(bool appearance, bool rowKey)
        => Render<ExGrid<TestRow>>(ps =>
        {
            // Started empty, so nothing the test framework keeps of the first parameters is a row.
            ps.Add(g => g.Window, Array.Empty<TestRow>())
              .Add(g => g.TotalCount, 50)
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.OnAction, (GridActionEventArgs<TestRow> _) => { });
            if (appearance)
                ps.Add(g => g.CellAppearance, Appearance);
            if (rowKey)
                ps.Add(g => g.RowKey, ByBook);
        });

    /// <summary>Hands the grid a Window of new row instances — the same Books, so under a Row Key
    /// every row component is kept (ADR-0140) — and answers weak references to them, and nothing
    /// that holds them.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] HandOverANewWindow(IRenderedComponent<ExGrid<TestRow>> cut, int version, bool reordered)
    {
        var rows = TestRows.Many(50);
        if (reordered)
            Array.Reverse(rows);
        cut.Render(ps => ps.Add(g => g.Window, rows).Add(g => g.RowSequenceVersion, version));
        return [.. rows.Select(static row => new WeakReference(row))];
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task ScrollAndSelectAsync(IRenderedComponent<ExGrid<TestRow>> cut, int round)
    {
        // A slice of its own each round, and a cell selected on it: the appearance cache and the
        // row components' serials see other rows than the round before.
        await ScrollToAsync(cut.Find(".ex-scroller"), round % 3 * 40);
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 10 + (round % 5 * 20) });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0, OffsetX = 50, OffsetY = 10 + (round % 5 * 20) });
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    /// <summary>The positions the newest render painted, read from the rows' own
    /// <c>aria-rowindex</c>: numbers, so the test holds no row by reading them.</summary>
    private static HashSet<int> PaintedPositions(IRenderedComponent<ExGrid<TestRow>> cut)
        => [.. cut.FindAll(".ex-viewport [role=row]").Select(static row => int.Parse(row.GetAttribute("aria-rowindex")!,
            System.Globalization.CultureInfo.InvariantCulture) - 1)];

    /// <summary>Hands the grid twelve new Windows, each in its own method, scrolling and selecting
    /// between them, then the Window in hand. Answers weak references to each earlier Window's rows,
    /// by position; the positions the render before the newest painted, which is the one that
    /// painted the last of them; and weak references to the current Window's rows.</summary>
    private async Task<(List<WeakReference[]> Earlier, HashSet<int> PaintedBefore, WeakReference[] Current)> RunOfNewWindowsAsync(
        IRenderedComponent<ExGrid<TestRow>> cut)
    {
        var earlier = new List<WeakReference[]>();
        for (var round = 0; round < 12; round++)
        {
            // Every third Window in another order, so the Row Sequence Version moves too.
            earlier.Add(HandOverANewWindow(cut, version: round / 3, reordered: round % 3 == 2));
            await ScrollAndSelectAsync(cut, round);
        }
        var paintedBefore = PaintedPositions(cut);
        // The Window in hand now, taken in by the render that ends the run.
        return (earlier, paintedBefore, HandOverANewWindow(cut, version: 99, reordered: false));
    }

    [Theory] // ADR-0160 / LV-22: after a run of new Windows and a full collection, with the render that took in the last one and no other, no row of an earlier Window is alive but the ones the render before it painted, which Blazor keeps — with CellAppearance declared and not, a Row Key declared and not
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task No_row_of_an_earlier_window_is_alive_after_a_run_of_new_windows(bool appearance, bool rowKey)
    {
        var cut = RenderEmpty(appearance, rowKey);
        var (earlier, paintedBefore, current) = await RunOfNewWindowsAsync(cut);

        Collect();

        Assert.True(current.All(static r => r.IsAlive), "the Window in hand is held, as it is painted");
        for (var round = 0; round < earlier.Count - 1; round++)
            AssertNoneAlive(earlier, round);
        // The last of the earlier Windows: alive only where the render before the newest painted it.
        var last = earlier[^1];
        var aliveElsewhere = Enumerable.Range(0, last.Length).Where(i => last[i].IsAlive && !paintedBefore.Contains(i)).ToArray();
        Assert.True(aliveElsewhere.Length == 0,
            $"rows {string.Join(", ", aliveElsewhere)} of the Window before are alive, and the render before painted only {string.Join(", ", paintedBefore.Order())} (ADR-0160)");
    }

    [Fact] // ADR-0160 / LV-22, ADR-0011 (note of 2026-10-07): an open editor holds the Row Key of its row, not the row — the rows of earlier Windows go while it stays open
    public async Task An_open_editor_holds_its_rows_key_and_no_row()
    {
        var cut = RenderEmpty(appearance: false, rowKey: true);
        var opened = HandOverANewWindow(cut, version: 0, reordered: false);
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 10 });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0, OffsetX = 50, OffsetY = 10 });
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("5", false, false, false, false, false));
        Assert.Single(cut.FindAll("input.ex-editor"));
        var later = new List<WeakReference[]>();
        for (var round = 0; round < 3; round++)
            later.Add(HandOverANewWindow(cut, version: round + 1, reordered: round % 2 == 0));
        HandOverANewWindow(cut, version: 9, reordered: false);

        Collect();

        Assert.Single(cut.FindAll("input.ex-editor"));
        Assert.DoesNotContain(opened, static r => r.IsAlive);
        Assert.DoesNotContain(later[0], static r => r.IsAlive);
        Assert.DoesNotContain(later[1], static r => r.IsAlive);
    }

    private static void AssertNoneAlive(List<WeakReference[]> windows, int round)
    {
        var alive = windows[round].Count(static r => r.IsAlive);
        Assert.True(alive == 0, $"{alive} rows of Window {round} are still alive after {windows.Count - round} newer Windows (ADR-0160)");
    }

    [Fact] // ADR-0160 / LV-22, ADR-0142: a press the core answered holds its row no longer than the press — the row goes with the next Window
    public async Task An_answered_action_press_holds_its_row_no_longer()
    {
        var cut = RenderEmpty(appearance: false, rowKey: false);
        var first = HandOverANewWindow(cut, version: 0, reordered: false);
        var paint = int.Parse(cut.Find(".ex-viewport").GetAttribute("data-ex-paint")!, System.Globalization.CultureInfo.InvariantCulture);
        // Every row a new instance: the press's row component is gone, and the core answers it.
        var second = HandOverANewWindow(cut, version: 0, reordered: false);
        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(paint, row: 0, column: 2, action: 0));
        HandOverANewWindow(cut, version: 0, reordered: false);
        HandOverANewWindow(cut, version: 0, reordered: false);

        Collect();

        // The press's row is not held past the press: both Windows before the last two are gone
        // (the one before is the renderer's previous frames' — see above).
        Assert.DoesNotContain(first, static r => r.IsAlive);
        Assert.DoesNotContain(second, static r => r.IsAlive);
    }
}
