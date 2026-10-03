using ExPivot.Engine;

namespace ExPivot;

/// <summary>
/// The caps a Pivot Layout is held to (ADR-0066): a report's cost grows with its cells, not its
/// records, and principle 5 puts the cap on what cannot be executed. A layout that breaks one is
/// refused by name — "This layout needs more than 200,000 cells." — and the report stays on the
/// layout it had before. Each cap has a default, and the Consumer may change it, because the right
/// cap depends on the device and on where the aggregation runs.
/// </summary>
public sealed record PivotCaps
{
    /// <summary>Excel's own limit on a worksheet's rows: 1,048,576.</summary>
    public const int ExcelRows = 1_048_576;

    /// <summary>Excel's own limit on a worksheet's columns: 16,384.</summary>
    public const int ExcelColumns = 16_384;

    private readonly int _maxLeaves = PivotQuery.DefaultMaxLeaves;
    private readonly int _maxRows = ExcelRows;
    private readonly int _maxColumns = ExcelColumns;

    /// <summary>The defaults: 200,000 leaves, and Excel's rows and columns.</summary>
    public static PivotCaps Default { get; } = new();

    /// <summary>The most leaves a question allows (<see cref="PivotQuery.MaxLeaves"/>): a source
    /// that would need more refuses. 200,000 by default — provisional, as ADR-0066 records.</summary>
    public int MaxLeaves
    {
        get => _maxLeaves;
        init => _maxLeaves = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(MaxLeaves), value, "A cap allows at least one leaf.");
    }

    /// <summary>The most rows a report may have; Excel's 1,048,576 by default.</summary>
    public int MaxRows
    {
        get => _maxRows;
        init => _maxRows = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(MaxRows), value, "A cap allows at least one row.");
    }

    /// <summary>The most columns a report may have, its label columns included; Excel's 16,384 by
    /// default.</summary>
    public int MaxColumns
    {
        get => _maxColumns;
        init => _maxColumns = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(MaxColumns), value, "A cap allows at least one column.");
    }
}

/// <summary>Where Show Details puts the records behind a cell when the Consumer does not take them
/// itself through <c>OnShowDetails</c> (ADR-0059).</summary>
public enum PivotDetailsView
{
    /// <summary>A tab at the report's foot, where Excel's sheet tabs are — the default.</summary>
    Tab = 0,

    /// <summary>A dialog over the report, in ExPivot's own frame.</summary>
    Dialog,
}
