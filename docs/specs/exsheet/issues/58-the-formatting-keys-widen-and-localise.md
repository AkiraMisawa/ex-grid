# 58: The formatting keys widen a standard-width column, and localise the date and time built-ins

Status: done

**What to build:** [ADR-0071](../../../adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md),
"What the twelfth Windows run settled", cases 17 to 19
(`verification/2026-10-01-windows-excel-12/cell-format-12.md`).

**Blocked by:** None (can start immediately)

- [x] **Widening** (case 17).
  - A formatting key widens a column still at the standard width when a cell's formatted text no
    longer fits. The width is the one General's fitting and the `####` decision use (ADR-0047).
  - The column then leaves the standard width, and is recorded as a column widened by an entry is
    (ADR-0046, SH-26).
  - A column the user has sized never widens (case 18).
  - Format Cells' OK and `SetCellFormatAsync` do the same. That is a reading, not yet observed.
  - The widening is part of the key's one undo step.
- [x] **The date key** writes built-in 15 (`d-mmm-yy`), localised by the Sheet's culture.
  - en-GB shows `05-Jan-26`, en-US `5-Jan-26`, and ja-JP `05-1-26` (the month as a number).
- [x] **The time key** writes built-in 20 (`h:mm`), localised: en-GB shows `09:05`, ja-JP `9:05`.
  - Under en-US it writes the AM/PM built-in, which shows `9:05 AM`.
- [x] **Recording**: both are recorded as culture-localised built-ins, as the short date and the
      currency key's format are (ticket 51).
- [x] **Tests**: layer 1 for the localised built-ins; layer 2 for the widening and its undo. Name
      each with ADR-0071 and case 17, 18 or 19.

## Comments

*(2026-10-01, agent cf-58, built.)* Both halves, in the engine and in ExSheet.

- **The localised built-ins** (case 19), in `NumberFormat.Format` beside the short date and the
  currency (`DateOrTimeIn`). The Sheet Document records `d-mmm-yy` and `h:mm`; what shows is the
  culture's form.
  - **15:** the day has two digits where the culture's short date writes it so, as built-in 16
    already does. Under ja-JP the month is its number. So the date shows `05-Jan-26` (en-GB),
    `5-Jan-26` (en-US) and `05-1-26` (ja-JP).
  - **20:** the hour has two digits where the culture's short time writes it so: `09:05` (en-GB),
    `9:05` (en-US, ja-JP).
  - **The AM/PM built-in** (`h:mm AM/PM`, which the time key writes where the culture's short time
    is 12-hour, as ticket 51 built it) shows as it is spelled: `9:05 AM` under en-US.
  - A date typed with a month name and a year records 15, so it now shows the same way:
    `5-Jan-2026` reads `05-Jan-26` under en-GB.
- **The widening** (cases 17 and 18).
  - `SheetWidening.Of` is one rule for an entry and for a Number Format. For every number in the
    cells, the engine says how many characters it needs under its Number Format as it is now
    (`Sheet.GetWidthOnEntry`, unchanged). A column the user has not sized widens to hold the widest
    of them, in the grid's own estimate, so the grid does not hash it (ADR-0016).
  - The width is recorded as one an entry widened (SH-26).
  - The entry's own widening goes through the same helper. Its old `WidenOnEntry` is gone.
  - `ExSheet.FormatSelectionAsync` passes the formatted ranges to `PerformAsync` when the change
    names a Number Format. The widths are steps of the same operation, so one undo puts back the
    format and the widths, and one redo sets both again.
  - The keys, Format Cells' OK and `SetCellFormatAsync` (with its two shorthands) all go through
    `FormatSelectionAsync`, so all of them widen. This is the reading the ticket names, and it is
    built that way.
  - `DoAsync` does a `SheetEdit` as it is written and widens nothing, just as `DoAsync(Enter)` does
    not widen today.
  - **New engine member:** `Sheet.EntryAddressesIn(CellRange)`, the Entries in a range. It walks
    the range or the Sheet's cells, whichever is fewer, so a key over whole columns or whole rows
    costs what the Sheet holds.
- **Readings built, not observed.** Each is a case for a later Windows run.
  - **A widened column widens again.** A column an entry or an earlier key widened is widened
    again by a key whose text is longer, as SH-26's widened-by-entry kind is. Case 17 asked only
    about columns at the standard width.
  - **General widens as an entry does.** Ctrl+Shift+~ widens a column too narrow for every integer
    digit, as typing the number would. Case 17 did not ask about `~`.
  - **Only a Number Format widens.** A Font, Fill or Border change leaves the width alone, even
    over a number already showing `####`. Its text does not change, and the metrics already charge
    the boldest weight.
- **Found.**
  - **Evidence for the reading.** The corpus already supports it for a Number Format set any way.
    FMT-052 and FMT-072 enter a General number and then set `Range.NumberFormat` through COM, and
    Excel's column then read 17.09 and 10.82 against a standard 8.09.
  - **An observation the ADR does not explain.** The eleventh run's case 20 recorded `$` under
    en-US as `########` at the standard width, which means the column did not widen. ADR-0071
    says every key widens a column at the standard width. This ticket follows the ADR. The
    eleventh run's procedure should be checked, or the case asked again.
  - **`mmm` under ja-JP.** Excel's own local code for 15 under ja-JP reads `dd-mmm-yy`, yet it
    showed the month as a number. That suggests Excel shows `mmm` as the month's number under
    ja-JP in every format, built-in 16 and Custom codes too. ExSheet still shows .NET's `1月`
    there. Only 15's local form was changed.
  - **Other cultures are derived, not observed.** Their forms come from .NET's patterns: the
    two-digit day or hour. Under de-DE, for instance, 15 reads `dd-mmm-yy`, while German Excel
    spells 16 `TT. MMM`.
  - **Excel's widths are not ExSheet's.** ExSheet widens a column for `05-Jan-26`, which fitted
    Excel's 8.09 in case 19. ExSheet charges every character one digit (ADR-0047, third round),
    so nine characters do not fit 8.43. That is the same decision that would otherwise show `####`.
- **Tests.**
  - **Layer 1:** `ExSheet.Engine.Tests` 2122, 15 new: 14 in `CultureTests` (the built-ins under
    three cultures, recorded as their codes, reopened under another culture, typed, and the width
    they need) and 1 in `ColumnWidthTests` (`EntryAddressesIn`).
  - **Layer 2:** `ExSheet.Components.Tests` 432, 18 new, in `FormatWideningTests`. They cover each
    key that widens and each that fits, the one undo step, the recorded kind, the user's column,
    several columns, a whole column, only a Number Format widening, `SetCellFormatAsync`, Format
    Cells' OK and `DoAsync`, and the date and time keys under three cultures.
  - The other suites pass unchanged: `ExGrid.Tests` 857, `ExGrid.Components` 1094 and 1 skipped,
    `ExGrid.MudBlazor.Tests` 88.
  - No layer 3, as the ticket says.
