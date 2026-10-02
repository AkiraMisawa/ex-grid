# 103: `mmm` under ja-JP is the month as a number

Status: done

**What to build:** the fourteenth Windows run's cases 10 and 11. ADR-0071, "What the fourteenth Windows
run settled", `mmm` under ja-JP:

- **Under ja-JP, `mmm` shows the month as a number with no leading zero, in every code.** 5 January
  2026 shows `05-1-26` in `dd-mmm-yy`, `5-1-26` in `d-mmm-yy`, `1 5, 2026` in `mmm d, yyyy`, and
  `2026/1/05` in `yyyy/mmm/dd`. `mmmm` shows `1月`. These are Windows' month names; .NET's `1月` for
  `mmm` is ICU's. Today only built-in 15 shows the number.
- **A code typed into Format Cells is a built-in only when it spells that built-in's code under the
  Sheet's culture.** Under ja-JP `dd-mmm-yy` is built-in 15; `d-mmm-yy` is a code of its own.

**Blocked by:** None (can start immediately)

- [x] **The four codes and `mmmm` above show as the run read them under ja-JP**, on Linux (ICU) and on
      Windows alike. Do it as `NumberFormat.AbbreviatedMonthNamesOf` did `Sept`: one place, named for
      the platform data it replaces.
- [x] **en-GB and en-US are unchanged** (`05-Jan-26`, `5-Jan-26`).
- [x] **Typed into Format Cells under ja-JP:** `dd-mmm-yy` is recorded as built-in 15, and `d-mmm-yy`
      as its own code. The same rule holds for the other built-ins ExSheet localises.
- [x] **Layer 1/2**, and the Linux check in Docker (`mcr.microsoft.com/dotnet/sdk:10.0`) as for `Sept`.

## Comments

