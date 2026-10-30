using System.Buffers.Binary;
using Apache.Arrow;
using Apache.Arrow.Ipc;
using Apache.Arrow.Types;
using ExGrid.Data;
using Xunit;
using static ExGrid.Data.Arrow.Tests.Fixtures;

namespace ExGrid.Data.Arrow.Tests;

/// <summary>
/// What a read takes from the Consumer's Stream that the shared cases (<see cref="Cases.ArrowCaseTests"/>)
/// cannot hand it: an Arrow file from a stream that starts further on (DA-14). The type table, other
/// producers' dictionaries and every refusal are cases under <c>tests/cases/arrow/</c>, which
/// <c>@ex-grid/data</c> runs too.
/// </summary>
public class ReadTests
{
    [Fact] // ADR-0065: Arrow's file format is read, from memory, from a stream that seeks, from one that does not, and from one that starts further on
    public async Task The_file_format_is_read()
    {
        var entries = new string?[] { "Rates", "FX" };
        var file = File(new IpcOptions(),
            Batch(("Desk", Dictionary(entries, [1, 0, null])), ("Pnl", Raw<double>(DoubleType.Default, 1.5, null, -2))),
            Batch(("Desk", Dictionary(entries, [0])), ("Pnl", Raw<double>(DoubleType.Default, 3.0))));

        var snapshot = await ReadEveryWayAsync(file);
        using var further = new MemoryStream();
        further.Write([1, 2, 3]);
        further.Write(file);
        further.Position = 3;
        var fromFurther = await SnapshotArrow.ReadAsync(further, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(Shown("FX", "Rates", null, "Rates"), Values(snapshot, "Desk"));
        Assert.Equal(Shown(1.5, null, -2.0, 3.0), Values(snapshot, "Pnl"));
        AssertSame(snapshot, fromFurther);
    }
}
