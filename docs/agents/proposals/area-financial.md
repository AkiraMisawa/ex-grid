# Area: financial — proposals after PMT's decision of 2026-10-12

Branch `claude/area2-financial`, on `claude/area-corrections` (which holds the first round, `claude/area-financial`).
The cases: FV-011 to 024, PV-008 to 016, PMT-011 to 038, NPV-001 and 009 to 022, XNPV-005 to 020. The
evidence: `verification/2026-10-12-windows-area-2/` (`results-2026-10-12-area-2-com-en-US.json`, Excel
16.0.20430.20146, each double Excel's `number`, G17), the first pass `verification/2026-10-12-windows-area/`, the
seventeenth run's results, and the third search in `spikes/exsheet-financial-evaluation/` (`Search.cs`,
`search-cs.json`; the README says what each family reproduces and which observed doubles rule each out).

What the branch did, in brief:

- **Decided with the user on 2026-10-12, and applied: `PMT` is kept, under an exception to ADR-0047 for
  `PMT` alone.** Its result may differ from Excel's in digits no format displays (observed up to 11 units in
  the last place, PMT-026, a relative error of about 1e-15), because the engine gives the exact payment rounded
  once and the difference is Excel's own rounding; it is listed under 0.1.0's Known limitations. The 19
  observed payments that differ (PMT-011 to 016, 018, 020, 021, 023 to 032) are recorded as differences by that
  decision, `source: observed`, `expect` the engine's double and `excelExpect` Excel's; the 11 that agree are
  observed. PMT-011 and PMT-012 (an `nper` of 10.5 and −10), refused before only because PMT's evaluation is not
  Excel's, are answered by the same exact model with 1+rate raised by the power PV and FV share, exactly:
  −989.2729401451878 and 970.3654226924855, each one unit in the last place from Excel's −989.2729401451879
  and 970.3654226924854. Where PMT's forms part in kind, it refuses until Excel is asked: a rate of −1 with an
  `nper` above 0 (PMT-034), a growth of exactly 1 (PMT-035), a growth past a double's range (PMT-038).
  PMT-036 and PMT-037 ask whether PMT shares the power's odd root and its `#DIV/0!`. `NPV` is not part of the
  exception and stays undeclared.
- **Settled and followed: `PV` and `FV`.** Of the 480 evaluations searched for each, one gives all 17 FV and 9
  PV doubles and all five Error Values with XNPV-013's exponential: Microsoft's identity in doubles, the
  payment times (1+rate·type)·((g−1)/rate), the sum as written (FV-014, FV-015: a fused multiply-add is one unit
  away), and g *the financial power*: squaring to a whole `nper`, 1 over the power to a negative one (FV-022),
  the square root at 0.5 (FV-016), e^(n ln(1+rate)) otherwise, the odd root of a negative 1+rate where 1/n is
  odd (FV-017) and `#NUM!` for any other fraction of one (PV-011), and at 1+rate = 0 the `^` operator's `#NUM!`
  for 0^0 (FV-018) and `#DIV/0!` for a negative power (FV-020, FV-021, PV-012). The exponential is correctly
  rounded, as XNPV-013 found Excel's where glibc's is not; the logarithm is correctly rounded too, which no
  observed double tells from glibc's (FV-023 and PV-016 ask). PV-014, PV-015 and FV-024 ask where PV's own
  observations leave other readings. PV at a rate of −1 with an `nper` above 0 divides by a growth of 0, whose
  Error Value is unobserved, and is refused (PV-013 asks).
- **`XNPV` follows Excel at every observed double.** Of 2,592 candidates, 4 give all nine doubles, and they are
  two evaluations: the days over 365, each value divided by the financial power of its years, summed from the
  first, with or without Excel's last-addition adjustment, which parts only where the last addition nearly
  cancels (the engine takes the first and refuses there; XNPV-018 asks). XNPV-013 is the correctly rounded
  exponential's double, where glibc's `exp`, which .NET takes on Linux, is a unit low. **None of the 4 gives
  Excel's six `#NUM!` at negative rates** (−0.5 to −2; each gives numbers at some): the engine checks the rate
  before it evaluates and is `#NUM!` below 0, Excel's answer at the four rates asked and the engine's at any
  other (XNPV-020 asks −0.01). It refuses a rate of exactly 0, where "below 0" and "0 or below" part, and this
  round moved that refusal after the counts and the dates are checked, since where those give `#NUM!` both
  readings give it (XNPV-019 asks).
- **New in the engine: `CorrectlyRounded.Exp` and `.Log`**, e^x and ln x rounded once, the same on every
  platform (double-double, and 256-bit fixed point near a rounding boundary), 40,010 values checked against
  Python's `decimal`; and, this round, **`ExpWide` and `LogWide`**, the same series unrounded, by which PMT
  raises 1+rate carried exactly. At 3,000 drawn inputs with a fractional or negative `nper`, PMT gave the exact
  payment of Python's `decimal` at 150 digits at all 2,710 where both are numbers, and `Search.cs --refusals`
  now checks PMT against an exact payment over its 7,296-input grid.
- **Not settled: `NPV`**, which stays undeclared (`#NAME?`) for 0.1.0 as ADR-0202's note decided: 0 of 5,796
  evaluations give all 21 observed doubles; the best give 7. The undeclaring is **not** reversed.
