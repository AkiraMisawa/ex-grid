# Run 17 Part H, the readings: what the orchestrator writes

Branch `claude/run17gh-readings`, from `origin/main` (`a533de01`). The seventeenth Windows run's Part H
(`verification/2026-10-11-windows-17/report.md` on `claude/exsheet-windows-verify-17`, h4, h9, h10 and h12;
`records/part-h-excel.jsonl`) asked Excel four things the code had built as readings. Excel answered each as the
reading said, so nothing in `src/` changed: the tests that called them readings now say they were observed
(ADR-0208: a recorded answer becomes a test), and h4 and h9 gain the cases the run read. Nothing a Consumer sees
changed, so there is no Change Note and nothing for the Docs Site.

## What was done, finding by finding

| # | Excel (run 17) | Test | Done |
|---|---|---|---|
| h4 | A1:B2, Ctrl+click F6, Ctrl+click F6 again: Selection `A1:B2`, A1 active. Enter: A2, B1, B2, the Selection kept | `tests/ExGrid.Tests/SelectionGestureTests.cs`, `Taking_out_the_whole_range_made_last_moves_the_focus_to_the_latest_range_left` | Comment: observed (h4). Enter three times through `GridSelection.CycleFocus(CycleOrder.ColumnMajor, …)`: the Focus A2, B1, B2, the Ranges still A1:B2. Passed as written |
| h9 | en-GB: `d-mmm-yy` `12-Oct-26` / `0-Jan-00`; `m/d/yyyy` `10/12/2026` / `1/0/1900`; `h:mm` `13:30` / `13:30`. ja-JP: `mmmmm` `1` / `1` | `tests/ExSheet.Engine.Tests/CultureTests.cs`, `A_code_typed_into_custom_shows_as_excel_showed_it_run_17_h9` (new, 8 cases) | Each code read through `NumberFormat.TryParseLocal` as Format Cells reads it; checked to be no date or time built-in's, spelled back as typed by `LocalCode`, and shown as Excel showed it on 46307.5625 and 0.5625. All 8 passed on the first run, so `NumberFormat.cs` is unchanged |
| h10 | Over A2 under A1's thick bottom, Top taken away, OK: A1's bottom gone too | `tests/ExSheet.Components/FormatCellsDraftTests.cs`, `Taking_a_shown_edge_away_clears_it_on_both_sides_case_14_13` | "(a reading: no run pressed it)" becomes "(observed in Excel, the seventeenth Windows run, h10)" |
| h12 | `@;0` and `0;@;0` refused: COM's "Unable to set the NumberFormat property of the Range class"; Format Cells keeps the dialog open, the code selected, and says nothing | `tests/ExSheet.Engine.Tests/NumberFormatTests.cs`, `An_unsupported_code_is_refused` | The `@;0` comment says Excel refuses both too, and how; ExSheet refuses by name (SH-45). `0;0;@;0` is marked "not asked of Excel" |

**One correction to the triage's wording for h10, so the ADR is not quietly wrong.** The triage proposed "Excel keeps
one line per edge, so 'both cells record one' cannot be made there, and the upper-or-left rule meets only a Sheet
Document written elsewhere." That holds for a line *set* on an edge, through COM or Format Cells: h10, the eleventh
run's case 7 (C2's thin blue left replaced B2's thick red right), and the twelfth run's model in ADR-0071, "How Excel
keeps the line between two cells" (setting an edge clears the neighbour's record). It does not hold for a copy or a
deletion:

