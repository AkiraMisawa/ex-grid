using Bunit;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests;

/// <summary>
/// LV-26's layer 2 clause, ADR-0152: a remote custom Order Key's refusal is surfaced. The report is
/// computed on a "server" — the bundled report source behind a transport that carries every question
/// and answer as JSON — and ExPivot names the server-registered Order Key by its identifier only.
/// A function that throws, and an identifier the server never registered, are said on the Pivot
/// Toolbar by name, and the report is not shown in label order instead.
/// </summary>
public sealed class OrderKeyRefusalTests : PivotTestContext
{
    private static readonly PivotLayout RegionAmount = new() { Rows = [P("Region")], Values = [Sum("Amount")] };

    /// <summary>The server's Order Keys: West first, then North, then East — not label order — and
    /// one that throws on West and on Pears.</summary>
    private static readonly Dictionary<string, Func<object, IComparable?>> ServerOrderKeys = new()
    {
        ["west-first"] = item => (string)item switch { "West" => 0, "North" => 1, "East" => 2, _ => 3 },
        ["picky"] = item => (string)item is "West" or "Pears" ? throw new FormatException("not this one") : (string)item,
    };

    private static T Wire<T>(T value) => PivotReportJson.Read<T>(PivotReportJson.Write(value));

    /// <summary>A report source whose every question and answer crosses the wire as JSON.</summary>
    private static (PivotReportSource Remote, LocalPivotReportSource Server) OverTheWire()
    {
        var server = PivotReportSource.From(Bundled(), ServerOrderKeys);
        var remote = PivotReportSource.Fetch(server.Fields, server.Features, server.UpdateMode,
            async (request, ct) => Wire(await server.WindowAsync(Wire(request), ct)),
            items: server.RawItemsAsync,
            reportItems: async (query, ct) => Wire(await server.ItemsAsync(Wire(query), ct)),
            readAheadRows: 0);
        return (remote, server);
    }

    private IRenderedComponent<PivotComponent> RenderWithPolicy(PivotReportSource remote, string policy)
    {
        var cut = RenderPivot(RegionAmount,
            ps => ps.Add(p => p.OrderKeyPolicies, new Dictionary<string, string> { ["Region"] = policy }),
            reportSource: remote);
        // Answered: a report shown, or the question refused.
        cut.WaitForState(() => !cut.Instance.IsLoading && (cut.Instance.Report is not null || cut.Instance.LastError is not null));
        return cut;
    }

    private static string FirstLabels(IRenderedComponent<PivotComponent> cut, int count)
        => string.Join(",", RowTexts(cut).Take(count).Select(row => row.Split(" | ")[0]));

    [Fact] // LV-26, ADR-0152: the control — a registered server Order Key orders the report, and nothing is refused
    public async Task LV26_a_registered_server_order_key_orders_the_report()
    {
        var (remote, server) = OverTheWire();
        await using var _ = server;

        var cut = RenderWithPolicy(remote, "west-first");

        Assert.Empty(cut.FindAll(".ex-pivot-refusal-notice"));
        Assert.Equal("West,North,East", FirstLabels(cut, 3));
    }

    [Fact] // LV-26, ADR-0152: a registered function that throws is refused by name on the Pivot Toolbar (OrderKeyFailed), never label order
    public async Task LV26_a_throwing_server_order_key_is_surfaced_by_name_and_no_label_order_is_shown()
    {
        var (remote, server) = OverTheWire();
        await using var _ = server;

        var cut = RenderWithPolicy(remote, "picky");

        var notice = cut.Find(".ex-pivot-refusal-notice");
        Assert.Equal("alert", notice.GetAttribute("role"));
        Assert.Equal("The source could not answer: The Order Key of Region failed on 'West'.", notice.TextContent);
        Assert.Equal("The Order Key of Region failed on 'West'.", cut.Instance.LastError?.Message);
        Assert.Null(cut.Instance.Report);
        Assert.Empty(RowTexts(cut));
    }

    [Fact] // LV-26, ADR-0152: an identifier the server never registered is refused by name on the Pivot Toolbar, never label order
    public async Task LV26_an_unknown_order_key_policy_is_surfaced_by_name_and_no_label_order_is_shown()
    {
        var (remote, server) = OverTheWire();
        await using var _ = server;

        var cut = RenderWithPolicy(remote, "missing");

        var notice = cut.Find(".ex-pivot-refusal-notice");
        Assert.Equal("alert", notice.GetAttribute("role"));
        Assert.Equal("The source could not answer: The Order Key policy 'missing' for 'Region' is not registered.", notice.TextContent);
        Assert.Null(cut.Instance.Report);
        Assert.Empty(RowTexts(cut));
    }

    [Fact] // LV-26, ADR-0152: with a report shown, a layout the throwing Order Key refuses is said by name; the report stays as it was, not redrawn in label order
    public async Task LV26_a_layout_refused_by_a_throwing_order_key_leaves_the_shown_report()
    {
        var (remote, server) = OverTheWire();
        await using var _ = server;
        var cut = RenderPivot(RegionAmount,
            ps => ps.Add(p => p.OrderKeyPolicies, new Dictionary<string, string> { ["Region"] = "west-first", ["Product"] = "picky" }),
            reportSource: remote);
        cut.WaitForState(() => !cut.Instance.IsLoading && cut.Instance.Report is not null);
        var shown = cut.Instance.Report;
        var before = RowTexts(cut);
        Assert.Equal("West,North,East", FirstLabels(cut, 3));

        await TickFieldAsync(cut, "Product", true);
        cut.WaitForState(() => !cut.Instance.IsLoading && cut.Instance.LastError is not null);

        cut.WaitForAssertion(() => Assert.Equal("The source could not answer: The Order Key of Product failed on 'Pears'.",
            cut.Find(".ex-pivot-refusal-notice").TextContent));
        Assert.Same(shown, cut.Instance.Report);
        Assert.Equal(before, RowTexts(cut));
        Assert.Equal(["Region"], cut.Instance.CurrentLayout.Rows.Select(p => p.Field));
    }
}
