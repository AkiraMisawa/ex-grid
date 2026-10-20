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
    public string Name { get; internal set; } = DefaultName;

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
    /// this Sheet is recalculated. A rename is a change, so <c>RAND</c> and <c>RANDBETWEEN</c> draw
    /// again, as Excel's do (ADR-0180); a rename to the name the Sheet already has changes nothing.
    /// Internal (ADR-0201): a Consumer renames through <see cref="Do"/> with <see cref="SheetEdit.Rename"/>,
    /// which is the same change and can be undone.
    /// </summary>
    /// <exception cref="ArgumentException">The name is not one a Sheet can have (<see cref="IsValidName"/>).</exception>
    internal SheetChange Rename(string name) => ApplyRename(name).Change;

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

    /// <summary>Renames the Sheet through its Workbook, which rewrites every Sheet's References to it (ADR-0331).</summary>
    /// <exception cref="ArgumentException">The name is not one a Sheet can have (<see cref="IsValidName"/>).</exception>
    /// <exception cref="SheetRefusedException">Another Sheet of the Workbook has the name.</exception>
    internal Workbook.SheetRename ApplyRename(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        CheckName(name, nameof(name));
        return Workbook.Rename(this, name);
    }

    /// <summary>
    /// Undoes a rename: the old name back, and the Formulas it rewrote exactly as they were. The undo
    /// is a change and draws again, as Excel's did (ADR-0180); the undo of a rename that changed
    /// nothing changes nothing.
    /// </summary>
    internal SheetChange UndoRename(Workbook.SheetRename rename) => Workbook.UndoRename(this, rename);

    /// <summary>Every Formula cell and its Entry.</summary>
    internal IEnumerable<(CellAddress Address, Entry Entry)> FormulaEntries() =>
        _cells.Values.Where(c => c.Entry?.Parsed is not null).Select(c => (c.Address, c.Entry!));

    /// <summary>The Formula cells with a qualified Reference, which may name another Sheet or none as Sheets change.</summary>
    internal IEnumerable<CellAddress> QualifiedFormulas() =>
        _cells.Values.Where(c => c.Entry?.Parsed is { } parsed && parsed.References.Any(r => r.SheetName is not null)).Select(c => c.Address);

    /// <summary>Puts an Entry in a cell without recalculating or registering it: for a change to the Workbook's Sheets, which rebuilds both after.</summary>
    internal void ReplaceEntry(CellAddress address, Entry entry)
    {
        if (!_cells.TryGetValue(address, out var cell)) _cells[address] = cell = new Cell(address);
        cell.Entry = entry;
    }

    /// <summary>Forgets every Reference this Sheet's Formulas make to another Sheet, on the Sheets they name.</summary>
    internal void UnregisterAllRemote()
    {
        foreach (var formulaCell in _remoteReferences.Keys.ToList()) UnregisterRemote(formulaCell);
    }
}
