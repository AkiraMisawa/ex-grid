# 51: Excel's formatting keys

Status: done

**What to build:** [ADR-0050](../../../adr/0050-what-exsheet-asks-of-exgrids-core.md) item 14, and
[ADR-0063](../../../adr/0063-a-sheets-cell-format-is-document-data-painted-on-white-paper.md), "Keys".

**Blocked by:** None (can start immediately). Ticket 50 is done, and the eleventh Windows run has
answered. Its record is `verification/2026-10-01-windows-excel-11/cell-format.md` with `../2026-10-01-windows-browser-11/keys.md`; ADR-0063 "What the
eleventh Windows run settled" sums it up.

- [x] **Core: declared keys** (DC-57).
  - A declared key is claimed and raised together with whether an edit is open.
  - An undeclared key stays the browser's.
  - A declaration naming a key the core answers itself is refused by name.
- [x] **ExSheet declares the keys the run found in Excel and in the page** (SH-42).
  - Each applies what Excel applies under the Sheet's culture, as one undo step (cases 18, 20):
    `~` General, `!` `#,##0.00`, `@` `h:mm` (`h:mm AM/PM` under en-US), `#` `d-mmm-yy`, `$` the
    built-in currency format localised by the culture (recorded as a culture-localised built-in, as
    the built-in short date is), `%` `0%`, `^` `0.00E+00`; `&` outlines each range, `_` clears every
    edge of the Selection and the same edges read from outside it (case 13).
  - B and 2 bold, I and 3 italic, U and 4 single underline, 5 strikethrough; each toggles, and the
    direction follows the Focus cell (cases 16, 17).
  - Keys are matched by the character they type (`KeyboardEvent.key`) with Ctrl, and whatever Shift
    the layout needs: on a UK layout `#` comes without Shift (case 19; Part B case 29).
- [ ] **Ctrl+1** opens Format Cells once ticket 52 is done. Until then it is not claimed.
- [x] **While an edit is open**, a formatting key changes nothing (SH-43).
  - It is announced through the live region with its reason, and raised to the Consumer.
  - Ctrl+U is claimed, so the page's source does not open.
  - Ctrl+1 is one of them: Excel opens a Font-only Format Cells there, for rich text ExSheet does
    not have (case 21).
- [x] Layer 2; Layer 3 for Ctrl+U and for the toggles.

## Comments

*(2026-10-01, built.)* ADR-0050 item 14 in the core, and Excel's formatting keys on ExSheet.

- **Core: declared keys** (DC-57).
  - `ExGrid.DeclaredKeys` (`IReadOnlyCollection<string>?`) names keys in `GridKeys.Canonical`'s
    form, matched exactly. `ExGrid.OnDeclaredKey` (`EventCallback<GridDeclaredKeyPress>`) raises
    a pressed one as `GridDeclaredKeyPress(string Key, bool EditOpen)`. The grid moves nothing,
    opens nothing, and leaves an open edit as it was.
  - `GridKeys.Declare(IEnumerable<string>?)` is the pure check, run when the parameter changes. It
    refuses by name, with what the core does with the key: every key of the core's table (claimed
    always or only on some grids), Ctrl+Enter and F4 (the editor's), typing (a character or F2
    with nothing but Shift; Control with Alt, which is AltGr), and the clipboard's Ctrl+C, Ctrl+V,
    Ctrl+Insert and Shift+Insert. A key not in the canonical form is refused too, since no press
    would match it. Declared keys without `OnDeclaredKey` are refused by name: they would be taken
    from the page for nothing.
  - The gate is handed the declared keys with the core's own, at attach and through `setClaims`
    (one more argument each). With no edit open they are among the taken keys. While an edit is
    open the editing branch claims them as `'core'`: no mode change, nothing held after them. The
    module names no formatting key of its own, and no listener was added (ADR-0021).
  - A declared key is claimed only on the root or in an editor surface, as every other key is. A
    control inside the grid, the Name Box among them, keeps its own keys.
- **ExSheet** declares `SheetFormatKeys.Declared`, 32 canonical forms built from one table of
  characters.
  - A letter is claimed in both cases, for CapsLock, and without Shift: Ctrl+Shift+U is another key
    in Excel. Every other character is claimed with Shift and without it, whichever its layout
    needs, so `#` on a UK layout is the date (case 19).
  - The toggles read the Focus cell through `CellFormatAt` (case 17).
  - `@` is `h:mm AM/PM` where the culture's own short time is 12-hour (en-US) and `h:mm`
    otherwise (en-GB, ja-JP).
  - `&` is a thin, Automatic outline per range. `_` is `BorderChange.None`. Neither test asserts
    the neighbour's side, which is ticket 55's.
  - Each key goes through `SetCellFormatAsync`: one undo step.
- **The currency built-in.** `NumberFormat.BuiltInCurrency(CultureInfo)` answers Excel's format 8,
  `$#,##0.00_);[Red]($#,##0.00)`, or format 6, `$#,##0_);[Red]($#,##0)`, where the culture's
  currency has no decimals, as Excel chose under ja-JP.
  - The Sheet Document records that invariant code. `NumberFormat.Format` shows it in the
    culture's own form, beside the built-in short date: `£1,234.50` / `-£1,234.50` under en-GB,
    `¥1,235` / `-¥1,235` under ja-JP, and the code itself under en-US. The negative section is
    red.
  - The local form is built from the culture's currency symbol and its positive and negative
    patterns. A symbol holding letters is quoted.
  - **en-US's negative pattern is set to parentheses by hand.** .NET's ICU data says `-$n`;
    Windows' regional default, which Excel follows, says `($n)`, as case 20 shows. en-GB and ja-JP
    agree with .NET's data. Other cultures are derived from .NET's data and are not yet observed.
