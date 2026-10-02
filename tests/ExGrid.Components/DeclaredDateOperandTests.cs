using System.Globalization;
using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The built-in filter panel reads a typed date as its column's declared date type (ADR-0023, section
/// of 2026-10-02; ticket 98): a condition typed on a DateOnly or DateTimeOffset column applies, a
/// date of the wrong form is refused by name with nothing applied, and an operand reopens in a text
/// that reads back as itself. Before, every typed date read as a DateTime and threw when applied.
/// </summary>
public class DeclaredDateOperandTests : GridTestContext
{
    private static readonly ColumnWidthSpec Wide = new(ColumnWidth.Fixed(280));

    private static readonly GridColumn<TestRow>[] Columns =
    [
        new("Day", ColumnType.Date, r => DateOnly.FromDateTime(r.AsOf), width: Wide, dateType: DateType.DateOnly),
        new("At", ColumnType.Date, r => new DateTimeOffset(r.AsOf, TimeSpan.FromHours(9)), width: Wide, dateType: DateType.DateTimeOffset),
    ];

    public static TheoryData<string> Cultures() => ["en-US", "en-GB", "ja-JP"];

    private sealed class CultureScope : IDisposable
    {
        private readonly (CultureInfo Culture, CultureInfo Ui) _previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);

        public CultureScope(string name) =>
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);

        public void Dispose() => (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = _previous;
    }

    private static TestSource Source(string? column = null, object? operand = null)
    {
        var source = new TestSource();
        source.Push(TestRows.Window(), totalCount: 3);
        if (column is not null)
        {
            source.OnFilterChanged(new GridFilter(new Dictionary<string, FilterSpec>
            {
                [column] = new([new FilterClause(FilterOperator.Equals, operand)]),
            }));
        }
        return source;
    }

    private async Task<IRenderedComponent<ExGrid<TestRow>>> OpenPanelAsync(TestSource source, int column)
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns)
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 800));
        await cut.FindAll(".ex-menu-button")[column].ClickAsync(new MouseEventArgs());
        return cut;
    }

    private static Task TypeAsync(IRenderedComponent<ExGrid<TestRow>> cut, string text)
        => cut.Find(".ex-popover input:not([type])").InputAsync(new ChangeEventArgs { Value = text });

    private static Task PressOkAsync(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.FindAll(".ex-popover-actions button").Single(b => b.TextContent == "OK").ClickAsync(new MouseEventArgs());

    [Theory] // ADR-0023: a day typed on a DateOnly column applies as a DateOnly, read in the culture
    [InlineData("en-US", "05/01/2026", 2026, 5, 1)]
    [InlineData("en-GB", "05/01/2026", 2026, 1, 5)]
    [InlineData("ja-JP", "2026-01-05", 2026, 1, 5)]
    public async Task A_day_typed_on_a_date_only_column_applies_as_a_date_only(string culture, string typed, int year, int month, int day)
    {
        using var _ = new CultureScope(culture);
        var source = Source();
        var cut = await OpenPanelAsync(source, 0);

        await TypeAsync(cut, typed);
        Assert.Empty(cut.FindAll(".ex-popover-refusal"));
        await PressOkAsync(cut);

        Assert.Equal(new DateOnly(year, month, day), Assert.Single(source.FilterChanges[^1]!.Columns["Day"].Clauses).Value);
    }

    [Theory] // ADR-0023, ISO 8601: a moment typed with its offset on a DateTimeOffset column applies with that offset
    [InlineData("2026-01-05T09:00:00+09:00", 9)]
    [InlineData("2026-01-05T00:00:00Z", 0)]
    [InlineData("2026-01-05 09:00:00 +09:00", 9)]
    public async Task A_moment_typed_with_its_offset_applies(string typed, int offsetHours)
    {
        using var _ = new CultureScope("en-GB");
        var source = Source();
        var cut = await OpenPanelAsync(source, 1);

        await TypeAsync(cut, typed);
        await PressOkAsync(cut);

        var applied = Assert.IsType<DateTimeOffset>(Assert.Single(source.FilterChanges[^1]!.Columns["At"].Clauses).Value);
        Assert.Equal(TimeSpan.FromHours(offsetHours), applied.Offset);
        Assert.Equal(new DateTime(2026, 1, 5, offsetHours, 0, 0), applied.DateTime);
    }

    [Theory] // ADR-0023, principle 1: a date not of the column's declared type is refused by name, and applies nothing
    [InlineData(0, "2026-01-05 13:00:00", "has a time")]
    [InlineData(1, "2026-01-05 09:00:00", "has no offset")]
    [InlineData(1, "2026-01-05", "has no offset")]
    public async Task A_date_not_of_the_declared_type_is_refused_by_name(int column, string typed, string said)
    {
        using var _ = new CultureScope("en-US");
        var source = Source();
        var cut = await OpenPanelAsync(source, column);
        var before = source.FilterChanges.Count;

        await TypeAsync(cut, typed);
        var refusal = cut.Find(".ex-popover-refusal");
        await PressOkAsync(cut);

        Assert.Contains($"“{typed}” {said}", refusal.TextContent);
        Assert.Equal(before, source.FilterChanges.Count);
    }

    [Theory, MemberData(nameof(Cultures))] // ADR-0023, principle 1: a DateOnly or DateTimeOffset operand reopens as its ISO text, and OK keeps it
    public async Task A_declared_date_operand_reopens_and_reads_back_as_itself(string culture)
    {
        using var _ = new CultureScope(culture);
        foreach (var (column, name, operand, shown) in new (int, string, object, string)[]
                 {
                     (0, "Day", new DateOnly(2026, 1, 5), "2026-01-05"),
                     (1, "At", new DateTimeOffset(2026, 1, 5, 9, 5, 7, TimeSpan.FromHours(-5)), "2026-01-05 09:05:07 -05:00"),
                 })
        {
            var source = Source(name, operand);
            var cut = await OpenPanelAsync(source, column);

            Assert.Equal(shown, cut.Find(".ex-popover input:not([type])").GetAttribute("value"));
            await PressOkAsync(cut);

            var reread = Assert.Single(source.FilterChanges[^1]!.Columns[name].Clauses).Value;
            Assert.Equal(operand, reread);
            if (operand is DateTimeOffset moment)
                Assert.Equal(moment.Offset, ((DateTimeOffset)reread!).Offset);
        }
    }
}
