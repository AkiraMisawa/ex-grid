using System.Globalization;
using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The built-in filter panel's typed date (ADR-0023's note of 2026-10-01; ticket 97; principle 1):
/// read in the form's culture alone, never invariant first, and refused by name, with nothing
/// applied, where it reads two ways or not at all.
/// </summary>
public class DateOperandTests : GridTestContext
{
    private sealed class CultureScope : IDisposable
    {
        private readonly (CultureInfo Culture, CultureInfo Ui) _previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);

        public CultureScope(string name) =>
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);

        public void Dispose() => (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = _previous;
    }

    private static TestSource Source()
    {
        var source = new TestSource();
        source.Push(TestRows.Window(), totalCount: 3);
        return source;
    }

    private async Task<IRenderedComponent<ExGrid<TestRow>>> OpenPanelAsync(TestSource source)
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, [new GridColumn<TestRow>("AsOf", ColumnType.Date, r => r.AsOf,
                width: new ColumnWidthSpec(ColumnWidth.Fixed(160)))])
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 400));
        await cut.Find(".ex-menu-button").ClickAsync(new MouseEventArgs());
        return cut;
    }

    private static Task PressOkAsync(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.FindAll(".ex-popover-actions button").Single(b => b.TextContent == "OK").ClickAsync(new MouseEventArgs());

    [Theory] // ADR-0023, principle 1: a typed date reads in the culture alone — 05/01/2026 is 5 January under en-GB and 1 May under en-US
    [InlineData("en-US", "05/01/2026", 2026, 5, 1)]
    [InlineData("en-GB", "05/01/2026", 2026, 1, 5)]
    [InlineData("en-GB", "05/01/26", 2026, 1, 5)]
    [InlineData("de-DE", "05.01.2026", 2026, 1, 5)]
    [InlineData("ja-JP", "2026/01/05", 2026, 1, 5)]
    public async Task A_typed_date_reads_in_the_culture(string culture, string typed, int year, int month, int day)
    {
        using var _ = new CultureScope(culture);
        var source = Source();
        var cut = await OpenPanelAsync(source);

        await cut.Find(".ex-popover input:not([type])").InputAsync(new ChangeEventArgs { Value = typed });
        Assert.Empty(cut.FindAll(".ex-popover-refusal"));
        await PressOkAsync(cut);

        Assert.Equal(new DateTime(year, month, day), Assert.Single(source.FilterChanges[^1]!.Columns["AsOf"].Clauses).Value);
    }

    [Fact] // ADR-0023, principle 1: a time with the date is read with it
    public async Task A_time_with_the_date_is_read_with_it()
    {
        using var _ = new CultureScope("en-GB");
        var source = Source();
        var cut = await OpenPanelAsync(source);

        await cut.Find(".ex-popover input:not([type])").InputAsync(new ChangeEventArgs { Value = "05/01/2026 21:05" });
        await PressOkAsync(cut);

        Assert.Equal(new DateTime(2026, 1, 5, 21, 5, 0), Assert.Single(source.FilterChanges[^1]!.Columns["AsOf"].Clauses).Value);
    }

    [Theory] // ADR-0023, principle 1: a date that reads two ways, or that the culture does not read, is refused by name and applies nothing
    [InlineData("ja-JP", "05/01/2026", "reads two ways")]
    [InlineData("en-US", "13/01/2026", "is not a value")]
    [InlineData("en-GB", "01/13/2026", "is not a value")]
    [InlineData("de-DE", "01/13/2026", "is not a value")]
    public async Task A_date_that_does_not_read_one_way_is_refused_by_name(string culture, string typed, string said)
    {
        using var _ = new CultureScope(culture);
        var source = Source();
        var cut = await OpenPanelAsync(source);
        var before = source.FilterChanges.Count;

        await cut.Find(".ex-popover input:not([type])").InputAsync(new ChangeEventArgs { Value = typed });
        var refusal = cut.Find(".ex-popover-refusal");
        await PressOkAsync(cut);

        Assert.Equal("alert", refusal.GetAttribute("role"));
        Assert.Contains($"“{typed}” {said}", refusal.TextContent);
        Assert.Equal(before, source.FilterChanges.Count);
    }
}
