# The area passes' corrections: LARGE, SMALL, AGGREGATE's array form, MOD — proposals for the orchestrator

*(Rewritten 2026-10-12 after the second area pass, to say what is true after it. The first round's
version, on `claude/area-kth-and-mod`, proposed seven classes of `MOD` evaluation and refusals where
they parted; the second pass ruled out every one of the seven, and this version replaces it.)*

Branches: `claude/area-kth-and-mod` (the first pass's corrections, merged into `claude/area-corrections`)
and `claude/area2-kth-and-mod` (the second pass's). The engine (`Functions.Math.cs`, `Functions.Subtotal.cs`),
the corpus (`small.json`, `large.json`, `aggregate.json`, `mod.json`), the tests (`KthAndModTests`,
`SubtotalTests`), ticket 16, the MOD search (`spikes/exsheet-financial-evaluation/mod-search.cs` and its
recorded run, `mod-search.txt`) and the Change Notes are on the branches. What follows is the text this
area would put into files a background agent does not edit, or that another area owns: ADR-0047, ADR-0202,
the engine's README, the function catalogue (`spec.md`), tickets 04 and 01, and the Docs Site. Each edit
quotes the whole lines it replaces, with the target's own indentation.

Evidence: `verification/2026-10-12-windows-area/report.md` (E1, the first pass) and
`verification/2026-10-12-windows-area-2/report.md` (E2, the second, Excel 16.0.20430.20146, en-US, through
COM), with `tests/ExSheet.Engine.Tests/ExcelOracle/results-2026-10-12-area-com-en-US.json`,
`results-2026-10-12-area-2-com-en-US.json` and the seventeenth run's `results-2026-10-11-run17*.json`.
Excel's double is its `number` (G17).

| Case | Formula | Excel | On `main` | Now |
|---|---|---|---|---|
| SMALL-004 | `=SMALL(A1:A9,1.5)` | 2 (E1) | `#VALUE!` | 2, observed |
| SMALL-008 | `=SMALL(A1:A9,9.5)` | `#NUM!` (E2) | `#VALUE!` (7 after the first round) | `#NUM!`, observed |
| AGGREGATE-060 | `=AGGREGATE(15,6,A1:A11,1.2)` | 30 (E1) | `#VALUE!` | 30, observed |
| LARGE-011 | `=LARGE(A1:B5,0.5)` | `#NUM!` (E1) | `#VALUE!` | `#NUM!`, observed |
| AGGREGATE-061 | `=AGGREGATE(14,6,"5",1)` | `#NUM!` (E1) | `#VALUE!` | `#NUM!`, observed |
| MOD-017 | `=MOD(123456789012,0.7)` | 0.5000078322611261 (E1) | 0.5000152587890625 | Excel's, observed |
| MOD-020 | `=MOD(1,0.1)` | 0.09999999999999995 (E2) | 0 | Excel's, observed |
| MOD-024 | `=MOD(3.3,0.01)` | 0.009999999999999754 (E2) | -4.440892098500626E-16 | Excel's, observed |
| MOD-029 | `=MOD(-0.087626,6630)` | 6629.9123739999995 (E2) | 6629.912374 | Excel's, observed |
| MOD-030 | `=MOD(1.1,0.1)` | 2.7755575615628914E-17 (E2) | 0 | Excel's, observed |
| MOD-032 | `=MOD(-1E-20,1)` | 0 (E2) | 1 | 0, observed |
| MOD-033 | `=MOD(-1E-17,1)` | not asked | 1 | `#VALUE!`, where the three evaluations that fit part (section 0) |
| MOD-034 | `=MOD(-0.084474,2270)` | not asked | 2269.915526 | `#VALUE!`, likewise |

MOD-018 to 028 and 030 to 032 are all Excel's now (`mod.json`); LARGE-013, 014, SMALL-007, 009, 010 and
AGGREGATE-062 to 067 agreed in E2.

---

## 0. MOD: three evaluations give every answer Excel gave, and the engine refuses where they part

**What the second pass settled.** The first pass left seven classes of evaluation, which parted at
MOD-020 to 032. Excel's answers there rule out all seven:

- MOD-020 to 028: Excel keeps the exact remainder, about the divisor, where the double quotient rounds up
  onto a whole number (`MOD(1,0.1)` is 0.09999999999999995): no fused multiply-add over that quotient,
  with or without a wrong sign taken as 0 (classes 3 to 5).
- MOD-030 and 031: a remainder within 2^-49 of the number is itself (2.7755575615628914E-17): no
  cancellation of a final addition (classes 5 and 6).
- MOD-029: `MOD(-0.087626,6630)` is 6629.9123739999995, the remainder plus the divisor rounded first at 64
  bits and then to a double; rounded once it is 6629.912374 (every class but 2). The exact sum lies 15/65536
  of a unit in the last place past the midpoint between the two doubles; 64 bits round it onto the
  midpoint, and the double breaks the tie to the even one, below.
- MOD-032: `MOD(-1E-20,1)` is 0, a sum stored as the divisor itself taken as 0 (every class but 7).

**The search, extended.** `mod-search.cs` now reads all 32 observed cases (67 reads, 27 numbers, Excel's
`number`) and tries 41 evaluations, each written once; `mod-search.txt` records every candidate's match
count and the cases it misses. The eight new ones add the divisor at 64 bits — rounded to nearest, as an
x87 register adds, or truncated, as a sum of 64-bit significands would be — or truncated to a double, and
take a sum that is the divisor as 0 in the double stored or in the register. Match counts: the identity in
doubles and its variants (A to A9) 14 to 16 of 32; the first round's class 1 (B, B2, B3) 30, class 2 (B4,
B5) 31, class 7 (B8) 31, classes 3 to 6 (C, C2 to C7, B6, B7) 19 to 30; x87 identities (D to D5) 17 to 25;
decimal (E to E3) 15; the new B11 and B13 (truncated at 64 bits, 0 in the register or not at all) 31, B14
to B16 (truncated to a double) 31. **Three give all 32:**

- **B9**: the exact remainder, the divisor added in an x87 register (64 bits, to nearest), stored as a
  double, and 0 where that double is the divisor;
- **B10**: the same, 0 where the register holds the divisor (((n%d)+d)%d with every step in the register);
- **B12**: the divisor added truncated at 64 bits, stored as a double to nearest, and 0 where it is the
  divisor.

A register of 54 to 63 bits rounded to nearest, or truncation at 54 to 65, would give MOD-029 too; no
hardware or natural software holds those widths, and they are not modelled. **The three part in two
places** (3,616,066 inputs searched: a grid of 418, 1,915,648 random inputs of up to five digits,
1,000,000 a few units from a multiple of the divisor, 200,000 far smaller than the divisor, of the other
sign, and 500,000 whose sum with the divisor lies near a midpoint):

- **The sum is the divisor as a double but not at 64 bits.** `MOD(-1E-17,1)` is 0 by B9 and B12, 1 by
  B10. 43,880 inputs, none of up to five digits. MOD-033.
- **Truncating at 64 bits stores another double than rounding there**, the sum within one 64-bit unit of
  a midpoint. `MOD(-0.084474,2270)` is 2269.915526 by B9 and B10, as rounded once, and 2269.9155259999998
  by B12; `MOD(-0.008511,81200)` is 81199.99148900001 by B9 and B10, which round twice, and 81199.991489
  by B12, as rounded once; `MOD(0.071472,-276)` is the latter's split with the divisor negative. 86,444
  inputs, 13 of the 1,915,648 of up to five digits. MOD-034 to 036.

**The engine** (`Mod`, `TurnedRemainder`) answers the exact remainder where it has the divisor's sign,
and where it has the number's adds the divisor as the three do — from the sum rounded once and its exact
error, the 64-bit sum is a whole number of 2^-11 steps of the double's last place — and is `#VALUE!` where
they part. `mod-search.cs --discriminate` checks over every input searched that it refuses exactly there:
130,316 of 3,616,066, and 13 of the 1,915,648 of up to five digits, where the seven classes' refusal took
19,633. MOD-037 (`MOD(-1E-16,1)`, 0.9999999999999999) and MOD-038 (`MOD(1E-20,1)`, 1E-20) ask two answers
all three give that an evaluation outside them might not: a sum one unit below the divisor, and a number of
the divisor's sign far smaller than it.

**For the orchestrator, plainly.** None of the seven classes the first round named fits; the three that
fit were found after the second pass's answers, by adding to the search the two features MOD-029 and
MOD-032 show (a 64-bit sum, and a sum stored as the divisor taken as 0). Each is a natural way to write
`MOD` on an x87 or with 64-bit significands, and the engine answers only where all three agree, so it
answers nothing the observations leave open between them. If the user holds that evaluations named after
the answers do not "follow from observed answers by a rule they settle" (ADR-0047), the alternative is to
refuse wherever the divisor is added and the sum is not exact — about one input of up to five digits in
ten, by a quick count over the search's random inputs, MOD-018, 019 and 029 among them, which would become
recorded cases (`engineDiffersByDecision`) — until a further pass asks MOD-033 to 038; the engine does not
do that now.

## 1. ADR-0047, the note of 2026-10-12 under "The catalogue's last Decide rows"

Dated additions, the sentences they answer kept. Each edit is whole lines, with the ADR's own indentation.

### 1a. The array-form bullet

Old:

```markdown
  - **`AGGREGATE`'s array form takes a number given directly** as an array of that one Value
    (AGGREGATE-039: `AGGREGATE(14,6,5,1)` is 5). Text and a boolean given directly have not been asked,
    and stay `#VALUE!` (AGGREGATE-061).
```

New:

```markdown
  - **`AGGREGATE`'s array form takes a number given directly** as an array of that one Value
    (AGGREGATE-039: `AGGREGATE(14,6,5,1)` is 5). Text and a boolean given directly have not been asked,
    and stay `#VALUE!` (AGGREGATE-061). *(2026-10-12, the area passes: text given directly is such an
    array too, and holds no number, since it is not coerced: `AGGREGATE(14,6,"5",1)` is `#NUM!`, neither
    5 nor `#VALUE!` (AGGREGATE-061), and so are a boolean and text that spells no number (AGGREGATE-064,
    065). An Error Value given directly is the result, as an Error Value argument is, and option 6 does
    not pass over it: `AGGREGATE(14,6,1/0,1)` is `#DIV/0!` (AGGREGATE-067). `LARGE` and `SMALL`
    themselves coerce a Value given directly: `LARGE("5",1)` is 5 and `SMALL(TRUE,1)` 1 (LARGE-014,
    SMALL-010). The engine follows each.)*
```

### 1b. The `LARGE` bullet

Old:

```markdown
  - **`LARGE`'s k that is not whole is rounded up**: `LARGE(A1:B5,1.5)` and `AGGREGATE(14,6,A1:A11,1.5)`
    are each the 2nd largest (LARGE-007, AGGREGATE-038), which rules out truncation; ODF 1.3's `LARGE`
    (6.18.40), LibreOffice and Gnumeric round it up, and rounding to nearest cannot be told from it at .5
    (LARGE-008 and AGGREGATE-059 ask 1.2). `SMALL`'s k that is not whole, where LibreOffice and ODF round
    down and Gnumeric up, and a k between 0 and 1, where they disagree too, stay `#VALUE!` until Excel is
    asked (SMALL-004, 005, AGGREGATE-060, LARGE-011). k is read at 15 significant digits first, as both
    implementations do, so `1+2^-52` is 1 (LARGE-012, SMALL-006).
```

New:

```markdown
  - **`LARGE`'s k that is not whole is rounded up**: `LARGE(A1:B5,1.5)` and `AGGREGATE(14,6,A1:A11,1.5)`
    are each the 2nd largest (LARGE-007, AGGREGATE-038), which rules out truncation; ODF 1.3's `LARGE`
    (6.18.40), LibreOffice and Gnumeric round it up, and rounding to nearest cannot be told from it at .5
    (LARGE-008 and AGGREGATE-059 ask 1.2). `SMALL`'s k that is not whole, where LibreOffice and ODF round
    down and Gnumeric up, and a k between 0 and 1, where they disagree too, stay `#VALUE!` until Excel is
    asked (SMALL-004, 005, AGGREGATE-060, LARGE-011). k is read at 15 significant digits first, as both
    implementations do, so `1+2^-52` is 1 (LARGE-012, SMALL-006). *(2026-10-12, the area passes: Excel
    truncates `SMALL`'s k, as LibreOffice and ODF do: `SMALL(A1:A9,1.5)` and `SMALL(A1:A9,1.2)` are each
    the smallest (SMALL-004, 005), `SMALL(A1:A9,3.9)` the 3rd (SMALL-009), and `AGGREGATE(15,6,A1:A11,1.2)`
    is 30 (AGGREGATE-060). `LARGE`'s 1.2 is the 2nd largest (LARGE-008, AGGREGATE-059), so `LARGE` rounds
    up, not to nearest. A k below 1 or above the count of the numbers is `#NUM!`, whole or not, before it
    is rounded: `LARGE(A1:B5,0.5)` and `LARGE(A1:B5,0.99)` are `#NUM!`, not rounded up to 1 (LARGE-011,
    013), as `SMALL(A1:A9,0.5)` is (SMALL-007), and `SMALL(A1:A9,9.5)` over nine numbers is `#NUM!`, not
    truncated to the 9th smallest (SMALL-008). The two are one rule — the k-th largest with k rounded up
    is the (n+1-k)-th smallest with that index truncated, and k lies within 1 to n exactly where n+1-k
    does. The engine follows it, and no k is `#VALUE!` any more. SMALL-011 and AGGREGATE-068 ask a k above
    the count again.)*
```

### 1c. The `MOD` paragraph

Old:

```markdown
  The `MOD` refusal of "admitted with some argument values refused" above is replaced: Excel 365 answers
  `MOD(2^27,1)` with 0 (MOD-008). Microsoft documents no limit, and names
  `MOD(n, d) = n - d*INT(n/d)`, which the engine computes in doubles and which gives Excel's digits on every
  case read (MOD-009's 0.09999999999999998 among them). A Microsoft 365 user reported on Microsoft's Q&A
  (2021) that `MOD(1125899999999,1)` is 0 and `MOD(1125900000000,1)` is `#NUM!`, a limit Excel 2010 brought
  in; the engine answers a quotient below 1,125,900,000,000 and is `#NUM!` from it, and MOD-010 to MOD-017
  ask Excel on both sides of it.
```

New:

```markdown
  The `MOD` refusal of "admitted with some argument values refused" above is replaced: Excel 365 answers
  `MOD(2^27,1)` with 0 (MOD-008). Microsoft documents no limit, and names
  `MOD(n, d) = n - d*INT(n/d)`, which the engine computes in doubles and which gives Excel's digits on every
  case read (MOD-009's 0.09999999999999998 among them). A Microsoft 365 user reported on Microsoft's Q&A
  (2021) that `MOD(1125899999999,1)` is 0 and `MOD(1125900000000,1)` is `#NUM!`, a limit Excel 2010 brought
  in; the engine answers a quotient below 1,125,900,000,000 and is `#NUM!` from it, and MOD-010 to MOD-017
  ask Excel on both sides of it. *(2026-10-12, the area passes: the limit holds on both sides (MOD-010 to
  016), but the doubles are not Excel's on every case. Excel's remainder is exact:
  `MOD(123456789012,0.7)` is 0.5000078322611261 (MOD-017), where the identity in doubles gives
  0.5000152587890625, and `MOD(1,0.1)` is 0.09999999999999995 (MOD-020), where the double quotient, 10,
  gives 0. A remainder a hair from 0 is itself, with no cancellation: `MOD(1.1,0.1)` is
  2.7755575615628914E-17 (MOD-030). Where the divisor is added to a remainder of the other sign, the sum
  is not rounded once: `MOD(-0.087626,6630)` is 6629.9123739999995 (MOD-029), the sum rounded first at 64
  bits and then to a double, where rounded once it is 6629.912374; and a sum stored as the divisor
  itself is 0: `MOD(-1E-20,1)` is 0 (MOD-032). The first pass's answers left seven classes of
  evaluation, and the second pass's ruled out all seven. Of 41 evaluations tried against the 32 cases
  Excel answered (`spikes/exsheet-financial-evaluation/mod-search.cs`), three give every one: the
  divisor added in an x87 register's 64 bits, or truncated at 64 bits, and stored as a double, a sum
  that is the divisor taken as 0 in the double or, for the x87's, in the register. They part where the
  sum is the divisor as a double but not at 64 bits (`MOD(-1E-17,1)` is 0 or 1) and where truncating at
  64 bits stores another double than rounding there (`MOD(-0.084474,2270)` is 2269.915526 or
  2269.9155259999998). So, as "admitted with some argument values refused" and ADR-0202 require, the
  engine answers as the three where they agree and is `#VALUE!` where they part, 13 of 1,915,648 random
  inputs of up to five digits, until a Windows run asks MOD-033 to MOD-036.)*
```

## 2. ADR-0202, "The run ExGrid's own by-hand rows need anyway"

Its example is out of date: `MOD` answers a quotient of 2^27 or more since the seventeenth run, and its
`#NUM!` from 1,125,900,000,000 is Excel's own answer, not a refusal.

Old:

```markdown
  - **A function that disagrees is corrected before the tag, or the arguments where it disagrees are
    refused with an Error Value**, as `MOD` refuses a quotient of 2^27 or more (ADR-0047). A width, a
    format or an undo that disagrees is corrected.
```

New:

```markdown
  - **A function that disagrees is corrected before the tag, or the arguments where it disagrees are
    refused with an Error Value**, as `MOD` refuses a quotient of 2^27 or more (ADR-0047). A width, a
    format or an undo that disagrees is corrected. *(2026-10-12: Excel answers a quotient of 2^27 or
    more, and so does `MOD` now; its `#NUM!` from 1,125,900,000,000 is Excel's own answer. `MOD` is an
    example still: where the evaluations that give every answer Excel gave part, as at `MOD(-1E-17,1)`,
    it is `#VALUE!` until a Windows run asks (ADR-0047's note of that date).)*
```

## 3. The engine's README (`src/ExSheet.Engine/README.md`)

### 3a. "Asked of Excel before they are answered"

The financial and dates areas edit the same bullet, so the orchestrator merges by phrase: this area's
phrase is "`SMALL` with a `k` that is not a whole number, and `LARGE` with one between 0 and 1", which
goes, and "`MOD` where the evaluations … (`#VALUE!`; MOD-033 to 036)", which comes.

Old:

```markdown
  (`#VALUE!`); `NETWORKDAYS` and `WORKDAY` with a boolean typed as a date (`#VALUE!`); `SMALL` with a
  `k` that is not a whole number, and `LARGE` with one between 0 and 1; `PMT`, `PV` and `FV` with a
```

New:

```markdown
  (`#VALUE!`); `NETWORKDAYS` and `WORKDAY` with a boolean typed as a date (`#VALUE!`); `MOD` where the
  evaluations that give every answer Excel gave part, as at `MOD(-1E-17,1)` and `MOD(-0.084474,2270)`
  (`#VALUE!`; MOD-033 to 036); `PMT`, `PV` and `FV` with a
```

### 3b. "Answered as the seventeenth Windows run read Excel"

Old:

```markdown
- **Answered as the seventeenth Windows run read Excel.** `MOD` answers a quotient of 2^27 or more, and
  is `#NUM!` from 1,125,900,000,000, the limit a Microsoft 365 user reported (MOD-010 to 017 ask
  Excel). `LARGE`, and `AGGREGATE`'s 14, round a `k` that is not whole up: `LARGE(A1:B5,1.5)` is the
```

New:

```markdown
- **Answered as the seventeenth Windows run read Excel.** `MOD` answers a quotient of 2^27 or more, and
  is `#NUM!` from 1,125,900,000,000, the limit a Microsoft 365 user reported, as the area pass of
  2026-10-12 read Excel on both sides of it (MOD-010 to 016). `LARGE`, and `AGGREGATE`'s 14, round a
  `k` that is not whole up: `LARGE(A1:B5,1.5)` is the
```

### 3c. A bullet after that one

Old:

```markdown
  an operator's result, a call such as `SEQUENCE(3)` — as Excel does.
```

New:

```markdown
  an operator's result, a call such as `SEQUENCE(3)` — as Excel does.
- **Answered as the area passes of 2026-10-12 read Excel.** `SMALL`, and `AGGREGATE`'s 15, truncate a
  `k` that is not whole: `SMALL(A1:A9,1.5)` is the smallest. A `k` below 1 or above the count of the
  numbers is `#NUM!` for `LARGE` and `SMALL` alike, whole or not, before it is rounded:
  `SMALL(A1:A9,9.5)` over nine numbers is `#NUM!`. `AGGREGATE`'s array form takes text or a boolean
  given directly as an array holding no number: `AGGREGATE(14,6,"5",1)` is `#NUM!`. `MOD`'s remainder is
  exact, as Excel's is: `MOD(123456789012,0.7)` is 0.5000078322611261 and `MOD(1,0.1)`
  0.09999999999999995, where `number - divisor*INT(number/divisor)` in doubles gives 0.5000152587890625
  and 0. Where the divisor is added to a remainder of the other sign, the sum is held at 64 bits before
  it is stored as a double, which gives Excel's `MOD(-0.087626,6630)`, 6629.9123739999995, and a sum
  stored as the divisor itself is 0, as Excel's `MOD(-1E-20,1)` is. Where the evaluations that give
  every answer Excel gave still part, `MOD` is `#VALUE!` (above).
```

### 3d. "`SUBTOTAL` and `AGGREGATE` while no row can be hidden"

Old:

```markdown
  array form takes a number given directly. `#VALUE!`: an argument that is no reference when it is
```

New:

```markdown
  array form takes a Value given directly as an array of that one Value: a number is that number, and
  text or a boolean holds none (`#NUM!`). `#VALUE!`: an argument that is no reference when it is
```

## 4. The function catalogue (`docs/specs/exsheet-functions/spec.md`)

Old:

```markdown
| `LARGE` | Supported | — | A `k` that is not whole is rounded up, as Excel's (ADR-0047, 2026-10-12) |
| `SMALL` | Supported | — | A `k` that is not whole is `#VALUE!` until observed |
```

New:

```markdown
| `LARGE` | Supported | — | A `k` that is not whole is rounded up, and one below 1 or above the count of the numbers is `#NUM!`, as Excel's (ADR-0047, 2026-10-12) |
| `SMALL` | Supported | — | A `k` that is not whole is truncated, and one below 1 or above the count of the numbers is `#NUM!`, as Excel's (ADR-0047, 2026-10-12) |
```

Old:

```markdown
| `AGGREGATE` | Supported | — | As `SUBTOTAL`; options 0 to 3 pass over a nested cell and 4 to 7 count it, and a function number for a function the engine does not support (13, 16 to 19) is `#VALUE!`. Ticket 16 |
```

New:

```markdown
| `AGGREGATE` | Supported | — | As `SUBTOTAL`; options 0 to 3 pass over a nested cell and 4 to 7 count it, and a function number for a function the engine does not support (13, 16 to 19) is `#VALUE!`; text or a boolean given as the array holds no number. Ticket 16 |
```

Old:

```markdown
| `MOD` | Supported | — | Excel's sign follows the divisor; `#NUM!` from a quotient of 1,125,900,000,000 |
```

New:

```markdown
| `MOD` | Supported | — | Excel's sign follows the divisor. The exact remainder; where the divisor is added to it, the sum held at 64 bits and then stored as a double, and a sum stored as the divisor 0, as Excel's (MOD-017 to 032); `#VALUE!` where the evaluations that give Excel's answers still part, as at `MOD(-1E-17,1)` and `MOD(-0.084474,2270)`, until Excel is asked (MOD-033 to 036; ADR-0047, 2026-10-12); `#NUM!` from a quotient of 1,125,900,000,000 |
```

## 5. Ticket 04 (`04-p2-ready-functions.md`, the financial area's)

A comment to append:

> 2026-10-12, with the area passes (`verification/2026-10-12-windows-area/report.md` and
> `verification/2026-10-12-windows-area-2/report.md`): Excel truncates `SMALL`'s k that is not whole
> (SMALL-004, 005, 009: 1.5 and 1.2 are each the smallest, 3.9 the 3rd), rounds `LARGE`'s up (LARGE-008,
> 009), and a k below 1 or above the count of the numbers is `#NUM!` before it is rounded (LARGE-011, 013:
> 0.5 and 0.99; SMALL-007: 0.5; SMALL-008: 9.5 over nine numbers, which truncated would be the 9th
> smallest; LARGE-010: 10.5 over ten). `LARGE` and `SMALL` coerce a Value given directly as the array
> (LARGE-014: `LARGE("5",1)` is 5; SMALL-010: `SMALL(TRUE,1)` is 1). The engine reads k so for `LARGE`,
> `SMALL` and `AGGREGATE`'s 14 and 15 (`KthOf`), and no k is `#VALUE!` any more (ADR-0047's note of that
> date; `KthAndModTests`). SMALL-011 (`SMALL(A1:A9,9.2)`, `#NUM!`) is a new `uncertain` case: a k a little
> above the count, which rounded to nearest would be 9.

The ticket is `done`, and ADR-0208 does not close one on "the next run asks". Either a box —

> - [ ] `oracle.ps1 -Area small` before the tag (ADR-0208): SMALL-011

— with the Status `ready-for-human` and a `Roadmap:` line ("ExSheet — `SMALL` checked against Excel for a
k a little above the count of its numbers, which it refuses with `#NUM!`"), or the orchestrator's word
that SMALL-011 belongs to ticket 16's open box, with AGGREGATE-068.

## 6. Ticket 01 (`01-p1-ready-functions.md`, the dates-and-text area's)

A comment to append:

> 2026-10-12, with the area passes: MOD-010 to 016 agreed, so the limit at a quotient of
> 1,125,900,000,000 holds on both sides. The doubles did not: Excel's remainder is exact
> (`MOD(123456789012,0.7)` is 0.5000078322611261, MOD-017; `MOD(1,0.1)` is 0.09999999999999995, MOD-020,
> where the identity in doubles gave 0.5000152587890625 and 0), a remainder a hair from 0 is itself
> (`MOD(1.1,0.1)` is 2.7755575615628914E-17, MOD-030, where it gave 0), the divisor added to a remainder
> of the other sign is held at 64 bits before the sum is stored as a double (`MOD(-0.087626,6630)` is
> 6629.9123739999995, MOD-029), and a sum stored as the divisor itself is 0 (`MOD(-1E-20,1)`, MOD-032).
> Three evaluations give all 32 answers Excel has given, and none evaluates the identity in doubles
> (`spikes/exsheet-financial-evaluation/mod-search.cs`, `mod-search.txt`). The engine answers as the three
> where they agree, and `#VALUE!` where they part: where the sum is the divisor as a double but not at 64
> bits (`MOD(-1E-17,1)`), and where truncating at 64 bits stores another double than rounding there
> (`MOD(-0.084474,2270)`), 13 of 1,915,648 random inputs of up to five digits. MOD-033 to 036 ask where
> they part, and MOD-037 and 038 two answers they share (ADR-0047's note of that date; `KthAndModTests`).

The same ADR-0208 question as ticket 04's: a box —

> - [ ] `oracle.ps1 -Area mod` before the tag (ADR-0208): MOD-033 to 038

— the Status `ready-for-human`, and a `Roadmap:` line ("ExSheet — `MOD` checked against Excel where the
divisor added to the remainder lands on the divisor or a hair from the midpoint between two doubles, as
in `MOD(-1E-17,1)` and `MOD(-0.084474,2270)`, which are refused until then").

## 7. The Docs Site

### `/exsheet/formulas`, "Supported functions" (`SheetFormulas.razor`)

Old:

```html
                <b><code>LARGE</code> and <code>SMALL</code>'s k.</b> <code>LARGE</code> rounds a k that is not whole
                up, as Excel does: <code>=LARGE(A1:B5,1.5)</code> is the 2nd largest. <code>SMALL</code>'s, and a k
                between 0 and 1, are <code>#VALUE!</code> until Excel has been asked. <b><code>MOD</code></b> is
                <code>#NUM!</code> from a quotient of 1,125,900,000,000, the limit reported of Excel 365.
```

New:

```html
                <b><code>LARGE</code> and <code>SMALL</code>'s k.</b> A k that is not whole is rounded up by
                <code>LARGE</code> and truncated by <code>SMALL</code>, as Excel does: <code>=LARGE(A1:B5,1.5)</code>
                is the 2nd largest, and <code>=SMALL(A1:A9,1.5)</code> the smallest. A k below 1 or above the count
                of the numbers is <code>#NUM!</code>, whole or not: <code>=SMALL(A1:A9,9.5)</code> over nine numbers
                is <code>#NUM!</code>. <b><code>MOD</code></b>'s remainder is exact, as Excel's is:
                <code>=MOD(1,0.1)</code> is 0.09999999999999995, where
                <code>number - divisor*INT(number/divisor)</code> in doubles would give 0. Where the divisor is
                added to a remainder of the other sign, the sum is held at 64 bits before it is stored, which gives
                Excel's <code>=MOD(-0.087626,6630)</code>, 6629.9123739999995, and a sum stored as the divisor is 0,
                as Excel's <code>=MOD(-1E-20,1)</code> is. Where the ways of computing it that give every answer
                Excel was seen to give part, it is <code>#VALUE!</code> until Excel has been asked: a sum that is
                the divisor only once stored, as in <code>=MOD(-1E-17,1)</code>, and a sum a hair from the midpoint
                between two doubles, as in <code>=MOD(-0.084474,2270)</code>, about one input of up to five digits
                in 150,000. It is <code>#NUM!</code> from a quotient of 1,125,900,000,000, as Excel 365's.
```

In the `SUBTOTAL` and `AGGREGATE` list item, old:

```html
                <code>MODE.SNGL</code>, is <code>#VALUE!</code>.
```

New:

```html
                <code>MODE.SNGL</code>, is <code>#VALUE!</code>. Given text or a boolean in place of its array,
                <code>AGGREGATE</code>'s <code>LARGE</code> and <code>SMALL</code> find no number, as Excel's do:
                <code>=AGGREGATE(14,6,"5",1)</code> is <code>#NUM!</code>.
```

No Example shows these functions; nothing else on the site moves.

## 8. Found while writing

- **SMALL-009 asks 3.9, not the triage's 2.9** (first round). Over A1:A9 the 2nd and 3rd smallest are
  both 3, so 2.9 could not tell truncation from rounding; the 3rd and 4th are 3 and 4.
- **Observed cases described as "Excel to be asked".** `-Update` rewrites `source` only. The 46 in this
  area's four files (SMALL-006, 007, 009, 010; LARGE-005, 006, 008 to 010, 012 to 014; MOD-006, 007, 009
  to 016; AGGREGATE-007, 008, 011, 022 to 024, 032 to 037, 041, 055 to 059, 062 to 067) now say what
  Excel answered and which run read it, and MOD-006, 007 and 009 compare Excel's doubles to the bit
  (`claude/area2-kth-and-mod`, the second commit). Other areas' files have the same leftovers; a sweep
  would fix them there.
