using ExGrid.Data;
using ExGrid.Data.Arrow;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace PackageSmoke;

/// <summary>
/// ExGrid.Data.Arrow's README examples, compiled against the packed package as a browser application
/// takes it (ADR-0042, ADR-0065). Not run here: <see cref="ArrowRoundTrip"/> runs a stream through the
/// packed packages, and what a read and a write do is their own suite's.
/// </summary>
internal static class ArrowSmoke
{
    public static async Task<Snapshot> Fetch(HttpClient http, IProgress<SnapshotProgress> progress, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "trades.arrows");
        request.SetBrowserResponseStreamingEnabled(false);
        using var response = await http.SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        byte[] payload = await response.Content.ReadAsByteArrayAsync(token);

        Snapshot snapshot = await SnapshotArrow.ReadAsync(payload, new SnapshotLoadOptions { Progress = progress }, cancellationToken: token);
        return snapshot;
    }

    public static async Task<Snapshot> Stream(HttpResponseMessage response, CancellationToken token)
    {
        await using var body = await response.Content.ReadAsStreamAsync(token);
        Snapshot snapshot = await SnapshotArrow.ReadAsync(body, cancellationToken: token);
        return snapshot;
    }

    public static async Task<string> Answer(Snapshot snapshot, Stream body, CancellationToken token)
    {
        await SnapshotArrow.WriteAsync(snapshot, body, token);
        return SnapshotArrow.StreamMediaType + SnapshotArrowMetadata.Caption + SnapshotArrowMetadata.RecordKey + SnapshotArrowMetadata.Version;
    }
}
