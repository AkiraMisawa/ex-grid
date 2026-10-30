using System.Text;
using System.Text.Json;
using ExPivot.Engine;
using Xunit;

namespace ExGrid.DemoApi.Tests;

public sealed class ReportWindowTests(PivotApiServer server) : IClassFixture<PivotApiServer>
{
    [Fact] // ADR-0151/0152: the server retains the report and sends only requested display rows
    public async Task Windows_operations_and_expired_state_cross_the_http_boundary()
    {
        using var http = server.Factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var path = "/api/pivot/reports/" + Guid.NewGuid().ToString("N");
        var layout = new PivotLayout
        {
            Rows = [new("Region"), new("Desk"), new("Month")],
            Values = [new("Pnl") { NumberFormat = "0.00" }],
        };
        var request = new PivotReportRequest("first", layout, PivotReportSettings.Invariant, new(0, 2));
        var first = await Post<PivotReportRequest, PivotReportUpdate>("window", request);
        Assert.Null(first.Refusal);
        Assert.Equal(2, first.Rows!.Count);
        Assert.True(first.Metadata!.RowCount > 20);
        string[] columns = [first.Metadata.ValueColumns[0].Name];
        var selection = new PivotReportCopyQuery(first.Metadata.Version, columns,
            [new(0, 0, first.Metadata.RowCount - 1, 0)]);
        var copy = await Post<PivotReportCopyQuery, PivotReportCopyResult>("copy", selection);
        Assert.Null(copy.Refusal);
        Assert.Equal(first.Metadata.RowCount, copy.Blocks[0].Rows.Count);
        var scrolled = await Post<PivotReportRequest, PivotReportUpdate>("window", request with
            { RequestId = "scroll", Window = new(10, 2), Baseline = first.Metadata.Version });
        Assert.Equal(first.Metadata.Version, scrolled.Metadata!.Version);
        Assert.Equal(2, scrolled.Rows!.Count);
        var items = await Post<PivotReportItemsQuery, PivotReportItemsResult>("items", new(first.Metadata.Version, "Region"));
        Assert.Null(items.Refusal);
        Assert.NotEmpty(items.Items);
        using (var deletion = await http.DeleteAsync(path, ct)) deletion.EnsureSuccessStatusCode();
        var expired = await Post<PivotReportCopyQuery, PivotReportCopyResult>("copy", selection);
        Assert.Equal(PivotReportRefusalKind.ReportVersionNotHeld, expired.Refusal!.Kind);
        var reset = await Post<PivotReportRequest, PivotReportUpdate>("window", request with
            { RequestId = "recover", Baseline = first.Metadata.Version });
        Assert.Null(reset.Refusal);
        Assert.NotEqual(first.Metadata.Version, reset.Metadata!.Version);
        Assert.Equal(2, reset.Rows!.Count);

        async Task<TAnswer> Post<TQuestion, TAnswer>(string operation, TQuestion question)
        {
            using var content = new StringContent(PivotReportJson.Write(question), Encoding.UTF8, "application/json");
            using var response = await http.PostAsync(path + "/" + operation, content, ct);
            response.EnsureSuccessStatusCode();
            return PivotReportJson.Read<TAnswer>(await response.Content.ReadAsStringAsync(ct));
        }
    }

