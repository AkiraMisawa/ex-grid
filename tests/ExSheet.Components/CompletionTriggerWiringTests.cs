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
/// list comes back after Backspace; and F3 is left to the browser.
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
        // A Reference can go at the caret too, and the open list is what the gate is told.
        Assert.Equal("completion", GateModesTold()[^1]);
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

    [Fact] // ADR-0058, SH-36: Tab writes the chosen value's number, not its text, and the list, having nothing left to offer, closes
    public async Task SH36_tab_writes_the_values_number()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "D10", AtMatchMode);
        await PressInEditorAsync(cut, "ArrowDown", AtMatchMode, AtMatchMode.Length);

        await PressInEditorAsync(cut, "Tab", AtMatchMode, AtMatchMode.Length);

        Assert.Equal(AtMatchMode + "-1", EditorText(cut));
        Assert.Empty(Candidates(cut));
        Assert.Equal("[match_mode]", cut.Find(".ex-completion .ex-completion-hint strong").TextContent);
    }

    [Fact] // ADR-0058, SH-36: Tab replaces the prefix typed with the whole value
    public async Task SH36_tab_replaces_the_prefix_typed()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "D10", AtMatchMode + "0,-");
        Assert.Equal(["-1 - Search last-to-first", "-2 - Binary search (sorted descending order)"], Candidates(cut));

        await PressInEditorAsync(cut, "ArrowDown", AtMatchMode + "0,-", AtMatchMode.Length + 3);
        await PressInEditorAsync(cut, "Tab", AtMatchMode + "0,-", AtMatchMode.Length + 3);

        Assert.Equal(AtMatchMode + "0,-2", EditorText(cut));
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

    [Fact] // ADR-0058, SH-36: after Table[ the table's columns are listed, and only they; Tab writes the column's name
    public async Task SH36_table_bracket_lists_the_columns_only()
    {
        var cut = RenderSheet();
        await cut.Instance.DeclareLinkedTableAsync("Positions", ["Id", "PV"]);
        await StartTypingAsync(cut, "D10", "=SUM(Positions[");

        Assert.Equal(["Id", "PV"], Candidates(cut));
        await PressInEditorAsync(cut, "ArrowDown", "=SUM(Positions[", 15);
        await PressInEditorAsync(cut, "Tab", "=SUM(Positions[", 15);

        Assert.Equal("=SUM(Positions[PV", EditorText(cut));
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

    /// <summary>The grid's key listener as the package ships it.</summary>
    private static string ShippedKeyListener()
    {
        var manifest = Path.Combine(AppContext.BaseDirectory, "ExGrid.staticwebassets.runtime.json");
        Assert.True(File.Exists(manifest), $"ExGrid ships no static assets here ({manifest})");
        using var document = JsonDocument.Parse(File.ReadAllText(manifest));
        var script = document.RootElement.GetProperty("ContentRoots").EnumerateArray()
            .Select(root => Path.Combine(root.GetString()!, "ex-grid.js"))
            .Single(File.Exists);
        return File.ReadAllText(script);
    }
}
