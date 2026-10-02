using System.Globalization;
using System.Text;
using ExGrid.Cells;

namespace ExGrid.Components;

/// <summary>
/// One grid's highlights for its coloured text (ADR-0057, note of 2026-10-01; ADR-0021's note of the
/// same date). The layer beneath an editor surface draws the text as one run, as the field does, so
/// its characters stand where the field's do; the References are coloured by the CSS Custom
/// Highlight API, over that one run. The core renders on the layer what each highlight covers, and
/// the editor listener builds the ranges when it shows the layer.
///
/// <para><b>Names of the grid's own.</b> <c>CSS.highlights</c> is one registry per document, so two
/// grids that shared a name would clear each other's colours. Each name carries the grid's id
/// prefix, and the grid's own stylesheet paints its names (ADR-0018).</para>
///
/// <para><b>Colours from the tokens.</b> The stylesheet holds no colour: each highlight takes a
/// custom property the shipped stylesheet declares on the layer, from the Visual Tokens
/// <c>--ex-reference-1</c> to <c>-7</c>, their pointed shades and the pointed ground, so a Theme,
/// the dark scheme and forced colours reach them as they reached the spans they replace.</para>
/// </summary>
internal sealed class ReferenceHighlights
{
    private readonly string[] _colours = new string[ReferenceColour.PaletteLength + 1];
    private readonly string[] _pointed = new string[ReferenceColour.PaletteLength + 1];

    public ReferenceHighlights(string idPrefix)
    {
        var css = new StringBuilder();
        for (var place = 1; place <= ReferenceColour.PaletteLength; place++)
        {
            _colours[place] = string.Create(CultureInfo.InvariantCulture, $"{idPrefix}reference-{place}");
            _pointed[place] = string.Create(CultureInfo.InvariantCulture, $"{idPrefix}reference-{place}-pointed");
            css.Append("::highlight(").Append(_colours[place]).Append("){color:var(--ex-reference-text-")
                .Append(place.ToString(CultureInfo.InvariantCulture)).Append(")}\n");
            css.Append("::highlight(").Append(_pointed[place]).Append("){color:var(--ex-reference-text-")
                .Append(place.ToString(CultureInfo.InvariantCulture)).Append("-pointed)}\n");
        }
        Ground = idPrefix + "reference-pointed";
        css.Append("::highlight(").Append(Ground).Append("){background-color:var(--ex-reference-text-pointed)}\n");
        Css = css.ToString();
    }

    /// <summary>The highlight of the grey ground the Reference Point is writing lies on.</summary>
    public string Ground { get; }

    /// <summary>The grid's own stylesheet: one rule per highlight, each reading a custom property of
    /// the layer.</summary>
    public string Css { get; }

    /// <summary>The highlight of a Reference in <paramref name="colour"/>, or in its pointed shade.</summary>
    public string NameOf(ReferenceColour colour, bool pointed) => (pointed ? _pointed : _colours)[colour.Place];

    /// <summary>
    /// What a layer's highlights cover, as the core writes it on the layer (<c>data-ex-colours</c>):
    /// <c>start,length,name</c> per range, separated by spaces, in the text's UTF-16 offsets. Each
    /// Reference is one range in its colour. The span Point wrote (ADR-0051/0058) lies on the grey
    /// ground, written first, and the References inside it wear their pointed shades: a Reference
    /// standing exactly there, or anything longer — a lookup a press outside the grid wrote — as long
    /// as no Reference crosses its ends. A span that cuts through a Reference wears no look: half of
    /// a Reference on the grey would say that half alone was pointed. Empty where there is nothing to
    /// colour.
    /// </summary>
    public string ColoursOf(ReferenceColouring colouring, (int Start, int Length)? pointed)
    {
        var ground = pointed is { Length: > 0 } span && (IsOneReference(colouring, span) || !CutsAReference(colouring, span))
            ? span
            : ((int Start, int Length)?)null;
        if (colouring.References.Count == 0 && ground is null)
            return "";

        var colours = new StringBuilder();
        if (ground is { } on)
            Append(colours, on.Start, on.Length, Ground);
        for (var i = 0; i < colouring.References.Count; i++)
        {
            var reference = colouring.References[i];
            var inGround = ground is { } g && reference.Start >= g.Start && reference.Start + reference.Length <= g.Start + g.Length;
            Append(colours, reference.Start, reference.Length, NameOf(colouring.Colours[i], inGround));
        }
        return colours.ToString();
    }

    private static void Append(StringBuilder colours, int start, int length, string name)
    {
        if (colours.Length > 0)
            colours.Append(' ');
        colours.Append(start.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(length.ToString(CultureInfo.InvariantCulture)).Append(',').Append(name);
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
