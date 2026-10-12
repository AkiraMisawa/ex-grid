# The area pass, dates and text: what the orchestrator writes

Branch `claude/area-dates-and-text`, from `claude/exsheet-windows-area-2026-10-12`. The area pass of 2026-10-12
(`verification/2026-10-12-windows-area/report.md`, "Every disagreement (29)";
`tests/ExSheet.Engine.Tests/ExcelOracle/results-2026-10-12-area-com-en-US.json`) disagreed with the corpus on four
cases of this area. Each was refused "until Excel is asked"; Excel answered, and each now follows the answer under
ADR-0202's rule and ADR-0047's admission rule. Nothing here changes an ADR, `CONTEXT.md`, the Definition of Done,
the engine's README, `NumberText.cs` or the Docs Site: the text for each is below, in the repository's voice, as
exact old -> new edits. A quoted line drops the file's leading indentation (a bullet's two spaces, a doc comment's
four); each old text is otherwise exact and occurs once in its file, and the edit keeps the indentation.

## What was done, case by case

| Case | Excel (area pass) | Was | Now | The rule it takes |
|---|---|---|---|---|
| EOMONTH-010 `=EOMONTH(0,1)` | 59 | `#VALUE!` (refused) | **59**, observed | the start read as `MONTH` reads it (MONTH-004: serial 0 is in January 1900); months on the real calendar |
| EDATE-010 `=EDATE(60,1)` | 89 | `#VALUE!` (refused) | **89**, observed | the start read as `DAY` reads it (DAY-002: serial 60 is the 29th of February 1900); the day kept, or the month's last |
| TEXTFN-025 `=TEXT(-1/3,"General")` | `-0.333333333` | `#VALUE!` (refused) | **`-0.333333333`**, observed | General at its own limit, 11 characters, the minus sign beside them |
| TEXTFN-028 `=TEXT(1/3,"@")` | `0.333333333` | `#VALUE!` (refused) | **`0.333333333`**, observed | a code of a text section alone shows a number as `TEXT`'s General does |

**Settled, or one point of it.** Each answer settles the rule its refusal waited for, but one:

- `EOMONTH` reads only its start's year and month. With serial 0 in January 1900 and serial 60 in February 1900, as
  `YEAR` and `MONTH` were observed to read them, EOMONTH-010 settles `EOMONTH` from either: the refusal goes.
- `EDATE` reads the start's day too. EDATE-010 shows that the 29th of February 1900 is kept as a 29th where the
  month has one. Serial 0's day is 0 (DAY-004), and no observation says what day 0 of a month becomes, so **`EDATE`
  from serial 0 stays `#VALUE!`** (`DateSerial.FromDate` has no day 0 either). EDATE-016 asks it.
- TEXTFN-025 settles the minus sign: it is not one of General's 11 characters. TEXTFN-028 settles the reading of a
  code of a text section alone: fitted General, not General in full.

**In the engine.** `Functions.Dates.cs`: `MonthsAway` no longer refuses a start of 0 or 60; it refuses only
`EDATE` from a start whose day is 0, which is serial 0 alone (the `NeverExisted` constant goes, unused).
`Functions.Text.cs`: `TextGeneral` is `NumberText.General(number, NumberText.GeneralLimit + 1 for a minus sign,
culture)`, with no refusal where 11 and 12 characters differ and none where the fitted General is not General in
full; `TextGeneralWidth` goes, as `NumberText.GeneralLimit` is the same 11. `NumberText.cs` is not touched. A code
of a text section alone, as `NumberFormat.ShowsNumbersAsGeneral` reads it, takes the same rule as a lone `@`, so
`TEXT(1/3,"""x""@")` is `0.333333333` (TEXTFN-034 asks).

