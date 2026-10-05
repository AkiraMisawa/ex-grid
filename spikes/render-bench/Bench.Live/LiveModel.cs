using System.Globalization;

namespace Bench.Live;

/// <summary>A row of a live screen: immutable, so a change is a new instance with the same
/// <see cref="Id"/> (ADR-0003). The Id stands for the Record Key.</summary>
public sealed class LiveRecord(int id, string book, decimal[] values)
{
    public int Id { get; } = id;

    public string Book { get; } = book;

    /// <summary>Never written after construction.</summary>
    public decimal[] Values { get; } = values;
}

/// <summary>The painted columns: how many, and the interned style string of each, as ExGrid's
/// <c>ColumnStyles</c> hands its rows one string per column.</summary>
public sealed class LiveColumns
{
    public LiveColumns(int count)
    {
        Count = count;
        KeyStyle = "left:0px;width:90px";
        Styles = new string[count];
        for (var c = 0; c < count; c++)
            Styles[c] = string.Create(CultureInfo.InvariantCulture, $"left:{90 + c * 90}px;width:90px");
    }

    public int Count { get; }

    public string KeyStyle { get; }

    public string[] Styles { get; }
}

/// <summary>What the rows of one grid did, counted by the rows themselves.</summary>
public sealed class LiveCounters
{
    public int Mounts;
    public int Renders;
    public int Disposals;

    public (int Mounts, int Renders, int Disposals) Take()
    {
        var taken = (Mounts, Renders, Disposals);
        Mounts = Renders = Disposals = 0;
        return taken;
    }
}

/// <summary>
/// The painted rows of one grid and the ticks that change them. A tick replaces k of the rows by
/// new instances with the same Id, each with <c>changedCells</c> of its painted values moved (or
/// all of them), and hands over a new Window array, as a Consumer pushes a new Window. Two tickers
/// made with the same seed make the same ticks, so the instance-keyed and the key-keyed grid see
/// the same data.
/// </summary>
public sealed class LiveTicker
{
    public const int MaxColumns = 50;

    private readonly Random _random;

    public LiveTicker(int rows, int seed)
    {
        _random = new Random(seed);
        var window = new LiveRecord[rows];
        for (var i = 0; i < rows; i++)
        {
            var values = new decimal[MaxColumns];
            for (var c = 0; c < MaxColumns; c++)
                values[c] = NextValue();
            window[i] = new LiveRecord(i, "BK" + (i % 120).ToString("000", CultureInfo.InvariantCulture), values);
        }
        Window = window;
    }

    public LiveRecord[] Window { get; private set; }

    /// <summary>Replaces <paramref name="k"/> distinct rows. <paramref name="changedCells"/> of
    /// each one's first <paramref name="paintedColumns"/> values move; a negative count moves all of
    /// them.</summary>
    public void Tick(int k, int changedCells, int paintedColumns)
    {
        var window = (LiveRecord[])Window.Clone();
        var positions = new HashSet<int>();
        while (positions.Count < Math.Min(k, window.Length))
            positions.Add(_random.Next(window.Length));
        foreach (var at in positions)
        {
            var old = window[at];
            var values = (decimal[])old.Values.Clone();
            if (changedCells < 0 || changedCells >= paintedColumns)
            {
                for (var c = 0; c < paintedColumns; c++)
                    values[c] = NextValue();
            }
            else
            {
                var cells = new HashSet<int>();
                while (cells.Count < changedCells)
                    cells.Add(_random.Next(paintedColumns));
                foreach (var c in cells)
                    values[c] = NextValue();
            }
            window[at] = new LiveRecord(old.Id, old.Book, values);
        }
        Window = window;
    }

    // Two decimals, either sign, so a change can also flip a cell's tone class.
    private decimal NextValue() => Math.Round((decimal)((_random.NextDouble() - 0.5) * 2_000_000), 2);
}

/// <summary>Interned per-cell strings, as ExGrid's CellClasses interns its classes.</summary>
public static class LiveText
{
    public const string Positive = "c num";
    public const string Negative = "c num neg";

    public static string Class(decimal value) => value < 0 ? Negative : Positive;

    public static string Of(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);
}
