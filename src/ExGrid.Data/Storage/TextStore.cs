using System.Runtime.InteropServices;
using System.Text;

namespace ExGrid.Data.Storage;

/// <summary>
/// A text column's dictionary while a build fills it: every distinct value once, in the order it
/// first appears, told apart ordinally (ADR-0063). Used by one thread, then handed to a
/// <see cref="TextStore"/>.
/// </summary>
internal sealed class TextInterner
{
    private readonly Dictionary<string, int> codes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> spans;
    private string[][] chunks = new string[4][];
    private int count;
    private string? last;
    private int lastCode;

    public TextInterner() => spans = codes.GetAlternateLookup<ReadOnlySpan<char>>();

    public int Count => count;

    public string this[int code] => chunks[code >> TextStore.ChunkShift][code & TextStore.ChunkMask];

    public int Intern(string text)
    {
        // A run of the same instance — the Consumer's records often share their strings — skips the hash.
        if (ReferenceEquals(text, last))
            return lastCode;
        ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(codes, text, out var exists);
        if (!exists)
        {
            slot = count;
            Append(text);
        }
        last = text;
        lastCode = slot;
        return slot;
    }

    public int Intern(ReadOnlySpan<char> text)
    {
        if (spans.TryGetValue(text, out var code))
            return code;
        var created = new string(text);
        code = count;
        codes.Add(created, code);
        Append(created);
        return code;
    }

    /// <summary>The store the finished Snapshot reads; the interner is not used again.</summary>
    public TextStore ToStore() => new(chunks, count, codes);

    private void Append(string text)
    {
        var chunk = count >> TextStore.ChunkShift;
        if (chunk == chunks.Length)
            Array.Resize(ref chunks, chunks.Length * 2);
        (chunks[chunk] ??= new string[TextStore.ChunkLength])[count & TextStore.ChunkMask] = text;
        count++;
    }
}

/// <summary>
/// A text column's dictionary, shared by every version of a Snapshot's lineage. It only grows
/// (ADR-0063): a version sees the first <c>count</c> entries it was made with, and a code means the
/// same text in every version. Entries below any version's count are never written again, so they
/// are read without a lock.
/// <para>
/// Two versions made from the same one do not share what each added: the second to add forks the
/// store, sharing the entries below their common count and copying none of them.
/// </para>
/// </summary>
internal sealed class TextStore
{
    internal const int ChunkShift = 10;
    internal const int ChunkLength = 1 << ChunkShift;
    internal const int ChunkMask = ChunkLength - 1;

    private readonly object gate = new();

    /// <summary>The entries the store was made with. Never changed, so read without the gate.</summary>
    private readonly Dictionary<string, int> fixedCodes;
    private readonly Dictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> fixedSpans;

    /// <summary>Entries appended since, by Change Batches. Guarded by the gate.</summary>
    private Dictionary<string, int>? addedCodes;

    private string[][] chunks;
    private int count;

    public TextStore(string[][] chunks, int count, Dictionary<string, int> fixedCodes)
        : this(chunks, count, fixedCodes, null)
    {
    }

    private TextStore(string[][] chunks, int count, Dictionary<string, int> fixedCodes, Dictionary<string, int>? addedCodes)
    {
        this.chunks = chunks;
        this.count = count;
        this.fixedCodes = fixedCodes;
        this.addedCodes = addedCodes;
        fixedSpans = fixedCodes.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    public string Text(int code) => Volatile.Read(ref chunks)[code >> ChunkShift][code & ChunkMask];

    /// <summary>The code of <paramref name="text"/> among the first <paramref name="visible"/> entries.</summary>
    public bool TryGetCode(string text, int visible, out int code)
    {
        if (fixedCodes.TryGetValue(text, out code))
            return code < visible || Miss(out code);
        lock (gate)
        {
            if (addedCodes is not null && addedCodes.TryGetValue(text, out code))
                return code < visible || Miss(out code);
        }
        return Miss(out code);
    }

    /// <summary>The code of <paramref name="text"/> among the first <paramref name="visible"/> entries.</summary>
    public bool TryGetCode(ReadOnlySpan<char> text, int visible, out int code)
    {
        if (fixedSpans.TryGetValue(text, out code))
            return code < visible || Miss(out code);
        lock (gate)
        {
            if (addedCodes is not null && addedCodes.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(text, out code))
                return code < visible || Miss(out code);
        }
        return Miss(out code);
    }

    /// <summary>
    /// Appends <paramref name="texts"/> after the first <paramref name="visible"/> entries, giving them
    /// the codes <paramref name="visible"/> onwards in order, and returns the store that holds them:
    /// this one, or a fork of it when another version has already appended past
    /// <paramref name="visible"/>.
    /// </summary>
    public TextStore Append(int visible, IReadOnlyList<string> texts)
    {
        if (texts.Count == 0)
            return this;
        lock (gate)
        {
            if (count == visible)
            {
                AppendLocked(texts);
                return this;
            }
        }
        var fork = Fork(visible);
        lock (fork.gate)
            fork.AppendLocked(texts);
        return fork;
    }

    private TextStore Fork(int visible)
    {
        var source = Volatile.Read(ref chunks);
        var used = (visible + ChunkMask) >> ChunkShift;
        var copy = new string[Math.Max(4, used)][];
        Array.Copy(source, copy, used);
        var partial = visible & ChunkMask;
        if (partial != 0)
        {
            // The chunk that holds this version's last entries is still being filled by the other
            // version, so the fork takes a copy of the part it sees.
            var last = new string[ChunkLength];
            Array.Copy(source[used - 1], last, partial);
            copy[used - 1] = last;
        }
        Dictionary<string, int>? added = null;
        lock (gate)
        {
            if (addedCodes is not null)
            {
                foreach (var (text, code) in addedCodes)
                {
                    if (code < visible)
                        (added ??= new Dictionary<string, int>(StringComparer.Ordinal))[text] = code;
                }
            }
        }
        return new TextStore(copy, visible, fixedCodes, added);
    }

    private void AppendLocked(IReadOnlyList<string> texts)
    {
        var map = addedCodes ??= new Dictionary<string, int>(StringComparer.Ordinal);
        var array = chunks;
        foreach (var text in texts)
        {
            var chunk = count >> ChunkShift;
            if (chunk == array.Length)
            {
                var grown = new string[array.Length * 2][];
                Array.Copy(array, grown, array.Length);
                array = grown;
            }
            (array[chunk] ??= new string[ChunkLength])[count & ChunkMask] = text;
            map.Add(text, count);
            count++;
        }
        Volatile.Write(ref chunks, array);
    }

    private static bool Miss(out int code)
    {
        code = -1;
        return false;
    }
}

/// <summary>Turns UTF-8 bytes into text without a string per value, for readers that hold bytes.</summary>
internal static class Utf8Text
{
    private static readonly UTF8Encoding Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Decodes <paramref name="utf8"/> into <paramref name="buffer"/>, growing it when needed;
    /// false when the bytes are not valid UTF-8.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> utf8, ref char[] buffer, out int length)
    {
        var needed = Strict.GetMaxCharCount(utf8.Length);
        if (buffer.Length < needed)
            buffer = new char[Math.Max(needed, buffer.Length * 2)];
        try
        {
            length = Strict.GetChars(utf8, buffer);
            return true;
        }
        catch (DecoderFallbackException)
        {
            length = 0;
            return false;
        }
    }
}
