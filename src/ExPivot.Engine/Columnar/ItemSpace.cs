using ExGrid.Data;

namespace ExPivot.Engine;

/// <summary>
/// One field's Items over a Snapshot (ADR-0060), and how each row maps to one:
/// <list type="bullet">
/// <item>Text is told apart ignoring case. The column's dictionary is folded, not its rows: every
/// code of one Item maps to that Item's index, and the Item is labelled by the first spelling to
/// arrive among the records still present — the lowest code of the Item that some held row
/// carries. The codes are counted per held row (<see cref="Presence"/>), so that a Change Batch can
/// take a spelling away.</item>
/// <item>A number is its value: an Integer 1, a Decimal 1.0 and a Double 1 are one Item, keyed by
/// the <c>double</c> as the first engine keyed it. A non-finite Double is <c>#NUM!</c>.</item>
/// <item>A date is its clock value; a date part is the part's number.</item>
/// <item>A Boolean is <c>TRUE</c> or <c>FALSE</c>, and a Blank is <c>(blank)</c>, in every kind.</item>
/// </list>
/// Item indexes are handed out as Items are first met and never change, so a held answer keeps
/// them across Change Batches. Not thread-safe: one pass, or one held answer under its lock.
/// </summary>
internal sealed class ItemSpace
{
    private readonly FieldBinding _binding;
    private readonly HashSet<ItemKey>? _hiddenKeys;
    private ItemKey[] _keys = new ItemKey[16];
    private bool[] _hidden = new bool[16];
    private int[] _firstCode = new int[16]; // a text Item's lowest code; -1 for the other kinds
    private int[] _lastCode = new int[16];
    private int _count;

    // Text: one fold of the field's Text column.
    private readonly int _textOrdinal = -1;
    private readonly Dictionary<string, int>? _itemOfText;
    private int[] _itemOfCode = [];
    private int[] _nextCode = [];
    private int[] _presence = [];
    private int _folded;
    private TextDictionary? _dictionary;

    // Numbers, by the double's bits; dates by their ticks; date parts by the part's number.
    private LongIntMap? _numbers;
    private LongIntMap? _dates;
    private long[]? _dayTicks;
    private int[]? _dayItems;
    private int[]? _parts;
    private int _blank = -1;
    private int _error = -1;
    private int _false = -1;
    private int _true = -1;

    public ItemSpace(FieldBinding binding, Snapshot snapshot, IReadOnlyList<PivotItemKey>? hidden)
    {
        _binding = binding;
        if (hidden is { Count: > 0 })
            _hiddenKeys = [.. hidden.Select(ItemKey.FromPublic)];
        foreach (var column in binding.Columns)
        {
            if (column.Role == ValueRole.Text)
            {
                _textOrdinal = column.Ordinal;
                _itemOfText = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }
        }
        Refresh(snapshot);
    }

    internal HashSet<int> ChangedItems { get; } = [];

    public FieldBinding Binding => _binding;

    /// <summary>How many Items the space has handed out.</summary>
    public int Count => _count;

    /// <summary>Whether the field hides any Item.</summary>
    public bool HidesAny => _hiddenKeys is not null;

    /// <summary>Per Item, whether it is hidden; longer than <see cref="Count"/>.</summary>
    public bool[] Hidden => _hidden;

    /// <summary>Whether the field reads a Text column, whose codes are counted.</summary>
    public bool HasText => _textOrdinal >= 0;

    /// <summary>Per code of the Text column, how many held rows carry it.</summary>
    public int[] Presence => _presence;

    /// <summary>The Item's key: its kind and value — for a text Item, its lowest code's text.</summary>
    public ItemKey KeyOf(int item) => _keys[item];

    public bool IsHidden(int item) => _hidden[item];

    /// <summary>Folds the codes the Text column's dictionary gained since: a Change Batch may bring
    /// new text, which joins an Item ignoring case or starts one.</summary>
    public void Refresh(Snapshot snapshot)
    {
        if (_textOrdinal < 0)
            return;
        var dictionary = ((TextColumn)snapshot.Columns[_textOrdinal]).Dictionary;
        _dictionary = dictionary;
        if (dictionary.Count == _folded)
            return;
        if (dictionary.Count > _itemOfCode.Length)
        {
            var size = Math.Max(dictionary.Count, _itemOfCode.Length * 2);
            Array.Resize(ref _itemOfCode, size);
            Array.Resize(ref _nextCode, size);
            Array.Resize(ref _presence, size);
        }
        for (var code = _folded; code < dictionary.Count; code++)
        {
            var text = dictionary[code];
            if (_itemOfText!.TryGetValue(text, out var item))
            {
                _nextCode[_lastCode[item]] = code;
                _lastCode[item] = code;
            }
            else
            {
                item = Add(ItemKey.OfText(text));
                _itemOfText.Add(text, item);
                _firstCode[item] = code;
                _lastCode[item] = code;
            }
            _itemOfCode[code] = item;
            _nextCode[code] = -1;
        }
        _folded = dictionary.Count;
    }

