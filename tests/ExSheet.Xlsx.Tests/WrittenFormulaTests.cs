using System.Globalization;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Xlsx.Tests;

/// <summary>
/// A Formula written as Excel stores it (ADR-0330 §4): its prefixes put back where Excel writes them and nowhere else, and read
/// back by the reader as the same Formula.
/// </summary>
public class WrittenFormulaTests
{
    private static string Written(string formula)
    {
        var text = WrittenFormula.Write(Entry.Parse(formula, CultureInfo.InvariantCulture)!, null, out var problem);
        Assert.Equal(WrittenFormulaProblem.None, problem);
        return text;
    }

    [Theory] // ADR-0330 §4: _xlfn., _xlfn._xlws., _xlpm. and ANCHORARRAY are put back where Excel stores them, and nowhere else
    [InlineData("=SUM(A1:B2)", "SUM(A1:B2)")]
    [InlineData("=XLOOKUP(A1,B:B,C:C)", "_xlfn.XLOOKUP(A1,B:B,C:C)")]
    [InlineData("=STDEV.S(A1:A9)+VAR.P(A1:A9)", "_xlfn.STDEV.S(A1:A9)+_xlfn.VAR.P(A1:A9)")]
    [InlineData("=FILTER(A1:A9,B1:B9>0)", "_xlfn._xlws.FILTER(A1:A9,B1:B9>0)")]
    [InlineData("=SORT(UNIQUE(A1:A9))", "_xlfn._xlws.SORT(_xlfn.UNIQUE(A1:A9))")]
    [InlineData("=SUM(A1#)", "SUM(_xlfn.ANCHORARRAY(A1))")]
    [InlineData("=COUNT(Sheet2!$B$3#)", "COUNT(_xlfn.ANCHORARRAY(Sheet2!$B$3))")]
    [InlineData("=SUM('My Sheet'!A1#)*2", "SUM(_xlfn.ANCHORARRAY('My Sheet'!A1))*2")]
    [InlineData("=LET(x,1,x+1)", "_xlfn.LET(_xlpm.x,1,_xlpm.x+1)")]
    [InlineData("=LET(rate,0.05,amount,A1,amount*(1+rate))", "_xlfn.LET(_xlpm.rate,0.05,_xlpm.amount,A1,_xlpm.amount*(1+_xlpm.rate))")]
    [InlineData("=LET(x,1,LET(y,x+1,x*y))", "_xlfn.LET(_xlpm.x,1,_xlfn.LET(_xlpm.y,_xlpm.x+1,_xlpm.x*_xlpm.y))")]
    [InlineData("=LET(x,SEQUENCE(3),SUM(x))", "_xlfn.LET(_xlpm.x,_xlfn.SEQUENCE(3),SUM(_xlpm.x))")]
    [InlineData("=\"_xlfn.SEQUENCE(1) and LET(x,1,x)\"&SEQUENCE(1)", "\"_xlfn.SEQUENCE(1) and LET(x,1,x)\"&_xlfn.SEQUENCE(1)")]
    [InlineData("=IFS(A1>1E+5,\"big\",TRUE,\"small\")", "_xlfn.IFS(A1>100000,\"big\",TRUE,\"small\")")]
    [InlineData("=SUM(1:3)+SUM($A:$C)", "SUM(1:3)+SUM($A:$C)")]
    [InlineData("=Sheet2!#REF!+#N/A", "Sheet2!#REF!+#N/A")]
    [InlineData("=LOG10(100)+XOR(TRUE,FALSE)", "LOG10(100)+_xlfn.XOR(TRUE,FALSE)")]
    [InlineData("=CONCAT(\"a\",\"\"\"b\"\"\")", "_xlfn.CONCAT(\"a\",\"\"\"b\"\"\")")]
    public void ADR0330_a_formula_is_written_with_excels_stored_prefixes(string formula, string stored)
    {
        Assert.Equal(stored, Written(formula));
    }

