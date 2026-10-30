using System.Collections;

namespace ExChart.Engine;

/// <summary>
/// An immutable list equal to another with the same items in the same order, so a piece of the scene
/// that did not change compares equal and its component skips its render (ADR-0252).
/// </summary>
/// <typeparam name="T">The item type, compared by its own equality.</typeparam>
public sealed class EquatableList<T> : IReadOnlyList<T>, IEquatable<EquatableList<T>>
{
    private readonly T[] _items;

    /// <summary>A list of the given items.</summary>
    public EquatableList(IEnumerable<T> items) => _items = items.ToArray();

    /// <summary>The empty list.</summary>
    public static EquatableList<T> Empty { get; } = new([]);

    /// <inheritdoc />
    public T this[int index] => _items[index];

    /// <inheritdoc />
    public int Count => _items.Length;

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc />
    public bool Equals(EquatableList<T>? other) => other is not null && (ReferenceEquals(this, other) || _items.AsSpan().SequenceEqual(other._items));

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as EquatableList<T>);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var h = new HashCode();
        foreach (var item in _items) h.Add(item);
        return h.ToHashCode();
    }
}

/// <summary>Builds <see cref="EquatableList{T}"/>.</summary>
public static class EquatableList
{
    /// <summary>The items as an <see cref="EquatableList{T}"/>.</summary>
    public static EquatableList<T> ToEquatableList<T>(this IEnumerable<T> items) => new(items);
}
