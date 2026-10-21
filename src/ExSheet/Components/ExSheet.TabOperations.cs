using System.Globalization;
using System.Runtime.CompilerServices;
using ExGrid.Chrome;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace ExSheet.Components;

/// <summary>
/// What the Sheet Tabs do besides showing a Sheet (ADR-0331 §4; exsheet-workbook ticket 06): <c>+</c>
/// and Shift+F11 add a Sheet, a double click edits a tab's name in place, the tab's menu — a secondary
/// press, Shift+F10 or the menu key — deletes, moves left or right, copies and renames, and a drag moves
/// a tab. Each is one input in the Sheet's Input Order (ADR-0240 section 6): its element claims the event
/// (<c>data-ex-claims</c>) and its handler goes through <see cref="InOrderAsync(GridInputKind, Func{Task})"/>,
/// so a key typed straight after it acts where its answer left the Sheet and the keyboard. Each change is
/// one step on the one undo stack (ADR-0331 §3), done through the Workbook as a Consumer's command is,
/// and said in the Sheet's notice, its live region. While an edit is open none is done, and the notice
/// says why.
///
/// <para>The drag is Blazor's own mouse events: a press on a tab notes the tab, and its release on another
/// tab, or past the last, moves it there. Nothing is measured and no script is added (ADR-0021); the release
/// on another tab is handled after every input received before it, as an unclaimed input is.</para>
/// </summary>
public partial class ExSheet
{
    // The element around the Sheet Tabs, the + button and the tab's menu: the frame the keyboard comes
    // back to the grid from, whichever of them holds it.
    private ElementReference _tabArea;

    // The name being edited on its tab (null when none), what it reads now, and whether the last
    // attempt to commit it was refused. The session numbers each edit, so the focus leaving a field
    // already ended is not taken for the end of the next.
    private Sheet? _renaming;
    private string _renameText = "";
    private bool _renameRefused;
    private int _renameSession;
    private bool _renameEnding;
    private ElementReference _renameBox;

    // The tab's menu, the Sheet it acts on, the command the keyboard is on, whether the keyboard came
    // from that Sheet's tab (Escape takes it back there; else to the grid), and the menu's own session.
    private SheetTabMenuContext? _menu;
    private Sheet? _menuSheet;
    private SheetTabCommandKind _menuActive;
    private bool _menuFromTab;
    private int _menuSession;
    private ElementReference _menuElement;

    // The tab a drag started on, from the press until its release or the pointer leaving the tabs.
    private Sheet? _dragFrom;
    private readonly Dictionary<string, object> _dragListeners = new(StringComparer.Ordinal);

    // A render asked for, completed as it is built, so an element it draws can be handed the keyboard.
    private TaskCompletionSource? _drawn;

    private static readonly SheetTabCommandKind[] MenuOrder =
        [SheetTabCommandKind.Rename, SheetTabCommandKind.Delete, SheetTabCommandKind.MoveLeft, SheetTabCommandKind.MoveRight, SheetTabCommandKind.Copy];

    /// <summary>Called from the markup as it is built: a render asked for by <see cref="DrawnAsync"/> has
    /// happened, and its continuation, which runs after the render is done, finds the elements it drew.</summary>
    private void NoteDrawn()
    {
        if (_drawn is { } drawn)
        {
            _drawn = null;
            drawn.TrySetResult();
        }
    }

    /// <summary>Renders, and completes once the render is built and the elements it draws are known.</summary>
    private Task DrawnAsync()
    {
        var drawn = _drawn ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        StateHasChanged();
        return drawn.Task;
    }

    /// <summary>A Workbook opened afresh: nothing of a gesture on the tabs it replaced stands, nor a tab of its Sheets.</summary>
    private void ForgetTabGestures()
    {
        _tabs.Clear();
        _renaming = null;
        _renameEnding = false;
        _renameRefused = false;
        _renameSession++;
        _menu = null;
        _menuSheet = null;
        _menuSession++;
        _dragFrom = null;
        _dragListeners.Clear();
    }