- **New cases:** FV-023, FV-024, PV-013 to 016, XNPV-018 to 020 (the second pass), and PMT-034 to 038 (this
  round). Every case of the area that Excel answered is `observed`; the descriptions that still said "Excel to
  be asked" of an answered case (PMT-004 to 007 and 010, PV-002 to 004 and 006, FV-004, 005, 008 and 010,
  XNPV-002 and 003) say what was observed.

The branch edits itself what is not an ADR, the glossary, the Definition of Done or the Docs Site: the
engine, the corpus, the spike, the financial Change Notes (section 9), the catalogue's PMT, PV, FV, NPV and XNPV
rows and its notes 15 and 16 (`docs/specs/exsheet-functions/spec.md`), and two comments on ticket 13.

Each edit below is exact: the old text, then the new, whole lines with the target's indentation. They replace
every edit this file proposed before; none of those has been applied.

---

## 1. Decided: PMT's last digits (applied in the branch)

**The decision** (with the user, 2026-10-12): PMT is kept, with an exception to ADR-0047 for PMT alone: its
result may differ from Excel's in digits no format displays (observed up to 11 units in the last place, a
relative error of about 1e-15), because the engine gives the exact payment rounded once and the difference is
Excel's own rounding. It is listed under 0.1.0's Known limitations. NPV stays undeclared, and is not part of
the exception.

**What it covers.** The 30 payments Excel was asked (PMT-001 to 004, 008 to 033; PMT-004 at a rate of 0), of
which these 19 differ from the engine's:

| Case | Formula | Excel | Engine | Units |
|---|---|---|---|---:|
| PMT-011 | `=PMT(8%/12,10.5,10000)` | -989.2729401451879 | -989.2729401451878 | 1 |
| PMT-012 | `=PMT(8%/12,-10,10000)` | 970.3654226924854 | 970.3654226924855 | 1 |
| PMT-013 | `=PMT(0.05/12,36,250000,-5000)` | -7363.7031239763755 | -7363.703123976373 | 3 |
| PMT-014 | `=PMT(0.0725/12,12,250000,0,1)` | -21530.430018725485 | -21530.430018725492 | 2 |
| PMT-015 | `=PMT(0.05/12,12,10000,0,1)` | -852.5226402171 | -852.5226402170998 | 2 |
| PMT-016 | `=PMT(0.045/12,12,250000,-5000)` | -20936.487788661154 | -20936.48778866115 | 1 |
| PMT-018 | `=PMT(0.0365/365,365,10000)` | -27.901671561224465 | -27.90167156122446 | 1 |
| PMT-020 | `=PMT(0.15,12,1000,-5000,1)` | -10.501648240558216 | -10.501648240558218 | 1 |
| PMT-021 | `=PMT(0.15,60,250000,-5000,1)` | -32615.986669943602 | -32615.986669943606 | 1 |
| PMT-023 | `=PMT(0.0365/365,24,250000,20000,1)` | -11260.941796175353 | -11260.941796175352 | 1 |
| PMT-024 | `=PMT(0.0525/12,24,250000,-5000,1)` | -10750.7823801456 | -10750.782380145603 | 2 |
| PMT-025 | `=PMT(0.065/12,60,1000,-5000,1)` | 50.90552130119933 | 50.90552130119935 | 3 |
| PMT-026 | `=PMT(0.07/12,240,1000,-5000)` | 1.8452907580883169 | 1.8452907580883144 | 11 |
| PMT-027 | `=PMT(0.00001,360,10000,-5000,1)` | -13.963833694802407 | -13.963833694802405 | 1 |
| PMT-028 | `=PMT(0.025/4,240,1000,-5000)` | 0.9737277420722876 | 0.9737277420722884 | 7 |
| PMT-029 | `=PMT(0.06/52,60,1000,-5000,1)` | 63.197272167756296 | 63.197272167756275 | 3 |
| PMT-030 | `=PMT(0.045/12,12,10000,-5000)` | -445.6426079318603 | -445.64260793186025 | 1 |
| PMT-031 | `=PMT(0.001,12,250000,20000,1)` | -22603.914076303507 | -22603.91407630351 | 1 |
| PMT-032 | `=PMT(0.03/12,12,250000,20000)` | -22817.29866479201 | -22817.298664792015 | 1 |

The largest relative difference is PMT-026's, 1.3e-15. PMT-001 to 004, 008 to 010, 017, 019, 022 and 033 are
the engine's doubles.

**The search, for the record.** `Search.cs`, the third search, tried 39,602 candidates (11,242 in `search.py`
and 7,344 in the second search before it): every family of the earlier searches and (1+rate)^n − 1 carried
accurately by every pairing of a log1p, the product n·log1p rounded or exact, an exp and an expm1; (1+rate)^n −
1 squared without forming 1+rate; the period-by-period recurrence; the financial power; LibreOffice's
`ScGetPMT`. None gives all 29 payments at a rate other than 0; the best give 16 and miss PMT-020 by 19,
PMT-026 by 15 and PMT-028 by 13 units. The spike's README has what rules each family out.

