using ExGrid.Selection;

namespace ExGrid.Components;

// The Heading Mark (ADR-0008, 2026-10-13): for every Selection, the Headings of the columns and the
// rows that hold part of it are marked with a line on their edge toward the cells, as Excel marks
// them (the readings of 2026-10-12, R4). Wherever a header or Row Headings stand: a Sheet's Headings,
// a plain grid's header and its Row Headings, a Pivot Report's header. It is an overlay, drawn over
// the header band and over the Row Headings, so no row learns the Selection and every row still
// skips its render when the Selection moves (ADR-0003/0008). The runs it marks are made once per
// Selection, and the header's styles once per Selection, column geometry and band: the root renders
// on every scroll frame, and composed there they would be strings per frame (PF-3).
public partial class ExGrid<TRow>
{
    // A distinct object, as the other layers' keys are, so the Row Headings' layer can carry a key
    // that cannot collide with a row's.
    private static readonly object HeadingMarksLayerKey = new();

    private GridSelection? _markedSelection;
    private ColumnRange[] _markedColumns = [];
    private RowRange[] _markedRows = [];

    private GridSelection? _columnMarksSelection;
    private ColumnStyles? _columnMarksGeometry;
    private double _columnMarksTopPx = double.NaN;
    private double _columnMarksHeightPx = double.NaN;
    private string?[] _pinnedColumnMarks = [];
    private string?[] _scrollableColumnMarks = [];
    private bool _anyPinnedColumnMark;

    /// <summary>The runs of marked rows (ADR-0008, 2026-10-13), for the Row Headings' layer.</summary>
    private RowRange[] MarkedRows
    {
        get
        {
            MarkHeadings();
            return _markedRows;
        }
    }

    /// <summary>Whether the header band carries a Heading Mark: a Selection stands, and the column
    /// geometry is resolved for these columns, as it must be for a Header Group's rectangle.</summary>
    private bool MarksColumns => !_selection.Selection.IsEmpty && GroupsHaveGeometry;

    /// <summary>Whether the Row Headings carry a Heading Mark: a Selection stands and the band is
    /// painted.</summary>
    private bool MarksRows => !_selection.Selection.IsEmpty && _rowHeadingStyle is not null;

    private void MarkHeadings()
    {
        var selection = _selection.Selection;
        if (ReferenceEquals(selection, _markedSelection))
            return;
        _markedSelection = selection;
        _markedColumns = HeadingMarks.Columns(selection);
        _markedRows = HeadingMarks.Rows(selection);
    }

    /// <summary>The marked columns' styles in one layer of the header band, one slot per run, null
    /// where the layer holds none of it (<see cref="HeadingMarkStyles.Column"/>).</summary>
    private string?[] ColumnMarks(bool pinnedLayer)
    {
        MarkHeadings();
        var topPx = BandHeightPx - _metrics.HeaderHeightPx;
        var heightPx = _metrics.HeaderHeightPx;
        if (!ReferenceEquals(_columnMarksSelection, _markedSelection)
            || !ReferenceEquals(_columnMarksGeometry, _columnStyles)
            || _columnMarksTopPx != topPx
            || _columnMarksHeightPx != heightPx)
        {
            _columnMarksSelection = _markedSelection;
            _columnMarksGeometry = _columnStyles;
            _columnMarksTopPx = topPx;
            _columnMarksHeightPx = heightPx;
            var geometry = _columnStyles.Geometry;
            _pinnedColumnMarks = new string?[_markedColumns.Length];
            _scrollableColumnMarks = new string?[_markedColumns.Length];
            _anyPinnedColumnMark = false;
            for (var i = 0; i < _markedColumns.Length; i++)
            {
                _pinnedColumnMarks[i] = HeadingMarkStyles.Column(_markedColumns[i], geometry, pinnedLayer: true, topPx, heightPx);
                _scrollableColumnMarks[i] = HeadingMarkStyles.Column(_markedColumns[i], geometry, pinnedLayer: false, topPx, heightPx);
                _anyPinnedColumnMark |= _pinnedColumnMarks[i] is not null;
            }
        }
        return pinnedLayer ? _pinnedColumnMarks : _scrollableColumnMarks;
    }

    /// <summary>Whether any marked column has a part among the Pinned Columns, so the header's
    /// pinned mark layer is emitted only while it holds something.</summary>
    private bool MarksPinnedColumns
    {
        get
        {
            ColumnMarks(pinnedLayer: true);
            return _anyPinnedColumnMark;
        }
    }
}
