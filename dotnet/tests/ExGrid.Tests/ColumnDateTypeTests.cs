using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// A Date column declares its date type, and the declaration is the law (ADR-0023, section of
/// 2026-10-02; ticket 98).
/// </summary>
public class ColumnDateTypeTests
{
    private sealed record Row(DateOnly Day);

    [Fact] // ADR-0023: a Date column declares its date type, and its Info carries it
    public void A_date_column_carries_its_declared_date_type()
    {
        var column = new GridColumn<Row>("Day", ColumnType.Date, r => r.Day, dateType: DateType.DateOnly);

        Assert.Equal(DateType.DateOnly, column.DateType);
        Assert.Equal(DateType.DateOnly, column.Info.DateType);
    }

    [Fact] // ADR-0023: an undeclared Date column holds DateTime
    public void An_undeclared_date_column_holds_date_time()
        => Assert.Equal(DateType.DateTime, new GridColumn<Row>("Day", ColumnType.Date, r => r.Day).DateType);

    [Fact] // ADR-0023: a date type declared on a column that is not Date is refused, naming the column
    public void A_date_type_on_a_column_that_is_not_date_is_refused_naming_the_column()
    {
        var refused = Assert.Throws<ArgumentException>(
            () => new GridColumn<Row>("Book", ColumnType.Text, r => r.Day, dateType: DateType.DateOnly));

        Assert.Contains("Book", refused.Message);
    }

    [Fact] // ADR-0023: an undefined date type is refused, naming the column
    public void An_undefined_date_type_is_refused_naming_the_column()
    {
        var refused = Assert.Throws<ArgumentOutOfRangeException>(
            () => new GridColumn<Row>("Day", ColumnType.Date, r => r.Day, dateType: (DateType)7));

        Assert.Contains("Day", refused.Message);
    }

    [Fact] // ADR-0023: a slice a Source author builds directly is held to the same rule
    public void A_column_info_declaring_a_date_type_on_a_column_that_is_not_date_is_refused()
    {
        var refused = Assert.Throws<ArgumentException>(
            () => new ColumnInfo<Row>("Book", ColumnType.Text, r => r.Day, DateType: DateType.DateOnly));

        Assert.Contains("Book", refused.Message);
        Assert.Equal(DateType.DateOnly, new ColumnInfo<Row>("Day", ColumnType.Date, r => r.Day, DateType: DateType.DateOnly).DateType);
    }
}