    /// <summary>Any open edit — a cell's, or the Formula Bar's — refuses a Sheet operation, and the notice says why.</summary>
    private bool RefuseSheetOperationWhileEditing()
    {
        if (_grid?.ReadEditText() is null && !IsEditing) return false;
        Tell(SheetWords.SheetOperationWhileEditing);
        StateHasChanged();
        return true;
    }

    /// <summary>
    /// <paramref name="edit"/> done on <paramref name="sheet"/> as one step on the undo stack, showing
    /// <paramref name="show"/>'s Sheet — the step's own, for a Sheet added or copied — when asked. A refusal
    /// is said in the notice. Answers the step, or null when refused.
    /// </summary>
    private async Task<WorkbookStep?> DoSheetOperationAsync(Sheet sheet, SheetEdit edit, Func<WorkbookStep, Sheet>? show = null)
    {
        var workbook = CurrentWorkbook;
        // A Sheet taken away while the gesture was on its way — a Consumer's command, an undo — is not done to.
        if (!workbook.Sheets.Contains(sheet)) return null;
        WorkbookStep step;
        try
        {
            step = workbook.Do(sheet.Name, edit);
        }
        catch (SheetRefusedException refused)
        {
            Tell(SheetWords.Refused(refused.Refusal));
            StateHasChanged();
            return null;
        }
        _history.Record(step);
        if (show?.Invoke(step) is { } shown && workbook.Sheets.Contains(shown)) workbook.Shown = shown;
        await ChangedAsync([step.Change]);
        return step;
    }

    // ---- Adding --------------------------------------------------------------------------------

    /// <summary>A press on <c>+</c>: a Sheet after the one shown, as Excel's button adds one, in its place in the order.</summary>
    private Task AddSheetByUserAsync() => InOrderAsync(GridInputKind.Click, () => AddSheetAsync(before: false));

    /// <summary>
    /// A new, empty Sheet beside the one shown — after it, or before it as Shift+F11 adds one — named as
    /// Excel names it, and shown, as Excel shows the Sheet it adds. A name being edited is committed first.
    /// </summary>
    private async Task AddSheetAsync(bool before)
    {
        if (RefuseSheetOperationWhileEditing() || _sheet is not { } shown) return;
        await LeaveTabGesturesAsync();
        if (await DoSheetOperationAsync(shown, SheetEdit.InsertSheet(before: before), step => step.Sheet) is not { } step) return;
        Tell(SheetWords.SheetAdded(step.Sheet.Name));
        StateHasChanged();
    }

    // ---- Renaming ------------------------------------------------------------------------------

    /// <summary>A double click on a tab: its name is edited on the tab, in its place in the order.</summary>
    private Task RenameByUserAsync(Sheet sheet) => InOrderAsync(GridInputKind.DoubleClick, () => BeginRenameAsync(sheet));

    /// <summary>
    /// Edits <paramref name="sheet"/>'s name on its tab, its whole name selected, the keyboard handed to
    /// the field through the grid within this input, so the keys typed straight after it are the field's.
    /// </summary>
    private async Task BeginRenameAsync(Sheet sheet)
    {
        if (RefuseSheetOperationWhileEditing() || _workbook is not { } workbook || !workbook.Sheets.Contains(sheet)) return;
        if (_renaming == sheet) return;
        await LeaveTabGesturesAsync();
        if (sheet != _sheet)
        {
            ClearNotice();
            await ShowSheetAsync(sheet);
        }
        _renaming = sheet;
        _renameText = sheet.Name;
        _renameRefused = false;
        _renameSession++;
        await DrawnAsync();
        if (_renaming == sheet && _grid is { } grid) await grid.HandKeyboardToAsync(_renameBox, preventScroll: true);
    }

    // A name refused stays marked until it is changed: the change event Chrome raises for the Enter that
    // was refused, after its keydown, carries the same text and leaves the mark standing.
    private void SetRenameText(string? text)
    {
        text ??= "";
        if (string.Equals(text, _renameText, StringComparison.Ordinal)) return;
        _renameText = text;
        _renameRefused = false;
    }

    // Text the grid's key gate typed into the field while it held the keys (ADR-0210) arrives as a change.
    private void OnRenameChanged(ChangeEventArgs change) => SetRenameText(change.Value as string);

