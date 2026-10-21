using System.Text;
using ExSheet.Engine;
using ExSheet.Engine.Formulas;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0047 and the spine's first rule: no Formula the engine accepts can end the process. A chain of
/// operators as long as Excel accepts is computed, and every walk of it loops along the chain; a
/// Formula nested deeper than the engine reads is refused on entry, by name, never accepted and
/// crashed on. Each test runs on a thread with Windows' default stack of 1 MB, the smallest a
/// Blazor Server thread has, so a walk that recursed along the chain would end this test process.
/// </summary>
public class FormulaDepthTests
{
    private const int Stack = 1024 * 1024;

    /// <summary>Runs <paramref name="test"/> on a thread with <paramref name="stack"/> bytes of stack, and rethrows what it threw.</summary>
    private static void OnSmallStack(Action test, int stack = Stack)
    {
        Exception? thrown = null;
        var thread = new Thread(() =>
        {
            try
            {
                test();
            }
            catch (Exception e)
            {
                thrown = e;
            }
        }, stack);
        thread.Start();
        thread.Join();
        if (thrown is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(thrown).Throw();
    }

    private static string Chain(string first, string op, string operand, int operators)
    {
        var text = new StringBuilder("=").Append(first);
        for (var i = 0; i < operators; i++) text.Append(op).Append(operand);
        return text.ToString();
    }

    private static string Nested(string open, string inner, string close, int levels) =>
        "=" + string.Concat(Enumerable.Repeat(open, levels)) + inner + string.Concat(Enumerable.Repeat(close, levels));

    [Fact] // ADR-0047: a chain of 4,095 operators, the most Excel's 8,192 characters hold, is computed, not a crash
    public void A_chain_of_4095_additions_is_computed() => OnSmallStack(() =>
    {
        var formula = Chain("1", "+", "1", 4095);
        Assert.Equal(8192, formula.Length);
        var sheet = NewSheet();
        sheet.Enter("A1", formula);

        Assert.Equal(4096, sheet.Number("A1"));
        Assert.Equal(formula, sheet.GetEntryText(CellAddress.Parse("A1")));
    });

    [Fact] // ADR-0047: a chain far longer than Excel accepts is computed too; every operator is left-associative
    public void A_chain_of_every_operator_far_past_excels_length_is_computed() => OnSmallStack(() =>
    {
        var sheet = NewSheet();
        sheet.Enter("A1", Chain("1", "+", "1", 100_000));
        sheet.Enter("A2", Chain("100", "-", "1", 50_000));
        sheet.Enter("A3", Chain("1", "*", "1", 50_000));
        sheet.Enter("A4", Chain("1", "/", "1", 50_000));
        sheet.Enter("A5", Chain("1", "^", "1", 50_000));
        sheet.Enter("A6", Chain("\"a\"", "&", "\"\"", 50_000));
        sheet.Enter("A7", Chain("1", "=", "1", 50_000));       // ((1=1)=1)=…: TRUE=1 is FALSE, then FALSE=1 is FALSE
        sheet.Enter("A8", Chain("1", "+", "1*2", 50_000));     // right operands of their own

        Assert.Equal(100_001, sheet.Number("A1"));
        Assert.Equal(100 - 50_000, sheet.Number("A2"));
        Assert.Equal(1, sheet.Number("A3"));
        Assert.Equal(1, sheet.Number("A4"));
        Assert.Equal(1, sheet.Number("A5"));
        Assert.Equal("a", sheet.Value("A6")!.Value.Text);
        Assert.False(sheet.Value("A7")!.Value.Boolean);
        Assert.Equal(100_001, sheet.Number("A8"));
    });

    [Fact] // ADR-0047: a chain's References are read along it — its Dependents recalculate, and an insertion rewrites them
    public void A_long_chain_of_references_recalculates_and_is_rewritten() => OnSmallStack(() =>
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "2");
        sheet.Enter("B1", Chain("A1", "+", "A1", 5000));
        Assert.Equal(10_002, sheet.Number("B1"));

        sheet.Enter("A1", "3");
        Assert.Equal(15_003, sheet.Number("B1"));

        sheet.InsertRows(0, 1);
        Assert.Equal(Chain("A2", "+", "A2", 5000), sheet.GetEntryText(CellAddress.Parse("B2")));
        Assert.Equal(15_003, sheet.Number("B2"));
    });

    [Fact] // ADR-0047: the walks over a parsed Formula loop along a chain — calls, names, Linked Tables, formats and spill shapes
    public void Every_walk_of_a_long_chain_loops_along_it() => OnSmallStack(() =>
    {
        const int Operators = 50_000;
        var chain = Parser.Parse(Chain("TODAY()", "+", "x", Operators));
        Assert.Equal("TODAY", Assert.Single(chain.Calls).Name);
        Assert.Empty(chain.References);
        Assert.Empty(chain.StructuredReferences);

        var names = Entry.FromFormula(Chain("1", "+", "total", Operators));
        Assert.Equal(["total"], names.UnboundNames);

        var tables = Entry.FromFormula(Chain("1", "+", "Positions[PV]", Operators));
        Assert.Equal(Operators, tables.Parsed!.StructuredReferences.Count());

        // The format a Formula gives on entry (FF-*) is read along the chain: a date plus days is a date.
        var sheet = NewSheet();
        sheet.Enter("A1", "2026-10-21");
        sheet.Enter("B1", Chain("A1", "+", "1", Operators));
        Assert.True(sheet.GetNumberFormat(CellAddress.Parse("B1")).IsDate);

        // A refused Formula's spill shape is read along the chain (KnownShape).
        Assert.Equal((3, 1), KnownShape.Of(Parser.Parse(Chain("A1:A3", "+", "1", Operators)), _ => true));
    });

    [Theory] // ADR-0047: Excel accepts 64 levels of nested functions, and so does ExSheet
    [InlineData("ABS(", "-1", ")", 1)]
    [InlineData("IF(TRUE,", "1", ")", 1)]
    [InlineData("SUM(1,", "0", ")", 64)]
    public void Sixty_four_nested_functions_are_computed(string open, string inner, string close, double expected) => OnSmallStack(() =>
    {
        var sheet = NewSheet();
        sheet.Enter("A1", Nested(open, inner, close, Parser.MostNestedFunctions));
        Assert.Equal(expected, sheet.Number("A1"));
    });

    [Theory] // ADR-0047: a 65th level of nested functions is refused on entry, as Excel refuses it, with the reason
    [InlineData("ABS(", "1", ")")]
    [InlineData("IF(TRUE,", "1", ")")]
    [InlineData("LET(x,", "1", ",x)")]
    public void A_65th_level_of_nested_functions_is_refused_on_entry(string open, string inner, string close) => OnSmallStack(() =>
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "5");
        var refusal = Assert.Throws<FormulaSyntaxException>(() => sheet.Enter("A1", Nested(open, inner, close, Parser.MostNestedFunctions + 1)));

        Assert.Contains("nested more than 64 levels", refusal.Reason);
        Assert.Equal(5, sheet.Number("A1")); // nothing changed
    });

    [Theory] // ADR-0047: nesting the engine can compute is computed — parentheses, signs and operators up to the limit
    [InlineData("(", "1", ")", 1)]
    [InlineData("-", "1", "", 1)]
    [InlineData("-(", "1", ")", 2)]
    [InlineData("1+(", "1", ")", 2)]
    public void Nesting_up_to_the_limit_is_computed(string open, string inner, string close, int levelsPerOpening) => OnSmallStack(() =>
    {
        var openings = (Parser.MostNesting - 1) / levelsPerOpening;
        openings -= openings % 2; // an even number of signs
        var sheet = NewSheet();
        sheet.Enter("A1", Nested(open, inner, close, openings));
        Assert.Equal(open == "1+(" ? openings + 1 : 1, sheet.Number("A1"));
    });

    [Theory] // ADR-0047 and the spine: nesting deeper than the engine computes is refused on entry with its reason, never a crash
    [InlineData("(", "1", ")", 1)]
    [InlineData("-", "1", "", 1)]
    [InlineData("1+(", "1", ")", 1)]
    [InlineData("-(", "1", ")", 1)]
    [InlineData("", "1", "%", 1)]
    [InlineData("(1&(", "1", "))", 1)]
    [InlineData("(", "1", ")", 100)]   // far deeper: the parser stops at the limit
    [InlineData("-", "1", "", 100)]
    public void Nesting_deeper_than_the_engine_computes_is_refused_on_entry(string open, string inner, string close, int times) => OnSmallStack(() =>
    {
        var sheet = NewSheet();
        var refusal = Assert.Throws<FormulaSyntaxException>(() => sheet.Enter("A1", Nested(open, inner, close, Parser.MostNesting * times + 1)));

        Assert.Contains($"nested more than {Parser.MostNesting} levels deep", refusal.Reason);
        Assert.Null(sheet.Value("A1"));
    });

    [Fact] // ADR-0181 and the spine: a LET name read computes its value there, so a long chain of names counts toward the limit
    public void A_chain_of_let_names_deeper_than_the_engine_computes_is_refused() => OnSmallStack(() =>
    {
        // Each LET binds 100 names, each the one before plus 1; nested LETs carry the chain on.
        string Let(int level, string inner)
        {
            var text = new StringBuilder("LET(");
            var previous = level == 0 ? "1" : $"n{level - 1}_99";
            for (var i = 0; i < 100; i++)
            {
                text.Append($"n{level}_{i},{previous}+1,");
                previous = $"n{level}_{i}";
            }
            return text.Append(inner.Replace("@", previous)).Append(')').ToString();
        }

        string Lets(int levels)
        {
            var inner = "@";
            for (var level = levels - 1; level >= 0; level--) inner = Let(level, inner);
            return "=" + inner;
        }

        var sheet = NewSheet();
        var levels = Parser.MostNesting / 100;
        sheet.Enter("A1", Lets(levels));
        Assert.Equal(levels * 100 + 1, sheet.Number("A1"));

        var refusal = Assert.Throws<FormulaSyntaxException>(() => sheet.Enter("A2", Lets(Parser.MostNesting / 100 + 1)));
        Assert.Contains("levels deep", refusal.Reason);
    });

    [Fact] // ADR-0181: the most names one LET binds (LET-029), each read through a call of the one before, are computed
    public void A_let_of_126_names_each_read_through_a_call_is_computed() => OnSmallStack(() =>
    {
        var sheet = NewSheet();
        sheet.Enter("A1", NamesReadThroughCalls());
        Assert.Equal(126, sheet.Number("A1"));
    });

    [Fact] // ADR-0047 and the spine: on a thread whose stack runs short all the same, the Formula is refused, not the process ended
    public void A_formula_the_stack_cannot_hold_is_refused_not_crashed_on() => OnSmallStack(() =>
    {
        // The runtime keeps the last 128 KB of a 64-bit thread's stack in reserve, so on 160 KB the
        // stack runs short within a few calls: on entry, or while the Formula is computed. Either way
        // it is said, never crashed on.
        var sheet = NewSheet();
        try
        {
            sheet.Enter("A1", NamesReadThroughCalls());
        }
        catch (FormulaSyntaxException refusal)
        {
            Assert.Contains("levels deep", refusal.Reason);
            return;
        }
        Assert.True(sheet.Value("A1")!.Value.IsRefused, sheet.Value("A1")!.Value.ToString());
    }, stack: 160 * 1024);

    /// <summary>A <c>LET</c> of 126 names, each <c>ABS</c> of the one before plus 1: 126.</summary>
    private static string NamesReadThroughCalls()
    {
        var text = new StringBuilder("=LET(x_0,1");
        for (var i = 1; i < 126; i++) text.Append($",x_{i},ABS(x_{i - 1})+1");
        return text.Append(",x_125)").ToString();
    }
}
