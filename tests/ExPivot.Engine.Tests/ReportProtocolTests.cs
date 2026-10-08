using ExPivot.Engine;
using Xunit;

namespace ExPivot.Engine.Tests;

public class ReportProtocolTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ADR0152_a_reply_superseded_during_validation_cannot_publish_state_or_refusal(bool malformed)
    {
        PivotReportClient client = null!;
        var requests = 0;
        var newerAdopted = false;
        var source = PivotReportSource.Fetch([], PivotSourceFeatures.All, PivotReportUpdateMode.FullRefresh,
            (request, _) =>
            {
                var first = ++requests == 1;
                var metadata = Metadata(first ? "older" : "newer");
                if (first) metadata = metadata with { LabelWidths = new InterleavedWidths(malformed ? [double.NaN] : [], () =>
                {
                    newerAdopted = client.ReadAsync(PivotLayout.Empty, PivotReportSettings.Invariant, new(0, 10),
                        cancellationToken: TestContext.Current.CancellationToken).AsTask().GetAwaiter().GetResult();
                }) };
                return ValueTask.FromResult(PivotReportUpdate.Complete(request, metadata, []));
            });
        client = new(source);
        Assert.False(await client.ReadAsync(PivotLayout.Empty, PivotReportSettings.Invariant, new(0, 10),
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(newerAdopted);
        Assert.Equal("newer", client.Current!.Metadata.Version.Value);
        Assert.Null(client.Refusal);
        Assert.Equal(2, requests);
    }

    // A provider may complete another request while an older response's lists are validated.
    // This deterministically interleaves that publication without delays or scheduler races.
    private sealed class InterleavedWidths(double[] values, Action interleave) : IReadOnlyList<double>
    {
        private Action? _interleave = interleave;
        public int Count { get { Interlocked.Exchange(ref _interleave, null)?.Invoke(); return values.Length; } }
        public double this[int index] => values[index];
        public IEnumerator<double> GetEnumerator() => ((IEnumerable<double>)values).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public async Task ADR0152_a_window_must_echo_the_requested_highlight_duration()
    {
        var source = PivotReportSource.Fetch([], PivotSourceFeatures.All, PivotReportUpdateMode.FullRefresh,
            (request, _) => ValueTask.FromResult(PivotReportUpdate.Complete(request, Metadata("v1") with {
                Settings = request.Settings with { ChangeHighlightDuration = TimeSpan.FromSeconds(9) } }, [])));
        var client = new PivotReportClient(source);
        Assert.False(await client.ReadAsync(PivotLayout.Empty, PivotReportSettings.Invariant, new(0, 10),
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Null(client.Current);
        Assert.Equal(PivotReportRefusalKind.InvalidResponse, client.Refusal!.Kind);
    }

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
        // Details names the Source Version, not the Report Version (ADR-0151): the provider still
        // holds the data the earlier report was computed from, so its records still add up to it.
        var details = await source.DetailsAsync(before.Metadata.DetailsQuery(before.Rows[0], 0), ct);
        Assert.False(details.IsRefused);
        Assert.Equal(before.Metadata.SourceVersion, details.SourceVersion);
        Assert.Equal(10m, Assert.Single(details.Records).Values[^1]);
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
        var remote = PivotReportSource.Fetch(source.Fields, source.Features, source.UpdateMode, source.WindowAsync,
            reportItems: async (query, ct) => PivotReportJson.Read<PivotReportItemsResult>(PivotReportJson.Write(
                await source.ItemsAsync(PivotReportJson.Read<PivotReportItemsQuery>(PivotReportJson.Write(query)), ct))));
        var items = await remote.ItemsAsync(new PivotReportItemsQuery(result.Metadata!.Version, "Label"), TestContext.Current.CancellationToken);
        Assert.Null(items.Refusal);
        Assert.Equal(["BB", "A"], items.Items.Select(item => item.Label));
        Assert.Equal(result.Metadata.SourceVersion, items.SourceVersion);
        var denied = await source.WindowAsync(request with { Settings = settings with {
            OrderKeyPolicies = new Dictionary<string, string> { ["Label"] = "missing" } } }, TestContext.Current.CancellationToken);
        Assert.Equal(PivotReportRefusalKind.UnknownOrderKeyPolicy, denied.Refusal!.Kind);
        Assert.Equal("Label", denied.Refusal.Field);
    }

    [Fact]
    public async Task ADR0153_unchanged_child_label_keeps_the_current_parent_spelling_in_details()
    {
        var fields = EntryFields().Text("Child", _ => "child");
        var data = PivotSource.From<Entry>([new(1, "Parent", 1m)], fields);
        await using var source = PivotReportSource.From(data);
        var client = new PivotReportClient(source);
        var layout = EntryLayout() with { Rows = [new("Label"), new("Child")] };
        var ct = TestContext.Current.CancellationToken;
        Assert.True(await client.ReadAsync(layout, PivotReportSettings.Invariant, new(0, 10), cancellationToken: ct));
        var before = client.Current!.Rows.Single(row => row.RowPath.Count == 2);
        data.Apply(fields.Batch(changed: [new(1, "PARENT", 1m)]));
        Assert.True(await client.ReadAsync(layout, PivotReportSettings.Invariant, new(0, 10), cancellationToken: ct));
        var after = client.Current!.Rows.Single(row => row.RowPath.Count == 2);
        Assert.Equal("Parent", before.RowPath[0].Item.Value);
        Assert.Equal("PARENT", after.RowPath[0].Item.Value);
        Assert.Equal(before.Labels, after.Labels);
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
        Assert.Null(after.ChangedIn[0]);
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

    [Fact]
    public async Task ADR0152_remote_operations_reject_answers_for_a_different_report_version()
    {
        var requested = new PivotReportVersion("selected");
        var wrong = new PivotReportVersion("latest");
        var source = PivotReportSource.Fetch([], PivotSourceFeatures.All, PivotReportUpdateMode.FullRefresh,
            (request, _) => ValueTask.FromResult(PivotReportUpdate.Complete(request, Metadata("x"), [])),
            copy: (_, _) => ValueTask.FromResult(new PivotReportCopyResult(wrong, [])),
            summary: (_, _) => ValueTask.FromResult(new PivotReportSummaryResult(wrong, default, default, default, false, null, "")),
            details: (query, _) => ValueTask.FromResult(new PivotDetailPage("another-source-version", [], query.Start, 0, [])));
        var ct = TestContext.Current.CancellationToken;
        Assert.Equal(PivotReportRefusalKind.InvalidResponse, (await source.CopyAsync(new(requested, [], []), ct)).Refusal!.Kind);
        Assert.Equal(PivotReportRefusalKind.InvalidResponse, (await source.SummaryAsync(new(requested, [], []), ct)).Refusal!.Kind);
        // Records of another Source Version would not add up to the cell: never shown.
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await source.DetailsAsync(new PivotDetailsQuery("source-1"), ct));
    }

    [Theory]
    [InlineData(PivotReportForm.Compact, PivotShowValuesAs.NoCalculation, PivotAxis.Columns)]
    [InlineData(PivotReportForm.Outline, PivotShowValuesAs.PercentOfGrandTotal, PivotAxis.Rows)]
    [InlineData(PivotReportForm.Tabular, PivotShowValuesAs.PercentOfColumnTotal, PivotAxis.Columns)]
    [InlineData(PivotReportForm.Tabular, PivotShowValuesAs.PercentOfRowTotal, PivotAxis.Rows)]
    public async Task ADR0151_serialized_windows_match_fresh_engine_report_forms_and_percentages(
        PivotReportForm form, PivotShowValuesAs show, PivotAxis axis)
    {
        var data = PivotSource.From(Pivot.Sales, Pivot.Fields);
        await using var source = PivotReportSource.From(data);
        var layout = new PivotLayout { Rows = [new("Region"), new("Product")], Columns = [new("Online")],
            Values = [new("Amount") { ShowValuesAs = show }, new("Quantity", PivotAggregation.CountNumbers)],
            Form = form, ValuesAxis = axis, RepeatItemLabels = true };
        var expected = Pivot.Report(layout, options: new() { Culture = System.Globalization.CultureInfo.InvariantCulture });
        var request = new PivotReportRequest("forms", layout, PivotReportSettings.Invariant, new(1, 3));
        var actual = PivotReportJson.Read<PivotReportUpdate>(PivotReportJson.Write(await source.WindowAsync(request, TestContext.Current.CancellationToken)));
        Assert.Equal(expected.Rows.Count, actual.Metadata!.RowCount);
        Assert.Equal(expected.HeaderSpans, actual.Metadata.HeaderSpans);
        Assert.Equal(expected.ValueColumns.Select(c => (c.Name, c.Header, c.Role, c.ValueField)),
            actual.Metadata.ValueColumns.Select(c => (c.Name, c.Header, c.Role, c.ValueField)));
        for (var i = 0; i < actual.Rows!.Count; i++)
        {
            var row = expected.Rows[i + 1];
            Assert.Equal(row.Key, actual.Rows[i].Key);
            Assert.Equal(row.Labels, actual.Rows[i].Labels);
            for (var col = 0; col < expected.ValueColumns.Count; col++)
            {
                var value = expected.ValueAt(row, col);
                Assert.Equal(value?.Text, actual.Rows[i].ValueAt(col)?.Text);
                Assert.Equal(value?.Exact, actual.Rows[i].ValueAt(col)?.Exact);
                Assert.Equal(value?.Number, actual.Rows[i].ValueAt(col)?.Number);
                Assert.Equal(value?.Error, actual.Rows[i].ValueAt(col)?.Error);
            }
        }
        var client = new PivotReportClient(PivotReportSource.Fetch(data.Fields, data.Features, source.UpdateMode,
            async (q, ct) => PivotReportJson.Read<PivotReportUpdate>(PivotReportJson.Write(await source.WindowAsync(q, ct)))));
        Assert.True(await client.ReadAsync(layout, PivotReportSettings.Invariant, new(1, 3), cancellationToken: TestContext.Current.CancellationToken), client.Refusal?.Message);
    }

    [Fact]
    public async Task ADR0152_copy_summary_items_and_details_round_trip_without_rebinding_or_boxing_decimal_as_double()
    {
        var data = PivotSource.From(Pivot.Sales, Pivot.Fields);
        await using var source = PivotReportSource.From(data);
        var ct = TestContext.Current.CancellationToken;
        var window = await source.WindowAsync(new("first", Pivot.RowsBy("Region"), PivotReportSettings.Invariant, new(0, 1)), ct);
        var version = window.Metadata!.Version;
        string[] columns = [window.Metadata.ValueColumns[0].Name];
        var copyRequest = new PivotReportCopyQuery(version, columns, [new(1, 0, 3, 0)]);
        var copy = PivotReportJson.Read<PivotReportCopyResult>(PivotReportJson.Write(await source.CopyAsync(
            PivotReportJson.Read<PivotReportCopyQuery>(PivotReportJson.Write(copyRequest)), ct)));
        Assert.Equal(new[] { "10", "90", "5" }, copy.Blocks[0].Rows.Select(r => r[0].Raw));
        var summary = PivotReportJson.Read<PivotReportSummaryResult>(PivotReportJson.Write(await source.SummaryAsync(new(version, columns, [new(1, 0, 3, 0)]), ct)));
        Assert.Equal(105m, summary.Sum.Exact);
        Assert.Equal(3, summary.Counts.Numbers);
        var query = window.Metadata.DetailsQuery(window.Rows![0], 0, 0, 10);
        var details = PivotReportJson.Read<PivotDetailPage>(PivotReportJson.Write(await source.DetailsAsync(
            PivotReportJson.Read<PivotDetailsQuery>(PivotReportJson.Write(query)), ct)));
        Assert.Equal(3, details.Total);
        Assert.All(details.Records, row => Assert.Null(row.Record));
        Assert.Equal(100m, details.Records[0].Values[3]);
        var items = PivotReportJson.Read<PivotItemPage>(PivotReportJson.Write(await source.RawItemsAsync(
            new("Region", window.Metadata.SourceVersion), ct)));
        Assert.Equal(4, items.Total);
    }

    [Fact]
    public async Task ADR0153_disposing_the_report_does_not_dispose_its_provider_and_rejects_more_report_work()
    {
        var fields = EntryFields();
        var data = PivotSource.From<Entry>([new(1, "A", 1m)], fields);
        var source = PivotReportSource.From(data);
        var ct = TestContext.Current.CancellationToken;
        var first = await source.WindowAsync(new("first", EntryLayout(), PivotReportSettings.Invariant, new(0, 1)), ct);
        await source.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await source.CopyAsync(new(first.Metadata!.Version, [], []), ct));
        data.Apply(fields.Batch(changed: [new(1, "A", 2m)]));
        await using var other = PivotReportSource.From(data);
        Assert.Equal(2m, (await other.WindowAsync(new("next", EntryLayout(), PivotReportSettings.Invariant, new(0, 1)), ct)).Rows![0].Values[0]!.Exact);
    }

    [Fact]
    public async Task ADR0152_published_operations_do_not_wait_for_a_new_report_computation()
    {
        var data = PivotSource.From<Entry>([new(1, "A", 10m)], EntryFields());
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var provider = PivotSource.Fetch(data.Fields, data.Features, async (query, ct) =>
        {
            if (++calls > 1) { entered.SetResult(); await release.Task.WaitAsync(ct); }
            return await data.AggregateAsync(query, ct);
        }, data.ItemsAsync, data.DetailsAsync);
        await using var source = PivotReportSource.From(provider);
        var ct = TestContext.Current.CancellationToken;
        var request = new PivotReportRequest("first", EntryLayout(), PivotReportSettings.Invariant, new(0, 1));
        var first = await source.WindowAsync(request, ct);
        var newer = source.WindowAsync(request with { RequestId = "next", RefreshData = true }, ct).AsTask();
        await entered.Task.WaitAsync(ct);
        try
        {
            var version = first.Metadata!.Version;
            string[] columns = [first.Metadata.ValueColumns[0].Name];
            var copying = source.CopyAsync(new(version, columns, [new(0, 0, 1, 0)]), ct);
            Assert.True(copying.IsCompleted, "A published report must be readable while its successor waits for data.");
            Assert.Equal("10", (await copying).Blocks[0].Rows[1][0].Raw);
            Assert.Equal(20m, (await source.SummaryAsync(new(version, columns, [new(0, 0, 1, 0)]), ct)).Sum.Exact);
            var details = await source.DetailsAsync(first.Metadata.DetailsQuery(first.Rows![0], 0), ct);
            Assert.Equal(first.Metadata.SourceVersion, details.SourceVersion);
        }
        finally { release.TrySetResult(); await newer; }
    }

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact] // ADR-0153/0068: a change is stamped on the client's clock when a report listing it is first adopted; scrolling to an off-screen changed row later shows it as of that time, on any server clock
    public async Task ADR0153_scrolling_keeps_offscreen_changes_at_their_first_shown_time()
    {
        var fields = EntryFields();
        var data = PivotSource.From<Entry>([new(1, "A", 10m), new(2, "B", 20m)], fields);
        // The server's clock is a day and a bit away from the client's: neither is read for the other.
        var server = new ManualClock { Now = new(2026, 10, 7, 11, 59, 55, TimeSpan.Zero) };
        var now = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
        await using var source = PivotReportSource.From(data, timeProvider: server);
        var client = new PivotReportClient(source);
        var times = new PivotChangeTimes();
        var ct = TestContext.Current.CancellationToken;
        async Task ReadAsync(int start)
        {
            Assert.True(await client.ReadAsync(EntryLayout(), PivotReportSettings.Invariant, new(start, 1), cancellationToken: ct));
            times.Adopt(client.Current!, now, TimeSpan.FromSeconds(1));
        }
        await ReadAsync(0);
        server.Now += TimeSpan.FromMilliseconds(100);
        now += TimeSpan.FromMilliseconds(100);
        var shownAt = now;
        data.Apply(fields.Batch(changed: [new(2, "B", 25m)]));
        await ReadAsync(0);
        server.Now += TimeSpan.FromMilliseconds(100);
        now += TimeSpan.FromMilliseconds(100);
        data.Apply(fields.Batch(changed: [new(1, "A", 11m)]));
        await ReadAsync(0);
        Assert.Equal(now, times.ChangedAt(client.Current!.Rows[0], 0));
        await ReadAsync(1);
        Assert.Equal(shownAt, times.ChangedAt(client.Current!.Rows[0], 0));
        var row = client.Current.Rows[0];
        server.Now += TimeSpan.FromMilliseconds(100);
        now += TimeSpan.FromMilliseconds(100);
        await ReadAsync(0);
        await ReadAsync(1);
        Assert.Equal(shownAt, times.ChangedAt(client.Current!.Rows[0], 0));
        Assert.Equal(25m, row.Values[0]!.Exact);
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
