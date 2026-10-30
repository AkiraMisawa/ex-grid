namespace ExGrid.DemoPages;

/// <summary>
/// The floating inspectors a page has open (docs/specs/row-inspectors): one per row, by the
/// Consumer's key, stacked by a counter the page paints as z-index — never by DOM order,
/// since moving a node in the DOM would take the keyboard out of it.
/// </summary>
public sealed class FloatingInspectors<TRow>(Func<TRow, string> keyOf, double left, double top)
{
    private readonly List<Entry> _open = [];
    private int _stack;

    /// <summary>One open inspector.</summary>
    public sealed class Entry(string key, TRow row, double x, double y)
    {
        public string Key { get; } = key;
        public TRow Row { get; } = row;
        public double X { get; } = x;
        public double Y { get; } = y;
        public int ZIndex { get; set; }
        public int FocusRequest { get; set; }
    }

    public IReadOnlyList<Entry> Open => _open;

    /// <summary>Opens the row's inspector, or brings the one already open to the front;
    /// either way the keyboard is asked into it.</summary>
    public void Show(TRow row)
    {
        var key = keyOf(row);
        var entry = _open.FirstOrDefault(e => e.Key == key);
        if (entry is null)
        {
            var offset = 28 * (_open.Count % 8);
            entry = new Entry(key, row, left + offset, top + offset);
            _open.Add(entry);
        }
        BringToFront(entry);
        entry.FocusRequest++;
    }

    public void BringToFront(Entry entry) => entry.ZIndex = ++_stack;

    public void Close(Entry entry) => _open.Remove(entry);

    public void Clear() => _open.Clear();
}
