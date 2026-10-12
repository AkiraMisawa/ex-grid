# The area passes, dates and text: what the orchestrator writes

Branches `claude/area-dates-and-text` (the first area pass) and `claude/area2-dates-and-text` (the second), from
`claude/area-corrections`. The first pass (`verification/2026-10-12-windows-area/`) disagreed with four cases of this
area, and the second (`verification/2026-10-12-windows-area-2/`, Excel 16.0.20430.20146, en-US, through COM) asked the
24 `uncertain` cases the first round's corrections added: 20 agreed and were promoted by `-Update`, and four disagreed.
Each disagreement was a refusal "until Excel is asked"; Excel answered, and each now follows the answer under ADR-0202's
rule and ADR-0047's admission rule. Nothing here changes an ADR, `CONTEXT.md`, the Definition of Done, the engine's
README, the function catalogue or the Docs Site: the text for each is below, in the repository's voice, as exact old ->
new edits. Each old and new text is a fenced block of whole lines, each line with the target file's own leading
indentation (a bullet's continuation two spaces, a sub-bullet's four); each old text is exact and occurs once in its
file at `claude/area2-dates-and-text`.

## What was done, case by case

| Case | Excel | Was | Now | The rule it takes |
|---|---|---|---|---|
| EOMONTH-010 `=EOMONTH(0,1)` | 59 (pass 1) | `#VALUE!` (refused) | **59**, observed | the start read as `MONTH` reads it (MONTH-004: serial 0 is in January 1900); months on the real calendar |
| EDATE-010 `=EDATE(60,1)` | 89 (pass 1) | `#VALUE!` (refused) | **89**, observed | the start read as `DAY` reads it (DAY-002: serial 60 is the 29th of February 1900); the day kept, or the month's last |
| TEXTFN-025 `=TEXT(-1/3,"General")` | `-0.333333333` (pass 1) | `#VALUE!` (refused) | **`-0.333333333`**, observed | General at its own limit, 11 characters, the minus sign beside them |
| TEXTFN-028 `=TEXT(1/3,"@")` | `0.333333333` (pass 1) | `#VALUE!` (refused) | **`0.333333333`**, observed | a code of a text section alone shows a number as `TEXT`'s General does |
| EDATE-016 `=EDATE(0,1)` | 31 (pass 2) | `#VALUE!` (refused) | **31**, observed | serial 0 keeps its day 0 (DAY-004), the day before the 1st of the month reached |
| EDATE-017 `=EDATE(-0.5,1)` | `#NUM!` (pass 2) | `#VALUE!` (refused) | **`#NUM!`**, observed | the start cut down to its day, -1, as `YEAR` reads a serial (YEAR-008) |
| EOMONTH-016 `=EOMONTH(-0.5,0)` | `#NUM!` (pass 2) | `#VALUE!` (refused) | **`#NUM!`**, observed | the same |
| EOMONTH-017 `=EOMONTH(-0.5,1)` | `#NUM!` (pass 2) | `#VALUE!` (refused) | **`#NUM!`**, observed | the same |

**Agreed in the second pass, and promoted** (the engine's answer was Excel's): EDATE-013 to 015 (59, 425, 29),
EOMONTH-012 to 015 (59, 91, 31, `#NUM!`), YEAR-008, MONTH-005, DAY-005, WEEKDAY-008, DAYS-005, NETWORKDAYS-009 and
WORKDAY-010 (each `#NUM!` for a serial of -0.5), and TEXTFN-029 to 034 (`-1.23457E+11`, `-12345678901`,
`-0.333333333`, `1.23457E+11`, `0.00001`, `0.333333333`). Their descriptions no longer say "Excel to be asked". Every
case of `text.json`, `year.json`, `month.json`, `day.json`, `weekday.json`, `days.json` and `eomonth.json` is
`observed`.

