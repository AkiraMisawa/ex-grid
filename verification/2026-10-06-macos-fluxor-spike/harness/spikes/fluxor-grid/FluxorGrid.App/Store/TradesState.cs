using System.Collections.Immutable;
using ExGrid;
using Fluxor;

namespace FluxorGrid.Store;

/// <summary>A trade, immutable: a change is a new instance made with <c>with</c> (ADR-0003).</summary>
public sealed record Trade(string Id, string Book, string Currency, DateTime TradeDate, decimal Notional, decimal Price, decimal Pnl);

/// <summary>One cell, for the change times W1 keeps itself.</summary>
public readonly record struct CellKey(string Id, string Column);

/// <summary>
/// The blotter's Fluxor feature. The trades are an immutable list whose unchanged elements keep
/// their instances from one state to the next, so ExGrid can tell a changed trade by its
/// reference. <see cref="Sorts"/> and <see cref="ChangedAt"/> are W1's: a Consumer that pushes the
/// Window keeps its View State and its Change Highlight itself.
/// </summary>
[FeatureState(Name = "Trades", CreateInitialStateMethodName = nameof(Initial))]
public sealed record TradesState
{
    public ImmutableList<Trade> Trades { get; init; } = [];

    /// <summary>Bumped by every reducer that changed something.</summary>
    public long Version { get; init; }

    public int NextId { get; init; }

    /// <summary>Feed batches applied since the store was made.</summary>
    public int Ticks { get; init; }

    public bool FeedRunning { get; init; }

    // ---- W1 only -------------------------------------------------------------------------------

    /// <summary>The Sort in force, for W1, which sorts the Window itself.</summary>
    public IReadOnlyList<SortSpec> Sorts { get; init; } = [];

    /// <summary>Whether the reducers keep <see cref="ChangedAt"/>: W1 turns it on.</summary>
    public bool TrackChanges { get; init; }

    /// <summary>When each cell's painted text last changed upstream, for W1's Change Highlight:
    /// what <c>GridSource.From</c> keeps for W2.</summary>
    public ImmutableDictionary<CellKey, DateTimeOffset> ChangedAt { get; init; } = ImmutableDictionary<CellKey, DateTimeOffset>.Empty;

    // ---- What the writes did ---------------------------------------------------------------------

    public int Writes { get; init; }

    public string LastWrite { get; init; } = "—";

    public int Refused { get; init; }

    public string LastRefusal { get; init; } = "—";

    public static TradesState Initial() => new()
    {
        Trades = TradeData.Generate(TradeData.InitialCount),
        NextId = TradeData.InitialCount,
    };
}

/// <summary>The trades the store starts with: deterministic, so a check can name them.</summary>
public static class TradeData
{
    public const int InitialCount = 1_000;

    private static readonly string[] Books = ["Rates-London", "Rates-NewYork", "Credit-London", "FX-Tokyo", "FX-London", "Equity-Paris"];
    private static readonly string[] Currencies = ["USD", "EUR", "GBP", "JPY", "CHF"];

    public static string IdOf(int n) => $"T{n:D5}";

    public static ImmutableList<Trade> Generate(int count)
    {
        var builder = ImmutableList.CreateBuilder<Trade>();
        for (var n = 0; n < count; n++)
            builder.Add(Make(n));
        return builder.ToImmutable();
    }

    public static Trade Make(int n) => new(
        IdOf(n),
        Books[n % Books.Length],
        Currencies[n % Currencies.Length],
        new DateTime(2026, 1, 1).AddDays(n % 270),
        1_000_000m + (n % 97) * 250_000m,
        Math.Round(100m + (n % 41) * 0.37m, 4),
        Math.Round(((n * 7919) % 200_001 - 100_000) * 1.25m, 2));
}