**Layer 1.** `tests/ExSheet.Engine.Tests/ObservedDatesAndTextTests.cs`:
`Adr0047_EDate_and_EOMonth_refuse_serial_0_and_the_day_that_never_existed` became
`Adr0047_EDate_and_EOMonth_read_serial_0_and_the_day_that_never_existed_as_Day_and_Month_do` (12 answers),
`Adr0047_EOMonth_from_serial_0_to_before_1900_is_NUM` and `Adr0047_EDate_refuses_serial_0_whose_day_is_0`.
`Adr0120_Text_General_refuses_where_the_unobserved_readings_disagree` became
`Adr0120_Text_General_leaves_the_minus_sign_out_of_its_11_characters` and
`Adr0120_Text_under_a_text_section_alone_shows_a_number_as_its_General_does`, with two facts:
`Adr0120_Text_General_and_a_lone_at_sign_agree` and
`Adr0120_Text_General_of_a_negative_number_is_its_magnitudes_with_a_minus_sign`.

## For the next pass: the `uncertain` cases this area adds

Each holds the engine's answer, or its refusal, for the next Windows run
(`oracle.ps1 -Update -Area edate,eomonth,text`):

| Case | Formula | Engine | What it tells apart |
|---|---|---|---|
| EDATE-013 | `=EDATE(60,0)` | 59 | the 29th of February 1900 cut to 28 February in its own month |
| EDATE-014 | `=EDATE(60,12)` | 425 | the same in a year with no day that never existed, 28 February 1901 |
| EDATE-015 | `=EDATE(60,-1)` | 29 | the 29th kept in a month that has one, backwards |
| EDATE-016 | `=EDATE(0,1)` | `#VALUE!` (refused) | what day 0 of a month becomes |
| EOMONTH-012 | `=EOMONTH(60,0)` | 59 | February 1900 ends on the 28th from serial 60 itself |
| EOMONTH-013 | `=EOMONTH(60,1)` | 91 | from serial 60 into March |
| EOMONTH-014 | `=EOMONTH(0,0)` | 31 | serial 0's own month |
| EOMONTH-015 | `=EOMONTH(0,-1)` | `#NUM!` | from serial 0 to before 1900 |
| TEXTFN-029 | `=TEXT(-123456789012,"General")` | `-1.23457E+11` | a negative twelve digits, scientific beside the sign |
| TEXTFN-030 | `=TEXT(-12345678901,"General")` | `-12345678901` | a negative eleven digits, whole beside the sign |
| TEXTFN-031 | `=TEXT(-1/3,"@")` | `-0.333333333` | the sign under a lone `@` |
| TEXTFN-032 | `=TEXT(123456789012,"@")` | `1.23457E+11` | twelve digits under a lone `@` |
| TEXTFN-033 | `=TEXT(0.00001,"@")` | `0.00001` | fitted General under `@` where General in full is `1E-05` |
| TEXTFN-034 | `=TEXT(1/3,"""x""@")` | `0.333333333` | a text section with a literal, as a lone `@` |

A sibling the rule reaches and this area may not write: a cell's General in a wide column with a negative number.
`general-width.json` has no such case (GW-004, 009 and 010 are positive). A case for the next pass, if the
orchestrator wants it there: `A1` `=-1/3` in a column 13 characters wide, the engine's `-0.333333333`.

## ADR-0047

**1. The note of 2026-10-12 (the seventeenth Windows run), its first bullet.** Old:

> calendar, and stays `#VALUE!` until Excel is asked. A boolean typed into either is `#VALUE!`, and that is

New:

> calendar, and stays `#VALUE!` until Excel is asked *(answered by the area pass, but for `EDATE` from serial 0:
> see the note of 2026-10-12, the area pass)*. A boolean typed into either is `#VALUE!`, and that is

**2. "The list grows: the first additions", the parenthesis.** Old:

> existed lies *(answered by the seventeenth Windows run, but for a start of serial 0 or 60: see the
> note of 2026-10-12 at the end)*; and a boolean typed into `EOMONTH` or `EDATE` *(Excel's answer too, the same
> note)*.

New:

