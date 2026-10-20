using System.Text.RegularExpressions;
using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

/// <summary>
/// Adding, renaming, moving, copying and deleting a Sheet (ADR-0331 §3, §4): each one change, with one
/// recalculation across the Workbook, and each undone exactly, a deletion included.
/// </summary>
public sealed partial class Workbook
{
    /// <summary>Where <paramref name="sheet"/> stands in the order; -1 for a Sheet the Workbook does not hold.</summary>
    internal int IndexOf(Sheet sheet) => _sheets.IndexOf(sheet);

    /// <summary>What a rename did, and the Formulas it rewrote on every Sheet, as they were, for undoing it.</summary>
    internal sealed record SheetRename(SheetChange Change, string OldName, IReadOnlyList<(Sheet Sheet, CellAddress Address, Entry Entry)> Rewritten);

    /// <summary>
    /// Renames <paramref name="sheet"/>: every Reference on every Sheet qualified with its old name is
    /// rewritten to the new one, as Excel rewrites them (ADR-0046, ADR-0331). A rename to the name the
    /// Sheet already has changes nothing.
    /// </summary>
    /// <exception cref="SheetRefusedException">Another Sheet of the Workbook has the name, without regard to case.</exception>
    internal SheetRename Rename(Sheet sheet, string name)
    {
        var oldName = sheet.Name;
        if (string.Equals(oldName, name, StringComparison.Ordinal)) return new SheetRename(SheetChange.None, oldName, []);
        if (NameTaken(name, sheet) is { } refusal) throw new SheetRefusedException(refusal);
        var rewritten = new List<(Sheet, CellAddress, Entry)>();
        var change = Reshape(sheet, dirty =>
        {
            string? Requalify(string qualifier) => Sheet.Names(qualifier, oldName) ? name : qualifier;
            Rewrite(r => r.SheetName is { } qualifier && Sheet.Names(qualifier, oldName) ? r with { SheetName = name } : r, Requalify, rewritten, dirty);
            sheet.Name = name;
        });
        return new SheetRename(change, oldName, rewritten);
    }

    /// <summary>Undoes a rename: the old name back, and the Formulas it rewrote exactly as they were. The undo of a rename that changed nothing changes nothing.</summary>
    internal SheetChange UndoRename(Sheet sheet, SheetRename rename)
    {
        if (string.Equals(sheet.Name, rename.OldName, StringComparison.Ordinal)) return SheetChange.None;
        return Reshape(sheet, _ =>
        {
            foreach (var (owner, address, entry) in rename.Rewritten) owner.ReplaceEntry(address, entry);
            sheet.Name = rename.OldName;
        });
    }

    /// <summary>Why <paramref name="name"/> cannot be given to <paramref name="sheet"/>: another Sheet has it, without regard to case; <see langword="null"/> when none does.</summary>
    internal SheetRefusal? NameTaken(string name, Sheet? sheet) =>
        Find(name) is { } other && other != sheet
            ? new SheetRefusal(SheetRefusalReason.SheetNameTaken, $"A Sheet of the Workbook is already named '{other.Name}'.")
            : null;

