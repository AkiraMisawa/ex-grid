using ExGrid.Cells;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;

namespace ExGrid.Components;

// Reference Outlines (ADR-0057), the fifth formula entry aid: while an edit is open, the
// Consumer answers the References in the editor's text, the core gives each a colour, and every
// range answered is outlined in the selection overlay (ADR-0008) in its colour. Point's outline is
// the Reference Outline of the Reference it writes. The grid does not know what a Formula is.
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