- the fourteenth run's case 12 (`verification/2026-10-01-windows-excel-14/cell-format-14.md`, line 262) read the
  saved file after E5 was pasted over B2: **B2 recorded a thick red right while C2 kept its thin blue left**, the
  thick red drawn. ADR-0071 already says so ("Each cell records its own sides … After a paste, C2 still records its
  thin blue left under B2's thick red right");
- the twelfth run observed it after a paste, a Ctrl+D and a deletion (its cases 1, 3, 6, 7 and 9), and drew the upper
  or left cell's line;
- ExSheet makes the same state the same ways (`CellFormatCarryTests`, the twelfth run's cases).

So the state is made in Excel and in ExSheet alike, not only by a Sheet Document written elsewhere, and which line
Format Cells shows there is still unasked. The text below says that, and names case 12's paste as the way a run can
ask it.

**SL-5 needs no change.** Its pass criterion is "every take-out in ADR-0052's third-run section gives Excel's Focus
and fragment order"; with the bullet below, the take-out of the whole range made last is one of them, observed, and
the layer-1 test covers it.

## ADR-0052: what to write

In "What the third run settled", replace the last bullet.

Old:

```
- **Read, not observed**: when a take-out removes the whole range made last (A1:B2, then Ctrl+click
  F6, then Ctrl+click F6 again), the latest range still standing takes its place, so the Focus goes
  to A1. Asked of Excel in the next Windows run.
```

New:

```
- **Read, not observed**: when a take-out removes the whole range made last (A1:B2, then Ctrl+click
  F6, then Ctrl+click F6 again), the latest range still standing takes its place, so the Focus goes
  to A1. Asked of Excel in the next Windows run.
  *(Observed 2026-10-12, the seventeenth Windows run, h4: the Selection is A1:B2 and the active cell
  A1. Enter then goes to A2, B1 and B2, in Enter's order down each column, with the Selection kept.)*
```

## ADR-0071: what to write

Three dated notes, each under the reading it answers. The readings stay as written.

### h9, in "What the fourteenth Windows run settled", `mmm` under ja-JP

Old:

```
  - The same rule holds under every culture. Under en-GB, `d-mmm-yy` typed into Format Cells shows
    `5-Jan-26`, not built-in 15's `05-Jan-26`. No run has typed it there.
```

New:

```
  - The same rule holds under every culture. Under en-GB, `d-mmm-yy` typed into Format Cells shows
    `5-Jan-26`, not built-in 15's `05-Jan-26`. No run has typed it there.
    *(Observed 2026-10-12, the seventeenth Windows run, h9: ticket 103's readings are Excel's. Under
    en-GB, `d-mmm-yy`, `m/d/yyyy` and `h:mm` typed into Custom are codes of their own and show as
    spelled: on 12 October 2026 at 13:30, `12-Oct-26`, `10/12/2026` and `13:30`; on 13:30 alone,
    `0-Jan-00`, `1/0/1900` and `13:30`. Under ja-JP, `mmmmm` shows `1` on both, the first character of
    `10月` and of `1月`. `h:mm`'s one-digit hour, `9:05` where the built-in shows `09:05`, is still a
    reading: 13:30 has two digits either way.)*
```

### h10, in "What the fourteenth Windows run settled", ticket 57's model

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
    Cells opened with a thick top, its button pressed; the Top button took it away, and after OK A1's
    bottom was gone too. Neither cell keeps a line there.)*
  - Where both cells record a line on an outer edge, the dialog shows the one drawn, the upper or
    left cell's (ticket 102). No run looked.
    *(2026-10-12, the seventeenth Windows run, h10: still not asked. A line set on an edge, through
    COM or Format Cells, clears the neighbour's record of it, as the twelfth run's model says: A2's
    thin top set after A1's thick bottom read back as A1's bottom thin too, and Format Cells over
    either cell showed that thin line. So the run could not make the state by setting lines. A copy or
    a deletion does make it, in Excel as in ExSheet (case 12 above; the twelfth run's cases 1, 3, 6, 7
    and 9). A run asks this by case 12's paste, then Format Cells over C2.)*
```

### h12, in "Readings until the fourteenth Windows run", tickets 88 and 89

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
    - *(2026-10-12, the seventeenth Windows run, h12.)* `@` before the last section is refused in
      Excel too, as ExSheet refuses it. Excel refused `@;0` and `0;@;0` through COM ("Unable to set the
      NumberFormat property of the Range class"), and its Format Cells kept the dialog open with the
      code selected and said nothing. ExSheet's Format Cells refuses them by name (SH-45). `0;0;@;0`
      was not asked.
```

## Tickets: dated comments

### Ticket 89 (`docs/specs/exsheet/issues/89-a-text-section-may-carry-a-colour.md`)

Old (line 53):

```
- `@;0` and `0;@;0` stay refused: no run has seen `@` before the last section.
```

New:

```
- `@;0` and `0;@;0` stay refused: no run has seen `@` before the last section. *(Excel refuses both:
  the seventeenth Windows run, h12, below.)*
```

Old (the last line):

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

### Ticket 102 (`docs/specs/exsheet/issues/102-format-cells-shows-a-ranges-outer-edge-as-drawn.md`)

Old (line 14):

```
  clears that edge on both sides, as clearing an edge does. *(A reading: no run pressed it.)*
```

New:

```
  clears that edge on both sides, as clearing an edge does. *(A reading: no run pressed it. Observed
  2026-10-12, the seventeenth Windows run, h10.)*
```

Old (the last two lines of the Comments):

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
  top, its button pressed; the Top button (Alt+T) took it away, and after OK A1's bottom was gone too.
  `FormatCellsDraftTests.Taking_a_shown_edge_away_clears_it_on_both_sides_case_14_13` now says observed.
- **Where both cells record a line on an outer edge, which one the dialog shows is still not asked.** The run could not
  make the state by setting lines: through COM, A2's thin top after A1's thick bottom read back as A1's bottom thin
  too, one line on the edge. A paste or a deletion does make it, in Excel and in ExSheet (the fourteenth run's case
  12; the twelfth run's cases 1, 6 and 7), so the next run can ask it there: case 12's paste, then Format Cells over
  C2.
```

### Ticket 103 (`docs/specs/exsheet/issues/103-mmm-under-ja-jp-is-the-month-as-a-number.md`)

Old (the last line):

```
  - **No layer 3.** No spec read `mmm` under ja-JP.
```

New:

```
  - **No layer 3.** No spec read `mmm` under ja-JP.

*(2026-10-12, the seventeenth Windows run, h9; branch `claude/run17gh-readings`.)* **The readings are observed.**
Format Cells' Custom, the code typed, on 12 October 2026 at 13:30 (46307.5625) and on 13:30 alone (0.5625):
- Under en-GB, `d-mmm-yy` showed `12-Oct-26` and `0-Jan-00`, `m/d/yyyy` `10/12/2026` and `1/0/1900`, and `h:mm`
  `13:30` on both. Each is a code of its own, shown as spelled, not built-in 15, 14 or 20, which en-GB spells
  `dd-mmm-yy`, `dd/mm/yyyy` and `hh:mm`.
- Under ja-JP, `mmmmm` showed `1` on both, the first character of `10月` and of `1月`.
- `h:mm`'s one-digit hour (`9:05`, where the built-in shows `09:05`) is still read: 13:30 has two digits either way.
- `CultureTests.A_code_typed_into_custom_shows_as_excel_showed_it_run_17_h9` pins the eight cells. They passed as
  the engine stood, so `NumberFormat` is unchanged.
```
