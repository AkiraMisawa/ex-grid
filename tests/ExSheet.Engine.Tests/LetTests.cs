using System.Globalization;
using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// <c>LET</c> (ADR-0181, docs/specs/exsheet-functions ticket 14, SH-67): names bound inside one
/// Formula, sequentially and lexically, as Excel's keyboard accepts them. The October 3
/// observations, and the seventeenth Windows run's answers by keys (LET-101 to LET-112), are in
/// <c>ExcelCases/let.json</c>; what they cannot state is here — the rule each observed name stands
/// for, dependencies through bindings, Linked Tables, and a Formula's References rewritten around
/// its names.
/// </summary>
public class LetTests
{
    private static string Name(int length) => new('x', length);

    private static string Pairs(int count) =>
        "=LET(" + string.Concat(Enumerable.Range(0, count).Select(i => $"name_{i},{i},")) + "1)";

    [Theory] // ADR-0181, SH-67: a malformed LET, or a name Excel's keyboard refuses, is refused on entry as an unreadable Formula is
    [InlineData("=LET(x,1,x,2,x)")]          // a name bound twice
    [InlineData("=LET(x,1,X,2,x)")]          // without regard to case
    [InlineData("=LET(x,1)")]                // no calculation
    [InlineData("=LET(x,1,y,2,x+y,3)")]      // an even number of arguments
    [InlineData("=LET(x,,y,x)")]             // an even number of arguments, after a value left empty
    [InlineData("=LET(x,1,)")]               // a calculation left empty
    [InlineData("=LET(x,,)")]                // a calculation left empty after a value left empty
    [InlineData("=LET(,1,1)")]               // a name left empty
    [InlineData("=LET(A1,2,A1)")]            // a Reference, which the keyboard refuses (LET-KEYS-004)
    [InlineData("=LET($A$1,2,1)")]
    [InlineData("=LET(XFD1048576,2,1)")]
    [InlineData("=LET(R2,2,R2+1)")]          // an A1 Reference, as the keyboard refuses it (LET-104)
    [InlineData("=LET(C3,2,C3+1)")]          // likewise (LET-105)
    [InlineData("=LET(RC1,2,1)")]            // column RC, row 1
    [InlineData("=LET(LOG10,2,1)")]          // a function's name that is also an A1 Reference
    [InlineData("=LET(TRUE,2,1)")]           // as the keyboard refuses it (LET-102)
    [InlineData("=LET(false,2,1)")]
    [InlineData("=LET(a.b,2,a.b)")]          // a dot (LET-024)
    [InlineData("=LET(1,2,1)")]              // a number (LET-027)
    [InlineData("=LET(\"x\",2,1)")]
    [InlineData("=LET(\\x,2,1)")]
    [InlineData("=LET(x y,2,1)")]
    [InlineData("=LET((x),2,1)")]
    [InlineData("=LET(Positions[PV],2,1)")]
    public void ADR0181_a_let_excel_does_not_accept_is_refused_on_entry(string formula)
    {
        var sheet = NewSheet();

        Assert.Throws<FormulaSyntaxException>(() => sheet.Enter("A1", formula));
        Assert.Null(sheet.GetEntry(CellAddress.Parse("A1")));
    }

    private static FormulaSyntaxException Refusal(string formula) =>
        Assert.Throws<FormulaSyntaxException>(() => NewSheet().Enter("A1", formula));

    [Theory] // ADR-0181 (run 17), SH-67: a name that reads as an A1 Reference, or a boolean, is refused where it stands, in its own words, as the keyboard refuses it (LET-104, LET-105, LET-102)
    [InlineData("=LET(R2,2,R2+1)", "'R2' reads as a Reference, and cannot be a LET name.")]
    [InlineData("=LET(c3,2,c3+1)", "'c3' reads as a Reference, and cannot be a LET name.")]
    [InlineData("=LET(true,2,1)", "TRUE cannot be a LET name.")]
    public void ADR0181_a_reference_or_a_boolean_is_refused_where_it_stands(string formula, string reason)
    {
        var refused = Refusal(formula);

        Assert.Equal(5, refused.Position);
        Assert.Equal(reason, refused.Reason);
    }

