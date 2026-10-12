# Run 17 corrections, dates and text: what the orchestrator writes

Branch `claude/run17-dates-text`, from `claude/run17-corrections`. The seventeenth Windows run's Part A
(`verification/2026-10-11-windows-17/report.md`, "Every disagreement") disagreed with the corpus on nine
cases of this area. Each is settled under ADR-0202's rule: corrected to Excel's answer, or refused with an
Error Value and Excel's answer recorded beside the refusal. Nothing here changes an ADR, `CONTEXT.md`, the
Definition of Done, the README or the Docs Site: the text for each is below, in the repository's voice.

## What was done, case by case

| Case | Excel (run 17) | Was | Now | Why |
|---|---|---|---|---|
| EDATE-008 `=EDATE(DATE(1900,3,15),-1)` | 46 | `#VALUE!` | **46**, observed | ADR-0047: refused until Excel answers; it answered |
| EOMONTH-008 `=EOMONTH(31,1)` | 59 | `#VALUE!` | **59**, observed | as EDATE-008 |
| NETWORKDAYS-005 `=NETWORKDAYS(30,100)` | 51 | `#VALUE!` | **51**, observed | as EDATE-008 |
| SUBSTITUTE-005 `=SUBSTITUTE("aaaa","aa","b",2)` | `aba` | `aab` | **`aba`**, observed | the engine's guess was wrong; ADR-0202 corrects it |
| TEXTFN-009, de-DE, `=TEXT(1234.5,"#,##0.00")` | `1234,5000` | `1.234,50` | `1.234,50`, observed, `engineDiffersByDecision` ADR-0120 + `excelExpect` | ADR-0120, "What differs from Excel, by decision" |
| TEXTFN-017 `=TEXT(1/3,"General")` | `0.333333333` | `#VALUE!` | **`0.333333333`**, observed | ADR-0120: "Which width it uses is to be observed" |
| TEXTFN-018 `=TEXT(5,"")` | empty text | `#VALUE!` | **empty text**, observed | ADR-0120: refused until a run answers; it answered |
| TEXTFN-019 `=TEXT(4.34,"# ?/?")` | `4 1/3` | `#VALUE!` | `#VALUE!`, observed, `engineDiffersByDecision` ADR-0202 + ADR-0120 + `excelExpect` | kept refused (below) |
| TEXTFN-020 `=TEXT(5,"[<=9]0;0.0")` | `5` | `#VALUE!` | `#VALUE!`, observed, as TEXTFN-019 | kept refused (below) |

The rule each correction takes, not only the case:

- **`EDATE` and `EOMONTH`** count months on the real calendar, where February 1900 has 28 days. A start from
  serial 1 to 59 is 1 January to 28 February 1900, and a result before 1 March is its own serial 1 to 59.
  `EOMONTH(31,1)` is 28 February, 59, not 60. A start of serial 0 or 60 (neither a day of that calendar) was not
  asked and stays `#VALUE!`. Booleans stay `#VALUE!` (EDATE-009 and EOMONTH-009 were observed so).
- **`NETWORKDAYS` and `WORKDAY`** count every serial from 0, each on the day of the week `WEEKDAY` gives it
  (serial 0 a Saturday and 1 a Sunday, WEEKDAY-005 and 006, observed; serial 60 a Wednesday). `WORKDAY` shares
  the date reading (`TryWorkdayDate`), so it takes the same rule rather than a split helper, and a result before
  serial 0 is `#NUM!` (it was `#VALUE!` before 1 March 1900). `NETWORKDAYS(30,100)` does not tell `WEEKDAY`'s day
  from the real calendar's with serial 60 counted as a weekday (both give 51), so cases that do tell them apart
  are added for a run (below).
- **`SUBSTITUTE`** with `instance_num` counts the occurrences at every character, overlapping ones included.
  Without it, every occurrence is replaced left to right and none overlaps, as before (SUBSTITUTE-001).
