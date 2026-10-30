using System.Text.Json;
using ExGrid.Data;
using ExGrid.Data.Arrow;
using Microsoft.JSInterop;

namespace ExGrid.Components;

/// <summary>
/// The Snapshots a Blazor Script adapter sends its module (ADR-0065, ADR-0370 section 1), .NET's half: each crosses
/// once, as the Arrow stream ADR-0065 has a Snapshot travel, under a number; a later message names it by that number
/// alone, and the browser says which numbers it has let go. Compiled into each adapter that sends Snapshots, so every
/// one crosses them alike; the browser's half is the Shared Script's <c>HeldSnapshots</c>
/// (<c>packages/blazor/src/snapshots.ts</c>).
/// </summary>
/// <remarks>
/// A Snapshot is named by its number again only while the message before named it: one dropped and named again later
/// is sent again, under a new number. So a number the browser let go is never named again, in whichever order the
/// browser's release and .NET's next message cross. Each number is kept until the browser lets it go, so an event the
/// browser raised from a Snapshot is read against the very Snapshot it was raised from.
/// </remarks>
internal sealed class SnapshotCrossing
{
    private readonly Dictionary<int, Snapshot> _held = [];
    private Dictionary<Snapshot, int> _named = new(ReferenceEqualityComparer.Instance);
    private int _last;

    /// <summary>Numbers <paramref name="data"/> for the next message, each Snapshot the message before named by its number,
    /// and every other under a new number, to be sent with it.</summary>
    public SnapshotNaming Name(IReadOnlyDictionary<string, Snapshot> data)
    {
        var named = new Dictionary<Snapshot, int>(ReferenceEqualityComparer.Instance);
        var numbers = new Dictionary<string, int>(StringComparer.Ordinal);
        var sent = new List<(int Number, Snapshot Snapshot)>();
        // Checked whole before any number is given, so a refusal leaves nothing numbered that never crosses.
        foreach (var (input, snapshot) in data)
        {
            if (snapshot is null)
                throw new ArgumentException($"Input '{input}' is handed no Snapshot.", nameof(data));
        }
        foreach (var (input, snapshot) in data)
        {
            if (!named.TryGetValue(snapshot, out var number))
            {
                if (!_named.TryGetValue(snapshot, out number))
                {
                    number = ++_last;
                    _held[number] = snapshot;
                    sent.Add((number, snapshot));
                }
                named[snapshot] = number;
            }
            numbers[input] = number;
        }
        _named = named;
        return new SnapshotNaming(numbers, sent);
    }

    /// <summary>The Snapshot the browser names by <paramref name="number"/>: one it holds, so .NET does.</summary>
    public Snapshot this[int number] => _held.TryGetValue(number, out var snapshot)
        ? snapshot
        : throw new InvalidOperationException($"Internal: the browser named Snapshot {number}, which .NET never sent or the browser let go.");

    /// <summary>The browser let go of <paramref name="numbers"/>: no message names them again.</summary>
    public void Release(IEnumerable<int> numbers)
    {
        foreach (var number in numbers)
        {
            if (_named.ContainsValue(number))
                throw new InvalidOperationException($"Internal: the browser let go of Snapshot {number}, which the last message names.");
            _held.Remove(number);
        }
    }
}

/// <summary>The Snapshots one message names, by input, and those it carries.</summary>
internal sealed class SnapshotNaming(IReadOnlyDictionary<string, int> numbers, IReadOnlyList<(int Number, Snapshot Snapshot)> sent)
{
    /// <summary>Each input's Snapshot, by its number.</summary>
    public IReadOnlyDictionary<string, int> Numbers { get; } = numbers;

    /// <summary>The Snapshots the message carries, which the browser does not hold yet.</summary>
    public IReadOnlyList<(int Number, Snapshot Snapshot)> Sent { get; } = sent;

    /// <summary>Writes <c>"data"</c>, each input's number, and <c>"sent"</c>, the numbers of the streams the message
    /// carries, in their order.</summary>
    public void Write(Utf8JsonWriter json)
    {
        json.WriteStartObject("data");
        foreach (var (input, number) in Numbers)
            json.WriteNumber(input, number);
        json.WriteEndObject();
        json.WriteStartArray("sent");
        foreach (var (number, _) in Sent)
            json.WriteNumberValue(number);
        json.WriteEndArray();
    }

    /// <summary>The Snapshots the message carries, each as its Arrow stream (ADR-0065), in the order of
    /// <c>"sent"</c>.</summary>
    public async Task<DotNetStreamReference[]> StreamsAsync(CancellationToken cancellationToken = default)
    {
        var streams = new DotNetStreamReference[Sent.Count];
        for (var i = 0; i < Sent.Count; i++)
        {
            var stream = new MemoryStream();
            await SnapshotArrow.WriteAsync(Sent[i].Snapshot, stream, cancellationToken);
            stream.Position = 0;
            streams[i] = new DotNetStreamReference(stream);
        }
        return streams;
    }
}