    /// <summary>The public key of an Item: a text Item spelled as the first of its codes some held
    /// row carries (ADR-0060), or as its first code when none does.</summary>
    public PivotItemKey PublicKeyOf(int item)
    {
        var first = _firstCode[item];
        if (first < 0)
            return _keys[item].ToPublic();
        return PivotItemKey.Text(Spelling(item));
    }

    /// <summary>The internal key of an Item, a text Item spelled as <see cref="PublicKeyOf"/> spells it.</summary>
    public ItemKey SpelledKeyOf(int item)
        => _firstCode[item] < 0 ? _keys[item] : ItemKey.OfText(Spelling(item));

    private string Spelling(int item)
    {
        for (var code = _firstCode[item]; code >= 0; code = _nextCode[code])
        {
            if (_presence[code] > 0)
                return _dictionary![code];
        }
        return _dictionary![_firstCode[item]];
    }

    /// <summary>
    /// The Items of rows [<paramref name="from"/>, +<c>items.Length</c>) of a slice. The codes of the
    /// held rows — those <paramref name="removed"/> does not mark — are counted.
    /// </summary>
    public void Map(in SnapshotSlice slice, int from, Span<int> items, ReadOnlySpan<ulong> removed)
    {
        if (_binding.Single is { } single)
        {
            MapColumn(single, slice, from, items, removed, blanks: true);
            return;
        }
        items.Fill(-1);
        foreach (var column in _binding.Columns)
            MapColumn(column, slice, from, items, removed, blanks: false);
        for (var i = 0; i < items.Length; i++)
        {
            if (items[i] < 0)
                items[i] = Blank();
        }
    }

    /// <summary>Counts out a row that is no longer held (ADR-0067): the code it carried, so that a
    /// text Item's spelling can leave with the last record that wrote it so.</summary>
    public void Unmap(in SnapshotSlice slice, int offset)
    {
        if (_textOrdinal < 0)
            return;
        foreach (var column in _binding.Columns)
        {
            if (column.Role != ValueRole.Text)
                continue;
            var code = slice.Codes((TextColumn)column.Column)[offset];
            if (code >= 0)
            {
                ChangedItems.Add(_itemOfCode[code]);
                _presence[code]--;
            }
        }
    }

    private void MapColumn(BoundColumn bound, in SnapshotSlice slice, int from, Span<int> items, ReadOnlySpan<ulong> removed, bool blanks)
    {
        switch (bound.Role)
        {
            case ValueRole.Text:
                MapText(slice.Codes((TextColumn)bound.Column), from, items, removed, blanks);
                return;
            case ValueRole.Exact:
            {
                var values = slice.Decimals((DecimalColumn)bound.Column);
                var blankBits = slice.Blanks(bound.Column);
                if (values.Scale >= 0)
                {
                    var scaled = values.Scaled;
                    var scale = values.Scale;
                    for (var i = 0; i < items.Length; i++)
                    {
                        var o = from + i;
                        if (Exactly.IsSet(blankBits, o))
                        {
                            if (blanks)
                                items[i] = Blank();
                        }
                        else
                        {
                            items[i] = Number(Exactly.ToDouble(scaled[o], scale));
                        }
                    }
                }
                else
                {
                    var exact = values.Exact;
                    for (var i = 0; i < items.Length; i++)
                    {
                        var o = from + i;
                        if (Exactly.IsSet(blankBits, o))
                        {
                            if (blanks)
                                items[i] = Blank();
                        }
                        else
                        {
                            items[i] = Number((double)exact[o]);
                        }
                    }
                }
                return;
            }
            case ValueRole.Integer:
            {
                var values = slice.Integers((IntegerColumn)bound.Column);
                var blankBits = slice.Blanks(bound.Column);
                for (var i = 0; i < items.Length; i++)
                {
                    var o = from + i;
                    if (Exactly.IsSet(blankBits, o))
                    {
                        if (blanks)
                            items[i] = Blank();
                    }
                    else
                    {
                        items[i] = Number(values[o]);
                    }
                }
                return;
            }
            case ValueRole.Double:
            {
                var values = slice.Doubles((DoubleColumn)bound.Column);
                var blankBits = slice.Blanks(bound.Column);
                for (var i = 0; i < items.Length; i++)
                {
                    var o = from + i;
                    if (Exactly.IsSet(blankBits, o))
                    {
                        if (blanks)
                            items[i] = Blank();
                    }
                    else
                    {
                        items[i] = Number(values[o]);
                    }
                }
                return;
            }
            case ValueRole.Date:
            {
                var ticks = slice.Ticks((DateColumn)bound.Column);
                var blankBits = slice.Blanks(bound.Column);
                // A date column holds few distinct clock values, each many times: a row's Item is
                // looked up in a small cache by its day before the clock value is hashed, or its
                // part computed from the calendar.
                _dayTicks ??= NewDayCache();
                _dayItems ??= new int[DayCacheSize];
                var dayTicks = _dayTicks;
                var dayItems = _dayItems;
                for (var i = 0; i < items.Length; i++)
                {
                    var o = from + i;
                    if (Exactly.IsSet(blankBits, o))
                    {
                        if (blanks)
                            items[i] = Blank();
                        continue;
                    }
                    var value = ticks[o];
                    var slot = (int)((ulong)(value / TimeSpan.TicksPerDay) & (DayCacheSize - 1));
                    if (dayTicks[slot] != value)
                    {
                        dayItems[slot] = _binding.Part is { } part ? Part(PivotDateWords.Of(part, new DateTime(value))) : Date(value);
                        dayTicks[slot] = value;
                    }
                    items[i] = dayItems[slot];
                }
                return;
            }
            default:
            {
                var values = slice.Booleans((BooleanColumn)bound.Column);
                var blankBits = slice.Blanks(bound.Column);
                for (var i = 0; i < items.Length; i++)
                {
                    var o = from + i;
                    if (Exactly.IsSet(blankBits, o))
                    {
                        if (blanks)
                            items[i] = Blank();
                    }
                    else
                    {
                        items[i] = Boolean(values[o]);
                    }
                }
                return;
            }
        }
    }

