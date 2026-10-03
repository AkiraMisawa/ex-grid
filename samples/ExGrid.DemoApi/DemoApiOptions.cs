using System.Globalization;

namespace ExGrid.DemoApi;

/// <summary>
/// What the server is started with: how many trades it generates, and the directory their
/// database lives in (ADR-0069). Both come from the environment, which the configuration
/// carries, so a test hands in its own.
/// </summary>
/// <param name="TradeCount">How many trades are generated at first start.</param>
/// <param name="DataDirectory">Where the generated database and each run's copy of it live —
/// never inside the repository.</param>
internal sealed record DemoApiOptions(int TradeCount, string DataDirectory)
{
    /// <summary>A million, because a million-row CSV is normal (ADR-0069, Q2 and Q49).</summary>
    public const int DefaultTradeCount = 1_000_000;

    /// <summary>The most trades the server will generate. Five million is the ADR's example of
    /// seeing the limits; fifty leaves room above it, and keeps every Record Key eight digits
    /// long (<see cref="TradeGenerator.TradeId"/>).</summary>
    public const int MaxTradeCount = 50_000_000;

    /// <summary>The variable that sets the count.</summary>
    public const string TradesVariable = "EXGRID_DEMO_TRADES";

    /// <summary>The variable that sets the data directory.</summary>
    public const string DataVariable = "EXGRID_DEMO_DATA";

    /// <summary>
    /// Reads <c>EXGRID_DEMO_TRADES</c> and <c>EXGRID_DEMO_DATA</c>. A count that is not a whole
    /// number in range is refused by name at start, rather than replaced by the default: a run
    /// that asked for five million and quietly got one would look like it worked.
    /// </summary>
    public static DemoApiOptions From(IConfiguration configuration)
    {
        var count = DefaultTradeCount;
        if (configuration[TradesVariable] is { } text && !string.IsNullOrWhiteSpace(text))
        {
            if (!int.TryParse(text.Trim(), NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out count)
                || count < 1 || count > MaxTradeCount)
            {
                throw new InvalidOperationException(
                    $"{TradesVariable} is \"{text}\"; it is a whole number of trades from 1 to "
                    + $"{MaxTradeCount.ToString("N0", CultureInfo.InvariantCulture)}.");
            }
        }

        var directory = configuration[DataVariable] is { } data && !string.IsNullOrWhiteSpace(data)
            ? Path.GetFullPath(data)
            : Path.Combine(Path.GetTempPath(), "exgrid-demo-api");
        return new DemoApiOptions(count, directory);
    }
}
