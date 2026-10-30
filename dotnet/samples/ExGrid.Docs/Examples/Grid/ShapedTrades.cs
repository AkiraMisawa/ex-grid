namespace ExGrid.Docs.Examples.Grid;

using System.Text.Json;
using ExGrid.Data;

/// <summary>What a tool handed back as JSON, the input that says its types, and the grid's spec — all
/// three only text, as they would be when an AI composes the view and a workflow runs it later.</summary>
public static class ShapedTrades
{
    public const string Input = """
        {"columns": [
          {"name": "desk", "type": "text"},
          {"name": "notional", "type": "decimal"},
          {"name": "rate", "type": "double"},
          {"name": "trades", "type": "integer"},
          {"name": "settles", "type": "date"}
        ]}
        """;

    public const string Data = """
        [
          {"desk": "Rates", "notional": 125000000.50, "rate": 0.04125, "trades": 1342, "settles": "2026-10-22"},
          {"desk": "FX", "notional": "-8400000", "rate": 1.08734, "trades": 96, "settles": "2026-10-21"},
          {"desk": "Credit", "notional": 3250000.25, "rate": 0.0275, "trades": 410, "settles": "2026-10-23"},
          {"desk": "Equities", "notional": null, "rate": 0.0198, "trades": 7, "settles": "2026-10-22"}
        ]
        """;

    public const string Spec = """
        {"data": "trades",
         "columns": [
           {"column": "desk", "header": "Desk", "pinned": true, "width": 100},
           {"column": "notional", "header": "Notional", "format": "#,##0.00;(#,##0.00)"},
           {"column": "rate", "header": "Rate", "format": "0.00000"},
           {"column": "trades", "header": "Trades"},
           {"column": "settles", "header": "Settles"}
         ],
         "sorts": [{"column": "notional", "direction": "Descending"}]}
        """;

    /// <summary>The grid the three texts describe, or the refusal that names what is wrong.</summary>
    public static (ShapedGrid? Grid, string? Refusal) Build(string input, string data, string spec)
    {
        try
        {
            using var declaration = JsonDocument.Parse(input);
            var trades = BoardInput.Read("trades", declaration.RootElement, "inputs.trades");
            var snapshot = trades.FromJson(data);
            var reading = GridViewSpec.Read(spec, name => name == trades.Name ? trades.Shape : null);
            return (ShapedGrid.Of(snapshot, reading), null);
        }
        catch (BoardRefusedException refused)
        {
            return (null, refused.Path.Length == 0 ? refused.Message : $"{refused.Path}: {refused.Message}");
        }
        catch (JsonException notJson)
        {
            return (null, $"The input is not JSON: {notJson.Message}");
        }
    }
}
