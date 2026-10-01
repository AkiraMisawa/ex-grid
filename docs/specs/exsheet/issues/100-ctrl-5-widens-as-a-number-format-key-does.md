# 100: Ctrl+5 widens as a Number Format key does

Status: done

**What to build:** the fourteenth Windows run's case 7. With A1 showing `########` at the standard
width, Ctrl+5 (strikethrough) widened column A to fit, as a Number Format key does. Ctrl+B and the Fill
did not, in any of three orders. ADR-0071, "What the fourteenth Windows run settled", Widening.

**Blocked by:** None (can start immediately)

- [x] **Ctrl+5 widens a column exactly as a Number Format key does** (ticket 58's rule): at the
      standard width, or left by an entry or a key; never a column the user sized.
- [x] **Ctrl+B, Ctrl+I, Ctrl+U, a Fill, a Border and Format Cells' Font tab still never widen.** The run
      asked only Ctrl+B and the Fill; the rest stay as Ctrl+B is (ADR-0071).
- [x] **Removing strikethrough with Ctrl+5 widens the same way** if the text no longer fits. *(A
      reading: the run pressed it only to set.)*
- [x] **One undo step** takes back the strikethrough and the width together.
- [x] **Layer 1/2.**

## Comments

*(2026-10-02, agent cf-100.)* Built in ExSheet. The engine is unchanged.

- **Ctrl+5 widens.**
  - `SheetFormatKeys.WidensAsANumberFormatDoes` names the keys that widen although they set no
    Number Format. Ctrl+5 is the only one.
  - `ExSheet.OnFormatKeyAsync` passes that to `FormatSelectionAsync`. It now widens over the
    Selection when the change sets a Number Format or the key widens.
  - The widening is ticket 58's `SheetWidening.Of`, unchanged. It widens a column at the standard
    width, or one an entry or a key widened, and never one the user sized. It widens to the text as
    painted, bold where the cell is, and records the entry's kind (SH-26).
  - The width steps join the key's step in `PerformAsync`, so one undo takes back both.
- **Setting and taking off widen alike.** The toggle's direction does not change the path.
- **What still never widens.** Ctrl+B, Ctrl+I, Ctrl+U, the Border keys, and Format Cells' OK or
  `SetCellFormatAsync` with a Font, Fill or Border change and no Number Format.
- **A reading built, not observed: `SetCellFormatAsync` with only strikethrough widens nothing.**
  - The ADR names only the key, and reads Format Cells' Font tab as Ctrl+B.
  - `SetCellFormatAsync` sets a Font as that tab does, and both reach `FormatSelectionAsync` with no
    key. Its doc comment says so.
  - A run that sets `Font.Strikethrough` through COM over a `####` number would settle it.
- **Tests.**
  - **Layer 2** (`FormatWideningTests`, each named with case 14-7):
    - Ctrl+5 widens, and the column is recorded with the entry's kind.
    - The run's passes b to d, in their three orders, with the width checked after each step.
    - Taking strikethrough off widens.
    - One undo step, undone and redone.
    - A column the user sized does not widen.
    - Format Cells' Font tab and `SetCellFormatAsync` do not widen.
  - `Only_a_number_format_widens` is now `Bold_italic_underline_a_fill_and_a_border_never_widen`. It
    also presses Ctrl+I, Ctrl+U and Ctrl+Shift+_, and sets a Fill.
  - With the change taken out, six of the new tests fail.
  - **No layer 1.** The rule lives in ExSheet, and the engine did not change.
  - **Layer 3.** No spec asserted that a Font key never widens, so none changed. It was not run, as
    briefed.
  - **Layers 1 and 2** (`dotnet test ExGrid.slnx`, with ticket 101's change too):
    - ExSheet.Engine.Tests: 2338 passed.
    - ExSheet.Components.Tests: 605 passed.
    - ExSheet.MudBlazor.Tests: 44 passed.
    - ExGrid.Tests: 1089 passed.
    - ExGrid.Components: 1356 passed, 1 skipped.
    - ExGrid.MudBlazor.Tests: 198 passed.
