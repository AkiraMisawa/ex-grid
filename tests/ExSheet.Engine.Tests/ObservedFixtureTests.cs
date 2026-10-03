using System.Text.Json;
using Xunit;

namespace ExSheet.Engine.Tests;

public class ObservedFixtureTests
{
    [Fact] // ADR-0047: the Oracle's Value2 fixtures retain their kinds and exact doubles.
    public void Observed_values_are_not_reparsed_as_typed_entries()
    {
        using var document = JsonDocument.Parse("""
            {"description":"Value2 fixtures", "culture":"en-GB",
             "values":{"A1":1.0000000000000002,"A2":"2","A3":true,"A4":null,"A5":"#N/A"},
             "cells":{"B1":"=SUM(A1:A5)"}, "check":"B1", "expect":{"value2":1.0000000000000002}}
            """);
        Assert.Empty(ExcelCorpus.Run(document.RootElement));
    }
}
