using Bunit;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using MudColor = global::MudBlazor.Utilities.MudColor;
using MudColorPicker = global::MudBlazor.MudColorPicker;
using MudSelectOfAlignment = global::MudBlazor.MudSelect<ExSheet.Engine.HorizontalAlignment>;
using MudSelectOfUnderline = global::MudBlazor.MudSelect<bool>;
using global::MudBlazor.Extensions;

namespace ExSheet.MudBlazor.Tests;

/// <summary>
/// Format Cells under ExSheet.MudBlazor's Chrome (ticket 53; ADR-0071, ADR-0010's note of
/// 2026-09-30; SH-45): a MudDialog at page level, outside the Sheet's root, drawing what ExSheet
/// offers and setting what the user chooses through ExSheet's draft, so that OK means under this
/// Chrome what it means under the built-in one; and the keyboard handed back through the core's
/// focus function however the dialog closes.
/// </summary>
public class MudFormatCellsTests : MudSheetTestContext
{
    private static readonly CellColour Red = CellColour.FromRgb(0xFF0000);

    // ---- The frame ----

    [Fact] // ADR-0071 / SH-45: under the MudBlazor Chrome Format Cells is a MudDialog beside the grid, not a popover inside it
    public async Task Format_cells_is_a_mud_dialog_outside_the_sheets_root()
    {
        var page = RenderPage(DocumentOf(("B2", "12")));

        await OpenAsync(page, "B2");

        var dialog = Dialog(page);
        Assert.Null(dialog.Closest(".ex-grid"));
        Assert.Empty(page.FindAll(".ex-popover-consumer"));
        Assert.Equal("dialog", dialog.GetAttribute("role"));
        Assert.Contains("Format Cells", page.Find(".mud-dialog-title").TextContent);
    }

    [Fact] // ADR-0071 / ADR-0050 item 14 / SH-45: Ctrl+1 opens the dialog, and the Context Menu's command is the same opening
    public async Task Ctrl_1_opens_the_dialog()
    {
        var page = RenderPage();
        await GoToAsync(page, "B2");

        await PressAsync(page, "1", ctrl: true);

        page.WaitForAssertion(() => Assert.True(IsOpen(page)));
        Assert.False(Sheet(page).CanUndo);
    }

    [Fact] // ADR-0050 item 16 / ADR-0039, 2026-10-01: the dialog is handed the keyboard through the grid, so that keys typed as it opens are its, never the grid's
    public async Task The_dialog_is_handed_the_keyboard_through_the_grid()
    {
        var page = RenderPage();
        await GoToAsync(page, "B2");

        await PressAsync(page, "1", ctrl: true);

        page.WaitForAssertion(() => Assert.True(IsOpen(page)));
        Assert.Equal(["frame"], HandOffs);
    }