    /// <summary>Enter commits the name and Escape keeps the old one, each giving the keyboard back to the grid, in their place in the order.</summary>
    private Task OnRenameKeyAsync(KeyboardEventArgs key) => InOrderAsync(GridInputKind.KeyDown, async () =>
    {
        switch (key.Key)
        {
            case "Enter":
                await CommitRenameAsync(returnKeyboard: true);
                break;
            case "Escape":
                await EndRenameAsync(returnKeyboard: true);
                break;
        }
    });

    /// <summary>The keyboard has left the field: the name is committed, as Excel commits it when the user
    /// presses elsewhere, or, refused, the old one is kept and the notice says why. A focus leaving a field
    /// whose edit has already ended changes nothing.</summary>
    private Task OnRenameLeftAsync(int session) => InOrderAsync(GridInputKind.Focus, async () =>
    {
        if (_renaming is null || session != _renameSession) return;
        if (!await CommitRenameAsync(returnKeyboard: false)) await EndRenameAsync(returnKeyboard: false);
    });

    /// <summary>
    /// Commits the name typed: one step, every Formula naming the Sheet rewritten (ADR-0331 §4). A name
    /// Excel's rules refuse — empty, longer than 31 characters, holding one of <c>: \ / ? * [ ]</c>, or
    /// another Sheet's — is refused by name, and the field stays, holding the keyboard. Answers whether
    /// the edit ended.
    /// </summary>
    private async Task<bool> CommitRenameAsync(bool returnKeyboard)
    {
        if (_renaming is not { } sheet || _renameEnding) return true;
        if (!CurrentWorkbook.Sheets.Contains(sheet))
        {
            await EndRenameAsync(returnKeyboard);
            return true;
        }
        var name = _renameText;
        if (string.Equals(name, sheet.Name, StringComparison.Ordinal))
        {
            await EndRenameAsync(returnKeyboard);
            return true;
        }
        if (!Sheet.IsValidName(name, out var reason))
        {
            RefuseRename(reason!);
            return false;
        }
        var from = sheet.Name;
        WorkbookStep step;
        try
        {
            step = CurrentWorkbook.Do(from, SheetEdit.Rename(name));
        }
        catch (SheetRefusedException refused)
        {
            RefuseRename(refused.Refusal.Message);
            return false;
        }
        _history.Record(step);
        await EndRenameAsync(returnKeyboard);
        await ChangedAsync([step.Change]);
        Tell(SheetWords.SheetRenamed(from, sheet.Name));
        StateHasChanged();
        return true;
    }

    private void RefuseRename(string reason)
    {
        _renameRefused = true;
        Tell(SheetWords.SheetNotRenamed(reason));
        StateHasChanged();
    }

    /// <summary>Ends the name's edit: the keyboard goes back to the grid first, when asked, while the field still holds it, and the field then goes.</summary>
    private async Task EndRenameAsync(bool returnKeyboard)
    {
        // Ended once: an end asked again while the keyboard is on its way back — the Sheet taken away by an
        // undo meanwhile — finds it ending.
        if (_renaming is null || _renameEnding) return;
        _renameEnding = true;
        _renameSession++;
        if (returnKeyboard && _grid is { } grid) await grid.ReturnKeyboardAsync(_tabArea, static () => []);
        _renaming = null;
        _renameEnding = false;
        _renameRefused = false;
        StateHasChanged();
    }

    // ---- The tab's menu ------------------------------------------------------------------------

    /// <summary>A secondary press on a tab, or the context menu the browser raises for Shift+F10 or the menu key: the tab's menu, in its place in the order.</summary>
    private Task OpenTabMenuByUserAsync(Sheet sheet, GridInputKind kind) => InOrderAsync(kind, () => OpenTabMenuAsync(sheet, fromTab: false));

