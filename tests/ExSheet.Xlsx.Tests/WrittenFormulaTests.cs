using ExSheet.Engine;
using Xunit;

namespace ExSheet.Xlsx.Tests;

/// <summary>
/// The writer's list of Excel's newer functions against every function the engine declares (ADR-0330 §4): one the engine adds
/// fails here until it is placed. How a Formula is written is held by the shared cases (<c>tests/cases/sheet-xlsx/calls.json</c>).
/// </summary>
public class WrittenFormulaTests
{
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
}
