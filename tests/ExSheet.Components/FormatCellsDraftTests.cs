using System.Globalization;
using ExSheet.Engine;
using Xunit;
using FontStyle = ExSheet.FontStyle;

namespace ExSheet.Components.Tests;

/// <summary>
/// Format Cells' draft (ADR-0071, SH-45): what it opens on, what each choice means, and the change
/// OK sets — the rules every Chrome's Format Cells sets its controls by. And the codes the Number
/// tab writes: each one the engine reads, under every culture ExSheet supports.
/// </summary>
public class FormatCellsDraftTests
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");
    private static readonly BorderLine Thin = new(BorderLineStyle.Thin);

    private static FormatCellsDraft Open(Action<Sheet>? prepare, string focus, params string[] ranges)
    {
        var sheet = new Sheet(EnUs);
        prepare?.Invoke(sheet);
        return FormatCellsDraft.Open(sheet, CellAddress.Parse(focus), [.. ranges.Select(CellRange.Parse)]);
    }

    private static void Format(Sheet sheet, string range, CellFormatChange change) =>
        sheet.SetCellFormat([CellRange.Parse(range)], change);

    public static TheoryData<string> Cultures => ["en-US", "en-GB", "ja-JP", "de-DE", "fr-FR", "sv-SE"];

    [Theory] // ADR-0071 / SH-45: every code the Number tab writes is one the engine reads, and opens again on the category that wrote it
    [MemberData(nameof(Cultures))]
    public void Every_code_the_number_tab_writes_is_read_and_recognised(string name)
    {
        var culture = CultureInfo.GetCultureInfo(name);
        var written = new List<(string Code, NumberFormatCategory Category)>();
        for (var places = 0; places <= NumberFormatCodes.MostDecimalPlaces; places++)
        {
            for (var negative = 0; negative < NumberFormatCodes.NumberNegativeStyles; negative++)
            {
                written.Add((NumberFormatCodes.Number(places, false, negative), NumberFormatCategory.Number));
                written.Add((NumberFormatCodes.Number(places, true, negative), NumberFormatCategory.Number));
            }
            for (var negative = 0; negative < NumberFormatCodes.CurrencyNegativeStyles; negative++)
                written.Add((NumberFormatCodes.Currency(places, negative, culture), NumberFormatCategory.Currency));
            written.Add((NumberFormatCodes.Percentage(places), NumberFormatCategory.Percentage));
            written.Add((NumberFormatCodes.Scientific(places), NumberFormatCategory.Scientific));
        }
        written.AddRange(NumberFormatCodes.DateTypes.Select(code => (code, NumberFormatCategory.Date)));
        written.AddRange(NumberFormatCodes.TimeTypes.Select(code => (code, NumberFormatCategory.Time)));
        written.Add(("@", NumberFormatCategory.Text));

        foreach (var (code, category) in written)
        {
            Assert.True(NumberFormat.TryParse(code, out var format, out var reason), $"{code}: {reason}");
            Assert.Equal(category, NumberFormatCodes.Recognise(format, culture).Category);
        }
        Assert.All(NumberFormatCodes.CustomTypes, code => Assert.True(NumberFormat.TryParse(code, out _, out _), code));
    }

    [Fact] // ADR-0071 / SH-45: Currency writes the culture's symbol where the culture puts it, and the eleventh run's en-GB key format opens as Currency
    public void Currency_writes_the_cultures_symbol()
    {
        Assert.Equal("$#,##0.00_);[Red]($#,##0.00)", NumberFormatCodes.Currency(2, 4, EnUs));
        var enGb = CultureInfo.GetCultureInfo("en-GB");
        Assert.Equal("£#,##0.00;[Red]-£#,##0.00", NumberFormatCodes.Currency(2, 2, enGb));
        Assert.Equal(NumberFormatCategory.Currency, NumberFormatCodes.Recognise(NumberFormat.Parse("£#,##0.00;[Red]-£#,##0.00"), enGb).Category);
        Assert.Equal("#,##0.00 €", NumberFormatCodes.Currency(2, 0, CultureInfo.GetCultureInfo("de-DE")));
    }

    [Fact] // ADR-0071: a code no category writes opens under Custom, as it is
    public void A_code_no_category_writes_opens_as_custom()
    {
        var draft = Open(sheet => Format(sheet, "A1", new CellFormatChange { NumberFormat = NumberFormat.Parse("0.0\" kg\"") }), "A1", "A1");

        Assert.Equal(NumberFormatCategory.Custom, draft.Category);
        Assert.Equal("0.0\" kg\"", draft.TypeCode);
        Assert.Equal("0.0\" kg\"", draft.Types[0]);
    }

    [Fact] // ADR-0071 / SH-45: a disabled category is refused with its reason
    public void A_disabled_category_is_refused_with_its_reason()
    {
        var draft = Open(null, "A1", "A1");

        var refused = Assert.Throws<ArgumentException>(() => draft.SelectCategory(NumberFormatCategory.Fraction));

        Assert.StartsWith("Fractions are not", refused.Message);
        Assert.True(draft.Change.IsEmpty);
    }

    [Fact] // ADR-0071 / SH-45: Custom starts from the code shown, as Excel's does
    public void Custom_starts_from_the_code_shown()
    {
        var draft = Open(null, "A1", "A1");
        draft.SelectCategory(NumberFormatCategory.Percentage);
        draft.SetDecimalPlaces(1);

        draft.SelectCategory(NumberFormatCategory.Custom);

        Assert.Equal("0.0%", draft.TypeCode);
        Assert.Equal(NumberFormat.Parse("0.0%"), draft.Change.NumberFormat);
    }

    [Fact] // ADR-0071 / SH-45, case 25: a font style sets bold and italic together, and nothing else of the Font
    public void A_font_style_sets_bold_and_italic_and_nothing_else()
    {
        var draft = Open(null, "A1", "A1");

        draft.SetFontStyle(FontStyle.BoldItalic);

        Assert.Equal(new CellFormatChange { Bold = true, Italic = true }, draft.Change);
    }

    [Fact] // ADR-0071 / SH-45: an edge's button takes the chosen line, and takes it away when the edge already shows it
    public void An_edge_button_toggles_the_chosen_line()
    {
        var draft = Open(null, "A1", "A1");

        draft.ToggleEdge(BorderEdge.Top);
        Assert.Equal(Thin, draft.EdgeLine(BorderEdge.Top));
        draft.ToggleEdge(BorderEdge.Top);

        Assert.Equal(BorderLine.None, draft.EdgeLine(BorderEdge.Top));
        Assert.Equal(new BorderChange { Top = BorderLine.None }, draft.Change.Borders);
    }

    [Fact] // ADR-0071 / SH-45, case 22: None over one cell takes its outline away and touches no inside edge it has none of
    public void None_over_one_cell_sets_its_outline_only()
    {
        var draft = Open(null, "A1", "A1");

        draft.ApplyPreset(BorderPreset.None);

        Assert.Equal(new BorderChange { Top = BorderLine.None, Bottom = BorderLine.None, Left = BorderLine.None, Right = BorderLine.None }, draft.Change.Borders);
        Assert.Throws<InvalidOperationException>(() => draft.ApplyPreset(BorderPreset.Inside));
        Assert.Throws<InvalidOperationException>(() => draft.ToggleEdge(BorderEdge.InsideVertical));
    }

    [Fact] // ADR-0071 / SH-45: Inside over a block sets both inside edges, in the chosen style and colour
    public void Inside_over_a_block_sets_both_inside_edges()
    {
        var draft = Open(null, "B2", "B2:C3");
        var line = new BorderLine(BorderLineStyle.Dashed, CellColour.FromRgb(0x0070C0));
        draft.SetLineStyle(BorderLineStyle.Dashed);
        draft.SetLineColour(CellColour.FromRgb(0x0070C0));

        draft.ApplyPreset(BorderPreset.Inside);

        Assert.Equal(new BorderChange { InsideHorizontal = line, InsideVertical = line }, draft.Change.Borders);
    }

    [Fact] // ADR-0071 / SH-45: choosing a colour from the palette replaces More Colours' text, and its refusal with it
    public void A_palette_colour_replaces_more_colours_text()
    {
        var draft = Open(null, "A1", "A1");
        draft.SetColourText(ColourTarget.Fill, "#12");
        Assert.NotNull(draft.Refusal);

        draft.SetFill(CellFill.Solid(CellColour.FromRgb(0xFFFF00)));

        Assert.Null(draft.Refusal);
        Assert.Equal("", draft.ColourText(ColourTarget.Fill));
    }

    [Fact] // ADR-0071 / SH-45: the case 24 Selection — bold, a red Fill and a thick bottom over a plain cell — opens with the Font style empty and No Colour
    public void Case_24_opens_with_the_font_style_empty_and_no_colour()
    {
        var draft = Open(sheet => Format(sheet, "A1", new CellFormatChange
        {
            Bold = true,
            Fill = CellFill.Solid(CellColour.FromRgb(0xFF0000)),
            Borders = new BorderChange { Bottom = new BorderLine(BorderLineStyle.Thick) },
        }), "A1", "A1:A2");

        Assert.Null(draft.FontStyle);
        Assert.Equal(CellFill.None, draft.Fill);
        Assert.Equal(BorderLine.None, draft.EdgeLine(BorderEdge.Top));
        Assert.Equal(BorderLine.None, draft.EdgeLine(BorderEdge.Bottom));
        Assert.True(draft.Change.IsEmpty);
    }

    [Fact] // ADR-0071 / SH-45: what is shared across the Selection opens as it is shared
    public void Shared_parts_open_as_they_are()
    {
        var draft = Open(sheet => Format(sheet, "A1:B2", new CellFormatChange
        {
            Italic = true,
            Borders = BorderChange.Outline(Thin),
        }), "A1", "A1:B2");

        Assert.Equal(FontStyle.Italic, draft.FontStyle);
        Assert.Equal(Thin, draft.EdgeLine(BorderEdge.Top));
        Assert.Equal(Thin, draft.EdgeLine(BorderEdge.Right));
        Assert.Equal(BorderLine.None, draft.EdgeLine(BorderEdge.InsideVertical));
    }

    [Fact] // ADR-0071 / SH-45, case 11-24: Format Cells compares each cell's own sides inside a range, so a thick bottom over a plain cell is an inside edge that differs, drawn grey and dotted, though the edge shows the line from either cell
    public void A_thick_bottom_over_a_plain_cell_is_an_edge_that_differs_case_11_24()
    {
        Assert.Null(Open(ThickBottomOnA1, "A1", "A1:A2").EdgeLine(BorderEdge.InsideHorizontal));
    }

    // ---- A range's outer edges show as drawn (the fourteenth Windows run, case 13) ----

    private static readonly BorderLine Thick = new(BorderLineStyle.Thick);

    private static void ThickBottomOnA1(Sheet sheet) => Format(sheet, "A1", new CellFormatChange { Borders = new BorderChange { Bottom = Thick } });

    [Fact] // ADR-0071 / SH-45, case 14-13: A2 under A1's thick bottom opens with a thick top, though A2 records nothing; A1 opens with its own thick bottom. This corrects the reading that the dialog shows A2's own sides
    public void A2_under_a1s_thick_bottom_opens_with_a_thick_top_case_14_13()
    {
        var a2 = Open(ThickBottomOnA1, "A2", "A2");
        Assert.Equal(Thick, a2.EdgeLine(BorderEdge.Top));
        Assert.Equal(BorderLine.None, a2.EdgeLine(BorderEdge.Bottom));
        Assert.True(a2.Change.IsEmpty);

        Assert.Equal(Thick, Open(ThickBottomOnA1, "A1", "A1").EdgeLine(BorderEdge.Bottom));
    }

    [Fact] // ADR-0071 / SH-45, case 14-13: over several ranges, each range's outer edges show as drawn, and differ where the lines drawn there do
    public void Each_ranges_outer_edges_show_as_drawn_case_14_13()
    {
        void Prepare(Sheet sheet)
        {
            ThickBottomOnA1(sheet);
            Format(sheet, "C1", new CellFormatChange { Borders = new BorderChange { Bottom = Thick } });
        }

        Assert.Equal(Thick, Open(Prepare, "A2", "A2", "C2").EdgeLine(BorderEdge.Top));
        Assert.Null(Open(Prepare, "A2", "A2", "E2").EdgeLine(BorderEdge.Top));
        // A2:B2's top is thick over A2 and drawn nowhere over B2.
        Assert.Null(Open(Prepare, "A2", "A2:B2").EdgeLine(BorderEdge.Top));
    }

    [Fact] // ADR-0071 / SH-45, case 14-13: an edge shown from the neighbour and left alone is not touched, so OK sets nothing and A2 still records no top
    public void A_shown_edge_left_alone_sets_nothing_case_14_13()
    {
        var sheet = new Sheet(EnUs);
        ThickBottomOnA1(sheet);
        var draft = FormatCellsDraft.Open(sheet, CellAddress.Parse("A2"), [CellRange.Parse("A2")]);

        draft.ToggleEdge(BorderEdge.Bottom);

        Assert.Equal(new BorderChange { Bottom = Thin }, draft.Change.Borders);
    }

    [Fact] // ADR-0071 / SH-45, case 14-13 (a reading: no run pressed it): taking the shown top away clears that edge on both sides, as clearing an edge does
    public void Taking_a_shown_edge_away_clears_it_on_both_sides_case_14_13()
    {
        var sheet = new Sheet(EnUs);
        ThickBottomOnA1(sheet);
        var draft = FormatCellsDraft.Open(sheet, CellAddress.Parse("A2"), [CellRange.Parse("A2")]);

        draft.SetLineStyle(BorderLineStyle.Thick);
        draft.ToggleEdge(BorderEdge.Top);
        Assert.Equal(new BorderChange { Top = BorderLine.None }, draft.Change.Borders);
        sheet.SetCellFormat([CellRange.Parse("A2")], draft.Change);

        Assert.Equal(CellBorders.None, sheet.GetBorders(CellAddress.Parse("A1")));
        Assert.Equal(CellBorders.None, sheet.GetBorders(CellAddress.Parse("A2")));
    }
}