> existed lies *(answered by the seventeenth Windows run and the area pass, but for `EDATE` from serial 0: see
> the notes of 2026-10-12 at the end)*; and a boolean typed into `EOMONTH` or `EDATE` *(Excel's answer too, the
> seventeenth run's note)*.

**3. A note to add at the end** (after the other area-pass notes, if they come first):

> *(2026-10-12, the area pass)* **`EOMONTH` from serial 0 or 60, and `EDATE` from serial 60, follow Excel's
> answer** (`verification/2026-10-12-windows-area/`, ADR-0202). `EOMONTH(0,1)` is 59 and `EDATE(60,1)` is 89, 29
> March 1900 (EOMONTH-010, EDATE-010).
>
> - **The start is read as `YEAR`, `MONTH` and `DAY` read it**: serial 0 is 0 January 1900 and serial 60 is 29
>   February 1900 (YEAR-003, MONTH-002 and 004, DAY-002 and 004, observed). The months are counted from its year
>   and month on the real calendar, as the seventeenth run's note says, and `EDATE` keeps its day, or the
>   month's last day where the month is shorter: `EDATE(60,0)` is 59, 28 February.
> - **`EDATE` from serial 0 stays `#VALUE!`.** Its day is 0, and no run has observed what day 0 of a month
>   becomes (EDATE-016 asks).
> - The rule's other cases are `uncertain` for the next pass: EOMONTH-012 to 015 and EDATE-013 to 015.

## ADR-0120

**1. The note of 2026-10-12, under "Refused until a Windows run answers".** Old:

> - The run asked a positive number only. Two readings it did not settle stay `#VALUE!` until Excel is asked.

New:

> - The run asked a positive number only. Two readings it did not settle stay `#VALUE!` until Excel is asked
>   *(both answered by the area pass: see the note of 2026-10-12, the area pass, below)*.

**2. A note to add after that note's last bullet ("Both cases record Excel's answer beside the refusal."), before
"## What differs from Excel, by decision":**

> *(2026-10-12, the area pass)* **Both readings the seventeenth run left open are answered, and follow Excel's
> answer** (`verification/2026-10-12-windows-area/`, ADR-0202). `TEXT(-1/3,"General")` is `-0.333333333`
> (TEXTFN-025) and `TEXT(1/3,"@")` is `0.333333333` (TEXTFN-028).
>
> - **`General` is General at its own limit, 11 characters, with the minus sign beside them**, as a wide column
>   shows it: a negative number takes one character more than its magnitude.
> - **A code of a text section alone, such as `@`, shows a number as `TEXT`'s General does**, fitted to those 11
>   characters, and not as General in full.
> - The rule's other cases are `uncertain` for the next pass: a negative number of twelve and of eleven digits
>   (TEXTFN-029, 030), under `@` a negative number, twelve digits and a small number (TEXTFN-031 to 033), and a
>   text section with a literal (TEXTFN-034).

## `src/ExSheet.Engine/README.md`

**1. The bullet "`TEXT` reads its code in the invariant spelling".** Old:

> format would under the Sheet's culture (ADR-0120). `General` is fitted to 11 characters, as a
> cell's General is fitted to its column, and an empty code shows a number as empty text, as Excel's
> do. A code outside the cell formats' subset is `#VALUE!`, a fraction or a condition among them,
> though Excel answers both. Until Excel is asked, so are a negative number whose minus sign would
> cost its General a digit, and a number under a lone `@` whose fitted General is not its General in
> full.

New:

> format would under the Sheet's culture (ADR-0120). `General` is fitted to 11 characters, as a
> cell's General is fitted to a wide column, with a minus sign beside them (`TEXT(-1/3,"General")` is
> `-0.333333333`); a lone `@` shows a number as that General does; and an empty code shows a number
> as empty text, as Excel's do. A code outside the cell formats' subset is `#VALUE!`, a fraction or a
> condition among them, though Excel answers both.

**2. The bullet "Asked of Excel before they are answered"** (other areas edit the rest of it). Old:

> Excel gives: `EOMONTH` and `EDATE` from serial 0 or from serial 60, the day that never existed
> (`#VALUE!`); `NETWORKDAYS` and `WORKDAY` with a boolean typed as a date (`#VALUE!`); `SMALL` with a

New:

> Excel gives: `EDATE` from serial 0, whose day is 0 (`#VALUE!`); `NETWORKDAYS` and `WORKDAY` with a
> boolean typed as a date (`#VALUE!`); `SMALL` with a

**3. The bullet "Before 1 March 1900".** Old:

