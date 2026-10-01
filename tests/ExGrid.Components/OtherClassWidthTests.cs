using System.Globalization;
using System.Text.RegularExpressions;
using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The other class as the grid reads it (ADR-0016's note of 2026-10-01; ticket 83): a Date or
/// Number cell's letters are charged at the widest glyph measured, so an Auto width holds a month
/// name and a Fixed width that holds it only at a digit's charge paints <c>####</c>. A Text cell's
/// Auto width charges them at the digit, as it always did, because text is cut visibly.
/// </summary>
public class OtherClassWidthTests : GridTestContext
{
    private const string Header = "D";

    private static readonly CellTextMetrics Metrics = ExGrid<TestRow>.DefaultCellMetrics;

    private static readonly Func<object, string> LongDate =
        static v => ((DateTime)v).ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);

    private static readonly TestRow[] Rows = [new() { Book = "September 30, 2026", AsOf = new DateTime(2026, 9, 30) }];

    private static double CellWidth(IRenderedComponent<ExGrid<TestRow>> cut)
    {
        var match = Regex.Match(cut.Find(".ex-cell").GetAttribute("style")!, @"width: ([0-9.]+)px");
        return double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(GridColumn<TestRow> column)
        => Render<ExGrid<TestRow>>(ps => ps.Add(g => g.Window, Rows).Add(g => g.Columns, [column]));

    [Fact] // ADR-0016, principle 1: an Auto Date column holds its month name at the other class's charge, and paints it
    public void An_auto_date_column_is_sized_with_the_other_class()
    {
        var cut = RenderGrid(new GridColumn<TestRow>(Header, ColumnType.Date, r => r.AsOf, format: LongDate));

        Assert.Equal(Metrics.EstimatePx("September 30, 2026"), CellWidth(cut), 9);
        Assert.True(Metrics.EstimatePx("September 30, 2026") > Metrics.For(ColumnType.Text).EstimatePx("September 30, 2026"));
        Assert.Equal("September 30, 2026", cut.Find(".ex-cell").TextContent);
    }

    [Fact] // ADR-0016: an Auto Text column charges the same letters at the digit, as before ticket 83
    public void An_auto_text_column_charges_letters_at_the_digit()
    {
        var cut = RenderGrid(new GridColumn<TestRow>(Header, ColumnType.Text, r => r.Book));

        Assert.Equal(Metrics.For(ColumnType.Text).EstimatePx("September 30, 2026"), CellWidth(cut), 9);
    }

    [Fact] // ADR-0016, principle 1: a date that fits only with its letters charged as digits is ####, not cut
    public void A_date_that_fits_only_at_a_digits_charge_is_hashed()
    {
        var width = Metrics.For(ColumnType.Text).EstimatePx("September 30, 2026");
        var cut = RenderGrid(new GridColumn<TestRow>(Header, ColumnType.Date, r => r.AsOf, format: LongDate,
            width: new ColumnWidthSpec(ColumnWidth.Fixed(width))));

        Assert.Matches("^#+$", cut.Find(".ex-cell").TextContent);
    }
}
