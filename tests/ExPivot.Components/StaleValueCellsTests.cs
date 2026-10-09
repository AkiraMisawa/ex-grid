using System.Text.RegularExpressions;
using Bunit;
using ExGrid.Components;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests;

/// <summary>
/// A Stale Report's value cells are marked too (ADR-0067, decided with the user on 2026-10-09).
/// While the report stays on the last version it could compute, ExPivot puts
/// <c>ex-pivot-report-stale</c> on the report, and its stylesheet paints the value cells of the
/// report's own grid in the Visual Token <c>--ex-pivot-stale-value-color</c>: still readable, and
/// still. The notice under the Pivot Toolbar stays. Whatever left the report stale, the mark comes
/// with the notice and goes with it. It is a class on ExPivot's own markup, so no row of the grid
/// renders for it. The clock is the test's.
/// </summary>
public sealed class StaleValueCellsTests : PivotTestContext
{
    private static readonly PivotLayout RegionAmount = new() { Rows = [P("Region")], Values = [Sum("Amount")] };

    private static readonly PivotLayout RegionProduct = new() { Rows = [P("Region"), P("Product")], Values = [Sum("Amount")] };

    private static readonly Sale South = new("South", "Apples", 1m, 1, true);

    private static Sale[] EastApples(decimal amount) => [Sales[0] with { Amount = amount }, .. Sales[1..]];

    public StaleValueCellsTests() => Clock.SetUtcNow(new DateTimeOffset(2026, 10, 9, 9, 30, 0, TimeSpan.Zero));

    /// <summary>A keyed sale, for Change Batches.</summary>
    public sealed record Trade(long Id, string Region, decimal Amount);

    /// <summary>What leaves the report stale.</summary>
    public enum Cause
    {
        /// <summary>The source fails a question for newer data.</summary>
        SourceFailed,

        /// <summary>The source refuses a question for newer data.</summary>
        SourceRefused,

        /// <summary>Newer data needs more leaves than the cap.</summary>
        LeafCap,

        /// <summary>Newer data needs more rows than the cap.</summary>
        RowCap,

        /// <summary>A Refresh fails.</summary>
        RefreshFailed,

        /// <summary>Memory runs out while the report of the newest data is laid out.</summary>
        OutOfMemory,
    }

    /// <summary>A pivot, what leaves its report stale, and what brings it back to the newest data.</summary>
    private sealed record Made(IRenderedComponent<PivotComponent> Cut, Func<Task> StaleAsync, Func<Task> RecoverAsync);

    private static Task PublishAsync(IRenderedComponent<PivotComponent> cut, LiveSource source, IReadOnlyList<Sale> records)
        => cut.InvokeAsync(() => source.Publish(records));

    /// <summary>Whether the report carries the mark its value cells are painted by.</summary>
    private static bool Marked(IRenderedComponent<PivotComponent> cut)
        => cut.Find(".ex-pivot-report").ClassList.Contains("ex-pivot-report-stale");

    private static bool NoticeShown(IRenderedComponent<PivotComponent> cut)
        => cut.FindAll(".ex-pivot-stale[role=status] .ex-pivot-stale-notice").Count == 1;

