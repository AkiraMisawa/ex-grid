using Bunit;
using ExPivot.Engine;
using ExPivot.MudBlazor.Tests.Support;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.MudBlazor.Tests;

/// <summary>
/// The Stale Report's notice under MudBlazor (ADR-0062/0067): a warning <c>MudAlert</c> inside
/// ExPivot's live region under the Pivot Toolbar, with a Retry <c>MudButton</c> — saying what the
/// built-in markup says, and asking what it asks (PV-9, PV-37).
/// </summary>
public class MudPivotStaleReportTests : MudPivotTestContext
{
    private static readonly PivotLayout RegionAmount = new() { Rows = [P("Region")], Values = [Sum("Amount")] };

    /// <summary>A server's source through <c>PivotSource.Fetch</c>, answering from the data the
    /// test holds now, or failing while the test says so.</summary>
    private sealed class Server
    {
        public PivotSource Data { get; set; } = PivotSource.From(Sales, Fields);

        public Exception? Fails { get; set; }

        public int Asked { get; private set; }

        public FetchingPivotSource Source { get; }

        public Server()
        {
            Source = PivotSource.Fetch(Fields, new PivotSourceFeatures(Enum.GetValues<PivotAggregation>(), canRefresh: true),
                (query, ct) =>
                {
                    Asked++;
                    return Fails is { } error ? ValueTask.FromException<PivotAnswer>(error) : Data.AggregateAsync(query, ct);
                },
                (query, ct) => Data.ItemsAsync(query, ct),
                (query, ct) => Data.DetailsAsync(query, ct));
        }
    }

    /// <summary>The bundled source, refreshable, whose Refresh fails while the test says so —
    /// a server that cannot reload its data — counting what it is asked.</summary>
    private sealed class RefreshingSource : PivotSource
    {
        private readonly PivotSource _inner = PivotSource.From(Sales, MudPivotTestContext.Fields);

        public Exception? RefreshFails { get; set; }

        public int Refreshes { get; private set; }

        public int Questions { get; private set; }

        public override IReadOnlyList<PivotField> Fields => _inner.Fields;

        public override PivotSourceFeatures Features { get; } = new(Enum.GetValues<PivotAggregation>(), canRefresh: true);

        public override ValueTask<PivotAnswer> AggregateAsync(PivotQuery query, CancellationToken cancellationToken = default)
        {
            Questions++;
            return _inner.AggregateAsync(query, cancellationToken);
        }

        public override ValueTask<PivotItemPage> ItemsAsync(PivotItemsQuery query, CancellationToken cancellationToken = default)
            => _inner.ItemsAsync(query, cancellationToken);

        public override ValueTask<PivotDetailPage> DetailsAsync(PivotDetailsQuery query, CancellationToken cancellationToken = default)
            => _inner.DetailsAsync(query, cancellationToken);

        public override ValueTask RefreshAsync(CancellationToken cancellationToken = default)
        {
            Refreshes++;
            return RefreshFails is { } error ? ValueTask.FromException(error) : ValueTask.CompletedTask;
        }
    }

    private static Sale[] EastApples(decimal amount) => [Sales[0] with { Amount = amount }, .. Sales[1..]];

    /// <summary>The server's data moves on while it cannot be reached: the report is left stale.</summary>
    private static async Task<Server> StaleAsync(IRenderedComponent<PivotComponent> cut, Server server)
    {
        server.Data = PivotSource.From(EastApples(101), Fields);
        server.Fails = new InvalidOperationException("The server is unreachable.");
        await cut.InvokeAsync(() => server.Source.NotifyChanged());
        return server;
    }

    [Fact] // ADR-0062/0067 (PV-37): the notice is a warning MudAlert in ExPivot's live region, with a Retry MudButton that asks again; it goes when the answer is laid out
    public async Task The_notice_is_a_mud_alert_with_retry()
    {
        var server = new Server();
        var cut = RenderPivot(RegionAmount, source: server.Source);
        await StaleAsync(cut, server);

        var alert = cut.FindComponent<MudAlert>();
        Assert.Equal(Severity.Warning, alert.Instance.Severity);
        var element = cut.Find(".ex-pivot-stale[role=status] .mud-alert.mud-ex-pivot-stale-notice");
        // The region is ExPivot's: the alert adds no live region of its own.
        Assert.Null(element.GetAttribute("role"));
        Assert.Empty(cut.FindAll(".ex-pivot-stale-notice"));
        var message = cut.Find(".mud-ex-pivot-stale-message").TextContent;
        Assert.StartsWith("Showing the data as of ", message);
        Assert.EndsWith(": the source could not answer: The server is unreachable.", message);
        Assert.Equal("East | 180", RowTexts(cut)[0]);
        var retry = cut.FindComponents<MudButton>().Single(b => b.Instance.Class == "mud-ex-pivot-retry");
        Assert.Equal("Retry", cut.Find(".mud-ex-pivot-retry").TextContent.Trim());
        Assert.Equal(Icons.Material.Filled.Refresh, retry.Instance.StartIcon);
        Assert.False(retry.Instance.Disabled);

        server.Fails = null;
        var asked = server.Asked;
        await cut.Find(".mud-ex-pivot-retry").ClickAsync(new MouseEventArgs());

        Assert.Equal(asked + 1, server.Asked);
        Assert.Empty(cut.FindAll(".mud-ex-pivot-stale-notice"));
        Assert.Equal("East | 181", RowTexts(cut)[0]);
    }

