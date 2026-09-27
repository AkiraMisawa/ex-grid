using System.Globalization;
using ExSheet.Engine;

namespace ExSheet.Engine.Tests;

/// <summary>Shorthand for the tests: cells by their A1 address.</summary>
internal static class SheetTestExtensions
{
    public static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    public static Sheet NewSheet() => new(EnUs);

    public static SheetChange Enter(this Sheet sheet, string address, string typed) =>
        sheet.Enter(CellAddress.Parse(address), typed);

    public static Value? Value(this Sheet sheet, string address) => sheet.GetValue(CellAddress.Parse(address));

    public static double Number(this Sheet sheet, string address) =>
        sheet.Value(address) is { Kind: ValueKind.Number } v ? v.Number : throw new Xunit.Sdk.XunitException($"{address} is {sheet.Value(address)?.ToString() ?? "blank"}, not a number.");

    public static ErrorValue Error(this Sheet sheet, string address) =>
        sheet.Value(address) is { Kind: ValueKind.Error } v ? v.Error : throw new Xunit.Sdk.XunitException($"{address} is {sheet.Value(address)?.ToString() ?? "blank"}, not an Error Value.");

    /// <summary>Enters <paramref name="formula"/> in a scratch cell far from the data and returns its Value.</summary>
    public static Value Evaluate(this Sheet sheet, string formula)
    {
        sheet.Enter("ZZ1000", formula);
        return sheet.Value("ZZ1000")!.Value;
    }

    public static string[] Addresses(this IEnumerable<CellAddress> addresses) => [.. addresses.Select(a => a.ToString())];
}
