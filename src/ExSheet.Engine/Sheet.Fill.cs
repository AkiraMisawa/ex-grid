using System.Globalization;
using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

/// <summary>Which way a fill extends its source (ADR-0050): along one axis, away from the source.</summary>
public enum FillDirection
{
    /// <summary>Into the rows below the source.</summary>
    Down,

    /// <summary>Into the columns to the right of the source.</summary>
    Right,

    /// <summary>Into the rows above the source.</summary>
    Up,

    /// <summary>Into the columns to the left of the source.</summary>
    Left,
}

public sealed partial class Sheet
{
    /// <summary>
    /// What a Fill Intent writes, as Excel fills (ADR-0050, item 5), or why it is refused. Each
    /// line along the direction — a column for a vertical fill, a row for a horizontal one — is
    /// filled from its own source cells:
    /// <list type="bullet">
    /// <item>Formulas are copied with their relative References shifted, repeating the source.</item>
    /// <item>Two or more numbers continue as Excel's linear trend: the least-squares line through
    /// them, the source cells left as they are (1, 2 → 3, 4; 1, 3, 4 → 5.67, 7.17, 8.67), held at
    /// 15 significant digits as Excel holds them.</item>
    /// <item>A single date (a number shown in a date format with no time of day) goes on by one day
    /// per cell.</item>
    /// <item>A single number, and text, booleans, Error Values and blanks, are copied, repeating
    /// the source.</item>
    /// </list>
    /// Anything else is refused, never filled with copies: text Excel would continue (holding a
    /// digit, like <c>Item 1</c>, or a day or month name), two or more dates (Excel may step them
    /// by month or year), a time of day, and numbers mixed with anything else.
    /// Cell Formats are repeated from the source. With <paramref name="copyOnly"/>, Excel's
    /// fill keys (Ctrl+D, Ctrl+R): every line is a copy, and no pattern is continued or refused.
    /// </summary>
    internal (SheetRefusal? Refusal, List<(CellAddress Address, CellState State)> States) PlanFill(CellRange source, CellRange target, FillDirection direction, bool copyOnly = false)
    {
        var vertical = direction is FillDirection.Down or FillDirection.Up;
        var forward = direction is FillDirection.Down or FillDirection.Right;
        var states = new List<(CellAddress, CellState)>();

        // The target may be given as the extension alone, or as the whole extended range.
        if (target.Contains(source.First) && target.Contains(source.Last) && target != source)
        {
            target = forward
                ? new CellRange(vertical ? new CellAddress(source.Last.Row + 1, target.First.Column) : new CellAddress(target.First.Row, source.Last.Column + 1), target.Last)
                : new CellRange(target.First, vertical ? new CellAddress(source.First.Row - 1, target.Last.Column) : new CellAddress(target.Last.Row, source.First.Column - 1));
        }
        var adjacent = vertical
            ? target.First.Column == source.First.Column && target.Last.Column == source.Last.Column
              && (forward ? target.First.Row == source.Last.Row + 1 : target.Last.Row == source.First.Row - 1)
            : target.First.Row == source.First.Row && target.Last.Row == source.Last.Row
              && (forward ? target.First.Column == source.Last.Column + 1 : target.Last.Column == source.First.Column - 1);
        if (!adjacent)
        {
            return (new SheetRefusal(SheetRefusalReason.FillShapeNotSupported,
                $"A fill {direction.ToString().ToLowerInvariant()} from {source} extends it along one axis; {target} does not."), states);
        }

        var lines = vertical ? source.ColumnCount : source.RowCount;
        var length = vertical ? source.RowCount : source.ColumnCount;
        var count = vertical ? target.RowCount : target.ColumnCount;
        for (var line = 0; line < lines; line++)
        {
            CellAddress SourceAt(int i) => vertical
                ? new CellAddress(source.First.Row + i, source.First.Column + line)
                : new CellAddress(source.First.Row + line, source.First.Column + i);

            // Position along the axis relative to the source's first cell: 0..length-1 is the
            // source; the target is length.. going forward and ..-1 going back.
            var cells = Enumerable.Range(0, length).Select(i => CarriedState(SourceAt(i))).ToArray();
            var rule = FillRule(cells, SourceAt, copyOnly, out var refusal);
            if (refusal is not null) return (refusal, []);
            for (var k = 0; k < count; k++)
            {
                var x = forward ? length + k : -1 - k;
                var at = vertical
                    ? new CellAddress(source.First.Row + x, source.First.Column + line)
                    : new CellAddress(source.First.Row + line, source.First.Column + x);
                var i = ((x % length) + length) % length;
                var state = cells[i];
                states.Add((at, state with { Entry = rule(x, i, at) }));
            }
        }
        return (null, states);
    }

