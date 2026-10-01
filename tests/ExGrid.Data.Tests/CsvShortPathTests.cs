using ExGrid.Data;
using Xunit;
using static ExGrid.Data.Tests.CsvFixtures;
using static ExGrid.Data.Tests.Fixtures;

namespace ExGrid.Data.Tests;

/// <summary>
/// The CSV reader reads the common cases by short paths over the field's bytes (ticket 07): a
/// Boolean's ASCII spellings, a date in a fixed layout. Each must read what the full reading reads,
/// and refuse what it refuses (ADR-0063).
/// </summary>
public class CsvShortPathTests
{
    [Fact] // ADR-0063: a Boolean's spellings are matched ignoring case exactly as .NET's ordinal comparison does, whatever bytes the field holds
    public void Boolean_spellings_match_as_an_ordinal_comparison_ignoring_case_does()
    {
        string[] fields = ["TRUE", "true", "tRuE", "FALSE", "false", "FaLsE", "FALſE", "ＴＲＵＥ", "TRUÉ", "truE ", "  false", "T", "TRUEX", "ı", "FALSE\t", "FALSE ", "Ｆalse", "tRUE"];
        var schema = new CsvSchema([new("B", SnapshotKind.Boolean)]);

        foreach (var field in fields)
        {
            var text = field.Trim(' ');
            bool? expected = text.Equals("TRUE", StringComparison.OrdinalIgnoreCase) ? true
                : text.Equals("FALSE", StringComparison.OrdinalIgnoreCase) ? false
                : null;
            if (expected is { } value)
                Assert.Equal([value], Values(Read(schema, $"B\n{field}\n"), "B"));
            else
                Assert.Equal($"Row 1, column 'B': '{field}' is not a spelling of true or false ('TRUE', 'FALSE') (line 2).", Refusal(schema, $"B\n{field}\n").Message);
        }
    }

    [Fact] // ADR-0063: declared spellings of any case, ASCII or not, are matched ignoring case
    public void Declared_spellings_are_matched_ignoring_case()
    {
        var ascii = new CsvSchema([new("B", SnapshotKind.Boolean) { TrueText = ["Yes", "y"], FalseText = ["No", "n"] }]);
        var japanese = new CsvSchema([new("B", SnapshotKind.Boolean) { TrueText = ["はい", "Ok"], FalseText = ["いいえ"] }]);

        Assert.Equal([true, true, true, false, false, false], Values(Read(ascii, "B\nYES\ny\nyEs\nnO\nN\nno\n"), "B"));
        Assert.Equal(1, Refusal(ascii, "B\nyess\n").Row);
        Assert.Equal([true, false, true, true], Values(Read(japanese, "B\nはい\nいいえ\nOK\nok\n"), "B"));
        Assert.Equal(1, Refusal(japanese, "B\nいい\n").Row);
    }
}
