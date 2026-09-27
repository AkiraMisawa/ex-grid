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
    /// them, the source cells left as they are (1, 2 → 3, 4; 1, 3, 4 → 5.67, 7.17, 8.67).</item>
    /// <item>A single date (a number shown in a date format with no time of day) goes on by one day
    /// per cell.</item>
    /// <item>A single number, and text, booleans, Error Values and blanks, are copied, repeating
    /// the source.</item>
    /// </list>
    /// Anything else is refused, never filled with copies: text Excel would continue (holding a
    /// digit, like <c>Item 1</c>, or a day or month name), two or more dates (Excel may step them
    /// by month or year), a time of day, and numbers mixed with anything else.
    /// Formats and alignment are repeated from the source.
    /// </summary>
    internal (SheetRefusal? Refusal, List<(CellAddress Address, CellState State)> States) PlanFill(CellRange source, CellRange target, FillDirection direction)
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
            var cells = Enumerable.Range(0, length).Select(i => StateOf(SourceAt(i)).Recorded).ToArray();
            var rule = FillRule(cells, SourceAt, out var refusal);
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
    private Func<int, int, CellAddress, Entry?> FillRule(CellState[] cells, Func<int, CellAddress> sourceAt, out SheetRefusal? refusal)
    {
        refusal = null;
        Entry? Copy(int x, int i, CellAddress at)
        {
            var from = sourceAt(i);
            return cells[i].Entry is { } entry ? ReferenceShift.Shift(entry, at.Row - from.Row, at.Column - from.Column) : null;
        }

        var numbers = 0;
        var dates = 0;
        for (var i = 0; i < cells.Length; i++)
        {
            var entry = cells[i].Entry;
            if (entry?.Constant is not { } constant) continue;
            if (constant.Kind == ValueKind.Number)
            {
                numbers++;
                if (cells[i].Format.IsDate)
                {
                    if (!cells[i].Format.IsDateOnly || cells.Length > 1)
                    {
                        refusal = Refuse(sourceAt(i), cells[i].Format.IsDateOnly
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
        return (x, _, _) => Series(meanY + slope * (x - meanX));

        static Entry Series(double number) =>
            Entry.FromValue(double.IsFinite(number) ? Value.FromNumber(number) : Value.FromError(ErrorValue.Num));
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
