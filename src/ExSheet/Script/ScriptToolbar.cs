using System.Text.Json;

namespace ExSheet.Script;

/// <summary>
/// A <c>ToolbarContent</c> as the ExSheet carries it (ADR-0100, ADR-0370 step 2): <see cref="ToolbarRow"/>s, <see cref="DefaultToolbarRow"/>, ExSheet's Toolbar
/// Items, <see cref="ToolbarSeparator"/>s and the Consumer's <see cref="ToolbarButton"/>s — each declaring itself here,
/// in markup order, drawing nothing. The Script Core draws the toolbar from what they declare, sent as its
/// <c>toolbar</c> option; a press on a button comes back by the number given here. Cascaded by the Script ExSheet to
/// its <c>ToolbarContent</c> only.
/// </summary>
internal sealed class ScriptToolbar
{
    /// <summary>The Script Core's name of each of ExSheet's own items.</summary>
    private static readonly IReadOnlyDictionary<Type, string> Commands = new Dictionary<Type, string>
    {
        [typeof(BoldItem)] = "bold",
        [typeof(ItalicItem)] = "italic",
        [typeof(UnderlineItem)] = "underline",
        [typeof(StrikethroughItem)] = "strikethrough",
        [typeof(FontColourItem)] = "fontColour",
        [typeof(FillItem)] = "fill",
        [typeof(BordersItem)] = "borders",
        [typeof(AlignLeftItem)] = "alignLeft",
        [typeof(AlignCenterItem)] = "alignCenter",
        [typeof(AlignRightItem)] = "alignRight",
        [typeof(NumberFormatItem)] = "numberFormat",
        [typeof(PercentItem)] = "percent",
        [typeof(CommaItem)] = "comma",
        [typeof(FormatCellsItem)] = "formatCells",
    };

    /// <summary>The KeyTip of the one row the items stand in when they stand in no <see cref="ToolbarRow"/>: Excel's
    /// Home.</summary>
    private const string ImplicitRowKeyTip = "H";

    private readonly Action _changed;
    private readonly HashSet<ToolbarRow> _rows = [];
    private readonly Dictionary<DefaultToolbarRow, ToolbarOrder> _defaults = [];
    private readonly HashSet<ToolbarItemBase> _items = [];
    private readonly Dictionary<ToolbarButton, int> _buttons = [];
    private readonly Dictionary<int, ToolbarButton> _numbered = [];
    private int _nextButton;

    /// <param name="changed">Told whenever a row or an item declares itself, again or anew, or goes: as the Sheet
    /// renders them, or as a component of the Consumer's around them renders on its own.</param>
    internal ScriptToolbar(Action changed) => _changed = changed;

    /// <summary>The scope the rows, and items that stand in no row, take their places from.</summary>
    internal ToolbarOrder Root { get; } = new();

    /// <summary>Whether anything is declared: a <c>ToolbarContent</c> that declares nothing leaves the default row.</summary>
    internal bool Any => _rows.Count > 0 || _defaults.Count > 0 || _items.Count > 0;

    internal void JoinRow(ToolbarRow row)
    {
        // The Script Core names every row; one without a name would stand unnamed among them.
        if (string.IsNullOrEmpty(row.Label))
            throw new InvalidOperationException("A ToolbarRow of ExSheet.Script.ExSheet names itself with Label, its accessible name, which the Script Core's rows require (ADR-0100).");
        _rows.Add(row);
        _changed();
    }

    internal void LeaveRow(ToolbarRow row)
    {
        if (_rows.Remove(row))
            _changed();
    }

    internal void JoinDefault(DefaultToolbarRow row, ToolbarOrder place)
    {
        _defaults[row] = place;
        _changed();
    }

    internal void LeaveDefault(DefaultToolbarRow row)
    {
        if (_defaults.Remove(row))
            _changed();
    }

    /// <summary>An item declares itself, or declares itself again with its parameters as they now stand.</summary>
    internal void Join(ToolbarItemBase item)
    {
        if (item is ToolbarButton button)
        {
            if (!_buttons.ContainsKey(button))
            {
                _nextButton += 1;
                _buttons[button] = _nextButton;
                _numbered[_nextButton] = button;
            }
        }
        else if (item is not ToolbarSeparator && item.KeyTip is not null)
        {
            throw new InvalidOperationException(
                $"{item.GetType().Name} carries Excel's KeyTip, and takes no other: a KeyTip of the Consumer's is a ToolbarButton's (ADR-0100).");
        }
        _items.Add(item);
        _changed();
    }