- **`TEXT`'s General** is General fitted to 11 characters, `NumberText.General(number, 11, culture)`. This also
  changes an answer the engine gave before: `TEXT(0.00001,"General")` is `0.00001`, as a cell's General shows it
  (GW-012), where the engine wrote General in full, `1E-05`.
  - **Two readings the run did not settle stay refused**, as ADR-0120's refusal "past 11 characters" did. The run
    asked a positive number only. A negative number whose General differs as its minus sign takes one of the 11
    characters (`-0.33333333`) or not (`-0.333333333`) is `#VALUE!` (TEXTFN-025). And a lone `@`, which shows a
    number in General, gives a number whose fitted General is not its General in full (`TEXT(1/3,"@")`) `#VALUE!`
    (TEXTFN-028): whether `@` takes TEXT's 11 characters was not asked. This is narrower than the triage's
    `NumberText.General(number, 11, culture)` for every number. It answers only where every reading agrees.
- **An empty code** shows a number as empty text. Text and booleans take ADR-0120's coercion for a code without a
  text section: text that reads as a number is that number and so is empty, other text stays as it is, a boolean
  stays `TRUE`. Those two are `uncertain` cases (TEXTFN-023, 024).
- **Fractions and conditions stay refused** (TEXTFN-019, 020). ADR-0202 lets a function refuse the arguments
  where it disagrees. `TEXT` shares `NumberFormat`'s subset with the cell formats (ADR-0120, "One renderer
  serves both"), so admitting either is a cell-format feature (Format Cells, a document's codes, painting), not a
  correction to `TEXT`. And one fraction does not pin Excel's approximation: which denominator it searches, and
  how it breaks a tie.

Layer-1 tests: `tests/ExSheet.Engine.Tests/ObservedDatesAndTextTests.cs` (59 cases, each named with its ADR), and
the corpus theories over the changed files.

## For a run: the `uncertain` cases this area adds

Each holds the engine's answer, or its refusal, for the next Windows run to ask
(`oracle.ps1 -Update -Area edate,eomonth,networkdays,workday,text`):