**What the branch applied.** The 19 cases above are `observed` with `engineDiffersByDecision` citing the
exception and `excelExpect` Excel's double. PMT answers an `nper` that is not a whole number, or is negative,
by the exact model with 1+rate raised by the financial power, exactly (`ExactGrowth`, double-double); the
power's Error Values are PMT's too. Where PMT's forms part in kind it refuses with `#VALUE!` until Excel is
asked: at a rate of −1 with an `nper` above 0, the forms that multiply by the growth give −fv and those that
divide by it an Error Value (PMT-034); a growth of exactly 1, a rate of −2 and an even `nper`, divides every
form by 0, with an Error Value no run has observed (PMT-035); a growth past a double's range gives `#NUM!` in
doubles and the payment in an x87's wider exponent (PMT-038, where the engine gave `#NUM!` before). PMT-036
(`=PMT(-9,1/3,-1)`, −6, the odd root) and PMT-037 (`=PMT(-1,-10,1000)`, `#DIV/0!`) are the engine's answers,
asked.

## 2. XNPV's evaluation (the first round's question 1b), answered as far as the doubles go

The first round found 72 evaluations that gave XNPV-001 and 004, and the engine refused where the six it
computed parted. The second pass read XNPV-005, 006 and 011 to 017:

- XNPV-005 and 006, whole years: squaring (2023.0065393622904, 2277.5848519247406), where e^(x ln b) gives
  2023.0065393622895 and 2277.5848519247415, and `Math.Pow` the second of those;
- XNPV-011, 101 days: the days over 365 (−7286.877809009438), where times 1/365 gives −7286.877809009439;
- XNPV-013: the discount e^(x ln(1+rate)) with the exponential correctly rounded (700.2269770028577). At 797
  days glibc's `exp` gives 1.2313555711418422, a unit below the correctly rounded 1.2313555711418425, and the
  sum 700.2269770028581, 4 units from Excel's;
- XNPV-014 and 015: values in no order of their dates, summed in the order given;
- XNPV-007 to 010, 012 and 017: `#NUM!` at −1, −1.5, −2 and −0.5.

Of the 2,592 candidates the third search tries, 4 give all nine doubles, and they are two evaluations: summed
from the first, and the same with Excel's last-addition adjustment. **None of the 4 gives the six `#NUM!`**:
each gives numbers at −0.5, at every date the first (−7250) or at whole years below −1 (400). So the engine
computes the first evaluation, refuses where the two part (XNPV-018 asks), and checks the rate before it
evaluates: `#NUM!` below 0, which is Excel's answer at −0.5, −1, −1.5 and −2 and the engine's at any other
negative rate (XNPV-020 asks −0.01), and `#VALUE!` at exactly 0, where "below 0" and "0 or below" part, but
where the counts or a date before the first give `#NUM!` under both readings (XNPV-019 asks).
`Search.cs --refusals` finds the engine giving the two evaluations' answer at all 4,004 inputs it draws, but
the 506 at a negative rate, where it gives `#NUM!`.

## 3. PV and FV: settled