    internal void Leave(ToolbarItemBase item)
    {
        if (!_items.Remove(item))
            return;
        if (item is ToolbarButton button && _buttons.Remove(button, out var number))
            _numbered.Remove(number);
        _changed();
    }

    /// <summary>The button numbered <paramref name="number"/>, while it stands.</summary>
    internal ToolbarButton? Button(int number) => _numbered.GetValueOrDefault(number);

    /// <summary>The toolbar as the Script Core's <c>toolbar</c> option reads it: its rows in markup order, the default
    /// row as <c>default</c>, and each run of items that stand in no row as a row of its own, named as the default row
    /// is. Such a row answers to Excel's Home, H, only on a toolbar that declares no other row.</summary>
    internal void Write(Utf8JsonWriter json)
    {
        var looseKeyTip = _rows.Count == 0 && _defaults.Count == 0 ? ImplicitRowKeyTip : null;
        var entries = new List<(ToolbarOrder Place, object Entry)>();
        entries.AddRange(_rows.Select(row => (row.Place, (object)row)));
        entries.AddRange(_defaults.Select(each => (each.Value, (object)each.Key)));
        entries.AddRange(_items.Where(item => item.Row is null).Select(item => (item.Place, (object)item)));
        entries.Sort((a, b) => a.Place.CompareTo(b.Place));
        json.WriteStartArray("toolbar");
        List<ToolbarItemBase>? loose = null;
        foreach (var (_, entry) in entries)
        {
            if (entry is ToolbarItemBase item)
            {
                (loose ??= []).Add(item);
                continue;
            }
            if (loose is not null)
            {
                WriteRow(json, null, looseKeyTip, loose);
                loose = null;
            }
            if (entry is ToolbarRow row)
                WriteRow(json, row.Label, row.KeyTip, [.. _items.Where(item => ReferenceEquals(item.Row, row)).OrderBy(item => item.Place)]);
            else
                json.WriteStringValue("default");
        }
        if (loose is not null)
            WriteRow(json, null, looseKeyTip, loose);
        json.WriteEndArray();
    }

    // A row: its label, null for the row items that stand in no row make, which the Script Core names as its own
    // default row.
    private void WriteRow(Utf8JsonWriter json, string? label, string? keyTip, IReadOnlyList<ToolbarItemBase> items)
    {
        // A row of nothing would take a band off the Sheet's height and show nothing in it.
        if (items.Count == 0)
            throw new InvalidOperationException($"The ToolbarRow {label} holds no Toolbar Item: on ExSheet.Script.ExSheet a row holds one or more (ADR-0100).");
        json.WriteStartObject();
        if (label is null)
            json.WriteNull("label");
        else
            json.WriteString("label", label);
        if (keyTip is not null)
            json.WriteString("keyTip", keyTip);
        json.WriteStartArray("items");
        foreach (var item in items)
        {
            switch (item)
            {
                case ToolbarSeparator:
                    json.WriteStringValue("separator");
                    break;
                case ToolbarButton button:
                    json.WriteStartObject();
                    json.WriteNumber("button", _buttons[button]);
                    json.WriteString("text", button.Text);
                    if (button.KeyTip is not null)
                        json.WriteString("keyTip", button.KeyTip);
                    if (button.DisabledReason is not null)
                        json.WriteString("disabledReason", button.DisabledReason);
                    json.WriteBoolean("content", button.ChildContent is not null);
                    json.WriteEndObject();
                    break;
                default:
                    json.WriteStringValue(Commands[item.GetType()]);
                    break;
            }
        }
        json.WriteEndArray();
        json.WriteEndObject();
    }
}

/// <summary>The slot a <see cref="ToolbarButton"/>'s <c>ChildContent</c> is drawn in, by the button's number: the
/// Consumer's markup draws it, not the Chrome (ADR-0100, ADR-0410).</summary>
/// <param name="Button">The button's number, as <see cref="ScriptToolbar"/> gave it.</param>
internal sealed record ToolbarContentView(int Button)
{
    /// <summary>The script's name of the slot.</summary>
    internal const string SlotName = "toolbarContent";

    /// <summary>The view of the slot's context, as the script sends it.</summary>
    internal static ToolbarContentView Of(string context)
    {
        using var document = JsonDocument.Parse(context);
        return new(document.RootElement.GetProperty("button").GetInt32());
    }
}
