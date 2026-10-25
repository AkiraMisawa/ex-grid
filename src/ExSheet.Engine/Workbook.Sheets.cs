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

    /// <summary>
    /// The Sheets a 3D Reference from <paramref name="first"/> to <paramref name="last"/> spans (ADR-0331
    /// §7): those from one to the other in tab order, both included, whichever stands first; the names
    /// are read without regard to case. <see langword="null"/> when no Sheet has one of the names.
    /// </summary>
    internal IReadOnlyList<Sheet>? Span(string first, string last)
    {
        if (Find(first) is not { } from || Find(last) is not { } to) return null;
        var (a, b) = (_sheets.IndexOf(from), _sheets.IndexOf(to));
        return _sheets.GetRange(Math.Min(a, b), Math.Abs(a - b) + 1);
    }

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
            // Either end of a 3D Reference's span is renamed as a qualifier is (ADR-0331 §7).
            Rewrite(r => r with
            {
                SheetName = r.SheetName is { } qualifier && Sheet.Names(qualifier, oldName) ? name : r.SheetName,
                LastSheetName = r.LastSheetName is { } last && Sheet.Names(last, oldName) ? name : r.LastSheetName,
            }, Requalify, rewritten, dirty);
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

    /// <summary>
    /// The name a new Sheet takes, as Excel names one (exsheet-workbook ticket 12, answer 13;
    /// verification/2026-10-25-windows-sheet-names): <c>Sheet</c> and the next number of the Workbook's count
    /// of the Sheets it has made, passing over a name a Sheet has. So with Sheet1 renamed Sheet5 the next is
    /// Sheet2, and after Sheet2 is inserted and deleted Sheet3; in a Workbook opened from a document, which
    /// keeps no count, the first number no Sheet has.
    /// </summary>
    internal string NextSheetName() => SheetName(NextSheetNumber());

    /// <summary>The number <see cref="NextSheetName"/> gives the next new Sheet.</summary>
    internal int NextSheetNumber()
    {
        for (var n = _sheetsMade + 1; ; n++)
        {
            if (Find(SheetName(n)) is null) return n;
        }
    }

    /// <summary><c>Sheet</c> and <paramref name="number"/>: the name a new Sheet numbered so takes.</summary>
    internal static string SheetName(int number) => "Sheet" + number.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A Workbook just opened from a document: it has made no Sheet yet, as Excel's opened workbook has not (<see cref="NextSheetName"/>).</summary>
    internal void Opened() => _sheetsMade = 0;

    /// <summary>
    /// Counts a new Sheet as made, as Excel counts it: one numbered by the count (<paramref name="numbered"/>,
    /// from <see cref="NextSheetNumber"/>) takes the count to its number, past the names it passed over; any
    /// other — a copy, as Excel's copy uses a number, or one the Consumer named, as Excel's added and renamed
    /// Sheet does — counts one. The count before is returned, for <see cref="UncountSheetMade"/>.
    /// </summary>
    internal int CountSheetMade(int? numbered)
    {
        var before = _sheetsMade;
        _sheetsMade = numbered ?? _sheetsMade + 1;
        return before;
    }

    /// <summary>
    /// Puts back the count of Sheets made as it stood before an insertion or a copy (<see cref="CountSheetMade"/>),
    /// when the step is undone: Excel's undo of an insertion gives its number back. Excel cannot undo a copy;
    /// ExSheet's undo of one gives its number back in the same way, its own choice.
    /// </summary>
    internal void UncountSheetMade(int before) => _sheetsMade = before;

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
    /// qualified with its name becomes <c>#REF!</c> (ADR-0331), and a 3D Reference it ends hands that end
    /// to the Sheet next to it inward (<see cref="Deleted"/>); taken out to undo its insertion or copy,
    /// nothing is rewritten. The Sheet shown, if it was this one, becomes the one that takes its place.
    /// </summary>
    internal SheetChange TakeOut(Sheet sheet, bool delete, List<(Sheet Sheet, CellAddress Address, Entry Entry)> rewritten)
    {
        var index = _sheets.IndexOf(sheet);
        var before = _sheets.Select(s => s.Name).ToList();
        return Reshape(sheet, dirty =>
        {
            _sheets.RemoveAt(index);
            if (delete)
            {
                var name = sheet.Name;
                Rewrite(r => r.IsThreeD ? Deleted(r, before, name) : r.SheetName is { } qualifier && Sheet.Names(qualifier, name) ? null : r,
                    q => Sheet.Names(q, name) ? null : q, rewritten, dirty);
            }
            if (_shown == sheet) _shown = _sheets[Math.Min(index, _sheets.Count - 1)];
        });
    }

    /// <summary>
    /// Moves <paramref name="sheet"/> to <paramref name="index"/> in the order. A 3D Reference whose end it is,
    /// moved past the other end, hands that end to the Sheet next to it inward (<see cref="Moved"/>); each
    /// Formula so rewritten is added to <paramref name="rewritten"/> as it was, for undoing the move.
    /// </summary>
    internal SheetChange Move(Sheet sheet, int index, List<(Sheet Sheet, CellAddress Address, Entry Entry)> rewritten)
    {
        var before = _sheets.Select(s => s.Name).ToList();
        return Reshape(sheet, dirty =>
        {
            _sheets.Remove(sheet);
            _sheets.Insert(index, sheet);
            var after = _sheets.Select(s => s.Name).ToList();
            Rewrite(r => r.IsThreeD ? Moved(r, before, after, sheet.Name) : r, q => q, rewritten, dirty);
        });
    }

    /// <summary>Undoes a move: <paramref name="sheet"/> back at <paramref name="index"/>, and the Formulas it rewrote exactly as they were.</summary>
    internal SheetChange MoveBack(Sheet sheet, int index, IReadOnlyList<(Sheet Sheet, CellAddress Address, Entry Entry)> rewritten) =>
        Reshape(sheet, _ =>
        {
            _sheets.Remove(sheet);
            _sheets.Insert(index, sheet);
            foreach (var (owner, address, entry) in rewritten) owner.ReplaceEntry(address, entry);
        });

    /// <summary>
    /// A copy of <paramref name="source"/> named <paramref name="name"/> at <paramref name="index"/>: its
    /// Entries, Cell Formats and column widths. A Reference in it qualified with the
    /// source's name names the copy, as Excel's copy does (ADR-0331); a 3D Reference the source ends is
    /// <c>#REF!</c> in the copy, as in Excel's, and any other is copied as it is (ADR-0331 §7).
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
            foreach (var cell in document.Cells)
            {
                if (cell.Entry is not { } entry) continue;
                var requalified = ReferenceRewriter.Rewrite(entry,
                    r => r.IsThreeD ? Sheet.Names(r.SheetName, sourceName) || Sheet.Names(r.LastSheetName, sourceName) ? null : r
                        : r.SheetName is { } qualifier && Sheet.Names(qualifier, sourceName) ? r with { SheetName = name } : r,
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

        [GeneratedRegex(@" \([0-9]+\)$", RegexOptions.CultureInvariant)]
    private static partial Regex CopyNumber();
}
