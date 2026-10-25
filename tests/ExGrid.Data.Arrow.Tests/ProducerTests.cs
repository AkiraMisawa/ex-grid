using ExGrid.Data;
using Xunit;
using static ExGrid.Data.Arrow.Tests.Fixtures;

namespace ExGrid.Data.Arrow.Tests;

/// <summary>
/// Streams pyarrow, Polars and DuckDB wrote, kept as they came under <c>tests/cases/arrow/producers/</c>
/// with the script that made them (DA-14). The shared cases read them; this reads the one that needs a
/// codec, which only ExGrid.Data.Arrow can be handed.
/// </summary>
public class ProducerTests
{
    [Fact] // ADR-0065: pyarrow's Arrow file with ZSTD buffers is refused without a codec, naming ZSTD, and read with one
    public async Task Pyarrows_zstd_file_needs_the_codec()
    {
        var file = Produced("pyarrow-zstd.arrow");

        var refusal = await RefusalAsync(file);
        var snapshot = await ReadEveryWayAsync(file, Codecs);

        Assert.Contains("compressed with ZSTD", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(Shown("Rates", null, "FX", "Rates"), Values(snapshot, "desk"));
        Assert.Equal(Shown(1.5m, -2.25m, null, 1_000_000.01m), Values(snapshot, "pnl"));
    }
}
