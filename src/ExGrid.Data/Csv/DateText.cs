using System.Globalization;

namespace ExGrid.Data.Csv;

/// <summary>
/// One declared date format, read as .NET's <see cref="DateTime.TryParseExact(ReadOnlySpan{char}, ReadOnlySpan{char}, IFormatProvider?, DateTimeStyles, out DateTime)"/>
/// reads it. A format made only of digit fields and ASCII literals — <c>yyyy-MM-dd</c>,
/// <c>dd.MM.yyyy HH:mm:ss</c>, <c>yyyy/M/d</c> — is also compiled into a reader of the field's bytes,
/// which gives the same answers without decoding them; any other is read from the decoded text. A
/// format with an offset is read as a <see cref="DateTimeOffset"/>, and held as the clock it shows.
/// </summary>
internal sealed class DateFormat
{
    private readonly string format;
    private readonly CultureInfo culture;
    private readonly bool offset;
    private readonly Piece[]? pieces;

    public DateFormat(string format, CultureInfo culture)
    {
        this.format = format;
        this.culture = culture;
        offset = HasOffset(format);
        pieces = offset ? null : Compile(format, culture);
    }

    public string Format => format;

    /// <summary>Whether this format reads the field's bytes itself; otherwise it needs the decoded text.</summary>
    public bool ReadsBytes => pieces is not null;

    /// <summary>Reads the field's bytes under a compiled format.</summary>
    public bool TryRead(ReadOnlySpan<byte> s, out long ticks)
    {
        ticks = 0;
        int year = 1, month = 1, day = 1, hour = 0, minute = 0, second = 0;
        long fraction = 0;
        var i = 0;
        foreach (var piece in pieces!)
        {
            if (piece.Field == Field.Literal)
            {
                if (i >= s.Length || s[i] != piece.Literal)
                    return false;
                i++;
                continue;
            }
            var width = piece.Width;
            if (piece.Flexible)
            {
                // One digit, or two: as .NET reads M, d, H, m and s.
                if (i + 1 < s.Length && (uint)(s[i + 1] - (byte)'0') <= 9)
                    width = 2;
                else
                    width = 1;
            }
            if (i + width > s.Length)
                return false;
            var value = 0;
            for (var k = 0; k < width; k++)
            {
                var digit = (uint)(s[i + k] - (byte)'0');
                if (digit > 9)
                    return false;
                value = (value * 10) + (int)digit;
            }
            i += width;
            switch (piece.Field)
            {
                case Field.Year: year = value; break;
                case Field.Month: month = value; break;
                case Field.Day: day = value; break;
                case Field.Hour: hour = value; break;
                case Field.Minute: minute = value; break;
                case Field.Second: second = value; break;
                case Field.Fraction: fraction = value * Scale[width]; break;
            }
        }
        if (i != s.Length
            || year is < 1 or > 9999
            || month is < 1 or > 12
            || day < 1 || day > DateTime.DaysInMonth(year, month)
            || hour > 23 || minute > 59 || second > 59)
        {
            return false;
        }
        ticks = (new DateOnly(year, month, day).DayNumber * TimeSpan.TicksPerDay)
            + (hour * TimeSpan.TicksPerHour) + (minute * TimeSpan.TicksPerMinute) + (second * TimeSpan.TicksPerSecond) + fraction;
        return true;
    }

    /// <summary>
    /// Reads decoded text; a value with an offset is held as the clock it shows. A format without a
    /// date reads a time on the first day, never on the day it happens to be read. A format with
    /// <c>Z</c> or <c>GMT</c>, which .NET reads as UTC and would turn into the clock of the machine
    /// reading it, is held as the clock it shows too: adjusting a UTC value to UTC moves it by nothing.
    /// </summary>
    public bool TryRead(ReadOnlySpan<char> s, out long ticks)
    {
        if (offset)
        {
            if (DateTimeOffset.TryParseExact(s, format, culture, DateTimeStyles.AssumeUniversal, out var clock))
            {
                ticks = clock.Ticks;
                return true;
            }
        }
        else if (DateTime.TryParseExact(s, format, culture, DateTimeStyles.NoCurrentDateDefault | DateTimeStyles.AdjustToUniversal, out var date))
        {
            ticks = date.Ticks;
            return true;
        }
        ticks = 0;
        return false;
    }

