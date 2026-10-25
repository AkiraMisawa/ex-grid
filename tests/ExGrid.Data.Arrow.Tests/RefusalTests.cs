using Apache.Arrow.Types;
using ExGrid.Data;
using Xunit;
using static ExGrid.Data.Arrow.Tests.Fixtures;

namespace ExGrid.Data.Arrow.Tests;

/// <summary>
/// What a refusal leaves behind (DA-14): nothing. Every refusal's text, row and column is a case under
/// <c>tests/cases/arrow/</c> (<see cref="Cases.ArrowCaseTests"/>), which <c>@ex-grid/data</c> runs too.
/// </summary>
public class RefusalTests
{
    [Fact] // ADR-0064: a refused read throws, and yields no Snapshot
    public async Task A_refused_read_yields_no_snapshot()
    {
        var payload = Stream(Batch(("Count", Raw<ulong>(UInt64Type.Default, ulong.MaxValue))));
        Snapshot? read = null;

        await Assert.ThrowsAsync<SnapshotException>(async () => read = await SnapshotArrow.ReadAsync(payload, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Null(read);
    }
}