    [Fact] // ADR-0071 / SH-45: the dialog is modal and closes on Escape, on its backdrop and by its ×, each a Cancel
    public async Task The_dialog_closes_as_a_cancel_by_mudblazors_own_means()
    {
        var page = RenderPage(DocumentOf(("B2", "12")));
        await OpenAsync(page, "B2");
        var before = Sheet(page).ToDocument().ToJson();
        await ChooseAsync(page, "Percentage");
        var reclaims = ReclaimCount;

        await page.Find(".mud-dialog .mud-button-close").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.False(IsOpen(page)));
        Assert.Equal(before, Sheet(page).ToDocument().ToJson());
        page.WaitForAssertion(() => Assert.True(ReclaimCount > reclaims));
        Assert.Empty(page.FindAll(".mud-ex-sheet-format-cells-leaving"));
    }

    [Fact] // ADR-0071: without a MudDialogProvider Format Cells is refused by name rather than standing open unseen
    public async Task Without_a_dialog_provider_it_is_refused_by_name()
    {
        var page = RenderPage(dialogs: false);
        await GoToAsync(page, "B2");

        Assert.True(await page.InvokeAsync(Sheet(page).OpenFormatCellsAsync));

        // MudBlazor waits five seconds for a provider to draw the dialog before it answers.
        var thrown = await Renderer.UnhandledException.WaitAsync(TimeSpan.FromSeconds(15), Xunit.TestContext.Current.CancellationToken);
        Assert.IsType<InvalidOperationException>(thrown);
        Assert.Contains("MudDialogProvider", thrown.Message);
        // And Format Cells did not stay open: the Sheet renders no frame for it.
        Assert.Empty(page.FindAll(".mud-ex-sheet-format-cells-leaving"));
    }

    // ---- The tabs ----

    [Fact] // ADR-0071 / SH-45, case 22: the tabs are Number, Alignment, Font, Border and Fill, and the first opening is on Number
    public async Task The_tabs_are_excels_order_and_the_first_opening_is_on_number()
    {
        var page = RenderPage();

        await OpenAsync(page, "B2");

        Assert.Equal(["Number", "Alignment", "Font", "Border", "Fill"], Tabs(page));
        Assert.Equal("Number", SelectedTab(page).TextContent.Trim());
    }

    [Fact] // ADR-0071 / SH-45: the tabs switch with the arrow keys, wrapping, and Home and End reach the first and the last (ARIA's tabs pattern)
    public async Task The_tabs_switch_with_the_arrow_keys()
    {
        var page = RenderPage();
        await OpenAsync(page, "B2");

        await SelectedTab(page).KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" });
        page.WaitForAssertion(() => Assert.Equal("Alignment", SelectedTab(page).TextContent.Trim()));
        await SelectedTab(page).KeyDownAsync(new KeyboardEventArgs { Key = "ArrowLeft" });
        await SelectedTab(page).KeyDownAsync(new KeyboardEventArgs { Key = "ArrowLeft" });
        page.WaitForAssertion(() => Assert.Equal("Fill", SelectedTab(page).TextContent.Trim()));
        await SelectedTab(page).KeyDownAsync(new KeyboardEventArgs { Key = "Home" });
        page.WaitForAssertion(() => Assert.Equal("Number", SelectedTab(page).TextContent.Trim()));
        await SelectedTab(page).KeyDownAsync(new KeyboardEventArgs { Key = "End" });
        page.WaitForAssertion(() => Assert.Equal("Fill", SelectedTab(page).TextContent.Trim()));
    }

    [Fact] // ADR-0071 / ADR-0010 / SH-45: a key typed while the keyboard waits on the frame's own element is the tab's — End shows Fill
    public async Task A_key_typed_on_the_way_is_the_tabs()
    {
        var page = RenderPage();
        await OpenAsync(page, "B2");

        await page.Find(".mud-ex-sheet-format-cells-leaving").KeyDownAsync(new KeyboardEventArgs { Key = "End" });

        page.WaitForAssertion(() => Assert.Equal("Fill", SelectedTab(page).TextContent.Trim()));
    }

    [Fact] // ADR-0071 / ADR-0010 / SH-45: Escape typed on the way is a Cancel, and the keyboard is handed back
    public async Task Escape_typed_on_the_way_is_a_cancel()
    {
        var page = RenderPage(DocumentOf(("B2", "12")));
        await OpenAsync(page, "B2");
        await ChooseAsync(page, "Percentage");
        var reclaims = ReclaimCount;

        await page.Find(".mud-ex-sheet-format-cells-leaving").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        page.WaitForAssertion(() => Assert.True(ReclaimCount > reclaims));
        Assert.False(IsOpen(page));
        Assert.Equal("General", FormatAt(page, "B2").NumberFormat.Code);
    }

    [Fact] // ADR-0071 / SH-45 / ADR-0018: two arrows typed together on a circuit — the second pressed on the tab the keyboard has not yet left — move two tabs
    public async Task Two_arrows_typed_together_move_two_tabs()
    {
        var page = RenderPage();
        await OpenAsync(page, "B2");
        await SelectedTab(page).KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" });
        page.WaitForAssertion(() => Assert.Equal("Alignment", SelectedTab(page).TextContent.Trim()));

        // Both on Alignment, where DOM focus still was when the second was typed.
        await AlignmentTab(page).KeyDownAsync(new KeyboardEventArgs { Key = "ArrowLeft" });
        await AlignmentTab(page).KeyDownAsync(new KeyboardEventArgs { Key = "ArrowLeft" });

        page.WaitForAssertion(() => Assert.Equal("Fill", SelectedTab(page).TextContent.Trim()));

        static AngleSharp.Dom.IElement AlignmentTab(Bunit.IRenderedComponent<Bunit.Rendering.ContainerFragment> page) =>
            page.FindAll(".mud-ex-sheet-format-cells [role=tab]").Single(t => t.TextContent.Trim() == "Alignment");
    }

    [Fact] // ADR-0071 / SH-45, case 22: a later opening reopens on the last tab shown
    public async Task A_later_opening_reopens_on_the_last_tab_shown()
    {
        var page = RenderPage();
        await OpenAsync(page, "B2");
        await ShowTabAsync(page, "Border");
        await CancelAsync(page);
        page.WaitForAssertion(() => Assert.False(IsOpen(page)));

        await OpenAsync(page, "C3");

        Assert.Equal("Border", SelectedTab(page).TextContent.Trim());
    }

    // ---- What it offers is ExSheet's ----

    [Fact] // ADR-0071 / SH-45, case 22: the categories in Excel's order; Accounting, Fraction and Special shown disabled, each with its reason
    public async Task The_categories_and_the_disabled_ones_with_their_reasons()
    {
        var page = RenderPage();

        await OpenAsync(page, "B2");

        foreach (var offer in FormatCellsOffer.Categories)
        {
            Assert.Equal(!offer.Enabled, Radio(page, FormatCellsOffer.NameOf(offer.Category)).HasAttribute("disabled"));
            if (offer.DisabledReason is { } reason) Assert.Contains(reason, Dialog(page).TextContent);
        }
    }

    [Fact] // ADR-0071 / SH-45: it opens on the Focus cell's Number Format, its category and its places
    public async Task It_opens_on_the_focus_cells_number_format()
    {
        var page = RenderPage(DocumentOf(sheet => sheet.SetCellFormat([CellRange.Parse("B2")], new CellFormatChange { NumberFormat = NumberFormat.Parse("0.000%") }), ("B2", "0.5")));

        await OpenAsync(page, "B2");

        Assert.True(Radio(page, "Percentage").HasAttribute("checked"));
        Assert.Equal("3", page.Find(".mud-ex-sheet-format-cells-places input").GetAttribute("value"));
    }

    [Fact] // ADR-0071 / SH-45, case 24: a differing Fill shows as No Colour, and a differing emphasis leaves the Font style empty
    public async Task Parts_that_differ_show_as_excel_shows_them()
    {
        var page = RenderPage(DocumentOf(
            sheet => sheet.SetCellFormat([CellRange.Parse("A1")], new CellFormatChange { Bold = true, Fill = CellFill.Solid(Red) }),
            ("A1", "x"), ("A2", "y")));

        await OpenAsync(page, "A1:A2");
        await ShowTabAsync(page, "Font");
        foreach (var style in FormatCellsOffer.FontStyles)
            Assert.False(Radio(page, FormatCellsOffer.NameOf(style)).HasAttribute("checked"));
        await ShowTabAsync(page, "Fill");

        Assert.True(page.Find(".mud-ex-sheet-palette-first input").HasAttribute("checked"));
    }

    // ---- What OK sets is ExSheet's ----

    [Fact] // ADR-0071 / ADR-0048 / SH-45: OK sets the part touched as one undo step, and the keyboard is handed back
    public async Task Ok_sets_the_part_touched_as_one_undo_step()
    {
        var page = RenderPage(DocumentOf(("B2", "0.5")));
        await OpenAsync(page, "B2");
        var before = FormatAt(page, "B2");
        await ChooseAsync(page, "Percentage");

        // OK waits for the keyboard's hand-back, which comes after the change is set.
        await OkAsync(page);

        Assert.False(IsOpen(page));
        Assert.Equal("0.00%", FormatAt(page, "B2").NumberFormat.Code);
        Assert.True(await page.InvokeAsync(Sheet(page).UndoAsync));
        Assert.Equal(before, FormatAt(page, "B2"));
        Assert.False(Sheet(page).CanUndo);
    }

    [Fact] // ADR-0071 / SH-45: OK with nothing touched sets nothing, raises nothing and records no step
    public async Task Ok_with_nothing_touched_sets_nothing()
    {
        var raised = new List<SheetDocument>();
        var page = RenderPage(DocumentOf(("B2", "12")), documentChanged: raised.Add);
        await OpenAsync(page, "B2");
        foreach (var tab in Tabs(page)) await ShowTabAsync(page, tab);

        await OkAsync(page);

        page.WaitForAssertion(() => Assert.False(IsOpen(page)));
        Assert.Empty(raised);
        Assert.False(Sheet(page).CanUndo);
    }

    [Fact] // ADR-0071 / SH-45: Cancel sets nothing, whatever was touched, and the keyboard is handed back
    public async Task Cancel_sets_nothing_and_hands_the_keyboard_back()
    {
        var page = RenderPage(DocumentOf(("B2", "12")));
        await OpenAsync(page, "B2");
        var before = Sheet(page).ToDocument().ToJson();
        await ShowTabAsync(page, "Font");
        await ChooseAsync(page, "Bold");
        var reclaims = ReclaimCount;

        await CancelAsync(page);

        page.WaitForAssertion(() => Assert.False(IsOpen(page)));
        Assert.Equal(before, Sheet(page).ToDocument().ToJson());
        page.WaitForAssertion(() => Assert.True(ReclaimCount > reclaims));
    }

    [Fact] // ADR-0071 / SH-45: a Custom code ExSheet does not read is refused by name, the dialog stays open, and nothing is set
    public async Task A_custom_code_ExSheet_does_not_read_is_refused_by_name()
    {
        var page = RenderPage(DocumentOf(("B2", "0.5")));
        await OpenAsync(page, "B2");
        await ChooseAsync(page, "Custom");

        await page.Find(".mud-ex-sheet-format-cells-code input").InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "[<0]0" });
        await page.Find("form.mud-ex-sheet-format-cells-form").SubmitAsync();

        page.WaitForAssertion(() => Assert.Contains("The number format '[<0]0' is not one ExSheet reads", page.Find(".mud-ex-sheet-format-cells-refusal").TextContent));
        Assert.True(IsOpen(page));
        Assert.Equal("General", FormatAt(page, "B2").NumberFormat.Code);
    }

    [Fact] // ADR-0071 / SH-45: Enter in a field is OK — a Custom code the engine reads is set
    public async Task Enter_in_a_field_is_ok()
    {
        var page = RenderPage(DocumentOf(("B2", "0.5")));
        await OpenAsync(page, "B2");
        await ChooseAsync(page, "Custom");

        await page.Find(".mud-ex-sheet-format-cells-code input").InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "#,##0.0000" });
        var reclaims = ReclaimCount;
        await page.Find("form.mud-ex-sheet-format-cells-form").SubmitAsync();

        page.WaitForAssertion(() => Assert.True(ReclaimCount > reclaims));
        Assert.False(IsOpen(page));
        Assert.Equal("#,##0.0000", FormatAt(page, "B2").NumberFormat.Code);
    }

    [Theory] // ADR-0071 / SH-45, case 14-11: under this Chrome too, dd-mmm-yy typed under ja-JP is built-in 15 and shows 05-1-26, and d-mmm-yy is a code of its own and shows 5-1-26
    [InlineData("dd-mmm-yy", "05-1-26", true)]
    [InlineData("d-mmm-yy", "5-1-26", false)]
    public async Task A_typed_code_is_a_built_in_only_where_it_spells_it_case_14_11(string typed, string shown, bool builtIn)
    {
        var sheet = new global::ExSheet.Engine.Sheet(System.Globalization.CultureInfo.GetCultureInfo("ja-JP"));
        sheet.Enter(CellAddress.Parse("A1"), "=46027");
        var page = RenderPage(sheet.ToDocument());
        await OpenAsync(page, "A1");
        await ChooseAsync(page, "Custom");

        await page.Find(".mud-ex-sheet-format-cells-code input").InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = typed });
        await OkAsync(page);

        page.WaitForAssertion(() => Assert.False(IsOpen(page)));
        Assert.Equal(builtIn, FormatAt(page, "A1").NumberFormat.Code == "d-mmm-yy");
        Assert.Equal(shown, global::ExSheet.Engine.Sheet.Open(Sheet(page).ToDocument()).GetDisplay(CellAddress.Parse("A1")).Text);
    }

    // Opens a dropdown and presses the item the list shows as text.
    private static async Task PickAsync<T>(Bunit.IRenderedComponent<Bunit.Rendering.ContainerFragment> page, string item)
    {
        var select = page.FindComponent<global::MudBlazor.MudSelect<T>>();
        await select.InvokeAsync(select.Instance.OpenMenu);
        await page.FindAll(".mud-popover-open .mud-list-item").Single(i => i.TextContent.Trim() == item).ClickAsync(new MouseEventArgs());
    }

    private static IReadOnlyList<string> ListItems(Bunit.IRenderedComponent<Bunit.Rendering.ContainerFragment> page) =>
        [.. page.FindAll(".mud-popover-open .mud-list-item").Select(i => i.TextContent.Trim())];

    [Fact] // ADR-0071 / SH-45: Horizontal and Underline are dropdowns, as Excel's, listing Excel's choices and showing the Focus cell's
    public async Task Horizontal_and_underline_are_dropdowns_as_excels()
    {
        var page = RenderPage(DocumentOf(("B2", "12")));
        await OpenAsync(page, "B2");
        await ShowTabAsync(page, "Alignment");

        var alignment = page.FindComponent<MudSelectOfAlignment>();
        Assert.Equal(HorizontalAlignment.General, alignment.Instance.GetState(x => x.Value));
        await alignment.InvokeAsync(alignment.Instance.OpenMenu);
        Assert.Equal(FormatCellsOffer.Alignments.Select(FormatCellsOffer.NameOf), ListItems(page));
        await alignment.InvokeAsync(() => alignment.Instance.CloseMenu());

        await ShowTabAsync(page, "Font");
        var underline = page.FindComponent<MudSelectOfUnderline>();
        Assert.False(underline.Instance.GetState(x => x.Value));
        await underline.InvokeAsync(underline.Instance.OpenMenu);
        Assert.Equal(["None", "Single"], ListItems(page));
    }

    [Fact] // ADR-0071 / SH-45: Alignment is horizontal only, and OK sets the one chosen
    public async Task Alignment_sets_the_horizontal_alignment()
    {
        var page = RenderPage(DocumentOf(("B2", "12")));
        await OpenAsync(page, "B2");
        await ShowTabAsync(page, "Alignment");

        await PickAsync<HorizontalAlignment>(page, "Centre");
        await OkAsync(page);

        page.WaitForAssertion(() => Assert.False(IsOpen(page)));
        Assert.Equal(HorizontalAlignment.Center, FormatAt(page, "B2").Alignment);
    }

    [Fact] // ADR-0071 / SH-45, case 25: the Font's style, underline and strikethrough are set as chosen
    public async Task The_font_tab_sets_the_style_underline_and_strikethrough()
    {
        var page = RenderPage(DocumentOf(("B2", "12")));
        await OpenAsync(page, "B2");
        await ShowTabAsync(page, "Font");

        await ChooseAsync(page, "Bold Italic");
        await PickAsync<bool>(page, "Single");
        await page.Find(".mud-ex-sheet-format-cells-strikethrough input").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = true });
        await OkAsync(page);

        page.WaitForAssertion(() => Assert.False(IsOpen(page)));
        var font = FormatAt(page, "B2").Font;
        Assert.True(font.Bold && font.Italic && font.Underline && font.Strikethrough);
    }

    [Fact] // ADR-0071 (2026-10-02) / SH-50: the Font tab's Normal font box sets every part of the Font to its default
    public async Task Normal_font_sets_the_default_font()
    {
        var page = RenderPage(DocumentOf(("B2", "12")));
        await OpenAsync(page, "B2");
        await ShowTabAsync(page, "Font");
        await ChooseAsync(page, "Bold Italic");
        await page.Find(".mud-ex-sheet-format-cells-strikethrough input").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = true });

        await page.Find(".mud-ex-sheet-format-cells-normal-font input").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = true });
        await OkAsync(page);

        page.WaitForAssertion(() => Assert.False(IsOpen(page)));
        Assert.Equal(default, FormatAt(page, "B2").Font);
    }

    [Fact] // ADR-0071 / SH-45, case 23: the Fill tab offers No Colour, the theme's sixty and the ten standard colours, as the Fill tab names them
    public async Task The_fill_palette_is_case_23s()
    {
        var page = RenderPage(DocumentOf(("B2", "12")));
        await OpenAsync(page, "B2");
        await ShowTabAsync(page, "Fill");

        Assert.Contains("No Colour", page.Find(".mud-ex-sheet-palette-first").TextContent);
        var swatches = page.FindAll(".mud-ex-sheet-swatch");
        Assert.Equal(70, swatches.Count);
        Assert.Equal("White, Background 1", swatches[0].GetAttribute("aria-label"));
        Assert.Equal("#7F7F7F", swatches.Single(s => s.GetAttribute("aria-label") == "Black, Text 1, Lighter 50%").GetAttribute("data-rgb"));

        await SwatchAsync(page, "Red");
        await OkAsync(page);

        page.WaitForAssertion(() => Assert.False(IsOpen(page)));
        Assert.Equal(CellFill.Solid(Red), FormatAt(page, "B2").Fill);
    }

    [Fact] // ADR-0071 / SH-45: More Colours is MudBlazor's colour picker, and the colour it picks is set, opaque
    public async Task More_colours_is_mudblazors_colour_picker()
    {
        var page = RenderPage(DocumentOf(("B2", "12")));
        await OpenAsync(page, "B2");
        await ShowTabAsync(page, "Font");

        var picker = page.FindComponent<MudColorPicker>();
        await page.InvokeAsync(() => picker.Instance.ValueChanged.InvokeAsync(new MudColor("#1F4E7980")));
        await OkAsync(page);

        page.WaitForAssertion(() => Assert.False(IsOpen(page)));
        Assert.Equal(CellColour.FromRgb(0x1F4E79), FormatAt(page, "B2").Font.Colour);
    }

    [Fact] // ADR-0071 / SH-45, case 22: the thirteen styles and None in Excel's two columns, Thin chosen on opening; for one cell Inside, Horizontal and Vertical are disabled
    public async Task The_border_tab_offers_excels_styles_and_presets()
    {
        var page = RenderPage();
        await OpenAsync(page, "B2");
        await ShowTabAsync(page, "Border");

        var columns = page.FindAll(".mud-ex-sheet-format-cells-line-column");
        Assert.Equal(2, columns.Count);
        for (var c = 0; c < 2; c++)
        {
            Assert.Equal(
                FormatCellsOffer.LineStyleColumns[c].Select(FormatCellsOffer.NameOf),
                columns[c].QuerySelectorAll("label").Select(l => l.TextContent.Trim()));
        }
        Assert.True(Radio(page, "Thin").HasAttribute("checked"));
        Assert.True(page.FindAll(".mud-ex-sheet-format-cells-preset").Single(b => b.TextContent.Trim() == "Inside").HasAttribute("disabled"));
        Assert.True(page.Find(".mud-ex-sheet-format-cells-edge[data-edge=InsideHorizontal]").HasAttribute("disabled"));
        Assert.True(page.Find(".mud-ex-sheet-format-cells-edge[data-edge=InsideVertical]").HasAttribute("disabled"));
    }

    [Fact] // ADR-0071 / SH-45: an edge's button sets the chosen style and colour on that edge
    public async Task An_edge_sets_the_chosen_line()
    {
        var page = RenderPage(DocumentOf(("B2", "12")));
        await OpenAsync(page, "B2");
        await ShowTabAsync(page, "Border");

        await ChooseAsync(page, "Double");
        await SwatchAsync(page, "Red");
        await page.Find(".mud-ex-sheet-format-cells-edge[data-edge=Bottom]").ClickAsync(new MouseEventArgs());
        Assert.Equal("true", page.Find(".mud-ex-sheet-format-cells-edge[data-edge=Bottom]").GetAttribute("aria-pressed"));
        await OkAsync(page);

        page.WaitForAssertion(() => Assert.False(IsOpen(page)));
        Assert.Equal(new BorderLine(BorderLineStyle.Double, Red), FormatAt(page, "B2").Borders.Bottom);
        Assert.Equal(BorderLine.None, FormatAt(page, "B2").Borders.Top);
    }

    // ---- A range's outer edges show as drawn (the fourteenth Windows run, case 13) ----

    private static readonly BorderLine Thick = new(BorderLineStyle.Thick);

    private static SheetDocument ThickBottomOnA1() =>
        DocumentOf(sheet => sheet.SetCellFormat([CellRange.Parse("A1")], new CellFormatChange { Borders = new BorderChange { Bottom = Thick } }));

    // What a cell records of its own, read back from the document: GetCellFormats answers each cell's own sides.
    private static IReadOnlySet<CellFormat> RecordedAt(IRenderedComponent<Bunit.Rendering.ContainerFragment> page, string range) =>
        global::ExSheet.Engine.Sheet.Open(Sheet(page).ToDocument()).GetCellFormats(CellRange.Parse(range));

    [Fact] // ADR-0071 / SH-45, case 14-13: under this Chrome too, A2 under A1's thick bottom opens with a thick top, its button pressed
    public async Task A2_under_a1s_thick_bottom_opens_with_a_thick_top_case_14_13()
    {
        var page = RenderPage(ThickBottomOnA1());

        await OpenAsync(page, "A2");
        await ShowTabAsync(page, "Border");

        Assert.Equal("true", page.Find(".mud-ex-sheet-format-cells-edge[data-edge=Top]").GetAttribute("aria-pressed"));
        Assert.Equal("3", page.Find(".mud-ex-sheet-format-cells-preview line[data-edge=Top]").GetAttribute("stroke-width"));
        Assert.Equal("false", page.Find(".mud-ex-sheet-format-cells-edge[data-edge=Bottom]").GetAttribute("aria-pressed"));
    }

    [Fact] // ADR-0071 / SH-45, case 14-13: OK with the shown top left alone sets nothing: A2 still records no top, and no undo step is added
    public async Task Ok_with_the_shown_top_left_alone_sets_nothing_case_14_13()
    {
        var page = RenderPage(ThickBottomOnA1());
        await OpenAsync(page, "A2");
        await ShowTabAsync(page, "Border");

        await OkAsync(page);

        page.WaitForAssertion(() => Assert.False(IsOpen(page)));
        Assert.False(Sheet(page).CanUndo);
        Assert.Equal([CellFormat.Default], RecordedAt(page, "A2"));
        Assert.Equal(Thick, FormatAt(page, "A1").Borders.Bottom);
    }

    [Fact] // ADR-0071 / SH-45, case 14-13 (a reading: no run pressed it): taking the shown top away clears A1's bottom too, so no line is drawn there
    public async Task Taking_the_shown_top_away_clears_a1s_bottom_case_14_13()
    {
        var page = RenderPage(ThickBottomOnA1());
        await OpenAsync(page, "A2");
        await ShowTabAsync(page, "Border");

        await ChooseAsync(page, "Thick");
        await page.Find(".mud-ex-sheet-format-cells-edge[data-edge=Top]").ClickAsync(new MouseEventArgs());
        Assert.Equal("false", page.Find(".mud-ex-sheet-format-cells-edge[data-edge=Top]").GetAttribute("aria-pressed"));
        await OkAsync(page);

        page.WaitForAssertion(() => Assert.False(IsOpen(page)));
        Assert.Equal(CellBorders.None, FormatAt(page, "A1").Borders);
        Assert.Equal(CellBorders.None, FormatAt(page, "A2").Borders);
        Assert.True(Sheet(page).CanUndo);
    }

    // ---- The keyboard ----

    [Fact] // ADR-0071 / ADR-0021's note of 2026-09-30: the dialog takes the keyboard from an element of the frame's own, which goes once the dialog has it
    public async Task The_keyboard_leaves_from_the_frames_own_element()
    {
        var page = RenderPage();
        await GoToAsync(page, "B2");
        Assert.True(await page.InvokeAsync(Sheet(page).OpenFormatCellsAsync));
        page.WaitForAssertion(() => Assert.True(IsOpen(page)));

        var leaving = page.Find(".mud-ex-sheet-format-cells-leaving");
        Assert.Null(leaving.Closest(".ex-grid"));
        Assert.Contains(JSInterop.Invocations, i => i.Identifier.EndsWith("focus", StringComparison.Ordinal));

        await page.Find("form.mud-ex-sheet-format-cells-form").FocusInAsync(new Microsoft.AspNetCore.Components.Web.FocusEventArgs());

        Assert.Empty(page.FindAll(".mud-ex-sheet-format-cells-leaving"));
        Assert.True(IsOpen(page));
    }

    [Fact] // ADR-0071 / ADR-0018: opening it again while it stands opens it afresh, in one dialog
    public async Task Opening_again_replaces_the_dialog()
    {
        var page = RenderPage();
        await OpenAsync(page, "B2");
        await ShowTabAsync(page, "Fill");

        Assert.True(await page.InvokeAsync(Sheet(page).OpenFormatCellsAsync));

        page.WaitForAssertion(() => Assert.Single(page.FindAll(".mud-dialog.mud-ex-sheet-format-cells")));
        page.WaitForAssertion(() => Assert.Equal("Fill", SelectedTab(page).TextContent.Trim()));
    }
}
