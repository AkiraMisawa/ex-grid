using Bunit;
using ExChart.Engine;
using Xunit;
using ChartComponent = ExChart.Components.ExChart;

namespace ExChart.Components.Tests;

/// <summary>Ticket 76: the component reads a Chart Spec through its Chart Sources (ADR-0322, ADR-0344).</summary>
public class ChartSourceComponentTests : BunitContext
{
    public ChartSourceComponentTests()
    {
        var module = JSInterop.SetupModule($"./{ShippedAssets.ScriptPath}?v={ShippedAssets.ScriptVersion}");
        module.Setup<string>("stamp").SetResult(ShippedAssets.ScriptStamp);
        var handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("draw", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    private const string Spec = """
        { "version": 1, "axis": { "values": "numbers", "title": "Second" },
          "series": [ { "id": "p", "name": "Price", "kind": "level", "data": "ticks", "x": "t", "y": "price", "fromZero": false } ] }
        """;

    // Ten thousand ticks, one spike at 7,000 that every thinning must keep.
    private static ChartData Ticks(decimal spike = 500m) => ChartData.Of(
        ("t", Enumerable.Range(0, 10_000).Select(i => (object?)(double)i).ToArray()),
        ("price", Enumerable.Range(0, 10_000).Select(i => (object?)(i == 7_000 ? spike : 100m + i % 7)).ToArray()));

    /// <summary>A source whose answers the test releases, each read as the reference reads it.</summary>
    private sealed class Gated(ChartData data) : IChartSource
    {
        public List<(ChartSourceQuery Query, TaskCompletionSource<ChartSourceAnswer> Answer)> Asks { get; } = [];

        public ValueTask<ChartSourceAnswer> ThinAsync(ChartSourceQuery query, CancellationToken cancellationToken = default)
        {
            var answer = new TaskCompletionSource<ChartSourceAnswer>(TaskCreationOptions.RunContinuationsAsynchronously);
            Asks.Add((query, answer));
            return new ValueTask<ChartSourceAnswer>(answer.Task);
        }

        public void Answer(int ask, long version = 1, ChartData? from = null) =>
            Asks[ask].Answer.SetResult(ChartSource.Thin(from ?? data, Asks[ask].Query, version));
    }

    private IRenderedComponent<ChartComponent> Render(Gated source) => Render<ChartComponent>(ps => ps
        .Add(c => c.Spec, Spec)
        .Add(c => c.Sources, h => h == "ticks" ? source : null)
        .Add(c => c.Width, 600));

    [Fact] // ADR-0344: the chart asks its source for its pixels, is Busy until the answer, and draws what was kept
    public void The_chart_reads_its_spec_through_its_source()
    {
        var source = new Gated(Ticks());
        var cut = Render(source);
        var (query, _) = Assert.Single(source.Asks);
        Assert.Equal(1200, query.Pixels);
        Assert.Null(query.From);
        Assert.Equal("true", cut.Find(".ex-chart").GetAttribute("aria-busy"));
        Assert.Empty(cut.FindAll("g.ex-chart-series"));

        source.Answer(0);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("g.ex-chart-series[data-series='p']")));
        var drawn = cut.Instance.Scene.Panels[0];
        Assert.True(drawn.Left!.Max >= 500); // the spike was kept
        Assert.True(cut.Find(".ex-chart-download-csv").HasAttribute("disabled")); // only the points drawn are held
    }

