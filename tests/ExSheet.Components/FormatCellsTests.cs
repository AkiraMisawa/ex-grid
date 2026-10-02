using System.Globalization;
using AngleSharp.Dom;
using Bunit;
using ExGrid.Chrome;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using FontStyle = ExSheet.FontStyle;
using HorizontalAlignment = ExSheet.Engine.HorizontalAlignment;

namespace ExSheet.Components.Tests;

/// <summary>
/// Format Cells (ticket 52, ADR-0071 "Format Cells, a Chrome seam whose frame the Chrome chooses";
/// SH-45, DC-60): what ExSheet offers and what OK means, the built-in Chrome's popover in the grid's
/// frame (ADR-0050 item 16), what opens it, and what the eleventh Windows run found (cases 22–26).
/// </summary>
public class FormatCellsTests : SheetTestContext
{
    private static readonly CellColour Red = CellColour.FromRgb(0xFF0000);
    private static readonly CellFill RedFill = CellFill.Solid(Red);
    private static readonly BorderLine Thick = new(BorderLineStyle.Thick);
    private static readonly BorderLine Thin = new(BorderLineStyle.Thin);

    private static SheetDocument DocumentOf(Action<Sheet>? prepare = null, params (string Address, string Typed)[] cells)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        foreach (var (address, typed) in cells) sheet.Enter(CellAddress.Parse(address), typed);
        prepare?.Invoke(sheet);
        return sheet.ToDocument();
    }

    private static void Format(Sheet sheet, string range, CellFormatChange change) =>
        sheet.SetCellFormat([CellRange.Parse(range)], change);

    private static CellFormat FormatAt(IRenderedComponent<ExSheet> cut, string address) =>
        cut.Instance.CellFormatAt(CellAddress.Parse(address));

    private static IElement Frame(IRenderedComponent<ExSheet> cut) => cut.Find(".ex-popover-consumer");

    private static bool IsOpen(IRenderedComponent<ExSheet> cut) => cut.FindAll(".ex-format-cells").Count > 0;

    private static async Task OpenAsync(IRenderedComponent<ExSheet> cut, string selection)
    {
        await GoToAsync(cut, selection);
        Assert.True(await cut.Instance.OpenFormatCellsAsync());
        cut.WaitForAssertion(() => Assert.True(IsOpen(cut)));
    }

    private static IReadOnlyList<string> Tabs(IRenderedComponent<ExSheet> cut) =>
        [.. cut.FindAll(".ex-format-cells-tab").Select(t => t.TextContent)];

    private static string SelectedTab(IRenderedComponent<ExSheet> cut) =>
        cut.FindAll(".ex-format-cells-tab").Single(t => t.GetAttribute("aria-selected") == "true").TextContent;

    private static Task ShowTabAsync(IRenderedComponent<ExSheet> cut, string tab) =>
        cut.FindAll(".ex-format-cells-tab").Single(t => t.TextContent == tab).ClickAsync(new MouseEventArgs());

    // A choice in one of Format Cells' groups, by the text of its label.
    private static IElement Choice(IRenderedComponent<ExSheet> cut, string group, string label) =>
        cut.FindAll($"{group} label").Single(l => l.TextContent.Trim() == label).QuerySelector("input")!;

    private static Task ChooseAsync(IRenderedComponent<ExSheet> cut, string group, string label) =>
        Choice(cut, group, label).ChangeAsync(new ChangeEventArgs { Value = "on" });

    private static Task SwatchAsync(IRenderedComponent<ExSheet> cut, string name) =>
        cut.FindAll(".ex-format-cells-swatch").First(s => s.GetAttribute("aria-label") == name).ChangeAsync(new ChangeEventArgs { Value = "on" });

    private static Task OkAsync(IRenderedComponent<ExSheet> cut) => cut.Find("form.ex-format-cells").SubmitAsync();

    private static Task CancelAsync(IRenderedComponent<ExSheet> cut) => cut.Find(".ex-format-cells-cancel").ClickAsync(new MouseEventArgs());

    private static double HeadingWidth(IRenderedComponent<ExSheet> cut) =>
        double.Parse(cut.Find(".ex-row-heading").GetAttribute("style")!.Replace("width:", "").Replace("px", "").Trim(), CultureInfo.InvariantCulture);

    private static Task SecondaryClickAsync(IRenderedComponent<ExSheet> cut, string address)
    {
        var at = CellAddress.Parse(address);
        return cut.Find(".ex-viewport").ContextMenuAsync(new MouseEventArgs
        {
            Button = 2,
            OffsetX = HeadingWidth(cut) + at.Column * SheetColumns.DefaultWidthPx + 5,
            OffsetY = at.Row * ExSheet.DefaultRowHeightPx + 5,
        });
    }

    // ---- What opens it ----

    [Fact] // ADR-0071 / SH-45 / DC-60: OpenFormatCellsAsync shows Format Cells as a popover in the grid's frame, inside the Sheet's box
    public async Task Open_format_cells_shows_it_in_the_grids_popover_frame()
    {
        var cut = RenderSheet();

        await OpenAsync(cut, "B2");

        var frame = Frame(cut);
        Assert.Equal("dialog", frame.GetAttribute("role"));
        Assert.Equal("Format Cells", frame.GetAttribute("aria-label"));
        Assert.NotNull(frame.QuerySelector(".ex-format-cells"));
        Assert.Contains("max-height:", frame.GetAttribute("style"));
    }

    [Fact] // ADR-0071 / ADR-0050 item 14 / SH-45, case 22: Ctrl+1 opens it, and sets nothing itself
    public async Task Ctrl_1_opens_it()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "B2");

        await PressAsync(cut, "1", ctrl: true);

        cut.WaitForAssertion(() => Assert.True(IsOpen(cut)));
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0071 / ADR-0036 / SH-45: the Context Menu's "Format Cells…" opens it
    public async Task The_context_menu_opens_it()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "B2");
        await SecondaryClickAsync(cut, "B2");

        await cut.FindAll("[role=menu] button[role=menuitem]").Single(b => b.TextContent == "Format Cells…").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => Assert.True(IsOpen(cut)));
        Assert.Empty(cut.FindAll("[role=menu]"));
    }

    [Fact] // ADR-0071 / SH-43 / SH-29: while an edit is open OpenFormatCellsAsync is refused by name, and nothing opens
    public async Task Open_format_cells_is_refused_while_an_edit_is_open()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "B2");
        await PressAsync(cut, "4");
        await TypeAsync(cut, "42");

        var refused = await Assert.ThrowsAsync<SheetRefusedException>(cut.Instance.OpenFormatCellsAsync);

        Assert.Equal(SheetRefusalReason.EditIsOpen, refused.Refusal.Reason);
        Assert.False(IsOpen(cut));
        Assert.Equal("42", EditorText(cut));
    }

    [Fact] // ADR-0071: with nothing selected there is nothing to format, and nothing opens
    public async Task With_nothing_selected_nothing_opens()
    {
        var cut = RenderSheet();

        Assert.False(await cut.Instance.OpenFormatCellsAsync());

        Assert.False(IsOpen(cut));
    }

    // ---- The tabs (case 22, case 26) ----

    [Fact] // ADR-0071 / SH-45, case 22: the tabs are Number, Alignment, Font, Border and Fill, in Excel's order, and the first opening is on Number
    public async Task The_tabs_are_excels_order_and_the_first_opening_is_on_number()
    {
        var cut = RenderSheet();

        await OpenAsync(cut, "B2");

        Assert.Equal(["Number", "Alignment", "Font", "Border", "Fill"], Tabs(cut));
        Assert.Equal("Number", SelectedTab(cut));
        Assert.Equal("tablist", cut.Find(".ex-format-cells-tabs").GetAttribute("role"));
        // ARIA's tabs pattern: one tab stop, the tab shown.
        Assert.Equal(["0", "-1", "-1", "-1", "-1"], cut.FindAll(".ex-format-cells-tab").Select(t => t.GetAttribute("tabindex")));
    }

    [Fact] // ADR-0071 / SH-45, case 22: a later opening reopens on the last tab shown, per Sheet instance
    public async Task A_later_opening_reopens_on_the_last_tab_shown_in_this_sheet()
    {
        var cut = RenderSheet();
        await OpenAsync(cut, "B2");
        await ShowTabAsync(cut, "Border");
        await CancelAsync(cut);
        cut.WaitForAssertion(() => Assert.False(IsOpen(cut)));

        await OpenAsync(cut, "C3");
        Assert.Equal("Border", SelectedTab(cut));

        // Another Sheet keeps its own: its first opening is on Number.
        var other = RenderSheet();
        await OpenAsync(other, "B2");
        Assert.Equal("Number", SelectedTab(other));
    }

    [Theory] // ADR-0071 / SH-45: the tabs switch with the arrow keys, wrapping, and Home and End (ARIA's tabs pattern); Ctrl+Tab is the browser's
    [InlineData("ArrowRight", "Alignment")]
    [InlineData("ArrowLeft", "Fill")]
    [InlineData("End", "Fill")]
    [InlineData("Home", "Number")]
    public async Task The_arrow_keys_switch_the_tabs(string key, string shown)
    {
        var cut = RenderSheet();
        await OpenAsync(cut, "B2");

        await cut.FindAll(".ex-format-cells-tab").Single(t => t.TextContent == "Number").KeyDownAsync(new KeyboardEventArgs { Key = key });

        Assert.Equal(shown, SelectedTab(cut));
        Assert.Equal(shown, cut.Find($"#{cut.Find(".ex-format-cells-panel").GetAttribute("aria-labelledby")}").TextContent);
    }

    // ---- The Number tab (case 22) ----

    [Fact] // ADR-0071 / SH-45, case 22: the categories in Excel's order; Accounting, Fraction and Special shown disabled, each with its reason
    public async Task The_categories_are_excels_and_three_are_disabled_with_the_reason()
    {
        var cut = RenderSheet();
        await OpenAsync(cut, "B2");

        var labels = cut.FindAll(".ex-format-cells-categories label").Select(l => l.TextContent.Trim()).ToList();
        Assert.Equal(["General", "Number", "Currency", "Accounting", "Date", "Time", "Percentage", "Fraction", "Scientific", "Text", "Special", "Custom"], labels);
        foreach (var category in new[] { "Accounting", "Fraction", "Special" })
        {
            var input = Choice(cut, ".ex-format-cells-categories", category);
            Assert.True(input.HasAttribute("disabled"));
            var reason = cut.Find($"#{input.GetAttribute("aria-describedby")}").TextContent;
            Assert.False(string.IsNullOrWhiteSpace(reason));
        }
        Assert.Contains("*", cut.Find($"#{Choice(cut, ".ex-format-cells-categories", "Accounting").GetAttribute("aria-describedby")}").TextContent);
        Assert.False(Choice(cut, ".ex-format-cells-categories", "Custom").HasAttribute("disabled"));
    }

    [Fact] // ADR-0071 / SH-45: a Custom code ExSheet does not read is refused by name, Format Cells stays open, and nothing is set
    public async Task A_custom_code_the_engine_does_not_read_is_refused_by_name()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(cells: ("B2", "12"))));
        await OpenAsync(cut, "B2");
        var before = cut.Instance.ToDocument().ToJson();
        await ChooseAsync(cut, ".ex-format-cells-categories", "Custom");

        await cut.Find(".ex-format-cells-code").InputAsync(new ChangeEventArgs { Value = "[<0]0;0" });
        await OkAsync(cut);

        Assert.True(IsOpen(cut));
        var alert = cut.Find(".ex-format-cells-refusal[role=alert]").TextContent;
        Assert.Contains("'[<0]0;0'", alert);
        Assert.Contains("conditions", alert);
        Assert.Equal(before, cut.Instance.ToDocument().ToJson());
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0071 / SH-45: a Custom code the engine reads is set
    public async Task A_custom_code_the_engine_reads_is_set()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(cells: ("B2", "12"))));
        await OpenAsync(cut, "B2");
        await ChooseAsync(cut, ".ex-format-cells-categories", "Custom");

        await cut.Find(".ex-format-cells-code").InputAsync(new ChangeEventArgs { Value = "0.000" });
        await OkAsync(cut);

        cut.WaitForAssertion(() => Assert.False(IsOpen(cut)));
        Assert.Equal("0.000", FormatAt(cut, "B2").NumberFormat.Code);
        Assert.Equal("12.000", CellText(cut, "B2"));
    }

    [Fact] // ADR-0071 / SH-45: Format Cells opens on the Focus cell's Number Format, its category and its options
    public async Task It_opens_on_the_focus_cells_number_format()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(
            sheet => Format(sheet, "B2", new CellFormatChange { NumberFormat = NumberFormat.Parse("0.0%") }), ("B2", "0.25"))));

        await OpenAsync(cut, "B2");

        Assert.True(Choice(cut, ".ex-format-cells-categories", "Percentage").HasAttribute("checked"));
        Assert.Equal("1", cut.Find(".ex-format-cells-places").GetAttribute("value"));
    }

    [Fact] // ADR-0071 / SH-45: Number with three places and the thousands separator sets #,##0.000
    public async Task Number_with_places_and_a_separator_sets_its_code()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(cells: ("B2", "1234.5"))));
        await OpenAsync(cut, "B2");
        await ChooseAsync(cut, ".ex-format-cells-categories", "Number");

        await cut.Find(".ex-format-cells-places").InputAsync(new ChangeEventArgs { Value = "3" });
        await cut.Find(".ex-format-cells-separator").ChangeAsync(new ChangeEventArgs { Value = true });
        await OkAsync(cut);

        Assert.Equal("#,##0.000", FormatAt(cut, "B2").NumberFormat.Code);
        Assert.Equal("1,234.500", CellText(cut, "B2"));
    }

    // ---- The Alignment tab ----

    [Fact] // ADR-0071 / SH-45: Alignment is horizontal only, and OK sets the one chosen
    public async Task Alignment_is_horizontal_only()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(cells: ("B2", "12"))));
        await OpenAsync(cut, "B2");
        await ShowTabAsync(cut, "Alignment");

        var panel = cut.Find(".ex-format-cells-panel");
        Assert.Single(panel.QuerySelectorAll("select"));
        Assert.Equal(["General", "Left", "Centre", "Right"], panel.QuerySelectorAll("option").Select(o => o.TextContent));
        await cut.Find(".ex-format-cells-alignment").ChangeAsync(new ChangeEventArgs { Value = "Right" });
        await OkAsync(cut);

        Assert.Equal(HorizontalAlignment.Right, FormatAt(cut, "B2").Alignment);
    }

    // ---- What differs across the Selection (case 24) ----

    [Fact] // ADR-0071 / SH-45, case 24: a differing bold shows the Font style empty, and a differing Fill as No Colour
    public async Task Differing_parts_are_shown_as_excel_shows_them()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(sheet =>
            Format(sheet, "A1", new CellFormatChange { Bold = true, Fill = RedFill, Borders = new BorderChange { Bottom = Thick } }))));

        await OpenAsync(cut, "A1:A2");

        await ShowTabAsync(cut, "Font");
        Assert.All(cut.FindAll(".ex-format-cells-font-styles input"), input => Assert.False(input.HasAttribute("checked")));
        await ShowTabAsync(cut, "Fill");
        Assert.True(Choice(cut, ".ex-format-cells-palette", "No Colour").HasAttribute("checked"));
        Assert.DoesNotContain(cut.FindAll(".ex-format-cells-swatch"), s => s.HasAttribute("checked"));
    }

    [Fact] // ADR-0071 / SH-45, case 24: an inside edge whose cells' sides differ is drawn as a grey dotted line, its button mixed
    public async Task A_differing_inside_edge_is_a_grey_dotted_line()
    {
        // A1's bottom is thick and A2's is not: whatever A2's top records, the inside edges of A1:A3 differ.
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(sheet =>
            Format(sheet, "A1", new CellFormatChange { Borders = new BorderChange { Bottom = Thick } }))));

        await OpenAsync(cut, "A1:A3");
        await ShowTabAsync(cut, "Border");

        var differs = cut.Find(".ex-format-cells-preview .ex-format-cells-differs");
        Assert.Equal("InsideHorizontal", differs.GetAttribute("data-edge"));
        Assert.Equal("#808080", differs.GetAttribute("stroke"));
        Assert.Equal("mixed", cut.Find(".ex-format-cells-edge[data-edge=InsideHorizontal]").GetAttribute("aria-pressed"));
        // The outline records nothing anywhere: no outer line, and not mixed either.
        Assert.Equal("false", cut.Find(".ex-format-cells-edge[data-edge=Top]").GetAttribute("aria-pressed"));
    }

    // ---- OK, Cancel and Escape (cases 25 and 26) ----

    [Fact] // ADR-0071 / SH-45, case 25: OK sets only the part touched — the colour — and each cell keeps its own emphasis
    public async Task Ok_sets_only_what_was_touched()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(sheet =>
        {
            Format(sheet, "A1", new CellFormatChange { Bold = true });
            Format(sheet, "A2", new CellFormatChange { Italic = true });
        })));
        await OpenAsync(cut, "A1:A2");
        await ShowTabAsync(cut, "Font");

        await SwatchAsync(cut, "Red");
        await OkAsync(cut);

        Assert.Equal(new CellFont(Red, Bold: true), FormatAt(cut, "A1").Font);
        Assert.Equal(new CellFont(Red, Italic: true), FormatAt(cut, "A2").Font);
    }

    [Fact] // ADR-0071 / ADR-0048 / SH-45: what OK sets is one undo step, however many parts and tabs it spans
    public async Task Ok_is_one_undo_step()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(cells: ("B2", "12"))));
        var before = cut.Instance.CellFormatAt(CellAddress.Parse("B2"));
        await OpenAsync(cut, "B2:C3");
        await ShowTabAsync(cut, "Font");
        await ChooseAsync(cut, ".ex-format-cells-font-styles", "Bold");
        await ShowTabAsync(cut, "Fill");
        await SwatchAsync(cut, "Yellow");
        await ShowTabAsync(cut, "Border");
        await cut.FindAll(".ex-format-cells-preset").Single(b => b.TextContent == "Outline").ClickAsync(new MouseEventArgs());

        await OkAsync(cut);
        var after = FormatAt(cut, "B2");
        Assert.True(after.Font.Bold);
        Assert.Equal(CellFill.Solid(CellColour.FromRgb(0xFFFF00)), after.Fill);
        Assert.Equal(Thin, after.Borders.Top);
        Assert.Equal(Thin, after.Borders.Left);

        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal(before, FormatAt(cut, "B2"));
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0071 / SH-45: OK with nothing touched sets nothing, raises nothing and records no step, and closes
    public async Task Ok_with_nothing_touched_calls_nothing()
    {
        var raised = new List<SheetDocument>();
        var cut = RenderSheet(ps => ps.Add(s => s.DocumentChanged, raised.Add));
        await OpenAsync(cut, "B2");
        // Looking at every tab touches nothing.
        foreach (var tab in Tabs(cut)) await ShowTabAsync(cut, tab);

        await OkAsync(cut);

        cut.WaitForAssertion(() => Assert.False(IsOpen(cut)));
        Assert.Empty(raised);
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0071 / SH-45: Cancel sets nothing, whatever was touched, and the keyboard is the Sheet's again
    public async Task Cancel_sets_nothing_and_returns_the_keyboard()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(cells: ("B2", "12"))));
        await OpenAsync(cut, "B2");
        var before = cut.Instance.ToDocument().ToJson();
        await ShowTabAsync(cut, "Font");
        await ChooseAsync(cut, ".ex-format-cells-font-styles", "Bold");
        var reclaims = ReclaimCount;

        await CancelAsync(cut);

        cut.WaitForAssertion(() => Assert.False(IsOpen(cut)));
        Assert.Equal(before, cut.Instance.ToDocument().ToJson());
        Assert.True(ReclaimCount > reclaims);
    }

    [Fact] // ADR-0071 / SH-45 / DC-60: Escape sets nothing, closes Format Cells, and the keyboard is the Sheet's again
    public async Task Escape_sets_nothing()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(cells: ("B2", "12"))));
        await OpenAsync(cut, "B2");
        var before = cut.Instance.ToDocument().ToJson();
        await ShowTabAsync(cut, "Font");
        await ChooseAsync(cut, ".ex-format-cells-font-styles", "Bold");
        var reclaims = ReclaimCount;

        var grid = Grid(cut);
        await grid.InvokeAsync(() => grid.Instance.OnKeyAsync("Escape", false, false, false, false, false, fromDescendant: true));

        Assert.False(IsOpen(cut));
        Assert.Equal(before, cut.Instance.ToDocument().ToJson());
        Assert.True(ReclaimCount > reclaims);
    }

    [Fact] // ADR-0071: a Selection moved under Format Cells — by the Name Box — ends it, as its OK would set the other cells
    public async Task A_selection_moved_under_it_ends_it()
    {
        var cut = RenderSheet();
        await OpenAsync(cut, "B2");

        await GoToAsync(cut, "D5");

        cut.WaitForAssertion(() => Assert.False(IsOpen(cut)));
    }

    // ---- The palette (case 23) ----

    [Fact] // ADR-0071 / SH-45, case 23: the Fill tab offers No Colour, the theme's sixty and the ten standard colours, as the Fill tab names them
    public async Task The_fill_palette_is_case_23s()
    {
        var cut = RenderSheet();
        await OpenAsync(cut, "B2");
        await ShowTabAsync(cut, "Fill");

        var swatches = cut.FindAll(".ex-format-cells-swatch");
        Assert.Equal(70, swatches.Count);
        Assert.NotNull(Choice(cut, ".ex-format-cells-palette", "No Colour"));
        Assert.DoesNotContain(cut.FindAll(".ex-format-cells-palette label"), l => l.TextContent.Trim() == "Automatic");
        string Rgb(string name) => swatches.Single(s => s.GetAttribute("aria-label") == name).GetAttribute("data-rgb")!;
        Assert.Equal("#156082", Rgb("Dark Teal, Accent 1"));
        Assert.Equal("#DAE9F8", Rgb("Dark Blue, Text 2, Lighter 90%"));
        // Both of the Fill tab's "Grey" tints are #7F7F7F.
        Assert.Equal("#7F7F7F", Rgb("Black, Text 1, Lighter 50%"));
        Assert.Equal("#7F7F7F", Rgb("White, Background 1, Darker 50%"));
        Assert.Equal("#002060", Rgb("Dark Blue"));
    }

    [Fact] // ADR-0071 / SH-45, case 23: the Font tab offers Automatic, not No Colour
    public async Task The_font_palette_offers_automatic()
    {
        var cut = RenderSheet();
        await OpenAsync(cut, "B2");
        await ShowTabAsync(cut, "Font");

        Assert.True(Choice(cut, ".ex-format-cells-palette", "Automatic").HasAttribute("checked"));
        Assert.DoesNotContain(cut.FindAll(".ex-format-cells-palette label"), l => l.TextContent.Trim() == "No Colour");
    }

    [Fact] // ADR-0071 / SH-45: More Colours takes a hex value
    public async Task More_colours_takes_a_hex_value()
    {
        var cut = RenderSheet();
        await OpenAsync(cut, "B2");
        await ShowTabAsync(cut, "Fill");

        await cut.Find(".ex-format-cells-hex").InputAsync(new ChangeEventArgs { Value = "#1f4e79" });
        await OkAsync(cut);

        Assert.Equal(CellFill.Solid(CellColour.FromRgb(0x1F4E79)), FormatAt(cut, "B2").Fill);
    }

    [Fact] // ADR-0071 / SH-45: text under More Colours that is not a colour is refused by name, and nothing is set
    public async Task More_colours_refuses_text_that_is_not_a_colour()
    {
        var cut = RenderSheet();
        await OpenAsync(cut, "B2");
        await ShowTabAsync(cut, "Fill");

        await cut.Find(".ex-format-cells-hex").InputAsync(new ChangeEventArgs { Value = "#12" });
        await OkAsync(cut);

        Assert.True(IsOpen(cut));
        Assert.Contains("'#12' is not a colour", cut.Find(".ex-format-cells-refusal[role=alert]").TextContent);
        Assert.False(cut.Instance.CanUndo);
    }

    // ---- The Border tab (case 22) ----

    [Fact] // ADR-0071 / SH-45, case 22: the thirteen styles and None in Excel's two columns, Thin chosen on opening
    public async Task The_line_styles_are_excels_two_columns()
    {
        var cut = RenderSheet();
        await OpenAsync(cut, "B2");
        await ShowTabAsync(cut, "Border");

        var columns = cut.FindAll(".ex-format-cells-line-column")
            .Select(column => column.QuerySelectorAll("input").Select(i => i.GetAttribute("aria-label")!).ToArray())
            .ToArray();
        Assert.Equal(["None", "Hair", "Dotted", "Dash-dot-dot", "Dash-dot", "Dashed", "Thin"], columns[0]);
        Assert.Equal(["Medium Dash-dot-dot", "Slanted Dash-dot", "Medium Dash-dot", "Medium Dashed", "Medium", "Thick", "Double"], columns[1]);
        var chosen = cut.FindAll(".ex-format-cells-line-column input").Single(i => i.HasAttribute("checked"));
        Assert.Equal("Thin", chosen.GetAttribute("aria-label"));
    }

    [Fact] // ADR-0071 / SH-45, case 22: for one cell Inside, Horizontal and Vertical are disabled, and there are no diagonals
    public async Task For_one_cell_the_inside_edges_are_disabled()
    {
        var cut = RenderSheet();
        await OpenAsync(cut, "B2");
        await ShowTabAsync(cut, "Border");

        Assert.Equal(["None", "Outline", "Inside"], cut.FindAll(".ex-format-cells-preset").Select(b => b.TextContent));
        Assert.True(cut.FindAll(".ex-format-cells-preset").Single(b => b.TextContent == "Inside").HasAttribute("disabled"));
        Assert.Equal(["Top", "Horizontal", "Bottom", "Left", "Vertical", "Right"], cut.FindAll(".ex-format-cells-edge").Select(b => b.TextContent));
        Assert.True(cut.Find(".ex-format-cells-edge[data-edge=InsideHorizontal]").HasAttribute("disabled"));
        Assert.True(cut.Find(".ex-format-cells-edge[data-edge=InsideVertical]").HasAttribute("disabled"));
        Assert.False(cut.Find(".ex-format-cells-edge[data-edge=Top]").HasAttribute("disabled"));
    }

    [Fact] // ADR-0071 / SH-45, case 22: over several rows Inside and Horizontal are offered, and over one column Vertical is not
    public async Task Over_a_column_of_cells_horizontal_is_offered_and_vertical_is_not()
    {
        var cut = RenderSheet();
        await OpenAsync(cut, "B2:B4");
        await ShowTabAsync(cut, "Border");

        Assert.False(cut.FindAll(".ex-format-cells-preset").Single(b => b.TextContent == "Inside").HasAttribute("disabled"));
        Assert.False(cut.Find(".ex-format-cells-edge[data-edge=InsideHorizontal]").HasAttribute("disabled"));
        Assert.True(cut.Find(".ex-format-cells-edge[data-edge=InsideVertical]").HasAttribute("disabled"));
    }

    [Fact] // ADR-0071, the fourteenth run's case 14: Inside over rows 3:4 sets every vertical side, A's left and XFD's right included, and the line between the rows, as one undo step
    public async Task Inside_over_whole_rows_sets_every_vertical_side_case_14_14()
    {
        var cut = RenderSheet();
        await OpenAsync(cut, "3:4");
        await ShowTabAsync(cut, "Border");
        await cut.FindAll(".ex-format-cells-preset").Single(b => b.TextContent == "Inside").ClickAsync(new MouseEventArgs());

        await OkAsync(cut);

        Assert.Equal(new CellBorders(Bottom: Thin, Left: Thin, Right: Thin), FormatAt(cut, "A3").Borders);
        Assert.Equal(new CellBorders(Top: Thin, Left: Thin, Right: Thin), FormatAt(cut, "A4").Borders);
        Assert.Equal(new CellBorders(Top: Thin, Left: Thin, Right: Thin), FormatAt(cut, "XFD4").Borders);
        Assert.Equal(CellBorders.None, FormatAt(cut, "A5").Borders);
        // Recorded on the rows, as Excel's file records it: row 3 left, right and bottom, row 4 left, right and top.
        var document = cut.Instance.ToDocument();
        Assert.Empty(document.Cells);
        Assert.Equal([2, 3], document.Rows.Select(r => r.First));
        Assert.Equal(new CellBorders(Bottom: Thin, Left: Thin, Right: Thin), document.Rows[0].Borders);
        Assert.Equal(new CellBorders(Top: Thin, Left: Thin, Right: Thin), document.Rows[1].Borders);

        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal(CellBorders.None, FormatAt(cut, "A3").Borders);
        Assert.Equal(CellBorders.None, FormatAt(cut, "A4").Borders);
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0071 / SH-45: an edge's button sets the chosen style and colour on that edge of the cell
    public async Task An_edge_button_sets_the_chosen_line()
    {
        var cut = RenderSheet();
        await OpenAsync(cut, "B2");
        await ShowTabAsync(cut, "Border");

        await cut.FindAll(".ex-format-cells-line-column input").Single(i => i.GetAttribute("aria-label") == "Double").ChangeAsync(new ChangeEventArgs { Value = "on" });
        await SwatchAsync(cut, "Red");
        await cut.Find(".ex-format-cells-edge[data-edge=Bottom]").ClickAsync(new MouseEventArgs());
        Assert.Equal("true", cut.Find(".ex-format-cells-edge[data-edge=Bottom]").GetAttribute("aria-pressed"));
        await OkAsync(cut);

        Assert.Equal(new BorderLine(BorderLineStyle.Double, Red), FormatAt(cut, "B2").Borders.Bottom);
        Assert.Equal(BorderLine.None, FormatAt(cut, "B2").Borders.Top);
    }

    // ---- A code typed under Custom (the fourteenth Windows run, case 11) ----

    [Theory] // ADR-0071 / SH-45, case 14-11: under ja-JP, dd-mmm-yy typed under Custom is built-in 15 and shows 05-1-26, and d-mmm-yy is a code of its own and shows 5-1-26
    [InlineData("dd-mmm-yy", "05-1-26", true)]
    [InlineData("d-mmm-yy", "5-1-26", false)]
    public async Task A_typed_code_is_a_built_in_only_where_it_spells_it_case_14_11(string typed, string shown, bool builtIn)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("ja-JP"));
        sheet.Enter(CellAddress.Parse("A1"), "=46027");
        var cut = RenderSheet(ps => ps.Add(s => s.Document, sheet.ToDocument()));
        await OpenAsync(cut, "A1");
        await ChooseAsync(cut, ".ex-format-cells-categories", "Custom");

        await cut.Find(".ex-format-cells-code").InputAsync(new ChangeEventArgs { Value = typed });
        await OkAsync(cut);

        cut.WaitForAssertion(() => Assert.False(IsOpen(cut)));
        Assert.Equal(shown, CellText(cut, "A1"));
        Assert.Equal(builtIn, FormatAt(cut, "A1").NumberFormat.Code == "d-mmm-yy");
    }

    // ---- A range's outer edges show as drawn (the fourteenth Windows run, case 13) ----

    private static void ThickBottomOnA1(Sheet sheet) => Format(sheet, "A1", new CellFormatChange { Borders = new BorderChange { Bottom = Thick } });

    // What a cell records of its own, read back from the document: GetCellFormats answers each cell's own sides.
    private static IReadOnlySet<CellFormat> RecordedAt(IRenderedComponent<ExSheet> cut, string range) =>
        Sheet.Open(cut.Instance.ToDocument()).GetCellFormats(CellRange.Parse(range));

    [Fact] // ADR-0071 / SH-45, case 14-13: A2 under A1's thick bottom opens with a thick top, its button pressed, though A2 records nothing
    public async Task A2_under_a1s_thick_bottom_opens_with_a_thick_top_case_14_13()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(ThickBottomOnA1)));

        await OpenAsync(cut, "A2");
        await ShowTabAsync(cut, "Border");

        Assert.Equal("true", cut.Find(".ex-format-cells-edge[data-edge=Top]").GetAttribute("aria-pressed"));
        Assert.Equal("3", cut.Find(".ex-format-cells-preview line[data-edge=Top]").GetAttribute("stroke-width"));
        Assert.Equal("false", cut.Find(".ex-format-cells-edge[data-edge=Bottom]").GetAttribute("aria-pressed"));
        Assert.Equal([CellFormat.Default], RecordedAt(cut, "A2"));
    }

    [Theory] // ADR-0071 / SH-45, case 14-13: over several ranges, each range's top shows the line drawn along it — the same over A2 and C2, and differing over A2 and E2
    [InlineData("C2", "true")]
    [InlineData("E2", "mixed")]
    public async Task Each_ranges_outer_edge_shows_as_drawn_case_14_13(string other, string pressed)
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(sheet =>
        {
            ThickBottomOnA1(sheet);
            Format(sheet, "C1", new CellFormatChange { Borders = new BorderChange { Bottom = Thick } });
        })));

        await GoToAsync(cut, "A2");
        await CtrlClickAsync(cut, other);
        Assert.True(await cut.Instance.OpenFormatCellsAsync());
        cut.WaitForAssertion(() => Assert.True(IsOpen(cut)));
        await ShowTabAsync(cut, "Border");

        Assert.Equal(pressed, cut.Find(".ex-format-cells-edge[data-edge=Top]").GetAttribute("aria-pressed"));
    }

    // A press with Ctrl on a cell, which adds it to the Selection as a range of its own.
    private static async Task CtrlClickAsync(IRenderedComponent<ExSheet> cut, string address)
    {
        var at = CellAddress.Parse(address);
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs
        {
            Button = 0, Buttons = 1, CtrlKey = true,
            OffsetX = HeadingWidth(cut) + at.Column * SheetColumns.DefaultWidthPx + 5,
            OffsetY = at.Row * ExSheet.DefaultRowHeightPx + 5,
        });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0 });
    }

    [Fact] // ADR-0071 / SH-45, case 14-13: OK with the shown top left alone sets nothing: A2 still records no top, and no undo step is added
    public async Task Ok_with_the_shown_top_left_alone_sets_nothing_case_14_13()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(ThickBottomOnA1)));
        await OpenAsync(cut, "A2");
        await ShowTabAsync(cut, "Border");

        await OkAsync(cut);

        cut.WaitForAssertion(() => Assert.False(IsOpen(cut)));
        Assert.False(cut.Instance.CanUndo);
        Assert.Equal([CellFormat.Default], RecordedAt(cut, "A2"));
        Assert.Equal(Thick, FormatAt(cut, "A1").Borders.Bottom);
    }

    [Fact] // ADR-0071 / SH-45, case 14-13 (a reading: no run pressed it): taking the shown top away clears A1's bottom too, so no line is drawn there
    public async Task Taking_the_shown_top_away_clears_a1s_bottom_case_14_13()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(ThickBottomOnA1)));
        await OpenAsync(cut, "A2");
        await ShowTabAsync(cut, "Border");

        await cut.FindAll(".ex-format-cells-line-column input").Single(i => i.GetAttribute("aria-label") == "Thick").ChangeAsync(new ChangeEventArgs { Value = "on" });
        await cut.Find(".ex-format-cells-edge[data-edge=Top]").ClickAsync(new MouseEventArgs());
        Assert.Equal("false", cut.Find(".ex-format-cells-edge[data-edge=Top]").GetAttribute("aria-pressed"));
        await OkAsync(cut);

        cut.WaitForAssertion(() => Assert.False(IsOpen(cut)));
        Assert.Equal(CellBorders.None, FormatAt(cut, "A1").Borders);
        Assert.Equal(CellBorders.None, FormatAt(cut, "A2").Borders);
        Assert.True(cut.Instance.CanUndo);
    }

    // ---- A Chrome that draws it in a frame of its own (ADR-0071, ADR-0010's note of 2026-09-30) ----

    private sealed class OwnFrameChrome : ISheetChrome
    {
        public FormatCellsContext? Context { get; private set; }

        public RenderFragment? FilterPanel(FilterPanelContext context) => null;

        public RenderFragment? ColumnMenu(ColumnMenuContext context) => null;

        public RenderFragment? CellEditor(CellEditorContext context) => null;

        public RenderFragment? LoadingIndicator(LoadingContext context) => null;

        public RenderFragment? FormatCells(FormatCellsContext context)
        {
            Context = context;
            return builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", "own-frame");
                builder.AddContent(2, context.Tab.ToString());
                builder.CloseElement();
            };
        }
    }

    [Fact] // ADR-0071 / ADR-0010's note of 2026-09-30: a Chrome that takes the frame draws Format Cells beside the grid, and OK and Cancel end it
    public async Task A_chrome_with_a_frame_of_its_own_draws_it_beside_the_grid()
    {
        var chrome = new OwnFrameChrome();
        var cut = RenderSheet(ps => ps.Add(s => s.Chrome, chrome).Add(s => s.Document, DocumentOf(cells: ("B2", "12"))));
        await GoToAsync(cut, "B2");

        Assert.True(await cut.Instance.OpenFormatCellsAsync());

        var frame = cut.Find(".own-frame");
        Assert.Null(frame.Closest(".ex-grid"));
        Assert.Empty(cut.FindAll(".ex-popover"));
        Assert.Equal("Number", frame.TextContent);

        // The draft is ExSheet's: the Chrome sets through it, and OK sets what was touched.
        chrome.Context!.Draft.SetFontStyle(FontStyle.Italic);
        Assert.True(await cut.InvokeAsync(chrome.Context.Ok));
        Assert.Empty(cut.FindAll(".own-frame"));
        Assert.True(FormatAt(cut, "B2").Font.Italic);

        // Cancel ends it too, and the core's focus function asks for the keyboard back.
        Assert.True(await cut.Instance.OpenFormatCellsAsync());
        await cut.InvokeAsync(chrome.Context!.Cancel);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".own-frame")));
        var reclaims = ReclaimCount;
        await chrome.Context.ReturnKeyboard();
        Assert.Equal(reclaims + 1, ReclaimCount);
    }

    // ---- Keys typed while Format Cells opens (ticket 93; ADR-0050 item 16 and ADR-0039, notes of 2026-10-01) ----

    [Fact] // ADR-0050 item 16, 2026-10-01: a frame of the Chrome's own — from the Consumer's call, Ctrl+1 or the Context Menu — tells the grid the keyboard is going to it
    public async Task A_frame_of_the_chromes_own_is_handed_the_keyboard_however_it_opens()
    {
        var chrome = new OwnFrameChrome();
        var cut = RenderSheet(ps => ps.Add(s => s.Chrome, chrome));
        await GoToAsync(cut, "B2");

        Assert.True(await cut.Instance.OpenFormatCellsAsync());
        Assert.Equal(["frame"], HandOffs);
        await cut.InvokeAsync(chrome.Context!.Cancel);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".own-frame")));

        await PressAsync(cut, "1", ctrl: true);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".own-frame")));
        Assert.Equal(["frame", "frame"], HandOffs);
        await cut.InvokeAsync(chrome.Context!.Cancel);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".own-frame")));

        await SecondaryClickAsync(cut, "B2");
        await cut.FindAll("[role=menu] button[role=menuitem]").Single(b => b.TextContent == "Format Cells…").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".own-frame")));
        Assert.Equal(["frame", "frame", "frame"], HandOffs);
    }

    [Fact] // ADR-0039, 2026-10-01: under the built-in Chrome the grid hands the keyboard to its own popover, and no frame is told of
    public async Task The_built_in_format_cells_is_handed_the_keyboard_as_the_grids_popover()
    {
        var cut = RenderSheet();

        await OpenAsync(cut, "B2");

        Assert.Equal(["popover"], HandOffs);
    }
}
