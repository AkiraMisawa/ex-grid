using AngleSharp.Dom;
using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// A per-cell appearance reaching the DOM (ADR-0050 item 15; ADR-0063, "What the measurement
/// chose"; DC-58, DC-59): Font and Fill as interned classes in a generated stylesheet, each line
/// drawn inside the cells either side of its edge as their shares of Excel's centred line, a bold
/// cell judged by the bold widths, and rows that repaint only when what they paint changed.
/// </summary>
public class CellAppearanceTests : GridTestContext
{
    private static readonly RgbColour Red = RgbColour.FromRgb(0xFF0000);
    private static readonly RgbColour Blue = RgbColour.FromRgb(0x0000FF);
    private static readonly RgbColour Yellow = RgbColour.FromRgb(0xFFFF00);

    private static GridColumn<TestRow>[] Columns(double amountWidthPx = 120) =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: new ColumnWidthSpec(ColumnWidth.Fixed(120))),
        new("Amount", ColumnType.Number, r => r.Amount, width: new ColumnWidthSpec(ColumnWidth.Fixed(amountWidthPx))),
    ];

    /// <summary>Answers by the row's Book and the column's name; every other cell has none.</summary>
    private static CellAppearanceOf<TestRow> From(params (string Book, string Column, CellAppearance Appearance)[] cells)
        => (row, column) =>
        {
            foreach (var (book, name, appearance) in cells)
            {
                if (row.Book == book && column.Name == name)
                    return appearance;
            }
            return default;
        };

    /// <summary>Answers by the row instance itself, so a test can hand over a new instance with
    /// another appearance and keep the old one's answer.</summary>
    private sealed class ByInstance
    {
        private readonly Dictionary<TestRow, Dictionary<string, CellAppearance>> _cells = new(ReferenceEqualityComparer.Instance);

        public CellAppearanceOf<TestRow> Lookup { get; }

        public ByInstance() => Lookup = (row, column)
            => _cells.TryGetValue(row, out var cells) && cells.TryGetValue(column.Name, out var appearance) ? appearance : default;

        public void Set(TestRow row, string column, CellAppearance appearance)
        {
            if (!_cells.TryGetValue(row, out var cells))
                _cells[row] = cells = [];
            cells[column] = appearance;
        }
    }

    private static IElement Cell(IRenderedComponent<ExGrid<TestRow>> cut, int row, int column)
        => cut.FindAll(".ex-viewport [role=row]")[row].QuerySelectorAll(".ex-cell")[column];

    private static string Css(IRenderedComponent<ExGrid<TestRow>> cut) => cut.Find(".ex-grid > style").TextContent;

    private static int RenderCountOf(IRenderedComponent<ExGrid<TestRow>> cut, TestRow row)
        => cut.FindComponents<ExGridRow<TestRow>>().Single(r => ReferenceEquals(r.Instance.Row, row)).RenderCount;

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        TestRow[] rows, CellAppearanceOf<TestRow>? appearance, EdgeBorderOf? edge = null,
        GridColumn<TestRow>[]? columns = null, CellTextMetrics? metrics = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, rows)
                .Add(g => g.Columns, columns ?? Columns())
                .Add(g => g.CellAppearance, appearance)
                .Add(g => g.EdgeBorder, edge);
            if (metrics is { } m)
                ps.Add(g => g.CellMetrics, m);
        });

    [Fact] // DC-1 / ADR-0050 item 15: without the declaration nothing is painted for it
    public void Without_the_declaration_nothing_changes()
    {
        var cut = RenderGrid(TestRows.Window(), appearance: null);

        Assert.Empty(cut.FindAll(".ex-grid > style"));
        Assert.Empty(cut.FindAll("[class*='ex-font-'], [class*='ex-fill-'], .ex-lined"));
        Assert.All(cut.FindComponents<ExGridRow<TestRow>>(), row => Assert.Null(row.Instance.Appearance));
    }

    [Fact] // DC-1: a declaration that answers nothing paints every cell exactly as without it
    public void A_declaration_that_answers_nothing_paints_the_same_cells()
    {
        var without = RenderGrid(TestRows.Window(), appearance: null);
        var with = RenderGrid(TestRows.Window(), (_, _) => default);

        Assert.Equal(
            without.FindAll(".ex-cell").Select(c => (c.ClassName, c.TextContent)),
            with.FindAll(".ex-cell").Select(c => (c.ClassName, c.TextContent)));
    }

    [Fact] // DC-58: a Font is painted on that cell alone, from one interned class
    public void A_font_is_painted_on_that_cell_alone()
    {
        var font = new CellAppearance { FontColour = Red, Bold = true, Italic = true, Underline = true, Strikethrough = true };
        var cut = RenderGrid(TestRows.Window(), From(("Beta", "Amount", font)));

        Assert.Contains("ex-font-ff0000bius", Cell(cut, 1, 1).ClassList);
        Assert.Contains("ex-cell-numeric", Cell(cut, 1, 1).ClassList);
        Assert.Single(cut.FindAll("[class*='ex-font-']"));
        Assert.Contains(
            ".ex-cell:not(.ex-state-stale, .ex-state-error).ex-font-ff0000bius{color:#ff0000;font-weight:700;font-style:italic;text-decoration-line:underline line-through;}",
            Css(cut));
    }

    [Fact] // ADR-0006: a Stale or Error state is never the one that disappears under a Font
    public void A_font_gives_way_to_a_stale_or_error_state()
    {
        var cut = RenderGrid(TestRows.Window(), From(("Beta", "Amount", new CellAppearance { FontColour = Red })));

        Assert.Contains(":not(.ex-state-stale, .ex-state-error).ex-font-ff0000{", Css(cut));
    }

    [Fact] // DC-58 / ADR-0063: a Fill is painted on that cell alone, and covers the gridlines at its edges
    public void A_fill_is_painted_on_that_cell_and_covers_its_gridlines()
    {
        var cut = RenderGrid(TestRows.Window(), From(("Beta", "Amount", new CellAppearance { Fill = Yellow })));

        Assert.Contains("ex-fill-ffff00", Cell(cut, 1, 1).ClassList);
        Assert.Single(cut.FindAll("[class*='ex-fill-']"));
        Assert.Contains(".ex-cell.ex-fill-ffff00{background-color:#ffff00;--ex-column-rule-color:transparent}", Css(cut));
        // The gridlines above it and left of it are the neighbours' pixels: they cover them.
        Assert.Contains("ex-lb-cover-ffff00", Cell(cut, 0, 1).ClassList);
        Assert.Contains("ex-lr-cover-ffff00", Cell(cut, 1, 0).ClassList);
        Assert.DoesNotContain("ex-lined", Cell(cut, 2, 1).ClassList);
    }

    [Fact] // DC-59: a thin line lies on the gridline, which the upper cell holds
    public void A_thin_line_is_the_upper_cells_share_alone()
    {
        var cut = RenderGrid(TestRows.Window(), From(("Alpha", "Amount", new CellAppearance { Bottom = new Border(BorderStyle.Thin) })));

        Assert.Contains("ex-lined", Cell(cut, 0, 1).ClassList);
        Assert.Contains("ex-lb-thin-000000", Cell(cut, 0, 1).ClassList);
        Assert.DoesNotContain("ex-lined", Cell(cut, 1, 1).ClassList);
        // On the gridline: the cell's own bottom border, one device pixel.
        Assert.Contains(".ex-cell.ex-lb-thin-000000{border-bottom:var(--ex-dp) solid #000000}", Css(cut));
    }

    [Fact] // DC-59: a thick line is centred on its gridline and reaches into both cells
    public void A_thick_line_reaches_into_both_cells()
    {
        var cut = RenderGrid(TestRows.Window(), From(
            ("Alpha", "Amount", new CellAppearance { Bottom = new Border(BorderStyle.Thick) }),
            ("Alpha", "Book", new CellAppearance { Right = new Border(BorderStyle.Thick, Red) })));

        Assert.Contains("ex-lb-thick-000000", Cell(cut, 0, 1).ClassList);
        Assert.Contains("ex-lt-thick-000000", Cell(cut, 1, 1).ClassList);
        Assert.Contains("ex-lr-thick-ff0000", Cell(cut, 0, 0).ClassList);
        Assert.Contains("ex-ll-thick-ff0000", Cell(cut, 0, 1).ClassList);
        // Two pixels on and above the gridline, as the upper cell's border, and one past it as a layer.
        Assert.Contains(".ex-cell.ex-lb-thick-000000{border-bottom:calc(2 * var(--ex-dp)) solid #000000}", Css(cut));
        Assert.Contains(".ex-lt-thick-000000{--ex-line-t:linear-gradient(to bottom,#000000 0 var(--ex-dp),transparent 0);", Css(cut));
        // A right border gives its width back out of the padding, so the text stays where it was.
        Assert.Contains(".ex-cell.ex-lr-thick-ff0000{border-right:calc(2 * var(--ex-dp)) solid #ff0000;padding-right:calc(var(--ex-cell-padding-x, 8px) - calc(2 * var(--ex-dp)))}", Css(cut));
    }

    [Fact] // DC-59: double is a line either side of its gridline, whose own pixel shows the ground
    public void A_double_line_shows_the_ground_on_its_gridline()
    {
        var cut = RenderGrid(TestRows.Window(), From(("Alpha", "Amount", new CellAppearance { Bottom = new Border(BorderStyle.Double) })));

        Assert.Contains("ex-lt-double-000000", Cell(cut, 1, 1).ClassList);
        Assert.Contains(
            "--ex-line-b:linear-gradient(to top,var(--ex-background, Canvas) 0 var(--ex-dp),#000000 0 calc(2 * var(--ex-dp)),transparent 0)",
            Css(cut));
    }

    [Fact] // DC-59 / case 9: a dash pattern is a tile as high as its line, its long dash Excel's
    public void A_dashed_line_is_a_tile_of_its_pattern()
    {
        var cut = RenderGrid(TestRows.Window(), From(("Alpha", "Amount", new CellAppearance { Bottom = new Border(BorderStyle.MediumDashDot) })));

        var css = Css(cut);
        Assert.Contains("--ex-line-b:repeating-linear-gradient(to right,#000000 0px calc(1 * var(--ex-dash) * var(--ex-dp))", css);
        Assert.Contains("--ex-line-b-size:100% calc(2 * var(--ex-dp))", css);
        Assert.DoesNotContain("ex-lt-", Cell(cut, 1, 1).ClassName);
    }

    [Fact] // ADR-0050 item 15: a line recorded by the lower cell alone is drawn on the shared edge
    public void A_line_recorded_by_the_lower_cell_alone_is_drawn()
    {
        var cut = RenderGrid(TestRows.Window(), From(("Beta", "Amount", new CellAppearance { Top = new Border(BorderStyle.Medium, Blue) })));

        Assert.Contains("ex-lb-medium-0000ff", Cell(cut, 0, 1).ClassList);
        Assert.DoesNotContain("ex-lined", Cell(cut, 1, 1).ClassList);
    }

    [Fact] // DC-59 / CONTEXT.md, Border: without an answer the upper or left cell's line is drawn
    public void Two_lines_on_one_edge_draw_the_upper_ones_without_an_answer()
    {
        var cut = RenderGrid(TestRows.Window(), From(
            ("Alpha", "Amount", new CellAppearance { Bottom = new Border(BorderStyle.Thin, Red) }),
            ("Beta", "Amount", new CellAppearance { Top = new Border(BorderStyle.Thick, Blue) })));

        Assert.Contains("ex-lb-thin-ff0000", Cell(cut, 0, 1).ClassList);
        Assert.DoesNotContain("ex-lined", Cell(cut, 1, 1).ClassList);
    }

    [Fact] // DC-59 / ADR-0050 item 15: the line drawn for an edge recorded on both sides is the Consumer's answer
    public void Two_lines_on_one_edge_draw_the_consumers_answer()
    {
        var asked = new List<(Border, Border)>();
        var cut = RenderGrid(TestRows.Window(), From(
                ("Alpha", "Amount", new CellAppearance { Bottom = new Border(BorderStyle.Thin, Red) }),
                ("Beta", "Amount", new CellAppearance { Top = new Border(BorderStyle.Thick, Blue) }),
                ("Gamma", "Book", new CellAppearance { Right = new Border(BorderStyle.Thin) }),
                ("Gamma", "Amount", new CellAppearance { Left = new Border(BorderStyle.Thin) })),
            edge: (upper, lower) =>
            {
                asked.Add((upper, lower));
                return lower;
            });

        Assert.Contains("ex-lb-thick-0000ff", Cell(cut, 0, 1).ClassList);
        Assert.Contains("ex-lt-thick-0000ff", Cell(cut, 1, 1).ClassList);
        // Asked only where the two differ: never for Gamma's two equal sides.
        Assert.NotEmpty(asked);
        Assert.All(asked, pair => Assert.Equal((new Border(BorderStyle.Thin, Red), new Border(BorderStyle.Thick, Blue)), pair));
        Assert.Contains("ex-lr-thin-000000", Cell(cut, 2, 0).ClassList);
    }

    [Fact] // ADR-0063: the stylesheet grows with the lines, never with the combinations of four sides
    public void The_stylesheet_has_one_rule_per_side_style_and_colour()
    {
        var thin = new Border(BorderStyle.Thin);
        var cut = RenderGrid(TestRows.Window(), From(
            ("Alpha", "Book", new CellAppearance { Bottom = thin, Right = thin }),
            ("Alpha", "Amount", new CellAppearance { Bottom = thin }),
            ("Beta", "Book", new CellAppearance { Right = thin, Bottom = thin }),
            ("Beta", "Amount", new CellAppearance { Bottom = thin, Right = thin })));

        var css = Css(cut);
        Assert.Equal(1, Occurrences(css, ".ex-lb-thin-000000{"));
        Assert.Equal(1, Occurrences(css, ".ex-lr-thin-000000{"));
        Assert.Equal(2, css.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);

        static int Occurrences(string text, string of) => text.Split(of).Length - 1;
    }

    [Fact] // DC-58 / ADR-0003: a held lookup and unchanged rows render no row again
    public void A_held_lookup_keeps_row_memoisation()
    {
        var rows = TestRows.Window();
        var columns = Columns();
        var lookup = From(("Beta", "Amount", new CellAppearance { Bold = true, Bottom = new Border(BorderStyle.Thick) }));
        var cut = Render<ExGrid<TestRow>>(ps => ps.Add(g => g.Window, rows).Add(g => g.Columns, columns).Add(g => g.CellAppearance, lookup));

        cut.Render(ps => ps.Add(g => g.Window, rows).Add(g => g.Columns, columns).Add(g => g.CellAppearance, lookup));

        Assert.All(cut.FindComponents<ExGridRow<TestRow>>(), row => Assert.Equal(1, row.RenderCount));
    }

    [Fact] // DC-58: a row whose Values change and whose lines do not leaves its neighbours alone
    public void A_new_instance_with_the_same_lines_repaints_no_neighbour()
    {
        var rows = TestRows.Window();
        var columns = Columns();
        var answers = new ByInstance();
        var replaced = new TestRow { Book = "Beta", Amount = 8m };
        answers.Set(rows[1], "Amount", new CellAppearance { Bottom = new Border(BorderStyle.Thick) });
        answers.Set(replaced, "Amount", new CellAppearance { Bottom = new Border(BorderStyle.Thick), Bold = true });
        var cut = Render<ExGrid<TestRow>>(ps => ps.Add(g => g.Window, rows).Add(g => g.Columns, columns).Add(g => g.CellAppearance, answers.Lookup));

        TestRow[] next = [rows[0], replaced, rows[2]];
        cut.Render(ps => ps.Add(g => g.Window, next).Add(g => g.Columns, columns).Add(g => g.CellAppearance, answers.Lookup));

        Assert.Equal(1, RenderCountOf(cut, rows[0]));
        Assert.Equal(1, RenderCountOf(cut, rows[2]));
        Assert.Contains("ex-font-xb", Cell(cut, 1, 1).ClassList);
    }

    [Fact] // DC-58 / ADR-0063: a line that moves repaints the rows either side of its edge, and no other
    public void A_moved_line_repaints_the_rows_either_side_of_its_edge()
    {
        var rows = TestRows.Many(5);
        var columns = Columns();
        var answers = new ByInstance();
        var replaced = new TestRow { Book = rows[2].Book, Amount = rows[2].Amount };
        answers.Set(replaced, "Amount", new CellAppearance { Top = new Border(BorderStyle.Thick), Bottom = new Border(BorderStyle.Thin) });
        var cut = Render<ExGrid<TestRow>>(ps => ps.Add(g => g.Window, rows).Add(g => g.Columns, columns).Add(g => g.CellAppearance, answers.Lookup));

        TestRow[] next = [rows[0], rows[1], replaced, rows[3], rows[4]];
        cut.Render(ps => ps.Add(g => g.Window, next).Add(g => g.Columns, columns).Add(g => g.CellAppearance, answers.Lookup));

        // The row above holds the thick line's gridline and the pixel above it, so it repaints. The
        // thin line lies on the replaced row's own bottom gridline, so the row below does not.
        Assert.Equal(2, RenderCountOf(cut, rows[1]));
        Assert.Contains("ex-lb-thick-000000", Cell(cut, 1, 1).ClassList);
        Assert.Contains("ex-lt-thick-000000", Cell(cut, 2, 1).ClassList);
        Assert.Contains("ex-lb-thin-000000", Cell(cut, 2, 1).ClassList);
        Assert.Equal(1, RenderCountOf(cut, rows[0]));
        Assert.Equal(1, RenderCountOf(cut, rows[3]));
        Assert.Equal(1, RenderCountOf(cut, rows[4]));
    }

    [Fact] // DC-58 / ADR-0063: a thick bottom line reaches into the row below, which repaints
    public void A_thick_bottom_line_repaints_the_row_below_as_well()
    {
        var rows = TestRows.Many(4);
        var columns = Columns();
        var answers = new ByInstance();
        var replaced = new TestRow { Book = rows[1].Book, Amount = rows[1].Amount };
        answers.Set(replaced, "Amount", new CellAppearance { Bottom = new Border(BorderStyle.Thick) });
        var cut = Render<ExGrid<TestRow>>(ps => ps.Add(g => g.Window, rows).Add(g => g.Columns, columns).Add(g => g.CellAppearance, answers.Lookup));

        TestRow[] next = [rows[0], replaced, rows[2], rows[3]];
        cut.Render(ps => ps.Add(g => g.Window, next).Add(g => g.Columns, columns).Add(g => g.CellAppearance, answers.Lookup));

        Assert.Equal(1, RenderCountOf(cut, rows[0]));
        Assert.Equal(2, RenderCountOf(cut, rows[2]));
        Assert.Equal(1, RenderCountOf(cut, rows[3]));
        Assert.Contains("ex-lt-thick-000000", Cell(cut, 2, 1).ClassList);
    }

    [Fact] // DC-58 / ADR-0006: a new lookup that answers the same repaints no row
    public void A_new_lookup_answering_the_same_repaints_nothing()
    {
        var rows = TestRows.Window();
        var columns = Columns();
        var appearance = new CellAppearance { Fill = Yellow, Bottom = new Border(BorderStyle.Double) };
        var cut = Render<ExGrid<TestRow>>(ps => ps.Add(g => g.Window, rows).Add(g => g.Columns, columns)
            .Add(g => g.CellAppearance, From(("Beta", "Amount", appearance))));

        cut.Render(ps => ps.Add(g => g.Window, rows).Add(g => g.Columns, columns)
            .Add(g => g.CellAppearance, From(("Beta", "Amount", appearance))));

        Assert.All(cut.FindComponents<ExGridRow<TestRow>>(), row => Assert.Equal(1, row.RenderCount));
    }

    [Fact] // DC-58 / ADR-0006: a new lookup repaints only the rows whose appearance it changed
    public void A_new_lookup_repaints_only_the_rows_it_changed()
    {
        var rows = TestRows.Many(4);
        var columns = Columns();
        var cut = Render<ExGrid<TestRow>>(ps => ps.Add(g => g.Window, rows).Add(g => g.Columns, columns)
            .Add(g => g.CellAppearance, From()));

        cut.Render(ps => ps.Add(g => g.Window, rows).Add(g => g.Columns, columns)
            .Add(g => g.CellAppearance, From((rows[2].Book, "Book", new CellAppearance { FontColour = Red }))));

        Assert.Equal(2, RenderCountOf(cut, rows[2]));
        Assert.Equal(1, RenderCountOf(cut, rows[0]));
        Assert.Equal(1, RenderCountOf(cut, rows[1]));
        Assert.Equal(1, RenderCountOf(cut, rows[3]));
    }

    [Fact] // DC-58 / ADR-0016: a bold number that fits at the regular widths and not at the bold ones is ####
    public void A_bold_number_that_fits_only_at_the_regular_widths_is_hashed()
    {
        // Regular digit 9, bold 10, padding 4: "12345" needs 53 regular and 58 bold.
        TestRow[] rows = [new() { Book = "Bold", Amount = 12345m }, new() { Book = "Regular", Amount = 12345m }];
        var cut = RenderGrid(rows, From(("Bold", "Amount", new CellAppearance { Bold = true })),
            columns: Columns(amountWidthPx: 55), metrics: new CellTextMetrics(14, 9, 5, 14, 4, 15, 10, 6));

        Assert.StartsWith("#", Cell(cut, 0, 1).TextContent);
        Assert.Equal("12345", Cell(cut, 0, 1).GetAttribute("aria-label"));
        Assert.Equal("12345", Cell(cut, 1, 1).TextContent);
    }

    [Fact] // DC-58 / ADR-0050 items 11 and 15: a bold cell's painted text is fitted with the bold widths
    public void A_bold_cells_painted_text_is_fitted_with_the_bold_widths()
    {
        var metrics = new CellTextMetrics(14, 9, 5, 14, 4, 15, 10, 6);
        var handed = new Dictionary<string, CellTextMetrics>();
        TestRow[] rows = [new() { Book = "Bold", Amount = 1m }, new() { Book = "Regular", Amount = 1m }];
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.Columns, Columns())
            .Add(g => g.CellMetrics, metrics)
            .Add(g => g.CellAppearance, From(("Bold", "Amount", new CellAppearance { Bold = true })))
            .Add(g => g.PaintedText, (PaintedTextOf<TestRow>)((row, column, _, m) =>
            {
                if (column.Name == "Amount") handed[row.Book] = m;
                return null;
            })));

        Assert.Equal(10, handed["Bold"].DigitWidthPx);
        Assert.Equal(9, handed["Regular"].DigitWidthPx);
    }

    [Fact] // ADR-0050 item 15: a Pinned Column's cell paints its appearance too, and so do the scrollable ones beside it
    public void A_pinned_cell_paints_its_appearance()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, Columns())
            .Add(g => g.PinnedColumnCount, 1)
            .Add(g => g.CellAppearance, From(
                ("Alpha", "Book", new CellAppearance { Fill = Yellow, Right = new Border(BorderStyle.Thick) }))));

        Assert.Contains("ex-fill-ffff00", Cell(cut, 0, 0).ClassList);
        Assert.Contains("ex-pinned", Cell(cut, 0, 0).ClassList);
        Assert.Contains("ex-lr-thick-000000", Cell(cut, 0, 0).ClassList);
        Assert.Contains("ex-ll-thick-000000", Cell(cut, 0, 1).ClassList);
    }

    [Fact] // ADR-0050 item 15: scrolled sideways, each painted cell carries its own appearance, read with its neighbours
    public async Task A_scrolled_column_carries_its_own_appearance()
    {
        var columns = TestRows.Wide(40);
        var target = TestRows.ColumnName(30);
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, columns)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.CellAppearance, From(("Alpha", target, new CellAppearance { Fill = Yellow }))));

        Assert.Empty(cut.FindAll(".ex-fill-ffff00"));
        // Panned a step under a Viewport at a time, so no step is a fling that paints Placeholders.
        for (var left = 300; left <= 2700; left += 300)
            await ScrollToAsync(cut.Find(".ex-scroller"), top: 0, left: left);

        var filled = Assert.Single(cut.FindAll(".ex-fill-ffff00"));
        Assert.EndsWith("/30", filled.TextContent);
        Assert.Single(cut.FindAll(".ex-lr-cover-ffff00"));
    }

    [Fact] // DC-58 / P4: nothing per cell reaches JavaScript — the same calls with the declaration as without
    public void Nothing_per_cell_reaches_javascript()
    {
        var before = JSInterop.Invocations.Count;
        RenderGrid(TestRows.Many(30), appearance: null);
        var without = JSInterop.Invocations.Count - before;

        before = JSInterop.Invocations.Count;
        RenderGrid(TestRows.Many(30), (_, _) => new CellAppearance { Bold = true, Fill = Yellow, Bottom = new Border(BorderStyle.Thick) });
        var with = JSInterop.Invocations.Count - before;

        Assert.Equal(without, with);
    }
}
