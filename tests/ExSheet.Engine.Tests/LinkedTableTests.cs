using ExSheet.Engine;
using Xunit;

namespace ExSheet.Engine.Tests;

/// <summary>
/// Linked Tables (ticket 16, SH-16) and a table's key (ticket 36, SH-33) are in the shared cases
/// (<c>tests/cases/sheet/linked-table.json</c>). What stays here reads the Workbook's .NET surface, which no case can.
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
}