    [Theory] // ADR-0330 §4: a function the engine does not declare is written with its prefix too, so Excel computes it
    [InlineData("=TEXTSPLIT(A1,\",\")", "_xlfn.TEXTSPLIT(A1,\",\")")]
    [InlineData("=LAMBDA(x,y,x+y)(1,2)", null)]
    [InlineData("=MAP(A1:A3,LAMBDA(v,v*2))", "_xlfn.MAP(A1:A3,_xlfn.LAMBDA(_xlpm.v,_xlpm.v*2))")]
    public void ADR0330_an_undeclared_function_is_written_with_its_prefix(string formula, string? stored)
    {
        Entry entry;
        try
        {
            entry = Entry.Parse(formula, CultureInfo.InvariantCulture)!;
        }
        catch (FormulaSyntaxException)
        {
            Assert.Null(stored);
            return;
        }
        Assert.Equal(stored, WrittenFormula.Write(entry, null, out _));
    }

    [Theory] // ADR-0330 §4: what is written reads back as the same Formula
    [InlineData("=XLOOKUP(A1,B:B,C:C,\"none\")")]
    [InlineData("=LET(x,SEQUENCE(3),y,x*2,SUM(x,y))")]
    [InlineData("=SUM(Sheet2!A1#)+SORTBY(A1:A3,B1:B3,-1)")]
    [InlineData("=FILTER(A1:B9,(B1:B9>0)*(A1:A9<>\"\"),\"none\")")]
    [InlineData("=IFERROR(1/0,NA())&TEXTJOIN(\",\",TRUE,A1:A3)")]
    [InlineData("=SUM(Sheet1:Sheet3!B2)+STDEV.S('Q1 Sales:Sheet3'!$B$2:C9)")]
    public void ADR0330_a_written_formula_reads_back_as_itself(string formula)
    {
        var stored = Written(formula);

        var read = StoredFormula.Read(stored, out var problem);

        Assert.Equal(StoredFormulaProblem.None, problem);
        Assert.Equal(formula, read);
    }

    [Theory] // ADR-0331 §7, ADR-0330 §4: a 3D Reference is stored as the engine writes it, and refused when the Workbook lacks a Sheet of either end
    [InlineData("=SUM(Sheet1:Sheet3!B2)", "SUM(Sheet1:Sheet3!B2)", true)]
    [InlineData("=AVERAGE('Q1 Sales:Sheet3'!B2:C3)", "AVERAGE('Q1 Sales:Sheet3'!B2:C3)", true)]
    [InlineData("=SUM(Sheet1:Sheet9!B2)", "SUM(Sheet1:Sheet9!B2)", false)]
    [InlineData("=SUM(Sheet9:Sheet3!B2)", "SUM(Sheet9:Sheet3!B2)", false)]
    public void ADR0331_a_3d_reference_is_written_as_stored(string formula, string stored, bool written)
    {
        var expected = written ? WrittenFormulaProblem.None : WrittenFormulaProblem.MissingSheet;
        string[] sheets = ["Sheet1", "Q1 Sales", "Sheet3"];
        var text = WrittenFormula.Write(Entry.Parse(formula, CultureInfo.InvariantCulture)!, n => sheets.Contains(n, StringComparer.OrdinalIgnoreCase), out var problem);

        Assert.Equal(expected, problem);
        Assert.Equal(stored, text);
    }

    [Fact] // ADR-0330 §6: a structured reference to no declared Linked Table has no Excel Table to read in the file written, and is not written
    public void ADR0330_a_structured_reference_to_no_declared_table_is_not_written()
    {
        WrittenFormula.Write(Entry.Parse("=SUM(Positions[PV])", CultureInfo.InvariantCulture)!, null, out var problem);
        Assert.Equal(WrittenFormulaProblem.StructuredReference, problem);

        WrittenFormula.Write(Entry.Parse("=SUM(Positions[Delta])", CultureInfo.InvariantCulture)!, null, out problem, Columns);
        Assert.Equal(WrittenFormulaProblem.StructuredReference, problem);
    }

