namespace ExGrid.Cells;

/// <summary>
/// Where a <see cref="GridPointedAt{TRow}"/> stands in a Pointing Scope held in the browser (ADR-0430): the declaration a
/// Scope made for a grid it registered, as Blazor's ExGrid over the Script Core carries it. That grid joins the
/// Scope in the page, and pointing at it is decided there, by the Script Core's Scope: what crosses from .NET is
/// which Scope, which declaration, which Linked Table, which grid columns are other table columns, and each row read
/// as the table's row.
/// </summary>
/// <typeparam name="TRow">The grid's row type.</typeparam>
public sealed class GridPointedInBrowser<TRow>
    where TRow : class
{
    /// <summary>Where a Scope's declaration stands in the page.</summary>
    /// <param name="scope">The Scope's name in the page, which every member of it names.</param>
    /// <param name="declaration">The declaration's own name, by which the page registers it once, however often a
    /// grid takes it.</param>
    /// <param name="table">The Linked Table the grid shows, as the Scope's Sheets declare it.</param>
    /// <param name="tableColumns">Which table column each grid column named otherwise is, by the grid column's
    /// name.</param>
    /// <param name="tableRow">A row read as the table's row: one Value per declared column, written as JSON as
    /// the Scope's Sheets read a Linked Table's row. Asked as each row is sent to the browser: it must be cheap and
    /// must not throw.</param>
    /// <exception cref="ArgumentException">A name is empty.</exception>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public GridPointedInBrowser(
        string scope, string declaration, string table, IReadOnlyDictionary<string, string> tableColumns, Func<TRow, string> tableRow)
    {
        ArgumentException.ThrowIfNullOrEmpty(scope);
        ArgumentException.ThrowIfNullOrEmpty(declaration);
        ArgumentException.ThrowIfNullOrEmpty(table);
        ArgumentNullException.ThrowIfNull(tableColumns);
        ArgumentNullException.ThrowIfNull(tableRow);
        Scope = scope;
        Declaration = declaration;
        Table = table;
        TableColumns = tableColumns;
        TableRow = tableRow;
    }

    /// <summary>The Scope's name in the page.</summary>
    public string Scope { get; }

    /// <summary>The declaration's own name in the page.</summary>
    public string Declaration { get; }

    /// <summary>The Linked Table the grid shows.</summary>
    public string Table { get; }

    /// <summary>Which table column each grid column named otherwise is, by the grid column's name.</summary>
    public IReadOnlyDictionary<string, string> TableColumns { get; }

    /// <summary>A row read as the table's row, as JSON.</summary>
    public Func<TRow, string> TableRow { get; }
}
