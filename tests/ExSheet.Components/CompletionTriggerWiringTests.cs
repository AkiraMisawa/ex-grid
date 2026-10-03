using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bunit;
using ExSheet.Components.Tests.Support;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// Completion's triggers, aligned with Excel, as the Sheet shows them (ticket 39, ADR-0058
/// "Completion, aligned with Excel", ADR-0051's note of 2026-09-30, SH-36): an argument's value
/// list opens as the argument begins, takes ↑/↓, Tab and Escape while it is open, and gives the
/// arrows back to Point once closed; a press still points; <c>Table[</c> lists the columns; the
/// list comes back after Backspace; and F3 is left to the browser. As the tenth Windows run saw
/// it (ticket 44): a value typed whole lists that value alone, any other beginning every value;
/// Tab closes any list, which does not come back on what Tab wrote; and → or ← at an open value
/// list points and closes it. As Part B of the ninth Windows run saw it (ticket 70): with the caret
/// before or inside a value nothing is listed, and Home and the Shift+arrows at an open value list
/// point and close it. As the thirteenth run saw it (ticket 74): text that is not a number lists
/// every value, letters included, a number that is no value nothing, and with the caret before
/// white space nothing is listed.
/// </summary>
public class CompletionTriggerWiringTests : SheetTestContext
{
    private const string AtMatchMode = "=XLOOKUP(1,A2:A4,B2:B4,,";

    private static readonly string[] MatchModes =
    [
        "0 - Exact match",
        "-1 - Exact match or next smaller item",
        "1 - Exact match or next larger item",
        "2 - Wildcard character match",
        "3 - Regex match",
    ];

    private static List<string> Candidates(IRenderedComponent<ExSheet> cut) =>
        [.. cut.FindAll(".ex-completion .ex-completion-item").Select(item => item.TextContent)];

    private static string Chosen(IRenderedComponent<ExSheet> cut) =>
        cut.Find(".ex-completion .ex-completion-selected").TextContent;

    private List<string> GateModesTold() =>
        [.. JSInterop.Invocations.Where(i => i.Identifier == "setEditing").Select(i => (string)i.Arguments[0]!)];

    /// <summary>Goes to <paramref name="address"/>, opens Overwrite with the first character and types the rest.</summary>
    private static async Task StartTypingAsync(IRenderedComponent<ExSheet> cut, string address, string typed)
    {
        await GoToAsync(cut, address);
        await PressAsync(cut, typed[..1]);
        if (typed.Length > 1) await TypeAsync(cut, typed);
    }

    // ---- An argument's value list -------------------------------------------------------------

    [Fact] // ADR-0058, SH-36: at match_mode the values are listed as the argument begins, with Excel's texts, the first chosen, and the gate takes the list's keys
    public async Task SH36_match_modes_values_are_listed_as_the_argument_begins()
    {
        var cut = RenderSheet();

        await StartTypingAsync(cut, "D10", AtMatchMode);

        Assert.Equal(MatchModes, Candidates(cut));
        Assert.Equal("0 - Exact match", Chosen(cut));
        Assert.Equal("[match_mode]", cut.Find(".ex-completion .ex-completion-hint strong").TextContent);
        // A Reference can go at the caret too: the list is open over Point, and the gate is told
        // so, and reads it off the list's box (ADR-0058, the tenth Windows run).
        Assert.Equal("completionOverPoint", GateModesTold()[^1]);
        Assert.True(cut.Find(".ex-completion").HasAttribute("data-ex-over-point"));
    }

    [Fact] // ADR-0058 (the tenth Windows run, case 1), SH-36: a value typed whole lists that value alone, chosen
    public async Task SH36_a_value_typed_whole_lists_that_value_alone()
    {
        var cut = RenderSheet();

        await StartTypingAsync(cut, "D10", AtMatchMode + "0");

        Assert.Equal(["0 - Exact match"], Candidates(cut));
        Assert.Equal("0 - Exact match", Chosen(cut));
        // No Reference can go after the 0: ← and → are the editor's.
        Assert.Equal("completion", GateModesTold()[^1]);
        Assert.False(cut.Find(".ex-completion").HasAttribute("data-ex-over-point"));
    }

    [Fact] // ADR-0058 (the tenth Windows run, case 2), SH-36: any other text at a value-list argument lists every value, the first chosen
    public async Task SH36_the_beginning_of_a_value_lists_every_value_the_first_chosen()
    {
        var cut = RenderSheet();

        await StartTypingAsync(cut, "D10", AtMatchMode + "-");

        Assert.Equal(MatchModes, Candidates(cut));
        Assert.Equal("0 - Exact match", Chosen(cut));
    }