    /// <summary>The Linked Table <c>Positions</c>, whose columns' names need every form a structured reference has.</summary>
    private static IReadOnlyList<string>? Columns(string table) =>
        table.Equals("Positions", StringComparison.OrdinalIgnoreCase) ? ["PV", "Market value", "Total $", "a#b", "it's", "[x]", "@PV", "Δ_2", "PV "] : null;

    [Theory] // ADR-0330 §6 (Q5): a structured reference to a Linked Table is written in Excel's own form (TEXT-021, TEXT-022), single brackets with ' before [ ] # ', and the rest of the Formula as before
    [InlineData("=SUM(Positions[PV])", "SUM(Positions[PV])")]
    [InlineData("=SUM(positions[[pv]])", "SUM(positions[pv])")]
    [InlineData("=XLOOKUP(1,Positions[Market value],Positions[PV])", "_xlfn.XLOOKUP(1,Positions[Market value],Positions[PV])")]
    [InlineData("=SUM(Positions[[a'#b]])", "SUM(Positions[a'#b])")]
    [InlineData("=SUM(Positions[[it''s]])", "SUM(Positions[it''s])")]
    [InlineData("=SUM(Positions[['[x']]])", "SUM(Positions['[x']])")]
    [InlineData("=SUM(Positions[Δ_2])", "SUM(Positions[Δ_2])")]
    [InlineData("=LET(x,Positions[PV],SUM(x))", "_xlfn.LET(_xlpm.x,Positions[PV],SUM(_xlpm.x))")]
    public void ADR0330_a_structured_reference_is_written_as_excel_stores_it(string formula, string stored)
    {
        var text = WrittenFormula.Write(Entry.Parse(formula, CultureInfo.InvariantCulture)!, null, out var problem, Columns);

        Assert.Equal(WrittenFormulaProblem.None, problem);
        Assert.Equal(stored, text);
    }

    [Theory] // ADR-0330 §6: a column whose name begins or ends with a space, which Excel's structured reference does not keep, or holds @ or a character Excel has not been seen to write there, is refused (ticket 07 asks)
    [InlineData("=SUM(Positions[PV ])")]
    [InlineData("=SUM(Positions[['@PV]])")]
    [InlineData("=SUM(Positions[[Total $]])")]
    public void ADR0330_a_column_excel_would_read_otherwise_is_not_written(string formula)
    {
        var entry = Entry.Parse(formula, CultureInfo.InvariantCulture)!;

        WrittenFormula.Write(entry, null, out var problem, Columns);

        Assert.Equal(WrittenFormulaProblem.ColumnName, problem);
    }

    [Theory] // ADR-0330 §6: a Linked Table's name read as a name of its own is #NAME? in ExSheet and the table in Excel, and is refused; one a LET binds is the LET's
    [InlineData("=SUM(Positions)", "TableName")]
    [InlineData("=ROWS(positions)+1", "TableName")]
    [InlineData("=LET(Positions,1,Positions+1)", "None")]
    public void ADR0330_a_linked_tables_bare_name_is_not_written(string formula, string expected)
    {
        WrittenFormula.Write(Entry.Parse(formula, CultureInfo.InvariantCulture)!, null, out var problem, Columns);

        Assert.Equal(expected, problem.ToString());
    }

    [Fact] // ADR-0330 §4: a name a LET binds is prefixed only where it is bound; read outside the LET it is a name of its own, unprefixed
    public void ADR0330_a_name_is_prefixed_only_where_a_let_binds_it()
    {
        Assert.Equal("_xlfn.LET(_xlpm.x,1,_xlpm.x)+x", Written("=LET(x,1,x)+x"));
    }

