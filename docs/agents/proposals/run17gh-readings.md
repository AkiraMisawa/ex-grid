# Run 17 Part H, the readings: what the orchestrator writes

Branch `claude/run17gh-readings`, from `origin/main` (`a533de01`). The seventeenth Windows run's Part H
(`verification/2026-10-11-windows-17/report.md` on `claude/exsheet-windows-verify-17`, h4, h9, h10 and h12, with
`part-h/shots/` and `records/part-h-excel.jsonl`) asked Excel what the code had built as readings. Three commits:

1. **The first** turned the readings Excel answered as read into observed tests (h4, h9, h10's taking-away, h12).
   Nothing in `src/` changed.
2. **The second (round 2)** follows a difference h10 found, and corrects four things in the first proposal:
   - **(a) h10, the Border tab's line on opening.** Excel's Format Cells over A2, under A1's thick bottom, opened its
     Style list on **Thick** with the Colour Automatic (`h-h10-take-away-1-dialog-over-a2-dialog.png`). Where the line
     on that edge was thin it opened on **Thin** (`h-h10-both-1-dialog-over-a2-dialog.png`). In the fourteenth run's
     case 13, A1's bottom thick black opened it on Thick and black, over A2 (`13-tab-border-window-1.png`) and over A1
     (`13-a1-opened-window-1.png`). ExSheet's `FormatCellsDraft` always opened on Thin and Automatic, so the run's
     single press of the Top button would have set a thin top, where Excel's took the line away. **Format Cells now
     opens the Style and the Colour on the line shown where the Selection is one cell and exactly one of its four
     edges shows a line, and on Thin and Automatic everywhere else.** Excel was seen to open on the line shown only
     for a top edge (h10; case 13 over A2) and a bottom edge (case 13 over A1), in Automatic or black, and on Thin
     and Automatic over R8's three Selections (below). The rest of the rule is a reading. The tests press only what
     the run pressed: Top, then OK. This changes what a Consumer sees, so it has a Change Note.
   - **(b) h9** names which of ticket 103's readings were observed (the en-GB typed `d-mmm-yy` and `m/d/yyyy` as codes
     of their own, and ja-JP's `mmmmm`) and which were not (`h:mm` as a code of its own, which `13:30` cannot show; a
     code reopened under en-US; zh-CN and ko-KR). The first proposal called all of them observed.
   - **(c) ADR-0052**: the bullet labelled "Read, not observed" is relabelled as observed, its history kept in the
     text, instead of a note appended under a false label.
   - **(d) h10's citation**: only the fourteenth run's case 12 read two records on one edge from a saved file. The
     twelfth run's cases 1, 3, 6, 7 and 9 read COM and the pixels, which show the edge and not each cell's record.