    [Fact] // ADR-0322, ADR-0344, spine rule 6: a narrowed range is asked again; what is shown stays until the newest answer, and an overtaken one is dropped
    public void A_narrowed_range_is_asked_again_and_an_overtaken_answer_is_dropped()
    {
        var source = new Gated(Ticks());
        var cut = Render(source);
        source.Answer(0);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("g.ex-chart-series")));
        var whole = cut.Instance.Scene;

        cut.Render(ps => ps.Add(c => c.Range, new ChartRange(1000, 2000)));
        cut.Render(ps => ps.Add(c => c.Range, new ChartRange(3000, 4000)));
        Assert.Equal(3, source.Asks.Count);
        Assert.Equal((3000.0, 4000.0), (source.Asks[2].Query.From!.Value, source.Asks[2].Query.To!.Value));
        Assert.Equal("true", cut.Find(".ex-chart").GetAttribute("aria-busy"));
        Assert.Same(whole.Panels[0].Series[0].Lines, cut.Instance.Scene.Panels[0].Series[0].Lines); // the previous drawing stays

        source.Answer(2);
        cut.WaitForAssertion(() => Assert.Equal(3000, cut.Instance.Scene.XFrom));
        source.Answer(1); // the answer to the replaced ask arrives last, and is dropped
        Assert.Equal(3000, cut.Instance.Scene.XFrom);
        Assert.Null(cut.Find(".ex-chart").GetAttribute("aria-busy"));
    }

    [Fact] // ADR-0322: an answer from data older than the chart shows is said in its place, never drawn
    public void An_answer_from_older_data_is_said_not_drawn()
    {
        var source = new Gated(Ticks());
        var cut = Render(source);
        source.Answer(0, version: 5);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("g.ex-chart-series")));

        cut.Render(ps => ps.Add(c => c.Range, new ChartRange(6000, 8000)));
        source.Answer(1, version: 4, from: Ticks(spike: 900m));
        cut.WaitForAssertion(() => Assert.Contains("version 4 of 'ticks', older than version 5", cut.Find(".ex-chart-source-refusal").TextContent));
        Assert.True(cut.Find(".ex-chart-stage").HasAttribute("hidden"));
    }

    [Fact] // ADR-0344, spine rule 6: a spec built again with the same text, or the same sources, is not asked again
    public void The_same_spec_and_sources_are_not_asked_again()
    {
        var source = new Gated(Ticks());
        var cut = Render(source);
        source.Answer(0);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("g.ex-chart-series")));

        cut.Render(ps => ps.Add(c => c.Spec, new string(Spec.AsSpan())));
        Assert.Single(source.Asks);
        Assert.Single(cut.FindAll("g.ex-chart-series"));
    }

    [Fact] // ADR-0344, spine rule 6: a Definition given while an ask is out is drawn, and the ask's answer, or its failure, is dropped
    public void An_ask_left_out_by_a_definition_is_dropped()
    {
        var source = new Gated(Ticks());
        var cut = Render(source);
        var definition = new ChartDefinition(ChartAxis.Numbers(), [ChartSeries.Of("d", "D", SeriesKind.Level, [new ChartPoint(0, ChartValue.Of(1.0)), new ChartPoint(1, ChartValue.Of(2.0))])]);
        cut.Render(ps => ps.Add(c => c.Spec, (string?)null).Add(c => c.Sources, null).Add(c => c.Definition, definition));
        Assert.Null(cut.Find(".ex-chart").GetAttribute("aria-busy"));

        source.Asks[0].Answer.SetException(new TaskCanceledException("timed out"));
        Assert.Empty(cut.FindAll(".ex-chart-source-refusal"));
        Assert.Single(cut.FindAll("g.ex-chart-series[data-series='d']"));
    }

    [Fact] // ADR-0344: a source that cancels its own ask is said, and the chart is not left Busy
    public void A_source_that_cancels_is_said_and_not_left_busy()
    {
        var source = new Gated(Ticks());
        var cut = Render(source);
        source.Asks[0].Answer.SetException(new TaskCanceledException("timed out"));
        cut.WaitForAssertion(() => Assert.Contains("did not answer", cut.Find(".ex-chart-source-refusal").TextContent));
        Assert.Null(cut.Find(".ex-chart").GetAttribute("aria-busy"));
    }

    [Fact] // ADR-0344: a spec its sources cannot answer is said in the chart's place
    public void A_refused_spec_is_said_in_the_charts_place()
    {
        var cut = Render<ChartComponent>(ps => ps
            .Add(c => c.Spec, Spec.Replace("\"ticks\"", "\"quotes\""))
            .Add(c => c.Sources, _ => null));
        cut.WaitForAssertion(() => Assert.StartsWith("series[0].data \"quotes\" is not known", cut.Find(".ex-chart-source-refusal").TextContent));
    }
}
