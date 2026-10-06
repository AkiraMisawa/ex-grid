using Bunit;
using ExPivot.Engine;
using ExPivot.MudBlazor.Tests.Support;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.MudBlazor.Tests;

/// <summary>
/// A field's Items under live data, under MudBlazor (ADR-0066 refined, ADR-0062): while a new
/// Source Version's Items are on their way, the earlier version's stay in view — the report filter
/// band's summary, and Filter…'s list with OK enabled — marked busy, with no progress drawn, until
/// the new version's replace them; a first listing still shows its progress (PV-9, PV-23).
/// </summary>
public class MudPivotLiveItemsTests : MudPivotTestContext
{
    private static readonly PivotLayout WestHidden = new()
    {
        Filters = [P("Region") with { HiddenItems = [PivotItemKey.Text("West")] }],
        Rows = [P("Product")],
        Values = [Sum("Amount")],
    };

    /// <summary>A server's source through <c>PivotSource.Fetch</c> whose data the test moves on, and
    /// whose Items it answers when it says, each under the data it was asked of.</summary>
    private sealed class Server
    {
        private PivotSource _data = PivotSource.From(Sales, Fields);

        public Server()
        {
            Source = PivotSource.Fetch(Fields, new PivotSourceFeatures(Enum.GetValues<PivotAggregation>()),
                (query, ct) => _data.AggregateAsync(query, ct),
                (query, ct) =>
                {
                    var done = new TaskCompletionSource<PivotItemPage>();
                    Items.Add((query, _data, done));
                    return new ValueTask<PivotItemPage>(done.Task);
                },
                (query, ct) => _data.DetailsAsync(query, ct));
        }

        public FetchingPivotSource Source { get; }

        public List<(PivotItemsQuery Query, PivotSource From, TaskCompletionSource<PivotItemPage> Done)> Items { get; } = [];

        /// <summary>The server's data moves on, and the Consumer tells the source.</summary>
        public void Publish(IReadOnlyList<Sale> sales)
        {
            _data = PivotSource.From(sales, Fields);
            Source.NotifyChanged();
        }

        public async Task AnswerAsync(int index)
        {
            var (query, from, done) = Items[index];
            done.SetResult(await from.ItemsAsync(query));
        }
    }

    private static string Summary(IRenderedComponent<PivotComponent> cut) => cut.Find(".mud-ex-pivot-filter-summary").TextContent.Trim();

    private static string[] Listed(IRenderedComponent<PivotComponent> cut)
        => cut.FindAll(".mud-ex-pivot-item-filter .mud-checkbox").Select(c => c.TextContent.Trim()).ToArray();

    [Fact] // ADR-0066 refined, ADR-0062 (PV-9, PV-23): a first listing shows its progress; a new version's Items on their way leave the earlier ones listed — busy, no progress drawn, OK enabled — until they land
    public async Task Filter_keeps_the_earlier_items_listed_while_the_new_ones_are_on_their_way()
    {
        var server = new Server();
        var cut = RenderPivot(WestHidden, source: server.Source);
        cut.WaitForAssertion(() => Assert.Single(server.Items));

        // A first listing: nothing to list yet, and the Filter… says so.
        Assert.Equal("Loading…", Summary(cut));
        await cut.Find(".mud-ex-pivot-filter-button").ClickAsync(new MouseEventArgs());
        Assert.Single(cut.FindComponents<MudProgressLinear>());
        Assert.True(cut.Find(".mud-ex-pivot-ok").HasAttribute("disabled"));

        await cut.InvokeAsync(() => server.AnswerAsync(0));
        cut.WaitForAssertion(() => Assert.Equal(["(Select All)", "East", "North", "West", "(blank)"], Listed(cut)));
        Assert.Equal("(Multiple Items)", Summary(cut));
        Assert.Null(cut.Find(".mud-ex-pivot-item-list").GetAttribute("aria-busy"));

        // The server's data moves on: the report redraws, and the new version's Items are asked for.
        await cut.InvokeAsync(() => server.Publish([.. Sales, new Sale("South", "Apples", 1m, 1, true)]));
        cut.WaitForAssertion(() => Assert.Equal(2, server.Items.Count));

        Assert.Equal(cut.Instance.Report!.Metadata.SourceVersion, server.Items[1].Query.SourceVersion);
        Assert.NotEqual(server.Items[0].Query.SourceVersion, server.Items[1].Query.SourceVersion);
        Assert.Equal("(Multiple Items)", Summary(cut));
        Assert.Equal(["(Select All)", "East", "North", "West", "(blank)"], Listed(cut));
        Assert.Equal("true", cut.Find(".mud-ex-pivot-item-list").GetAttribute("aria-busy"));
        Assert.Empty(cut.FindComponents<MudProgressLinear>());
        Assert.False(cut.Find(".mud-ex-pivot-ok").HasAttribute("disabled"));

        await cut.InvokeAsync(() => server.AnswerAsync(1));

        cut.WaitForAssertion(() => Assert.Equal(["(Select All)", "East", "North", "South", "West", "(blank)"], Listed(cut)));
        Assert.Null(cut.Find(".mud-ex-pivot-item-list").GetAttribute("aria-busy"));
        Assert.Single(cut.FindAll(".ex-pivot-popup"));
    }
}
