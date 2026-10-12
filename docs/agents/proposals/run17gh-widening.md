# Run 17 Part H, widening: what the orchestrator applies

Branch `claude/run17gh-widening`, two commits:

- **fb91016d**: Ctrl+U widens as Ctrl+5 does (h7).
- **The second**: the user's decision (3) of 2026-10-12, built.
  - Format Cells' OK widens for any part of the Font it sets, under both Chromes.
  - `SetCellFormatAsync` widens as Format Cells' OK does.
  - Each Sheet Toolbar toggle widens as the key it names.
  - The verifier's text fixes are in this commit too.

What is on the branch: the code, layer 2 and layer 3 tests, the dated comments on ticket 100, three
Change Notes, `src/ExSheet/README.md` and `tests/ExGrid.Browser/README.md`. Each item below is text for
a file a background agent does not change (an ADR, the Docs Site). Each is written as exact old -> new
whole lines, with where it goes. Item 4 of the first version of this file (three remarks in
`ExSheet.razor`) and item 5 (the Underline item's summary) are now on the branch, rewritten for the
decision, and are dropped here.

**The sources.**

- **The run's record**: `verification/2026-10-11-windows-17/report.md` on
  `claude/exsheet-windows-verify-17`, Part H, rows h7 and h8, with `records/part-h-excel.jsonl` and
  `excel/part_h_excel.py`. Each key and the Font tab ran in a workbook of its own.
  - **The set-width cases** held 123456.78 in `0.00` showing `#####` at width 5, set through COM.
  - **The standard-width cases** held 123456789.12 in `0.00` showing `########` at 8.09.
- **The readings of 2026-10-12**: `verification/2026-10-12-windows-readings/report.md`, section R6,
  with `summary.md` and `records/r6.jsonl`. They are on `claude/exsheet-windows-readings-2026-10-12`
  at 89e0b5ad, which `main` does not hold yet.
  - **R6 answered every question the brief listed for the next run**: the Font tab with Italic,
    Underline or a colour alone; the Fill and Alignment tabs; the Home tab's B, I and U buttons;
    `Range.Font` through COM, for Bold and Underline.
  - **So the ADR text below names those parts observed, citing R6.** Only what R6 did not reach is
    left a reading and asked next run.
  - If the orchestrator would rather not cite R6 before the readings branch merges, each R6 line
    below goes back to "a reading", and its question goes back into the list at the end. The
    procedure is R6's own, `excel/r6.py`.

## 1. ADR-0071: a section "What the seventeenth Windows run settled"

Insert before `## Consequences` (line 852 on this branch, at a533de01), after the section "What was
decided after Part C":

```markdown
## What the seventeenth Windows run settled *(2026-10-12)*

The run was made at 8436e45e. Its record is `verification/2026-10-11-windows-17/report.md`, Part H,
rows h7 and h8. It asked what the fourteenth run left "as Ctrl+B is, until a run asks". The readings
of 2026-10-12 asked what h7 raised (`verification/2026-10-12-windows-readings/report.md`, R6). Every
answer is Excel's (ADR-0047's rule). Each part below says which of them observed it, or that it is a
reading.

**What the run observed.** Each case was a workbook of its own at 100% zoom. The format and then the
number were set through COM, and then one gesture was made.
- **At the standard width**, over 123456789.12 in `0.00` showing `########` at 8.09, a width nobody
  set:
  - **Ctrl+U widened column A to 11.73** (h7).
  - **Ctrl+I did not widen it** (h7). The number showed `#######` in italic.
  - **Format Cells' Font tab, with Bold and OK, widened it to 11.73** (h7). Ctrl+B did not widen in
    the fourteenth run's case 7.
  - **Ctrl+5 widened it to 11.73** (h8), as case 7 read.
- **At a width set through COM**, over 123456.78 in `0.00` showing `#####` at width 5, nothing
  widened it (h7, h8): not Ctrl+I, Ctrl+U, the Font tab with Bold or Ctrl+5. A column the user sized
  never widens.

**What the readings observed** (R6). The same set-up at the standard width, each case a workbook of
its own:
- **Format Cells' Font tab widened the column** with Italic alone, to 12.45. With a single Underline
  alone, or a colour (Red) alone, it widened to 11.73.
- **The Fill tab and the Alignment tab did not widen it**, untouched or changed (Yellow; Vertical:
  Top).
- **The Home tab's buttons acted as the keys.** Bold and Italic did not widen it, and Underline
  widened it to 11.73.
- **Ctrl+B pressed twice** did not widen it, after either press.
- **Through COM, `Range.Font` acted as the keys.** `Font.Bold = True` did not widen it, and
  `Font.Underline = 2` widened it to 11.73.

**Decided with the user, 2026-10-12**, from h7, before R6 was read:
- **Format Cells' OK widens for any part of the Font it sets**, as it widens for a Number Format.
  - It widens under every Chrome, in OK's one undo step, and never a column the user sized.
  - Bold is h7's. Italic, Underline and a colour are R6's.
  - Strikethrough set there, and the Normal font box, are readings.
- **`SetCellFormatAsync` widens as Format Cells' OK does.** This is the decision, not an
  observation: no run sets a Cell Format as this method does. R6 has since found `Range.Font`
  through COM acting as the keys, not as the Font tab.
- **Each Sheet Toolbar toggle widens as the key it names.**
  - Underline widens as Ctrl+U, and Strikethrough as Ctrl+5. Bold and Italic widen nothing, as
    Ctrl+B and Ctrl+I do not.
  - R6 has since observed the Home tab's Bold, Italic and Underline buttons act so.
  - Excel's Home tab has no Strikethrough button. That ExSheet's widens as Ctrl+5 is the decision's.
- **A Fill, Borders and an alignment widen nothing**, set any way.
  - Observed for the Home tab's Fill Colour (case 7), the Fill tab (R6), the Border tab (case 9) and
    the Alignment tab's vertical alignment (R6).
  - The horizontal alignment, the only one ExSheet sets, is a reading.

**What ExSheet builds** (`SheetWidening`, ticket 100's path):
- **The formatting keys widen for a Number Format, underline or strikethrough.** So does
  `SetCellFormatFromFocusAsync`, the command the toolbar's toggles set through (ADR-0100): an item
  acts through public commands only, so the toggles' rule is that command's.
- **Format Cells' OK and `SetCellFormatAsync` widen for a Number Format or any part of the Font.**
  - The toolbar's Font Colour face sets through `SetCellFormatAsync`, and widens with it.
  - That Excel's Font Color button widens is a reading: no run has pressed it over `####`.
- **Each widening is ticket 58's.** The width is recorded as an entry widened it (SH-26), in the
  action's one undo step, and a column the user sized is never widened.

**Readings that stay**, each asked in the questions below:
- Taking underline off with Ctrl+U, or strikethrough with Ctrl+5, widens as setting it does.
- Ctrl+4 widens as Ctrl+U, being the same key in ExSheet's table.
- Ctrl+I then Ctrl+U on one Selection widen as Ctrl+U alone. The run pressed each key in a
  workbook of its own.
- Strikethrough from the Font tab, and its Normal font box, widen as its other Font parts do.
- A horizontal alignment from Format Cells widens nothing.
- The Home tab's Font Color button widens, as the face that sets through `SetCellFormatAsync` does.
- Over several ranges, whole columns or whole rows, each command widens every column it holds
  numbers in, as over one range.

**Seen beside it, and not decided:**
- **Excel widened an italic number further.** For Italic alone it widened to 12.45, where Bold,
  Underline, a colour and the keys gave 11.73.
- **ExSheet charges italic as regular.** It widens to what the grid's own estimate charges for the
  text (ADR-0016), and the grid judges italic text by the regular widths: its slant leans past a
  glyph's advance by less than the cell's padding (`CellAppearance.Italic`). So an italic number
  widens its column to the width a regular one takes, and is not cut. Whether to widen further, as
  Excel does, is a question of its own.
- **R6's COM answer sits beside the decision on `SetCellFormatAsync`.** `Range.Font` through COM acts
  as the keys. The user decided `SetCellFormatAsync` follows Format Cells' OK before R6 was read, and
  is asked whether that stands.
```

## 2. ADR-0071: dated notes on the four places that left this to a run

**a.** "What the fourteenth Windows run settled", Widening, lines 643 to 646:

old:
```markdown
  - Ctrl+I, Ctrl+U and Format Cells' Font tab were not asked. They stay as Ctrl+B is, until a run
    asks.
  - `SetCellFormatAsync` setting strikethrough is read as the Font tab, and does not widen. It sets
    a Font as the tab does (ticket 100).
```
new:
```markdown
  - Ctrl+I, Ctrl+U and Format Cells' Font tab were not asked. They stay as Ctrl+B is, until a run
    asks. *(2026-10-12: the seventeenth run asked. Ctrl+U widens and Ctrl+I does not. Format Cells'
    Font tab with Bold widened, and the user decided that OK widens for any part of the Font. See
    "What the seventeenth Windows run settled".)*
  - `SetCellFormatAsync` setting strikethrough is read as the Font tab, and does not widen. It sets
    a Font as the tab does (ticket 100). *(2026-10-12: the Font tab widens, and the user decided that
    `SetCellFormatAsync` widens as Format Cells' OK does, strikethrough among the Font's parts. See
    "What the seventeenth Windows run settled".)*
```

**b.** "Seen in the run, and left to the next PR", lines 709 and 710:

old:
```markdown
- **Ctrl+5's widening raises a question about the other Font keys** (above). The next run that asks
  Excel about Cell Format can ask about Ctrl+I, Ctrl+U and the Font tab.
```
new:
```markdown
- **Ctrl+5's widening raises a question about the other Font keys** (above). The next run that asks
  Excel about Cell Format can ask about Ctrl+I, Ctrl+U and the Font tab. *(2026-10-12: the
  seventeenth run asked them, h7. See "What the seventeenth Windows run settled".)*
```

**c.** "What was decided after Part C", "Seen beside the readings", lines 849 and 850:

old:
```markdown
- **The other Font keys** (Ctrl+I, Ctrl+U, and the Font tab beside Ctrl+5's widening) wait for the
  next run that asks Excel about Cell Format.
```
new:
```markdown
- **The other Font keys** (Ctrl+I, Ctrl+U, and the Font tab beside Ctrl+5's widening) wait for the
  next run that asks Excel about Cell Format. *(2026-10-12: the seventeenth run asked them, h7. See
  "What the seventeenth Windows run settled".)*
```

## 3. ADR-0100: a dated note on the toggles' command

"Found while implementing *(ticket 54, 2026-10-02)*", after the bullet's last sub-bullet, line 164 on
this branch (line 167 on `main` at 2265cda6, which added three lines above it):

old:
```markdown
  - It is a new name, not an overload of `SetCellFormatAsync`. An overload made a Consumer's
    `SetCellFormatAsync(null)` ambiguous.
```
new:
```markdown
  - It is a new name, not an overload of `SetCellFormatAsync`. An overload made a Consumer's
    `SetCellFormatAsync(null)` ambiguous.
  - *(2026-10-12, decided with the user, ADR-0071: each toggle widens a column over `####` as the key
    it names. Underline and Strikethrough widen, and Bold and Italic do not. `SetCellFormatAsync`
    widens as Format Cells' OK does, for any part of the Font. An item acts through public commands
    only, so the toggles' rule is this command's: it widens as the formatting keys do.)*
```

## 4. The Docs Site: `samples/ExGrid.Docs/Pages/Sheet/SheetFormatting.razor`

This replaces item 3 of the first version of this file.

**a.** "Cell Formats", line 30 (the paragraph "A number that does not fit shows ####"):

old:
```html
            sized widens it, as Excel's does, and so does setting a Number Format, as Excel's formatting keys do. A
```
new:
```html
            sized widens it, as Excel's does, and so does setting a Number Format, as Excel's formatting keys do.
            So do Ctrl+5 and Ctrl+U, Format Cells' OK when it sets any part of the Font, and
            <code>SetCellFormatAsync</code> as OK does. Ctrl+B and Ctrl+I widen nothing, nor do a Fill, an alignment
            or Borders. A column you sized is never widened. A
```

**b.** "The Sheet Toolbar", line 119 (the paragraph's last sentence):

old:
```html
            out while a cell is edited. A press leaves the keyboard on the Sheet.
```
new:
```html
            out while a cell is edited. A press leaves the keyboard on the Sheet. Over a number that shows
            <code>####</code>, Underline and Strikethrough widen a column you have not sized as Ctrl+U and Ctrl+5
            do; Bold and Italic widen nothing, as Ctrl+B and Ctrl+I do not.
```

The Change Notes link `exsheet/formatting#cell-formats` (`run17gh-widening-ctrl-u.md`,
`run17gh-widening-font-widens.md`) and `exsheet/formatting#the-sheet-toolbar`
(`run17gh-widening-toolbar-toggles.md`). Run the site once after applying it
(`nix develop -c dotnet run --project samples/ExGrid.Docs`). The page's Examples set formats in code,
before the document reaches the Sheet, and the Format from code Example's numbers fit their columns.
No Example changes.

## 5. Questions for the next Windows run

Each case is a new workbook of one sheet at 100% zoom, under en-GB, as R6 ran it (`excel/r6.py`).
Unless it says otherwise, A1 holds 123456789.12 in `0.00` at the standard width (8.09), both set
through COM, so A1 shows `########`. Then one gesture.

After the gesture, read through COM:
- A1's `Text`;
- column A's `ColumnWidth` and `UseStandardWidth`;
- A1's `Font.Bold`, `Font.Italic`, `Font.Underline`, `Font.Strikethrough` and `Font.Color`.

Take a picture of A1:C3. "Widens" means `UseStandardWidth` false and A1 showing the number.

1. **Format Cells' Font tab, Strikethrough alone** (Effects), then OK.
   - ExSheet widens: a reading.
   - If Excel does not, the Font tab widens for some parts and not others, which needs the user.
2. **The Font tab's Normal font box.** A1 first gets a red font through COM (`Font.Color = 255`),
   still `########`. Then Ctrl+1, the Font tab, the Normal font box checked, OK.
   - ExSheet widens: a reading.
3. **The Font tab shown and OK pressed with nothing changed.**
   - ExSheet sets nothing and widens nothing. R6 saw the Fill and Alignment tabs do the same.
   - If Excel widens here, OK on the Font tab widens whatever is touched, which needs the user.
4. **The Alignment tab's Horizontal: Center**, then OK.
   - ExSheet widens nothing: a reading. R6 asked only the vertical.
5. **The Home tab's Font Color button**, pressed on its face. A new Excel's face is red.
   - ExSheet's Font Colour face widens, as `SetCellFormatAsync` does: a reading.
   - If Excel's does not, the user decides whether the face follows the button or the method.
6. **Through COM, one property each**, in a workbook each: `Range.Font.Italic = True`,
   `Range.Font.Strikethrough = True`, `Range.Font.Color = 255`.
   - R6 asked Bold and Underline. This completes `Range.Font` for the user's question on
     `SetCellFormatAsync` (below).
7. **Ctrl+U taking underline off.** A1 is underlined first through COM (`Font.Underline = 2`).
   - Read the width after the COM step too: R6 saw that step widen through COM. If it did, clear the
     column back to the standard width through COM (`UseStandardWidth = True`) and check `########`
     before the key.
   - Then Ctrl+U. ExSheet widens: a reading.
   - The same for Ctrl+5 taking strikethrough off, with `Font.Strikethrough = True` first.
8. **Ctrl+4** over `########`.
   - ExSheet widens, as for Ctrl+U: a reading.
9. **Ctrl+I, then Ctrl+U, in one workbook**: the width after each.
   - ExSheet: no change after Ctrl+I, widened after Ctrl+U.
10. **Over two ranges**: A1 and C1 each hold the number. Select A1, Ctrl+click C1, then the Font tab
    with Bold, OK.
    - ExSheet widens both columns, in one undo step: a reading.
    - Then the same over column A selected from its Heading.

## For the user

- **`SetCellFormatAsync` and R6's COM answer.** You decided `SetCellFormatAsync` follows Format Cells'
  OK, so it widens for bold and italic too. R6, read after, found `Range.Font` through COM act as the
  keys: `Font.Bold = True` did not widen, and `Font.Underline = 2` did.
  - Built as decided: `SetCellFormatAsync` widens for any part of the Font.
  - The other way is one line in `ExSheet.razor`: `SetCellFormatAsync` takes the keys' rule, as
    `SetCellFormatFromFocusAsync` does.
  - Does the decision stand?
- **The toolbar's Font Colour face** sets through `SetCellFormatAsync`, so it widens with it.
  - Excel's Font Color button is not observed (question 5).
  - If the user would rather the face follow the Home tab's other buttons, it can set through
    `SetCellFormatFromFocusAsync`, which widens as the keys do. Then a colour widens nothing.
- **Italic widened further in Excel** (12.45, against 11.73). ExSheet widens an italic number to the
  regular width, as the grid judges italic text by the regular widths (`CellAppearance.Italic`). The
  number shows whole either way. Should ExSheet widen further, as Excel does?
