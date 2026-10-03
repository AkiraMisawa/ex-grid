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
    public List<FrameRun>? FrameRuns { get; init; }
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

    /// <summary>The same classes carrying the CSS overflow switch (OverflowPaint.Css).</summary>
    public static string ClassHx(CellState s) => s switch
    {
        CellState.Stale => "c num hx stale",
        CellState.Missing => "c num hx missing",
        CellState.Error => "c num hx err",
        _ => "c num hx",
    };

    /// <summary>The classes carrying candidate B's switch (OverflowPaint.CssScrollState).</summary>
    public static string ClassHs(CellState s) => s switch
    {
        CellState.Stale => "c num hs stale",
        CellState.Missing => "c num hs missing",
        CellState.Error => "c num hs err",
        _ => "c num hs",
    };

    /// <summary>The C# decision as the product makes it, in miniature: a per-class estimate
    /// against the cell's content width, the run interned by length (ADR-0016, P5). The
    /// widths are DejaVu Sans at the bench's 13px, weight 400, rounded up.</summary>
    public const double CellWidthPx = 90, PaddingPx = 6, DigitPx = 8.3, NarrowPx = 4.6, WidePx = 11;

    public static double EstimatePx(string text)
    {
        var w = 2 * PaddingPx;
        foreach (var ch in text)
            w += ch is >= '0' and <= '9' ? DigitPx : ch is ',' or '.' ? NarrowPx : WidePx;
        return w;
    }

    private static readonly string[] HashRuns = Enumerable.Range(0, 64).Select(n => new string('#', Math.Max(1, n))).ToArray();

    public static string Decide(string text) =>
        EstimatePx(text) <= CellWidthPx ? text : HashRuns[(int)((CellWidthPx - 2 * PaddingPx) / WidePx)];
}

/// <summary>The product's closed tone vocabulary (ADR-0006), as the bench prices it.</summary>
public enum CellTone : byte { None = 0, Positive = 1, Negative = 2 }

/// <summary>
/// Who decides #### for a numeric cell that does not fit (ADR-0016), priced per frame
/// (docs/research/css-decided-overflow.md).
/// </summary>
public enum OverflowPaint
{
    /// <summary>No decision at all: the value is painted and clipped. The floor.</summary>
    None,
    /// <summary>Today's design: a C# glyph-width estimate per cell, #### painted as text.</summary>
    CSharp,
    /// <summary>The candidate: the value is always painted, and every numeric cell carries a
    /// scroll-timeline animation that switches the #### run on while the cell overflows.</summary>
    Css,
    /// <summary>Candidate B: the same switch made with a scroll-state container query
    /// (`scrollable: inline-end`) — nothing animates.</summary>
    CssScrollState,
}

/// <summary>What a frame-loop scenario does to the grid on each frame.</summary>
public enum FrameScenario
{
    /// <summary>Nothing changes; frames keep running. Prices whatever the cells cost when idle.</summary>
    Idle,
    /// <summary>One row enters and one leaves per frame (a slow scroll).</summary>
    ScrollSlow,
    /// <summary>Every row is replaced per frame (a fling, 50 rows a step).</summary>
    ScrollFling,
    /// <summary>A live feed: 300 visible cells take new values once a second (every 60th frame).</summary>
    ChurnBurst,
    /// <summary>A live feed spread out: 5 visible cells take new values every frame (300/s).</summary>
    ChurnTrickle,
}

public sealed class FrameRun
{
    public required string Overflow { get; init; }
    public required string Scenario { get; init; }
    public required int Frames { get; init; }
    public required int Cells { get; init; }
    /// <summary>Blazor's render and DOM update, timed in JS around the synchronous .NET call.</summary>
    public required Stats Net { get; init; }
    /// <summary>The browser's own rendering steps for the frame — style, layout, animation
    /// update, paint recording — from the end of the rAF callback to a message posted from
    /// it, which runs only once the frame's rendering is done.</summary>
    public required Stats Browser { get; init; }
    /// <summary>Net + Browser per frame: the main-thread cost the frame carried.</summary>
    public required Stats Total { get; init; }
    /// <summary>rAF-to-rAF interval (vsync-quantised; shows dropped frames, not cost).</summary>
    public required Stats Interval { get; init; }
    /// <summary>Only the frames that changed something (for ChurnBurst, the burst frames).</summary>
    public Stats? ChangedFramesTotal { get; init; }
    public int Animations { get; init; }
    public int HashedCells { get; init; }
    /// <summary>Whether a full garbage collection ran just before this run.</summary>
    public bool GcBeforeRun { get; init; }
}
