namespace Bench.Client;

/// <summary>A generic cell state vocabulary — deliberately NOT any one Consumer's terms.</summary>
public enum CellState : byte { Normal = 0, Stale = 1, Missing = 2, Error = 3 }

/// <summary>Stand-in for a realistic row: a few string keys plus many numeric metrics.</summary>
public sealed class Row
{
    public int Id;
    public int BookId;
    public string Book = "";
    public string Typology = "";
    public decimal[] Values = [];
}

/// <summary>
/// A runtime column object that knows how to pull its own value out of a row —
/// the shape CONTEXT.md settled on, which makes static and data-driven columns
/// the same thing.
/// </summary>
public sealed class Col
{
    public required string Header { get; init; }
    /// <summary>Boxing accessor — the ergonomic API shape we want to price.</summary>
    public required Func<Row, object> Get { get; init; }
}

public enum CellRenderMode
{
    /// <summary>Plain markup, direct field access. The floor — no API abstraction at all.</summary>
    Direct,
    /// <summary>Plain markup via a per-column Func&lt;Row,object&gt; accessor (boxes every decimal).</summary>
    Accessor,
    /// <summary>Accessor + a per-cell metadata lookup. What ADR-0001's design actually costs.</summary>
    AccessorMeta,
    /// <summary>Row is a component (memoisation boundary), cells inside stay plain markup.</summary>
    RowComponent,
    /// <summary>RowComponent + a per-cell tone rule (a Consumer delegate on the value, answering a
    /// closed enum painted as an interned class — ADR-0006). Prices the rule on the settled design.</summary>
    RowComponentTone,
    /// <summary>Every cell is a Blazor component. Cheap when little changes, dear on a full rebuild.</summary>
    Component,
}

public sealed class Stats
{
    public int Count { get; init; }
    public double Min { get; init; }
    public double P50 { get; init; }
    public double P95 { get; init; }
    public double Max { get; init; }
    public double Mean { get; init; }
    public int OverBudget { get; init; }   // frames slower than 16.6ms
    public double OverBudgetPct { get; init; }

    public static Stats From(IReadOnlyList<double> samplesIn)
    {
        var s = samplesIn.OrderBy(x => x).ToArray();
        if (s.Length == 0) return new Stats();
        double Pct(double p) => s[Math.Clamp((int)Math.Ceiling(p * s.Length) - 1, 0, s.Length - 1)];
        var over = samplesIn.Count(x => x > 16.6);
        return new Stats
        {
            Count = s.Length,
            Min = s[0],
            P50 = Pct(0.50),
            P95 = Pct(0.95),
            Max = s[^1],
            Mean = s.Average(),
            OverBudget = over,
            OverBudgetPct = 100.0 * over / s.Length,
        };
    }
}

public sealed class RunResult
{
    public required string Mode { get; init; }
    public required int VisibleRows { get; init; }
    public required int VisibleCols { get; init; }
    public required int Cells { get; init; }
    public required int Step { get; init; }
    public required Stats Render { get; init; }
}

public sealed class Report
{
    public required string TimestampLocal { get; init; }
    public required Dictionary<string, object> Environment { get; init; }
    public required List<RunResult> Runs { get; init; }
    public Stats? ManualScrollFrames { get; init; }
    public string? Note { get; init; }
}

/// <summary>Shared so every render mode formats identically — otherwise the ladder isn't comparable.</summary>
public static class CellFormat
{
    public static string Value(object v) => v is decimal d ? d.ToString("N2") : v?.ToString() ?? "";

    public static string Class(CellState s) => s switch
    {
        CellState.Stale => "c num stale",
        CellState.Missing => "c num missing",
        CellState.Error => "c num err",
        _ => "c num",
    };

    /// <summary>State × tone, interned up front the way the product's CellClasses is: the
    /// render path indexes, never concatenates.</summary>
    private static readonly string[] Toned =
    [
        "c num", "c num pos", "c num neg",
        "c num stale", "c num stale pos", "c num stale neg",
        "c num missing", "c num missing pos", "c num missing neg",
        "c num err", "c num err pos", "c num err neg",
    ];

    public static string Class(CellState s, CellTone t) => Toned[(int)s * 3 + (int)t];
}

/// <summary>The product's closed tone vocabulary (ADR-0006), as the bench prices it.</summary>
public enum CellTone : byte { None = 0, Positive = 1, Negative = 2 }
