namespace ExGrid;

/// <summary>
/// Strings indexed by a small non-negative number, each composed the first time it is asked
/// for and kept for the life of the process (ADR-0027 P5): the same few are all any grid ever
/// needs — a column's <c>aria-colindex</c>, a run of <c>####</c> — so composing one per cell
/// per render bought nothing.
///
/// <para>Read without a lock, safely from any circuit's thread: the slot array is grown by
/// replacing it whole, never in place, and a slot filled on an array a grow has just replaced
/// is only composed again the next time. Filled on demand, so reaching one large index costs
/// the pointers below it, not the strings.</para>
/// </summary>
internal sealed class InternedStrings(Func<int, string> compose)
{
    private string?[] _slots = [];
    private readonly object _growLock = new();

    public string this[int index]
    {
        get
        {
            var slots = Volatile.Read(ref _slots);
            if (index >= slots.Length)
                slots = Grow(index);
            return slots[index] ??= compose(index);
        }
    }

    private string?[] Grow(int index)
    {
        lock (_growLock)
        {
            var slots = _slots;
            if (index >= slots.Length)
            {
                var grown = new string?[Math.Max(Math.Max(index + 1, 32), slots.Length * 2)];
                slots.CopyTo(grown, 0);
                Volatile.Write(ref _slots, grown);
                slots = grown;
            }

            return slots;
        }
    }
}
