using System.Globalization;
using Bunit;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// Excel's formatting keys on ExSheet (ticket 51; ADR-0063, "Keys"; ADR-0050 item 14), as the
/// eleventh Windows run found them in Excel (<c>verification/2026-10-01-windows-excel-11</c>,
/// cases 16 to 21): each applies what Excel applies under the Sheet's culture as one undo step
/// (SH-42), and while an edit is open each changes nothing, says why and is raised (SH-43). The
/// keys reach ExSheet as ExGrid's declared keys; that the browser hands them over before its own
/// meaning is layer 3's.
/// </summary>
public class FormatKeyTests : SheetTestContext
{
    private static SheetDocument DocumentIn(string culture, params (string Address, string Typed)[] cells)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo(culture));
        foreach (var (address, typed) in cells) sheet.Enter(CellAddress.Parse(address), typed);
        return sheet.ToDocument();
    }

    private static CellFormat FormatAt(IRenderedComponent<ExSheet> cut, string address) =>
        cut.Instance.CellFormatAt(CellAddress.Parse(address));

    private static string Notice(IRenderedComponent<ExSheet> cut) => cut.Find(".ex-sheet-notice").TextContent;

    private static bool FontPart(CellFont font, string part) => part switch
    {
        "bold" => font.Bold,
        "italic" => font.Italic,
        "underline" => font.Underline,
        "strikethrough" => font.Strikethrough,
        _ => throw new ArgumentOutOfRangeException(nameof(part), part, "Not a Font emphasis."),
    };

    /// <summary>A formatting key as the capture listener forwards it while an edit is open.</summary>
    private static Task PressInEditAsync(IRenderedComponent<ExSheet> cut, string key, bool shift, string text, bool fromBar = false)
    {
        var grid = Grid(cut);
        return grid.InvokeAsync(() => grid.Instance.OnKeyAsync(
            key, true, shift, false, false, false, fromDescendant: fromBar, editorText: text, editorCaret: text.Length,
            editorSelectionEnd: text.Length));
    }

    // ---- What ExSheet declares ----

    [Fact] // ADR-0063 / ADR-0050 item 14, SH-42: exactly Excel's keys, by the character typed, with the Shift a layout needs; a key Excel does not have is not claimed
    public void Exactly_excels_formatting_keys_are_declared()
    {
        string[] expected =
        [
            "Control+b", "Control+B", "Control+i", "Control+I", "Control+u", "Control+U",
            "Control+2", "Control+Shift+2", "Control+3", "Control+Shift+3", "Control+4", "Control+Shift+4",
            "Control+5", "Control+Shift+5",
            "Control+~", "Control+Shift+~", "Control+!", "Control+Shift+!", "Control+@", "Control+Shift+@",
            "Control+#", "Control+Shift+#", "Control+$", "Control+Shift+$", "Control+%", "Control+Shift+%",
            "Control+^", "Control+Shift+^", "Control+&", "Control+Shift+&", "Control+_", "Control+Shift+_",
        ];

        Assert.Equal(expected.Order(StringComparer.Ordinal), SheetFormatKeys.Declared.Order(StringComparer.Ordinal));
        // Ctrl+1 waits for Format Cells (ticket 52); Ctrl+Shift+U is another key in Excel.
        Assert.DoesNotContain("Control+1", SheetFormatKeys.Declared);
        Assert.DoesNotContain("Control+Shift+U", SheetFormatKeys.Declared);
        Assert.Equal(expected.Length, Grid(RenderSheet()).Instance.DeclaredKeys!.Count);
    }

    [Fact] // SH-42: a key Excel does not have changes nothing and adds no step
    public async Task A_key_excel_does_not_have_changes_nothing()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentIn("en-US", ("A1", "1234.5"))));
        await GoToAsync(cut, "A1");

        await PressAsync(cut, "1", ctrl: true);
        await PressAsync(cut, "U", ctrl: true, shift: true);
        await PressAsync(cut, "6", ctrl: true);

        Assert.Equal(CellFormat.Default, FormatAt(cut, "A1"));
        Assert.False(cut.Instance.CanUndo);
    }

    // ---- The toggles (cases 16 and 17) ----

    [Theory] // ADR-0063, SH-42 (case 16): B and 2 bold, I and 3 italic, U and 4 single underline, 5 strikethrough; each toggles, one undo step a press
    [InlineData("b", "bold")]
    [InlineData("B", "bold")] // CapsLock
    [InlineData("2", "bold")]
    [InlineData("i", "italic")]
    [InlineData("3", "italic")]
    [InlineData("u", "underline")]
    [InlineData("4", "underline")]
    [InlineData("5", "strikethrough")]
    public async Task A_toggle_key_sets_and_clears_its_emphasis(string key, string part)
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentIn("en-US", ("A1", "abc"))));
        await GoToAsync(cut, "A1");

        await PressAsync(cut, key, ctrl: true);
        Assert.True(FontPart(FormatAt(cut, "A1").Font, part));
        Assert.Equal(CellFont.Default with { Bold = part == "bold", Italic = part == "italic", Underline = part == "underline", Strikethrough = part == "strikethrough" },
            FormatAt(cut, "A1").Font);
        Assert.Equal(NumberFormat.General, FormatAt(cut, "A1").NumberFormat);

        await PressAsync(cut, key, ctrl: true);
        Assert.False(FontPart(FormatAt(cut, "A1").Font, part));

        Assert.True(await cut.Instance.UndoAsync());
        Assert.True(FontPart(FormatAt(cut, "A1").Font, part));
        Assert.True(await cut.Instance.UndoAsync());
        Assert.False(FontPart(FormatAt(cut, "A1").Font, part));
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0063, SH-42 (case 17): the direction follows the Focus cell — a bold Focus takes bold off every cell, a plain one puts it on every cell
    public async Task A_toggles_direction_follows_the_focus_cell()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentIn("en-US", ("A1", "abc"), ("A2", "def"))));
        await GoToAsync(cut, "A1");
        await PressAsync(cut, "b", ctrl: true);

        // Focus A1 (bold), A2 plain: both become plain.
        await GoToAsync(cut, "A1");
        await PressAsync(cut, "ArrowDown", shift: true);
        await PressAsync(cut, "b", ctrl: true);
        Assert.False(FormatAt(cut, "A1").Font.Bold);
        Assert.False(FormatAt(cut, "A2").Font.Bold);

        // A1 bold again; Focus A2 (plain): both become bold.
        await GoToAsync(cut, "A1");
        await PressAsync(cut, "b", ctrl: true);
        await GoToAsync(cut, "A2");
        await PressAsync(cut, "ArrowUp", shift: true);
        await PressAsync(cut, "b", ctrl: true);
        Assert.True(FormatAt(cut, "A1").Font.Bold);
        Assert.True(FormatAt(cut, "A2").Font.Bold);
    }

    [Fact] // ADR-0063: a toggle sets only its own emphasis; the other parts stay as each cell has them
    public async Task A_toggle_keeps_every_other_part()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentIn("en-US", ("A1", "1234.5"), ("A2", "x"))));
        await GoToAsync(cut, "A1");
        await PressAsync(cut, "%", ctrl: true, shift: true);
        await PressAsync(cut, "i", ctrl: true);
        await GoToAsync(cut, "A1:A2");

        await PressAsync(cut, "u", ctrl: true);

        Assert.Equal("0%", FormatAt(cut, "A1").NumberFormat.Code);
        Assert.True(FormatAt(cut, "A1").Font.Italic);
        Assert.False(FormatAt(cut, "A2").Font.Italic);
        Assert.True(FormatAt(cut, "A1").Font.Underline);
        Assert.True(FormatAt(cut, "A2").Font.Underline);
    }

    [Fact] // ADR-0050 item 14: with nothing selected a key has nothing to format, and adds no step
    public async Task With_nothing_selected_a_key_formats_nothing()
    {
        var cut = RenderSheet();

        await PressAsync(cut, "b", ctrl: true);

        Assert.False(cut.Instance.CanUndo);
    }

    // ---- The Number Formats (cases 18 to 20) ----

    [Theory] // ADR-0063, SH-42 (cases 18 to 20): the Number Format each Ctrl+Shift key applies under each culture the run observed, as one undo step
    [InlineData("en-GB", "~", true, "General")]
    [InlineData("en-GB", "!", true, "#,##0.00")]
    [InlineData("en-GB", "@", true, "h:mm")]
    [InlineData("en-GB", "#", false, "d-mmm-yy")]   // UK: # needs no Shift (case 19)
    [InlineData("en-GB", "#", true, "d-mmm-yy")]
    [InlineData("en-GB", "$", true, "$#,##0.00_);[Red]($#,##0.00)")]
    [InlineData("en-GB", "%", true, "0%")]
    [InlineData("en-GB", "^", true, "0.00E+00")]
    [InlineData("en-US", "~", true, "General")]
    [InlineData("en-US", "!", true, "#,##0.00")]
    [InlineData("en-US", "@", true, "h:mm AM/PM")]
    [InlineData("en-US", "#", true, "d-mmm-yy")]
    [InlineData("en-US", "$", true, "$#,##0.00_);[Red]($#,##0.00)")]
    [InlineData("en-US", "%", true, "0%")]
    [InlineData("en-US", "^", true, "0.00E+00")]
    [InlineData("ja-JP", "~", true, "General")]
    [InlineData("ja-JP", "!", true, "#,##0.00")]
    [InlineData("ja-JP", "@", true, "h:mm")]
    [InlineData("ja-JP", "#", true, "d-mmm-yy")]
    [InlineData("ja-JP", "$", true, "$#,##0_);[Red]($#,##0)")]
    [InlineData("ja-JP", "%", true, "0%")]
    [InlineData("ja-JP", "^", true, "0.00E+00")]
    public async Task A_number_format_key_applies_excels_format_under_the_culture(string culture, string key, bool shift, string code)
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentIn(culture, ("A1", "=1234.5"))));
        await GoToAsync(cut, "A1");
        // General is applied over something else, so that it is seen to apply.
        if (code == "General") await cut.Instance.SetNumberFormatAsync(NumberFormat.Parse("0.00"));
        var steps = cut.Instance.CanUndo;

        await PressAsync(cut, key, ctrl: true, shift: shift);

        Assert.Equal(code, FormatAt(cut, "A1").NumberFormat.Code);
        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal(code == "General" ? "0.00" : "General", FormatAt(cut, "A1").NumberFormat.Code);
        Assert.Equal(steps, cut.Instance.CanUndo);
    }

    [Theory] // ADR-0063, SH-42 (case 20): Ctrl+Shift+$ shows the Sheet culture's own currency, recorded as Excel's built-in
    [InlineData("en-GB", "£5.00")]
    [InlineData("ja-JP", "¥5")]
    [InlineData("en-US", "$5.00 ")]
    public async Task The_currency_key_shows_the_cultures_currency(string culture, string shown)
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentIn(culture, ("A1", "=5"))));
        await GoToAsync(cut, "A1");

        await PressAsync(cut, "$", ctrl: true, shift: true);

        Assert.Equal(shown, CellText(cut, "A1"));
        Assert.StartsWith("$#,##0", FormatAt(cut, "A1").NumberFormat.Code, StringComparison.Ordinal);
    }

    // ---- Borders (cases 13 and 15) ----

    [Fact] // ADR-0063, SH-42 (case 13): Ctrl+Shift+& outlines the range, thin and Automatic; inside it nothing is set
    public async Task The_outline_key_outlines_the_range()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "B2:D4");
        var thin = new BorderLine(BorderLineStyle.Thin);

        await PressAsync(cut, "&", ctrl: true, shift: true);

        Assert.Equal(new CellBorders(Top: thin, Left: thin), FormatAt(cut, "B2").Borders);
        Assert.Equal(new CellBorders(Top: thin), FormatAt(cut, "C2").Borders);
        Assert.Equal(new CellBorders(Bottom: thin, Right: thin), FormatAt(cut, "D4").Borders);
        Assert.Equal(new CellBorders(Left: thin), FormatAt(cut, "B3").Borders);
        Assert.Equal(CellBorders.None, FormatAt(cut, "C3").Borders);
        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal(CellBorders.None, FormatAt(cut, "B2").Borders);
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0063, SH-42 (case 13): Ctrl+Shift+_ clears every edge of the Selection, outer and inner, as one step
    public async Task The_no_borders_key_clears_every_edge_of_the_selection()
    {
        var cut = RenderSheet();
        var thick = new BorderLine(BorderLineStyle.Thick);
        await GoToAsync(cut, "B2:D4");
        await cut.Instance.SetCellFormatAsync(new CellFormatChange { Borders = BorderChange.Outline(thick) with { InsideHorizontal = thick, InsideVertical = thick } });

        await PressAsync(cut, "_", ctrl: true, shift: true);

        foreach (var address in new[] { "B2", "C2", "D2", "B3", "C3", "D3", "B4", "C4", "D4" })
        {
            Assert.Equal(CellBorders.None, FormatAt(cut, address).Borders);
        }
        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal(new CellBorders(thick, thick, thick, thick), FormatAt(cut, "C3").Borders);
    }

    // ---- While an edit is open (case 21) ----

    [Theory] // ADR-0063 / ADR-0048, SH-43: while an edit is open a formatting key changes nothing, says why, is raised, and leaves the edit as it was
    [InlineData("b", false)]
    [InlineData("u", false)]
    [InlineData("5", false)]
    [InlineData("$", true)]
    [InlineData("&", true)]
    [InlineData("_", true)]
    public async Task While_an_edit_is_open_a_formatting_key_changes_nothing_and_says_why(string key, bool shift)
    {
        var refusals = new List<SheetRefusal>();
        var cut = RenderSheet(ps => ps
            .Add(s => s.Document, DocumentIn("en-US", ("A1", "abc")))
            .Add(s => s.OnFormatKeyRefused, refusals.Add));
        await GoToAsync(cut, "A1");
        await PressAsync(cut, "F2");
        await TypeAsync(cut, "abcd");
        var before = cut.Instance.ToDocument().ToJson();

        await PressInEditAsync(cut, key, shift, "abcd");

        Assert.Equal(before, cut.Instance.ToDocument().ToJson());
        Assert.False(cut.Instance.CanUndo);
        Assert.Equal(SheetWords.FormatKeyWhileEditing, Notice(cut));
        var refusal = Assert.Single(refusals);
        Assert.Equal(SheetRefusalReason.EditIsOpen, refusal.Reason);
        Assert.Equal(SheetWords.FormatKeyWhileEditing, refusal.Message);
        // The edit stands, on its text, and commits as typed.
        Assert.True(cut.Instance.IsEditing);
        Assert.Equal("abcd", EditorText(cut));
        await PressInEditorAsync(cut, "Enter", "abcd", 4);
        Assert.Equal("abcd", CellText(cut, "A1"));
        Assert.Equal(CellFormat.Default, FormatAt(cut, "A1"));
    }

    [Fact] // ADR-0063 / ADR-0051, SH-43: from the Formula Bar too, a formatting key changes nothing and says why
    public async Task In_the_formula_bar_a_formatting_key_changes_nothing()
    {
        var refusals = new List<SheetRefusal>();
        var cut = RenderSheet(ps => ps
            .Add(s => s.Document, DocumentIn("en-US", ("A1", "abc")))
            .Add(s => s.OnFormatKeyRefused, refusals.Add));
        await GoToAsync(cut, "A1");
        await cut.Find(".ex-formula-bar-text").FocusAsync(new FocusEventArgs());
        await TypeInBarAsync(cut, "abcx");

        await PressInEditAsync(cut, "i", false, "abcx", fromBar: true);

        Assert.Single(refusals);
        Assert.Equal(SheetWords.FormatKeyWhileEditing, Notice(cut));
        Assert.False(cut.Instance.CanUndo);
        Assert.True(cut.Instance.IsEditing);
        Assert.False(FormatAt(cut, "A1").Font.Italic);
    }

    [Fact] // ADR-0063 / ADR-0050 section 6, SH-43: a key the grid forwards while ExSheet still hears an edit open is refused the same way, never thrown into the key handler
    public async Task A_key_while_exsheet_still_hears_an_edit_open_is_refused()
    {
        var refusals = new List<SheetRefusal>();
        var cut = RenderSheet(ps => ps
            .Add(s => s.Document, DocumentIn("en-US", ("A1", "abc")))
            .Add(s => s.OnFormatKeyRefused, refusals.Add));
        await GoToAsync(cut, "A1");
        await PressAsync(cut, "F2");
        var grid = Grid(cut);

        // The grid raises the key as though its edit had ended, as it does in the render after a
        // parameter change discarded the edit, before ExSheet has heard the end.
        await grid.InvokeAsync(() => grid.Instance.OnDeclaredKey.InvokeAsync(new global::ExGrid.Keys.GridDeclaredKeyPress("Control+b", false)));

        Assert.Single(refusals);
        Assert.False(FormatAt(cut, "A1").Font.Bold);
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0063, SH-43: once the edit has ended the keys format again, and the notice goes with the user's next action
    public async Task Once_the_edit_ends_the_keys_format_again()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentIn("en-US", ("A1", "abc"))));
        await GoToAsync(cut, "A1");
        await PressAsync(cut, "F2");
        await PressInEditAsync(cut, "b", false, "abc");
        await PressInEditorAsync(cut, "Escape", "abc", 3);

        await PressAsync(cut, "b", ctrl: true);

        Assert.True(FormatAt(cut, "A1").Font.Bold);
        Assert.Equal("", Notice(cut));
    }
}