The financial power (above; `Search.cs` g22) is the only one of FV's 480 candidates that gives all 17 doubles
and all three Error Values with the platform-independent functions: g20 gives the doubles but `#NUM!` for
FV-020's `#DIV/0!`, and g23, the same power with the platform's `Math.Exp` and `Math.Log`, gives everything FV
was observed to give and is ruled out by XNPV-013, which raises 1+rate by the same power and tells the
exponentials apart. No observed double tells the logarithms apart: the engine takes the correctly rounded one
with the exponential, and FV-023 and PV-016 ask where it parts from glibc's. PV's own doubles and Error Values
leave g18, g20, g22 and g23; FV-022 rules out g18 and FV-020 g20, PV raising 1+rate as FV does (every PV double
and Error Value is the financial power's). PV-014 and PV-015 ask PV where its own observations leave those
readings, and FV-024 asks half a period before. `Search.cs --refusals` finds the engine giving g22's answer at
all 7,296 inputs of each grid, but PV's 384 at a rate of −1 with `nper` above 0, which it refuses (PV-013).

So the first round's refusals where readings parted (half a period, a negative fraction, a rate of −1 with a
negative `nper`, FV's fused sums, a fraction at a rate below −1) are all answered as Excel answered them, and
its two kept exceptions (FV-018 answered 500, FV-019's unfused sum) are settled: FV-018 is `#NUM!`, FV-019 the
unfused sum.

## 4. ADR-0047

### 4a. The admission rule: a pointer to the exception

Old:

```
**A function is added only when it can match Excel, including Excel's edge cases.** That means its
argument coercions, how it treats blanks and text inside ranges, how it propagates Error Values,
and its rounding. The tests for a function are written from Excel's observed behaviour. A known
deviation keeps the function out.
```

New:

```
**A function is added only when it can match Excel, including Excel's edge cases.** That means its
argument coercions, how it treats blanks and text inside ranges, how it propagates Error Values,
and its rounding. The tests for a function are written from Excel's observed behaviour. A known
deviation keeps the function out.
*(2026-10-12, decided with the user: one exception, `PMT`, whose result may differ from Excel's in
digits no format displays. The note of that date, "An exception for `PMT` alone", says why and how
far it goes.)*
```

### 4b. The `type` bullet of the note of 2026-10-12 "PV and FV give Excel's double, and PMT every double Excel was seen to give"

Old:

```
- **A `type` that is a whole number other than 0 is payment at the start**, as Excel read 2
  (PMT-008). A fractional one stays `#VALUE!` until Excel is asked.
```

New:

```
- **A `type` that is a whole number other than 0 is payment at the start**, as Excel read 2
  (PMT-008). A fractional one stays `#VALUE!` until Excel is asked. *(Answered by the area passes of
  2026-10-12: `type` is a truth value. Excel gave the payment at the start for 0.5, −1 and 0.1 (PMT-009,
  PMT-010, PMT-019), so a fraction is not truncated or rounded, and any `type` other than 0 is payment at
  the start, in PMT, PV and FV.)*
```

### 4c. The `nper` bullet

Old:

```
- Following "admitted with some argument values refused", at a rate other than 0 an `nper` that is
  not a whole number from 0 to 2^53 is `#VALUE!`: squaring raises only to a whole number, and how
  Excel raises 1+rate to any other has not been observed. At a rate of 0 no power is raised, and any
  `nper` is answered.
```

New:

```
- Following "admitted with some argument values refused", at a rate other than 0 an `nper` that is
  not a whole number from 0 to 2^53 is `#VALUE!`: squaring raises only to a whole number, and how
  Excel raises 1+rate to any other has not been observed. At a rate of 0 no power is raised, and any
  `nper` is answered. *(Answered by the area passes of 2026-10-12 for PV and FV. Excel's doubles are
  those of raising 1+rate to a negative `nper` as 1 over the power to its absolute value (FV-007,
  FV-022), to 0.5 by the square root (FV-016), and to any other fraction as e^(nper ln(1+rate)) with
  the exponential correctly rounded (FV-006, PV-005; XNPV-013 tells it from glibc's) and the logarithm
  correctly rounded too, which no observed double tells from glibc's (FV-023 and PV-016 ask). Excel
  gave a negative 1+rate to a fraction whose reciprocal is odd its odd root (FV-017) and to another
  fraction `#NUM!` (PV-011), and 1+rate of 0 to the power 0 `#NUM!` (FV-018) and to a negative power
  `#DIV/0!` (FV-020, FV-021, PV-012). PV and FV now answer every `nper` so, but PV at a rate of −1 with
  an `nper` above 0, which divides by a growth of 0 and is refused until Excel is asked (PV-013). PMT
  answers every `nper` by its exact model, 1+rate raised exactly by the same power, under the exception
  for PMT below (PMT-011, PMT-012). Past 2^53, where every double is a whole number, all three still
  refuse.)*
```

### 4d. A dated note after the paragraph that ends the note

Old:

```
The cases the search could not settle — which of two orders of the payment's factors, and which of
the evaluations that also give PMT-001 to 008 — are `uncertain` in the corpus, with the engine's
double, for the next Windows run.
```

New:

```
The cases the search could not settle — which of two orders of the payment's factors, and which of
the evaluations that also give PMT-001 to 008 — are `uncertain` in the corpus, with the engine's
double, for the next Windows run.

*(2026-10-12, the two area passes, `verification/2026-10-12-windows-area/` and `-area-2/`.)* **What
those cases answered.** The order of the payment's factors: Excel's doubles are the payment times
the product of 1+rate·type and ((g−1)/rate) (FV-009, PV-007, where the engine's order was one unit in
the last place below), and with a present value as well the sum as written, not a fused multiply-add
(FV-014, FV-015). PV and FV raise 1+rate by one power, the note's squaring at a whole number and the
bullet above otherwise: `spikes/exsheet-financial-evaluation/` searched 480 evaluations of FV and 480
of PV, and this power is the one that gives all 17 and 9 of their observed doubles and their Error
Values with the exponential XNPV-013 tells apart. XNPV divides each value by that power of its days
over 365 and sums from the first value: of 2,592 candidates, 4 give its nine observed doubles, two
evaluations, this one and the same with Excel's last-addition adjustment, which parts from it only
where the last addition nearly cancels and is refused there until asked. None of the four gives
XNPV's `#NUM!` at a negative rate: the engine checks the rate before it evaluates and is `#NUM!`
below 0, as Excel was at −0.5, −1, −1.5 and −2, its own answer at other negative rates until
XNPV-020 is read, and refuses a rate of 0 until asked. The engine now computes e^x and ln x correctly
rounded on every platform; the platform's `Math.Exp` is a unit in the last place from Excel's
discount at XNPV-013, and no observed double tells the logarithms apart. **PMT's evaluation is not
settled**: of 39,602 evaluations searched, none gives all 29 observed payments at a rate other than
0 and the best give 16; 19 of the 30 observed payments are 1 to 11 units in the last place from the
engine's exact model, which PMT keeps by the exception that follows.

*(2026-10-12, decided with the user)* **An exception for `PMT` alone: its result may differ from
Excel's in digits no format displays.** Two area passes asked Excel 29 payments at a rate other than
0, and some 58,000 evaluations were searched; none gives them all. The engine gives the exact payment
over Excel's doubles, rounded once: the payment at the end of each period carried in double-double
arithmetic with 1+rate exact and raised exactly, and at the start that double divided by 1+rate,
rounded once. Excel's payment differs from it at 19 of the 30 observed, by 1 to 11 units in the last
place (PMT-026, a relative error of about 1e-15), and the difference is Excel's own rounding, which no
format displays. `PMT` is kept with that difference. Its corpus cases record each as a difference by
this decision, with Excel's double beside the engine's. The exception covers those digits and nothing
else:

- An `nper` that is not a whole number, or is negative, is answered by the same exact model, 1+rate
  raised exactly by the power PV and FV share (PMT-011, PMT-012, each one unit in the last place from
  Excel's). The power's Error Values are PMT's too (PMT-036 and PMT-037 ask).
- Where PMT's forms part in kind, a number against an Error Value or one Error Value against another,
  it is refused with `#VALUE!` until Excel is asked: a rate of −1 with an `nper` above 0 (PMT-034), a
  growth of exactly 1 (PMT-035), a growth past a double's range (PMT-038).
- `NPV` is not part of it, and stays undeclared (ADR-0202's note of this date).
- `0.1.0` lists it under Known limitations (ADR-0202).
```

## 5. ADR-0202

### 5a. A dated note on the note of 2026-10-12

Old:

```
  to the one `oracle.ps1 -Area` pass before `0.1.0-rc.1` (ADR-0208's note of this date), and the answers
  decide. If no such pass can be made before the tag, `NPV` is refused for `0.1.0` as `IRR` is, listed
  under Known limitations, and `PMT` stays as it is.
```

New:

```
  to the one `oracle.ps1 -Area` pass before `0.1.0-rc.1` (ADR-0208's note of this date), and the answers
  decide. If no such pass can be made before the tag, `NPV` is refused for `0.1.0` as `IRR` is, listed
  under Known limitations, and `PMT` stays as it is.
  *(2026-10-12, what the answers decided. Two passes were made, `verification/2026-10-12-windows-area/`
  and `-area-2/`. **`NPV` is refused for `0.1.0`, as `IRR` is** (`#NAME?`), and listed under Known
  limitations: Excel answers the same values alike typed or in a range, which leaves 18 distinct observed
  doubles, and none of the 5,796 evaluations searched gives them all; the best give 7 of 21. **`PMT` at
  small rates agrees** (PMT-017, at 10^-7). **`PMT` at ordinary rates does not**: 19 of its 30 observed
  payments are 1 to 11 units in the last place from the engine's, and none of the 39,602 evaluations
  searched gives every payment Excel gave. Decided with the user the same day: **`PMT` is kept, under
  ADR-0047's exception for `PMT` alone** (its note of this date), and its last digits are listed under
  Known limitations. **`XNPV` agrees at every observed double**: 4 of the 2,592 candidates searched give
  all nine, two evaluations that part only where a last addition nearly cancels, where the engine
  refuses; none of them gives Excel's `#NUM!` at a negative rate, which the engine gives by a check of
  the rate. `spikes/exsheet-financial-evaluation/` holds the searches.)*
```

### 5b. Known limitations (the Consequences)

Old:

```
  *(2026-10-12, decided with the user: also a whole-column `SUM` on every row of a large Sheet. SH-19 is the
  case it decided, a 10,000 × 20 block with its twenty whole-column totals beside it, where an edit takes
  tens of milliseconds; with such a `SUM` on every row instead, an edit was measured at about 40 to 48
  seconds and holds the circuit that long. `verification/2026-10-11-linux-sh19/` has both.)*
```

New:

```
  *(2026-10-12, decided with the user: also a whole-column `SUM` on every row of a large Sheet. SH-19 is the
  case it decided, a 10,000 × 20 block with its twenty whole-column totals beside it, where an edit takes
  tens of milliseconds; with such a `SUM` on every row instead, an edit was measured at about 40 to 48
  seconds and holds the circuit that long. `verification/2026-10-11-linux-sh19/` has both.)*
  *(2026-10-12: also `NPV`, refused by the note of that date above, and `PMT`'s last digits, which may
  differ from Excel's in digits no format displays, under ADR-0047's exception of that date.)*
```

### 5c. The Known limitations lines for `0.1.0`'s Release Notes

```
- `NPV` is not supported and is `#NAME?`, as `IRR`, `XIRR` and `RATE` are: its last digits differ from
  Excel's, and no evaluation found gives every result Excel was seen to give
  ([Formulas](exsheet/formulas#supported-functions)).
- `PMT` gives the exact payment rounded once, and Excel's payment can differ from it in digits no format
  displays: 19 of the 30 payments Excel was asked differ by 1 to 11 units in the last place, a relative
  difference of at most about 1e-15 ([Formulas](exsheet/formulas#supported-functions)).
```

## 6. The Definition of Done, §27's note of 2026-10-11 and SH-7

### 6a. The note's first bullet

Old:

```
- **SH-7 is met as written**: every case under `tests/ExSheet.Engine.Tests/ExcelCases/` whose source is
  `uncertain` or `documented` is put to Excel on the sign-off's Windows run (`oracle.ps1 -Update`), and
  each that disagrees is corrected, or its arguments refused with an Error Value, before the tag.
```

New:

```
- **SH-7 is met as written**: every case under `tests/ExSheet.Engine.Tests/ExcelCases/` whose source is
  `uncertain` or `documented` is put to Excel on the sign-off's Windows run (`oracle.ps1 -Update`), and
  each that disagrees is corrected, or its arguments refused with an Error Value, before the tag.
  *(2026-10-12, decided with the user: one exception, `PMT`, whose result may differ from Excel's in
  digits no format displays, by ADR-0047's note of that date. Its cases that differ are recorded as
  differences by that decision, with Excel's double beside the engine's, and it is a known limitation.)*
```

### 6b. The note's third bullet

Old:

```
- **SH-66 to SH-68 are built.** `IRR`, `XIRR` and `RATE` (ticket 13) and dragging a Reference Outline
  (ticket 31) are not in `0.1.0`, and are said as known limitations.
```

New:

```
- **SH-66 to SH-68 are built.** `IRR`, `XIRR` and `RATE` (ticket 13) and dragging a Reference Outline
  (ticket 31) are not in `0.1.0`, and are said as known limitations. *(2026-10-12: so is `NPV`, refused
  by ADR-0202's note of that date, and so are `PMT`'s last digits, kept by ADR-0047's.)*
```

### 6c. SH-7's row

Old:

```
| **SH-7** | MUST | Each function of ADR-0047's set matches a table of Excel's observed results, blanks, text and Error Values in ranges included; an unknown name is `#NAME?` (ADR-0047) | Layer 1 | every row of every table |
```

New:

```
| **SH-7** | MUST | Each function of ADR-0047's set matches a table of Excel's observed results, blanks, text and Error Values in ranges included; an unknown name is `#NAME?` (ADR-0047). *(2026-10-12, decided with the user: `PMT` but in digits no format displays, ADR-0047's exception for `PMT` alone; its rows that differ are recorded as differences by that decision.)* | Layer 1 | every row of every table |
```

## 7. The Docs Site (`samples/ExGrid.Docs`, not edited by this branch)

### 7a. The Formulas page, `Pages/Sheet/SheetFormulas.razor`

Old:

```
            <li>
                <b><code>PMT</code>, <code>PV</code> and <code>FV</code></b> give, to the last digit, every result Excel
                was seen to give for them. Which result that is depends on how Excel raises 1+rate to the number of
                periods. For <code>PMT</code> it rests on payments at rates of 0.5% to 10% a period. At much smaller
                rates Excel has not been asked, and its payment may differ from ExSheet's from the 13th significant
                digit. At a rate other than 0, a number of periods that is not a whole number, or is negative, is
                <code>#VALUE!</code> until Excel has been asked how it raises to one; so is a <code>type</code> that is
                not a whole number. Any other <code>type</code> but 0, 2 as well as 1, is payment at the start of each
                period, as Excel reads it.
            </li>
```

New:

```
            <li>
                <b><code>PV</code>, <code>FV</code> and <code>XNPV</code></b> give, to the last digit, every result Excel
                was seen to give for them, Error Values included. 1+rate is raised to the number of periods by
                squaring to a whole number, as 1 over the power to a negative one, by the square root at half a
                period, and otherwise as e<sup>n·ln(1+rate)</sup> with the exponential and the logarithm correctly
                rounded, which ExSheet computes the same on every platform. A negative 1+rate has an odd root, as
                <code>(-8)^(1/3)</code> does, and no other fractional power (<code>#NUM!</code>); at a rate of -1 no
                periods is <code>#NUM!</code> and a negative number of periods <code>#DIV/0!</code>, as Excel gave
                them. <code>PV</code> at a rate of -1 with periods is <code>#VALUE!</code> until Excel has
                been asked. <code>type</code> is read as a truth value: any number but 0, 0.5 and 2 as well as 1, is
                payment at the start of each period, as Excel reads it. <code>XNPV</code> counts the years as the days
                over 365 and sums from the first value. A rate below 0 is <code>#NUM!</code>, which is Excel's answer
                at every negative rate it was asked, -0.5 to -2, and ExSheet's own between -0.5 and 0 until Excel has
                been asked; a rate of exactly 0, or values whose last addition nearly cancels, are
                <code>#VALUE!</code> until Excel has been asked.
                <b><code>PMT</code></b> gives the exact payment, rounded once, at any number of periods, and reads
                <code>type</code> as <code>PV</code> and <code>FV</code> do. Excel's own payment can differ from it in
                digits no format displays: of the 30 payments Excel was asked, 19 differ by one to eleven units in the
                last of a double's digits, a relative difference of about 10<sup>-15</sup>. No evaluation found gives
                every payment Excel gave, and this is a known limitation of 0.1.0. At a rate of -1 with periods, where
                1+rate raised is exactly 1, and where it passes a double's range, <code>PMT</code> is
                <code>#VALUE!</code> until Excel has been asked.
                <b><code>NPV</code></b> is <code>#NAME?</code>, as <code>IRR</code> is: its last digits differ from
                Excel's, and no evaluation found gives every result Excel was seen to give.
            </li>
```

### 7b. The Roadmap, `Pages/RoadmapByHand.cs`

Old:

```
        new("What the stable version does not do is said where a reader meets it: on the product's page, and under Known limitations in its Release Notes. Among them: IRR, XIRR and RATE, which it does not support, and a Reference Outline, which cannot be dragged.",
```

New:

```
        new("What the stable version does not do is said where a reader meets it: on the product's page, and under Known limitations in its Release Notes. Among them: NPV, IRR, XIRR and RATE, which it does not support, PMT's last digits, which may differ from Excel's where no format shows them, and a Reference Outline, which cannot be dragged.",
```

The Functions page needs no edit: it reads the catalogue, where `NPV` is Blocked under ticket 13 and `PMT`'s row
says the exception.

## 8. The engine's README (`src/ExSheet.Engine/README.md`, shared)

### 8a. The table of functions

Old:

```
| `PV`, `FV` | `rate, nper, pmt, [fv or pv], [type]` |
| `NPV` | `rate, value1, [value2], ...` |
| `XNPV` | `rate, values, dates` |
```

New:

```
| `PV`, `FV` | `rate, nper, pmt, [fv or pv], [type]` |
| `XNPV` | `rate, values, dates` |
```

### 8b. "Asked of Excel before they are answered"

Only the `PMT`, `PV`, `FV` and `XNPV` clause is this area's; other areas rewrite the clauses beside it in the same
lines. Old:

```
  `k` that is not a whole number, and `LARGE` with one between 0 and 1; `PMT`, `PV` and `FV` with a
  fractional `type`, and, at a rate other than 0, with an `nper` that is not a whole number from 0 to
  2^53 (`#VALUE!`); `UNIQUE` with a blank in its array, and `SORT` and `SORTBY` with a blank or an
```

New:

```
  `k` that is not a whole number, and `LARGE` with one between 0 and 1; `PV` at a rate of -1 with an
  `nper` above 0; `PMT` at a rate of -1 with an `nper` above 0, where 1+rate raised is exactly 1, and
  where it passes a double's range; `PMT`, `PV` and `FV` with an `nper` past 2^53 at a rate other than
  0; and `XNPV` at a rate of 0 and where its last addition nearly cancels (`#VALUE!`); `UNIQUE` with a
  blank in its array, and `SORT` and `SORTBY` with a blank or an
```

### 8c. The financial bullet

Old:

```
- **`PMT`, `PV` and `FV` give every double Excel was observed to give, to the bit** (ADR-0047, the
  seventeenth run). `PV` and `FV` evaluate Microsoft's identity in doubles, raising 1+rate by squaring.
  `PMT` computes the payment at the end exactly and rounds it once, and the payment at the start
  divides that double by 1+rate, rounded once. For `PMT` that is one of several evaluations that fit
  the four payments observed, at rates of 0.5% to 10% a period. At smaller rates they part, from the
  13th significant digit at a rate of 10^-7, and PMT-016 to 018 ask Excel. A `type` that is a whole
  number other than 0 is payment at the start, as 1 is.
```

New:

```
- **`PV`, `FV` and `XNPV` give every double and Error Value Excel was observed to give, to the bit**
  (ADR-0047, the seventeenth run and the area passes of 2026-10-12). `PV` and `FV`
  evaluate Microsoft's identity in doubles, the payment times (1+rate·type)·((g−1)/rate), with g the
  financial power: squaring to a whole `nper`, 1 over the power to a negative one, the square root at
  0.5, and e^(nper ln(1+rate)) otherwise, the exponential and the logarithm correctly rounded
  (`CorrectlyRounded`, the same on every platform; Excel's exponential is the correctly rounded one at
  XNPV-013, and no observed double tells logarithms apart); a negative 1+rate has an odd root where
  1/nper is odd and is `#NUM!` otherwise, and 1+rate of 0 is `#NUM!` to the power 0 and `#DIV/0!` to a
  negative one. `XNPV` divides each value by that power of its days over 365 and sums from the first;
  it checks the rate before it evaluates and is `#NUM!` below 0, Excel's answer at the rates asked
  (-0.5 to -2) and the engine's at others. `type` is a truth value: any number other than 0, 0.5
  included, is payment at the start. **`PMT` is the exact payment rounded once, under ADR-0047's
  exception for `PMT` alone**: the payment at the end computed in double-double with 1+rate exact and
  raised exactly by the same power, and the payment at the start that double divided by 1+rate, rounded
  once. Excel's payment differs from it at 19 of the 30 observed, by 1 to 11 units in the last place,
  digits no format displays, and no evaluation searched gives every one (a Known limitation of 0.1.0).
```

### 8d. The functions that stay undeclared

Old:

```
`IRR`, `XIRR` and `RATE` remain undeclared (`#NAME?`). The solver experiment in
`spikes/exsheet-financial-solvers/` does not yet reproduce Excel's chosen roots, stopping results
and failures. The October 3 observation run did not resolve existing Supported functions' other
`uncertain` corpus cases.
```

New:

```
`IRR`, `XIRR` and `RATE` remain undeclared (`#NAME?`). The solver experiment in
`spikes/exsheet-financial-solvers/` does not yet reproduce Excel's chosen roots, stopping results
and failures. `NPV` is undeclared too (ADR-0202, note of 2026-10-12): no evaluation in
`spikes/exsheet-financial-evaluation/` gives every double Excel gave for it. The October 3
observation run did not resolve existing Supported functions' other `uncertain` corpus cases.
```

## 9. Change Notes (applied in the branch; nothing for the orchestrator to apply)

The branch's own area notes, rewritten to be true after this round: `area-financial-last-digits.md` no longer
says the exponential and the logarithm are correctly rounded "as Excel's are" (only Excel's exponential at
XNPV-013 was observed to part from glibc's); `area-financial-xnpv-rate.md` says `#NUM!` is Excel's answer at
the four rates asked and ExSheet's own between −0.5 and 0; `area-financial-type-and-periods.md` says `PMT`
answers a fractional or negative `nper` too; `area-financial-refused-where-readings-part.md` adds `PMT`'s three
refusals and narrows `XNPV`'s at a rate of 0; `area-financial-xnpv-last-digits.md` says what `XNPV` took before
against 0.1.0-beta.2, the platform's power. `area-financial-npv.md` is unchanged.

The run 17 branch's two financial notes, which the first version of this file proposed as edits, the branch
now edits itself, since this round makes both false and no other branch writes them:

- `run17-financial-periods-refused.md` is **deleted**. It said `PMT`, `PV` and `FV` became `#VALUE!` at a
  fractional or negative `nper`; after this branch all three answer it, as 0.1.0-beta.2 did, so the refusal
  reaches no released version, and the area notes say what changes against 0.1.0-beta.2.
- `run17-financial-annuity-doubles.md` says that `PMT`'s exact payment gives every payment the seventeenth run
  read and Excel's at 10^-7, and that Excel's payment differs from it in digits no format displays at 19 of the
  30 asked, a Known limitation of 0.1.0.

## 10. Tickets 04 and 05 (for the orchestrator, with the other areas' comments)

A pointer under `## Comments` of `docs/specs/exsheet-functions/issues/04-p2-ready-functions.md`:

```
2026-10-12, the area passes: `NPV` is refused for 0.1.0 (ADR-0202's note of that date) and moves to
ticket 13. `PV` and `FV` give every double and Error Value Excel was observed to give: they read any
`type` as Excel does and raise 1+rate to any `nper` by the financial power, with the exponential and
the logarithm correctly rounded (`spikes/exsheet-financial-evaluation/`, `Search.cs --refusals`); PV at
a rate of -1 with an `nper` above 0 is refused until asked (PV-013). `PMT` is kept under ADR-0047's
exception for `PMT` alone (decided with the user): the exact payment rounded once, which Excel's
differs from in digits no format displays, answered at any `nper`, and refused where its forms part in
kind until asked (PMT-034, 035 and 038).
```

A pointer under `## Comments` of `docs/specs/exsheet-functions/issues/05-p3-ready-functions.md`:

```
2026-10-12, the area passes: `XNPV` gives every double Excel was observed to give: the days over 365,
whole years by squaring and e^(years ln(1+rate)) correctly rounded otherwise, each value divided,
summed from the first (XNPV-001, 004 to 006, 011, 013 to 016), one of the two evaluations that give
them. It checks the rate before it evaluates: `#NUM!` below 0, Excel's answer at -0.5 to -2 (XNPV-007
to 010, 012, 017), which no evaluation gives, and the engine's at other negative rates (XNPV-020
asks). It refuses a rate of 0 and a last addition that nearly cancels until Excel is asked (XNPV-018,
XNPV-019; `spikes/exsheet-financial-evaluation/`).
```

Ticket 13 carries this round's comments itself (the branch rewrote its second-pass comment and added the
decision).

## 11. Found on the way, for the orchestrator

- **Excel's exponential is not glibc's.** At XNPV-013's 797 days glibc's `exp` (which .NET's `Math.Exp` calls
  on Linux) rounds the other way from the correctly rounded value, and Excel's double is the correctly
  rounded one's. That is the one argument observed where they part; no observed double tells logarithms
  apart. The engine's `^` operator, `EXP`, `LN`, `LOG`, `POWER` and the hyperbolic functions still take the
  platform's `Math.Exp` and `Math.Log`, which differ between platforms and from the correctly rounded ones at
  about 1 argument in 2,000 (18 of the 40,010 checked). `CorrectlyRounded` is in the engine; whether the
  arithmetic area takes it is that area's question (ARITH-098's and ARITH-134's doubles are the correctly
  rounded functions' too). PV's and FV's power and the `^` operator now differ at such arguments.
- **The order of a domain check and an Error Value in a range is unobserved.** `XNPV` reads its values and
  dates before it checks the rate, so at a rate of 0 or below with an Error Value among them it gives that
  Error Value; an Excel that checked the rate first would give `#NUM!`. No case asks it, and the same order
  question stands for every function with a check of an argument beside a range; it is left as it was.
- **`Search.cs` hung in Debug** after the second pass: FV-017's rate of −9 made Goldberg's log1p NaN, and
  reading a NaN's bits as a dyadic number fed the fixed-point `exp` an argument of 2^1000. `X.Of` now refuses a
  non-finite double. The spike's runs are `-c Release` (half a minute; Debug took over 15 minutes).
- **Excel gives a financial Formula's General cell its currency format on entry** (the second pass read
  `$#,##0.00_);[Red]($#,##0.00)` on every PMT, PV and FV cell). Following it is a cell-format question for
  another ticket.
- The first round's `oracle.ps1` proposal (G17 for Excel's doubles) is applied (`afd70b15`).
