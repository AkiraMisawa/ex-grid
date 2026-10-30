using System.Globalization;
using ExGrid.Cells;
using ExGrid.Selection;
using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// A Pointing Scope (ADR-0058): the Sheets and the grids a Consumer groups so that a Formula edited
/// in one of the Sheets can point at the grids' cells. Pointing at a grid of the Scope writes what
/// reads the pressed cell by its row's key, <c>XLOOKUP("R-4471", Positions[Id], Positions[PV])</c>,
/// or the pressed column's structured reference, <c>Positions[PV]</c> — never a position, so the
/// Formula reads the same row after the grid is sorted (ADR-0049, rule 2).
///
/// <para>The Consumer makes one per group, as the page is made, and passes it to each ExSheet of the
/// group (<c>ExSheet.PointingScope</c>) and, through <see cref="RegisterGrid"/>, to each ExGrid that
/// shows a Linked Table (<c>ExGrid.PointedAt</c>). Nothing on a page is joined unless it is put in
/// the same Scope: a grid in none behaves as it always does, and two Scopes divide a page.</para>
///
/// <para>A registered grid is pointed at while a Sheet of the Scope holds the keyboard, has an edit
/// open, and is in Point — its caret stands where a Reference can go, or what Point wrote there still
/// stands — and while the grid holds no open edit of its own. Then a press on its rows or its column
/// headers moves neither DOM focus nor its Selection, and is written into that Sheet's edit where
/// Point writes, or refused with a reason the Sheet tells its Consumer
/// (<c>ExSheet.OnPointingRefused</c>). Only the Sheet that holds the keyboard points; when the
/// keyboard leaves it, no grid is pointed at, and the edit stands. A Sheet is never pointed at. After
/// a press on a cell, that Sheet's arrow keys point inside the grid pressed: one row up or down in its
/// current order, or to the next column its table has. After a press on a column header, ↓ points at
/// the column's first row, and ← and → at the next column its table has, as a column, which the grid
/// scrolls into view across (ADR-0058, "The keyboard").</para>
///
/// <para>The Scope also draws in its grids what ADR-0057 and ADR-0058 ask of them. While a Formula
/// is edited in a Sheet of the Scope, the Linked Table columns it reads are outlined in the grids
/// that show them, in the colours their References wear: the page wires no <c>OutlinedColumns</c>
/// for a registered grid, and states the correspondence of columns once, when it registers it. And
/// the cell or column a press wrote for is dashed in the Focus outline's colour — a cell found by
/// its row's key, wherever a sort puts it — until Point over what it wrote ends.</para>
///
/// <para>The key column is the table's own: the Scope reads it, and the table's column names, from
/// the declaration of the Sheet that points (<c>DeclareLinkedTableAsync</c>). A Scope belongs to one
/// page, on one renderer: it is not shared between users.</para>
///
/// <para>The Scope serves Blazor's components over the Script Core (<c>ExSheet.Script.ExSheet</c> and
/// <c>ExGrid.Script.ExGrid</c>). The Scope is held in the browser, which decides all of the above there,
/// and this object only names it; the function that reads a row as the table's row is asked as each row
/// crosses to the browser (ADR-0430). The Razor ExGrid, which would decide in C#, is refused by name as
/// it takes the declaration.</para>
/// </summary>
public sealed class PointingScope
{
    // How many grids the Scope has registered, which names each declaration in the page.
    private int _declarations;

    /// <summary>
    /// Registers a grid that shows a Linked Table, and gives back the declaration to pass it as its
    /// <c>PointedAt</c> parameter. The page's Scope, in the browser, declares the grid pointed at
    /// while a Sheet of the Scope points, and answers each press on it. Register a grid once, as the page is made.
    /// </summary>
    /// <typeparam name="TRow">The grid's row type.</typeparam>
    /// <param name="table">The name of the Linked Table the grid shows, as the Sheets declare it;
    /// matched without regard to case, as a Formula names it.</param>
    /// <param name="tableRow">A grid row as the table's row: one Value per declared column, in the
    /// order the columns are declared — what the Consumer pushes for the row
    /// (<c>PushLinkedTableAsync</c>). A pressed cell's key is read from it, so the key written is
    /// the one the table holds. Null is a blank. Asked for every row as it crosses to the browser
    /// (ADR-0430), those read for a copy or a find included: it must be cheap and must not throw.</param>
    /// <param name="tableColumns">Which of the table's columns each grid column is, by the grid
    /// column's name (<c>GridColumn.Name</c>), for the grid columns named otherwise than their table
    /// column. A grid column with the same name as its table column needs no entry — named exactly as
    /// the Sheets declare the column, so that the column is outlined while a Formula reads it — and a
    /// grid column that is not one of the table's is simply not the table's: a press on it is
    /// refused. Null — the default — for a grid whose columns are all named as the table's. The
    /// Scope outlines each table column a Formula reads over the grid columns this makes it, so the
    /// page states the correspondence here and nowhere else.</param>
    /// <returns>The declaration to pass to the grid's <c>PointedAt</c> parameter.</returns>
    /// <exception cref="ArgumentException">The table's name is empty, or an entry of
    /// <paramref name="tableColumns"/> names no column.</exception>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public GridPointedAt<TRow> RegisterGrid<TRow>(
        string table, Func<TRow, IReadOnlyList<Value?>> tableRow, IReadOnlyDictionary<string, string>? tableColumns = null)
        where TRow : class
    {
        ArgumentException.ThrowIfNullOrEmpty(table);
        ArgumentNullException.ThrowIfNull(tableRow);
        var columns = new Dictionary<string, string>(StringComparer.Ordinal);
        if (tableColumns is not null)
        {
            foreach (var (gridColumn, tableColumn) in tableColumns)
            {
                if (string.IsNullOrEmpty(gridColumn) || string.IsNullOrEmpty(tableColumn))
                    throw new ArgumentException("A grid column and the table column it is are both named (ADR-0058).", nameof(tableColumns));
                columns[gridColumn] = tableColumn;
            }
        }
        _declarations += 1;
        var declaration = $"{Id}-{_declarations.ToString(CultureInfo.InvariantCulture)}";
        return new GridPointedAt<TRow>(_ => throw new InvalidOperationException(
            "A Pointing Scope is held in the browser, which answers a press on its grids there (ADR-0430)."))
        {
            // The grid joins the Scope in the page, and is sent each row as the table's row.
            InBrowser = new GridPointedInBrowser<TRow>(
                Id, declaration, table, columns, row => SheetWire.WriteRow(tableRow(row)),
                inBrowser => Claim(inBrowser, "a Razor ExGrid")),
        };
    }

    /// <summary>The Scope's name in the page, which every member drawn by the Script Core names (ADR-0430).</summary>
    internal string Id { get; } = Guid.NewGuid().ToString("N");

    /// <summary>A member joins: one drawn by the Script Core (<paramref name="inBrowser"/>) is taken, and the Razor
    /// ExGrid, which would decide in C#, is refused by name (ADR-0430).</summary>
    /// <exception cref="InvalidOperationException">The member is not drawn by the Script Core.</exception>
    internal void Claim(bool inBrowser, string member)
    {
        if (!inBrowser)
        {
            throw new InvalidOperationException(
                $"A Pointing Scope is held in the browser, and {member} cannot join it: give the grid ExGrid.Script.ExGrid (ADR-0430).");
        }
    }
}
