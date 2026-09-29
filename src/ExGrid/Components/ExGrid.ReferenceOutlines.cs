using ExGrid.Cells;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;

namespace ExGrid.Components;

// Reference Outlines (ADR-0057), the fifth formula entry aid: while an edit is open, the
// Consumer answers the References in the editor's text, the core gives each a colour, and every
// range answered is outlined in the selection overlay (ADR-0008) in its colour. Point's outline is
// the Reference Outline of the Reference it writes. Beside them, the columns a Consumer asks for
// are outlined over all their rows, with or without an edit: a Linked Table's columns, in the
// grid that shows the table. The grid does not know what a Formula is.
public partial class ExGrid<TRow>
{
    /// <summary>
    /// A Consumer declaration (ADR-0057): the References in the editor's text. Asked
    /// synchronously while an edit is open, in every editing state and from either editor
    /// surface, with the text as it stands after each change; it answers each Reference's span
    /// and either the cells it names on this grid or a key naming something the grid does not
    /// hold. It must answer for text that is not finished — <c>=SUM(A1,</c> — since a Formula is
    /// unfinished for as long as it is typed, and with nothing for text that is not a Formula.
    ///
    /// <para>References naming the same cells, or carrying the same key, share a colour, handed
    /// out in order of first appearance round a palette of
    /// <see cref="ReferenceColour.PaletteLength"/>; a Consumer never picks one. Each range answered
    /// is outlined once in the selection overlay, cut to the painted rows, and the grid never
    /// scrolls to show one. Point's outline takes the colour of the range it points at. A commit
    /// or a cancel takes every outline away. A span that does not lie inside the text, or
    /// overlaps another, is refused by name. Null — the default — outlines nothing, and Point's
    /// outline keeps its own look (DC-1).</para>
    /// </summary>
    [Parameter] public Func<string, IReadOnlyList<EditorReference>>? ReferencesIn { get; set; }

    /// <summary>
    /// Tells the Consumer the colour each key <see cref="ReferencesIn"/> answered was given
    /// (ADR-0057): each key once, in order of first appearance. Raised once whenever that set
    /// changes and never when it does not, and with an empty list when the edit ends. A
    /// Reference naming cells on this grid is not told: the grid outlines it itself. The Consumer
    /// outlines what a key names wherever it shows it — a Linked Table's column in the grid that
    /// shows the table.
    /// </summary>
    [Parameter] public EventCallback<IReadOnlyList<ReferenceKeyColour>> OnReferenceKeyColoursChanged { get; set; }

    /// <summary>
    /// A Consumer declaration (ADR-0057): columns to outline, each in a colour. Each is drawn as a
    /// Reference Outline over the column's body, across all its rows, in the selection overlay as
    /// a whole-column range is (ADR-0008): one element per column, cut to the painted rows, and
    /// the grid never scrolls to show one. It is how the grid that shows a Linked Table outlines
    /// the columns a Formula edited elsewhere reads, in the colours that Formula's grid told its
    /// Consumer (<see cref="OnReferenceKeyColoursChanged"/>); it asks for no References function,
    /// no edit and no selection here.
    ///
    /// <para>A column is named as <see cref="GridColumn{TRow}.Name"/> names it, and outlined
    /// wherever the order puts it. A name the grid does not show is outlined nowhere, as a
    /// Reference to cells the grid does not have is; a column listed twice has no one colour and
    /// is refused by name. The grid cannot check that it shows the rows the Formula reads: a grid
    /// filtered to some of them outlines the rows it shows, and whether that is the table the
    /// Formula reads is the Consumer's to vouch for. Null or empty — the default — outlines nothing
    /// (DC-1, DC-50).</para>
    /// </summary>
    [Parameter] public IReadOnlyList<OutlinedColumn>? OutlinedColumns { get; set; }

    // The columns asked for, resolved to their places in the current order and their class
    // lists: once per push, off the render path, as Header Groups are (ADR-0032), and again only
    // when the list or the columns are a different instance.
    private (int Column, string Class)[] _columnOutlines = [];
    private IReadOnlyList<OutlinedColumn>? _outlinesResolvedFrom;
    private IReadOnlyList<GridColumn<TRow>>? _outlinesResolvedOver;

    // The class list of a Reference Outline in each colour, solid and as Point's: interned, since
    // the few there are serve every outline of every render (ADR-0027 P5).
    private static readonly string[] OutlineClasses = OutlineClassesOf("");
    private static readonly string[] PointOutlineClasses = OutlineClassesOf(" ex-point");

    // The colouring of the editor's text, and the text and the function it was asked of: asked
    // again only when either changes, so a render that changed neither asks nothing.
    private ReferenceColouring _colouring = ReferenceColouring.None;
    private string? _colouredText;
    private Func<string, IReadOnlyList<EditorReference>>? _colouredBy;

    // The keys and colours the Consumer was last told, so a change is told once (DC-49).
    private IReadOnlyList<ReferenceKeyColour> _keyColoursTold = [];

    private static string[] OutlineClassesOf(string suffix)
    {
        var classes = new string[ReferenceColour.PaletteLength + 1];
        for (var place = 1; place <= ReferenceColour.PaletteLength; place++)
            classes[place] = FormattableString.Invariant($"ex-reference-outline ex-reference-{place}{suffix}");
        return classes;
    }