| Case | Formula | Engine | What it tells apart |
|---|---|---|---|
| EDATE-010 | `=EDATE(60,1)` | `#VALUE!` (refused) | what a start on the day that never existed gives |
| EDATE-011 | `=EDATE(31,1)` | 59 | February 1900's 28 days under `EDATE`, as EOMONTH-008 showed for `EOMONTH` |
| EOMONTH-010 | `=EOMONTH(0,1)` | `#VALUE!` (refused) | what a start on serial 0 gives |
| EOMONTH-011 | `=EOMONTH(DATE(1900,3,1),-1)` | 59 | the end of February counted back from March: 59, not 60 |
| NETWORKDAYS-006 | `=NETWORKDAYS(1,1)` | 0 | serial 1 a Sunday (`WEEKDAY`) against a Monday (1 January 1900 on the real calendar) |
| NETWORKDAYS-007 | `=NETWORKDAYS(6,6)` | 1 | serial 6 a Friday against a Saturday |
| NETWORKDAYS-008 | `=NETWORKDAYS(60,60)` | 1 | serial 60 a workday |
| WORKDAY-006 | `=WORKDAY(30,10)` | 44 | `WORKDAY` before March 1900 at all (the triage's case) |
| WORKDAY-007 | `=WORKDAY(30,4)` | 34 | `WEEKDAY`'s day (34) against the real calendar's (36) |
| WORKDAY-008 | `=WORKDAY(55,5)` | 62 | serial 60 counted as a workday |
| WORKDAY-009 | `=WORKDAY(2,-1)` | `#NUM!` | a result before serial 0 |
| TEXTFN-023 | `=TEXT("abc","")` | `abc` | text under an empty code |
| TEXTFN-024 | `=TEXT(TRUE,"")` | `TRUE` | a boolean under an empty code |
| TEXTFN-025 | `=TEXT(-1/3,"General")` | `#VALUE!` (refused) | whether the minus sign takes one of the 11 characters |
| TEXTFN-026 | `=TEXT(123456789012,"General")` | `1.23457E+11` | twelve digits go scientific, as in a wide column |
| TEXTFN-027 | `=TEXT(0.00001,"General")` | `0.00001` | fitted General, not General in full (`1E-05`) |
| TEXTFN-028 | `=TEXT(1/3,"@")` | `#VALUE!` (refused) | whether a lone `@` fits General to TEXT's 11 characters |

## ADR-0047: a note to add after the note of 2026-10-11

> *(2026-10-12)* **The seventeenth Windows run answered three of the refusals above, and they follow Excel's
> answer** (`verification/2026-10-11-windows-17/`, ADR-0202).
>
> - **`EOMONTH` and `EDATE` before 1 March 1900** count months on the real calendar, where February 1900 has 28
>   days, and a day before 1 March is its own serial, 1 to 59. `EOMONTH(31,1)` is 59, 28 February, and not 60;
>   `EDATE(DATE(1900,3,15),-1)` is 46 (EOMONTH-008, EDATE-008). A start of serial 0 or 60 is no day of that
>   calendar, and stays `#VALUE!` until Excel is asked. A boolean typed into either is `#VALUE!`, and that is
>   Excel's answer too (EOMONTH-009, EDATE-009).
> - **`NETWORKDAYS` and `WORKDAY`** count every serial from 0. Each takes the day of the week `WEEKDAY` gives it:
>   serial 0 a Saturday, 1 a Sunday, 60 a Wednesday. `NETWORKDAYS(30,100)` is 51 (NETWORKDAYS-005). `WORKDAY`
>   reads its dates as `NETWORKDAYS` does, and a result before serial 0 is `#NUM!`. The cases that tell
>   `WEEKDAY`'s day from the real calendar's are `uncertain` until a run asks them.
> - **`SUBSTITUTE` with `instance_num`** counts the occurrences at every character, overlapping ones included:
>   `SUBSTITUTE("aaaa","aa","b",2)` is `aba` (SUBSTITUTE-005). The engine counted them without overlapping, which
>   was a guess, and wrong. Without `instance_num`, every occurrence is replaced left to right, none overlapping.

## ADR-0120: notes

Under **Refused until a Windows run answers**, after its list:

> *(2026-10-12)* **The seventeenth Windows run answered two of these, and they follow Excel's answer.**
>
> - **`General` is General fitted to 11 characters**, as a cell's General is fitted to its column:
>   `TEXT(1/3,"General")` is `0.333333333` (TEXTFN-017). So `TEXT(0.00001,"General")` is `0.00001`, as a cell
>   shows it, and not General in full, `1E-05`.
>   - The run asked a positive number only. Two readings it did not settle stay `#VALUE!` until Excel is asked.
>   - A negative number whose General differs as its minus sign takes one of the 11 characters or not
>     (`-1/3`: `-0.33333333` or `-0.333333333`; TEXTFN-025).
>   - A number under a lone `@`, which shows numbers in General, whose fitted General is not its General in full
>     (`1/3`; TEXTFN-028).
> - **An empty `format_text` shows a number as empty text** (TEXTFN-018). Text and booleans are coerced as for any
>   code without a text section: text stays as it is, and a boolean stays `TRUE` or `FALSE` (TEXTFN-023 and 024,
>   `uncertain`).
> - **Fractions and conditions stay refused.** Excel answers `TEXT(4.34,"# ?/?")` with `4 1/3` and
>   `TEXT(5,"[<=9]0;0.0")` with `5` (TEXTFN-019, 020). ADR-0202 lets a function refuse the arguments where it
>   disagrees, and the reason is this ADR's "One renderer serves both".
>   - `TEXT` shows what a cell format shows. Admitting a fraction or a condition is a cell-format feature (Format
>     Cells, a document's codes, painting), not a correction to `TEXT`.
>   - One fraction does not pin Excel's approximation: which denominators it searches, and how it breaks a tie.
>   - Both cases record Excel's answer beside the refusal.

Under **What differs from Excel, by decision**, after its paragraph:

> *(2026-10-12)* The seventeenth run read it under de-DE. German Excel answers `TEXT(1234.5,"#,##0.00")` with
> `1234,5000`, because it reads `,` as its decimal separator and `.` as its grouping, so the invariant code is
> another code to it. ExSheet shows `1.234,50`. The case records Excel's answer beside ExSheet's (TEXTFN-009).

## `src/ExSheet.Engine/README.md`

In the bullet **`TEXT` reads its code in the invariant spelling**, replace its last sentence ("A code outside the
cell formats' subset, an empty code, and `General` past 11 characters are `#VALUE!`.") with:

> `General` is fitted to 11 characters, as a cell's General is fitted to its column, and an empty code shows a
> number as empty text, as Excel's do. A code outside the cell formats' subset is `#VALUE!`, a fraction or a
> condition among them, though Excel answers both. Until Excel is asked, so are a negative number whose minus
> sign would cost its General a digit, and a number under a lone `@` whose fitted General is not its General in
> full.

In the bullet **Asked of Excel before they are answered**, replace "`EOMONTH` and `EDATE` from or to a day before
1 March 1900, where Excel's calendar holds the day that never existed, and with a boolean typed as an argument
(`#VALUE!`); `NETWORKDAYS` and `WORKDAY` the same way, and with text among the holidays;" with:

> `EOMONTH` and `EDATE` from serial 0 or from serial 60, the day that never existed (`#VALUE!`); `NETWORKDAYS`
> and `WORKDAY` with a boolean typed as a date (`#VALUE!`);

(a boolean typed into `EOMONTH` or `EDATE`, and text among the holidays, are `#VALUE!` in Excel too, EDATE-009,
EOMONTH-009, WORKDAY-005), and add a bullet of its own beside the `YEARFRAC` and `DATEDIF` one:

> - **Before 1 March 1900**, `EDATE` and `EOMONTH` count months on the real calendar, where February 1900 has 28
>   days (`EOMONTH(31,1)` is 59), and `NETWORKDAYS` and `WORKDAY` give each serial the day of the week `WEEKDAY`
>   gives it, serial 1 a Sunday and serial 60 a Wednesday (`NETWORKDAYS(30,100)` is 51), as Excel was observed
>   to. A `WORKDAY` that falls before serial 0 is `#NUM!`.

## The Docs Site

**No sentence on the site is made false.** `/exsheet/functions` lists each function's name, arguments and
description, and none of those changes. `/exsheet/formulas#supported-functions` lists refusals, and names none
of the ones this changes. One addition is proposed, to that list on `samples/ExGrid.Docs/Pages/Sheet/SheetFormulas.razor`,
because `TEXT`'s refusal of fractions and conditions is now a known difference from Excel:

> **`TEXT`** reads its code in the invariant spelling and shows what a cell in that format would show, under the
> Sheet's culture. A code a cell format does not have is `#VALUE!`, though Excel answers it: a fraction such as
> `# ?/?`, or a condition such as `[<=9]`.

The Change Notes link `exsheet/functions` without an anchor. The site checker (EXDOCS009) knows that page's
route but none of its sections, because its category sections are drawn at run time from the catalogue.
`#date-and-time` and `#text` exist on the page, and the checker refused both.

## Tickets

The done tickets that record these refusals could each gain a dated comment. These are proposals, not edits:
`docs/specs/` is not this area's.

- `docs/specs/exsheet-functions/issues/01-p1-ready-functions.md`:
  > 2026-10-12: the seventeenth Windows run answered EOMONTH-008 and EDATE-008 (59 and 46). Before 1 March 1900
  > the months are counted on the real calendar; a start of serial 0 or 60 stays refused (ADR-0047's note).
- `docs/specs/exsheet-functions/issues/02-text.md`:
  > 2026-10-12: the seventeenth Windows run answered TEXTFN-017 (`0.333333333`, General fitted to 11 characters)
  > and TEXTFN-018 (empty text). Fractions and conditions stay refused with Excel's answers recorded, and
  > TEXTFN-009 records German Excel's (ADR-0120's notes).
- `docs/specs/exsheet-functions/issues/04-p2-ready-functions.md`:
  > 2026-10-12: the seventeenth Windows run answered NETWORKDAYS-005 (51) and SUBSTITUTE-005 (`aba`). Dates
  > before 1 March 1900 count on `WEEKDAY`'s days, for `WORKDAY` too, and `SUBSTITUTE` counts overlapping
  > instances (ADR-0047's note).

## Change Notes written

- `docs/releases/unreleased/run17-dates-text-dates-before-march-1900.md` (ExSheet, Behaviour, ADR-0047)
- `docs/releases/unreleased/run17-dates-text-substitute-instance.md` (ExSheet, Fixed, ADR-0047)
- `docs/releases/unreleased/run17-dates-text-text-general.md` (ExSheet, Behaviour, ADR-0120)

ADR-0190's kinds are Added, Fixed, Behaviour and Breaking, so the brief's "Changed" is written as Behaviour.
`SUBSTITUTE`'s note is Fixed, because its earlier answer was a wrong answer and not a refusal.
