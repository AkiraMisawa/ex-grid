using ExGrid.Cells;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace ExGrid.Components;

// The coloured text (ADR-0057, "The coloured text is a layer that shows only while it is up to
// date"): an <input> cannot colour part of its text, so beneath each editor surface a layer draws
// the same text with each Reference in its colour. The core renders it — beside its own inputs,
// and as a fragment a Chrome places immediately before its control — and carries on it the text it
// was rendered for. The editor listener shows it, and makes the field's own text transparent, only
// while that text is the field's value; the stylesheet does the rest. Only where a References
// function is declared: without one no layer is rendered (DC-1).
public partial class ExGrid<TRow>
{
    // A distinct object, so the Cell Editor's layer can carry a key beside the rows' and the
    // editor's own, as every sibling there does (ADR-0003).
    private static readonly object ReferenceTextLayerKey = new();

    // The class of a Reference's span in each colour: interned, since the few there are serve
    // every span of every render (ADR-0027 P5).
    private static readonly string[] ReferenceTextClasses = ReferenceTextClassesOf();

    // Cached, so a render hands a Chrome's control the same delegate each time (ADR-0003). Each
    // reads the edit as it stands when it is rendered.
    private RenderFragment? _editReferenceSpans;
    private RenderFragment? _barReferenceSpans;
    private RenderFragment? _cellReferenceText;
    private RenderFragment? _barReferenceText;

    private static string[] ReferenceTextClassesOf()
    {
        var classes = new string[ReferenceColour.PaletteLength + 1];
        for (var place = 1; place <= ReferenceColour.PaletteLength; place++)
            classes[place] = FormattableString.Invariant($"ex-reference-{place}");
        return classes;
    }

    /// <summary>Whether the Formula Bar shows the open edit's text (ADR-0051): while an edit is
    /// open on the Focus cell.</summary>
    private bool BarShowsTheEdit
        => _editMode != EditMode.None && !_selection.Selection.IsEmpty && _selection.Selection.Focus == _editingCell;

    /// <summary>The text the Formula Bar's layer is rendered for: the edit's while the bar shows
    /// it, and nothing otherwise. The bar's layer stands whenever a References function is
    /// declared, so an edit opening changes its text, and the listener hears it even when the
    /// bar already showed the opening text.</summary>
    private string BarReferenceText => BarShowsTheEdit ? _editText : "";

    /// <summary>The spans of the edit's text, for the core's own layers.</summary>
    private RenderFragment EditReferenceSpans
        => _editReferenceSpans ??= builder => AddReferenceSpans(builder, _editText, Colouring);

    /// <summary>The spans of what the Formula Bar's layer draws: the edit's while the bar shows
    /// it; nothing otherwise.</summary>
    private RenderFragment BarReferenceSpans
        => _barReferenceSpans ??= builder =>
        {
            if (BarShowsTheEdit)
                AddReferenceSpans(builder, _editText, Colouring);
        };

    /// <summary>The Cell Editor's layer as a Chrome places it (<see cref="Chrome.CellEditorContext.ReferenceText"/>);
    /// null where no References function is declared.</summary>
    private RenderFragment? CellReferenceTextForChrome
        => ReferencesIn is null ? null : _cellReferenceText ??= builder => AddReferenceText(builder, _editText, EditReferenceSpans);

    /// <summary>The Formula Bar's layer as a Chrome places it (<see cref="Chrome.FormulaBarTextContext.ReferenceText"/>);
    /// null where no References function is declared.</summary>
    private RenderFragment? BarReferenceTextForChrome
        => ReferencesIn is null ? null : _barReferenceText ??= builder => AddReferenceText(builder, BarReferenceText, BarReferenceSpans);

    /// <summary>The Formula Bar's layer beside the core's own field: from the Name Box's right
    /// edge, where the field starts, to the band's, as the resolved metrics say (ADR-0028).</summary>
    private string ReferenceTextBarStyle()
        => FormattableString.Invariant($"left: {_metrics.NameBoxWidthPx}px");

    /// <summary>
    /// A layer as a Chrome places it: immediately before its control, inside the core's box, which
    /// the control fills (<see cref="Chrome.IGridChrome.CellEditor"/>). The stylesheet stands it
    /// over the box's content, so its text starts where the control's does. The text it was
    /// rendered for is written on it, where the listener reads it (ADR-0057).
    /// </summary>
    private static void AddReferenceText(RenderTreeBuilder builder, string text, RenderFragment spans)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "ex-reference-text");
        builder.AddAttribute(2, "data-ex-text", text);
        builder.AddAttribute(3, "aria-hidden", "true");
        builder.OpenElement(4, "div");
        builder.AddAttribute(5, "class", "ex-reference-text-line");
        builder.AddContent(6, spans);
        builder.CloseElement();
        builder.CloseElement();
    }

    /// <summary>
    /// The text, each Reference a span in its colour and the rest as it stands — nothing added,
    /// nothing left out, so the layer's characters stand where the field's do. The colouring is
    /// always of this very text (<see cref="Colouring"/>).
    /// </summary>
    private static void AddReferenceSpans(RenderTreeBuilder builder, string text, ReferenceColouring colouring)
    {
        var at = 0;
        for (var i = 0; i < colouring.References.Count; i++)
        {
            var reference = colouring.References[i];
            if (reference.Start > at)
                builder.AddContent(0, text[at..reference.Start]);
            builder.OpenElement(1, "span");
            builder.AddAttribute(2, "class", ReferenceTextClasses[colouring.Colours[i].Place]);
            builder.AddContent(3, text.Substring(reference.Start, reference.Length));
            builder.CloseElement();
            at = reference.Start + reference.Length;
        }
        if (at < text.Length)
            builder.AddContent(4, at == 0 ? text : text[at..]);
    }
}
