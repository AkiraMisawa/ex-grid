using Bunit;
using ExGrid.Chrome;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using SheetComponent = ExSheet.Components.ExSheet;

namespace ExSheet.Components.Tests;

/// <summary>
/// The Sheet Toolbar's keys (ADR-0100; ticket 160; SH-51, SH-52): Ctrl+F1 raises the toggle where the
/// Consumer binds it, and is not claimed otherwise; Alt released alone, or F10, shows a KeyTip per
/// Toolbar Row and takes the keyboard to the toolbar; a row's letter shows its items' letters, which
/// are Excel's; an item's letters press it and give the keyboard back; Escape goes back a level; an
/// unmatched key ends them; Alt with another key is a chord; and two letters that collide at one
/// level are refused by name. The keys are declared only while they mean something.
/// </summary>
public class SheetToolbarKeyTipTests : SheetTestContext
{
    private IRenderedComponent<SheetComponent> RenderToolbarSheet(Action<ComponentParameterCollectionBuilder<SheetComponent>>? parameters = null) =>
        RenderSheet(ps =>
        {
            ps.Add(s => s.ShowToolbar, true);
            parameters?.Invoke(ps);
        });

    private static IReadOnlyCollection<string> Declared(IRenderedComponent<SheetComponent> cut) => Grid(cut).Instance.DeclaredKeys ?? [];

    private static Task AltDownAsync(IRenderedComponent<SheetComponent> cut)
    {
        var grid = Grid(cut);
        return grid.InvokeAsync(() => grid.Instance.OnKeyAsync("Alt", false, false, true, false, false));
    }

    private static Task KeyUpAsync(IRenderedComponent<SheetComponent> cut, string key) =>
        cut.Find(".ex-sheet").KeyUpAsync(new KeyboardEventArgs { Key = key });

    // A key typed on the band, which holds the keyboard while the KeyTips stand: the grid handed it there
    // (HandKeyboardToAsync), and the browser tells the band it has DOM focus before the key reaches it.
    private static async Task TypeOnToolbarAsync(IRenderedComponent<SheetComponent> cut, string key)
    {
        await cut.Find(".ex-sheet-toolbar").FocusAsync(new FocusEventArgs());
        await cut.Find(".ex-sheet-toolbar").KeyDownAsync(new KeyboardEventArgs { Key = key });
    }

    private static IReadOnlyList<string> ShownKeyTips(IRenderedComponent<SheetComponent> cut) =>
        [.. cut.FindAll(".ex-sheet-toolbar .ex-sheet-keytip").Select(k => k.TextContent)];

    private static CellFormat FormatAt(IRenderedComponent<SheetComponent> cut, string address) =>
        cut.Instance.CellFormatAt(CellAddress.Parse(address));

    // ---- Ctrl+F1 (SH-51) ----

    [Fact] // ADR-0100, SH-51: where the Consumer binds the toggle, Ctrl+F1 hides the toolbar and shows it again
    public async Task Ctrl_F1_toggles_a_bound_toolbar()
    {
        var page = RenderPage<SheetWithToolbar>();
        var cut = page.FindComponent<SheetComponent>();
        Assert.Contains("Control+F1", Declared(cut));

        await PressAsync(cut, "F1", ctrl: true);
        Assert.False(cut.Instance.ShowToolbar);
        Assert.Empty(cut.FindAll(".ex-sheet-toolbar"));

        await PressAsync(cut, "F1", ctrl: true);
        Assert.True(cut.Instance.ShowToolbar);
        Assert.NotEmpty(cut.FindAll(".ex-sheet-toolbar"));
    }

    [Fact] // ADR-0100, SH-51: unbound, Ctrl+F1 is not claimed and stays the browser's
    public void Ctrl_F1_is_not_claimed_unbound()
    {
        var cut = RenderToolbarSheet();

        Assert.DoesNotContain("Control+F1", Declared(cut));
    }

    // ---- Which keys are declared (SH-52) ----

    [Fact] // ADR-0100, SH-52: Alt alone and F10 are claimed while the toolbar is shown, and with an edit open they start nothing
    public async Task The_keytip_keys_are_claimed_while_the_toolbar_is_shown()
    {
        Assert.DoesNotContain("Alt+Alt", Declared(RenderSheet()));

        var cut = RenderToolbarSheet();
        Assert.Contains("Alt+Alt", Declared(cut));
        Assert.Contains("F10", Declared(cut));

        await GoToAsync(cut, "B2");
        await PressAsync(cut, "x");
        Assert.True(cut.Instance.IsEditing);
        await PressAsync(cut, "F10");
        await AltDownAsync(cut);
        await KeyUpAsync(cut, "Alt");

        Assert.Empty(ShownKeyTips(cut));
        Assert.True(cut.Instance.IsEditing);
    }