    /// <summary>
    /// Why a date read under <paramref name="format"/> would take part of its value from the day it is
    /// read — .NET gives a month or a day without a year the current year, and an offset without a
    /// date the current date — or <see langword="null"/> when nothing is taken from it. It is told by
    /// what the format writes, so standard formats, quoted text and escapes are seen as .NET sees them.
    /// </summary>
    public static string? TakenFromToday(string format, CultureInfo culture)
    {
        var shown = Write(2001, 2, 3);
        if (shown != Write(2002, 2, 3))
            return null;
        if (shown != Write(2001, 3, 4))
            return "it has a month or a day but no year, so a date read under it would take the year it is read in";
        return HasOffset(format)
            ? "it has an offset but no date, so a date read under it would take the day it is read on"
            : null;

        string Write(int year, int month, int day) => new DateTime(year, month, day, 4, 5, 6, 7).ToString(format, culture);
    }

    /// <summary>Whether .NET can read <paramref name="format"/> at all; it throws for one it cannot.</summary>
    public static bool IsValid(string format, CultureInfo culture, out string? why)
    {
        try
        {
            _ = new DateTime(2026, 9, 30, 12, 34, 56).ToString(format, culture);
            why = null;
            return true;
        }
        catch (FormatException e)
        {
            why = e.Message;
            return false;
        }
    }

    private static readonly long[] Scale = [0, 1_000_000, 100_000, 10_000, 1_000, 100, 10, 1];

    private static bool HasOffset(string format)
    {
        var quoted = '\0';
        for (var i = 0; i < format.Length; i++)
        {
            var c = format[i];
            if (quoted != '\0')
            {
                if (c == quoted)
                    quoted = '\0';
                continue;
            }
            if (c is '\'' or '"')
                quoted = c;
            else if (c == '\\')
                i++;
            else if (c is 'z' or 'K')
                return true;
        }
        return false;
    }

    /// <summary>Compiles a format of digit fields and single ASCII literals; <see langword="null"/> for
    /// any other, which is then read from the decoded text. Only a culture that writes dates in the
    /// Gregorian calendar is compiled: Thai writes 2026 as 2569, which .NET reads in its own calendar.</summary>
    private static Piece[]? Compile(string format, CultureInfo culture)
    {
        if (culture.DateTimeFormat.Calendar is not GregorianCalendar)
            return null;
        if (format.Length < 2)
            return null; // a one-letter format is a standard format, not a custom one
        var pieces = new List<Piece>();
        var seen = new HashSet<Field>();
        for (var i = 0; i < format.Length;)
        {
            var c = format[i];
            var run = 1;
            while (i + run < format.Length && format[i + run] == c)
                run++;
            Piece? piece = (c, run) switch
            {
                ('y', 4) => new Piece(Field.Year, 4, false),
                ('M', 2) => new Piece(Field.Month, 2, false),
                ('M', 1) => new Piece(Field.Month, 2, true),
                ('d', 2) => new Piece(Field.Day, 2, false),
                ('d', 1) => new Piece(Field.Day, 2, true),
                ('H', 2) => new Piece(Field.Hour, 2, false),
                ('H', 1) => new Piece(Field.Hour, 2, true),
                ('m', 2) => new Piece(Field.Minute, 2, false),
                ('m', 1) => new Piece(Field.Minute, 2, true),
                ('s', 2) => new Piece(Field.Second, 2, false),
                ('s', 1) => new Piece(Field.Second, 2, true),
                ('f', <= 7) => new Piece(Field.Fraction, run, false),
                _ => null,
            };
            if (piece is { } digits)
            {
                // Two flexible fields side by side cannot be told apart by width; leave them to .NET.
                if (!seen.Add(digits.Field) || (digits.Flexible && pieces.Count > 0 && pieces[^1].Field != Field.Literal))
                    return null;
                if (pieces.Count > 0 && pieces[^1].Flexible && pieces[^1].Field != Field.Literal)
                    return null;
                pieces.Add(digits);
                i += run;
                continue;
            }
            if (char.IsAsciiLetter(c) || c is '%' or '\'' or '"' or '\\')
                return null;
            var literal = c switch
            {
                '/' => culture.DateTimeFormat.DateSeparator,
                ':' => culture.DateTimeFormat.TimeSeparator,
                _ => c.ToString(),
            };
            if (literal.Length != 1 || !char.IsAscii(literal[0]))
                return null;
            pieces.Add(new Piece(Field.Literal, 0, false, (byte)literal[0]));
            i++;
        }
        // A format a compiled reader takes must be a whole date: a year, a month and a day.
        return seen.Contains(Field.Year) && seen.Contains(Field.Month) && seen.Contains(Field.Day) ? [.. pieces] : null;
    }

    private enum Field
    {
        Literal,
        Year,
        Month,
        Day,
        Hour,
        Minute,
        Second,
        Fraction,
    }

    private readonly record struct Piece(Field Field, int Width, bool Flexible, byte Literal = 0);
}
