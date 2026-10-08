using ExPivot.Engine;

namespace PackageSmoke;

// Run from a trimmed, reflection-disabled Consumer against the packed engine. No assembly roots
// are declared: generated protocol metadata must preserve exactly what these messages require.
internal static class PivotReportRoundTrip
{
    private sealed record Trade(long Id, string Desk, decimal Amount);
    private static T Wire<T>(T value) => PivotReportJson.Read<T>(PivotReportJson.Write(value));
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Report protocol: " + message);
    }

    public static async Task RunAsync()
    {
        var fields = PivotFields.Of<Trade>().Key("Id", row => row.Id).Text("Desk", row => row.Desk).Number("Amount", row => row.Amount);
        var data = PivotSource.From<Trade>([new(1, "Rates", 0.1m), new(2, "Rates", 0.2m)], fields);
        await using var source = PivotReportSource.From(data,
            new Dictionary<string, Func<object, IComparable?>> { ["desk-order"] = item => (string)item });
        var layout = new PivotLayout { Rows = [new("Desk")], Values = [new("Amount") { NumberFormat = "0.00" }] };
        var settings = Wire(new PivotReportSettings
        {
            CultureName = "de-DE",
            ChangeHighlightDuration = TimeSpan.FromSeconds(3),
            Words = new Dictionary<string, string> { [PivotWords.GrandTotal] = "All desks" },
            OrderKeyPolicies = new Dictionary<string, string> { ["Desk"] = "desk-order" },
            LabelMetrics = new(9, 7, 3, 14, 6, 4, new Dictionary<int, double> { ['R'] = 8.5 }),
        });
        Require(settings.LabelMetrics!.GlyphWidths['R'] == 8.5 && settings.LabelMetrics.EstimatePx("R") == 16.5,
            "glyph overrides changed");
        Require(settings.ChangeHighlightDuration == TimeSpan.FromSeconds(3)
            && settings.OrderKeyPolicies["Desk"] == "desk-order", "explicit settings changed");
        var query = Wire(new PivotReportRequest("first", layout, settings, new(0, 1)));
        var first = Wire(await source.WindowAsync(query));
        Require(first.Rows is { Count: 1 } && first.Metadata!.RowCount == 2, "the Window lost its full extent");
        Require(first.Rows![0].ValueAt(0)!.Exact == 0.3m, "the exact value changed");
        Require(first.Rows![0].ValueAt(0)!.Text == "0,30" && first.Metadata!.LabelWidths.Count == 1,
            "explicit culture or label geometry changed");
        data.Apply(fields.Batch(changed: [new(1, "Rates", 0.4m)]));
        var delta = Wire(await source.WindowAsync(query with { RequestId = "next", Baseline = first.Metadata!.Version }));
        Require(delta.Rows is null && delta.Changes.Count == 1 && delta.Changes[0].Row.ValueAt(0)!.Exact == 0.6m, "the delta changed");
        Require(delta.WindowDigest is { Length: 16 } && first.WindowDigest is { Length: 16 }
            && PivotReportDigest.Of(delta.Metadata!, 0, [delta.Changes[0].Row]) == delta.WindowDigest
            && PivotReportDigest.Of(first.Metadata!, 0, first.Rows!) == first.WindowDigest, "the Window digest changed");
        var version = delta.Metadata!.Version;
        string[] columns = [delta.Metadata.ValueColumns[0].Name];
        PivotReportRange[] ranges = [new(0, 0, 1, 0)];
        var copy = Wire(await source.CopyAsync(Wire(new PivotReportCopyQuery(version, columns, ranges))));
        Require(copy.Refusal is null && copy.Blocks[0].Rows.Count == 2 && copy.Blocks[0].Rows[1][0].Raw == "0.6", "offscreen Copy changed");
        var summary = Wire(await source.SummaryAsync(Wire(new PivotReportSummaryQuery(version, columns, ranges))));
        Require(summary.Counts.Numbers == 2 && summary.Sum.Exact == 1.2m, "Summary parts changed");
        var items = Wire(await source.ItemsAsync(Wire(new PivotReportItemsQuery(version, "Desk"))));
        Require(items.Items is [{ Label: "Rates" }], "Items changed");
        var rawItems = Wire(await source.RawItemsAsync(Wire(new PivotItemsQuery("Desk", delta.Metadata.SourceVersion))));
        Require(rawItems.Items.Count == 1, "raw Items changed");
        var details = Wire(await source.DetailsAsync(Wire(delta.Metadata.DetailsQuery(delta.Changes[0].Row, 0, 0, 10))));
        Require(details.Records.Count == 2 && details.SourceVersion == delta.Metadata.SourceVersion, "Details changed");
        var refused = Wire(PivotReportUpdate.Refused(query, new(PivotReportRefusalKind.ReportVersionNotHeld, "Expired")));
        Require(refused.Refusal?.Kind == PivotReportRefusalKind.ReportVersionNotHeld, "the refusal changed");
        Console.WriteLine("Report protocol: trimmed packed Consumer preserves Windows, deltas and every versioned operation");
    }
}
