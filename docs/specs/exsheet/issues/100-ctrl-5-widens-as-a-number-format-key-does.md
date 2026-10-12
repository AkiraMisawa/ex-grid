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

*(2026-10-12, agent run17gh-widening.)* The seventeenth Windows run corrects the second box for
Ctrl+U (`verification/2026-10-11-windows-17/report.md`, Part H, h7). Each key was pressed in a
workbook of its own. Over 123456789.12 in `0.00` showing `########` at the standard width, Excel's
Ctrl+U widened column A from 8.09 to 11.73, and Ctrl+I did not. Over 123456.78 in `0.00` showing
`#####` at a width set through COM (5), none of the three did, Format Cells' Font tab among them.
Its h8 observed Ctrl+5 widen column A over 123456789.12 from 8.09 to 11.73, and not at the set
width.

- **Ctrl+U now widens as Ctrl+5 does**, by this ticket's path. `WidensAsANumberFormatDoes` is true
  for underline as for strikethrough, so Ctrl+4, the same key, widens too.
  - It widens a column at the standard width, or one an entry or a key widened, and never one the
    user sized.
  - Setting underline and taking it off widen alike. Taking it off is a reading, as for Ctrl+5.
  - The width steps are part of the key's one undo step.
- **Ctrl+B and Ctrl+I still never widen.** h7 observed Ctrl+I not to.
- **Format Cells' OK, `SetCellFormatAsync` and the Sheet Toolbar's toggles are unchanged.** In h7,
  Excel widened for the Font tab with Bold and OK, where Ctrl+B did not widen in the fourteenth run.
  What follows from that waits for the user's question.
- **Tests.**
  - Layer 2 (`FormatWideningTests`, each named with h7):
    - Ctrl+U, with CapsLock off and on, and Ctrl+4 widen, and the column is recorded with the
      entry's kind.
    - Ctrl+I then Ctrl+U on one cell. The run pressed each in a workbook of its own, so the two
      together are a reading.
    - Taking underline off widens.
    - One undo step, undone and redone.
    - A sized column, at the default width or at h7's 5, does not widen.
  - `Bold_italic_underline_a_fill_and_a_border_never_widen` loses underline, and is now
    `Bold_italic_a_fill_and_a_border_never_widen`.
  - With the change taken out, six of the new tests fail.
  - Layer 3 (`format-keys.spec.mjs`): Ctrl+I and Ctrl+U over `####` in a standard column and a
    column sized by its grip, and one Ctrl+Z.

*(2026-10-12, agent run17gh-widening, second part.)* The user decided what follows from h7's Font
tab: Format Cells' OK widens for any part of the Font it sets (Bold observed, the rest a reading
then), `SetCellFormatAsync` follows Format Cells' OK, and each Sheet Toolbar toggle widens as the key
it names. The readings of 2026-10-12 (`verification/2026-10-12-windows-readings/report.md`, R6) have
since observed the Font tab widen for Italic, Underline and a colour each set alone, the Fill and
Alignment tabs not widen, and the Home tab's Bold, Italic and Underline buttons act as their keys.

- **One rule per kind of command**, in `SheetWidening`. `SheetFormatKeys.WidensAsANumberFormatDoes`
  is gone.
  - `WidensAsAKeyDoes`: a Number Format, underline or strikethrough. The keys use it, and so does
    `SetCellFormatFromFocusAsync`, the public command the toolbar's toggles set through (ADR-0100), so
    Underline and Strikethrough widen and Bold and Italic do not.
  - `WidensAsFormatCellsOkDoes`: a Number Format or any part of the Font. Format Cells' OK uses it,
    under both Chromes, since both reach `OkFormatCellsAsync`, and so does `SetCellFormatAsync`.
  - The toolbar's Font Colour face sets through `SetCellFormatAsync`, so it widens too. That Excel's
    Font Color button does is a reading.
  - A Fill, Borders and an alignment widen nothing under either rule.
  - The widening is this ticket's path, unchanged: never a column the user sized, and in the action's
    one undo step.