    private void MapText(ReadOnlySpan<int> codes, int from, Span<int> items, ReadOnlySpan<ulong> removed, bool blanks)
    {
        var itemOfCode = _itemOfCode;
        var presence = _presence;
        if (removed.IsEmpty)
        {
            for (var i = 0; i < items.Length; i++)
            {
                var code = codes[from + i];
                if (code < 0)
                {
                    if (blanks)
                        items[i] = Blank();
                    continue;
                }
                items[i] = itemOfCode[code];
                ChangedItems.Add(itemOfCode[code]);
                presence[code]++;
            }
            return;
        }
        for (var i = 0; i < items.Length; i++)
        {
            var o = from + i;
            var code = codes[o];
            if (code < 0)
            {
                if (blanks)
                    items[i] = Blank();
                continue;
            }
            items[i] = itemOfCode[code];
            if (!Exactly.IsSet(removed, o))
            {
                ChangedItems.Add(itemOfCode[code]);
                presence[code]++;
            }
        }
    }

    private int Blank() => _blank >= 0 ? _blank : _blank = Add(ItemKey.Blank);

    private int Error() => _error >= 0 ? _error : _error = Add(ItemKey.Error);

    private int Boolean(bool value)
        => value
            ? _true >= 0 ? _true : _true = Add(ItemKey.OfBoolean(true))
            : _false >= 0 ? _false : _false = Add(ItemKey.OfBoolean(false));

    private int Number(double value)
    {
        if (!double.IsFinite(value))
            return Error();
        if (value == 0)
            value = 0; // -0 is 0
        var bits = BitConverter.DoubleToInt64Bits(value);
        _numbers ??= new LongIntMap();
        var item = _numbers.GetOrAdd(bits, _count, out var added);
        if (added)
            Add(ItemKey.OfNumber(value));
        return item;
    }

    private const int DayCacheSize = 1024;

    // No clock value is negative, so a slot holding one is empty.
    private static long[] NewDayCache()
    {
        var cache = new long[DayCacheSize];
        cache.AsSpan().Fill(-1);
        return cache;
    }

    private int Date(long ticks)
    {
        _dates ??= new LongIntMap();
        var item = _dates.GetOrAdd(ticks, _count, out var added);
        if (added)
            Add(ItemKey.OfDate(new DateTime(ticks)));
        return item;
    }

    private int Part(int value)
    {
        _parts ??= new int[_binding.Part switch { PivotDatePart.Year => 10_000, PivotDatePart.Quarter => 5, _ => 13 }];
        ref var slot = ref _parts[value];
        if (slot == 0)
            slot = Add(ItemKey.OfNumber(value)) + 1;
        return slot - 1;
    }

    private int Add(ItemKey key)
    {
        if (_count == _keys.Length)
        {
            var size = _keys.Length * 2;
            Array.Resize(ref _keys, size);
            Array.Resize(ref _hidden, size);
            Array.Resize(ref _firstCode, size);
            Array.Resize(ref _lastCode, size);
        }
        _keys[_count] = key;
        _hidden[_count] = _hiddenKeys?.Contains(key) == true;
        _firstCode[_count] = -1;
        _lastCode[_count] = -1;
        return _count++;
    }
}