    private Made Make(Cause cause)
    {
        switch (cause)
        {
            case Cause.SourceFailed:
            {
                var source = new LiveSource();
                var cut = RenderPivot(RegionAmount, source: source);
                return new(cut,
                    () =>
                    {
                        source.Fails = new InvalidOperationException("The server is unreachable.");
                        return PublishAsync(cut, source, EastApples(101));
                    },
                    () =>
                    {
                        source.Fails = null;
                        return cut.Find(".ex-pivot-retry").ClickAsync(new MouseEventArgs());
                    });
            }
            case Cause.SourceRefused:
            {
                var source = new LiveSource();
                var cut = RenderPivot(RegionAmount, source: source);
                return new(cut,
                    () =>
                    {
                        source.Refuses = new PivotSourceRefusal(PivotSourceRefusalKind.SourceVersionNotHeld, "The data is being reloaded.");
                        return PublishAsync(cut, source, EastApples(101));
                    },
                    () =>
                    {
                        // The next change of data that can be shown takes the report back.
                        source.Refuses = null;
                        Clock.Advance(PivotComponent.DefaultRedrawInterval);
                        return PublishAsync(cut, source, EastApples(102));
                    });
            }
            case Cause.LeafCap:
            {
                var source = new LiveSource();
                var cut = RenderPivot(RegionAmount, ps => ps.Add(p => p.Caps, new PivotCaps { MaxLeaves = 4 }), source: source);
                return new(cut,
                    () => PublishAsync(cut, source, [.. Sales, South]),
                    () =>
                    {
                        Clock.Advance(PivotComponent.DefaultRedrawInterval);
                        return PublishAsync(cut, source, EastApples(101));
                    });
            }
            case Cause.RowCap:
            {
                var source = new LiveSource();
                var cut = RenderPivot(RegionProduct, ps => ps.Add(p => p.Caps, new PivotCaps { MaxRows = 11 }), source: source);
                return new(cut,
                    () => PublishAsync(cut, source, [.. Sales, South]),
                    // Collapsing East lays the newest answer, held, out in rows that fit.
                    () => cut.FindAll(".ex-pivot-toggle")[0].ClickAsync(new MouseEventArgs()));
            }
            case Cause.RefreshFailed:
            {
                var source = new OnDemandSource(Bundled(), new PivotSourceFeatures(Enum.GetValues<PivotAggregation>(), canRefresh: true))
                {
                    AnswersAtOnce = true,
                };
                var cut = RenderPivot(RegionProduct, source: source);
                return new(cut,
                    () =>
                    {
                        source.RefreshFails = new InvalidOperationException("The server cannot be reached.");
                        return cut.Find(".ex-pivot-refresh-button").ClickAsync(new MouseEventArgs());
                    },
                    () =>
                    {
                        source.RefreshFails = null;
                        return cut.Find(".ex-pivot-retry").ClickAsync(new MouseEventArgs());
                    });
            }
            case Cause.OutOfMemory:
            {
                var starved = false;
                // The engine asks a field's Order Key of each new Item as it lays the report out.
                var fields = PivotFields.Of<Trade>()
                    .Key("Id", t => t.Id)
                    .Text("Region", t => t.Region, orderKey: region => starved ? throw new OutOfMemoryException() : region)
                    .Number("Amount", t => t.Amount);
                var data = PivotSource.From([new Trade(1, "East", 100m), new Trade(2, "West", 70m)], fields);
                var cut = RenderPivot(RegionAmount, source: data);
                return new(cut,
                    () =>
                    {
                        starved = true;
                        return cut.InvokeAsync(() => data.Apply(fields.Batch(added: [new Trade(3, "North", 10m)])));
                    },
                    () =>
                    {
                        starved = false;
                        Clock.Advance(PivotComponent.DefaultRedrawInterval);
                        return cut.InvokeAsync(() => data.Apply(fields.Batch(added: [new Trade(4, "South", 1m)])));
                    });
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(cause), cause, null);
        }
    }

    [Theory] // ADR-0067 (decided 2026-10-09): whatever leaves the report stale — the source failing or refusing, newer data over a cap, a failed Refresh, memory running out — its value cells are marked while the notice stands, and unmarked when the report recovers
    [InlineData(Cause.SourceFailed)]
    [InlineData(Cause.SourceRefused)]
    [InlineData(Cause.LeafCap)]
    [InlineData(Cause.RowCap)]
    [InlineData(Cause.RefreshFailed)]
    [InlineData(Cause.OutOfMemory)]
    public async Task ADR0067_A_stale_reports_value_cells_are_marked_until_it_recovers(Cause cause)
    {
        var made = Make(cause);
        var cut = made.Cut;
        var before = RowTexts(cut);
        Assert.False(Marked(cut));

        await made.StaleAsync();

        Assert.True(cut.Instance.IsStale);
        Assert.True(Marked(cut));
        // The notice stays, and the values stay readable where they were.
        Assert.True(NoticeShown(cut));
        Assert.Equal(before, RowTexts(cut));

        await made.RecoverAsync();

        cut.WaitForAssertion(() => Assert.False(cut.Instance.IsStale));
        Assert.False(Marked(cut));
        Assert.False(NoticeShown(cut));
    }

