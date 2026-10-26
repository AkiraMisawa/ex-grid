using System.Text;
using ExSheet.Engine;
using ExSheet.Engine.Formulas;
using ExSheet.Engine.Tests.Cases;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0047 and the spine's first rule: no Formula the engine accepts can end the process. What a Formula that long or
/// that deep computes, and how one too deep is refused, is in the shared cases (<c>tests/cases/sheet/formula-depth.json</c>).
/// What stays here is .NET's own: those cases run again on a thread with Windows' default stack of 1 MB, the smallest a
/// Blazor Server thread has, so a walk that recursed along a chain would end this test process; a stack that runs short
/// all the same; and the parser's walks, read directly.
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

    public static TheoryData<string> DepthCases()
    {
        var rows = new TheoryData<string>();
        foreach (var (file, name) in SheetCases.Of("sheet"))
        {
            if (file == "formula-depth.json")
                rows.Add(name);
        }
        return rows;
    }

    [Theory, MemberData(nameof(DepthCases))] // ADR-0047: what a long chain and a deep nesting compute, or how they are refused, holds on a stack of 1 MB
    public void A_depth_case_holds_on_a_small_stack(string name) => OnSmallStack(() =>
    {
        var (document, entry) = SheetCases.Find("formula-depth.json", name);
        var differences = SheetCases.Run(document, entry);
        Assert.True(differences.Count == 0, $"sheet/formula-depth.json, \"{name}\":\n  {string.Join("\n  ", differences)}");
    });

    [Fact] // ADR-0047: the walks over a parsed Formula loop along a chain — calls, names, Linked Tables and spill shapes
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

        // A refused Formula's spill shape is read along the chain (KnownShape).
        Assert.Equal((3, 1), KnownShape.Of(Parser.Parse(Chain("A1:A3", "+", "1", Operators)), _ => true));
    });

    [Fact] // ADR-0047 and the spine: a Formula computed where the stack runs short all the same is refused (#VALUE!), not the process ended
    public void A_formula_computed_where_the_stack_runs_short_is_refused_not_crashed_on()
    {
        // Read on an ordinary thread; computed again on a 160 KB thread, at the edge of the stack the
        // runtime keeps in reserve, where the evaluator's first call finds it short.
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("B1", NamesReadThroughCalls().Replace("LET(x_0,1", "LET(x_0,A1", StringComparison.Ordinal));
        Assert.Equal(126, sheet.Number("B1"));

        OnSmallStack(() => AtStackEdge(() => sheet.Enter("A1", "2")), stack: 160 * 1024);
        Assert.True(sheet.Value("B1")!.Value.IsRefused, sheet.Value("B1")!.Value.ToString());

        // With the stack to compute it, it is computed again.
        sheet.Enter("A1", "3");
        Assert.Equal(128, sheet.Number("B1"));
    }

    [Fact] // ADR-0047 and the spine: a Formula read where the stack runs short is refused on entry, saying so, however shallow
    public void A_formula_read_where_the_stack_runs_short_is_refused_with_its_own_reason() => OnSmallStack(() => AtStackEdge(() =>
    {
        var refusal = Assert.Throws<FormulaSyntaxException>(() => Entry.FromFormula("=(1)"));
        Assert.Contains("stack", refusal.Reason);
        Assert.DoesNotContain("levels deep", refusal.Reason);
        Assert.True(refusal.IsBeyondLimit);
    }), stack: 160 * 1024);

    /// <summary>Runs <paramref name="test"/> once the runtime says the thread's stack is short (<see cref="System.Runtime.CompilerServices.RuntimeHelpers.TryEnsureSufficientExecutionStack"/>), inside the reserve it keeps.</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void AtStackEdge(Action test)
    {
        if (!System.Runtime.CompilerServices.RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            test();
            return;
        }
        Span<byte> pad = stackalloc byte[512];
        pad[0] = 1;
        AtStackEdge(test);
        Assert.Equal(1, pad[0]);
    }

    [Theory] // ADR-0047, third run: text after a sign without '=' that is a Formula past the limits (refused, formula-depth.json) spells a signed Formula, so text that spells one keeps its apostrophe
    [InlineData("-", "ABS(", "1", ")", 65)]
    [InlineData("-", "(", "1", ")", 300)]
    [InlineData("+", "-", "1", "", 300)]
    public void A_signed_formula_past_the_limits_spells_one(string sign, string open, string inner, string close, int levels) => OnSmallStack(() =>
    {
        var typed = sign + string.Concat(Enumerable.Repeat(open, levels)) + inner + string.Concat(Enumerable.Repeat(close, levels));
        Assert.True(Entry.IsSignedFormula(typed));
    });

    [Fact] // ADR-0047: a chain's References and calls are read in the text's order, as they were before it was walked in a loop
    public void A_chains_references_and_calls_keep_the_texts_order()
    {
        var references = Parser.Parse("=A1+B1*C1-D1^E1&F1").References.Select(r => new CellAddress(r.Row1, r.Column1).ToString());
        Assert.Equal(["A1", "B1", "C1", "D1", "E1", "F1"], references);

        var calls = Parser.Parse("=ABS(SUM(1))+MAX(2)*MIN(3)-ROUND(INT(4),0)").Calls.Select(c => c.Name);
        Assert.Equal(["ABS", "SUM", "MAX", "MIN", "ROUND", "INT"], calls);
    }

    /// <summary>A <c>LET</c> of 126 names, each <c>ABS</c> of the one before plus 1: 126.</summary>
    private static string NamesReadThroughCalls()
    {
        var text = new StringBuilder("=LET(x_0,1");
        for (var i = 1; i < 126; i++) text.Append($",x_{i},ABS(x_{i - 1})+1");
        return text.Append(",x_125)").ToString();
    }
}