    /// <summary>
    /// Opens <paramref name="sheet"/>'s menu, showing the Sheet, with the keyboard on its first command
    /// that can be done, handed to the menu through the grid within this input. A menu already open for
    /// the Sheet stays as it is.
    /// </summary>
    private async Task OpenTabMenuAsync(Sheet sheet, bool fromTab)
    {
        if (_menuSheet == sheet) return;
        if (RefuseSheetOperationWhileEditing() || _workbook is not { } workbook || !workbook.Sheets.Contains(sheet)) return;
        await LeaveTabGesturesAsync();
        if (sheet != _sheet)
        {
            ClearNotice();
            await ShowSheetAsync(sheet);
        }
        _menuSheet = sheet;
        _menuFromTab = fromTab;
        _menuSession++;
        _menuActive = MenuOrder.First(kind => CommandEnabled(kind, sheet, out _));
        BuildTabMenu();
        await DrawnAsync();
        if (_menuSheet == sheet && _grid is { } grid) await grid.HandKeyboardToAsync(_menuElement, preventScroll: true);
    }

    /// <summary>The menu's commands as they stand: which can be done on its Sheet, and which the keyboard is on.</summary>
    private void BuildTabMenu()
    {
        if (_menuSheet is not { } sheet)
        {
            _menu = null;
            return;
        }
        var prefix = "ex-sheet-tab-menu-" + RuntimeHelpers.GetHashCode(this).ToString(CultureInfo.InvariantCulture);
        _menu = new SheetTabMenuContext(sheet.Name, [.. MenuOrder.Select(kind =>
        {
            var enabled = CommandEnabled(kind, sheet, out var reason);
            return new SheetTabCommand(prefix + "-" + kind.ToString().ToLowerInvariant(), kind, TabCommandLabel(kind), enabled, reason,
                kind == _menuActive, () => InOrderAsync(GridInputKind.Click, () => DoTabCommandAsync(kind)));
        })]);
    }

    private static string TabCommandLabel(SheetTabCommandKind kind) => kind switch
    {
        SheetTabCommandKind.Rename => "Rename",
        SheetTabCommandKind.Delete => "Delete",
        SheetTabCommandKind.MoveLeft => "Move Left",
        SheetTabCommandKind.MoveRight => "Move Right",
        _ => "Copy",
    };

    /// <summary>Whether <paramref name="kind"/> can be done on <paramref name="sheet"/> in the Workbook as it stands now, and why not.</summary>
    private bool CommandEnabled(SheetTabCommandKind kind, Sheet sheet, out string? reason)
    {
        reason = null;
        if (_workbook is not { } workbook) return false;
        var at = IndexOf(workbook, sheet);
        if (at < 0)
        {
            reason = SheetWords.SheetGone;
            return false;
        }
        reason = kind switch
        {
            SheetTabCommandKind.Delete when workbook.Sheets.Count == 1 => SheetWords.OnlySheet,
            SheetTabCommandKind.MoveLeft when at == 0 => SheetWords.FirstSheet,
            SheetTabCommandKind.MoveRight when at == workbook.Sheets.Count - 1 => SheetWords.LastSheet,
            _ => null,
        };
        return reason is null;
    }

    /// <summary>
    /// The menu's keys, in their place in the order: ↑ and ↓ move among the commands that can be done,
    /// round from the last to the first, Home and End to the first and the last, Enter or Space does the
    /// command, and Escape or Tab closes the menu, the keyboard going back to the tab it came from, or to
    /// the grid.
    /// </summary>
    private Task OnTabMenuKeyAsync(KeyboardEventArgs key) => InOrderAsync(GridInputKind.KeyDown, async () =>
    {
        if (_menuSheet is not { } menuSheet) return;
        var enabled = MenuOrder.Where(kind => CommandEnabled(kind, menuSheet, out _)).ToList();
        switch (key.Key)
        {
            case "ArrowDown":
            case "ArrowUp":
                var at = enabled.IndexOf(_menuActive);
                var step = key.Key == "ArrowDown" ? 1 : -1;
                _menuActive = enabled[((at < 0 ? 0 : at + step) % enabled.Count + enabled.Count) % enabled.Count];
                BuildTabMenu();
                StateHasChanged();
                break;
            case "Home":
            case "End":
                _menuActive = key.Key == "Home" ? enabled[0] : enabled[^1];
                BuildTabMenu();
                StateHasChanged();
                break;
            case "Enter":
            case " ":
                await DoTabCommandAsync(_menuActive);
                break;
            case "Escape":
            case "Tab":
            case "ContextMenu":
                await CloseTabMenuAsync(_menuFromTab ? KeyboardTo.Tab : KeyboardTo.Grid);
                break;
            case "F10" when key.ShiftKey:
                await CloseTabMenuAsync(_menuFromTab ? KeyboardTo.Tab : KeyboardTo.Grid);
                break;
        }
    });