    [Fact] // ADR-0330 §4: where the names this writer finds bound are not the ones the engine bound, the Formula is refused rather than written with a prefix in the wrong place
    public void ADR0330_a_formula_whose_names_bind_otherwise_than_the_engine_binds_them_is_refused()
    {
        // A LET's name read in its own value is not yet bound to the engine; this writer binds a name from where it stands.
        var entry = Entry.Parse("=LET(x,x+1,x)", CultureInfo.InvariantCulture)!;
        Assert.Equal(["x"], entry.UnboundNames);

        WrittenFormula.Write(entry, null, out var problem);

        Assert.Equal(WrittenFormulaProblem.Unreadable, problem);
    }

    [Fact] // ADR-0330 §4: every function the engine declares that Excel stores with a prefix is in the writer's list
    public void ADR0330_every_newer_declared_function_is_written_with_its_prefix()
    {
        string[] newer =
        [
            "ACOT", "ACOTH", "AGGREGATE", "CEILING.MATH", "CONCAT", "COT", "COTH", "CSC", "CSCH", "DAYS", "FILTER", "FLOOR.MATH", "IFNA",
            "IFS", "LET", "MAXIFS", "MINIFS", "NUMBERVALUE", "RANK.EQ", "SEC", "SECH", "SEQUENCE", "SORT", "SORTBY", "STDEV.P", "STDEV.S",
            "SWITCH", "TEXTJOIN", "UNIQUE", "VAR.P", "VAR.S", "XLOOKUP", "XMATCH", "XOR",
        ];

        Assert.Equal(newer, DeclaredFunction.All.Select(f => f.Name).Where(WrittenFormula.Newer.Contains).Order(StringComparer.Ordinal));
    }

    /// <summary>Excel 2007's functions the engine declares, which a file stores with no prefix.</summary>
    private static readonly string[] Excel2007 =
    [
        "ABS", "ACOS", "ACOSH", "AND", "ASIN", "ASINH", "ATAN", "ATAN2", "ATANH", "AVERAGE", "AVERAGEIF", "AVERAGEIFS", "CHOOSE",
        "COLUMN", "COLUMNS", "CONCATENATE", "COS", "COSH", "COUNT", "COUNTA", "COUNTBLANK", "COUNTIF", "COUNTIFS", "DATE", "DATEDIF",
        "DAY", "DEGREES", "EDATE", "EOMONTH", "ERROR.TYPE", "EXACT", "EXP", "FIND", "FV", "HLOOKUP", "HOUR", "IF", "IFERROR",
        "INDEX", "INDIRECT", "INT", "ISBLANK", "ISERROR", "ISNA", "ISNUMBER", "ISTEXT", "LARGE", "LEFT", "LEN", "LN", "LOG", "LOG10",
        "LOWER", "MATCH", "MAX", "MEDIAN", "MID", "MIN", "MINUTE", "MOD", "MONTH", "MROUND", "NA", "NETWORKDAYS", "NOT", "NOW",
        "OFFSET", "OR", "PI", "PMT", "POWER", "PRODUCT", "PROPER", "PV", "RADIANS", "RAND", "RANDBETWEEN", "REPLACE", "REPT",
        "RIGHT", "ROUND", "ROUNDDOWN", "ROUNDUP", "ROW", "ROWS", "SEARCH", "SECOND", "SIGN", "SIN", "SINH", "SMALL", "SQRT",
        "SUBSTITUTE", "SUBTOTAL", "SUM", "SUMIF", "SUMIFS", "SUMPRODUCT", "TAN", "TANH", "TEXT", "TIME", "TODAY", "TRANSPOSE",
        "TRIM", "TRUNC", "UPPER", "VALUE", "VLOOKUP", "WEEKDAY", "WORKDAY", "XNPV", "YEAR", "YEARFRAC",
    ];

    [Fact] // ADR-0330 §4: every function the engine declares is either one of Excel 2007's, written bare, or a newer one, written with its prefix; one the engine adds fails here until it is placed
    public void ADR0330_every_declared_function_is_placed_as_excel_2007s_or_newer()
    {
        var excel2007 = Excel2007.ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.All(DeclaredFunction.All.Select(f => f.Name), name =>
            Assert.True(excel2007.Contains(name) != WrittenFormula.Newer.Contains(name), $"{name} is placed in {(excel2007.Contains(name) ? "both lists" : "neither list")}."));
    }

