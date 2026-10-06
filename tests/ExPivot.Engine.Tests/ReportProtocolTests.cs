using ExPivot.Engine;
using Xunit;

namespace ExPivot.Engine.Tests;

public class ReportProtocolTests
{
    [Fact]
    public async Task ADR0152_missing_baseline_recovers_a_complete_window_before_publication()
    {
        var requests = new List<PivotReportRequest>();
        var source = PivotReportSource.Fetch([], PivotSourceFeatures.All, PivotReportUpdateMode.FullRefresh,
            (request, _) =>
            {
                requests.Add(request);
                return ValueTask.FromResult(request.Baseline is not null
                    ? PivotReportUpdate.Refused(request, new(PivotReportRefusalKind.BaselineNotHeld, "The baseline expired."))
                    : PivotReportUpdate.Complete(request, Metadata("v2"), []));
            });
        var client = new PivotReportClient(source);
        Assert.True(await client.ReadAsync(PivotLayout.Empty, PivotReportSettings.Invariant, new(0, 10), cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(await client.ReadAsync(PivotLayout.Empty, PivotReportSettings.Invariant, new(0, 10), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(3, requests.Count);
        Assert.NotNull(requests[1].Baseline);
        Assert.Null(requests[2].Baseline);
        Assert.Equal("v2", client.Current!.Metadata.Version.Value);
        Assert.Null(client.Refusal);
    }

    [Fact]
    public async Task ADR0151_local_and_serialized_remote_windows_keep_the_full_extent_and_exact_values()
    {
        var data = PivotSource.From(Pivot.Sales, Pivot.Fields);
        await using var local = PivotReportSource.From(data);
        var remote = PivotReportSource.Fetch(data.Fields, data.Features, local.UpdateMode,
            async (query, ct) => PivotReportJson.Read<PivotReportUpdate>(PivotReportJson.Write(
                await local.WindowAsync(PivotReportJson.Read<PivotReportRequest>(PivotReportJson.Write(query)), ct))));
        var client = new PivotReportClient(remote);
        var layout = Pivot.RowsBy("Region");
        Assert.True(await client.ReadAsync(layout, PivotReportSettings.Invariant, new(0, 1),
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(5, client.Current!.Metadata.RowCount);
        Assert.Single(client.Current.Rows);
        Assert.Equal("East", client.Current.Rows[0].Labels[0].Text);
        Assert.Equal(180m, client.Current.Rows[0].ValueAt(0)!.Exact);
    }

    private sealed record Entry(long Id, string Label, decimal Amount);
    private static PivotFields<Entry> EntryFields() => PivotFields.Of<Entry>().Key("Id", row => row.Id)
        .Text("Label", row => row.Label).Number("Amount", row => row.Amount);
    private static PivotLayout EntryLayout() => new() { Rows = [new("Label")], Values = [new("Amount") { NumberFormat = "0" }] };

    [Fact]
    public async Task ADR0152_offscreen_change_advances_metadata_and_versioned_operations_never_rebind()
    {
        var fields = EntryFields();
        var data = PivotSource.From<Entry>([new(1, "A", 10m), new(2, "B", 20m)], fields);
        await using var source = PivotReportSource.From(data);
        var client = new PivotReportClient(source);
        var layout = EntryLayout();
        var ct = TestContext.Current.CancellationToken;
        Assert.True(await client.ReadAsync(layout, PivotReportSettings.Invariant, new(0, 1), cancellationToken: ct));
        var before = client.Current!;
        data.Apply(fields.Batch(changed: [new(2, "B", 30m)]));
        Assert.True(await client.ReadAsync(layout, PivotReportSettings.Invariant, new(0, 1), cancellationToken: ct));
        var after = client.Current!;
        Assert.Same(before.Rows[0], after.Rows[0]);
        Assert.NotEqual(before.Metadata.Version, after.Metadata.Version);
        Assert.NotEqual(before.Metadata.SourceVersion, after.Metadata.SourceVersion);
        Assert.Equal(before.Metadata.RowSequenceVersion, after.Metadata.RowSequenceVersion);
        string[] columns = [after.Metadata.ValueColumns[0].Name];
        PivotReportRange[] ranges = [new(1, 0, 1, 0)];
        var oldCopy = await source.CopyAsync(new(before.Metadata.Version, columns, ranges), ct);
        var newCopy = await source.CopyAsync(new(after.Metadata.Version, columns, ranges), ct);
        Assert.Equal("20", oldCopy.Blocks[0].Rows[0][0].Raw);
        Assert.Equal("30", newCopy.Blocks[0].Rows[0][0].Raw);
        var sum = await source.SummaryAsync(new(after.Metadata.Version, columns, [new(0, 0, 1, 0), new(1, 0, 2, 0)]), ct);
        Assert.Equal(3, sum.Counts.Numbers);
        Assert.Equal(80m, sum.Sum.Exact);
        data.Apply(fields.Batch(changed: [new(2, "B", 40m)]));
        Assert.True(await client.ReadAsync(layout, PivotReportSettings.Invariant, new(0, 1), cancellationToken: ct));
        var expired = await source.CopyAsync(new(before.Metadata.Version, columns, ranges), ct);
        Assert.Equal(PivotReportRefusalKind.ReportVersionNotHeld, expired.Refusal!.Kind);
        var details = await source.DetailsAsync(new(before.Metadata.Version, before.Rows[0].Key, 0), ct);
        Assert.Equal(PivotReportRefusalKind.ReportVersionNotHeld, details.Refusal!.Kind);
    }

    [Fact]
    public async Task ADR0152_explicit_words_and_order_policy_survive_serialization_and_unknown_policy_refuses()
    {
        var fields = EntryFields();
        var data = PivotSource.From<Entry>([new(1, "A", 1.25m), new(2, "BB", 2m)], fields);
        await using var source = PivotReportSource.From(data,
            new Dictionary<string, Func<object, IComparable?>> { ["long-first"] = item => -((string)item).Length });
        var settings = new PivotReportSettings { CultureName = "de-DE", Words = new Dictionary<string, string> { [PivotWords.GrandTotal] = "My Total" },
            OrderKeyPolicies = new Dictionary<string, string> { ["Label"] = "long-first" } };
        var request = new PivotReportRequest("ask", EntryLayout() with { Values = [new("Amount") { NumberFormat = "0.00" }] }, settings, new(0, 3));
        var result = PivotReportJson.Read<PivotReportUpdate>(PivotReportJson.Write(await source.WindowAsync(
            PivotReportJson.Read<PivotReportRequest>(PivotReportJson.Write(request)), TestContext.Current.CancellationToken)));
        Assert.Null(result.Refusal);
        Assert.Equal("BB", result.Rows![0].Labels[0].Text);
        Assert.Equal("1,25", result.Rows[1].Values[0]!.Text);
        Assert.Equal("My Total", result.Rows[2].Labels[0].Text);
        var denied = await source.WindowAsync(request with { Settings = settings with {
            OrderKeyPolicies = new Dictionary<string, string> { ["Label"] = "missing" } } }, TestContext.Current.CancellationToken);
        Assert.Equal(PivotReportRefusalKind.UnknownOrderKeyPolicy, denied.Refusal!.Kind);
        Assert.Equal("Label", denied.Refusal.Field);
    }

    [Fact]
    public async Task ADR0153_hidden_precision_changes_the_value_without_a_false_highlight()
    {
        var fields = EntryFields();
        var data = PivotSource.From<Entry>([new(1, "A", 1.1m)], fields);
        await using var source = PivotReportSource.From(data);
        var client = new PivotReportClient(source);
        var ct = TestContext.Current.CancellationToken;
        Assert.True(await client.ReadAsync(EntryLayout(), PivotReportSettings.Invariant, new(0, 1), cancellationToken: ct));
        var before = client.Current!.Rows[0];
        data.Apply(fields.Batch(changed: [new(1, "A", 1.2m)]));
        Assert.True(await client.ReadAsync(EntryLayout(), PivotReportSettings.Invariant, new(0, 1), cancellationToken: ct));
        var after = client.Current!.Rows[0];
        Assert.NotSame(before, after);
        Assert.Equal("1", after.Values[0]!.Text);
        Assert.Equal(1.2m, after.Values[0]!.Exact);
        Assert.Null(after.ChangedAt[0]);
    }

    [Fact]
    public async Task ADR0152_malformed_delta_and_failed_recovery_keep_the_previous_window_atomically()
    {
        var call = 0;
        var row = DisplayRow("A", 1m);
        var source = PivotReportSource.Fetch([], PivotSourceFeatures.All, PivotReportUpdateMode.Incremental,
            (request, _) => ValueTask.FromResult(++call switch
            {
                1 => PivotReportUpdate.Complete(request, OneRowMetadata("v1"), [row]),
                2 => PivotReportUpdate.Delta(request, OneRowMetadata("v2"), [new(0, DisplayRow("A", 2m)), new(0, DisplayRow("A", 3m))]),
                _ => PivotReportUpdate.Refused(request, new(PivotReportRefusalKind.SourceRefused, "Recovery unavailable.")),
            }));
        var client = new PivotReportClient(source);
        var ct = TestContext.Current.CancellationToken;
        Assert.True(await client.ReadAsync(EntryLayout(), PivotReportSettings.Invariant, new(0, 1), cancellationToken: ct));
        var previous = client.Current;
        Assert.False(await client.ReadAsync(EntryLayout(), PivotReportSettings.Invariant, new(0, 1), cancellationToken: ct));
        Assert.Same(previous, client.Current);
        Assert.Same(row, client.Current!.Rows[0]);
        Assert.Equal(1m, row.Values[0]!.Exact);
        Assert.Equal(PivotReportRefusalKind.StaleReport, client.Refusal!.Kind);
        Assert.Equal(3, call);
    }

    [Fact]
    public async Task ADR0152_a_newer_window_request_wins_even_if_the_old_reply_arrives_last()
    {
        var first = new TaskCompletionSource<PivotReportUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
        PivotReportRequest? firstRequest = null;
        var source = PivotReportSource.Fetch([], PivotSourceFeatures.All, PivotReportUpdateMode.FullRefresh,
            (request, _) =>
            {
                if (firstRequest is null) { firstRequest = request; return new(first.Task); }
                return ValueTask.FromResult(PivotReportUpdate.Complete(request, OneRowMetadata("new"), [DisplayRow("A", 2m)]));
            });
        var client = new PivotReportClient(source);
        var ct = TestContext.Current.CancellationToken;
        var old = client.ReadAsync(EntryLayout(), PivotReportSettings.Invariant, new(0, 1), cancellationToken: ct).AsTask();
        Assert.True(await client.ReadAsync(EntryLayout(), PivotReportSettings.Invariant, new(0, 1), cancellationToken: ct));
        first.SetResult(PivotReportUpdate.Complete(firstRequest!, OneRowMetadata("old"), [DisplayRow("A", 1m)]));
        Assert.False(await old);
        Assert.Equal("new", client.Current!.Metadata.Version.Value);
        Assert.Equal(2m, client.Current.Rows[0].Values[0]!.Exact);
    }

    [Fact]
    public async Task ADR0152_null_transport_payload_is_a_named_refusal_not_an_exception()
    {
        var source = PivotReportSource.Fetch([], PivotSourceFeatures.All, PivotReportUpdateMode.FullRefresh,
            (_, _) => ValueTask.FromResult<PivotReportUpdate>(null!));
        var client = new PivotReportClient(source);
        Assert.False(await client.ReadAsync(EntryLayout(), PivotReportSettings.Invariant, new(0, 1),
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(PivotReportRefusalKind.InvalidResponse, client.Refusal!.Kind);
        Assert.Null(client.Current);
    }

    private static PivotDisplayRow DisplayRow(string label, decimal value) => new(
        new(PivotRowRole.Item, -1, [PivotItemKey.Text(label)]), PivotRowRole.Item, -1, true,
        [new(label)], [new((double)value, value, null, value.ToString(System.Globalization.CultureInfo.InvariantCulture))],
        [new("Label", PivotItemKey.Text(label))]);
    private static PivotReportMetadata OneRowMetadata(string version) => new(new(version), "s-" + version, "same-rows",
        EntryLayout(), PivotReportSettings.Invariant, 1, [new("label", "Label", "Label")],
        [new("amount", "Amount", PivotColumnRole.Item, 0, [])], [], 0, ["Amount"]);

    private static PivotReportMetadata Metadata(string version) => new(
        new(version), "source-1", "rows-1", PivotLayout.Empty, PivotReportSettings.Invariant,
        0, [], [], [], 0, []);
}