    /// <summary>A click inside the menu that no command took — on its surface — is still an input in its place.</summary>
    private Task OnTabMenuClickAsync() => InOrderAsync(GridInputKind.Click, static () => Task.CompletedTask);

    /// <summary>The keyboard has left the menu for somewhere else: the menu closes, the keyboard staying where it went.</summary>
    private void OnTabMenuLeft(int session)
    {
        if (session != _menuSession || _menuSheet is null) return;
        _menuSession++;
        _menuSheet = null;
        _menu = null;
    }

    private enum KeyboardTo
    {
        Nowhere,
        Grid,
        Tab,
    }

    /// <summary>Closes the menu, handing the keyboard on first, while the menu still holds it.</summary>
    private async Task CloseTabMenuAsync(KeyboardTo keyboard)
    {
        if (_menuSheet is not { } sheet) return;
        // Closed at once, for every key and command after this one, while the menu stays drawn, holding the
        // keyboard, until the keyboard has gone on.
        _menuSheet = null;
        var session = ++_menuSession;
        if (_grid is { } grid)
        {
            if (keyboard == KeyboardTo.Grid) await grid.ReturnKeyboardAsync(_tabArea, static () => []);
            else if (keyboard == KeyboardTo.Tab && _tabs.TryGetValue(sheet, out var tab)) await grid.HandKeyboardToAsync(tab, preventScroll: true);
        }
        if (session == _menuSession) _menu = null;
        StateHasChanged();
    }

    /// <summary>
    /// The command <paramref name="kind"/> on the menu's Sheet: each one step, said in the notice, the
    /// keyboard going back to the grid — but for Rename, which hands it to the name's field. A command
    /// that cannot be done does nothing, and the menu stays.
    /// </summary>
    private async Task DoTabCommandAsync(SheetTabCommandKind kind)
    {
        if (_menuSheet is not { } sheet || !CommandEnabled(kind, sheet, out _)) return;
        if (kind == SheetTabCommandKind.Rename)
        {
            await CloseTabMenuAsync(KeyboardTo.Nowhere);
            await BeginRenameAsync(sheet);
            return;
        }
        await CloseTabMenuAsync(KeyboardTo.Grid);
        if (RefuseSheetOperationWhileEditing()) return;
        // The close waited on the browser, and the Workbook may have changed meanwhile — a Consumer's
        // command, an undo: the command is judged again on the Workbook as it stands now.
        if (!CommandEnabled(kind, sheet, out var reason))
        {
            Tell(SheetWords.SheetNotChanged(reason!));
            StateHasChanged();
            return;
        }
        var workbook = CurrentWorkbook;
        var name = sheet.Name;
        switch (kind)
        {
            case SheetTabCommandKind.Delete:
                if (await DoSheetOperationAsync(sheet, SheetEdit.DeleteSheet()) is null) return;
                Tell(SheetWords.SheetDeleted(name));
                break;
            case SheetTabCommandKind.MoveLeft:
            case SheetTabCommandKind.MoveRight:
                var to = IndexOf(workbook, sheet) + (kind == SheetTabCommandKind.MoveLeft ? -1 : 1);
                if (await DoSheetOperationAsync(sheet, SheetEdit.MoveSheet(to)) is null) return;
                Tell(SheetWords.SheetMoved(name, to + 1, workbook.Sheets.Count));
                break;
            case SheetTabCommandKind.Copy:
                if (await DoSheetOperationAsync(sheet, SheetEdit.CopySheet(), step => step.Sheet) is not { } copied) return;
                Tell(SheetWords.SheetCopied(name, copied.Sheet.Name));
                break;
        }
        StateHasChanged();
    }

