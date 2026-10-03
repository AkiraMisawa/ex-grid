# 97: A typed date operand reads in the culture, never invariant first

Status: done

**What to build:** ADR-0023's note of 2026-10-01, a quiet fault ticket 96 found. A date the user types in the
condition form is still read invariant first. Under en-GB, `05/01/2026` becomes 1 May, which is quietly wrong
(principle 1). Reopened dates are fine because they use the ISO form.

**Blocked by:** None (can start immediately)

- [x] **`ReadOperand` reads a date as ticket 96 reads a number:**
  - ticket 94's ISO forms exactly;
  - anything else in the culture the form shows dates in, alone;
  - a text that reads two ways, or not at all, refused by name with the same `OperandRefusal` words.
- [x] **Both Chromes' panels**, as for numbers.
- [x] **Layer 1 and 2** under en-US, en-GB, de-DE and ja-JP, named after ADR-0023 and principle 1. Cover:
  - `05/01/2026` under en-GB is 5 January;
  - the same text under en-US is 1 May;
  - a two-digit year;
  - a time with a date.

## Comments

2026-10-01, agent cf-83 (ticket 97).

### The reading

`FilterPanelChoices.ReadOperand(ColumnType.Date, text, culture)` now reads a date as ticket 96 reads a
number.
- **Ticket 94's forms first, exactly**, in every culture:
  - `yyyy-MM-dd` and `yyyy-MM-dd HH:mm:ss` read as a `DateTime`;
  - `yyyy-MM-dd HH:mm:ss zzz` reads as a `DateTimeOffset`. Before, an offset read invariant became a
    local `DateTime`.
- **Anything else in the form's culture alone** (`DateTime.TryParse(text, culture)`), never invariant
  first. Under en-GB, `05/01/2026` is 5 January; under en-US, 1 May.
  - A text the culture does not read is `NotReadable`: `13/01/2026` under en-US, `01/13/2026` under
    en-GB.
  - Another culture's order is never tried.
- **A numeric date is read in the order its year stands in:**
  - Year first: year, month, day. Every culture that writes the year first writes it in that order.
  - Year last, under a culture that writes the year last: the culture's own order of day and month.
  - Year last, under a culture that writes the year first (ja-JP's `yyyy/MM/dd`): the culture gives
    no order of day and month.
    - Where both orders are dates and they differ, the text is `ReadsTwoWays`, refused naming both:
      `“05/01/2026” reads two ways: as 2026-05-01, and as 2026-01-05. Type the one you mean as
      2026-05-01 or 2026-01-05.` .NET alone had read it as 1 May.
    - Where only one order is a date, that one is read: `01/13/2026` is 13 January.
  - A two-digit year stands where the culture writes its year, and the culture's calendar widens it:
    `05/01/26` under en-GB is 5 January 2026, and `26/01/05` under ja-JP is 5 January 2026.
  - A time after the date is read with it.
- **Refusal words.** `RefusalText` names a date that reads two ways by both ISO forms: the day alone
  at midnight, otherwise the day and time. Both read back exactly. A text the culture does not read
  gets the same words as a number.
- `OperandRefusal.ReadsTwoWays` and `OperandReading.OtherValue` are documented for dates as well. For
  a date, `OtherValue` holds month first, then day first. No public shape is added.

### Both Chromes

- **The built-in panel** already read through `ReadOperand` (ticket 96). Its typed dates now follow
  the rule above, and a refused date shows `ex-popover-refusal` with nothing applied.
- **The MudBlazor panel had the same fault in its own reader.**
  - Its `MudDatePicker` read typed text in MudBlazor's culture, the invariant one, so `05/01/2026`
    was 1 May whatever the user's culture.
  - The picker now has a converter (`OperandDateConverter`, made once per field) whose reading is
    `ReadOperand` in the current culture.
  - The converter shows a date in ISO form: the day alone at midnight, otherwise the day and time.
    The picker also needs `DateFormat="yyyy-MM-dd"`. Without it, the picker writes its date in the
    culture's short form and never asks the converter; layer 2 caught this.
  - A refused text is the field's error, in the Chrome's word for the refusal or the core's
    English, and Apply is unavailable.
  - The calendar is unchanged.

### Found, not fixed: a `DateOnly` or `DateTimeOffset` column's condition

- Neither panel knows its column's CLR date type, so a typed date reads as a `DateTime`. The exception
  is the offset form, which reads as a `DateTimeOffset`.
- The engine holds a column to one date type (`MixedDateTypes`, ADR-0023). So a condition typed on a
  `DateOnly` column, or a `DateTimeOffset` column without the offset form, is refused by the engine
  when applied. That is loud, not quiet.
- Fixing it needs the column's date type in `FilterPanelContext`, or a conversion rule, which is a
  decision. This predates ticket 97.

### Tests

All under en-US, en-GB, de-DE and ja-JP, and named after ADR-0023 and principle 1.

- **Layer 1:** `DateOperandReadingTests` (ExGrid.Tests, 35 cases):
  - `05/01/2026` under en-GB is 5 January, and under en-US 1 May;
  - each culture's own order, with two-digit years, times with the date, and month names;
  - the ISO forms exactly, the offset form included;
  - a reopened date reading back as itself;
  - ja-JP's year-last date read two ways, and refused naming both;
  - a text the culture does not read;
  - the refusal's words.
- **Layer 2, the built-in panel:** `DateOperandTests` (ExGrid.Components, 10). It covers typed dates
  in each culture (en-GB's `05/01/2026` and `05/01/26` are 5 January), a time with the date, and
  refusals by name with nothing applied.
- **Layer 2, the MudBlazor panel:** `MudDateOperandTests` (ExGrid.MudBlazor.Tests, 12). The picker
  reopens a date as `2026-01-05`, and Apply keeps it. It covers typed dates in each culture, and
  refusals as the field's error with Apply unavailable.
- **Layers 1 and 2 pass:** ExGrid.Tests 1089, ExGrid.Components 1353 (one skipped),
  ExGrid.MudBlazor.Tests 198, ExSheet.Engine.Tests 2334, ExSheet.Components.Tests 594,
  ExSheet.MudBlazor.Tests 44.
- **Layer 3**, targeted on port 5481, headless, project chrome, WebAssembly host:
  `popovers.spec.mjs` and `date-text.spec.mjs`, 65 passed. That includes the MudBlazor date
  calendar's Inner Popup cases. The host was stopped afterwards.

### Fixed later: the MudBlazor panel's refusal vanished on a later render (2026-10-02)

- CI failed `MudDateOperandTests` once on #49, en-GB `01/13/2026`: the field showed no error, and
  locally the class passed every run.
- The cause is in the panel, not the test. It bound `Date="state.Date"`, and MudDatePicker's
  `Date` is a setter that runs on every render of the panel. A date set again within 100 ms of
  the last is ignored. A refused text leaves the date null, and a null set later than that clears
  the picker's text and revalidates it, which writes the field's Error back to false over the
  panel's own. The refusal vanished while Apply stayed unavailable. A user met it by pressing
  Enter a second time; on the slow runner the panel's first render after the typing was already
  late enough.
- The panel now gives the picker its date only while the condition has one.
- `MudDateOperandTests.A_refused_date_stays_refused_when_the_panel_renders_again_later` moves
  MudBlazor's clock by hand: 200 ms between the render, the typing and two submits. It failed
  before the fix with the field showing `ValueValue`, as on CI.
- Layers 1 and 2 pass, ExGrid.MudBlazor.Tests 201. Layer 3, Linux, headed, project chrome,
  WebAssembly host: `popovers.spec.mjs` and `date-text.spec.mjs`, 65 passed.