    [Fact] // ADR-0061/0062 (PV-9, PV-37): the same change says the same thing, and Retry asks the same question, under the built-in markup and MudPivotChrome
    public async Task The_notice_says_what_the_built_in_says()
    {
        var mudServer = new Server();
        var mud = RenderPivot(RegionAmount, source: mudServer.Source);
        var plainServer = new Server();
        var plain = RenderPivot(RegionAmount, chrome: BuiltIn, source: plainServer.Source);

        await StaleAsync(mud, mudServer);
        await StaleAsync(plain, plainServer);

        static string Reason(string message) => message[(message.IndexOf(": ", StringComparison.Ordinal) + 2)..];
        Assert.Equal(Reason(plain.Find(".ex-pivot-stale-message").TextContent), Reason(mud.Find(".mud-ex-pivot-stale-message").TextContent));
        Assert.Equal(plain.Find(".ex-pivot-retry").TextContent.Trim(), mud.Find(".mud-ex-pivot-retry").TextContent.Trim());

        mudServer.Fails = null;
        plainServer.Fails = null;
        await mud.Find(".mud-ex-pivot-retry").ClickAsync(new MouseEventArgs());
        await plain.Find(".ex-pivot-retry").ClickAsync(new MouseEventArgs());

        Assert.Equal(plainServer.Asked, mudServer.Asked);
        Assert.Equal(RowTexts(plain), RowTexts(mud));
        Assert.Equal(PivotLayoutJson.Write(plain.Instance.CurrentLayout), PivotLayoutJson.Write(mud.Instance.CurrentLayout));
        Assert.False(mud.Instance.IsStale);
        Assert.False(plain.Instance.IsStale);
    }

    [Fact] // ADR-0067 refined, ADR-0062 (PV-9, PV-37): a failed Refresh is a Stale Report under MudBlazor too — the warning MudAlert says the source could not answer, nothing is said on the Pivot Toolbar, and Retry refreshes again, as under the built-in markup
    public async Task A_failed_refresh_is_a_stale_report_under_mudblazor()
    {
        var mudSource = new RefreshingSource { RefreshFails = new InvalidOperationException("The server cannot be reached.") };
        var mud = RenderPivot(RegionAmount, source: mudSource);
        var plainSource = new RefreshingSource { RefreshFails = new InvalidOperationException("The server cannot be reached.") };
        var plain = RenderPivot(RegionAmount, chrome: BuiltIn, source: plainSource);

        await mud.Find(".mud-ex-pivot-refresh-button").ClickAsync(new MouseEventArgs());
        await plain.Find(".ex-pivot-refresh-button").ClickAsync(new MouseEventArgs());

        Assert.True(mud.Instance.IsStale);
        Assert.Equal(Severity.Warning, mud.FindComponent<MudAlert>().Instance.Severity);
        var message = mud.Find(".ex-pivot-stale[role=status] .mud-ex-pivot-stale-message").TextContent;
        Assert.StartsWith("Showing the data as of ", message);
        Assert.EndsWith(": the source could not answer: The server cannot be reached.", message);
        static string Reason(string sentence) => sentence[(sentence.IndexOf(": ", StringComparison.Ordinal) + 2)..];
        Assert.Equal(Reason(plain.Find(".ex-pivot-stale-message").TextContent), Reason(message));
        Assert.Empty(mud.FindAll(".mud-ex-pivot-refusal-notice"));
        Assert.Equal("East | 180", RowTexts(mud)[0]);

        mudSource.RefreshFails = null;
        plainSource.RefreshFails = null;
        await mud.Find(".mud-ex-pivot-retry").ClickAsync(new MouseEventArgs());
        await plain.Find(".ex-pivot-retry").ClickAsync(new MouseEventArgs());

        Assert.Equal(2, mudSource.Refreshes);
        Assert.Equal(plainSource.Refreshes, mudSource.Refreshes);
        Assert.Equal(plainSource.Questions, mudSource.Questions);
        mud.WaitForAssertion(() => Assert.False(mud.Instance.IsStale));
        Assert.Empty(mud.FindAll(".mud-ex-pivot-stale-notice"));
    }
}
