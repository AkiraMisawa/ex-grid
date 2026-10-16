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

    [Fact] // ADR-0071 / SH-45, case 14-13: an edge shown from the neighbour and left alone is not touched, so OK sets nothing and A2 still records no top; the bottom takes the line the tab opened on, the thick one shown (run 17, h10)
    public void A_shown_edge_left_alone_sets_nothing_case_14_13()
    {
        var sheet = new Sheet(EnUs);
        ThickBottomOnA1(sheet);
        var draft = FormatCellsDraft.Open(sheet, CellAddress.Parse("A2"), [CellRange.Parse("A2")]);

        draft.ToggleEdge(BorderEdge.Bottom);

        Assert.Equal(new BorderChange { Bottom = Thick }, draft.Change.Borders);
    }

    [Fact] // ADR-0071 / SH-45, case 14-13 (observed in Excel, the seventeenth Windows run, h10): over A2, the Top button alone takes the shown top away, and OK clears that edge on both sides, as clearing an edge does
    public void Taking_a_shown_edge_away_clears_it_on_both_sides_case_14_13()
    {
        var sheet = new Sheet(EnUs);
        ThickBottomOnA1(sheet);
        var draft = FormatCellsDraft.Open(sheet, CellAddress.Parse("A2"), [CellRange.Parse("A2")]);

        // What the run pressed: the Top button (Alt+T), then OK. The tab opened on the thick line.
        draft.ToggleEdge(BorderEdge.Top);
        Assert.Equal(new BorderChange { Top = BorderLine.None }, draft.Change.Borders);
        sheet.SetCellFormat([CellRange.Parse("A2")], draft.Change);

        Assert.Equal(CellBorders.None, sheet.GetBorders(CellAddress.Parse("A1")));
        Assert.Equal(CellBorders.None, sheet.GetBorders(CellAddress.Parse("A2")));
    }

    // ---- The line the Border tab opens on (the seventeenth Windows run, h10) ----

    private static readonly CellColour Black = CellColour.FromRgb(0x000000);

    private static readonly CellColour Red = CellColour.FromRgb(0xFF0000);

    private static (BorderLineStyle, CellColour) PenOf(FormatCellsDraft draft) => (draft.LineStyle, draft.LineColour);

    [Fact] // ADR-0071 / SH-45, run 17 h10 (observed in Excel): over A2 under A1's thick bottom, the one line A2 shows, the Border tab opens on Thick and Automatic, as the line is, not on Thin
    public void Over_a2_under_a1s_thick_bottom_the_tab_opens_on_thick_run_17_h10()
    {
        var draft = Open(ThickBottomOnA1, "A2", "A2");

        Assert.Equal((BorderLineStyle.Thick, CellColour.Automatic), PenOf(draft));
        Assert.True(draft.Change.IsEmpty);
    }

    [Theory] // ADR-0071 / SH-45, case 14-13 (observed in Excel): A1's bottom thick black opens the tab on Thick and black, over A2, which shows it, and over A1, which records it
    [InlineData("A2")]
    [InlineData("A1")]
    public void The_line_opens_in_its_own_colour_case_14_13(string focus)
    {
        var draft = Open(sheet => Format(sheet, "A1", new CellFormatChange
        {
            Borders = new BorderChange { Bottom = new BorderLine(BorderLineStyle.Thick, Black) },
        }), focus, focus);

        Assert.Equal((BorderLineStyle.Thick, Black), PenOf(draft));
    }

    [Fact] // ADR-0071 / SH-45, run 17 h10's second case (observed in Excel): A2's top set thin after A1's thick bottom leaves one thin line on the edge, and the tab over A2 opens on Thin and Automatic
    public void A_thin_top_shown_opens_the_tab_on_thin_run_17_h10()
    {
        var draft = Open(sheet =>
        {
            ThickBottomOnA1(sheet);
            Format(sheet, "A2", new CellFormatChange { Borders = new BorderChange { Top = Thin } });
        }, "A2", "A2");

        Assert.Equal((BorderLineStyle.Thin, CellColour.Automatic), PenOf(draft));
    }

    [Theory] // ADR-0071 / SH-45, the readings of 2026-10-12 (R8 (1) to (3)), observed in Excel: Thin and Automatic over B2 with a thin top and a thick right of its own, over A2:B3 with a different line on four of its cells' sides, and over D5 with no line, as the eleventh run's case 22 saw Thin over A1
    [InlineData(1, "B2", "B2")]
    [InlineData(2, "A2", "A2:B3")]
    [InlineData(3, "D5", "D5")]
    public void Thin_and_automatic_over_the_three_selections_r8(int @case, string focus, string range)
    {
        var draft = Open(sheet =>
        {
            switch (@case)
            {
                case 1:
                    Format(sheet, "B2", new CellFormatChange { Borders = new BorderChange { Top = Thin, Right = Thick } });
                    break;
                case 2:
                    Format(sheet, "A2", new CellFormatChange { Borders = new BorderChange { Top = Thin } });
                    Format(sheet, "B2", new CellFormatChange { Borders = new BorderChange { Right = new BorderLine(BorderLineStyle.Medium) } });
                    Format(sheet, "A3", new CellFormatChange { Borders = new BorderChange { Bottom = Thick } });
                    Format(sheet, "B3", new CellFormatChange { Borders = new BorderChange { Left = new BorderLine(BorderLineStyle.Dashed) } });
                    break;
            }
        }, focus, range);

        Assert.Equal((BorderLineStyle.Thin, CellColour.Automatic), PenOf(draft));
    }

    [Theory] // ADR-0071 / SH-45 (2026-10-14, after run 18a, format-cells 2, 4 and 5, observed in Excel): B2 with a thick top and a medium right opens on the top's Thick; A2:B2 under A1:B1's thick bottom, and A2 and C2 under A1's and C1's, open on the Thick their top shows
    [InlineData("B2", "B2")]
    [InlineData("A2", "A2:B2")]
    [InlineData("A2", "A2,C2")]
    public void The_tab_opens_on_the_tops_line_over_a_cell_a_range_and_several_ranges_run_18a(string focus, string ranges)
    {
        var draft = Open(sheet =>
        {
            Format(sheet, "B2", new CellFormatChange { Borders = new BorderChange { Top = Thick, Right = new BorderLine(BorderLineStyle.Medium) } });
            Format(sheet, "A1", new CellFormatChange { Borders = new BorderChange { Bottom = Thick } });
            Format(sheet, "B1", new CellFormatChange { Borders = new BorderChange { Bottom = Thick } });
            Format(sheet, "C1", new CellFormatChange { Borders = new BorderChange { Bottom = Thick } });
        }, focus, ranges.Split(','));

        Assert.Equal((BorderLineStyle.Thick, CellColour.Automatic), PenOf(draft));
    }

    [Fact] // ADR-0071 / SH-45 (2026-10-14, after run 18a, format-cells 3, observed in Excel): B2 with a thick top and a thick bottom opens on Thick
    public void A_thick_top_and_bottom_open_the_tab_on_thick_run_18a()
    {
        var draft = Open(sheet => Format(sheet, "B2", new CellFormatChange { Borders = new BorderChange { Top = Thick, Bottom = Thick } }), "B2", "B2");

        Assert.Equal((BorderLineStyle.Thick, CellColour.Automatic), PenOf(draft));
    }

    [Theory] // ADR-0071 / SH-45, ticket 102 (2026-10-14, after run 18a, format-cells 7, observed in Excel): B2's thick red right over C2's own thin blue left opens the tab on the drawn line, Thick and red, over C2 and over B2
    [InlineData("C2")]
    [InlineData("B2")]
    public void The_tab_opens_on_the_drawn_line_not_the_cells_own_ticket_102_run_18a(string cell)
    {
        var thickRed = new BorderLine(BorderLineStyle.Thick, Red);
        var draft = Open(sheet =>
        {
            Format(sheet, "C2", new CellFormatChange { Borders = new BorderChange { Left = new BorderLine(BorderLineStyle.Thin, CellColour.FromRgb(0x0000FF)) } });
            Format(sheet, "B2", new CellFormatChange { Borders = new BorderChange { Right = thickRed } });
        }, cell, cell);

        Assert.Equal((BorderLineStyle.Thick, Red), PenOf(draft));
    }

    [Theory] // ADR-0071 / SH-45 (2026-10-14, after run 18a, format-cells 1 and 6, observed in Excel): one line shown on a left or a right edge, a neighbour's as drawn included, in Medium Dashed, or in red, opens the tab on that line
    [InlineData("B2's left thick", "B2")]
    [InlineData("B2's right thick", "B2")]
    [InlineData("C2's left thick, shown as B2's right", "B2")]
    [InlineData("A1's bottom medium dashed", "A2")]
    [InlineData("A1's bottom thick red", "A2")]
    public void One_line_on_any_edge_in_any_style_opens_the_tab_on_it_run_18a(string line, string cell)
    {
        var mediumDashed = new BorderLine(BorderLineStyle.MediumDashed);
        var thickRed = new BorderLine(BorderLineStyle.Thick, Red);
        var (at, borders, expected) = line switch
        {
            "B2's left thick" => ("B2", new BorderChange { Left = Thick }, Thick),
            "B2's right thick" => ("B2", new BorderChange { Right = Thick }, Thick),
            "C2's left thick, shown as B2's right" => ("C2", new BorderChange { Left = Thick }, Thick),
            "A1's bottom medium dashed" => ("A1", new BorderChange { Bottom = mediumDashed }, mediumDashed),
            "A1's bottom thick red" => ("A1", new BorderChange { Bottom = thickRed }, thickRed),
            _ => throw new ArgumentOutOfRangeException(nameof(line), line, "Not a case."),
        };
        var draft = Open(sheet => Format(sheet, at, new CellFormatChange { Borders = borders }), cell, cell);

        Assert.Equal((expected.Style, expected.Colour), PenOf(draft));
        Assert.True(draft.Change.IsEmpty);
    }

    // ---- A code typed under Custom (the fourteenth Windows run, case 11) ----

    private static FormatCellsDraft OpenIn(string culture, NumberFormat? format)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo(culture));
        if (format is not null) sheet.SetNumberFormat(CellAddress.Parse("A1"), format);
        return FormatCellsDraft.Open(sheet, CellAddress.Parse("A1"), [CellRange.Parse("A1")]);
    }

    private static NumberFormat? Typed(string culture, string code)
    {
        var draft = OpenIn(culture, null);
        draft.SelectCategory(NumberFormatCategory.Custom);
        draft.SetType(code);
        return draft.Change.NumberFormat;
    }

    [Fact] // ADR-0071 / SH-45, case 14-11: under ja-JP, dd-mmm-yy typed under Custom is built-in 15, and d-mmm-yy is a code of its own
    public void A_typed_code_is_a_built_in_only_where_it_spells_it_case_14_11()
    {
        Assert.Equal("d-mmm-yy", Typed("ja-JP", "dd-mmm-yy")!.Code);
        var own = Typed("ja-JP", "d-mmm-yy")!;
        Assert.NotEqual("d-mmm-yy", own.Code);
        Assert.Equal("d-mmm-yy", own.LocalCode(CultureInfo.GetCultureInfo("ja-JP")));
        // Under en-US the built-in spells d-mmm-yy, so typed there it is the built-in.
        Assert.Equal("d-mmm-yy", Typed("en-US", "d-mmm-yy")!.Code);
    }

    [Fact] // ADR-0071 / SH-45, case 14-11: a code of its own opens under Custom spelled as it was typed, and OK without a change keeps it
    public void A_code_of_its_own_opens_as_it_was_typed_case_14_11()
    {
        var draft = OpenIn("ja-JP", Typed("ja-JP", "d-mmm-yy"));

        Assert.Equal(NumberFormatCategory.Custom, draft.Category);
        Assert.Equal("d-mmm-yy", draft.TypeCode);
        Assert.True(draft.Change.IsEmpty);
        draft.SelectCategory(NumberFormatCategory.Custom);
        Assert.Equal(Typed("ja-JP", "d-mmm-yy"), draft.Change.NumberFormat);
    }

    [Fact] // ADR-0071 / SH-45, case 14-11: Custom starts from the built-in shown as the culture spells it, so OK keeps the built-in
    public void Custom_starts_from_a_built_in_as_the_culture_spells_it_case_14_11()
    {
        var draft = OpenIn("ja-JP", NumberFormat.Parse("d-mmm-yy"));
        Assert.Equal(NumberFormatCategory.Date, draft.Category);

        draft.SelectCategory(NumberFormatCategory.Custom);

        Assert.Equal("dd-mmm-yy", draft.TypeCode);
        Assert.Equal("d-mmm-yy", draft.Change.NumberFormat!.Code);
    }

    [Fact] // ADR-0071 / SH-45, case 14-11: a code typed under Custom stays as typed when Custom is chosen again
    public void A_typed_code_stays_as_typed_when_custom_is_chosen_again_case_14_11()
    {
        var draft = OpenIn("ja-JP", null);
        draft.SelectCategory(NumberFormatCategory.Custom);
        draft.SetType("d-mmm-yy");

        draft.SelectCategory(NumberFormatCategory.Custom);

        Assert.Equal("d-mmm-yy", draft.TypeCode);
        Assert.Equal(Typed("ja-JP", "d-mmm-yy"), draft.Change.NumberFormat);
    }

    [Fact] // ADR-0071 / SH-45, case 14-11: the currency key's built-in under en-GB opens under Custom in en-GB's spelling, and set again it stays the built-in
    public void A_built_in_opening_under_custom_is_spelled_as_the_culture_spells_it_case_14_11()
    {
        var enGb = CultureInfo.GetCultureInfo("en-GB");
        var draft = OpenIn("en-GB", NumberFormat.BuiltInCurrency(enGb));

        Assert.Equal(NumberFormatCategory.Custom, draft.Category);
        Assert.Equal("£#,##0.00;[Red]-£#,##0.00", draft.TypeCode);
        draft.SelectCategory(NumberFormatCategory.Custom);
        Assert.Equal(NumberFormat.BuiltInCurrency(enGb), draft.Change.NumberFormat);
    }

    [Fact] // ADR-0071 / SH-45, case 14-11: Custom lists the codes as the culture spells them, so the built-in short date is listed in en-GB's spelling, once
    public void Custom_lists_codes_as_the_culture_spells_them_case_14_11()
    {
        var draft = OpenIn("en-GB", null);
        draft.SelectCategory(NumberFormatCategory.Custom);

        Assert.Contains("dd/mm/yyyy", draft.Types);
        Assert.Contains("dd-mmm-yy", draft.Types);
        Assert.Contains("hh:mm", draft.Types);
        Assert.DoesNotContain("m/d/yyyy", draft.Types);
        Assert.Equal(draft.Types.Count, draft.Types.Distinct().Count());
        draft.SetType("dd/mm/yyyy");
        Assert.Equal("m/d/yyyy", draft.Change.NumberFormat!.Code);
        // Under en-US the list is spelled as it always was.
        var enUs = OpenIn("en-US", null);
        enUs.SelectCategory(NumberFormatCategory.Custom);
        Assert.Equal(NumberFormatCodes.CustomTypes, enUs.Types);
    }

    // ---- Normal font (ADR-0071, 2026-10-02; Part C, case 24) ----

    [Fact] // SH-55: the box shows checked exactly while every part of the Font is the default
    public void Normal_font_shows_checked_while_every_part_is_the_default()
    {
        Assert.True(Open(null, "A1", "A1").IsNormalFont);
        Assert.False(Open(sheet => Format(sheet, "A1", new() { Bold = true }), "A1", "A1").IsNormalFont);
        Assert.False(Open(sheet => Format(sheet, "A1", new() { FontColour = CellColour.FromRgb(0xFF0000) }), "A1", "A1").IsNormalFont);
        // Bold differing across the Selection shows the Font style empty, which is not the default.
        Assert.False(Open(sheet => Format(sheet, "A2", new() { Bold = true }), "A1", "A1:A2").IsNormalFont);

        var draft = Open(null, "A1", "A1");
        draft.SetUnderline(true);
        Assert.False(draft.IsNormalFont);
    }

    [Fact] // SH-55: checking it sets every part of the Font to its default, and OK records each one
    public void Normal_font_sets_every_part_to_its_default()
    {
        var draft = Open(sheet => Format(sheet, "A1", new() { Bold = true, Italic = true, Underline = true, Strikethrough = true, FontColour = CellColour.FromRgb(0x0000FF) }), "A1", "A1");

        draft.SetNormalFont();

        Assert.True(draft.IsNormalFont);
        var change = draft.Change;
        Assert.Equal(CellColour.Automatic, change.FontColour);
        Assert.Equal(false, change.Bold);
        Assert.Equal(false, change.Italic);
        Assert.Equal(false, change.Underline);
        Assert.Equal(false, change.Strikethrough);
        Assert.Null(change.Fill);
        Assert.Null(change.NumberFormat);
    }

    [Fact] // SH-55: OK records the default Font on the cell, over its row's Font
    public void Normal_font_is_recorded_over_a_rows_font()
    {
        var sheet = new Sheet(EnUs);
        Format(sheet, "2:2", new() { Bold = true, FontColour = CellColour.FromRgb(0xFF0000) });
        var draft = FormatCellsDraft.Open(sheet, CellAddress.Parse("B2"), [CellRange.Parse("B2")]);

        draft.SetNormalFont();
        sheet.SetCellFormat([CellRange.Parse("B2")], draft.Change);

        Assert.Equal(default, sheet.GetCellFormat(CellAddress.Parse("B2")).Font);
        Assert.True(sheet.GetCellFormat(CellAddress.Parse("C2")).Font.Bold);
    }
}