> (`NETWORKDAYS(30,100)` is 51), as Excel was observed to. A `WORKDAY` that falls before serial 0
> is `#NUM!`.

New:

> (`NETWORKDAYS(30,100)` is 51), as Excel was observed to. A `WORKDAY` that falls before serial 0
> is `#NUM!`. `EDATE` and `EOMONTH` read a start of serial 0 or 60 as `MONTH` and `DAY` read it, in
> January 1900 and on the 29th of February: `EOMONTH(0,1)` is 59 and `EDATE(60,1)` is 89.

## `src/ExSheet.Engine/NumberText.cs` (optional; its comment is now behind the corpus)

`GeneralLimit`'s summary. Old:

> /// <c>1.23457E+11</c>. Microsoft documents only that twelve or more digits go scientific; the
> /// eleven, and leaving the sign out of them, are uncertain in the case corpus (ADR-0047).

New:

> /// <c>1.23457E+11</c>. Microsoft documents only that twelve or more digits go scientific; the
> /// eleven were observed in a wide column (GW-004, GW-009, GW-010), and leaving the sign out of them
> /// in <c>TEXT</c>'s General (TEXTFN-025, ADR-0120); a wide column has not been asked a negative number.

## The catalogue, `docs/specs/exsheet-functions/spec.md` (the financial area's file)

No row is made false: the `EOMONTH`, `EDATE` and `TEXT` rows name no refusal this changes. One addition, as other
rows name the arguments they refuse. Old:

> | `EDATE` | Supported | — | |

New:

> | `EDATE` | Supported | — | From serial 0, whose day is 0, `#VALUE!` until Excel is asked (ADR-0047) |

## The Docs Site

`/exsheet/functions` lists each function's name, arguments and description from the engine's declarations; none
of those changes, and no page states `EDATE` or `EOMONTH` from serial 0 or 60. **Nothing is proposed for them.**

The Formulas page's `TEXT` bullet (`samples/ExGrid.Docs/Pages/Sheet/SheetFormulas.razor`, "Supported functions")
is not made false, but it is where a reader meets `TEXT`, and it says nothing of what `General` shows, which now
differs from General in full for every number that reaches 11 characters. Old:

```razor
                <b><code>TEXT</code></b> reads its code in the invariant spelling and shows what a cell in that format
                would show, under the Sheet's culture. A code a cell format does not have is <code>#VALUE!</code>,
```

New (Razor: `@` is written `@@`):

```razor
                <b><code>TEXT</code></b> reads its code in the invariant spelling and shows what a cell in that format
                would show, under the Sheet's culture. <code>General</code>, and <code>@@</code> alone, show a number as
                a wide column's General does, in 11 characters and a minus sign: <code>TEXT(-1/3,"General")</code> is
                <code>-0.333333333</code>. A code a cell format does not have is <code>#VALUE!</code>,
```

## Tickets

Written on this branch (this area's files): a dated comment on `docs/specs/exsheet-functions/issues/01-p1-ready-functions.md`
(EOMONTH-010, EDATE-010) and on `docs/specs/exsheet-functions/issues/02-text.md` (TEXTFN-025, TEXTFN-028). Neither
ticket's last box is ticked: each has `uncertain` cases for the next pass. The kth area's `MOD` comment on ticket
01 is the orchestrator's to apply.

## Change Notes

- Written: `docs/releases/unreleased/area-dates-and-text-serial-0-and-60.md` (ExSheet, Behaviour,
  `exsheet/functions`, ADR-0047) and `docs/releases/unreleased/area-dates-and-text-text-general.md` (ExSheet,
  Behaviour, `exsheet/formulas#supported-functions`, ADR-0120).
- Corrected, outside this area's list, because each said what is no longer so and both are unreleased: the last
  sentence of `run17-dates-text-dates-before-march-1900.md` ("`EDATE` and `EOMONTH` from serial 0 or 60 stay
  `#VALUE!`, and a `WORKDAY` …" is now "A `WORKDAY` …") and of `run17-dates-text-text-general.md` (the sentence
  on the two readings refused until Excel is asked is removed).
