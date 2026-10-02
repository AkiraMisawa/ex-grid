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
/// The MudBlazor panel reads a typed date as its column's declared date type, as the built-in panel
/// does (ADR-0023, section of 2026-10-02; ticket 98). A DateOnly column keeps the date picker and
/// applies a DateOnly; a DateTimeOffset column draws a text field, since a calendar's day has no
/// offset; a date of the wrong form is the field's error with Apply unavailable; and an operand of
/// either type reopens as itself.
/// </summary>
public class MudDeclaredDateOperandTests : MudTestContext
{
    private readonly List<FilterSpec?> _applied = [];

    private sealed class CultureScope : IDisposable
    {
        private readonly (CultureInfo Culture, CultureInfo Ui) _previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);

        public CultureScope(string name) =>
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);

        public void Dispose() => (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = _previous;
    }

    private IRenderedComponent<MudPopoverProvider>? _provider;

    private IRenderedComponent<ContainerFragment> RenderPanel(DateType dateType, FilterSpec? current = null, Func<string, string?>? label = null)
    {
        _provider ??= Render<MudPopoverProvider>();
        var context = new FilterPanelContext(
            "AsOf", ColumnType.Date, current, FilterOperators.AllowedFor(ColumnType.Date), FilterUiMode.Condition,
            () => Task.FromResult(DistinctValues.Of([])), spec => _applied.Add(spec), () => { }, () => { }, 1,
            DateType: dateType);
        var chrome = label is null ? MudGridChrome.Default : new MudGridChrome { Label = label };
        return Render(chrome.FilterPanel(context)!);
    }

    // The picker reads its text on change; the text field a DateTimeOffset column draws reads it
    // as it is typed.
    private static Task TypeAsync(IRenderedComponent<ContainerFragment> cut, string text)
    {
        var field = cut.Find(".mud-ex-grid-filter-operand input");
        return cut.FindAll(".mud-picker").Count > 0
            ? field.ChangeAsync(new ChangeEventArgs { Value = text })
            : field.InputAsync(new ChangeEventArgs { Value = text });
    }

    private object? AppliedOperand() => Assert.Single(Assert.Single(_applied)!.Clauses).Value;

    [Theory] // ADR-0023: a day typed into a DateOnly column's picker applies as a DateOnly, read in the culture
    [InlineData("en-US", "05/01/2026", 2026, 5, 1)]
    [InlineData("en-GB", "05/01/2026", 2026, 1, 5)]
    [InlineData("ja-JP", "2026-01-05", 2026, 1, 5)]
    public async Task A_day_typed_on_a_date_only_column_applies_as_a_date_only(string culture, string typed, int year, int month, int day)
    {
        using var _ = new CultureScope(culture);
        var cut = RenderPanel(DateType.DateOnly);

        Assert.NotEmpty(cut.FindAll(".mud-ex-grid-filter-operand.mud-picker"));
        await TypeAsync(cut, typed);
        await cut.Find("form").SubmitAsync();

        Assert.Equal(new DateOnly(year, month, day), AppliedOperand());
    }

    [Theory] // ADR-0023, ISO 8601: a DateTimeOffset column's operand is a text field, and a moment typed with its offset applies with it
    [InlineData("2026-01-05T09:00:00+09:00", 9)]
    [InlineData("2026-01-05T00:00:00Z", 0)]
    [InlineData("2026-01-05 09:00:00 +09:00", 9)]
    public async Task A_moment_typed_with_its_offset_applies(string typed, int offsetHours)
    {
        using var _ = new CultureScope("en-GB");
        var cut = RenderPanel(DateType.DateTimeOffset);

        Assert.Empty(cut.FindAll(".mud-picker"));
        await TypeAsync(cut, typed);
        await cut.Find("form").SubmitAsync();

        var applied = Assert.IsType<DateTimeOffset>(AppliedOperand());
        Assert.Equal(TimeSpan.FromHours(offsetHours), applied.Offset);
        Assert.Equal(new DateTime(2026, 1, 5, offsetHours, 0, 0), applied.DateTime);
    }

    [Theory] // ADR-0023, principle 1: a date not of the declared type is the field's error, and Apply is unavailable
    [InlineData(DateType.DateOnly, "2026-01-05 13:00:00", "has a time")]
    [InlineData(DateType.DateTimeOffset, "2026-01-05 09:00:00", "has no offset")]
    [InlineData(DateType.DateTime, "2026-01-05T09:00:00+09:00", "has an offset")]
    public async Task A_date_not_of_the_declared_type_is_refused_by_name(DateType dateType, string typed, string said)
    {
        using var _ = new CultureScope("en-US");
        var cut = RenderPanel(dateType);

        await TypeAsync(cut, typed);
        await cut.Find("form").SubmitAsync();

        Assert.Contains($"“{typed}” {said}", cut.Find(".mud-ex-grid-filter-operand").TextContent);
        Assert.True(cut.Find(".mud-ex-grid-filter-apply").HasAttribute("disabled"));
        Assert.Empty(_applied);
    }

    [Fact] // ADR-0023: the Chrome's own word for the refusal stands in for the core's English
    public async Task The_chromes_word_for_the_refusal_is_used_where_it_has_one()
    {
        var cut = RenderPanel(DateType.DateOnly,
            label: id => id == MudExGridWords.OperandNotTheColumnsDateForm ? "Nur ein Tag, ohne Uhrzeit." : null);

        await TypeAsync(cut, "2026-01-05 13:00:00");
        await cut.Find("form").SubmitAsync();

        Assert.Contains("Nur ein Tag, ohne Uhrzeit.", cut.Find(".mud-ex-grid-filter-operand").TextContent);
    }

    [Theory] // ADR-0023, principle 1: a DateOnly or DateTimeOffset operand reopens as itself, and Apply keeps it
    [InlineData("en-US")]
    [InlineData("en-GB")]
    [InlineData("ja-JP")]
    public async Task A_declared_date_operand_reopens_and_reads_back_as_itself(string culture)
    {
        using var _ = new CultureScope(culture);
        foreach (var (dateType, operand, shown) in new (DateType, object, string)[]
                 {
                     (DateType.DateOnly, new DateOnly(2026, 1, 5), "2026-01-05"),
                     (DateType.DateTimeOffset, new DateTimeOffset(2026, 1, 5, 9, 5, 7, TimeSpan.FromHours(-5)), "2026-01-05 09:05:07 -05:00"),
                 })
        {
            _applied.Clear();
            var cut = RenderPanel(dateType, new FilterSpec([new FilterClause(FilterOperator.GreaterThan, operand)]));

            Assert.Equal(shown, cut.Find(".mud-ex-grid-filter-operand input").GetAttribute("value"));
            await cut.Find("form").SubmitAsync();

            var reread = AppliedOperand();
            Assert.Equal(operand, reread);
            if (operand is DateTimeOffset moment)
                Assert.Equal(moment.Offset, ((DateTimeOffset)reread!).Offset);
        }
    }
}
