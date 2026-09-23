using ExGrid.Chrome;
using ExGrid.Columns;
using Microsoft.AspNetCore.Components;

using ExGrid.Cells;

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
        ColumnWidthSpec? width = null,
        bool editable = false,
        FilterUiMode filterUi = FilterUiMode.Condition,
        CellAlign align = CellAlign.Auto,
        CellAlign headerAlign = CellAlign.Auto,
        Func<TRow, string, EditVerdict>? validate = null,
        Func<object, string>? format = null,
        Func<object, CellTone>? tone = null)
        : this(name, type, value, header, width, [], null, queryable: true,
            editable: editable, filterUi: filterUi, align: align, headerAlign: headerAlign,
            validate: validate, format: format, tone: tone)
    {
    }

    private GridColumn(
        string name,
        ColumnType type,
        Func<TRow, object?> value,
        string? header,
        ColumnWidthSpec? width,
        IReadOnlyList<GridAction> actions,
        RenderFragment<TemplateCellContext<TRow>>? template,
        bool queryable,
        bool editable = false,
        FilterUiMode filterUi = FilterUiMode.Condition,
        CellAlign align = CellAlign.Auto,
        CellAlign headerAlign = CellAlign.Auto,
        Func<TRow, string, EditVerdict>? validate = null,
        Func<object, string>? format = null,
        Func<object, CellTone>? tone = null)
    {
        if (align is not (CellAlign.Auto or CellAlign.Left or CellAlign.Center or CellAlign.Right))
            throw new ArgumentOutOfRangeException(nameof(align), align, null);
        if (headerAlign is not (CellAlign.Auto or CellAlign.Left or CellAlign.Center or CellAlign.Right))
            throw new ArgumentOutOfRangeException(nameof(headerAlign), headerAlign, null);
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
        Editable = editable;
        FilterUi = filterUi;
        Align = align;
        HeaderAlign = headerAlign;
        Validate = validate;
        Format = format;
        Tone = tone;
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
    ///
    /// <para>The fragment receives a <see cref="TemplateCellContext{TRow}"/> rather than the
    /// bare row: Space enters a Template cell by asking its content to take DOM focus, and
    /// the context is where that request arrives. A control that should be reachable by
    /// keyboard focuses itself when it sees one; the core never reaches into markup it did
    /// not render (ADR-0037). Such a control should also carry <c>tabindex="-1"</c>, or the
    /// grid stops being one tab stop.</para>
    /// </summary>
    public static GridColumn<TRow> TemplateColumn(
        string name,
        ColumnType type,
        Func<TRow, object?> value,
        RenderFragment<TemplateCellContext<TRow>> template,
        string? header = null,
        ColumnWidthSpec? width = null,
        Func<object, string>? format = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        return new GridColumn<TRow>(name, type, value, header, width, [], template, queryable: true, format: format);
    }

    /// <summary>The slice filtering and sorting need — handed to the engine as-is.</summary>
    public ColumnInfo<TRow> Info { get; }

    public string Name => Info.Name;

    public ColumnType Type => Info.Type;

    public Func<TRow, object?> Value => Info.Value;

    /// <summary>The label the header row paints. Defaults to <see cref="Name"/>.</summary>
    public string Header { get; }

    /// <summary>The Consumer's judgement on a commit into this column, or null for none —
    /// which means Accept, so a column that declares nothing behaves exactly as before
    /// (ADR-0034). It receives the current row instance and the committed text, so an
    /// in-row rule ("the break date must precede maturity") is expressible: the row
    /// carries the other values.</summary>
    public Func<TRow, string, EditVerdict>? Validate { get; }

    /// <summary>Never <c>default(ColumnWidthSpec)</c>: an unspecified width means Auto
    /// within the default bounds.</summary>
    public ColumnWidthSpec Width { get; }

    /// <summary>The actions this column's cells carry — empty for every other column
    /// (ADR-0020).</summary>
    public IReadOnlyList<GridAction> Actions { get; }

    /// <summary>What the Consumer paints in this column's cells, or null (ADR-0020) — handed
    /// the row and the core's focus request for the cell (ADR-0037).</summary>
    public RenderFragment<TemplateCellContext<TRow>>? Template { get; }

    /// <summary>Whether the query engine will sort or filter on this column. False only
    /// for an Action Column, which has no value to order by (ADR-0020).</summary>
    public bool IsQueryable => Info.IsQueryable;

    /// <summary>The cells' alignment (ADR-0016): Auto derives from the type — the
    /// ex-cell-numeric behaviour — and an explicit value beats the derivation.</summary>
    public CellAlign Align { get; }

    /// <summary>The header cell's own alignment; Auto is the header's default (left).</summary>
    public CellAlign HeaderAlign { get; }

    /// <summary>
    /// The column's display format (ADR-0006): the text a non-null value paints as, or
    /// null for the value's own <c>ToString()</c>. A null value paints empty either way —
    /// an absent value is a Cell State, not a format's business. This is the ONE text the
    /// grid shows for a value, so it is also what copy puts in <c>text/plain</c>, what the
    /// value list offers, what the Auto width estimates over and what the editor opens
    /// with (ADR-0005/0016); the raw, locale-free <c>text/html</c> form never goes through
    /// it. The Consumer's delegate owns the culture: the grid takes no view on separators.
    /// </summary>
    public Func<object, string>? Format { get; }

    /// <summary>
    /// The column's tone rule (ADR-0006): what a non-null value means — a gain, a loss —
    /// for the theme to paint, or null for no rule. The Consumer declares WHEN; the
    /// theme's tokens say what colour, and the bare grid paints none, as Excel's default
    /// number format does. A null value has no tone and the rule is not asked. Painted as
    /// an interned class per <see cref="CellTone"/>, so the rule may return a different
    /// answer per row at no allocation; it is called once per painted cell, on the row's
    /// render path, and should stay a comparison.
    /// </summary>
    public Func<object, CellTone>? Tone { get; }

    /// <summary>Whether this column's filter offers a value list, only conditions, or
    /// both (ADR-0009). Declared here because only the Consumer knows the cardinality;
    /// the runtime safety net is <see cref="Chrome.DistinctValues.TooMany"/>.</summary>
    public FilterUiMode FilterUi { get; }

    /// <summary>
    /// Whether the Cell Editor opens on this column's cells (ADR-0007/0010). Off by
    /// default: this is a display-first grid, and a cell that edits when nobody wired
    /// <c>OnEdit</c> would type into nothing. The grid never holds the committed value
    /// either way — an edit leaves as an intent.
    /// </summary>
    public bool Editable { get; }

    /// <summary>
    /// Whether this column's cells paint their value as text. False for Action and
    /// Template columns, and that is what takes them out of the Overflow decision and
    /// the Auto width observation: <c>####</c> is about a value that does not fit
    /// (ADR-0016), and there is no text here to measure or to hash.
    /// </summary>
    public bool PaintsValue => Template is null && Actions.Count == 0;
}
