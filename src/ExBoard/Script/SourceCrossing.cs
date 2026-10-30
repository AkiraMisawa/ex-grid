using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ExGrid.Data;
using ExPivot.Engine;

namespace ExBoard.Script;

/// <summary>
/// The sources held where the data lives (ADR-0343) that ExBoard's Script Core asks through .NET (ADR-0370 section 1):
/// each stays in .NET, and crosses as a number the browser asks it under, as a Snapshot crosses once and is named by
/// its number after (<c>SnapshotCrossing</c>). A source is named by its number again only while the message before
/// named it for the same input; one dropped and handed in again is numbered anew, so a number the browser let go is
/// never named again, in whichever order the browser's release and .NET's next message cross.
/// </summary>
/// <remarks>A source a pivot view reads makes a Pivot Source the browser's pivot asks through .NET
/// (<see cref="BoardSource.Pivot"/>, ADR-0370 section 3). The browser makes one each time the view's narrowing changes,
/// at once, so it is told the fields and what such a source answers with the number: they are read from the one the
/// source makes over every row, which answers the first pivot the browser makes over every row.</remarks>
internal sealed class SourceCrossing
{
    private readonly Dictionary<int, Held> _held = [];
    private Dictionary<Held, int> _named = new(HeldKey.Comparer);
    private int _last;
    // What the browser declares each source's Pivot Sources by, and the one made over every row to read it from, until
    // the browser's pivot over every row takes it.
    private readonly Dictionary<int, string> _pivots = [];
    private readonly Dictionary<int, PivotSource> _unclaimed = [];

    /// <summary>
    /// Writes <c>"sources"</c>, each input's source by its number and what it answers, numbering for the next message
    /// each source the message before named for that input, under the same declaration of it, by its number, and every
    /// other anew: a source is asked, and what it is asked and answers is read, as the Board that numbered it declares
    /// the input, whichever Board .NET holds by the time the question arrives (spine rule 6). An input
    /// <paramref name="board"/> does not declare is not read.
    /// </summary>
    public void Write(Utf8JsonWriter json, IReadOnlyDictionary<string, BoardSource>? sources, BoardReading board)
    {
        var named = new Dictionary<Held, int>(HeldKey.Comparer);
        json.WriteStartObject("sources");
        foreach (var (input, source) in sources ?? (IReadOnlyDictionary<string, BoardSource>)ImmutableDictionary<string, BoardSource>.Empty)
        {
            if (board.Input(input) is not { } declared)
                continue;
            var held = new Held(input, source, declared.Shape);
            if (!_named.TryGetValue(held, out var number))
            {
                number = ++_last;
                _held[number] = held;
            }
            // Declared once per number, as a Board that pivots the input first names it: a later Board sharing the
            // declaration may add the pivot.
            if (!_pivots.ContainsKey(number) && source.Pivot is { } pivot && board.Views.OfType<BoardPivotView>().Any(view => view.Input == input))
            {
                var whole = pivot([]) ?? throw new InvalidOperationException($"The source of input '{input}' made no Pivot Source.");
                _pivots[number] = Members(whole);
                _unclaimed[number] = whole;
            }
            named[held] = number;
            json.WriteStartObject(input);
            json.WriteNumber("number", number);
            json.WriteBoolean("thin", source.Thin is not null);
            json.WriteBoolean("under", source.Under is not null);
            json.WritePropertyName("pivot");
            if (_pivots.TryGetValue(number, out var members))
                json.WriteRawValue(members);
            else
                json.WriteNullValue();
            json.WriteEndObject();
        }
        json.WriteEndObject();
        _named = named;
    }

    /// <summary>The source the browser asks by <paramref name="number"/>, the input it is bound to and the Board's
    /// declaration of that input when it was numbered. The browser lets go of a number only once no message it applied
    /// names it, after every question and event it sent under it, so one .NET no longer holds is the bridge's defect.</summary>
    public Held this[int number] => _held.TryGetValue(number, out var held)
        ? held
        : throw new InvalidOperationException($"Internal: the browser names source {number}, which .NET never sent or the browser let go.");

    /// <summary>A Pivot Source over the rows of source <paramref name="number"/> that <paramref name="narrowing"/> lets
    /// through: the one made as the source was numbered, for the first over every row, and one the source makes for
    /// every other.</summary>
    public PivotSource PivotOver(int number, IReadOnlyList<LinkCondition> narrowing)
    {
        var held = this[number];
        if (!_pivots.ContainsKey(number))
            throw new InvalidOperationException($"Internal: the browser made a pivot over source {number}, which .NET declared none for.");
        // The one over every row is the first pivot's, or nobody's: never held past the first pivot made.
        if (_unclaimed.Remove(number, out var whole) && narrowing.Count == 0)
            return whole;
        var pivot = held.Source.Pivot ?? throw new InvalidOperationException($"Internal: source {number} makes no Pivot Source.");
        var made = pivot(narrowing) ?? throw new InvalidOperationException($"The source of input '{held.Input}' made no Pivot Source.");
        // The browser's pivot reads the one declaration: a source made with other fields or answers is refused by name.
        if (Members(made) != _pivots[number])
        {
            throw new InvalidOperationException(
                $"The source of input '{held.Input}' made a Pivot Source over a narrowing whose fields or answers are not those of the one over every row, which the pivot in the browser was told of.");
        }
        return made;
    }

    /// <summary>The browser let go of <paramref name="numbers"/>: no message names them again.</summary>
    public void Release(IEnumerable<int> numbers)
    {
        foreach (var number in numbers)
        {
            if (_named.ContainsValue(number))
                throw new InvalidOperationException($"Internal: the browser let go of source {number}, which the last message names.");
            _held.Remove(number);
            _pivots.Remove(number);
            _unclaimed.Remove(number);
        }
    }

    // A Pivot Source's fields and what it answers, as the browser declares the ones it makes over the source.
    private static string Members(PivotSource pivot)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            global::ExGrid.Script.PivotSourceCrossing.WriteMembers(json, pivot);
            json.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>A source as it is held under a number: the input it is bound to, the source, and the Board's
    /// declaration of the input it was numbered under.</summary>
    internal sealed record Held(string Input, BoardSource Source, DataShape Declared);

    // A source held for an input under a declaration: the input by its name, the source and the declaration by identity.
    private sealed class HeldKey : IEqualityComparer<Held>
    {
        public static readonly HeldKey Comparer = new();

        public bool Equals(Held? x, Held? y)
            => x is not null && y is not null && string.Equals(x.Input, y.Input, StringComparison.Ordinal)
                && ReferenceEquals(x.Source, y.Source) && ReferenceEquals(x.Declared, y.Declared);

        public int GetHashCode(Held key)
            => HashCode.Combine(StringComparer.Ordinal.GetHashCode(key.Input), RuntimeHelpers.GetHashCode(key.Source), RuntimeHelpers.GetHashCode(key.Declared));
    }
}