    [Fact] // ADR-0181 (run 17), SH-67: every one of Excel's function names that is a name in shape, and no Reference or boolean, is a LET name, declared by the engine or not, as the keyboard took SUM, IRR and N (LET-101, LET-111, LET-112)
    public void ADR0181_every_excel_function_name_that_is_a_name_is_a_let_name()
    {
        var names = FunctionCatalogueTests.ExcelFunctions()
            .Where(name => !name.Contains('.') && name is not ("TRUE" or "FALSE") && !CellAddress.TryParse(name, out _))
            .ToList();
        Assert.Contains("SUM", names);
        Assert.Contains("IRR", names);
        Assert.Contains("N", names);
        Assert.Contains("LET", names);
        var sheet = NewSheet();

        Assert.All(names, name => Assert.Equal(2, sheet.Evaluate($"=LET({name},1,{name}+1)").Number));
    }

    [Theory] // ADR-0181 (run 17), SH-67: the name of an Excel function the engine does not declare is bound exactly as a declared one's is (LET-111 beside LET-101)
    [InlineData("IRR", "SUM")]
    [InlineData("NPER", "RAND")]
    [InlineData("xirr", "rows")]
    [InlineData("N", "PI")]
    public void ADR0181_an_undeclared_excel_function_is_bound_as_a_declared_one_is(string undeclared, string declared)
    {
        Assert.Null(DeclaredFunction.Find(undeclared));
        Assert.NotNull(DeclaredFunction.Find(declared));
        var sheet = NewSheet();

        foreach (var (formula, value) in new[] { ("=LET({0},1,{0})", 1d), ("=1+LET(x,2,{0},3,x)", 3d), ("=LET(x,LET({0},1,{0}),x)", 1d) })
        {
            Assert.Equal(value, sheet.Evaluate(string.Format(CultureInfo.InvariantCulture, formula, declared)).Number);
            Assert.Equal(value, sheet.Evaluate(string.Format(CultureInfo.InvariantCulture, formula, undeclared)).Number);
        }
    }

    [Fact] // ADR-0181 (run 17), SH-67: a function's name followed by ( is still the function's call, whatever a LET binds; an undeclared one's call is still #NAME?
    public void ADR0181_a_bound_functions_name_followed_by_a_parenthesis_is_still_its_call()
    {
        var sheet = NewSheet();

        Assert.Equal(3, sheet.Evaluate("=LET(SUM,2,SUM(SUM,1))").Number);
        Assert.Equal(5, sheet.Evaluate("=LET(PI,2,ROUND(PI(),0)+PI)").Number);
        Assert.Equal(6, sheet.Evaluate("=LET(LET,2,LET(x,LET*3,x))").Number);
        Assert.Equal(ErrorValue.Name, sheet.Evaluate("=LET(IRR,1,IRR(IRR))").Error);
        Assert.Equal("=LET(SUM,2,SUM(SUM,1))", Entered("=let(SUM,2,sum(SUM,1))"));
        Assert.Equal("=LET(sum,2,SUM(sum,1))", Entered("=let(sum,2,sum(sum,1))"));
    }

    [Fact] // ADR-0181 (run 17), SH-67: a function's name is bound in the scope ADR-0181 gives every name, and outside every LET it is #NAME? as before
    public void ADR0181_a_functions_name_is_bound_only_where_its_let_reaches()
    {
        var sheet = NewSheet();

        Assert.Equal(ErrorValue.Name, sheet.Evaluate("=LET(x,1,1)+SUM").Error);
        Assert.Equal(ErrorValue.Name, sheet.Evaluate("=LET(SUM,1,1)+SUM").Error);
        Assert.Equal(ErrorValue.Name, sheet.Evaluate("=LET(x,N,N,1,x)").Error);
        Assert.Equal(5, sheet.Evaluate("=LET(N,2,LET(N,N+3,N))").Number);
    }

    [Theory] // ADR-0181 (run 17), SH-67: an R1C1 shape that does not read as an A1 Reference is a name, as the keyboard took R1C1 and RC (LET-103, LET-106)
    [InlineData("=LET(R1C1,2,R1C1+1)")]
    [InlineData("=LET(RC,2,RC+1)")]
    [InlineData("=LET(rc,2,RC+1)")]
    [InlineData("=LET(r1c1,2,R1C1+1)")]
    [InlineData("=LET(R1C,2,R1C+1)")]
    [InlineData("=LET(R10C20,2,r10c20+1)")]
    public void ADR0181_an_r1c1_shape_is_a_name(string formula)
    {
        Assert.Equal(3, NewSheet().Evaluate(formula).Number);
    }

