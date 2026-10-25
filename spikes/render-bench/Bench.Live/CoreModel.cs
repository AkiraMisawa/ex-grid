using System.Globalization;

namespace Bench.Live;

/// <summary>
/// The data of the core comparison (/core, ADR-0350 section 15). Every host of the comparison — the
/// C# core's two Blazor hosts today, the Script Core's React and Blazor hosts later — makes the same
/// rows from the same formula, so that a value painted on one host is the value painted on the
/// others, and the driver (core-bench.js) can say what each cell must read without asking the page.
/// The formula is the contract: a TypeScript host writes it out again, not a port of this class.
/// </summary>
public static class CoreData
{
    /// <summary>The numeric columns after the Book column.</summary>
    public const int Measures = 20;

    /// <summary>The value of measure <paramref name="column"/> of row <paramref name="id"/> at
    /// generation <paramref name="gen"/>: a whole number of cents between -10,000.00 and 9,999.99.</summary>
    public static decimal Value(int id, int column, int gen)
        => ((id * 7919L + column * 104729L + gen * 15485863L) % 2_000_000L - 1_000_000L) / 100m;

    /// <summary>What the Book column reads: the row's generation and Id, so the driver can tell a
    /// row repainted at this generation from one still showing the last.</summary>
    public static string Book(int id, int gen) => string.Create(CultureInfo.InvariantCulture, $"G{gen}-{id}");

    public static CoreRow Row(int id, int gen)
    {
        var values = new decimal[Measures];
        for (var c = 0; c < Measures; c++)
            values[c] = Value(id, c, gen);
        return new CoreRow(id, gen, values);
    }

    /// <summary>A Window of <paramref name="size"/> rows, every one new and at <paramref name="gen"/>.</summary>
    public static CoreRow[] Window(int size, int gen)
    {
        var rows = new CoreRow[size];
        for (var i = 0; i < size; i++)
            rows[i] = Row(i, gen);
        return rows;
    }

    /// <summary>A new Window instance in which rows 0 to <paramref name="count"/> − 1 are new rows at
    /// <paramref name="gen"/> and every other row is the instance it was.</summary>
    public static CoreRow[] Replace(CoreRow[] window, int count, int gen)
    {
        var next = (CoreRow[])window.Clone();
        for (var i = 0; i < count && i < next.Length; i++)
            next[i] = Row(i, gen);
        return next;
    }

    /// <summary>A measure painted as text, as both cores format it: two decimals, grouped, invariant.</summary>
    public static string Format(object value) => ((decimal)value).ToString("N2", CultureInfo.InvariantCulture);
}

/// <summary>A row of the core comparison: immutable, so a change is a new instance (ADR-0003).</summary>
public sealed class CoreRow(int id, int gen, decimal[] values)
{
    public int Id { get; } = id;

    public int Gen { get; } = gen;

    public string Book { get; } = CoreData.Book(id, gen);

    /// <summary>Never written after construction.</summary>
    public decimal[] Values { get; } = values;
}
