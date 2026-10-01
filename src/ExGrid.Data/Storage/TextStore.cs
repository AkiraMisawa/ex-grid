using System.Runtime.InteropServices;
using System.Text;

namespace ExGrid.Data.Storage;

/// <summary>
/// A text column's dictionary while a build fills it: every distinct value once, in the order it
/// first appears, told apart ordinally (ADR-0064). Used by one thread, then handed to a
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

    /// <summary>The entries <see cref="codes"/> holds: the first ones. Those after them were appended
    /// by <see cref="AddNew"/>, by a reader that tells its texts apart itself.</summary>
    private int indexed;

    public TextInterner() => spans = codes.GetAlternateLookup<ReadOnlySpan<char>>();

    public int Count => count;

    public string this[int code] => chunks[code >> TextStore.ChunkShift][code & TextStore.ChunkMask];

    public int Intern(string text)
    {
        // A run of the same instance — the Consumer's records often share their strings — skips the hash.
        if (ReferenceEquals(text, last))
            return lastCode;
        Index();
        ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(codes, text, out var exists);
        if (!exists)
        {
            slot = count;
            Append(text);
            indexed = count;
        }
        last = text;
        lastCode = slot;
        return slot;
    }

    public int Intern(ReadOnlySpan<char> text)
    {
        Index();
        if (spans.TryGetValue(text, out var code))
            return code;
        var created = new string(text);
        code = count;
        codes.Add(created, code);
        Append(created);
        indexed = count;
        return code;
    }

    /// <summary>
    /// Appends <paramref name="text"/>, which the caller has made sure the dictionary does not hold,
    /// and returns its code — for a reader that tells texts apart by something cheaper than their
    /// characters, such as their UTF-8 bytes. The codes are not looked up by text meanwhile; the store
    /// indexes them when first asked for one (<see cref="FixedCodes"/>).
    /// </summary>
    public int AddNew(string text)
    {
        Append(text);
        return count - 1;
    }

    /// <summary>The store the finished Snapshot reads; the interner is not used again.</summary>
    public TextStore ToStore() => new(chunks, count, new FixedCodes(chunks, count, codes, indexed));

    /// <summary>Indexes the entries <see cref="AddNew"/> appended, before a text is looked up.</summary>
    private void Index()
    {
        for (; indexed < count; indexed++)
            codes.Add(this[indexed], indexed);
    }

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
/// The codes of the entries a <see cref="TextStore"/> was made with, by their text. When the build
/// told its texts apart without them, the entries come unindexed, and they are indexed when a code is
/// first asked for — by a Change Batch, or a reader of <see cref="TextDictionary.TryGetCode(string, out int)"/>
/// — once for every version that shares them: a load that is never asked spends nothing on them. Once
/// indexed, they are never changed, so they are read without a lock.
/// </summary>
internal sealed class FixedCodes
{
    private readonly object gate = new();
    private readonly string[][] chunks;
    private readonly int count;
    private readonly Dictionary<string, int> codes;
    private readonly Dictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> spans;
    private int indexed;
    private volatile bool complete;

    /// <param name="chunks">The entries; those below <paramref name="count"/> are never written again.</param>
    /// <param name="count">The entries the store was made with.</param>
    /// <param name="codes">The codes of the first <paramref name="indexed"/> entries, which no one else
    /// changes from now on.</param>
    /// <param name="indexed">The entries <paramref name="codes"/> holds.</param>
    public FixedCodes(string[][] chunks, int count, Dictionary<string, int> codes, int indexed)
    {
        this.chunks = chunks;
        this.count = count;
        this.codes = codes;
        this.indexed = indexed;
        spans = codes.GetAlternateLookup<ReadOnlySpan<char>>();
        complete = indexed == count;
    }

    public bool TryGetValue(string text, out int code)
    {
        if (!complete)
            Complete();
        return codes.TryGetValue(text, out code);
    }

    public bool TryGetValue(ReadOnlySpan<char> text, out int code)
    {
        if (!complete)
            Complete();
        return spans.TryGetValue(text, out code);
    }

    private void Complete()
    {
        lock (gate)
        {
            if (complete)
                return;
            codes.EnsureCapacity(count);
            for (; indexed < count; indexed++)
                codes.Add(chunks[indexed >> TextStore.ChunkShift][indexed & TextStore.ChunkMask], indexed);
            complete = true;
        }
    }
}

/// <summary>
/// A text column's dictionary, shared by every version of a Snapshot's lineage. It only grows
/// (ADR-0064): a version sees the first <c>count</c> entries it was made with, and a code means the
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

    /// <summary>The codes of the entries the store was made with, shared by every fork of it.</summary>
    private readonly FixedCodes fixedCodes;

    /// <summary>Entries appended since, by Change Batches. Guarded by the gate.</summary>
    private Dictionary<string, int>? addedCodes;

    private string[][] chunks;
    private int count;

    public TextStore(string[][] chunks, int count, FixedCodes fixedCodes)
        : this(chunks, count, fixedCodes, null)
    {
    }

    private TextStore(string[][] chunks, int count, FixedCodes fixedCodes, Dictionary<string, int>? addedCodes)
    {
        this.chunks = chunks;
        this.count = count;
        this.fixedCodes = fixedCodes;
        this.addedCodes = addedCodes;
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
        if (fixedCodes.TryGetValue(text, out code))
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
