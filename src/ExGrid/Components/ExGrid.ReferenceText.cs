using ExGrid.Cells;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace ExGrid.Components;

// The coloured text (ADR-0057, "The coloured text is a layer that shows only while it is up to
// date"): an <input> cannot colour part of its text, so beneath each editor surface a layer draws
// the same text, and each Reference in it is coloured. The core renders it — beside its own inputs,
// and as a fragment a Chrome places immediately before its control — and carries on it the text it
// was rendered for. The editor listener shows it, and makes the field's own text transparent, only
// in the surface the edit is in — the field holding DOM focus; the other keeps its plain text, as
// Excel's does — and only while that text is the field's value; the stylesheet does the rest. Both
// surfaces carry the text, so the colours can follow a press from one into the other with nothing
// to render. Only where a References function is declared: without one no layer is rendered (DC-1).
//
// The text is one run, as the field's is (ADR-0057's note of 2026-10-01, ticket 86): a span per
// Reference made a run of text each, the browser rounds each run's width up to its layout unit, and
// the layer drifted 1/64 px a run from the field's characters. The References are coloured by the
// CSS Custom Highlight API instead: the core writes on the layer which highlight covers which
// characters (ReferenceHighlights), and the listener builds the ranges when it shows the layer.
public partial class ExGrid<TRow>
{
    // A distinct object, so the Cell Editor's layer can carry a key beside the rows' and the
    // editor's own, as every sibling there does (ADR-0003).
    private static readonly object ReferenceTextLayerKey = new();

    // Cached, so a render hands a Chrome's control the same delegate each time (ADR-0003). Each
    // reads the edit as it stands when it is rendered.
    private RenderFragment? _cellReferenceText;
    private RenderFragment? _barReferenceText;

    // This grid's highlight names and the stylesheet that paints them, made the first time a layer
    // asks: names of its own, so two grids never touch each other's colours (ADR-0018).
    private ReferenceHighlights? _referenceHighlights;

    // What the layers' highlights cover, and the colouring and the pointed span it was written for:
    // written again only when either changes, so a render that changed neither writes the same string.
    private string _layerColours = "";
    private ReferenceColouring? _layerColouredBy;
    private (int Start, int Length)? _layerPointed;

    private ReferenceHighlights Highlights => _referenceHighlights ??= new ReferenceHighlights(_idPrefix);

    /// <summary>The grid's own stylesheet for its highlights (ADR-0057, note of 2026-10-01).</summary>
    private string ReferenceHighlightStyles => Highlights.Css;

    /// <summary>Whether the Formula Bar shows the open edit's text (ADR-0051): while an edit is
    /// open on the Focus cell.</summary>
    private bool BarShowsTheEdit
        => _editMode != EditMode.None && !_selection.Selection.IsEmpty && _selection.Selection.Focus == _editingCell;

    /// <summary>The text the Formula Bar's layer is rendered for: the edit's while the bar shows
    /// it, and nothing otherwise. The bar's layer stands whenever a References function is
    /// declared, so an edit opening changes its text, and the listener hears it even when the
    /// bar already showed the opening text.</summary>
    private string BarReferenceText => BarShowsTheEdit ? _editText : "";

    /// <summary>
    /// Where what Point wrote stands in the edit's text, to be shown selected (ADR-0051, "The
    /// Reference being written is shown selected"; ADR-0057, "What cases 24–32 settled"): the
    /// Reference Point is writing, or the text a press outside the grid wrote (ADR-0058,
    /// <c>XLOOKUP("R-4471", Positions[Id], Positions[PV])</c>) — exactly what a further press would
    /// replace — while pointing, unless it starts right after the text's first character. Excel
    /// shows <c>=D11</c>, pointed straight after the <c>=</c>, without the look however often it
    /// was pointed, and <c>=SUM(D11</c>, <c>=1+D11</c> and <c>=D11+D12</c> with it; the core
    /// reads no Formula, and says so of the first character whatever it is. Null while nothing
    /// is. A look only: the field's text is never selected, so a key typed next follows the
    /// Reference, and ends pointing, as it always did.
    /// </summary>
    private (int Start, int Length)? PointedSpan
        => PointingContinues && _pointStart != 1 ? (_pointStart, _pointLength) : null;

    /// <summary>What the highlights of the core's own Cell Editor layer cover (<see cref="ReferenceHighlights.ColoursOf"/>).</summary>
    private string EditReferenceColours
    {
        get
        {
            var colouring = Colouring;
            var pointed = PointedSpan;
            if (!ReferenceEquals(colouring, _layerColouredBy) || pointed != _layerPointed)
            {
                _layerColours = Highlights.ColoursOf(colouring, pointed);
                _layerColouredBy = colouring;
                _layerPointed = pointed;
            }
            return _layerColours;
        }
    }

    /// <summary>What the highlights of the Formula Bar's layer cover: the edit's while the bar shows
    /// it; nothing otherwise.</summary>
    private string BarReferenceColours => BarShowsTheEdit ? EditReferenceColours : "";

    /// <summary>The Cell Editor's layer as a Chrome places it (<see cref="Chrome.CellEditorContext.ReferenceText"/>);
    /// null where no References function is declared.</summary>
    private RenderFragment? CellReferenceTextForChrome
        => ReferencesIn is null ? null : _cellReferenceText ??= builder => AddReferenceText(builder, _editText, EditReferenceColours);

    /// <summary>The Formula Bar's layer as a Chrome places it (<see cref="Chrome.FormulaBarTextContext.ReferenceText"/>);
    /// null where no References function is declared.</summary>
    private RenderFragment? BarReferenceTextForChrome
        => ReferencesIn is null ? null : _barReferenceText ??= builder => AddReferenceText(builder, BarReferenceText, BarReferenceColours);

    /// <summary>The Formula Bar's layer beside the core's own field: from the Name Box's right
    /// edge, where the field starts, to the band's, as the resolved metrics say (ADR-0028).</summary>
    private string ReferenceTextBarStyle()
        => FormattableString.Invariant($"left: {_metrics.NameBoxWidthPx}px");

    /// <summary>
    /// A layer as a Chrome places it: immediately before its control, inside the core's box, which
    /// the control fills (<see cref="Chrome.IGridChrome.CellEditor"/>). The stylesheet stands it
    /// over the box's content, so its text starts where the control's does. The text it was
    /// rendered for, and what its highlights cover, are written on it, where the listener reads
    /// them (ADR-0057). Its line holds the text as one run, nothing added and nothing left out.
    /// </summary>
    private static void AddReferenceText(RenderTreeBuilder builder, string text, string colours)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "ex-reference-text");
        builder.AddAttribute(2, "data-ex-text", text);
        builder.AddAttribute(3, "data-ex-colours", colours);
        builder.AddAttribute(4, "aria-hidden", "true");
        builder.OpenElement(5, "div");
        builder.AddAttribute(6, "class", "ex-reference-text-line");
        builder.AddContent(7, text);
        builder.CloseElement();
        builder.CloseElement();
    }
}