    /// <summary>
    /// The Sheets have changed — by a gesture, a Consumer's command, an undo or a redo: a Sheet no longer
    /// in the Workbook has no tab and no view kept for it, a name being edited on its tab ends, the
    /// keyboard going back to the grid, and its menu closes the same way. A menu still standing is built
    /// again, so what it offers follows the Sheets as they stand.
    /// </summary>
    private async Task ForgetSheetsGoneAsync()
    {
        if (_workbook is not { } workbook) return;
        var sheets = workbook.Sheets;
        foreach (var gone in _tabs.Keys.Where(sheet => !sheets.Contains(sheet)).ToList()) _tabs.Remove(gone);
        foreach (var gone in _views.Keys.Where(sheet => !sheets.Contains(sheet)).ToList()) _views.Remove(gone);
        if (_renaming is { } renaming && !sheets.Contains(renaming)) await EndRenameAsync(returnKeyboard: true);
        if (_menuSheet is { } menuSheet)
        {
            if (!sheets.Contains(menuSheet)) await CloseTabMenuAsync(KeyboardTo.Grid);
            else
            {
                if (!CommandEnabled(_menuActive, menuSheet, out _)) _menuActive = MenuOrder.First(kind => CommandEnabled(kind, menuSheet, out _));
                BuildTabMenu();
            }
        }
    }

    /// <summary>A name being edited is committed — or, refused, kept as it was — and a menu closes, before another gesture on the tabs.</summary>
    private async Task LeaveTabGesturesAsync()
    {
        if (_renaming is not null && !await CommitRenameAsync(returnKeyboard: false)) await EndRenameAsync(returnKeyboard: false);
        if (_menuSheet is not null) await CloseTabMenuAsync(KeyboardTo.Nowhere);
    }

    // ---- Dragging ------------------------------------------------------------------------------

    /// <summary>A press of the main button on a tab notes where a drag starts; nothing is done yet.</summary>
    private void StartTabDrag(MouseEventArgs press, Sheet sheet)
    {
        if (press.Button != 0 || press.CtrlKey || press.ShiftKey || press.AltKey || press.MetaKey)
        {
            EndTabDrag();
            return;
        }
        _dragFrom = sheet;
        // Heard only while a drag is under way: leaving the tabs ends it, moving nothing.
        _dragListeners["onmouseleave"] = (Action)EndTabDrag;
    }

    private void EndTabDrag()
    {
        _dragFrom = null;
        _dragListeners.Clear();
    }

    /// <summary>The release on a tab: the tab dragged moves to that tab's place, and its Sheet is shown.</summary>
    private Task DropOnTabAsync(MouseEventArgs release, Sheet target)
    {
        var from = _dragFrom;
        EndTabDrag();
        if (from is null || from == target || release.Button != 0) return Task.CompletedTask;
        return InOrderAsync(GridInputKind.Press, () => MoveByDragAsync(from, target));
    }

    /// <summary>The release past the last tab, on the <c>+</c> button or the strip's empty end: the tab dragged moves to the last place.</summary>
    private Task DropOnTabAreaAsync(MouseEventArgs release)
    {
        var from = _dragFrom;
        EndTabDrag();
        if (from is null || release.Button != 0) return Task.CompletedTask;
        return InOrderAsync(GridInputKind.Press, () => MoveByDragAsync(from, null));
    }

    private async Task MoveByDragAsync(Sheet sheet, Sheet? target)
    {
        if (RefuseSheetOperationWhileEditing() || _workbook is not { } workbook || !workbook.Sheets.Contains(sheet)) return;
        if (target is not null && !workbook.Sheets.Contains(target)) return;
        await LeaveTabGesturesAsync();
        var to = target is null ? workbook.Sheets.Count - 1 : IndexOf(workbook, target);
        if (to == IndexOf(workbook, sheet))
        {
            if (sheet != _sheet) await ShowSheetAsync(sheet);
            return;
        }
        var name = sheet.Name;
        if (await DoSheetOperationAsync(sheet, SheetEdit.MoveSheet(to), _ => sheet) is null) return;
        Tell(SheetWords.SheetMoved(name, to + 1, workbook.Sheets.Count));
        StateHasChanged();
    }
}
