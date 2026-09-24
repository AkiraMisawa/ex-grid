using Bunit;
using ExGrid.Components.Tests.Support;
using ExGrid.Rows;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Row Stripes (ADR-0038): one class on the row, from the row's position in the whole
/// result — never from its place on the screen. 20px rows in a 100px Viewport, of which
/// the header takes the first 20: a Viewport is five rows, and a move of more than five
/// is a fling (ADR-0004).
/// </summary>
public class RowStripeTests : GridTestContext
{
    private const double RowHeightPx = 20;
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(150);

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        bool? stripes = true, TestRow[]? window = null, int windowStart = 0, int total = 1000,
        Action<ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? extra = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, window ?? TestRows.Many(total))
              .Add(g => g.WindowStart, windowStart)
              .Add(g => g.TotalCount, total)
              .Add(g => g.Columns, TestRows.Columns())
              .Add(g => g.RowHeight, RowHeightPx)
              .Add(g => g.ViewportHeight, 100);
            if (stripes is { } on)
                ps.Add(g => g.StripeRows, on);
            extra?.Invoke(ps);
        });

    /// <summary>The row's position in the whole result, from the attribute the root
    /// already states it in (ADR-0033) — zero-based.</summary>
    private static int PositionOf(AngleSharp.Dom.IElement row)
        => int.Parse(row.GetAttribute("aria-rowindex")!) - 1;

    /// <summary>Every painted row — data row or Placeholder — wears the stripe exactly
    /// when its position in the result is odd.</summary>
    private static void AssertStripesFollowTheResult(IRenderedComponent<ExGrid<TestRow>> cut)
    {
        var rows = cut.FindAll(".ex-row");
        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Equal(
            PositionOf(row) % 2 == 1, row.ClassList.Contains("ex-row-stripe")));
    }

    private static bool FirstRowIsStriped(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.FindAll(".ex-row")[0].ClassList.Contains("ex-row-stripe");

    [Fact] // UX-15 / ADR-0038: stripes are off unless a Consumer asks for them
    public void Stripes_are_off_by_default()
    {
        var cut = RenderGrid(stripes: null);

        Assert.NotEmpty(cut.FindAll(".ex-row"));
        Assert.Empty(cut.FindAll(".ex-row-stripe"));
    }

    [Fact] // UX-15 / ADR-0038: counted in the result, never on the screen — exact parity at every offset
    public async Task The_stripe_stays_with_its_row_at_every_scroll_offset()
    {
        var cut = RenderGrid();
        AssertStripesFollowTheResult(cut);
        Assert.False(FirstRowIsStriped(cut));

        // Three rows down, the first painted row is the fourth of the result: the stripe
        // moved with the rows, and :nth-child() would have left it on the first child.
        await ScrollToAsync(cut.Find(".ex-scroller"), 3 * RowHeightPx);
        AssertStripesFollowTheResult(cut);
        Assert.True(FirstRowIsStriped(cut));

        // A Viewport further and half a row in: the boundary row is part-shown.
        await ScrollToAsync(cut.Find(".ex-scroller"), 8.5 * RowHeightPx);
        AssertStripesFollowTheResult(cut);
        Assert.False(FirstRowIsStriped(cut));
    }

    [Fact] // UX-15 / ADR-0038 / ADR-0004: a flung row is striped by its position, so the pattern does not flicker
    public async Task Placeholders_of_a_fling_are_striped_by_their_position()
    {
        var cut = RenderGrid();

        await ScrollToAsync(cut.Find(".ex-scroller"), 101 * RowHeightPx);
        Assert.Equal(5, cut.FindAll(".ex-placeholder").Count);
        AssertStripesFollowTheResult(cut);
        var flung = cut.FindAll(".ex-row").Select(r => r.ClassList.Contains("ex-row-stripe")).ToArray();

        Clock.Advance(SettleDelay);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".ex-placeholder")));
        AssertStripesFollowTheResult(cut);
        // The same pattern before and after the rows arrived.
        Assert.Equal(flung, cut.FindAll(".ex-row").Select(r => r.ClassList.Contains("ex-row-stripe")));
    }

    [Fact] // UX-15 / ADR-0038: a position with no row behind it yet is striped like any other
    public void Placeholders_outside_the_window_are_striped_by_their_position()
    {
        // Rows 3-6 are in hand while the Viewport shows 0-4: the top three are gaps.
        var cut = RenderGrid(window: TestRows.Many(4), windowStart: 3);

        var gaps = cut.FindAll(".ex-placeholder");
        Assert.Equal(3, gaps.Count);
        Assert.True(gaps[1].ClassList.Contains("ex-row-stripe"));
        AssertStripesFollowTheResult(cut);
    }

    [Fact] // UX-15 / ADR-0038 / ADR-0015: under a pager the count continues across pages
    public async Task The_count_continues_across_a_pagers_pages()
    {
        // An odd page size, so a page-local count and the result's count disagree on
        // every second page.
        var cut = RenderGrid(total: 100, extra: ps => ps.Add(g => g.PageSize, 25));
        AssertStripesFollowTheResult(cut);

        await cut.FindAll(".ex-pager button")[1].ClickAsync(new MouseEventArgs());

        Assert.Equal(25, PositionOf(cut.FindAll(".ex-row")[0]));
        Assert.True(FirstRowIsStriped(cut));
        AssertStripesFollowTheResult(cut);
    }

    [Fact] // UX-16 / ADR-0038: a group or total row still counts, so the rows around it keep their places
    public void A_row_kind_counts_in_the_parity()
    {
        // "Row 000001" is a total row at an odd position; "Row 000002" a group row at an
        // even one.
        Func<TestRow, RowKind> kindOf = row => row.Book switch
        {
            "Row 000001" => RowKind.Total,
            "Row 000002" => RowKind.Group,
            _ => RowKind.Detail,
        };
        var cut = RenderGrid(extra: ps => ps.Add(g => g.RowKind, kindOf));

        var rows = cut.FindAll(".ex-row");
        Assert.Contains("ex-row-total", rows[1].ClassList);
        Assert.Contains("ex-row-stripe", rows[1].ClassList);
        Assert.Contains("ex-row-group", rows[2].ClassList);
        Assert.DoesNotContain("ex-row-stripe", rows[2].ClassList);
        Assert.Contains("ex-row-stripe", rows[3].ClassList);
        AssertStripesFollowTheResult(cut);
    }

    [Fact] // ADR-0038 / ADR-0030: a Wrapper's cascaded default turns stripes on
    public void A_cascaded_default_turns_stripes_on()
    {
        var cut = RenderGrid(stripes: null, extra: ps => ps.AddCascadingValue(
            new GridPresentationDefaults(10.4, 8.0, 4.95, 14, stripeRows: true)));

        Assert.NotEmpty(cut.FindAll(".ex-row-stripe"));
        AssertStripesFollowTheResult(cut);
    }

    [Fact] // ADR-0038 / ADR-0030: an explicit parameter beats the Wrapper's cascaded default
    public void An_explicit_parameter_beats_the_cascade()
    {
        var cut = RenderGrid(stripes: false, extra: ps => ps.AddCascadingValue(
            new GridPresentationDefaults(10.4, 8.0, 4.95, 14, stripeRows: true)));

        Assert.Empty(cut.FindAll(".ex-row-stripe"));
    }

    [Fact] // ADR-0038 / ADR-0030: a cascade that says nothing about stripes leaves them off
    public void A_cascade_without_stripes_leaves_them_off()
    {
        var cut = RenderGrid(stripes: null, extra: ps => ps.AddCascadingValue(
            new GridPresentationDefaults(10.4, 8.0, 4.95, 14)));

        Assert.Empty(cut.FindAll(".ex-row-stripe"));
    }

    [Fact] // RR-13 / ADR-0038 / ADR-0003: with stripes on, scrolling one row re-renders only the row that entered
    public async Task Scrolling_one_row_with_stripes_re_renders_only_the_entering_row()
    {
        var cut = RenderGrid();
        var before = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.Instance).ToArray();

        await ScrollToAsync(cut.Find(".ex-scroller"), RowHeightPx);

        var after = cut.FindComponents<ExGridRow<TestRow>>();
        Assert.Equal(before.Skip(1), after.Take(4).Select(r => r.Instance));
        Assert.All(after, r => Assert.Equal(1, r.RenderCount));
        AssertStripesFollowTheResult(cut);
    }

    [Fact] // RR-13 / ADR-0038: turning stripes on re-renders the rows that gain one, and no other
    public void Turning_stripes_on_re_renders_only_the_striped_rows()
    {
        var cut = RenderGrid(stripes: false);

        cut.Render(ps => ps.Add(g => g.StripeRows, true));

        foreach (var row in cut.FindComponents<ExGridRow<TestRow>>())
            Assert.Equal(row.Instance.RowIndex % 2 == 1 ? 2 : 1, row.RenderCount);
        AssertStripesFollowTheResult(cut);
    }
}
