using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// Completion's triggers, aligned with Excel (ticket 39, ADR-0058 "Completion, aligned with Excel",
/// SH-36): an argument's value list before anything is typed, the columns after <c>Table[</c>, the
/// list again after Backspace, and nothing where a Reference is pointed instead.
/// </summary>
public class CompletionTriggerTests
{
    private static readonly string[] MatchModes =
    [
        "0 - Exact match",
        "-1 - Exact match or next smaller item",
        "1 - Exact match or next larger item",
        "2 - Wildcard character match",
        "3 - Regex match",
    ];

    private static readonly string[] SearchModes =
    [
        "1 - Search first-to-last",
        "-1 - Search last-to-first",
        "2 - Binary search (sorted ascending order)",
        "-2 - Binary search (sorted descending order)",
    ];

    /// <summary>Text with a <c>|</c> marking the caret.</summary>
    private static (string Text, int Caret) AtCaret(string marked) => (marked.Replace("|", "", StringComparison.Ordinal), marked.IndexOf('|', StringComparison.Ordinal));

    private static FormulaCompletion? Complete(string marked)
    {
        var (text, caret) = AtCaret(marked);
        return FormulaEntry.Complete(text, caret, []);
    }

    private static Sheet WithPositions(params string[] columns)
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Positions", columns.Length == 0 ? ["Id", "PV"] : columns);
        return sheet;
    }

    private static FormulaCompletion? Complete(Sheet sheet, string marked)
    {
        var (text, caret) = AtCaret(marked);
        return sheet.Complete(text, caret);
    }

    // ---- The declarations ---------------------------------------------------------------------

    [Fact] // ADR-0058, SH-36: XLOOKUP declares match_mode's and search_mode's values with Excel's texts, observed by the Windows runs of 2026-09-27 and 2026-09-30
    public void XLOOKUP_declares_its_two_value_lists_with_excels_texts()
    {
        var xlookup = DeclaredFunction.Find("XLOOKUP")!;

        Assert.Equal(MatchModes, xlookup.ValuesOf(4).Select(v => v.Text));
        Assert.Equal(["0", "-1", "1", "2", "3"], xlookup.ValuesOf(4).Select(v => v.Value));
        Assert.Equal(SearchModes, xlookup.ValuesOf(5).Select(v => v.Text));
        Assert.Equal(["1", "-1", "2", "-2"], xlookup.ValuesOf(5).Select(v => v.Value));
    }

    [Fact] // ADR-0058, SH-36: every other argument takes any value, and no other function declares a list
    public void Other_arguments_declare_no_values()
    {
        var xlookup = DeclaredFunction.Find("XLOOKUP")!;
        Assert.All([0, 1, 2, 3, 6, -1], index => Assert.Empty(xlookup.ValuesOf(index)));
        Assert.All(DeclaredFunction.All.Where(f => f.Name != "XLOOKUP"),
            f => Assert.All(Enumerable.Range(0, 8), index => Assert.Empty(f.ValuesOf(index))));
    }

    [Fact] // ADR-0058 / ADR-0047, SH-36: each value the lists write is a mode the engine evaluates, not the #VALUE! of a mode it does not know
    public void Every_listed_value_is_a_mode_xlookup_takes()
    {
        // 1, 2 ascending in A, and 2, 1 descending in C, each beside what it returns.
        var sheet = NewSheet();
        foreach (var (address, typed) in new[] { ("A1", "1"), ("A2", "2"), ("B1", "10"), ("B2", "20"), ("C1", "2"), ("C2", "1"), ("D1", "20"), ("D2", "10") })
            sheet.Enter(CellAddress.Parse(address), typed);
        var xlookup = DeclaredFunction.Find("XLOOKUP")!;
        var answer = CellAddress.Parse("F1");

        foreach (var match in xlookup.ValuesOf(4))
        {
            sheet.Enter(answer, $"=XLOOKUP(2,A1:A2,B1:B2,,{match.Value})");
            Assert.Equal(Value.FromNumber(20), sheet.GetValue(answer));
        }
        foreach (var search in xlookup.ValuesOf(5))
        {
            // A binary search answers only over data sorted as it says (ADR-0047).
            var (keys, values) = search.Value == "-2" ? ("C1:C2", "D1:D2") : ("A1:A2", "B1:B2");
            sheet.Enter(answer, $"=XLOOKUP(2,{keys},{values},,0,{search.Value})");
            Assert.Equal(Value.FromNumber(20), sheet.GetValue(answer));
        }
    }

    // ---- An argument's value list -------------------------------------------------------------

    [Theory] // ADR-0058, SH-36: at match_mode the values are listed before anything is typed, with Excel's texts, in Excel's order
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,|")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,|)")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,|,1)")]
    [InlineData("=XLOOKUP(1, A2:A4, B2:B4, , |")]
    [InlineData("=xlookup(1,A2:A4,B2:B4,\"none\",|")]
    [InlineData("=IF(A1,XLOOKUP(1,A2:A4,B2:B4,,|")]
    [InlineData("=XLOOKUP(SUM(1,2),A2:A4,B2:B4,,|")]
    public void Match_mode_lists_its_values_before_anything_is_typed(string marked)
    {
        var (_, caret) = AtCaret(marked);

        var completion = Complete(marked)!;

        Assert.Equal(MatchModes, completion.Candidates.Select(c => c.Name));
        Assert.All(completion.Candidates, c => Assert.Equal(CompletionKind.ArgumentValue, c.Kind));
        Assert.Equal(caret, completion.Start);
        Assert.Equal(0, completion.Length);
    }

    [Fact] // ADR-0058, SH-36: at search_mode its four values are listed, "order)" and all (the ninth Windows run, case 12)
    public void Search_mode_lists_its_values()
    {
        Assert.Equal(SearchModes, Complete("=XLOOKUP(1,A2:A4,B2:B4,,0,|")!.Candidates.Select(c => c.Name));
    }

    [Fact] // ADR-0058, SH-36: accepting a value writes its number, not its text
    public void A_value_writes_its_number()
    {
        var completion = Complete("=XLOOKUP(1,A2:A4,B2:B4,,|")!;

        Assert.Equal(["0", "-1", "1", "2", "3"], completion.Candidates.Select(c => c.InsertText));
        Assert.All(completion.Candidates, c => Assert.Null(c.Description));
    }

    [Theory] // ADR-0058 (the tenth Windows run), SH-36: the beginning of a value lists every value, the first to be chosen — Excel does not narrow a value list by prefix — and accepting one replaces what was typed
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,-|", 24, 1, 4)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,0,-|", 26, 1, 5)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,, -|)", 25, 1, 4)]
    public void ADR0058_the_beginning_of_a_value_lists_every_value(string marked, int start, int length, int argument)
    {
        var completion = Complete(marked)!;

        Assert.Equal(argument == 4 ? MatchModes : SearchModes, completion.Candidates.Select(c => c.Name));
        Assert.Equal(start, completion.Start);
        Assert.Equal(length, completion.Length);
    }

    [Theory] // ADR-0058 (Part B of the ninth Windows run, x1; Q49), SH-36: the value list opens only while nothing of the argument stands after the caret — with the caret before a value, or inside one, nothing is listed
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,|1)")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,|-1)")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,-|1)")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,-|1")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,| 1)")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,|1,0)")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,0,|-2)")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,0,-|2)")]
    public void ADR0058_Q49_with_the_caret_before_or_inside_a_value_nothing_is_listed(string marked)
    {
        var (text, caret) = AtCaret(marked);

        Assert.Null(Complete(marked));
        // The argument's hint still shows (x1: the ScreenTip with [match_mode] bold).
        Assert.NotNull(FormulaEntry.HintAt(text, caret));
    }

    [Theory] // ADR-0058 (Q49), SH-36: what follows the argument is not of it — a value list still opens with the caret before the comma or the parenthesis that ends it
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,|,1)", 5)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,|)", 5)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,-|)", 5)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,0|,1)", 1)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,0,|)", 4)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,  |)", 5)]
    public void ADR0058_Q49_the_list_opens_before_what_ends_the_argument(string marked, int listed)
    {
        Assert.Equal(listed, Complete(marked)!.Candidates.Count);
    }

    [Theory] // ADR-0058 (the tenth Windows run, case 1), SH-36: a value typed whole, with the caret after it, lists that value alone — 0 lists 0 - Exact match — and accepting it writes it again over itself
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,0|", "0 - Exact match", 24, 1)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,-1|", "-1 - Exact match or next smaller item", 24, 2)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,3|)", "3 - Regex match", 24, 1)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,, 1|)", "1 - Exact match or next larger item", 25, 1)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,0,1|", "1 - Search first-to-last", 26, 1)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,0,-2|", "-2 - Binary search (sorted descending order)", 26, 2)]
    public void ADR0058_a_value_typed_whole_lists_that_value_alone(string marked, string listed, int start, int length)
    {
        var completion = Complete(marked)!;

        var candidate = Assert.Single(completion.Candidates);
        Assert.Equal(listed, candidate.Name);
        Assert.Equal(CompletionKind.ArgumentValue, candidate.Kind);
        Assert.Equal(start, completion.Start);
        Assert.Equal(length, completion.Length);
        Assert.Equal(AtCaret(marked).Text.Substring(start, length), candidate.InsertText);
    }

    [Theory] // ADR-0058 (the thirteenth Windows run, Q54), SH-36: at a value-list argument, text that is not a number lists every value, the first selected — letters list the values, not the functions or tables they begin — and accepting one writes it over the whole of what was typed
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,A1|", 24, 2, 4)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,1+|", 24, 2, 4)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,X|", 24, 1, 4)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,AV|", 24, 2, 4)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,Positions|", 24, 9, 4)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,\"|", 24, 1, 4)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,0,A|", 26, 1, 5)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,x|)", 24, 1, 4)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,- |", 24, 2, 4)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,\"a,b|", 24, 4, 4)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,SUM(1)|", 24, 6, 4)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,--1|", 24, 3, 4)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,- 1|", 24, 3, 4)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,50%|", 24, 3, 4)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,1e|", 24, 2, 4)]
    public void ADR0058_Q54_text_that_is_not_a_number_lists_every_value(string marked, int start, int length, int argument)
    {
        // Positions is declared, so that a table could be listed for its name were names listed here.
        var completion = Complete(WithPositions(), marked)!;

        Assert.Equal(argument == 4 ? MatchModes : SearchModes, completion.Candidates.Select(c => c.Name));
        Assert.All(completion.Candidates, c => Assert.Equal(CompletionKind.ArgumentValue, c.Kind));
        Assert.Equal(start, completion.Start);
        Assert.Equal(length, completion.Length);
    }

    [Theory] // ADR-0058 (the thirteenth Windows run, Q54; the ninth, Part B), SH-36: a number that is no value of the argument lists nothing — 4 at match_mode, 5 at search_mode — and the argument's hint still shows
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,4|")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,10|")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,-2|")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,2.5|)")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,0,5|")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,0,3|")]
    public void ADR0058_Q54_a_number_that_is_no_value_lists_nothing(string marked)
    {
        var (text, caret) = AtCaret(marked);

        Assert.Null(Complete(marked));
        Assert.NotNull(FormulaEntry.HintAt(text, caret));
    }

    [Theory] // ADR-0058 (Q54), SH-36: a number is read as the grammar reads one, with its sign, and lists the value it is — 1.0 is the value 1 — so Tab on a number writes that same number or nothing (decided with the user 2026-10-01, not asked of Excel)
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,1.0|", "1 - Exact match or next larger item", 24, 3)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,+1|", "1 - Exact match or next larger item", 24, 2)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,-0|", "0 - Exact match", 24, 2)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,1 |", "1 - Exact match or next larger item", 24, 2)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,0,-2E0|", "-2 - Binary search (sorted descending order)", 26, 4)]
    public void ADR0058_Q54_a_number_lists_the_value_it_is(string marked, string listed, int start, int length)
    {
        var completion = Complete(marked)!;

        Assert.Equal(listed, Assert.Single(completion.Candidates).Name);
        Assert.Equal(start, completion.Start);
        Assert.Equal(length, completion.Length);
    }

    [Theory] // ADR-0058 (the thirteenth Windows run, Q55; 11a), SH-36: white space after the caret is something of the argument — with the caret before it, nothing is listed, and the hint still shows
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,|  )", "[match_mode]")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,|  ", "[match_mode]")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,| ,1)", "[match_mode]")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,, | )", "[match_mode]")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,0| )", "[match_mode]")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,-| )", "[match_mode]")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,X| )", "[match_mode]")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,0,| )", "[search_mode]")]
    public void ADR0058_Q55_white_space_after_the_caret_is_something_of_the_argument(string marked, string argument)
    {
        var (text, caret) = AtCaret(marked);

        Assert.Null(Complete(marked));
        Assert.Equal(argument, FormulaEntry.HintAt(text, caret)!.CurrentArgument);
    }

    [Theory] // ADR-0058 (Q49, Q54), SH-36: what stands inside the argument after the caret is of it, text in quotes and its commas too, and a letter there is no name being typed
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,\"a|,b\")")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,\"a|,b")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,AV|ERAGE)")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,|X)")]
    public void ADR0058_what_stands_after_the_caret_inside_the_argument_lists_nothing(string marked)
    {
        Assert.Null(Complete(WithPositions(), marked));
    }

    [Theory] // ADR-0058, SH-36: outside a value-list argument nothing is listed for what is not a name — past the call, before what the argument holds, inside a call of its own, or in text that is not a Formula
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,(|")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,SUM(|")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,|0+1)")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,0)|")]
    [InlineData("XLOOKUP(1,A2:A4,B2:B4,,|")]
    public void Nothing_is_listed_outside_the_argument(string marked)
    {
        Assert.Null(Complete(marked));
    }

    [Theory] // ADR-0058 / ADR-0051, SH-36: nothing is listed after =, an operator, ( or , at any other argument, so the arrows still point there
    [InlineData("=|")]
    [InlineData("=1+|")]
    [InlineData("=SUM(|")]
    [InlineData("=SUM(1,|")]
    [InlineData("=XLOOKUP(|")]
    [InlineData("=XLOOKUP(1,|")]
    [InlineData("=XLOOKUP(1,A2:A4,|")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,|")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,0,1,|")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,(|")]
    [InlineData("=IF(XLOOKUP(1,A2:A4,B2:B4),|")]
    [InlineData("=IF(A1,|")]
    [InlineData("=ROUND(A1,|")]
    public void Nothing_is_listed_where_a_reference_is_pointed(string marked)
    {
        var (text, caret) = AtCaret(marked);

        Assert.Null(WithPositions().Complete(text, caret));
        Assert.NotNull(FormulaEntry.PointAt(text, caret));
    }

    [Theory] // ADR-0058 (Q54), SH-36: outside a value-list argument a letter lists the functions and Linked Tables it begins, as before — at another argument, after an operator, inside a call of its own, and inside a grouping parenthesis, which holds an expression of its own (decided with the user 2026-10-01, not asked of Excel)
    [InlineData("=A|", "AVERAGE")]
    [InlineData("=1+A|", "AVERAGE")]
    [InlineData("=SUM(A|", "AVERAGE")]
    [InlineData("=XLOOKUP(1,A|", "AVERAGE")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,P|", "Positions")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,0,1,P|", "Positions")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,SUM(A|", "AVERAGE")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,(A|", "AVERAGE")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,ROUND(1,X|", "XLOOKUP")]
    public void ADR0058_Q54_outside_a_value_list_argument_a_letter_lists_names(string marked, string listed)
    {
        var completion = Complete(WithPositions(), marked)!;

        Assert.Contains(listed, completion.Candidates.Select(c => c.Name));
        Assert.DoesNotContain(completion.Candidates, c => c.Kind == CompletionKind.ArgumentValue);
    }

    [Theory] // ADR-0058 (decided with the user 2026-10-01, not asked of Excel), SH-36: at a value-list argument Table[ lists the table's columns, as anywhere else — [ opens a structured reference, a context of its own as a grouping parenthesis is — and nothing else inside its brackets, not the values
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,Positions[|", new[] { "Id", "PV" }, 34, 0)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,Positions[P|", new[] { "PV" }, 34, 1)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,Positions[P|V])", new[] { "PV" }, 34, 2)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,1+Positions[|", new[] { "Id", "PV" }, 36, 0)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,0,Positions[|", new[] { "Id", "PV" }, 36, 0)]
    public void ADR0058_table_bracket_at_a_value_list_argument_lists_the_columns(string marked, string[] listed, int start, int length)
    {
        var completion = Complete(WithPositions(), marked)!;

        Assert.Equal(listed, completion.Candidates.Select(c => c.Name));
        Assert.All(completion.Candidates, c => Assert.Equal(CompletionKind.LinkedTableColumn, c.Kind));
        Assert.Equal(start, completion.Start);
        Assert.Equal(length, completion.Length);
    }

    [Theory] // ADR-0058 (decided with the user 2026-10-01), SH-36: inside the brackets at a value-list argument, what lists no column lists nothing — not the values
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,Positions[Q|")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,Trades[|")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,Positions[#|")]
    public void ADR0058_inside_the_brackets_at_a_value_list_argument_no_value_is_listed(string marked)
    {
        Assert.Null(Complete(WithPositions(), marked));
    }

    [Theory] // ADR-0058 (Q54, and the decisions of 2026-10-01), SH-36: once a structured reference's bracket or a grouping parenthesis closes, the caret is back at the argument, and what it holds is text that is not a number: every value is listed
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,Positions[Id]|", 24, 13)]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,(1)|", 24, 3)]
    public void ADR0058_past_a_closed_bracket_or_parenthesis_the_values_are_listed(string marked, int start, int length)
    {
        var completion = Complete(WithPositions(), marked)!;

        Assert.Equal(MatchModes, completion.Candidates.Select(c => c.Name));
        Assert.Equal(start, completion.Start);
        Assert.Equal(length, completion.Length);
    }

    [Fact] // ADR-0051 / ADR-0058: the hint sets off match_mode while its values are listed
    public void The_hint_names_match_mode_while_its_values_are_listed()
    {
        var hint = FormulaEntry.HintAt("=XLOOKUP(1,A2:A4,B2:B4,,", 24)!;

        Assert.Equal(4, hint.ArgumentIndex);
        Assert.Equal("[match_mode]", hint.CurrentArgument);
    }

    // ---- After Table[ --------------------------------------------------------------------------

    [Fact] // ADR-0058, SH-36: after Table[ the table's columns are listed, in the table's order, and nothing else (not @, #All, #Data, #Headers, #Totals)
    public void After_table_bracket_the_columns_are_listed_and_nothing_else()
    {
        var sheet = WithPositions("PV", "Id", "Book");

        var completion = Complete(sheet, "=SUM(Positions[|")!;

        Assert.Equal(["PV", "Id", "Book"], completion.Candidates.Select(c => c.Name));
        Assert.All(completion.Candidates, c => Assert.Equal(CompletionKind.LinkedTableColumn, c.Kind));
        Assert.Equal(["PV", "Id", "Book"], completion.Candidates.Select(c => c.InsertText));
        Assert.Equal(15, completion.Start);
        Assert.Equal(0, completion.Length);
    }

    [Theory] // ADR-0058, SH-36: the column typed so far narrows the list, without regard to case, and is what accepting replaces
    [InlineData("=SUM(Positions[p|", 15, 1)]
    [InlineData("=SUM(positions[P|", 15, 1)]
    [InlineData("=XLOOKUP(1,Positions[Id],Positions[P|", 35, 1)]
    [InlineData("=SUM(Positions[P|V])", 15, 2)]
    [InlineData("=SUM(Positions[|PV])", 15, 2)]
    [InlineData("=SUM(Positions[P|, 3)", 15, 1)]
    public void The_column_typed_narrows_the_list(string marked, int start, int length)
    {
        var completion = Complete(WithPositions(), marked)!;

        var column = Assert.Single(completion.Candidates, c => c.Name == "PV");
        Assert.Equal(marked.Contains("[|", StringComparison.Ordinal) ? 2 : 1, completion.Candidates.Count);
        Assert.Equal("PV", column.InsertText);
        Assert.Equal(start, completion.Start);
        Assert.Equal(length, completion.Length);
    }

    [Theory] // ADR-0058 / ADR-0047, SH-36: nothing inside brackets the grammar refuses, after the bracket closes, or for a table that is not declared
    [InlineData("=SUM(Positions[#|")]
    [InlineData("=SUM(Positions[@|")]
    [InlineData("=SUM(Positions[[|")]
    [InlineData("=SUM(Positions[[P|V]])")]
    [InlineData("=SUM(Positions[PV]|")]
    [InlineData("=SUM(Positions[PV])|")]
    [InlineData("=SUM(Positions[Q|")]
    [InlineData("=SUM(Trades[|")]
    [InlineData("=SUM(\"Positions[|")]
    [InlineData("=SUM(Positions|[PV])")]
    [InlineData("=SUM(A1 Positions[|")]
    public void Nothing_is_listed_in_brackets_it_cannot_complete(string marked)
    {
        Assert.Null(Complete(WithPositions(), marked));
    }

    [Fact] // ADR-0058, SH-36: without the tables' columns, as FormulaEntry.Complete answers with names alone, nothing is listed after Table[
    public void Without_columns_nothing_is_listed_after_table_bracket()
    {
        Assert.Null(FormulaEntry.Complete("=SUM(Positions[", 15, ["Positions"]));
    }

    [Fact] // ADR-0058 / ADR-0047, SH-36: a column whose name holds a character the grammar reads specially is written escaped, and reads back as that column
    public void A_column_with_special_characters_is_written_escaped()
    {
        var sheet = WithPositions("Id", "Rate #1", "O'Brien", "[Old]");

        var written = Complete(sheet, "=SUM(Positions[|")!.Candidates.Select(c => c.InsertText).ToArray();

        Assert.Equal(["Id", "Rate '#1", "O''Brien", "'[Old']"], written);
        foreach (var (insert, column) in written.Zip(new[] { "Id", "Rate #1", "O'Brien", "[Old]" }))
        {
            var reference = Assert.Single(FormulaEntry.References($"=SUM(Positions[{insert}])", "Sheet1"));
            Assert.Equal(new LinkedTableColumn("Positions", column), reference.LinkedColumn);
        }
    }

    [Fact] // ADR-0058, SH-36: an escape typed before the caret is read as the character it escapes
    public void An_escape_typed_is_read()
    {
        var sheet = WithPositions("Id", "O'Brien");

        Assert.Equal(["O'Brien"], Complete(sheet, "=SUM(Positions[O''|")!.Candidates.Select(c => c.Name));
        Assert.Null(Complete(sheet, "=SUM(Positions[O'|"));
    }

    // ---- Backspace ------------------------------------------------------------------------------

    [Fact] // ADR-0058, SH-36: Backspace back into a name lists again, as Excel does (the ninth Windows run, case 11)
    public void Backspace_back_into_a_name_lists_again()
    {
        var sheet = WithPositions();

        Assert.Equal(["Positions"], Complete(sheet, "=Posit|")!.Candidates.Select(c => c.Name));
        Assert.Equal(["Positions"], Complete(sheet, "=Posi|")!.Candidates.Select(c => c.Name));
        Assert.Equal(["PV"], Complete(sheet, "=SUM(Positions[P|")!.Candidates.Select(c => c.Name));
    }
}
