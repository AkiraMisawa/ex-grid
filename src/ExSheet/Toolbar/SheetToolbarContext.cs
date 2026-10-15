using System.Globalization;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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
    private readonly Func<string, IReadOnlyList<ToolbarChoice>, Func<Task>?, Action?, Task<bool>> _openChoices;
    private readonly Func<Task> _returnKeyboard;
    private readonly Action<string> _tell;
    private readonly Action _rowsChanged;
    private readonly List<ToolbarRow> _rows = [];
    private int _nextId;

    internal SheetToolbarContext(
        SheetComponent sheet, string idPrefix, Func<string, IReadOnlyList<ToolbarChoice>, Func<Task>?, Action?, Task<bool>> openChoices,
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
    /// from on its own. A list an item opened from the keyboard that may still be on its way is asked
    /// to close (<see cref="ToolbarItemContext.CloseRequest"/>), so it does not take the keyboard
    /// from the Sheet after it.
    /// </summary>
    public Task ReturnKeyboardAsync()
    {
        KeyTipList = null;
        foreach (var item in _items.ToList()) item.RequestCloseOfChoices();
        return LeaveToolbar?.Invoke() ?? _returnKeyboard();
    }

    /// <summary>
    /// The item whose list a KeyTip opened, while that list may stand (ADR-0100's note of 2026-10-12,
    /// the Windows readings' R2 (b)): Escape in it, or on the band before it has the keyboard, goes
    /// back to its row's KeyTips rather than to the Sheet.
    /// </summary>
    internal ToolbarItemBase? KeyTipList { get; set; }

    /// <summary>A Chrome's list begins to close (<see cref="ToolbarItemContext.ChoicesClosing"/>). Set by the toolbar.</summary>
    internal Func<ToolbarItemBase, KeyboardEventArgs?, Action<KeyboardEventArgs>>? ListClosingHook { get; set; }

    /// <summary>A Chrome's list has closed (<see cref="ToolbarItemContext.ChoicesClosed"/>). Set by the toolbar.</summary>
    internal Func<ToolbarItemBase, Task>? ListClosedHook { get; set; }

    /// <summary>Escape closed an item's list in the Sheet's built-in frame. Set by the toolbar.</summary>
    internal Func<ToolbarItemBase, Task>? ListEscapedHook { get; set; }

    /// <summary>An item's list is asked to open from the toolbar's keys (decided with the user, 2026-10-13). Set by the toolbar.</summary>
    internal Action<ToolbarItemBase>? ListOpeningHook { get; set; }

    /// <summary>A Chrome's list holds the keyboard, or will not (<see cref="ToolbarItemContext.ChoicesOpened"/>). Set by the toolbar.</summary>
    internal Func<ToolbarItemBase, bool, IReadOnlyList<KeyboardEventArgs>>? ListOpenedHook { get; set; }

    /// <summary>The built-in frame's list was asked to open, and opened or not. Set by the toolbar.</summary>
    internal Func<ToolbarItemBase, bool, Task>? ListOpenedInGridHook { get; set; }

    /// <summary>The list of <paramref name="item"/> is asked to open from the toolbar's keys: the keys typed on the toolbar are held for it.</summary>
    internal void ListOpening(ToolbarItemBase item) => ListOpeningHook?.Invoke(item);

    /// <summary>A Chrome's list of <paramref name="item"/> holds the keyboard (<paramref name="took"/>), or will not: the keys held for it.</summary>
    internal IReadOnlyList<KeyboardEventArgs> ListOpened(ToolbarItemBase item, bool took) => ListOpenedHook?.Invoke(item, took) ?? [];

    /// <summary>The built-in frame's list of <paramref name="item"/> was asked to open: the keys held for it go to the grid.</summary>
    internal Task ListOpenedInGridAsync(ToolbarItemBase item, bool opened) => ListOpenedInGridHook?.Invoke(item, opened) ?? Task.CompletedTask;

    /// <summary>A Chrome's list of <paramref name="item"/> begins to close: where the keys typed in it go meanwhile.</summary>
    internal Action<KeyboardEventArgs> ListClosing(ToolbarItemBase item, KeyboardEventArgs? closedBy) =>
        ListClosingHook?.Invoke(item, closedBy) ?? (static _ => { });

    /// <summary>A Chrome's list of <paramref name="item"/> has closed: the keyboard goes on from the toolbar.</summary>
    internal Task ListClosedAsync(ToolbarItemBase item) => ListClosedHook?.Invoke(item) ?? ReturnKeyboardAsync();

    /// <summary>The built-in frame's list of <paramref name="item"/> has gone, however it closed.</summary>
    internal void ListGone(ToolbarItemBase item)
    {
        if (ReferenceEquals(KeyTipList, item)) KeyTipList = null;
    }

    /// <summary>
    /// The toolbar's band held the release it heard just now, among the keys in flight: the Sheet's handler,
    /// which hears the same release next, leaves it (decided with the user, 2026-10-13).
    /// </summary>
    internal bool KeyUpHeld { get; set; }

    /// <summary>
    /// A Chrome's own list is closing (<see cref="ToolbarItemContext.ChoicesClosing"/>) and the toolbar has
    /// not yet given the keyboard back: the keyboard is on the toolbar, and comes back through the grid. Set
    /// by the toolbar.
    /// </summary>
    internal bool ClosingList { get; set; }

    /// <summary>Whether the band held the release the Sheet hears now; asked once.</summary>
    internal bool TakeKeyUpHeld()
    {
        var held = KeyUpHeld;
        KeyUpHeld = false;
        return held;
    }

    /// <summary>The toolbar's own way of giving the keyboard back, while it is shown.</summary>
    internal Func<Task>? LeaveToolbar { get; set; }

    /// <summary>The core's focus function itself (ADR-0021's note of 2026-09-30).</summary>
    internal Task ReturnKeyboardToSheetAsync() => _returnKeyboard();

    /// <summary>
    /// The core's focus function from a frame of the Sheet's own, the toolbar, with the keys typed on
    /// it on the way (ADR-0050 item 16's note of 2026-10-12). Set by the Sheet.
    /// </summary>
    internal Func<ElementReference, Func<IReadOnlyList<KeyboardEventArgs>>, Task>? TakeKeyboardBack { get; set; }

    /// <summary>
    /// Tells the grid's key gate that the keyboard is going to the toolbar, a frame of the Sheet's own
    /// outside the grid (ADR-0050 item 16): the keys typed until the toolbar holds it are the toolbar's.
    /// Set by the Sheet.
    /// </summary>
    internal Func<Task>? HandKeyboardToToolbar { get; set; }

    /// <summary>
    /// Gives the toolbar's band the keyboard through the grid (<c>HandKeyboardToAsync</c>), which, while
    /// it holds keys for the toolbar, gives it in turn, with those keys (ADR-0050 item 16's note of
    /// 2026-10-12). Set by the Sheet.
    /// </summary>
    internal Func<ElementReference, Task>? GiveToolbarTheKeyboard { get; set; }

    /// <summary>Closes the list titled so in the Sheet's built-in frame, if it is what the frame shows. Set by the Sheet.</summary>
    internal Func<string, Task>? CloseChoices { get; set; }

    /// <summary>Closes the built-in frame's list titled <paramref name="title"/>, if it stands (ADR-0100's note of 2026-10-12).</summary>
    internal Task CloseChoicesAsync(string title) => CloseChoices?.Invoke(title) ?? Task.CompletedTask;

    /// <summary>
    /// Opens <paramref name="choices"/> in the Sheet's built-in frame: a popover inside the Sheet's
    /// box (ADR-0040), which takes the keyboard and gives it back when it closes (ADR-0039). A
    /// Chrome with lists of its own opens its own instead.
    /// </summary>
    /// <param name="title">The list's accessible name.</param>
    /// <param name="choices">The choices, in order.</param>
    public Task OpenChoicesAsync(string title, IReadOnlyList<ToolbarChoice> choices) => _openChoices(title, choices, null, null);

    /// <summary>
    /// Opens <paramref name="item"/>'s choices in the Sheet's built-in frame, which tells the toolbar
    /// when Escape closes them and when they have gone (ADR-0100's note of 2026-10-12), answering whether
    /// they opened.
    /// </summary>
    internal Task<bool> OpenChoicesOfAsync(ToolbarItemBase item, string title, IReadOnlyList<ToolbarChoice> choices) =>
        _openChoices(title, choices, () => ListEscapedHook?.Invoke(item) ?? Task.CompletedTask, () => ListGone(item));

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

    // ---- KeyTips (ADR-0100) ----

    /// <summary>What the KeyTips show: none, each row's letter, or the items' letters in one row.</summary>
    internal KeyTipStage KeyTips { get; private set; }

    /// <summary>The row whose items' letters are shown; null for the one row of a toolbar that declares none.</summary>
    internal ToolbarRow? KeyTipRow { get; private set; }

    /// <summary>The letters typed so far at this level.</summary>
    internal string KeyTipTyped { get; private set; } = "";

    /// <summary>The KeyTip of the one row of a toolbar that declares no rows: Excel's Home.</summary>
    internal const string ImplicitRowKeyTip = "H";

    /// <summary>Starts the KeyTips: the rows' letters. Set by the toolbar while it is shown.</summary>
    internal Func<Task>? StartKeyTips { get; set; }

    internal void ShowKeyTips(KeyTipStage stage, ToolbarRow? row = null, string typed = "")
    {
        if (stage == KeyTips && ReferenceEquals(row, KeyTipRow) && typed == KeyTipTyped) return;
        KeyTips = stage;
        KeyTipRow = row;
        KeyTipTyped = typed;
        Changed?.Invoke();
    }

    /// <summary>Whether <paramref name="item"/> shows its KeyTip now.</summary>
    internal bool ShowsKeyTip(ToolbarItemBase item) =>
        KeyTips == KeyTipStage.Items && ReferenceEquals(item.Row, KeyTipRow)
        && item.KeyTipLetters is { } letters && letters.StartsWith(KeyTipTyped, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Refuses two KeyTips at one level when either begins the other, naming both (ADR-0100): typed,
    /// the shorter would always be taken for the longer, or the two would be one key. The rows'
    /// letters are one level, and the items' letters in each row another.
    /// </summary>
    internal void RefuseCollidingKeyTips()
    {
        var rows = _rows.Where(row => row.KeyTip is not null).Select(row => (Letters: row.KeyTip!, Name: $"the Toolbar Row '{row.Label ?? row.KeyTip}'"));
        Refuse(rows);
        foreach (var group in _items.Where(item => item.KeyTipLetters is not null).GroupBy(item => item.Row))
        {
            Refuse(group.Select(item => (Letters: item.KeyTipLetters!, Name: $"{item.GetType().Name} '{item.Describe().Name}'")));
        }

        static void Refuse(IEnumerable<(string Letters, string Name)> tips)
        {
            var seen = new List<(string Letters, string Name)>();
            foreach (var tip in tips)
            {
                foreach (var other in seen)
                {
                    if (tip.Letters.StartsWith(other.Letters, StringComparison.OrdinalIgnoreCase)
                        || other.Letters.StartsWith(tip.Letters, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"The KeyTips '{other.Letters}' of {other.Name} and '{tip.Letters}' of {tip.Name} collide at one level: " +
                            "typed, one would be taken for the other. Give one of them other letters (ADR-0100).");
                    }
                }
                seen.Add(tip);
            }
        }
    }

    /// <summary>Puts the keyboard on <paramref name="item"/> inside the toolbar, or on none.</summary>
    internal void Activate(ToolbarItemBase? item)
    {
        if (ReferenceEquals(Active, item)) return;
        Active = item;
        Changed?.Invoke();
    }
}

/// <summary>What the KeyTips show (ADR-0100).</summary>
internal enum KeyTipStage
{
    /// <summary>None.</summary>
    Off,

    /// <summary>Each Toolbar Row's letter, as Excel's tabs show theirs.</summary>
    Rows,

    /// <summary>The letters of the items in one row.</summary>
    Items,
}
