using System.Diagnostics.CodeAnalysis;
using ExSheet.Engine;

namespace ExSheet.Xlsx;

/// <summary>
/// A Number Format between an <c>.xlsx</c>'s id and code and the engine's <see cref="NumberFormat"/>, both ways (ADR-0330 §4,
/// ADR-0071). A built-in id is the engine's format of the same meaning: the dates and the time Excel shows in the culture's
/// own form (the short date 14 and 22, 15, 16, 20) are the engine's built-ins of the same invariant code, which it shows in
/// the Workbook's culture as Excel shows them in the computer's; the rest are their fixed codes (ECMA-376 Part 1, §18.8.30).
/// The currencies 5 to 8 are read from the code the file spells, as every other id whose code follows the computer's
/// locale: an amount is never shown in the reader's currency where the file meant another. Written, the engine's built-in
/// currency is a code of the file's own that spells the Workbook culture's currency, so that it means the same amount under
/// every locale Excel opens it in. A format of the file's own is read as written (<see cref="NumberFormat.TryParseAsWritten(string, out NumberFormat?, out string?)"/>).
/// A code the engine does not read, or a built-in id whose code follows the computer's locale and that the file does not
/// spell out, cannot cross, and the file is refused by name: a cell shown in General instead would show a date as
/// <c>45000</c>, or drop a currency symbol, and look plausible.
/// </summary>
internal static class XlsxNumberFormats
{
    /// <summary>The first id a styles part gives a format of its own.</summary>
    public const int FirstCustomId = 164;

    /// <summary>
    /// The built-ins whose meaning is fixed, by id, in the engine's invariant codes. 14 and 22 are the engine's built-in short
    /// dates (ECMA-376 spells them <c>mm-dd-yy</c> and <c>m/d/yy h:mm</c>, and Excel shows both in the computer's short-date
    /// pattern). The currencies 5 to 8 are not here: Excel shows them in the computer's currency.
    /// </summary>
    private static readonly Dictionary<int, string> BuiltIns = new()
    {
        [0] = "General",
        [1] = "0",
        [2] = "0.00",
        [3] = "#,##0",
        [4] = "#,##0.00",
        [9] = "0%",
        [10] = "0.00%",
        [11] = "0.00E+00",
        [12] = "# ?/?",
        [13] = "# ??/??",
        [14] = "m/d/yyyy",
        [15] = "d-mmm-yy",
        [16] = "d-mmm",
        [17] = "mmm-yy",
        [18] = "h:mm AM/PM",
        [19] = "h:mm:ss AM/PM",
        [20] = "h:mm",
        [21] = "h:mm:ss",
        [22] = "m/d/yyyy h:mm",
        [37] = "#,##0 ;(#,##0)",
        [38] = "#,##0 ;[Red](#,##0)",
        [39] = "#,##0.00;(#,##0.00)",
        [40] = "#,##0.00;[Red](#,##0.00)",
        [45] = "mm:ss",
        [46] = "[h]:mm:ss",
        [47] = "mmss.0",
        [48] = "##0.0E+0",
        [49] = "@",
    };

    /// <summary>The built-ins the engine shows in the culture's own form; a code of the engine's that spells one, in any case, is it.</summary>
    private static readonly HashSet<int> Localised = [14, 15, 16, 20, 22];

    /// <summary>The engine's built-in currency, in its invariant codes, with and without decimals (<see cref="NumberFormat.BuiltInCurrency"/>).</summary>
    private static readonly string[] BuiltInCurrencies = ["$#,##0.00_);[Red]($#,##0.00)", "$#,##0_);[Red]($#,##0)"];

    /// <summary>
    /// The code a format of the file's own writes for <paramref name="format"/> under the Workbook's <paramref name="culture"/>:
    /// the engine's built-in currency spelled in the culture's currency (<see cref="NumberFormat.LocalCode"/>), so that the file
    /// says which currency it is; any other format its own code.
    /// </summary>
    public static string WrittenCode(NumberFormat format, System.Globalization.CultureInfo culture) =>
        BuiltInCurrencies.Any(c => string.Equals(c, format.Code, StringComparison.OrdinalIgnoreCase)) ? format.LocalCode(culture) : format.Code;

    /// <summary>The fixed code of the built-in <paramref name="id"/> in the engine's invariant codes, or null for an id whose code follows the computer's locale.</summary>
    public static string? BuiltInCode(int id) => BuiltIns.GetValueOrDefault(id);

    /// <summary>
    /// The format a cell format's <paramref name="id"/> names, with the <paramref name="code"/> the styles part writes for it,
    /// or false with the reason it cannot cross and what to name. A built-in the engine knows is that built-in, whatever code
    /// the part writes beside it; any other id is the code the part writes.
    /// </summary>
    /// <param name="id">The Number Format's id.</param>
    /// <param name="code">The code the styles part writes for the id, or null.</param>
    /// <param name="format">The format, when it crosses.</param>
    /// <param name="reason">Why it does not, as a sentence.</param>
    /// <param name="held">What the refusal names as held: the code, or the id where the file writes none.</param>
    public static bool TryRead(int id, string? code, [NotNullWhen(true)] out NumberFormat? format, [NotNullWhen(false)] out string? reason, [NotNullWhen(false)] out string? held)
    {
        format = null;
        if (BuiltIns.TryGetValue(id, out var builtIn))
        {
            held = builtIn;
            if (NumberFormat.TryParse(builtIn, out format, out var why))
            {
                reason = null;
                held = null;
                return true;
            }
            reason = $"Excel's built-in Number Format {id} ({builtIn}) is not one the engine reads: {why}";
            return false;
        }
        if (code is null)
        {
            held = $"Number Format {id}";
            reason = id < FirstCustomId
                ? $"The cell takes Excel's built-in Number Format {id}, whose code follows the computer's locale, and the file does not spell it out."
                : $"The cell takes the Number Format {id}, which the file does not hold.";
            return false;
        }
        if (NumberFormat.TryParseAsWritten(code, out format, out var cannot))
        {
            reason = null;
            held = null;
            return true;
        }
        held = code;
        reason = $"The Number Format '{code}' is not one the engine reads: {cannot}";
        return false;
    }

    /// <summary>
    /// The built-in id <paramref name="format"/> is written as, or null for one written as a format of the file's own. A built-in
    /// the engine shows in the culture's form is its id whatever the case of its code, as the engine reads it; any other is
    /// its id only when its code is the built-in's exactly.
    /// </summary>
    public static int? BuiltInId(NumberFormat format)
    {
        foreach (var (id, code) in BuiltIns)
        {
            var same = Localised.Contains(id)
                ? string.Equals(code, format.Code, StringComparison.OrdinalIgnoreCase)
                : string.Equals(code, format.Code, StringComparison.Ordinal);
            if (same) return id;
        }
        return format.IsGeneral ? 0 : null;
    }
}
