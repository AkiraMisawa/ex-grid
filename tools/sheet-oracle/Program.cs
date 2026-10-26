using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using ExSheet.Engine;

// The C# side of @ex-grid/sheet-engine's differential runs (ADR-0370, section 2).
//
//   serve   answers one JSON request per line on stdin with one JSON line on stdout
//
// src/npm/sheet-engine/differential/oracle.ts starts `serve` and drives it (differential/README.md says how).
return args switch
{
    ["serve"] => ExSheet.Oracle.Serve.Run(),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("usage: ExSheet.Oracle serve");
    return 2;
}

namespace ExSheet.Oracle
{
    /// <summary>
    /// A request is a Sheet: its culture, the entries typed into it in order, the Number Formats set on cells, and the
    /// cells to read. The answer is each cell read: its Value, exactly, and its display, at a column width when the
    /// request names one. An entry the engine refuses is answered by the kind of refusal, as a shared case reads one,
    /// and a format code it does not read by its reason.
    /// </summary>
    internal static class Serve
    {
        public static int Run()
        {
            using var output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = false };
            using var input = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
            while (input.ReadLine() is { } line)
            {
                if (line.Length == 0) continue;
                JsonNode answer;
                try
                {
                    answer = Answer(JsonNode.Parse(line)!.AsObject());
                }
                catch (Exception e)
                {
                    // Every exception is an answer, so the answers stay in step with the requests: one the Script
                    // Core does not throw is a difference the run reports by name, not a stalled run.
                    answer = new JsonObject { ["error"] = e.GetType().Name, ["message"] = e.Message };
                }
                output.WriteLine(answer.ToJsonString());
                output.Flush();
            }
            return 0;
        }

        private static JsonNode Answer(JsonObject request)
        {
            var name = (string)request["culture"]!;
            var culture = name == "invariant" ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(name);
            var (previous, previousUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
            // The thread is in the Sheet's culture, so that a format or parse that leaked it would show (ADR-0047).
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
            try
            {
                var workbook = new Workbook(culture);
                var sheet = workbook.Shown;
                var refusals = new JsonObject();
                foreach (var entry in request["entries"]!.AsArray())
                {
                    var address = (string)entry![0]!;
                    try
                    {
                        workbook.Do(sheet.Name, SheetEdit.Enter(CellAddress.Parse(address), (string)entry[1]!));
                    }
                    catch (FormulaSyntaxException e)
                    {
                        refusals[address] = new JsonObject { ["refusal"] = "formulaSyntax", ["reason"] = e.Reason };
                    }
                    catch (SheetRefusedException e)
                    {
                        refusals[address] = new JsonObject { ["refusal"] = "refused", ["reason"] = Camel(e.Refusal.Reason.ToString()) };
                    }
                }
                // A format code the engine does not read is answered by its reason under the code, and a range the
                // Sheet refuses to format by its refusal under the range, as an entry's refusal is.
                var formatRefusals = new JsonObject();
                if (request["formats"] is JsonArray formats)
                {
                    foreach (var format in formats)
                    {
                        var (range, code) = ((string)format![0]!, (string)format[1]!);
                        if (!NumberFormat.TryParse(code, out var parsed, out var reason))
                        {
                            formatRefusals[code] = reason;
                            continue;
                        }
                        try
                        {
                            workbook.Do(sheet.Name, SheetEdit.SetNumberFormat(CellRange.Parse(range), parsed));
                        }
                        catch (SheetRefusedException e)
                        {
                            formatRefusals[range] = new JsonObject { ["refusal"] = "refused", ["reason"] = Camel(e.Refusal.Reason.ToString()) };
                        }
                    }
                }
                var width = request["width"] is { } w ? (double?)w : null;
                var cells = new JsonObject();
                foreach (var read in request["read"]!.AsArray())
                {
                    var at = CellAddress.Parse((string)read!);
                    var display = width is { } columns ? sheet.GetDisplay(at, columns) : sheet.GetDisplay(at);
                    cells[(string)read!] = new JsonObject
                    {
                        ["value"] = Encode(sheet.GetValue(at)),
                        ["text"] = display.Text,
                        ["cannotShow"] = display.CannotShow,
                    };
                }
                return new JsonObject { ["cells"] = cells, ["refusals"] = refusals, ["formatRefusals"] = formatRefusals };
            }
            finally
            {
                (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (previous, previousUi);
            }
        }

        private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

        /// <summary>A Value as the run compares it: a number by its 64 bits, so that -0, the last digit and NaN all show.</summary>
        private static JsonNode? Encode(Value? value)
        {
            if (value is not { } v) return null;
            return v.Kind switch
            {
                ValueKind.Number => new JsonObject
                {
                    ["number"] = BitConverter.DoubleToInt64Bits(v.Number).ToString("x16", CultureInfo.InvariantCulture),
                    ["shown"] = v.Number.ToString("R", CultureInfo.InvariantCulture),
                },
                ValueKind.Text => new JsonObject { ["text"] = v.Text },
                ValueKind.Boolean => new JsonObject { ["boolean"] = v.Boolean },
                ValueKind.Error => new JsonObject { ["error"] = v.ToString(), ["refused"] = v.IsRefused },
                _ => throw new InvalidOperationException($"{v.Kind} is no kind of Value."),
            };
        }
    }
}