    /// <summary>The rule for one line: given a position along the axis, the source cell it repeats and its address, the Entry written there.</summary>
    private Func<int, int, CellAddress, Entry?> FillRule(CellState[] cells, Func<int, CellAddress> sourceAt, bool copyOnly, out SheetRefusal? refusal)
    {
        refusal = null;
        Entry? Copy(int x, int i, CellAddress at)
        {
            var from = sourceAt(i);
            return cells[i].Entry is { } entry ? ReferenceShift.Shift(entry, at.Row - from.Row, at.Column - from.Column) : null;
        }
        if (copyOnly) return Copy;

        var numbers = 0;
        var dates = 0;
        for (var i = 0; i < cells.Length; i++)
        {
            var entry = cells[i].Entry;
            if (entry?.Constant is not { } constant) continue;
            if (constant.Kind == ValueKind.Number)
            {
                numbers++;
                var format = cells[i].NumberFormat!;
                if (format.IsDate)
                {
                    if (!format.IsDateOnly || cells.Length > 1)
                    {
                        refusal = Refuse(sourceAt(i), format.IsDateOnly
                            ? "two or more dates, which Excel may step by month or year"
                            : "a time of day, which Excel steps by the hour");
                        return Copy;
                    }
                    dates++;
                }
            }
            else if (constant.Kind == ValueKind.Text && ContinuesAsSeries(constant.Text))
            {
                refusal = Refuse(sourceAt(i), $"'{constant.Text}', text Excel would continue as a series");
                return Copy;
            }
        }

        if (numbers == 0) return Copy;
        if (cells.Length == 1)
        {
            if (dates == 0) return Copy;
            var serial = cells[0].Entry!.Constant!.Value.Number;
            return (x, _, _) => Series(serial + x);
        }
        if (numbers != cells.Length)
        {
            refusal = Refuse(sourceAt(Array.FindIndex(cells, c => c.Entry?.Constant is { Kind: ValueKind.Number })), "numbers mixed with other cells, which Excel continues in ways ExSheet does not yet");
            return Copy;
        }

        // Excel's linear trend: the least-squares line through (0, y0) .. (n-1, yn-1).
        var ys = cells.Select(c => c.Entry!.Constant!.Value.Number).ToArray();
        var meanX = (ys.Length - 1) / 2.0;
        var meanY = ys.Average();
        double sxy = 0, sxx = 0;
        for (var i = 0; i < ys.Length; i++)
        {
            sxy += (i - meanX) * (ys[i] - meanY);
            sxx += (i - meanX) * (i - meanX);
        }
        var slope = sxy / sxx;
        // Excel stores a trend at 15 significant digits (observed, verification/2026-09-27-windows-excel
        // item 4): the step and the cell next to the source are the line's, each rounded so, and
        // every further cell is that cell plus the step, counted exactly and rounded so. 1, 2, 4
        // gives 5.33333333333333, 6.83333333333333, ...; 0.1, 0.2, 0.4 gives ..., 0.983333333333333,
        // where rounding the line's own double at each cell would give 0.983333333333334.
        var step = TrendDigits.Round(slope);
        var ahead = TrendDigits.Round(meanY + slope * (ys.Length - meanX));
        var behind = TrendDigits.Round(meanY + slope * (-1 - meanX));
        return (x, _, _) => Series(x >= 0 ? TrendDigits.Step(ahead, step, x - ys.Length) : TrendDigits.Step(behind, -step, -1 - x));

        static Entry Series(double number) =>
            Entry.FromValue(double.IsFinite(number) ? Value.FromNumber(number) : Value.FromError(ErrorValue.Num));
    }

