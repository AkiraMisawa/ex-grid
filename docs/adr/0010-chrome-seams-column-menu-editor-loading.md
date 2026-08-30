# The remaining Chrome — column menu, cell editor, loading indicator

Following [ADR-0009](./0009-filter-panel-contract.md), which fixed the filter panel contract,
this settles the remaining three substitutable UI seams (`IGridChrome`). One rule throughout:
**Chrome renders and calls back; it does not decide meaning.**

## Column menu — the core decides the items

```csharp
public sealed record ColumnMenuContext(
    ColumnInfo Column,
    IReadOnlyList<GridCommand> Commands,   // the core decides; Chrome only lays them out
    Action Close);

public sealed record GridCommand(string Id, string Label, bool Enabled, Action Invoke);
```

**Chrome does not decide what goes in the menu.** Letting it do so would mean the default and the
MudBlazor implementations offering different items, which breaks the premise that substituting
Chrome does not change behaviour.

The core's standard commands: sort ascending/descending, open the filter, hide this column, pin
the column, size the column to fit. **All of them move View State, so `Invoke` ultimately
notifies the Consumer** ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)).

**The Consumer can add its own commands.** Something like "open the report for this book" will
come up. It is added by the Consumer, not by Chrome — Chrome still only lays them out.

## Loading indicator — receive and render

```csharp
public sealed record LoadingContext(bool IsLoading, int PlaceholderRowCount);
```

**Placeholders are one mechanism** (waiting for data and deliberate skipping during fast
scrolling are the same thing,
[ADR-0004](./0004-cap-the-cells-touched-per-frame.md)), so Chrome does not need to distinguish
them either.

## Cell editor — never inside the row; one floating input

An `<input>` must not go inside a row. It breaks row memoisation
([ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md)) and adds DOM weight.
Use **one input floated over the focused cell** — the same idea as the selection overlay
([ADR-0008](./0008-selection-is-painted-by-an-overlay.md)), and the coordinate arithmetic can be
shared.

```csharp
public sealed record CellEditorContext(
    ColumnInfo Column,
    object? InitialValue,      // in Caret, the current value; in Overwrite, the character typed
    CellEditMode Mode,         // Overwrite / Caret
    Action<object?> Commit,
    Action Cancel);
```

### Two editing states

Excel has two editing states, and **the same arrow key means different things in each**.

| State | Entered by | Original value | Arrow keys |
|---|---|---|---|
| **Overwrite** | typing straight onto a selected cell | replaced | **commit and move to the neighbouring cell** |
| **Caret** | F2, or a double click | kept, with the caret inside it | **move the caret within the text** |

F2 moves between the two.

Many grid products do not implement this and behave as if always in Caret. Continuous entry —
"type a value, arrow to the next cell" — then does not work, and anyone coming from Excel
notices. This component **claims Excel-like operability, and this is what that claim is made of**,
so it is implemented.

Rejected: **Caret only** — simpler, and the arrows always belong to the editor, but continuous
entry does not work. That is lowering the claim.

### The core arbitrates the keys; Chrome is not trusted with it

If the editor is a component such as a `MudTextField`, it handles keys itself. Left alone, the
mode-dependent meaning of the arrow keys cannot work.

**The core sees key input first, in the capture phase.** A capture-phase `keydown` listener is
attached to the grid root, and keys the core should handle are taken there and `preventDefault`-ed.
Only the keys the core decides to pass reach the editor.

| Key | Handled by |
|---|---|
| Esc / Enter / Tab | **always the core** (cancel, commit, move) |
| Arrows / Home / End | **the core in Overwrite** (commit and move); **the editor in Caret** (move the caret) |
| Everything else | the editor |

Bubbling is too late — once the editor has handled the key and moved the caret, it cannot be taken
back. **The capture phase is the point.**

This approach **requires no cooperation from Chrome.** A contract of the form "the editor forwards
keydown to the core" would make behaviour depend on whether each Chrome implements the forwarding,
which breaks the rule above.

## Consequences

- **The core carries a small amount of JavaScript.** A capture-phase listener can only be attached
  through JS interop. Reading scroll position and the clipboard need it anyway, so this is not a
  new dependency — and the complete list of permitted JS is fixed in
  [ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md).
- **The editor sits outside the cell, so it will not match the cell exactly.** Its font, line
  height and padding must be pulled from the cell's CSS variables. A mismatch makes the text jump
  at the moment editing starts.
- **The editor's position uses the same coordinate system as the selection overlay.** This is
  another reason the row height must go through a C# parameter — changing it in CSS alone
  displaces the editor.
- **`GridCommand.Id` is a stable identifier.** It is the hook for Chrome to assign icons per
  command, and for a Consumer to substitute a particular one.
- **Do not lose the character typed in Overwrite mode.** The first character is pressed before the
  editor exists. The core holds it and passes it as `InitialValue`.
- **There is a third mode, Interactive**, for cells whose content is interactive
  ([ADR-0020](./0020-action-and-template-columns.md)). It rides on the same capture-phase key
  handling.