    [Theory] // ADR-0330 §4: a call of a function the engine declares is the function's, though a LET binds a name of the same spelling: only the name read is the binding's
    [InlineData("=LET(sum,1,SUM(sum,2))", "_xlfn.LET(_xlpm.sum,1,SUM(_xlpm.sum,2))")]
    [InlineData("=LET(xor,TRUE,XOR(xor,FALSE))", "_xlfn.LET(_xlpm.xor,TRUE,_xlfn.XOR(_xlpm.xor,FALSE))")]
    [InlineData("=LET(max,5,MAX(max,1))", "_xlfn.LET(_xlpm.max,5,MAX(_xlpm.max,1))")]
    [InlineData("=LET(seq,2,SEQUENCE(seq))", "_xlfn.LET(_xlpm.seq,2,_xlfn.SEQUENCE(_xlpm.seq))")]
    public void ADR0330_a_declared_function_called_where_a_let_binds_its_name_is_the_function(string formula, string stored)
    {
        Assert.Equal(stored, Written(formula));
    }

    [Theory] // ADR-0330 §4: a bound name called is a call of an unknown function to the engine (#NAME?) and of the binding to Excel: refused, not written to mean another thing
    [InlineData("=LET(f,LAMBDA(x,x+1),f(2))")]
    [InlineData("=MAP(A1:A3,LAMBDA(g,g(1)))")]
    public void ADR0330_a_bound_name_called_is_refused(string formula)
    {
        WrittenFormula.Write(Entry.Parse(formula, CultureInfo.InvariantCulture)!, null, out var problem);

        Assert.Equal(WrittenFormulaProblem.BoundCall, problem);
    }

    [Fact] // ADR-0330 §6: a text constant in a Formula is at most 255 characters in Excel, a doubled quote counting as one
    public void ADR0330_a_text_constant_longer_than_excel_takes_is_refused()
    {
        var longest = "=LEN(\"" + new string('x', 254) + "\"\"\")";
        var tooLong = "=LEN(\"" + new string('x', 256) + "\")";

        Assert.Equal(longest[1..], Written(longest));
        WrittenFormula.Write(Entry.Parse(tooLong, CultureInfo.InvariantCulture)!, null, out var problem);
        Assert.Equal(WrittenFormulaProblem.LongText, problem);
    }

    [Fact] // ADR-0330 §6: Excel nests calls 64 levels deep at most; parentheses that call nothing are no level
    public void ADR0330_calls_nested_deeper_than_excel_takes_are_refused()
    {
        static string Nested(int depth) => "=" + string.Concat(Enumerable.Repeat("ABS(", depth)) + "((1))" + new string(')', depth);

        Assert.Equal(Nested(WrittenFormula.MaxDepth)[1..], Written(Nested(WrittenFormula.MaxDepth)));
        // One level deeper never reaches the writer: the engine refuses it as it is typed (ADR-0047).
        Assert.Throws<FormulaSyntaxException>(() => Entry.Parse(Nested(WrittenFormula.MaxDepth + 1), CultureInfo.InvariantCulture));
    }

    [Fact] // ADR-0330 §6: a call takes 255 arguments at most in Excel
    public void ADR0330_a_call_with_more_arguments_than_excel_takes_is_refused()
    {
        static string Call(int arguments) => "=HSTACK(" + string.Join(",", Enumerable.Repeat("1", arguments)) + ")";

        Assert.Equal("_xlfn." + Call(WrittenFormula.MaxArguments)[1..], Written(Call(WrittenFormula.MaxArguments)));
        WrittenFormula.Write(Entry.Parse(Call(WrittenFormula.MaxArguments + 1), CultureInfo.InvariantCulture)!, null, out var problem);
        Assert.Equal(WrittenFormulaProblem.TooManyArguments, problem);
    }
}
