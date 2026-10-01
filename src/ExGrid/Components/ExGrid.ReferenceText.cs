using ExGrid.Cells;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace ExGrid.Components;

// The coloured text (ADR-0057, "The coloured text is a layer that shows only while it is up to
// date"): an <input> cannot colour part of its text, so beneath each editor surface a layer draws
// the same text with each Reference in its colour. The core renders it — beside its own inputs,
// and as a fragment a Chrome places immediately before its control — and carries on it the text it
// was rendered for. The editor listener shows it, and makes the field's own text transparent, only
// in the surface the edit is in — the field holding DOM focus; the other keeps its plain text, as
// Excel's does — and only while that text is the field's value; the stylesheet does the rest. Both
// surfaces carry the text, so the colours can follow a press from one into the other with nothing
// to render. Only where a References function is declared: without one no layer is rendered (DC-1).
public partial class ExGrid<TRow>
{
    // A distinct object, so the Cell Editor's layer can carry a key beside the rows' and the
    // editor's own, as every sibling there does (ADR-0003).
    private static readonly object ReferenceTextLayerKey = new();

    // The class of a Reference's span in each colour, and of the span of the Reference Point is
    // writing: interned, since the few there are serve every span of every render (ADR-0027 P5).
    private static readonly string[] ReferenceTextClasses = ReferenceTextClassesOf("");
    private static readonly string[] PointedReferenceTextClasses = ReferenceTextClassesOf(" ex-reference-pointed");

    // Cached, so a render hands a Chrome's control the same delegate each time (ADR-0003). Each
    // reads the edit as it stands when it is rendered.
    private RenderFragment? _editReferenceSpans;
    private RenderFragment? _barReferenceSpans;
    private RenderFragment? _cellReferenceText;
    private RenderFragment? _barReferenceText;

    private static string[] ReferenceTextClassesOf(string suffix)
    {
        var classes = new string[ReferenceColour.PaletteLength + 1];
        for (var place = 1; place <= ReferenceColour.PaletteLength; place++)
            classes[place] = FormattableString.Invariant($"ex-reference-{place}{suffix}");
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

    /// <summary>The spans of the edit's text, for the core's own layers.</summary>
    private RenderFragment EditReferenceSpans
        => _editReferenceSpans ??= builder => AddReferenceSpans(builder, _editText, Colouring, PointedSpan);

    /// <summary>The spans of what the Formula Bar's layer draws: the edit's while the bar shows
    /// it; nothing otherwise.</summary>
    private RenderFragment BarReferenceSpans
        => _barReferenceSpans ??= builder =>
        {
            if (BarShowsTheEdit)
                AddReferenceSpans(builder, _editText, Colouring, PointedSpan);
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
    /// always of this very text (<see cref="Colouring"/>), and so is the pointed span, which wears
    /// the pointed look: a Reference standing exactly there wears it beside its colour; anything
    /// longer — a lookup a press outside the grid wrote, holding References of its own — wears it
    /// as one span, with its References inside in their colours (ADR-0058, "What is drawn"). A
    /// pointed span that cuts through a Reference — one the Consumer reads as beginning before
    /// what Point wrote, or running on past it — wears none, as before: half of a Reference on the
    /// grey would say that half alone was pointed. Only the layer of the surface the edit is in
    /// shows, so only there is it seen.
    /// </summary>
    private static void AddReferenceSpans(RenderTreeBuilder builder, string text, ReferenceColouring colouring, (int Start, int Length)? pointed)
    {
        if (pointed is { Length: > 0 } span && !IsOneReference(colouring, span) && !CutsAReference(colouring, span))
        {
            var (at, next) = AddReferenceRun(builder, text, colouring, 0, 0, span.Start, pointed: null);
            builder.OpenElement(5, "span");
            builder.AddAttribute(6, "class", "ex-reference-pointed");
            (at, next) = AddReferenceRun(builder, text, colouring, at, next, span.Start + span.Length, pointed: null);
            builder.CloseElement();
            AddReferenceRun(builder, text, colouring, at, next, text.Length, pointed: null);
            return;
        }
        AddReferenceRun(builder, text, colouring, 0, 0, text.Length, pointed);
    }

    /// <summary>
    /// The text from <paramref name="at"/> to <paramref name="end"/>, each Reference in it — from
    /// the <paramref name="next"/>th on — a span in its colour, and the one standing exactly over
    /// <paramref name="pointed"/> with the pointed look beside it. No Reference crosses
    /// <paramref name="end"/>.
    /// </summary>
    /// <returns>Where the run ended, and the first Reference after it.</returns>
    private static (int At, int Next) AddReferenceRun(
        RenderTreeBuilder builder, string text, ReferenceColouring colouring, int at, int next, int end, (int Start, int Length)? pointed)
    {
        for (; next < colouring.References.Count && colouring.References[next].Start < end; next++)
        {
            var reference = colouring.References[next];
            if (reference.Start > at)
                builder.AddContent(0, text[at..reference.Start]);
            var classes = pointed is { } span && span.Start == reference.Start && span.Length == reference.Length
                ? PointedReferenceTextClasses
                : ReferenceTextClasses;
            builder.OpenElement(1, "span");
            builder.AddAttribute(2, "class", classes[colouring.Colours[next].Place]);
            builder.AddContent(3, text.Substring(reference.Start, reference.Length));
            builder.CloseElement();
            at = reference.Start + reference.Length;
        }
        if (at < end)
            builder.AddContent(4, at == 0 && end == text.Length ? text : text[at..end]);
        return (end, next);
    }

    /// <summary>Whether one Reference stands exactly over the span.</summary>
    private static bool IsOneReference(ReferenceColouring colouring, (int Start, int Length) span)
    {
        foreach (var reference in colouring.References)
        {
            if (reference.Start == span.Start && reference.Length == span.Length)
                return true;
        }
        return false;
    }

    /// <summary>Whether a Reference begins on one side of an end of the span and finishes on the
    /// other.</summary>
    private static bool CutsAReference(ReferenceColouring colouring, (int Start, int Length) span)
    {
        var end = span.Start + span.Length;
        foreach (var reference in colouring.References)
        {
            var referenceEnd = reference.Start + reference.Length;
            if ((reference.Start < span.Start && referenceEnd > span.Start) || (reference.Start < end && referenceEnd > end))
                return true;
        }
        return false;
    }
}