    // ---- KeyTips (SH-52) ----

    [Fact] // ADR-0100, SH-52: F10 shows the rows' letters, H shows Excel's Home letters, and 1 sets bold and gives the keyboard back
    public async Task F10_H_1_sets_bold()
    {
        var cut = RenderToolbarSheet();
        await GoToAsync(cut, "B2");

        await PressAsync(cut, "F10");
        Assert.Equal(["H"], ShownKeyTips(cut));

        await TypeOnToolbarAsync(cut, "h");
        var shown = ShownKeyTips(cut);
        Assert.Contains("1", shown);
        Assert.Contains("FC", shown);
        Assert.Contains("AL", shown);
        Assert.Contains("4", shown);
        Assert.Contains("N", shown);
        Assert.Contains("OE", shown);

        var returned = KeyboardReturns;
        await TypeOnToolbarAsync(cut, "1");

        Assert.True(FormatAt(cut, "B2").Font.Bold);
        Assert.Empty(ShownKeyTips(cut));
        Assert.True(KeyboardReturns > returned);
    }

    [Fact] // ADR-0100, SH-52: Alt released alone starts the KeyTips, and two letters reach an item: A, L aligns left
    public async Task Alt_released_alone_starts_them_and_two_letters_reach_an_item()
    {
        var cut = RenderToolbarSheet();
        await GoToAsync(cut, "C3");

        await AltDownAsync(cut);
        await KeyUpAsync(cut, "Alt");
        Assert.Equal(["H"], ShownKeyTips(cut));

        await TypeOnToolbarAsync(cut, "H");
        await TypeOnToolbarAsync(cut, "A");
        Assert.Equal(["AL", "AC", "AR"], ShownKeyTips(cut));
        await TypeOnToolbarAsync(cut, "L");

        Assert.Equal(HorizontalAlignment.Left, FormatAt(cut, "C3").Alignment);
    }

    [Fact] // ADR-0100, ADR-0050 item 16 (decided with the user, 2026-10-12): starting the KeyTips tells the grid's key gate the keyboard is going to a frame of the Sheet's own, so the letters typed before the toolbar has it are held for it; a release that starts nothing tells it nothing
    public async Task Starting_the_keytips_tells_the_key_gate_the_keyboard_is_going_to_the_toolbar()
    {
        var cut = RenderToolbarSheet();
        await GoToAsync(cut, "C3");

        await AltDownAsync(cut);
        await KeyUpAsync(cut, "Alt");

        Assert.Equal(["frame"], HandOffs);

        // A chord's release starts nothing, and tells nothing.
        await TypeOnToolbarAsync(cut, "Escape");
        await AltDownAsync(cut);
        await KeyUpAsync(cut, "ArrowDown");
        await KeyUpAsync(cut, "Alt");
        Assert.Equal(["frame"], HandOffs);
    }

    [Fact] // ADR-0100, SH-52: Alt held with another key is a chord, and its release shows nothing
    public async Task Alt_with_another_key_is_a_chord()
    {
        var cut = RenderToolbarSheet();

        await AltDownAsync(cut);
        await KeyUpAsync(cut, "ArrowDown");
        await KeyUpAsync(cut, "Alt");

        Assert.Empty(ShownKeyTips(cut));
    }

    [Fact] // ADR-0100, SH-52: Escape goes back one level and ends from the top; an unmatched key ends them doing nothing
    public async Task Escape_goes_back_and_an_unmatched_key_ends_them()
    {
        var cut = RenderToolbarSheet();
        await GoToAsync(cut, "B2");
        await PressAsync(cut, "F10");
        await TypeOnToolbarAsync(cut, "H");

        await TypeOnToolbarAsync(cut, "Escape");
        Assert.Equal(["H"], ShownKeyTips(cut));
        var returned = KeyboardReturns;
        await TypeOnToolbarAsync(cut, "Escape");
        Assert.Empty(ShownKeyTips(cut));
        Assert.True(KeyboardReturns > returned);
        // The browser takes the keyboard back, and asks for what was typed on the way: nothing.
        Assert.Empty(AnswerTakeBacks(cut));

        await PressAsync(cut, "F10");
        await TypeOnToolbarAsync(cut, "H");
        await TypeOnToolbarAsync(cut, "Z");
        Assert.Empty(ShownKeyTips(cut));
        Assert.Equal(CellFormat.Default, FormatAt(cut, "B2"));
    }