    /// <summary>
    /// The colours of the References in the open edit's text (ADR-0057), asked of the Consumer
    /// only when the text, or the function, has changed since it was last asked; none while no
    /// edit is open or none is declared. Everything a render paints reads the same answer, and
    /// the keys are told from it after the render. An edit that ends forgets it: the next one
    /// asks afresh, whatever its text.
    /// </summary>
    private ReferenceColouring Colouring
    {
        get
        {
            if (_editMode == EditMode.None || ReferencesIn is not { } referencesIn)
            {
                _colouring = ReferenceColouring.None;
                _colouredText = null;
                _colouredBy = null;
                return _colouring;
            }
            if (!ReferenceEquals(referencesIn, _colouredBy) || !string.Equals(_editText, _colouredText, StringComparison.Ordinal))
            {
                var text = _editText;
                _colouring = ReferenceColouring.Of(text, referencesIn(text));
                _colouredText = text;
                _colouredBy = referencesIn;
            }
            return _colouring;
        }
    }

    /// <summary>
    /// How one range the text names is outlined solid (ADR-0057): cut to the grid's extent, in
    /// its colour. Null for the range Point's outline stands on — that one is Point's, dashed in
    /// its colour (<see cref="PointOutlineClass"/>), so a range is outlined once — and for a range
    /// that lies off the grid.
    /// </summary>
    private (SelectionRange Range, string Class)? ReferenceOutline((SelectionRange Range, ReferenceColour Colour) named)
    {
        if (named.Range == PointRange || WithinExtent(named.Range, Extent) is not { } drawn)
            return null;
        return (drawn, OutlineClasses[named.Colour.Place]);
    }

    /// <summary>Point's outline (ADR-0051/0057): the Reference Outline of the range it points
    /// at, dashed in that range's colour, where the text names it; its own look where it does
    /// not, or where no References function is declared.</summary>
    private string PointOutlineClass(SelectionRange pointed)
        => Colouring.ColourOf(pointed) is { } colour ? PointOutlineClasses[colour.Place] : "ex-point";

    /// <summary>
    /// Resolves <see cref="OutlinedColumns"/> to the places its columns stand in the current
    /// order (ADR-0057), when the list or the columns changed since they were last resolved. A
    /// name the grid does not show resolves to nothing; a null entry, or a column listed twice,
    /// is refused by name.
    /// </summary>
    private void ResolveOutlinedColumns()
    {
        if (ReferenceEquals(_outlinesResolvedFrom, OutlinedColumns) && ReferenceEquals(_outlinesResolvedOver, Columns))
            return;
        _outlinesResolvedFrom = OutlinedColumns;
        _outlinesResolvedOver = Columns;
        if (OutlinedColumns is not { Count: > 0 } outlined)
        {
            _columnOutlines = [];
            return;
        }
        var resolved = new List<(int Column, string Class)>(outlined.Count);
        for (var i = 0; i < outlined.Count; i++)
        {
            var asked = outlined[i] ?? throw new ArgumentNullException(nameof(OutlinedColumns),
                "OutlinedColumns holds a null column (ADR-0057).");
            for (var before = 0; before < i; before++)
            {
                if (string.Equals(outlined[before]!.Column, asked.Column, StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        $"OutlinedColumns lists the column '{asked.Column}' twice: a column is outlined in one colour, " +
                        "and the grid will not pick one of two (ADR-0057).", nameof(OutlinedColumns));
                }
            }
            for (var column = 0; column < Columns.Count; column++)
            {
                if (string.Equals(Columns[column].Name, asked.Column, StringComparison.Ordinal))
                {
                    resolved.Add((column, OutlineClasses[asked.Colour.Place]));
                    break;
                }
            }
        }
        _columnOutlines = [.. resolved];
    }

    /// <summary>The body of the column at <paramref name="column"/>: every row the grid has, or
    /// null when it has none.</summary>
    private SelectionRange? ColumnOutlineRange(int column)
        => Extent.RowCount > 0 ? new SelectionRange(0, column, Extent.RowCount, 1) : null;

    /// <summary>The part of a range that lies on this grid, or null when none does: an outline
    /// is drawn over cells the grid has, never over columns it would have to invent.</summary>
    private static SelectionRange? WithinExtent(SelectionRange range, GridExtent extent)
    {
        var rows = Math.Min(range.BottomRow, extent.RowCount - 1L) - range.TopRow + 1;
        var columns = Math.Min(range.RightColumn, extent.ColumnCount - 1L) - range.LeftColumn + 1;
        if (rows <= 0 || columns <= 0)
            return null;
        return rows == range.RowCount && columns == range.ColumnCount
            ? range
            : new SelectionRange(range.TopRow, range.LeftColumn, (int)rows, (int)columns);
    }

    /// <summary>
    /// Tells the Consumer the colour each key was given, when that changed since it was last
    /// told (ADR-0057, DC-49): the keys of the open edit's text, or none once the edit has
    /// ended. From after the render, for the reason the Range Request is raised there — a
    /// Consumer answering it synchronously would re-enter the batch it was raised from.
    /// </summary>
    private async Task RaiseKeyColoursIfChangedAsync()
    {
        if (_disposed || !OnReferenceKeyColoursChanged.HasDelegate)
            return;
        var keys = Colouring.Keys;
        if (SameKeyColours(keys, _keyColoursTold))
            return;
        _keyColoursTold = keys;
        await OnReferenceKeyColoursChanged.InvokeAsync(keys);
    }

    /// <summary>Whether two tellings hold the same keys in the same colours, in whatever
    /// order: each key is listed once.</summary>
    private static bool SameKeyColours(IReadOnlyList<ReferenceKeyColour> a, IReadOnlyList<ReferenceKeyColour> b)
    {
        if (a.Count != b.Count)
            return false;
        foreach (var told in a)
        {
            var found = false;
            foreach (var other in b)
            {
                if (told == other)
                {
                    found = true;
                    break;
                }
            }
            if (!found)
                return false;
        }
        return true;
    }
}
