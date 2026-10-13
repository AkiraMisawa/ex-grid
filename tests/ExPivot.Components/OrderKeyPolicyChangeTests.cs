using Bunit;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests;

/// <summary>
/// ADR-0152's note of 2026-10-13 (LV-26): a change of <c>OrderKeyPolicies</c> on a report already
/// shown asks for the report again, as a layout change does, so the screen never keeps one order
/// while the parameter names another; the same policies handed again ask nothing. The report is
/// computed on a "server" behind a JSON wire, whose Window questions are counted.
/// </summary>
public sealed class OrderKeyPolicyChangeTests : PivotTestContext
{
    private static readonly PivotLayout RegionAmount = new() { Rows = [P("Region")], Values = [Sum("Amount")] };

    private static readonly Dictionary<string, Func<object, IComparable?>> ServerOrderKeys = new()
    {
        ["west-first"] = item => (string)item switch { "West" => 0, "North" => 1, "East" => 2, _ => 3 },
        ["north-first"] = item => (string)item switch { "North" => 0, "East" => 1, "West" => 2, _ => 3 },
        ["picky"] = item => (string)item == "West" ? throw new FormatException("not this one") : (string)item,
    };

    private static T Wire<T>(T value) => PivotReportJson.Read<T>(PivotReportJson.Write(value));

    /// <summary>The server, and how many Window questions have crossed the wire to it.</summary>
    private sealed class Server : IAsyncDisposable
    {
        private readonly LocalPivotReportSource _server = PivotReportSource.From(Bundled(), ServerOrderKeys);

        public Server()
        {
            Remote = PivotReportSource.Fetch(_server.Fields, _server.Features, _server.UpdateMode,
                async (request, ct) =>
                {
                    Questions++;
                    return Wire(await _server.WindowAsync(Wire(request), ct));
                },
                items: _server.RawItemsAsync,
                reportItems: async (query, ct) => Wire(await _server.ItemsAsync(Wire(query), ct)),
                readAheadRows: 0);
        }

        public PivotReportSource Remote { get; }

        public int Questions { get; private set; }

        public ValueTask DisposeAsync() => _server.DisposeAsync();
    }

    private static Dictionary<string, string> Policy(string id) => new() { ["Region"] = id };

    private IRenderedComponent<PivotComponent> RenderShown(Server server, string policy)
    {
        var cut = RenderPivot(RegionAmount, ps => ps.Add(p => p.OrderKeyPolicies, Policy(policy)), reportSource: server.Remote);
        cut.WaitForState(() => !cut.Instance.IsLoading && cut.Instance.Report is not null);
        return cut;
    }

    private static string FirstLabels(IRenderedComponent<PivotComponent> cut)
        => string.Join(",", RowTexts(cut).Take(3).Select(row => row.Split(" | ")[0]));

    [Fact] // LV-26, ADR-0152 (2026-10-13): a new Order Key policy on a shown report asks for it again, and the report reorders
    public async Task LV26_a_changed_order_key_policy_asks_again_and_reorders()
    {
        await using var server = new Server();
        var cut = RenderShown(server, "west-first");
        var shown = cut.Instance.Report;
        var asked = server.Questions;
        Assert.Equal("West,North,East", FirstLabels(cut));

        cut.Render(ps => ps.Add(p => p.OrderKeyPolicies, Policy("north-first")));

        cut.WaitForState(() => !cut.Instance.IsLoading && !ReferenceEquals(cut.Instance.Report, shown));
        Assert.True(server.Questions > asked);
        Assert.Equal("North,East,West", FirstLabels(cut));
        Assert.Empty(cut.FindAll(".ex-pivot-refusal-notice"));
    }

    [Fact] // LV-26, ADR-0152 (2026-10-13): the same policies handed again, in a new dictionary, ask nothing
    public async Task LV26_the_same_order_key_policies_handed_again_ask_nothing()
    {
        await using var server = new Server();
        var cut = RenderShown(server, "west-first");
        var shown = cut.Instance.Report;
        var asked = server.Questions;

        cut.Render(ps => ps.Add(p => p.OrderKeyPolicies, Policy("west-first")));
        cut.Render(ps => ps.Add(p => p.OrderKeyPolicies, Policy("west-first")));

        Assert.False(cut.Instance.IsLoading);
        Assert.Equal(asked, server.Questions);
        Assert.Same(shown, cut.Instance.Report);
        Assert.Equal("West,North,East", FirstLabels(cut));
    }

    [Fact] // LV-26, ADR-0152 (2026-10-13): a new policy whose key throws is asked, refused by name, and the report shown stays — never label order
    public async Task LV26_a_changed_policy_that_throws_is_refused_by_name_and_the_shown_report_stays()
    {
        await using var server = new Server();
        var cut = RenderShown(server, "west-first");
        var shown = cut.Instance.Report;
        var before = RowTexts(cut);

        cut.Render(ps => ps.Add(p => p.OrderKeyPolicies, Policy("picky")));

        cut.WaitForState(() => !cut.Instance.IsLoading && cut.Instance.LastError is not null);
        Assert.Equal("The source could not answer: The Order Key of Region failed on 'West'.",
            cut.Find(".ex-pivot-refusal-notice").TextContent);
        Assert.Same(shown, cut.Instance.Report);
        Assert.Equal(before, RowTexts(cut));
    }
}