    [Fact] // ADR-0100, SH-52: a split control's letters open its list, as Excel's Alt, H, H opens the Fill's palette
    public async Task A_split_controls_letters_open_its_list()
    {
        var cut = RenderToolbarSheet();
        await GoToAsync(cut, "B2");
        await PressAsync(cut, "F10");
        await TypeOnToolbarAsync(cut, "H");

        await TypeOnToolbarAsync(cut, "H");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".ex-popover .ex-sheet-swatch-choice")));
        Assert.Equal(CellFill.None, FormatAt(cut, "B2").Fill);
    }

    [Fact] // ADR-0100's CloseRequest (decided with the user, 2026-10-12) and R2 (b) of the Windows readings of 2026-10-12 (observed: Escape after Alt, H, F, C shows the Home tab's KeyTips): Escape typed on the toolbar before the list a KeyTip opened has the keyboard closes it, as the opening it closes, and goes back to its row's KeyTips, with the band taking the keyboard through the grid
    public async Task Escape_on_the_toolbar_closes_the_list_a_keytip_opened_and_goes_back_to_its_rows_keytips()
    {
        var chrome = new RecordingChrome();
        var cut = RenderToolbarSheet(ps => ps.Add(s => s.Chrome, chrome));
        await GoToAsync(cut, "B2");
        await PressAsync(cut, "F10");
        await TypeOnToolbarAsync(cut, "H");
        await TypeOnToolbarAsync(cut, "F");
        await TypeOnToolbarAsync(cut, "C");
        var opened = chrome.Drawn.Last(c => c.Name == "Font Colour");
        Assert.Equal(1, opened.OpenRequest);
        Assert.Equal(0, opened.CloseRequest);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".ex-popover .ex-sheet-swatch-choice")));
        Assert.Empty(ShownKeyTips(cut));
        var taken = TakeBackCount;
        var handedTo = HandedToCount;

        await TypeOnToolbarAsync(cut, "Escape");

        var closing = chrome.Drawn.Last(c => c.Name == "Font Colour");
        Assert.Equal(1, closing.CloseRequest);
        Assert.Equal(closing.OpenRequest, closing.CloseRequest);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".ex-popover .ex-sheet-swatch-choice")));
        // The Home row's letters again, and the keyboard kept on the band: not given back to the Sheet.
        Assert.Contains("FC", ShownKeyTips(cut));
        Assert.Contains("1", ShownKeyTips(cut));
        Assert.Equal(taken, TakeBackCount);
        Assert.Equal(handedTo + 1, HandedToCount);
        Assert.Equal(["frame", "popover", "frame", "none"], HandOffs);
        // No other item was asked: only a list that may be open from the keys is.
        Assert.All(chrome.Drawn.Where(c => c.Name != "Font Colour"), c => Assert.Equal(0, c.CloseRequest));

        // Escape goes back a level, and from the rows' letters ends them, with the keyboard on the Sheet.
        await TypeOnToolbarAsync(cut, "Escape");
        Assert.Equal(["H"], ShownKeyTips(cut));
        await TypeOnToolbarAsync(cut, "Escape");
        Assert.Empty(ShownKeyTips(cut));
        Assert.Equal(taken + 1, TakeBackCount);
    }

    [Fact] // R2 (b) of the Windows readings of 2026-10-12 (observed: after Alt, H, F, C one Escape shows the Home tab's KeyTips, a second the tabs', a third none), under the built-in Chrome: Escape in the list a KeyTip opened, in the grid's popover, goes back to its row's KeyTips, and the band takes the keyboard through the grid with the keys typed after the Escape
    public async Task Escape_in_the_list_a_keytip_opened_goes_back_to_its_rows_keytips()
    {
        var cut = RenderToolbarSheet();
        await GoToAsync(cut, "B2");
        await PressAsync(cut, "F10");
        await TypeOnToolbarAsync(cut, "H");
        await TypeOnToolbarAsync(cut, "H");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".ex-popover .ex-sheet-swatch-choice")));
        // The list takes the keyboard: the band holds it no longer.
        await cut.Find(".ex-popover-consumer > .ex-popover-body").FocusInAsync(new FocusEventArgs());
        await cut.Find(".ex-sheet-toolbar").FocusOutAsync(new FocusEventArgs());
        var taken = TakeBackCount;
        var handedTo = HandedToCount;
        var handOffs = HandOffs.Count;

        await PressAsync(cut, "Escape", fromDescendant: true);

        Assert.Empty(cut.FindAll(".ex-popover .ex-sheet-swatch-choice"));
        Assert.Contains("FC", ShownKeyTips(cut));
        Assert.Contains("H", ShownKeyTips(cut));
        Assert.Equal(["frame"], HandOffs.Skip(handOffs));
        Assert.Equal(handedTo + 1, HandedToCount);
        Assert.Equal(taken, TakeBackCount);
        Assert.Equal(CellFill.None, FormatAt(cut, "B2").Fill);

        // The keys after it are the KeyTips': 1 sets bold and gives the keyboard back.
        await TypeOnToolbarAsync(cut, "1");
        Assert.True(FormatAt(cut, "B2").Font.Bold);
        Assert.Empty(ShownKeyTips(cut));
    }

    [Fact] // ADR-0100's note of 2026-10-12: a list opened from the band's own keys, not a KeyTip, closes on Escape with the keyboard back on the Sheet, as before; and a choice in a list a KeyTip opened gives it to the Sheet, after which Escape on the band is the band's again
    public async Task Escape_in_a_list_the_band_opened_and_a_choice_give_the_keyboard_to_the_sheet()
    {
        var cut = RenderToolbarSheet();
        await GoToAsync(cut, "B2");
        var band = cut.Find(".ex-sheet-toolbar");
        await band.FocusAsync(new FocusEventArgs());
        foreach (var _ in Enumerable.Range(0, 5)) await cut.Find(".ex-sheet-toolbar").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" });
        await cut.Find(".ex-sheet-toolbar").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".ex-popover .ex-sheet-swatch-choice")));
        await cut.Find(".ex-popover-consumer > .ex-popover-body").FocusInAsync(new FocusEventArgs());
        await cut.Find(".ex-sheet-toolbar").FocusOutAsync(new FocusEventArgs());
        var handOffs = HandOffs.Count;

        await PressAsync(cut, "Escape", fromDescendant: true);

        Assert.Empty(cut.FindAll(".ex-popover"));
        Assert.Empty(ShownKeyTips(cut));
        Assert.Empty(HandOffs.Skip(handOffs));

        // A choice in a list a KeyTip opened: the cell is filled, no KeyTips stand, and the list is
        // forgotten — Escape on the band later gives the keyboard back, as the band's own Escape does.
        await PressAsync(cut, "F10");
        await TypeOnToolbarAsync(cut, "H");
        await TypeOnToolbarAsync(cut, "H");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".ex-popover .ex-sheet-swatch-choice")));
        await cut.FindAll(".ex-popover .ex-sheet-swatch-choice").First().ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".ex-popover")));
        Assert.NotEqual(CellFill.None, FormatAt(cut, "B2").Fill);
        Assert.Empty(ShownKeyTips(cut));
        var taken = TakeBackCount;
        await TypeOnToolbarAsync(cut, "Escape");
        Assert.Empty(ShownKeyTips(cut));
        Assert.Equal(taken + 1, TakeBackCount);
    }

    [Fact] // ADR-0100 (Escape goes back one level) and R2 (a) of the Windows readings of 2026-10-12 (observed: after Alt, H, O three Escapes leave Excel Ready): letters that narrow a level are a level of their own, as Excel's Format menu is behind O
    public async Task Escape_after_narrowing_letters_shows_the_levels_letters_again()
    {
        var cut = RenderToolbarSheet();
        await GoToAsync(cut, "B2");
        await PressAsync(cut, "F10");
        await TypeOnToolbarAsync(cut, "H");
        await TypeOnToolbarAsync(cut, "O");
        Assert.Equal(["OE"], ShownKeyTips(cut));
        var taken = TakeBackCount;

        await TypeOnToolbarAsync(cut, "Escape");
        Assert.Contains("OE", ShownKeyTips(cut));
        Assert.Contains("1", ShownKeyTips(cut));
        await TypeOnToolbarAsync(cut, "Escape");
        Assert.Equal(["H"], ShownKeyTips(cut));
        await TypeOnToolbarAsync(cut, "Escape");

        Assert.Empty(ShownKeyTips(cut));
        Assert.Equal(taken + 1, TakeBackCount);
        Assert.Empty(cut.FindAll(".ex-format-cells"));
    }

    [Fact] // ADR-0100's note of 2026-10-12, SH-52 (R2 (a) of the Windows readings of 2026-10-12, observed: Alt, H, O, E): Format Cells is Excel's O, E — O narrows the KeyTips to it and opens nothing, and E opens it
    public async Task Format_cells_is_o_then_e()
    {
        var cut = RenderToolbarSheet();
        await GoToAsync(cut, "B2");
        await PressAsync(cut, "F10");
        await TypeOnToolbarAsync(cut, "H");

        await TypeOnToolbarAsync(cut, "O");
        // Excel's Alt, H, O opens the Cells group's Format menu, whose KeyTip for Format Cells… is E,
        // observed (the Windows readings of 2026-10-12, R2 (a)): an E typed after O is a KeyTip's,
        // never the dialog's.
        Assert.Equal(["OE"], ShownKeyTips(cut));
        Assert.Empty(cut.FindAll(".ex-format-cells"));
        var returned = KeyboardReturns;

        await TypeOnToolbarAsync(cut, "E");

        Assert.Empty(ShownKeyTips(cut));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".ex-format-cells")));
        // The keyboard is handed back to the grid, which holds the keys typed meanwhile for Format
        // Cells and leaves the keyboard in it once it has it (ADR-0100's note of 2026-10-12).
        Assert.True(KeyboardReturns > returned);
    }

    [Fact] // ADR-0100's note of 2026-10-12, SH-52: a Consumer's O beside Format Cells would be taken for its O, E, and is refused by name
    public void A_consumers_o_beside_format_cells_is_refused_by_name()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => RenderToolbarSheet(ps => ps.Add(s => s.ToolbarContent, (RenderFragment)(builder =>
        {
            builder.OpenComponent<ToolbarRow>(0);
            builder.AddComponentParameter(1, nameof(ToolbarRow.ChildContent), (RenderFragment)(row =>
            {
                row.OpenComponent<FormatCellsItem>(0);
                row.CloseComponent();
                row.OpenComponent<ToolbarButton>(1);
                row.AddComponentParameter(2, nameof(ToolbarButton.Text), "Open");
                row.AddComponentParameter(3, nameof(ToolbarButton.KeyTip), "O");
                row.CloseComponent();
            }));
            builder.CloseComponent();
        }))));

        Assert.Contains("'OE' of FormatCellsItem 'Format Cells'", ex.Message);
        Assert.Contains("'O' of ToolbarButton 'Open'", ex.Message);
    }

    [Fact] // ADR-0100, SH-52: a Consumer's row and item carry the letters declared, and reach the item
    public async Task A_consumers_letters_reach_its_item()
    {
        var page = RenderPage<SheetWithToolbar>();
        page.Render(ps => ps.Add(p => p.TwoRows, true));
        var cut = page.FindComponent<SheetComponent>();

        await PressAsync(cut, "F10");
        Assert.Equal(["H", "Y"], ShownKeyTips(cut));
        await TypeOnToolbarAsync(cut, "Y");
        Assert.Equal(["A"], ShownKeyTips(cut));
        await TypeOnToolbarAsync(cut, "A");

        Assert.Equal(1, page.Instance.Approvals);
    }

    [Fact] // ADR-0100, SH-52: two letters that collide at one level are refused when rendered, naming both
    public void Colliding_letters_are_refused_by_name()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => RenderToolbarSheet(ps => ps.Add(s => s.ToolbarContent, (RenderFragment)(builder =>
        {
            builder.OpenComponent<ToolbarRow>(0);
            builder.AddComponentParameter(1, nameof(ToolbarRow.ChildContent), (RenderFragment)(row =>
            {
                row.OpenComponent<BoldItem>(0);
                row.CloseComponent();
                row.OpenComponent<ToolbarButton>(1);
                row.AddComponentParameter(2, nameof(ToolbarButton.Text), "Approve");
                row.AddComponentParameter(3, nameof(ToolbarButton.KeyTip), "1");
                row.CloseComponent();
            }));
            builder.CloseComponent();
        }))));

        Assert.Contains("BoldItem 'Bold'", ex.Message);
        Assert.Contains("ToolbarButton 'Approve'", ex.Message);
        Assert.Contains("ADR-0100", ex.Message);
    }

    /// <summary>A Chrome that draws the built-in items and records each context it is handed.</summary>
    private sealed class RecordingChrome : ISheetChrome
    {
        public List<ToolbarItemContext> Drawn { get; } = [];

        public RenderFragment? FilterPanel(FilterPanelContext context) => null;

        public RenderFragment? ColumnMenu(ColumnMenuContext context) => null;

        public RenderFragment? CellEditor(CellEditorContext context) => null;

        public RenderFragment? LoadingIndicator(LoadingContext context) => null;

        public RenderFragment? ToolbarItem(ToolbarItemContext context)
        {
            Drawn.Add(context);
            return null;
        }
    }
}
