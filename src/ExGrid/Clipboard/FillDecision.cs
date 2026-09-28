using ExGrid.Selection;

namespace ExGrid.Clipboard;

/// <summary>
/// What a fill key (Ctrl+D, Ctrl+R) will do once approved (ADR-0035): read
/// <see cref="Source"/> and write it over <see cref="Paste"/>'s one target, tiled by the
/// paste arithmetic of ADR-0014 — a source one row tall (one column wide) tiles its target
/// exactly.
/// </summary>
public sealed class FillPlan
{
    internal FillPlan(SelectionRange source, PastePlan paste)
    {
        Source = source;
        Paste = paste;
    }

    /// <summary>The cells read: the range's top row or left column, or the row above or
    /// the column to the left of a range one cell deep.</summary>
    public SelectionRange Source { get; }

    /// <summary>The write, in the shape a paste has — what the intent carries.</summary>
    public PastePlan Paste { get; }
}

/// <summary>
/// A fill key's answer (ADR-0035): an approved <see cref="FillPlan"/>, or the paste reason
/// it was refused for. Read <see cref="Plan"/> only when not refused.
/// </summary>
public sealed class FillDecision
{
    private readonly FillPlan? _plan;
    private readonly PasteRefusalReason _reason;

    private FillDecision(FillPlan? plan, PasteRefusalReason reason)
    {
        _plan = plan;
        _reason = reason;
    }

    internal static FillDecision Approve(FillPlan plan) => new(plan, default);

    internal static FillDecision Refuse(PasteRefusalReason reason) => new(null, reason);

    /// <summary>Whether the fill was refused.</summary>
    public bool IsRefused => _plan is null;

    /// <summary>Why the fill was refused. Throws on an approved fill.</summary>
    public PasteRefusalReason Reason => _plan is null
        ? _reason
        : throw new InvalidOperationException("The fill was approved; there is no refusal reason.");

    /// <summary>The approved plan. Throws on a refused fill.</summary>
    public FillPlan Plan => _plan
        ?? throw new InvalidOperationException(
            "The fill was refused; read Reason instead (ADR-0035: say which one when refusing).");
}