- **Tests.**
  - Layer 2 (`FormatWideningTests`):
    - Format Cells' OK widens for Bold, Italic, Underline, a colour, Strikethrough and Normal font,
      and one undo takes back both. The Fill, Alignment and Border tabs do not widen.
    - Format Cells' OK with Bold leaves a sized column, at the default width and at 5.
    - `SetCellFormatAsync` widens for each Font part, and not for a Fill, an alignment or Borders. It
      leaves a sized column.
    - `SetCellFormatFromFocusAsync` widens for underline, and not for bold and italic.
    - Each toolbar toggle widens as its key does, in one undo step, and none widens a sized column.
    - The Font Colour face widens, and the Fill Colour face does not.
  - Layer 2 under the MudBlazor Chrome (`MudFormatCellsTests`): OK with Bold widens, and OK with a
    Fill does not.
  - `Strikethrough_from_format_cells_or_set_cell_format_never_widens_case_14_7` asserted the old
    reading, and is gone. The theories above hold the opposite.
  - With both rules made "a Number Format only", and Bold added to the keys' rule, 17 tests fail in
    `ExSheet.Components` and 1 in `ExSheet.MudBlazor.Tests`.
  - Layer 3:
    - `format-cells.spec.mjs` and `format-cells-mud.spec.mjs`: OK with Bold over `####` widens a
      standard column, leaves one sized by its grip, and one Ctrl+Z takes it back. OK with a Fill
      widens nothing.
    - `sheet-toolbar.spec.mjs`, under both Chromes: Italic and Bold widen nothing. Underline and
      Strikethrough widen, each taken back by one Ctrl+Z.
    - The set-up is `hashesAtTheStandardWidth` in `sheet-helpers.mjs`, which `format-keys.spec.mjs`'s
      Ctrl+U test now uses too.
  - **Layer 3 run**, Playwright's Chromium, headed under xvfb, `--project=chrome`:
    - The four spec files on each host: 51 passed on the Server host, and 51 on WebAssembly.
    - The five new or rewritten tests with `--repeat-each=3`: 15 passed on each host.
  - Layers 1 and 2: ExSheet.Components.Tests 784 passed, ExSheet.MudBlazor.Tests 63, and
    ExSheet.Engine.Tests 4762, with 3 skipped.

*(2026-10-12, agent run17gh-widening, third part.)* The readings of 2026-10-12 (R6), case (5), set
`Range.Font` through COM over 123456789.12 in `0.00` showing `########` at the standard width:
`Font.Bold = True` left column A at 8.09, and `Font.Underline = 2` widened it to 11.73, as the keys
do. *(2026-10-12, decided with the user: `SetCellFormatAsync` widens as the formatting keys do, not
as Format Cells' OK does.)* This replaces the second part's `SetCellFormatAsync` rule.

- **`SetCellFormatAsync` takes `WidensAsAKeyDoes`**: a Number Format, underline or strikethrough
  widens, and bold, italic and a Font colour widen nothing. Only Format Cells' OK keeps
  `WidensAsFormatCellsOkDoes`.
- **The Sheet Toolbar's Font Colour widens nothing**, its face or a colour from its list, since both
  set through `SetCellFormatAsync`. R6, case (3), pressed the Home tab's Bold, Italic and Underline
  over `########`, and not Font Color, so that Excel's Font Color widens nothing is a reading.
- **An italic number is widened to the width the upright number takes**, as before. R6, case (1),
  saw Excel's Font tab widen column A to 12.45 for Italic alone, against 11.73 for a single underline
  or a colour alone. ADR-0071 judges italic by the regular widths, so this is a difference that
  follows it, recorded as a known limitation of `0.1.0` (proposed for ADR-0202), and not a change.
- **The Border keys, Ctrl+Shift+& and Ctrl+Shift+_, widen nothing**, as before: a reading, since no
  run has pressed them over `####`. The fourteenth run's Border tab (case 9) was over a General number
  shown rounded, `1234568`, not over `####`.
- **Tests.**
  - Layer 2 (`FormatWideningTests`):
    - `Set_cell_format_widens_as_the_keys_do_r6` replaces `Set_cell_format_widens_as_format_cells_ok_does`:
      underline, strikethrough, and bold with underline widen, the last to the bold widths; bold,
      italic, a colour, bold with italic, a Fill, an alignment and Borders do not.
    - `Bold_widens_through_format_cells_ok_and_not_through_set_cell_format_h7_r6`: one change, two
      commands, side by side.
    - `Neither_colour_face_nor_a_colour_from_its_list_widens` replaces
      `The_font_colour_face_widens_and_the_fill_colour_face_does_not`; Underline then widens the same
      cell.
    - With `SetCellFormatAsync` given the OK rule back, 6 tests fail.
  - Layer 3 (`sheet-toolbar.spec.mjs`, under both Chromes): the Font Colour face, a colour from its
    list and the Fill Colour face widen no column over `####`; Underline then widens the one at the
    standard width and leaves the one sized by its grip.
