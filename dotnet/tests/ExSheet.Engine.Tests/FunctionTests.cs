using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0047 admits a function only with Excel's result, edge cases included (SH-7). What each
/// function answers is in the Excel case corpus (<c>ExcelCases/*.json</c>, run by
/// <see cref="ExcelCaseTests"/> and by the Excel oracle); what stays here is the engine's own
/// surface: the declared list. Dependencies, refusals on entry and XLOOKUP's regular expressions are shared cases
/// (<c>tests/cases/sheet/functions.json</c>).
/// </summary>
public class FunctionTests
{
    [Fact] // ADR-0047/0051: the declared list is exposed for completion and hints, and is exactly ADR-0047's set with its additions
    public void The_declared_functions_are_exactly_adr_0047s_set()
    {
        Assert.Equal(
            [
                "ABS", "ACOS", "ACOSH", "ACOT", "ACOTH", "AGGREGATE", "AND", "ASIN", "ASINH", "ATAN", "ATAN2", "ATANH", "AVERAGE", "AVERAGEIF", "AVERAGEIFS", "CEILING.MATH", "CHOOSE", "COLUMN", "COLUMNS", "CONCAT", "CONCATENATE", "COS", "COSH", "COT", "COTH", "COUNT", "COUNTA", "COUNTBLANK", "COUNTIF", "COUNTIFS", "CSC", "CSCH", "DATE", "DATEDIF", "DAY", "DAYS", "DEGREES", "EDATE", "EOMONTH", "ERROR.TYPE", "EXACT", "EXP", "FILTER", "FIND", "FLOOR.MATH", "FV", "HLOOKUP", "HOUR", "IF", "IFERROR", "IFNA", "IFS", "INDEX", "INDIRECT", "INT", "ISBLANK", "ISERROR", "ISNA", "ISNUMBER", "ISTEXT", "LARGE", "LEFT", "LEN", "LET", "LN", "LOG", "LOG10", "LOWER", "MATCH", "MAX", "MAXIFS", "MEDIAN", "MID", "MIN", "MINIFS", "MINUTE", "MOD", "MONTH", "MROUND", "NA", "NETWORKDAYS", "NOT", "NOW", "NUMBERVALUE", "OFFSET", "OR", "PI", "PMT", "POWER", "PRODUCT", "PROPER", "PV", "RADIANS", "RAND", "RANDBETWEEN", "RANK.EQ", "REPLACE", "REPT", "RIGHT", "ROUND", "ROUNDDOWN", "ROUNDUP", "ROW", "ROWS", "SEARCH", "SEC", "SECH", "SECOND", "SEQUENCE", "SIGN", "SIN", "SINH", "SMALL", "SORT", "SORTBY", "SQRT", "STDEV.P", "STDEV.S", "SUBSTITUTE", "SUBTOTAL", "SUM", "SUMIF", "SUMIFS", "SUMPRODUCT", "SWITCH", "TAN", "TANH", "TEXT", "TEXTJOIN", "TIME", "TODAY", "TRANSPOSE", "TRIM", "TRUNC", "UNIQUE", "UPPER", "VALUE", "VAR.P", "VAR.S", "VLOOKUP", "WEEKDAY", "WORKDAY", "XLOOKUP", "XMATCH", "XNPV", "XOR", "YEAR", "YEARFRAC",
            ],
            DeclaredFunction.All.Select(f => f.Name));
        Assert.Equal("SUM(number1, [number2], ...)", DeclaredFunction.Find("sum")!.Signature);
        Assert.Equal(
            "XLOOKUP(lookup_value, lookup_array, return_array, [if_not_found], [match_mode], [search_mode])",
            DeclaredFunction.Find("XLOOKUP")!.Signature);
        Assert.Null(DeclaredFunction.Find("IRR"));
        Assert.Null(DeclaredFunction.Find("NPV")); // ADR-0202, note of 2026-10-12: refused for 0.1.0 as IRR is
        Assert.All(DeclaredFunction.All, f => Assert.False(string.IsNullOrWhiteSpace(f.Description)));
    }
}
