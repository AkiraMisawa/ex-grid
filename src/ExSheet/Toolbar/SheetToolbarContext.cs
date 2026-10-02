using System.Globalization;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components;
using SheetComponent = ExSheet.Components.ExSheet;

namespace ExSheet;

/// <summary>
/// What a Sheet Toolbar cascades to its Toolbar Rows and Toolbar Items (ADR-0100): the Sheet, by its
/// public commands only, and what the items show — the Focus cell's Cell Format, whether an edit is
/// open, the Sheet's culture. It tells its items when any of it changes. A Consumer's own item reads
/// it as ExSheet's items do.
/// </summary>
public sealed class SheetToolbarContext
{
    private readonly List<ToolbarItemBase> _items = [];
    private readonly Func<string, IReadOnlyList<ToolbarChoice>, Task> _openChoices;
    private readonly Func<Task> _returnKeyboard;
    private readonly Action<string> _tell;
    private readonly Action _rowsChanged;
    private readonly List<ToolbarRow> _rows = [];
    private int _nextId;

    internal SheetToolbarContext(
        SheetComponent sheet, string idPrefix, Func<string, IReadOnlyList<ToolbarChoice>, Task> openChoices,
        Func<Task> returnKeyboard, Action<string> tell, Action rowsChanged)
    {
        Sheet = sheet;
        IdPrefix = idPrefix;
        _openChoices = openChoices;
        _returnKeyboard = returnKeyboard;
        _tell = tell;
        _rowsChanged = rowsChanged;
    }

    /// <summary>The Sheet the toolbar belongs to, and acts on through its public commands.</summary>
    public SheetComponent Sheet { get; }

    /// <summary>The Cell Format of the Focus cell as it shows: what a toggle shows pressed. The
    /// default Cell Format while nothing is selected.</summary>
    public CellFormat FocusFormat { get; private set; } = CellFormat.Default;

    /// <summary>Whether an edit is open. While one is, every Toolbar Item is unavailable (ADR-0100).</summary>
    public bool IsEditing { get; private set; }

    /// <summary>The Sheet's culture: what a currency or a date is written in.</summary>
    public CultureInfo Culture { get; private set; } = CultureInfo.InvariantCulture;

    /// <summary>Whether anything is selected. A command over an empty Selection does nothing.</summary>
    public bool HasSelection { get; private set; }

    /// <summary>The Chrome that draws the items: the Sheet's, when it is an <see cref="ISheetChrome"/>.</summary>
    public ISheetChrome? Chrome { get; private set; }

    /// <summary>Raised whenever anything an item shows may have changed.</summary>
    public event Action? Changed;

    internal string IdPrefix { get; }

    /// <summary>One Toolbar Row's height, resolved in C# (ADR-0100, ADR-0028).</summary>
    internal double RowHeightPx { get; set; }

    /// <summary>How many Toolbar Rows the toolbar holds: one when its content declares none.</summary>
    internal int RowCount => Math.Max(1, _rows.Count);

    /// <summary>The items in their order on the toolbar, which is the markup's (<see cref="ToolbarOrder"/>).</summary>
    internal IReadOnlyList<ToolbarItemBase> Items => [.. _items.OrderBy(item => item.Place)];

    /// <summary>The item the keyboard is on inside the toolbar, while the toolbar holds DOM focus.</summary>
    internal ToolbarItemBase? Active { get; private set; }

    internal void Update(CellFormat focusFormat, bool hasSelection, bool editing, CultureInfo culture, ISheetChrome? chrome)
    {
        if (focusFormat == FocusFormat && hasSelection == HasSelection && editing == IsEditing
            && Equals(culture, Culture) && ReferenceEquals(chrome, Chrome))
        {
            return;
        }
        FocusFormat = focusFormat;
        HasSelection = hasSelection;
        IsEditing = editing;
        Culture = culture;
        Chrome = chrome;
        Changed?.Invoke();
    }

    /// <summary>
    /// Runs a command from a Toolbar Item. A refusal — an edit opened between the press and the
    /// command — is said in the Sheet's notice, as the Sheet says its other refusals, rather than
    /// thrown into the press's handler.
    /// </summary>
    /// <param name="command">The command, made of the Sheet's public commands.</param>
    public async Task RunAsync(Func<Task> command)
    {
        ArgumentNullException.ThrowIfNull(command);
        try
        {
            await command();
        }
        catch (SheetRefusedException refused)
        {
            _tell(refused.Refusal.Message);
        }
    }

    /// <summary>
    /// Gives the keyboard back to the Sheet, as a command run from the toolbar's keys does (ADR-0100):
    /// from the toolbar itself when it holds DOM focus, which the core does not take the keyboard
    /// from on its own.
    /// </summary>
    public Task ReturnKeyboardAsync() => LeaveToolbar?.Invoke() ?? _returnKeyboard();

    /// <summary>The toolbar's own way of giving the keyboard back, while it is shown.</summary>
    internal Func<Task>? LeaveToolbar { get; set; }

    /// <summary>The core's focus function itself (ADR-0021's note of 2026-09-30).</summary>
    internal Task ReturnKeyboardToSheetAsync() => _returnKeyboard();

    /// <summary>
    /// Opens <paramref name="choices"/> in the Sheet's built-in frame: a popover inside the Sheet's
    /// box (ADR-0040), which takes the keyboard and gives it back when it closes (ADR-0039). A
    /// Chrome with lists of its own opens its own instead.
    /// </summary>
    /// <param name="title">The list's accessible name.</param>
    /// <param name="choices">The choices, in order.</param>
    public Task OpenChoicesAsync(string title, IReadOnlyList<ToolbarChoice> choices) => _openChoices(title, choices);

    internal string NextId() => $"{IdPrefix}-item-{_nextId++}";

    internal void Join(ToolbarItemBase item)
    {
        if (!_items.Contains(item)) _items.Add(item);
    }

    internal void Leave(ToolbarItemBase item)
    {
        _items.Remove(item);
        if (ReferenceEquals(Active, item)) Active = null;
    }

    internal void JoinRow(ToolbarRow row)
    {
        if (_rows.Contains(row)) return;
        _rows.Add(row);
        _rowsChanged();
    }

    internal void LeaveRow(ToolbarRow row)
    {
        if (_rows.Remove(row)) _rowsChanged();
    }

    /// <summary>The Toolbar Rows in their order on the toolbar, which is the markup's.</summary>
    internal IReadOnlyList<ToolbarRow> Rows => [.. _rows.OrderBy(row => row.Place)];

    /// <summary>Puts the keyboard on <paramref name="item"/> inside the toolbar, or on none.</summary>
    internal void Activate(ToolbarItemBase? item)
    {
        if (ReferenceEquals(Active, item)) return;
        Active = item;
        Changed?.Invoke();
    }
}
