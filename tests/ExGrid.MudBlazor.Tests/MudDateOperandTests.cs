using System.Globalization;
using Bunit;
using Bunit.Rendering;
using ExGrid.Chrome;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Xunit;
using FilterOperator = ExGrid.FilterOperator;

namespace ExGrid.MudBlazor.Tests;

/// <summary>
/// The MudBlazor panel's date picker reads typed text as the built-in panel does (ADR-0023's note of
/// 2026-10-01; ticket 97; principle 1): through FilterPanelChoices, in the current culture alone.
/// MudBlazor's own reading was the invariant culture's, under which <c>05/01/2026</c> is 1 May
/// whatever the user's culture. A date reopens in its ISO form, and a refused text is the field's
/// error with Apply unavailable.
/// </summary>
public class MudDateOperandTests : MudTestContext
{
    private readonly List<FilterSpec?> _applied = [];

    public static TheoryData<string> Cultures() => ["en-US", "en-GB", "de-DE", "ja-JP"];

    private sealed class CultureScope : IDisposable
    {
        private readonly (CultureInfo Culture, CultureInfo Ui) _previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);

        public CultureScope(string name) =>
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);

        public void Dispose() => (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = _previous;
    }

    private IRenderedComponent<MudPopoverProvider>? _provider;

    private IRenderedComponent<ContainerFragment> RenderPanel(FilterSpec? current)
    {
        _provider ??= Render<MudPopoverProvider>();
        var context = new FilterPanelContext(
            "AsOf", ColumnType.Date, current, FilterOperators.AllowedFor(ColumnType.Date), FilterUiMode.Condition,
            () => Task.FromResult(DistinctValues.Of([])), spec => _applied.Add(spec), () => { }, () => { }, 1);
        return Render(MudGridChrome.Default.FilterPanel(context)!);
    }

    private static Task TypeAsync(IRenderedComponent<ContainerFragment> cut, string text)
        => cut.Find(".mud-ex-grid-filter-operand input").ChangeAsync(new ChangeEventArgs { Value = text });

    [Theory, MemberData(nameof(Cultures))] // ADR-0023 / ADR-0006: a date operand reopens in its ISO form, and Apply keeps it
    public async Task A_date_operand_reopens_in_iso_form_and_reads_back_as_itself(string culture)
    {
        using var _ = new CultureScope(culture);
        var date = new DateTime(2026, 1, 5);
        var cut = RenderPanel(new FilterSpec([new FilterClause(FilterOperator.GreaterThan, date)]));

        // The picker holds a day, so it shows the day's ISO form.
        Assert.Equal("2026-01-05", cut.Find(".mud-ex-grid-filter-operand input").GetAttribute("value"));
        await cut.Find("form").SubmitAsync();

        Assert.Equal(date, Assert.Single(Assert.Single(_applied)!.Clauses).Value);
    }

    [Theory] // ADR-0023, principle 1: a typed date reads in the culture alone — 05/01/2026 is 5 January under en-GB, 1 May under en-US
    [InlineData("en-US", "05/01/2026", 2026, 5, 1)]
    [InlineData("en-GB", "05/01/2026", 2026, 1, 5)]
    [InlineData("en-GB", "05/01/26", 2026, 1, 5)]
    [InlineData("de-DE", "05.01.2026", 2026, 1, 5)]
    [InlineData("ja-JP", "2026/01/05", 2026, 1, 5)]
    public async Task A_typed_date_reads_in_the_culture(string culture, string typed, int year, int month, int day)
    {
        using var _ = new CultureScope(culture);
        var cut = RenderPanel(null);

        await TypeAsync(cut, typed);
        await cut.Find("form").SubmitAsync();

        Assert.Equal(new DateTime(year, month, day), Assert.Single(Assert.Single(_applied)!.Clauses).Value);
    }

    [Theory] // ADR-0023, principle 1: a date that reads two ways, or that the culture does not read, is the field's error, and Apply is unavailable
    [InlineData("ja-JP", "05/01/2026", "reads two ways")]
    [InlineData("en-US", "13/01/2026", "is not a value")]
    [InlineData("en-GB", "01/13/2026", "is not a value")]
    public async Task A_date_that_does_not_read_one_way_is_refused_by_name(string culture, string typed, string said)
    {
        using var _ = new CultureScope(culture);
        var cut = RenderPanel(null);

        await TypeAsync(cut, typed);
        await cut.Find("form").SubmitAsync();

        Assert.Contains($"“{typed}” {said}", cut.Find(".mud-ex-grid-filter-operand").TextContent);
        Assert.True(cut.Find(".mud-ex-grid-filter-apply").HasAttribute("disabled"));
        Assert.Empty(_applied);
    }

    /// <summary>The time MudBlazor reads, moved only by the test.</summary>
    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    [Theory] // ADR-0023, principle 1: a refused date stays the field's error through later renders of the panel
    [InlineData("ja-JP", "05/01/2026", "reads two ways")]
    [InlineData("en-GB", "01/13/2026", "is not a value")]
    public async Task A_refused_date_stays_refused_when_the_panel_renders_again_later(string culture, string typed, string said)
    {
        // MudDatePicker ignores a date set again within 100 ms of the last. The panel once bound
        // the picker's date, which every render of the panel sets, and a render later than that
        // cleared the text and wrote the field's Error back to false: the refusal vanished while
        // Apply stayed unavailable. A slow CI runner put the panel's own first render that late.
        using var _ = new CultureScope(culture);
        var clock = new ManualClock();
        Services.AddSingleton<TimeProvider>(clock);
        var cut = RenderPanel(null);
        clock.Advance(TimeSpan.FromMilliseconds(200));

        await TypeAsync(cut, typed);
        clock.Advance(TimeSpan.FromMilliseconds(200));
        await cut.Find("form").SubmitAsync();
        clock.Advance(TimeSpan.FromMilliseconds(200));
        await cut.Find("form").SubmitAsync();

        var field = cut.Find(".mud-ex-grid-filter-operand");
        Assert.Contains($"\u201C{typed}\u201D {said}", field.TextContent);
        Assert.Equal(typed, cut.Find(".mud-ex-grid-filter-operand input").GetAttribute("value"));
        Assert.True(cut.Find(".mud-ex-grid-filter-apply").HasAttribute("disabled"));
        Assert.Empty(_applied);
    }
}
