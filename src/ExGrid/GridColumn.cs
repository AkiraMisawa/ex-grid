using ExGrid.Columns;
using Microsoft.AspNetCore.Components;

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
        : this(name, type, value, header, width, [], null, queryable: true)
    {
    }

    private GridColumn(
        string name,
        ColumnType type,
        Func<TRow, object?> value,
        string? header,
        ColumnWidthSpec? width,
        IReadOnlyList<GridAction> actions,
        RenderFragment<TRow>? template,
        bool queryable)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(value);
        // Refused here, at the one construction site, so a cast-in integer cannot reach
        // rendering (which would quietly left-align it as text) while the engine and the
        // overflow rule throw for the same value much later, naming no origin.
        if (type is not (ColumnType.Text or ColumnType.Number or ColumnType.Date or ColumnType.Boolean))
            throw new ArgumentOutOfRangeException(nameof(type), type, $"Unknown ColumnType for column '{name}'.");
        Info = new ColumnInfo<TRow>(name, type, value, queryable);
        Header = header ?? name;
        Width = width ?? new ColumnWidthSpec(ColumnWidth.Auto);
        Actions = actions;
        Template = template;
    }

    /// <summary>
    /// A column whose cells carry declared actions and no value (ADR-0020). Painted as
    /// plain markup, so it adds no component boundary and stays on the memoised row's
    /// path.
    ///
    /// <para>It is <b>unsortable and unfilterable</b>, and says so rather than sorting
    /// every row by nothing: with no value to order by, an accepted sort would reorder
    /// the result in a way nobody could account for — refusing is the same rule as
    /// refusing to copy rather than truncate (ADR-0005).</para>
    /// </summary>
    public static GridColumn<TRow> ActionColumn(
        string name,
        IReadOnlyList<GridAction> actions,
        string? header = null,
        ColumnWidthSpec? width = null)
    {
        ArgumentNullException.ThrowIfNull(actions);
        if (actions.Count == 0)
            throw new ArgumentException($"Action Column '{name}' declares no actions.", nameof(actions));
        if (actions.Any(action => action is null))
            throw new ArgumentException($"Action Column '{name}' declares a null action.", nameof(actions));
        var names = actions.Select(action => action.Name).ToList();
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Count)
        {
            throw new ArgumentException(
                $"Action Column '{name}' declares two actions with the same name; the name is how one is " +
                "told from another when it is reported back.", nameof(actions));
        }

        // Text, because the declared type decides the filter UI and the numeric
        // presentation (ADR-0016/0023) and neither applies here; nothing reads it as a
        // value, because the column is not queryable and paints no text.
        return new GridColumn<TRow>(
            name, ColumnType.Text, static _ => null, header ?? "", width,
            actions.ToArray(), template: null, queryable: false);
    }

    /// <summary>
    /// A column whose cell contents the Consumer paints (ADR-0020). <b>The value accessor
    /// is still required</b>: sorting, filtering and copy all read the value, never the
    /// template — this is the hole `MudDataGrid` fell into, where Consumers had to walk
    /// the rendered columns to map a column back to a property (ADR-0009).
    ///
    /// <para>Hold the fragment on the column declaration; do not construct it per render.
    /// A new lambda each time makes the column look changed and slips the whole row past
    /// memoisation (ADR-0003).</para>
    /// </summary>
    public static GridColumn<TRow> TemplateColumn(
        string name,
        ColumnType type,
        Func<TRow, object?> value,
        RenderFragment<TRow> template,
        string? header = null,
        ColumnWidthSpec? width = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        return new GridColumn<TRow>(name, type, value, header, width, [], template, queryable: true);
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

    /// <summary>The actions this column's cells carry — empty for every other column
    /// (ADR-0020).</summary>
    public IReadOnlyList<GridAction> Actions { get; }

    /// <summary>What the Consumer paints in this column's cells, or null (ADR-0020).</summary>
    public RenderFragment<TRow>? Template { get; }

    /// <summary>Whether the query engine will sort or filter on this column. False only
    /// for an Action Column, which has no value to order by (ADR-0020).</summary>
    public bool IsQueryable => Info.IsQueryable;

    /// <summary>
    /// Whether this column's cells paint their value as text. False for Action and
    /// Template columns, and that is what takes them out of the Overflow decision and
    /// the Auto width observation: <c>####</c> is about a value that does not fit
    /// (ADR-0016), and there is no text here to measure or to hash.
    /// </summary>
    public bool PaintsValue => Template is null && Actions.Count == 0;
}
