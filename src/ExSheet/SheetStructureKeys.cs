using ExGrid.Selection;
using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// Excel's insert and delete keys, as ExSheet declares them to its grid (ADR-0050 item 14's note
/// of 2026-10-02; ADR-0071, Part C's case 12): Ctrl with <c>+</c>, typed with Shift (Ctrl+Shift+=
/// on a US or UK layout) or without it (the numeric keypad's <c>+</c>, or a layout with a key of its
/// own), and Ctrl with <c>-</c>. They are the browser's zoom keys, so declaring them is what keeps a
/// Sheet from zooming the page. Like the formatting keys they are read by the character typed.
/// </summary>
internal static class SheetStructureKeys
{
    /// <summary>What an insert or delete key does.</summary>
    internal enum Kind
    {
        Insert,
        Delete,
    }

    private static readonly Dictionary<string, Kind> ByKey = new(StringComparer.Ordinal)
    {
        ["Control++"] = Kind.Insert,
        ["Control+Shift++"] = Kind.Insert,
        ["Control+-"] = Kind.Delete,
    };

    /// <summary>The keys ExSheet declares to its grid for inserting and deleting, in the grid's canonical form.</summary>
    internal static IReadOnlyCollection<string> Declared { get; } = [.. ByKey.Keys];

    /// <summary>What the declared key <paramref name="key"/> does; null for a key that is not one of these.</summary>
    internal static Kind? KindOf(string key) => ByKey.TryGetValue(key, out var kind) ? kind : null;

    /// <summary>
    /// What <paramref name="kind"/> does to <paramref name="selection"/> on a Sheet of
    /// <paramref name="extent"/>: the rows a Selection of whole rows spans, or the columns a
    /// Selection of whole columns spans, inserted above or to the left, or deleted, as the Context
    /// Menu's commands do. Null for every other Selection — several ranges, every cell at once, or
    /// a range that is not whole rows or whole columns — which the key refuses: Excel opens a dialog
    /// there that shifts cells, and ExSheet shifts no cells (ADR-0050 item 14).
    /// </summary>
    internal static SheetEdit? EditFor(Kind kind, GridSelection selection, GridExtent extent)
    {
        if (selection.Ranges is not [var range]) return null;
        var wholeRows = range.SpansEveryColumn(extent);
        var wholeColumns = range.SpansEveryRow(extent);
        return (wholeRows, wholeColumns, kind) switch
        {
            (true, false, Kind.Insert) => SheetEdit.InsertRows(range.TopRow, range.RowCount),
            (true, false, Kind.Delete) => SheetEdit.DeleteRows(range.TopRow, range.RowCount),
            (false, true, Kind.Insert) => SheetEdit.InsertColumns(range.LeftColumn, range.ColumnCount),
            (false, true, Kind.Delete) => SheetEdit.DeleteColumns(range.LeftColumn, range.ColumnCount),
            _ => null,
        };
    }
}