    [Fact] // ADR-0181 (run 17), SH-67: a value left empty is bound, and the name alone shows 0, as the keyboard showed for LET(x,,x) (LET-107) and as IF(TRUE,) shows
    public void ADR0181_a_value_left_empty_is_bound()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=LET(x,,x)");

        Assert.Equal(0, sheet.Number("A1"));
        Assert.Equal("=LET(x,,x)", sheet.GetEntry(CellAddress.Parse("A1"))!.Formula);
        Assert.Equal(sheet.Evaluate("=IF(TRUE,)"), sheet.Evaluate("=LET(x,,x)"));
        Assert.Equal(1, sheet.Evaluate("=LET(x,,y,x,1)").Number);
        Assert.Equal(0, sheet.Evaluate("=LET(x,,y,x,y)").Number);
        Assert.Equal(0, sheet.Evaluate("=LET(x,,y,,x)").Number);
    }

    [Theory] // ADR-0181 (run 17): elsewhere a name bound to a value left empty reads as that empty argument would read in its place
    [InlineData("=LET(x,,IF(FALSE,1,x))&\"a\"", "=IF(FALSE,1,)&\"a\"")]
    [InlineData("=LET(x,,y,x,IF(FALSE,1,y))&\"a\"", "=IF(FALSE,1,)&\"a\"")]
    [InlineData("=LET(x,,LOG(100,x))", "=LOG(100,)")]
    [InlineData("=LET(x,,ROUND(2.5,x))", "=ROUND(2.5,)")]
    [InlineData("=LET(x,,SUM(x,1))", "=SUM(,1)")]
    [InlineData("=LET(x,,COUNTA(x,1))", "=COUNTA(,1)")]
    [InlineData("=LET(x,,CONCAT(\"a\",x,\"b\"))", "=CONCAT(\"a\",,\"b\")")]
    [InlineData("=LET(x,,x+1)", "=1+0")]
    [InlineData("=LET(x,,x&\"a\")", "=\"a\"")]
    public void ADR0181_a_name_bound_empty_reads_as_the_empty_argument_would(string bound, string written)
    {
        var sheet = NewSheet();

        Assert.Equal(sheet.Evaluate(written), sheet.Evaluate(bound));
    }

    /// <summary>The Formula an entry stores.</summary>
    private static string Entered(string typed)
    {
        var sheet = NewSheet();
        sheet.Enter("A1", typed);
        return sheet.GetEntry(CellAddress.Parse("A1"))!.Formula!;
    }

    [Fact] // ADR-0181, SH-67: a name of 250 characters, and a 127th pair, are refused; 249 characters and 126 pairs are not
    public void ADR0181_a_name_is_at_most_249_characters_and_a_let_holds_at_most_126_pairs()
    {
        var sheet = NewSheet();

        Assert.Equal(3, sheet.Evaluate($"=LET({Name(249)},2,{Name(249)}+1)").Number);
        Assert.Throws<FormulaSyntaxException>(() => sheet.Enter("A1", $"=LET({Name(250)},2,{Name(250)}+1)"));
        Assert.Equal(1, sheet.Evaluate(Pairs(126)).Number);
        Assert.Throws<FormulaSyntaxException>(() => sheet.Enter("A1", Pairs(127)));
    }

    [Theory] // ADR-0181, SH-67: names start with a letter of any script or _, and go on with letters, digits and _; c and r alone are names
    [InlineData("=LET(c,2,c+1)")]
    [InlineData("=LET(r,2,r+1)")]
    [InlineData("=LET(_x,2,_x+1)")]
    [InlineData("=LET(_,2,_+1)")]
    [InlineData("=LET(名前,2,名前+1)")]
    [InlineData("=LET(é,2,É+1)")]
    [InlineData("=LET(Total_2026,2,total_2026+1)")]
    [InlineData("=LET(abc1234567,2,abc1234567+1)")] // past the last row, so no Reference
    public void ADR0181_a_name_excel_accepts_is_bound(string formula)
    {
        Assert.Equal(3, NewSheet().Evaluate(formula).Number);
    }

    [Fact] // ADR-0181: a name is seen by the values bound after it and its own calculation; an inner LET's value sees the outer name until it binds its own
    public void ADR0181_names_are_sequential_and_lexical()
    {
        var sheet = NewSheet();

        Assert.Equal(2, sheet.Evaluate("=LET(x,1,LET(x,x+1,x))").Number);
        Assert.Equal(8, sheet.Evaluate("=LET(x,2,y,LET(x,x*3,x+1),x+y-1)").Number);
        Assert.Equal(ErrorValue.Name, sheet.Evaluate("=LET(x,1,y,x+z,y)").Error);
        Assert.Equal(ErrorValue.Name, sheet.Evaluate("=LET(x,LET(y,1,y),y)").Error);
    }

    [Fact] // ADR-0181: a name read before its binding in the same LET is #NAME?, whichever script it is written in
    public void ADR0181_a_name_read_before_its_binding_is_name()
    {
        var sheet = NewSheet();

        Assert.Equal(ErrorValue.Name, sheet.Evaluate("=LET(x,名前,名前,2,x)").Error);
        Assert.Equal(ErrorValue.Name, sheet.Evaluate("=LET(x,LET(y,é,y),é,2,x)").Error);
    }

    [Theory] // ADR-0047, ADR-0181: a name of another script is a LET name or nothing; outside a LET that binds it, it is refused on entry, as before
    [InlineData("=名前")]
    [InlineData("=LET(x,1,名前)")]
    [InlineData("=LET(x,1,x)+é")]
    public void ADR0181_a_name_of_another_script_no_let_binds_is_refused(string formula)
    {
        var sheet = NewSheet();

        Assert.Throws<FormulaSyntaxException>(() => sheet.Enter("A1", formula));
    }

    [Theory] // ADR-0047, ADR-0181: outside a LET, a name of another script is refused at its first such character and with the same words as before LET was admitted, whatever follows it
    [InlineData("=A1é", 3)]
    [InlineData("=1é", 2)]
    [InlineData("=é!A1", 1)]
    [InlineData("=1+é)", 3)]
    [InlineData("=SUM(A1,é", 8)]
    [InlineData("=é+\"x", 1)]
    [InlineData("=Aé", 2)]
    [InlineData("=1+名前+", 3)]
    public void ADR0181_outside_a_let_a_name_of_another_script_is_refused_where_it_stands(string formula, int position)
    {
        var refused = Assert.Throws<FormulaSyntaxException>(() => NewSheet().Enter("A1", formula));

        Assert.Equal(position, refused.Position);
        Assert.Equal($"'{formula[position]}' is not part of the formula syntax ExSheet reads.", refused.Reason);
    }

    [Fact] // ADR-0048: text typed with a leading minus that does not read as a Formula is still text
    public void A_signed_name_of_another_script_is_still_text()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "-été");

        Assert.Equal("-été", sheet.Value("A1")!.Value.Text);
    }

    [Fact] // ADR-0181: two bound ranges with a space between them would be Excel's intersection, which the engine does not implement: refused by name
    public void ADR0181_an_intersection_of_bound_names_is_refused()
    {
        var sheet = NewSheet();

        var refused = Assert.Throws<FormulaSyntaxException>(() => sheet.Enter("A1", "=LET(x,B1:C2,y,C1:D2,x y)"));
        Assert.Contains("intersection operator", refused.Reason, StringComparison.Ordinal);
    }

    [Fact] // ADR-0181, SH-67: a LET depends on every cell its text names, a binding's included, read or not
    public void ADR0181_dependencies_follow_the_text_bindings_included()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "3");
        sheet.Enter("A2", "4");
        sheet.Enter("B1", "=LET(x,A1,unused,A2,x*2)");
        Assert.Equal(6, sheet.Number("B1"));

        sheet.Enter("A1", "4");
        Assert.Equal(8, sheet.Number("B1"));

        var change = sheet.Enter("A2", "5");
        Assert.Contains(CellAddress.Parse("B1"), change.Recalculated);
    }

    [Fact] // ADR-0181, SH-67: a bound range follows a change inside it (LET-FOLLOWUP-012)
    public void ADR0181_a_bound_range_follows_a_change_inside_it()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("A2", "2");
        sheet.Enter("A3", "3");
        sheet.Enter("B1", "=LET(x,A1:A3,SUM(x))");
        Assert.Equal(6, sheet.Number("B1"));

        sheet.Enter("A2", "5");

        Assert.Equal(9, sheet.Number("B1"));
    }

    [Fact] // ADR-0181, ADR-0047, SH-67: a cycle through a binding the calculation never reads is still #CIRC!
    public void ADR0181_a_cycle_through_an_unused_binding_is_circ()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=LET(x,B1,1)");
        sheet.Enter("B1", "=A1+1");

        Assert.Equal(ErrorValue.Circ, sheet.Error("A1"));
        Assert.Equal(ErrorValue.Circ, sheet.Error("B1"));
    }

    [Fact] // ADR-0181, ADR-0049, SH-67: an unused binding over a waiting Linked Table waits, and IFERROR does not catch it
    public void ADR0181_an_unused_binding_over_a_waiting_table_waits()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Rates", ["Pair", "Mid"]);
        sheet.Enter("A1", "=LET(x,Rates[Mid],1)");
        sheet.Enter("A2", "=IFERROR(LET(x,Rates[Mid],1),0)");

        Assert.Equal(ErrorValue.GettingData, sheet.Error("A1"));
        Assert.Equal(ErrorValue.GettingData, sheet.Error("A2"));

        sheet.PushLinkedTable("Rates", [[Value.FromText("EURUSD"), Value.FromNumber(1.08)]]);

        Assert.Equal(1, sheet.Number("A1"));
        Assert.Equal(1, sheet.Number("A2"));
    }

    [Fact] // ADR-0181, ADR-0049, SH-67: Table[Column] inside a LET reads the Linked Table, whatever the LET binds
    public void ADR0181_a_table_column_reads_the_linked_table_whatever_is_bound()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Rates", ["Pair", "Spot"]);
        sheet.PushLinkedTable("Rates",
        [
            [Value.FromText("EURUSD"), Value.FromNumber(1.5)],
            [Value.FromText("USDJPY"), Value.FromNumber(2.5)],
        ]);

        Assert.Equal(4, sheet.Evaluate("=LET(Rates,100,SUM(Rates[Spot]))").Number);
        Assert.Equal(4, sheet.Evaluate("=LET(Spot,100,SUM(Rates[Spot]))").Number);
        Assert.Equal(104, sheet.Evaluate("=LET(Rates,100,Rates+SUM(Rates[Spot]))").Number);
    }

    [Fact] // ADR-0181, ADR-0125: a bound array, and a calculation whose result is an array, spill
    public void ADR0181_a_bound_array_spills()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=LET(x,SEQUENCE(3),x+10)");

        Assert.Equal(11, sheet.Number("A1"));
        Assert.Equal(12, sheet.Number("A2"));
        Assert.Equal(13, sheet.Number("A3"));
        Assert.Equal(CellAddress.Parse("A1"), sheet.SpilledFrom(CellAddress.Parse("A3")));
    }

    [Fact] // ADR-0181, ADR-0046/0047: an insertion rewrites the References inside a LET and leaves its names as typed
    public void ADR0181_an_insertion_rewrites_references_and_never_names()
    {
        var sheet = NewSheet();
        sheet.Enter("A5", "7");
        sheet.Enter("B1", "=LET(x,A5,Total,x*2,Total)");

        sheet.InsertRows(2);

        Assert.Equal("=LET(x,A6,Total,x*2,Total)", sheet.GetEntry(CellAddress.Parse("B1"))!.Formula);
        Assert.Equal(14, sheet.Number("B1"));
    }

    [Fact] // ADR-0181, ADR-0048: a copy shifts the References inside a LET and leaves its names as typed
    public void ADR0181_a_fill_shifts_references_and_never_names()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("A2", "2");
        sheet.Enter("B1", "=LET(x,A1,x*10)");

        sheet.Do(SheetEdit.Fill(CellRange.Parse("B1"), CellRange.Parse("B1:B2"), FillDirection.Down));

        Assert.Equal("=LET(x,A2,x*10)", sheet.GetEntry(CellAddress.Parse("B2"))!.Formula);
        Assert.Equal(20, sheet.Number("B2"));
    }

    [Fact] // ADR-0181, ADR-0047: the stored Formula writes LET in upper case and its names as typed
    public void ADR0181_names_are_written_as_typed()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=let(Share,0.05,Amount,100,share*amount)");

        Assert.Equal("=LET(Share,0.05,Amount,100,share*amount)", sheet.GetEntry(CellAddress.Parse("A1"))!.Formula);
        Assert.Equal(5, sheet.Number("A1"));
    }

    [Fact] // ADR-0181, ADR-0048: a Sheet Document holding a LET opens to the same Values
    public void ADR0181_a_document_with_a_let_opens_to_the_same_values()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "2");
        sheet.Enter("B1", "=LET(名前,A1*3,名前+1)");

        var reopened = Sheet.Open(SheetDocument.FromJson(sheet.ToDocument().ToJson()));

        Assert.Equal(7, reopened.Number("B1"));
    }
}