    /// <summary>The name a new Sheet takes: <c>Sheet</c> and one more than the highest number any <c>SheetN</c> has.</summary>
    internal string NextSheetName()
    {
        var highest = 0;
        foreach (var sheet in _sheets)
        {
            if (NumberedSheet().Match(sheet.Name) is { Success: true } match && int.TryParse(match.Groups[1].ValueSpan, out var n)) highest = Math.Max(highest, n);
        }
        return "Sheet" + (highest + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The name a copy of the Sheet named <paramref name="name"/> takes, as Excel names one: <c>Name (2)</c>, or the next number free, the name cut to fit 31 characters.</summary>
    internal string CopyName(string name)
    {
        var root = CopyNumber().Replace(name, "");
        for (var n = 2; ; n++)
        {
            var suffix = " (" + n.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
            var candidate = root[..Math.Min(root.Length, Sheet.MaximumNameLength - suffix.Length)] + suffix;
            if (Find(candidate) is null) return candidate;
        }
    }

    /// <summary>Inserts a new, empty Sheet at <paramref name="index"/>.</summary>
    internal Sheet InsertSheet(int index, string name, out SheetChange change)
    {
        var sheet = new Sheet(this, name);
        change = Reshape(sheet, _ => _sheets.Insert(index, sheet));
        return sheet;
    }

    /// <summary>Puts back a Sheet taken out, at <paramref name="index"/>, with what it held.</summary>
    internal SheetChange PutBack(Sheet sheet, int index, IReadOnlyList<(Sheet Sheet, CellAddress Address, Entry Entry)> rewritten, bool shown) =>
        Reshape(sheet, _ =>
        {
            _sheets.Insert(index, sheet);
            foreach (var (owner, address, entry) in rewritten) owner.ReplaceEntry(address, entry);
            if (shown) _shown = sheet;
        });

    /// <summary>
    /// Takes <paramref name="sheet"/> out of the Workbook. Deleted, every Reference on another Sheet
    /// qualified with its name becomes <c>#REF!</c> (ADR-0331); taken out to undo its insertion or copy,
    /// nothing is rewritten. The Sheet shown, if it was this one, becomes the one that takes its place.
    /// </summary>
    internal SheetChange TakeOut(Sheet sheet, bool delete, List<(Sheet Sheet, CellAddress Address, Entry Entry)> rewritten)
    {
        var index = _sheets.IndexOf(sheet);
        return Reshape(sheet, dirty =>
        {
            _sheets.RemoveAt(index);
            if (delete)
            {
                var name = sheet.Name;
                Rewrite(r => r.SheetName is { } qualifier && Sheet.Names(qualifier, name) ? null : r, q => Sheet.Names(q, name) ? null : q, rewritten, dirty);
            }
            if (_shown == sheet) _shown = _sheets[Math.Min(index, _sheets.Count - 1)];
        });
    }

    /// <summary>Moves <paramref name="sheet"/> to <paramref name="index"/> in the order.</summary>
    internal SheetChange Move(Sheet sheet, int index) =>
        Reshape(sheet, _ =>
        {
            _sheets.Remove(sheet);
            _sheets.Insert(index, sheet);
        });

    /// <summary>
    /// A copy of <paramref name="source"/> named <paramref name="name"/> at <paramref name="index"/>: its
    /// Entries, Cell Formats and column widths, and its seed. A Reference in it qualified with the
    /// source's name names the copy, as Excel's copy does (ADR-0331).
    /// </summary>
    internal Sheet Copy(Sheet source, int index, string name, out SheetChange change)
    {
        var copy = new Sheet(this, name);
        var document = source.ToDocument([]);
        var sourceName = source.Name;
        change = Reshape(copy, dirty =>
        {
            _sheets.Insert(index, copy);
            copy.OpenFormats(document);
            copy.TakeSeedOf(source);
            foreach (var cell in document.Cells)
            {
                if (cell.Entry is not { } entry) continue;
                var requalified = ReferenceRewriter.Rewrite(entry,
                    r => r.SheetName is { } qualifier && Sheet.Names(qualifier, sourceName) ? r with { SheetName = name } : r,
                    q => Sheet.Names(q, sourceName) ? name : q);
                copy.ReplaceEntry(cell.Address, requalified);
                dirty.Add(new SheetCell(copy, cell.Address));
            }
        });
        return copy;
    }

    /// <summary>Puts back a copy, or an inserted Sheet, taken out by an undo.</summary>
    internal SheetChange Reinsert(Sheet sheet, int index) => Reshape(sheet, _ => _sheets.Insert(index, sheet));

    /// <summary>
    /// Changes the Workbook's Sheets — which it holds, their order or their names — as one change: the
    /// References to other Sheets are forgotten before and learnt again after, and every Formula whose
    /// qualifier may now name another Sheet, or none, is recalculated with every volatile Formula, and
    /// whatever <paramref name="mutate"/> adds. What it changed on <paramref name="initiator"/> is
    /// returned; what it changed on every other Sheet is reported to the Workbook operation under way.
    /// </summary>
    private SheetChange Reshape(Sheet initiator, Action<HashSet<SheetCell>> mutate)
    {
        // A change, and draws again, as a rename did in Excel (ADR-0180).
        initiator.CountChange();
        var before = _sheets.ToDictionary(s => s, s => s.Snapshot());
        foreach (var sheet in _sheets) sheet.UnregisterAllRemote();
        var dirty = new HashSet<SheetCell>();
        mutate(dirty);
        _reshaped = true;
        foreach (var sheet in _sheets) sheet.RebuildDependencies();
        // INDIRECT's text may name a Sheet by a name it has had or now has (ADR-0124): it is volatile.
        foreach (var sheet in _sheets) dirty.UnionWith(sheet.QualifiedFormulas().Select(a => new SheetCell(sheet, a)));
        var from = _sheets.Contains(initiator) ? initiator : _sheets[0];
        var recalculated = dirty.Count == 0 && !_sheets.Any(s => s.HasVolatileFormulas)
            ? []
            : Sheet.RecalculateWorkbook(this, dirty, from, []);
        var own = SheetChange.None;
        foreach (var sheet in _sheets)
        {
            var change = sheet.Diff(before.GetValueOrDefault(sheet) ?? [], recalculated.GetValueOrDefault(sheet)?.Recalculated ?? []);
            if (sheet == initiator) own = change;
            else Collect(sheet, change);
        }
        return own;
    }

    /// <summary>Rewrites every Formula on every Sheet that names a Sheet through <paramref name="map"/> and <paramref name="requalify"/>, recording each as it was,
    /// and marks it to be recalculated: a Formula left naming no Sheet (<c>=#REF!*2</c>) is no longer among the qualified ones the change recalculates.</summary>
    private void Rewrite(Func<Reference, Reference?> map, Func<string, string?> requalify, List<(Sheet, CellAddress, Entry)> rewritten, HashSet<SheetCell> dirty)
    {
        foreach (var sheet in _sheets)
        {
            foreach (var (address, entry) in sheet.FormulaEntries().ToList())
            {
                // A qualified #REF! (Sheet1!#REF!) is no Reference, but its qualifier is rewritten too.
                if (!entry.Formula!.Contains('!', StringComparison.Ordinal)) continue;
                var mapped = ReferenceRewriter.Rewrite(entry, map, requalify);
                if (ReferenceEquals(mapped, entry)) continue;
                rewritten.Add((sheet, address, entry));
                sheet.ReplaceEntry(address, mapped);
                dirty.Add(new SheetCell(sheet, address));
            }
        }
    }

    [GeneratedRegex(@"^Sheet([0-9]{1,9})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NumberedSheet();

    [GeneratedRegex(@" \([0-9]+\)$", RegexOptions.CultureInvariant)]
    private static partial Regex CopyNumber();
}