    [Fact] // ADR-0058, SH-36: at search_mode its four values are listed, as the ninth Windows run read them
    public async Task SH36_search_modes_values_are_listed()
    {
        var cut = RenderSheet();

        await StartTypingAsync(cut, "D10", AtMatchMode + "0,");

        Assert.Equal(
            ["1 - Search first-to-last", "-1 - Search last-to-first", "2 - Binary search (sorted ascending order)", "-2 - Binary search (sorted descending order)"],
            Candidates(cut));
    }

    [Fact] // ADR-0058 (Part B of the ninth Windows run, x1; Q49), SH-36: with the caret moved back before the value, nothing is listed, the hint still shows, and Tab commits and moves on, as anywhere else in the edit
    public async Task SH36_with_the_caret_before_a_value_nothing_is_listed_and_tab_commits()
    {
        var cut = RenderSheet();
        const string typed = AtMatchMode + "1)";
        await StartTypingAsync(cut, "D10", typed);
        Assert.Empty(Candidates(cut));

        // F2, then ←← in Caret: the editor's own, which the listener reports.
        await PressInEditorAsync(cut, "F2", typed, typed.Length);
        Assert.Equal("caret", GateModesTold()[^1]);
        await ReportCaretAsync(cut, typed, typed.Length - 1);
        await ReportCaretAsync(cut, typed, AtMatchMode.Length);

        Assert.Empty(Candidates(cut));
        Assert.Equal("[match_mode]", cut.Find(".ex-completion .ex-completion-hint strong").TextContent);
        Assert.Equal("caret", GateModesTold()[^1]);

        await PressInEditorAsync(cut, "Tab", typed, AtMatchMode.Length);

        Assert.Empty(cut.FindAll(".ex-viewport .ex-editor"));
        Assert.Equal("E10", cut.Find(".ex-name-box").GetAttribute("value"));
        await GoToAsync(cut, "D10");
        Assert.Equal(typed, cut.Find(".ex-formula-bar-text").GetAttribute("value"));
    }

    [Fact] // ADR-0058 (Part B of the ninth Windows run, Q49), SH-36: with the caret inside a value, nothing is listed either, until Excel is observed
    public async Task SH36_with_the_caret_inside_a_value_nothing_is_listed()
    {
        var cut = RenderSheet();
        const string typed = AtMatchMode + "-1)";
        await StartTypingAsync(cut, "D10", typed);

        await PressInEditorAsync(cut, "F2", typed, typed.Length);
        await ReportCaretAsync(cut, typed, AtMatchMode.Length + 1);

        Assert.Empty(Candidates(cut));
        Assert.Equal("[match_mode]", cut.Find(".ex-completion .ex-completion-hint strong").TextContent);
    }

    [Theory] // ADR-0058 (the thirteenth Windows run, Q54), SH-36: text that is not a number lists every value, the first chosen — letters list no function and no table — and Tab writes the value over the whole typed text, closes the list, and it is not opened again on what Tab wrote
    [InlineData("X")]
    [InlineData("AV")]
    [InlineData("Positions")]
    [InlineData("A1")]
    [InlineData("\"")]
    public async Task SH36_text_that_is_not_a_number_lists_every_value_and_tab_writes_over_it(string typed)
    {
        var cut = RenderSheet();
        await cut.Instance.DeclareLinkedTableAsync("Positions", ["Id", "PV"]);
        await StartTypingAsync(cut, "D10", AtMatchMode + typed);

        Assert.Equal(MatchModes, Candidates(cut));
        Assert.Equal("0 - Exact match", Chosen(cut));
        // No Reference can go after what was typed: the list is not open over Point.
        Assert.Equal("completion", GateModesTold()[^1]);
        Assert.False(cut.Find(".ex-completion").HasAttribute("data-ex-over-point"));

        await PressInEditorAsync(cut, "Tab", AtMatchMode + typed, AtMatchMode.Length + typed.Length);

        Assert.Equal(AtMatchMode + "0", EditorText(cut));
        // The Sheet lists 0 alone for the text Tab wrote, and the grid does not show it.
        Assert.Single((await Grid(cut).Instance.CompleteEditorText!(AtMatchMode + "0", AtMatchMode.Length + 1))!.Candidates);
        Assert.Empty(Candidates(cut));
        Assert.Equal("[match_mode]", cut.Find(".ex-completion .ex-completion-hint strong").TextContent);
        Assert.Equal("overwrite", GateModesTold()[^1]);
    }

