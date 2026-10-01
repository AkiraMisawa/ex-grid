using ExGrid.Data;
using Xunit;
using static ExGrid.Data.Arrow.Tests.Fixtures;

namespace ExGrid.Data.Arrow.Tests;

/// <summary>
/// Streams pyarrow, Polars and DuckDB wrote, kept as they came under <c>Producers/</c> with the script
/// that made them (DA-14): what a data pipeline hands a server is read under the Snapshot's rules.
/// </summary>
public class ProducerTests
{
    [Fact] // ADR-0064: pyarrow's delta dictionaries — each batch's dictionary extending the one before, out of order, amer beside AMER, a null entry, a null index — are read under the Snapshot's rules
    public async Task Pyarrows_delta_dictionaries_are_read_under_the_snapshots_rules()
    {
        var snapshot = await ReadEveryWayAsync(Produced("pyarrow-deltas.arrows"));

        Assert.Equal(Shown("AMER", "emea", null, null, "AMER", "amer", "APAC", "AMER", "emea", "Amer", "amer"), Values(snapshot, "region"));
        Assert.Equal(["AMER", "emea", "amer", "APAC", "Amer"], Dictionary(snapshot, "region"));
        Assert.Equal(Shown(1L, 2L, 3L, 4L, 5L, 6L, 7L, 8L, 9L, 10L, 11L), Values(snapshot, "n"));
    }

    [Fact] // ADR-0064: a dictionary pyarrow replaced between record batches is read by the batches after it
    public async Task Pyarrows_replaced_dictionary_is_read_by_the_batches_after_it()
    {
        var snapshot = await ReadEveryWayAsync(Produced("pyarrow-replaced.arrows"));

        Assert.Equal(Shown("b", "a", "a", "c", "a", "b", null), Values(snapshot, "region"));
        Assert.Equal(["b", "a", "c"], Dictionary(snapshot, "region"));
    }

    [Fact] // ADR-0064: pyarrow's dictionaries with indices of every integer width, over utf8 and large_utf8, are Text
    public async Task Pyarrows_indices_of_every_width_are_read()
    {
        var snapshot = await ReadEveryWayAsync(Produced("pyarrow-indices.arrows"));

        foreach (var column in new[] { "i8", "i16", "u8", "u16", "u32", "i64", "u64" })
        {
            Assert.Equal(SnapshotKind.Text, snapshot[column].Kind);
            Assert.Equal(Shown("x", "z", null, "y"), Values(snapshot, column));
            Assert.Equal(["x", "z", "y"], Dictionary(snapshot, column));
        }
    }