- **While an edit is open** (SH-43), a formatting key changes nothing. ExSheet says
  `SheetWords.FormatKeyWhileEditing` in its notice, the `role="status"` region through which it
  says every refusal, and raises the new `ExSheet.OnFormatKeyRefused`
  (`EventCallback<SheetRefusal>`, `SheetRefusalReason.EditIsOpen`). ExSheet's own `IsEditing`
  counts as well as the grid's word, for the render after a parameter change discarded an edit.
- **Ctrl+1** is not claimed. Once ticket 52's `OpenFormatCellsAsync` exists, it is one row in
  `SheetFormatKeys.Keys`, `('1', Kind.FormatCells)` with a `FormatCells` member in `Kind`, and
  one branch in `ExSheet.OnFormatKeyAsync`, which calls `OpenFormatCellsAsync()` for that kind in
  place of `SetCellFormatAsync`. The refusal while an edit is open comes before that branch, so
  Ctrl+1 is refused like the others with nothing more written. On AZERTY, Ctrl+Shift+1 types `1`;
  the table claims it, as Excel reads by character.
- **The DemoHost's `/sheet`** shows, under the Sheet, the Focus cell's Font and Number Format read
  back through `CellFormatAt`, until ticket 48 paints the Font and ticket 54's toolbar replaces
  the line. The page says a refused key in its status line, as it says a refused command.
- **Seen in the run's record and not built.** Each needs an observation or a decision first.
  - Case 18 says Excel widened column A to 8.73 for the date, while `$` under en-US showed
    `########` at the standard width. ExSheet widens a column on an entry, never on a format
    change.
  - `NumberFormatLocal` reads `hh:mm` for `@` and `dd-mmm-yy` for `#` under en-GB, which suggests
    Excel localises the built-ins 20 and 15 as it does 16. The run's samples (`12:00`,
    `18-May-03`) cannot tell. ExSheet shows them as their codes spell them.
- **Tests.** Layer 1: `ExGrid.Tests` 857 (31 new, `DeclaredKeyTests`); `ExSheet.Engine.Tests`
  2091 (10 new, in `CultureTests`). Layer 2: `ExGrid.Components` 1073 and 1 skipped (11 new:
  `DeclaredKeyTests`, and one inspection of the module in `ShippedStylesheetTests`, whose
  `setClaims` pattern gained the new argument); `ExSheet.Components.Tests` 359 (49 new,
  `FormatKeyTests`); `ExGrid.MudBlazor.Tests` 88.
  Layer 3: `format-keys.spec.mjs`, 4 of 4 on Chrome, headless on macOS, on the WebAssembly host.
  It covers the toggles, the toggle over a range, the Ctrl+Shift characters, and Ctrl+U with an
  edit open. Edge and the Server host are left to CI.


*(2026-10-01, a fix found on the Server host.)* `format-keys.spec.mjs`, "the toggle follows the
Focus cell over a range", failed every time on the Server host. Ctrl+B pressed straight after
Shift+ArrowUp bolded the Focus cell alone.

- **The cause.** The grid raises `SelectionChanged` from after the render that shows a move. On a
  circuit, that is a round trip later, so the next key reached ExSheet first. `OnFormatKeyAsync`
  read the Selection ExSheet had last heard, and formatted fewer cells than were selected,
  saying nothing. Ctrl+1 opened Format Cells over that same stale Selection.
- **The fix.**
  - `GridDeclaredKeyPress` carries the Selection as the grid holds it at the key, and the Row
    Sequence Version it is written in: `GridDeclaredKeyPress(string Key, bool EditOpen,
    GridSelection Selection, int RowSequenceVersion)`.
  - ExSheet adopts that Selection before it formats or opens Format Cells.
  - The late `SelectionChanged` then names the Selection ExSheet already holds. ExSheet ends
    Format Cells only for a Selection that moved: the same notification would otherwise close the
    Format Cells Ctrl+1 had just opened.
- **Layer 2.**
  - In the core, a Range Request the Consumer has not answered holds the selection notification
    back, as a circuit does. Ctrl+B then carries the extended Selection before the Consumer has
    heard it.
  - In ExSheet, a key carries a Selection ExSheet has not heard. Ctrl+B formats it, Ctrl+1 opens
    over it, and the late notification leaves Format Cells open. Both tests fail without the fix.
- **Layer 3.** `format-keys.spec.mjs` and `format-cells.spec.mjs` on Chrome, headless: 14 of 14 on
  each host.
- **Still read from the last Selection heard.** The same race reaches these, and none has a
  one-line fix:
  - A Consumer's own commands (`SetCellFormatAsync` and its shorthands, `OpenFormatCellsAsync`)
    act on the Selection ExSheet last heard. A button pressed within a round trip of a keyboard
    move would act on the earlier one.
  - The Context Menu's "Format Cells…". Its context carries the ranges but not the Focus.
  - Grouping the columns of a whole-column resize into one undo step.
