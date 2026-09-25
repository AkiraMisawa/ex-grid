using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests;

public class GridColumnTests
{
    private sealed class Row
    {
        public string Book = "";
    }

    [Fact] // CONTEXT.md "Column": one runtime object — accessor, type, header label, width intent
    public void A_column_carries_accessor_type_header_and_width()
    {
        var width = new ColumnWidthSpec(ColumnWidth.Fixed(120));
        var column = new GridColumn<Row>("Book", ColumnType.Text, r => r.Book, header: "Book name", width: width);

        Assert.Equal("Book", column.Name);
        Assert.Equal(ColumnType.Text, column.Type);
        Assert.Equal("Book name", column.Header);
        Assert.Equal(width, column.Width);
        Assert.Equal("Alpha", column.Value(new Row { Book = "Alpha" }));
    }

    [Fact] // ADR-0016 / FN-12: a Fixed width below MinWidth is refused, not clamped, naming the column
    public void A_fixed_width_below_min_width_is_refused_naming_the_column()
    {
        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => new GridColumn<Row>(
            "Tenor 5Y", ColumnType.Text, r => r.Book,
            width: new ColumnWidthSpec(ColumnWidth.Fixed(10), minWidthPx: 40)));

        Assert.Contains("'Tenor 5Y'", refused.Message);
        Assert.Contains("MinWidth (40)", refused.Message);

        // At MinWidth itself it stands, and above MaxWidth too (the user's width).
        Assert.Equal(40d, new GridColumn<Row>("Book", ColumnType.Text, r => r.Book,
            width: new ColumnWidthSpec(ColumnWidth.Fixed(40), minWidthPx: 40)).Width.Width.FixedPx);
        Assert.Equal(900d, new GridColumn<Row>("Book", ColumnType.Text, r => r.Book,
            width: new ColumnWidthSpec(ColumnWidth.Fixed(900))).Width.Width.FixedPx);
    }

    [Fact] // ADR-0016: a width with no bounds is not a width; the column says which one was given it
    public void A_default_width_spec_is_refused_naming_the_column()
    {
        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => new GridColumn<Row>(
            "Book", ColumnType.Text, r => r.Book, width: default(ColumnWidthSpec)));

        Assert.Contains("'Book'", refused.Message);
    }

    [Fact] // CONTEXT.md "Column": the header label defaults to the name
    public void The_header_defaults_to_the_name()
    {
        var column = new GridColumn<Row>("Book", ColumnType.Text, r => r.Book);

        Assert.Equal("Book", column.Header);
    }

    [Fact] // ADR-0016: an unspecified width means Auto within the default bounds — never default(ColumnWidthSpec)
    public void An_unspecified_width_is_auto_within_the_default_bounds()
    {
        var column = new GridColumn<Row>("Book", ColumnType.Text, r => r.Book);

        Assert.True(column.Width.Width.IsAuto);
        Assert.Equal(ColumnWidthSpec.DefaultMinWidthPx, column.Width.MinWidthPx);
        Assert.Equal(ColumnWidthSpec.DefaultMaxWidthPx, column.Width.MaxWidthPx);
    }

    [Fact] // ADR-0020: the accessor is required — even a Template Column carries one
    public void A_null_accessor_is_refused()
    {
        Assert.Throws<ArgumentNullException>(
            () => new GridColumn<Row>("Book", ColumnType.Text, null!));
    }

    [Fact] // Rather than be quietly wrong: an undefined ColumnType would render as text while the engine throws
    public void An_undefined_column_type_is_refused_at_construction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GridColumn<Row>("Book", (ColumnType)9, r => r.Book));
    }

    [Fact] // Rather than be quietly wrong: a column with no name cannot be addressed by Filter or Sort
    public void A_null_or_empty_name_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() => new GridColumn<Row>(null!, ColumnType.Text, r => r.Book));
        Assert.Throws<ArgumentException>(() => new GridColumn<Row>("", ColumnType.Text, r => r.Book));
    }

    [Fact] // ADR-0023: the Info slice exposes the same name, type and accessor instance the engine reads
    public void The_info_slice_shares_the_column_identity()
    {
        Func<Row, object?> accessor = r => r.Book;
        var column = new GridColumn<Row>("Book", ColumnType.Text, accessor);

        Assert.Equal("Book", column.Info.Name);
        Assert.Equal(ColumnType.Text, column.Info.Type);
        Assert.Same(accessor, column.Info.Value);
        Assert.Same(column.Info.Value, column.Value);
    }
}