*(2026-10-02, agent cf-102.)* Both halves are built. **One choice of mechanism needs recording in
ADR-0071**: how a code of its own is kept apart from the built-in it spells (below, "A decision to
record").

- **`mmm` under ja-JP, in one place.** `NumberFormat.AbbreviatedMonthNamesOf` now also replaces
  ICU's Japanese month abbreviations (`1月` … `12月`, the full names) with Windows' (`1` … `12`), as it
  replaces `Sept` with `Sep`. So `mmm` is the month's number in every code under ja-JP, and `mmmm`
  stays `1月`. Ticket 58's special case for built-in 15 under ja-JP (`dd-m-yy`) is gone: 15's ja-JP
  form is now `dd-mmm-yy`, Excel's own local code, and it shows `05-1-26` through the month names.
  The other users of the names (typed dates, the fill's series) are unaffected: a typed month name
  must be letters, and text holding a digit already continues a series.
- **A typed code is a built-in only where it spells it.** Two new public members on `NumberFormat`:
  - `LocalCode(culture)` spells a format as the culture's Format Cells does. A built-in ExSheet shows
    in the culture's form is spelled in that form, so 15 is `dd-mmm-yy` under ja-JP and en-GB.
  - `TryParseLocal(code, culture, …)` reads a code typed under Custom. It is a built-in when it spells
    one of the localised built-ins' `LocalCode`, in any case.
  - A code that spells a localised built-in's *invariant* code where the culture spells that built-in
    otherwise is a code of its own. That is `d-mmm-yy` under ja-JP, `5-1-26`.
  - Any other code is read as `TryParse` reads it.
  - The built-ins are 14, 22, 16, 15, 20 and the currency (6 and 8).
- **A decision to record (proposal).** A Sheet Document records a Number Format as its invariant
  code, and that code already *means* the built-in. A code of its own that spells it therefore needs
  another spelling. **It is recorded with its separators escaped**: `d\-mmm\-yy`, `m\/d\/yyyy`,
  `h\:mm`, `\$#,##0.00_);[Red](\$#,##0.00)`.
  - Excel reads that as the same code, and it is no built-in's.
  - It shows as spelled under every culture.
  - It needs no change to the Sheet Document.
  - `LocalCode` spells it back without the escapes, so Format Cells shows what was typed.
  - The alternative was a field in the Sheet Document marking a code as custom, as Excel's file does
    with its format ids. That costs a version and gives the same fidelity.
  - **This is a mechanism choice ADR-0071 does not state.** I could not edit the ADR, so I took the
    cheaper option. ADR-0071's case 11 bullet could say so.
- **Format Cells follows the rule wherever it would otherwise turn a built-in quietly into a code of
  its own.**
  - The Custom box reads its code with `TryParseLocal`.
  - Custom opens on a code no category writes in the culture's spelling. The currency key's
    built-in under en-GB now opens as `£#,##0.00;[Red]-£#,##0.00`, where it showed
    `$#,##0.00_);[Red]($#,##0.00)` before.
  - Custom starts from the shown code in the culture's spelling: Date's `d-mmm-yy` becomes
    `dd-mmm-yy` under ja-JP. So choosing Custom and pressing OK keeps the built-in.
  - Custom's list of codes is in the culture's spelling, each once. Under en-GB it lists
    `dd/mm/yyyy`, `dd-mmm-yy`, `dd-mmm`, `hh:mm` and `dd/mm/yyyy h:mm`, and under en-US it is
    unchanged.
  - The Date and Time lists still hold the invariant codes, and a choice from them is the built-in.
- **Readings that follow from the rule, not observed.**
  - Under en-GB a typed `d-mmm-yy` is now a code of its own and shows `5-Jan-26`. A typed
    `m/d/yyyy` shows `1/5/2026`, and a typed `h:mm` shows `9:05`.
  - A code of its own opened under a culture that spells the built-in the same way (en-US) shows
    the plain code. If the user touches the Number tab and presses OK, it becomes the built-in. The
    two show alike there.
  - `mmmmm` under ja-JP shows `1`, the first character of `1月`. No run asked about it.
  - zh-CN and ko-KR keep ICU's names. Only ja-JP was observed.
- **The glyph corpus changed** (ADR-0016, tickets 82, 83 and 91). The built-in formats now paint
  `01-1`, `30-1`, `1-26` … under ja-JP, which `GlyphCorpusTests` caught.
  - `corpus.json` was regenerated: 3,065 strings became 3,091. The same corpus comes out on macOS
    and on Linux.
  - The three macOS records were measured again with `measure.mjs` in headless Chrome 154. That is
    not layer 3: no DemoHost and no Playwright.
  - Every glyph width and every width of a string already in the corpus read the same as before.
    The new strings are digits and hyphens.
  - `tables.mjs` wrote the tables unchanged.
  - **A branch merged later that also changes the corpus will need its records measured again.**
- **Tests.**
  - **Layer 1**, `CultureTests`, 25 new cases:
    - case 10's five codes under ja-JP;
    - en-GB and en-US unchanged;
    - ICU's and Windows' Japanese names both giving `1`;
    - typed codes under ja-JP, en-GB and en-US;
    - every localised built-in's spelling and its own code under en-GB and ja-JP, both ways;
    - a code of its own surviving the Sheet Document;
    - other codes read as written.
  - **Layer 2, draft:** `FormatCellsDraftTests`, 6 new tests:
    - the typed rule;
    - a code of its own reopening as typed;
    - Custom starting from the culture's spelling;
    - en-GB's currency built-in under Custom;
    - Custom's list in the culture's spelling;
    - a typed code kept when Custom is chosen again.
  - **Layer 2, Chromes:** `FormatCellsTests` and `MudFormatCellsTests`, each a theory of 2 cases.
    `dd-mmm-yy` and `d-mmm-yy` are typed under ja-JP through each Chrome, showing `05-1-26` and
    `5-1-26`.
  - **Mac:** `nix develop -c dotnet test ExGrid.slnx` passes: ExGrid.Tests 1089,
    ExGrid.MudBlazor.Tests 198, ExSheet.Engine.Tests 2365, ExGrid.Components 1356 (+1 skipped),
    ExSheet.MudBlazor.Tests 49, ExSheet.Components.Tests 613.
  - **Linux (ICU)**, in Docker (`mcr.microsoft.com/dotnet/sdk:10.0`), on a copy of the working tree,
    all pass: ExSheet.Engine.Tests 2365, ExSheet.Components 613, ExGrid.Tests 1089,
    ExGrid.MudBlazor.Tests 198, ExSheet.MudBlazor.Tests 49.
  - **No layer 3.** No spec read `mmm` under ja-JP.

