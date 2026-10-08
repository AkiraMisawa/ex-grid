using Bunit;
using ExGrid;
using ExGrid.Components;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests;

public sealed class ReportWindowTests : PivotTestContext
{
    [Fact] // ADR-0151: the component's row extent is metadata; only requested rows reach ExGrid.
    public void ADR0151_A_remote_report_renders_a_window_with_the_complete_extent()
    {
        var requests = new List<PivotReportRequest>();
        var layout = new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] };
        var source = PivotReportSource.Fetch(Fields, PivotSourceFeatures.All, PivotReportUpdateMode.Incremental,
            (request, _) =>
            {
                requests.Add(request);
                var metadata = new PivotReportMetadata(new("report-1"), "source-1", "order-1", request.Layout,
                    request.Settings, 1_000_000, [new("label", "Region", "Region")],
                    [new("value", "Sum of Amount", PivotColumnRole.Item, 0, [])], [], 0, ["Sum of Amount"]);
                var rows = Enumerable.Range(request.Window.Start, request.Window.Count).Select(i =>
                    new PivotDisplayRow(new(PivotRowRole.Item, -1, [PivotItemKey.Text($"Region {i}")]),
                        PivotRowRole.Item, -1, true, [new($"Region {i}")], [new(i, i, null, i.ToString())],
                        [new("Region", PivotItemKey.Text($"Region {i}"))])).ToArray();
                return ValueTask.FromResult(PivotReportUpdate.Complete(request, metadata, rows));
            }, items: (query, _) => ValueTask.FromResult(new PivotItemPage(query.SourceVersion, [], 0)));
        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
        var cut = Render<PivotComponent>(ps => ps.Add(p => p.Source, source).Add(p => p.Layout, layout)
            .Add(p => p.ShowFieldList, false).Add(p => p.ViewportHeight, (ViewportSize)200));

        var grid = cut.FindComponent<ExGrid<PivotDisplayRow>>().Instance;
        Assert.Equal(1_000_000, grid.TotalCount);
        Assert.InRange(grid.Window.Count, 1, 100);
        Assert.Equal("Region 0", grid.Window[0].Labels[0].Text);
        Assert.All(requests, request => Assert.InRange(request.Window.Count, 1, 100));
    }
    /// <summary>A keyed sale, for a server's Change Batches.</summary>
    public sealed record KeyedSale(long Id, string? Region, string Product, decimal Amount, int Quantity, bool Online);

    [Fact] // ADR-0152: a delta that leaves out a total's change fails its digest, and is never painted: ExPivot shows the complete Window asked for in its place, equal to the server's report
    public async Task ADR0152_A_delta_that_fails_its_digest_is_never_painted()
    {
        var fields = PivotFields.Of<KeyedSale>().Key("Id", s => s.Id).Text("Region", s => s.Region).Text("Product", s => s.Product)
            .Number("Amount", s => s.Amount).Number("Quantity", s => s.Quantity).Boolean("Online", s => s.Online);
        var sales = Sales.Select((s, i) => new KeyedSale(i, s.Region, s.Product, s.Amount, s.Quantity, s.Online)).ToArray();
        var data = PivotSource.From(sales, fields);
        await using var server = PivotReportSource.From(data, timeProvider: Clock);
        static T Wire<T>(T value) => PivotReportJson.Read<T>(PivotReportJson.Write(value));
        var answers = new List<PivotReportUpdate>();
        // A relay that rebuilds deltas and forgets the grand total, keeping the server's digest.
        var relay = PivotReportSource.Fetch(server.Fields, server.Features, server.UpdateMode,
            async (request, ct) =>
            {
                var update = Wire(await server.WindowAsync(Wire(request), ct));
                if (update.Rows is null)
                    update = update with { Changes = [.. update.Changes.Where(change => change.Row.Role != PivotRowRole.GrandTotal)] };
                answers.Add(update);
                return update;
            },
            items: server.RawItemsAsync, reportItems: async (query, ct) => Wire(await server.ItemsAsync(Wire(query), ct)));
        data.Changed += change => relay.NotifyChanged(change.SourceVersion);
        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
        var cut = Render<PivotComponent>(ps => ps.Add(p => p.Source, relay)
            .Add(p => p.Layout, new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] })
            .Add(p => p.Culture, System.Globalization.CultureInfo.GetCultureInfo("en-US"))
            .Add(p => p.ViewportHeight, (ViewportSize)400).Add(p => p.ViewportWidth, (ViewportSize)700));
        cut.WaitForAssertion(() => Assert.Equal(["East | 180", "North | 10", "West | 90", "(blank) | 5", "Grand Total | 285"], RowTexts(cut)));
        var asked = answers.Count;

        Clock.Advance(TimeSpan.FromSeconds(1));
        await cut.InvokeAsync(() => data.Apply(fields.Batch(changed: [sales[0] with { Amount = 101m }])));

        cut.WaitForAssertion(() => Assert.Equal(["East | 181", "North | 10", "West | 90", "(blank) | 5", "Grand Total | 286"], RowTexts(cut)));
        // The delta, discarded unseen, and the complete Window in its place.
        Assert.Equal(asked + 2, answers.Count);
        Assert.Null(answers[^2].Rows);
        Assert.NotNull(answers[^1].Rows);
        Assert.False(cut.Instance.IsStale);
        Assert.Equal(["181", "286"], ChangeHighlightTests.MarkedTexts(cut));
    }
}
