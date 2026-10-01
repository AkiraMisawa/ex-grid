using System.Globalization;
using Bunit;
using ExGrid.Chrome;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The built-in filter panel's number operand (ADR-0023; ADR-0006's note of 2026-10-01; ticket 96;
/// principle 1): it reopens in a text that reads back as itself, a typed number reads in the
/// culture the form shows numbers in, and a text that reads two ways is refused by name, with
/// nothing applied.
/// </summary>
public class NumberOperandTests : GridTestContext
{
    public static TheoryData<string> Cultures() => ["en-US", "de-DE", "fr-FR", "ja-JP"];

    private sealed class CultureScope : IDisposable
    {
        private readonly (CultureInfo Culture, CultureInfo Ui) _previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);

        public CultureScope(string name) =>
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);

        public void Dispose() => (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = _previous;
    }

    private static TestSource SourceFiltered(decimal? greaterThan)
    {
        var source = new TestSource();
        source.Push(TestRows.Window(), totalCount: 3);
        if (greaterThan is { } operand)
        {
            source.OnFilterChanged(new GridFilter(new Dictionary<string, FilterSpec>
            {
                ["Amount"] = new([new FilterClause(FilterOperator.GreaterThan, operand)]),
            }));
        }
        return source;
    }

    private async Task<IRenderedComponent<ExGrid<TestRow>>> OpenPanelAsync(TestSource source)
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, [new GridColumn<TestRow>("Amount", ColumnType.Number, r => r.Amount,
                width: new ColumnWidthSpec(ColumnWidth.Fixed(120)))])
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 400));
        await cut.Find(".ex-menu-button").ClickAsync(new MouseEventArgs());
        return cut;
    }

    private static Task PressOkAsync(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.FindAll(".ex-popover-actions button").Single(b => b.TextContent == "OK").ClickAsync(new MouseEventArgs());

    [Theory, MemberData(nameof(Cultures))] // ADR-0023, principle 1: 1234.5 reopens in the culture's text, and OK keeps 1234.5 — under de-DE it became 12345
    public async Task A_number_operand_reopens_and_reads_back_as_itself(string culture)
    {
        using var _ = new CultureScope(culture);
        var source = SourceFiltered(1234.5m);
        var cut = await OpenPanelAsync(source);

        Assert.Equal(1234.5m.ToString(CultureInfo.CurrentCulture), cut.Find(".ex-popover input:not([type])").GetAttribute("value"));
        Assert.Empty(cut.FindAll(".ex-popover-refusal"));
        await PressOkAsync(cut);

        var clause = Assert.Single(source.FilterChanges[^1]!.Columns["Amount"].Clauses);
        Assert.Equal(1234.5m, clause.Value);
    }

    [Theory] // ADR-0023, principle 1: a typed number reads as the culture writes it
    [InlineData("en-US", "1,234.5")]
    [InlineData("de-DE", "1.234,5")]
    [InlineData("fr-FR", "1 234,5")]
    [InlineData("ja-JP", "1,234.5")]
    public async Task A_typed_number_reads_as_the_culture_writes_it(string culture, string typed)
    {
        using var _ = new CultureScope(culture);
        var source = SourceFiltered(null);
        var cut = await OpenPanelAsync(source);

        await cut.Find(".ex-popover input:not([type])").InputAsync(new ChangeEventArgs { Value = typed });
        await PressOkAsync(cut);

        Assert.Equal(1234.5m, Assert.Single(source.FilterChanges[^1]!.Columns["Amount"].Clauses).Value);
    }

    [Theory] // ADR-0023, principle 1: a text that reads two ways, or in another culture's separators, is refused by name and applies nothing
    [InlineData("de-DE", "1.234", "reads two ways")]
    [InlineData("de-DE", "1234.5", "is not a value")]
    [InlineData("en-US", "1234,5", "is not a value")]
    [InlineData("fr-FR", "1.234", "is not a value")]
    [InlineData("ja-JP", "1234,5", "is not a value")]
    public async Task A_text_that_does_not_read_one_way_is_refused_by_name(string culture, string typed, string said)
    {
        using var _ = new CultureScope(culture);
        var source = SourceFiltered(null);
        var cut = await OpenPanelAsync(source);
        var before = source.FilterChanges.Count;

        await cut.Find(".ex-popover input:not([type])").InputAsync(new ChangeEventArgs { Value = typed });
        var refusal = cut.Find(".ex-popover-refusal");
        await PressOkAsync(cut);

        Assert.Equal("alert", refusal.GetAttribute("role"));
        Assert.Contains($"“{typed}” {said}", refusal.TextContent);
        Assert.Equal(before, source.FilterChanges.Count);
        Assert.NotEmpty(cut.FindAll(".ex-popover"));
    }
}
