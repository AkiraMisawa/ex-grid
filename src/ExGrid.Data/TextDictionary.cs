using System.Collections;
using ExGrid.Data.Storage;

namespace ExGrid.Data;

/// <summary>
/// A Text column's dictionary as one version sees it: every distinct value once, exactly as written,
/// at its code (ADR-0064). Values are told apart ordinally, so two spellings are two entries; a
/// reader that tells text apart ignoring case folds the dictionary, not the rows.
/// <para>
/// Codes read from a later version may lie past <see cref="Count"/>; read them through that
/// version's column.
/// </para>
/// <para>
/// A dictionary read from a UTF-8 CSV is looked up by text only once asked: the first
/// <see cref="TryGetCode(string, out int)"/>, or the first Change Batch, indexes its entries, once
/// for every version that shares them.
/// </para>
/// </summary>
public sealed class TextDictionary : IReadOnlyList<string>
{
    internal TextDictionary(TextStore store, int count)
    {
        Store = store;
        Count = count;
    }

    /// <summary>The number of entries this version holds.</summary>
    public int Count { get; }

    internal TextStore Store { get; }

    /// <summary>The text at <paramref name="code"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The code is not one of this version's.</exception>
    public string this[int code]
    {
        get
        {
            if ((uint)code >= (uint)Count)
                throw new ArgumentOutOfRangeException(nameof(code), code, $"The dictionary holds codes 0 to {Count - 1} in this version.");
            return Store.Text(code);
        }
    }

    /// <summary>The code of <paramref name="text"/>, matched ordinally; false when this version's
    /// dictionary does not hold it.</summary>
    public bool TryGetCode(string text, out int code)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Store.TryGetCode(text, Count, out code);
    }

    /// <summary>The code of <paramref name="text"/>, matched ordinally; false when this version's
    /// dictionary does not hold it.</summary>
    public bool TryGetCode(ReadOnlySpan<char> text, out int code) => Store.TryGetCode(text, Count, out code);

    /// <summary>The entries in code order.</summary>
    public IEnumerator<string> GetEnumerator()
    {
        for (var code = 0; code < Count; code++)
            yield return Store.Text(code);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