    [Fact] // ADR-0067/ADR-0003 (decided 2026-10-09): the mark is a class on ExPivot's own markup, so a report left stale renders neither its grid nor any of its rows again for it
    public async Task ADR0067_The_mark_renders_no_row_of_the_grid()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, source: source);
        var grid = Grid(cut).RenderCount;
        var rows = cut.FindComponents<ExGridRow<PivotDisplayRow>>().Sum(row => row.RenderCount);
        source.Fails = new InvalidOperationException("The server is unreachable.");

        await PublishAsync(cut, source, EastApples(101));

        Assert.True(Marked(cut));
        Assert.Equal(grid, Grid(cut).RenderCount);
        Assert.Equal(rows, cut.FindComponents<ExGridRow<PivotDisplayRow>>().Sum(row => row.RenderCount));
    }

    // ---- What ExPivot's stylesheet says --------------------------------------------------------

    private static string Stylesheet()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExGrid.slnx")))
            directory = directory.Parent;
        Assert.True(directory is not null, $"no ExGrid.slnx above {AppContext.BaseDirectory}");
        var file = Path.Combine(directory!.FullName, "src", "ExPivot", "Assets", "ex-pivot.css");
        Assert.True(File.Exists(file), $"{file} is not there");
        return Regex.Replace(File.ReadAllText(file), @"/\*.*?\*/", "", RegexOptions.Singleline);
    }

    /// <summary>The stylesheet's rules, outside any at-rule and inside <c>@media (forced-colors: active)</c>,
    /// each as its selector and its declarations.</summary>
    private static (List<(string Selector, string Body)> Plain, List<(string Selector, string Body)> ForcedColors) Rules(string css)
    {
        var forced = Regex.Match(css, @"@media\s*\(forced-colors:\s*active\)\s*\{((?:[^{}]*\{[^{}]*\})*[^{}]*)\}");
        Assert.True(forced.Success, "no forced-colors block in ExPivot's stylesheet");
        static List<(string, string)> Read(string text) => [.. Regex.Matches(text, @"([^{}@]+)\{([^{}]*)\}")
            .Select(m => (Regex.Replace(m.Groups[1].Value.Trim(), @"\s+", " "), m.Groups[2].Value.Trim()))];
        return (Read(css.Remove(forced.Index, forced.Length)), Read(forced.Groups[1].Value));
    }

    [Fact] // ADR-0067/ADR-0029 (decided 2026-10-09): the mark is painted by ExPivot's stylesheet through the Visual Token --ex-pivot-stale-value-color, with a default, on the value cells of the report's own grid alone — never a details tab's records nor the labels — and in forced colours as GrayText
    public void ADR0067_The_stylesheet_paints_the_mark_through_its_token_on_the_reports_value_cells()
    {
        var (plain, forced) = Rules(Stylesheet());

        var rule = Assert.Single(plain, r => r.Selector.Contains("ex-pivot-report-stale", StringComparison.Ordinal));
        // The report's own grid is the sheet's child; a details tab's records stand in a panel beside
        // it, and the label cells are Text, so only the report's value cells are numeric.
        Assert.Equal(".ex-pivot-report-stale > .ex-pivot-sheet > .ex-grid .ex-cell-numeric", rule.Selector);
        // One declaration: the colour, and nothing that moves, sizes or covers the cell.
        var color = Assert.Single(Regex.Matches(rule.Body, @"([\w-]+)\s*:\s*([^;]+)").ToArray());
        Assert.Equal("color", color.Groups[1].Value);
        // The token, read with a default of its own, so the bare pivot marks its cells too.
        Assert.Matches(@"^var\(--ex-pivot-stale-value-color\s*,\s*\S.*\)$", color.Groups[2].Value.Trim());

        var high = Assert.Single(forced, r => r.Selector.Contains("ex-pivot-report-stale", StringComparison.Ordinal));
        Assert.Equal(rule.Selector, high.Selector);
        Assert.Matches(@"^color:\s*GrayText;?$", high.Body);
    }

    [Fact] // ADR-0067/ADR-0027 P8 (decided 2026-10-09): the mark comes and goes in one step — the rule that paints it, and every rule of ExPivot's stylesheet, transitions and animates nothing
    public void ADR0067_The_mark_comes_and_goes_in_one_step()
    {
        var css = Stylesheet();

        Assert.Contains("ex-pivot-report-stale", css);
        Assert.DoesNotMatch(new Regex(@"(?<![\w-])(transition|animation)(-[\w-]+)?\s*:|@keyframes"), css);
    }
}