    [Fact] // ADR-0058 (decided with the user 2026-10-01), SH-36: at match_mode, Table[ lists the table's columns, as anywhere else, and Tab writes the column without the ], closes the list, and does not open it again on what it wrote
    public async Task SH36_table_bracket_at_match_mode_lists_the_columns()
    {
        var cut = RenderSheet();
        await cut.Instance.DeclareLinkedTableAsync("Positions", ["Id", "PV"]);
        const string typed = AtMatchMode + "Positions[";
        await StartTypingAsync(cut, "D10", typed);

        Assert.Equal(["Id", "PV"], Candidates(cut));
        await PressInEditorAsync(cut, "ArrowDown", typed, typed.Length);
        await PressInEditorAsync(cut, "Tab", typed, typed.Length);

        Assert.Equal(typed + "PV", EditorText(cut));
        Assert.Empty(Candidates(cut));
        Assert.Equal("[match_mode]", cut.Find(".ex-completion .ex-completion-hint strong").TextContent);
        Assert.Equal("overwrite", GateModesTold()[^1]);
    }

    [Fact] // ADR-0058 (Q54), SH-36: at search_mode a letter lists its four values, not AVERAGE
    public async Task SH36_a_letter_at_search_mode_lists_its_values()
    {
        var cut = RenderSheet();

        await StartTypingAsync(cut, "D10", AtMatchMode + "0,A");

        Assert.Equal(
            ["1 - Search first-to-last", "-1 - Search last-to-first", "2 - Binary search (sorted ascending order)", "-2 - Binary search (sorted descending order)"],
            Candidates(cut));
        Assert.Equal("1 - Search first-to-last", Chosen(cut));
    }

    [Fact] // ADR-0058 (Q54), SH-36: after an operator at match_mode (1+) every value is listed, 0 chosen, not 1; the list is open over Point, so Escape closes it and ↓ then points
    public async Task SH36_after_an_operator_at_match_mode_every_value_is_listed_over_point()
    {
        var cut = RenderSheet();
        const string typed = AtMatchMode + "1+";
        await StartTypingAsync(cut, "D10", typed);

        Assert.Equal(MatchModes, Candidates(cut));
        Assert.Equal("0 - Exact match", Chosen(cut));
        Assert.Equal("completionOverPoint", GateModesTold()[^1]);
        Assert.True(cut.Find(".ex-completion").HasAttribute("data-ex-over-point"));

        await PressInEditorAsync(cut, "Escape", typed, typed.Length);
        Assert.Empty(Candidates(cut));
        await PressInEditorAsync(cut, "ArrowDown", typed, typed.Length);

        Assert.Equal(typed + "D11", EditorText(cut));
    }

    [Theory] // ADR-0058 (Q54; the ninth run's Part B), SH-36: a number that is no value lists nothing — 4 at match_mode, 5 at search_mode — and the hint still shows
    [InlineData("4", "[match_mode]")]
    [InlineData("0,5", "[search_mode]")]
    public async Task SH36_a_number_that_is_no_value_lists_nothing(string typed, string argument)
    {
        var cut = RenderSheet();

        await StartTypingAsync(cut, "D10", AtMatchMode + typed);

        Assert.Empty(Candidates(cut));
        Assert.Equal(argument, cut.Find(".ex-completion .ex-completion-hint strong").TextContent);
        Assert.NotEqual("completion", GateModesTold()[^1]);
    }

