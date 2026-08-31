using ExGrid.Columns;

namespace ExGrid;

/// <summary>
/// A Column as CONTEXT.md defines it: one runtime object holding how to extract the
/// value from a row, the declared type, the header label, and the width intent
/// (ADR-0016). The filtering/sorting slice is exposed as <see cref="Info"/> and is what
/// the query engine and <see cref="InMemoryGridSource{TRow}"/> read (ADR-0023).
/// Immutable: the change signal for rendering is a different columns array instance,
/// never a rewritten column (ADR-0003).
/// </summary>
public sealed record GridColumn<TRow>
{
    public GridColumn(
        string name,
        ColumnType type,
        Func<TRow, object?> value,
        string? header = null,
        ColumnWidthSpec? width = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(value);
        // Refused here, at the one construction site, so a cast-in integer cannot reach
        // rendering (which would quietly left-align it as text) while the engine and the
        // overflow rule throw for the same value much later, naming no origin.
        if (type is not (ColumnType.Text or ColumnType.Number or ColumnType.Date or ColumnType.Boolean))
            throw new ArgumentOutOfRangeException(nameof(type), type, $"Unknown ColumnType for column '{name}'.");
        Info = new ColumnInfo<TRow>(name, type, value);
        Header = header ?? name;
        Width = width ?? new ColumnWidthSpec(ColumnWidth.Auto);
    }

    /// <summary>The slice filtering and sorting need — handed to the engine as-is.</summary>
    public ColumnInfo<TRow> Info { get; }

    public string Name => Info.Name;

    public ColumnType Type => Info.Type;

    public Func<TRow, object?> Value => Info.Value;

    /// <summary>The label the header row paints. Defaults to <see cref="Name"/>.</summary>
    public string Header { get; }

    /// <summary>Never <c>default(ColumnWidthSpec)</c>: an unspecified width means Auto
    /// within the default bounds.</summary>
    public ColumnWidthSpec Width { get; }
}
