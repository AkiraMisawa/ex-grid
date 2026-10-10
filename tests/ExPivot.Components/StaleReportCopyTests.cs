using System.Globalization;
using System.Text.RegularExpressions;
using Bunit;
using ExGrid.Selection;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests;

/// <summary>
/// Copy from a Stale Report is refused (ADR-0067 and ADR-0152, decided with the user on 2026-10-09).
/// A Stale Report shows the data as of its time, and says so beside it; pasted elsewhere, the "as
/// of" is lost and the numbers look current. So while the report is stale, Copy is refused, in a
/// word of ExPivot's table that says as of when the report's data is and to Retry first — its
/// Japanese bundled, replaceable by id — and nothing reaches the clipboard. The Selection Summary
/// still answers: it stands on screen beside the notice. Once Retry brings the newest, Copy copies
/// again. The clock is the test's.
/// </summary>
public sealed class StaleReportCopyTests : PivotTestContext
{
    private const string CopyStaleReport = "copy-stale-report";

    private static readonly PivotLayout RegionAmount = new() { Rows = [P("Region")], Values = [Sum("Amount")] };

    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    // A Latin letter in a sentence the Japanese words say is an English word left.
    private static readonly Regex Latin = new(@"[A-Za-z]+", RegexOptions.CultureInvariant);

    private static Sale[] EastApples(decimal amount) => [Sales[0] with { Amount = amount }, .. Sales[1..]];

    public StaleReportCopyTests() => Clock.SetUtcNow(new DateTimeOffset(2026, 10, 9, 9, 30, 0, TimeSpan.Zero));

    /// <summary>The time the report's data is as of, as the notice writes it: today's, in en-US.</summary>
    private string TimeOf(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, Clock.LocalTimeZone).ToString("T", English);

    /// <summary>A pivot over a source that fails once its data moves on: the report is left stale,
    /// showing the data as of the time it was first shown.</summary>
    private async Task<(IRenderedComponent<PivotComponent> Cut, DateTimeOffset ShownAt)> RenderStaleAsync(LiveSource source,
        Action<ComponentParameterCollectionBuilder<PivotComponent>>? parameters = null)
    {
        var cut = RenderPivot(RegionAmount, parameters, source: source);
        var shownAt = Clock.GetUtcNow();
        Clock.Advance(TimeSpan.FromSeconds(5));
        source.Fails = new InvalidOperationException("The server is unreachable.");
        await cut.InvokeAsync(() => source.Publish(EastApples(101)));
        Assert.True(cut.Instance.IsStale);
        return (cut, shownAt);
    }

    /// <summary>Selects the first three values — East, North and West — as the user does.</summary>
    private static Task SelectThreeValuesAsync(IRenderedComponent<PivotComponent> cut)
    {
        var grid = Grid(cut).Instance;
        return cut.InvokeAsync(() => grid.PlaceSelectionAsync(new SelectionRange(0, 1, 3, 1), new CellPosition(0, 1), grid.RowSequenceVersion));
    }

    private static Task<ExGrid.Clipboard.ClipboardPayload?> CopyAsync(IRenderedComponent<PivotComponent> cut)
        => cut.InvokeAsync(() => Grid(cut).Instance.BuildCopyPayloadAsync());

    /// <summary>What the report grid last said in its live region.</summary>
    private static string Announced(IRenderedComponent<PivotComponent> cut)
        => cut.Find(".ex-pivot-sheet > .ex-grid .ex-announce").TextContent;

    [Fact] // ADR-0067/ADR-0152 (decided 2026-10-09): Copy from a Stale Report is refused, saying the report shows the data as of its time and to Retry first; nothing reaches the clipboard
    public async Task ADR0067_ADR0152_Copy_from_a_stale_report_is_refused_as_of_its_time()
    {
        var (cut, shownAt) = await RenderStaleAsync(new LiveSource());
        await SelectThreeValuesAsync(cut);

        var payload = await CopyAsync(cut);

        Assert.Null(payload);
        var expected = PivotWords.Fill(PivotWords.EnglishFor(CopyStaleReport)!, TimeOf(shownAt));
        Assert.Equal($"The report shows the data as of {TimeOf(shownAt)}: Retry before copying.", expected);
        cut.WaitForAssertion(() => Assert.Equal(expected, Announced(cut)));
        // The report and its notice stay as they were.
        Assert.True(cut.Instance.IsStale);
        Assert.Equal("East | 180", RowTexts(cut)[0]);
    }

    [Fact] // ADR-0067/ADR-0152 (decided 2026-10-09): the Selection Summary still answers over a Stale Report — it stands on screen beside the notice that says as of when
    public async Task ADR0067_ADR0152_The_selection_summary_still_answers_over_a_stale_report()
    {
        var (cut, _) = await RenderStaleAsync(new LiveSource());

        await SelectThreeValuesAsync(cut);

        // East 180, North 10 and West 90: the report on screen, not the newer data.
        cut.WaitForAssertion(() => Assert.Contains("Sum: 280", cut.Find(".ex-summary").TextContent));
        Assert.Contains("Count: 3", cut.Find(".ex-summary").TextContent);
    }

    [Fact] // ADR-0067/ADR-0152 (decided 2026-10-09): once Retry brings the newest data, Copy copies again — the newest
    public async Task ADR0067_ADR0152_Copy_copies_again_once_Retry_brings_the_newest()
    {
        var source = new LiveSource();
        var (cut, _) = await RenderStaleAsync(source);
        await SelectThreeValuesAsync(cut);
        Assert.Null(await CopyAsync(cut));
        source.Fails = null;

        await cut.Find(".ex-pivot-retry").ClickAsync(new MouseEventArgs());

        Assert.False(cut.Instance.IsStale);
        await SelectThreeValuesAsync(cut);
        var payload = await CopyAsync(cut);
        Assert.NotNull(payload);
        Assert.Equal(["181", "10", "90"], payload!.Text!.TrimEnd('\r', '\n').Split('\n').Select(line => line.TrimEnd('\r')));
    }

    [Fact] // ADR-0060/ADR-0067 (decided 2026-10-09): under the Japanese words the refusal is the bundled Japanese, the time put in; without them, a Consumer's Label replaces it by id
    public async Task ADR0060_ADR0067_The_refusal_is_a_word_of_the_table_in_japanese_and_replaced_by_id()
    {
        var (japanese, shownAt) = await RenderStaleAsync(new LiveSource(), ps => ps.Add(p => p.Label, PivotWords.Japanese));
        await SelectThreeValuesAsync(japanese);
        Assert.Null(await CopyAsync(japanese));
        var word = PivotWords.JapaneseFor(CopyStaleReport);
        Assert.Equal("レポートは {0} 時点のデータを表示しています。コピーする前に再試行してください。", word);
        var expected = PivotWords.Fill(word!, TimeOf(shownAt));
        japanese.WaitForAssertion(() => Assert.Equal(expected, Announced(japanese)));
        Assert.DoesNotMatch(Latin, Announced(japanese).Replace(TimeOf(shownAt), "", StringComparison.Ordinal));

        var (relabelled, relabelledAt) = await RenderStaleAsync(new LiveSource(),
            ps => ps.Add(p => p.Label, id => id == CopyStaleReport ? "Stale since {0}: retry, then copy." : null));
        await SelectThreeValuesAsync(relabelled);
        Assert.Null(await CopyAsync(relabelled));
        relabelled.WaitForAssertion(() => Assert.Equal($"Stale since {TimeOf(relabelledAt)}: retry, then copy.", Announced(relabelled)));
    }
}