    [Fact] // ADR-0058 (the thirteenth Windows run, Q55; 11a), SH-36: with the caret before two spaces nothing is listed, and Tab commits the Formula with the spaces kept and moves on, as Tab does with no list open
    public async Task SH36_with_the_caret_before_white_space_nothing_is_listed_and_tab_commits()
    {
        var cut = RenderSheet();
        const string typed = AtMatchMode + "  )";
        await StartTypingAsync(cut, "D10", typed);
        Assert.Empty(Candidates(cut));

        // F2, then ←←← in Caret, to stand straight after ,, before the two spaces.
        await PressInEditorAsync(cut, "F2", typed, typed.Length);
        await ReportCaretAsync(cut, typed, typed.Length - 1);
        await ReportCaretAsync(cut, typed, typed.Length - 2);
        await ReportCaretAsync(cut, typed, AtMatchMode.Length);

        Assert.Empty(Candidates(cut));
        Assert.Equal("[match_mode]", cut.Find(".ex-completion .ex-completion-hint strong").TextContent);
        Assert.Equal("caret", GateModesTold()[^1]);

        await PressInEditorAsync(cut, "Tab", typed, AtMatchMode.Length);

        Assert.Empty(cut.FindAll(".ex-viewport .ex-editor"));
        Assert.Equal("E10", cut.Find(".ex-name-box").GetAttribute("value"));
        await GoToAsync(cut, "D10");
        Assert.Equal(typed, cut.Find(".ex-formula-bar-text").GetAttribute("value"));
    }

