using System.Globalization;
using Bunit;
using Bunit.Rendering;
using ExGrid.Chrome;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Xunit;
using FilterOperator = ExGrid.FilterOperator;

namespace ExGrid.MudBlazor.Tests;

/// <summary>
/// The MudBlazor panel's number operand reads as the built-in panel's does (ADR-0023; ADR-0006's
/// note of 2026-10-01; ticket 96; principle 1): typed as text in the culture the field shows
/// numbers in, read by FilterPanelChoices, reopened in a text that reads back as itself, and a text
/// that reads two ways refused by name with Apply unavailable.
/// </summary>
public class MudNumberOperandTests : MudTestContext
{
    private readonly List<FilterSpec?> _applied = [];

    public static TheoryData<string> Cultures() => ["en-US", "de-DE", "fr-FR", "ja-JP"];

    private sealed class CultureScope : IDisposable
    {
        private readonly (CultureInfo Culture, CultureInfo Ui) _previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);

        public CultureScope(string name) =>
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);

        public void Dispose() => (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = _previous;
    }

    private IRenderedComponent<MudPopoverProvider>? _provider;

    private IRenderedComponent<ContainerFragment> RenderPanel(FilterSpec? current, MudGridChrome? chrome = null)
    {
        _provider ??= Render<MudPopoverProvider>();
        var context = new FilterPanelContext(
            "Amount", ColumnType.Number, current, FilterOperators.AllowedFor(ColumnType.Number), FilterUiMode.Condition,
            () => Task.FromResult(DistinctValues.Of([])), spec => _applied.Add(spec), () => { }, () => { }, 1);
        return Render((chrome ?? MudGridChrome.Default).FilterPanel(context)!);
    }

    private static FilterSpec GreaterThan(decimal operand) => new([new FilterClause(FilterOperator.GreaterThan, operand)]);

    [Theory, MemberData(nameof(Cultures))] // ADR-0023, principle 1: 1234.5 reopens in the culture's text, and Apply keeps 1234.5
    public async Task A_number_operand_reopens_and_reads_back_as_itself(string culture)
    {
        using var _ = new CultureScope(culture);
        var cut = RenderPanel(GreaterThan(1234.5m));

        Assert.Equal(1234.5m.ToString(CultureInfo.CurrentCulture), cut.Find(".mud-ex-grid-filter-operand input").GetAttribute("value"));
        await cut.Find("form").SubmitAsync();

        Assert.Equal(1234.5m, Assert.Single(Assert.Single(_applied)!.Clauses).Value);
    }

    [Theory] // ADR-0023, principle 1: a typed number reads as the built-in panel reads it, in the culture's separators
    [InlineData("en-US", "1,234.5")]
    [InlineData("de-DE", "1.234,5")]
    [InlineData("fr-FR", "1 234,5")]
    [InlineData("ja-JP", "1,234.5")]
    public async Task A_typed_number_reads_as_the_culture_writes_it(string culture, string typed)
    {
        using var _ = new CultureScope(culture);
        var cut = RenderPanel(null);

        await cut.Find(".mud-ex-grid-filter-operand input").InputAsync(new ChangeEventArgs { Value = typed });
        await cut.Find("form").SubmitAsync();

        Assert.Equal(1234.5m, Assert.Single(Assert.Single(_applied)!.Clauses).Value);
    }

    [Theory] // ADR-0023, principle 1: a text that reads two ways, or in another culture's separators, is refused by name, and Apply is unavailable
    [InlineData("de-DE", "1.234", "reads two ways")]
    [InlineData("de-DE", "1234.5", "is not a value")]
    [InlineData("en-US", "1234,5", "is not a value")]
    [InlineData("fr-FR", "1.234", "is not a value")]
    [InlineData("ja-JP", "1234,5", "is not a value")]
    public async Task A_text_that_does_not_read_one_way_is_refused_by_name(string culture, string typed, string said)
    {
        using var _ = new CultureScope(culture);
        var cut = RenderPanel(null);

        await cut.Find(".mud-ex-grid-filter-operand input").InputAsync(new ChangeEventArgs { Value = typed });
        await cut.Find("form").SubmitAsync();

        Assert.Contains($"“{typed}” {said}", cut.Find(".mud-ex-grid-filter-operand").TextContent);
        Assert.True(cut.Find(".mud-ex-grid-filter-apply").HasAttribute("disabled"));
        Assert.Empty(_applied);
    }

    [Fact] // ADR-0030 / WR-3: the Chrome's own word for a refusal stands in place of the core's English
    public async Task The_chromes_own_word_for_a_refusal_stands()
    {
        using var _ = new CultureScope("de-DE");
        var chrome = new MudGridChrome
        {
            Label = id => id == MudExGridWords.OperandReadsTwoWays ? "Zweideutig: ohne Punkt eingeben." : null,
        };
        var cut = RenderPanel(null, chrome);

        await cut.Find(".mud-ex-grid-filter-operand input").InputAsync(new ChangeEventArgs { Value = "1.234" });

        Assert.Contains("Zweideutig: ohne Punkt eingeben.", cut.Find(".mud-ex-grid-filter-operand").TextContent);
    }
}