3. **The third (round 3)** makes every text on the branch say what Excel was seen to do, and call the rest a reading:
   - **The opening line.** The Change Note, the Docs Site paragraph (edit 14) and ticket 52's correction (edit 5)
     presented the whole rule as Excel's ("as Excel's does"). So did `FormatCellsDraft`'s comments, two test names
     (three are renamed), ADR-0071's bullet (edit 3) and ticket 102's comment (edit 10). Each now says that Excel was
     seen to open on the line shown only for a top edge (the seventeenth Windows run, h10) and a bottom edge (the
     fourteenth run, case 13), in Automatic or black, and on Thin and Automatic for R8's three cases, and that the
     rest is a reading.
   - **"Exactly one."** The Docs Site sentence and ticket 52's correction said "a line on one of its edges". They now
     say "exactly one", as the Change Note and the code have it.
   - **Found on the way, in the same texts:**
     - Ticket 52's comment said "Thin selected on opening" held "only where no line shows". R8 (1) and (2) opened on
       Thin where lines show, so it now says it does not hold over every Selection.
     - "Over a range" (the code, a test comment, ticket 52's comment) now names the range R8 opened, A2:B3.
     - ADR-0052's bullet (edit 1) called the whole take-out rule observed. h4 saw it with A1:B2 the only range
       left, so which range takes the place where several are left stays a reading. So does the layer-1 test's
       comment.
     - h9's notes (edits 2 and 13, `CultureTests`) said what built-ins 15 and 14 "show" on 13:30 alone. No run showed
       that. They now say what the built-ins' codes would show.
     - ADR-0071's note of h12 (edit 4) said "`@` before the last section is refused in Excel too". Only `@;0` and
       `0;@;0` were asked, so it now names those two.
     - The twelfth run's model (edits 3 and 10, the triage paragraph below) is named as the source of "setting a
       line clears the neighbour's record". h10 read COM, which shows the edge as drawn, so h10 agrees with the model
       but did not see the records.
     - Question 6 below asks a style other than Thick or Thin, a reading this round names.
   - **The edits are numbered**, each an exact Old and New of whole lines. `docs/agent-runs/check-proposal.py` (in
     the worktree, git-ignored) applies them in order against the branch tip and checks that each Old occurs once.

**Two readings made after the run answer part of what (a) and (b) leave.** The readings of 2026-10-12
(`verification/2026-10-12-windows-readings/report.md` on `claude/exsheet-windows-readings-2026-10-12`, at `89e0b5ad`):

- **R8** opened Format Cells' Border tab over three Selections, and each opened on Thin and Automatic: B2 with a thin
  top and a thick right of its own; A2:B3 with A2's thin top, B2's medium right, A3's thick bottom and B3's dashed left;
  and D5 with no line. Those are the brief's "several edges with different lines" and "a range". They agree with the
  rule above, and are now tests. R8 (1) has a thin top, so it does not tell "Thin, since two edges show lines" from
  "the top's line". That stays a question below.
- **R7** typed `h:mm` into Custom under en-GB on 0.37847222 and saw `9:05`, not `09:05`; and `d-mmm-yy` on 46027 and
  saw `5-Jan-26`. Built-in 20 shows `09:05` under en-GB (the twelfth run's case 19), so R7 observes what h9 could
  not: a typed `h:mm` is a code of its own. They are two test cases.

So the round-3 items "Border tab opens Thin" and "h9 one-digit" are done here, as far as R7 and R8 answered them.

## What was done, finding by finding

| # | Excel | Test | Done |
|---|---|---|---|
| h4 | A1:B2, Ctrl+click F6, Ctrl+click F6 again: Selection `A1:B2`, A1 active. Enter: A2, B1, B2, the Selection kept | `tests/ExGrid.Tests/SelectionGestureTests.cs`, `Taking_out_the_whole_range_made_last_moves_the_focus_to_the_latest_range_left` | Comment: observed (h4), with one range left; where several are left, a reading. Enter three times through `GridSelection.CycleFocus(CycleOrder.ColumnMajor, …)`: the Focus A2, B1, B2, the Ranges still A1:B2. Passed as written |
| h9 | en-GB: `d-mmm-yy` `12-Oct-26` / `0-Jan-00`; `m/d/yyyy` `10/12/2026` / `1/0/1900`; `h:mm` `13:30` / `13:30`. ja-JP: `mmmmm` `1` / `1` | `tests/ExSheet.Engine.Tests/CultureTests.cs`, `A_code_typed_into_custom_shows_as_excel_showed_it_run_17_h9` (8 cases) | Each code read through `NumberFormat.TryParseLocal` as Format Cells reads it, checked to be no date or time built-in's, spelled back as typed by `LocalCode`, and shown as Excel showed it. Round 2: its comment says which part h9 observed. `h:mm`'s 13:30 does not show it is a code of its own. Round 3: what built-ins 15, 14 and 20 would show is said to follow from their codes |
| R7 | en-GB: `h:mm` `9:05` on 0.37847222; `d-mmm-yy` `5-Jan-26` on 46027 | `CultureTests.A_code_typed_into_custom_is_no_built_in_under_en_gb_r7` (new, 2 cases) | As h9's theory. Both passed as the engine stood |
| h10 | Over A2 under A1's thick bottom: Style on Thick, Colour Automatic, the Top button pressed. Top (Alt+T) alone, OK: A1's bottom gone too | `tests/ExSheet.Components/FormatCellsDraftTests.cs`: `Taking_a_shown_edge_away_clears_it_on_both_sides_case_14_13` (rewritten), `Over_a2_under_a1s_thick_bottom_the_tab_opens_on_thick_run_17_h10`, `The_line_opens_in_its_own_colour_case_14_13` (A2 and A1), `A_thin_top_shown_opens_the_tab_on_thin_run_17_h10`, `Thin_and_automatic_over_the_three_selections_r8` (R8's three), `Several_lines_shown_open_the_tab_on_thin_a_reading` (two cases, named a reading). `A_shown_edge_left_alone_sets_nothing_case_14_13` now expects the thick bottom the tab opened on | `FormatCellsDraft` opens `LineStyle` and `LineColour` on the line shown (`OpeningPen`). The rewritten test no longer chooses Thick: it presses Top, as the run did, and asserts the change and both cells' records. Without the fix, 7 layer-2 tests fail. Round 3 renamed three of the new tests after what Excel was seen to do, where they had named the whole rule |
| h10 | the same, under each Chrome | `tests/ExSheet.Components/FormatCellsTests.cs` and `tests/ExSheet.MudBlazor.Tests/MudFormatCellsTests.cs`: `A2_under_a1s_thick_bottom_opens_with_a_thick_top_case_14_13` (Thick chosen on opening), `Taking_the_shown_top_away_clears_a1s_bottom_case_14_13` (Top alone, then OK; "a reading" becomes observed) | Both Chromes bind their radio groups to the draft, so neither Chrome's code changed |
| h10 | the same, in a browser | `tests/ExGrid.Browser/format-cells.spec.mjs` and `format-cells-mud.spec.mjs`: `SH-45, run 17 h10: …` (new in each) | A1's thick bottom set through the dialog (opening on Thin); over A2 Thick is chosen and Top pressed; Top alone, OK; reopened, A2 shows no top and opens on Thin, and A1 shows no bottom |
| h12 | `@;0` and `0;@;0` refused: COM's "Unable to set the NumberFormat property of the Range class"; Format Cells keeps the dialog open, the code selected, and says nothing | `tests/ExSheet.Engine.Tests/NumberFormatTests.cs`, `An_unsupported_code_is_refused` | The `@;0` comment says Excel refuses both too, and how; ExSheet refuses by name (SH-45). `0;0;@;0` is marked "not asked of Excel" |

**Change Note:** `docs/releases/unreleased/run17gh-readings-border-tab-opening-line.md` (ExSheet, Behaviour).

**SL-5 needs no change.** Its pass criterion is "every take-out in ADR-0052's third-run section gives Excel's Focus
and fragment order". With edit 1, the take-out of the whole range made last is one of them, observed for the case the
bullet names, and the layer-1 test covers it. **SH-45 needs no change**: it does not say what the Border tab opens on.

**The triage's wording for h10, corrected.** The triage proposed "Excel keeps one line per edge, so 'both cells record
one' cannot be made there, and the upper-or-left rule meets only a Sheet Document written elsewhere." For a line *set*
on an edge, through COM or Format Cells, that follows from the twelfth run's model in ADR-0071, "How Excel keeps the
line between two cells" (setting an edge clears the neighbour's record). h10 and the eleventh run's case 7 (C2's thin
blue left drawn in place of B2's thick red right) agree with the model; both read the edge as drawn, not each cell's
record. It does not hold for a paste. The fourteenth run's case 12
(`verification/2026-10-01-windows-excel-14/cell-format-14.md`, line 262) read the saved file after E5 was pasted over
B2: **B2 recorded a thick red right while C2 kept its thin blue left**, and the thick red was drawn. That is the one
observation of two records on one edge. The twelfth run's cases 1, 3, 6, 7 and 9 (a paste, Ctrl+D and deletions) read
COM and the pixels, which show the edge and not each cell's record, so they agree with the model and do not show the
state. ExSheet makes the state by a paste, a fill and a deletion (`CellFormatCarryTests`). Which line Format Cells
shows there is still unasked; edit 3 names case 12's paste as the way a run can ask it.

**A record note on h10's second case.** The report says Format Cells "over A2 and over A1 shows that thin line". The
picture over A1, `h-h10-both-3-dialog-over-a1-dialog.png`, shows the Number tab, so only the dialog over A2 is
pictured. Nothing below rests on the dialog over A1.

## The edits, in order

Fourteen blocks, applied in this order. Each Old is whole lines of the target at the branch tip (or as the blocks
before it in the same file leave it), with the target's indentation.

**ADR-0052**, in "What the third run settled": the last bullet. Its label is now false, so it is relabelled, and the
text keeps that it was read first.

### 1. docs/adr/0052-the-focus-is-excels-active-cell-and-the-extent-is-the-moving-end.md

Old:

```
- **Read, not observed**: when a take-out removes the whole range made last (A1:B2, then Ctrl+click
  F6, then Ctrl+click F6 again), the latest range still standing takes its place, so the Focus goes
  to A1. Asked of Excel in the next Windows run.
```

New:

```
- **Observed by the seventeenth Windows run, for the case below** *(2026-10-12, h4; read here first,
  and asked of Excel in a later run)*: when a take-out removes the whole range made last (A1:B2, then
  Ctrl+click F6, then Ctrl+click F6 again), the latest range still standing takes its place, so the
  Focus goes to A1. Excel's Selection was A1:B2 and its active cell A1, and Enter then went to A2, B1
  and B2, down each column, with the Selection kept. A1:B2 was the only range left, so which range
  takes the place where several are left, the latest as read here, is still a reading.
```

**ADR-0071**: dated notes, each under the reading it answers, and one new bullet. The readings stay as written.

h9 and R7, in "What the fourteenth Windows run settled", `mmm` under ja-JP:

### 2. docs/adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md

Old:

```
  - The same rule holds under every culture. Under en-GB, `d-mmm-yy` typed into Format Cells shows
    `5-Jan-26`, not built-in 15's `05-Jan-26`. No run has typed it there.
```

New:

```
  - The same rule holds under every culture. Under en-GB, `d-mmm-yy` typed into Format Cells shows
    `5-Jan-26`, not built-in 15's `05-Jan-26`. No run has typed it there.
    *(2026-10-12, the seventeenth Windows run, h9, on 12 October 2026 at 13:30 and on 13:30 alone.
    Observed under en-GB: `d-mmm-yy` and `m/d/yyyy` typed into Custom are codes of their own. On
    13:30 alone they showed `0-Jan-00` and `1/0/1900`, where built-ins 15 and 14, spelled
    `dd-mmm-yy` and `dd/mm/yyyy` under en-GB, would show `00-Jan-00` and `00/01/1900` by those codes;
    no run showed them there. `h:mm` showed `13:30` on both values, as built-in 20 would, so h9 did
    not show it to be a code of its own. Observed under ja-JP: `mmmmm` shows `1`, the first character
    of `10月` and of `1月`. Not asked: a code of its own reopened under en-US, and the month names
    under zh-CN and ko-KR. The readings of 2026-10-12, R7, then observed under en-GB `h:mm` typed
    into Custom as `9:05`, not `09:05`, and this bullet's `d-mmm-yy` as `5-Jan-26`.)*
```

h10, in "What the fourteenth Windows run settled", ticket 57's model:

### 3. docs/adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md

Old:

```
  - OK still sets only what the user touched (SH-45). Taking the shown top away clears that edge on
    both sides, as clearing an edge does. That is a reading; no run pressed it.
  - Where both cells record a line on an outer edge, the dialog shows the one drawn, the upper or
    left cell's (ticket 102). No run looked.
```

New:

```
  - OK still sets only what the user touched (SH-45). Taking the shown top away clears that edge on
    both sides, as clearing an edge does. That is a reading; no run pressed it.
    *(Observed 2026-10-12, the seventeenth Windows run, h10: over A2 under A1's thick bottom, Format
    Cells opened with a thick top, its button pressed. The Top button alone (Alt+T) took it away,
    and after OK A1's bottom was gone too. Neither cell keeps a line there.)*
  - **Over one cell, the Border tab's line opened on the line its top or bottom showed**
    *(2026-10-12, the seventeenth Windows run, h10)*. Excel's Style list opened on Thick over A2
    under A1's thick Automatic bottom, and on Thin where the line on that edge was thin, its Colour
    Automatic both times. In case 13 it opened on Thick and black for A1's thick black bottom, over
    A2 and over A1. So one press of the edge's button takes the line away, where ExSheet's Thin
    would have set a thin top. Format Cells now opens the Style and the Colour on the line shown
    where the Selection is one cell and exactly one of its four edges shows a line, and on Thin and
    Automatic everywhere else, as before. Excel was seen to open on the line shown only for a top
    edge and a bottom edge, in Automatic or black; the rest of that rule is a reading.
    - Observed on Thin and Automatic, in the readings of 2026-10-12 (R8): B2 with a thin top and a
      thick right of its own; the range A2:B3, with a different line on four of its cells' sides;
      and D5 with no line, as the eleventh run's case 22 saw over A1.
    - A reading, asked of the next Windows run: one cell whose one line is on its left or right; a
      line in a style other than Thick or Thin, or in a colour other than Automatic or black; one
      cell with lines on two edges, none of them thin (R8's thin top cannot tell Thin from the top's
      line); one cell with the same line on two edges; a range whose edges show one line; and
      several one-cell ranges.
  - Where both cells record a line on an outer edge, the dialog shows the one drawn, the upper or
    left cell's (ticket 102). No run looked.
    *(2026-10-12, the seventeenth Windows run, h10: still not asked. By the twelfth run's model, a
    line set on an edge, through COM or Format Cells, clears the neighbour's record of it, and h10
    agrees: A2's thin top set through COM after A1's thick bottom read back as A1's bottom thin too,
    and Format Cells over A2 showed that thin line. COM reads the edge as drawn, not each cell's
    record, so h10 did not see the records themselves. By the model, setting lines cannot make the
    state. A paste makes it: case 12 above read the saved file after a paste, and B2 recorded a
    thick red right while C2 kept its thin blue left. That is the one run that read two records on
    one edge. The twelfth run's cases 1, 3, 6, 7 and 9 read COM and the pixels, which show the edge
    and not each cell's record. A run asks this by case 12's paste, then Format Cells over C2.)*
```

h12, in "Readings until the fourteenth Windows run", tickets 88 and 89:

### 4. docs/adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md

Old:

```
  - **`0;[Red]@`**: Excel kept it as written and showed 5 in black (case 3b).
    - -5 and 0 show in the first section, with no colour.
    - Text shows in the last section's red.
    - In three sections that end in `@`, zero takes the first section.
```

New:

```
  - **`0;[Red]@`**: Excel kept it as written and showed 5 in black (case 3b).
    - -5 and 0 show in the first section, with no colour.
    - Text shows in the last section's red.
    - In three sections that end in `@`, zero takes the first section.
    - *(2026-10-12, the seventeenth Windows run, h12.)* Excel refused `@;0` and `0;@;0`, as ExSheet
      refuses `@` before the last section. Through COM it answered "Unable to set the NumberFormat
      property of the Range class", and its Format Cells kept the dialog open with the code selected
      and said nothing. ExSheet's Format Cells refuses them by name (SH-45). `0;0;@;0` was not asked.
```

**Ticket 52** (`docs/specs/exsheet/issues/52-format-cells-the-seam-and-the-built-in-chrome.md`): lines 48 to 51, and
a dated comment after the last line of the Comments.

### 5. docs/specs/exsheet/issues/52-format-cells-the-seam-and-the-built-in-chrome.md

Old:

```
- [x] The Border tab's line styles in Excel's two columns (None, Hair, Dotted, Dash-dot-dot,
      Dash-dot, Dashed, Thin; Medium Dash-dot-dot, Slanted Dash-dot, Medium Dash-dot, Medium Dashed,
      Medium, Thick, Double), Thin selected on opening; presets None, Outline and Inside, Inside and
      the Horizontal and Vertical buttons disabled for one cell; no diagonal buttons.
```

New:

```
- [x] The Border tab's line styles in Excel's two columns (None, Hair, Dotted, Dash-dot-dot,
      Dash-dot, Dashed, Thin; Medium Dash-dot-dot, Slanted Dash-dot, Medium Dash-dot, Medium Dashed,
      Medium, Thick, Double), Thin selected on opening; presets None, Outline and Inside, Inside and
      the Horizontal and Vertical buttons disabled for one cell; no diagonal buttons.
      *(Corrected 2026-10-12: Thin opens over a cell with no line, as case 22 saw. Over one cell
      that shows a line on exactly one of its edges, ExSheet now opens on that line. Excel was seen
      to do so only for a top edge and a bottom edge, in Automatic or black (the seventeenth Windows
      run, h10; the fourteenth run, case 13); the rest is a reading. See the comment of that date.)*
```

### 6. docs/specs/exsheet/issues/52-format-cells-the-seam-and-the-built-in-chrome.md

Old:

```
- **Left open, also the core's.** A Tab or Shift+Tab held by the core's own hold is still dropped
  with the keys behind it (ADR-0010). Before the contents hold the keyboard, only one that reaches a
  sentinel enters as the user meant.
```

New:

```
- **Left open, also the core's.** A Tab or Shift+Tab held by the core's own hold is still dropped
  with the keys behind it (ADR-0010). Before the contents hold the keyboard, only one that reaches a
  sentinel enters as the user meant.

*(2026-10-12, the seventeenth Windows run, h10; branch `claude/run17gh-readings`.)* **"Thin selected on opening" does
not hold over every Selection.** Over A2 under A1's thick bottom, Excel's Border tab opened on Thick, Automatic as the
line was; in the fourteenth run's case 13 it opened on Thick and black, over A2 and over A1. Where the line shown was
thin it opened on Thin. The readings of 2026-10-12 (R8) opened on Thin and Automatic over B2 with a thin top and a
thick right, over the range A2:B3 with a different line on four of its cells' sides, and over D5 with no line.
`FormatCellsDraft` now opens the Style and the Colour on the line shown where the Selection is one cell and exactly one
of its edges shows a line, and on Thin and Automatic everywhere else. Excel was seen to open on the line shown only for
a top edge (h10; case 13 over A2) and a bottom edge (case 13 over A1), in Automatic or black, and on Thin and Automatic
over R8's three Selections. The rest of the rule is a reading, listed in ADR-0071's note of this date. Both Chromes
bind their line choice to the draft, so both follow. Layer 2 (`FormatCellsDraftTests`, `FormatCellsTests`,
`MudFormatCellsTests`) and layer 3 (`format-cells.spec.mjs`, `format-cells-mud.spec.mjs`) test it.
```

**Ticket 89** (`docs/specs/exsheet/issues/89-a-text-section-may-carry-a-colour.md`): line 53, and the last line.

### 7. docs/specs/exsheet/issues/89-a-text-section-may-carry-a-colour.md

Old:

```
- `@;0` and `0;@;0` stay refused: no run has seen `@` before the last section.
```

New:

```
- `@;0` and `0;@;0` stay refused: no run has seen `@` before the last section. *(Excel refuses both:
  the seventeenth Windows run, h12, below.)*
```

### 8. docs/specs/exsheet/issues/89-a-text-section-may-carry-a-colour.md

Old:

```
- ExSheet.Engine.Tests 2333 and ExSheet.Components.Tests 560 passed.
```

New:

```
- ExSheet.Engine.Tests 2333 and ExSheet.Components.Tests 560 passed.

*(2026-10-12, the seventeenth Windows run, h12; branch `claude/run17gh-readings`.)* **Excel refuses `@;0` and
`0;@;0`**, on 12 and on `abc`. Through COM: "Unable to set the NumberFormat property of the Range class". Typed into
Format Cells' Custom box, OK leaves the dialog open with the code selected and says nothing; both cells stay General.
ExSheet refuses both, by name (SH-45), so the refusal is now observed. `NumberFormatTests.An_unsupported_code_is_refused`
says so. `0;0;@;0` was not asked.
```

**Ticket 102** (`docs/specs/exsheet/issues/102-format-cells-shows-a-ranges-outer-edge-as-drawn.md`): line 14, and the
last two lines of the Comments.

### 9. docs/specs/exsheet/issues/102-format-cells-shows-a-ranges-outer-edge-as-drawn.md

Old:

```
  clears that edge on both sides, as clearing an edge does. *(A reading: no run pressed it.)*
```

New:

```
  clears that edge on both sides, as clearing an edge does. *(A reading: no run pressed it. Observed
  2026-10-12, the seventeenth Windows run, h10.)*
```

### 10. docs/specs/exsheet/issues/102-format-cells-shows-a-ranges-outer-edge-as-drawn.md

Old:

```
    ExSheet.Engine.Tests 2340, ExGrid.Components 1356 (+1 skipped), ExSheet.MudBlazor.Tests 47,
    ExSheet.Components.Tests 605. All pass.
```

New:

```
    ExSheet.Engine.Tests 2340, ExGrid.Components 1356 (+1 skipped), ExSheet.MudBlazor.Tests 47,
    ExSheet.Components.Tests 605. All pass.

*(2026-10-12, the seventeenth Windows run, h10; branch `claude/run17gh-readings`.)*
- **Taking the shown top away is observed.** Over A2 under A1's thick bottom, Excel's Format Cells opened with a thick
  top, its button pressed; the Top button alone (Alt+T) took it away, and after OK A1's bottom was gone too.
- **Excel's Border tab opened on that thick line**, Automatic as the line was, where ExSheet opened on Thin. The same
  single press of Top would have set a thin top in ExSheet. `FormatCellsDraft` now opens on the line shown where the
  Selection is one cell and exactly one of its edges shows a line. Excel was seen to do so only for a top edge and a
  bottom edge, in Automatic or black; the rest is a reading (ticket 52's comment of this date). The tests that took
  the line away chose Thick first; they now press only Top, then OK, as the run did: `FormatCellsDraftTests`,
  `FormatCellsTests` and `MudFormatCellsTests`, `Taking_…_case_14_13`, and a new test in each of
  `format-cells.spec.mjs` and `format-cells-mud.spec.mjs`.
- **Where both cells record a line on an outer edge, which one the dialog shows is still not asked.** By the twelfth
  run's model, setting lines cannot make the state, and the run agrees: through COM, A2's thin top after A1's thick
  bottom read back as A1's bottom thin too, one line drawn on the edge. COM reads the edge as drawn, not each cell's
  record. A paste makes it: the fourteenth run's case 12 read the saved file after E5 was pasted over B2, and B2
  recorded a thick red right while C2 kept its thin blue left. That is the only run that read two records on one
  edge; the twelfth run's cases read COM and the pixels, which show the edge only. The next run asks it there: case
  12's paste, then Format Cells over C2.
```

**Ticket 103** (`docs/specs/exsheet/issues/103-mmm-under-ja-jp-is-the-month-as-a-number.md`): two notes in "Readings
that follow from the rule, not observed", and a dated comment after the last line.

### 11. docs/specs/exsheet/issues/103-mmm-under-ja-jp-is-the-month-as-a-number.md

Old:

```
  - Under en-GB a typed `d-mmm-yy` is now a code of its own and shows `5-Jan-26`. A typed
    `m/d/yyyy` shows `1/5/2026`, and a typed `h:mm` shows `9:05`.
```

New:

```
  - Under en-GB a typed `d-mmm-yy` is now a code of its own and shows `5-Jan-26`. A typed
    `m/d/yyyy` shows `1/5/2026`, and a typed `h:mm` shows `9:05`. *(Observed 2026-10-12: a typed
    `d-mmm-yy` and a typed `m/d/yyyy` are codes of their own, by the seventeenth Windows run's h9,
    on other values; `d-mmm-yy`'s `5-Jan-26` and `h:mm`'s `9:05`, by the readings of that date, R7.
    `m/d/yyyy`'s `1/5/2026` follows from its code; no run showed it. Comment below.)*
```

### 12. docs/specs/exsheet/issues/103-mmm-under-ja-jp-is-the-month-as-a-number.md

Old:

```
  - `mmmmm` under ja-JP shows `1`, the first character of `1月`. No run asked about it.
```

New:

```
  - `mmmmm` under ja-JP shows `1`, the first character of `1月`. No run asked about it.
    *(Observed 2026-10-12, the seventeenth Windows run, h9.)*
```

### 13. docs/specs/exsheet/issues/103-mmm-under-ja-jp-is-the-month-as-a-number.md

Old:

```
  - **No layer 3.** No spec read `mmm` under ja-JP.
```

New:

```
  - **No layer 3.** No spec read `mmm` under ja-JP.

*(2026-10-12, the seventeenth Windows run, h9; branch `claude/run17gh-readings`.)* Format Cells' Custom, the code
typed, on 12 October 2026 at 13:30 (46307.5625) and on 13:30 alone (0.5625). Of the readings above:
- **Observed: under en-GB, a typed `d-mmm-yy` and a typed `m/d/yyyy` are codes of their own.** They showed
  `12-Oct-26` and `0-Jan-00`, and `10/12/2026` and `1/0/1900`. On 13:30 alone, built-ins 15 and 14, spelled
  `dd-mmm-yy` and `dd/mm/yyyy` under en-GB, would show `00-Jan-00` and `00/01/1900` by those codes; no run showed
  them there.
- **Observed: under ja-JP, `mmmmm` showed `1` on both**, the first character of `10月` and of `1月`.
- **Not observed by h9: a typed `h:mm` as a code of its own.** It showed `13:30` on both, as built-in 20 (`hh:mm`)
  would. The readings of 2026-10-12 (R7, `verification/2026-10-12-windows-readings/report.md` on
  `claude/exsheet-windows-readings-2026-10-12`) observed it: `9:05` on 0.37847222, not built-in 20's `09:05` (the
  twelfth run's case 19). R7 also saw a typed `d-mmm-yy` show `5-Jan-26` on 46027, the reading's own case.
- **Not asked:** a code of its own reopened under en-US, and the month names under zh-CN and ko-KR.
- `CultureTests.A_code_typed_into_custom_shows_as_excel_showed_it_run_17_h9` pins h9's eight cells, and
  `A_code_typed_into_custom_is_no_built_in_under_en_gb_r7` R7's two. They passed as the engine stood, so
  `NumberFormat` is unchanged.
```

**The Docs Site** (`samples/ExGrid.Docs/Pages/Sheet/SheetFormatting.razor`, "Format Cells"). The page does not say
what the Border tab opens on, so nothing on it is now false. The change is one a reader meets on every Example's
Format Cells, so a sentence belongs there (CLAUDE.md, "Something visible that arrives without being asked for"). The
Change Note's `Docs:` points at this section. The page says what Excel was seen to do and what has not been asked, as
the Formulas page does ("until Excel has been asked").

### 14. samples/ExGrid.Docs/Pages/Sheet/SheetFormatting.razor

Old:

```
            opened on the Focus cell's format, with what differs across the Selection shown as Excel shows it. OK sets
            only the parts the user touched, as one undo step; Cancel and Escape set nothing. It opens from Ctrl+1,
            from "Format Cells…" in the Context Menu, and from <code>OpenFormatCellsAsync()</code>.
        </p>
```

New:

```
            opened on the Focus cell's format, with what differs across the Selection shown as Excel shows it. OK sets
            only the parts the user touched, as one undo step; Cancel and Escape set nothing. It opens from Ctrl+1,
            from "Format Cells…" in the Context Menu, and from <code>OpenFormatCellsAsync()</code>.
        </p>
        <p>
            Over one cell that shows a line on exactly one of its edges, the Border tab's Style and Colour open on
            that line, so pressing that edge's button takes the line away. Anywhere else they open on Thin and
            Automatic. Excel's was seen to open on the line shown for a top edge and a bottom edge, in Automatic or
            black, and on Thin and Automatic over a cell with a thin top and a thick right, over a range whose cells
            show different lines, and over a cell with no line. The rest is ExSheet's reading, not yet asked of
            Excel: a line on a left or right edge, a style other than Thick or Thin, a colour other than Automatic
            or black, and every other Selection.
        </p>
```

## Questions for the next Windows run

Each in a new workbook at 100% zoom, the lines set through COM on each cell's own sides unless it says otherwise, then
the cells selected, Ctrl+1, and the Border tab (Ctrl+Tab three times from Number, or the tab pressed). **Read**: the
Style list's selected entry (its focus rectangle; a picture of the list enlarged, as R8 took), the Colour box, and
which edge buttons are pressed. Escape afterwards, and read back that every line is as set.

1. **One line on a left or right edge.** (a) B2's left thick, nothing else. (b) C2's left thick and B2 recording
   nothing; select B2 (its right shows C2's line, if case 13's neighbour rule holds sideways; read whether Right is
   pressed). **Decides**: Thick in both extends what was seen on a top and a bottom edge to the left and the right;
   Thin in either limits the rule to what was seen (top and bottom), and ExSheet follows.
2. **Two edges with lines, none thin.** B2 with a thick top and a medium right of its own. **Decides**: Thin keeps the
   rule; Thick means the top's line opens (and R8 (1)'s Thin was the top's); Medium means the right's, or the last
   edge's.
3. **The same line on two edges.** B2 with a thick top and a thick bottom. **Decides**: Thin keeps "exactly one edge";
   Thick means the rule is "one line shown", whatever the number of edges.
4. **A range whose edges show one line.** A1:B1's bottom thick; select A2:B2 (its top shows the thick line, the rest
   nothing). **Decides**: Thin keeps the rule to one cell; Thick extends it to a range.
5. **Several one-cell ranges.** A1's and C1's bottom thick; A2, then Ctrl+click C2. **Decides** as 4, for a Selection
   of several ranges.
6. **A line in another colour or style.** (a) A1's bottom thick red (`#FF0000`); select A2. (b) A1's bottom medium
   dashed, Automatic; select A2. **Decides**: red in the Colour box in (a), and Medium Dashed in the Style list in
   (b), confirm that each follows the line, as Thick, Thin, black and Automatic were seen to; anything else is a
   difference to record.
7. **Ticket 102: both cells record a line on one edge.** The fourteenth run's case 12: C2's left thin blue; E5's right
   thick red, E5 = 1; E5, Ctrl+C, B2, Ctrl+V. Save, and read the file: B2 records a thick right `FFFF0000` and C2 a
   thin left `FF0000FF`. Then C2, Ctrl+1, Border. **Read** whether Left is pressed, the preview's left line (thick red
   or thin blue), and the Style and Colour on opening. Also over B2 (its right). **Decides** ticket 102's reading (the
   drawn line, B2's thick red, shows over C2) and which line the tab opens on where two records lie on one edge.
8. **h10's second case over A1.** A1's bottom thick, then A2's top thin through COM (one thin line on the edge); select
   A1, Ctrl+1, and show the Border tab before the picture. **Read** the Bottom button, the preview and the Style.
   **Decides**: the run's report says the dialog over A1 showed the thin line, but its picture shows the Number tab.
9. **Ticket 103: a code of its own reopened under en-US.** Under en-GB, A1 = 46027, `d-mmm-yy` typed into Custom, OK;
   save as `.xlsx` and read A1's `numFmt` (a custom id, 164 or above, not 15). Set-Culture en-US, a new Excel, open the
   file, A1, Ctrl+1: **read** the Number tab's category and Type. Then choose Custom, OK without a change, save, and
   read A1's `numFmt` again. **Decides** the reading that the code shows as plain `d-mmm-yy` under en-US and becomes
   built-in 15 once the Number tab is touched and OK pressed.
10. **Ticket 103: month names under zh-CN and ko-KR.** Set-Culture zh-CN, a new Excel, A1 = 46027, Custom `d-mmm-yy`,
    then `mmm`, then `mmmm`; **read** the cell's text each time. The same under ko-KR, then put the regional format
    back. **Decides** whether ExSheet keeps ICU's names there (`1月`, `1월`) or needs Windows', as it took for ja-JP.