    [Fact] // ADR-0064: pyarrow's Arrow file with ZSTD buffers is refused without a codec, naming ZSTD, and read with one
    public async Task Pyarrows_zstd_file_needs_the_codec()
    {
        var file = Produced("pyarrow-zstd.arrow");

        var refusal = await RefusalAsync(file);
        var snapshot = await ReadEveryWayAsync(file, Codecs);

        Assert.Contains("compressed with ZSTD", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(Shown("Rates", null, "FX", "Rates"), Values(snapshot, "desk"));
        Assert.Equal(Shown(1.5m, -2.25m, null, 1_000_000.01m), Values(snapshot, "pnl"));
    }

    [Fact] // ADR-0064: Polars' stream for older readers — large_utf8, a Categorical over large_utf8 with uint32 indices, its numbers, dates, decimals and booleans, a timestamp in UTC — is read whole
    public async Task Polars_stream_for_older_readers_is_read()
    {
        var snapshot = await ReadEveryWayAsync(Produced("polars-oldest.arrows"));

        Assert.Equal(Shown("amer", "AMER", null, "emea"), Values(snapshot, "region"));
        Assert.Equal(Shown("b", "a", "b", null), Values(snapshot, "category"));
        Assert.Equal(["b", "a"], Dictionary(snapshot, "category"));
        Assert.Equal(Shown(1.5, -2.0, null, double.PositiveInfinity), Values(snapshot, "amount"));
        Assert.Equal(Shown(1L, null, 3L, -4L), Values(snapshot, "quantity"));
        Assert.Equal(Shown(new DateTime(2026, 1, 1, 12, 30, 0), null, new DateTime(2026, 1, 2), new DateTime(2026, 1, 3).AddMicroseconds(123_456)), Values(snapshot, "when"));
        Assert.Equal(Shown(new DateTime(2026, 1, 1), null, new DateTime(2026, 1, 2), new DateTime(1969, 12, 31)), Values(snapshot, "day"));
        Assert.Equal(Shown(1.5m, null, -2.25m, 0m), Values(snapshot, "pnl"));
        Assert.Equal(Shown(true, false, null, true), Values(snapshot, "live"));
        Assert.All(Values(snapshot, "utc"), v => Assert.Equal(Show(new DateTime(2026, 1, 1, 12, 0, 0)), v));
        Assert.Equal(SnapshotKind.Date, snapshot["utc"].Kind);
    }

    [Fact] // ADR-0064: Polars' default stream, its text as utf8_view, is read as Text — the same values Polars writes for older readers as large_utf8
    public async Task Polars_default_stream_is_read_with_its_utf8_view()
    {
        var snapshot = await ReadEveryWayAsync(Produced("polars-default.arrows"));
        var oldest = await ReadEveryWayAsync(Produced("polars-oldest.arrows"));

        Assert.Equal(SnapshotKind.Text, snapshot["region"].Kind);
        Assert.Equal(Shown("amer", "AMER", null, "emea"), Values(snapshot, "region"));
        Assert.Equal(["amer", "AMER", "emea"], Dictionary(snapshot, "region"));
        Assert.Equal(Values(oldest, "region"), Values(snapshot, "region"));
    }

    [Fact] // ADR-0064: DuckDB's stream — an ENUM as a dictionary with uint8 indices, decimals, a TIMESTAMPTZ in Etc/UTC, every TIMESTAMP unit, a HUGEINT and a UBIGINT within range — is read whole
    public async Task Duckdbs_stream_is_read()
    {
        var snapshot = await ReadEveryWayAsync(Produced("duckdb.arrows"));
        var noon = new DateTime(2026, 1, 1, 12, 0, 0);

        Assert.Equal(Shown("FX", null, "Rates"), Values(snapshot, "desk"));
        Assert.Equal(["FX", "Rates"], Dictionary(snapshot, "desk"));
        Assert.Equal(Shown(1.5m, null, -0.01m), Values(snapshot, "pnl"));
        Assert.Equal(Shown(12_345_678_901_234_567_890.12m, null, -1m), Values(snapshot, "big"));
        Assert.Equal(Shown(noon, null, new DateTime(1999, 12, 31, 23, 59, 59)), Values(snapshot, "tstz"));
        Assert.Equal(Shown(noon.AddMicroseconds(123_456), null, DateTime.UnixEpoch), Values(snapshot, "ts"));
        Assert.Equal(Shown(noon.AddSeconds(1), null, DateTime.UnixEpoch), Values(snapshot, "ts_s"));
        Assert.Equal(Shown(noon.AddMilliseconds(500), null, DateTime.UnixEpoch), Values(snapshot, "ts_ms"));
        Assert.Equal(Shown(noon.AddTicks(1), null, DateTime.UnixEpoch), Values(snapshot, "ts_ns"));
        Assert.Equal(Shown(new DateTime(2026, 1, 1), null, DateTime.UnixEpoch), Values(snapshot, "d"));
        Assert.Equal(Shown(true, null, false), Values(snapshot, "b"));
        Assert.Equal(Shown(42L, null, -42L), Values(snapshot, "n"));
        Assert.Equal(Shown(12_345_678_901_234_567_890_123m, null, -1m), Values(snapshot, "h"));
        Assert.Equal(Shown(long.MaxValue, null, 0L), Values(snapshot, "u"));
        Assert.Equal(SnapshotKind.Integer, snapshot["u"].Kind);
        Assert.Equal(SnapshotKind.Decimal, snapshot["h"].Kind);
    }
}
