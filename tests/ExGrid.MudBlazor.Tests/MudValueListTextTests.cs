using System.Globalization;
using Bunit;
using Bunit.Rendering;
using ExGrid.Chrome;
using MudBlazor;
using Xunit;

namespace ExGrid.MudBlazor.Tests;

/// <summary>
/// The MudBlazor value list shows each value as the cells show it (ADR-0006, note of 2026-10-01):
/// an unformatted date in its ISO form, whatever the culture, as the built-in panel lists it since
/// ticket 94. It wrote the current culture's text before ticket 96.
/// </summary>
public class MudValueListTextTests : MudTestContext
{
    public static TheoryData<string> Cultures() => ["en-US", "de-DE", "fr-FR", "ja-JP"];

    [Theory, MemberData(nameof(Cultures))] // ADR-0006 (2026-10-01): an unformatted date is listed in its ISO form, the same under every culture
    public void An_unformatted_date_is_listed_in_its_iso_form(string culture)
    {
        var previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            Render<MudPopoverProvider>();
            var context = new FilterPanelContext(
                "When", ColumnType.Date, null, FilterOperators.AllowedFor(ColumnType.Date), FilterUiMode.ValueList,
                () => Task.FromResult(DistinctValues.Of([new DateTime(2026, 1, 5, 9, 5, 7), null])), _ => { }, () => { }, () => { }, 1);
            var cut = Render(MudGridChrome.Default.FilterPanel(context)!);

            var labels = cut.FindAll(".mud-ex-grid-filter-value").Select(v => v.TextContent.Trim()).ToArray();
            Assert.Equal(["2026-01-05 09:05:07", "(Blanks)"], labels);
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = previous;
        }
    }
}