**The rule, checked against every observed case.** `spikes/exsheet-date-readings/readings.py` writes out 112 whole
readings of `EDATE` and `EOMONTH` (how the start is made whole, what serial 0 is, what `EDATE`'s day 0 becomes, what 31
December 1899 is as a result, and the months' lengths) and counts the 32 observed cases of the two functions each
reproduces (EDATE-009 and EOMONTH-009, a boolean typed, are no question of reading):

- **6 reproduce all 32.** The best of the rest reproduce 31: the engine before this round (a start between -1 and 0
  refused, `EDATE` from day 0 refused) reproduces 28; a start truncated rather than cut down misses EOMONTH-016, 017
  and EDATE-017; `EDATE`'s day 0 read as day 1 (32) or as `#NUM!` misses EDATE-016; serial 0 read as 31 December 1899
  under `EOMONTH` misses EOMONTH-010 and 014; a result of 31 December 1899 read as serial 0 misses EOMONTH-015; months
  on Excel's calendar (February 1900 of 29 days) miss EDATE-011 and 013 and EOMONTH-008 and 010 to 012.
- **The six are one start reading** (cut down to its day; "below 0 `#NUM!`, then truncated" gives the same answer for
  every start) **times three readings of `EDATE` from serial 0**: the day before the 1st of the month reached by
  `DATE`'s arithmetic, which counts 29 February 1900; the month before's last day on the real calendar; and serial 0
  read as 31 December 1899 under `EDATE` alone, which answers as the second.
- **They part at three months**, each where day 0 falls on an edge: `EDATE(0,0)` is 0 or `#NUM!` (the day before 1
  January 1900 is serial 0 by `DATE`'s arithmetic, 31 December 1899 on the real calendar); `EDATE(0,2)` is 60 or 59
  (the day that never existed, or 28 February); `EDATE(0,97200)` is `#NUM!` or 2958465 (January 10000 is past the last
  year; the month before ends on 31 December 9999). The same holds from a start between 0 and 1.
- 16 observed cases of a serial of -1, -0.5 or 0 under `YEAR`, `MONTH`, `DAY`, `WEEKDAY`, `DAYS`, `NETWORKDAYS`,
  `WORKDAY`, `EOMONTH` and `EDATE`: truncation reproduces 9, refusal 9, cutting down to the day 16.

No digits decide in this area: every answer is a whole serial, an Error Value or a text.

**In the engine.** `Functions.Dates.cs`, `MonthsAway`: the start is `Math.Floor`ed, as `DatePart` does, and below 0 is
`#NUM!` (the refusal of a start between -1 and 0 goes). `EDATE` from a start whose day is 0, serial 0 alone, answers the
day before the 1st of the month reached, `DateSerial.FromDate(year, month, 1) - 1`, and is `#VALUE!` where the six
part: the month reached is January 1900, March 1900 or January 10000. Its comments, and `DatePart`'s, `Weekday`'s,
`Days`'s and `TryWorkdayDate`'s, say "cut down to its day", which is what they do, and cite the observed -0.5 cases.
`Functions.Text.cs` and `NumberText.cs`: comments only, citing TEXTFN-029 and 030 (`NumberText.GeneralLimit`'s summary
no longer calls the 11 characters uncertain; this round writes the edit the first round proposed).

**Layer 1.** `tests/ExSheet.Engine.Tests/ObservedDatesAndTextTests.cs`: `Adr0047_EDate_refuses_serial_0_whose_day_is_0`
became `Adr0047_EDate_from_serial_0_is_the_day_before_the_first_of_the_month_it_reaches` (7 answers),
`Adr0047_EDate_from_serial_0_before_1900_or_past_9999_is_NUM` (4) and
`Adr0047_EDate_from_serial_0_is_refused_where_the_readings_that_fit_part` (6), with
`Adr0047_EOMonth_from_serial_0_answers_where_EDates_readings_part`. `Adr0047_EDate_and_EOMonth_refuse_a_start_between_minus_1_and_0`
became `Adr0047_EDate_and_EOMonth_cut_a_start_between_minus_1_and_0_down_to_minus_1_which_is_NUM` (the same 7
formulas), with `Adr0047_A_serial_between_minus_1_and_0_is_NUM_under_every_function_that_reads_a_date` (12).
`Adr0047_EDate_and_EOMonth_before_1900_are_NUM` gains `EDATE(31,-1)`, 31 December 1899. The comments that said "to be
asked" of observed cases say "observed".

## For the next pass: the `uncertain` cases this area holds

`oracle.ps1 -Update -Area edate,networkdays,workday`:

| Case | Formula | Engine | What it tells apart |
|---|---|---|---|
| EDATE-018 | `=EDATE(0,2)` | `#VALUE!` (refused) | day 0 of March 1900: 60 by `DATE`'s arithmetic, 59 on the real calendar |
| EDATE-019 | `=EDATE(0,0)` | `#VALUE!` (refused) | day 0 of January 1900: serial 0 by `DATE`'s arithmetic, 31 December 1899 (`#NUM!`) on the real calendar |
| EDATE-020 | `=EDATE(0,97200)` | `#VALUE!` (refused) | day 0 of January 10000: `#NUM!` by `DATE`'s arithmetic, 31 December 9999 (2958465) as the month before's last day |
| EDATE-021 | `=EDATE(0,3)` | 91 | the six agree: day 0 of April 1900 is 31 March |
| EDATE-022 | `=EDATE(0.5,12)` | 366 | the six agree: a start between 0 and 1 is serial 0, and a year on is 31 December 1900 |
| NETWORKDAYS-010 | `=NETWORKDAYS(TRUE,6)` | `#VALUE!` (refused) | a boolean typed as a date: `#VALUE!` as under `EDATE` (EDATE-009), or read as 1 (5) |
| WORKDAY-011 | `=WORKDAY(TRUE,1)` | `#VALUE!` (refused) | the same under `WORKDAY` (`#VALUE!`, or 2) |

NETWORKDAYS-010 and WORKDAY-011 are new to the corpus but not to the engine: `NETWORKDAYS` and `WORKDAY` have refused a
boolean typed as a date "until Excel is asked" since ticket 04, and no case asked it, so no run could.

`DATEDIF` and `YEARFRAC` answer `#NUM!` for a serial between -1 and 0 (`TryDifferenceDate` refuses a negative serial
before truncating it), which is the answer the second pass read under seven other functions. Their corpus files are
ticket 11's, written from the run of 2026-10-03 in a form of their own (a culture, an observation, the bits), so no case
is added there; two for the orchestrator to place, each the engine's `#NUM!`: `=DATEDIF(-0.5,1,"D")` and
`=YEARFRAC(-0.5,1)`.

A sibling the rule reaches and this area may not write: a cell's General in a wide column with a negative number.
`general-width.json` has none (GW-003 and GW-024 are negative in narrow columns). A case for the next pass, if the
orchestrator wants it there: `A1` `=-1/3` in a column 13 characters wide, the engine's `-0.333333333`.

## ADR-0047

**1. "The list grows: the first additions", the parenthesis** (lines 416 to 418). Old:

```markdown
existed lies *(answered by the seventeenth Windows run, but for a start of serial 0 or 60: see the
note of 2026-10-12 at the end)*; and a boolean typed into `EOMONTH` or `EDATE` *(Excel's answer too, the same
note)*. The functions that answer about any Value —
```

New:

```markdown
existed lies *(answered by the seventeenth Windows run and the area passes, but for `EDATE` from serial 0 to
January 1900, March 1900 or January 10000: see the notes of 2026-10-12 at the end)*; and a boolean typed into
`EOMONTH` or `EDATE` *(Excel's answer too, the seventeenth run's note)*. The functions that answer about any Value —
```

**2. The note of 2026-10-12 (the seventeenth Windows run), its first bullet** (line 642). Old:

```markdown
  calendar, and stays `#VALUE!` until Excel is asked. A boolean typed into either is `#VALUE!`, and that is
```

New:

```markdown
  calendar, and stays `#VALUE!` until Excel is asked *(answered by the area passes, but for `EDATE` from serial
  0 to January 1900, March 1900 or January 10000: see the note of 2026-10-12, the area passes)*. A boolean typed
  into either is `#VALUE!`, and that is
```

**3. The same note, its second bullet** (line 647; the first area pass observed the cases it names). Old:

```markdown
  `WEEKDAY`'s day from the real calendar's are `uncertain` until a run asks them.
```

New:

```markdown
  `WEEKDAY`'s day from the real calendar's are `uncertain` until a run asks them *(the area pass asked them,
  and Excel's day is `WEEKDAY`'s: NETWORKDAYS-006 to 008, WORKDAY-006 to 008)*.
```

**4. A note to add at the end**, after the file's last line (or after another area's note, if one is added there
first). Old:

```markdown
OR-010 ask it.
```

New:

```markdown
OR-010 ask it.

*(2026-10-12, the area passes)* **`EOMONTH` and `EDATE` from serial 0 or 60, and from a start between -1 and 0,
follow Excel's answers** (`verification/2026-10-12-windows-area/` and `-area-2/`, ADR-0202). `EOMONTH(0,1)` is 59,
`EDATE(60,1)` is 89 and `EDATE(0,1)` is 31 (EOMONTH-010, EDATE-010, EDATE-016), and `EOMONTH(-0.5,0)` is `#NUM!`
(EOMONTH-016).

- **The start is read as `YEAR`, `MONTH` and `DAY` read it**: cut down to its day, with serial 0 0 January 1900 and
  serial 60 29 February 1900 (YEAR-003, MONTH-002 and 004, DAY-002 and 004). The months are counted from its year
  and month on the real calendar, as the seventeenth run's note says, and `EDATE` keeps its day, or the month's last
  day where the month is shorter (EOMONTH-012 to 015, EDATE-013 to 015).
- **A date between -1 and 0 is cut down to -1, and is `#NUM!`**, under `YEAR`, `MONTH`, `DAY`, `WEEKDAY`, `DAYS`,
  `NETWORKDAYS`, `WORKDAY`, `EOMONTH` and `EDATE` alike (YEAR-008, MONTH-005, DAY-005, WEEKDAY-008, DAYS-005,
  NETWORKDAYS-009, WORKDAY-010, EOMONTH-016 and 017, EDATE-017). Excel does not truncate it to serial 0.
- **`EDATE` from serial 0 keeps its day 0, which is the day before the 1st of the month reached** (EDATE-016). Six
  of the 112 readings `spikes/exsheet-date-readings/` writes out give every observed answer of the two functions, and
  they part at three months: the day before 1 January 1900 is serial 0 by `DATE`'s arithmetic and 31 December 1899,
  `#NUM!`, on the real calendar; the day before 1 March 1900 is 60, the day that never existed, or 28 February, 59;
  and January 10000 is past the last year, while the month before ends on 31 December 9999. **`EDATE` from serial 0
  to January 1900, March 1900 or January 10000 stays `#VALUE!`** until Excel is asked (EDATE-018 to 020); EDATE-021
  and 022 ask two answers on which the six agree.
- `NETWORKDAYS` and `WORKDAY` still refuse a boolean typed as a date with `#VALUE!`; NETWORKDAYS-010 and WORKDAY-011
  ask it.
```

## ADR-0120

**1. The note of 2026-10-12, under "Refused until a Windows run answers"** (line 53, a sub-bullet). Old:

```markdown
  - The run asked a positive number only. Two readings it did not settle stay `#VALUE!` until Excel is asked.
```

New:

```markdown
  - The run asked a positive number only. Two readings it did not settle stay `#VALUE!` until Excel is asked
    *(both answered by the area pass: see the note of 2026-10-12, the area passes, below)*.
```

**2. The same note, the empty code's bullet** (lines 59 and 60; the first area pass observed both cases). Old:

```markdown
  code without a text section: text stays as it is, and a boolean stays `TRUE` or `FALSE` (TEXTFN-023 and 024,
  `uncertain`).
```

New:

```markdown
  code without a text section: text stays as it is, and a boolean stays `TRUE` or `FALSE` (TEXTFN-023 and 024,
  observed by the area pass).
```

**3. A note to add after that note's last bullet** (line 67), before "## What differs from Excel, by decision". Old:

```markdown
  - Both cases record Excel's answer beside the refusal.
```

New:

```markdown
  - Both cases record Excel's answer beside the refusal.

*(2026-10-12, the area passes)* **Both readings the seventeenth run left open are answered, and follow Excel's
answer** (`verification/2026-10-12-windows-area/` and `-area-2/`, ADR-0202). `TEXT(-1/3,"General")` is
`-0.333333333` (TEXTFN-025) and `TEXT(1/3,"@")` is `0.333333333` (TEXTFN-028).

- **`General` is General at its own limit, 11 characters, as a wide column shows a positive number (GW-004), and
  the minus sign is not one of them**: a negative number takes one character more than its magnitude, of eleven
  digits and of twelve alike (TEXTFN-029, 030).
- **A code of a text section alone, such as `@`, shows a number as `TEXT`'s General does**, fitted to those 11
  characters, and not as General in full: a negative number, twelve digits, a small number and a section with a
  literal, which is left out (TEXTFN-031 to 034).
- Every case of `text.json` is `observed`.
```

## `src/ExSheet.Engine/README.md`

**1. The bullet "`TEXT` reads its code in the invariant spelling"** (lines 231 to 236). Old:

```markdown
  format would under the Sheet's culture (ADR-0120). `General` is fitted to 11 characters, as a
  cell's General is fitted to its column, and an empty code shows a number as empty text, as Excel's
  do. A code outside the cell formats' subset is `#VALUE!`, a fraction or a condition among them,
  though Excel answers both. Until Excel is asked, so are a negative number whose minus sign would
  cost its General a digit, and a number under a lone `@` whose fitted General is not its General in
  full.
```

New:

```markdown
  format would under the Sheet's culture (ADR-0120). `General` is fitted to 11 characters, as a
  cell's General is fitted to a wide column, and a minus sign is not one of them
  (`TEXT(-1/3,"General")` is `-0.333333333`); a lone `@` shows a number as that General does; and an
  empty code shows a number as empty text, as Excel's do. A code outside the cell formats' subset is
  `#VALUE!`, a fraction or a condition among them, though Excel answers both.
```

**2. The bullet "Asked of Excel before they are answered"**, its first clause (line 238 alone; other areas edit the
rest of the bullet, and the next line, which begins `(`#VALUE!`); `NETWORKDAYS``, is kept). Old:

```markdown
  Excel gives: `EOMONTH` and `EDATE` from serial 0 or from serial 60, the day that never existed
```

New:

```markdown
  Excel gives: `EDATE` from serial 0 to January 1900, March 1900 or January 10000, where the readings
  that give Excel's other answers part
```

**3. The bullet "Before 1 March 1900"** (lines 363 and 364). Old:

```markdown
  (`NETWORKDAYS(30,100)` is 51), as Excel was observed to. A `WORKDAY` that falls before serial 0
  is `#NUM!`.
```

New:

```markdown
  (`NETWORKDAYS(30,100)` is 51), as Excel was observed to. A `WORKDAY` that falls before serial 0
  is `#NUM!`. `EDATE` and `EOMONTH` read a start of serial 0 or 60 as `MONTH` and `DAY` read it, in
  January 1900 and on the 29th of February: `EOMONTH(0,1)` is 59 and `EDATE(60,1)` is 89, and from
  serial 0 `EDATE` keeps day 0, the day before the 1st, so `EDATE(0,1)` is 31. A date between -1 and
  0 is cut down to -1, and is `#NUM!` under every function that reads a date, as Excel's is.
```

## The catalogue, `docs/specs/exsheet-functions/spec.md` (shared with the financial area)

No row is made false. Four notes, as other rows name the arguments they refuse (lines 203 to 207). Old:

```markdown
| `EOMONTH` | Supported | — | |
| `EDATE` | Supported | — | |
| `WEEKDAY` | Supported | — | |
| `NETWORKDAYS` | Supported | — | |
| `WORKDAY` | Supported | — | |
```

New:

```markdown
| `EOMONTH` | Supported | — | A start of serial 0 is in January 1900, as `MONTH` reads it |
| `EDATE` | Supported | — | From serial 0 to January 1900, March 1900 or January 10000, `#VALUE!` until Excel is asked (ADR-0047) |
| `WEEKDAY` | Supported | — | |
| `NETWORKDAYS` | Supported | — | A boolean typed as a date is `#VALUE!` until Excel is asked (ADR-0047) |
| `WORKDAY` | Supported | — | A boolean typed as a date is `#VALUE!` until Excel is asked (ADR-0047) |
```

## The Docs Site

`/exsheet/functions` lists each function's name, arguments and description from the engine's declarations; none of
those changes, and no page states `EDATE` or `EOMONTH` from serial 0 or 60, or a date between -1 and 0. **Nothing is
proposed for them.**

The Formulas page's `TEXT` bullet (`samples/ExGrid.Docs/Pages/Sheet/SheetFormulas.razor`, "Supported functions", lines
177 and 178) is not made false, but it is where a reader meets `TEXT`, and it says nothing of what `General` shows,
which differs from General in full for every number that reaches 11 characters. Old:

```razor
                <b><code>TEXT</code></b> reads its code in the invariant spelling and shows what a cell in that format
                would show, under the Sheet's culture. A code a cell format does not have is <code>#VALUE!</code>,
```

New (Razor: `@` is written `@@`):

```razor
                <b><code>TEXT</code></b> reads its code in the invariant spelling and shows what a cell in that format
                would show, under the Sheet's culture. <code>General</code>, and <code>@@</code> alone, fit a number to 11
                characters, as a wide column's General does, and a minus sign is not one of them:
                <code>TEXT(-1/3,"General")</code> is <code>-0.333333333</code>. A code a cell format does not have is
                <code>#VALUE!</code>,
```

## Tickets

Written on this branch (this area's files): a dated comment on `docs/specs/exsheet-functions/issues/01-p1-ready-functions.md`
(the second pass: EOMONTH-012 to 017, EDATE-013 to 017, YEAR-008, MONTH-005, DAY-005; the six readings and where they
part; EDATE-018 to 022) and on `docs/specs/exsheet-functions/issues/02-text.md` (TEXTFN-029 to 034), whose last box,
"The next Windows run asks the `uncertain` cases", is **ticked**: every case of `text.json` is `observed`. Ticket 01's
last box stays open: EDATE-018 to 022 are `uncertain`, and so are the kth area's `MOD` cases. Ticket 04 (`WEEKDAY`,
`DAYS`, `NETWORKDAYS`, `WORKDAY`) is shared and not written here; a line for it, if the orchestrator wants one:
"2026-10-12, the second area pass: WEEKDAY-008, DAYS-005, NETWORKDAYS-009 and WORKDAY-010 are `#NUM!` in Excel, as
the engine answers: a serial between -1 and 0 is cut down to -1. NETWORKDAYS-010 and WORKDAY-011 ask the refusal of a
boolean typed as a date, which no case asked before."

## Change Notes

- `docs/releases/unreleased/area-dates-and-text-serial-0-and-60.md` (ExSheet, Behaviour, `exsheet/functions`,
  ADR-0047), rewritten to what is true now: `EOMONTH` and `EDATE` from serial 0 or 60 answer, `EDATE(0,1)` is 31, `EDATE`
  from serial 0 to January 1900, March 1900 or January 10000 stays `#VALUE!`, and a start between -1 and 0 is `#NUM!`
  where it gave `#VALUE!`.
- `docs/releases/unreleased/area-dates-and-text-text-general.md` (ExSheet, Behaviour,
  `exsheet/formulas#supported-functions`, ADR-0120): unchanged, and still true; the second pass agreed with every
  `TEXT` case it asked.
- Corrected by the first round, outside this area's list, because each said what is no longer so: the last sentence
  of `run17-dates-text-dates-before-march-1900.md` and of `run17-dates-text-text-general.md`.