    [Fact] // LV-34/FN-3a, SRV-2: a test holds a Window question at the demo API's gate, from either host, and reads the Window it named
    public async Task A_window_question_waits_at_a_held_gate_until_it_is_opened_and_reset_forgets_it()
    {
        using var http = server.Factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var path = "/api/pivot/reports/" + Guid.NewGuid().ToString("N") + "/window";
        var request = new PivotReportRequest("held", new PivotLayout { Rows = [new("Region")], Values = [new("Pnl")] },
            PivotReportSettings.Invariant, new(3, 4));
        using (var reset = await http.PostAsync("/api/reset", null, ct)) reset.EnsureSuccessStatusCode();
        Assert.Empty((await Gate()).GetProperty("windows").EnumerateArray());

        using (var hold = await http.PostAsync("/api/pivot/window-gate", Json("""{ "hold": true }"""), ct)) hold.EnsureSuccessStatusCode();
        var asked = http.PostAsync(path, new StringContent(PivotReportJson.Write(request), Encoding.UTF8, "application/json"), ct);
        // Recorded as it arrives, before it waits: once the Window is read back, the question is at the gate.
        JsonElement state;
        do state = await Gate(); while (state.GetProperty("windows").GetArrayLength() == 0 && !asked.IsCompleted);
        Assert.True(state.GetProperty("hold").GetBoolean());
        Assert.Equal(3, state.GetProperty("windows")[0].GetProperty("start").GetInt32());
        Assert.Equal(4, state.GetProperty("windows")[0].GetProperty("count").GetInt32());
        Assert.False(asked.IsCompleted);

        using (var open = await http.PostAsync("/api/pivot/window-gate", Json("""{ "hold": false }"""), ct)) open.EnsureSuccessStatusCode();
        using (var answer = await asked)
        {
            answer.EnsureSuccessStatusCode();
            Assert.Null(PivotReportJson.Read<PivotReportUpdate>(await answer.Content.ReadAsStringAsync(ct)).Refusal);
        }
        using (var reset = await http.PostAsync("/api/reset", null, ct)) reset.EnsureSuccessStatusCode();
        Assert.Empty((await Gate()).GetProperty("windows").EnumerateArray());

        async Task<JsonElement> Gate() => JsonDocument.Parse(await http.GetStringAsync("/api/pivot/window-gate", ct)).RootElement;
        static StringContent Json(string text) => new(text, Encoding.UTF8, "application/json");
    }

    [Fact] // LV-27: the gate refuses questions for a complete Window with 503 and spoils only Window Changes, by count, records which questions named a Baseline, and reset forgets it all
    public async Task The_gate_fails_and_spoils_by_count_and_reset_forgets_it()
    {
        using var http = server.Factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var path = "/api/pivot/reports/" + Guid.NewGuid().ToString("N") + "/window";
        var request = new PivotReportRequest("first", new PivotLayout { Rows = [new("Region")], Values = [new("Pnl")] },
            PivotReportSettings.Invariant, new(0, 3));
        using (var reset = await http.PostAsync("/api/reset", null, ct)) reset.EnsureSuccessStatusCode();
        using (var arm = await http.PostAsync("/api/pivot/window-gate", Json("""{ "fail": 1, "spoil": 1 }"""), ct)) arm.EnsureSuccessStatusCode();

        using (var refused = await Ask(request))
            Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, refused.StatusCode);
        PivotReportUpdate first;
        using (var answered = await Ask(request))
        {
            answered.EnsureSuccessStatusCode();
            first = PivotReportJson.Read<PivotReportUpdate>(await answered.Content.ReadAsStringAsync(ct));
        }
        // A complete Window is never spoiled: its digest is its rows'.
        Assert.Equal(PivotReportDigest.Of(first.Metadata!, 0, first.Rows!), first.WindowDigest);
        // Asked again from that Window, the answer is Window Changes, none of them here, and spoiled.
        using (var again = await Ask(request with { RequestId = "again", Baseline = first.Metadata!.Version }))
        {
            again.EnsureSuccessStatusCode();
            var changes = PivotReportJson.Read<PivotReportUpdate>(await again.Content.ReadAsStringAsync(ct));
            Assert.Null(changes.Rows);
            Assert.Equal("spoiled-" + first.WindowDigest, changes.WindowDigest);
        }

        var state = await Gate();
        Assert.Equal(0, state.GetProperty("fail").GetInt32());
        Assert.Equal(1, state.GetProperty("failed").GetInt32());
        Assert.Equal(2, state.GetProperty("complete").GetInt32());
        Assert.Equal(0, state.GetProperty("spoil").GetInt32());
        Assert.Equal(1, state.GetProperty("spoiled").GetInt32());
        Assert.Equal([false, false, true], state.GetProperty("baselined").EnumerateArray().Select(b => b.GetBoolean()));

        using (var reset = await http.PostAsync("/api/reset", null, ct)) reset.EnsureSuccessStatusCode();
        state = await Gate();
        Assert.Equal(0, state.GetProperty("spoiled").GetInt32());
        Assert.Equal(0, state.GetProperty("complete").GetInt32());
        Assert.Empty(state.GetProperty("baselined").EnumerateArray());

        Task<HttpResponseMessage> Ask(PivotReportRequest question)
            => http.PostAsync(path, new StringContent(PivotReportJson.Write(question), Encoding.UTF8, "application/json"), ct);
        async Task<JsonElement> Gate() => JsonDocument.Parse(await http.GetStringAsync("/api/pivot/window-gate", ct)).RootElement;
        static StringContent Json(string text) => new(text, Encoding.UTF8, "application/json");
    }
}
