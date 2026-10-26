using ExSheet.Engine;
using Xunit;

namespace ExSheet.Engine.Tests;

/// <summary>
/// Linked Tables (ticket 16, SH-16) and a table's key (ticket 36, SH-33) are in the shared cases
/// (<c>tests/cases/sheet/linked-table.json</c>). What stays here reads the Workbook's .NET surface, which no case can:
/// the methods it has, and the parameter an argument refusal names, which the Script Core's RangeError does not carry.
/// </summary>
public class LinkedTableTests
{
    [Fact] // ADR-0049, ADR-0331 §9: a table is never undeclared — the Workbook, which holds the tables, has no API for it
    public void A_table_is_never_undeclared()
    {
        Assert.Equal(
            ["DeclareLinkedTable", "DeclareLinkedTable", "LinkedTableColumns", "PushLinkedTable", "get_LinkedTables"],
            typeof(Workbook).GetMethods().Select(m => m.Name).Where(n => n.Contains("LinkedTable", StringComparison.Ordinal) || n.Contains("Undeclare", StringComparison.Ordinal)).Order(StringComparer.Ordinal));
    }

    [Fact] // ADR-0049 (2026-09-30), SH-33: a key that is none of the columns is refused as the key argument
    public void A_key_that_is_no_column_is_refused_as_the_key_argument()
    {
        var workbook = new Workbook(System.Globalization.CultureInfo.GetCultureInfo("en-US"));

        var refused = Assert.Throws<ArgumentException>(() => workbook.DeclareLinkedTable("Positions", ["Id", "PV"], key: "Book"));

        Assert.Equal("key", refused.ParamName);
    }
}
