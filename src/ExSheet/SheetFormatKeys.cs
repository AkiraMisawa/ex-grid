using System.Globalization;
using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// Excel's formatting keys, as ExSheet declares them to its grid (ADR-0071, "Keys"; ADR-0050 item
/// 14) and what each applies. Only the keys Excel has, each only as Excel has it, as the eleventh
/// Windows run found them (<c>verification/2026-10-01-windows-excel-11/cell-format.md</c>, cases
/// 16 to 20).
/// </summary>
internal static class SheetFormatKeys
{
    /// <summary>What a formatting key does.</summary>
    internal enum Kind
    {
        Bold,
        Italic,
        Underline,
        Strikethrough,
        General,
        Number,
        Time,
        Date,
        Currency,
        Percent,
        Scientific,
        Outline,
        NoBorders,
        FormatCells,
    }

    // Each key is the character it types with Ctrl, as Excel reads it — by the character, not by
    // where the key lies (case 19) — so a layout that types `#` without Shift (UK) reaches the date
    // format as one that needs Shift does. A letter is claimed in both cases for CapsLock, and
    // without Shift: Ctrl+Shift+U is another key in Excel. Every other character is claimed with
    // Shift and without it, whichever its layout needs. Ctrl+1 opens Format Cells (ticket 52); it
    // sets nothing itself, so it has no change below.
    private static readonly (char Character, Kind Kind)[] Keys =
    [
        ('b', Kind.Bold), ('2', Kind.Bold),
        ('i', Kind.Italic), ('3', Kind.Italic),
        ('u', Kind.Underline), ('4', Kind.Underline),
        ('5', Kind.Strikethrough),
        ('~', Kind.General),
        ('!', Kind.Number),
        ('@', Kind.Time),
        ('#', Kind.Date),
        ('$', Kind.Currency),
        ('%', Kind.Percent),
        ('^', Kind.Scientific),
        ('&', Kind.Outline),
        ('_', Kind.NoBorders),
        ('1', Kind.FormatCells),
    ];

    private static readonly Dictionary<string, Kind> ByKey = Build();

    /// <summary>The keys ExSheet declares to its grid, in the grid's canonical form.</summary>
    internal static IReadOnlyCollection<string> Declared { get; } = [.. ByKey.Keys];

    /// <summary>What the declared key <paramref name="key"/> does; null for a key that is not one of these.</summary>
    internal static Kind? KindOf(string key) => ByKey.TryGetValue(key, out var kind) ? kind : null;

    private static readonly NumberFormat Number = NumberFormat.Parse("#,##0.00");
    private static readonly NumberFormat Time = NumberFormat.Parse("h:mm");
    private static readonly NumberFormat TwelveHourTime = NumberFormat.Parse("h:mm AM/PM");
    private static readonly NumberFormat Date = NumberFormat.Parse("d-mmm-yy");
    private static readonly NumberFormat Percent = NumberFormat.Parse("0%");
    private static readonly NumberFormat Scientific = NumberFormat.Parse("0.00E+00");

    /// <summary>
    /// What a key sets on the Selection, under the Sheet's <paramref name="culture"/> (cases 16 to
    /// 20). A toggle's direction is the Focus cell's (case 17): over a Focus that is bold, Ctrl+B
    /// takes bold off every cell, and otherwise puts it on every cell. The Number Formats are the
    /// ones Excel applied under en-GB, en-US and ja-JP alike, but for two: the time is 12-hour
    /// where the culture's own time is (<c>h:mm AM/PM</c> under en-US, <c>h:mm</c> under en-GB and
    /// ja-JP), and the currency is Excel's built-in, shown in the culture's own currency
    /// (<see cref="NumberFormat.BuiltInCurrency"/>). The date and the time are Excel's built-ins
    /// 15 and 20 (or the AM/PM one), recorded in their codes and shown in the culture's own form,
    /// as Excel shows them (the twelfth run, case 19): <c>05-Jan-26</c> and <c>09:05</c> under
    /// en-GB, <c>5-Jan-26</c> and <c>9:05 AM</c> under en-US, <c>05-1-26</c> and <c>9:05</c> under
    /// ja-JP. A Number Format widens the columns it no longer fits, as every Number Format set on
    /// the Selection does (case 17), and so does strikethrough (<see cref="WidensAsANumberFormatDoes"/>).
    /// The outline is thin and Automatic on each range's four edges; no borders clears every edge
    /// of each range.
    /// </summary>
    /// <param name="kind">The key.</param>
    /// <param name="focus">The Focus cell's Cell Format, as it shows.</param>
    /// <param name="culture">The Sheet's culture.</param>
    internal static CellFormatChange ChangeFor(Kind kind, CellFormat focus, CultureInfo culture) => kind switch
    {
        Kind.Bold => new() { Bold = !focus.Font.Bold },
        Kind.Italic => new() { Italic = !focus.Font.Italic },
        Kind.Underline => new() { Underline = !focus.Font.Underline },
        Kind.Strikethrough => new() { Strikethrough = !focus.Font.Strikethrough },
        Kind.General => new() { NumberFormat = NumberFormat.General },
        Kind.Number => new() { NumberFormat = Number },
        Kind.Time => new() { NumberFormat = culture.DateTimeFormat.ShortTimePattern.Contains('t', StringComparison.Ordinal) ? TwelveHourTime : Time },
        Kind.Date => new() { NumberFormat = Date },
        Kind.Currency => new() { NumberFormat = NumberFormat.BuiltInCurrency(culture) },
        Kind.Percent => new() { NumberFormat = Percent },
        Kind.Scientific => new() { NumberFormat = Scientific },
        Kind.Outline => new() { Borders = BorderChange.Outline(new BorderLine(BorderLineStyle.Thin)) },
        Kind.NoBorders => new() { Borders = BorderChange.None },
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a formatting key."),
    };

    /// <summary>
    /// Whether a key that sets no Number Format still widens the columns whose numbers no longer
    /// fit, exactly as a Number Format key does (ticket 58's rule): Ctrl+5, setting strikethrough or
    /// taking it off, as Excel's widened column A for <c>1234567.50</c> (the fourteenth Windows run,
    /// case 7). Ctrl+B and the Fill did not, in any of three orders, and Ctrl+I, Ctrl+U and the
    /// Border keys stay as Ctrl+B is until a run asks (ADR-0071).
    /// </summary>
    internal static bool WidensAsANumberFormatDoes(Kind kind) => kind == Kind.Strikethrough;

    private static Dictionary<string, Kind> Build()
    {
        var keys = new Dictionary<string, Kind>(StringComparer.Ordinal);
        foreach (var (character, kind) in Keys)
        {
            var typed = character.ToString();
            if (char.IsAsciiLetter(character))
            {
                keys["Control+" + typed] = kind;
                keys["Control+" + typed.ToUpperInvariant()] = kind;
            }
            else
            {
                keys["Control+" + typed] = kind;
                keys["Control+Shift+" + typed] = kind;
            }
        }
        return keys;
    }
}
