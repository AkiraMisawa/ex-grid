# 91: ExSheet's fitting and widening use the grid's glyph widths

Status: done

**What to build:** the Excel gap left after ticket 83 (its comment, part 1). ExSheet's General fitting, its `####`
decision and ticket 58's widening charge every character one digit width (ADR-0047). So under en-GB the date key's
`05-Jan-26` widens a standard-width column (9 × 9.75 + 16 = 103.75px > 99), where Excel fits it at 8.09 (the
twelfth run's case 19). The core now holds per-glyph widths for each face (ticket 83, ADR-0016).

**Blocked by:** None (can start immediately)

- [x] **Charge Number and Date text with the grid's metrics.** ExSheet's fitting, its `####` decision and its
      widening use `CellTextMetrics.For(ColumnType.Number/Date)` for the column's face and weight, bold included,
      instead of one digit per character.
- [x] **Keep what ADR-0047 settled where Excel counts characters.** For example, the width recorded in the
      Sheet Document stays in Excel's character units. Say in the comment what changed and what did not.
- [x] **Layer 1 and 2** against the runs' cases:
  - run 12, case 17 (which keys widen, and to what);
  - run 12, case 19 (`05-Jan-26` fits at 8.09 under en-GB);
  - the corpus: nothing is cut.
- [x] **Say how close each case comes to Excel's width**, before and after.

## Comments

2026-10-01, agent cf-83 (ticket 91).

### What changed

- **General fitting** (`SheetRow.PaintedAt`, `SheetColumns.PaintedText`). The grid hands ExSheet the
  content width in pixels and the Cell Metrics it judges the cell with, the bold ones for a bold
  cell. ExSheet asks the engine for General's text at the width in characters that fits, and at one
  character more. It paints the widest text those metrics charge within the content, each glyph at
  its own width; where none fits, it paints `####`.
  - A decimal point, narrower than a digit, can leave room for one more digit.
  - An exponent's `+`, wider than a digit, can take one away. The cell then shows `1.2E+08` where it
    used to paint `1.23E+08`, which the grid then hashed.
  - The search starts one character past the digits, because General's text holds at most one
    glyph narrower than a digit: its decimal separator.
- **Ticket 58's widening** (`SheetWidening`). A column widens to what the grid's estimate charges for
  the text the cell shows, each glyph at its own width, bold where the cell's Font is.
  - It is no longer floored at one digit a character.
  - So `05-Jan-26` needs 102px under the core's widths, not the 104 that nine digits took, and
    `£1,234,567.50` needs 133px, not 143.
- **The `####` decision** for a number in any other format was already the grid's (ADR-0016) and
  per glyph since ticket 83. It did not change.

### What did not change: ADR-0047's character unit

- The engine still takes a column's width in characters of the default font, and fits General to
  whole characters.
- The Sheet Document records widths in characters (Excel's unit). They are converted at one digit of
  the grid's font a character (`SheetColumns.PxOf`, `CharactersOfColumn`).
- The default column is still 8.43 characters, 99px.
- Only what fits in a width is charged glyph by glyph. ADR-0047's third round said one digit a
  character was "an estimate for other characters until the case corpus has observed Excel"; the
  twelfth run's cases 17 and 19 are that observation.

### How close each case comes to Excel

All widths below are in characters: digits of the default font.
- The core's character is 9.75px, so its standard column is 8.51 characters.
- Roboto's character is 8.3px. ExSheet's default is 99px either way, which is 10.00 Roboto characters.
- Excel's standard on the run's machine was 8.09 (Aptos Narrow).

**Case 17, the widened widths.** Pass b set the standard width to 4, so every key widened:

| key, text | Excel | core before | core after | Roboto before | Roboto after |
|---|---|---|---|---|---|
| `#` `09-Dec-25` | 8.73 | 9.03 (+0.30) | 9.03 (+0.30) | 9.04 (+0.31) | 8.92 (+0.19) |
| `$` `£1,234,567.50` | 11.82 | 13.03 (+1.21) | **12.00 (+0.18)** | 13.01 (+1.19) | **12.17 (+0.35)** |
| `!` `1,234,567.50` | 10.82 | 12.00 (+1.18) | **10.97 (+0.15)** | 12.05 (+1.23) | **11.20 (+0.38)** |
| `%` `123456750%` | 10.73 | 10.46 (−0.27) | 10.46 (−0.27) | 10.36 (−0.37) | 10.36 (−0.37) |
| `^` `1.23E+06` | 7.73 | 8.10 (+0.37) | 8.10 (+0.37) | 8.07 | 7.95 (+0.22) |
| `@` `12:00` | 4.73 | 5.03 (+0.30) | **4.72 (−0.01)** | 5.06 | **4.70 (−0.03)** |

- Every key now widens to within 0.4 characters of Excel. Before, `$` and `!` were 1.2 over.
- In pass a, the standard column: `12%`, `1.23E+06` and `00:00` fit, as in Excel. In the core, the
  date, `$` and `!` keys widen, as Excel's did. Under Roboto, the default column is 10 characters,
  so the date fits it, where Excel widened at its 8.09.

**Case 19, the date and time keys at the standard width:**

| culture, text | Excel | core before | core after | Roboto before | Roboto after |
|---|---|---|---|---|---|
| en-GB `05-Jan-26` | fits 8.09 | widens to 9.03 | **widens to 8.82** | fits | fits |
| en-GB `09:05` | fits | fits | fits | fits | fits |
| en-US `5-Jan-26` | fits | fits | fits | fits | fits |
| en-US `9:05 AM` | fits | fits | fits | fits | fits |
| ja-JP `05-1-26`, `9:05` | fits | fits | fits | fits | fits |

**One case still differs from Excel: en-GB's `05-Jan-26` in the core.**
- The core's regular widths cover every weight the grid paints regular text in, 400 to 600. ExGrid
  paints group and total rows at 600, and in DejaVu Sans Bold `05-Jan-26` is 75.2px.
- So the estimate is 85.9px, past the default column's 83px of content, though SF paints the text at
  70.4px at 400 on a Mac.
- To fit it, ExSheet's cells would have to be charged at the weight they paint, 400. That needs a
  regular tier at 400 beside one at 600, in `CellTextMetrics`, the core's presets and every Wrapper's
  widths. It changes what ADR-0016 decided about the regular widths, so it is not done here.
- It would not bring the whole run closer to Excel:
  - **Case 19:** `05-Jan-26` would fit, needing 94.5px at 400, as Excel's did.
  - **Case 17's date:** `09-Dec-25` would fit too (96.7px), where Excel widened it.
  - **Case 17's numbers:** they would widen less than Excel's: `$` to 10.87, `!` to 9.95, `%` to 9.64.
- So it trades case 17's closeness for case 19's, and is left for the user to weigh.

### The corpus: nothing is cut

- The corpus now holds General fitted to every width from 1 to 12 characters as well. That is 50
  more strings, 3,065 in all, such as `1.2E+11`, `0.12` and `-0`.
- The three faces were measured again, and the tables regenerated; they are unchanged.
- The corpus tests pass for the core and Roboto, at 14px and 12px and at every weight. Every text
  ExSheet can paint for those values fits within its estimate.

### Tests

- **`PaintedTextTests`**:
  - a decimal point leaves room for one more digit;
  - an exponent's `+` takes one away, rather than `####`;
  - a bold cell is fitted with the bold widths.
- **`FormatWideningTests`**:
  - the widened width is the estimate, not floored at a digit a character. `WidenedFor` changed,
    and with it seven expectations in case 17;
  - en-GB's `05-Jan-26` widens to its estimate, 102px, short of nine digits' 104;
  - a bold number widens by the bold widths.
- **`GlyphCorpusTests`**: the corpus includes General's fitted texts.
- Each new test fails on the code before this ticket.
- Layers 1 and 2 pass: ExGrid.Tests 1001, ExGrid.Components 1276 (one skipped), ExGrid.MudBlazor.Tests
  168, ExSheet.Engine.Tests 2317, ExSheet.Components.Tests 590, ExSheet.MudBlazor.Tests 38.
- **Layer 3**, targeted on port 5481, headless, project chrome, WebAssembly host:
  `format-keys.spec.mjs` and `appearance.spec.mjs`, 38 passed. The host was stopped afterwards.

### For the orchestrator

- ADR-0047's third round says "Every character counts as one digit width" for fitting and widening.
  The unit stands; what fits in it is now charged glyph by glyph.
- No public shape changed. `SheetRow.PaintedAt` is internal.
- The 400-weight tier above is a proposal, with the trade it makes.
