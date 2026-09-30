using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

public sealed partial class Sheet
{
    /// <summary>The name a new Sheet has, as Excel's first sheet does (ADR-0046).</summary>
    public const string DefaultName = "Sheet1";

    /// <summary>The longest name a Sheet can have: 31 characters, as in Excel.</summary>
    public const int MaximumNameLength = 31;

    /// <summary>
    /// The Sheet's name (ADR-0046), <see cref="DefaultName"/> unless one was given. A Reference
    /// qualified with it (<c>Sheet1!A1</c>, matched without regard to case) names this Sheet's
    /// cells; one qualified with any other name is <c>#REF!</c>, as there is one Sheet.
    /// </summary>
    public string Name { get; private set; } = DefaultName;

    /// <summary>
    /// Whether <paramref name="name"/> is a name a Sheet can have, as Excel decides one: 1 to 31
    /// characters, none of <c>: \ / ? * [ ]</c>, not beginning or ending with <c>'</c>, and not
    /// <c>History</c>, which Excel reserves.
    /// </summary>
    /// <param name="name">The name to check.</param>
    /// <param name="reason">Why it is not a name, in words; <see langword="null"/> when it is one.</param>
    public static bool IsValidName(string? name, out string? reason)
    {
        reason = name switch
        {
            null or "" => "A Sheet's name cannot be empty.",
            { Length: > MaximumNameLength } => $"A Sheet's name is at most {MaximumNameLength} characters.",
            _ when name.AsSpan().IndexOfAny(":\\/?*[]") >= 0 => "A Sheet's name cannot contain any of : \\ / ? * [ ].",
            _ when name[0] == '\'' || name[^1] == '\'' => "A Sheet's name cannot begin or end with an apostrophe.",
            _ when name.Equals("History", StringComparison.OrdinalIgnoreCase) => "'History' is a name Excel reserves.",
            _ => null,
        };
        return reason is null;
    }

    /// <summary>
    /// Renames the Sheet (ADR-0046). Every Reference qualified with the old name is rewritten to the
    /// new one, as Excel rewrites them, and a Formula whose qualifier now names or stops naming
    /// this Sheet is recalculated.
    /// </summary>
    /// <exception cref="ArgumentException">The name is not one a Sheet can have (<see cref="IsValidName"/>).</exception>
    public SheetChange Rename(string name) => ApplyRename(name).Change;

    /// <summary>Whether <paramref name="qualifier"/>, a Reference's Sheet name, names this Sheet: no qualifier, or this Sheet's name without regard to case.</summary>
    internal bool IsThisSheet(string? qualifier) => Names(qualifier, Name);

    /// <summary>Whether <paramref name="qualifier"/>, a Reference's Sheet name, names the Sheet called <paramref name="name"/>: no qualifier, or that name without regard to case.</summary>
    internal static bool Names(string? qualifier, string name) =>
        qualifier is null || string.Equals(qualifier, name, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether a Reference names this Sheet's cells.</summary>
    internal bool IsLocal(Reference reference) => IsThisSheet(reference.SheetName);

    /// <summary>
    /// A typed Formula with every qualifier that names this Sheet written in the Sheet's own name,
    /// as Excel writes it back: <c>=sheet1!#ref!*2</c> is stored as <c>=Sheet1!#REF!*2</c>
    /// (TEXT-083, ADR-0047 second run). A qualifier naming another Sheet is left as typed. The
    /// same instance when nothing changes.
    /// </summary>
    internal Entry InOwnName(Entry entry)
    {
        if (entry.Parsed is null || !entry.Formula!.Contains('!', StringComparison.Ordinal)) return entry;
        bool Other(string qualifier) => IsThisSheet(qualifier) && !string.Equals(qualifier, Name, StringComparison.Ordinal);
        return ReferenceRewriter.Rewrite(entry,
            r => r.SheetName is { } s && Other(s) ? r with { SheetName = Name } : r,
            q => Other(q) ? Name : q);
    }

    internal static string CheckName(string name, string parameter) =>
        IsValidName(name, out var reason) ? name : throw new ArgumentException(reason, parameter);

    /// <summary>What a rename did, and the Formulas it rewrote, as they were, for undoing it.</summary>
    internal sealed record RenameOutcome(SheetChange Change, string OldName, IReadOnlyList<(CellAddress Address, Entry Entry)> Rewritten);

    internal RenameOutcome ApplyRename(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        CheckName(name, nameof(name));
        var oldName = Name;
        if (string.Equals(oldName, name, StringComparison.Ordinal)) return new RenameOutcome(SheetChange.None, oldName, []);

        var before = Snapshot();
        var rewritten = new List<(CellAddress, Entry)>();
        var dirty = new HashSet<CellAddress>();
        foreach (var cell in _cells.Values)
        {
            if (cell.Entry?.Parsed is not { } parsed) continue;
            var qualified = parsed.References.Any(r => r.SheetName is not null);
            // A qualified #REF! (Sheet1!#REF!) is no Reference, but its qualifier is renamed too.
            if (!qualified && !cell.Entry.Formula!.Contains("!#REF!", StringComparison.OrdinalIgnoreCase)) continue;
            // Whether a qualifier names this Sheet may change with the name: recompute it.
            if (qualified) dirty.Add(cell.Address);
            string Requalify(string qualifier) => string.Equals(qualifier, oldName, StringComparison.OrdinalIgnoreCase) ? name : qualifier;
            var mapped = ReferenceRewriter.Rewrite(cell.Entry, r =>
                r.SheetName is not null && string.Equals(r.SheetName, oldName, StringComparison.OrdinalIgnoreCase) ? r with { SheetName = name } : r, Requalify);
            if (!ReferenceEquals(mapped, cell.Entry))
            {
                rewritten.Add((cell.Address, cell.Entry));
                cell.Entry = mapped;
            }
        }
        Name = name;
        RebuildDependencies();
        var recalculated = dirty.Count == 0 ? [] : Recalculate(dirty, []).Recalculated;
        return new RenameOutcome(Diff(before, recalculated), oldName, rewritten);
    }

    /// <summary>Undoes a rename: the old name back, and the Formulas it rewrote exactly as they were.</summary>
    internal SheetChange UndoRename(RenameOutcome outcome)
    {
        var before = Snapshot();
        var dirty = new HashSet<CellAddress>();
        foreach (var cell in _cells.Values)
        {
            if (cell.Entry?.Parsed is { } parsed && parsed.References.Any(r => r.SheetName is not null)) dirty.Add(cell.Address);
        }
        foreach (var (address, entry) in outcome.Rewritten)
        {
            _cells[address].Entry = entry;
            dirty.Add(address);
        }
        Name = outcome.OldName;
        RebuildDependencies();
        var recalculated = dirty.Count == 0 ? [] : Recalculate(dirty, []).Recalculated;
        return Diff(before, recalculated);
    }
}
