# Run 17 Part H, widening: what the orchestrator applies

Branch `claude/run17gh-widening`, three commits:

- **fb91016d**: Ctrl+U widens as Ctrl+5 does (h7).
- **51122690**: the user's decision (3) of 2026-10-12, from h7, built.
  - Format Cells' OK widens for any part of the Font it sets, under both Chromes.
  - Each Sheet Toolbar toggle widens as the key it names.
  - `SetCellFormatAsync` widened as Format Cells' OK does. The third commit replaces this.
- **The third**: the user's decision of 2026-10-12 from the readings' R6 (5), built.
  - `SetCellFormatAsync` widens as the formatting keys do, not as Format Cells' OK does.
  - So the Sheet Toolbar's Font Colour, which sets through it, widens nothing.
  - The italic difference is recorded as a known limitation that follows ADR-0071 (item 8, and the
    Release Notes line).
  - The verifier's round-2 findings are fixed here too.

What is on the branch: the code, layer 2 and layer 3 tests, the dated comments on ticket 100, four
Change Notes, `src/ExSheet/README.md` and `tests/ExGrid.Browser/README.md`. Each item below is text for
a file a background agent does not change (an ADR, the Docs Site, a version's Release Notes). Each is
written as exact old -> new whole lines, with where it goes. Items 1 to 8 apply in order against the
branch tip (`docs/agent-runs/run17gh-widening/check-proposal.py`, git-ignored, checks it); the Release
Notes line waits for the cut. A line number given below is the tip's, before any item is applied.

**The sources.**

- **The run's record**: `verification/2026-10-11-windows-17/report.md` on
  `claude/exsheet-windows-verify-17`, Part H, rows h7 and h8, with `records/part-h-excel.jsonl` and
  `excel/part_h_excel.py`.
  - Each key and the Font tab ran in a workbook of its own at 100% zoom, under the regional format
    en-GB (Part H: "The regional format is en-GB, but for h9's ja-JP half").
  - **The set-width cases** held 123456.78 in `0.00` showing `#####` at width 5, set through COM.
  - **The standard-width cases** held 123456789.12 in `0.00` showing `########` at 8.09.
- **The readings of 2026-10-12**: `verification/2026-10-12-windows-readings/report.md`, section R6,
  with `summary.md`, `records/r6.jsonl` and `excel/r6.py`. They are on
  `claude/exsheet-windows-readings-2026-10-12` at 89e0b5ad, which `main` does not hold yet.
  - **R6 ran under the regional format en-US**, not h7's en-GB: the report sets en-US at 16:22:09
    "for R1 to R6, R8 and R3".
  - It held h7's standard-width number and format, set through COM in the same order, the format
    first on the empty cell. It states no zoom.
  - **R6 answered every question the brief listed for the next run**: the Font tab with Italic,
    Underline or a colour alone; the Fill and Alignment tabs; the Home tab's B, I and U buttons;
    `Range.Font` through COM, for Bold and Underline.
  - **So the ADR text below names those parts observed, citing R6.** Only what R6 did not reach is
    left a reading and asked next run.
  - If the orchestrator would rather not cite R6 before the readings branch merges, each R6 line
    below goes back to "a reading", and its question goes back into the questions below. The user's
    decision on `SetCellFormatAsync` was taken from R6 (5), so that note keeps its citation.
- **The fourteenth run**: `verification/2026-10-01-windows-excel-14/cell-format-14.md` on this branch.
  - Its case 7 pressed the Home tab's Fill Colour button over `########` (1234567.5 in `0.00`).
  - Its case 9 pressed the Border tab's Outline over 1234567.5 in **General**, which showed `1234568`,
    not `####`. So it observed Borders not widening a column whose number shows rounded, and Borders
    over `####` remain a reading.

## The edits, in order

### 1. docs/adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md

A section "What the seventeenth Windows run settled". It goes before `## Consequences` (line 852 on
this branch), after the section "What was decided after Part C", whose last bullet the old text holds
and the new text keeps:

Old:
```markdown
- **The other Font keys** (Ctrl+I, Ctrl+U, and the Font tab beside Ctrl+5's widening) wait for the
  next run that asks Excel about Cell Format.

## Consequences
```
New:
```markdown
- **The other Font keys** (Ctrl+I, Ctrl+U, and the Font tab beside Ctrl+5's widening) wait for the
  next run that asks Excel about Cell Format.

## What the seventeenth Windows run settled *(2026-10-12)*

The run was made at 8436e45e. Its record is `verification/2026-10-11-windows-17/report.md`, Part H,
rows h7 and h8. It asked what the fourteenth run left "as Ctrl+B is, until a run asks". The readings
of 2026-10-12 asked what h7 raised (`verification/2026-10-12-windows-readings/report.md`, R6). Every
answer is Excel's (ADR-0047's rule). Each part below says which of them observed it, or that it is a
reading.

**What the run observed** (h7, h8). Each case was a workbook of its own at 100% zoom, under the
regional format en-GB. The format and then the number were set through COM, and then one gesture was
made.
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

**What the readings observed** (R6). The same number and format at the standard width, set through
COM in the same order, each case a workbook of its own, under the regional format en-US (h7's was
en-GB):
- **Format Cells' Font tab widened the column** with Italic alone, to 12.45. With a single Underline
  alone, or a colour (Red) alone, it widened to 11.73.
- **The Fill tab and the Alignment tab did not widen it**, untouched or changed (Yellow; Vertical:
  Top).
- **The Home tab's buttons acted as the keys.** Bold and Italic did not widen it, and Underline
  widened it to 11.73. R6 did not press Font Color.
- **Ctrl+B pressed twice** did not widen it, after either press.
- **Through COM, `Range.Font` acted as the keys.** `Font.Bold = True` did not widen it, and
  `Font.Underline = 2` widened it to 11.73.

**Decided with the user.**
- *(2026-10-12, decided with the user: Format Cells' OK widens for any part of the Font it sets.)*
  - It widens as it widens for a Number Format.
  - It widens under every Chrome, in OK's one undo step, and never a column the user sized.
  - Bold is h7's. Italic, Underline and a colour are R6's.
  - Strikethrough set there, and the Normal font box, are readings.
- *(2026-10-12, decided with the user: each Sheet Toolbar toggle widens as the key it names.)*
  - Underline widens as Ctrl+U, and Strikethrough as Ctrl+5. Bold and Italic widen nothing, as
    Ctrl+B and Ctrl+I do not.
  - R6 has since observed the Home tab's Bold, Italic and Underline buttons act so.
  - Excel's Home tab has no Strikethrough button. That ExSheet's widens as Ctrl+5 follows from the
    decision, and no run observes it.
- *(2026-10-12, decided with the user: `SetCellFormatAsync` widens as the formatting keys do, not as
  Format Cells' OK does.)*
  - It was decided from R6's `Range.Font` through COM. The day's first decision, from h7 before R6
    was read, had `SetCellFormatAsync` widen as Format Cells' OK does. This replaces that part of it;
    the two rules above stand.
  - So a Number Format, underline or strikethrough set through it widens, and bold, italic or a
    Font colour does not.

**What ExSheet builds** (`SheetWidening`, ticket 100's path):
- **The formatting keys widen for a Number Format, underline or strikethrough**
  (`WidensAsAKeyDoes`). So do `SetCellFormatAsync` and `SetCellFormatFromFocusAsync`, the command the
  toolbar's toggles set through (ADR-0100).
- **Format Cells' OK widens for a Number Format or any part of the Font**
  (`WidensAsFormatCellsOkDoes`), under every Chrome. Only OK widens by this rule.
- **The toolbar's Font Colour widens nothing**, its face or a colour from its list, since both set
  through `SetCellFormatAsync`. That Excel's Font Color button widens nothing is a reading.
- **A Fill, Borders and an alignment widen nothing**, set any way.
  - Observed for the Home tab's Fill Colour button over `####` (the fourteenth run's case 7), and the
    Fill tab and the Alignment tab's vertical alignment over `####` (R6).
  - The Border tab did not widen over a General number shown rounded, `1234568` (case 9).
  - Borders over `####`, from the tab or the Border keys (Ctrl+Shift+& and Ctrl+Shift+_), and the
    horizontal alignment, the only one ExSheet sets, are readings.
- **Each widening is ticket 58's.** The width is recorded as an entry widened it (SH-26), in the
  action's one undo step, and a column the user sized is never widened.

**Readings that stay**, each for the next Windows run that asks Excel about Cell Format:
- Taking underline off with Ctrl+U, or strikethrough with Ctrl+5, widens as setting it does.
- Ctrl+4 widens as Ctrl+U, being the same key in ExSheet's table.
- The Border keys, Ctrl+Shift+& and Ctrl+Shift+_, widen nothing, and neither does the Border tab
  over `####`.
- Ctrl+I then Ctrl+U on one Selection widen as Ctrl+U alone. The run pressed each key in a
  workbook of its own.
- Strikethrough from the Font tab, and its Normal font box, widen as its other Font parts do.
- A horizontal alignment from Format Cells widens nothing.
- The Home tab's Font Color button widens nothing, as ExSheet's Font Colour does not.
- `Range.Font` through COM acts as the keys for italic, strikethrough and a colour too.
- Over several ranges, whole columns or whole rows, each command widens every column it holds
  numbers in, as over one range.

**Where ExSheet differs, as this ADR has it.**
- **An italic number is widened only to the width the upright number takes.** Over 123456789.12 in
  `0.00`, Excel's Font tab widened column A to 12.45 for Italic alone, where a single underline or a
  colour alone gave 11.73 (R6), and Bold 11.73 (h7).
- ExSheet widens to what the grid's own estimate charges for the text (ADR-0016), and this ADR judges
  italic by the regular widths: its slant leans past a glyph's advance by less than the cell's padding
  ("Bold, and what fits"). So the number shows whole, and the column is not widened further.
- This is a difference that follows this ADR, not a change. It is a known limitation of `0.1.0`
  (ADR-0202's note of this date).

## Consequences
```

### 2. docs/adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md

A dated note on the first place that left this to a run: "What the fourteenth Windows run settled",
Widening, lines 643 to 646.

Old:
```markdown
  - Ctrl+I, Ctrl+U and Format Cells' Font tab were not asked. They stay as Ctrl+B is, until a run
    asks.
  - `SetCellFormatAsync` setting strikethrough is read as the Font tab, and does not widen. It sets
    a Font as the tab does (ticket 100).
```
New:
```markdown
  - Ctrl+I, Ctrl+U and Format Cells' Font tab were not asked. They stay as Ctrl+B is, until a run
    asks. *(2026-10-12: the seventeenth run asked, h7. Ctrl+U widens and Ctrl+I does not, and
    Format Cells' Font tab with Bold widened. What follows is in "What the seventeenth Windows run
    settled".)*
  - `SetCellFormatAsync` setting strikethrough is read as the Font tab, and does not widen. It sets
    a Font as the tab does (ticket 100). *(2026-10-12, decided with the user: `SetCellFormatAsync`
    widens as the formatting keys do, not as Format Cells' OK does.)* So strikethrough set through
    it widens, as Ctrl+5 does. See "What the seventeenth Windows run settled".
```

### 3. docs/adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md

"Seen in the run, and left to the next PR", lines 709 and 710.

Old:
```markdown
- **Ctrl+5's widening raises a question about the other Font keys** (above). The next run that asks
  Excel about Cell Format can ask about Ctrl+I, Ctrl+U and the Font tab.
```
New:
```markdown
- **Ctrl+5's widening raises a question about the other Font keys** (above). The next run that asks
  Excel about Cell Format can ask about Ctrl+I, Ctrl+U and the Font tab. *(2026-10-12: the
  seventeenth run asked them, h7. See "What the seventeenth Windows run settled".)*
```

### 4. docs/adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md

"What was decided after Part C", "Seen beside the readings", the two lines item 1 kept. Item 1 is
applied first, since its old text holds these lines.

Old:
```markdown
- **The other Font keys** (Ctrl+I, Ctrl+U, and the Font tab beside Ctrl+5's widening) wait for the
  next run that asks Excel about Cell Format.
```
New:
```markdown
- **The other Font keys** (Ctrl+I, Ctrl+U, and the Font tab beside Ctrl+5's widening) wait for the
  next run that asks Excel about Cell Format. *(2026-10-12: the seventeenth run asked them, h7. See
  "What the seventeenth Windows run settled".)*
```

### 5. docs/adr/0100-the-sheet-toolbar-ships-as-an-opt-in-part-of-exsheet.md

Dated notes on the toggles' command: "Found while implementing *(ticket 54, 2026-10-02)*", after the
bullet's last sub-bullet, line 163 on this branch.

Old:
```markdown
  - It is a new name, not an overload of `SetCellFormatAsync`. An overload made a Consumer's
    `SetCellFormatAsync(null)` ambiguous.
```
New:
```markdown
  - It is a new name, not an overload of `SetCellFormatAsync`. An overload made a Consumer's
    `SetCellFormatAsync(null)` ambiguous.
  - *(2026-10-12, decided with the user: each Sheet Toolbar toggle widens as the key it names.)*
    *(2026-10-12, decided with the user: `SetCellFormatAsync` widens as the formatting keys do, not
    as Format Cells' OK does.)* ADR-0071, "What the seventeenth Windows run settled", holds both.
    - An item acts through public commands only, so the toggles' rule is this command's. It widens
      a column over `####` as the formatting keys do: Underline and Strikethrough widen, and Bold and
      Italic do not.
    - The Font Colour, its face and its list, sets through `SetCellFormatAsync`, so it widens
      nothing. That Excel's Font Color button widens nothing is a reading.
```

### 6. samples/ExGrid.Docs/Pages/Sheet/SheetFormatting.razor

The Docs Site. Items 6 and 7 replace item 4 of the second version of this file. "Cell Formats",
line 30, the paragraph "A number that does not fit shows ####".

Old:
```html
            sized widens it, as Excel's does, and so does setting a Number Format, as Excel's formatting keys do. A
```
New:
```html
            sized widens it, as Excel's does, and so does setting a Number Format, as Excel's formatting keys do.
            So do Ctrl+5 and Ctrl+U, which set no Number Format, and <code>SetCellFormatAsync</code> when it sets
            underline or strikethrough. Ctrl+B and Ctrl+I widen nothing, nor do bold, italic or a Font colour set from
            code, a Fill, an alignment or Borders. Format Cells' OK widens when it sets any part of the Font. An italic
            number widens its column only to the width the same number takes upright, where Excel's Font tab widened
            it further. A column you sized is never widened. A
```

### 7. samples/ExGrid.Docs/Pages/Sheet/SheetFormatting.razor

"The Sheet Toolbar", line 119, the paragraph's last sentence.

Old:
```html
            out while a cell is edited. A press leaves the keyboard on the Sheet.
```
New:
```html
            out while a cell is edited. A press leaves the keyboard on the Sheet. Over a number that shows
            <code>####</code>, Underline and Strikethrough widen a column you have not sized, as Ctrl+U and Ctrl+5
            do. Bold and Italic widen nothing, as Ctrl+B and Ctrl+I do not, and neither does the Font Colour, whose
            face and list set the colour through <code>SetCellFormatAsync</code>.
```

The Change Notes link `exsheet/formatting#cell-formats` (`run17gh-widening-ctrl-u.md`,
`run17gh-widening-font-widens.md`, `run17gh-widening-set-cell-format.md`) and
`exsheet/formatting#the-sheet-toolbar` (`run17gh-widening-toolbar-toggles.md`). The Release Notes
line below links `exsheet/formatting#cell-formats`, which item 6 makes say it in full. Run the site
once after applying items 6 and 7 (`nix develop -c dotnet run --project samples/ExGrid.Docs`).

No Example changes:
- The page's Examples set formats in code, before the document reaches the Sheet.
- The Format from code Example's numbers fit their columns.
- The Toolbar Examples' own buttons set a Fill, and a Fill with bold, through `SetCellFormatAsync`,
  which widens for neither.

### 8. docs/adr/0202-what-0-1-0-holds-of-exsheet-and-expivot-and-where-it-says-what-it-does-not-do.md

The known limitation. "Consequences", the bullet "`0.1.0`'s Known limitations will hold at least",
after its note of 2026-10-12.

Old:
```markdown
  tens of milliseconds; with such a `SUM` on every row instead, an edit was measured at about 40 to 48
  seconds and holds the circuit that long. `verification/2026-10-11-linux-sh19/` has both.)*
```
New:
```markdown
  tens of milliseconds; with such a `SUM` on every row instead, an edit was measured at about 40 to 48
  seconds and holds the circuit that long. `verification/2026-10-11-linux-sh19/` has both.)*
  *(2026-10-12: also how far ExSheet widens a column for an italic number over `####`. Over
  123456789.12 in `0.00` at the standard width, Excel's Format Cells' Font tab widened column A to
  12.45 for Italic alone, and to 11.73 for a single underline or a colour alone (the readings of
  2026-10-12 (R6)). ExSheet widens an italic number to the width the upright one takes, because
  ADR-0071 judges italic by the regular widths. It is a difference that follows ADR-0071, not a
  change.)*
```

## The Release Notes: a Known limitations line, at the cut

For the Known limitations of `0.1.0` (ADR-0202), and of each `0.1.0-rc.N`, which rehearses them
(ADR-0190's note of 2026-10-12). The heading does not exist in the notes or the parser yet (ADR-0202,
Consequences), so the line waits for the cut that writes the version's file:

```markdown
- **ExSheet:** An italic number over `####` widens its column only to the width the same number takes upright, because ExSheet judges italic by the regular widths (ADR-0071); Excel's Font tab widened column A to 12.45 for italic, against 11.73 for a single underline or a colour, over 123456789.12 in `0.00` ([Cell Formats](exsheet/formatting#cell-formats)).
```

## Questions for the next Windows run

Each case is a new workbook, by R6's procedure (`excel/r6.py`), under the regional format en-US, as
R6 ran (h7 ran under en-GB). Zoom 100%, as h7 states its cases. Unless it says otherwise, A1 holds
123456789.12 in `0.00` at the standard width (8.09), the format and then the number set through COM,
so A1 shows `########`. Then one gesture.

After the gesture, and after every step that sets something through COM, read through COM:
- A1's `Text`;
- column A's `ColumnWidth` and `UseStandardWidth`;
- A1's `Font.Bold`, `Font.Italic`, `Font.Underline`, `Font.Strikethrough` and `Font.Color`.

Take a picture of A1:C3. "Widens" means `UseStandardWidth` false and A1 showing the number. Where a
COM step before the gesture widened the column, set `UseStandardWidth = True` through COM and check
`########` before the gesture.

1. **Format Cells' Font tab, Strikethrough alone** (Effects), then OK.
   - ExSheet widens: a reading.
   - If Excel does not, the Font tab widens for some parts and not others, which needs the user.
2. **The Font tab's Normal font box.** A1 first gets a red font through COM (`Font.Color = 255`).
   - Read the width after that step: whether a colour set through COM widens is question 6's.
     If it widened, set `UseStandardWidth = True` and check `########`.
   - Then Ctrl+1, the Font tab, the Normal font box checked, OK.
   - ExSheet widens: a reading.
3. **The Font tab shown and OK pressed with nothing changed.**
   - ExSheet sets nothing and widens nothing. R6 saw the Fill and Alignment tabs do the same.
   - If Excel widens here, OK on the Font tab widens whatever is touched, which needs the user.
4. **The Alignment tab's Horizontal: Center**, then OK.
   - ExSheet widens nothing: a reading. R6 asked only the vertical.
5. **The Home tab's Font Color button**, pressed on its face. A new Excel's face is red. Then its
   arrow, and Blue from the list, in a workbook of its own.
   - ExSheet's Font Colour widens nothing, face or list, as `SetCellFormatAsync` does not for a
     colour: a reading.
   - If Excel's widens, the user decides whether the face follows the button or the method.
6. **Through COM, one property each**, in a workbook each: `Range.Font.Italic = True`,
   `Range.Font.Strikethrough = True`, `Range.Font.Color = 255`.
   - R6 asked Bold and Underline. This completes `Range.Font` beside the decision on
     `SetCellFormatAsync`.
   - ExSheet's `SetCellFormatAsync`: italic and a colour widen nothing, and strikethrough widens: a
     reading. If Excel's differs, the user decides whether the method follows.
7. **Ctrl+U taking underline off.** A1 is underlined first through COM (`Font.Underline = 2`).
   - R6 saw that COM step widen. Set `UseStandardWidth = True` and check `########` before the key.
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
11. **The Border keys and the Border tab over `########`**, each in a workbook of its own.
    - Ctrl+Shift+& (an outline).
    - Ctrl+Shift+_ (no borders), over A1 whose four edges were first set thin through COM.
    - Format Cells' Border tab, Outline, OK. The fourteenth run's case 9 was over a General number
      showing `1234568`, not `####`.
    - ExSheet widens nothing for any of the three: a reading.
    - If Excel widens for any, the user decides whether Borders widen as that gesture does.

## For the user

Nothing here waits on a decision.
- `SetCellFormatAsync` is built as decided: it widens as the formatting keys do.
- The toolbar's Font Colour follows from it, and widens nothing. Excel's Font Color button is asked
  in question 5.
- The italic difference follows ADR-0071, and is proposed as a known limitation (item 8, and the
  Release Notes line).
- Each reading above is asked in the questions. An answer that disagrees comes back to the user, as
  each question says.