    [Fact] // ADR-0058, SH-36: while the list is open ↓ and ↑ choose in it, and write nothing
    public async Task SH36_down_and_up_choose_in_the_list()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "D10", AtMatchMode);

        await PressInEditorAsync(cut, "ArrowDown", AtMatchMode, AtMatchMode.Length);
        await PressInEditorAsync(cut, "ArrowDown", AtMatchMode, AtMatchMode.Length);
        Assert.Equal("1 - Exact match or next larger item", Chosen(cut));
        await PressInEditorAsync(cut, "ArrowUp", AtMatchMode, AtMatchMode.Length);

        Assert.Equal("-1 - Exact match or next smaller item", Chosen(cut));
        Assert.Equal(AtMatchMode, EditorText(cut));
        Assert.Equal(MatchModes, Candidates(cut));
    }

    [Fact] // ADR-0058 (the tenth Windows run, case 3), SH-36: Tab writes the chosen value's number, not its text, and closes the list, which is not opened again on the value written; the hint stays
    public async Task SH36_tab_writes_the_values_number_and_closes_the_list()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "D10", AtMatchMode);
        await PressInEditorAsync(cut, "ArrowDown", AtMatchMode, AtMatchMode.Length);

        await PressInEditorAsync(cut, "Tab", AtMatchMode, AtMatchMode.Length);

        Assert.Equal(AtMatchMode + "-1", EditorText(cut));
        // The Sheet lists -1 alone for the text Tab wrote, and the grid does not show it.
        Assert.Single((await Grid(cut).Instance.CompleteEditorText!(AtMatchMode + "-1", AtMatchMode.Length + 2))!.Candidates);
        Assert.Empty(Candidates(cut));
        Assert.Equal("[match_mode]", cut.Find(".ex-completion .ex-completion-hint strong").TextContent);
        Assert.Equal("overwrite", GateModesTold()[^1]);
    }

    [Fact] // ADR-0058, SH-36: Tab replaces the beginning typed with the whole value
    public async Task SH36_tab_replaces_the_beginning_typed()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "D10", AtMatchMode + "0,-");
        Assert.Equal(4, Candidates(cut).Count);

        await PressInEditorAsync(cut, "ArrowDown", AtMatchMode + "0,-", AtMatchMode.Length + 3);
        await PressInEditorAsync(cut, "ArrowDown", AtMatchMode + "0,-", AtMatchMode.Length + 3);
        await PressInEditorAsync(cut, "ArrowDown", AtMatchMode + "0,-", AtMatchMode.Length + 3);
        await PressInEditorAsync(cut, "Tab", AtMatchMode + "0,-", AtMatchMode.Length + 3);

        Assert.Equal(AtMatchMode + "0,-2", EditorText(cut));
        Assert.Empty(Candidates(cut));
    }

    // ---- → at an open value list ----------------------------------------------------------------

    [Fact] // ADR-0058 (the tenth Windows run, case 6), SH-36: → at an open value list, where a Reference can go, points — E10 from D10, shown selected — and closes the list
    public async Task SH36_right_arrow_at_an_open_value_list_points_and_closes_it()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "D10", AtMatchMode);
        Assert.Equal(MatchModes, Candidates(cut));

        await PressInEditorAsync(cut, "ArrowRight", AtMatchMode, AtMatchMode.Length);

        Assert.Equal(AtMatchMode + "E10", EditorText(cut));
        Assert.Empty(Candidates(cut));
        Assert.Equal("point", GateModesTold()[^1]);
        Assert.EndsWith(",,<span class=\"ex-reference-3 ex-reference-pointed\">E10</span>",
            global::ReferenceText.ColouredText.Of(Grid(cut).Find(".ex-viewport > .ex-reference-text")), StringComparison.Ordinal);
        // Pointing goes on from there: ↓ moves the outline, as after any arrow that pointed.
        await PressInEditorAsync(cut, "ArrowDown", AtMatchMode + "E10", AtMatchMode.Length + 3);
        Assert.Equal(AtMatchMode + "E11", EditorText(cut));
    }

    [Fact] // ADR-0058 (the tenth Windows run), SH-36: ← at an open value list points as →, and closes the list
    public async Task SH36_left_arrow_at_an_open_value_list_points_too()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "D10", AtMatchMode);

        await PressInEditorAsync(cut, "ArrowLeft", AtMatchMode, AtMatchMode.Length);

        Assert.Equal(AtMatchMode + "C10", EditorText(cut));
        Assert.Empty(Candidates(cut));
    }

    [Fact] // ADR-0058 (Part B of the ninth Windows run, x6; Q51), SH-36: Shift+→ at an open value list, where a Reference can go, points at D10:E10 from D10, as Excel's did, and closes the list
    public async Task SH36_shift_right_at_an_open_value_list_points_at_a_range_and_closes_it()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "D10", AtMatchMode);
        Assert.Equal(MatchModes, Candidates(cut));

        await PressInEditorAsync(cut, "ArrowRight", AtMatchMode, AtMatchMode.Length, shift: true);

        Assert.Equal(AtMatchMode + "D10:E10", EditorText(cut));
        Assert.Empty(Candidates(cut));
        Assert.Equal("point", GateModesTold()[^1]);
        Assert.EndsWith(",,<span class=\"ex-reference-3 ex-reference-pointed\">D10:E10</span>",
            global::ReferenceText.ColouredText.Of(Grid(cut).Find(".ex-viewport > .ex-reference-text")), StringComparison.Ordinal);
        Assert.NotEmpty(cut.FindAll(".ex-viewport .ex-editor"));
    }

    [Fact] // ADR-0058 (Part B of the ninth Windows run, x4; Q51, Q53), SH-36: Home at an open value list points at A10, the row's first column, as Excel's did, and closes the list
    public async Task SH36_home_at_an_open_value_list_points_at_the_rows_first_column()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "D10", AtMatchMode);
        Assert.Equal(MatchModes, Candidates(cut));

        await PressInEditorAsync(cut, "Home", AtMatchMode, AtMatchMode.Length);

        Assert.Equal(AtMatchMode + "A10", EditorText(cut));
        Assert.Empty(Candidates(cut));
        Assert.Equal("point", GateModesTold()[^1]);
        Assert.EndsWith(",,<span class=\"ex-reference-3 ex-reference-pointed\">A10</span>",
            global::ReferenceText.ColouredText.Of(Grid(cut).Find(".ex-viewport > .ex-reference-text")), StringComparison.Ordinal);
        Assert.Equal("A10", cut.Find(".ex-name-box").GetAttribute("value"));
    }

    [Fact] // ADR-0058 (x5; Q51, Q53), SH-36: End at an open value list closes it and writes nothing; no commit is asked for, so nothing is refused, and the edit stays open
    public async Task SH36_end_at_an_open_value_list_closes_it_and_writes_nothing()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "D10", AtMatchMode);

        await PressInEditorAsync(cut, "End", AtMatchMode, AtMatchMode.Length);

        Assert.Equal(AtMatchMode, EditorText(cut));
        Assert.Empty(Candidates(cut));
        Assert.Empty(Grid(cut).FindAll(".ex-point"));
        Assert.Empty(cut.FindAll(".ex-message"));
        Assert.Equal("D10", cut.Find(".ex-name-box").GetAttribute("value"));
    }

    [Fact] // ADR-0058 (Q53), SH-36: with no list, at a Reference's place, Home points at the row's first column and End writes nothing — neither asks for the commit the Sheet would refuse
    public async Task SH36_without_a_list_home_points_and_end_writes_nothing()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "D10", "=SUM(");
        Assert.Empty(Candidates(cut));

        await PressInEditorAsync(cut, "Home", "=SUM(", 5);
        Assert.Equal("=SUM(A10", EditorText(cut));
        Assert.Equal("point", GateModesTold()[^1]);
        await PressInEditorAsync(cut, "Escape", "=SUM(A10", 8);

        await StartTypingAsync(cut, "D10", "=SUM(");
        await PressInEditorAsync(cut, "End", "=SUM(", 5);

        Assert.Equal("=SUM(", EditorText(cut));
        Assert.Empty(Grid(cut).FindAll(".ex-point"));
        Assert.Empty(cut.FindAll(".ex-message"));
        Assert.Equal("D10", cut.Find(".ex-name-box").GetAttribute("value"));
    }

    [Theory] // ADR-0058 (Q51), SH-36: in a list of names Home, End and the Shift+arrows stay the editor's — the gate does not claim them — and one claimed all the same writes nothing and commits nothing
    [InlineData("Home", false)]
    [InlineData("End", false)]
    [InlineData("ArrowRight", true)]
    public async Task SH36_in_a_list_of_names_home_end_and_shift_arrows_stay_the_editors(string key, bool shift)
    {
        var cut = RenderSheet();
        await cut.Instance.DeclareLinkedTableAsync("Positions", ["Id", "PV"]);
        await StartTypingAsync(cut, "D10", "=Posit");
        Assert.Equal("completion", GateModesTold()[^1]);

        await PressInEditorAsync(cut, key, "=Posit", 6, shift: shift);

        Assert.Equal("=Posit", EditorText(cut));
        Assert.Empty(Grid(cut).FindAll(".ex-point"));
        Assert.Equal("D10", cut.Find(".ex-name-box").GetAttribute("value"));
    }

    [Fact] // ADR-0058 / ADR-0051 second round, SH-36: in a list of names ← and → are not claimed — they move the caret — and the box says so
    public async Task SH36_in_a_list_of_names_left_and_right_are_the_editors()
    {
        var cut = RenderSheet();
        await cut.Instance.DeclareLinkedTableAsync("Positions", ["Id", "PV"]);

        await StartTypingAsync(cut, "D10", "=Posit");

        Assert.Equal(["Positions"], Candidates(cut));
        Assert.Equal("completion", GateModesTold()[^1]);
        Assert.False(cut.Find(".ex-completion").HasAttribute("data-ex-over-point"));
    }

    [Fact] // ADR-0058, SH-36: Escape closes the list first and leaves the edit open, and ↓ then points
    public async Task SH36_escape_closes_the_list_and_down_then_points()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "D10", AtMatchMode);

        await PressInEditorAsync(cut, "Escape", AtMatchMode, AtMatchMode.Length);
        Assert.Empty(Candidates(cut));
        Assert.Equal(AtMatchMode, EditorText(cut));
        Assert.NotEqual("completion", GateModesTold()[^1]);

        await PressInEditorAsync(cut, "ArrowDown", AtMatchMode, AtMatchMode.Length);

        Assert.Equal(AtMatchMode + "D11", EditorText(cut));
        Assert.Equal("point", GateModesTold()[^1]);
    }

    [Fact] // ADR-0058, SH-36: a press on the grid points while the list is open, and the list goes
    public async Task SH36_a_press_points_while_the_list_is_open()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "D10", AtMatchMode);
        Assert.NotEmpty(Candidates(cut));
        var heading = double.Parse(cut.Find(".ex-row-heading").GetAttribute("style")!.Replace("width:", "").Replace("px", "").Trim(), CultureInfo.InvariantCulture);

        // Column C, row 4: past the Row Headings and two columns, and three rows down the viewport.
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs
        {
            Button = 0, Buttons = 1, OffsetX = heading + 2 * SheetColumns.DefaultWidthPx + 5, OffsetY = 28 * 3 + 5,
        });

        Assert.Equal(AtMatchMode + "C4", EditorText(cut));
        Assert.Empty(Candidates(cut));
    }

    [Fact] // ADR-0058 / ADR-0051, SH-36: nothing is listed after ( or , at any other argument, so ↓ points there
    public async Task SH36_nothing_is_listed_at_another_argument_and_down_points()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "D10", "=XLOOKUP(1,");

        Assert.Empty(Candidates(cut));
        await PressInEditorAsync(cut, "ArrowDown", "=XLOOKUP(1,", 11);

        Assert.Equal("=XLOOKUP(1,D11", EditorText(cut));
    }

    [Fact] // ADR-0058, SH-36: the list opens again at the next value argument once a comma is typed after a value
    public async Task SH36_a_comma_after_the_value_opens_the_next_list()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "D10", AtMatchMode);
        await PressInEditorAsync(cut, "Tab", AtMatchMode, AtMatchMode.Length);

        await TypeAsync(cut, AtMatchMode + "0,");

        Assert.Equal("1 - Search first-to-last", Chosen(cut));
        Assert.Equal(4, Candidates(cut).Count);
    }

    // ---- After Table[, and Backspace ----------------------------------------------------------

    [Fact] // ADR-0058 (the tenth Windows run, case 4), SH-36: after Table[ the table's columns are listed, and only they; Tab writes the column's name without the ], closes the list, and it is not opened again on the column written; SUM's hint stays
    public async Task SH36_table_bracket_lists_the_columns_only()
    {
        var cut = RenderSheet();
        await cut.Instance.DeclareLinkedTableAsync("Positions", ["Id", "PV"]);
        await StartTypingAsync(cut, "D10", "=SUM(Positions[");

        Assert.Equal(["Id", "PV"], Candidates(cut));
        await PressInEditorAsync(cut, "ArrowDown", "=SUM(Positions[", 15);
        await PressInEditorAsync(cut, "Tab", "=SUM(Positions[", 15);

        Assert.Equal("=SUM(Positions[PV", EditorText(cut));
        Assert.Empty(Candidates(cut));
        Assert.StartsWith("SUM(", cut.Find(".ex-completion .ex-completion-hint").TextContent, StringComparison.Ordinal);
        Assert.NotEqual("completion", GateModesTold()[^1]);
    }

    [Fact] // ADR-0058 (the tenth Windows run, case 5), SH-36: Tab on a table's name writes the name, closes the list, and nothing is shown for the name written
    public async Task SH36_tab_on_a_tables_name_closes_the_list()
    {
        var cut = RenderSheet();
        await cut.Instance.DeclareLinkedTableAsync("Positions", ["Id", "PV"]);
        await StartTypingAsync(cut, "D10", "=Posit");
        Assert.Equal(["Positions"], Candidates(cut));

        await PressInEditorAsync(cut, "Tab", "=Posit", 6);

        Assert.Equal("=Positions", EditorText(cut));
        Assert.Empty(cut.FindAll(".ex-completion"));
        Assert.Equal("overwrite", GateModesTold()[^1]);
    }

    [Fact] // ADR-0058, SH-36: Backspace back into a name lists again, after Escape closed the list (the ninth Windows run, case 11)
    public async Task SH36_backspace_back_into_a_name_lists_again()
    {
        var cut = RenderSheet();
        await cut.Instance.DeclareLinkedTableAsync("Positions", ["Id", "PV"]);
        await StartTypingAsync(cut, "D10", "=Posit");
        Assert.Equal(["Positions"], Candidates(cut));
        await PressInEditorAsync(cut, "Escape", "=Posit", 6);
        Assert.Empty(Candidates(cut));

        // Backspace is the editor's own: the browser changes the text and reports it.
        await TypeAsync(cut, "=Posi");

        Assert.Equal(["Positions"], Candidates(cut));
        Assert.Equal("Positions", Chosen(cut));
    }

    // ---- F3 --------------------------------------------------------------------------------------

    [Fact] // ADR-0058 / ADR-0021, SH-36: F3 is not claimed — the key listener names no F3, and the keys the Sheet has the grid claim do not include it
    public async Task SH36_f3_is_not_claimed()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "D10", "=");

        Assert.DoesNotMatch(new Regex(@"\bF3\b"), ShippedKeyListener());
        var claimed = JSInterop.Invocations
            .Where(i => i.Identifier is "attach" or "setClaims")
            .SelectMany(i => i.Arguments.OfType<IEnumerable<string>>().SelectMany(keys => keys))
            .ToList();
        Assert.NotEmpty(claimed);
        Assert.DoesNotContain(claimed, key => key.Contains("F3", StringComparison.Ordinal));

        // Were it forwarded all the same, it would do nothing to the edit.
        await PressInEditorAsync(cut, "F3", "=", 1);
        Assert.Equal("=", EditorText(cut));
        Assert.Empty(Candidates(cut));
    }

    /// <summary>The grid's key listener as it is written: what ships is its minified form (ADR-0123).</summary>
    private static string ShippedKeyListener()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExGrid.slnx"))) directory = directory.Parent;
        Assert.True(directory is not null, $"no ExGrid.slnx above {AppContext.BaseDirectory}");
        return File.ReadAllText(Path.Combine(directory!.FullName, "src", "ExGrid", "Assets", "ex-grid.js"));
    }
}