    /// <summary>Excel's 15 significant digits, for a fill's linear trend (ADR-0050, ticket 15).</summary>
    internal static class TrendDigits
    {
        private const int Digits = 15;

        /// <summary><paramref name="number"/> rounded to 15 significant digits, as the nearest double.</summary>
        public static double Round(double number) =>
            double.IsFinite(number) && number != 0
                ? double.Parse(number.ToString("E" + (Digits - 1).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
                : number;

        /// <summary>
        /// <paramref name="first"/> plus <paramref name="count"/> steps, rounded to 15 significant
        /// digits. Both are already at 15 digits, so the sum is counted exactly in decimal where
        /// decimal holds it; outside decimal's range it is counted in doubles.
        /// </summary>
        public static double Step(double first, double step, int count)
        {
            if (count == 0) return first;
            if (Exact(first) is { } a && Exact(step) is { } s)
            {
                var sum = a + count * s;
                // Through text, which double.Parse rounds correctly to the nearest double.
                if (Inside(sum)) return double.Parse(RoundDecimal(sum).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            }
            return Round(first + count * step);
        }

        private static decimal? Exact(double number)
        {
            if (!Inside(number)) return null;
            return number == 0 ? 0m : decimal.Parse(number.ToString("E" + (Digits - 1).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        // Decimal keeps 15 significant digits from 1e-13 (its 28 places) up to 1e15 (a whole number of 15 digits).
        private static bool Inside(double number) => number == 0 || (Math.Abs(number) >= 1e-13 && Math.Abs(number) < 1e15);

        private static bool Inside(decimal number) => number == 0 || (Math.Abs(number) >= 1e-13m && Math.Abs(number) < 1e15m);

        private static decimal RoundDecimal(decimal number)
        {
            if (number == 0) return 0;
            var magnitude = 0;
            for (var a = Math.Abs(number); a >= 10; a /= 10) magnitude++;
            for (var a = Math.Abs(number); a < 1; a *= 10) magnitude--;
            return Math.Round(number, Digits - 1 - magnitude, MidpointRounding.AwayFromZero);
        }
    }

    private static SheetRefusal Refuse(CellAddress at, string what) =>
        new(SheetRefusalReason.FillPatternNotSupported, $"{at} holds {what}; ExSheet refuses the fill rather than fill it with copies.");

    /// <summary>
    /// Text Excel's fill would not simply copy: text holding a digit (Excel steps the number in
    /// it) and the day and month names of its built-in lists, in English and in the Sheet's culture.
    /// </summary>
    private bool ContinuesAsSeries(string text)
    {
        if (text.Any(char.IsDigit)) return true;
        var trimmed = text.Trim();
        return ListNames.Value.Contains(trimmed) || (_cultureListNames ??= CultureListNames(Culture)).Contains(trimmed);
    }

    private HashSet<string>? _cultureListNames;

    private static readonly Lazy<HashSet<string>> ListNames = new(() => CultureListNames(CultureInfo.InvariantCulture));

    private static HashSet<string> CultureListNames(CultureInfo culture)
    {
        var format = culture.DateTimeFormat;
        return new HashSet<string>(
            format.DayNames.Concat(format.AbbreviatedDayNames).Concat(format.MonthNames).Concat(format.AbbreviatedMonthNames).Where(n => n.Length > 0),
            StringComparer.Create(culture, ignoreCase: true));
    }
}
